using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using MasterPlayer.Settings;
using PromeRotation.Helpers;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Radar;

/// <summary>
/// 雷达悬浮窗：圆形 HUD 式 2D 俯视小地图（原生 ImGui 窗口，UiBuilder.Draw 主线程驱动）。
/// 游戏内 3D 范围提示由 RadarRenderer（真实世界绘制）负责，本窗口才是「雷达」。
/// 所有图形内容经 RadarCanvas 裁剪到屏幕圆盘内；世界轮廓经 PR GeometryHelper.WorldToLocal
/// 转为雷达局部坐标（右为 +X，前为 -Y）。
/// 交互：解锁状态下拖拽移动、滚轮缩放、右键快捷菜单；锁定后鼠标穿透。
/// </summary>
public sealed class RadarOverlayWindow : IDisposable
{
    private const float ScreenHalfSize = 150f;
    private const float ScreenMargin = 20f;
    private const float DefaultVisibleRadius = 25f;
    private const float BoundaryPadding = 3f;

    private static readonly Vector4 DiscColor = new(0.045f, 0.065f, 0.115f, 1f);
    private static readonly Vector4 DiscCenterTint = new(1f, 1f, 1f, 0.035f);
    private static readonly Vector4 RimColor = new(0.85f, 0.92f, 1f, 0.50f);
    private static readonly Vector4 RimGlowColor = new(0.35f, 0.75f, 1f, 0.10f);
    private static readonly Vector4 RingColor = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 TickColor = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 BoundaryColor = new(1f, 1f, 1f, 0.80f);
    private static readonly Vector4 PathColor = new(0.30f, 0.95f, 1f, 0.85f);
    private static readonly Vector4 SelfColor = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 SelfAccentColor = new(0.30f, 0.85f, 1f, 0.85f);
    private static readonly Vector4 SelfHaloColor = new(0.30f, 0.85f, 1f, 0.16f);
    private static readonly Vector4 PartyColor = new(0.40f, 0.65f, 1f, 1f);
    private static readonly Vector4 EnemyColor = new(1f, 0.28f, 0.24f, 1f);
    private static readonly Vector4 MarkerOutlineColor = new(0f, 0f, 0f, 0.65f);
    private static readonly Vector4 TargetRingColor = new(1f, 1f, 1f, 0.85f);
    private static readonly Vector4 CompassNorthColor = new(1f, 0.82f, 0.35f, 0.95f);
    private static readonly Vector4 CompassColor = new(1f, 1f, 1f, 0.50f);
    private static readonly Vector4 CompassShadowColor = new(0f, 0f, 0f, 0.60f);

    // FFXIV 世界方位：X 西→东，Z 北→南（北为 -Z）。
    private static readonly (string Label, Vector3 Direction, bool IsNorth)[] Cardinals =
    [
        ("N", new Vector3(0f, 0f, -1f), true),
        ("E", new Vector3(1f, 0f, 0f), false),
        ("S", new Vector3(0f, 0f, 1f), false),
        ("W", new Vector3(-1f, 0f, 0f), false),
    ];

    private Vector2 _screenCenter;
    private Vector3 _center;
    private RadarProjection _projection;
    private bool _disposed;
    private bool _hovered;
    private DateTime _lastErrorLogUtc;
    private DateTime? _pendingSaveUtc;

    private readonly List<Vector2> _projectedA = new(64);
    private readonly List<Vector2> _projectedB = new(64);
    private readonly Vector2[] _quad = new Vector2[4];
    private readonly List<Vector2> _pathPoints = new(32);
    private readonly List<float> _pathLengths = new(32);

    public void Draw()
    {
        var settings = MasterPlayerSettings.Instance;
        if (_disposed || !settings.EnableRadarOverlay ||
            (settings.RadarOnlyWithScript && !HasZoneScript()))
        {
            return;
        }

        try
        {
            DrawInternal();
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastErrorLogUtc).TotalSeconds >= 5d)
            {
                _lastErrorLogUtc = now;
                Svc.Log.Error(ex, "[MasterPlayer] RadarOverlayWindow.Draw 失败。");
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        if (_pendingSaveUtc.HasValue)
        {
            _pendingSaveUtc = null;
            try
            {
                MasterPlayerSettings.Instance.Save();
            }
            catch
            {
            }
        }
    }

    private void DrawInternal()
    {
        var settings = MasterPlayerSettings.Instance;
        var plugin = PromeRotation.Plugin.Instance;
        var player = Svc.Objects.LocalPlayer;
        var now = DateTime.UtcNow;
        var animT = (float)(now.TimeOfDay.TotalSeconds % 60.0);
        var snapshot = plugin.SafeZoneManager.BuildSnapshot(now);

        float visibleRadius;
        if (settings.RadarCenterMode == 0 && snapshot.ArenaBoundary is { } boundary)
        {
            _center = boundary.Position;
            visibleRadius = ComputeArenaRadius(boundary) + BoundaryPadding;
        }
        else
        {
            if (player == null)
            {
                return;
            }

            _center = player.Position;
            visibleRadius = DefaultVisibleRadius;
        }

        var rotation = settings.RadarRotateWithCamera ? GetCameraAzimuth() : RadarProjection.NorthUpRotation;
        var scale = ScreenHalfSize / MathF.Max(visibleRadius, 1f) * MathF.Max(settings.RadarScale, 0.05f);

        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysAutoResize;
        if (settings.RadarOverlayLock)
        {
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;
        }

        var fullSize = 2f * (ScreenMargin + ScreenHalfSize);
        ImGui.SetNextWindowSize(new Vector2(fullSize), ImGuiCond.FirstUseEver);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        if (!ImGui.Begin("MasterPlayer 雷达", flags))
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        _screenCenter = cursor + new Vector2(ScreenMargin + ScreenHalfSize);
        _projection = new RadarProjection(_center, _screenCenter, scale, rotation);

        ImGui.InvisibleButton("##radar_hit", new Vector2(fullSize));
        HandleInteraction(settings, !settings.RadarOverlayLock);

        var canvas = new RadarCanvas(drawList, _screenCenter, ScreenHalfSize);
        DrawDisc(canvas, settings);
        DrawRangeRings(canvas, settings, scale);
        DrawRimTicks(canvas);
        DrawAreas(canvas, snapshot, settings, now, animT);
        DrawSolutionPath(canvas, plugin, animT);
        DrawActors(canvas, player, visibleRadius, animT);
        DrawRim(canvas);
        DrawCompass(drawList, canvas, settings);

        ImGui.End();
        ImGui.PopStyleVar(2);

        if (_pendingSaveUtc is DateTime pending && now >= pending)
        {
            _pendingSaveUtc = null;
            settings.Save();
        }
    }

    private void HandleInteraction(MasterPlayerSettings settings, bool interactive)
    {
        _hovered = interactive && ImGui.IsItemHovered();
        if (!interactive)
        {
            return;
        }

        if (_hovered)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                settings.RadarScale = Math.Clamp(settings.RadarScale + wheel * 0.1f, 0.3f, 3f);
                _pendingSaveUtc = DateTime.UtcNow.AddSeconds(0.8);
            }
        }

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        }

        if (ImGui.BeginPopupContextItem("##radar_menu"))
        {
            var changed = false;
            var lockOverlay = settings.RadarOverlayLock;
            if (ImGui.Checkbox("锁定位置（鼠标穿透）", ref lockOverlay))
            {
                settings.RadarOverlayLock = lockOverlay;
                changed = true;
            }

            var onlyWithScript = settings.RadarOnlyWithScript;
            if (ImGui.Checkbox("仅在有脚本的区域显示", ref onlyWithScript))
            {
                settings.RadarOnlyWithScript = onlyWithScript;
                changed = true;
            }

            var opacity = settings.RadarBackgroundOpacity;
            if (ImGui.SliderFloat("背景不透明度", ref opacity, 0f, 1f))
            {
                settings.RadarBackgroundOpacity = opacity;
                changed = true;
            }

            var compass = settings.RadarShowCompass;
            if (ImGui.Checkbox("显示罗盘", ref compass))
            {
                settings.RadarShowCompass = compass;
                changed = true;
            }

            var rings = settings.RadarShowRangeRings;
            if (ImGui.Checkbox("显示距离环", ref rings))
            {
                settings.RadarShowRangeRings = rings;
                changed = true;
            }

            var pulse = settings.RadarPulseDanger;
            if (ImGui.Checkbox("危险区临期脉冲", ref pulse))
            {
                settings.RadarPulseDanger = pulse;
                changed = true;
            }

            ImGui.Separator();
            if (ImGui.MenuItem("隐藏雷达悬浮窗"))
            {
                settings.EnableRadarOverlay = false;
                changed = true;
            }

            if (changed)
            {
                settings.Save();
            }

            ImGui.EndPopup();
        }
    }

    private void DrawDisc(RadarCanvas canvas, MasterPlayerSettings settings)
    {
        canvas.StrokeCircle(canvas.Center, canvas.Radius + 4.5f, ImGui.GetColorU32(RimGlowColor), 8f, 96);

        var opacity = Math.Clamp(settings.RadarBackgroundOpacity, 0f, 1f);
        if (opacity <= 0.01f)
        {
            return;
        }

        var disc = DiscColor;
        disc.W = opacity;
        canvas.FillCircle(canvas.Center, canvas.Radius, ImGui.GetColorU32(disc), 96);

        var tint = DiscCenterTint;
        tint.W *= opacity;
        canvas.FillCircle(canvas.Center, canvas.Radius * 0.55f, ImGui.GetColorU32(tint), 64);
    }

    private void DrawRangeRings(RadarCanvas canvas, MasterPlayerSettings settings, float scale)
    {
        if (!settings.RadarShowRangeRings || settings.RadarBackgroundOpacity <= 0.01f)
        {
            return;
        }

        var effectiveVisibleRadius = canvas.Radius / MathF.Max(scale, 0.001f);
        var step = PickRingStep(effectiveVisibleRadius);
        var color = ImGui.GetColorU32(RingColor);
        for (var d = step; d * scale < canvas.Radius - 4f; d += step)
        {
            canvas.StrokeCircle(canvas.Center, d * scale, color, 1f, 64);
        }
    }

    private static float PickRingStep(float visibleRadius)
    {
        ReadOnlySpan<float> candidates = [2f, 5f, 10f, 20f, 40f];
        foreach (var candidate in candidates)
        {
            if (visibleRadius / candidate <= 4f)
            {
                return candidate;
            }
        }

        return 40f;
    }

    private void DrawRimTicks(RadarCanvas canvas)
    {
        var color = ImGui.GetColorU32(TickColor);
        foreach (var (_, direction, isNorth) in Cardinals)
        {
            if (isNorth)
            {
                continue;
            }

            var dir = ScreenDirection(direction);
            canvas.StrokeSegment(
                canvas.Center + dir * (canvas.Radius - 7f),
                canvas.Center + dir * (canvas.Radius - 2f),
                color,
                1.5f);
        }
    }

    private void DrawAreas(RadarCanvas canvas, SafeZoneSnapshot snapshot, MasterPlayerSettings settings, DateTime now, float animT)
    {
        var appearance = settings.RadarAoe;
        var visibility = MasterPlayerPlugin.Instance?.AoeVisibility;

        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (!appearance.IsVisible(obstacle.Geometry, false, visibility?.GetVisibility(obstacle.Handle).Radar ?? true))
                continue;
            var style = appearance.GetDangerStyle();
            FillGeometry(canvas, obstacle.Geometry, style.FillColor!.Value, style.StrokeColor!.Value, 1f);
        }

        foreach (var goal in snapshot.GoalAreas)
        {
            if (!appearance.IsVisible(goal.Geometry, true, visibility?.GetVisibility(goal.Handle).Radar ?? true))
                continue;
            var style = appearance.GetSafeStyle();
            FillGeometry(canvas, goal.Geometry, style.FillColor!.Value, style.StrokeColor!.Value, 1f);
        }

        foreach (var danger in snapshot.DangerAreas)
        {
            if (!appearance.IsVisible(danger.Geometry, false, visibility?.GetVisibility(danger.Handle).Radar ?? true))
                continue;
            var progress = GetTelegraphProgress(danger, now);
            var style = appearance.GetDangerStyle(progress);
            var pulse = 1f;
            if (settings.RadarPulseDanger && progress >= 0.7f && progress < 1f)
            {
                pulse = 0.55f + 0.45f * MathF.Sin(animT * 2f * MathF.PI * 2.4f);
            }

            FillGeometry(canvas, danger.Geometry, style.FillColor!.Value, style.StrokeColor!.Value, pulse);
        }

        if (visibility != null)
            foreach (var preview in visibility.GetPreviews(now))
            {
                if (!appearance.IsVisible(preview.Geometry, preview.Safe, preview.Visibility.Radar))
                    continue;
                var style = preview.Safe ? appearance.GetSafeStyle() : appearance.GetDangerStyle();
                FillGeometry(canvas, preview.Geometry, style.FillColor!.Value, style.StrokeColor!.Value, 1f);
            }

        DrawBoundary(canvas, snapshot);
    }

    private void DrawBoundary(RadarCanvas canvas, SafeZoneSnapshot snapshot)
    {
        if (snapshot.ArenaBoundary is not { } boundary)
        {
            return;
        }

        foreach (var contour in boundary.GetContours())
        {
            ProjectInto(contour, _projectedA);
            canvas.StrokeContourGlow(_projectedA, BoundaryColor, 1.5f);
        }
    }

    private void FillGeometry(RadarCanvas canvas, IGeometryShape geometry, Vector4 fill, Vector4 stroke, float pulse)
    {
        var contours = geometry.GetContours();
        if (contours.Count == 0)
        {
            return;
        }

        var fillColor = ImGui.GetColorU32(fill);
        if (geometry is DonutGeometry && contours.Count >= 2 &&
            contours[0].Count == contours[1].Count && contours[0].Count >= 3)
        {
            ProjectInto(contours[0], _projectedA);
            ProjectInto(contours[1], _projectedB);
            for (var i = 0; i < _projectedA.Count; i++)
            {
                var next = (i + 1) % _projectedA.Count;
                _quad[0] = _projectedA[i];
                _quad[1] = _projectedA[next];
                _quad[2] = _projectedB[next];
                _quad[3] = _projectedB[i];
                canvas.FillPolygon(_quad, fillColor);
            }
        }
        else
        {
            foreach (var contour in contours)
            {
                ProjectInto(contour, _projectedA);
                canvas.FillPolygon(_projectedA, fillColor);
            }
        }

        stroke.W *= pulse;
        var thickness = pulse < 1f ? 1.8f : 1.1f;
        foreach (var contour in contours)
        {
            ProjectInto(contour, _projectedA);
            canvas.StrokeContourGlow(_projectedA, stroke, thickness);
        }
    }

    private void DrawSolutionPath(RadarCanvas canvas, PromeRotation.Plugin plugin, float animT)
    {
        var path = plugin.SafeZoneMovementStatus.LatestSolution?.ExecutablePath;
        if (path is null || path.Count == 0)
        {
            return;
        }

        _pathPoints.Clear();
        _pathLengths.Clear();
        foreach (var point in path)
        {
            _pathPoints.Add(_projection.Project(point.Position));
        }

        var total = 0f;
        for (var i = 1; i < _pathPoints.Count; i++)
        {
            _pathLengths.Add(total);
            total += Vector2.Distance(_pathPoints[i - 1], _pathPoints[i]);
        }

        const float dash = 6f;
        const float period = 10.5f;
        var offset = PositiveModulo(animT * 16f, period);
        var totalSafe = MathF.Max(total, 1f);

        for (var i = 1; i < _pathPoints.Count; i++)
        {
            var a = _pathPoints[i - 1];
            var b = _pathPoints[i];
            var segLen = Vector2.Distance(a, b);
            if (segLen < 0.01f)
            {
                continue;
            }

            var segStart = _pathLengths[i - 1];
            var segEnd = segStart + segLen;
            var s = segStart;
            while (s < segEnd)
            {
                var phase = PositiveModulo(s - offset, period);
                if (phase >= dash)
                {
                    s += period - phase;
                    continue;
                }

                var dashEnd = MathF.Min(s + (dash - phase), segEnd);
                var color = PathColor;
                color.W *= 0.45f + 0.55f * (s / totalSafe);
                canvas.StrokeSegment(
                    Vector2.Lerp(a, b, (s - segStart) / segLen),
                    Vector2.Lerp(a, b, (dashEnd - segStart) / segLen),
                    ImGui.GetColorU32(color),
                    2f);
                s = dashEnd;
            }
        }

        var destination = _pathPoints[^1];
        if (!canvas.Contains(destination))
        {
            return;
        }

        _quad[0] = destination + new Vector2(0f, -5f);
        _quad[1] = destination + new Vector2(5f, 0f);
        _quad[2] = destination + new Vector2(0f, 5f);
        _quad[3] = destination + new Vector2(-5f, 0f);
        canvas.FillPolygon(_quad, ImGui.GetColorU32(PathColor));

        var ping = PositiveModulo(animT * 1.5f, 1f);
        var ring = PathColor;
        ring.W *= (1f - ping) * 0.7f;
        canvas.StrokeCircle(destination, 4f + 5f * ping, ImGui.GetColorU32(ring), 1.5f, 32);
    }

    private void DrawActors(RadarCanvas canvas, IPlayerCharacter? player, float visibleRadius, float animT)
    {
        var maxDistSq = visibleRadius * visibleRadius;
        var targetId = Svc.Targets.Target?.EntityId;
        var outlineColor = ImGui.GetColorU32(MarkerOutlineColor);

        try
        {
            foreach (var member in PromeRotation.Helpers.PartyHelper.GetParty())
            {
                if (member.IsDead || (player != null && member.EntityId == player.EntityId))
                {
                    continue;
                }

                if (WithinRange(member.Position, maxDistSq))
                {
                    var pos = _projection.Project(member.Position);
                    canvas.FillCircle(pos, 3f, ImGui.GetColorU32(PartyColor));
                    canvas.StrokeCircle(pos, 3f, outlineColor, 1.5f);
                }
            }
        }
        catch
        {
        }

        for (var i = 0; i < Svc.Objects.Length; i++)
        {
            if (Svc.Objects[i] is not IBattleNpc { IsDead: false, IsTargetable: true } npc)
            {
                continue;
            }

            if (!npc.StatusFlags.HasFlag(StatusFlags.Hostile) || !WithinRange(npc.Position, maxDistSq))
            {
                continue;
            }

            var pos = _projection.Project(npc.Position);
            canvas.FillCircle(pos, 3.5f, ImGui.GetColorU32(EnemyColor));
            canvas.StrokeCircle(pos, 3.5f, outlineColor, 1.5f);

            if (targetId == npc.EntityId)
            {
                var ring = TargetRingColor;
                ring.W *= 0.65f + 0.35f * MathF.Sin(animT * 2f * MathF.PI * 2.4f);
                canvas.StrokeCircle(pos, 6.5f, ImGui.GetColorU32(ring), 1.5f, 24);
            }
        }

        if (player != null)
        {
            DrawSelfMarker(canvas, player);
        }
    }

    private void DrawSelfMarker(RadarCanvas canvas, IPlayerCharacter player)
    {
        var selfPos = _projection.Project(player.Position);
        var forward = GeometryHelper.PointFromRotationDistanceXZ(player.Position, player.Rotation, 1f);
        var dir = _projection.Project(forward) - selfPos;
        dir = dir.LengthSquared() <= float.Epsilon ? new Vector2(0f, -1f) : Vector2.Normalize(dir);
        var ortho = new Vector2(dir.Y, -dir.X);

        if (canvas.Contains(selfPos))
        {
            canvas.FillCircle(selfPos, 8.5f, ImGui.GetColorU32(SelfHaloColor), 24);
        }

        _quad[0] = selfPos + dir * 9.5f;
        _quad[1] = selfPos - dir * 5.5f + ortho * 6f;
        _quad[2] = selfPos - dir * 5.5f - ortho * 6f;
        canvas.FillPolygon(new ArraySegment<Vector2>(_quad, 0, 3), ImGui.GetColorU32(SelfAccentColor));

        _quad[0] = selfPos + dir * 7f;
        _quad[1] = selfPos - dir * 4f + ortho * 4.2f;
        _quad[2] = selfPos - dir * 4f - ortho * 4.2f;
        canvas.FillPolygon(new ArraySegment<Vector2>(_quad, 0, 3), ImGui.GetColorU32(SelfColor));
    }

    private void DrawRim(RadarCanvas canvas)
    {
        var rim = RimColor;
        if (_hovered)
        {
            rim.W = MathF.Min(1f, rim.W + 0.3f);
        }

        canvas.StrokeCircle(canvas.Center, canvas.Radius - 0.75f, ImGui.GetColorU32(rim), 1.5f, 96);
    }

    private void DrawCompass(ImDrawListPtr drawList, RadarCanvas canvas, MasterPlayerSettings settings)
    {
        if (!settings.RadarShowCompass)
        {
            return;
        }

        var shadow = ImGui.GetColorU32(CompassShadowColor);
        foreach (var (label, direction, isNorth) in Cardinals)
        {
            var dir = ScreenDirection(direction);
            var pos = canvas.Center + dir * (canvas.Radius - 13f);
            var size = ImGui.CalcTextSize(label);
            var topLeft = pos - size * 0.5f;
            drawList.AddText(topLeft + new Vector2(1f, 1f), shadow, label);
            drawList.AddText(topLeft, ImGui.GetColorU32(isNorth ? CompassNorthColor : CompassColor), label);
        }
    }

    private static bool HasZoneScript()
    {
        var scripts = MasterPlayerPlugin.Instance?.Scripts;
        if (scripts == null)
        {
            return false;
        }

        foreach (var running in scripts.Running.Values)
        {
            if (running.Descriptor.Metadata.Territorys.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static float ComputeArenaRadius(IGeometryShape boundary)
    {
        var radius = 1f;
        foreach (var contour in boundary.GetContours())
        {
            foreach (var point in contour)
            {
                radius = MathF.Max(radius, GeometryHelper.DistanceXZ(point, boundary.Position));
            }
        }

        return radius;
    }

    private Vector2 ScreenDirection(Vector3 worldDirection)
    {
        var projected = _projection.Project(_center + worldDirection) - _screenCenter;
        return projected.LengthSquared() <= 1e-6f ? new Vector2(0f, -1f) : Vector2.Normalize(projected);
    }

    private void ProjectInto(IReadOnlyList<Vector3> world, List<Vector2> target)
    {
        target.Clear();
        foreach (var point in world)
        {
            target.Add(_projection.Project(point));
        }
    }

    private static float GetTelegraphProgress(DangerAreaSnapshot danger, DateTime now)
    {
        var progress = 1f;
        if (danger.DangerWindows.Count > 0)
        {
            progress = 0f;
            foreach (var window in danger.DangerWindows)
                progress = MathF.Max(progress, window.TelegraphProgressAt(now));
        }

        return progress;
    }

    private static float PositiveModulo(float value, float modulus)
        => ((value % modulus) + modulus) % modulus;

    private bool WithinRange(Vector3 position, float maxDistSq)
        => GeometryHelper.DistanceSquaredXZ(position, _center) <= maxDistSq;

    private static unsafe float GetCameraAzimuth()
    {
        try
        {
            var manager = CameraManager.Instance();
            if (manager == null)
            {
                return RadarProjection.NorthUpRotation;
            }

            var camera = manager->GetActiveCamera();
            if (camera == null)
            {
                return RadarProjection.NorthUpRotation;
            }

            return camera->DirH + MathF.PI;
        }
        catch
        {
            return RadarProjection.NorthUpRotation;
        }
    }
}

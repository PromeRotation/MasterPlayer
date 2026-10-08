using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using MasterPlayer.Settings;
using MasterPlayer.Mechanics.Crucible;
using PromeRotation.Helpers;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Radar;

/// <summary>
/// 可嵌入雷达视图：按实际场地轮廓在给定屏幕视口内绘制 BMR 式 2D 俯视内容
/// （场地底图、距离环、场地边界、危险/安全区、解算路径、队友/敌人/自身、罗盘）。
/// 数据来源为 SafeZoneRebornManager 快照与 AoeVisibilityRegistry，外观遵循 RadarAoe 设置。
/// 供 RadarOverlayWindow（悬浮窗）与 ReplayWindow（回放窗口内嵌）共用。
/// </summary>
public sealed class RadarView
{
    private const float DefaultVisibleRadius = 25f;

    private static readonly Vector4 DiscColor = new(0.045f, 0.065f, 0.115f, 1f);
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
    private bool _hovered;
    private float _visibleRadius;

    private readonly List<Vector2> _projectedA = new(64);
    private readonly List<Vector2> _projectedB = new(64);
    private readonly Vector2[] _quad = new Vector2[4];
    private readonly List<Vector2> _pathPoints = new(32);
    private readonly List<RadarPathDash> _pathDashes = new(64);

    /// <summary>在指定屏幕圆心/半径绘制一帧雷达内容；玩家不存在且无场地边界时返回 false（未绘制）。</summary>
    public bool Draw(ImDrawListPtr drawList, Vector2 screenCenter, float radiusPx, bool hovered = false)
    {
        var settings = MasterPlayerSettings.Instance;
        var plugin = PromeRotation.Plugin.Instance;
        var player = Svc.Objects.LocalPlayer;
        var world = MasterPlayerPlugin.Instance?.Replay?.World;
        var now = MasterPlayerPlugin.Instance!.SafeZone.CurrentTime;
        var animT = (float)(now.TimeOfDay.TotalSeconds % 60.0);
        var snapshot = MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(now);

        var rotation = settings.RadarRotateWithCamera ? GetCameraAzimuth() : RadarProjection.NorthUpRotation;
        _screenCenter = screenCenter;
        _hovered = hovered;
        float visibleRadius;
        IReadOnlyList<Vector2>? arenaOutline = null;
        if (settings.RadarCenterMode == 0 && snapshot.ArenaBoundary is { } boundary && boundary is not ILocalViewBoundary)
        {
            var display = boundary;
            _center = boundary.Position;
            if (MasterPlayerPlugin.Instance?.Scripts is { } scripts)
                foreach (var running in scripts.Running.Values)
                    if (running.Script is CrucibleScript crucible && crucible.GetRadarArena(boundary) is { } arena)
                    {
                        display = arena.Boundary;
                        _center = arena.Center;
                        break;
                    }
            var layout = RadarArenaLayout.Create(display, _center, _screenCenter, radiusPx, rotation);
            _projection = layout.Projection;
            arenaOutline = layout.Outline.Count >= 3 ? layout.Outline : null;
            visibleRadius = layout.WorldRadius;
        }
        else
        {
            var position = world != null ? world.LocalPlayer?.Position : player?.Position;
            if (position == null)
            {
                return false;
            }

            _center = position.Value;
            visibleRadius = (snapshot.ArenaBoundary as ILocalViewBoundary)?.ViewRadius ?? DefaultVisibleRadius;
            _projection = new(_center, _screenCenter,
                radiusPx / visibleRadius, rotation);
        }

        _visibleRadius = visibleRadius;

        var canvas = new RadarCanvas(drawList, _screenCenter, radiusPx, arenaOutline);
        DrawDisc(canvas, settings);
        DrawRangeRings(canvas, settings, _projection.Scale);
        DrawRimTicks(canvas);
        DrawAreas(canvas, snapshot, settings, now, animT);
        DrawMechanicExtras(drawList, canvas, settings);
        DrawSolutionPath(canvas, plugin, animT);
        if (world != null) DrawReplayActors(canvas, world, visibleRadius, animT);
        else DrawActors(canvas, player, visibleRadius, animT);
        DrawRim(canvas);
        DrawCompass(drawList, canvas, settings);
        return true;
    }

    private void DrawDisc(RadarCanvas canvas, MasterPlayerSettings settings)
    {
        canvas.StrokeRim(ImGui.GetColorU32(RimGlowColor), 8f);

        var opacity = Math.Clamp(settings.RadarBackgroundOpacity, 0f, 1f);
        if (opacity <= 0.01f)
        {
            return;
        }

        var disc = DiscColor;
        disc.W = opacity;
        canvas.FillBackground(ImGui.GetColorU32(disc));
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
                canvas.RimPoint(dir) - dir * 7f,
                canvas.RimPoint(dir) - dir * 2f,
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
            if (visibility?.IsNavigationOnly(danger.Handle) == true) continue;
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
                var style = preview.Style(appearance);
                FillGeometry(canvas, preview.Geometry, style.FillColor!.Value, style.StrokeColor!.Value, 1f);
            }

        if (visibility != null)
            foreach (var line in visibility.GetLines())
            {
                if (!line.Visibility.Radar || !(line.Safe ? appearance.ShowSafe : appearance.ShowDanger)) continue;
                var style = line.Safe ? appearance.GetSafeStyle() : appearance.GetDangerStyle();
                canvas.StrokeSegment(_projection.Project(line.Start), _projection.Project(line.End), ImGui.GetColorU32(style.StrokeColor!.Value), style.StrokeThickness);
            }

        DrawBoundary(canvas, snapshot);
    }

    private void DrawMechanicExtras(ImDrawListPtr drawList, RadarCanvas canvas, MasterPlayerSettings settings)
    {
        var bridge = MasterPlayerPlugin.Instance?.MechanicHost;
        var extras = bridge?.DrawExtras;
        if (extras == null || extras.Count == 0 || bridge!.DrawExtrasKey is not { } key)
            return;
        if (MasterPlayerPlugin.Instance?.AoeVisibility?.FindRule(key)?.Radar == false)
            return;

        var dangerColor = ImGui.GetColorU32(settings.RadarAoe.GetDangerStyle().StrokeColor!.Value);
        var safeColor = ImGui.GetColorU32(settings.RadarAoe.GetSafeStyle().StrokeColor!.Value);
        var shadow = ImGui.GetColorU32(CompassShadowColor);
        foreach (var extra in extras)
        {
            var pos = _projection.Project(extra.Position);
            switch (extra.Kind)
            {
                case MechanicHost.MechanicRadarExtraKind.Eye:
                    DrawEyeMarker(drawList, pos, extra.Danger ? dangerColor : safeColor);
                    break;
                case MechanicHost.MechanicRadarExtraKind.Text when extra.Text is { Length: > 0 } text:
                    var color = extra.Color != 0 ? extra.Color : ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
                    var size = ImGui.CalcTextSize(text);
                    var topLeft = pos - size * 0.5f;
                    drawList.AddText(topLeft + new Vector2(1f, 1f), shadow, text);
                    drawList.AddText(topLeft, color, text);
                    break;
                case MechanicHost.MechanicRadarExtraKind.Sign when extra.Text is { Length: > 0 } label:
                    var labelSize = ImGui.CalcTextSize(label);
                    var labelPos = pos - labelSize * 0.5f;
                    drawList.AddText(labelPos + new Vector2(1f, 1f), shadow, label);
                    drawList.AddText(labelPos, ImGui.GetColorU32(CompassNorthColor), label);
                    break;
            }
        }
    }

    private static void DrawEyeMarker(ImDrawListPtr drawList, Vector2 center, uint color)
    {
        const float scale = 0.55f;
        const float outerR = 11.3333f * scale;
        const float offsetV = 5.3333f * scale;
        const float halfAngle = 1.080839f;
        var offset = new Vector2(0f, offsetV);
        drawList.PathArcTo(center - offset, outerR, MathF.PI / 2f + halfAngle, MathF.PI / 2f - halfAngle);
        drawList.PathArcTo(center + offset, outerR, -(MathF.PI / 2f + halfAngle), -(MathF.PI / 2f - halfAngle));
        drawList.PathFillConvex(color);
        drawList.AddCircleFilled(center, 4f * scale, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));
    }

    private void DrawBoundary(RadarCanvas canvas, SafeZoneSnapshot snapshot)
    {
        if (snapshot.ArenaBoundary is not { } boundary)
        {
            return;
        }

        if (boundary is ILocalViewBoundary)
        {
            foreach (var (a, b) in RadarLocalGeometry.Segments(boundary, _center, _visibleRadius))
                canvas.StrokeSegment(_projection.Project(a), _projection.Project(b), ImGui.GetColorU32(BoundaryColor), 1.5f);
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
        if (!RadarLocalGeometry.Intersects(geometry.GetBounds(), _center, _visibleRadius)) return;
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
        if (geometry is FrozenConstraintGeometry grid)
        {
            // Grid rows are fill pieces, not separate arenas: omit their internal shared edges.
            foreach (var (start, end) in grid.GetBoundarySegments())
                canvas.StrokeSegment(_projection.Project(start), _projection.Project(end), ImGui.GetColorU32(stroke), thickness);
            return;
        }
        foreach (var contour in contours)
        {
            ProjectInto(contour, _projectedA);
            canvas.StrokeContourGlow(_projectedA, stroke, thickness);
        }
    }

    private void DrawSolutionPath(RadarCanvas canvas, PromeRotation.Plugin plugin, float animT)
    {
        var path = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus.LatestSolution?.ExecutablePath;
        if (path is null || path.Count == 0)
        {
            return;
        }

        _pathPoints.Clear();
        foreach (var point in path)
        {
            _pathPoints.Add(_projection.Project(point.Position));
        }

        if (!RadarPathDashes.TryBuild(_pathPoints, animT, _pathDashes)) return;
        foreach (var dash in _pathDashes)
        {
            var color = PathColor;
            color.W *= 0.45f + 0.55f * dash.Progress;
            canvas.StrokeSegment(dash.Start, dash.End, ImGui.GetColorU32(color), 2f);
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
        => DrawSelfMarker(canvas, player.Position, player.Rotation);

    private void DrawReplayActors(RadarCanvas canvas, Replay.ReplayWorldState world, float radius, float animT)
    {
        var party = world.Party.Select(p => p.EntityId).ToHashSet();
        foreach (var actor in world.Actors.Values)
        {
            if (actor.Id == world.LocalPlayerId || actor.Dead || !WithinRange(actor.Position, radius * radius)) continue;
            var isParty = party.Contains(actor.Id);
            if (!isParty && (!actor.Hostile || !actor.Targetable)) continue;
            var pos = _projection.Project(actor.Position);
            var size = isParty ? 3f : 3.5f;
            canvas.FillCircle(pos, size, ImGui.GetColorU32(isParty ? PartyColor : EnemyColor));
            canvas.StrokeCircle(pos, size, ImGui.GetColorU32(MarkerOutlineColor), 1.5f);
            if (world.TargetId == actor.Id)
            {
                var ring = TargetRingColor;
                ring.W *= 0.65f + 0.35f * MathF.Sin(animT * 2f * MathF.PI * 2.4f);
                canvas.StrokeCircle(pos, 6.5f, ImGui.GetColorU32(ring), 1.5f, 24);
            }
        }
        if (world.LocalPlayer is { } player) DrawSelfMarker(canvas, player.Position, player.Rotation);
    }

    private void DrawSelfMarker(RadarCanvas canvas, Vector3 position, float rotation)
    {
        var selfPos = _projection.Project(position);
        var forward = GeometryHelper.PointFromRotationDistanceXZ(position, rotation, 1f);
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

        canvas.StrokeRim(ImGui.GetColorU32(rim), 1.5f);
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
            var pos = canvas.RimPoint(dir) + dir * 11f;
            var size = ImGui.CalcTextSize(label);
            var topLeft = pos - size * 0.5f;
            drawList.AddText(topLeft + new Vector2(1f, 1f), shadow, label);
            drawList.AddText(topLeft, ImGui.GetColorU32(isNorth ? CompassNorthColor : CompassColor), label);
        }
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

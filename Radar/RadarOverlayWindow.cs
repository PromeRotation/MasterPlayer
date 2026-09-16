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
/// 雷达悬浮窗：BMR MiniArena 式 2D 俯视小地图（原生 ImGui 窗口，UiBuilder.Draw 主线程驱动）。
/// 游戏内 3D 范围提示由 RadarRenderer（真实世界绘制）负责，本窗口才是「雷达」。
/// 世界轮廓经 PR GeometryHelper.WorldToLocal 转为雷达局部坐标（右为 +X，前为 -Y）。
/// </summary>
public sealed class RadarOverlayWindow : IDisposable
{
    private const float ScreenHalfSize = 150f;
    private const float ScreenMargin = 20f;
    private const float DefaultVisibleRadius = 25f;
    private const float BoundaryPadding = 3f;

    private static readonly Vector4 BoundaryColor = new(1f, 1f, 1f, 0.9f);
    private static readonly Vector4 FallbackCircleColor = new(1f, 1f, 1f, 0.35f);
    private static readonly Vector4 PathColor = new(0f, 1f, 1f, 0.6f);
    private static readonly Vector4 SelfColor = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 PartyColor = new(0.35f, 0.6f, 1f, 1f);
    private static readonly Vector4 EnemyColor = new(1f, 0.25f, 0.25f, 1f);

    private Vector2 _screenCenter;
    private Vector3 _center;
    private RadarProjection _projection;
    private bool _disposed;
    private DateTime _lastErrorLogUtc;

    public void Draw()
    {
        if (_disposed || !MasterPlayerSettings.Instance.EnableRadarOverlay)
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
    }

    private void DrawInternal()
    {
        var settings = MasterPlayerSettings.Instance;

        var flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                    ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize;
        if (settings.RadarOverlayLock)
        {
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;
        }

        if (settings.RadarOverlayTransparent)
        {
            flags |= ImGuiWindowFlags.NoBackground;
        }

        var fullSize = 2f * (ScreenMargin + ScreenHalfSize);
        ImGui.SetNextWindowSize(new Vector2(fullSize), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("MasterPlayer 雷达", flags))
        {
            ImGui.End();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(fullSize));
        _screenCenter = cursor + new Vector2(ScreenMargin + ScreenHalfSize);
        drawList.PushClipRect(cursor, cursor + new Vector2(fullSize), true);
        try
        {
            DrawRadarContents(drawList, settings);
        }
        finally
        {
            drawList.PopClipRect();
            ImGui.End();
        }
    }

    private void DrawRadarContents(ImDrawListPtr drawList, MasterPlayerSettings settings)
    {
        var plugin = PromeRotation.Plugin.Instance;
        var player = Svc.Objects.LocalPlayer;
        var now = DateTime.UtcNow;
        var snapshot = plugin.SafeZoneManager.BuildSnapshot(now);
        var appearance = settings.RadarAoe;
        var visibility = MasterPlayerPlugin.Instance?.AoeVisibility;

        float visibleRadius;
        if (settings.RadarCenterMode == 0 && snapshot.ArenaBoundary is { } boundary)
        {
            _center = boundary.Position;
            var bounds = boundary.GetBounds();
            visibleRadius = MathF.Max(bounds.Width, bounds.Height) * 0.5f + BoundaryPadding;
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
        _projection = new RadarProjection(_center, _screenCenter, scale, rotation);

        DrawBoundary(drawList, snapshot);
        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (!appearance.IsVisible(obstacle.Geometry, false, visibility?.GetVisibility(obstacle.Handle).Radar ?? true))
                continue;
            var style = appearance.GetDangerStyle();
            FillGeometry(drawList, obstacle.Geometry, style.FillColor!.Value, style.StrokeColor!.Value);
        }

        foreach (var goal in snapshot.GoalAreas)
        {
            if (!appearance.IsVisible(goal.Geometry, true, visibility?.GetVisibility(goal.Handle).Radar ?? true))
                continue;
            var style = appearance.GetSafeStyle();
            FillGeometry(drawList, goal.Geometry, style.FillColor!.Value, style.StrokeColor!.Value);
        }

        foreach (var danger in snapshot.DangerAreas)
        {
            if (!appearance.IsVisible(danger.Geometry, false, visibility?.GetVisibility(danger.Handle).Radar ?? true))
                continue;
            var style = appearance.GetDangerStyle(danger, now);
            FillGeometry(drawList, danger.Geometry, style.FillColor!.Value, style.StrokeColor!.Value);
        }

        if (visibility != null)
            foreach (var preview in visibility.GetPreviews(now))
            {
                if (!appearance.IsVisible(preview.Geometry, preview.Safe, preview.Visibility.Radar))
                    continue;
                var style = preview.Safe ? appearance.GetSafeStyle() : appearance.GetDangerStyle();
                FillGeometry(drawList, preview.Geometry, style.FillColor!.Value, style.StrokeColor!.Value);
            }

        DrawSolutionPath(drawList, plugin);
        DrawActors(drawList, player, visibleRadius);
    }

    private void DrawBoundary(ImDrawListPtr drawList, SafeZoneSnapshot snapshot)
    {
        if (snapshot.ArenaBoundary is { } boundary)
        {
            var color = ImGui.GetColorU32(BoundaryColor);
            foreach (var contour in boundary.GetContours())
            {
                StrokeContour(drawList, contour, color, 1.5f);
            }
        }
        else
        {
            drawList.AddCircle(_screenCenter, ScreenHalfSize - 1f, ImGui.GetColorU32(FallbackCircleColor), 48, 1.5f);
        }
    }

    private void FillGeometry(ImDrawListPtr drawList, IGeometryShape geometry, Vector4 fill, Vector4 stroke)
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
            FillRing(drawList, contours[0], contours[1], fillColor);
        }
        else
        {
            foreach (var contour in contours)
            {
                FillContour(drawList, contour, fillColor);
            }
        }

        var strokeColor = ImGui.GetColorU32(stroke);
        foreach (var contour in contours)
        {
            StrokeContour(drawList, contour, strokeColor, 1f);
        }
    }

    private void FillContour(ImDrawListPtr drawList, IReadOnlyList<Vector3> contour, uint color)
    {
        if (contour.Count < 3)
        {
            return;
        }

        if (PolygonTriangulator.TryTriangulate(contour, out var indices))
        {
            var flags = drawList.Flags;
            drawList.Flags &= ~ImDrawListFlags.AntiAliasedFill;
            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                drawList.AddTriangleFilled(
                    WorldToScreen(contour[indices[i]]),
                    WorldToScreen(contour[indices[i + 1]]),
                    WorldToScreen(contour[indices[i + 2]]),
                    color);
            }

            drawList.Flags = flags;
            return;
        }

        drawList.PathClear();
        foreach (var point in contour)
        {
            drawList.PathLineTo(WorldToScreen(point));
        }

        drawList.PathFillConvex(color);
        drawList.PathClear();
    }

    private void FillRing(ImDrawListPtr drawList, IReadOnlyList<Vector3> outer, IReadOnlyList<Vector3> inner, uint color)
    {
        var flags = drawList.Flags;
        drawList.Flags &= ~ImDrawListFlags.AntiAliasedFill;
        for (var i = 0; i < outer.Count; i++)
        {
            var next = (i + 1) % outer.Count;
            var o0 = WorldToScreen(outer[i]);
            var o1 = WorldToScreen(outer[next]);
            var i1 = WorldToScreen(inner[next]);
            var i0 = WorldToScreen(inner[i]);
            drawList.AddTriangleFilled(o0, o1, i1, color);
            drawList.AddTriangleFilled(o0, i1, i0, color);
        }

        drawList.Flags = flags;
    }

    private void StrokeContour(ImDrawListPtr drawList, IReadOnlyList<Vector3> contour, uint color, float thickness)
    {
        if (contour.Count < 2)
        {
            return;
        }

        for (var i = 0; i < contour.Count; i++)
        {
            drawList.AddLine(
                WorldToScreen(contour[i]),
                WorldToScreen(contour[(i + 1) % contour.Count]),
                color,
                thickness);
        }
    }

    private void DrawSolutionPath(ImDrawListPtr drawList, PromeRotation.Plugin plugin)
    {
        var path = plugin.SafeZoneMovementStatus.LatestSolution?.ExecutablePath;
        if (path is null || path.Count == 0)
        {
            return;
        }

        var color = ImGui.GetColorU32(PathColor);
        for (var i = 0; i < path.Count; i++)
        {
            var point = WorldToScreen(path[i].Position);
            if (i > 0)
            {
                drawList.AddLine(WorldToScreen(path[i - 1].Position), point, color, 1.5f);
            }

            drawList.AddCircleFilled(point, 2f, color);
        }
    }

    private void DrawActors(ImDrawListPtr drawList, IPlayerCharacter? player, float visibleRadius)
    {
        var maxDistSq = visibleRadius * visibleRadius;

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
                    drawList.AddCircleFilled(WorldToScreen(member.Position), 3f, ImGui.GetColorU32(PartyColor));
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

            drawList.AddCircleFilled(WorldToScreen(npc.Position), 3.5f, ImGui.GetColorU32(EnemyColor));
        }

        if (player != null)
        {
            DrawSelfMarker(drawList, player);
        }
    }

    private void DrawSelfMarker(ImDrawListPtr drawList, IPlayerCharacter player)
    {
        var selfPos = WorldToScreen(player.Position);
        var forward = GeometryHelper.PointFromRotationDistanceXZ(player.Position, player.Rotation, 1f);
        var dir = WorldToScreen(forward) - selfPos;
        if (dir.LengthSquared() <= float.Epsilon)
        {
            dir = new Vector2(0f, -1f);
        }
        else
        {
            dir = Vector2.Normalize(dir);
        }

        var ortho = new Vector2(dir.Y, -dir.X);
        var apex = selfPos + dir * 7f;
        var baseCenter = selfPos - dir * 4f;
        drawList.AddTriangleFilled(apex, baseCenter + ortho * 4.5f, baseCenter - ortho * 4.5f, ImGui.GetColorU32(SelfColor));
    }

    private bool WithinRange(Vector3 position, float maxDistSq)
        => GeometryHelper.DistanceSquaredXZ(position, _center) <= maxDistSq;

    private Vector2 WorldToScreen(Vector3 world)
        => _projection.Project(world);

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

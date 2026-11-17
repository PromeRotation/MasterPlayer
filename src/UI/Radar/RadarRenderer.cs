using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.Spatial.Drawing;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Radar;

/// <summary>
/// 雷达绘制：每帧在主线程把 SafeZone 快照与当前解算路径提交到 PR 的 DrawManager。
/// 生命周期策略：确定性 id 原位覆盖（DrawManager.Add 同 id 即 upsert）+ 帧末差集移除消失元素，
/// 并给每个元素挂 500ms 自过期兜底；不采用每帧 RemoveByRegex 全量重建（省一次正则构造与字典全扫）。
/// </summary>
public sealed class RadarRenderer : IDisposable
{
    private const string IdPrefix = "MasterPlayer_Radar_";
    private const string RemovePattern = "^MasterPlayer_Radar_";
    private const float SelfExpireMs = 500f;

    private static readonly DrawStyle BoundaryStyle = new(null, new Vector4(1f, 1f, 1f, 0.9f));
    private static readonly Vector4 PathColor = new(0.30f, 0.95f, 1f, 1f);
    private static readonly Vector4 PathShadeColor = new(0.02f, 0.10f, 0.16f, 1f);
    private static readonly DrawStyle PathCoreStyle = new(PathColor with { W = 0.62f }, null);
    private static readonly DrawStyle PathEdgeStyle = new(PathShadeColor with { W = 0.55f }, null);
    private static readonly DrawStyle PathChevronStyle = new(new Vector4(1f, 1f, 1f, 0.95f), PathShadeColor with { W = 0.85f }, 1.5f);
    private static readonly DrawStyle PathGoalRingStyle = new(PathColor with { W = 0.80f }, PathShadeColor with { W = 0.85f }, 1.5f);
    private static readonly DrawStyle PathGoalDotStyle = new(new Vector4(1f, 1f, 1f, 0.95f), PathShadeColor with { W = 0.85f }, 1.5f);

    private readonly List<Vector3> _pathInput = new(64);
    private readonly List<Vector3> _pathPoints = new(64);
    private readonly List<RadarWorldPathQuad> _pathQuads = new(96);
    private readonly List<RadarWorldPathChevron> _pathChevrons = new(RadarWorldPath.MaxChevrons);

    private HashSet<string> _drawnIds = new(StringComparer.Ordinal);
    private HashSet<string> _frameIds = new(StringComparer.Ordinal);
    private DateTime _lastErrorLogUtc;
    private bool _disposed;

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var plugin = PromeRotation.Plugin.Instance;
            if (!MasterPlayerSettings.Instance.EnableRadar)
            {
                if (_drawnIds.Count > 0)
                {
                    plugin.DrawManager.RemoveByRegex(RemovePattern);
                    _drawnIds.Clear();
                }

                return;
            }

            RenderFrame(plugin);
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastErrorLogUtc).TotalSeconds >= 5d)
            {
                _lastErrorLogUtc = now;
                Svc.Log.Error(ex, "[MasterPlayer] RadarRenderer.Update 失败。");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            PromeRotation.Plugin.Instance.DrawManager.RemoveByRegex(RemovePattern);
        }
        catch (Exception ex)
        {
            try
            {
                Svc.Log.Warning($"[MasterPlayer] RadarRenderer 清理绘制失败：{ex.Message}");
            }
            catch
            {
            }
        }

        _drawnIds.Clear();
        _frameIds.Clear();
    }

    private void RenderFrame(PromeRotation.Plugin plugin)
    {
        var now = MasterPlayerPlugin.Instance!.SafeZone.CurrentTime;
        var snapshot = MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(now);
        var appearance = MasterPlayerSettings.Instance.WorldAoe;
        var visibility = MasterPlayerPlugin.Instance?.AoeVisibility;
        var draw = plugin.DrawManager;
        var frameIds = _frameIds;
        frameIds.Clear();

        if (appearance.ShowAreas)
            DrawAreas(plugin, snapshot, appearance, visibility, now, frameIds);
        if (appearance.ShowPath)
            DrawPath(plugin, frameIds);

        foreach (var stale in _drawnIds)
        {
            if (!frameIds.Contains(stale))
            {
                draw.Remove(stale);
            }
        }

        (_drawnIds, _frameIds) = (frameIds, _drawnIds);
    }

    private static void DrawAreas(PromeRotation.Plugin plugin, SafeZoneSnapshot snapshot, AoeViewSettings appearance,
        AoeVisibilityRegistry? visibility, DateTime now, HashSet<string> frameIds)
    {
        var draw = plugin.DrawManager;
        if (snapshot.ArenaBoundary is { } boundary && boundary is not ILocalViewBoundary)
        {
            var id = IdPrefix + "Boundary";
            draw.Add(id, HostDrawingGeometry.Convert(boundary), BoundaryStyle, durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (!appearance.IsVisible(obstacle.Geometry, false, visibility?.GetVisibility(obstacle.Handle).World ?? true))
                continue;
            var id = $"{IdPrefix}Obstacle_{obstacle.Handle.Id}";
            draw.Add(id, HostDrawingGeometry.Convert(obstacle.Geometry), appearance.GetDangerStyle(), durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var goal in snapshot.GoalAreas)
        {
            if (visibility?.IsNavigationOnly(goal.Handle) == true) continue;
            if (!appearance.IsVisible(goal.Geometry, true, visibility?.GetVisibility(goal.Handle).World ?? true))
                continue;
            var id = $"{IdPrefix}Goal_{goal.Handle.Id}";
            draw.Add(id, HostDrawingGeometry.Convert(goal.Geometry), appearance.GetSafeStyle(), durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var danger in snapshot.DangerAreas)
        {
            if (visibility?.IsNavigationOnly(danger.Handle) == true) continue;
            if (!appearance.IsVisible(danger.Geometry, false, visibility?.GetVisibility(danger.Handle).World ?? true))
                continue;
            var id = $"{IdPrefix}Danger_{danger.Handle.Id}";
            draw.Add(id, HostDrawingGeometry.Convert(danger.Geometry), appearance.GetDangerStyle(danger, now), durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        if (visibility != null)
            foreach (var preview in visibility.GetPreviews(now))
            {
                if (!appearance.IsVisible(preview.Geometry, preview.Safe, preview.Visibility.World))
                    continue;
                var id = IdPrefix + "Preview_" + preview.Id;
                draw.Add(id, HostDrawingGeometry.Convert(preview.Geometry), preview.Style(appearance), durationMs: SelfExpireMs);
                frameIds.Add(id);
            }

        if (visibility != null)
            foreach (var line in visibility.GetLines())
            {
                if (!line.Visibility.World || !(line.Safe ? appearance.ShowSafe : appearance.ShowDanger)) continue;
                var id = IdPrefix + "Preview_" + line.Id;
                draw.AddTether(id, line.Start, line.End, line.Safe ? appearance.GetSafeStyle() : appearance.GetDangerStyle(), durationMs: SelfExpireMs);
                frameIds.Add(id);
            }
    }

    // Route = mitered ribbon + arrow heads flowing to the destination + a pulsing destination marker.
    private void DrawPath(PromeRotation.Plugin plugin, HashSet<string> frameIds)
    {
        var path = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus.LatestSolution?.ExecutablePath;
        if (path is null || path.Count == 0)
        {
            return;
        }

        _pathInput.Clear();
        foreach (var waypoint in path)
        {
            _pathInput.Add(waypoint.Position);
        }

        var time = Environment.TickCount64 % 3_600_000L / 1000d;
        if (!RadarWorldPath.TryBuild(_pathInput, time, _pathPoints, _pathQuads, _pathChevrons, out var destination))
        {
            return;
        }

        var draw = plugin.DrawManager;
        for (var i = 0; i < _pathQuads.Count; i++)
        {
            var quad = _pathQuads[i];
            AddPathPolygon(draw, frameIds, $"{IdPrefix}Path_Quad_{i}",
                quad.Part == RadarWorldPathPart.Core ? PathCoreStyle : PathEdgeStyle,
                quad.A, quad.B, quad.C, quad.D);
        }

        for (var i = 0; i < _pathChevrons.Count; i++)
        {
            var chevron = _pathChevrons[i];
            AddPathPolygon(draw, frameIds, $"{IdPrefix}Path_Chevron_{i}", PathChevronStyle,
                chevron.Tip, chevron.LeftFront, chevron.LeftBack, chevron.Notch, chevron.RightBack, chevron.RightFront);
        }

        var pulse = (float)(time * 0.9d % 1d);
        var pulseColor = PathColor with { W = 0.75f * (1f - pulse) };
        AddPathShape(draw, frameIds, IdPrefix + "Path_GoalPulse",
            new CircleGeometry(destination, 0.50f + 0.60f * pulse), new DrawStyle(null, pulseColor, 2f));
        AddPathShape(draw, frameIds, IdPrefix + "Path_GoalRing", new DonutGeometry(destination, 0.30f, 0.44f), PathGoalRingStyle);
        AddPathShape(draw, frameIds, IdPrefix + "Path_GoalDot", new CircleGeometry(destination, 0.13f), PathGoalDotStyle);
    }

    private static void AddPathPolygon(PromeRotation.Spatial.Drawing.DrawManager draw, HashSet<string> frameIds,
        string id, DrawStyle style, params Vector3[] points)
    {
        try
        {
            AddPathShape(draw, frameIds, id, new WorldPolygonGeometry(points), style);
        }
        catch (ArgumentException)
        {
            // A degenerate sliver on a very sharp turn is skipped rather than failing the whole frame.
        }
    }

    private static void AddPathShape(PromeRotation.Spatial.Drawing.DrawManager draw, HashSet<string> frameIds,
        string id, IGeometryShape shape, DrawStyle style)
    {
        draw.Add(id, HostDrawingGeometry.Convert(shape), style, durationMs: SelfExpireMs);
        frameIds.Add(id);
    }

}

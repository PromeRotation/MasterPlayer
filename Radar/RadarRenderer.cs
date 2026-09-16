using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

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
    private static readonly DrawStyle PathLineStyle = new(null, new Vector4(0f, 1f, 1f, 0.6f));
    private static readonly DrawStyle PathDotStyle = new(new Vector4(0f, 1f, 1f, 0.7f), new Vector4(0f, 1f, 1f, 1f));

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
        var now = DateTime.UtcNow;
        var snapshot = plugin.SafeZoneManager.BuildSnapshot(now);
        var appearance = MasterPlayerSettings.Instance.WorldAoe;
        var visibility = MasterPlayerPlugin.Instance?.AoeVisibility;
        var draw = plugin.DrawManager;
        var frameIds = _frameIds;
        frameIds.Clear();

        if (snapshot.ArenaBoundary is { } boundary)
        {
            var id = IdPrefix + "Boundary";
            draw.Add(id, boundary, BoundaryStyle, durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (!appearance.IsVisible(obstacle.Geometry, false, visibility?.GetVisibility(obstacle.Handle).World ?? true))
                continue;
            var id = $"{IdPrefix}Obstacle_{obstacle.Handle.Id}";
            draw.Add(id, obstacle.Geometry, appearance.GetDangerStyle(), durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var goal in snapshot.GoalAreas)
        {
            if (!appearance.IsVisible(goal.Geometry, true, visibility?.GetVisibility(goal.Handle).World ?? true))
                continue;
            var id = $"{IdPrefix}Goal_{goal.Handle.Id}";
            draw.Add(id, goal.Geometry, appearance.GetSafeStyle(), durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var danger in snapshot.DangerAreas)
        {
            if (!appearance.IsVisible(danger.Geometry, false, visibility?.GetVisibility(danger.Handle).World ?? true))
                continue;
            var id = $"{IdPrefix}Danger_{danger.Handle.Id}";
            draw.Add(id, danger.Geometry, appearance.GetDangerStyle(danger, now), durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        if (visibility != null)
            foreach (var preview in visibility.GetPreviews(now))
            {
                if (!appearance.IsVisible(preview.Geometry, preview.Safe, preview.Visibility.World))
                    continue;
                var id = IdPrefix + "Preview_" + preview.Id;
                draw.Add(id, preview.Geometry, preview.Safe ? appearance.GetSafeStyle() : appearance.GetDangerStyle(), durationMs: SelfExpireMs);
                frameIds.Add(id);
            }

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

    private void DrawPath(PromeRotation.Plugin plugin, HashSet<string> frameIds)
    {
        var path = plugin.SafeZoneMovementStatus.LatestSolution?.ExecutablePath;
        if (path is null || path.Count == 0)
        {
            return;
        }

        var draw = plugin.DrawManager;
        for (var i = 0; i < path.Count; i++)
        {
            var dotId = $"{IdPrefix}Path_Dot_{i}";
            draw.Add(dotId, new CircleGeometry(path[i].Position, 0.3f), PathDotStyle, durationMs: SelfExpireMs);
            frameIds.Add(dotId);

            if (i > 0)
            {
                var segmentId = $"{IdPrefix}Path_Seg_{i}";
                draw.AddTether(segmentId, path[i - 1].Position, path[i].Position, PathLineStyle,
                    width: 0.15f, endRadius: 0f, durationMs: SelfExpireMs);
                frameIds.Add(segmentId);
            }
        }
    }

}

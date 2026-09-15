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
    private static readonly DrawStyle ObstacleStyle = new(new Vector4(0.5f, 0.5f, 0.5f, 0.3f), new Vector4(0.7f, 0.7f, 0.7f, 0.9f));
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
            var id = $"{IdPrefix}Obstacle_{obstacle.Handle.Id}";
            draw.Add(id, obstacle.Geometry, ObstacleStyle, durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var goal in snapshot.GoalAreas)
        {
            var id = $"{IdPrefix}Goal_{goal.Handle.Id}";
            draw.Add(id, goal.Geometry, DrawStyle.Safe, durationMs: SelfExpireMs);
            frameIds.Add(id);
        }

        foreach (var danger in snapshot.DangerAreas)
        {
            var id = $"{IdPrefix}Danger_{danger.Handle.Id}";
            draw.Add(id, danger.Geometry, DangerStyle(danger, now), durationMs: SelfExpireMs);
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

    private static DrawStyle DangerStyle(DangerAreaSnapshot danger, DateTime now)
    {
        var progress = 0f;
        var windows = danger.DangerWindows;
        if (windows.Count == 0)
        {
            progress = 1f;
        }
        else
        {
            for (var i = 0; i < windows.Count; i++)
            {
                progress = MathF.Max(progress, windows[i].TelegraphProgressAt(now));
            }
        }

        var rgb = DrawStyle.DangerPresetColor;
        var fillAlpha = 0.12f + 0.43f * progress;
        return new DrawStyle(new Vector4(rgb, fillAlpha), new Vector4(rgb, 1f));
    }
}

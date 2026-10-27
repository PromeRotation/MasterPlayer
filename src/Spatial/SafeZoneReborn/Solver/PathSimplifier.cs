using MasterPlayer.Spatial.SafeZone;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 原始时间路径简化：删除重复空间点 → 合并共线移动节点 → 重新计算时间。
/// <para>安全性由 A* 的 <see cref="BossModStyleAStar.Result.PathLeeway"/> 保证；本类只做视觉/长度优化，不做二次验证。</para>
/// </summary>
internal static class PathSimplifier
{
    /// <summary>判定两点"重复"的距离阈值（yalm）。</summary>
    private const float DuplicateDistance = 0.05f;

    /// <summary>共线判定：相邻两段方向夹角余弦阈值（接近 1 视为共线）。</summary>
    private const float CollinearCosThreshold = 0.9995f;

    /// <summary>
    /// 简化路径并重算时间。
    /// </summary>
    public static IReadOnlyList<TimedWaypoint> Simplify(
        IReadOnlyList<TimedWaypoint> raw,
        float speed,
        SolveOptions options)
    {
        if (raw.Count <= 2)
        {
            return raw;
        }

        var dedup = RemoveDuplicates(raw);
        var merged = MergeCollinear(dedup);
        return RecomputeTimes(merged, speed);
    }

    private static List<TimedWaypoint> RemoveDuplicates(IReadOnlyList<TimedWaypoint> path)
    {
        var result = new List<TimedWaypoint> { path[0] };
        for (var i = 1; i < path.Count; i++)
        {
            var wp = path[i];
            var prev = result[result.Count - 1];

            if (wp.IsWaitPoint)
            {
                result.Add(wp);
                continue;
            }

            if (!prev.IsWaitPoint && GeometryHelper.DistanceXZ(prev.Position, wp.Position) < DuplicateDistance)
            {
                result[result.Count - 1] = wp with { Position = prev.Position };
                continue;
            }

            result.Add(wp);
        }

        return result;
    }

    private static List<TimedWaypoint> MergeCollinear(List<TimedWaypoint> path)
    {
        if (path.Count <= 2)
        {
            return path;
        }

        var result = new List<TimedWaypoint> { path[0] };
        for (var i = 1; i + 1 < path.Count; i++)
        {
            var prev = result[result.Count - 1];
            var cur = path[i];
            var next = path[i + 1];

            if (cur.IsWaitPoint || prev.IsWaitPoint || next.IsWaitPoint)
            {
                result.Add(cur);
                continue;
            }

            var d1 = new Vector2(cur.Position.X - prev.Position.X, cur.Position.Z - prev.Position.Z);
            var d2 = new Vector2(next.Position.X - cur.Position.X, next.Position.Z - cur.Position.Z);
            var l1 = d1.Length();
            var l2 = d2.Length();
            if (l1 > GeometryHelper.DefaultEpsilon && l2 > GeometryHelper.DefaultEpsilon)
            {
                var cos = Vector2.Dot(d1 / l1, d2 / l2);
                if (cos >= CollinearCosThreshold)
                {
                    continue;
                }
            }

            result.Add(cur);
        }

        result.Add(path[path.Count - 1]);
        return result;
    }

    /// <summary>按路径长度 / 速度重算每个节点的到达/离开时间；保留等待节点的等待时长。</summary>
    private static List<TimedWaypoint> RecomputeTimes(List<TimedWaypoint> path, float speed)
    {
        if (path.Count == 0)
        {
            return path;
        }

        var safeSpeed = MathF.Max(speed, 0.1f);
        var result = new List<TimedWaypoint>(path.Count) { path[0] };
        var cursor = path[0].PlannedDepartureTime;

        for (var i = 1; i < path.Count; i++)
        {
            var src = path[i];
            var prev = result[result.Count - 1];

            if (src.IsWaitPoint)
            {
                var waitSeconds = (src.PlannedDepartureTime - src.PlannedArrivalTime).TotalSeconds;
                if (waitSeconds < 0d)
                {
                    waitSeconds = 0d;
                }

                var arrival = cursor;
                var departure = arrival + TimeSpan.FromSeconds(waitSeconds);
                result.Add(new TimedWaypoint(src.Position, arrival, departure, IsWaitPoint: true, src.SpeedMultiplier));
                cursor = departure;
                continue;
            }

            var dist = GeometryHelper.DistanceXZ(prev.Position, src.Position);
            var arrivalTime = cursor + TimeSpan.FromSeconds(dist / safeSpeed);
            result.Add(new TimedWaypoint(src.Position, arrivalTime, arrivalTime, IsWaitPoint: false, src.SpeedMultiplier));
            cursor = arrivalTime;
        }

        return result;
    }
}

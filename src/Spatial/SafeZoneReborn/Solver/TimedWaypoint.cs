using MasterPlayer.Spatial.SafeZone;
using System;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 解算路径上的单个时间路点。
/// <para>所有时间字段都是 SafeZone 内部绝对 UTC 时间，与 <see cref="DangerWindow"/> / <see cref="GoalWindow"/> 同一基准。</para>
/// </summary>
public readonly record struct TimedWaypoint(
    Vector3 Position,
    DateTime PlannedArrivalTime,
    DateTime PlannedDepartureTime,
    bool IsWaitPoint,
    float SpeedMultiplier)
{
    /// <summary>创建一个非等待路点：到达即离开，使用指定速度倍率（默认 1.0）。</summary>
    public static TimedWaypoint Pass(Vector3 position, DateTime arrival, float speedMultiplier = 1f)
        => new(position, arrival, arrival, false, speedMultiplier);
}

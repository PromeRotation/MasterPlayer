using System;
using System.Collections.Generic;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 不可变快照。
/// <para>由 <c>SafeZoneManager.BuildSnapshot</c> 在主线程构造后即可安全地传递到任意线程（包括后台解算线程）。</para>
/// <para>所有几何对象都是构造时已经解析完成的 <see cref="IGeometryShape"/>，不再持有任何运行时实体引用。</para>
/// </summary>
public sealed class SafeZoneSnapshot
{
    /// <summary>构造一个全空的快照（不包含任何边界、障碍、危险区或目标区）。</summary>
    public static SafeZoneSnapshot Empty(int version, DateTime createdAt)
        => new(
            version,
            createdAt,
            null,
            Array.Empty<ObstacleSnapshot>(),
            Array.Empty<DangerAreaSnapshot>(),
            Array.Empty<GoalAreaSnapshot>());

    public SafeZoneSnapshot(
        int version,
        DateTime createdAt,
        IGeometryShape? arenaBoundary,
        IReadOnlyList<ObstacleSnapshot> hardObstacles,
        IReadOnlyList<DangerAreaSnapshot> dangerAreas,
        IReadOnlyList<GoalAreaSnapshot> goalAreas)
    {
        Version = version;
        CreatedAt = createdAt;
        ArenaBoundary = arenaBoundary;
        HardObstacles = hardObstacles;
        DangerAreas = dangerAreas;
        GoalAreas = goalAreas;
    }

    /// <summary>SafeZoneManager 单调递增的版本号。Solver 用它判断 Solution 是否仍指向同一份快照。</summary>
    public int Version { get; }

    /// <summary>快照构造时间（UTC）。</summary>
    public DateTime CreatedAt { get; }

    /// <summary>副本边界。当前只支持静态边界，可为空。</summary>
    public IGeometryShape? ArenaBoundary { get; }

    /// <summary>硬障碍。Solver 路径不得穿越。</summary>
    public IReadOnlyList<ObstacleSnapshot> HardObstacles { get; }

    /// <summary>危险区。每个条目自带 <see cref="DangerWindow"/> 列表。</summary>
    public IReadOnlyList<DangerAreaSnapshot> DangerAreas { get; }

    /// <summary>目标区。每个条目自带 <see cref="GoalWindow"/> 列表。</summary>
    public IReadOnlyList<GoalAreaSnapshot> GoalAreas { get; }
}

/// <summary>快照中的硬障碍条目。</summary>
public sealed record ObstacleSnapshot(SafeZoneAreaHandle Handle, IGeometryShape Geometry);

/// <summary>快照中的危险区条目。Geometry 已经按主线程实体状态解析完成。</summary>
public sealed record DangerAreaSnapshot(
    SafeZoneAreaHandle Handle,
    IGeometryShape Geometry,
    IReadOnlyList<DangerWindow> DangerWindows);

/// <summary>快照中的目标区条目。Geometry 已经按主线程实体状态解析完成。</summary>
public sealed record GoalAreaSnapshot(
    SafeZoneAreaHandle Handle,
    IGeometryShape Geometry,
    IReadOnlyList<GoalWindow> GoalWindows);

using MasterPlayer.Spatial.SafeZone;
using System;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Spatial.SafeZoneReborn;

/// <summary>
/// SafeZone 自动移动系统的只读观测接口，供外部模块绘制状态 UI。
/// </summary>
public interface ISafeZoneRebornMovementStatus
{
    SafeZoneSolution? LatestSolution { get; }

    bool HasControl { get; }

    string PhaseText { get; }

    int DispatchCount { get; }

    DateTime LastDispatchUtc { get; }

    bool IsAsyncSolveRunning { get; }

    int AsyncSolveStartedCount { get; }

    int AsyncSolveAcceptedCount { get; }

    int AsyncSolveCancelledCount { get; }

    int AsyncSolveDiscardedCount { get; }
}

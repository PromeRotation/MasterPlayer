using MasterPlayer.Spatial.SafeZone;
namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 解算结果状态。仅一部分值会被当前 Solver 实际产出，其余为扩展预留以保持枚举稳定。
/// </summary>
public enum SolutionStatus
{
    /// <summary>成功：当前已安全且无需移动，或找到一条安全可执行路径。</summary>
    Success,

    /// <summary>玩家当前位于副本边界之外。</summary>
    PlayerOutsideArena,

    /// <summary>找不到可达的安全格。</summary>
    NoReachableSafeCell,

    /// <summary>存在显式目标区域，但找不到可达目标。</summary>
    NoReachableGoal,

    /// <summary>所有候选目标都无法在到达时刻保持时间安全。</summary>
    NoTemporalPath,

    /// <summary>路径与危险时间窗口冲突。</summary>
    DangerWindowConflict,

    /// <summary>错过了目标窗口的可用时间。</summary>
    GoalWindowMissed,

    /// <summary>超过允许的最大等待时间。</summary>
    MaximumWaitExceeded,

    /// <summary>所需速度超过可达到的上限。</summary>
    RequiredSpeedTooHigh,

    /// <summary>最终连续几何验证失败。</summary>
    PathValidationFailed,

    /// <summary>输入快照无效（缺失或版本不一致）。</summary>
    SnapshotInvalid,

    /// <summary>结果已过期（异步完成时校验失败）。</summary>
    ResultExpired,

    /// <summary>解算被取消。</summary>
    Cancelled,
}

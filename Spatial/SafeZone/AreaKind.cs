namespace MasterPlayer.Spatial.SafeZone;

/// <summary>SafeZone 区域类型。</summary>
public enum AreaKind
{
    /// <summary>危险区。玩家在 DangerWindow 内进入区域视为受击。</summary>
    Danger,

    /// <summary>目标区。Solver 在 GoalWindow 内优先把玩家引导进入。</summary>
    Goal,

    /// <summary>硬障碍。Solver 路径不得穿越，与时间窗口无关。</summary>
    Obstacle,

    /// <summary>副本边界。Solver 必须确保解算结果不会离开该区域。</summary>
    ArenaBoundary
}

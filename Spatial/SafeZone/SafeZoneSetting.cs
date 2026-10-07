using System;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 运行时配置。仅内存状态，无持久化。
/// </summary>
public sealed class SafeZoneSetting
{
    /// <summary>是否启用安全区解算系统。</summary>
    public bool EnableSafeZone { get; set; }

    /// <summary>是否允许安全区系统接管自动移动。</summary>
    public bool EnableAutoMovement { get; set; }

    internal bool RuntimeMovementRequested { get; set; }
    internal bool RuntimeMovementHeld { get; set; }
    internal bool RuntimePreferStraightPaths { get; set; }
    internal float? RuntimeBaseSpeed { get; set; }
    internal float? RuntimeArenaSafetyMargin { get; set; }
    internal bool EffectiveEnableSafeZone => EnableSafeZone || RuntimeMovementRequested;
    internal bool EffectiveEnableAutoMovement => (EnableAutoMovement || RuntimeMovementRequested) && !RuntimeMovementHeld;

    /// <summary>短期输出时间优化请求的有效期限。它不是出发时刻；Reborn 必须实时验证等待和撤离路线。</summary>
    public DateTime AttackUptimeExpiresAt { get; set; }

    /// <summary>是否只在已注册求解区域内运行解算。</summary>
    public bool OnlySolveInRegisteredRegion { get; set; } = true;

    /// <summary>开发期雷达默认边界中心。</summary>
    public Vector3 DebugArenaCenter { get; set; } = new(0f, 0f, 0f);

    /// <summary>开发期雷达默认边界半径。</summary>
    public float DebugArenaRadius { get; set; } = 20f;
}

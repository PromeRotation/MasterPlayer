using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 解算用的移动能力不可变快照。速度按 <c>BaseSpeed × EffectiveSpeedMultiplier</c> 线性估算。
/// <para>由 <see cref="CaptureFromMainThread"/> 在主线程读取速度倍率服务后产出，之后可安全跨线程。</para>
/// </summary>
public readonly record struct MovementCapability(
    float BaseSpeed,
    float CurrentSpeed,
    float MovementStartDelay,
    bool AllowSpeedAdjustment,
    float MinimumRequestedSpeed,
    float MaximumAllowedSpeed)
{
    /// <summary>默认基础移动速度（yalm/秒）。</summary>
    public const float DefaultBaseSpeed = 6.0f;

    /// <summary>
    /// 默认移动启动延迟（秒）。归零：视移动为瞬时开始，保守余量统一放在 <c>SolveOptions.DangerStartSafetyTime</c>。
    /// </summary>
    public const float DefaultMovementStartDelay = 0f;

    /// <summary>
    /// 在主线程读取速度倍率服务，产出不可变移动能力快照。
    /// <para><see cref="CurrentSpeed"/> = <paramref name="baseSpeed"/> × <c>EffectiveMultiplier</c>。</para>
    /// </summary>
    public static MovementCapability CaptureFromMainThread(
        float speedMultiplier,
        float baseSpeed = DefaultBaseSpeed,
        float movementStartDelay = DefaultMovementStartDelay)
    {
        var currentSpeed = baseSpeed * speedMultiplier;

        // 未启用自动提速，最低/最高请求速度都收敛到当前速度。
        return new MovementCapability(
            BaseSpeed: baseSpeed,
            CurrentSpeed: currentSpeed,
            MovementStartDelay: movementStartDelay,
            AllowSpeedAdjustment: false,
            MinimumRequestedSpeed: currentSpeed,
            MaximumAllowedSpeed: currentSpeed);
    }
}

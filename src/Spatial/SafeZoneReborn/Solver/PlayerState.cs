using MasterPlayer.Spatial.SafeZone;
using System;
using System.Numerics;
using ECommons.DalamudServices;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 解算用的玩家状态不可变快照。
/// <para>由 <see cref="CaptureFromMainThread"/> 在主线程读取 Dalamud 对象后产出，之后可安全跨线程使用。</para>
/// <para>时间字段以秒计；位置/朝向使用 Dalamud 世界坐标与 FFXIV Rotation 弧度。</para>
/// </summary>
public readonly record struct PlayerState(
    Vector3 Position,
    float Rotation,
    float CurrentSpeed,
    bool IsMoving,
    bool IsCasting,
    float CastTimeRemaining,
    bool CanInterruptMovement,
    int SnapshotVersion)
{
    /// <summary>
    /// 在主线程读取当前本地玩家，产出不可变快照。
    /// <para><paramref name="currentSpeed"/> 由 <see cref="MovementCapability.CurrentSpeed"/> 传入，使两者一致。</para>
    /// <para><paramref name="snapshotVersion"/> 一般取 <c>SafeZoneManager.Version</c>，用于下游判断解算结果是否仍对应同一份快照。</para>
    /// <para>玩家不存在时返回带版本号的默认值（位置为零、未移动、未读条），由调用方决定是否解算。</para>
    /// </summary>
    public static PlayerState CaptureFromMainThread(int snapshotVersion, float currentSpeed, bool isMoving = false)
    {
        var me = Svc.Objects.LocalPlayer;
        if (me is null)
        {
            return new PlayerState(Vector3.Zero, 0f, currentSpeed, false, false, 0f, true, snapshotVersion);
        }

        var castTimeRemaining = me.IsCasting ? MathF.Max(0f, me.TotalCastTime - me.CurrentCastTime) : 0f;

        return new PlayerState(
            Position: me.Position,
            Rotation: me.Rotation,
            CurrentSpeed: currentSpeed,
            IsMoving: isMoving,
            IsCasting: me.IsCasting,
            CastTimeRemaining: castTimeRemaining,
            // FFXIV 中移动随时可发起（硬读条会被移动打断），统一视为可打断；
            // 是否因读条余量不足而打断读条移动交由 SafeZoneRebornMovementSystem.ShouldWaitForCast 决策。
            CanInterruptMovement: true,
            SnapshotVersion: snapshotVersion);
    }
}

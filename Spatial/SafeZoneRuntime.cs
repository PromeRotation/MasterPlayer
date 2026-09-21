using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

namespace MasterPlayer.Spatial;

public sealed class SafeZoneRuntime : IDisposable
{
    private readonly SafeZoneRebornMovementSystem _movement;
    private Vector3? _previousPosition;
    private bool _disposed;

    public SafeZoneSetting Setting { get; } = new();
    public SafeZoneManager Manager { get; }
    public SafeZoneRegistry Registry { get; }
    public ISafeZoneRebornMovementStatus MovementStatus => _movement;

    internal SafeZoneRuntime(IGreenMoveSystem movement, Func<bool> externalMovementEnabled, Func<bool> externalHasControl)
    {
        Manager = new(Setting);
        Registry = new(Manager);
        _movement = new(Setting, Manager, externalMovementEnabled, externalHasControl);
        _movement.Attach(movement);
    }

    internal void Update(float speedMultiplier, bool allowMovement)
    {
        if (_disposed) return;
        var capability = MovementCapability.CaptureFromMainThread(speedMultiplier);
        PlayerState? player = null;
        if (Svc.Objects.LocalPlayer is { IsDead: false } actor)
        {
            var moving = _previousPosition is { } previous && Vector3.DistanceSquared(previous, actor.Position) > 0.0001f;
            player = PlayerState.CaptureFromMainThread(Manager.Version, capability.CurrentSpeed, moving);
            _previousPosition = actor.Position;
        }
        else _previousPosition = null;
        _movement.Update(player, capability, DateTime.UtcNow, allowMovement);
    }

    public void Clear()
    {
        if (_disposed) return;
        Manager.ClearAllAreas();
        _movement.Reset();
        _previousPosition = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _movement.Dispose(); }
        finally { Manager.Dispose(); }
    }
}

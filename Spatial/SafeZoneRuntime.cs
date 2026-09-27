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
    internal Func<DateTime>? ReplayClock { get; set; }
    public DateTime CurrentTime => ReplayClock?.Invoke() ?? DateTime.UtcNow;

    /// <summary>当前发布的解算传送边（机制导航桥写入，仅主线程）。</summary>
    public IReadOnlyList<SolverTeleporter> Teleporters { get; private set; } = [];

    /// <summary>发布/替换解算传送边；空列表清除。</summary>
    public void SetTeleporters(IReadOnlyList<SolverTeleporter> teleporters)
    {
        Teleporters = teleporters;
        _movement.SetTeleporters(teleporters);
    }

    internal SafeZoneRuntime(IGreenMoveSystem movement, Func<bool> externalMovementEnabled, Func<bool> externalHasControl)
    {
        Manager = new(Setting);
        Registry = new(Manager, () => CurrentTime);
        _movement = new(Setting, Manager, externalMovementEnabled, externalHasControl);
        _movement.Attach(movement);
    }

    internal void Update(float speedMultiplier, bool allowMovement)
    {
        if (_disposed) return;
        if (!allowMovement)
        {
            _movement.Update(null, default, CurrentTime, false);
            Manager.PruneExpiredAreas(CurrentTime - TimeSpan.FromSeconds(MathF.Max(0f, SolveOptionsDefaults.Default.DangerEndSafetyTime)));
            _previousPosition = null;
            return;
        }
        var capability = MovementCapability.CaptureFromMainThread(speedMultiplier);
        PlayerState? player = null;
        if (Svc.Objects.LocalPlayer is { IsDead: false } actor)
        {
            var moving = _previousPosition is { } previous && Vector3.DistanceSquared(previous, actor.Position) > 0.0001f;
            player = PlayerState.CaptureFromMainThread(Manager.Version, capability.CurrentSpeed, moving);
            _previousPosition = actor.Position;
        }
        else _previousPosition = null;
        _movement.Update(player, capability, CurrentTime, allowMovement);
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

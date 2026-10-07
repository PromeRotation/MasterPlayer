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
    private readonly HashSet<object> _movementRequests = [];
    private readonly HashSet<object> _movementHolds = [];
    private readonly HashSet<object> _straightPathRequests = [];
    private readonly Dictionary<object, float> _movementSpeeds = [];
    private readonly Dictionary<object, float> _arenaMargins = [];

    public SafeZoneSetting Setting { get; } = new();
    public SafeZoneManager Manager { get; }
    public SafeZoneRegistry Registry { get; }
    public ISafeZoneRebornMovementStatus MovementStatus => _movement;
    internal Func<DateTime>? ReplayClock { get; set; }
    public DateTime CurrentTime => ReplayClock?.Invoke() ?? DateTime.UtcNow;

    internal void RequestAutomaticMovement(object owner, bool enabled, bool hold = false, float? baseSpeed = null, float? arenaSafetyMargin = null, bool preferStraightPaths = false)
    {
        if (_disposed) return;
        var wasHeld = Setting.RuntimeMovementHeld;
        if (enabled) _movementRequests.Add(owner); else _movementRequests.Remove(owner);
        if (enabled && hold) _movementHolds.Add(owner); else _movementHolds.Remove(owner);
        if (enabled && preferStraightPaths) _straightPathRequests.Add(owner); else _straightPathRequests.Remove(owner);
        Setting.RuntimePreferStraightPaths = _straightPathRequests.Count != 0;
        if (enabled && baseSpeed is { } speed && float.IsFinite(speed) && speed is > 0 and <= 30) _movementSpeeds[owner] = speed;
        else _movementSpeeds.Remove(owner);
        if (enabled && arenaSafetyMargin is { } margin && float.IsFinite(margin) && margin is >= 0 and <= 2) _arenaMargins[owner] = margin;
        else _arenaMargins.Remove(owner);
        Setting.RuntimeMovementRequested = _movementRequests.Count != 0;
        Setting.RuntimeMovementHeld = _movementHolds.Count != 0;
        Setting.RuntimeBaseSpeed = _movementSpeeds.Count != 0 ? _movementSpeeds.Values.Min() : null;
        var oldMargin = Setting.RuntimeArenaSafetyMargin;
        Setting.RuntimeArenaSafetyMargin = _movementRequests.Count == 0 ? null
            : _movementRequests.Max(request => _arenaMargins.GetValueOrDefault(request, 0.5f));
        if (wasHeld != Setting.RuntimeMovementHeld || oldMargin != Setting.RuntimeArenaSafetyMargin) _movement.Reset();
    }

    private void ClearMovementRequests()
    {
        _movementRequests.Clear(); _movementHolds.Clear();
        _straightPathRequests.Clear(); Setting.RuntimePreferStraightPaths = false;
        _movementSpeeds.Clear(); Setting.RuntimeBaseSpeed = null;
        _arenaMargins.Clear(); Setting.RuntimeArenaSafetyMargin = null;
        Setting.RuntimeMovementRequested = false; Setting.RuntimeMovementHeld = false;
    }

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
        var capability = MovementCapability.CaptureFromMainThread(speedMultiplier,
            Setting.RuntimeBaseSpeed ?? MovementCapability.DefaultBaseSpeed);
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
        ClearMovementRequests();
        _movement.Reset();
        _previousPosition = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearMovementRequests();
        try { _movement.Dispose(); }
        finally { Manager.Dispose(); }
    }
}

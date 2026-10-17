using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

namespace MasterPlayer.Spatial;

internal sealed record DirectTraversalExecution(IReadOnlyList<Vector3> Path, int CurrentIndex,
    bool HasControl, bool Rejected, bool InputLocked, DirectTraversalMotion? Motion = null);

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
    private readonly Dictionary<object, float> _hardMargins = [];
    private object? _committedOwner;
    private object? _directOwner;
    private object? _directFeedbackOwner;

    public SafeZoneSetting Setting { get; } = new();
    public SafeZoneManager Manager { get; }
    public SafeZoneRegistry Registry { get; }
    public ISafeZoneRebornMovementStatus MovementStatus => _movement;
    internal Func<DateTime>? ReplayClock { get; set; }
    public DateTime CurrentTime => ReplayClock?.Invoke() ?? DateTime.UtcNow;

    internal void RequestAutomaticMovement(object owner, bool enabled, bool hold = false, float? baseSpeed = null, float? arenaSafetyMargin = null, bool preferStraightPaths = false, float? hardSafetyMargin = null)
    {
        if (_disposed) return;
        enabled &= Setting.MovementAllowed;
        if (!enabled && ReferenceEquals(_directOwner, owner)) RequestDirectTraversal(owner, null, CurrentTime);
        if ((!enabled || hold) && ReferenceEquals(_committedOwner, owner)) ReleaseCommittedMovement(owner);
        var wasHeld = Setting.RuntimeMovementHeld;
        var ownersChanged = enabled ? _movementRequests.Add(owner) : _movementRequests.Remove(owner);
        if (ownersChanged) _movement.ClearTemporalContinuation();
        if (_directOwner != null && _movementRequests.Count != 1)
        { _directOwner = null; _movement.SetDirectTraversal(null, CurrentTime); }
        if (enabled && hold) _movementHolds.Add(owner); else _movementHolds.Remove(owner);
        if (enabled && preferStraightPaths) _straightPathRequests.Add(owner); else _straightPathRequests.Remove(owner);
        Setting.RuntimePreferStraightPaths = _straightPathRequests.Count != 0;
        if (enabled && baseSpeed is { } speed && float.IsFinite(speed) && speed is > 0 and <= 30) _movementSpeeds[owner] = speed;
        else _movementSpeeds.Remove(owner);
        if (enabled && arenaSafetyMargin is { } margin && float.IsFinite(margin) && margin is >= 0 and <= 2) _arenaMargins[owner] = margin;
        else _arenaMargins.Remove(owner);
        if (enabled && hardSafetyMargin is { } hardMargin && float.IsFinite(hardMargin) && hardMargin is >= 0 and <= 2) _hardMargins[owner] = hardMargin;
        else _hardMargins.Remove(owner);
        Setting.RuntimeMovementRequested = _movementRequests.Count != 0;
        Setting.RuntimeMovementHeld = _movementHolds.Count != 0;
        Setting.RuntimeBaseSpeed = _movementSpeeds.Count != 0 ? _movementSpeeds.Values.Min() : null;
        var oldMargin = Setting.RuntimeArenaSafetyMargin;
        var oldHardMargin = Setting.RuntimeHardSafetyMargin;
        Setting.RuntimeArenaSafetyMargin = _movementRequests.Count == 0 ? null
            : _movementRequests.Max(request => _arenaMargins.GetValueOrDefault(request, 0.5f));
        Setting.RuntimeHardSafetyMargin = _hardMargins.Count == 0 ? null
            : _movementRequests.Max(request => _hardMargins.GetValueOrDefault(request, 0.5f));
        if (wasHeld != Setting.RuntimeMovementHeld || oldMargin != Setting.RuntimeArenaSafetyMargin
            || oldHardMargin != Setting.RuntimeHardSafetyMargin) _movement.Reset();
    }

    // Only a requesting owner may commit its already-dispatched, direct landing
    // route. This short lease does not create a second movement controller.
    internal bool CommitJumpMovement(object owner, Vector3 landing, Vector3 player, float speed, DateTime now)
    {
        if (_disposed || !Setting.MovementAllowed || !_movementRequests.Contains(owner) || _movementHolds.Count != 0
            || !_movement.CommitJumpRoute(landing, player, speed, now)) return false;
        _committedOwner = owner;
        return true;
    }

    internal void ReleaseCommittedMovement(object owner)
    {
        if (!ReferenceEquals(_committedOwner, owner)) return;
        _committedOwner = null;
        _movement.ClearJumpRoute();
    }

    internal void StopOwnedMovement(object owner)
    {
        if (!_movementRequests.Contains(owner)) return;
        ReleaseCommittedMovement(owner);
        _movement.Reset();
    }

    internal void RequestDirectTraversal(object owner, Vector3? target, DateTime now, IReadOnlyList<Vector3>? path = null,
        float headingTolerance = 0, DirectTraversalMotion? motion = null)
    {
        if (_disposed) return;
        if (Setting.MovementAllowed && target.HasValue && _movementRequests.Contains(owner) && _movementRequests.Count == 1)
        {
            _directOwner = _directFeedbackOwner = owner;
            _movement.SetDirectTraversal(target, now.AddSeconds(.25), path, headingTolerance, motion, now);
        }
        else if (ReferenceEquals(_directOwner, owner))
        {
            _directOwner = null;
            _movement.SetDirectTraversal(null, now);
        }
    }

    internal DirectTraversalExecution GetDirectTraversalExecution(object owner, DateTime now)
        => !_disposed && ReferenceEquals(_directFeedbackOwner, owner)
            && _movementRequests.Contains(owner) && _movementRequests.Count == 1
            ? _movement.GetDirectTraversalExecution(now) : new([], 0, false, false, false);

    private void ClearMovementRequests()
    {
        _directOwner = null;
        _directFeedbackOwner = null;
        _movement.SetDirectTraversal(null, CurrentTime);
        _committedOwner = null;
        _movement.ClearJumpRoute();
        _movementRequests.Clear(); _movementHolds.Clear();
        _straightPathRequests.Clear(); Setting.RuntimePreferStraightPaths = false;
        _movementSpeeds.Clear(); Setting.RuntimeBaseSpeed = null;
        _arenaMargins.Clear(); Setting.RuntimeArenaSafetyMargin = null;
        _hardMargins.Clear(); Setting.RuntimeHardSafetyMargin = null;
        Setting.RuntimeMovementRequested = false; Setting.RuntimeMovementHeld = false;
    }

    internal void SuspendMovement()
    {
        Setting.MovementAllowed = false;
        ClearMovementRequests();
        _movement.Reset();
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

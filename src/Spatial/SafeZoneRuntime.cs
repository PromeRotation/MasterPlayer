using System.Numerics;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Terrain;
using Dalamud.Game.ClientState.Conditions;

namespace MasterPlayer.Spatial;

internal sealed record DirectTraversalExecution(IReadOnlyList<Vector3> Path, int CurrentIndex,
    bool HasControl, bool Rejected, bool InputLocked, DirectTraversalMotion? Motion = null);

public sealed class SafeZoneRuntime : IDisposable
{
    private readonly SafeZoneRebornMovementSystem _movement;
    private readonly LocalTerrainController terrain;
    private readonly IGreenMoveSystem hostMovement;
    private string? lastTerrainError;
    internal LocalTerrainBoundary? LocalTerrain => terrain.Current;
    internal LocalTerrainBoundary? TerrainDisplay => terrain.Display;
    internal float TerrainViewRadius => terrain.ViewRadius;
    internal string TerrainStatus => terrain.StatusText;
    internal double TerrainScanMilliseconds => terrain.LastScanMilliseconds;
    internal TerrainCaptureDiagnostics? TerrainCaptureDiagnostics => terrain.CaptureDiagnostics;
    internal TerrainSceneCapture? TerrainCapture => terrain.LatestCapture;
    internal bool TerrainExporting => terrain.Exporting;
    internal string TerrainExportStatus => terrain.ExportStatus;
    internal bool ExportTerrainScene(string directory) => terrain.ExportScene(directory);
    private Vector3? _previousPosition;
    private readonly ObservedMoveSpeed _observedSpeed = new();
    private ulong? _previousPlayerId;
    private bool _disposed;
    private readonly HashSet<object> _movementRequests = [];
    private readonly HashSet<object> _movementHolds = [];
    private readonly HashSet<object> _straightPathRequests = [];
    private readonly HashSet<object> _timedGoalRequests = [];
    private readonly HashSet<object> _noEscapeDelayRequests = [];
    private readonly HashSet<object> _safeRefugeRequests = [];
    private readonly HashSet<object> _measuredSpeedRequests = [];
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
    internal void SetMovementHandoff(Func<bool, bool>? request)
    {
        if (request == null) _movement.Reset();
        _movement.RequestExclusiveMovement = request;
    }
    internal Func<DateTime>? ReplayClock { get; set; }
    public DateTime CurrentTime => ReplayClock?.Invoke() ?? DateTime.UtcNow;
    /// <summary>Measured steady run speed (y/s), when one has been observed.</summary>
    internal float? ObservedRunSpeed => _observedSpeed.Observed;
    internal float PlanningSpeed { get; private set; }

    internal void RequestAutomaticMovement(object owner, bool enabled, bool hold = false, float? baseSpeed = null, float? arenaSafetyMargin = null, bool preferStraightPaths = false, float? hardSafetyMargin = null, bool goalDirectedTemporalPlanning = false, bool noEscapeDelay = false, bool preferSafeRefuge = false, bool useMeasuredRunSpeed = false)
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
        if (enabled && goalDirectedTemporalPlanning) _timedGoalRequests.Add(owner); else _timedGoalRequests.Remove(owner);
        Setting.RuntimeGoalDirectedTemporalPlanning = _timedGoalRequests.Count != 0;
        if (enabled && noEscapeDelay) _noEscapeDelayRequests.Add(owner); else _noEscapeDelayRequests.Remove(owner);
        Setting.RuntimeNoEscapeDelay = _noEscapeDelayRequests.Count != 0;
        if (enabled && preferSafeRefuge) _safeRefugeRequests.Add(owner); else _safeRefugeRequests.Remove(owner);
        Setting.RuntimePreferSafeRefuge = _safeRefugeRequests.Count != 0;
        if (enabled && useMeasuredRunSpeed) _measuredSpeedRequests.Add(owner); else _measuredSpeedRequests.Remove(owner);
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
        float headingTolerance = 0, DirectTraversalMotion? motion = null, DateTime? deadline = null)
    {
        if (_disposed) return;
        if (Setting.MovementAllowed && target.HasValue && _movementRequests.Contains(owner) && _movementRequests.Count == 1)
        {
            _directOwner = _directFeedbackOwner = owner;
            _movement.SetDirectTraversal(target, now.AddSeconds(.25), path, headingTolerance, motion, now, deadline);
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
        _timedGoalRequests.Clear(); Setting.RuntimeGoalDirectedTemporalPlanning = false;
        _noEscapeDelayRequests.Clear(); Setting.RuntimeNoEscapeDelay = false;
        _safeRefugeRequests.Clear(); Setting.RuntimePreferSafeRefuge = false;
        _measuredSpeedRequests.Clear();
        _observedSpeed.Reset();
        PlanningSpeed = 0;
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
        terrain = new(Manager, new GameTerrainProbe(new NativeTerrainRaycaster()), new NativeTerrainSceneSource());
        hostMovement = movement;
        _movement = new(Setting, Manager, externalMovementEnabled, externalHasControl);
        _movement.CheckTerrainTraversal = ValidateTerrainTraversal;
        _movement.Attach(movement);
    }

    private bool ValidateTerrainTraversal(Vector3 from, Vector3 to)
    {
        if (LocalTerrain == null || terrain.ValidateStep(from, to)) return true;
        terrain.RejectRoute(PlayerPerspective.LivePlayer?.Position ?? from, MasterPlayerSettings.Instance.TerrainScanRadius, CurrentTime);
        return false;
    }

    internal void Update(float speedMultiplier, bool allowMovement)
    {
        if (_disposed) return;
        if (!allowMovement)
        {
            terrain.Clear();
            _observedSpeed.Reset();
            PlanningSpeed = 0;
            _movement.Update(null, default, CurrentTime, false);
            Manager.PruneExpiredAreas(CurrentTime - TimeSpan.FromSeconds(MathF.Max(0f, SolveOptionsDefaults.Default.DangerEndSafetyTime)));
            _previousPosition = null;
            return;
        }
        var baseSpeed = Setting.RuntimeBaseSpeed ?? MovementCapability.DefaultBaseSpeed;
        PlayerState? player = null;
        var actor = PlayerPerspective.LivePlayer is { IsDead: false } local ? local : null;
        if (actor == null || PlayerPerspective.IsReplay || !Setting.EffectiveEnableSafeZone || Manager.HasRegisteredArenaBoundary)
            terrain.Update(actor?.Position ?? default, 0, false, 20, CurrentTime);
        else
        {
            var settings = MasterPlayerSettings.Instance;
            terrain.Update(actor.Position, Svc.ClientState.TerritoryType,
                settings.EnableTerrainCollision,
                settings.TerrainScanRadius, CurrentTime,
                Svc.Condition[ConditionFlag.Jumping] || Svc.Condition[ConditionFlag.Jumping61],
                NativeTerrainRaycaster.CaptureLayoutIdentity());
            if (LocalTerrain != null && _movement.HasControl && hostMovement.GetStatus() is { IsRunning: true, CurrentPoint: { } point }
                && !terrain.ValidateStep(actor.Position, point.Position))
            {
                _movement.Reset();
                terrain.RejectRoute(actor.Position, settings.TerrainScanRadius, CurrentTime);
            }
        }
        var playerId = actor?.EntityId ?? 0;
        if (_previousPlayerId.HasValue && _previousPlayerId != playerId)
        {
            _movement.Reset();
            _observedSpeed.Reset();
            _previousPosition = null;
        }
        _previousPlayerId = playerId;
        // A script's explicit base speed stays authoritative. PT30 opts in to
        // confirmed faster motion so it can use short crossing windows.
        var useMeasuredRunSpeed = Setting.RuntimeBaseSpeed == null && _measuredSpeedRequests.Count != 0;
        if (actor != null) _observedSpeed.Record(CurrentTime, actor.Position, baseSpeed * speedMultiplier, useMeasuredRunSpeed);
        else _observedSpeed.Reset();
        if (Setting.RuntimeBaseSpeed == null && speedMultiplier > 0)
            baseSpeed = _observedSpeed.Effective(baseSpeed * speedMultiplier, useMeasuredRunSpeed) / speedMultiplier;
        var capability = MovementCapability.CaptureFromMainThread(speedMultiplier, baseSpeed);
        PlanningSpeed = capability.CurrentSpeed;
        if (actor != null)
        {
            var moving = _previousPosition is { } previous && Vector3.DistanceSquared(previous, actor.Position) > 0.0001f;
            player = PlayerState.CaptureFromMainThread(Manager.Version, capability.CurrentSpeed, moving);
            _previousPosition = actor.Position;
        }
        else _previousPosition = null;
        _movement.Update(player, capability, CurrentTime, allowMovement && !PlayerPerspective.IsDutyReplay);
        if (terrain.LastError != null && terrain.LastError != lastTerrainError)
            Svc.Log.Warning($"[PromeRadar] 地形查询失败，已暂停无场地移动：{terrain.LastError}");
        lastTerrainError = terrain.LastError;
    }

    public void Clear()
    {
        if (_disposed) return;
        terrain.Clear();
        ClearCombatState();
    }

    internal void ClearCombatState()
    {
        if (_disposed) return;
        Manager.ClearAllAreas();
        Manager.SetFallbackBoundary(terrain.Current);
        ClearMovementRequests();
        _movement.Reset();
        _previousPosition = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        terrain.Clear();
        ClearMovementRequests();
        try { _movement.Dispose(); }
        finally { Manager.Dispose(); }
    }
}

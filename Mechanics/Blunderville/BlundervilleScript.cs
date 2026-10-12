using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace MasterPlayer.Mechanics.Blunderville;

public abstract class BlundervilleScript : MasterPlayerScript
{
    private BlundervilleEncounter? _encounter;
    private BlundervilleOutput? _output;
    private DateTime _nextScan;
    private long _replayVersion;
    private Vector3? _player;
    private BlundervilleRaceState? _race;
    private BlundervilleRacePlanner? _planner;
    private BlundervilleNavigation? _navigation;
    private BlundervilleNativeControl? _native;
    private BlundervilleRaceDecision? _decision;
    private DateTime _lastTick, _nextDiagnostic;
    private BlundervilleBridgeTracker? _bridges;
    private BlundervilleBridgePlanner? _bridgePlanner;
    private BlundervilleJumpPlanner? _jumpPlanner;
    private IGeometryShape? _recoveryCorridor;
    private readonly BlundervilleJumpExecution _jumpExecution = new();
    private readonly BlundervilleArrowPlanner _arrowPlanner = new();
    private readonly BlundervilleBumperPlanner _bumperPlanner = new();
    private readonly BlundervilleFinalPlanner _finalPlanner = new();
    private readonly BlundervilleFinalPlanner _rampPlanner = new(BlundervilleFinalPlanner.FirstBRamp);
    private readonly BlundervilleFinalPlanner _swingerPlanner = new(BlundervilleFinalPlanner.FirstARamp);
    private readonly BlundervilleFinalPlanner _slopePlanner = new(BlundervilleFinalPlanner.FirstASlope);
    private readonly BlundervilleFinalPlanner _deliveryWest = new(BlundervilleFinalPlanner.DeliveryWest);
    private readonly BlundervilleFinalPlanner _deliveryEast = new(BlundervilleFinalPlanner.DeliveryEast);
    private readonly BlundervilleFinalPlanner _deliveryNorth = new(BlundervilleFinalPlanner.DeliveryNorth);
    private BlundervilleFinalPlanner[] LanePlanners => [_finalPlanner, _rampPlanner, _swingerPlanner, _slopePlanner, _deliveryWest, _deliveryEast, _deliveryNorth];

    [UserSetting("自动竞速（走位、跳跃与搬运）")]
    public bool AutomaticRace { get => Settings.GetBool(nameof(AutomaticRace), true); set => Settings.Set(nameof(AutomaticRace), value); }

    public override IEnumerable<string> GetMechanisms() => BlundervilleCatalog.Groups.Append("竞速路线与机制处理");

    protected override void OnStart()
    {
        _encounter = new(LogError);
        _output = new(SafeZone, Draw);
        _race = new(); _planner = new(); _native = new();
        _bridges = new(); _bridgePlanner = new();
        _jumpPlanner = new();
        _navigation = new(MasterPlayerPlugin.Instance!.SafeZone, SafeZone, Draw);
        SuppressGenericDodge(BlundervilleCatalog.ClaimedActions.ToArray());
        OnLogEvent(HandleEvent);
        Scan(CurrentTime);
        Render(CurrentTime);
    }

    protected override void OnUpdate(float deltaTime)
    {
        var now = CurrentTime;
        // Sample actual run-up every frame; 10Hz object scans miss launch windows.
        if (now < _nextScan && _nextScan - now <= TimeSpan.FromSeconds(0.1)
            && (ReplayWorld == null || ReplayWorld.Version == _replayVersion))
        {
            if (AutomaticRace && ReplayWorld == null && _native?.SupportsMisdirection == true && _decision is { DirectTraversal: true } && _race is { HasMisdirection: true }
                && IsMechanismEnabled("竞速路线与机制处理") && Svc.Objects.LocalPlayer is { IsDead: false } player)
            {
                _decision = _arrowPlanner.Apply(_decision, _race, player.Position, _native?.ReadArrowDirection(), now, _navigation?.HasMovementControl == true);
                _navigation?.ApplyDirectFrame(_race, _decision, now);
            }
            ApplyPendingJump(now); return;
        }
        Scan(now); Render(now);
    }

    protected override void OnStop()
    {
        _navigation?.Clear(); _native?.Dispose(); _planner?.Reset(); _race?.Reset();
        _output?.Clear(); _encounter?.Reset();
        _navigation = null; _native = null; _planner = null; _race = null; _decision = null;
        _lastTick = _nextDiagnostic = default;
        _bridges?.Reset(); _bridgePlanner?.Reset(); _bridges = null; _bridgePlanner = null; _recoveryCorridor = null;
        _jumpPlanner?.Reset(); _jumpPlanner = null;
        _output = null; _encounter = null; _player = null; _nextScan = default;
        _jumpExecution.Clear();
        _arrowPlanner.Reset();
        _bumperPlanner.Reset(); foreach (var lane in LanePlanners) lane.Reset();
    }

    private unsafe void Scan(DateTime now)
    {
        _nextScan = now.AddSeconds(0.1);
        if (_encounter == null) return;
        if (_lastTick != default && now < _lastTick)
        { _race?.Reset(); _planner?.Reset(); _navigation?.Clear(); _bridges?.Reset(); _bridgePlanner?.Reset(); _jumpPlanner?.Reset(); _arrowPlanner.Reset(); _bumperPlanner.Reset(); foreach (var lane in LanePlanners) lane.Reset(); _decision = null; _recoveryCorridor = null; _jumpExecution.Clear(); }
        _lastTick = now;
        if (ReplayWorld is { } world)
        {
            _replayVersion = world.Version;
            _player = world.LocalPlayer is { Dead: false } player ? player.Position : null;
            _race?.SynchronizeStatuses(world.LocalPlayer?.Statuses.Select(s => new BlundervilleRaceStatus(s.Id, s.Param, s.ExpiresAt)) ?? [], now);
            _encounter.Synchronize(world.Actors.Values.Where(a => BlundervilleCatalog.IsActor(a.Oid)).Select(a =>
                new BlundervilleActor(a.Id, a.Oid, a.Position, a.Dead,
                    a.Cast is { } c ? new(c.ActionId, c.Location, c.Rotation, c.StartedAt, c.FinishAt, a.Position) : null, a.Rotation, a.HitboxRadius)), now);
            return;
        }
        var actors = new List<BlundervilleActor>();
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == 0 || !BlundervilleCatalog.IsActor(obj.BaseId)) continue;
            var id = obj.EntityId is not (0 or 0xE0000000) ? obj.EntityId : obj.GameObjectId;
            if (id is 0 or 0xE0000000) continue;
            BlundervilleCast? cast = null;
            if (obj is IBattleChara battle && battle.IsCasting && BlundervilleCatalog.TryGet(battle.CastActionId, out _))
            {
                var native = (NativeBattleChara*)battle.Address;
                var location = native->CastInfo.TargetLocation;
                var remaining = Math.Max(0, battle.TotalCastTime - battle.CurrentCastTime);
                cast = new(battle.CastActionId, new(location.X, location.Y, location.Z),
                    float.IsFinite(native->CastRotation) ? native->CastRotation : null,
                    now.AddSeconds(-battle.CurrentCastTime), now.AddSeconds(remaining + 0.3), obj.Position);
            }
            actors.Add(new(id, obj.BaseId, obj.Position, obj.IsDead, cast, obj.Rotation, obj.HitboxRadius));
        }
        _player = Svc.Objects.LocalPlayer is { IsDead: false } livePlayer ? livePlayer.Position : null;
        _race?.SynchronizeStatuses(Svc.Objects.LocalPlayer?.StatusList.Select(s => new BlundervilleRaceStatus(s.StatusId, s.Param,
            Replay.ReplayStatus.Expiration(now, s.RemainingTime))) ?? [], now);
        _encounter.Synchronize(actors, now);
        _encounter.DeliverySchedule.SetStart(_race is { Course: BlundervilleCourse.Delivery, IsRacing: true, StartedByDirector: true } delivery
            ? delivery.StartedAt : null);
        _encounter.RingBeam.SetStart(_race is { Course: BlundervilleCourse.Ring, IsRacing: true, StartedByDirector: true } ring
            ? ring.StartedAt : null);
        if (_player is { } current && _race is { IsRacing: false, IsPrepared: false, Qualified: false } race)
        {
            var course = BlundervilleRoutes.FromPosition(current);
            var start = BlundervilleRoutes.VerifiedStart(course);
            if (course != BlundervilleCourse.None && !race.HasStatus(3782, now) && !race.HasStatus(3708, now)
                && (race.HasStatus(3704, now) || race.HasStatus(3694, now)
                    || Vector3.DistanceSquared(start, current) > 8 * 8 && actors.Any(a => a.Cast != null)))
                race.RecoverInProgress(course, now);
        }
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        if (_encounter == null) return;
        var now = CurrentTime;
        switch (ev)
        {
            case LogSystemDirectorUpdateEvent director:
                _race?.Director(director.Instance, director.Command, director.Param1, director.Param2, director.Param3, director.Param4, now);
                break;
            case LogSystemMapEffectEvent map:
                _bridges?.Map((byte)map.Position, map.Param1 | ((uint)map.Param2 << 16), now);
                _encounter.Map((byte)map.Position, map.Param1 | ((uint)map.Param2 << 16), now);
                break;
            case LogSystemCastStartEvent cast when BlundervilleCatalog.TryGet(cast.ActionId, out _):
                if (!float.IsFinite(cast.CastTime) || cast.CastTime < 0 || cast.CastTime > 3600) return;
                var started = cast.Timestamp.ToUniversalTime();
                _encounter.StartCast(cast.SourceId, new(cast.ActionId, cast.EffectPosition,
                    CastRotation.TryGet(cast, out var rotation) ? rotation : null,
                    started, started.AddSeconds(cast.CastTime + 0.3), cast.SourcePosition), now);
                break;
            case LogSystemActionEffectEvent effect:
                _encounter.Effect(effect.SourceId, effect.ActionId, effect.GlobalSequence,
                    effect.SourcePosition, effect.SourceRotation, now, effect.EffectPosition);
                break;
            case LogSystemCancelCastEvent cancel: _encounter.Cancel(cancel.SourceId, cancel.ActionId, cancel.Timestamp.ToUniversalTime()); break;
            case LogSystemInterruptCastEvent interrupt: _encounter.Cancel(interrupt.SourceId, interrupt.ActionId, interrupt.Timestamp.ToUniversalTime()); break;
            case LogSystemObjectRemoveEvent remove: _encounter.Remove(remove.SourceId); break;
        }
        // Coalesce high-rate helper effects; render once at the next 100ms scan.
    }

    private void Render(DateTime now)
    {
        if (_encounter != null) _output?.Apply(_encounter, now, _player, IsMechanismEnabled);
        if (_race == null || _planner == null || _encounter == null) return;
        if (_player is not { } player)
        {
            _navigation?.Clear(); _native?.Apply(now, false, false, null, null);
            _planner.Reset(); _bridgePlanner?.Reset(); _jumpPlanner?.Reset(); _recoveryCorridor = null;
            _decision = null; _jumpExecution.Clear(); _arrowPlanner.Reset(); _bumperPlanner.Reset(); foreach (var lane in LanePlanners) lane.Reset(); return;
        }
        var replay = ReplayWorld != null;
        var motion = default(BlundervilleNativeState);
        Vector3? executionTarget = null;
        if (!replay)
        {
            var desired = _decision?.Target ?? player;
            var runtime = MasterPlayerPlugin.Instance!.SafeZone;
            if (runtime.MovementStatus.HasControl && PromeRotation.Plugin.Instance.GreenMoveSystem.GetStatus().CurrentPoint is { } point)
                executionTarget = desired = point.Position;
            motion = _native?.Capture(now, desired - player) ?? default;
        }
        _decision = _planner.Update(_race, now, player, motion.Grounded, motion.Jumping, motion.GapAhead, motion.ArrowDirection,
            !replay && _native?.SupportsMisdirection == true, executionTarget);
        var automatic = AutomaticRace && IsMechanismEnabled("竞速路线与机制处理");
        if (automatic && !replay)
        {
            if (_native?.SupportsMisdirection == true)
                _decision = _arrowPlanner.Apply(_decision, _race, player, motion.ArrowDirection, now, _navigation?.HasMovementControl == true);
            else _arrowPlanner.Reset();
            _decision = _bumperPlanner.Apply(_decision, _race, _encounter, player, now, _navigation?.GetDirectTraversalExecution(now));
            _decision = BlundervilleDeliveryPlanner.Apply(_decision, _race, _encounter, player, now);
            _decision = BlundervilleSpinWeave.Apply(_decision, _race, player, _planner.Recovering);
            // Timed lane search on the Final slope, the FirstB pusher ramp, the
            // FirstA swinger ramp and the Delivery carry corridor of the
            // crystal's colour.
            var lanePlanner = _race.Course switch
            {
                BlundervilleCourse.Final => _finalPlanner,
                BlundervilleCourse.FirstB => _rampPlanner,
                // The cylinder/ice planner owns z -158..-208.
                BlundervilleCourse.FirstA => player.Z > -180 ? _swingerPlanner : player.Z < -209 ? _slopePlanner : null,
                BlundervilleCourse.Delivery => _race.CarryCrystalParam switch { 35 => _deliveryWest, 36 => _deliveryEast, 37 => _deliveryNorth, _ => null },
                _ => null
            };
            if (lanePlanner != null)
                _decision = lanePlanner.Apply(_decision, _race, _encounter.TraversalHazards(now).ToArray(), player, now,
                    _navigation?.GetDirectTraversalExecution(now));
            foreach (var lane in LanePlanners) if (lane != lanePlanner) lane.Reset();
        }
        else { _arrowPlanner.Reset(); _bumperPlanner.Reset(); foreach (var lane in LanePlanners) lane.Reset(); }
        // The slope corridor plans its own stand-still clear of the sweeps.
        if (_race.IsRacing && !_decision.Hold && !(_slopePlanner.HoldsBomb && !_race.MustStopForAccelerationBomb(now))
            && BlundervilleBombStop.StopEarly(_race, _encounter.TraversalHazards(now), player, now))
            _decision = _decision with { Hold = true, Jump = false, Hint = "加速度炸弹：在安全处提前停步" };
        if (automatic && !replay && _race.Course == BlundervilleCourse.Final && !_decision.DirectTraversal)
        {
            var solution = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus.LatestSolution;
            _decision = BlundervilleRefuge.Apply(_decision, _race, _encounter.TraversalHazards(now).ToArray(), player, now,
                solution is not { IsSuccess: true });
        }
        IGeometryShape? crossing = null;
        if (_race.Course == BlundervilleCourse.FirstA && _decision.Target is { } bridgeForward && _bridges != null)
        {
            if (_bridgePlanner?.Plan(_bridges, now, player, bridgeForward, motion.Grounded, motion.Jumping) is { } bridge)
            {
                _decision = _decision with { Target = bridge.Target, Hold = _decision.Hold || bridge.Hold,
                    Jump = !_decision.Hold && _race.CanJump(now) && bridge.Jump, Hint = bridge.Hint };
                crossing = bridge.CrossingCorridor;
            }
        }
        else _bridgePlanner?.Reset();
        if (_race.CanJump(now) && !_decision.Hold && !_planner.Recovering && _decision.Target is { } jumpForward)
        {
            if (_jumpPlanner?.Plan(_race.Course, now, player, jumpForward, motion.Grounded, motion.Jumping) is { } step)
            {
                _decision = _decision with { Target = step.Target, Hold = step.Hold, Jump = step.Jump, Hint = step.Hint };
                crossing = step.Corridor;
            }
        }
        else _jumpPlanner?.Reset();
        if (_planner.Recovering)
        {
            _recoveryCorridor = _planner.RecoveryCorridor;
            crossing = _recoveryCorridor;
        }
        else _recoveryCorridor = null;
        if (automatic && !replay && _race.CanJump(now) && (!_decision.Hold || _jumpExecution.InFlight))
        {
            if (_jumpExecution.Prepare(crossing as BlundervilleJumpCorridor, player) is { } jump)
            {
                _decision = _decision with { Target = jump.Target, Hold = false, Jump = false, Hint = jump.Hint };
                crossing = jump.Corridor;
            }
        }
        else { _jumpExecution.Clear(); _navigation?.ReleaseJump(); }
        _navigation?.Apply(_race, _decision, _encounter, now, player, automatic, replay, IsMechanismEnabled("竞速路线与机制处理"), crossing);
        _native?.Apply(now, automatic && _race.IsRacing && !replay,
            false, _decision.Facing, null);
        ApplyPendingJump(now);
        if (automatic && _race.IsRacing && now >= _nextDiagnostic)
        {
            _nextDiagnostic = now.AddSeconds(5);
            var movement = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus;
            Log($"糖豆人 {_race.Course}：{_decision.Hint}，节点{_planner.Cursor}，得分{_race.Score}/3，移动：{movement.PhaseText}，"
                + $"路点{movement.LatestSolution?.ExecutablePath.Count ?? 0}，累计下发{movement.DispatchCount}，"
                + $"解算{movement.AsyncSolveStartedCount}/接收{movement.AsyncSolveAcceptedCount}/取消{movement.AsyncSolveCancelledCount}");
        }
    }

    private void ApplyPendingJump(DateTime now)
    {
        if (!AutomaticRace || ReplayWorld != null
            || !IsMechanismEnabled("竞速路线与机制处理") || _race?.CanJump(now) != true
            || _decision is not { Hold: false } || Svc.Objects.LocalPlayer is not { IsDead: false } player)
        { _jumpExecution.Clear(); _navigation?.ReleaseJump(); return; }
        if (_jumpExecution.Corridor == null) return;
        var runtime = MasterPlayerPlugin.Instance!.SafeZone;
        var motion = _native?.Capture(now, (_jumpExecution.Landing ?? player.Position) - player.Position) ?? default;
        if (_jumpExecution.Sample(now, player.Position, motion.Grounded, motion.Jumping, runtime.MovementStatus.HasControl,
                PromeRotation.Plugin.Instance.GreenMoveSystem.GetStatus()) && _jumpExecution.Landing is { } landing)
        {
            if (_navigation?.CommitJump(landing, player.Position, _jumpExecution.MeasuredSpeed, now) == true
                && _native?.Apply(now, true, true, _decision.Facing, null) == true)
            {
                Log($"糖豆人起跳：{player.Position} → {landing}，实测助跑{_jumpExecution.MeasuredSpeed:F2}m/s");
                _jumpExecution.ConfirmLaunch(now);
            }
            else { _navigation?.ReleaseJump(); _jumpExecution.RejectLaunch(); }
        }
        if (!_jumpExecution.InFlight) _navigation?.ReleaseJump();
        if (_jumpExecution.NeedsRunupRetry)
        { _navigation?.StopForRunup(); _nextScan = now; }
    }
}

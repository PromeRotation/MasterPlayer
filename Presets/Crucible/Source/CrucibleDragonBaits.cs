// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 23:48:43: tether 17 -> 48809 (3.2s cast + NPC delay), then three
// 48810 snapshots 3.05-3.20s apart. Each snapshot leaves a R6 pool ~1.2-1.6s
// later. Confirm each new real pool before advancing to the next bait point.
sealed class CrucibleDragonBaits(BossModule module) : BossComponent(module)
{
    private bool _prepareNextRound;
    private const uint PoisonOID = 0x1EB704;
    private const float RouteRadius = 18.3f; // Keep pools 2/3 inside the 19.05y navigation boundary, including its 0.5y grid cells.
    private const float RouteHalfWidth = 7f; // R6 pools with centers 14y apart leave a 2y corridor.
    private const float RouteDepth = 7f; // 7y between pools 1/2 and 3/4.
    private const float BayMargin = 4f; // Past the outer row to the fence.
    private static readonly float RouteOuterOffset = MathF.Sqrt(RouteRadius * RouteRadius - RouteHalfWidth * RouteHalfWidth);
    private Actor? _source;
    private Actor? _target;
    private Actor? _previousTarget;
    private DateTime _prepareUntil;
    private DateTime _exitUntil;
    private WPos _exitGate;
    private bool _exitGatePassed;
    private int _step;
    private WPos[] _route = [];
    private WDir _north = new(0, -1);
    private DateTime _expires;
    private readonly HashSet<uint> _events = [];
    private readonly HashSet<ulong> _seenPools = [];
    private readonly Queue<(DateTime Earliest, DateTime Expires)> _pendingPools = [];
    private int _baitEffects;
    private bool _bayClosed;
    internal void Configure(bool prepareNextRound)
    {
        if (_prepareNextRound == prepareNextRound) return;
        _prepareNextRound = prepareNextRound;
        _prepareUntil = default;
        _exitUntil = default;
    }
    private bool Active => _target is { IsDeadOrDestroyed: false } && _source is { IsDeadOrDestroyed: false }
        && _step < 4 && WorldState.CurrentTime < _expires;

    private void Begin(Actor source, Actor target)
    {
        if (source.IsDeadOrDestroyed || target.IsDeadOrDestroyed)
            return;
        if (Active && _target == target && _source == source)
            return;
        _source = source;
        _target = target;
        _previousTarget = target;
        _prepareUntil = default;
        _exitUntil = default;
        _step = 0;
        _events.Clear();
        _pendingPools.Clear();
        _baitEffects = 0;
        _bayClosed = false;
        _seenPools.Clear();
        foreach (var pool in Module.Enemies(PoisonOID))
            _seenPools.Add(pool.InstanceID); // Existing objects, including inactive pools, belong to an earlier round.
        _expires = WorldState.FutureTime(16.5); // Include the opening cast and last snapshot's delayed pool.
        // Lock this round's north to the boss's position; active baits never rotate with it.
        BuildRoute(source);
    }

    private void BuildRoute(Actor source)
    {
        var offset = source.Position - Module.Center;
        // Near the center, retain the last valid route direction (world north on the first round).
        if (offset.LengthSq() > 0.01f)
            _north = offset.Normalized();
        if (_route.Length == 0)
            _route = new WPos[4];
        // Form a rectangle with 2/3 near the edge and 1/4 one row toward the center.
        var outer = Module.Center + _north * RouteOuterOffset;
        var inner = outer - _north * RouteDepth;
        var side = _north.OrthoL() * RouteHalfWidth;
        _route[0] = inner + side;
        _route[1] = outer + side;
        _route[2] = outer - side;
        _route[3] = inner - side;
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.NameID == 14628 && tether.ID == 17 && WorldState.Actors.Find(tether.Target) is { Type: ActorType.Player } target)
            Begin(source, target);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.NameID == 14628 && spell.Action.ID == 48809 && !spell.EventHappened
            && WorldState.Actors.Find(spell.TargetID) is { Type: ActorType.Player } target)
        {
            Begin(caster, target);
            _expires = Module.CastFinishAt(spell).AddSeconds(13);
        }
    }

    public override void Update()
    {
        if (!Active)
            _target = null;
        // Cast target is a safe late-load fallback; a lingering tether alone
        // cannot reveal how many of the four snapshots have already happened.
        if (_target == null)
            foreach (var caster in Module.Enemies(19672))
                if (caster.CastInfo is { Action.ID: 48809, EventHappened: false } spell && spell.NPCRemainingTime > 0)
                    OnCastStarted(caster, spell);
        // Preparation follows the upcoming first point; Begin freezes the route for all four pools.
        if (!Active && _prepareUntil > WorldState.CurrentTime && _source is { IsDeadOrDestroyed: false } source)
            BuildRoute(source);
        while (_pendingPools.TryPeek(out var expired) && WorldState.CurrentTime > expired.Expires)
            _pendingPools.Dequeue();
        var pools = Module.Enemies(PoisonOID);
        foreach (var pool in pools)
        {
            if (pool.EventState == 7 || pool.IsDeadOrDestroyed)
                continue;
            if (!_seenPools.Add(pool.InstanceID) || !Active)
                continue;
            // Each confirmed action owns at most one new object in its observed delivery window.
            // Pool placement can lock after the effect; neither current nor effect-time distance
            // identifies it reliably. Existing objects were excluded when this round began.
            if (!_pendingPools.TryPeek(out var pending) || WorldState.CurrentTime < pending.Earliest)
                continue;
            _pendingPools.Dequeue();
            if (_step == 3)
                Module.FindComponent<CrucibleDragonPoison>()?.ActivateImmediately(pool);
            if (++_step == 4)
            {
                // Leave via the middle of the two inner pools. Following the
                // boss on the outside can enter the narrow bay between four
                // pools, where the coarse navigation grid cannot find an exit.
                // This remains a proposal: runtime checks all actual obstacles
                // and cloud dangers before accepting either segment.
                if (_prepareNextRound)
                {
                    _exitGate = new((_route[0].X + _route[3].X) * 0.5f, (_route[0].Z + _route[3].Z) * 0.5f);
                    _exitGatePassed = false;
                    _bayClosed = (_source!.Position - _exitGate).Dot(_north) > 0;
                    // Only override following when the source is outside the
                    // inner row. A boss already in the center naturally leads
                    // out through the gate and needs no speculative detour.
                    if ((_source!.Position - _exitGate).Dot(_north) > 0)
                        _exitUntil = WorldState.FutureTime(5);
                }
                _target = null;
            }
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // The center landing hit precedes the next bait cast by ~4.8s. Return
        // toward the first bait point now: crossing from the far wall after the
        // tether alone is too late, and the old first pool is still present.
        if (caster.NameID == 14628 && spell.Action.ID == 48828 && _route.Length > 0)
        {
            _prepareUntil = WorldState.FutureTime(8);
            if (!Active)
                BuildRoute(caster);
        }
        if (!Active || caster != _source || spell.Action.ID != (_baitEffects == 0 ? 48809u : 48810u)
            || _baitEffects >= 4 || !_events.Add(spell.GlobalSequence))
            return;
        ++_baitEffects;
        // Associate the source's round, not MainTargetID: effects can target a pet.
        // The measured object delays are ~1.2-1.6s; tolerate delivery jitter without
        // manufacturing a pool or advancing before an actual object is observed.
        _pendingPools.Enqueue((WorldState.FutureTime(0.5), WorldState.FutureTime(3.1)));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == _source && spell.Action.ID == 48809 && spell.Interrupted)
            _target = null;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor == _source || actor == _target)
            _target = null;
        if (actor == _source || actor == _previousTarget)
        {
            _prepareUntil = default;
            _exitUntil = default;
        }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // run2033 t892-909: melee follow walked back into the 2y slot between the four pools because the boss stood at
        // its far end. Clouds then closed the only way out, the planner released and the player stood 12s until touched.
        // Melee reaches the boss from just outside the inner row. A breath pocket beyond the row is still walked to.
        if (_bayClosed && _source is { IsDeadOrDestroyed: false } boss && (boss.Position - _exitGate).Dot(_north) > 0
            && Module.FindComponent<CrucibleDragonWaves>()?.Active != true)
            hints.ForbiddenZones.Add(new(new SDRect(_exitGate, _north, RouteDepth + BayMargin, 0, RouteHalfWidth), default, null, ExactGeometry: true));
        if (!Active)
        {
            if (_prepareNextRound && actor == _previousTarget && !actor.IsDeadOrDestroyed && _exitUntil > WorldState.CurrentTime)
            {
                if (actor.Position.InCircle(Module.Center, 1))
                    _exitUntil = default;
                else
                {
                    _exitGatePassed |= actor.Position.InCircle(_exitGate, 0.65f);
                    hints.DirectRoute = _exitGatePassed
                        ? [Module.Center] : [_exitGate, Module.Center];
                    hints.GoalPoints.Add((Module.Center, 1f, 100));
                    return;
                }
            }
            if (_prepareNextRound && actor == _previousTarget && !actor.IsDeadOrDestroyed && _prepareUntil > WorldState.CurrentTime)
            {
                var idleDestination = _route[0];
                // This is only pre-positioning before the next tether, not a locked
                // pool placement. A tiny goal forces a fine grid across the arena
                // while old pools are still present and can delay the next solve.
                // Begin switches back to the precise goal for all four real baits.
                hints.GoalPoints.Add((idleDestination, 1f, 100));
            }
            return;
        }
        if (actor != _target)
            return;
        var destination = _route[_step];
        // A goal lets navigation approach the edge of last round's lingering
        // pool and enter as it ends, without requiring an obstructed endpoint.
        hints.GoalPoints.Add((destination, 0.2f, 100));
        // Start Sprint after the first real pool so its duration covers the
        // successive moves between bait points. The action queue handles cooldowns.
        if (_step > 0 && !actor.Position.InCircle(destination, 0.65f) && actor.FindStatus(50u) == null)
            hints.ActionHints.Add("连续放毒：建议使用疾跑");
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Active && actor == _target)
            hints.Add($"连线放毒 {_step + 1}/4：前往标记点，确认毒池生成后去下一点！", !actor.Position.InCircle(_route[_step], 0.65f));
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (!Active || pc != _target)
            return;
        for (var i = _step; i < 4; ++i)
        {
            Arena.ZoneCircleOutline(_route[i], 0.65f, i == _step ? Colors.SafeFromAOE : Colors.Object);
            if (i + 1 < 4)
                Arena.AddLine(_route[i], _route[i + 1], Colors.Object);
        }
        Arena.AddLine(pc.Position, _route[_step], Colors.SafeFromAOE);
        Arena.AddLine(_source!.Position, pc.Position, Colors.Danger);
    }
}

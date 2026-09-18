// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 23:48:43: tether 17 -> 48809 (3.2s cast + NPC delay), then three
// 48810 snapshots 3.05-3.20s apart. Each snapshot leaves a R6 pool ~1.2-1.6s
// later. Confirm each new real pool before advancing to the next bait point.
sealed class CrucibleDragonBaits(BossModule module) : BossComponent(module)
{
    private const uint PoisonOID = 0x1EB704;
    private const float RouteRadius = 18.3f; // Keep pools 2/3 inside the 19.05y navigation boundary, including its 0.5y grid cells.
    private const float RouteHalfWidth = 7f; // R6 pools with centers 14y apart leave a 2y corridor.
    private const float RouteDepth = 7f; // 7y between pools 1/2 and 3/4.
    private static readonly float RouteOuterOffset = MathF.Sqrt(RouteRadius * RouteRadius - RouteHalfWidth * RouteHalfWidth);
    private Actor? _source;
    private Actor? _target;
    private Actor? _previousTarget;
    private DateTime _prepareUntil;
    private int _step;
    private WPos[] _route = [];
    private WDir _north = new(0, -1);
    private DateTime _expires;
    private readonly HashSet<uint> _events = [];
    private readonly HashSet<ulong> _seenPools = [];
    private readonly List<(WPos Position, DateTime ExpiresAt, ulong TargetID)> _snapshots = [];
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
        _step = 0;
        _events.Clear();
        _snapshots.Clear();
        _seenPools.Clear();
        foreach (var pool in Module.Enemies(PoisonOID))
            if (pool.EventState != 7 && !pool.IsDeadOrDestroyed)
                _seenPools.Add(pool.InstanceID); // Existing pools belong to an earlier round.
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
        _snapshots.RemoveAll(s => WorldState.CurrentTime > s.ExpiresAt);
        var pools = Module.Enemies(PoisonOID);
        var advance = false;
        foreach (var pool in pools)
        {
            if (pool.EventState == 7 || pool.IsDeadOrDestroyed)
            {
                _seenPools.Remove(pool.InstanceID);
                continue;
            }
            if (!_seenPools.Add(pool.InstanceID) || !Active)
                continue;
            // Match the current target or its recorded snapshot. Unrelated/old pools
            // must not advance the route, and repeated observations count only once.
            if (pool.Position.InCircle(_target!.Position, 1f)
                || _snapshots.Any(s => s.TargetID == _target.InstanceID && pool.Position.InCircle(s.Position, 1f)))
            {
                advance = true;
                // Make the final pool dangerous as soon as routing releases the player.
                if (_step == 3)
                    Module.FindComponent<CrucibleDragonPoison>()?.ActivateImmediately(pool);
                // Only a newly observed matching pool consumes a snapshot.
                // Lingering pools from the previous round must not erase it.
                _snapshots.RemoveAll(s => s.TargetID == _target.InstanceID && pool.Position.InCircle(s.Position, 1f));
            }
        }
        if (advance)
        {
            ++_step;
            if (_step == 4)
                _target = null;
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
        if (caster.NameID != 14628 || spell.Action.ID is not (48809 or 48810) || !_events.Add(spell.GlobalSequence))
            return;
        var target = WorldState.Actors.Find(spell.MainTargetID)
            ?? (Active ? _target : null)
            ?? spell.Targets.Select(t => WorldState.Actors.Find(t.ID)).FirstOrDefault(a => a?.Type == ActorType.Player);
        if (target is { Type: ActorType.Player })
            // Correlate real pools even if the target moves after the hit.
            // Snapshots never draw circles or contribute AI danger zones.
            _snapshots.Add((target.Position, WorldState.FutureTime(3.1), target.InstanceID));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == _source && spell.Action.ID == 48809 && spell.Interrupted)
            _target = null;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == PoisonOID)
            _seenPools.Remove(actor.InstanceID);
        if (actor == _source || actor == _target)
            _target = null;
        if (actor == _source || actor == _previousTarget)
            _prepareUntil = default;
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!Active)
        {
            if (actor == _previousTarget && !actor.IsDeadOrDestroyed && _prepareUntil > WorldState.CurrentTime)
            {
                var idleDestination = _route[0];
                hints.GoalPoints.Add((idleDestination, 0.2f, 100));
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

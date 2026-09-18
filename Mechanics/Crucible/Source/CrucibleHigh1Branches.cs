// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Reuse event-object lifetimes, including already active pools on module load.
// Ice shares the R9 b0522 asset/placement used by The Ram's Keeper.
sealed class CrucibleIcePools(BossModule module) : Components.Voidzone(module, 9, GetPools)
{
    private static IEnumerable<Actor> GetPools(BossModule m) => m.Enemies(2004778).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}
sealed class CrucibleMorbolPools(BossModule module) : Components.Voidzone(module, 6, GetPools)
{
    private static IEnumerable<Actor> GetPools(BossModule m) => m.Enemies(2006848).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var pool in GetPools(Module))
            hints.TemporaryObstacles.Add(new SDCircle(pool.Position, 6.4f));
    }
}
sealed class CrucibleBombPools(BossModule module) : Components.Voidzone(module, 6, GetPools)
{
    private static IEnumerable<Actor> GetPools(BossModule m) => m.Enemies(2002331).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}

// The safe pad is selected by the chapter, not whichever object is nearest.
// Keep the player on the pad through impact: these transformation statuses are
// proximity effects and disappear again when the player leaves.
sealed class CrucibleOwlChapters(BossModule module) : Components.GenericAOEs(module)
{
    private uint _padOID;
    private uint _status;
    private uint _action;
    private ulong _caster;
    private DateTime _showAt;
    private DateTime _activation;
    private static readonly AOEShapeCircle Spot = new(6);
    private Actor[] Pads() => WorldState.CurrentTime >= _showAt && _activation.AddSeconds(1) >= WorldState.CurrentTime && _padOID != 0
        ? Module.Enemies(_padOID).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed).ToArray() : [];
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Action.ID is not (48657 or 48658 or 48659))
            return;
        (_padOID, _status) = spell.Action.ID switch { 48657 => (2004354u, 12u), 48658 => (2015457u, 1134u), _ => (2015456u, 1608u) };
        _action = spell.Action.ID;
        _caster = caster.InstanceID;
        // Show pads and enable guidance for the final 3s of the cast bar.
        // The NPC finish correction and hammer's impact delay only affect resolution.
        _showAt = WorldState.FutureTime(spell.RemainingTime - 3);
        _activation = Module.CastFinishAt(spell, spell.Action.ID == 48659 ? 0.8 : 0);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => Pads()
        .Select(p => new AOEInstance(Spot, p.Position, activation: _activation, color: Colors.SafeFromAOE, risky: false)).ToArray();
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Pads().Length > 0)
            hints.Add(_action switch { 48657 => "进入浮空区域，等爆震判定后再离开！", 48658 => "进入河童区域，等狂水判定后再离开！", _ => "进入变身区域应对魔法锤！" }, actor.FindStatus(_status) == null);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Status gain does not resolve the chapter. Leaving the pad removes
        // Levitate and previously made AI alternate between the pad and boss.
        var pad = Pads().MinBy(p => (p.Position - actor.Position).LengthSq());
        if (pad != null)
            hints.AddForbiddenConstraint(new SDInvertedCircle(pad.Position, Spot.Radius - 0.5f), _activation.AddSeconds(-0.8));
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (_action == 48659 ? 48660u : _action))
            _padOID = 0;
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == _action && spell.Interrupted)
            _padOID = 0;
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _caster)
            _padOID = 0;
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// 48671/48672 reveal -/+45 degree rotation before the first breath. Four
// followups have only 0.2s casts, 2.1s apart. Reconcile each actual helper cast
// without re-arming resolved steps or forbidding all five cones at once.
// ActionEffect8 (including zero-target misses) confirms 48673/48675 damage.
// 48674 is only the rotation visual and must not advance the damage sequence.
sealed class CrucibleMorbolBreath(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(50, 45f.Degrees());
    private readonly List<AOEInstance> _sequence = [];
    private readonly HashSet<uint> _events = [];
    private ulong _boss;
    private DateTime _first;
    private Angle _step;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened)
            return;
        var activation = Module.CastFinishAt(spell);
        if (spell.Action.ID is 48671 or 48672)
        {
            activation = activation.AddSeconds(0.5); // visual 4.2 + NPC finish 0.3 + helper delay 0.5 = 5s
            if (_boss == caster.InstanceID && Math.Abs((activation - _first).TotalSeconds) < 0.75)
                return;
            _boss = caster.InstanceID;
            _first = activation;
            _step = (spell.Action.ID == 48671 ? -45f : 45f).Degrees();
            _sequence.Clear();
            for (var i = 0; i < 5; ++i)
                _sequence.Add(new(Shape, spell.LocXZ, spell.Rotation + i * _step, activation.AddSeconds(i * 2.1)));
        }
        else if (spell.Action.ID is 48673 or 48675)
        {
            var index = _sequence.FindIndex(a => Math.Abs((a.Activation - activation).TotalSeconds) < 1);
            var actual = new AOEInstance(Shape, spell.LocXZ, spell.Rotation, activation);
            if (index >= 0)
                _sequence[index] = actual;
            else if (_sequence.Count == 0)
                _sequence.Add(actual); // actual cast still works on late loading
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _sequence.RemoveAll(a => a.Activation.AddSeconds(1) < WorldState.CurrentTime);
        return _sequence.Select((a, i) => a with { Risky = i == 0, Color = i == 0 ? Colors.Danger : Colors.AOE }).ToArray();
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not (48673 or 48675) || !_events.Add(spell.GlobalSequence))
            return;
        var index = _sequence.FindIndex(a => a.Activation <= WorldState.FutureTime(0.75) && a.Origin.AlmostEqual(caster.Position, 1));
        if (index >= 0)
            _sequence.RemoveAt(index);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            // 五连锥每 2.1 秒转 45°。原来只把“当前这一道”当危险区，AI 只能等这一道结算后才开始
            // 跑位；在 R11 上横移 45° 需要 8.6 米，慢速职业(4.2y/s)要 2.05 秒，加上延迟必然被下一道扫到。
            // 把 5 秒内会结算的锥（也就是下一道）一并交给寻路器，让它提前把两段一起规划。
            CrucibleScorpionAI.Avoid(hints, aoe);
        if (_sequence.Count > 1)
            // At R11 a 45-degree change costs <9y; it is traversable between
            // breaths even with the R6 pool occupying the middle of the arena.
            hints.AddForbiddenConstraint(new SDInvertedCircle(_sequence[0].Origin, 11), _sequence[0].Activation.AddSeconds(-0.5), _sequence[0].Activation.AddSeconds(0.6));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48671 or 48672 && spell.Interrupted)
            _sequence.Clear();
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _boss)
            _sequence.Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// Add targeting is left to the rotation. Only report the targeted missile;
// chasing autoattacks do not impose movement or range requirements.
sealed class CrucibleMorbolAdds(BossModule module) : BossComponent(module)
{
    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Module.Enemies(19644).Any(a => !a.IsDeadOrDestroyed && a.IsTargetable
            && a.CastInfo is { Action.ID: 48680 } cast && cast.TargetID == actor.InstanceID))
            hints.Add("粘液飞弹是锁定点名，注意减伤！", true);
    }
}

sealed class CrucibleMorbolKnockback(BossModule module) : HighCrucibleKnockback(module, 50758u, 10)
{
    private Components.GenericAOEs.AOEInstance[] Pools(int slot, Actor actor) => Module.FindComponent<CrucibleMorbolPools>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f) || Pools(slot, actor).Any(a => a.Check(pos));
    private sealed class Landing(WPos center, Knockback source, Components.GenericAOEs.AOEInstance[] pools) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var end = p + source.Distance * (p - source.Origin).Normalized();
            var clearance = 19.5f - (end - center).Length();
            foreach (var pool in pools)
                clearance = Math.Min(clearance, (end - pool.Origin).Length() - 6.5f);
            return clearance;
        }
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new Landing(Module.Center, kb, Pools(slot, actor)), kb.Activation.AddSeconds(-0.4));
    }
}

// The 48761 helper target is the receiving golem (a real actor), so its position
// predicts the jumped-to ring ~6s before impact. A helper's own position is stale.
sealed class CrucibleGolemJump(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeDonut Ring = new(5, 50);
    private AOEInstance? _prediction;
    private DateTime _ringSequence;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48763 ? new(Ring, true) : null;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && spell.Action.ID is 48759 or 48760)
        {
            _ringSequence = spell.Action.ID == 48759 ? Module.CastFinishAt(spell, 8) : default;
            _prediction = null;
        }
        if (spell.Action.ID == 48763)
            _prediction = null;
        base.OnCastStarted(caster, spell);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48761 && _ringSequence > WorldState.CurrentTime
            && WorldState.Actors.Find(spell.MainTargetID) is { IsDeadOrDestroyed: false } target)
            _prediction = new(Ring, target.Position, activation: WorldState.FutureTime(6), actorID: target.InstanceID);
        else if (spell.Action.ID == 48763)
            _prediction = null;
        base.OnEventCast(caster, spell);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_prediction is { } p && (p.Activation.AddSeconds(1) < WorldState.CurrentTime || WorldState.Actors.Find(p.ActorID) is not { IsDeadOrDestroyed: false }))
            _prediction = null;
        var actual = base.ActiveAOEs(slot, actor);
        return actual.Length > 0 ? actual : _prediction is { } prediction ? new[] { prediction } : [];
    }
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// Moving fire/snow bombs have no cast bar; remaining close is unsafe throughout
// their approach. Keep their current explosion reach as a proximity warning and
// clear immediately on detonation. This does not assume a fixed spawn-to-fuse time.
sealed class CrucibleMovingBombs(BossModule module) : Components.GenericAOEs(module)
{
    private readonly HashSet<ulong> _resolved = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => Module.Enemies(19668).Concat(Module.Enemies(19669))
        .Where(a => !a.IsDeadOrDestroyed && !_resolved.Contains(a.InstanceID))
        .Select(a => new AOEInstance(new AOEShapeCircle(a.OID == 19668 ? 6 : 10), a.Position, actorID: a.InstanceID)).ToArray();
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48792 or 48794)
            _resolved.Add(caster.InstanceID);
    }
    public override void OnActorDestroyed(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

sealed class CrucibleBombMeltdown(BossModule module) : Components.GenericAOEs(module, 48801u)
{
    private AOEInstance? _aoe;
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48800)
            _aoe = new(new AOEShapeRect(40, 6), caster.Position, spell.Rotation, WorldState.FutureTime(0.9));
        else if (spell.Action.ID == WatchedAction)
            _aoe = null;
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _aoe is { } a && a.Activation.AddSeconds(1) >= WorldState.CurrentTime ? new[] { a } : [];
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19666)
            _aoe = null;
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}
sealed class CrucibleBombEnrageHint(BossModule module) : Components.CastHints(module, [48790u], "榴弹怪连续大爆炸：优先处理榴弹怪，注意减伤！");

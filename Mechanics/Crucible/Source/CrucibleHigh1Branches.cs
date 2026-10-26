// See docs/reference/CrucibleAll.spec.md.
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
// predicts the receiving golem's circle/ring ~6s before impact. The helper's
// own position is stale. 48759 selects a ring; 48760 selects an R20 circle.
sealed class CrucibleGolemJump(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeDonut Ring = new(5, 50);
    private static readonly AOEShapeCircle Circle = new(20);
    private AOEInstance? _prediction;
    private DateTime _sequenceExpires;
    private AOEShape? _shape;
    private ulong _caster;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48763 => new(Ring, true),
        48765 => new(Circle, true),
        _ => null
    };
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && spell.Action.ID is 48759 or 48760)
        {
            _sequenceExpires = Module.CastFinishAt(spell, 8);
            _shape = spell.Action.ID == 48759 ? Ring : Circle;
            _caster = caster.InstanceID;
            _prediction = null;
        }
        if (spell.Action.ID is 48763 or 48765)
            _prediction = null;
        base.OnCastStarted(caster, spell);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48761 && _sequenceExpires > WorldState.CurrentTime && _shape != null
            && WorldState.Actors.Find(spell.MainTargetID) is { IsDeadOrDestroyed: false } target)
            _prediction ??= new(_shape, target.Position, activation: WorldState.FutureTime(6), actorID: target.InstanceID);
        else if (spell.Action.ID is 48763 or 48765)
        {
            _prediction = null;
            _shape = null;
        }
        base.OnEventCast(caster, spell);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48759 or 48760 && spell.Interrupted)
        {
            _prediction = null;
            _shape = null;
        }
        base.OnCastFinished(caster, spell);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        // After the handoff, the receiving golem owns the attack. Killing the
        // original caster must not erase the surviving receiver's warning.
        if (actor.InstanceID == (_prediction?.ActorID ?? _caster))
        {
            _prediction = null;
            _shape = null;
        }
        base.OnActorDestroyed(actor);
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
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

// User strategy: let the rotation kill the chasing bombs and accept their burst.
// Keep an informational outline, but never kite them or force add targeting.
sealed class CrucibleMovingBombs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Small = new(6), Large = new(10);
    private readonly HashSet<ulong> _resolved = [];
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => Module.Enemies(19668).Concat(Module.Enemies(19669))
        .Where(a => !a.IsDeadOrDestroyed && !_resolved.Contains(a.InstanceID))
        .Select(a => new AOEInstance(a.OID == 19668 ? Small : Large, a.Position, risky: false, actorID: a.InstanceID)).ToArray();
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 48792 or 48794)
            _resolved.Add(caster.InstanceID);
    }
    public override void OnActorDestroyed(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void OnActorCreated(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (var aoe in ActiveAOEs(pcSlot, pc))
            aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, Colors.AOE);
    }
}

sealed class CrucibleBombMeltdown(BossModule module) : Components.GenericAOEs(module, 48801u)
{
    private static readonly AOEShapeRect Shape = new(40, 6);
    private AOEInstance? _aoe;
    private Actor? _boss;
    private Actor? _target;
    private DateTime _activation;
    private bool _locked;

    // ARR 15:46:32: icon 412 -> bind 48800 ~7.2s -> hit 48801 ~0.96s.
    // The marked player cannot dodge after the bind; prepare their landing early.
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID == 412 && Module.Enemies(19666).FirstOrDefault(a => !a.IsDeadOrDestroyed) is { } boss)
        {
            _boss = boss;
            _target = actor;
            _activation = WorldState.FutureTime(8.2);
            _locked = false;
            _aoe = null;
        }
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48800)
        {
            _boss = caster;
            _target = WorldState.Actors.Find(spell.MainTargetID);
            _activation = WorldState.FutureTime(0.95);
            _locked = true;
            // Boss facing in the bind packet can lag behind the real target.
            var rotation = _target != null ? Angle.FromDirection(_target.Position - caster.Position) : spell.Rotation;
            _aoe = new(Shape, caster.Position, rotation, _activation);
        }
        else if (spell.Action.ID == WatchedAction)
            Clear();
    }
    private void Clear()
    {
        _aoe = null;
        _boss = _target = null;
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_boss == null || _boss.IsDeadOrDestroyed || _target is { IsDeadOrDestroyed: true } || _activation.AddSeconds(1) < WorldState.CurrentTime)
        {
            Clear();
            return [];
        }
        if (!_locked && _target != null)
            _aoe = new(Shape, _boss.Position, Angle.FromDirection(_target.Position - _boss.Position), _activation);
        return _aoe is { } aoe ? new[] { aoe with { Risky = actor != _target } } : [];
    }
    public Components.GenericKnockback.Knockback? KnockbackFor(int slot, Actor actor)
    {
        var aoes = ActiveAOEs(slot, actor);
        return aoes.Length > 0 ? new(aoes[0].Origin, 15, _activation,
            shape: actor == _target ? null : Shape, direction: aoes[0].Rotation) : null;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor == _boss || actor == _target)
            Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

sealed class CrucibleBombMeltdownKnockback(BossModule module) : Components.GenericKnockback(module)
{
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
        => Module.FindComponent<CrucibleBombMeltdown>()?.KnockbackFor(slot, actor) is { } source ? new[] { source } : [];

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f)
        || (Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray().Any(a => a.Check(pos)) ?? false);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var pools = Module.FindComponent<CrucibleBombPools>()?.ActiveAOEs(slot, actor).ToArray() ?? [];
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                // Prepare before 48800 binds the target, not only before damage.
                hints.AddForbiddenConstraint(new Landing(Module.Center, kb, pools), kb.Activation.AddSeconds(-1.5));
    }

    private sealed class Landing(WPos center, Knockback source, Components.GenericAOEs.AOEInstance[] pools) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            if (source.Shape != null && !source.Shape.Check(p, source.Origin, source.Direction))
                return 1;
            var landing = AwayFromSource(p, source.Origin, source.Distance);
            var clearance = Math.Min((p - source.Origin).Length() - 0.5f, 19.5f - (landing - center).Length());
            foreach (var pool in pools)
                clearance = Math.Min(clearance, (landing - pool.Origin).Length() - 6.35f);
            return clearance;
        }
    }
}
sealed class CrucibleBombEnrageHint(BossModule module) : Components.CastHints(module, [48790u, 48802u], "大爆炸：全场伤害，注意减伤！");

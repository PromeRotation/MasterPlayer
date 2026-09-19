// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Normal second board, ARR 2026-09-10 22:14:09. All cast geometry uses the
// packet's location/rotation: several helpers have not moved to that location yet.
class Low2CrucibleCastAOEs(BossModule module, Battle battle) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => (battle, actionID) switch
    {
        (Battle.B09, 48167) => new(new AOEShapeRect(40, 5), true),
        (Battle.B09, 48172) => new(new AOEShapeCircle(6), true),
        (Battle.B09, 48175) => new(new AOEShapeCone(60, 90f.Degrees()), true),
        (Battle.B09, 48178) => new(new AOEShapeCone(25, 30f.Degrees()), true),
        (Battle.B09, 48180) => new(new AOEShapeCircle(8), true),
        (Battle.B10, 48182) => new(new AOEShapeCircle(10), true),
        (Battle.B10, 48186) => new(new AOEShapeCircle(18), true),
        (Battle.B11, 48206) => new(new AOEShapeCircle(8), true),
        (Battle.B13, 48227) => new(new AOEShapeCone(60, 45f.Degrees()), true),
        (Battle.B13, 48229) => new(new AOEShapeCircle(8), true),
        (Battle.B13, 48519) => new(new AOEShapeCircle(6), true),
        _ => null
    };

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// A cast finishing is not the end of Liquid Hell. Bridge its ~0.9s spawn delay,
// then let the actual b0740 objects own the lifetime (EventState 7 ends it).
sealed class CrucibleLiquidHell(BossModule module) : Components.Voidzone(module, 6f,
    m => m.Enemies(2008685).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed))
{
    private readonly Dictionary<ulong, (WPos pos, DateTime expires)> _bridges = [];
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48172)
            _bridges[caster.InstanceID] = (caster.CastInfo?.LocXZ ?? caster.Position, WorldState.FutureTime(3));
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var result = base.ActiveAOEs(slot, actor).ToArray().ToList();
        foreach (var (id, bridge) in _bridges.ToArray())
            if (bridge.expires < WorldState.CurrentTime || result.Any(a => a.Origin.AlmostEqual(bridge.pos, 0.5f)))
                _bridges.Remove(id);
            else
                result.Add(new(Shape, bridge.pos));
        return CollectionsMarshal.AsSpan(result);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            hints.TemporaryObstacles.Add(new SDCircle(aoe.Origin, 6.25f));
    }
}

// User strategy: kill zombies rather than kite them. Keep target information,
// but never publish their bodies or movement as navigation hazards.
sealed class CrucibleZombies(BossModule module) : BossComponent(module)
{
    private IEnumerable<Actor> Sources => Module.Enemies(19549).Where(a => !a.IsDeadOrDestroyed);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var target = Sources.OrderBy(a => (a.Position - actor.Position).LengthSq()).FirstOrDefault();
        if (target != null) hints.SetPriority(target, 1);
    }
    public override void AddGlobalHints(GlobalHints hints)
    {
        if (Sources.Any()) hints.Add("优先击杀僵尸");
    }
}

// The tornado action has no documented damage radius. Retain the existing
// conservative 2y contact envelope; it is not an Action-sheet AOE radius.
sealed class CrucibleTornadoes(BossModule module) : CrucibleMovingContact(module, 2f, 3f, [19545, 19546]);

// Contact actors can turn to their victim when striking. Measure displacement,
// not facing, and drop the forecast when they stop or teleport. Analytic circles
// and rectangles avoid resampling an 84x84 field for every moving actor/frame.
abstract class CrucibleMovingContact(BossModule module, float radius, float lookAhead, uint[] oids) : Components.GenericAOEs(module)
{
    private readonly record struct Motion(WPos Position, DateTime SampledAt, WDir Velocity);
    private readonly Dictionary<ulong, Motion> _motion = [];
    private readonly List<AOEInstance> _display = [];
    public IEnumerable<Actor> Sources => WorldState.Actors.Where(a => oids.Contains(a.OID) && !a.IsDeadOrDestroyed);

    public override void Update()
    {
        var seen = new HashSet<ulong>();
        foreach (var source in Sources)
        {
            seen.Add(source.InstanceID);
            var velocity = default(WDir);
            if (_motion.TryGetValue(source.InstanceID, out var previous))
            {
                var dt = (WorldState.CurrentTime - previous.SampledAt).TotalSeconds;
                if (dt < 0.05) continue;
                var delta = source.Position - previous.Position;
                if (dt <= 0.5 && delta.LengthSq() >= 0.0025f && delta.Length() / dt <= 12)
                    velocity = delta / (float)dt;
            }
            _motion[source.InstanceID] = new(source.Position, WorldState.CurrentTime, velocity);
        }
        foreach (var id in _motion.Keys.Where(id => !seen.Contains(id)).ToArray()) _motion.Remove(id);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _display.Clear();
        foreach (var source in Sources)
        {
            _display.Add(new(new AOEShapeCircle(radius), source.Position, actorID: source.InstanceID));
            if (!_motion.TryGetValue(source.InstanceID, out var motion) || (WorldState.CurrentTime - motion.SampledAt).TotalSeconds > 0.5)
                continue;
            var length = Math.Min(lookAhead, motion.Velocity.Length() * 1.1f);
            if (length < 0.05f) continue;
            var direction = motion.Velocity.Normalized();
            _display.Add(new(new AOEShapeRect(length, radius), source.Position, Angle.FromDirection(direction), actorID: source.InstanceID));
            _display.Add(new(new AOEShapeCircle(radius), source.Position + length * direction, actorID: source.InstanceID));
        }
        return CollectionsMarshal.AsSpan(_display);
    }

    public override void OnActorDestroyed(Actor actor) => _motion.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Four stationary miasmas pulse R8 every ~5.1s. The first warning starts at
// spawn, 3.9s before damage, instead of waiting for the 0.7s cast each time.
sealed class CrucibleNecromist(BossModule module) : Components.Voidzone(module, 8f,
    m => m.Enemies(19550).Where(a => !a.IsDeadOrDestroyed));

sealed class CrucibleTyphoon(BossModule module) : HighCrucibleKnockback(module, 48168u, 10f)
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        => !pos.InRect(Module.Center, 19.5f, 14.5f) || LandingHazards(slot, actor, WorldState.CurrentTime).Any(a => a.Check(pos));

    private Components.GenericAOEs.AOEInstance[] LandingHazards(int slot, Actor actor, DateTime activation)
        => Module.Components.OfType<Components.GenericAOEs>().SelectMany(c => c.ActiveAOEs(slot, actor).ToArray())
            .Where(a => a.Risky && (a.Activation == default || a.Activation >= activation.AddSeconds(-0.6) && a.Activation <= activation.AddSeconds(0.8))).ToArray();

    private sealed class Landing(WPos origin, WPos center, ShapeDistance[] hazards) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var delta = p - origin;
            var end = p + delta.Normalized() * 10f;
            if (delta.LengthSq() < 0.01f || !end.InRect(center, 19.5f, 14.5f))
                return -1;
            foreach (var hazard in hazards)
                if (hazard.Distance(end) <= 0.5f)
                    return -1;
            return 1;
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
            {
                hints.AddForbiddenConstraint(new Landing(kb.Origin, Module.Center, LandingHazards(slot, actor, kb.Activation)
                    .Select(a => a.ShapeDistance ?? a.Shape.Distance(a.Origin, a.Rotation)).ToArray()), kb.Activation.AddSeconds(-0.4d));
            }
    }
}

// Paired R23 circles are 6s apart; opposite 60x60 sweeps are 3s apart.
// Only the current wave blocks navigation. Stage within 2y of its edge so the
// cleared region can be reached immediately, rather than running to a far corner.
sealed class CrucibleMinotaurSequence(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override double RiskyActivationWindow => 0.5d;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48194 or 48196 => new(new AOEShapeCircle(23), true),
        48190 or 48192 => new(new AOEShapeRect(60, 30), true),
        _ => null
    };

    private sealed class OutsideEdge(ShapeDistance shape, float width) : ShapeDistance
    {
        public override float Distance(in WPos p) => width - shape.Distance(p);
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        foreach (ref readonly var aoe in aoes)
            CrucibleScorpionAI.Avoid(hints, aoe);
        if (aoes.Length > 1 && !aoes[1].Risky)
        {
            var first = aoes[0];
            hints.AddForbiddenConstraint(new OutsideEdge(first.Shape.Distance(first.Origin, first.Rotation), 2f), first.Activation.AddSeconds(-1), first.Activation.AddSeconds(0.6));
            // Prefer the part of that edge closest to the next safe region.
            var next = aoes[1].Shape.Distance(aoes[1].Origin, aoes[1].Rotation);
            hints.GoalZones.Add(p => Math.Clamp(30f + next.Distance(p), 0f, 60f));
        }
    }
}

// Icon 167/168 gives the rotation direction before the initial cast. Subsequent
// 0.2s casts are too late for movement; predict the next two 30-degree steps and
// reconcile every actual cast. Twelve steps complete the recorded revolution.
sealed class CrucibleMinotaurRotation(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Shape = new(40, 30f.Degrees());
    private Angle _step;
    private AOEInstance? _next;
    private int _remaining;
    private readonly HashSet<uint> _events = [];
    private readonly List<AOEInstance> _display = [];

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (actor.NameID is 14555 or 14556 && iconID is 167 or 168)
            _step = (iconID == 167 ? -30f : 30f).Degrees();
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not (48210 or 48212) || spell.EventHappened)
            return;
        if (spell.Action.ID == 48210)
        {
            _remaining = 13;
            _events.Clear();
        }
        else if (_next is { } predicted)
        {
            var diff = (spell.Rotation - predicted.Rotation).Normalized();
            if (Math.Abs(diff.Deg) > 5 && Math.Abs(diff.Deg) < 65)
                _step = diff.Deg < 0 ? -30f.Degrees() : 30f.Degrees();
        }
        // Late load without an icon still draws the real cast; don't guess a spin direction.
        _next = new(Shape, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _display.Clear();
        if (_next is not { } next || next.Activation.AddSeconds(2) < WorldState.CurrentTime)
            return [];
        _display.Add(next);
        if (_step != default)
            for (var i = 1; i < Math.Min(3, _remaining); ++i)
                _display.Add(new(Shape, next.Origin, next.Rotation + _step * i, next.Activation.AddSeconds(1.62d * i), Colors.AOE));
        return CollectionsMarshal.AsSpan(_display);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not (48210 or 48212) || _next is not { } next || !_events.Add(spell.GlobalSequence))
            return;
        ++NumCasts;
        if (--_remaining <= 0 || _step == default)
            _next = null;
        else
            _next = new(Shape, next.Origin, next.Rotation + _step, WorldState.FutureTime(1.62d), actorID: caster.InstanceID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48210 or 48212 && spell.Interrupted)
        {
            _next = null;
            _remaining = 0;
            _events.Clear();
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (_next?.ActorID == actor.InstanceID)
            _next = null;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
        if (_next is { } next && next.Activation.AddSeconds(2) >= WorldState.CurrentTime)
            hints.AddForbiddenConstraint(new SDInvertedCircle(next.Origin, 8f), next.Activation.AddSeconds(-0.5d), next.Activation.AddSeconds(0.6));
    }
}

// Six b4221 sandpits are fixed EObjects. Radius comes from their 50452 damage
// action, not the visible sand plume. Never clear them on a breath cast finish.
sealed class CrucibleSandpits(BossModule module) : Components.Voidzone(module, 6f,
    m => m.Enemies(2015270).Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed));

// Bombs can be moved and destroyed before detonation. AnimState1=1 starts the
// fuse ~7s before impact. Before this tell they are movable, not armed AOEs;
// blocking all four from spawn would eliminate the room's safe destinations.
sealed class CrucibleGoblinBombs(BossModule module) : Components.GenericAOEs(module, 48235u)
{
    private readonly HashSet<ulong> _resolved = [];
    private readonly Dictionary<ulong, DateTime> _ignited = [];
    private static readonly AOEShapeCircle Shape = new(12);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var result = new List<AOEInstance>();
        foreach (var bomb in Module.Enemies(19561))
            if (!bomb.IsDeadOrDestroyed && !_resolved.Contains(bomb.InstanceID)
                && (bomb.ModelState.AnimState1 == 1 || bomb.CastInfo?.Action.ID == WatchedAction))
                result.Add(new(Shape, bomb.Position, activation: bomb.CastInfo is { } cast ? Module.CastFinishAt(cast) : _ignited.GetValueOrDefault(bomb.InstanceID)));
        return CollectionsMarshal.AsSpan(result);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
            _resolved.Add(caster.InstanceID);
    }
    public override void OnActorModelStateChange(Actor actor, byte modelState, byte animState1, byte animState2)
    {
        if (actor.OID == 19561 && animState1 == 1)
            _ignited.TryAdd(actor.InstanceID, WorldState.FutureTime(7.1d));
    }
    public override void OnActorCreated(Actor actor)
    {
        _resolved.Remove(actor.InstanceID);
        _ignited.Remove(actor.InstanceID);
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// Sand pillars travel at 5y/0.3s between the six pits. Once two impacts identify
// a line, warn along its remaining path; only continue to an observed pit on that
// line, never extrapolate across arbitrary space. A gap resets the route.
sealed class CrucibleSandPillars(BossModule module) : Components.GenericAOEs(module, 48225u)
{
    private WPos? _last;
    private DateTime _lastTime;
    private AOEInstance? _route;
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != WatchedAction)
            return;
        var pos = caster.Position;
        if (_last is { } last && (WorldState.CurrentTime - _lastTime).TotalSeconds is > 0.15d and < 0.6d)
        {
            var delta = pos - last;
            if (delta.LengthSq() is > 16f and < 36f)
            {
                var dir = delta.Normalized();
                var end = Module.Enemies(2015270).Where(a => !a.IsDeadOrDestroyed)
                    .Select(a => a.Position).Where(p => (p - pos).Dot(dir) > 0 && Math.Abs((p - pos).Cross(dir)) < 1f)
                    .OrderBy(p => (p - pos).LengthSq()).FirstOrDefault();
                if (end != default)
                    _route = new(new AOEShapeRect((end - pos).Length() + 4, 4, 4), pos, Angle.FromDirection(dir), WorldState.FutureTime(0.3d));
            }
        }
        else
            _route = null;
        _last = pos;
        _lastTime = WorldState.CurrentTime;
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _route is { } route && (WorldState.CurrentTime - _lastTime).TotalSeconds < 0.7d ? new[] { route } : [];
}

sealed class CrucibleNecromancerHint(BossModule module) : Components.CastHints(module, [48183u], "全体伤害；优先击杀僵尸，避开持续瘴气！");
sealed class CrucibleMinotaurEnrageHint(BossModule module) : Components.CastHints(module, [48214u], "无尽挥打覆盖全场：尽快击败牛魔！");
sealed class CrucibleWormQuakeHint(BossModule module) : Components.CastHints(module, [48239u], "连续全体地震：治疗并尽快击败小地豆！");

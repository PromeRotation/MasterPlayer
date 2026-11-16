// See docs/reference/CrucibleAll.spec.md.
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
        (Battle.B11, 48198 or 48201) => new(new AOEShapeCircle(5), true),
        (Battle.B11, 48204) => new(new AOEShapeRect(65, 4)),
        (Battle.B11, 48206) => new(new AOEShapeCircle(8), true),
        (Battle.B13, 48227) => new(new AOEShapeCone(60, 45f.Degrees()), true),
        (Battle.B13, 48229) => new(new AOEShapeCircle(8), true),
        (Battle.B13, 48231) => new(new AOEShapeDonut(4, 40), true),
        (Battle.B13, 48233) => new(new AOEShapeCircle(8), true),
        (Battle.B13, 48236) => new(new AOEShapeCross(40, 4), true),
        (Battle.B13, 48237) => new(new AOEShapeCircle(6), true),
        (Battle.B13, 48519) => new(new AOEShapeCircle(6), true),
        _ => null
    };

    // B13 48233：18 个 R8 圆同时读 4.7s。ARR 01.49.45 t945.92：人在离圆心 6.36 处被打中，MP 日志在 3.6s/2.6s 两次
    // NoTemporalPath，剩 1.64s 才「等待撤离时机」出发，差约 2m。圆间安全缝离圆心仅约 8.4–8.8（ARR 21.52.36 t1011.16
    // 站在离最近圆心 8.41 处未中）；Avoid 的 +0.35、危险外扩 0.5 和求解器硬余量 0.5 叠成 9.35，把缝全部堵死。
    // 半径 8 由 ARR 核对（最远命中 6.36，最近未中 8.41），这里只用精确几何，不再外扩；圆数多时立刻撤离。
    public const uint FieldCircles = 48233;
    public const int DenseFieldCircles = 6;
    // 48229（尾王自身 R8 回旋剑）也是 R8 圆，但施放者是尾王本体；48233 的施放者是 9020 辅助体。
    private bool IsFieldCircle(in AOEInstance aoe)
    {
        foreach (var boss in Module.Enemies(19557))
            if (boss.InstanceID == aoe.ActorID) return false;
        return true;
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var field = 0;
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            if (battle == Battle.B13 && aoe.Shape is AOEShapeCircle { Radius: 8 } && IsFieldCircle(aoe)) ++field;
        if (field >= DenseFieldCircles) hints.NoEscapeDelay = true;
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
        {
            if (battle == Battle.B13 && aoe.Shape is AOEShapeCircle { Radius: 8 } && IsFieldCircle(aoe))
            {
                hints.ForbiddenZones.Add(new(aoe.Shape.Distance(aoe.Origin, aoe.Rotation),
                    aoe.Activation == default ? default : aoe.Activation.AddSeconds(-0.25d),
                    aoe.Activation == default ? null : aoe.Activation.AddSeconds(0.6), ExactGeometry: true));
                continue;
            }
            CrucibleScorpionAI.Avoid(hints, aoe);
            // B10 zombies can cause confusion before Dark Orb resolves. Leaving
            // at the last safe instant can therefore leave the player unable to
            // move. Start the escape while player control is still available.
            if (battle == Battle.B10 && aoe.Shape is AOEShapeCircle { Radius: 18 })
                hints.NoEscapeDelay = true;
        }
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
    public virtual IEnumerable<Actor> Sources => WorldState.Actors.Where(a => oids.Contains(a.OID) && !a.IsDeadOrDestroyed);
    protected virtual bool KeepMotionBetweenPositionUpdates => false;
    protected virtual float MotionLookAheadSeconds => 1.1f;

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
                // B11 native positions arrive as discrete updates. An
                // unchanged sample between them is not a measured stop.
                // Keep at most 0.5s of already observed motion; other moving
                // hazards retain their existing sampling policy.
                if (KeepMotionBetweenPositionUpdates && dt <= 0.5 && delta.LengthSq() < 0.0025f)
                    continue;
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
            var length = Math.Min(lookAhead, motion.Velocity.Length() * MotionLookAheadSeconds);
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

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        foreach (ref readonly var aoe in aoes)
            if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
        if (aoes.Length > 1 && !aoes[1].Risky)
        {
            var first = aoes[0];
            var edge = first.Shape.Distance(first.Origin, first.Rotation);
            // Edge staging is a preference, not an additional mechanic. A hard
            // two-yalm band becomes sub-cell after navigation margins and can
            // reject the otherwise reachable safe crescent entirely.
            var next = aoes[1].Shape.Distance(aoes[1].Origin, aoes[1].Rotation);
            hints.GoalZones.Add(p => edge.Distance(p) is >= 1f and <= 3f
                ? Math.Clamp(150f + 5f * next.Distance(p), 100f, 200f) : 0f);
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
        {
            _next = null;
            // Each Endless Swipes chain announces its own direction; never reuse the previous one.
            if (_remaining <= 0) _step = default;
        }
        else
            _next = new(Shape, next.Origin, next.Rotation + _step, WorldState.FutureTime(1.62d), actorID: caster.InstanceID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48210 or 48212 && spell.Interrupted)
        {
            _next = null;
            _step = default;
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
        // 攻略 (game8 第2盤 / インフィ note, 11-08): the chasing brother's rotating 30-degree
        // cones are dodged with one large move, never by last-moment small steps.
        if (_next is { } spin && spin.Activation.AddSeconds(2) >= WorldState.CurrentTime)
            hints.NoEscapeDelay = true;
        if (_next is { } next && next.Activation.AddSeconds(2) >= WorldState.CurrentTime)
            // Staying near the pivot reduces travel, but the other brother's
            // R8 contact can cover that whole region. It is not a mechanic
            // that forces the player into this circle.
            hints.GoalZones.Add(p => Math.Clamp(8f - (p - next.Origin).Length(), 0f, 3f));
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
        if (actor.OID == 19561)
        {
            if (animState1 == 1)
                _ignited.TryAdd(actor.InstanceID, WorldState.FutureTime(7.1d));
            else
                _ignited.Remove(actor.InstanceID);
        }
    }
    public override void OnActorCreated(Actor actor)
    {
        _resolved.Remove(actor.InstanceID);
        _ignited.Remove(actor.InstanceID);
    }
    public override void OnActorDestroyed(Actor actor)
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

// Sand pillars travel at 5y/0.3s between the six pits. All seven run2003 bursts
// begin 5y from their source pit: a unique source + collinear destination can
// identify the first segment. Otherwise two impacts must identify the line.
// Never extrapolate past an observed pit or retain a route across a gap.
sealed class CrucibleSandPillars(BossModule module) : Components.GenericAOEs(module, 48225u)
{
    private WPos? _last;
    private DateTime _lastTime;
    private AOEInstance? _route;
    private bool _burrowing;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 48226 or 48227 or 48239)
            _burrowing = false;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48224)
            _burrowing = true;
        else if (spell.Action.ID is 48227 or 48239 or 48240)
            _burrowing = false;
        if (spell.Action.ID != WatchedAction)
            return;
        _burrowing = true;
        var pos = caster.Position;
        _route = null;
        if (_last is { } last && (WorldState.CurrentTime - _lastTime).TotalSeconds is > 0.15d and < 0.6d)
            _route = Route(last, pos);
        else
        {
            var starts = Pits().Where(p => (pos - p).LengthSq() is > 16f and < 36f).Take(2).ToArray();
            if (starts.Length == 1)
                _route = Route(starts[0], pos);
        }
        _last = pos;
        _lastTime = WorldState.CurrentTime;
    }
    private IEnumerable<WPos> Pits() => Module.Enemies(2015270)
        .Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed).Select(a => a.Position);
    private AOEInstance? Route(WPos previous, WPos pos)
    {
        var delta = pos - previous;
        if (delta.LengthSq() is not (> 16f and < 36f))
            return null;
        var dir = delta.Normalized();
        var ends = Pits().Where(p => (p - pos).Dot(dir) > 0 && Math.Abs((p - pos).Cross(dir)) < 1f)
            .Take(2).ToArray();
        return ends.Length == 1
            // This is an already active travelling corridor. Refreshing a
            // future 0.3s deadline at every pulse repeatedly makes escape
            // infeasible, cancelling a route that was already being walked.
            // Its lifetime is bounded by ActiveAOEs and the next impact.
            ? new(new AOEShapeRect((ends[0] - pos).Length() + 4, 4, 4), pos, Angle.FromDirection(dir))
            : null;
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => _route is { } route && (WorldState.CurrentTime - _lastTime).TotalSeconds < 0.7d ? new[] { route } : [];
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
        {
            CrucibleScorpionAI.Avoid(hints, aoe);
            hints.NoEscapeDelay = true;
        }
        AddStagingGoal(slot, actor, hints);
    }

    private void AddStagingGoal(int slot, Actor actor, AIHints hints)
    {
        if (!_burrowing || actor.CastInfo != null || hints.GoalPoints.Count > 0
            || !Module.Enemies(19558).Any(a => !a.IsDeadOrDestroyed)
            || Module.Enemies(19557).FirstOrDefault(a => !a.IsDeadOrDestroyed) is not { } goblin)
            return;
        var pits = Pits().ToArray();
        if (pits.Length != 6)
            return;
        var range = 2.5f + goblin.HitboxRadius + actor.HitboxRadius;
        var margin = WorldState.NavigationMargin + 0.35f;
        var hazards = Module.Components.OfType<Components.GenericAOEs>()
            .SelectMany(c => c.ActiveAOEs(slot, actor).ToArray()).Where(a => a.Risky)
            .Select(a => a.ShapeDistance ?? a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
        var candidates = new List<(WPos Position, float PitGap, float RouteGap)>();
        for (var x = (int)MathF.Ceiling((goblin.Position.X - range) * 2); x <= (goblin.Position.X + range) * 2; ++x)
            for (var z = (int)MathF.Ceiling((goblin.Position.Z - range) * 2); z <= (goblin.Position.Z + range) * 2; ++z)
            {
                var p = new WPos(x * 0.5f, z * 0.5f);
                if ((p - goblin.Position).LengthSq() > range * range || !Module.Arena.InBounds(p)
                    || hazards.Any(h => h.Distance(p) <= margin))
                    continue;
                var pitGap = pits.Min(pit => (p - pit).Length() - 6f - margin);
                if (pitGap <= 0)
                    continue;
                var routeGap = float.PositiveInfinity;
                for (var i = 0; i < pits.Length; ++i)
                    for (var j = i + 1; j < pits.Length; ++j)
                    {
                        var delta = pits[j] - pits[i];
                        var along = Math.Clamp((p - pits[i]).Dot(delta) / delta.LengthSq(), 0, 1);
                        routeGap = Math.Min(routeGap, (p - (pits[i] + delta * along)).Length() - 4f - margin);
                    }
                candidates.Add((p, pitGap, routeGap));
            }
        if (candidates.Count == 0)
            return;
        // All pit-to-pit directions are possible here; the future chosen line
        // is deliberately unknown. Prefer a point clear of every line if one
        // exists in melee. Otherwise maximize fixed-pit clearance in melee,
        // without claiming that this fallback is safe from every future line.
        var outsideEveryRoute = candidates.Any(c => c.RouteGap > 0);
        var best = candidates.Where(c => !outsideEveryRoute || c.RouteGap > 0)
            .OrderByDescending(c => outsideEveryRoute ? c.RouteGap : c.PitGap)
            .ThenBy(c => (c.Position - actor.Position).LengthSq()).First();
        hints.GoalPoints.Add((best.Position, 0.6f, 30));
    }
}

sealed class CrucibleNecromancerHint(BossModule module) : Components.CastHints(module, [48183u], "全体伤害；优先击杀僵尸，避开持续瘴气！");
sealed class CrucibleMinotaurEnrageHint(BossModule module) : Components.CastHints(module, [48214u], "无尽挥打覆盖全场：尽快击败牛魔！");
sealed class CrucibleWormQuakeHint(BossModule module) : Components.CastHints(module, [48239u], "连续全体地震：治疗并尽快击败小地豆！");

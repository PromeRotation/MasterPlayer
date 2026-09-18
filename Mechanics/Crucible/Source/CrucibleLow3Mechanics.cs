// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Normal third board, ARR 2026-09-10/11. Geometry is from the CN
// Action/Omen rows; origins/rotations use cast packets, not stale helper positions.
class Low3CrucibleCastAOEs(BossModule module, Battle battle) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => (battle, actionID) switch
    {
        (Battle.B14, 48473) => new(new AOEShapeCone(60, 65f.Degrees()), true),
        (Battle.B14, 48464) => new(new AOEShapeCircle(20), true),
        (Battle.B14, 48462) => new(new AOEShapeRect(60, 4), true),
        (Battle.B15, 48483) => new(new AOEShapeCircle(6), true),
        (Battle.B16, 48492) => new(new AOEShapeCross(50, 4), true),
        (Battle.B16, 48501) => new(new AOEShapeCircle(6), true),
        (Battle.B18, 48560) => new(new AOEShapeCircle(6), true),
        (Battle.B19, 48570) => new(new AOEShapeRect(50, 8), true),
        // gl_sircle_5003bf scaled to Action's outer radius 43.
        (Battle.B19, 48575) => new(new AOEShapeDonut(2.58f, 43), true),
        (Battle.B21, 48605) => new(new AOEShapeRect(80, 10), true),
        (Battle.B21, 48620) => new(new AOEShapeRect(50, 3), true), // client width 6, not spreadsheet width 40
        (Battle.B21, 48614) => new(new AOEShapeRect(60, 3), true),
        _ => null
    };

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// The four f1x3_a1_gim01 rocks are enabled by MapEffect 2..5. Use their
// model bounding box as a conservative physical obstacle, not as damage.
// The NE rock (index 3) disappears during the recorded Ymir encounter.
sealed class CrucibleYmirRocks(BossModule module) : BossComponent(module)
{
    private readonly bool[] _active = [true, true, true, true];
    private static readonly WPos[] Positions = [new(105, -15), new(135, -15), new(135, 15), new(105, 15)];
    private static readonly float[] Rotations = [45, -45, -135, 135];

    public override void OnMapEffect(byte index, uint state)
    {
        if (index is >= 2 and <= 5 && state is 0x00020001 or 0x00080004)
            _active[index - 2] = state == 0x00020001;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        for (var i = 0; i < 4; ++i)
            if (_active[i])
            {
                var dir = Rotations[i].Degrees().ToDirection();
                // Mdl min/max X -3.456..2.730, Z -4.061..1.266; +0.2y clearance.
                var origin = Positions[i] - 0.363f * dir.OrthoL() - 1.397f * dir;
                hints.TemporaryObstacles.Add(new SDRect(origin, dir, 2.864f, 2.864f, 3.293f));
            }
        foreach (var target in hints.PotentialTargets)
            if (target.Actor.OID == 19605 && target.Actor.FindStatus(5434u) != null)
            {
                target.Spikes = true;
                target.ShouldBeDispelled = true;
                target.Priority = AIHints.Enemy.PriorityForbidden;
            }
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (Module.Enemies(19605).Any(a => !a.IsDeadOrDestroyed && a.FindStatus(5434u) != null))
            hints.Add("鱼人有麻痹尖刺：先驱散或换目标，避免反击麻痹！");
    }
}

// Shared cleanup for the three observed knockbacks. In particular, a single
// target cast's LocXZ is the victim, whereas its push originates at the caster.
abstract class Low3CrucibleKnockback(BossModule module, uint actionID, float distance,
    Components.GenericKnockback.Kind kind, AOEShape? shape = null, bool singleTarget = false)
    : Components.GenericKnockback(module, actionID)
{
    private readonly Dictionary<ulong, (Knockback source, ulong target)> _sources = [];
    protected readonly List<Knockback> Sources = [];
    public DateTime? Earliest => GetSources().Count > 0 ? Sources.Min(k => k.Activation) : null;

    private List<Knockback> GetSources(Actor? actor = null)
    {
        foreach (var (id, entry) in _sources.ToArray())
            if (entry.source.Activation.AddSeconds(2) < WorldState.CurrentTime)
                _sources.Remove(id);
        Sources.Clear();
        foreach (var entry in _sources.Values)
            if (!singleTarget || actor == null || entry.target == actor.InstanceID)
                Sources.Add(entry.source);
        Sources.Sort((l, r) => l.Activation.CompareTo(r.Activation));
        return Sources;
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        GetSources(actor);
        return CollectionsMarshal.AsSpan(Sources);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
            _sources[caster.InstanceID] = (new(singleTarget ? caster.Position : spell.LocXZ, distance,
                Module.CastFinishAt(spell), shape, spell.Rotation, kind, actorID: caster.InstanceID), spell.TargetID);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _sources.Remove(caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
            _sources.Remove(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => _sources.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    protected static WPos Landing(WPos p, in Knockback source) => p + source.Distance * (source.Kind switch
    {
        Kind.DirForward => source.Direction.ToDirection(),
        Kind.DirLeft => (source.Direction + 90f.Degrees()).ToDirection(),
        _ => (p - source.Origin).Normalized()
    });

    protected sealed class LandingZone(Knockback[] sources, WPos center, float halfWidth, float halfHeight,
        Components.GenericAOEs.AOEInstance[] followups) : ShapeDistance
    {
        private readonly ShapeDistance[] _followups = followups.Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
        public override float Distance(in WPos p)
        {
            var end = p;
            var matched = false;
            foreach (var source in sources)
                if (source.Shape == null || source.Shape.Check(p, source.Origin, source.Direction))
                {
                    end = Landing(p, source);
                    matched = true;
                    break; // simultaneous strip knockbacks; never apply them sequentially
                }
            // Lane boundaries can differ by a few hundredths due to packet quantization.
            // Do not advertise those seams as a way to avoid the entire knockback.
            if (!matched)
                return -1;
            var safe = Math.Min(halfWidth - Math.Abs(end.X - center.X), halfHeight - Math.Abs(end.Z - center.Z));
            foreach (var aoe in _followups)
                safe = Math.Min(safe, aoe.Distance(end) - 0.5f);
            return safe;
        }
    }
}

sealed class CrucibleRangerKnockback(BossModule module)
    : Low3CrucibleKnockback(module, 48471u, 15, Kind.AwayFromOrigin, singleTarget: true)
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, 19.5f, 19.5f);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new LandingZone([kb], Module.Center, 19.5f, 19.5f, []), kb.Activation.AddSeconds(-0.4d));
    }
}

// 48556 is unavoidable damage plus a 20y forward push (Knockback row 108).
// The four strips cover the field. 48555 follows ~2.1s later and IS avoidable.
sealed class CrucibleLahamuKnockback(BossModule module)
    : Low3CrucibleKnockback(module, 48556u, 20, Kind.DirForward, new AOEShapeRect(45, 5))
{
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        var all = base.ActiveKnockbacks(slot, actor);
        foreach (var kb in all)
            if (kb.Shape!.Check(actor.Position, kb.Origin, kb.Direction))
                return new[] { kb }; // draw just the push belonging to the current strip
        return [];
    }

    private Components.GenericAOEs.AOEInstance[] Followups(int slot, Actor actor)
        => Module.FindComponent<CrucibleLahamuGolems>()?.ActiveAOEs(slot, actor).ToArray() ?? [];

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        => !pos.InRect(Module.Center, 19.5f, 19.5f) || Followups(slot, actor).Any(a => a.Check(pos));

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var all = base.ActiveKnockbacks(slot, actor).ToArray();
        if (all.Length == 0)
            return;
        var next = Followups(slot, actor);
        if (IsImmune(slot, all[0].Activation))
        {
            foreach (var aoe in next)
                CrucibleScorpionAI.Avoid(hints, aoe);
        }
        else
            hints.AddForbiddenConstraint(new LandingZone(all, Module.Center, 19.5f, 19.5f, next), all[0].Activation.AddSeconds(-0.5d));
    }
}

sealed class CrucibleLahamuGolems(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48555 ? new(new AOEShapeRect(45, 5), true) : null;
    protected override double RiskyActivationWindow => 0.5d;
    protected override DateTime? CompetingActivation => Module.FindComponent<CrucibleLahamuKnockback>()?.Earliest;
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Before the push, its component evaluates these at the LANDING position.
        if (CompetingActivation != null)
            return;
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// Sand orbs are stationary. Their spawn provides 5.7s of warning, rather than
// only the 2.7s cast; reconcile the predicted impact against the actual cast.
sealed class CrucibleLahamuSand(BossModule module) : Components.GenericAOEs(module, 48562u)
{
    private readonly Dictionary<ulong, AOEInstance> _orbs = [];
    private readonly HashSet<ulong> _resolved = [];
    private static readonly AOEShapeCircle Shape = new(12);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 19616 && !actor.IsDeadOrDestroyed && !_resolved.Contains(actor.InstanceID))
            _orbs.TryAdd(actor.InstanceID, new(Shape, actor.Position, activation: WorldState.FutureTime(5.7d), actorID: actor.InstanceID));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && !_resolved.Contains(caster.InstanceID))
            _orbs[caster.InstanceID] = new(Shape, spell.LocXZ, activation: Module.CastFinishAt(spell), actorID: caster.InstanceID);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _orbs.Remove(caster.InstanceID);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            _orbs.Remove(caster.InstanceID);
            _resolved.Add(caster.InstanceID);
        }
    }
    public override void OnActorDestroyed(Actor actor) => _orbs.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        foreach (var (id, aoe) in _orbs.ToArray())
            if (aoe.Activation.AddSeconds(2) < WorldState.CurrentTime)
                _orbs.Remove(id);
        return _orbs.Values.ToArray();
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// 48564 contains the landing location 2.2s before the first song pulse. The
// exact cone angle is absent from Action/Omen: display safe flank destinations,
// not a guessed damage cone. ARR hits reach 18.7deg; the player is safe at
// 48.6deg and then 62.3deg for nine pulses. Both suggested circles stay beyond
// 51deg from the facing and inside the circular field.
sealed class CrucibleSirenSong(BossModule module) : Components.GenericAOEs(module, 48566u)
{
    private WPos _origin;
    private Angle _rotation;
    private DateTime _activation;
    private DateTime _expires;
    private int _pulses;
    private readonly HashSet<uint> _events = [];
    private static readonly AOEShapeCircle Spot = new(1.2f);

    private void Begin(WPos origin, Angle rotation, double delay)
    {
        _origin = origin;
        _rotation = rotation;
        _activation = WorldState.FutureTime(delay);
        _expires = _activation.AddSeconds(7.5d);
        _pulses = 0;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not (48564 or 48565 or 48566) || !_events.Add(spell.GlobalSequence))
            return;
        if (spell.Action.ID == 48564 && spell.TargetXZ.InCircle(Module.Center, 25))
            Begin(spell.TargetXZ, Angle.FromDirection(Module.Center - spell.TargetXZ), 2.2d);
        else if (spell.Action.ID == 48565)
            Begin(caster.Position, spell.Rotation, 1.05d);
        else if (spell.Action.ID == WatchedAction)
        {
            if (_expires < WorldState.CurrentTime)
                Begin(caster.Position, spell.Rotation, 0.58d);
            _origin = caster.Position;
            _rotation = spell.Rotation;
            _activation = WorldState.FutureTime(0.58d);
            if (++_pulses >= 12)
                _expires = default;
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (_expires <= WorldState.CurrentTime)
            return [];
        var result = new List<AOEInstance>();
        foreach (var offset in new[] { -60f, 60f })
        {
            var spot = _origin + 8f * (_rotation + offset.Degrees()).ToDirection();
            if (spot.InCircle(Module.Center, 18.7f))
                result.Add(new(Spot, spot, activation: _activation, color: Colors.SafeFromAOE, risky: false));
        }
        return CollectionsMarshal.AsSpan(result);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_expires > WorldState.CurrentTime)
            hints.Add("混乱之歌连续判定：移到首领侧面，别回正面！",
                !ActiveAOEs(slot, actor).ToArray().Any(a => a.Check(actor.Position)));
    }

    private sealed class OutsideSpots(WPos[] spots) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var nearest = float.MaxValue;
            foreach (var spot in spots)
                nearest = Math.Min(nearest, (p - spot).Length());
            return 1.2f - nearest;
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var spots = ActiveAOEs(slot, actor).ToArray().Select(a => a.Origin).ToArray();
        if (spots.Length > 0)
            hints.AddForbiddenConstraint(new OutsideSpots(spots), _activation.AddSeconds(-0.3d));
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19617)
            _expires = default;
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// SourceLeft (row 235), not a radial push. With the source east of the floor
// facing west, this moves the player SOUTH by 20y. LocXZ is 40y from the stale
// helper actor position, so both the direction and packet location matter.
sealed class CrucibleGatlerTorrent(BossModule module)
    : Low3CrucibleKnockback(module, 48607u, 20, Kind.DirLeft, new AOEShapeRect(80, 40))
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, 14.4f, 24.4f);
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new LandingZone([kb], Module.Center, 14.4f, 24.4f, []), kb.Activation.AddSeconds(-0.5d));
    }
}

// R8 bombs resolve ~3.5s before the R40 bite. Allow the current safe gaps while
// staging close enough to the far end that the large circle can be escaped.
sealed class CrucibleGatlerExplosions(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override double RiskyActivationWindow => 0.5d;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48597 => new(new AOEShapeCircle(8), true),
        48603 => new(new AOEShapeCircle(40), true),
        _ => null
    };

    private sealed class BiteStaging(WPos origin, WPos center, float travel) : ShapeDistance
    {
        private const float Radius = 40.5f;
        private readonly WPos[] _edgeCandidates = EdgeCandidates(origin, center);
        // Nearest point outside R40 AND on the 30x50 floor. A radial escape
        // near a side wall would leave the floor, so also consider the circle's
        // intersections with every floor edge. The floor is convex; the segment
        // to any accepted candidate stays on it once the small bombs resolve.
        public float EscapeDistance(WPos p)
        {
            var delta = p - origin;
            var length = delta.Length();
            if (length >= Radius)
                return 0;
            var nearest = float.MaxValue;
            if (length > 0 && (origin + Radius / length * delta).InRect(center, 14.35f, 24.35f))
                nearest = Radius - length;
            foreach (var candidate in _edgeCandidates)
                nearest = Math.Min(nearest, (candidate - p).Length());
            return nearest;
        }
        private static WPos[] EdgeCandidates(WPos origin, WPos center)
        {
            var result = new List<WPos>();
            foreach (var x in new[] { center.X - 14.35f, center.X + 14.35f })
            {
                var square = Radius * Radius - (x - origin.X) * (x - origin.X);
                if (square < 0)
                    continue;
                var dz = MathF.Sqrt(square);
                foreach (var z in new[] { origin.Z - dz, origin.Z + dz })
                    if (Math.Abs(z - center.Z) <= 24.35f)
                        result.Add(new(x, z));
            }
            foreach (var z in new[] { center.Z - 24.35f, center.Z + 24.35f })
            {
                var square = Radius * Radius - (z - origin.Z) * (z - origin.Z);
                if (square < 0)
                    continue;
                var dx = MathF.Sqrt(square);
                foreach (var x in new[] { origin.X - dx, origin.X + dx })
                    if (Math.Abs(x - center.X) <= 14.35f)
                        result.Add(new(x, z));
            }
            return result.ToArray();
        }
        public override float Distance(in WPos p) => travel - EscapeDistance(p);
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        foreach (ref readonly var aoe in aoes)
            CrucibleScorpionAI.Avoid(hints, aoe);
        if (aoes.Length == 0)
            return;
        foreach (ref readonly var aoe in aoes)
            if (!aoe.Risky && aoe.Shape is AOEShapeCircle { Radius: 40 })
            {
                var lastBomb = aoes[0].Activation;
                foreach (ref readonly var earlier in aoes)
                    if (earlier.Risky && earlier.Activation > lastBomb)
                        lastBomb = earlier.Activation;
                // 6y/s after the LAST bomb; reserve 1.5y for reaction/turning.
                var travel = Math.Max(0f, 6f * (float)(aoe.Activation - lastBomb).TotalSeconds - 1.5f);
                var staging = new BiteStaging(aoe.Origin, Module.Center, travel);
                hints.AddForbiddenConstraint(staging, aoes[0].Activation.AddSeconds(-0.4d), aoes[0].Activation.AddSeconds(0.6));
                hints.GoalZones.Add(p => Math.Max(0f, 50f - staging.EscapeDistance(p)));
            }
    }
}

// EObj 2015427 is a 10x10 binding square, centered on its spawn, followed
// ~12.5s later by a 15y cross of width 10. Bind (3625) is followed by a pull
// to its center: avoid entering the square from spawn, before either event.
sealed class CrucibleGatlerTraps(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private readonly Dictionary<ulong, (WPos pos, DateTime cross)> _traps = [];
    private readonly List<AOEInstance> _display = [];
    private static readonly AOEShapeRect Square = new(5, 5, 5);
    private static readonly AOEShapeCross Cross = new(15, 5);
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 48611 ? new(Cross, true) : null;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 2015427 && !actor.IsDeadOrDestroyed && actor.EventState != 7)
            _traps.TryAdd(actor.InstanceID, (actor.Position, WorldState.FutureTime(12.5d)));
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48611 && !spell.EventHappened)
            foreach (var (id, trap) in _traps.ToArray())
                if (trap.pos.AlmostEqual(spell.LocXZ, 0.5f))
                    _traps[id] = (trap.pos, Module.CastFinishAt(spell));
        base.OnCastStarted(caster, spell);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48611)
            foreach (var (id, trap) in _traps.ToArray())
                if (trap.pos.AlmostEqual(spell.LocXZ, 0.5f))
                    _traps.Remove(id);
        base.OnCastFinished(caster, spell);
    }
    public override void OnActorDestroyed(Actor actor)
    {
        _traps.Remove(actor.InstanceID);
        base.OnActorDestroyed(actor);
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48611)
            foreach (var (id, trap) in _traps.ToArray())
                if (trap.pos.AlmostEqual(caster.CastInfo?.LocXZ ?? caster.Position, 0.5f))
                    _traps.Remove(id);
        base.OnEventCast(caster, spell);
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var real = base.ActiveAOEs(slot, actor).ToArray();
        _display.Clear();
        foreach (var (id, trap) in _traps.ToArray())
        {
            if (trap.cross.AddSeconds(2) < WorldState.CurrentTime || WorldState.Actors.Find(id) is not { EventState: not 7, IsDeadOrDestroyed: false })
            {
                _traps.Remove(id);
                continue;
            }
            _display.Add(new(Square, trap.pos));
            if (!real.Any(a => a.Origin.AlmostEqual(trap.pos, 0.5f)))
                _display.Add(new(Cross, trap.pos, activation: trap.cross, color: Colors.AOE,
                    risky: trap.cross <= WorldState.FutureTime(5)));
        }
        _display.AddRange(real);
        return CollectionsMarshal.AsSpan(_display);
    }
    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            if (aoe.Shape == Square)
                hints.TemporaryObstacles.Add(new SDRect(aoe.Origin, new WDir(0, 1), 5.25f, 5.25f, 5.25f));
            else if (aoe.Risky)
                CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

sealed class CrucibleYmirThunderHint(BossModule module) : Components.CastHints(module, [48479u], "万雷即将结算：注意雷击，处理尤弥尔与场地机关！");
sealed class CrucibleZuCrossHint(BossModule module) : Components.CastHints(module, [48491u], "十字风即将点名落地：保持移动，落点出现后斜向离开！");
sealed class CrucibleZuFrenzyHint(BossModule module) : Components.CastHints(module, [50465u], "狂乱即将结算，尽快击败祖！");
sealed class CrucibleLahamuEarthshakerHint(BossModule module) : Components.CastHints(module, [48557u], "大地摇动：远离被点名单位的方向！");

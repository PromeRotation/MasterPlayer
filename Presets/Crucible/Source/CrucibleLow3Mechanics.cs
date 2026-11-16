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
        (Battle.B17, 48512) => new(new AOEShapeCone(60, 90f.Degrees()), true),
        (Battle.B18, 48560) => new(new AOEShapeCircle(6), true),
        (Battle.B19, 48570) => new(new AOEShapeRect(50, 8), true),
        (Battle.B19, 48573) => new(new AOEShapeCircle(12), true),
        (Battle.B19, 50662) => new(new AOEShapeRect(5, 1.5f), true),
        // gl_sircle_5003bf scaled to Action's outer radius 43.
        (Battle.B19, 48575) => new(new AOEShapeDonut(2.58f, 43), true),
        (Battle.B21, 48605) => new(new AOEShapeRect(80, 10), true),
        (Battle.B21, 48614) => new(new AOEShapeRect(60, 3), true),
        (Battle.B21, 48618) => new(new AOEShapeCircle(6), true),
        _ => null
    };

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Interrupted)
            base.OnCastFinished(caster, spell);
    }

    // CrucibleScorpionAI's 0.35y plus the 0.5y navigation inflation.
    private const float EscapeClearance = .85f;

    // 亡者之歌 48573: R12 on the siren for 6.7s, overlapping the staggered 48577 bombs.
    // Staggered activations disable the uptime escape delay, so the player stood
    // ~4s at 10y (run2005 b5 148–153s). Reveal it once the walk out (5y/s) plus
    // 1.5s reserve needs the time left; it is a fixed circle, nothing else changes.
    private const float RingWalkSpeed = 5f;
    private const double RingReserve = 1.5;
    private bool WithholdSirenRing(Actor actor, in AOEInstance aoe)
    {
        if (battle != Battle.B19 || aoe.Shape is not AOEShapeCircle { Radius: 12 } || aoe.Activation == default)
            return false;
        var walk = MathF.Max(0, 12 + EscapeClearance + .5f - (actor.Position - aoe.Origin).Length()) / RingWalkSpeed;
        return aoe.Activation > WorldState.FutureTime(walk + .25 + RingReserve);
    }

    private const float CornerRadius = 1f, CornerClearance = 21f;
    private const int CornerPriority = 50;
    // Circle sets are a random corner or two plus the centre (30 sealed fights). None clear when all five resolve.
    private WPos? ClearCorner(AOEInstance[] live, WPos boss)
    {
        var circles = live.Where(a => a.Shape is AOEShapeCircle { Radius: 20 } && a.Activation != default).ToArray();
        if (circles.Length is < 2 or > 4)
            return null;
        WPos? best = null;
        for (var x = -18f; x <= 18; x += 2)
            for (var z = -18f; z <= 18; z += 2)
            {
                var p = Module.Center + new WDir(x, z);
                if (circles.All(c => !p.InCircle(c.Origin, CornerClearance)) && (best is not { } b || (p - boss).LengthSq() < (b - boss).LengthSq()))
                    best = p;
            }
        return best;
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var live = ActiveAOEs(slot, actor);
        if (battle == Battle.B15 && hints.DirectRoute == null)
            foreach (ref readonly var aoe in live)
                if (aoe.Shape is AOEShapeCircle { Radius: 6 } && aoe.Activation != default && aoe.Activation > WorldState.CurrentTime
                    && actor.Position.InCircle(aoe.Origin, 6 + EscapeClearance))
                {
                    // 激水 lands on the player (2.7s cast, ~7y to clear incl. margins). A single
                    // multi-second background solve (run1840-b3 @0.3 on the training machine: 2.07s)
                    // forfeits it, so walk straight out; the controller still validates the route
                    // against every danger and falls back to the solver if it is unsafe.
                    var away = actor.Position - aoe.Origin;
                    var dir = away.LengthSq() > .25f ? away.Normalized() : (Module.Center - aoe.Origin).Normalized();
                    if (!float.IsFinite(dir.X)) dir = new WDir(0, 1);
                    var exit = aoe.Origin + dir * (6 + EscapeClearance + .3f);
                    if (!Module.Arena.InBounds(exit))
                        exit = aoe.Origin + (Module.Center - aoe.Origin).Normalized() * (6 + EscapeClearance + .3f);
                    // run2007: walking straight away from the drop left the R0.5 sahagin
                    // 6–13y off for ten seconds. The exit ring is equally far in every
                    // direction; take the in-bounds point nearest the current target that
                    // avoids every other pending splash.
                    // Without a selectable target (late load, untargetable gap) prefer the
                    // nearest live B15 enemy; this only orders equally safe exits.
                    if ((Module.FindComponent<CrucibleYmirTargets>()?.Selected(actor)
                        ?? Module.Enemies(19605).Concat(Module.Enemies(19604)).Concat(Module.Enemies(19603))
                            .Where(a => !a.IsDeadOrDestroyed).MinBy(a => (a.Position - actor.Position).LengthSq())) is { } target)
                    {
                        var origin = aoe.Origin;
                        var rocks = Module.FindComponent<CrucibleYmirRocks>();
                        var from = actor.Position;
                        var others = live.ToArray().Where(o => !o.Origin.AlmostEqual(origin, .1f) && o.Activation > WorldState.CurrentTime).ToArray();
                        var best = exit;
                        var bestDistance = (exit - target.Position).LengthSq();
                        for (var deg = 0; deg < 360; deg += 15)
                        {
                            var p = origin + (6 + EscapeClearance + .3f) * deg.Degrees().ToDirection();
                            var d = (p - target.Position).LengthSq();
                            if (d < bestDistance && Module.Arena.InBounds(p) && !others.Any(o => o.Shape.Check(p, o.Origin, o.Rotation))
                                && (rocks?.Clear(from, p, 1) ?? true))
                                (best, bestDistance) = (p, d);
                        }
                        exit = best;
                    }
                    if (Module.Arena.InBounds(exit))
                    {
                        hints.DirectRoute = [exit];
                        hints.DirectRouteDeadline = aoe.Activation.AddSeconds(-.35);
                    }
                    break;
                }
        var pushes =battle == Battle.B14 && live.Length < 5 && Module.FindComponent<CrucibleRangerKnockback>() is { } ranger
            ? ranger.ActiveKnockbacks(slot, actor).ToArray() : [];
        // kano 11-06: lead the knight into the clear corner as soon as the circles are known (6s ahead), so melee
        // goes on there through the hit. It follows its top-threat target: the player walks there when holding it,
        // and a beast holding it is sent there too (run on normal low1: the knight followed the beast 2 moves of 3).
        var knightTarget = Module.PrimaryActor.TargetID;
        var petHolds = WorldState.Actors.Find(knightTarget) is { OwnerID: var owner } && owner == actor.InstanceID;
        if (battle == Battle.B14 && pushes.Length == 0 && (knightTarget == actor.InstanceID || petHolds)
            && ClearCorner(live.ToArray(), Module.PrimaryActor.Position) is { } corner)
        {
            hints.GoalPoints.Add((corner, CornerRadius, CornerPriority));
            if (petHolds)
                hints.PetMoveTo = corner;
        }
        if (pushes.Length > 0)
        {
            // Circles followed ~1.5s later by the 15y push: the dash from the circle edge to
            // a safe launch point needs ~1.4s at 6y/s. Keep the 0.35y timing/spatial margin
            // but skip the generic 0.5y navigation inflation for these circles only.
            foreach (ref readonly var aoe in live)
                if (aoe.Shape is AOEShapeCircle { Radius: 20 } && aoe.Activation != default && aoe.Activation < pushes[0].Activation)
                    hints.ForbiddenZones.Add(new(new AOEShapeCircle(20 + CrucibleRangerKnockback.CircleMargin).Distance(aoe.Origin, aoe.Rotation),
                        aoe.Activation.AddSeconds(-0.25d), aoe.Activation.AddSeconds(0.6), ExactGeometry: true));
                else CrucibleScorpionAI.Avoid(hints, aoe);
            return;
        }
        if (battle == Battle.B14 && B14LaneOverlap.TryCreate(Module, live.ToArray(), out var lanes, out var laneOverlap))
        {
            // 残杀: five abutting 8y-wide lanes (run1830 x=104..136, run1831) cover the whole
            // field. Every spot takes exactly one lane, so forbid only the seams (two lanes) and
            // keep planning the following 钢铁裂斩 cone instead of reporting no safe cell.
            hints.AddForbiddenZone(laneOverlap!, lanes.Min(a => a.Activation).AddSeconds(-.25), lanes.Max(a => a.Activation).AddSeconds(.6));
            foreach (ref readonly var aoe in live)
                if (!lanes.Contains(aoe)) CrucibleScorpionAI.Avoid(hints, aoe);
            return;
        }
        if (battle != Battle.B14 || live.Length < 5)
        {
            foreach (ref readonly var aoe in live)
                if (!WithholdSirenRing(actor, aoe))
                    CrucibleScorpionAI.Avoid(hints, aoe);
            return;
        }
        var active = live.ToArray();
        if (battle == Battle.B14 && B14FiveCircleOverlap.TryCreate(Module.Center, active, out var overlap))
        {
            // Five native R20 circles cover the whole field. Permit the one
            // unavoidable hit only; keep original AOEs for display/damage audit.
            var circles = active.Where(a => a.Shape is AOEShapeCircle { Radius: 20 }).ToArray();
            hints.GoalPoints.Add((Module.Center, .5f, 2));
            hints.AddForbiddenZone(overlap!, circles.Min(a => a.Activation).AddSeconds(-.25),
                circles.Max(a => a.Activation).AddSeconds(.6));
            foreach (var aoe in active.Where(a => a.Shape is not AOEShapeCircle { Radius: 20 }))
                CrucibleScorpionAI.Avoid(hints, aoe);
        }
        else foreach (var aoe in active) CrucibleScorpionAI.Avoid(hints, aoe);
    }
}

// 48462 lanes resolving together that cover the whole arena: the overlap (two or more
// lanes, +0.35y margin) is the only avoidable part.
internal sealed class B14LaneOverlap(ShapeDistance[] lanes) : ShapeDistance
{
    internal static bool TryCreate(BossModule module, Components.GenericAOEs.AOEInstance[] live,
        out Components.GenericAOEs.AOEInstance[] lanes, out ShapeDistance? overlap)
    {
        overlap = null;
        lanes = live.Where(a => a.Shape is AOEShapeRect && a.Activation != default).ToArray();
        if (lanes.Length < 2 || (lanes.Max(a => a.Activation) - lanes.Min(a => a.Activation)).TotalSeconds > .3) return false;
        var set = lanes;
        // Sample cell centres so measure-zero seams between abutting lanes do not read as gaps.
        for (var x = -19.85f; x <= 20f; x += .3f)
            for (var z = -19.85f; z <= 20f; z += .3f)
            {
                var p = module.Center + new WDir(x, z);
                if (module.Arena.InBounds(p) && !set.Any(a => a.Shape.Check(p, a.Origin, a.Rotation))) return false;
            }
        overlap = new B14LaneOverlap(lanes.Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray());
        return true;
    }
    public override float Distance(in WPos p)
    {
        var first = float.PositiveInfinity; var second = first;
        foreach (var lane in lanes)
        {
            var d = lane.Distance(p) - .35f;
            if (d < first) { second = first; first = d; } else if (d < second) second = d;
        }
        return second;
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

    private IEnumerable<SDRect> Obstacles()
    {
        for (var i = 0; i < 4; ++i)
            if (_active[i])
            {
                var dir = Rotations[i].Degrees().ToDirection();
                // Mdl min/max X -3.456..2.730, Z -4.061..1.266; +0.2y clearance.
                var origin = Positions[i] - 0.363f * dir.OrthoL() - 1.397f * dir;
                yield return new SDRect(origin, dir, 2.864f, 2.864f, 3.293f);
            }
    }

    // Straight walk from 'from' to 'to' keeps 'clearance' off every active rock.
    internal bool Clear(WPos from, WPos to, float clearance)
    {
        var rocks = Obstacles().ToArray();
        for (var s = 0; s <= 10; ++s)
        {
            var p = from + (to - from) * (s / 10f);
            if (rocks.Any(r => r.Distance(p) < clearance))
                return false;
        }
        return true;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var rock in Obstacles())
            hints.TemporaryObstacles.Add(rock);
        foreach (var target in hints.PotentialTargets)
            if (target.Actor.OID == 19605 && target.Actor.FindStatus(5434u) != null)
            {
                target.Spikes = true;
            }
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (Module.Enemies(19605).Any(a => !a.IsDeadOrDestroyed && a.FindStatus(5434u) != null))
            hints.Add("鱼人有麻痹尖刺（不可驱散）：暂转壳，尖刺结束后回鱼人；范围攻击避开鱼人！");
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
        Components.GenericAOEs.AOEInstance[] followups, float rasterStep = 0) : ShapeDistance
    {
        private readonly ShapeDistance[] _followups = followups.Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
        // Optional finer navigation raster than the generic 0.5y one (which forbids a whole cell
        // when any corner is forbidden), keyed by content so it is built once per push.
        internal const float RasterRadius = 21;
        public override MasterPlayer.Spatial.Geometry.IGeometryShape? Geometry(float y) => rasterStep > 0
            ? FrozenConstraintFactory.Create(p => Distance(p), center, y, radius: RasterRadius, step: rasterStep) : null;
        public override string? ContentKey() => rasterStep > 0 && followups.Length == 0
            ? $"low3-landing|{center.X:F3},{center.Z:F3}|{halfWidth:F3},{halfHeight:F3}|{rasterStep:F3}|" + string.Join(";", sources.Select(s =>
                $"{s.Origin.X:F3},{s.Origin.Z:F3},{s.Distance:F3},{s.Kind},{s.Direction.Rad:F4},{s.Shape?.GetType().Name}"))
            : null;
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
    // Effect packets observed up to ~0.3s after the estimated cast finish + NPC delay.
    private const double EffectGrace = 1.0, NavigationStamp = 0.25, NavigationBuffer = 0.25;
    private Knockback[] _pending = [];
    private ulong _target;
    private WPos? _stageGoal;
    private string? _planKey;
    private readonly List<WPos> _launch = [];
    private WPos? _launchOrigin;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        _stageGoal = null;
        _planKey = null;
        _launch.Clear(); _launchOrigin = null;
        _target = spell.TargetID;
        // The landing is fixed by the position at the action effect: across 45 unobstructed
        // ARR pushes (run1808-1855), effect-time position + 15y away from the caster matches the
        // final landing within 0.1y, although displacement only starts ~1.1-1.2s later. Movement
        // in between is rolled back, so the launch point must be safe by the effect itself.
        // FinishAt already carries the 0.3s NPC finish delay (CrucibleEventAdapter); the
        // recorded effects land 0.03-0.08s before it. The direct-route validator pre-buffers a
        // window by 0.2s and adds 0.1s endpoint safety, so a stamp at FinishAt+0.25 binds from
        // about FinishAt-0.05; the solver's own start buffer makes its staging at least as early.
        // The dash deadline is FinishAt including the controller's 0.1s input allowance (replay
        // input latency 0.08s), i.e. arrival about at the effect; the 0.6y landing slack to the
        // 20y fence absorbs the last few centimetres (run1835 needs this; FinishAt-0.05 rejects it).
        _pending = [new(caster.Position, 15, Module.CastFinishAt(spell, NavigationStamp), actorID: caster.InstanceID)];
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && spell.Interrupted && _pending is [var source] && source.ActorID == caster.InstanceID)
            _pending = [];
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        // The effect fixes the landing; nothing the player does afterwards changes it.
        if (spell.Action.ID == WatchedAction && _pending is [var source] && source.ActorID == caster.InstanceID)
            _pending = [];
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _target || _pending is [var source] && source.ActorID == actor.InstanceID)
            _pending = [];
    }

    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        // Hold the launch constraint until the effect arrives (it can trail the estimate);
        // a lost effect packet must not pin the player forever.
        if (_pending is [var source] && WorldState.CurrentTime >= source.Activation.AddSeconds(EffectGrace))
            _pending = [];
        return actor.InstanceID == _target ? _pending : [];
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InRect(Module.Center, LandingHalfWidth, LandingHalfWidth);

    // The fence starts 20y from the centre (the replay judge's bound), and its navigation
    // danger (inflated 0.5y) starts at 19.5y: a landing beyond that leaves the solver unable
    // to start (NoReachableSafeCell, run1840 at 19.52y). Keep the landing at 19.4y; unobstructed
    // ARR pushes land within 0.1y of effect-position+15y. The constraint is exact (un-inflated)
    // because the generic 0.5y navigation margin makes the post-circle dash infeasible
    // (run1842/1846: 1.54s between the two effects).
    internal const float LandingHalfWidth = 19.4f;
    // Preceding circles are registered exactly at R20+0.35 (Low3CrucibleCastAOEs).
    internal const float CircleMargin = 0.35f;
    private const float StageCircleClearance = CircleMargin + 0.1f, StageEdgeClearance = CircleMargin + 0.02f;
    private const float StageGrid = 0.25f;
    // Without preceding circles the solver stages the launch; take over only for the final dash.
    private const double DashWindow = 2.5;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (ActiveKnockbacks(slot, actor).Length == 0)
        {
            AnticipateStage(slot, actor, hints);
            return;
        }
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
            {
                hints.ForbiddenZones.Add(new(new LandingZone([kb], Module.Center, LandingHalfWidth, LandingHalfWidth, [], CellStep), kb.Activation, null, ExactGeometry: true));
                var circles = (Module.FindComponent<XBMB14AOE>()?.ActiveAOEs(slot, actor).ToArray() ?? [])
                    .Where(a => a.Shape is AOEShapeCircle { Radius: 20 } && a.Activation != default && a.Activation < kb.Activation).ToArray();
                // Five circles cover the field: the centre is both the overlap-minimising spot
                // and a safe launch, so B14FiveCircleOverlap alone handles that pattern.
                if (circles.Length is >= 1 and <= 4)
                {
                    var key = string.Join(";", circles.Select(a => $"{a.Origin.X:F1},{a.Origin.Z:F1}").Order());
                    if (_planKey != key)
                    {
                        _planKey = key;
                        _stageGoal = PlanStage(actor.Position, kb.Origin, circles, circles.Min(a => a.Activation));
                    }
                    if (_stageGoal is { } stage)
                    {
                        hints.GoalDirectedTemporalPlanning = true;
                        hints.GoalPoints.Add((stage, .35f, 100));
                        // The solver keeps its own clearance from the circle edge (~1y here);
                        // that metre is the margin of the post-circle dash. Walk the exact
                        // spot directly; the route is still validated against every danger.
                        hints.DirectRoute = [stage];
                    }
                }
                else if (circles.Length == 0 && (_planKey != null || kb.Activation <= WorldState.FutureTime(DashWindow))
                    && !LandingZoneSafe(actor.Position, kb.Origin, LandingHalfWidth - .05f, Module.Center)
                    && LaunchPoints(kb.Origin).Count > 0)
                {
                    // Every preceding circle has resolved (they leave ActiveAOEs on the effect), or the
                    // effect is close. Dash at once to the nearest launch point from where the player
                    // really is; the solver's publication delay would cost the window.
                    var sector = NearestLaunch(actor.Position);
                    hints.DirectRoute = [sector];
                    hints.DirectRouteDeadline = kb.Activation.AddSeconds(-NavigationBuffer);
                }
            }
    }
    // In every recorded B14 pull (22 runs) the 2-4 circle set cast at ~103.5s is followed by
    // 粉碎剑 cast ~2.5s later from the stationary ranger, with its effect ~1.5s after the
    // circles. If the player learns of the push only at its cast start, the reachable circle-free
    // spot can be on the wrong side (run1835: 8.7y dash; run1823@0.3: the far pocket). While
    // the circles are cast, stage against a push predicted from the ranger's current position.
    // The stage is circle-safe either way; the real cast replans from wherever the player is.
    private void AnticipateStage(int slot, Actor actor, AIHints hints)
    {
        var ranger = Module.PrimaryActor;
        if (ranger.IsDeadOrDestroyed || actor.IsDeadOrDestroyed) return;
        var circles = (Module.FindComponent<XBMB14AOE>()?.ActiveAOEs(slot, actor).ToArray() ?? [])
            .Where(a => a.Shape is AOEShapeCircle { Radius: 20 } && a.Activation != default).ToArray();
        if (circles.Length is < 1 or > 4) return;
        var key = "anticipated|" + string.Join(";", circles.Select(a => $"{a.Origin.X:F1},{a.Origin.Z:F1}").Order());
        if (_planKey != key)
        {
            _planKey = key;
            _stageGoal = PlanStage(actor.Position, ranger.Position, circles, circles.Min(a => a.Activation));
        }
        if (_stageGoal is { } stage)
        {
            hints.GoalDirectedTemporalPlanning = true;
            hints.GoalPoints.Add((stage, .35f, 100));
            hints.DirectRoute = [stage];
        }
    }

    private static bool LandingZoneSafe(WPos p, WPos origin, float half, WPos center)
    {
        var away = p - origin;
        var landing = p + 15 * (away.LengthSq() < 1e-4f ? new WDir(0, 1) : away.Normalized());
        return Math.Abs(landing.X - center.X) <= half && Math.Abs(landing.Z - center.Z) <= half;
    }

    // Boundary cells of the safe launch set (15y radial push from the caster lands inside the
    // fence), on the same 0.5y cells FrozenConstraintGeometry rasterises the landing constraint
    // into. That raster forbids a whole cell when its centre or any corner is forbidden, so a cell
    // counts only when all five samples are safe; any point inside such a cell then passes.
    private const float CellStep = .2f, CellHalf = CellStep / 2, CellInset = CellHalf - .02f;
    private List<WPos> LaunchPoints(WPos origin)
    {
        if (_launchOrigin == origin) return _launch;
        _launchOrigin = origin;
        var center = Module.Center;
        // Raster cell centres are centre + (k + 0.5) * step - RasterRadius; keep those inside ±20.
        var first = -LandingZone.RasterRadius + CellHalf + MathF.Ceiling((LandingZone.RasterRadius - 20) / CellStep) * CellStep;
        var m = (int)MathF.Floor(-2 * first / CellStep + .001f) + 1;
        WPos Cell(int i, int j) => center + new WDir(first + i * CellStep, first + j * CellStep);
        bool Safe(WPos p) => LandingZoneSafe(p, origin, LandingHalfWidth - .01f, center);
        bool Launch(int i, int j)
        {
            if (i < 0 || j < 0 || i >= m || j >= m) return false;
            var c = Cell(i, j);
            return Safe(c) && Safe(c + new WDir(-CellHalf, -CellHalf)) && Safe(c + new WDir(CellHalf, -CellHalf))
                && Safe(c + new WDir(-CellHalf, CellHalf)) && Safe(c + new WDir(CellHalf, CellHalf));
        }
        _launch.Clear();
        for (var i = 0; i < m; ++i)
            for (var j = 0; j < m; ++j)
                if (Launch(i, j) && !(Launch(i - 1, j) && Launch(i + 1, j) && Launch(i, j - 1) && Launch(i, j + 1)))
                    _launch.Add(Cell(i, j));
        return _launch;
    }

    // Nearest point of the boundary launch cells (kept just inside each cell).
    private WPos NearestLaunch(WPos p)
    {
        var best = p; var gap = float.MaxValue;
        foreach (var q in _launch)
        {
            var c = new WPos(Math.Clamp(p.X, q.X - CellInset, q.X + CellInset), Math.Clamp(p.Z, q.Z - CellInset, q.Z + CellInset));
            var d = (c - p).LengthSq();
            if (d < gap) { gap = d; best = c; }
        }
        return best;
    }
    // Choose the circle-free waiting spot nearest to the set of safe launch points
    // (whose 15y radial push from the caster lands inside the fence), and the launch
    // point nearest to it. The landing constraint still validates the actual route.
    private WPos? PlanStage(WPos start, WPos origin, Components.GenericAOEs.AOEInstance[] circles, DateTime circleActivation)
    {
        var center = Module.Center;
        var launch = LaunchPoints(origin);
        if (launch.Count == 0) return null;
        var n = (int)MathF.Round(2 * 19f / StageGrid) + 1;
        // Anything farther than the player can walk before the circles resolve is not a stage.
        // 3y covers the danger pre-buffers (0.25s + 0.2s) and a 0.3s publication delay at 6y/s.
        var reach = MathF.Max(4, 6f * (float)(circleActivation - WorldState.CurrentTime).TotalSeconds - 3f);
        WPos? best = null; var score = float.MaxValue;
        WPos Point(int i, int j) => center + new WDir(-19f + i * StageGrid, -19f + j * StageGrid);
        bool Free(int i, int j)
        {
            if (i < 0 || j < 0 || i >= n || j >= n) return false;
            var p = Point(i, j);
            foreach (var c in circles)
                if ((p - c.Origin).LengthSq() < (20 + StageCircleClearance) * (20 + StageCircleClearance)) return false;
            return true;
        }
        for (var i = 0; i < n; ++i)
            for (var j = 0; j < n; ++j)
            {
                var p = Point(i, j);
                if ((p - start).LengthSq() > reach * reach || !Free(i, j)) continue;
                // The nearest launch point from the free region is seen from its border, unless
                // a free spot is itself a launch point.
                var gap = LandingZoneSafe(p, origin, LandingHalfWidth - CellStep, center) ? 0f
                    : Free(i - 1, j) && Free(i + 1, j) && Free(i, j - 1) && Free(i, j + 1) ? float.NaN
                    : (NearestLaunch(p) - p).Length();
                if (float.IsNaN(gap)) continue;
                var cost = gap + .01f * (p - start).Length();
                if (cost < score) { score = cost; best = p; }
            }
        // The 0.25y grid leaves up to ~0.1y of extra clearance: slide toward the nearest
        // launch point until the exact circle margin.
        if (best is { } coarse && !LandingZoneSafe(coarse, origin, LandingHalfWidth - CellStep, center))
        {
            var dir = (NearestLaunch(coarse) - coarse).Normalized();
            for (var s = .02f; s <= .5f; s += .02f)
            {
                var p = coarse + dir * s;
                if (circles.Any(c => (p - c.Origin).Length() < 20 + StageEdgeClearance)) break;
                best = p;
            }
        }
        return best;
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
    // Choose a fixed staging side from the boss's visible entry position, not
    // from any future random jump. The following cast is the observed cue.
    private readonly WDir _entrySide = (module.PrimaryActor.Position-module.Center).LengthSq() > 1
        ? (module.PrimaryActor.Position-module.Center).Normalized() : new(0,-1);
    private bool _preparingSong;
    private DateTime _jumpExpected;
    private WPos _preJump;
    // run2005 b5/b6: 48564 left CastFinishAt (incl. 0.3s NPC delay) +6.19/+6.19s after
    // 48563 and +8.28/+8.14s after 48571. Stage by the earliest sample less 0.45s.
    private const double SongJumpLead = 5.7, MarchJumpLead = 7.7;
    private const float StageWalkSpeed = 5f;
    private const double StageReserve = 1.5;
    private DateTime _landingUntil;
    private WPos _origin;
    private Angle _rotation;
    private DateTime _activation;
    private DateTime _expires;
    private int _pulses;
    private readonly HashSet<uint> _events = [];
    private static readonly AOEShapeCircle Spot = new(1.2f);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && spell.Action.ID is 48563 or 48571)
        {
            _preparingSong = true;
            _jumpExpected = Module.CastFinishAt(spell, spell.Action.ID == 48563 ? SongJumpLead : MarchJumpLead);
        }
    }

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
        if (spell.Action.ID == 48564)
        {
            _preparingSong = false;
            _preJump = caster.Position;
            _landingUntil = WorldState.FutureTime(3);
            if (spell.TargetXZ.InCircle(Module.Center, 25))
                Begin(spell.TargetXZ, Angle.FromDirection(Module.Center - spell.TargetXZ), 2.2d);
        }
        else if (spell.Action.ID == 48565)
        {
            _landingUntil = default;
            // The teleport's own effect can retain the pre-jump facing. The
            // fixed song channel faces the center after landing (native ARR
            // run2005 first setup -76deg, all following pulses +90deg).
            Begin(caster.Position, Angle.FromDirection(Module.Center - caster.Position), 1.05d);
        }
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

    public override void Update()
    {
        base.Update();
        var position = Module.PrimaryActor.Position;
        // The jump-start packet can have no destination. Wait for the actual
        // visible relocation (run2005: 1.512s before the first pulse), rather
        // than read any future cast location.
        if (_landingUntil > WorldState.CurrentTime && !position.AlmostEqual(_preJump, 3)
            && (position - Module.Center).Length() >= 18)
        {
            Begin(position, Angle.FromDirection(Module.Center - position), 1.3d);
            _landingUntil = default;
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

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var sprintUntil = actor.FindStatus(50)?.ExpireAt;
        var observedSprint = sprintUntil is { } until && until != DateTime.MaxValue && until > WorldState.CurrentTime;
        // A live, long-enough sprint makes the centre the direction-independent
        // reaction point. Without it retain the existing walking-only staging.
        var sprintStage = observedSprint && sprintUntil > _jumpExpected.AddSeconds(3);
        var prepare = sprintStage ? Module.Center : Module.Center + 18f*_entrySide;
        // run2005: staging from the cast start stood at the rim 7–10s before the jump.
        if (_preparingSong && _expires <= WorldState.CurrentTime && !actor.Position.InCircle(prepare,1)
            && _jumpExpected > WorldState.FutureTime((actor.Position-prepare).Length()/StageWalkSpeed + StageReserve))
        {
            // Not due yet: keep attacking; nothing else about the jump is known.
        }
        else if (_preparingSong && _expires <= WorldState.CurrentTime)
        {
            hints.GoalPoints.Add((prepare,.5f,200));
            if (sprintStage && !actor.Position.InCircle(prepare,.5f))
            {
                hints.DirectRoute ??= [prepare];
                hints.DirectRouteDeadline = _jumpExpected;
            }
            hints.GoalDirectedTemporalPlanning = true;
            hints.NoEscapeDelay = true;
            if (!actor.Position.InCircle(prepare,1)) hints.MaxCastTime = 0;
        }
        var spots = ActiveAOEs(slot, actor).ToArray().Select(a => a.Origin).ToArray();
        if (spots.Length > 0)
        {
            // Conservative avoidance envelope above the independently observed
            // <48.6deg bound; 50deg is not asserted as the native cone angle.
            var envelope = new AOEShapeCone(50, 50f.Degrees());
            // A landing can leave the player too far forward to clear the first
            // pulse. Keep escaping instead of repeatedly replacing a reachable
            // route with NoTemporalPath at every .58s pulse. The independent
            // replay referee still charges every pulse that actually hits.
            var offset = actor.Position - _origin;
            var fromCenter = _origin-Module.Center;
            var exits = new[] {-53f, 53f}.Select(a => (_rotation+a.Degrees()).ToDirection())
                .Select(d =>
                {
                    // Project onto the in-arena segment of the same 53deg ray.
                    // Dropping an out-of-arena perpendicular projection loses
                    // the nearby flank for players already staged at the rim.
                    var b = fromCenter.Dot(d);
                    var disc = b*b + 18.7f*18.7f-fromCenter.LengthSq();
                    if (disc < 0) return ((WPos, WPos)?)null;
                    var root = MathF.Sqrt(disc);
                    var low = MathF.Max(4,-b-root);
                    var high = MathF.Min(30,-b+root);
                    // Exit: nearest point on the ray. Flank: the ray's inner end, within
                    // melee of the R4 siren; the 130deg wedge from the ray to the back is
                    // convex, so walking in along it never re-enters the cone.
                    return low <= high ? (_origin+d*Math.Clamp(offset.Dot(d),low,high), _origin+d*low) : null;
                })
                .Where(p => p.HasValue).Select(p => p!.Value)
                .OrderBy(p => (p.Item1-actor.Position).LengthSq()).ToArray();
            var recovery = exits.Length > 0 && envelope.Check(actor.Position,_origin,_rotation)
                && (exits[0].Item1-actor.Position).Length()/6f+.4 > (_activation-WorldState.CurrentTime).TotalSeconds-.25;
            if (!recovery)
                CrucibleScorpionAI.Avoid(hints, new(envelope, _origin, _rotation, _activation));
            // run2005: holding the projected exit kept the player 10–17y off the boss for
            // the whole 7s channel. Once clear of the envelope, close in along the ray.
            if (exits.Length > 0)
            {
                var exit = envelope.Check(actor.Position,_origin,_rotation) ? exits[0].Item1 : exits[0].Item2;
                hints.GoalPoints.Add((exit, .3f, 200));
                // The existing recovery branch already keeps escaping after a
                // missed deadline. Avoid another background solve, without
                // changing its target, cone or any registered danger checks.
                if (recovery && observedSprint && !actor.Position.InCircle(exit,.3f))
                    hints.DirectRoute ??= [exit];
            }
            foreach (var spot in spots)
                hints.GoalPoints.Add((spot, 1.2f, 100));
            hints.GoalDirectedTemporalPlanning = true;
            hints.NoEscapeDelay = true;
            hints.MaxCastTime = 0;
            hints.ForceCancelCast = true;
        }
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19617)
        {
            _preparingSong = false;
            _expires = default;
            _landingUntil = default;
        }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// R8 bombs resolve ~3.5s before the R40 bite. Allow the current safe gaps while
// staging close enough to the far end that the large circle can be escaped.
sealed class CrucibleGatlerExplosions(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private readonly Dictionary<ulong, WPos> _knifePositions = [];
    private readonly Dictionary<ulong, AOEInstance> _earlyKnives = [];
    private readonly List<AOEInstance> _visible = [];
    private ulong _openingCaster;
    private DateTime _openingActivation;
    private WPos _openingOrigin;
    private bool _bombsRevealed;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID is 48594 or 48595 or 48596 or 48597)
        {
            _bombsRevealed = true;
            foreach (var (id, aoe) in _earlyKnives.ToArray())
                if (aoe.Origin.AlmostEqual(spell.LocXZ, .5f)) _earlyKnives.Remove(id);
        }
        else if (spell.Action.ID == 48603)
            foreach (var pending in Pending)
                if (pending.ActionID == 48603 && pending.AOE.ActorID == caster.InstanceID
                    && (_openingCaster != caster.InstanceID || _openingActivation != pending.AOE.Activation))
                {
                    _openingCaster = caster.InstanceID;
                    _openingActivation = pending.AOE.Activation;
                    _openingOrigin = pending.AOE.Origin;
                    _bombsRevealed = false;
                }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48603 && caster.InstanceID == _openingCaster)
            _openingCaster = 0;
        base.OnCastFinished(caster, spell);
    }

    public override void OnActorDeath(Actor actor)
    {
        _knifePositions.Remove(actor.InstanceID);
        _earlyKnives.Remove(actor.InstanceID);
        if (actor.InstanceID == _openingCaster || actor.OID == 19626)
            _openingCaster = 0;
        base.OnActorDeath(actor);
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);

    public override void Update()
    {
        base.Update();
        foreach (var knife in Module.Enemies(19627))
        {
            if (knife.IsDeadOrDestroyed) continue;
            // Native 19627 actors reveal their new R2 centers before the very
            // short helper casts (run2005: teleport 26.329, cast 26.413, hit
            // 26.909..27.314). React to an observed relocation, never a future
            // helper location or the common staging position on initial spawn.
            if (_knifePositions.TryGetValue(knife.InstanceID, out var previous)
                && !knife.Position.AlmostEqual(previous, 3))
                _earlyKnives[knife.InstanceID] = new(new AOEShapeCircle(2), knife.Position,
                    activation: WorldState.FutureTime(.6));
            _knifePositions[knife.InstanceID] = knife.Position;
        }
        foreach (var (id, aoe) in _earlyKnives.ToArray())
            if (aoe.Activation.AddSeconds(1) < WorldState.CurrentTime) _earlyKnives.Remove(id);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _visible.Clear();
        _visible.AddRange(base.ActiveAOEs(slot, actor).ToArray());
        _visible.AddRange(_earlyKnives.Values);
        return CollectionsMarshal.AsSpan(_visible);
    }

    protected override double RiskyActivationWindow => 0.5d;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        48594 or 48595 or 48596 => new(new AOEShapeCircle(2), true),
        48597 => new(new AOEShapeCircle(8), true),
        48600 => new(new AOEShapeRect(50, 20), true),
        48603 => new(new AOEShapeCircle(40), true),
        _ => null
    };

    private EscapeRoutes? _escape;

    // Cache walking distances on the actual floor, once per large cast. A
    // straight-line estimate can cut across burning notches or choose a false
    // corner refuge. R40 escapes to the opposite tip; the rectangle goes behind.
    private sealed class EscapeRoutes
    {
        public readonly AOEInstance AOE;
        private const int Width = 61, Height = 101;
        private readonly float[] _distance = new float[Width * Height];
        private static WPos Position(int x, int z) => new(505 + x * 0.5f, -445 + z * 0.5f);

        public EscapeRoutes(AOEInstance aoe)
        {
            AOE = aoe;
            Array.Fill(_distance, float.PositiveInfinity);
            var walkable = new bool[_distance.Length];
            var queue = new PriorityQueue<int, float>();
            var damage = aoe.Shape.Distance(aoe.Origin, aoe.Rotation);
            for (var z = 0; z < Height; ++z)
                for (var x = 0; x < Width; ++x)
                {
                    var i = z * Width + x;
                    var p = Position(x, z);
                    walkable[i] = CrucibleLaudaFloor.Contains(p);
                    if (walkable[i] && damage.Distance(p) >= 0.75f)
                    {
                        _distance[i] = 0;
                        queue.Enqueue(i, 0);
                    }
                }
            while (queue.TryDequeue(out var i, out var d))
            {
                if (d > _distance[i])
                    continue;
                var x = i % Width;
                var z = i / Width;
                for (var dz = -1; dz <= 1; ++dz)
                    for (var dx = -1; dx <= 1; ++dx)
                    {
                        var nx = x + dx;
                        var nz = z + dz;
                        if ((dx == 0 && dz == 0) || nx < 0 || nx >= Width || nz < 0 || nz >= Height)
                            continue;
                        var ni = nz * Width + nx;
                        if (!walkable[ni] || !walkable[z * Width + nx] || !walkable[nz * Width + x])
                            continue;
                        var next = d + (dx == 0 || dz == 0 ? 0.5f : 0.707107f);
                        if (next < _distance[ni])
                        {
                            _distance[ni] = next;
                            queue.Enqueue(ni, next);
                        }
                    }
            }
        }
        public float Distance(WPos p)
        {
            var x = (int)MathF.Round((p.X - 505) * 2);
            var z = (int)MathF.Round((p.Z + 445) * 2);
            return x >= 0 && x < Width && z >= 0 && z < Height
                ? _distance[z * Width + x] + (p - Position(x, z)).Length() : float.PositiveInfinity;
        }
        public WPos NearestRefuge(WPos from)
        {
            var best = from;
            var distance = float.PositiveInfinity;
            for (var z = 0; z < Height; ++z)
                for (var x = 0; x < Width; ++x)
                    if (_distance[z * Width + x] == 0)
                    {
                        var candidate = Position(x, z);
                        // Both observed blade formations use the central
                        // column. Approach the circular bite refuge off that
                        // column so its short spawn hit does not intercept us.
                        if (AOE.Shape is AOEShapeCircle && Math.Abs(candidate.X - 520) < 3.5f)
                            continue;
                        var d = (candidate - from).LengthSq();
                        if (d < distance)
                        {
                            distance = d;
                            best = candidate;
                        }
                    }
            return best;
        }
    }
    private sealed class Staging(EscapeRoutes routes, float travel) : ShapeDistance
    {
        public override float Distance(in WPos p) => travel - routes.Distance(p);
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        foreach (ref readonly var aoe in aoes)
            CrucibleScorpionAI.Avoid(hints, aoe);
        if (_openingCaster != 0 && !_bombsRevealed && _openingActivation > WorldState.CurrentTime
            && Math.Abs(_openingOrigin.Z - Module.Center.Z) > 1)
        {
            var stage = Module.Center + new WDir(0, _openingOrigin.Z < Module.Center.Z ? 10 : -10);
            if (CrucibleLaudaFloor.Contains(stage))
                hints.GoalPoints.Add((stage, .5f, 2));
        }
        if (aoes.Length == 0)
            return;
        // The long bite cast precedes the tiny 0.2--0.6s blade-spawn hits.
        // Start towards an independently computed refuge while only that cast
        // is known; waiting in melee until the blades appear cannot reliably
        // leave their R2 telegraphs with a 300ms command delay.
        if (aoes.Length == 1 && aoes[0].Shape is AOEShapeCircle { Radius: 40 } or AOEShapeRect)
        {
            var aoe = aoes[0];
            if (_escape == null || _escape.AOE.Origin != aoe.Origin || _escape.AOE.Rotation != aoe.Rotation || _escape.AOE.Activation != aoe.Activation)
                _escape = new(aoe);
            hints.GoalPoints.Add((_escape.NearestRefuge(actor.Position), 0.75f, 100));
        }
        foreach (ref readonly var aoe in aoes)
            if (!aoe.Risky && aoe.Shape is AOEShapeCircle { Radius: 40 } or AOEShapeRect)
            {
                var lastBomb = aoes[0].Activation;
                foreach (ref readonly var earlier in aoes)
                    if (earlier.Risky && earlier.Activation > lastBomb)
                        lastBomb = earlier.Activation;
                // 6y/s after the LAST bomb; reserve 1.5y for reaction/turning.
                var travel = Math.Max(0f, 6f * (float)(aoe.Activation - lastBomb).TotalSeconds - 1.5f);
                if (_escape == null || _escape.AOE.Origin != aoe.Origin || _escape.AOE.Rotation != aoe.Rotation || _escape.AOE.Activation != aoe.Activation)
                    _escape = new(aoe);
                var routes = _escape;
                hints.AddForbiddenConstraint(new Staging(routes, travel), aoes[0].Activation.AddSeconds(-0.4d), aoes[0].Activation.AddSeconds(0.6));
                hints.GoalZones.Add(p => Math.Max(0, 50 - routes.Distance(p)));
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
sealed class CrucibleLahamuEarthshakerHint(BossModule module) : Components.CastHints(module, [48557u], "大地摇动：远离被点名单位的方向！")
{
    private Actor? _caster;
    private Angle _facing;
    private DateTime _expires;
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (spell.Action.ID != 48557 || spell.EventHappened)
            return;
        _caster = caster;
        _facing = spell.Rotation;
        _expires = Module.CastFinishAt(spell).AddSeconds(1);
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (spell.Action.ID == 48557 && spell.Interrupted)
            _expires = default;
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_caster is { IsDeadOrDestroyed: false } && WorldState.CurrentTime < _expires)
        {
            // The companion can move to a different side after the cast starts.
            // Avoid its current aimed half-plane; the player's own aimed hit
            // remains unavoidable and must still be paid by the HP ledger.
            var pet = WorldState.Actors.FirstOrDefault(a => a != actor && a.OwnerID == actor.InstanceID
                && !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0);
            var direction = _facing;
            if (pet != null && (pet.Position - _caster.Position).LengthSq() > .01f)
            {
                direction = Angle.FromDirection(pet.Position - _caster.Position);
                CrucibleScorpionAI.Avoid(hints, new(new AOEShapeCone(60, 90f.Degrees()),
                    _caster.Position, direction, _expires.AddSeconds(-1)));
            }
            var rear = _caster.Position - 6 * direction.ToDirection();
            if (rear.InCircle(Module.Center, 18))
                hints.GoalPoints.Add((rear, 1, 100));
        }
    }
}

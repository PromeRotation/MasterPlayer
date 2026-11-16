// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 48719 predicts both hits, including when loading before the helpers arrive.
// ARR: R13 steel, then the R30 ring two seconds later. Misses out to 6.15y
// establish a conservative R6 inner pocket, not the ring's exact inner edge.
sealed class CrucibleGargoyleInOut(BossModule module) : Components.GenericAOEs(module)
{
    internal static readonly AOEShapeCircle Steel = new(13);
    internal static readonly AOEShapeDonut Ring = new(6, 30);
    private AOEInstance? _steel;
    private AOEInstance? _ring;
    private DateTime _steelResolved;
    internal bool RingRunActive => _steel == null && _ring != null;
    private DateTime _ringResolved;
    private WPos? _stagingPoint;
    private (WPos Origin, WPos Position)? _ringPoint;
    private readonly List<AOEInstance> _display = [];

    public override void Update()
    {
        // Activation already includes the NPC finish delay. Waiting another
        // 250ms before requesting the return can consume its entire solve budget.
        if (_steel is { } steel && steel.Activation <= WorldState.CurrentTime)

            _steel = null;
        if (_ring is { } ring && ring.Activation.AddSeconds(1) < WorldState.CurrentTime)
            _ring = null;
        if (_steel == null && _ring == null)
            (_stagingPoint, _ringPoint, _stagingDir, _outerEscape) = (null, null, 0, null);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Action.ID is not (48719 or 48720 or 48721))
            return;
        Update();
        var finish = Module.CastFinishAt(spell);
        var steelTime = finish.AddSeconds(spell.Action.ID switch { 48719 => 0.6, 48721 => -2, _ => 0 });
        var ringTime = steelTime.AddSeconds(2);
        if (steelTime > WorldState.CurrentTime && _steelResolved.AddSeconds(1) < WorldState.CurrentTime
            && (spell.Action.ID == 48720 || _steel == null))
            _steel = new(Steel, spell.LocXZ, activation: steelTime, actorID: spell.Action.ID == 48720 ? caster.InstanceID : 0);
        if (ringTime > WorldState.CurrentTime && _ringResolved.AddSeconds(1) < WorldState.CurrentTime
            && (spell.Action.ID == 48721 || _ring == null))
            _ring = new(Ring, spell.LocXZ, activation: ringTime, actorID: spell.Action.ID == 48721 ? caster.InstanceID : 0);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Update();
        _display.Clear();
        if (_steel is { } steel)
        {
            steel.Color = Colors.Danger;
            _display.Add(steel);
        }
        if (_ring is { } ring)
        {
            ring.Risky = _steel == null;
            ring.Color = ring.Risky ? Colors.Danger : Colors.AOE;
            _display.Add(ring);
        }
        return CollectionsMarshal.AsSpan(_display);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
            if (aoe.Risky)
                aoe.Shape.Draw(Arena, aoe.Origin, aoe.Rotation, aoe.Color);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
            if (!aoe.Risky)
                aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, aoe.Color);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Update();
        // run1888-b3: with a chasing R6 puddle under the player, the uptime optimisation held the escape
        // for its departure reserve ("等待撤离时机"); the next puddle then dropped beside the late route and
        // the player stalled inside the first.
        if (PitchSeaActive())
            hints.NoEscapeDelay = true;
        if (_steel is { } steel)
        {
            // Two seconds must cover the return and event/worker latency. Keep
            // PR's margin without inflating the real shapes again in the facade.
            // run1898–1900: with a puddle under the player the inward run only starts when this zone ends;
            // 0.6s after the steel plus 0.5s start safety left no legal route to the ring pocket (NoTemporalPath).
            hints.ForbiddenZones.Add(new(Steel.Distance(steel.Origin, default), steel.Activation, steel.Activation.AddSeconds(SteelLinger), ExactGeometry: true));
            // During Pitch Sea, start the mine-free approach as soon as the steel is
            // known. A current-time leash with no goal pulled the player only a few
            // yalms inside its boundary, then stopped inside the chasing R6 puddle.
            // The later staging bound still guarantees the short return for the ring.
            // Match the visible countdown, excluding the NPC finish delay and 48719's 0.6s hit delay.
            var castFinish = Module.PrimaryActor.CastInfo is { Action.ID: 48719 } cast
                ? WorldState.FutureTime(cast.RemainingTime)
                : steel.Activation.AddSeconds(-ActorCastInfo.NPCFinishDelay);
            if (castFinish > WorldState.FutureTime(3) && !PitchSeaActive())
                return;
            if (_ring is { } next)
            {
                // The goal fixes the 13.5y staging distance. Leave room around that
                // route for PR's grid and hard margin instead of sealing the annulus.
                // During chasing puddles, this is a preference rather than a
                // physical wall: the player may need another staging angle first.
                // A timed whole-field constraint can require crossing the arena
                // before steel and turn a valid immediate escape into a long wait.
                var kiting = PitchSeaActive();
                if (!kiting)
                    hints.AddForbiddenConstraint(new SDInvertedCircle(steel.Origin, 15.5f), steel.Activation.AddSeconds(-0.5), steel.Activation.AddSeconds(SteelLinger));
                var mines = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? [];
                var approach = new RingApproach(next.Origin, mines);
                if (mines.Length > 0 && !kiting)
                    hints.AddForbiddenConstraint(approach, steel.Activation.AddSeconds(-0.5), steel.Activation.AddSeconds(SteelLinger));
                // A precise staging goal preserves the safe strip between the steel and
                // the outer positioning constraint after PR dilates both by its margin.
                var stagingApproach = new RingApproach(next.Origin, mines, 1.25f);
                // run1887: the cached staging point sat 5.6y from a pending Pitch Sea (R6); the player
                // waited on it for the steel and took 2743. Skip points inside other pending hits that
                // land while the player still waits there. run1890: a puddle dropped on the point itself
                // resolves with the ring, after the inward run; moving the point then sent the player
                // around a corner arc it could not cover and MP released inside the puddle.
                var hazards = Module.Components.OfType<Components.GenericAOEs>().Where(c => c != this && c is not (CrucibleBoundary or CrucibleGargoyleResidue))
                    .SelectMany(c => c.ActiveAOEs(slot, actor).ToArray())
                    .Where(a => a.Risky && a.Activation <= steel.Activation.AddSeconds(StagingHazardLead))
                    .Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray();
                // Pitch residues hurt only by standing in them (Panic tick): keep the point out of them, but
                // without the pending-hit clearance, and drop that preference before a hit-free point.
                var residue = Module.FindComponent<CrucibleGargoyleResidue>();
                bool HitFree(WPos p) => hazards.All(h => h.Distance(p) > StagingHazardClearance);
                bool Clear(WPos p) => HitFree(p) && residue?.Covers(p, 0.5f) != true;
                var ring = Enumerable.Range(0, 72).Select(i => steel.Origin + 13.5f * (i * 5f).Degrees().ToDirection()).Where(Arena.InBounds).ToArray();
                // run1900-b4 (0.3s worker): a point picked 21y around the circle while the chasing puddles covered the
                // near side stayed "valid" after those puddles landed; the player kited away from it and missed both the
                // staging and the ring pocket. Switch once a clearly nearer clear point exists.
                var nearestClear = ring.Where(p => stagingApproach.Distance(p) > 0 && Clear(p))
                    .Select(p => (p - actor.Position).Length()).DefaultIfEmpty(float.MaxValue).Min();
                if (_stagingPoint is not { } stage || MathF.Abs((stage - steel.Origin).Length() - 13.5f) > 0.01f
                    || !Arena.InBounds(stage) || stagingApproach.Distance(stage) <= 0 || !HitFree(stage)
                    || !Clear(stage) && nearestClear < float.MaxValue
                    || Module.FindComponent<CrucibleGargoyleResidue>() is not { ChasersThisWave: < StagingChasers } && (stage - actor.Position).Length() > nearestClear + StagingRepickGain)
                {
                    // run1901: with every candidate filtered out there was no goal at all and the planner fled
                    // 21y out into the fence, where the donut hit. Always keep some staging goal.
                    _stagingPoint = (ring.Where(p => stagingApproach.Distance(p) > 0 && Clear(p)).ToArray() is { Length: > 0 } clear ? clear
                        : ring.Where(p => stagingApproach.Distance(p) > 0 && HitFree(p)).ToArray() is { Length: > 0 } hitFree ? hitFree
                        : ring.Where(p => stagingApproach.Distance(p) > 0).ToArray() is { Length: > 0 } reachable ? reachable : ring)
                        .OrderBy(p => (p - actor.Position).LengthSq()).Select(p => (WPos?)p).FirstOrDefault();
                }
                // Wave 2 (agent-panic README): each chaser lands, and leaves a 33s residue, where the player stood at
                // its cast. Taking the nearest staging point parks the player on it for the early chasers (puddle and
                // residue on the point and the inward run). While chasers before the steel remain, stage at the farthest
                // clear point along the circle that one residue-free walk still reaches before the steel: the chasers
                // then drop behind the player, ~8y apart, and the point ahead stays clear.
                var wave = Module.FindComponent<CrucibleGargoyleResidue>();
                // run1988-b4 replay: the boss had dashed into a corner, the mines and the first chasers shut every
                // stage with a straight run inward, and the fallback stage sat inside a residue: the player stopped
                // 27y out, under its own puddles, and took the ring. With the boss cornered the ring's R30 ends inside
                // the arena; walking past it needs neither the stage nor the two-second dive.
                // run2031 t184: with a clear stage available the escape still ran 37y from the boss (21:54:19, 33s
                // without a GCD). Only take it when the stage is really gone.
                if (kiting && nearestClear == float.MaxValue && OuterEscape(slot, actor, next, mines) is { } beyond)
                {
                    hints.GoalPoints.Add((beyond, 1f, 100));
                    return;
                }
                WPos? holdGoal = null;
                if (kiting && wave?.PlannedArc is { } plan && plan.Center.InCircle(steel.Origin, 2))
                {
                    // Stage in the planned open arc; stand off from it while the early chasers are cast.
                    var target = ring.Where(p => Clear(p) && stagingApproach.Distance(p) > 0
                            && MathF.Abs(MathF.IEEERemainder(Angle.FromDirection(p - steel.Origin).Rad - plan.Angle, 2 * MathF.PI)) <= plan.Half)
                        .Select(p => (WPos?)p).MinBy(p => (p!.Value - wave.ArcPoint(plan)).LengthSq());
                    if (target is { } inArc && (_stagingPoint is not { } held || !Clear(held) || stagingApproach.Distance(held) <= 0
                            || wave.ChasersThisWave >= StagingChasers && (inArc - actor.Position).Length() + StagingRepickGain < (held - actor.Position).Length()
                            || MathF.Abs(MathF.IEEERemainder(Angle.FromDirection(held - steel.Origin).Rad - plan.Angle, 2 * MathF.PI)) > plan.Half))
                        _stagingPoint = inArc;
                    if (_stagingPoint is { } stagePoint && wave.ChasersThisWave < StagingChasers
                        && wave.Standoff(stagePoint, plan.Center, actor.Position, mines) is { } hold
                        && (steel.Activation - WorldState.CurrentTime).TotalSeconds > (stagePoint - hold).Length() * 1.1f / 6 + StandoffWalkInLead)
                        holdGoal = hold;
                }
                else if (kiting && wave is { ChasersThisWave: < StagingChasers })
                {
                    var budget = MathF.Max(0, (float)((steel.Activation - WorldState.CurrentTime).TotalSeconds - StagingWalkInLead)) * 6 * 0.9f;
                    var start = Angle.FromDirection(actor.Position - steel.Origin).Rad;
                    WPos? far = null;
                    var farArc = -1f;
                    var farDir = 0;
                    foreach (var dir in _stagingDir != 0 ? [_stagingDir] : new[] { 1, -1 })
                        for (var arc = 1f; arc <= budget; arc += 1)
                        {
                            var p = steel.Origin + 13.5f * ((start + dir * arc / 13.5f) * 180 / MathF.PI).Degrees().ToDirection();
                            if (!Arena.InBounds(p + new WDir(1.5f, 0)) || !Arena.InBounds(p - new WDir(1.5f, 0))
                                || !Arena.InBounds(p + new WDir(0, 1.5f)) || !Arena.InBounds(p - new WDir(0, 1.5f)) || residue?.Covers(p, 0.5f) == true)
                                break;
                            if (arc > farArc && Clear(p) && stagingApproach.Distance(p) > 0)
                                (far, farArc, farDir) = (p, arc, dir);
                        }
                    if (far is { } ahead && farArc >= StagingAheadMinimum)
                        (_stagingPoint, _stagingDir) = (ahead, farDir);
                }
                // run1888-b3 replay: walking across the circle's inside during a chaser cast left a residue 9y from the
                // boss that sealed the staging arc and the inward run. Keep out of the inner disk until all four are cast.
                if (kiting && wave is { ChasersThisWave: < 4 })
                    hints.AddForbiddenZone(new AOEShapeCircle(InnerDiskRadius), steel.Origin);

                if (holdGoal is { } holding)
                    hints.GoalPoints.Add((holding, 1f, 100));
                else if (_stagingPoint is { } point)
                {
                    // R0.2 refines the entire arena to 400x400 cells. R0.4 still
                    // fits wholly outside R13 at the R13.5 center, while leaving
                    // enough time to solve a new chasing puddle before it hits.
                    hints.GoalPoints.Add((point, 0.4f, 100));
                    if (kiting)
                    {
                        // This shortcut is only a request. The production executor
                        // still rejects any segment crossing mines, puddles, steel
                        // or the fence, and the normal search remains the fallback.
                        // run1904-b4 (0.3s worker): the direct traversal stopped short of a staging point that a
                        // new chasing puddle had just invalidated, inside the first puddle; only hand it over once
                        // no puddle is still pending before the steel.
                        if (!Module.FindComponent<XBMB26AOE>()!.HasPending(48729, steel.Activation.AddSeconds(-0.5)))
                        {
                            hints.DirectRoute = [point];
                            hints.DirectRouteDeadline = steel.Activation.AddSeconds(-0.5);
                        }
                    }
                }
            }
        }
        else if (_ring is { } ring)
        {
            // PR uses 0.5s normally and 0.25s for emergency routes. Even the
            // emergency deadline must precede the actual hit, never exceed it.
            hints.ForbiddenZones.Add(new(Ring.Distance(ring.Origin, default), ring.Activation.AddSeconds(-0.1), ring.Activation.AddSeconds(0.6), ExactGeometry: true));
            if (_outerEscape != null)
            {
                if (OuterEscape(slot, actor, ring, Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? []) is { } beyond)
                {
                    hints.GoalPoints.Add((beyond, 1f, 100));
                    return;
                }
                if (!actor.Position.InCircle(ring.Origin, Ring.OuterRadius + 0.5f))
                    return;
            }
            if (_ringPoint is not { } point || point.Origin != ring.Origin || RingPocketCovered(slot, actor, point.Position, ring.Activation))
            {
                var offset = actor.Position - ring.Origin;
                var destination = offset.LengthSq() > 25 ? ring.Origin + 5 * offset.Normalized() : actor.Position;
                if ((offset.LengthSq() > 25 || RingPocketCovered(slot, actor, destination, ring.Activation))
                    && PocketEscaping(slot, actor, ring) is { } escape)
                    destination = escape;
                _ringPoint = (ring.Origin, destination);
            }
            hints.GoalPoints.Add((_ringPoint.Value.Position, 0.4f, 100));
            // run1898–1902: a Pitch Sea puddle pending under the player on the staging ring made the timed
            // search refuse every route into the pocket (NoTemporalPath) and the player stood through both
            // hits. The straight run inward is the only move that helps; hand it over as a direct route the
            // runtime re-validates each frame instead of waiting for a search that cannot satisfy its margins.
            // Start as soon as the steel resolves. Waiting for another worker result can consume the
            // short return window even without Pitch Sea; the executor still checks every real hazard.
            hints.DirectRoute = [_ringPoint.Value.Position];
            hints.DirectRouteDeadline = ring.Activation.AddSeconds(-0.2);
        }
    }

    // A floor point beyond the ring's outer edge that one straight, residue- and mine-free walk still reaches in time.
    // Only exists while the boss stands near a fence (the far corner of a central boss is 26y away). Take the farthest
    // such point: each chaser lands where the player stood at its cast, so parking at the nearest one (the edge of the
    // region, by a corner) puts the last two chasers on the player and shuts it in (replay: pushed back into the ring).
    private WPos? OuterEscape(int slot, Actor actor, AOEInstance ring, WPos[] mines)
    {
        var from = actor.Position;
        var residue = Module.FindComponent<CrucibleGargoyleResidue>();
        var live = residue?.Live().ToArray() ?? [];
        var puddles = Module.FindComponent<XBMB26AOE>()?.ActiveAOEs(slot, actor).ToArray()
            .Where(a => a.Activation <= ring.Activation.AddSeconds(0.5) && a.Shape is AOEShapeCircle)
            .Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray() ?? [];
        var reach = MathF.Max(0, (float)(ring.Activation - WorldState.CurrentTime).TotalSeconds - OuterEscapeLead) * 6 * 0.9f;
        bool Usable(WPos p)
        {
            var length = (p - from).Length();
            if (length > reach && from.InCircle(ring.Origin, OuterEscapeRadius) || p.InCircle(ring.Origin, OuterEscapeRadius)
                || !residue!.OnStagingCircle(p) || residue.Covers(p, 0.5f) || puddles.Any(d => d.Distance(p) <= StagingHazardClearance)
                || mines.Any(m => m.InCircle(p, CrucibleMiasma.ContactRadius + 0.5f)))
                return false;
            if (length < 0.01f)
                return true;
            var dir = (p - from) / length;
            bool Clips(WPos c, float r) => c.InCircle(from + Math.Clamp((c - from).X * dir.X + (c - from).Z * dir.Z, 0, length) * dir, r);
            return !live.Any(r => !from.InCircle(r, CrucibleGargoyleResidue.Radius + 0.5f) && Clips(r, CrucibleGargoyleResidue.Radius + 0.5f))
                && !mines.Any(m => Clips(m, CrucibleMiasma.ContactRadius + 0.5f));
        }
        if (residue == null)
            return null;
        if (_outerEscape is { } kept && Usable(kept))
            return kept;
        _outerEscape = null;
        var best = -1f;
        for (var x = -18f; x <= 18; x += 1.5f)
            for (var z = -18f; z <= 18; z += 1.5f)
            {
                var p = Module.Center + new WDir(x, z);
                var travel = (p - from).LengthSq();
                if (travel > best && Usable(p))
                    (_outerEscape, best) = (p, travel);
            }
        return _outerEscape;
    }
    private WPos? _outerEscape;
    internal const float OuterEscapeRadius = 31.5f;
    internal const float OuterEscapeLead = 0.8f;

    private bool RingPocketCovered(int slot, Actor actor, WPos point, DateTime until)
        => Module.FindComponent<CrucibleGargoyleResidue>()?.Covers(point, 1) == true
            || Module.FindComponent<XBMB26AOE>()?.ActiveAOEs(slot, actor).ToArray()
                .Any(a => a.Risky && a.Activation <= until.AddSeconds(0.5) && a.Shape.Distance(a.Origin, a.Rotation).Distance(point) <= 1) == true;

    // run1900-b3 (0.3s worker): the last chasing puddle, dropped while the player walked onto the staging point,
    // lands ~0.4s before the ring. The radial run to the pocket crossed 7.7y of it and missed its own start margin
    // by 0.06s; the shortcut was withdrawn and the player stood through both hits. Among pocket points the runner
    // can still reach, take the one that leaves the pending puddles soonest (mine-free straight run).
    private WPos? PocketEscaping(int slot, Actor actor, AOEInstance ring)
    {
        var from = actor.Position;
        var puddles = Module.FindComponent<XBMB26AOE>()?.ActiveAOEs(slot, actor).ToArray()
            .Where(a => a.Activation <= ring.Activation.AddSeconds(0.5) && a.Shape is AOEShapeCircle)
            .Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray() ?? [];
        const float Margin = 1f;
        var residue = Module.FindComponent<CrucibleGargoyleResidue>();
        var radial = ring.Origin + 5 * (from - ring.Origin).Normalized();
        if (!puddles.Any(p => p.Distance(from) <= Margin || p.Distance(radial) <= Margin) && residue?.Covers(radial, Margin) != true)
            return null;
        var mines = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? [];
        // Leave ~0.25s for the start and keep 0.2s before the ring.
        var reach = MathF.Max(0, (float)(ring.Activation - WorldState.CurrentTime).TotalSeconds - 0.45f) * 6f;
        WPos? best = null;
        var bestExit = float.MaxValue;
        foreach (var radius in new[] { 5f, 4.5f })
            for (var deg = 0; deg < 360; deg += 5)
            {
                var p = ring.Origin + radius * ((float)deg).Degrees().ToDirection();
                var length = (p - from).Length();
                // The electric fence is a danger band along the arena edge; keep the pocket point clear of it.
                if (length > reach || puddles.Any(d => d.Distance(p) <= Margin) || residue?.Covers(p, Margin) == true
                    || !Arena.InBounds(p + new WDir(1.5f, 0)) || !Arena.InBounds(p - new WDir(1.5f, 0))
                    || !Arena.InBounds(p + new WDir(0, 1.5f)) || !Arena.InBounds(p - new WDir(0, 1.5f)))
                    continue;
                if (length < 0.01f)
                    return p;
                var dir = (p - from) / length;
                if (mines.Any(m => m.InCircle(from + Math.Clamp((m - from).X * dir.X + (m - from).Z * dir.Z, 0, length) * dir, CrucibleMiasma.ContactRadius + 0.5f)))
                    continue;
                if (residue?.Live().Any(r => !from.InCircle(r, CrucibleGargoyleResidue.Radius + Margin)
                    && r.InCircle(from + Math.Clamp((r - from).X * dir.X + (r - from).Z * dir.Z, 0, length) * dir,
                        CrucibleGargoyleResidue.Radius + Margin)) == true)
                    continue;
                var exit = 0f;
                while (exit < length && puddles.Any(d => d.Distance(from + exit * dir) <= Margin))
                    exit += 0.1f;
                if (exit < bestExit - 0.05f || MathF.Abs(exit - bestExit) <= 0.05f && length < (best!.Value - from).Length())
                    (best, bestExit) = (p, exit);
            }
        return best;
    }

    internal const double SteelLinger = 0.6; // run1901: 0.3 let the steel/ring catch the player twice; keep the original window
    internal const float StagingHazardClearance = 1.5f;
    internal const double StagingHazardLead = 0.5;
    internal const float StagingRepickGain = 4f;
    internal const int StagingChasers = 3;
    internal const float InnerDiskRadius = 12f;
    internal const double StagingWalkInLead = 1.2;
    internal const double StandoffWalkInLead = 0.6; // the third chaser is cast 3s before the steel; the walk must still fit after it
    internal const float StagingAheadMinimum = 8f;
    private int _stagingDir;
    private bool PitchSeaActive()
        => Module.FindComponent<CrucibleGargoyleResidue>()?.PitchWave ?? (Module.PrimaryActor.CastInfo?.Action.ID == 48728 || Module.FindComponent<XBMB26AOE>()?.HasPending(48729) == true);

    // Pick a staging angle with a straight path into the ring pocket. There
    // are only two seconds between hits; detouring around a ball after steel
    // is too slow once movement latency is included.
    private sealed class RingApproach(WPos origin, WPos[] mines, float clearance = 0.5f) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var offset = origin - p;
            var length = offset.Length();
            if (length < 5.3f)
                return 1;
            var direction = offset / length;
            foreach (var mine in mines)
            {
                var toMine = mine - p;
                var along = Math.Clamp(toMine.X * direction.X + toMine.Z * direction.Z, 0, length - 5.3f);
                if (mine.InCircle(p + along * direction, CrucibleMiasma.ContactRadius + clearance))
                    return 0;
            }
            return 1;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48720 && (_steel?.ActorID is null or 0 || _steel.Value.ActorID == caster.InstanceID))
        {
            _steel = null;
            _steelResolved = WorldState.CurrentTime;
        }
        if (spell.Action.ID == 48721 && (_ring?.ActorID is null or 0 || _ring.Value.ActorID == caster.InstanceID))
        {
            _ring = null;
            _ringResolved = WorldState.CurrentTime;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Interrupted)
        {
            if (spell.Action.ID == 48719)
                (_steel, _ring) = (null, null);
            if (spell.Action.ID == 48720 && _steel?.ActorID == caster.InstanceID)
                _steel = null;
            if (spell.Action.ID == 48721 && _ring?.ActorID == caster.InstanceID)
                _ring = null;
        }
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19650)
            (_steel, _ring) = (null, null);
        else
        {
            if (_steel?.ActorID == actor.InstanceID)
                _steel = null;
            if (_ring?.ActorID == actor.InstanceID)
                _ring = null;
        }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// Each chasing Pitch Sea (48729) leaves a pitch residue (EventObj 2004541 / 0x1E963D) at its cast location:
// it appears ~1s after the hit and lasts ~32.8s. Standing in it applies Panic (4167) on a 3s server tick, which
// then runs the player 5y/s in a random heading for 5s, half the time into the electric fence (agent-panic
// README: 42/42 panics within R6.5 of a live residue across 14 recorded fights). Keep it as a persistent hazard
// from the moment the puddle resolves; every component that reads risky GenericAOEs (pocket choice, ball
// reachability, staging/ring points) sees it as well. Built from the cast location, so the offline replay's
// chasers (cast at the simulated player's position) get their own residues.
sealed class CrucibleGargoyleResidue(BossModule module) : Components.GenericAOEs(module, warningText: "离开沥青残留，防止恐慌！")
{
    internal const uint PitchSea = 48729, ResidueOid = 0x1E963D;
    internal const float Radius = 6.5f; // 6 + hitbox; MP's danger margin adds its own navigation clearance.
    internal const double Lifetime = 34; // ~1s spawn delay + 32.8s until eventState 7, rounded up.
    internal static readonly AOEShapeCircle Shape = new(Radius);
    private readonly Dictionary<ulong, WPos> _casts = [];
    private readonly List<(WPos Position, DateTime Until)> _residues = [];
    private readonly List<AOEInstance> _active = [];

    internal IEnumerable<WPos> Live()
    {
        PruneResidues();
        return _residues.Select(r => r.Position);
    }

    // Perf: bumped whenever the residue set changes; keys the reachability cache below.
    private int _residueVersion;
    private void PruneResidues()
    {
        var now = WorldState.CurrentTime;
        foreach (var r in _residues)
            if (r.Until <= now)
            {
                _residues.RemoveAll(e => e.Until <= now);
                ++_residueVersion;
                return;
            }
    }

    internal bool Covers(WPos p, float margin = 0)
    {
        PruneResidues();
        foreach (var r in _residues)
            if (p.InCircle(r.Position, Radius + margin))
                return true;
        return false;
    }

    // Wave-2 staging plan (agent-panic README / run1888-b3 replay): at the Pitch Sea cast, take the largest arc of the
    // 13.5y staging circle that is inside the fence margin and residue-free. The chasers are then kited just outside
    // that arc, so their residues never land on it, and the player walks in for the steel.
    internal readonly record struct StagingArc(WPos Center, float Angle, float Half);
    internal StagingArc? PlannedArc { get; private set; }
    internal const float ArcRadius = 13.5f, HoldGap = 8f;

    internal bool OnStagingCircle(WPos p) => Module.Arena.InBounds(p + new WDir(1.5f, 0)) && Module.Arena.InBounds(p - new WDir(1.5f, 0))
        && Module.Arena.InBounds(p + new WDir(0, 1.5f)) && Module.Arena.InBounds(p - new WDir(0, 1.5f));

    private StagingArc? LargestFreeArc(WPos center)
    {
        const int N = 72;
        var free = new bool[N];
        for (var i = 0; i < N; ++i)
        {
            var p = center + ArcRadius * (i * 360f / N).Degrees().ToDirection();
            free[i] = OnStagingCircle(p) && !Covers(p, 0.5f);
        }
        if (free.All(f => f))
            return null; // open circle: no side to protect
        var (bestStart, bestLength) = (-1, 0);
        for (var s = 0; s < N; ++s)
        {
            if (!free[s] || free[(s + N - 1) % N])
                continue;
            var length = 0;
            while (length < N && free[(s + length) % N])
                ++length;
            if (length > bestLength)
                (bestStart, bestLength) = (s, length);
        }
        if (bestStart < 0)
            return null;
        var step = 2 * MathF.PI / N;
        return new(center, (bestStart + (bestLength - 1) / 2f) * step, (bestLength - 1) / 2f * step);
    }

    // A point on the staging circle just outside the planned arc, on the side nearer the player, still on the floor.
    internal WPos? HoldPoint(StagingArc arc, WPos player)
    {
        // Never hold where a pending chaser lands: a goal inside a pending hit still wins the search, and the
        // player then waits in its own puddle (r19 replays: first chaser hit at every hold).
        var pending = Module.FindComponent<XBMB26AOE>()?.ActiveAOEs(0, Module.Raid.Player() ?? Module.PrimaryActor).ToArray()
            .Where(a => a.Shape is AOEShapeCircle).Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray() ?? [];
        bool Usable(WPos p) => OnStagingCircle(p) && !Covers(p, 0.5f) && pending.All(d => d.Distance(p) > 1.5f);
        WPos At(float angle) => arc.Center + ArcRadius * (angle * 180 / MathF.PI).Degrees().ToDirection();
        var toPlayer = MathF.IEEERemainder(Angle.FromDirection(player - arc.Center).Rad - arc.Angle, 2 * MathF.PI);
        var sides = toPlayer >= 0 ? new[] { 1, -1 } : new[] { -1, 1 };
        // Outside the arc first, stepping further out past pending puddles.
        foreach (var side in sides)
            for (var extra = 0f; extra <= 10; extra += 1)
                if (At(arc.Angle + side * (arc.Half + (HoldGap + extra) / ArcRadius)) is var p && Usable(p))
                    return p;
        return null;
    }
    // run1951 (b3 191s/291s, b4 190s): with the boss in a corner the old hold point was gone after one chaser, so the
    // player parked on the staging point, the third chaser landed on it, and every way into the ring pocket was shut:
    // it fled to 20–26y and only a pet's cover stopped the ring (3195, 2764, 3230, each killing or crippling a pet).
    // Drop the early chasers around the staging point instead: 8.5–10.5y from it, so no residue touches the point or the
    // dive inward (agent-panic README point 3), and one walk away once the third chaser has been cast.
    internal const float StandoffMin = 8.5f, StandoffMax = 10.5f;
    private WPos? _standoff;
    internal WPos? Standoff(WPos stage, WPos center, WPos player, IReadOnlyList<WPos> mines)
    {
        var pending = Module.FindComponent<XBMB26AOE>()?.ActiveAOEs(0, Module.Raid.Player() ?? Module.PrimaryActor).ToArray()
            .Where(a => a.Shape is AOEShapeCircle).Select(a => a.Shape.Distance(a.Origin, a.Rotation)).ToArray() ?? [];
        var live = Live().ToArray();
        bool Open(WPos from, WPos to)
        {
            var length = (to - from).Length();
            if (length < 0.01f)
                return true;
            var dir = (to - from) / length;
            bool Clips(WPos c, float r) => c.InCircle(from + Math.Clamp((c - from).X * dir.X + (c - from).Z * dir.Z, 0, length) * dir, r);
            return !live.Any(r => Clips(r, Radius + 0.5f)) && !mines.Any(m => Clips(m, CrucibleMiasma.ContactRadius + 0.5f));
        }
        bool Usable(WPos p) => (p - stage).Length() is >= StandoffMin - 0.01f and <= StandoffMax + 0.01f && (p - center).Length() >= ArcRadius
            && OnStagingCircle(p) && !Covers(p, 0.5f) && pending.All(d => d.Distance(p) > 1.5f)
            && !mines.Any(m => m.InCircle(p, CrucibleMiasma.ContactRadius + 0.5f)) && Open(p, stage);
        if (_standoff is { } kept && Usable(kept))
            return kept;
        _standoff = null;
        var best = float.MaxValue;
        foreach (var radius in new[] { StandoffMin, (StandoffMin + StandoffMax) / 2, StandoffMax })
            for (var deg = 0; deg < 360; deg += 10)
            {
                var p = stage + radius * ((float)deg).Degrees().ToDirection();
                var travel = (p - player).Length();
                if (travel < best && Usable(p))
                    (_standoff, best) = (p, travel);
            }
        return _standoff;
    }

    internal WPos ArcPoint(StagingArc arc) => arc.Center + ArcRadius * (arc.Angle * 180 / MathF.PI).Degrees().ToDirection();

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _active.Clear();
        foreach (var r in Live())
            _active.Add(new(Shape, r, color: Colors.Danger));
        return CollectionsMarshal.AsSpan(_active);
    }

    // Pitch Sea (48728) → chasers start ~0.9/3/5s after its cast ends (123.1/125.2/127.2 after a 122.2 end).
    // Between the cast bar and the first chaser nothing is pending; still count that gap as the wave, or the
    // player returns to the pocket and the first residue lands on it.
    internal const uint PitchSeaCast = 48728;
    internal const double ChaserWindow = 5.5;
    private DateTime _waveUntil;
    // The cast bar and the chaser window after it (no stale pending puddle keeps this alive).
    internal bool PitchCastWindow => Module.PrimaryActor.CastInfo?.Action.ID == PitchSeaCast || WorldState.CurrentTime < _waveUntil;
    // A chaser whose landing passed without an effect (late load, lost packet) no longer counts.
    internal bool PitchWave => Module.PrimaryActor.CastInfo?.Action.ID == PitchSeaCast || WorldState.CurrentTime < _waveUntil
        || Module.FindComponent<XBMB26AOE>()?.HasPendingAfter(PitchSea, WorldState.FutureTime(-0.5)) == true;

    // Chasers cast since the current Pitch Sea started (4 per wave, 2s apart).
    internal int ChasersThisWave { get; private set; }
    private DateTime _lastChaserAt;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == PitchSea)
        {
            _casts[caster.InstanceID] = spell.LocXZ;
            // One chaser can arrive as several helper casts; count casts at least 1s apart.
            if (WorldState.CurrentTime > _lastChaserAt.AddSeconds(1))
                ++ChasersThisWave;
            _lastChaserAt = WorldState.CurrentTime;
        }
        if (spell.Action.ID == PitchSeaCast)
        {
            _waveUntil = Module.CastFinishAt(spell).AddSeconds(ChaserWindow);
            ChasersThisWave = 0;
            // Wave 2 (no five-hit): choose the staging side now, while the circle is still open.
            PlannedArc = Module.FindComponent<CrucibleGargoyleKnockback>()?.ActiveKnockbacks(0, caster).Length is null or 0
                ? LargestFreeArc(Module.PrimaryActor.Position) : null;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == PitchSeaCast && spell.Interrupted)
            _waveUntil = default;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != PitchSea)
            return;
        var at = _casts.Remove(caster.InstanceID, out var cast) ? cast : spell.TargetXZ;
        if (!_residues.Any(r => r.Position.InCircle(at, 1)))
        {
            _residues.Add((at, WorldState.CurrentTime.AddSeconds(Lifetime)));
            ++_residueVersion;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == CrucibleGargoyleTetherStretch.GargoyleOid)
        {
            _residues.Clear();
            ++_residueVersion;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Covers(actor.Position))
            hints.Add(WarningText);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var r in Live())
            hints.AddForbiddenZone(Shape, r);
        // README wave rule: be ≥13y from the boss at each chaser cast so residues land outside every pocket and the
        // ring-safe disk. Without a five-hit prediction (wave 2 before the steel is known) nothing else keeps the
        // player out, and melee uptime pulls it in (run1888-b3 replay: residue 9y from the boss sealed the ring).
        if (PlannedArc is { } plan && PitchCastWindow && ChasersThisWave < 3
            && Module.FindComponent<CrucibleGargoyleInOut>()?.ActiveAOEs(slot, actor).Length is null or 0
            && HoldPoint(plan, actor.Position) is { } hold)
            hints.GoalPoints.Add((hold, 1f, 60));
        // Wave 1 (five-hit): three chasers, and the pocket return in the last 3.5s before the push must stay open
        // (run1898-b3 replay: escaping a puddle straight at the boss left the third residue 3y from the pocket).
        var pushes = Module.FindComponent<CrucibleGargoyleKnockback>()?.ActiveKnockbacks(slot, actor).ToArray() ?? [];
        // Never during the ring run itself: the pocket is inside the disk.
        if (PitchCastWindow && Module.FindComponent<CrucibleGargoyleInOut>()?.RingRunActive != true && (pushes.Length == 0 ? ChasersThisWave < 4
                : ChasersThisWave < 3 && (pushes[0].Activation - WorldState.CurrentTime).TotalSeconds > CrucibleGargoyleKnockback.PocketReturnSeconds))
            hints.AddForbiddenZone(new AOEShapeCircle(CrucibleGargoyleInOut.InnerDiskRadius), Module.PrimaryActor.Position);
    }

    // Whether a residue-free walk (navigation clearance, 1y grid inside the fence) leads from `from` to within
    // `radius` of `target`. A knockback that lands between residues and the fence walls the player in until the
    // next steel/ring (B26 replay: NoTemporalPath from 142s, ring hit at 150s).
    internal bool Reaches(WPos from, WPos target, float radius)
    {
        PruneResidues();
        const float Step = 1f, Half = 17.5f, Margin = Radius + 1f; // the solver also erodes the fence band and inflates hazards
        if (_residues.Count == 0 || from.InCircle(target, radius))
            return true;
        var n = (int)(2 * Half / Step) + 1;
        WPos At(int x, int z) => Module.Center + new WDir(x * Step - Half, z * Step - Half);
        var sx = (int)MathF.Round((from.X - Module.Center.X + Half) / Step);
        var sz = (int)MathF.Round((from.Z - Module.Center.Z + Half) / Step);
        if (sx < 0 || sz < 0 || sx >= n || sz >= n)
            return false;
        // Perf (same answer as the forward BFS kept below): the start cell is always visited; from it the search
        // only enters passable cells. So it succeeds iff the start cell is in the target circle, or a passable
        // 4-neighbour is connected through passable cells to a passable cell in the circle. That set is flooded
        // once per residue layout and target (B26 pocket and ball checks used to run several BFS every tick).
        if (At(sx, sz).InCircle(target, radius))
            return true;
        var field = ReachField(target, radius, n, Step, Half, Margin);
        return sx + 1 < n && field[sx + 1 + n * sz] || sx > 0 && field[sx - 1 + n * sz]
            || sz + 1 < n && field[sx + n * (sz + 1)] || sz > 0 && field[sx + n * (sz - 1)];
    }

    private readonly Dictionary<(WPos Target, float Radius), bool[]> _reachFields = [];
    private int _reachFieldsVersion = -1;
    private WPos _reachFieldsCenter;
    private bool[] ReachField(WPos target, float radius, int n, float step, float half, float margin)
    {
        if (_reachFieldsVersion != _residueVersion || _reachFieldsCenter != Module.Center || _reachFields.Count >= 64)
        {
            _reachFields.Clear();
            _reachFieldsVersion = _residueVersion;
            _reachFieldsCenter = Module.Center;
        }
        if (_reachFields.TryGetValue((target, radius), out var cached))
            return cached;
        WPos At(int x, int z) => Module.Center + new WDir(x * step - half, z * step - half);
        var passable = new bool[n * n];
        var field = new bool[n * n];
        var queue = new Queue<int>();
        for (var z = 0; z < n; ++z)
            for (var x = 0; x < n; ++x)
            {
                var q = At(x, z);
                var open = true;
                foreach (var r in _residues)
                    if (q.InCircle(r.Position, margin))
                    {
                        open = false;
                        break;
                    }
                passable[x + n * z] = open;
                if (open && q.InCircle(target, radius))
                {
                    field[x + n * z] = true;
                    queue.Enqueue(x + n * z);
                }
            }
        while (queue.TryDequeue(out var i))
        {
            var (x, z) = (i % n, i / n);
            if (x + 1 < n && passable[i + 1] && !field[i + 1]) { field[i + 1] = true; queue.Enqueue(i + 1); }
            if (x > 0 && passable[i - 1] && !field[i - 1]) { field[i - 1] = true; queue.Enqueue(i - 1); }
            if (z + 1 < n && passable[i + n] && !field[i + n]) { field[i + n] = true; queue.Enqueue(i + n); }
            if (z > 0 && passable[i - n] && !field[i - n]) { field[i - n] = true; queue.Enqueue(i - n); }
        }
        _reachFields[(target, radius)] = field;
        return field;
    }

    // Original forward search, kept for reference/regression comparison (unused).
    internal bool ReachesBfs(WPos from, WPos target, float radius)
    {
        var live = Live().ToArray();
        const float Step = 1f, Half = 17.5f, Margin = Radius + 1f;
        if (live.Length == 0 || from.InCircle(target, radius))
            return true;
        var n = (int)(2 * Half / Step) + 1;
        WPos At(int x, int z) => Module.Center + new WDir(x * Step - Half, z * Step - Half);
        var sx = (int)MathF.Round((from.X - Module.Center.X + Half) / Step);
        var sz = (int)MathF.Round((from.Z - Module.Center.Z + Half) / Step);
        if (sx < 0 || sz < 0 || sx >= n || sz >= n)
            return false;
        var seen = new bool[n * n];
        var queue = new Queue<(int X, int Z)>();
        queue.Enqueue((sx, sz));
        seen[sx + n * sz] = true;
        while (queue.TryDequeue(out var c))
        {
            if (At(c.X, c.Z).InCircle(target, radius))
                return true;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var (x, z) = (c.X + dx, c.Z + dz);
                if (x < 0 || z < 0 || x >= n || z >= n || seen[x + n * z])
                    continue;
                seen[x + n * z] = true;
                var q = At(x, z);
                if (!live.Any(r => q.InCircle(r, Margin)))
                    queue.Enqueue((x, z));
            }
        }
        return false;
    }
}

// 50932 supplies the actual dash endpoint ~2.8s before two uncast 48718 hits,
// 2.05s apart and facing opposite ways. ARR hits at -84 degrees and misses at
// +96 degrees bound the half-cleave; use the rear pocket while awaiting a tell.
// 48717 突进连斩 (7.6s) tethers the gargoyle to the player and then dashes along the tether.
// The hit scales with how short the tether is: run1859 7.1y → 3456 and 2.5y → 5298 onto the protecting
// pet (pet max 3492). Stretch the tether during the cast: goal at the farthest interior point from the
// gargoyle, released once the dash event lands so the existing half-cleave logic takes over.
sealed class CrucibleGargoyleTetherStretch(BossModule module) : BossComponent(module)
{
    internal const uint DashCast = 48717u, GargoyleOid = 19650u;
    internal const float WallMargin = 2.5f;
    internal const int Priority = 150;
    // 闪躲之书 (status 4849, stacks in Extra, 120s): the dash is absorbed at any recorded distance, amount 0 at 14–24y
    // (run1888-b4, run1951-b3). Stretching then only costs melee uptime (agent-b26-dps: up to 16s). The hit lands ~1.4s
    // after the cast ends; keep stretching if the book runs out before that.
    internal const uint DodgeBook = 4849u;
    internal const double BookCoverSeconds = 2;
    // run1988: the 48717 event lands 1.07s before the hit (helper 50933). Releasing on it let the player walk back
    // from ~21.5y to ~19y before the hit. After the cast keep the last goal, not one recomputed from the moving boss.
    internal const uint DashHit = 50933u;
    internal const double HitFallbackSeconds = 1.2;
    private const double DashHitHoldSeconds = 2;
    internal const float GoalRadius = 0.25f;
    private const float StretchWalkSpeed = 5f;
    private const double StretchArrivalSeconds = 1.2;
    private Actor? _caster;
    private DateTime _until;
    private DateTime _holdUntil;
    private bool _castEnded;
    private WPos? _lastGoal;
    private bool BookAbsorbs => Module.Raid.Player()?.FindStatus(DodgeBook) is { Extra: >= 1 } book && book.ExpireAt > _until.AddSeconds(BookCoverSeconds);
    internal bool Active => _caster != null && WorldState.CurrentTime <= _holdUntil && !BookAbsorbs;
    internal Actor? Gargoyle => Active ? _caster : null;
    internal double SecondsLeft => (_until - WorldState.CurrentTime).TotalSeconds;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == DashCast && caster.OID == GargoyleOid)
        {
            _caster = caster;
            var finish = Module.CastFinishAt(spell);
            _until = finish.AddSeconds(HitFallbackSeconds);
            // Recorded 50933 lands 1.34-1.38s after the bar; do not return before it.
            // This timeout is separate from the unchanged pre-cast-finish walking budget.
            _holdUntil = finish.AddSeconds(DashHitHoldSeconds);
            _castEnded = false;
            _lastGoal = null;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != DashCast || caster != _caster) return;
        if (spell.Interrupted) _caster = null;
        else _castEnded = true;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == DashCast && caster == _caster) _castEnded = true;
        else if (spell.Action.ID == DashHit) _caster = null;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_caster is not { } gargoyle || WorldState.CurrentTime > _holdUntil || gargoyle.IsDeadOrDestroyed)
        {
            _caster = null;
            return;
        }
        if (BookAbsorbs)
            return;
        if (_castEnded)
        {
            hints.GoalPoints.Add((_lastGoal ?? actor.Position, GoalRadius, Priority));
            return;
        }
        // A ball past the colour change both stretches the tether and strips a Damage Up stack.
        if (Module.FindComponent<CrucibleMiasma>()?.CollectTarget(actor) is { } ball)
        {
            if (WaitToStretch(ball.Position, actor))
                return;
            _lastGoal = ball.Position;
            hints.GoalPoints.Add((ball.Position, 1f, Priority));
        }
        else if ((StretchPoint(gargoyle.Position, actor.Position) ?? FarthestInterior(gargoyle.Position)) is { } point)
        {
            // Only the tether length at the hit counts. run2031: leaving at cast start stood 5s at the far
            // point without a GCD; keep hitting until the walk needs what is left of the 7.6s cast.
            if (WaitToStretch(point, actor))
                return;
            _lastGoal = point;
            hints.GoalPoints.Add((point, GoalRadius, Priority));
        }
    }

    // 魔兽加速药 (盗贼的药, status 2322) is +20% run speed (7.2y/s); budget 6y/s with it, still under the real speed.
    // run2212: no book, four stretched dashes per try left melee ~5s each.
    internal const uint ThiefPotion = 2322u;
    private const float StretchWalkSpeedHasted = 6f;
    internal bool WaitToStretch(WPos goal, Actor actor)
        => _lastGoal == null && SecondsLeft - HitFallbackSeconds > (goal - actor.Position).Length() / (actor.FindStatus(ThiefPotion) != null ? StretchWalkSpeedHasted : StretchWalkSpeed) + StretchArrivalSeconds;

    // run1861/1863: tether 0039 turns to 0001 at 20.7–22y. Past the colour change the
    // stretch buys nothing; staying near the landing keeps the 48718 half-cleaves dodgeable.
    internal const float SafeTether = 22f;
    internal const float LandingInnerHalf = 10f; // arena half-size ~19.7: keeps the landing ~10y from both fences
    internal WPos? StretchPoint(WPos from, WPos player)
    {
        // The gargoyle lands where the tethered player stands. A landing in a corner cramps every later
        // steel/ring, Pitch Sea and residue against the fence (r22 replays: 5/8 remaining hits had the boss
        // ≤3y from a fence in wave 2). Prefer points that keep the landing ≥ LandingFenceClearance from both
        // fences, as long as the tether still reaches its colour change there.
        WPos? best = null, inner = null;
        float bestTravel = float.MaxValue, innerTravel = float.MaxValue;
        for (var z = -20f; z <= 20f; z += 0.5f)
            for (var x = -20f; x <= 20f; x += 0.5f)
            {
                var p = Module.Center + new WDir(x, z);
                if ((p - from).Length() < SafeTether || !Arena.InBounds(p)
                    || !Arena.InBounds(p + new WDir(WallMargin, 0)) || !Arena.InBounds(p - new WDir(WallMargin, 0))
                    || !Arena.InBounds(p + new WDir(0, WallMargin)) || !Arena.InBounds(p - new WDir(0, WallMargin)))
                    continue;
                var travel = (p - player).Length();
                if (travel < bestTravel) { bestTravel = travel; best = p; }
                if (Math.Max(Math.Abs(x), Math.Abs(z)) <= LandingInnerHalf && travel < innerTravel) { innerTravel = travel; inner = p; }
            }
        return inner ?? best;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_caster != null && !BookAbsorbs) hints.Add("突进连斩：把连线拉到变色（约 22 米）！");
    }

    internal WPos? FarthestInterior(WPos from)
    {
        WPos? best = null;
        var bestDistance = 0f;
        for (var deg = 0; deg < 360; deg += 10)
        {
            var dir = ((float)deg).Degrees().ToDirection();
            var edge = 0f;
            for (var r = 0.5f; r <= 40; r += 0.5f)
            {
                if (!Arena.InBounds(Module.Center + dir * r)) break;
                edge = r;
            }
            var radius = edge - WallMargin;
            if (radius <= 0) continue;
            var point = Module.Center + dir * radius;
            var distance = (point - from).Length();
            if (distance > bestDistance) { bestDistance = distance; best = point; }
        }
        return best;
    }
}

sealed class CrucibleGargoyleCharge(BossModule module) : Components.GenericAOEs(module)
{
    internal static readonly AOEShapeCone Cleave = new(60, 90f.Degrees());
    private readonly List<AOEInstance> _sequence = [];
    private readonly HashSet<uint> _events = [];
    private ulong _boss;
    private WPos? _dashStart;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == CrucibleGargoyleTetherStretch.DashCast && caster.OID == 19650)
            _dashStart = caster.Position;
    }

    // run1872–1884: a long dash's first half-cleave faces its travel (start→landing), not the
    // pre-dash facing. run1884 second dash: facing -1.32, travel/cleave 3.07; the 3y refuge sat
    // on the cleave's edge and took 3390. Short or missing travel keeps the packet rotation (1872).
    internal const float TravelFacingDistance = 10f;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 50932 && caster.OID == 19650 && spell.TargetXZ.InRect(Module.Center, 25, 25))
        {
            if (!_events.Add(spell.GlobalSequence))
                return;
            _boss = caster.InstanceID;
            var travel = spell.TargetXZ - (_dashStart ?? caster.Position);
            var facing = travel.Length() >= TravelFacingDistance ? Angle.FromDirection(travel) : spell.Rotation;
            _dashStart = null;
            _sequence.Clear();
            _sequence.Add(new(Cleave, spell.TargetXZ, facing, WorldState.FutureTime(2.8)));
            _sequence.Add(new(Cleave, spell.TargetXZ, facing + 180f.Degrees(), WorldState.FutureTime(4.85)));
        }
        else if (spell.Action.ID == 48718 && _events.Add(spell.GlobalSequence))
        {
            if (_sequence.Count == 2)
            {
                _sequence.RemoveAt(0);
                _sequence[0] = new(Cleave, caster.Position, spell.Rotation + 180f.Degrees(), WorldState.FutureTime(2.05));
            }
            else
                _sequence.Clear();
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _sequence.RemoveAll(a => a.Activation.AddSeconds(0.5) < WorldState.CurrentTime);
        return _sequence.Select((a, i) => a with { Risky = i == 0, Color = i == 0 ? Colors.Danger : Colors.AOE }).ToArray();
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        if (aoes.Length == 0)
            return;
        // The tether destination is held until the actual charge hit. Adding
        // its opposite-side cleave destination during that hold makes the
        // 0.25y tether goal conflict with a future half-field danger and can
        // exhaust the refined temporal search before the first cleave. The
        // hit leaves about two seconds to cover this short three-yard move.
        if (Module.FindComponent<CrucibleGargoyleTetherStretch>() is { Active: true })
            return;
        var next = aoes[0];
        // The two half-cleaves cover the arena together. Keep the later one as
        // a preview so settling behind the first cannot start a full-field time search.
        CrucibleScorpionAI.Avoid(hints, next);
        // Stay near the landing to cross to the first cleave's safe side after
        // it resolves, even when the dash lands beside the electric boundary.
        var destination = next.Origin - 3 * next.Rotation.ToDirection();
        hints.GoalPoints.Add((destination, 1f, 100));
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _boss)
            _sequence.Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

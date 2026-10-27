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
    private static readonly AOEShapeCircle Steel = new(13);
    private static readonly AOEShapeDonut Ring = new(6, 30);
    private AOEInstance? _steel;
    private AOEInstance? _ring;
    private DateTime _steelResolved;
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
            (_stagingPoint, _ringPoint) = (null, null);
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
        if (_steel is { } steel)
        {
            // Two seconds must cover the return and event/worker latency. Keep
            // PR's margin without inflating the real shapes again in the facade.
            hints.ForbiddenZones.Add(new(Steel.Distance(steel.Origin, default), steel.Activation, steel.Activation.AddSeconds(0.6), ExactGeometry: true));
            // Match the visible countdown, excluding the NPC finish delay and 48719's 0.6s hit delay.
            var castFinish = Module.PrimaryActor.CastInfo is { Action.ID: 48719 } cast
                ? WorldState.FutureTime(cast.RemainingTime)
                : steel.Activation.AddSeconds(-ActorCastInfo.NPCFinishDelay);
            if (castFinish > WorldState.FutureTime(3))
                return;
            if (_ring is { } next)
            {
                // The goal fixes the 13.5y staging distance. Leave room around that
                // route for PR's grid and hard margin instead of sealing the annulus.
                hints.AddForbiddenConstraint(new SDInvertedCircle(steel.Origin, 15.5f), steel.Activation.AddSeconds(-0.5), steel.Activation.AddSeconds(0.6));
                var mines = Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() ?? [];
                var approach = new RingApproach(next.Origin, mines);
                if (mines.Length > 0)
                    hints.AddForbiddenConstraint(approach, steel.Activation.AddSeconds(-0.5), steel.Activation.AddSeconds(0.6));
                // A precise staging goal preserves the safe strip between the steel and
                // the outer positioning constraint after PR dilates both by its margin.
                var stagingApproach = new RingApproach(next.Origin, mines, 1.25f);
                if (_stagingPoint is not { } stage || MathF.Abs((stage - steel.Origin).Length() - 13.5f) > 0.01f
                    || !Arena.InBounds(stage) || stagingApproach.Distance(stage) <= 0)
                    _stagingPoint = Enumerable.Range(0, 72).Select(i => steel.Origin + 13.5f * (i * 5f).Degrees().ToDirection())
                        .Where(p => Arena.InBounds(p) && stagingApproach.Distance(p) > 0)
                        .OrderBy(p => (p - actor.Position).LengthSq()).Select(p => (WPos?)p).FirstOrDefault();
                if (_stagingPoint is { } point)
                    hints.GoalPoints.Add((point, 0.2f, 100));
            }
        }
        else if (_ring is { } ring)
        {
            // PR uses 0.5s normally and 0.25s for emergency routes. Even the
            // emergency deadline must precede the actual hit, never exceed it.
            hints.ForbiddenZones.Add(new(Ring.Distance(ring.Origin, default), ring.Activation.AddSeconds(0.2), ring.Activation.AddSeconds(0.6), ExactGeometry: true));
            if (_ringPoint is not { } point || point.Origin != ring.Origin)
            {
                var offset = actor.Position - ring.Origin;
                var destination = offset.LengthSq() > 25 ? ring.Origin + 5 * offset.Normalized() : actor.Position;
                _ringPoint = (ring.Origin, destination);
            }
            hints.GoalPoints.Add((_ringPoint.Value.Position, 0.2f, 100));
        }
    }

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

// 50932 supplies the actual dash endpoint ~2.8s before two uncast 48718 hits,
// 2.05s apart and facing opposite ways. ARR hits at -84 degrees and misses at
// +96 degrees bound the half-cleave; use the rear pocket while awaiting a tell.
sealed class CrucibleGargoyleCharge(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Cleave = new(60, 90f.Degrees());
    private readonly List<AOEInstance> _sequence = [];
    private readonly HashSet<uint> _events = [];
    private ulong _boss;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 50932 && caster.OID == 19650 && spell.TargetXZ.InRect(Module.Center, 25, 25))
        {
            if (!_events.Add(spell.GlobalSequence))
                return;
            _boss = caster.InstanceID;
            _sequence.Clear();
            _sequence.Add(new(Cleave, spell.TargetXZ, spell.Rotation, WorldState.FutureTime(2.8)));
            _sequence.Add(new(Cleave, spell.TargetXZ, spell.Rotation + 180f.Degrees(), WorldState.FutureTime(4.85)));
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

// Every recorded six-pulse breath originates 20y BEHIND the visual's facing,
// ~5.4s after cast start. Guide toward its inner pocket early and hold through
// all six pulses; the inner radius is unknown, so only draw a conservative
// destination, not a damage donut. The field bounds still constrain navigation.
sealed class CrucibleDragonWaves(BossModule module) : Components.GenericAOEs(module, 48808u)
{
    private WPos _origin;
    private DateTime _first;
    private DateTime _activation;
    private DateTime _expires;
    private int _pulses;
    private readonly HashSet<uint> _events = [];
    private static readonly AOEShapeCircle Spot = new(7);
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != 48807 || spell.EventHappened)
            return;
        var first = Module.CastFinishAt(spell, 2.4);
        if (Math.Abs((first - _first).TotalSeconds) < 0.75)
            return;
        _first = _activation = first;
        _expires = first.AddSeconds(6.5);
        _origin = Module.Center - 20 * spell.Rotation.ToDirection();
        _pulses = 0;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction && _events.Add(spell.GlobalSequence))
        {
            _origin = caster.Position;
            if (_expires < WorldState.CurrentTime)
            {
                _expires = WorldState.FutureTime(6);
                _pulses = 0;
            }
            _activation = WorldState.FutureTime(1.07);
            if (++_pulses >= 6)
                _expires = default;
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _expires > WorldState.CurrentTime
        ? new[] { new AOEInstance(Spot, _origin, activation: _activation, color: Colors.SafeFromAOE, risky: false) } : [];
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_expires > WorldState.CurrentTime)
        {
            // 23:15 ARR: with only the deadline the player left at it, 20y away, and took five pulses.
            // Drifting clouds can wall off the pocket: the general planner only targets places that
            // stay safe, so walk a route timed through the clouds' lines as a direct traversal.
            hints.DirectRoute = RouteNow(actor);
            hints.AddForbiddenConstraint(new SDInvertedCircle(_origin, Spot.Radius), _activation.AddSeconds(-0.5));
            hints.GoalPoints.Add((_origin, Spot.Radius - 2, 100));
        }
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48807 && spell.Interrupted)
            _expires = default;
    }

    // Time-expanded A* on a 0.5y grid in 0.1s steps; clouds are extrapolated along their lines.
    private const float RouteSpeed = 5.5f, RouteStep = 0.1f, RouteCell = 0.5f;
    private const float CloudClearance = CrucibleDragonClouds.ContactRadius + 0.5f;
    private const int RouteSteps = 70, RouteExpansions = 12000;
    private List<WPos>? _route;
    private DateTime _routeAt, _searchAt;
    private Task<List<WPos>?>? _search;
    internal Task? PendingRoute => _search;

    private List<WPos>? RouteNow(Actor actor)
    {
        var now = WorldState.CurrentTime;
        if (_search is { IsCompleted: true } done)
        {
            _route = done.IsCompletedSuccessfully ? done.Result : null;
            _routeAt = _searchAt;
            _search = null;
        }
        if (_search == null && now >= _searchAt.AddSeconds(0.25))
        {
            var clouds = Module.FindComponent<CrucibleDragonClouds>()?.Motions().ToArray() ?? [];
            var start = actor.Position;
            // Inside the pocket only a cloud about to pass through it needs a route.
            if (clouds.Length == 0 || start.InCircle(_origin, Spot.Radius - 2) && CanHold(start, 0, clouds))
            {
                _route = null;
                _searchAt = now;
            }
            else
            {
                // The search can take several milliseconds; keep it off the game thread.
                var bounds = CrucibleArena.Find(Module.Center)?.SafeBounds ?? Module.Arena.Bounds;
                var (center, origin) = (Module.Center, _origin);
                _searchAt = now;
                _search = Task.Run(() => Route(start, clouds, bounds, center, origin));
            }
        }
        if (_route == null)
            return null;
        // Walk the part of the plan from now until its first wait; the next replan resumes it.
        var first = Math.Min(_route.Count - 1, (int)((now - _routeAt).TotalSeconds / RouteStep) + 1);
        var run = new List<WPos>();
        for (var i = first; i < _route.Count; ++i)
        {
            if (_route[i].AlmostEqual(_route[i - 1], 0.01f))
                break;
            run.Add(_route[i]);
        }
        // Keep only the corners of the run.
        for (var i = run.Count - 2; i > 0; --i)
            if (MathF.Abs((run[i] - run[i - 1]).Cross(run[i + 1] - run[i])) < 0.01f)
                run.RemoveAt(i);
        return run.Count > 0 ? run : null;
    }

    private const int HoldSteps = 15;
    private static bool Clear(WPos p, int step, (WPos Position, WDir Velocity)[] clouds)
    {
        var t = step * RouteStep;
        foreach (var (position, velocity) in clouds)
            if ((p - (position + velocity * t)).LengthSq() < CloudClearance * CloudClearance) return false;
        return true;
    }
    // The route ends where the player can then stand for 1.5s.
    private static bool CanHold(WPos p, int step, (WPos Position, WDir Velocity)[] clouds)
    {
        for (var i = 0; i <= HoldSteps; i += 3)
            if (!Clear(p, step + i, clouds)) return false;
        return true;
    }

    private static List<WPos>? Route(WPos start, (WPos Position, WDir Velocity)[] clouds, ArenaBounds bounds, WPos center, WPos origin)
    {
        bool Safe(WPos p, int step) => bounds.Contains(p - center) && Clear(p, step, clouds);
        var goal = Spot.Radius - 2;
        var move = RouteSpeed * RouteStep;
        var open = new PriorityQueue<(int Node, int Step), float>();
        var nodes = new List<(WPos P, int Parent)> { (start, -1) };
        var seen = new HashSet<(int, int, int)>();
        open.Enqueue((0, 0), 0);
        var expansions = 0;
        while (open.TryDequeue(out var current, out _) && ++expansions < RouteExpansions)
        {
            var at = nodes[current.Node].P;
            if (at.InCircle(origin, goal) && CanHold(at, current.Step, clouds))
            {
                var path = new List<WPos>();
                for (var i = current.Node; i >= 0; i = nodes[i].Parent) path.Add(nodes[i].P);
                path.Reverse();
                return path;
            }
            if (current.Step >= RouteSteps) continue;
            for (var dx = -1; dx <= 1; ++dx)
                for (var dz = -1; dz <= 1; ++dz)
                {
                    var dir = new WDir(dx, dz);
                    var next = dir.LengthSq() > 0 ? at + dir.Normalized() * move : at;
                    var step = current.Step + 1;
                    if (!seen.Add(((int)MathF.Round(next.X / RouteCell), (int)MathF.Round(next.Z / RouteCell), step)) || !Safe(next, step))
                        continue;
                    nodes.Add((next, current.Node));
                    var remaining = MathF.Max(0, (next - origin).Length() - goal) / RouteSpeed;
                    open.Enqueue((nodes.Count - 1, step), step * RouteStep + remaining);
                }
        }
        return null;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19672)
            _expires = default;
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

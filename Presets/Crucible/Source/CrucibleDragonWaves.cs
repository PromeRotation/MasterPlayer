// See docs/reference/CrucibleAll.spec.md.
namespace MasterPlayer.Mechanics.Crucible;

// Every recorded six-pulse breath originates 20y BEHIND the visual's facing (39/39 ARR breaths,
// 2026-11-03 survey: source = arena centre - 20y x cast facing, on the arena edge). Pulses land
// at cast finish + 2.667/3.734/4.830/5.898/6.966/8.033s; the safe inner radius lies in
// (6.957, 8.732]. Guide into the pocket for the whole pulse train while dodging the clouds.
sealed class CrucibleDragonWaves(BossModule module) : Components.GenericAOEs(module, 48808u)
{
    // Pulse offsets after the first pulse (medians of 37 complete ARR breaths, spread <= 0.3s).
    private static readonly double[] PulseOffsets = [0, 1.067, 2.163, 3.231, 4.299, 5.366];
    private WPos _origin;
    internal bool RoutingThroughClouds { get; private set; }
    private bool _aggressiveRouting;
    private DateTime _first;
    private DateTime _activation;
    private DateTime _last;
    private DateTime _expires;
    private DateTime _stageUntil;
    private DateTime _stageFrom;
    private const double StageLeadSeconds = 2.5;
    private int _pulses;
    private bool _ended;
    // 48816 landing: be within its inner radius at the outer hit, after the 48814 clouds swept the arena.
    private bool _landing;
    private bool _outerLanded;
    private DateTime _centerActivation;
    private readonly HashSet<uint> _events = [];
    // Registered pocket: inside the proven-safe 6.957y with a margin for movement tolerance.
    private const float PocketRadius = 6.5f;
    private static readonly AOEShapeCircle Spot = new(PocketRadius);
    internal bool Active => _expires > WorldState.CurrentTime;
    internal bool Staging => !Active && _stageUntil > WorldState.CurrentTime;
    internal void Configure(bool aggressiveRouting)
    {
        if (_aggressiveRouting == aggressiveRouting) return;
        _aggressiveRouting = aggressiveRouting;
        ClearRoute();
    }
    private void ClearRoute()
    {
        _search = null;
        _route = null;
        _issuedRoute = null;
        _searchAt = _routeAt = default;
        RoutingThroughClouds = false;
    }
    private void End()
    {
        _expires = default;
        _ended = true;
        ClearRoute();
    }
    public override void Update()
    {
        if (_expires != default && _expires <= WorldState.CurrentTime)
            End();
    }
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48828 && !spell.EventHappened)
            _centerActivation = Module.CastFinishAt(spell);
        // run1856: the second breath starts ~3.4s after the last 48812 cloud burst. Its pocket is a
        // cardinal arena edge 20y from the centre (2026-11-03 survey: all four occur) and is only known
        // at cast start, 5.4s before the first pulse; from the far edge it is ~34y away. A replay that
        // stood 28y out at cast start found no cloud-free run, so wait near the centre meanwhile.
        if (spell.Action.ID == 48812 && !spell.EventHappened)
        {
            _stageUntil = Module.CastFinishAt(spell, 12);
            // run2206 03:13:39-48: staging from the first 48812 cast start left the boss (north edge) for the
            // centre ~10s before the breath was even cast. The three 48812 casts start 2s apart; each pushes
            // the staging start to 2.5s before its own burst, so the player stays on the boss (dodging the
            // circles normally) and only walks the ~10y to the centre for the last burst, ~6s before breath.
            _stageFrom = Module.CastFinishAt(spell, -StageLeadSeconds);
        }
        // run1856 replay: the 48814 lines leave two cloud rows sweeping outward; during the landing
        // cast the shared planner kept the player 18y out behind them and the outer ring hit.
        if (spell.Action.ID == 48816 && !spell.EventHappened && !(Active && !_landing))
        {
            ClearRoute();
            _ended = false;
            _landing = true;
            _outerLanded = false;
            _origin = spell.LocXZ;
            _first = default;
            _activation = _last = Module.CastFinishAt(spell);
            if (_centerActivation <= _last)
                _centerActivation = _last.AddSeconds(1.5);
            _expires = _centerActivation.AddSeconds(0.6);
            return;
        }
        if (spell.Action.ID != 48807 || spell.EventHappened)
            return;
        _stageUntil = default;
        _landing = false;
        _outerLanded = false;
        // CastFinishAt includes the 0.3s NPC finish delay; first pulse = cast finish + 2.667s.
        var first = Module.CastFinishAt(spell, 2.37);
        if (Math.Abs((first - _first).TotalSeconds) < 0.75)
            return;
        ClearRoute();
        _events.Clear();
        _ended = false;
        _first = _activation = first;
        _last = first.AddSeconds(PulseOffsets[^1]);
        _expires = _last.AddSeconds(1.1);
        _origin = Module.Center - 20 * spell.Rotation.ToDirection();
        _pulses = 0;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (_landing && spell.Action.ID == 48816)
        {
            _outerLanded = true;
            _activation = _last = WorldState.CurrentTime;
            ClearRoute();
            return;
        }
        if (_landing && spell.Action.ID == 48828)
        {
            _landing = false;
            End();
            return;
        }
        if (!_ended && !_landing && spell.Action.ID == WatchedAction && _events.Add(spell.GlobalSequence))
        {
            _origin = caster.Position;
            if (_expires < WorldState.CurrentTime)
                _pulses = 0;
            var index = Math.Min(_pulses, PulseOffsets.Length - 1);
            // Re-anchor the remaining pulses on the observed one (repeat spread <= 0.07s).
            _last = WorldState.FutureTime(PulseOffsets[^1] - PulseOffsets[index]);
            _expires = _last.AddSeconds(1.1);
            _activation = index + 1 < PulseOffsets.Length ? WorldState.FutureTime(PulseOffsets[index + 1] - PulseOffsets[index]) : _last;
            if (++_pulses >= PulseOffsets.Length)
                End();
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => Active && !_landing
        ? new[] { new AOEInstance(Spot, _origin, activation: _activation, color: Colors.SafeFromAOE, risky: false) } : [];
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Active)
        {
            // run1856 replay: with ~20 clouds crossing the arena the shared planner found no timed path
            // to the pocket (NoTemporalPath/NoReachableSafeCell) and released movement every frame, so
            // the player stood 23y out through all six pulses in both strategies. Both strategies walk
            // this searched route; the runtime still validates every run against all registered hazards
            // and falls back to the shared planner (pocket goal below) when it refuses one.
            hints.DirectRoute = _issuedRoute = RouteNow(actor);
            RoutingThroughClouds = hints.DirectRoute != null;
            hints.GoalDirectedTemporalPlanning = true;
            // CrucibleDragonLanding owns the landing's constraint and goal.
            if (_landing)
                return;
            // From just before the next pulse until just after the last one. While a route is walked the
            // route itself keeps the pocket at each pulse; a continuous constraint (buffered by the runtime)
            // would also forbid the brief steps out between pulses that let a cloud pass (213938 ARR).
            if (hints.DirectRoute == null)
                hints.AddForbiddenConstraint(new SDInvertedCircle(_origin, PocketRadius), _activation.AddSeconds(-.5), _last.AddSeconds(.3));
            hints.GoalPoints.Add((_origin, PocketRadius - 1.5f, 100));
            // run2210 04:43:39: 粘液怪 stayed by the boss and took two pulses (954+974). The whole arena outside
            // the pocket is hit, so the pet must stand in the same pocket as the player.
            hints.PetMoveTo = _origin;
        }
        else
        {
            ClearRoute();
            // Wait near the centre, at most 20y from any pocket, while the clouds disperse.
            if (_stageUntil > WorldState.CurrentTime && _stageFrom <= WorldState.CurrentTime)
            {
                hints.GoalPoints.Add((Module.Center, StageRadius, 50));
            }
        }
    }
    private const float StageRadius = 6f;

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Interrupted && (_landing ? spell.Action.ID is 48816 or 48828 : spell.Action.ID == 48807))
            End();
    }

    // Time-expanded reachability on a 0.5y grid in 0.1s steps until a short hold inside the pocket
    // window (replanned every 0.2s); clouds are extrapolated along their straight lines.
    // Planned at 5.5y/s against the 6y/s run speed: each run starts ~0.1s late, and a plan at full
    // speed fell further behind its schedule with every short run (231558 feedback replay).
    // A pocket behind a cloud wall can be reachable only at full run speed (213938: 5.5y/s arrives
    // too late from every position after the cast starts, 6y/s does not), so a tight plan is tried
    // before giving up on the pocket: a partial approach walks into a dead end and misses the pulse.
    private const float RouteSpeed = 5.5f, TightRouteSpeed = 6f, RouteStep = 0.1f, RouteCell = 0.5f;
    // Judge: contact radius < 3.164y at the forecast instant, plus a margin for forecast and timing error.
    private const float CloudPointClearance = CrucibleDragonClouds.JudgedRadius + 0.35f;
    // The runtime validates a direct run against the windows CrucibleDragonClouds registers while this
    // route is active (0.2s samples, each +-0.1s, buffered 0.2s before and 0.1s after) and allows 0.1s
    // to start moving: a cloud blocks its track from ~0.35s behind to ~0.55s ahead of its forecast.
    private const float CloudTrackClearance = CrucibleDragonClouds.RouteWindowRadius + 0.3f;
    private const float TrackBefore = 0.35f, TrackAfter = 0.55f;
    // Judge inner radius 6.95 (proven safe < 6.957) minus forecast/arrival tolerance.
    private const float PocketPlanRadius = 6.4f;
    private const double PocketBefore = 0.3, PocketAfter = 0.3;
    private const float PulseSlack = 0.25f;
    private const float LandingPlanRadius = 5 - 0.6f;
    private const double LandingBefore = 0.5 + 0.2 + 0.1, LandingAfter = 0.5;
    private const float LandingExitRadius = 6.6f;
    private const float LandingInnerRim = 3.8f;
    // Pools are hard obstacles: R6 plus the runtime's 0.5y obstacle margin.
    private const float PoolClearance = 6.6f;
    // A partial approach needs enough horizon to walk around a cloud wall, not just the
    // 2s nearest-cell sprint: the 231558/213938 pockets are behind clouds whose only
    // corridor opens west, and a 20-step search picked a dead end to the north-east.
    private const int MaxSteps = 90, FallbackSteps = 20;
    private const float HoldSeconds = 1.5f;
    private List<WPos>? _route;
    private List<WPos>? _issuedRoute;
    private DateTime _routeAt, _searchAt;
    private Task<List<WPos>?>? _search;
    internal Task? PendingRoute => _search;
    internal object RouteEvidence => new { active=Active,landing=_landing,outerLanded=_outerLanded,
        activation=_activation,centerActivation=_centerActivation,routeAt=_routeAt,
        partial=_route is PartialRoute,routeCount=_route?.Count??0,
        routeEnd=_route is { Count: > 0 } route ? new[]{route[^1].X,route[^1].Z}:null,
        issuedRoute=_issuedRoute?.Select(p=>new[]{p.X,p.Z}).ToArray(),
        searchStatus=_search?.Status.ToString() };

    private List<WPos>? RouteNow(Actor actor)
    {
        var now = WorldState.CurrentTime;
        if (_search is { IsCompleted: true } done)
        {
            var result = done.IsCompletedSuccessfully && now <= _searchAt.AddSeconds(0.6) ? done.Result : null;
            // Replacing a run already being walked makes its stale search-start
            // waypoint a backtrack, spending the thin arrival budget repeatedly.
            // Keep the current feasible stage while near its scheduled progress;
            // runtime still validates it against the latest hazards every frame.
            var keep = (result is null or PartialRoute || _landing) && _route is { } current and not PartialRoute
                && now <= _routeAt.AddSeconds(_landing ? KeepFullRoute : RouteLifetime(current)) && NearestOnRoute(current, actor.Position, now) is { } nearest
                && actor.Position.InCircle(current[nearest], 1f);
            if (!keep)
            {
                _route = result;
                _routeAt = _searchAt;
            }
            _search = null;
        }
        if (_search == null && now >= _searchAt.AddSeconds(0.2))
        {
            var clouds = Module.FindComponent<CrucibleDragonClouds>()?.Motions().ToArray() ?? [];
            // 21:23 ARR: the four bait pools stay down through the second breath and leave only a
            // 2y corridor to the pocket. A cloud-only route crossed one; the runtime refused it every
            // frame and the player stood still through all six pulses.
            var pools = Module.FindComponent<CrucibleDragonPoison>()?.ActiveAOEs(0, actor).ToArray().Select(a => a.Origin).ToArray() ?? [];
            _searchAt = now;
            if (clouds.Length == 0)
                _route = null;
            else
            {
                // The search can take several milliseconds; keep it off the game thread.
                var start = actor.Position;
                var bounds = CrucibleArena.Find(Module.Center)?.SafeBounds ?? Module.Arena.Bounds;
                var (center, origin) = (Module.Center, _origin);
                // Landing: CrucibleDragonLanding's R5 constraint from 0.5s before the outer hit until it lands.
                var pocketFrom = _landing && _outerLanded ? float.PositiveInfinity
                    : (float)((_activation - now).TotalSeconds - (_landing ? LandingBefore : PocketBefore));
                var pocketUntil = (float)((_last - now).TotalSeconds + (_landing ? LandingAfter : PocketAfter));
                var planRadius = _landing ? LandingPlanRadius : PocketPlanRadius;
                var landing = _landing;
                var exitAt = landing ? (float)(_centerActivation - now).TotalSeconds - 0.3f : float.PositiveInfinity;
                var finishRadius = landing ? LandingExitRadius + 0.3f : 0;
                var approachRadius = landing ? (_outerLanded ? finishRadius : LandingPlanRadius) : 0;
                // Breath: only the pulse instants hurt. Inside the pocket around each predicted pulse
                // (first-pulse spread <= 0.3s, repeats <= 0.07s, plus 0.1s start latency); between pulses
                // the route may step out to let a cloud pass.
                var pulses = landing ? [] : PulseOffsets.Select(o => (float)((_last - now).TotalSeconds - (PulseOffsets[^1] - o)))
                    .Where(p => p >= (float)((_activation - now).TotalSeconds) - 0.01f).ToArray();
                // When no cloud-free run reaches the pocket in time (a late start behind a cloud wall),
                // still approach it without touching a cloud instead of standing still.
                var innerRim = landing ? LandingInnerRim : 0;
                _search = Task.Run(() => (landing ? null : Plans.Select(plan => Route(start, clouds, pools, bounds, center, origin, planRadius, innerRim, pocketFrom, pocketUntil, pulses, exitAt, finishRadius, true, plan)).FirstOrDefault(r => r != null))
                    ?? Route(start, clouds, pools, bounds, center, origin, planRadius, innerRim, pocketFrom, pocketUntil, pulses, exitAt, finishRadius, true)
                    ?? (Route(start, clouds, pools, bounds, center, origin, planRadius, innerRim, pocketFrom, pocketUntil, pulses, float.PositiveInfinity, approachRadius, false) is { } partial ? new PartialRoute(partial) : null));
            }
        }
        if (_route == null)
            return null;
        if (now > _routeAt.AddSeconds(_route is PartialRoute ? 0.6 : _landing ? KeepFullRoute : RouteLifetime(_route)))
        {
            _route = null;
            return null;
        }
        // Resume from the player's actual progress, never from a waypoint ahead of the schedule:
        // a player who has not started yet must not be sent toward a later waypoint.
        var at = NearestOnRoute(_route, actor.Position, now) ?? 0;
        // Movement was refused or another mechanic displaced the player.
        if (!actor.Position.InCircle(_route[at], 1f))
            return null;
        // Each run starts ~0.1s late and short runs between planned waits made the player fall further
        // behind its schedule with every run (231558 feedback replay). A player behind schedule spends
        // the planned waits ahead instead: the same path, never earlier than planned, in longer runs.
        var lag = Math.Max(0, (int)((now - _routeAt).TotalSeconds / RouteStep) - at);
        // A planned wait: hold this waypoint until the schedule moves on.
        if (at + 1 >= _route.Count || lag == 0 && _route[at + 1].AlmostEqual(_route[at], 0.01f))
            return [_route[at]];
        // Walk the run until its next wait that the schedule still requires; the next replan resumes it.
        var run = new List<WPos>();
        for (var i = at + 1; i < _route.Count; ++i)
        {
            // The searched path has an absolute clock, but DirectRoute is an
            // untimed run validated/executed immediately at native run speed.
            // A continuous walk around the rim can spend the time until the
            // outer hit without containing a duplicate-point "wait". Do not
            // publish its future escape as part of the current inward run:
            // runtime would correctly refuse that premature R5 departure.
            // The actual outer packet releases this stage and clears/reanchors
            // the search, so its outward suffix is planned from real progress.
            if (_landing && !_outerLanded && _route[i - 1].InCircle(_origin, LandingPlanRadius)
                && !_route[i].InCircle(_origin, LandingPlanRadius))
                break;
            if (_route[i].AlmostEqual(_route[i - 1], 0.01f))
            {
                if (lag-- > 0)
                    continue;
                break;
            }
            run.Add(_route[i]);
            // The runtime validates a published run as one untimed walk at native speed, so a far
            // waypoint timed for a later cloud gap refuses the whole run while the next steps are safe.
            if (!_landing && run.Count >= MaxPublishedSteps)
                break;
        }
        if (run.Count == 0)
            return [_route[at]];
        // Keep only the corners of the run.
        for (var i = run.Count - 2; i > 0; --i)
            if (MathF.Abs((run[i] - run[i - 1]).Cross(run[i + 1] - run[i])) < 0.01f)
                run.RemoveAt(i);
        return run;
    }

    private int? NearestOnRoute(List<WPos> route, WPos position, DateTime now)
    {
        var due = Math.Min(route.Count - 1, (int)((now - _routeAt).TotalSeconds / RouteStep) + 1);
        var at = 0;
        for (var i = 1; i <= due; ++i)
            if ((route[i] - position).LengthSq() <= (route[at] - position).LengthSq())
                at = i;
        return at;
    }
    // A cloud-free approach that does not reach the pocket in time.
    private sealed class PartialRoute(IEnumerable<WPos> path) : List<WPos>(path);
    private const double KeepFullRoute = 1.5;
    // Best plan first. A plan that holds only a short time inside the pocket can end in a corner a later
    // cloud crosses (213938 replay, 17.3s), and the first route found through a corridor may pass closer
    // than it has to, so look for a roomy plan of the whole pulse train before the thin ones.
    private readonly record struct Plan(float Speed, float CloudExtra, float Hold);
    private static readonly Plan[] Plans = [new(RouteSpeed, 0.7f, float.MaxValue), new(RouteSpeed, 0, float.MaxValue), new(RouteSpeed, 0, HoldSeconds), new(TightRouteSpeed, 0, HoldSeconds)];
    private const int MaxPublishedSteps = 8;
    // A pulse-train plan stays valid to its last step. Dropping it after KeepFullRoute swapped a route
    // on schedule for a partial approach 1.2s before the first pulse (213938 replay, 11.38s).
    private static double RouteLifetime(List<WPos> route) => Math.Max(KeepFullRoute, route.Count * RouteStep);

    // Each cloud at one instant: forecast centre and the track segment validation sees.
    private readonly record struct CloudAt(WPos Centre, WPos TrackStart, WDir Track, float TrackLengthSq);
    private static CloudAt[] CloudsAt(float t, (WPos Position, WDir Velocity)[] clouds)
        => [.. clouds.Select(c => new CloudAt(c.Position + c.Velocity * t, c.Position + c.Velocity * (t - TrackBefore),
            c.Velocity * (TrackBefore + TrackAfter), (c.Velocity * (TrackBefore + TrackAfter)).LengthSq()))];
    // Clearance shortfall against every cloud (<= 0 means clear).
    private static float CloudShortfall(WPos p, CloudAt[] clouds, float extra = 0)
    {
        var worst = -1f;
        foreach (var c in clouds)
        {
            var d = p - c.Centre;
            // Far clouds cannot matter: the track reaches at most ~1.2y from the centre.
            if (MathF.Abs(d.X) > CloudPointClearance + extra + 1.5f || MathF.Abs(d.Z) > CloudPointClearance + extra + 1.5f)
                continue;
            worst = MathF.Max(worst, CloudPointClearance + extra - d.Length());
            var along = c.TrackLengthSq > 1e-6f ? Math.Clamp((p - c.TrackStart).Dot(c.Track) / c.TrackLengthSq, 0, 1) : 0;
            worst = MathF.Max(worst, CloudTrackClearance + extra - (p - (c.TrackStart + c.Track * along)).Length());
        }
        return worst;
    }

    // The pocket sits on the arena edge. v5 replay parked the player 0.05y inside the boundary, where the
    // shared planner's edge margin left no reachable cell and it stood there for 50s after the breath.
    private const float EdgeMargin = 1.2f;
    private static bool InsideWithMargin(ArenaBounds bounds, WDir offset)
    {
        var length = offset.Length();
        return bounds.Contains(offset) && (length < 0.01f || bounds.Contains(offset * ((length + EdgeMargin) / length)));
    }
    private static bool InPool(WPos p, WPos[] pools)
    {
        foreach (var pool in pools)
            if (p.InCircle(pool, PoolClearance))
                return true;
        return false;
    }

    // Breadth-first reachability over (cell, step) up to the end of the pocket window. Every step must
    // keep the cloud clearance (a step inside it is allowed only while it strictly improves, so a
    // player who starts next to a cloud can still leave) and, inside the pulse window, the pocket.
    private static WDir[] MovesAt(float speed) => [.. Enumerable.Range(0, 9).Select(i => new WDir(i / 3 - 1, i % 3 - 1))
        .Select(d => d.LengthSq() > 0 ? d.Normalized() * (speed * RouteStep) : d)];
    private static readonly WDir[] Moves = MovesAt(RouteSpeed), TightMoves = MovesAt(TightRouteSpeed);
    private const int GridHalf = 46; // cells each side of the centre: covers a 22y half-extent
    private static List<WPos>? Route(WPos start, (WPos Position, WDir Velocity)[] clouds, WPos[] pools, ArenaBounds bounds,
        WPos center, WPos origin, float planRadius, float innerRim, float pocketFrom, float pocketUntil, float[] pulses, float exitAt, float finishRadius, bool pocketRequired, Plan? plan = null)
    {
        var (speed, extra, hold) = plan ?? new(RouteSpeed, 0, HoldSeconds);
        var moves = speed == RouteSpeed ? Moves : TightMoves;
        // Plan until a short hold inside the pocket window; the 0.2s replans extend it.
        var end = float.IsFinite(exitAt) ? exitAt + 0.2f : hold == float.MaxValue ? pocketUntil : MathF.Min(pocketUntil, MathF.Max(pocketFrom, 0) + hold);
        var steps = Math.Clamp((int)MathF.Ceiling(end / RouteStep), 1, pocketRequired ? MaxSteps : FallbackSteps);
        const int size = 2 * GridHalf + 1;
        var claimed = new int[size * size];
        var nodes = new List<(WPos P, int Parent, float Shortfall)> { (start, -1, CloudShortfall(start, CloudsAt(0, clouds), extra)) };
        var frontier = new List<int> { 0 };
        var order = new float[0];
        var scores = new float[9];
        var moveOrder = new int[9];
        for (var step = 1; step <= steps; ++step)
        {
            var t = step * RouteStep;
            var inPocket = pocketRequired && (pulses.Length == 0 ? t >= pocketFrom && t <= pocketUntil : pulses.Any(p => MathF.Abs(t - p) <= PulseSlack));
            var forecast = CloudsAt(t, clouds);
            // A landing's goal is an annulus, then the outer escape. Ranking
            // every frontier by distance to the exact center made the search
            // spend its slack walking inward before a last-second escape. That
            // detour becomes late when a published route is resumed from the
            // player's real progress rather than its old search-start position.
            var preferredRadius = float.IsFinite(exitAt) && !float.IsPositiveInfinity(pocketFrom) && t <= pocketUntil
                ? (innerRim + planRadius) * .5f : finishRadius;
            float GoalDistance(WPos p) => MathF.Abs((p - origin).Length() - preferredRadius);
            // A cell is claimed (step) or blocked (-step) at most once per step.
            var reached = new List<int>();
            // Expand nodes closest to the pocket first so they claim shared cells.
            if (order.Length < frontier.Count) order = new float[Math.Max(frontier.Count, 2 * order.Length)];
            for (var i = 0; i < frontier.Count; ++i) order[i] = GoalDistance(nodes[frontier[i]].P);
            var sorted = frontier.ToArray();
            Array.Sort(order, sorted, 0, sorted.Length);
            foreach (var index in sorted)
            {
                var (at, _, shortfall) = nodes[index];
                for (var m = 0; m < 9; ++m)
                {
                    scores[m] = GoalDistance(at + moves[m]);
                    moveOrder[m] = m;
                }
                Array.Sort(scores, moveOrder);
                foreach (var m in moveOrder)
                {
                    var p = at + moves[m];
                    var gx = (int)MathF.Round((p.X - center.X) / RouteCell) + GridHalf;
                    var gz = (int)MathF.Round((p.Z - center.Z) / RouteCell) + GridHalf;
                    if (gx < 0 || gz < 0 || gx >= size || gz >= size)
                        continue;
                    var cell = gz * size + gx;
                    if (claimed[cell] == step || claimed[cell] == -step)
                        continue;
                    // Also drop cells from which the pocket can no longer be reached in time.
                    if (!InsideWithMargin(bounds, p - center) || InPool(p, pools) || inPocket && (!p.InCircle(origin, planRadius) || p.InCircle(origin, innerRim))
                        || t >= exitAt && p.InCircle(origin, LandingExitRadius)
                        || pocketRequired && t < pocketFrom && (p - origin).Length() - planRadius > (pocketFrom - t) * speed + RouteCell)
                    {
                        claimed[cell] = -step;
                        continue;
                    }
                    var s = CloudShortfall(p, forecast, extra);
                    // Inside the clearance only while strictly leaving it (another parent may still reach it).
                    if (s > 0 && (shortfall <= 0 || s >= shortfall - 0.05f))
                    {
                        if (shortfall <= 0 || s > 1.5f) claimed[cell] = -step;
                        continue;
                    }
                    nodes.Add((p, index, s));
                    claimed[cell] = step;
                    reached.Add(nodes.Count - 1);
                }
            }
            if (reached.Count == 0)
                return null;
            frontier = reached;
        }
        // A partial landing approach must also aim for the inner rim: heading for
        // the exact center left no cloud-free escape before 48828 in the v8 replay.
        var best = frontier.MinBy(i => MathF.Abs((nodes[i].P - origin).Length() - finishRadius));
        var path = new List<WPos>();
        for (var i = best; i >= 0; i = nodes[i].Parent) path.Add(nodes[i].P);
        path.Reverse();
        return path;
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19672)
            End();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

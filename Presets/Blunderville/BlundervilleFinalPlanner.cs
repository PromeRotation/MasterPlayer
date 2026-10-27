using System.Numerics;
using System.Diagnostics;
using System.Collections.Concurrent;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// ARR 2026-10-12 15:10:49: the whole Final slope between z≈286 and the stairs
// is covered by periodic circles, sweeps and waves. The general timed solver
// either found no route with its 0.5s cushions (stop-and-go) or, with smaller
// cushions, arrived late after scan/dispatch lag and was hit. Search lanes
// layer by layer with explicit waits and an explicit execution lag instead,
// then publish each wait-free stretch as a direct traversal.
internal sealed class BlundervilleFinalPlanner
{
    // Active down to just above the stair run-up start (2.2m before the z=153
    // takeoff, inside the jump executor's run-up band), then hand over.
    // FloorClearance .25: the right walkway lane x=6 is 0.275m from the moat
    // rim at x=5.725 (z 262.8–268.2). Lane 7 is a 1.45s chord through the
    // 2.54s x=10 circle and never fits (23:23 replay side -1 idled 18s).
    // Top 321: from the start line (2026-10-14 sims: the race-route hand-over
    // at z=290 stopped 0.67s in every run, live 0.2-0.3s at z=284).
    internal const float Top = 321, Bottom = 155.4f, Exit = 156, RunupZ = 155.2f, LayerStep = 2, MaxLateral = 2, Clearance = .3f, FloorClearance = .25f;
    internal const float Horizon = 60, ArrivalTolerance = .35f, OffPath = 1f;
    // |x|=16 is a 0.55m strip behind the z=178–182 pillars that dead-ends
    // (2026.10.07 19:51 replay side -1 idled there for 20s).
    private const int MaxLane = 15;
    internal sealed record Profile(BlundervilleCourse Course, float Top, float Bottom, float Exit, int Lanes, float MinY, float? Speed, bool Stairs, string Name,
        (float From, float To)[]? Strips = null, Vector3[]? Spoke = null, ushort Colour = 0,
        float LaneWidth = 1, float HazardClearance = Clearance, double ExecutionLag = Lag, float StandClearance = Clearance, bool Bomb = false,
        double WaitSlip = 0, double HazardLead = Lead, double HazardWaveLead = WaveLead, double HazardWaveTail = WaveTail, bool Returning = false)
    {
        // An intermediate plan may only stop where standing is not a gamble.
        public bool Rest(float z) => Strips == null || Strips.Any(s => z >= s.From && z <= s.To);
    }
    // Hazard-free strips of the Final (32 ARRs): above 272.5, 257–262.5,
    // 235.3–237 and 180.7–187.4. 2026-10-13: with the corrected wave boxes a
    // plan ending mid wave zone (z 206 north of the pit) found no way on for
    // 27s; humans cross each zone in one go and only wait on these strips.
    internal static readonly Profile Final = new(BlundervilleCourse.Final, Top, Bottom, Exit, MaxLane, 2, null, true, "决赛",
        [(272.5f, Top + 4), (257, 262.5f), (235.3f, 237), (180.7f, 187.4f)],
        HazardLead: FinalLead, HazardWaveLead: FinalWaveLead, HazardWaveTail: FinalWaveTail);
    // Delivery carries (2026-10-13 route research, 21 rounds): the generic
    // solver walked onto the E bridge, where one of two charger lines along
    // the bridge fires about every 2s, and then found no departure and stood
    // in it. One corridor per colour through the hold points of the timed
    // carry plans; stations every 2m along it, lanes 1m apart across it, the
    // last station is the drop trigger. The hub start is reached by the route.
    internal static readonly Profile DeliveryWest = Corridor("搬运西线", 35,
        [new(-200, 6, 252), new(-206.5f, 6, 258), new(-221, 6, 265.5f), new(-240.2991f, 6, 275.4989f)]);
    internal static readonly Profile DeliveryEast = Corridor("搬运东线", 36,
        [new(-200, 6, 252), new(-190.5f, 6, 257), new(-181, 6, 263), new(-172, 6, 268.5f), new(-159.4339f, 6, 275.2527f)]);
    internal static readonly Profile DeliveryNorth = Corridor("搬运北线", 37,
        // The drop pad is cut off at z~208.2 for x -203.4..-199.5: the planned
        // carries come down the west walkway and step east of that hole.
        [new(-200, 6, 252), new(-200.5f, 6, 233), new(-203.2f, 6, 229.9f), new(-200, 6, 227),
            new(-204.1f, 6, 224.1f), new(-204, 6, 220), new(-204, 6, 212.2f),
            new(-200.6f, 6, 210.9f), new(-199.2f, 6, 207.8f), new(-199.6f, 6, 207), new(-200.1303f, 6, 205.1951f)]);
    internal static readonly Profile ReturnWest = Return(DeliveryWest), ReturnEast = Return(DeliveryEast), ReturnNorth = Return(DeliveryNorth);
    private static Profile Return(Profile outbound) => outbound with
    {
        Spoke = outbound.Spoke!.Reverse().ToArray(), Returning = true, Name = outbound.Name + "返中央"
    };
    // 2026-10-13 route research: MP stopped 0.8–2.8s before each of the three
    // 34716 swinger rows on the FirstA ramp (z -130.3/-138.8/-147.3), 10.3s
    // for the section against 3.6s for the best humans (17:44, 16:55, 02:33).
    // Each row sweeps its three 3m columns one after another every 2.1s, so a
    // column is clear for ~1.6s between hits: a timed corridor crosses them.
    // The race route's west lane, x -213..-205.
    internal static readonly Profile FirstARamp = new(BlundervilleCourse.FirstA, 0, 0, 0, 4, 10, null, false, "坡道摆锤", null,
        [new(-209, BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstA, -126), -126),
            new(-209, BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstA, -151.5f), -151.5f)]);
    // The FirstA slope (z -212.6..-247): four 34716 rows alternating centre
    // and side groups, and the 3802 bomb gained at -212.6 that explodes on any
    // movement in its last ~0.6s. MP took 12.7–14.0s against 6.6s for the best
    // humans; the corridor also places the bomb stand-still where no sweep
    // reaches it (the forced stop used to land inside a row: 01:01, 02:33).
    internal static readonly Profile FirstASlope = new(BlundervilleCourse.FirstA, 0, 0, 0, 11, 9.5f, null, false, "炸弹坡", null,
        [new(-200, BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstA, -209), -209),
            new(-200, BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstA, -247.5f), -247.5f)], Bomb: true);
    // The schedule footprints already include their calibrated growth and the
    // windows their +-0.45s, so no extra clearance and a 0.25s lag: with the
    // Final margins the E bridge pendulum row (gaps 0.36–0.79s between 11
    // windows per 10s) never opened. Half-metre lanes find the slivers.
    private static Profile Corridor(string name, ushort colour, Vector3[] spoke)
        => new(BlundervilleCourse.Delivery, 0, 0, 0, 3, 5, null, false, name, null, spoke, colour, .5f, 0, Lag, .4f,
            WaitSlip: DeliveryWaitSlip);
    private readonly Profile _profile;
    public BlundervilleFinalPlanner(Profile? profile = null) => _profile = profile ?? Final;
    internal const double Lag = .35, StandAfterGoal = 2.5, Lead = .15, Tail = .1, WaveLead = .4, WaveTail = .3;
    // The runtime re-validates a published stretch against the raw windows
    // widened by 0.2s/0.1s at the actual (up to Lag late) time and stops the
    // player when it fails, mid-slope. Final leads must stay above its 0.2s.
    // Wave cycle forecasts carry their own (measured) step spread.
    internal const double FinalLead = .21, FinalWaveLead = .25, FinalWaveTail = .1;
    private static readonly double[] Waits = [0, .5, 1, 1.5, 2, 3];
    // Delivery pendulum cells fire twice per 4.2s: a hold must reach the next gap.
    private static readonly double[] CorridorWaits = [0, .5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5];
    // 2026-10-14 02:08 ARR / Delivery replays: the east carry waited 0.5s at
    // (-171.7,266.9), 0.3m off the last charger lane. The departure slipped
    // (runtime start + its stricter check), no plan left that lane and the
    // charge took the crystal (all 3 replay losses). A Delivery wait point
    // must stay clear this long past its departure: a pocket, not a lane.
    // 15 replays: 0.6s with the retreat below delivers all three in every
    // round (before 11/15, 3 crystals lost); 1.0s stalls the east carry.
    internal const double DeliveryWaitSlip = .6;

    internal sealed record Waypoint(Vector3 Position, DateTime Arrival, DateTime Departure);
    private sealed record Node(Vector3 Position, double Time, double Wait, Node? Parent, int Lane);
    private readonly record struct Window(IGeometryShape Geometry, GeometryBounds Bounds, double From, double Until, bool Wind = false);

    private List<Waypoint>? _plan;
    private DateTime? _planBomb;
    private int _next, _stretchStart;
    private Vector3[]? _stretch;
    private DateTime _lastTime, _retry, _stretchSince;
    private Vector3 _stretchFrom;
    private Task<List<Waypoint>?>? _pendingSearch;
    private Task<List<Waypoint>?>? _continuation;
    private Waypoint? _continuationFrom;
    private CancellationTokenSource? _searchCancellation;
    private CancellationTokenSource? _continuationCancellation;
    private readonly BlundervillePlanningBudget _planningBudget = new();
    private long _pendingClock, _continuationClock;
    private bool _workStarted, _pendingCold, _continuationCold;
    private double _pollSeconds = .1;
    private Vector3 _pendingOrigin;
    private DateTime _pendingAt;
    internal Task? PendingSearch => _pendingSearch;

    public IReadOnlyList<Waypoint>? Plan => _plan;
    // The current plan already contains the bomb stand-still.
    public bool HoldsBomb => _plan != null && _planBomb != null;

    public void Reset()
    {
        _searchCancellation?.Cancel(); _searchCancellation?.Dispose(); _searchCancellation = null;
        _plan = null; _planBomb = null; _next = 0; _stretch = null; _pendingSearch = null; _lastTime = _retry = _stretchSince = default;
        _continuation = null; _continuationFrom = null;
        _continuationCancellation?.Cancel(); _continuationCancellation?.Dispose(); _continuationCancellation = null;
    }

    public bool Active(BlundervilleRaceState state, Vector3 player, DateTime now)
        => state.IsRacing && !state.Qualified && state.Course == _profile.Course && state.CanMove(now) && player.Y > _profile.MinY
            && (_profile.Spoke is { } spoke
                ? (_profile.Returning ? state.NeedsPickup && state.LastCrystalParam == _profile.Colour : state.CarryCrystalParam == _profile.Colour)
                    && Along(spoke, player) is var (s, _, distance, length)
                    && distance <= _profile.Lanes + 2 && s < length - .3f
                : player.Z > _profile.Bottom && player.Z <= _profile.Top && MathF.Abs(player.X) <= _profile.Lanes + 2);

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now, DirectTraversalExecution? execution = null)
    {
        if (now < _lastTime) Reset();
        if (now > _lastTime && _lastTime != default) _pollSeconds = Math.Clamp((now - _lastTime).TotalSeconds, 1d / 240, .1);
        _lastTime = now;
        // Other courses' planners (bumper, ice, arrow bridge) publish their own
        // direct traversals; clearing that flag here stalled FirstA in the
        // 2026.10.12 16:55 ARR (1.14.0). Only ever pass them through.
        if (!Active(state, player, now) || normal.Hold || normal.RecoveryFloor != null) { Reset(); return normal; }
        var floor = BlundervilleRoutes.NavigationBoundary(state.Course, player);
        var speed = _profile.Speed ?? state.NavigationBaseSpeed(now);
        // 23:45 ARR: a 34801 sweep threw the player to (1.66,190.02), 0.38m
        // inside the modelled hole around the z=188 fountain. Every lane edge
        // from there failed the floor check; the planner reported a dead end
        // and the race route could not leave either, so the player stood until
        // eliminated. Step back onto the modelled floor first.
        // 2026-10-15 12:05 ARR: the north carry stood 16s at (-198.2,211.7),
        // just inside the modelled hole south of the north hammers; no corridor
        // vertex was reachable by floor from there. Same rejoin for corridors.
        if (!floor.Contains(player, 0))
        {
            Reset();
            var outside = new HazardIndex(hazards, now, _profile);
            return Rejoin(_profile, floor, outside, player, speed) is { } rejoin
                ? normal with { Target = rejoin.Target, Hold = false, Jump = false, Hint = $"{_profile.Name}：被击出可行走区，先回到地面",
                    DirectTraversal = true, ContinuousPath = [rejoin.Target], RecoveryFloor = rejoin.Floor }
                : normal with { Target = player, Hold = true, Jump = false, Hint = $"{_profile.Name}：被击出可行走区，等待机关间隙回到地面" };
        }
        // Charger body zones (delivery/body/) are a preference: plan around
        // them when a way exists, never block or drop a plan on them alone.
        var soft = hazards.Any(h => h.Id.StartsWith(BlundervilleDeliverySchedule.BodyPrefix, StringComparison.Ordinal));
        var index = new HazardIndex(soft ? hazards.Where(h => !h.Id.StartsWith(BlundervilleDeliverySchedule.BodyPrefix, StringComparison.Ordinal)).ToArray() : hazards, now, _profile);
        // A newly gained (or refreshed) bomb needs its stand-still in the plan.
        var bomb = _profile.Bomb && state.AccelerationBombExpiresAt is { } expires && expires != DateTime.MaxValue ? expires : (DateTime?)null;
        if (_plan != null)
        {
            var following = _profile.Course == BlundervilleCourse.Delivery && _stretch != null && execution is { HasControl: true }
                && execution.Path.SequenceEqual(_stretch);
            if (following)
                _next = Math.Max(_next, Math.Min(_plan.Count, _stretchStart + execution!.CurrentIndex));
            while (_next < _plan.Count && Distance(player, _plan[_next].Position) <= (_profile.Course == BlundervilleCourse.Delivery ? .05f : ArrivalTolerance)) ++_next;
            // The runtime revalidates the published run every frame. A clock
            // correction after a later wait must not stop this safe run in a
            // charger lane; check the remaining plan at its next wait point.
            if (_next >= _plan.Count || execution is { Rejected: true } || OffRoute(player)
                || !following && !StillSafe(index, floor, player, now, speed)
                || Refused(execution, player, now) || bomb != _planBomb)
                _plan = null;
        }
        if (_plan != null && _profile.Course != BlundervilleCourse.Delivery) Continue(hazards, index, floor, now, speed);
        if (_plan == null)
        {
            if (now < _retry) return Fallback(index, floor, hazards, player, now, speed);
            _planBomb = bomb;
            (double, double)? hold = bomb is { } b ? ((b - now).TotalSeconds - BombLead, (b - now).TotalSeconds + BombTail) : null;
            if (_profile.Course == BlundervilleCourse.Delivery)
            {
                if (_pendingSearch == null)
                {
                    _pendingClock = Stopwatch.GetTimestamp(); _pendingCold = !_workStarted; _workStarted = true;
                    // Snapshot-only work. Never make the framework wait for
                    // the graph or for its parallel workers to finish.
                    var captured = hazards.ToArray();
                    _pendingOrigin = player; _pendingAt = now;
                    _searchCancellation = new();
                    var cancellation = _searchCancellation.Token;
                    _pendingSearch = SearchDeliveryAsync(captured, floor, player, now, speed, soft, cancellation);
                    return Fallback(index, floor, hazards, player, now, speed);
                }
                if (!_pendingSearch.IsCompleted) return Fallback(index, floor, hazards, player, now, speed);
                var completed = _pendingSearch;
                _pendingSearch = null;
                _searchCancellation?.Dispose(); _searchCancellation = null;
                // Read Exception as well, so discarded failed tasks are observed.
                var result = completed.IsCompletedSuccessfully ? completed.Result : null;
                _ = completed.Exception;
                if ((now - _pendingAt).TotalSeconds < 3 && Distance(player, _pendingOrigin) <= 1)
                    _plan = Retime(result, index, floor, player, now, speed);
                _planningBudget.Observe(Stopwatch.GetElapsedTime(_pendingClock).TotalSeconds, _pendingCold);
            }
            else
            {
                var began = Stopwatch.GetTimestamp();
                _plan = SearchPreferred(index, hazards, floor, player, now, speed, hold, soft);
                _planningBudget.Observe(Stopwatch.GetElapsedTime(began).TotalSeconds, !_workStarted);
                _workStarted = true;
            }
            _next = 1; _stretch = null;
            if (_plan is not { Count: >= 2 })
            {
                _plan = null; _retry = now.AddSeconds(.2);
                // In a dead end no forward layer is reachable by floor; hand
                // back to the race route, whose solver can walk back out.
                if (!CanAdvance(_profile, floor, player))
                {
                    // 23:45 ARR: after rejoining the floor at (2.25,190.15) beside the
                    // fountain, no 2m layer edge cleared its rim; the race route could
                    // not walk out either. Step to the nearest lane cell that advances.
                    if (Rejoin(_profile, floor, index, player, speed) is { } step && Distance(step.Target, player) > .05f)
                        return normal with { Target = step.Target, Hold = false, Jump = false, Hint = $"{_profile.Name}：离开死角，回到可前进车道",
                            DirectTraversal = true, ContinuousPath = [step.Target], RecoveryFloor = step.Floor };
                    return normal with { Hint = $"{_profile.Name}：退出死角，改走常规路线" };
                }
                return Fallback(index, floor, hazards, player, now, speed);
            }
        }
        var current = _plan[_next - 1];
        // Every window tolerates the player up to Tail early. ArrivalTolerance
        // advances onto a pass-through point a few ms before its arrival, and
        // holding there stopped the run inside the next window; the runtime
        // then refused the restart (151049 replays: 6 hits in 420 runs). A real
        // wait also releases one 10Hz scan early to absorb the start latency.
        if (current.Departure - current.Arrival >= TimeSpan.FromMilliseconds(50)
            && current.Departure - now > TimeSpan.FromSeconds(_profile.Course == BlundervilleCourse.Delivery ? 0 : Tail))
        {
            _stretch = null;
            if (Distance(player, current.Position) <= ArrivalTolerance)
                return new(player, true, false, null, $"{_profile.Name}：在安全处等待机关间隙", false);
            // A replan while still moving can overshoot its first wait point
            // (Delivery replay: 0.56m into a charger line); step back onto it.
            return new(current.Position, false, false, null, $"{_profile.Name}：回到等待点", true) { ContinuousPath = [current.Position] };
        }
        // Publish one unchanged stretch up to the next wait; the runtime
        // follows GreenMove progress, so the path must not shrink per point.
        if (_stretch == null || _next >= _stretchStart + _stretch.Length)
        {
            var end = _next;
            while (end < _plan.Count - 1 && _plan[end].Departure - _plan[end].Arrival < TimeSpan.FromMilliseconds(50)) ++end;
            _stretchStart = _next;
            _stretch = _plan.Skip(_next).Take(end - _next + 1).Select(w => w.Position).ToArray();
            _stretchSince = default;
        }
        return new(_stretch[^1], false, false, null, $"{_profile.Name}：按机关时序连续穿行", true) { ContinuousPath = _stretch };
    }

    // A plan ends at the horizon or at a refuge short of the stairs; the next
    // one used to be searched only on arrival there (one more stop-and-start
    // and a search on the game thread). Search the continuation from the
    // plan's end, at its arrival time, on a worker shortly before the player
    // gets there; append it once it validates against the newest forecasts.
    private void Continue(IReadOnlyList<BlundervilleHazard> hazards, HazardIndex index, IGeometryShape floor, DateTime now, float speed)
    {
        var end = _plan![^1];
        if (_profile.Spoke is { } spoke ? Distance(end.Position, spoke[^1]) < 1
            : end.Position.Z <= Exit + .01f || _profile.Stairs && MathF.Abs(end.Position.Z - RunupZ) < .01f)
        {
            _continuationCancellation?.Cancel(); _continuationCancellation?.Dispose(); _continuationCancellation = null;
            _continuation = null; return;
        }
        if (_continuation == null)
        {
            if ((end.Arrival - now).TotalSeconds > _planningBudget.LeadSeconds(_planningBudget.Samples == 0, _pollSeconds)) return;
            _continuationClock = Stopwatch.GetTimestamp(); _continuationCold = !_workStarted; _workStarted = true;
            var captured = hazards.ToArray(); var profile = _profile; var from = end;
            _continuationFrom = end;
            _continuationCancellation = new(); var token = _continuationCancellation.Token;
            _continuation = Task.Run(() => Search(profile, new HazardIndex(captured, from.Arrival, profile), floor, from.Position, from.Arrival, speed, cancellation: token));
            return;
        }
        if (!_continuation.IsCompleted) return;
        var done = _continuation; _continuation = null;
        var result = done.IsCompletedSuccessfully ? done.Result : null;
        _ = done.Exception;
        _continuationCancellation?.Dispose(); _continuationCancellation = null;
        try
        {
            if (result is not { Count: >= 2 } || _continuationFrom != end || Distance(result[0].Position, end.Position) > .01f) return;
            // Revalidate the continuation against the latest forecasts.
            var time = (result[0].Departure - now).TotalSeconds; var position = end.Position;
            if (result[0].Departure > end.Arrival && !index.Stand(position, (end.Arrival - now).TotalSeconds, time)) return;
            for (var i = 1; i < result.Count; ++i)
            {
                var arrival = (result[i].Arrival - now).TotalSeconds; var depart = (result[i].Departure - now).TotalSeconds;
                if (!ArenaBoundaryTraversal.ContainsTube(floor, position, result[i].Position, FloorClearance) || !index.Segment(position, result[i].Position, time, arrival)) return;
                if (depart > arrival && !index.Stand(result[i].Position, arrival, depart)) return;
                time = depart; position = result[i].Position;
            }
            var count = _plan.Count;
            _plan[^1] = end with { Departure = result[0].Departure };
            _plan.AddRange(result.Skip(1));
            // A wait-free junction extends the published stretch: its prefix is
            // unchanged, so the runtime keeps following it without a stop.
            if (_stretch != null && _stretchStart + _stretch.Length == count && result[0].Departure - end.Arrival < TimeSpan.FromMilliseconds(50))
            {
                var last = count - 1;
                while (last < _plan.Count - 1 && _plan[last].Departure - _plan[last].Arrival < TimeSpan.FromMilliseconds(50)) ++last;
                _stretch = _plan.Skip(_stretchStart).Take(last - _stretchStart + 1).Select(w => w.Position).ToArray();
            }
        }
        finally
        {
            _planningBudget.Observe(Stopwatch.GetElapsedTime(_continuationClock).TotalSeconds, _continuationCold);
        }
    }

    private List<Waypoint>? SearchPreferred(HazardIndex index, IReadOnlyList<BlundervilleHazard> hazards, IGeometryShape floor,
        Vector3 player, DateTime now, float speed, (double, double)? hold, bool soft, CancellationToken cancellation = default)
    {
        var plan = Search(_profile, index, floor, player, now, speed, hold, cancellation);
        if (cancellation.IsCancellationRequested) return null;
        if (soft && Search(_profile, new HazardIndex(hazards, now, _profile), floor, player, now, speed, hold, cancellation) is { } preferred
            && (plan == null || Distance(preferred[^1].Position, plan[^1].Position) < .5f
                && preferred[^1].Arrival <= plan[^1].Arrival.AddSeconds(1))) return preferred;
        return plan;
    }

    private async Task<List<Waypoint>?> SearchDeliveryAsync(BlundervilleHazard[] hazards, IGeometryShape floor,
        Vector3 player, DateTime now, float speed, bool soft, CancellationToken cancellation)
    {
        var hard = soft ? hazards.Where(h => !h.Id.StartsWith(BlundervilleDeliverySchedule.BodyPrefix, StringComparison.Ordinal)).ToArray() : hazards;
        var basic = Task.Run(() => Search(_profile, new HazardIndex(hard, now, _profile), floor, player, now, speed, null, cancellation));
        if (!soft) return await basic.ConfigureAwait(false);
        var sheltered = Task.Run(() => Search(_profile, new HazardIndex(hazards, now, _profile), floor, player, now, speed, null, cancellation));
        await Task.WhenAll(basic, sheltered).ConfigureAwait(false);
        if (cancellation.IsCancellationRequested) return null;
        var plan = basic.Result; var preferred = sheltered.Result;
        return preferred != null && (plan == null || Distance(preferred[^1].Position, plan[^1].Position) < .5f
            && preferred[^1].Arrival <= plan[^1].Arrival.AddSeconds(1)) ? preferred : plan;
    }

    // The background result was timed from its request, not from completion.
    // Absorb computation delay in the first wait, and revalidate every leg
    // against the newest clock before granting it movement control.
    private List<Waypoint>? Retime(List<Waypoint>? plan, HazardIndex index, IGeometryShape floor, Vector3 player, DateTime now, float speed)
    {
        if (plan is not { Count: >= 2 }) return null;
        var result = new List<Waypoint>();
        var from = player; var arrival = now;
        for (var i = 0; i < plan.Count; ++i)
        {
            var to = i == 0 ? player : plan[i].Position;
            if (i > 0)
            {
                var start = result[^1].Departure;
                arrival = start.AddSeconds(Distance(from, to) / speed);
                if (!BlundervilleDeliverySearch.FloorLink(floor, from, to, i == 1)
                    || !index.Segment(from, to, (start - now).TotalSeconds, (arrival - now).TotalSeconds)) return null;
            }
            var departure = plan[i].Departure > plan[i].Arrival && plan[i].Departure > arrival ? plan[i].Departure : arrival;
            if (departure > arrival && !index.Stand(to, (arrival - now).TotalSeconds, (departure - now).TotalSeconds + _profile.WaitSlip)) return null;
            result.Add(new(to, arrival, departure)); from = to;
        }
        if (Distance(from, _profile.Spoke![^1]) >= 1.5f && !index.Stand(from, (arrival - now).TotalSeconds, (arrival - now).TotalSeconds + StandAfterGoal)) return null;
        return result;
    }

    // Inside the Final hazard slope the ordinary solver re-creates the
    // stop-and-go: hold where standing stays clear, otherwise step aside.
    private BlundervilleRaceDecision Fallback(HazardIndex index, IGeometryShape floor, IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now, float speed)
    {
        _stretch = null;
        if (index.Stand(player, 0, 1.5)) return new(player, true, false, null, $"{_profile.Name}：等待可通行的机关时序", false);
        // Delivery only: the FirstA corridors include the bomb stand-still.
        if (_profile.Course == BlundervilleCourse.Delivery && _profile.Spoke is { } spoke
            && Retreat(_profile, spoke, index, floor, player, speed) is { } pocket)
            return new(pocket, false, false, null, $"{_profile.Name}：退到机关够不到的位置", true) { ContinuousPath = [pocket] };
        if (BlundervilleRefuge.Find(floor, hazards, player, now) is { } refuge)
            return new(refuge, false, false, null, $"{_profile.Name}：先避开即将判定的机关", true) { ContinuousPath = [refuge] };
        return new(player, true, false, null, $"{_profile.Name}：无可行避让，原地等待", false);
    }

    internal const float RejoinReach = 3.5f;
    // Nearest point of the modelled floor with lane clearance that the player
    // can reach and stand on; the floor is widened by the short step itself.
    internal static (Vector3 Target, IGeometryShape Floor)? Rejoin(Profile profile, IGeometryShape floor, HazardIndex index, Vector3 player, float speed)
    {
        var inside = floor.Contains(player, 0);
        for (var reach = .3f; reach <= RejoinReach + 1e-3f; reach += .1f)
        {
            Vector3? best = null; var bestScore = float.MaxValue;
            for (var step = 0; step < 72; ++step)
            {
                var angle = step * MathF.Tau / 72;
                var to = player + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * reach;
                if (!ArenaBoundaryTraversal.ContainsTube(floor, to, to, FloorClearance) || !CanAdvance(profile, floor, to)) continue;
                // From the floor the step itself must stay on it.
                if (inside && !ArenaBoundaryTraversal.ContainsTube(floor, player, to, 0)) continue;
                var travel = reach / speed;
                if (!index.Segment(player, to, 0, travel) || !index.Stand(to, travel, travel + 1.5)) continue;
                // Prefer the downhill (course) direction among equally short steps.
                var score = to.Z - player.Z;
                if (score < bestScore) { best = to; bestScore = score; }
            }
            if (best is { } target)
                return (target, new BlundervilleNavigation.CrossingCorridor(floor,
                    new CircleGeometry(Vector3.Lerp(player, target, .5f), reach / 2 + .6f)));
        }
        return null;
    }

    // 2026-10-14 02:08 ARR: with no way on, the east carry stood in the last
    // charger lane ("无可行避让，原地等待") until the charge took the crystal;
    // the generic refuge found nothing. Step back along the corridor (or
    // across it) to the nearest station that stays clear for a while.
    internal const double RetreatHold = 1.5;
    private static Vector3? Retreat(Profile profile, Vector3[] spoke, HazardIndex index, IGeometryShape floor, Vector3 player, float speed)
    {
        var (along, _, _, _) = Along(spoke, player);
        var width = (int)MathF.Round(profile.Lanes / profile.LaneWidth);
        Vector3? best = null; var bestTime = double.MaxValue;
        foreach (var (station, point, lateral) in Stations(spoke))
        {
            if (station > along + LayerStep + .01f || station < along - 10) continue;
            for (var lane = -width; lane <= width; ++lane)
            {
                var to = point + lateral * (lane * profile.LaneWidth);
                var travel = Distance(player, to) / speed;
                if (travel < .05 || travel >= bestTime) continue;
                if (!ArenaBoundaryTraversal.ContainsTube(floor, player, to, FloorClearance)) continue;
                if (!index.Segment(player, to, 0, travel) || !index.Stand(to, travel, travel + RetreatHold)) continue;
                best = to; bestTime = travel;
            }
        }
        return best;
    }

    // The runtime releases a plain direct path it judges unsafe without
    // flagging it (only motion routes are marked Rejected); republishing the
    // same stretch then leaves the player standing in the next window.
    private bool Refused(DirectTraversalExecution? execution, Vector3 player, DateTime now)
    {
        if (_stretch == null || execution is not { HasControl: false }) { _stretchSince = default; return false; }
        if (_stretchSince == default) { _stretchSince = now; _stretchFrom = player; return false; }
        return (now - _stretchSince).TotalSeconds > .2 && Distance(player, _stretchFrom) < .1f;
    }

    private bool OffRoute(Vector3 player)
    {
        var from = _plan![_next - 1].Position; var to = _plan[_next].Position;
        return GeometryHelper.DistanceSquaredPointToSegmentXZ(player, from, to) > OffPath * OffPath;
    }

    private bool StillSafe(HazardIndex index, IGeometryShape floor, Vector3 player, DateTime now, float speed)
    {
        // The plan already tolerates running up to Lag behind its schedule.
        // Re-check the remaining schedule as planned against the latest
        // forecasts, and replan only once the player is later than that.
        // Re-timing from "now" would demand a fresh Lag on top of the delay
        // already absorbed and drop every tight plan mid-crossing.
        var previous = _plan![_next - 1]; var upcoming = _plan[_next];
        var leg = Distance(previous.Position, upcoming.Position);
        var progress = Math.Clamp(leg - Distance(player, upcoming.Position), 0, leg);
        var scheduled = (previous.Departure - now).TotalSeconds + progress / speed;
        if (scheduled < -_profile.ExecutionLag) return false;
        if (Distance(player, previous.Position) <= ArrivalTolerance && scheduled > 0 && !index.Stand(player, 0, scheduled)) return false;
        var from = previous.Position; var time = (previous.Departure - now).TotalSeconds;
        for (var i = _next; i < _plan.Count; ++i)
        {
            var to = _plan[i].Position; var arrival = (_plan[i].Arrival - now).TotalSeconds;
            if (!index.Segment(from, to, time, arrival)) return false;
            var depart = (_plan[i].Departure - now).TotalSeconds;
            if (depart > arrival && !index.Stand(to, arrival, depart)) return false;
            time = depart; from = to;
        }
        // The stair run-up start is not a standing refuge: keep at least the
        // run-up and first flight clear (the full stair timeline is only a
        // preference when the search picked the departure).
        if (_profile.Stairs && MathF.Abs(from.Z - RunupZ) < .01f) return FirstFlightClear(index, from, time);
        // The drop trigger delivers on arrival; nobody stands there.
        if (_profile.Spoke is { } spoke && Distance(from, spoke[^1]) < 1.5f) return true;
        return index.Stand(from, time, time + StandAfterGoal);
    }

    // The bomb explodes on movement within ~0.6s of expiry; the race state
    // forces the stop 1.2s before it. Stand still from 1.3s before to 0.3s after.
    internal const double BombLead = 1.3, BombTail = .3;

    private static List<Waypoint>? Search(Profile profile, HazardIndex index, IGeometryShape floor, Vector3 player, DateTime now, float speed,
        (double From, double Until)? hold = null, CancellationToken cancellation = default)
    {
        if (profile.Spoke is { } spoke) return SearchCorridor(profile, spoke, index, floor, player, now, speed, hold, cancellation);
        // Plan onto the stair landing (z=154); below Bottom the stair jump
        // route takes over.
        var goal = MathF.Max(profile.Exit, player.Z - Horizon);
        List<Node> layer = [new(player, 0, 0, null, (int)MathF.Round(player.X))];
        // A horizon end that cannot be reached yet: advance to the deepest
        // node that stays clear for a while, then plan again from there.
        Node? refuge = null;
        var z = MathF.Floor((player.Z - .5f) / LayerStep) * LayerStep;
        for (; z >= goal - 1e-3f && layer.Count > 0; z -= LayerStep)
        {
            if (cancellation.IsCancellationRequested) return null;
            var row = z;
            var next = Expand(layer, node =>
            {
                var found = new List<((int Lane, int Bucket) Key, Node Node)>();
                foreach (var wait in Waits)
                {
                    if (wait > 0 && !index.Stand(node.Position, node.Time, node.Time + wait)) break;
                    var depart = node.Time + wait;
                    for (var lane = Math.Max(node.Lane - (int)MaxLateral, -profile.Lanes); lane <= Math.Min(node.Lane + (int)MaxLateral, profile.Lanes); ++lane)
                    {
                        var to = new Vector3(lane, node.Position.Y, row);
                        if (!Viable(profile, lane, row)) continue;
                        var arrival = depart + Distance(node.Position, to) / speed;
                        if (arrival > 30) continue;
                        if (!ArenaBoundaryTraversal.ContainsTube(floor, node.Position, to, FloorClearance)) continue;
                        if (!index.Segment(node.Position, to, depart, arrival)) continue;
                        found.Add(((lane, (int)(arrival / .2)), new(to, arrival, wait, node, lane)));
                    }
                }
                return found;
            });
            // Keep the two earliest histories per lane so a later safe window
            // survives next to the fastest one.
            layer = next.Values.GroupBy(n => n.Lane).SelectMany(g => g.OrderBy(n => n.Time).Take(2)).ToList();
            foreach (var node in layer.OrderBy(n => n.Time))
                if (profile.Rest(node.Position.Z) && index.Stand(node.Position, node.Time, node.Time + StandAfterGoal)) { refuge = node; break; }
        }
        // An end inside a forecast is a dead end once the next plan cannot
        // leave it; only an end that stays clear for a while is published.
        // Layers are visited in order, so this is the deepest such node.
        var best = refuge;
        // From the last layer, hand over at the stair run-up start only when
        // the run-up, the takeoff and the first flight stay clear (the 34796
        // circles at z=156 hit 15:10 and 19:51 replays right at the hand-over).
        if (profile.Stairs && layer.Count > 0 && layer[0].Position.Z <= Exit + .01f && StairRunup(index, floor, layer, speed) is { } runup) best = runup;
        // The run-up start is only 0.8m below z=156; a plan from there must
        // still hand over (20:42 replay idled at z=156 inside an R5 circle).
        if (best == null || player.Z - best.Position.Z < LayerStep - .01f && !(profile.Stairs && MathF.Abs(best.Position.Z - RunupZ) < .01f)) return null;
        return ToPlan(best, now);
    }

    // 23:48 ARR: this search ran on the game thread (frames up to ~70ms). Each
    // node of a layer expands on its own core; candidates merge in the
    // sequential order, so the plan is unchanged.
    private static Dictionary<(int Lane, int Bucket), Node> Expand(List<Node> layer, Func<Node, List<((int Lane, int Bucket) Key, Node Node)>> expand)
    {
        var results = new List<((int Lane, int Bucket) Key, Node Node)>[layer.Count];
        Parallel.For(0, layer.Count, i => results[i] = expand(layer[i]));
        var next = new Dictionary<(int Lane, int Bucket), Node>();
        foreach (var found in results)
            foreach (var (key, node) in found)
                if (!next.TryGetValue(key, out var old) || old.Time > node.Time) next[key] = node;
        return next;
    }

    private static List<Waypoint> ToPlan(Node best, DateTime now)
    {
        var nodes = new List<Node>();
        for (var n = best; n != null; n = n.Parent) nodes.Add(n);
        nodes.Reverse();
        var plan = new List<Waypoint>(nodes.Count);
        for (var i = 0; i < nodes.Count; ++i)
        {
            var wait = i + 1 < nodes.Count ? nodes[i + 1].Wait : 0;
            plan.Add(new(nodes[i].Position, now.AddSeconds(nodes[i].Time), now.AddSeconds(nodes[i].Time + wait)));
        }
        // Merge collinear walking points so GreenMove does not stop at each layer.
        for (var i = plan.Count - 2; i > 0; --i)
        {
            var a = plan[i].Position - plan[i - 1].Position; var b = plan[i + 1].Position - plan[i].Position; a.Y = b.Y = 0;
            if (plan[i].Departure - plan[i].Arrival < TimeSpan.FromMilliseconds(50) && a.LengthSquared() > 1e-4f && b.LengthSquared() > 1e-4f
                && Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)) > .9999f)
                plan.RemoveAt(i);
        }
        return plan;
    }

    // Four grounded hops with continuous input, 0.7s native cooldown and an
    // initial hand-over allowance. Prefer a safe complete ascent, including
    // the 29795 circles near the top. If only the first flight is known clear,
    // the runtime still validates the whole ascent before acquiring input;
    // this preference never authorizes an unsafe jump.
    internal const double StairSeconds = 3.5, FinishWalkSeconds = 2.2, FirstFlightSeconds = 1.2, StairPreference = 3;
    internal const float SweepEnd = 137;
    private const int StairWaitSteps = 60;

    internal static bool FirstFlightClear(HazardIndex index, Vector3 runup, double arrival)
        => index.Segment(runup, runup with { Z = 149 }, arrival, arrival + FirstFlightSeconds);

    private static bool StairsClear(HazardIndex index, Vector3 runup, double arrival)
    {
        var end = runup with { Z = SweepEnd };
        var duration = StairSeconds * (runup.Z - SweepEnd) / (runup.Z - 137);
        foreach (var offset in new[] { -.4f, 0, .4f })
            if (!index.Segment(runup + Vector3.UnitZ * offset, end + Vector3.UnitZ * offset, arrival, arrival + duration)) return false;
        return true;
    }

    private static Node? StairRunup(HazardIndex index, IGeometryShape floor, List<Node> layer, float speed)
    {
        Node? clear = null, quick = null;
        foreach (var node in layer)
            foreach (var side in new[] { -1f, 1f })
            {
                var runup = new Vector3(side * BlundervilleRoutes.FinalStairLane, node.Position.Y, RunupZ);
                if (!ArenaBoundaryTraversal.ContainsTube(floor, node.Position, runup, FloorClearance)) continue;
                // The full stair timeline leaves ~0.4s of slack inside the
                // 6.03s sweep gap, so its departure is searched on a 0.1s grid.
                for (var step = 0; step <= StairWaitSteps; ++step)
                {
                    var wait = step * .1;
                    if (wait > 0 && !index.Stand(node.Position, node.Time, node.Time + wait)) break;
                    var depart = node.Time + wait; var arrival = depart + Distance(node.Position, runup) / speed;
                    if (!index.Segment(node.Position, runup, depart, arrival) || !FirstFlightClear(index, runup, arrival)) continue;
                    var candidate = new Node(runup, arrival, wait, node, (int)(side * BlundervilleRoutes.FinalStairLane));
                    if (quick == null || arrival < quick.Time) quick = candidate;
                    if (StairsClear(index, runup, arrival)) { if (clear == null || arrival < clear.Time) clear = candidate; break; }
                }
            }
        return clear != null && (quick == null || clear.Time <= quick.Time + StairPreference) ? clear : quick;
    }

    // Lane cells (integer x, even z) that can still reach a stair run-up by
    // floor alone. The 17:44 ARR plan ended in the V wedge at (-1.2,206.2)
    // between two wave-zone pits; layers only advance, so it stood there and
    // a wave column hit it every 6s. The floor is static: compute once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Profile, HashSet<(int Lane, int Z)>> ViableCells = new();

    internal static bool Viable(int lane, float z) => Viable(Final, lane, z);

    private static bool Viable(Profile profile, int lane, float z)
        => z > profile.Top + .01f || ViableCells.GetOrAdd(profile, BuildViable).Contains((lane, (int)MathF.Round(z)));

    private static bool CanAdvance(Profile profile, IGeometryShape floor, Vector3 player)
    {
        if (profile.Spoke != null) return true;
        var z = MathF.Floor((player.Z - .5f) / LayerStep) * LayerStep;
        var center = (int)MathF.Round(player.X);
        for (var lane = Math.Max(center - (int)MaxLateral, -profile.Lanes); lane <= Math.Min(center + (int)MaxLateral, profile.Lanes); ++lane)
            if (Viable(profile, lane, z) && ArenaBoundaryTraversal.ContainsTube(floor, player, new(lane, player.Y, z), FloorClearance)) return true;
        return z < profile.Exit - .01f;
    }

    private static HashSet<(int Lane, int Z)> BuildViable(Profile profile)
    {
        var floor = BlundervilleRoutes.Boundary(profile.Course);
        var viable = new HashSet<(int Lane, int Z)>();
        var exit = (int)profile.Exit; var lanes = profile.Lanes;
        for (var lane = -lanes; lane <= lanes; ++lane)
        {
            var cell = new Vector3(lane, 0, exit);
            if (profile.Stairs
                ? new[] { -1f, 1f }.Any(side => ArenaBoundaryTraversal.ContainsTube(floor, cell, new(side * BlundervilleRoutes.FinalStairLane, 0, RunupZ), FloorClearance))
                : ArenaBoundaryTraversal.ContainsTube(floor, cell, cell - Vector3.UnitZ * .01f, FloorClearance))
                viable.Add((lane, exit));
        }
        for (var z = exit + (int)LayerStep; z <= profile.Top; z += (int)LayerStep)
            for (var lane = -lanes; lane <= lanes; ++lane)
                for (var next = Math.Max(lane - (int)MaxLateral, -lanes); next <= Math.Min(lane + (int)MaxLateral, lanes); ++next)
                    if (viable.Contains((next, z - (int)LayerStep))
                        && ArenaBoundaryTraversal.ContainsTube(floor, new(lane, 0, z), new(next, 0, z - LayerStep), FloorClearance))
                    { viable.Add((lane, z)); break; }
        return viable;
    }

    internal sealed class HazardIndex
    {
        private readonly Dictionary<int, List<Window>> _rows = [];
        private readonly float _clearance, _standClearance;
        private readonly double _lag;
        private readonly BlundervilleCourse _profileCourse;
        private HazardIndex(HazardIndex source, double lag)
        {
            _rows = source._rows;
            _clearance = source._clearance;
            _standClearance = source._standClearance;
            _profileCourse = source._profileCourse;
            _lag = lag;
        }

        internal HazardIndex WithExecutionLag(double lag) => Math.Abs(lag - _lag) < 1e-9 ? this : new(this, lag);

        public HazardIndex(IReadOnlyList<BlundervilleHazard> hazards, DateTime now, Profile profile)
        {
            _clearance = profile.HazardClearance; _lag = profile.ExecutionLag; _standClearance = profile.StandClearance;
            _profileCourse = profile.Course;
            foreach (var h in hazards)
            {
                var wave = h.Id.StartsWith("wave/", StringComparison.Ordinal) || h.Id.StartsWith("cast/", StringComparison.Ordinal);
                // The Delivery schedule windows already carry their calibrated +-0.45s.
                var calibrated = h.Id.StartsWith("delivery/", StringComparison.Ordinal);
                var window = new Window(h.Geometry, h.Geometry.GetBounds(), (h.From - now).TotalSeconds - (calibrated ? 0 : wave ? profile.HazardWaveLead : profile.HazardLead),
                    (h.Until - now).TotalSeconds + (calibrated ? 0 : wave ? profile.HazardWaveTail : Tail), h.Id.StartsWith("delivery/wind/", StringComparison.Ordinal));
                if (window.Until < 0 || !float.IsFinite(window.Bounds.MinZ) || !float.IsFinite(window.Bounds.MaxZ)) continue;
                for (var row = (int)MathF.Floor((window.Bounds.MinZ - 1) / 2); row <= (int)MathF.Floor((window.Bounds.MaxZ + 1) / 2); ++row)
                {
                    if (!_rows.TryGetValue(row, out var list)) _rows[row] = list = [];
                    list.Add(window);
                }
            }
        }

        public bool Stand(Vector3 p, double from, double until)
        {
            if (!_rows.TryGetValue((int)MathF.Floor(p.Z / 2), out var list)) return true;
            foreach (var w in list)
                // Wind pushes toward the barrier. Its landing is at the back
                // of the pocket; growing exposed floor into that wall contact
                // makes the next search unable even to stand at its start.
                if (w.Until >= from && w.From <= until + _lag && w.Geometry.Contains(p, w.Wind ? 0 : _standClearance)) return false;
            return true;
        }

        internal readonly record struct TimeRange(double From, double Until);
        internal const double WindowGap = .001;
        private readonly ConcurrentDictionary<(Vector3, Vector3, double), (double From, double Until)[]> _departures = new();
        private readonly ConcurrentDictionary<(Vector3 Point, double Horizon), TimeRange[]> _standing = new();

        internal (double From, double Until)[] BlockedDepartures(Vector3 from, Vector3 to, double duration)
        {
            if (_departures.TryGetValue((from, to, duration), out var cached)) return cached;
            var blocked = new List<(double From, double Until)>();
            var seen = new HashSet<Window>();
            for (var row = (int)MathF.Floor(MathF.Min(from.Z, to.Z) / 2); row <= (int)MathF.Floor(MathF.Max(from.Z, to.Z) / 2); ++row)
            {
                if (!_rows.TryGetValue(row, out var list)) continue;
                foreach (var w in list)
                {
                    if (!seen.Add(w)) continue;
                    (double From, double Until)? contact;
                    // Delivery's wind/barrier pockets and charge schedule are
                    // calibrated against complete exposure on non-box legs.
                    if (_profileCourse == BlundervilleCourse.Delivery && w.Geometry is not RectangleGeometry)
                        contact = w.Geometry.IntersectsSegment(from, to, _clearance) ? (0, 1) : null;
                    else
                        contact = BlundervilleSegmentContacts.Find(w.Geometry, from, to, _clearance);
                    if (contact is { } occupied)
                        blocked.Add((w.From - duration * occupied.Until - _lag, w.Until - duration * occupied.From));
                }
            }
            var merged = new List<(double From, double Until)>();
            foreach (var window in blocked.OrderBy(w => w.From))
                if (merged.Count > 0 && window.From <= merged[^1].Until)
                    merged[^1] = (merged[^1].From, Math.Max(merged[^1].Until, window.Until));
                else merged.Add(window);
            return _departures[(from, to, duration)] = merged.ToArray();
        }

        public double Departure(Vector3 from, Vector3 to, double earliest, double duration, double limit)
            => Earliest(BlockedDepartures(from, to, duration), earliest, limit);

        internal IEnumerable<TimeRange> DepartureRanges(Vector3 from, Vector3 to, double duration, double first, double last)
            => ClearRanges(TraversalBlockedDepartures(from, to, duration), first, last);

        private readonly ConcurrentDictionary<(Vector3, Vector3, double), (double From, double Until)[]> _sampledDepartures = new();
        internal (double From, double Until)[] TraversalBlockedDepartures(Vector3 from, Vector3 to, double duration)
        {
            if (_profileCourse != BlundervilleCourse.Final) return BlockedDepartures(from, to, duration);
            var key = (from, to, duration);
            if (_sampledDepartures.TryGetValue(key, out var cached)) return cached;
            // Final movement retains its calibrated spatial/clock envelope.
            // The graph searches its continuous openings without time bins.
            var samples = Math.Max(2, (int)MathF.Ceiling(Distance(from, to) / .5f) + 1);
            var half = duration / (samples - 1) / 2;
            var blocked = new List<(double From, double Until)>();
            for (var i = 0; i < samples; ++i)
            {
                var fraction = i / (float)(samples - 1);
                var point = Vector3.Lerp(from, to, fraction);
                if (!_rows.TryGetValue((int)MathF.Floor(point.Z / 2), out var windows)) continue;
                foreach (var window in windows)
                    if (window.Geometry.Contains(point, _clearance))
                        blocked.Add((window.From - fraction * duration - _lag - half, window.Until - fraction * duration + half));
            }
            var merged = new List<(double From, double Until)>();
            foreach (var window in blocked.OrderBy(w => w.From))
                if (merged.Count > 0 && window.From <= merged[^1].Until)
                    merged[^1] = (merged[^1].From, Math.Max(merged[^1].Until, window.Until));
                else merged.Add(window);
            return _sampledDepartures[key] = merged.ToArray();
        }

        internal TimeRange[] StandingRanges(Vector3 point, double horizon)
        {
            if (_standing.TryGetValue((point, horizon), out var ranges)) return ranges;
            var blocked = !_rows.TryGetValue((int)MathF.Floor(point.Z / 2), out var windows)
                ? [] : windows.Where(w => w.Geometry.Contains(point, w.Wind ? 0 : _standClearance))
                    .Select(w => (From: w.From - _lag, w.Until)).OrderBy(w => w.From).ToArray();
            return _standing[(point, horizon)] = ClearRanges(blocked, 0, horizon).ToArray();
        }

        internal static IEnumerable<TimeRange> ClearRanges(IEnumerable<(double From, double Until)> blocked, double first, double last, double gap = WindowGap)
        {
            foreach (var window in blocked)
            {
                if (window.Until < first) continue;
                if (window.From > last) break;
                var end = Math.Min(last, window.From - gap);
                if (end >= first) yield return new(first, end);
                first = Math.Max(first, window.Until + gap);
                if (first > last) yield break;
            }
            if (first <= last) yield return new(first, last);
        }

        private static double Earliest((double From, double Until)[] blocked, double earliest, double limit)
        {
            var departure = earliest;
            foreach (var w in blocked)
                if (w.Until >= departure && w.From <= departure) departure = w.Until + .01;
            return departure <= limit ? departure : double.PositiveInfinity;
        }

        public double SafeInterval(Vector3 p, double arrival)
            => !_rows.TryGetValue((int)MathF.Floor(p.Z / 2), out var list) ? double.PositiveInfinity
                : list.Where(w => w.Until >= arrival && w.Geometry.Contains(p, _clearance))
                    .Select(w => w.From).DefaultIfEmpty(double.PositiveInfinity).Min();

        internal bool CachedSegment(Vector3 from, Vector3 to, double start, double duration)
        {
            var intervals = TraversalBlockedDepartures(from, to, duration);
            var lo = 0; var hi = intervals.Length;
            while (lo < hi) { var mid = (lo + hi) / 2; if (intervals[mid].Until < start) lo = mid + 1; else hi = mid; }
            return lo == intervals.Length || intervals[lo].From > start;
        }

        public bool Segment(Vector3 from, Vector3 to, double start, double end)
            => end >= start && CachedSegment(from, to, start, end - start);
    }

    private static List<Waypoint>? SearchCorridor(Profile profile, Vector3[] spoke, HazardIndex index, IGeometryShape floor, Vector3 player, DateTime now, float speed,
        (double From, double Until)? hold, CancellationToken cancellation)
    {
        if (profile.Course == BlundervilleCourse.Delivery)
            return BlundervilleDeliverySearch.Find(profile, index, floor, player, now, speed, cancellation);
        var stations = Stations(spoke, profile.Colour == 37);
        var (start, across, _, _) = Along(spoke, player);
        var width = (int)MathF.Round(profile.Lanes / profile.LaneWidth); var reach = (int)MathF.Round(MaxLateral / profile.LaneWidth);
        List<Node> layer = [new(player, 0, 0, null, Math.Clamp((int)MathF.Round(across / profile.LaneWidth), -width, width))];
        Node? refuge = null, goal = null;
        var from = stations.FindIndex(station => station.S > start + .5f);
        if (from < 0 && start < stations[^1].S - .05f) from = stations.Count - 1;
        for (var k = from; k >= 0 && k < stations.Count && layer.Count > 0; ++k)
        {
            if (cancellation.IsCancellationRequested) return null;
            var (_, point, lateral) = stations[k];
            var last = k == stations.Count - 1;
            var lanes = last ? 0 : width;
            var next = Expand(layer, node =>
            {
                var found = new List<((int Lane, int Bucket) Key, Node Node)>();
                foreach (var wait in CorridorWaits)
                {
                    if (wait > 0 && !index.Stand(node.Position, node.Time, node.Time + wait + profile.WaitSlip)) break;
                    var depart = node.Time + wait;
                    for (var lane = Math.Max(node.Lane - reach, -lanes); lane <= Math.Min(node.Lane + reach, lanes); ++lane)
                    {
                        var to = point + lateral * (lane * profile.LaneWidth);
                        var arrival = depart + Distance(node.Position, to) / speed;
                        if (arrival > 30) continue;
                        // No movement during a bomb stand-still.
                        if (hold is { } h && depart < h.Until && arrival > h.From) continue;
                        if (!ArenaBoundaryTraversal.ContainsTube(floor, node.Position, to, FloorClearance)) continue;
                        if (!index.Segment(node.Position, to, depart, arrival)) continue;
                        found.Add(((lane, (int)(arrival / .2)), new(to, arrival, wait, node, lane)));
                    }
                }
                return found;
            });
            layer = next.Values.GroupBy(n => n.Lane).SelectMany(g => g.OrderBy(n => n.Time).Take(2)).ToList();
            foreach (var node in layer.OrderBy(n => n.Time))
                if (index.Stand(node.Position, node.Time, node.Time + StandAfterGoal)) { refuge = node; break; }
            if (last && layer.Count > 0) goal = layer.MinBy(n => n.Time);
        }
        var best = goal ?? refuge;
        return best?.Parent == null ? null : ToPlan(best, now);
    }

    internal static List<(float S, Vector3 Point, Vector3 Lateral)> Stations(Vector3[] spoke, bool includeCorners = false)
    {
        var result = new List<(float, Vector3, Vector3)>();
        var walked = 0f;
        for (var i = 0; i + 1 < spoke.Length; ++i)
        {
            var (direction, length) = Heading(spoke[i], spoke[i + 1]);
            var lateral = new Vector3(-direction.Z, 0, direction.X);
            // Wind shelters sit at these corners; a 2m grid can skip their
            // entire standing pocket and leave no place to receive the push.
            if (includeCorners && i > 0 && (result.Count == 0 || walked - result[^1].Item1 > .05f))
                result.Add((walked, spoke[i], lateral));
            // Heights follow the spoke (the FirstA ramp climbs 6m).
            for (var s = MathF.Ceiling(walked / LayerStep) * LayerStep; s < walked + length - .5f; s += LayerStep)
                result.Add((s, Vector3.Lerp(spoke[i], spoke[i + 1], (s - walked) / length), lateral));
            walked += length;
        }
        var (tail, _) = Heading(spoke[^2], spoke[^1]);
        result.Add((walked, spoke[^1], new Vector3(-tail.Z, 0, tail.X)));
        return result;
    }

    // Arc length and signed lateral offset of the nearest corridor point.
    internal static (float S, float U, float Distance, float Length) Along(Vector3[] spoke, Vector3 p)
    {
        float walked = 0, bestS = 0, bestU = 0, best = float.PositiveInfinity;
        for (var i = 0; i + 1 < spoke.Length; ++i)
        {
            var (direction, length) = Heading(spoke[i], spoke[i + 1]);
            var offset = (p - spoke[i]) with { Y = 0 };
            var along = Math.Clamp(Vector3.Dot(offset, direction), 0, length);
            var across = offset - direction * along;
            if (across.Length() < best)
            { best = across.Length(); bestS = walked + along; bestU = Vector3.Dot(across, new Vector3(-direction.Z, 0, direction.X)); }
            walked += length;
        }
        return (bestS, bestU, best, walked);
    }

    private static (Vector3 Direction, float Length) Heading(Vector3 a, Vector3 b)
    {
        var delta = (b - a) with { Y = 0 };
        return (delta / delta.Length(), delta.Length());
    }

    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}

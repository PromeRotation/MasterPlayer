using System.Numerics;
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
    internal const float Top = 286, Bottom = 155.4f, Exit = 156, RunupZ = 155.2f, LayerStep = 2, MaxLateral = 2, Clearance = .3f, FloorClearance = .25f;
    internal const float Horizon = 60, ArrivalTolerance = .35f, OffPath = 1f;
    // |x|=16 is a 0.55m strip behind the z=178–182 pillars that dead-ends
    // (2026.10.07 19:51 replay side -1 idled there for 20s).
    private const int MaxLane = 15;
    // ARR 2026-10-12 18:27 FirstB: the generic solver on the 3703 ramp walked
    // back 5m three times below the first pusher row (z -347.8), then side-
    // stepped into the top row and was pushed down twice. The ramp has the
    // same shape as the Final slope (timed rows, uphill = -Z), so the same
    // layered lane search runs there: x -6.5..6.5, goal row -368, then the
    // ordinary route takes the flat finish. Speed is the measured 2.0m/s.
    internal sealed record Profile(BlundervilleCourse Course, float Top, float Bottom, float Exit, int Lanes, float MinY, float? Speed, bool Stairs, string Name,
        (float From, float To)[]? Strips = null)
    {
        // An intermediate plan may only stop where standing is not a gamble.
        public bool Rest(float z) => Strips == null || Strips.Any(s => z >= s.From && z <= s.To);
    }
    // Hazard-free strips of the Final (32 ARRs): above 272.5, 257–262.5,
    // 235.3–237 and 180.7–187.4. 2026-10-13: with the corrected wave boxes a
    // plan ending mid wave zone (z 206 north of the pit) found no way on for
    // 27s; humans cross each zone in one go and only wait on these strips.
    internal static readonly Profile Final = new(BlundervilleCourse.Final, Top, Bottom, Exit, MaxLane, 2, null, true, "决赛",
        [(272.5f, 290), (257, 262.5f), (235.3f, 237), (180.7f, 187.4f)]);
    internal static readonly Profile FirstBRamp = new(BlundervilleCourse.FirstB, -336, -367.5f, -368, 6, 9.5f,
        BlundervilleRaceState.SlidingSlopeSpeed, false, "终点坡");
    private readonly Profile _profile;
    public BlundervilleFinalPlanner(Profile? profile = null) => _profile = profile ?? Final;
    internal const double Lag = .35, StandAfterGoal = 2.5, Lead = .15, Tail = .1, WaveLead = .4, WaveTail = .3;
    private static readonly double[] Waits = [0, .5, 1, 1.5, 2, 3];

    internal sealed record Waypoint(Vector3 Position, DateTime Arrival, DateTime Departure);
    private sealed record Node(Vector3 Position, double Time, double Wait, Node? Parent, int Lane);
    private readonly record struct Window(IGeometryShape Geometry, GeometryBounds Bounds, double From, double Until);

    private List<Waypoint>? _plan;
    private int _next, _stretchStart;
    private Vector3[]? _stretch;
    private DateTime _lastTime, _retry;

    public IReadOnlyList<Waypoint>? Plan => _plan;

    public void Reset() { _plan = null; _next = 0; _stretch = null; _lastTime = _retry = default; }

    public bool Active(BlundervilleRaceState state, Vector3 player, DateTime now)
        => state.IsRacing && !state.Qualified && state.Course == _profile.Course && state.CanMove(now)
            && player.Z > _profile.Bottom && player.Z <= _profile.Top && player.Y > _profile.MinY && MathF.Abs(player.X) <= _profile.Lanes + 2;

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now, DirectTraversalExecution? execution = null)
    {
        if (now < _lastTime) Reset();
        _lastTime = now;
        // Other courses' planners (bumper, ice, arrow bridge) publish their own
        // direct traversals; clearing that flag here stalled FirstA in the
        // 2026.10.12 16:55 ARR (1.14.0). Only ever pass them through.
        if (!Active(state, player, now) || normal.Hold) { Reset(); return normal; }
        var floor = BlundervilleRoutes.Boundary(state.Course);
        var speed = _profile.Speed ?? state.NavigationBaseSpeed(now);
        var index = new HazardIndex(hazards, now);
        if (_plan != null)
        {
            while (_next < _plan.Count && Distance(player, _plan[_next].Position) <= ArrivalTolerance) ++_next;
            if (_next >= _plan.Count || execution is { Rejected: true } || OffRoute(player) || !StillSafe(index, floor, player, now, speed))
                _plan = null;
        }
        if (_plan == null)
        {
            if (now < _retry) return Fallback(index, floor, hazards, player, now);
            _plan = Search(_profile, index, floor, player, now, speed);
            _next = 1; _stretch = null;
            if (_plan is not { Count: >= 2 })
            {
                _plan = null; _retry = now.AddSeconds(.2);
                // In a dead end no forward layer is reachable by floor; hand
                // back to the race route, whose solver can walk back out.
                if (!CanAdvance(_profile, floor, player)) return normal with { Hint = $"{_profile.Name}：退出死角，改走常规路线" };
                return Fallback(index, floor, hazards, player, now);
            }
        }
        var current = _plan[_next - 1];
        if (now < current.Departure && Distance(player, current.Position) <= ArrivalTolerance)
        {
            _stretch = null;
            return new(player, true, false, null, $"{_profile.Name}：在安全处等待机关间隙", false);
        }
        // Publish one unchanged stretch up to the next wait; the runtime
        // follows GreenMove progress, so the path must not shrink per point.
        if (_stretch == null || _next >= _stretchStart + _stretch.Length)
        {
            var end = _next;
            while (end < _plan.Count - 1 && _plan[end].Departure - _plan[end].Arrival < TimeSpan.FromMilliseconds(50)) ++end;
            _stretchStart = _next;
            _stretch = _plan.Skip(_next).Take(end - _next + 1).Select(w => w.Position).ToArray();
        }
        return new(_stretch[^1], false, false, null, $"{_profile.Name}：按机关时序连续穿行", true) { ContinuousPath = _stretch };
    }

    // Inside the Final hazard slope the ordinary solver re-creates the
    // stop-and-go: hold where standing stays clear, otherwise step aside.
    private BlundervilleRaceDecision Fallback(HazardIndex index, IGeometryShape floor, IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now)
    {
        _stretch = null;
        if (index.Stand(player, 0, 1.5)) return new(player, true, false, null, $"{_profile.Name}：等待可通行的机关时序", false);
        if (BlundervilleRefuge.Find(floor, hazards, player, now) is { } refuge)
            return new(refuge, false, false, null, $"{_profile.Name}：先避开即将判定的机关", true) { ContinuousPath = [refuge] };
        return new(player, true, false, null, $"{_profile.Name}：无可行避让，原地等待", false);
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
        if (scheduled < -Lag) return false;
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
        return index.Stand(from, time, time + StandAfterGoal);
    }

    private static List<Waypoint>? Search(Profile profile, HazardIndex index, IGeometryShape floor, Vector3 player, DateTime now, float speed)
    {
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
            var next = new Dictionary<(int Lane, int Bucket), Node>();
            foreach (var node in layer)
                foreach (var wait in Waits)
                {
                    if (wait > 0 && !index.Stand(node.Position, node.Time, node.Time + wait)) break;
                    var depart = node.Time + wait;
                    for (var lane = Math.Max(node.Lane - (int)MaxLateral, -profile.Lanes); lane <= Math.Min(node.Lane + (int)MaxLateral, profile.Lanes); ++lane)
                    {
                        var to = new Vector3(lane, node.Position.Y, z);
                        if (!Viable(profile, lane, z)) continue;
                        var arrival = depart + Distance(node.Position, to) / speed;
                        if (arrival > 30) continue;
                        if (!ArenaBoundaryTraversal.ContainsTube(floor, node.Position, to, FloorClearance)) continue;
                        if (!index.Segment(node.Position, to, depart, arrival)) continue;
                        var key = (lane, (int)(arrival / .2));
                        if (!next.TryGetValue(key, out var old) || old.Time > arrival)
                            next[key] = new(to, arrival, wait, node, lane);
                    }
                }
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

    // The stair executor ignores hazards: each landing is the next takeoff, so
    // it steps back ~1.15m for a run-up (~1.4s per step, 155.2→137 in ~5.5s).
    // Prefer a departure that crosses the 34801 sweep row over z≈140–152 (one
    // 6.03s cycle box per stair lane) inside its gap, padded 1.2m along the
    // stairs. The alternating 29795 circles over z≈132–140 repeat every 2.54s
    // and cannot be timed from here. When no departure within StairPreference
    // clears the sweep, only the run-up and first flight (~1.6s) must stay
    // clear; otherwise the run stalls at z=156 inside the 34796 circles
    // (2026.10.08 23:46 and 2026.10.07 20:42 replays).
    internal const double StairSeconds = 5.5, FinishWalkSeconds = 2.2, FirstFlightSeconds = 1.6, StairPreference = 3;
    internal const float SweepEnd = 140;
    private const int StairWaitSteps = 60;

    private static bool FirstFlightClear(HazardIndex index, Vector3 runup, double arrival)
        => index.Segment(runup, runup with { Z = 149 }, arrival, arrival + FirstFlightSeconds);

    private static bool StairsClear(HazardIndex index, Vector3 runup, double arrival)
    {
        var end = runup with { Z = SweepEnd };
        var duration = StairSeconds * (runup.Z - SweepEnd) / (runup.Z - 137);
        foreach (var offset in new[] { -1.2f, 0, 1.2f })
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

    private sealed class HazardIndex
    {
        private readonly Dictionary<int, List<Window>> _rows = [];
        public HazardIndex(IReadOnlyList<BlundervilleHazard> hazards, DateTime now)
        {
            foreach (var h in hazards)
            {
                var wave = h.Id.StartsWith("wave/", StringComparison.Ordinal) || h.Id.StartsWith("cast/", StringComparison.Ordinal);
                var window = new Window(h.Geometry, h.Geometry.GetBounds(), (h.From - now).TotalSeconds - (wave ? WaveLead : Lead),
                    (h.Until - now).TotalSeconds + (wave ? WaveTail : Tail));
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
                if (w.Until >= from && w.From <= until + Lag && w.Geometry.Contains(p, Clearance)) return false;
            return true;
        }

        // Executed somewhere between the plan and Lag seconds later.
        public bool Segment(Vector3 from, Vector3 to, double start, double end)
        {
            var samples = Math.Max(2, (int)MathF.Ceiling(Distance(from, to) / .5f) + 1);
            for (var i = 0; i < samples; ++i)
            {
                var s = i / (float)(samples - 1);
                var p = Vector3.Lerp(from, to, s);
                var at = start + s * (end - start);
                if (!_rows.TryGetValue((int)MathF.Floor(p.Z / 2), out var list)) continue;
                foreach (var w in list)
                    if (w.Until >= at && w.From <= at + Lag && w.Geometry.Contains(p, Clearance)) return false;
            }
            return true;
        }
    }

    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}

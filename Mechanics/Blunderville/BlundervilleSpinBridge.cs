using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// FirstB spin bridge (floor z -270.5..-334.75, 3702 from ~-271 to ~-332.5).
// 2026-10-13 13:20 ARR: 3702 keeps the character moving at 3.6m/s along its
// facing, which turns toward the input direction at ~40°/s (a 5.2m turning
// radius); it never stops on the bridge, input or not. The timing-free weave
// of 1.14.9 asked for 5m side steps within 2.4m, fell 2–6m behind them into
// the left rows and was knocked off twice (eliminated); in 12 of 18 FirstB
// ARRs MP was knocked off this bridge 1–6 times.
//
// Once on the bridge the timing is fixed: weaving in the 10m between row
// pairs buys under 0.1s at that radius. So the plan is a lane per row pair
// (held through both rows 7m apart, changed in the 13m between pairs) and an
// entry time picked while still standing at the gate: a DP over the blade
// clock (BlundervilleSpinSchedule) on 0.05s bins, turned into a trajectory by
// simulating the steering itself and checked against every impact. In 3 of
// 18 rounds no straight lane is ever clear at the wide margins.
internal sealed class BlundervilleSpinBridge
{
    internal const float Speed = 3.6f, Entry = -270.5f, GateZ = -269.5f, Exit = -333.5f, End = -336;
    internal const double Step = .1, Tick = .05, Lookahead = .4, MaxWait = 9, Horizon = 40;
    internal static readonly float Turn = 36 * MathF.PI / 180 * (float)Step;
    private static readonly float MaxSteer = 60 * MathF.PI / 180;
    private const float Reach = 3, WalkSpeed = 6;
    // Each pair: the right row (boxes at x 0.5/3.5/6.5), the left row 7m on.
    internal static readonly float[] Pairs = [-281, -301, -321];
    private static readonly float[] Rows = [-281, -288, -301, -308, -321, -328];
    private const float PairGap = 7, PairStep = 20;
    internal static readonly float[] Grid = [.. Enumerable.Range(-11, 23).Select(i => i * .5f)];

    internal readonly record struct Margins(double Before, double After, float Side, float Along);
    internal static readonly Margins Wide = new(.2, .15, .35f, .15f), Narrow = new(.1, .08, .15f, .05f), Minimal = new(.03, .03, 0, 0);
    // Erosion: the whole plan also clears shifted by this many 0.05s ticks
    // (entry and steering timing). Prediction error is ~0.05s (2 sd).
    internal readonly record struct Tier(Margins Margins, int Erosion);
    internal static readonly Tier[] Tiers = [new(Wide, 2), new(Wide, 0), new(Narrow, 1), new(Narrow, 0), new(Minimal, 0)];

    internal readonly record struct Point(double T, float X, float Z, float Heading);
    // Path times are seconds after the planning instant.
    internal sealed record Plan(Point[] Path, float[] Lanes, int Tier, double Wait);

    private Vector3? _previous;
    private Plan? _plan;
    private DateTime _planned;
    public Plan? Current => _plan;

    public void Reset() { _plan = null; _previous = null; }

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state, BlundervilleSpinSchedule blades,
        Vector3 player, float? facing, DateTime now, bool recovering)
    {
        var previous = _previous; _previous = player;
        if (state.Course != BlundervilleCourse.FirstB || !state.IsRacing || state.Qualified || normal.Hold || recovering
            || player.Y < 9.5f || player.Z > -255 || player.Z < End + 1 || MathF.Abs(player.X) > 8 || !blades.Ready(now))
        { _plan = null; return normal; }
        // Past the last row: run out straight (no blade reaches z < -329.5).
        if (player.Z <= Exit)
        { _plan = null; return Traverse(normal, [new(Math.Clamp(player.X, -4, 4), 10.03f, End)], "旋转桥：出桥"); }
        var heading = Heading(facing, player, previous);
        var impacts = blades.Impacts(now.AddSeconds(-5), now.AddSeconds(Horizon));
        var at = (now - _planned).TotalSeconds;
        if (player.Z > Entry && !state.IsSpinning)
        {
            // A departure is kept until the bridge: deciding again every scan
            // turns each go into a stop at the next blocked slot.
            if (_plan is { Wait: <= 0 } departing && at <= departing.Path[0].T + .8)
                return Traverse(normal, Approach(departing, player, at), "旋转桥：按扇叶节奏入桥");
            // A waiting plan holds for a second (re-checked); the wait left is
            // its entry time less the walk still ahead, so walking to the gate
            // does not count as waiting.
            if (_plan is { Wait: > 0 } waiting && at < 1
                && Verify(waiting.Path, impacts, Times(impacts, now), Tiers[waiting.Tier].Margins, at))
            {
                var left = waiting.Path[0].T - at - Walk(player, waiting.Lanes[0]) - .1;
                if (left > Step / 2)
                    return Hold(normal, new(waiting.Lanes[0], 10.03f, Math.Min(player.Z, GateZ)), $"旋转桥：桥头等待{left:F1}s后入桥（第{waiting.Tier + 1}档）");
                if (left > -.15)
                {
                    _plan = waiting with { Wait = 0 };
                    return Traverse(normal, Approach(_plan, player, at), "旋转桥：按扇叶节奏入桥");
                }
            }
            _plan = Gate(impacts, now, player);
            _planned = now;
            if (_plan is not { } plan)
                return Hold(normal, new(Math.Clamp(player.X, -5, 5), 10.03f, Math.Min(player.Z, GateZ)), "旋转桥：桥头等待扇叶节奏");
            if (plan.Wait > Step / 2)
                return Hold(normal, new(plan.Lanes[0], 10.03f, Math.Min(player.Z, GateZ)), $"旋转桥：桥头等待{plan.Wait:F1}s后入桥（第{plan.Tier + 1}档）");
            _plan = plan with { Wait = 0 };
            return Traverse(normal, Approach(_plan, player, 0), "旋转桥：按扇叶节奏入桥");
        }
        // Replanned from the actual position and facing every scan (speed and
        // steering drift go straight into the lane choice); the previous lanes
        // win ties, so the route does not flip between equal schedules.
        _plan = Bridge(impacts, now, player, heading, _plan?.Lanes);
        _planned = now;
        if (_plan is { } fresh) return Traverse(normal, Publish(fresh, Lookahead), Describe(fresh, 0));
        // No clear timing at all: the next row's free side is safe whatever the
        // clock (right rows never reach x < -1, left rows never x > 1).
        var row = Array.FindIndex(Rows, z => player.Z > z - BlundervilleSpinSchedule.Half - Wide.Along);
        var side = row < 0 ? Math.Clamp(player.X, -4, 4) : row % 2 == 0 ? -2.5f : 2.5f;
        var escape = Steer(player.X, player.Z, heading, 0, [side, side, side], BlundervilleSpinSchedule.Half + Wide.Along);
        if (escape != null)
            return Traverse(normal, Publish(new(escape, [side, side, side], Tiers.Length - 1, 0), Lookahead), "旋转桥：没有安全节奏，躲到下一排的空侧");
        var ahead = player + new Vector3(MathF.Sin(heading), 0, -MathF.Cos(heading)) * 2;
        return Traverse(normal, [ahead with { Y = 10.03f }, new(Math.Clamp(ahead.X, -4, 4), 10.03f, End)], "旋转桥：没有安全节奏，保持方向");
    }

    // 3702 moves along the facing (game rotation 0 = +z); a plan heading is 0
    // down the bridge (-z), positive toward +x.
    internal static float Heading(float? facing, Vector3 player, Vector3? previous)
    {
        if (facing is { } r && float.IsFinite(r)) return Normalize(MathF.PI - r);
        if (previous is { } p && Vector2.DistanceSquared(new(p.X, p.Z), new(player.X, player.Z)) > .01f)
            return MathF.Atan2(player.X - p.X, p.Z - player.Z);
        return 0;
    }

    // Walking time to the bridge entry: along to the lane at the gate line, then 1m on.
    private static double Walk(Vector3 player, float lane)
    {
        var gateZ = Math.Min(player.Z, GateZ);
        return (Vector2.Distance(new(player.X, player.Z), new(lane, gateZ)) + gateZ - Entry) / WalkSpeed;
    }

    // Before the bridge: walk to the lane at the gate line, wait, enter straight.
    internal static Plan? Gate(IReadOnlyList<BlundervilleSpinImpact> impacts, DateTime now, Vector3 player)
    {
        var times = Times(impacts, now);
        var walk = new double[Grid.Length];
        for (var i = 0; i < Grid.Length; ++i) walk[i] = Walk(player, Grid[i]);
        for (var tier = 0; tier < Tiers.Length - 1; ++tier)
        {
            var half = BlundervilleSpinSchedule.Half + Tiers[tier].Margins.Along;
            var safe = SafeSets(impacts, times, Tiers[tier], 0);
            var reach = new ulong[Pairs.Length][][];
            reach[0] = new ulong[Grid.Length][];
            for (var i = 0; i < Grid.Length; ++i)
            {
                // Walking straight on at the earliest; any later entry waits at the gate.
                var earliest = walk[i] + (Entry - Pairs[0]) / Speed;
                reach[0][i] = Window(Bin(earliest, true), Bin(earliest + MaxWait + .1, false));
                And(reach[0][i], safe[0][i]);
            }
            Propagate(reach, safe, 0);
            foreach (var (lanes, first) in Candidates(reach, safe, 0))
            {
                var entry = Time(first) - (Entry - Pairs[0]) / Speed;
                // Starting again after standing takes ~0.1s: go that much earlier.
                var wait = entry - walk[Array.IndexOf(Grid, lanes[0])] - .1;
                var path = Steer(lanes[0], Entry, 0, entry, lanes, half);
                if (path != null && Verify(path, impacts, times, Tiers[tier].Margins, 0))
                    return new(path, lanes, tier, Math.Max(0, wait));
            }
        }
        return null;
    }

    // On the bridge: the current pair's lane follows from where the steering
    // can still get, then the DP for the pairs after it.
    internal static Plan? Bridge(IReadOnlyList<BlundervilleSpinImpact> impacts, DateTime now, Vector3 player, float heading, float[]? preferred = null)
    {
        var times = Times(impacts, now);
        for (var tier = 0; tier < Tiers.Length; ++tier)
        {
            var margins = Tiers[tier].Margins;
            var half = BlundervilleSpinSchedule.Half + margins.Along;
            var current = Array.FindIndex(Pairs, z => player.Z > z - PairGap - half);
            if (current < 0)
            {
                var lane = Math.Clamp(player.X, -5.5f, 5.5f);
                var run = Steer(player.X, player.Z, heading, 0, [lane, lane, lane], half);
                if (run != null) return new(run, [lane, lane, lane], tier, 0);
                continue;
            }
            if (preferred != null)
            {
                var kept = Steer(player.X, player.Z, heading, 0, preferred, half);
                if (kept != null && Verify(kept, impacts, times, margins, 0)) return new(kept, preferred, tier, 0);
            }
            var safe = SafeSets(impacts, times, Tiers[tier], current + 1);
            var reach = new ulong[Pairs.Length][][];
            reach[current] = new ulong[Grid.Length][];
            var bandEnd = Pairs[current] - PairGap - half;
            for (var i = 0; i < Grid.Length; ++i)
            {
                reach[current][i] = new ulong[Words];
                var lanes = new[] { Grid[i], Grid[i], Grid[i] };
                var path = Steer(player.X, player.Z, heading, 0, lanes, half, bandEnd - Speed * (float)Step);
                if (path == null) continue;
                var end = Array.FindIndex(path, p => p.Z <= bandEnd);
                if (end < 0 || MathF.Abs(path[end].X - Grid[i]) > .25f
                    || !Verify(path[..(end + 1)], impacts, times, margins, 0)) continue;
                // The time at the pair's first row, virtual if already behind.
                var row = Array.FindIndex(path, p => p.Z <= Pairs[current]);
                var t = row > 0 ? path[row].T - (Pairs[current] - path[row].Z) / Speed : -(Pairs[current] - player.Z) / Speed;
                Set(reach[current][i], Bin(t, false));
            }
            Propagate(reach, safe, current);
            foreach (var (lanes, _) in Candidates(reach, safe, current))
            {
                var path = Steer(player.X, player.Z, heading, 0, lanes, half);
                if (path != null && Verify(path, impacts, times, margins, 0)) return new(path, lanes, tier, 0);
            }
        }
        return null;
    }

    // Lane changes between pairs: the steering below from straight in one lane
    // to straight in the other within the 9.7m between the bands (widest
    // margins), and the time it adds.
    private static readonly (bool Ok, double Extra)[] Changes = BuildChanges();

    private static (bool Ok, double Extra)[] BuildChanges()
    {
        var gap = PairStep - PairGap - 2 * (BlundervilleSpinSchedule.Half + Wide.Along);
        var result = new (bool, double)[Grid.Length];
        for (var k = 0; k < result.Length; ++k)
        {
            var delta = k * .5f;
            float x = 0, z = 0, heading = 0; var t = 0.0;
            while (z > -gap) { (x, z, heading) = SteerStep(x, z, heading, delta); t += Step; }
            result[k] = (MathF.Abs(x - delta) <= .15f && MathF.Abs(heading) <= 5 * MathF.PI / 180, t + z / Speed);
        }
        return result;
    }

    private static (float X, float Z, float Heading) SteerStep(float x, float z, float heading, float lane)
    {
        var desired = Math.Clamp(MathF.Atan2(lane - x, Reach), -MaxSteer, MaxSteer);
        var next = heading + Math.Clamp(Normalize(desired - heading), -Turn, Turn);
        var mid = (heading + next) / 2;
        return (x + Speed * (float)Step * MathF.Sin(mid), z - Speed * (float)Step * MathF.Cos(mid), next);
    }

    // Steering toward each pair's lane, switching after the pair's last band.
    internal static Point[]? Steer(float x, float z, float heading, double t, float[] lanes, float half, float until = Exit)
    {
        var path = new List<Point>(200) { new(t, x, z, heading) };
        for (var guard = 0; z > until; ++guard)
        {
            if (guard > 600) return null;
            var pair = Array.FindIndex(Pairs, p => z > p - PairGap - half);
            (x, z, heading) = SteerStep(x, z, heading, lanes[pair < 0 ? Pairs.Length - 1 : pair]);
            t += Step;
            if (!Floor(x, z)) return null;
            path.Add(new(t, x, z, heading));
        }
        return [.. path];
    }

    // Reachable pass times of each later pair: lane held through a pair, the
    // next pair 20m on plus the lane change.
    private static void Propagate(ulong[][][] reach, ulong[][][] safe, int from)
    {
        for (var p = from + 1; p < Pairs.Length; ++p)
        {
            reach[p] = new ulong[Grid.Length][];
            for (var j = 0; j < Grid.Length; ++j)
            {
                var next = new ulong[Words];
                for (var i = 0; i < Grid.Length; ++i)
                {
                    var change = Changes[Math.Abs(i - j)];
                    if (change.Ok) ShiftOr(next, reach[p - 1][i], (int)Math.Round((PairStep / Speed + change.Extra) / Tick));
                }
                And(next, safe[p][j]);
                reach[p][j] = next;
            }
        }
    }

    // Lane schedules in order of the last pair's pass time; one per time and
    // lane, preferring the fewest and smallest lane changes on the way back.
    private static IEnumerable<(float[] Lanes, int First)> Candidates(ulong[][][] reach, ulong[][][] safe, int from)
    {
        var last = Pairs.Length - 1;
        var tried = 0;
        for (var bin = 0; bin < Bins && tried < 16; ++bin)
            for (var j = 0; j < Grid.Length && tried < 16; ++j)
            {
                if (!Get(reach[last][j], bin)) continue;
                var lanes = new float[Pairs.Length]; var index = new int[Pairs.Length];
                index[last] = j; var b = bin; var ok = true;
                for (var p = last; p > from && ok; --p)
                {
                    ok = false;
                    foreach (var i in Enumerable.Range(0, Grid.Length).OrderBy(i => Math.Abs(i - index[p])))
                    {
                        var change = Changes[Math.Abs(i - index[p])];
                        if (!change.Ok) continue;
                        var previous = b - (int)Math.Round((PairStep / Speed + change.Extra) / Tick);
                        if (previous < 0 || !Get(reach[p - 1][i], previous)) continue;
                        index[p - 1] = i; b = previous; ok = true; break;
                    }
                }
                if (!ok) continue;
                for (var p = 0; p < Pairs.Length; ++p) lanes[p] = Grid[index[Math.Max(p, from)]];
                ++tried;
                yield return (lanes, b);
            }
    }

    // Pass times at each pair's first row that keep a lane clear of both rows.
    private static ulong[][][] SafeSets(IReadOnlyList<BlundervilleSpinImpact> impacts, double[] times, Tier tier, int from)
    {
        var m = tier.Margins;
        var half = BlundervilleSpinSchedule.Half + m.Along;
        var result = new ulong[Pairs.Length][][];
        for (var p = from; p < Pairs.Length; ++p)
        {
            result[p] = new ulong[Grid.Length][];
            for (var i = 0; i < Grid.Length; ++i) result[p][i] = Window(0, Bins - 1);
        }
        for (var k = 0; k < impacts.Count; ++k)
        {
            var impact = impacts[k];
            var p = Array.FindIndex(Pairs, z => MathF.Abs(impact.Z - z) < .1f || MathF.Abs(impact.Z - (z - PairGap)) < .1f);
            if (p < from || p < 0) continue;
            // Pass time of the pair's first row while this row's band is occupied.
            var offset = (Pairs[p] - impact.Z) / Speed;
            var lo = Bin(times[k] - m.Before - half / Speed - offset - tier.Erosion * Tick, false);
            var hi = Bin(times[k] + m.After + half / Speed - offset + tier.Erosion * Tick, true);
            var reachX = BlundervilleSpinSchedule.Half + m.Side;
            var first = Math.Max(0, (int)MathF.Floor((impact.X - reachX - Grid[0]) / .5f));
            var last = Math.Min(Grid.Length - 1, (int)MathF.Ceiling((impact.X + reachX - Grid[0]) / .5f));
            for (var i = first; i <= last; ++i)
                if (MathF.Abs(Grid[i] - impact.X) < reachX) Clear(result[p][i], lo, hi);
        }
        return result;
    }

    // A path against the clock; `at` shifts path time onto `times`.
    private static bool Verify(IReadOnlyList<Point> path, IReadOnlyList<BlundervilleSpinImpact> impacts, double[] times, Margins margins, double at)
    {
        for (var i = 0; i + 1 < path.Count; ++i)
        {
            var a = path[i]; var b = path[i + 1];
            if (b.T < at) continue;
            if (Hits(times, impacts, margins, a.T - at, a.X, a.Z, b.X, b.Z)) return false;
        }
        return true;
    }

    private static Point At(Plan plan, double at)
    {
        var path = plan.Path;
        var i = Math.Clamp((int)Math.Floor((at - path[0].T) / Step), 0, path.Length - 2);
        var w = (float)Math.Clamp((at - path[i].T) / Step, 0, 1);
        return new(at, path[i].X + (path[i + 1].X - path[i].X) * w, path[i].Z + (path[i + 1].Z - path[i].Z) * w,
            path[i].Heading + Normalize(path[i + 1].Heading - path[i].Heading) * w);
    }

    private static string Describe(Plan plan, double at)
        => $"旋转桥：按扇叶节奏穿行（第{plan.Tier + 1}档，车道{string.Join("/", plan.Lanes.Select(l => l.ToString("0.#")))}，{plan.Path[^1].T - at:F1}s到桥尾）";

    // Plan positions from `from` seconds on, every 0.5s, then off the bridge.
    // The first point stays ahead of the player: a passed waypoint would turn
    // a 3702 character back, and every scan republishes from its position.
    internal static List<Vector3> Publish(Plan plan, double from)
    {
        var result = new List<Vector3>();
        var path = plan.Path;
        for (var t = Math.Max(from, path[0].T + Step); t < path[^1].T; t += .5)
        {
            var point = At(plan, t);
            result.Add(new(point.X, 10.03f, point.Z));
        }
        var last = path[^1];
        result.Add(new(last.X, 10.03f, last.Z));
        result.Add(new(Math.Clamp(last.X, -4, 4), 10.03f, End));
        return result;
    }

    // From the gate: along the gate line to the lane, straight onto the bridge.
    private static List<Vector3> Approach(Plan plan, Vector3 player, double at)
    {
        var lane = plan.Lanes[0];
        var result = new List<Vector3>();
        if (MathF.Abs(player.X - lane) > .3f) result.Add(new(lane, 10.03f, Math.Min(player.Z, GateZ)));
        result.Add(new(lane, 10.03f, Entry));
        result.AddRange(Publish(plan, Math.Max(at, plan.Path[0].T) + Lookahead));
        return result;
    }

    // The bridge floor, the narrower walkway before it and the exit after it,
    // with the steering lag as margin.
    private static bool Floor(float x, float z)
        => z > Entry ? MathF.Abs(x) <= 6 : z >= -331.5f ? x is >= -6.8f and <= 6.6f : MathF.Abs(x) <= 5.5f;

    private static bool Hits(double[] times, IReadOnlyList<BlundervilleSpinImpact> impacts, Margins m, double t0,
        float x0, float z0, float x1, float z1)
    {
        var t1 = t0 + Step;
        // First impact whose window can still overlap this step.
        int lo = 0, hi = times.Length;
        while (lo < hi) { var mid = (lo + hi) / 2; if (times[mid] + m.After < t0) lo = mid + 1; else hi = mid; }
        for (var i = lo; i < times.Length && times[i] - m.Before <= t1; ++i)
        {
            var a = Math.Max(t0, times[i] - m.Before); var b = Math.Min(t1, times[i] + m.After);
            if (a > b) continue;
            var wa = (float)((a - t0) / Step); var wb = (float)((b - t0) / Step);
            var impact = impacts[i];
            if (Crosses(x0 + (x1 - x0) * wa, z0 + (z1 - z0) * wa, x0 + (x1 - x0) * wb, z0 + (z1 - z0) * wb,
                impact.X, impact.Z, BlundervilleSpinSchedule.Half + m.Side, BlundervilleSpinSchedule.Half + m.Along)) return true;
        }
        return false;
    }

    // Segment against an axis-aligned box (slab test).
    private static bool Crosses(float ax, float az, float bx, float bz, float cx, float cz, float hx, float hz)
    {
        float lo = 0, hi = 1;
        return Slab(ax - cx, bx - ax, hx, ref lo, ref hi) && Slab(az - cz, bz - az, hz, ref lo, ref hi);
    }

    private static bool Slab(float start, float delta, float half, ref float lo, ref float hi)
    {
        if (MathF.Abs(delta) < 1e-6f) return MathF.Abs(start) <= half;
        var u = (-half - start) / delta; var v = (half - start) / delta;
        if (u > v) (u, v) = (v, u);
        lo = MathF.Max(lo, u); hi = MathF.Min(hi, v);
        return lo <= hi;
    }

    private static double[] Times(IReadOnlyList<BlundervilleSpinImpact> impacts, DateTime now)
    {
        var times = new double[impacts.Count];
        for (var i = 0; i < times.Length; ++i) times[i] = (impacts[i].At - now).TotalSeconds;
        return times;
    }

    // Pass-time bins: Tick apart, starting 5s back (a pair already entered).
    private const int Back = 100;
    private static readonly int Bins = Back + (int)(Horizon / Tick);
    private static readonly int Words = (Bins + 63) / 64;
    private static int Bin(double t, bool up)
        => Math.Clamp(up ? (int)Math.Ceiling(t / Tick - 1e-9) + Back : (int)Math.Floor(t / Tick + 1e-9) + Back, 0, Bins - 1);
    private static double Time(int bin) => (bin - Back) * Tick;
    private static ulong[] Window(int from, int to)
    {
        var set = new ulong[Words];
        if (from > to) return set;
        Clear(set, from, to);
        for (var w = 0; w < set.Length; ++w) set[w] = ~set[w];
        var below = from >> 6; var above = to >> 6;
        for (var w = 0; w < set.Length; ++w)
            if (w < below || w > above) set[w] = 0;
        set[below] &= ~0UL << (from & 63);
        set[above] &= Mask(to);
        return set;
    }

    // Bits 0..(bin & 63) of the bin's word.
    private static ulong Mask(int bin) => (bin & 63) == 63 ? ~0UL : (1UL << ((bin & 63) + 1)) - 1;
    private static void Set(ulong[] set, int bin) => set[bin >> 6] |= 1UL << (bin & 63);
    private static bool Get(ulong[] set, int bin) => (set[bin >> 6] & (1UL << (bin & 63))) != 0;
    private static void Clear(ulong[] set, int from, int to)
    {
        if (from > to) return;
        int a = from >> 6, b = to >> 6;
        ulong head = ~0UL << (from & 63), tail = Mask(to);
        if (a == b) { set[a] &= ~(head & tail); return; }
        set[a] &= ~head;
        for (var w = a + 1; w < b; ++w) set[w] = 0;
        set[b] &= ~tail;
    }
    private static void And(ulong[] set, ulong[] mask) { for (var w = 0; w < set.Length; ++w) set[w] &= mask[w]; }

    // set |= source shifted `bins` later.
    private static void ShiftOr(ulong[] set, ulong[] source, int bins)
    {
        int words = bins >> 6, bits = bins & 63;
        for (var w = set.Length - 1; w >= words; --w)
        {
            var value = source[w - words] << bits;
            if (bits != 0 && w - words - 1 >= 0) value |= source[w - words - 1] >> (64 - bits);
            set[w] |= value;
        }
    }

    private static float Normalize(float angle)
    {
        while (angle > MathF.PI) angle -= 2 * MathF.PI;
        while (angle <= -MathF.PI) angle += 2 * MathF.PI;
        return angle;
    }

    // A direct traversal that has arrived stands still without a solver taking
    // over; a runtime hold would reset the movement on every change.
    private static BlundervilleRaceDecision Hold(BlundervilleRaceDecision normal, Vector3 gate, string hint)
        => normal with { Target = gate, Hold = false, Jump = false, Hint = hint, DirectTraversal = true, ContinuousPath = [gate], SpinRowsOwned = true };

    private static BlundervilleRaceDecision Traverse(BlundervilleRaceDecision normal, IReadOnlyList<Vector3> path, string hint)
        => normal with { Target = path[^1], Hold = false, Jump = false, Hint = hint, DirectTraversal = true, ContinuousPath = path, SpinRowsOwned = true };
}

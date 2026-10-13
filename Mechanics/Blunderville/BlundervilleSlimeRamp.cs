using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// FirstB final ramp (slime, status 3703, z -338..-367) and its three 34774
// pushers on rows z -347.83/-355.82/-363.82. 2026-10-13 19:04 ARR: MP spent
// 66s on it and was eliminated. The 1.14.4 lane planner waited for pusher gaps
// standing, and on the slime a stop is a 4m/s slide back down: once from -354
// to the bottom without any push, then three pushes. ARR fits (MP, every
// FirstB round): the input runs at 6m/s and the slime adds 4m/s straight down
// the ramp, so straight up nets 2m/s, 48° off up holds the height while
// strafing at 4.5m/s, and releasing slides back at 4m/s.
//
// So the input always points up the ramp, at most 48° off (W held, A/D to
// steer). Below each row the plan picks a lane and the moment to cross; an
// early arrival strafes to and fro at the hold angle, and the row is crossed
// straight up while that lane's segment is not swept. The rows' lane/entry
// times come from a DP over the pusher clock (BlundervilleRampPushers), redone
// from the actual position every scan.
internal sealed class BlundervilleSlimeRamp
{
    internal const float Run = 6, Drift = 4, Climb = Run - Drift;
    internal static readonly float Hold = MathF.Acos(Drift / Run);
    // The slime runs from -338 (flat floor before it: standing is fine) to the top at -367.
    internal const float Begin = -335, Bottom = -338, Top = -367;
    internal static readonly float[] Rows = [-347.83f, -355.82f, -363.82f];
    // Lanes inside the outer stops, leaving room to strafe at the floor edge (±6.3).
    internal static readonly float[] Lanes = [.. Enumerable.Range(-9, 19).Select(i => i * .5f)];
    private const float Swing = 1, Edge = 5.6f, Step = 2;
    internal const double Horizon = 30;

    // Push: within 1.5m of the row and the step's x range 0.1–0.2s before its
    // packet. Windows around the scheduled packet, lane reach past the swept
    // range, band margin; the narrow set only when the wide one has no way up.
    internal readonly record struct Margins(double Before, double After, float Side, float Along);
    internal static readonly Margins[] Tiers = [new(.45, .2, .45f, .3f), new(.3, .1, .25f, .15f)];

    internal readonly record struct Crossing(int Row, float Lane, double Entry);
    internal sealed record Plan(Crossing[] Crossings, double Finish, int Tier);

    private float _side = 1;
    public Plan? Current { get; private set; }

    public void Reset() { Current = null; _side = 1; }

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state, BlundervilleRampPushers pushers,
        Vector3 player, DateTime now, bool recovering)
    {
        if (state.Course != BlundervilleCourse.FirstB || !state.IsRacing || state.Qualified || normal.Hold || recovering
            || player.Y < 9.5f || player.Z > Begin || player.Z < Top || MathF.Abs(player.X) > 7.5f)
        { Current = null; return normal; }
        // Without a pusher clock a solver would stop, and on the slime that is
        // a slide to the bottom: climb straight instead.
        if (!pushers.Ready(now))
        {
            Current = null;
            return player.Z > Bottom ? normal : Publish(normal, [player + new Vector3(0, 0, -Step)], "终点坡：推板时钟未就绪，直接上坡");
        }
        var steps = pushers.Steps(now.AddSeconds(Horizon));
        Plan? plan = null;
        foreach (var tier in Enumerable.Range(0, Tiers.Length))
            if ((plan = Schedule(steps, now, player, tier)) != null) break;
        Current = plan;
        var angle = Steer(plan, player, out var stand, out var hint);
        if (stand) return Publish(normal, [player], hint);
        var direction = new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle));
        // Keep the aim point on the floor; it only sets the input direction.
        var reach = Step;
        if (MathF.Abs(direction.X) > 1e-3f)
            reach = Math.Clamp((MathF.Sign(direction.X) * 6 - player.X) / direction.X, .6f, Step);
        return Publish(normal, [player + direction * reach], hint);
    }

    // The input angle (0 straight up the ramp, + toward +x).
    private float Steer(Plan? plan, Vector3 player, out bool stand, out string hint)
    {
        stand = false;
        var next = plan?.Crossings.FirstOrDefault(c => player.Z > Rows[c.Row] - Band(plan.Tier));
        if (plan == null || next is not { } crossing)
        {
            hint = plan == null ? "终点坡：没有推板间隙，直接上坡" : "终点坡：已过最后一排，直接上坡";
            return 0;
        }
        var band = Band(plan.Tier);
        var entry = Rows[crossing.Row] + band;
        // Crossing a row: straight up.
        if (player.Z <= entry)
        {
            hint = $"终点坡：第{crossing.Row + 1}排推板，车道{crossing.Lane:0.#}直上";
            return 0;
        }
        var rise = player.Z - entry;
        var lateral = crossing.Lane - player.X;
        var slack = crossing.Entry - Travel(lateral, rise);
        if (slack <= .1)
        {
            hint = $"终点坡：赶往第{crossing.Row + 1}排车道{crossing.Lane:0.#}";
            return Toward(lateral, rise);
        }
        // Early: stand on the flat floor, or climb to just below the row and strafe there.
        if (player.Z > Bottom && slack > .25)
        {
            stand = true;
            hint = $"终点坡：坡下等待{slack:F1}s";
            return 0;
        }
        var hold = entry + .6f;
        if (player.Z > hold + .4f && MathF.Abs(lateral) < 2)
        {
            hint = $"终点坡：上到第{crossing.Row + 1}排下方";
            return Toward(lateral, player.Z - hold);
        }
        // To and fro around the lane, never toward the floor edge.
        if (player.X < crossing.Lane - Swing || player.X < -Edge) _side = 1;
        else if (player.X > crossing.Lane + Swing || player.X > Edge) _side = -1;
        // Height correction while strafing: above the hold line drift back, below climb.
        var up = Math.Clamp((player.Z - hold) * 2, -1.5f, 1.5f);
        hint = $"终点坡：第{crossing.Row + 1}排下方横移等待{slack:F1}s";
        return _side * MathF.Acos(Math.Clamp((up + Drift) / Run, -1, 1));
    }

    private static float Band(int tier) => BlundervilleRampPushers.Half + Tiers[tier].Along;

    // The fastest constant input to cover `lateral` while rising `rise`: the
    // slime's velocities form a 6m/s circle centred 4m/s down the ramp.
    internal static double Travel(float lateral, float rise)
    {
        var length = MathF.Sqrt(lateral * lateral + rise * rise);
        if (length < 1e-4f) return 0;
        var down = -rise / length;
        return length / (MathF.Sqrt(16 * down * down + 20) + 4 * down);
    }

    private static float Toward(float lateral, float rise)
    {
        var length = MathF.Sqrt(lateral * lateral + rise * rise);
        if (length < 1e-4f) return 0;
        var ux = lateral / length; var uz = -rise / length;
        var speed = MathF.Sqrt(16 * uz * uz + 20) + 4 * uz;
        // Input = (net velocity - drift) / run speed, drift +4 along z.
        var ix = ux * speed / Run; var iz = (uz * speed - Drift) / Run;
        return Math.Clamp(MathF.Atan2(ix, -iz), -Hold, Hold);
    }

    // Earliest lane/entry per remaining row (waiting is free: strafing holds).
    internal static Plan? Schedule(IReadOnlyList<BlundervilleRampStep> steps, DateTime now, Vector3 player, int tier)
    {
        var m = Tiers[tier];
        var band = BlundervilleRampPushers.Half + m.Along;
        var occupy = 2 * band / Climb;
        var gap = Rows[0] - Rows[1] - 2 * band;
        // Danger windows per row and lane, in seconds after now.
        var windows = new List<(double From, double To)>[Rows.Length, Lanes.Length];
        for (var r = 0; r < Rows.Length; ++r)
            for (var i = 0; i < Lanes.Length; ++i) windows[r, i] = [];
        foreach (var step in steps)
        {
            var r = Array.FindIndex(Rows, z => MathF.Abs(z - step.Z) < .5f);
            if (r < 0) continue;
            var at = (step.At - now).TotalSeconds;
            float lo = MathF.Min(step.FromX, step.ToX) - m.Side, hi = MathF.Max(step.FromX, step.ToX) + m.Side;
            for (var i = 0; i < Lanes.Length; ++i)
                if (Lanes[i] >= lo && Lanes[i] <= hi) windows[r, i].Add((at - m.Before, at + m.After));
        }
        double Next(int r, int i, double t)
        {
            foreach (var (from, to) in windows[r, i])
            {
                if (to < t) continue;
                if (from > t + occupy) break;
                t = to + .01;
            }
            return t;
        }
        var first = Array.FindIndex(Rows, z => player.Z > z - band);
        if (first < 0) return new([], (player.Z - Top) / Climb, tier);
        var entry = new double[Rows.Length, Lanes.Length];
        var from = new int[Rows.Length, Lanes.Length];
        var inside = player.Z <= Rows[first] + band;
        var start = first;
        if (inside)
        {
            // Mid-crossing: the lane is where the player is; the next row follows its exit.
            var lane = Array.IndexOf(Lanes, Lanes.MinBy(l => MathF.Abs(l - player.X)));
            var exit = (player.Z - (Rows[first] - band)) / Climb;
            if (first == Rows.Length - 1) return new([new(first, player.X, -(Rows[first] + band - player.Z) / Climb)], exit + (Rows[first] - band - Top) / Climb, tier);
            start = first + 1;
            for (var j = 0; j < Lanes.Length; ++j)
            {
                entry[start, j] = Next(start, j, exit + Travel(Lanes[j] - Lanes[lane], gap));
                from[start, j] = lane;
            }
        }
        else
            for (var j = 0; j < Lanes.Length; ++j)
                entry[start, j] = Next(start, j, Travel(Lanes[j] - player.X, player.Z - (Rows[start] + band)));
        for (var r = start + 1; r < Rows.Length; ++r)
            for (var j = 0; j < Lanes.Length; ++j)
            {
                entry[r, j] = double.MaxValue;
                for (var i = 0; i < Lanes.Length; ++i)
                {
                    var t = Next(r, j, entry[r - 1, i] + occupy + Travel(Lanes[j] - Lanes[i], gap));
                    if (t < entry[r, j] - 1e-6) { entry[r, j] = t; from[r, j] = i; }
                }
            }
        var last = Rows.Length - 1;
        var best = Enumerable.Range(0, Lanes.Length).MinBy(j => entry[last, j]);
        var finish = entry[last, best] + occupy + (Rows[last] - band - Top) / Climb;
        if (finish > Horizon - 2) return null;
        var crossings = new List<Crossing>();
        for (int r = last, j = best; r >= start; j = from[r, j], --r) crossings.Add(new(r, Lanes[j], entry[r, j]));
        if (inside) crossings.Add(new(first, player.X, -(Rows[first] + band - player.Z) / Climb));
        crossings.Reverse();
        return new([.. crossings], finish, tier);
    }

    private static BlundervilleRaceDecision Publish(BlundervilleRaceDecision normal, IReadOnlyList<Vector3> path, string hint)
        => normal with { Target = path[^1], Hold = false, Jump = false, Hint = hint, DirectTraversal = true, ContinuousPath = path, RampOwned = true };
}

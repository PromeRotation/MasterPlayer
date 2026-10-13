using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// FirstB final ramp (slime, status 3703, z -338..-367) and its three 34774
// pushers on rows z -347.83/-355.82/-363.82. Slime physics (MP and human ARR
// fits): input runs at 6m/s and the slime adds 4m/s down the ramp, so straight
// up nets 2m/s.
//
// 2026-10-14 02:08 and 04:10 ARRs: the slide state (server move state, pid 277
// = 3: about a second without control, often down to the foot) hit MP 0.25 times
// per second of climbing against 0.027 for the humans on the same ramps, 0.2/s
// against 0.008/s far from any pusher. MP climbed this ramp straight along its
// route in 11 rounds (10/07-10/11) without a single slide; every version since
// that steered sideways, held height or switched lanes on the slime slid. So
// the slime is climbed straight up only, like holding W: the pusher timing is
// chosen on the flat floor before it, where standing is safe. A lane and a
// departure are picked so one straight climb at 2m/s crosses every row between
// its pusher steps; the aim point stays fixed, and a slide or push back onto the
// floor plans again from there.
internal sealed class BlundervilleSlimeRamp
{
    internal const float Run = 6, Drift = 4, Climb = Run - Drift;
    // Flat floor before the slime: standing is fine. The slime status starts
    // between -338.6 and -339.8 (ARR gains); climb times count from -338.6.
    internal const float Begin = -335, Bottom = -338, SlimeEdge = -338.6f, Wait = -337.2f, Top = -367;
    internal static readonly float[] Rows = [-347.83f, -355.82f, -363.82f];
    // Straight lanes inside the floor (the edge is +-6.3..6.8 along the slime).
    internal static readonly float[] Lanes = [.. Enumerable.Range(-11, 23).Select(i => i * .5f)];
    internal const double Horizon = 30, DepartureStep = .05, MaxDeparture = 12;
    private const float AimBeyond = 2;

    // Push: within 1.5m of the row and the step's x range 0.1–0.2s before its
    // packet. Windows around the scheduled packet, lane reach past the swept
    // range, band margin; the narrow set only when the wide one has no way up.
    // Speed: the climb rate may differ by this fraction (MP measured 2.00–2.01m/s;
    // humans 1.8–2.1), which grows into a timing error up the ramp: +-5% is
    // over a second at the top row.
    internal readonly record struct Margins(double Before, double After, float Side, float Along, float Speed);
    internal static readonly Margins[] Tiers = [new(.45, .2, .45f, .3f, .04f), new(.3, .1, .25f, .15f, .02f), new(.3, .1, .25f, .15f, 0)];

    // Depart: seconds from now when the climb leaves the slime edge (z -338.6).
    internal sealed record Plan(float Lane, double Depart, double Finish, int Tier);

    private Plan? _plan;
    private DateTime _planned, _waitingSince;
    // Without a pusher clock, or with no clear climb in sight, the floor wait
    // ends after these many seconds and the climb goes straight anyway.
    internal const double ClockWait = 6, GapWait = 15;
    private Vector3? _aim;
    public Plan? Current => _plan;

    public void Reset() { _plan = null; _aim = null; _waitingSince = default; }

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state, BlundervilleRampPushers pushers,
        Vector3 player, DateTime now, bool recovering)
    {
        if (state.Course != BlundervilleCourse.FirstB || !state.IsRacing || state.Qualified || normal.Hold || recovering
            || player.Y < 9.5f || player.Z > Begin || player.Z < Top || MathF.Abs(player.X) > 7.5f)
        { Reset(); return normal; }
        var slime = player.Z < Bottom;
        if (slime)
        {
            // On the slime: straight up from where the player is, never sideways.
            _plan = null; _waitingSince = default;
            return Publish(normal, [Straight(player)], "终点坡：粘液上直线上坡");
        }
        _aim = null;
        if (_waitingSince == default) _waitingSince = now;
        var waited = (now - _waitingSince).TotalSeconds;
        if (!pushers.Ready(now))
            return waited < ClockWait ? Publish(normal, [new(player.X, player.Y, Wait)], "终点坡：坡下等待推板时钟")
                : Publish(normal, [new(player.X, player.Y, Top - AimBeyond)], "终点坡：推板时钟未就绪，直接上坡");
        var steps = pushers.Steps(now.AddSeconds(Horizon));
        // Keep a committed plan while its straight climb still clears; a new
        // plan every scan would wander between equal lanes on the floor.
        if (_plan is { } kept)
        {
            var elapsed = (now - _planned).TotalSeconds;
            var shifted = kept with { Depart = kept.Depart - elapsed, Finish = kept.Finish - elapsed };
            _plan = shifted.Depart > -.3 && Clear(steps, now, shifted.Lane, Math.Max(0, shifted.Depart), Tiers[shifted.Tier])
                && shifted.Depart >= Walk(player, shifted.Lane) - .05 ? shifted : null;
        }
        if (_plan == null)
            for (var tier = 0; tier < Tiers.Length && _plan == null; ++tier)
                _plan = Schedule(steps, now, player, tier);
        _planned = now;
        if (_plan is not { } plan)
            return waited < GapWait ? Publish(normal, [new(player.X, player.Y, Wait)], "终点坡：坡下等待推板间隙")
                : Publish(normal, [new(player.X, player.Y, Top - AimBeyond)], "终点坡：等待过久，直接上坡");
        var start = new Vector3(plan.Lane, player.Y, Wait);
        var walk = Walk(player, plan.Lane);
        if (plan.Depart - walk > .1)
        {
            if (Vector2.Distance(new(player.X, player.Z), new(start.X, start.Z)) > .15f)
                return Publish(normal, [start], $"终点坡：到车道{plan.Lane:0.#}坡下等待");
            return Publish(normal, [player], $"终点坡：车道{plan.Lane:0.#}坡下等待{plan.Depart - walk:F1}s后直上");
        }
        // Reach the lane on the floor first: entering the slime diagonally
        // would climb a different lane than the one timed.
        Vector3[] path = MathF.Abs(player.X - plan.Lane) > .15f ? [start, start with { Z = Top - AimBeyond }] : [start with { Z = Top - AimBeyond }];
        return Publish(normal, path, $"终点坡：车道{plan.Lane:0.#}出发直上");
    }

    // A fixed aim straight up the ramp (re-dispatch only when the lane drifts).
    private Vector3 Straight(Vector3 player)
    {
        if (_aim is { } aim && MathF.Abs(aim.X - player.X) < .3f && aim.Z < player.Z - 1) return aim;
        _aim = new Vector3(Math.Clamp(player.X, -5.8f, 5.8f), player.Y, Top - AimBeyond);
        return _aim.Value;
    }

    // Seconds to walk the flat floor to the lane's slime edge.
    private static double Walk(Vector3 player, float lane)
        => Vector2.Distance(new(player.X, player.Z), new(lane, Wait)) / Run + (Wait - SlimeEdge) / Run;

    // The earliest departure from the slime edge (and the lane) whose straight
    // climb crosses every row between its pusher steps.
    internal static Plan? Schedule(IReadOnlyList<BlundervilleRampStep> steps, DateTime now, Vector3 player, int tier)
    {
        var margins = Tiers[tier];
        Plan? best = null;
        foreach (var lane in Lanes.OrderBy(l => MathF.Abs(l - player.X)))
        {
            var earliest = Walk(player, lane);
            for (var depart = earliest; depart <= MaxDeparture; depart += DepartureStep)
            {
                if (best != null && depart >= best.Depart - 1e-6) break;
                if (!Clear(steps, now, lane, depart, margins)) continue;
                best = new(lane, depart, depart + (SlimeEdge - Top) / Climb, tier);
                break;
            }
        }
        return best;
    }

    // Whether a straight climb up `lane`, leaving the slime edge `depart`
    // seconds from now, stays out of every pusher step window.
    internal static bool Clear(IReadOnlyList<BlundervilleRampStep> steps, DateTime now, float lane, double depart, Margins margins)
    {
        var band = BlundervilleRampPushers.Half + margins.Along;
        foreach (var step in steps)
        {
            var r = Array.FindIndex(Rows, z => MathF.Abs(z - step.Z) < .5f);
            if (r < 0) continue;
            if (lane < MathF.Min(step.FromX, step.ToX) - margins.Side || lane > MathF.Max(step.FromX, step.ToX) + margins.Side) continue;
            var enter = depart + (SlimeEdge - (Rows[r] + band)) / (Climb * (1 + margins.Speed));
            var leave = depart + (SlimeEdge - (Rows[r] - band)) / (Climb * (1 - margins.Speed));
            var at = (step.At - now).TotalSeconds;
            if (at + margins.After >= enter && at - margins.Before <= leave) return false;
        }
        return true;
    }

    private static BlundervilleRaceDecision Publish(BlundervilleRaceDecision normal, IReadOnlyList<Vector3> path, string hint)
        => normal with { Target = path[^1], Hold = false, Jump = false, Hint = hint, DirectTraversal = true, ContinuousPath = path, RampOwned = true };
}

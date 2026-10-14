using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleSlimeRamp
{
    internal const float Run = 6, Drift = 4, Climb = Run - Drift;
    internal const float Begin = -335, Bottom = -338, SlimeEdge = -338.6f, Wait = -337.2f, Top = -367;
    internal static readonly float[] Rows = [-347.83f, -355.82f, -363.82f];
    private Vector3? _aim;
    private DateTime _nextAim, _departure;
    private float? _lane;
    private bool _entering;
    internal bool Complete { get; private set; }
    private BlundervilleRampAscent.Result? _ascent;
    private DateTime _ascentAt;
    private DateTime? _waitingSince;
    internal const double MaxFootWait = 3;
    internal const bool ClimbFromFoot = true;

    public void Reset() { _aim = null; _lane = null; _ascent = null; _entering = false; _nextAim = _departure = default; _waitingSince = null; }

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state, BlundervilleRampPushers pushers,
        Vector3 player, DateTime now, bool recovering, BlundervilleRampBalls? balls = null)
    {
        if (state.Course != BlundervilleCourse.FirstB || !state.IsRacing || state.Qualified || normal.Hold || recovering
            || player.Y < 9.5f || player.Z > Begin || player.Z < Top || MathF.Abs(player.X) > 7.5f)
        { Reset(); return normal; }
        // 2026-10-15 02:10 ARR: the timed foot plan re-picked lanes every 0.5s
        // and walked the player left and right along z=-337.2 until the round
        // ended; 01:45 waited 12.5s there. The user's rule for this ramp: keep
        // forward (W) held from the foot to the top and only steer. So the foot
        // is not a waiting point any more: the on-ramp search steers from here.
        if (player.Z < Bottom || ClimbFromFoot)
        {
            _lane = null;
            // Every aim remains uphill; target handoff never releases movement.
            // Re-sample from the actual position, including the slime onset and
            // any knockback, instead of keeping the original lane blindly.
            if (_aim == null || now >= _nextAim)
            {
                var ascent = BlundervilleRampAscent.Search(player, now, pushers.Steps(now.AddSeconds(19)), balls?.Forecast(now, 19) ?? []);
                _aim = ascent.Aim;
                Complete = ascent.Complete;
                if (ascent.Complete) { _ascent = ascent; _ascentAt = now; }
                else if (_ascent != null)
                {
                    // A truncated search must not replace the next timed turn
                    // of the committed ascent with a straight, dead-end prefix.
                    // Reuse only while actual progress matches that segment.
                    var index = (int)Math.Round((now - _ascentAt).TotalSeconds / BlundervilleRampAscent.Segment);
                    if (index >= 0 && index < _ascent.Aims.Count && Vector2.Distance(new(player.X, player.Z), _ascent.Starts[index]) < .35f)
                        _aim = _ascent.Aims[index];
                }
                _nextAim = now.AddSeconds(BlundervilleRampAscent.Segment);
            }
            return Publish(normal, [_aim.Value], "终点坡：持续前进，微调避开雪球");
        }
        _aim = null;
        _ascent = null;
        if (!pushers.Ready(now) || balls?.Ready(now) != true)
            return Publish(normal, [new(player.X, player.Y, Wait)], "终点坡：坡下观察推板与雪球");
        // Refresh while still on dry ground. New ball observations can both
        // open an earlier departure and invalidate a previously safe window.
        // Once entering, keep the forward target through the slime handoff.
        if (!_entering && now >= _nextAim)
        {
            var plan = BlundervilleRampAscent.Search(player, now, pushers.Steps(now.AddSeconds(31)), balls.Forecast(now, 31), floor: true);
            _nextAim = now.AddSeconds(.5);
            _waitingSince ??= now;
            // 2026-10-15 01:45 ARR: no fully clear ascent for 12.5s at the foot
            // (the user climbed by hand). After MaxFootWait take the deepest
            // partial plan; the on-ramp search keeps steering around the balls.
            if (!plan.Complete && ((now - _waitingSince.Value).TotalSeconds < MaxFootWait || plan.Aims.Count == 0))
            {
                _lane = null;
                return Publish(normal, [new(player.X, player.Y, Wait)], "终点坡：坡下等待安全上坡时机");
            }
            _lane = plan.Lane;
            _departure = now.AddSeconds(plan.Depart);
        }
        if (_lane == null) return Publish(normal, [new(player.X, player.Y, Wait)], "终点坡：坡下等待安全上坡时机");
        var start = new Vector3(_lane.Value, player.Y, Wait);
        var walk = Vector2.Distance(new(player.X, player.Z), new(start.X, start.Z)) / Run + (Wait - Bottom) / Run;
        if ((_departure - now).TotalSeconds - walk > .08)
            return Publish(normal, [start], $"终点坡：车道{_lane:0.##}坡下等待");
        _entering = true;
        Vector3[] path = MathF.Abs(player.X - _lane.Value) > .12f ? [start, start with { Z = Top - 2 }] : [start with { Z = Top - 2 }];
        return Publish(normal, path, $"终点坡：车道{_lane:0.##}持续上坡");
    }

    private static BlundervilleRaceDecision Publish(BlundervilleRaceDecision normal, IReadOnlyList<Vector3> path, string hint)
        => normal with { Target = path[^1], Hold = false, Jump = false, Hint = hint, DirectTraversal = true, ContinuousPath = path, RampOwned = true };
}

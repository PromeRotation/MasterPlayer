using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;

// ARR 2026-10-12 00:17:11: FirstA arrow bridge pinned against the exit hole,
// near-staging re-approaches and full-width lateral ice slides on the platform.
internal static class BlundervilleFeedback001711Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);
    private static readonly BlundervilleRaceDecision Normal = new(new Vector3(-200, 10.03f, -290), false, false, null, "normal");

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville 001711 second hammer row stops before the exit hole", () =>
        {
            var state = ArrowState(); var planner = new BlundervilleArrowPlanner();
            var player = new Vector3(-206, 11.6f, -273.5f);
            var decision = Aligned(planner, state, player, new(-206, 11.6f, BlundervilleArrowPlanner.SecondRowExit), Epoch.AddSeconds(1));
            Check(decision is { DirectTraversal: true, Hold: false, Target: { } target }
                && MathF.Abs(target.Z - BlundervilleArrowPlanner.SecondRowExit) < .01f && MathF.Abs(target.X + 206) < .05f,
                $"The second-row run did not end between the hammers and the hole: {decision.Target} hold={decision.Hold}.");
            // The exit holes begin at Z=-289.525 on both walkways.
            Check(decision.Target!.Value.Z > -289.525f + 1.5f
                    && BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA).Contains(decision.Target.Value - Vector3.UnitZ),
                "The straight run still ends against the exit hole edge.");
        });
        run("Blunderville 001711 second hammer row hands off once without ping-pong", () =>
        {
            var state = ArrowState(); var planner = new BlundervilleArrowPlanner();
            var now = Epoch.AddSeconds(2);
            var released = planner.Apply(Normal, state, new(-206, 11.6f, -287.2f), MathF.PI, now, true);
            Check(!released.DirectTraversal && released.Target == Normal.Target && released.Hint == Normal.Hint,
                "The arrow controller kept the player after clearing the second hammer row.");
            // Ordinary navigation may nudge the player a little north while it
            // bends toward the central exit. That must not re-enter arrow mode.
            var drift = planner.Apply(Normal, state, new(-205.6f, 11.6f, -285.6f), MathF.PI, now.AddSeconds(.1), true);
            Check(!drift.DirectTraversal && drift.Target == Normal.Target,
                "A small drift re-armed the arrow controller and recreated the -288.5 ping-pong.");
            var knocked = planner.Apply(Normal, state, new(-204, 11.6f, -283.9f), MathF.PI, now.AddSeconds(.2), true);
            Check(knocked.DirectTraversal, "A real return into the hammer row was left to ordinary timed navigation.");
        });
        run("Blunderville 001711 a start just short of staging crosses without a micro approach", () =>
        {
            var state = ArrowState(); var planner = new BlundervilleArrowPlanner();
            var player = new Vector3(-206.5f, 11.6f, -273.06f);
            var decision = Aligned(planner, state, player, new(-206, 11.6f, BlundervilleArrowPlanner.SecondRowExit), Epoch.AddSeconds(3));
            Check(decision is { DirectTraversal: true, Hold: false, Target.Z: < -287f },
                $"A position 0.44m from staging still demanded a separate exact-arrow approach: {decision.Target} {decision.Hint}.");
            var first = new BlundervilleArrowPlanner();
            var entrance = new Vector3(-200, 10.03f, -248.2f);
            var staged = Aligned(first, state, entrance, new(-200, 10.03f, -253.5f), Epoch.AddSeconds(4));
            Check(staged is { DirectTraversal: true, Hold: false, Target.Z: > -253.6f and < -253.4f },
                $"The bridge entrance no longer stages before the first hammer row: {staged.Target}.");
        });
        run("Blunderville 001711 platform lateral reversals require clear progress", () =>
        {
            // Recorded reversal: east along Z=-183.28, then back west 0.73m lower.
            var from = new Vector3(-193.71f, 17.47f, -183.28f);
            var east = new Vector3(4.9f, 0, 0);
            Check(BlundervilleBumperPlanner.PlatformReversal(from, east, new(-198.6f, 17.47f, -184.01f)),
                "The recorded east-west platform ping-pong is still permitted.");
            Check(!BlundervilleBumperPlanner.PlatformReversal(from, east, from + new Vector3(-2.5f, 0, -4.2f)),
                "A steep reversal that leaves the platform row was rejected.");
            // Inside a 5.5m lane, the recorded good zigzag reverses after a
            // mostly lateral slide while dropping only ~1m; keep it legal.
            Check(!BlundervilleBumperPlanner.PlatformReversal(new(-196.58f, 17.47f, -190.42f), new(-4.55f, 0, -.98f),
                    new(-195f, 17.47f, -191.5f)),
                "The lane zigzag was restricted by the platform-only rule.");
        });
        run("Blunderville 001711 a standing plan on the ice platform may hold instead of sliding laterally", () =>
        {
            var floor = BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA);
            var origin = new Vector3(-200, 17.47f, -183.3f);
            BlundervilleHazard[] block = [new("test/lanes", new RectangleGeometry(new(-200, 17.47f, -188), 0, 26, 4),
                Epoch, Epoch.AddSeconds(2.5), Epoch)];
            var result = (BlundervilleBumperPlanner.Step[])typeof(BlundervilleBumperPlanner)
                .GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object?[] { floor, block, origin, 6f, Epoch, 20.5, null, true })!;
            // Only two verified slides are committed; the exit is a preview, so
            // the queued route advances past the blocked row rather than to -205.
            Check(result.Length > 0 && (result[^1].LandingPosition ?? result[^1].Position).Z < origin.Z - 5,
                "A temporarily blocked lane left no plan from the standing ice platform.");
            Check(result[0] is { Sliding: false, Wait: > 0 } && Vector3.Distance(result[0].Position, origin) < .01f,
                "The blocked lane was not waited out at the standing point.");
            var start = origin; Vector3? incoming = null;
            foreach (var step in result)
            {
                var landing = step.LandingPosition ?? step.Position;
                if (!step.Sliding) { start = landing; incoming = null; continue; }
                var outgoing = step.Position - start;
                if (incoming is { } previous && start.Z > BlundervilleBumperPlanner.IcePlatformSouthEdge
                    && MathF.Abs(previous.Z) < .4f * Flat(previous) && previous.X * outgoing.X < 0)
                    Check(MathF.Abs(outgoing.Z) >= .3f * Flat(outgoing),
                        $"The platform plan reversed laterally at {start}: {string.Join(" ", result.Select(s => s.Position))}.");
                incoming = landing - start; start = landing;
            }
            Check(result.Take(result.Length - 1).Skip(result[0].Sliding ? 0 : 1).All(s => s.Wait == 0),
                "Only the initial standing point may carry a hold.");
        });
    }

    private static BlundervilleRaceState ArrowState()
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        state.SynchronizeStatuses([new(3694, 360, Epoch.AddMinutes(2))], Epoch);
        return state;
    }

    // Teach a 3 rad/s rotation with two samples so the one-frame prediction
    // lands exactly on the requested aim.
    private static BlundervilleRaceDecision Aligned(BlundervilleArrowPlanner planner, BlundervilleRaceState state,
        Vector3 player, Vector3 aim, DateTime now)
    {
        const float speed = 3;
        var desired = MathF.Atan2(aim.X - player.X, aim.Z - player.Z);
        var second = desired - speed * .016f;
        planner.Apply(Normal, state, player, second - speed * .016f, now.AddSeconds(-.016));
        return planner.Apply(Normal, state, player, second, now);
    }

    private static float Flat(Vector3 v) => MathF.Sqrt(v.X * v.X + v.Z * v.Z);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

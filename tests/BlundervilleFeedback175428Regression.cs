using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;

internal static class BlundervilleFeedback175428Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville 175428 restarting at the ice-status edge cannot walk sideways", () =>
        {
            var origin = new Vector3(-207.4f, 17.47f, -180.72f);
            var floor = BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA);
            var steps = (BlundervilleBumperPlanner.Step[])typeof(BlundervilleBumperPlanner)
                .GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [floor, Array.Empty<BlundervilleHazard>(), origin, 3f, Epoch, 20.5, null, false, true])!;
            if (steps.Length == 0 || !steps[0].Sliding || steps[0].LandingPosition is not { } landing
                || Vector2.Distance(new(origin.X, origin.Z), new(landing.X, landing.Z)) < 4.8f)
                throw new InvalidOperationException("A standing restart under 3693 was planned as a short walking adjustment.");
        });
        run("Blunderville 175428 partial progress needs a complete safe slide refuge", () =>
        {
            var origin = new Vector3(-210, 17.47f, -181.5f);
            var floor = new RectangleGeometry(new(-210, 17.47f, -200), 0, 2, 45);
            var blockedExit = new BlundervilleHazard("wall", new RectangleGeometry(new(-210, 17.47f, -202), 0, 4, 3),
                Epoch, Epoch.AddSeconds(30), Epoch);
            BlundervilleBumperPlanner.Step[] Search(BlundervilleHazard[] hazards)
                => (BlundervilleBumperPlanner.Step[])typeof(BlundervilleBumperPlanner)
                    .GetMethod("Search", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, [floor, hazards, origin, 6f, Epoch, 20.5, null, false, true])!;
            var progress = Search([blockedExit]);
            if (progress.Length == 0 || progress.Any(s => !s.Sliding)
                || progress[^1].LandingPosition is not { Z: < -186 }
                || progress[^1].EndpointSafety < 6 || !double.IsPositiveInfinity(progress[^1].RemainingDuration))
                throw new InvalidOperationException("A distant blocked exit prevented certified progress or claimed an unplanned finish.");
            var closesRefuges = new BlundervilleHazard("closing", new RectangleGeometry(new(-210, 17.47f, -202), 0, 4, 36),
                Epoch.AddSeconds(4), Epoch.AddSeconds(30), Epoch);
            if (Search([blockedExit, closesRefuges]).Length != 0)
                throw new InvalidOperationException("A short-lived landing was accepted without a complete standing window.");
        });
        run("Blunderville 175428 learned period does not close the current bridge lane", () =>
        {
            using var replay = new BlundervilleObstacleRegression.Recording("obstacles-firsta-175428.json");
            // Fourth endpoint confirms the learned period at 15.45s, while
            // its cold/coalesced movement fallback still spans both ends.
            // The player is crossing beside the stationary eastern endpoint.
            replay.Advance(15.5);
            var now = Epoch.AddSeconds(15.5);
            var bodies = replay.Encounter.TraversalHazards(now)
                .Where(h => h.Id.StartsWith("bumper/6/") && h.From <= now && h.Until >= now).ToArray();
            if (bodies.Length == 0 || bodies.Any(h => h.Geometry.Contains(new(-213.47f, 17.441f, -166.21f))))
                throw new InvalidOperationException("Clock handoff falsely closed the lane beside the parked cylinder.");
            if (!bodies.Any(h => h.Geometry.Contains(new(-209.003f, 17.502f, -166.003f))))
                throw new InvalidOperationException("The real cylinder body was lost while preserving the passage.");
            replay.Encounter.Synchronize(replay.Actors.Values, now.AddSeconds(40));
            if (replay.Encounter.BumpersReady(new(-210, 17.47f, -158)))
                throw new InvalidOperationException("Expired clocks kept authorizing bridge entry.");
        });
        foreach (var (fps, delay, length) in new[] { (30, .3, 5f), (30, .6, 4.88f), (60, .3, 5.1f), (60, .2, 4.88f) })
            run($"Blunderville 175428 continuous bridge {fps}fps worker {delay:F1} slide {length:F2}",
                () => BlundervilleObstacleRegression.Simulate175428(fps, delay, length));
        run("Blunderville 175428 entrance departure", () =>
        {
            using var replay = new BlundervilleObstacleRegression.Recording("obstacles-firsta-175428.json");
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
            var player = new Vector3(-209.4f, 17.441f, -158.282f);
            var time = 11.572;
            var planner = new BlundervilleBumperPlanner(work => Task.FromResult(work()));
            for (; time < 37; time += .1)
            {
                replay.Advance(time);
                var now = Epoch.AddSeconds(time);
                var decision = planner.Apply(new(player - Vector3.UnitZ * 10, false, false, null, "normal"), state, replay.Encounter, player, now);
                if (!decision.Hold)
                {
                    Console.WriteLine($"DEPART t={time:F3} waited={time-11.572:F2} path={decision.ContinuousPath?.Count}");
                    if (time - 11.572 > 5) throw new InvalidOperationException("Recorded entrance still waits more than five seconds.");
                    return;
                }
            }
            throw new InvalidOperationException("No departure.");
        });
    }
}

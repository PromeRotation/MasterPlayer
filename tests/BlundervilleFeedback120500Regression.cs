using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;

// 2026-10-15 12:05 ARR. Final: the third riser landed at z=139.6, 1.4m past
// the last takeoff (z=141); the step was dropped and the player stood 8s on
// the last stair. Delivery: long stands at the north drop and the east row.
internal static class BlundervilleFeedback120500Regression
{
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville 120500 Final last riser is still jumped from a long landing", () =>
        {
            var planner = new BlundervilleJumpPlanner(); var now = BlundervilleRingRegression.Epoch;
            var target = new Vector3(-4, 35.8f, 137);
            foreach (var z in new[] { 141f, 140.4f, 139.6f, 139.3f })
            {
                planner.Reset();
                var step = planner.Plan(BlundervilleCourse.Final, now, new(-4, 34.47f, z), target, true, false);
                Check(step?.Corridor is { Stairs: true } corridor && corridor.Landing.Z < 138, $"No stair jump offered from z={z}.");
            }
        });
        run("Blunderville 120500 Delivery north carry rejoins the floor from the recorded stand", () =>
        {
            // The real player stood here 16s with no plan: the point is 0.05m
            // inside the modelled hole south of the north hammers.
            var state = new BlundervilleRaceState(); var epoch = BlundervilleRingRegression.Epoch;
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, epoch);
            var now = epoch.AddSeconds(16.2);
            state.SynchronizeStatuses([new(3704, 37, now.AddSeconds(30))], now);
            var p = new Vector3(-198.2f, 6, 211.7f);
            var planner = new BlundervilleFinalPlanner(BlundervilleFinalPlanner.DeliveryNorth);
            var decision = planner.Apply(new(null, false, false, null, ""), state, [], p, now);
            Check(!BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, p).Contains(p, 0), "The recorded stand is on the modelled floor again.");
            Check(decision is { Hold: false, DirectTraversal: true, Target: { } target }
                && BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, target).Contains(target, 0) && Vector3.Distance(target, p) < 1,
                $"No short rejoin was published: {decision.Hint} {decision.Target}.");
        });
    }

    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}

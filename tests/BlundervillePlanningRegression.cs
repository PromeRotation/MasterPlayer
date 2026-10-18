using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;

internal static class BlundervillePlanningRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("Blunderville planning continuous contacts agree with dense geometry", () =>
        {
            IGeometryShape[] shapes =
            [
                new CircleGeometry(Vector3.Zero, 2),
                new RectangleGeometry(Vector3.Zero, .7f, 3, 5),
                new RayRectangleGeometry(Vector3.Zero, -.6f, -1, 2, 5),
            ];
            var random = new Random(7401);
            foreach (var shape in shapes)
                for (var path = 0; path < 60; ++path)
                {
                    var from = new Vector3(random.NextSingle() * 16 - 8, -50, random.NextSingle() * 16 - 8);
                    var to = new Vector3(random.NextSingle() * 16 - 8, 50, random.NextSingle() * 16 - 8);
                    var contact = BlundervilleSegmentContacts.Find(shape, from, to, .3f);
                    for (var sample = 0; sample <= 1000; ++sample)
                    {
                        var fraction = sample / 1000d;
                        if (contact is { } edge && Math.Min(Math.Abs(fraction - edge.From), Math.Abs(fraction - edge.Until)) < 1e-5) continue;
                        var expected = shape.Contains(Vector3.Lerp(from, to, (float)fraction), .3f);
                        var actual = contact is { } interval && fraction >= interval.From && fraction <= interval.Until;
                        Check(actual == expected, $"{shape.GetType().Name} contact mismatch at {fraction}.");
                    }
                }
            var thin = BlundervilleSegmentContacts.Find(new CircleGeometry(new(.25f, 0, 0), .01f), Vector3.Zero, Vector3.UnitX, 0);
            Check(thin is { From: > .23 and < .25, Until: > .25 and < .27 }, "Missed a footprint between old spatial samples.");
        });

        run("Blunderville planning continuous opening below 100 milliseconds", () =>
        {
            Vector3 start = new(0, 4, 323), goal = new(0, 4, 320);
            var graph = new BlundervilleRouteGraph.Compiled(1, new() { [1] = start, [2] = goal, [3] = new(4, 31.5f, 155.2f) },
                new() { [1] = [2], [2] = [3], [3] = [] }, [3]);
            var shape = new RectangleGeometry(new(0, 4, 321.5f), 0, 3, .1f);
            BlundervilleHazard[] hazards =
            [
                new("early", shape, Epoch.AddSeconds(-2), Epoch.AddSeconds(.13), Epoch),
                new("late", shape, Epoch.AddSeconds(.95), Epoch.AddSeconds(30), Epoch),
            ];
            var plan = BlundervilleGraphPlanner.Find(graph, 1, null, hazards, start, Epoch, 6);
            Check(plan is { Steps.Count: 2 }, "Missed the narrow departure opening.");
            var departure = (plan!.Steps[0].Departure - Epoch).TotalSeconds;
            Check(departure is > 0 and < .1, $"Expected a sub-100ms opening, got {departure}.");
            Verify(graph, plan, hazards);
        });

        run("Blunderville planning shared hazard caches support parallel expansion", () =>
        {
            BlundervilleHazard[] hazards = [new("sweep", new RectangleGeometry(new(0, 4, 280), .7f, 3, 6), Epoch, Epoch.AddSeconds(4), Epoch)];
            foreach (var profile in new[] { BlundervilleFinalPlanner.Final, BlundervilleFinalPlanner.FirstARamp })
            {
                var paths = Enumerable.Range(0, 1000).Select(i => (From: new Vector3(i * .01f - 5, 4, 285), To: new Vector3(5 - i * .012f, 4, 275))).ToArray();
                var expected = paths.Select(p => new BlundervilleFinalPlanner.HazardIndex(hazards, Epoch, profile).Segment(p.From, p.To, 1, 3)).ToArray();
                var shared = new BlundervilleFinalPlanner.HazardIndex(hazards, Epoch, profile);
                Parallel.For(0, paths.Length, i => Check(shared.Segment(paths[i].From, paths[i].To, 1, 3) == expected[i], "Concurrent cache changed a segment result."));
            }
        });

        run("Blunderville planning later arrivals survive bounded waits", () =>
        {
            Vector3 start = new(6, 5, 275), middle = new(6, 6, 270), goal = new(6, 8, 260);
            var graph = new BlundervilleRouteGraph.Compiled(1, new() { [1] = start, [2] = middle, [3] = goal, [4] = new(4, 31.5f, 155.2f) },
                new() { [1] = [2], [2] = [3], [3] = [4], [4] = [] }, [4]);
            var hazard = new BlundervilleHazard("closed corridor", new CircleGeometry(new(6, 7, 265), 1), Epoch, Epoch.AddSeconds(13), Epoch);
            var plan = BlundervilleGraphPlanner.Find(graph, 1, null, [hazard], start, Epoch, 6);
            Check(plan is { Steps.Count: 3 }, "Earliest arrival incorrectly dominated the later usable arrival.");
            Check((plan!.Steps[0].Departure - Epoch).TotalSeconds > 10, "Did not wait upstream.");
            Check((plan.Steps[1].Departure - plan.Steps[1].Arrival).TotalSeconds <= 1.00001, "Exceeded the intermediate wait limit.");
            Verify(graph, plan, [hazard]);
        });

        run("Blunderville planning cold and slow worker budgets", () =>
        {
            var budget = new BlundervillePlanningBudget();
            budget.Observe(1.2, true);
            Check(budget.LeadSeconds(true, .1) > 1.5, "Ignored cold graph construction.");
            for (var i = 0; i < 64; ++i) budget.Observe(.02, false);
            Check(budget.LeadSeconds(false, .1) < .5, "Kept a fixed multi-second warm budget.");
            budget.Observe(.8, false);
            Check(budget.LeadSeconds(false, .1) >= 1.2, "Did not immediately reserve a slow worker sample.");
            budget.Observe(double.NaN, false); budget.Observe(-1, false);
            Check(budget.Samples == 64 && double.IsFinite(budget.LeadSeconds(false, double.NaN)), "Invalid samples damaged the budget.");
        });

        run("Blunderville planning wave effects anchor later cycles through delayed casts", () =>
        {
            var encounter = new BlundervilleEncounter(_ => { });
            Vector3 origin = new(-6, 25.59f, 190.4f);
            encounter.Synchronize([new(1, 16353, origin)], Epoch);
            encounter.StartCast(1, new(34812, origin, 0, Epoch, Epoch.AddSeconds(1), origin), Epoch);
            encounter.Effect(1, 34812, 1, origin, 0, Epoch.AddSeconds(.7));
            var expected = Epoch.AddSeconds(.7 + BlundervilleEncounter.WaveCyclePeriod);
            var first = encounter.TraversalHazards(Epoch.AddSeconds(1)).Single(h => h.Id == "wave/cycle/1/1/0");
            Check(Math.Abs((first.From.AddSeconds(.3) - expected).TotalSeconds) < .00001, "Future wave ignored the confirmed effect clock.");
            var nextCast = Epoch.AddSeconds(6.3);
            encounter.StartCast(1, new(34812, origin, 0, nextCast, nextCast.AddSeconds(1), origin), nextCast);
            var step = encounter.Hazards(nextCast).Single(h => h.Id == "wave/1/4");
            Check(Math.Abs((step.From - expected.AddSeconds(1.8)).TotalSeconds) < .00001, "A delayed cast moved the confirmed wave clock.");
        });

    }

    private static void Verify(BlundervilleRouteGraph.Compiled graph, BlundervilleGraphPlanner.Result plan, IReadOnlyList<BlundervilleHazard> hazards)
    {
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, Epoch, BlundervilleFinalPlanner.Final);
        foreach (var (a, b) in plan.Steps.Zip(plan.Steps.Skip(1)))
        {
            Check(graph.Next[a.Node].Contains(b.Node), "Invented an edge.");
            Check(index.Stand(a.Position, (a.Arrival - Epoch).TotalSeconds, (a.Departure - Epoch).TotalSeconds), "Unsafe reconstructed wait.");
            Check(index.Segment(a.Position, b.Position, (a.Departure - Epoch).TotalSeconds, (b.Arrival - Epoch).TotalSeconds), "Unsafe reconstructed edge.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

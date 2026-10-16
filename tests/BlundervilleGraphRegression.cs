using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

internal static class BlundervilleGraphRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    internal static void Run(Action<string, Action> run)
    {
        run("Blunderville graph 202148 recorded traversal", () =>
            RecordedNavigation(BlundervilleMechanismGraph.Create(), 0, "graph-feedback-202148.json", new(.5f,4.245f,323)));
        run("Blunderville graph flexible stairs full 202148", () =>
            BlundervilleFeedback213132Regression.Stairs(.3,true,30,start: new(.5f,4.245f,323),routeGraph:BlundervilleMechanismGraph.Create(),startTime:0,file:"graph-feedback-202148.json",flexible:true));
        foreach (var x in new[] {-12f,-9f,-4f,7f,12f})
            run($"Blunderville graph flexible stairs side {x}", () =>
                BlundervilleFeedback213132Regression.Stairs(.3,false,30,start:new(x,31.5f,155.2f),routeGraph:BlundervilleMechanismGraph.Create(),startTime:0,flexible:true));
        run("Blunderville graph flexible stairs recorded outer last tread", () =>
            BlundervilleFeedback213132Regression.Stairs(.3,false,30,start:new(-14.45f,34.41f,140.4f),routeGraph:BlundervilleMechanismGraph.Create(),startTime:0,flexible:true,expectedJumps:1));
        run("Blunderville graph flexible stairs reject blocked and unsupported landings", () =>
        {
            Check(BlundervilleRouteGraph.ValidStairLane(12) && BlundervilleRouteGraph.ValidStairLane(-12), "Outer entrances rejected by a later tread's width.");
            var danger = new BlundervilleHazard("blocked stairs", new CircleGeometry(new(8,32,149),30), Epoch.AddSeconds(-1), Epoch.AddSeconds(10), Epoch);
            Check(BlundervilleStairGeometry.Choose(new(12,31.5f,153),Epoch,[danger]) == null, "Ignored blocked landing.");
            Check(BlundervilleStairGeometry.Choose(new(17,31.5f,153),Epoch,[]) == null, "Accepted outside staircase.");
        });
        run("Blunderville graph 202148 different Y still advances", () =>
            RecordedNavigation(BlundervilleMechanismGraph.Create(), 0, "graph-feedback-202148.json", new(.5f,4.245f,323), heightOffset: -1));
        run("Blunderville graph mechanism layout", () =>
        {
            var graph = BlundervilleMechanismGraph.Create();
            Console.WriteLine($"MECHANISM GRAPH nodes={graph.Nodes.Count} edges={graph.Edges.Count}");
            if (Environment.GetEnvironmentVariable("MP_GRAPH_EXPORT") is { Length: > 0 } path)
                File.WriteAllText(path, JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true }));
        });
        run("Blunderville graph mechanism recorded traversal", () =>
        {
            foreach (var phase in new[] { 0d, 2, 4 })
                RecordedNavigation(BlundervilleMechanismGraph.Create(), phase);
        });
        run("Blunderville graph speed benchmark", () => Benchmark(BlundervilleMechanismGraph.Create()));
        run("Blunderville course graphs cover the verified lanes", () =>
        {
            foreach (var course in BlundervilleCourseGraph.Editable)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var graph = BlundervilleCourseGraph.Default(course);
                var built = watch.Elapsed.TotalMilliseconds;
                Check(graph.Validate() == null, $"{course}: {graph.Validate()}");
                var json = JsonSerializer.Deserialize<BlundervilleCourseGraph>(JsonSerializer.Serialize(graph))!;
                Check(json.Validate() == null && json.Edges.Count == graph.Edges.Count, $"{course}: round trip lost edges");
                Console.WriteLine($"COURSE GRAPH {course} nodes={graph.Nodes.Count} edges={graph.Edges.Count} forks={graph.Edges.GroupBy(e => e.From).Count(g => g.Count() > 1)} built={built:F0}ms");
            }
            // The verified lanes' nodes stay on the graph in order: jumps, ramps
            // and bridge takeoffs are kept as chokepoints the grids lead into.
            void Ordered(BlundervilleCourseGraph.Compiled graph, List<int> route, IEnumerable<BlundervilleRouteNode> lane, string name)
            {
                var positions = route.Select(id => graph.Nodes[id].Position).ToList(); var at = 0;
                foreach (var node in lane)
                {
                    var found = positions.FindIndex(at, p => p == node.Position);
                    Check(found >= 0, $"{name}: route skipped {node.Position}");
                    at = found;
                }
            }
            var a = BlundervilleCourseGraph.Default(BlundervilleCourse.FirstA).Compile();
            Ordered(a, BlundervilleCourseRouter.Route(a, BlundervilleCourseRouter.Nearest(a, new(-204, 11.28f, -119)), [], Epoch, 6),
                BlundervilleRoutes.Nodes(BlundervilleCourse.FirstA, new(-204, 0, 0)).Where(n => n.Jump), "FirstA");
            var b = BlundervilleCourseGraph.Default(BlundervilleCourse.FirstB).Compile();
            foreach (var side in new[] { -1f, 1f })
                Ordered(b, BlundervilleCourseRouter.Route(b, BlundervilleCourseRouter.Nearest(b, new(side * 10, 10.03f, -123)), [], Epoch, 6),
                    BlundervilleRoutes.Nodes(BlundervilleCourse.FirstB, new(side, 0, 0)).Where(n => n.Jump || n.Position.Z < -336), $"FirstB {side}");
            var ring = BlundervilleCourseGraph.Default(BlundervilleCourse.Ring).Compile();
            var loop = BlundervilleCourseRouter.Route(ring, ring.Start, [], Epoch, 6);
            Check(loop.Count > 2 && loop[^1] == loop[0], "Ring route does not close the loop.");
            var delivery = BlundervilleCourseGraph.Default(BlundervilleCourse.Delivery).Compile();
            foreach (var pad in new[] { BlundervilleCourseGraph.WestPad, BlundervilleCourseGraph.EastPad, BlundervilleCourseGraph.NorthPad })
            {
                var hub = new Vector3(-200, 5.42f, 252);
                var route = BlundervilleCourseRouter.Route(delivery, BlundervilleCourseRouter.Nearest(delivery, hub), [], Epoch, 6, pad);
                Check(delivery.Nodes[route[^1]].Position == pad, $"Delivery to {pad} ended at {delivery.Nodes[route[^1]].Position}.");
                var back = BlundervilleCourseRouter.Route(delivery, BlundervilleCourseRouter.Nearest(delivery, pad), [], Epoch, 6, BlundervilleRaceState.PickupCenter);
                Check(Vector3.Distance(delivery.Nodes[back[^1]].Position, hub) < 1, $"Return from {pad} did not reach the hub.");
            }
        });
        if (Environment.GetEnvironmentVariable("MP_GRAPH_CASE") is { Length: > 0 } single)
            run("Blunderville graph single case", () =>
            {
                var parts = single.Split('@'); var file = parts[0]; var phase = double.Parse(parts[1]);
                var spawn = file == "feedback-234521.json" ? new Vector3(-1.511f, 4.227f, 322.988f) : new Vector3(.473f, 4.227f, 322.988f);
                Traverse(BlundervilleMechanismGraph.Create(), phase, file, spawn);
            });
        if (Environment.GetEnvironmentVariable("MP_GRAPH_HAZ") is { Length: > 0 } hazCase)
            run("Blunderville graph hazard dump", () =>
            {
                var parts = hazCase.Split('@'); using var replay = new BlundervilleRingRegression.Recording("final", parts[0]);
                var t = double.Parse(parts[1]); replay.Advance(t); var now = BlundervilleRingRegression.Epoch.AddSeconds(t);
                var probe = new Vector3(float.Parse(parts[2]), 0, float.Parse(parts[3]));
                foreach (var h in replay.Encounter.TraversalHazards(now).Where(h => h.Geometry.Contains(probe with { Y = h.Geometry.Position.Y }, .3f)).OrderBy(h => h.From))
                    Console.WriteLine($"HAZ {h.Id} {(h.From - now).TotalSeconds:F2}..{(h.Until - now).TotalSeconds:F2} at {h.Geometry.Position}");
            });
        run("Blunderville graph 043407 recorded traversal", () =>
            RecordedNavigation(BlundervilleMechanismGraph.Create(), 0, "graph-feedback-043407.json", new(.473f,4.227f,322.988f)));
        run("Blunderville graph latest recorded traversal", () =>
            RecordedNavigation(BlundervilleMechanismGraph.Create(), 0, "graph-feedback-194307.json", new(.5f,4.245f,323)));
        run("Blunderville graph latest complete course", () =>
        {
            var elapsed = BlundervilleFeedback213132Regression.Stairs(.3,true,30,
                start: new(.5f,4.245f,323), routeGraph: BlundervilleMechanismGraph.Create(), startTime: 0, file: "graph-feedback-194307.json");
            Console.WriteLine($"LATEST COMPLETE elapsed={elapsed:F2}");
            Check(elapsed < 48, "Latest course missed the early finish window.");
        });
        run("Blunderville graph spawn joins only authored start", () =>
        {
            foreach (var x in new[] { -.5f, .5f })
            {
                var planner = new BlundervilleGraphPlanner(); planner.ConfigureCourse(new() { Graph = Branches() });
                var p = new Vector3(x,4.245f,323); var decision = planner.Apply(Normal(p),State(),[],p,Epoch);
                Check(!decision.Hold && decision.Target == Branches().Nodes[0].Position, $"Spawn {x}: {decision.Hint}, {decision.Target} did not join {Branches().Nodes[0].Position}.");
                planner.Reset();
            }
            var outside = new BlundervilleGraphPlanner(); outside.ConfigureCourse(new() { Graph = Branches() });
            var offEdge = new Vector3(.5f,4,300);
            Check(outside.Apply(Normal(offEdge),State(),[],offEdge,Epoch).Hold, "Spawn join created a mid-course shortcut."); outside.Reset();
        });
        run("Blunderville graph separates entrance arrival from stair departure", () =>
        {
            Vector3 approach = new(-4,31.1f,158), exit = new(-4,31.38f,EndZ);
            var graph = new BlundervilleRouteGraph.Compiled(1, new() { [1] = approach, [2] = exit }, new() { [1] = [2], [2] = [] }, [2]);
            var hazard = new BlundervilleHazard("blocked-stairs",new CircleGeometry(new(-4,33,145),2),Epoch,Epoch.AddSeconds(4),Epoch);
            var plan = BlundervilleGraphPlanner.Find(graph,1,null,[hazard],approach,Epoch,6);
            Console.WriteLine("HANDOFF " + string.Join(";", plan?.Steps.Select(s => $"{s.Node}: {(s.Arrival-Epoch).TotalSeconds:F3}/{(s.Departure-Epoch).TotalSeconds:F3}") ?? []));
            Check(plan != null && (plan.Steps[^1].Arrival-Epoch).TotalSeconds < 1
                && (plan.Steps[^1].Departure-plan.Steps[^1].Arrival).TotalSeconds > 1, "Entrance wait was incorrectly moved upstream.");
            var planner = new BlundervilleGraphPlanner(); planner.ConfigureCourse(new() { Graph = Branches() }); var state = State();
            Check(planner.Apply(Normal(exit),state,[hazard],exit,Epoch).Hold && planner.ExitLane == null, "Committed blocked stair ascent.");
            Check(!planner.Apply(Normal(exit),state,[],exit,Epoch.AddSeconds(5)).Hold && planner.ExitLane == -4, "Did not release the clear stair ascent.");
            planner.Reset();
        });
        run("Blunderville graph complete recorded stair handoff", () =>
        {
            foreach (var phase in new[] { 27.6, 33.6, 39.6 })
            {
                var graph = BlundervilleMechanismGraph.Create();
                var elapsed = BlundervilleFeedback213132Regression.Stairs(.3,true,30,
                    start: graph.Nodes.Single(n => n.X == 13.5f && n.Z == 184).Position, routeGraph: graph, startTime: phase);
                Console.WriteLine($"GRAPH COMPLETE phase={phase} elapsed={elapsed:F2}");
            }
        });
        run("Blunderville graph cached hazard checks match sampled checks", () =>
        {
            using var replay = new BlundervilleRingRegression.Recording("final", "feedback-213132.json");
            replay.Advance(20); var now = BlundervilleRingRegression.Epoch.AddSeconds(20);
            var hazards = replay.Encounter.TraversalHazards(now).ToArray();
            var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
            var graph = BlundervilleMechanismGraph.Create().Compile();
            foreach (var (from, targets) in graph.Next)
                foreach (var to in targets)
                    foreach (var t in new[] { 0d, .073, 1.27, 2.8, 5.53, 9.2 })
                    {
                        var a = graph.Positions[from]; var b = graph.Positions[to]; var duration = Distance(a,b)/6;
                        var sampled = true;
                        var count = Math.Max(2, (int)MathF.Ceiling(Distance(a, b) / .5f) + 1);
                        var half = duration / (count - 1) / 2;
                        for (var sample = 0; sample < count && sampled; ++sample)
                        {
                            var fraction = sample / (float)(count - 1);
                            var point = Vector3.Lerp(a, b, fraction);
                            var instant = t + fraction * duration;
                            foreach (var hazard in hazards)
                            {
                                var lead = hazard.Id.StartsWith("wave/", StringComparison.Ordinal) || hazard.Id.StartsWith("cast/", StringComparison.Ordinal)
                                    ? BlundervilleFinalPlanner.Final.HazardWaveLead : BlundervilleFinalPlanner.Final.HazardLead;
                                if ((hazard.Until - now).TotalSeconds + .1 >= instant - half
                                    && (hazard.From - now).TotalSeconds - lead <= instant + BlundervilleFinalPlanner.Lag + half
                                    && hazard.Geometry.Contains(point, BlundervilleFinalPlanner.Clearance)) { sampled = false; break; }
                            }
                        }
                        Check(index.CachedSegment(a, b, t, duration) == sampled, "Cached window changed the independent sampled safety contract.");
                    }
        });
        run("Blunderville graph authored benchmark", () =>
        {
            var graph = JsonSerializer.Deserialize<BlundervilleRouteGraph>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "authored-graph-1015.json")))!;
            Check(graph.Validate() == null, graph.Validate() ?? "saved graph");
            var compiled = graph.Compile();
            using var replay = new BlundervilleRingRegression.Recording("final", "feedback-213132.json");
            foreach (var t in new[] { 5d, 15, 25, 35 })
            {
                replay.Advance(t); var now = BlundervilleRingRegression.Epoch.AddSeconds(t);
                var hazards = replay.Encounter.TraversalHazards(now).ToArray();
                foreach (var z in new[] { 290f, 275, 260, 236, 184 })
                {
                    var at = compiled.Positions.OrderBy(p => MathF.Abs(p.Value.Z - z)).First().Key;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var result = BlundervilleGraphPlanner.Find(compiled, at, null, hazards, compiled.Positions[at], now, 6);
                    Console.WriteLine($"AUTHORED t={t} z={z} ms={watch.Elapsed.TotalMilliseconds:F2} expanded={result?.Expanded} end={result?.Steps[^1].Position} duration={(result?.Steps[^1].Arrival-now)?.TotalSeconds:F2}");
                }
            }
            if (Environment.GetEnvironmentVariable("MP_GRAPH_PROBE") == "1")
                foreach (var z in new[] { 290f,278,275,273,267,262,260,256,248,236,230,220,214,210,206,202,198,194,190,184,180,176,170,164,160,155.2f })
                    Console.WriteLine($"FLOOR z={z} x=" + string.Join(",", Enumerable.Range(-30,61).Select(i => i*.5f).Where(x => Floor.Contains(new(x,0,z), .3f))));
        });
        run("Blunderville graph migration and editing preserve explicit edges", () =>
        {
            var old = BlundervilleFinalRouteSettings.FromEdit(new BlundervilleFinalRouteSettings().Edit());
            var graph = old.EditGraph();
            Check(graph.Validate() == null, graph.Validate() ?? "migration");
            var expected = old.AssignedRoute()!.Points.Select(p => p.Position);
            Check(graph.Nodes.Select(n => n.Position).SequenceEqual(expected), "Migration changed authored points.");
            var copy = graph.Copy(); copy.Nodes[0].X = 1;
            Check(graph.Nodes[0].X == 0, "Draft changed live graph.");
            var read = JsonSerializer.Deserialize<BlundervilleRouteGraph>(JsonSerializer.Serialize(graph))!;
            Check(read.Validate() == null && read.Edges.Count == graph.Edges.Count, "Round trip lost edges.");
            graph.RemoveNode(2);
            Check(!graph.Edges.Any(e => e.From == 1 && e.To == 3) && graph.Validate() != null, "Deleting a node invented a shortcut.");
        });
        run("Blunderville graph branches select connected safe successor", () =>
        {
            var graph = Branches(); var compiled = graph.Compile(); var p = graph.Nodes.Single(n => n.Id == 2).Position;
            var hazard = new BlundervilleHazard("blocked-west", new CircleGeometry(new(-4, 5, 278), 5), Epoch, Epoch.AddSeconds(40), Epoch);
            var result = BlundervilleGraphPlanner.Find(compiled, 2, null, [hazard], p, Epoch, 6);
            Check(result != null && result.Steps[1].Position.X > 0, "Did not choose safe outgoing edge.");
            CheckEdges(compiled, result!);
            var left = compiled.Next[2].Single(id => compiled.Positions[id].X < 0);
            Check(BlundervilleGraphPlanner.Find(compiled, 2, left, [hazard], p, Epoch, 6) == null, "Switched branches in the middle of a committed edge.");
            Check(BlundervilleGraphPlanner.Find(compiled, 2, graph.Nodes[^1].Id, [], p, Epoch, 6) == null, "Accepted an unconnected target.");
        });
        run("Blunderville graph topology permits joins and cycles without implicit crossings", () =>
        {
            var graph = Branches(); var nextId = graph.Nodes.Max(n => n.Id) + 1;
            graph.Nodes.Add(new() { Id = nextId, X = 1, Z = 300, Mechanism = "可选绕行" });
            graph.Connect(1, nextId); graph.Connect(nextId, 2); graph.Connect(nextId, 1);
            Check(graph.Validate() == null, graph.Validate() ?? "Cycle with reachable exit rejected.");
            var compiled = graph.Compile();
            Check(compiled.Next[nextId].Length == 2, "Unexpected connection at crossing.");
            var result = BlundervilleGraphPlanner.Find(compiled, 1, null, [], compiled.Positions[1], Epoch, 6);
            Check(result != null && result.Expanded <= 12000, "Cyclic graph did not terminate."); CheckEdges(compiled, result!);
            graph.Nodes.Add(new() { Id = nextId + 1, X = 2, Z = 300 }); graph.Connect(1, nextId + 1);
            Check(graph.Validate() != null, "Accepted a dead-end branch.");
        });
        run("Blunderville graph waits upstream before unsafe junction", () =>
        {
            var graph = Branches().Compile();
            var at = graph.Positions.OrderBy(p => Vector3.DistanceSquared(p.Value with { Y = 0 }, new(-5, 0, 275))).First().Key;
            var hazard = new BlundervilleHazard("timed-block", new CircleGeometry(new(-6, 6, 268), 2), Epoch, Epoch.AddSeconds(3), Epoch);
            var result = BlundervilleGraphPlanner.Find(graph, at, null, [hazard], graph.Positions[at], Epoch, 6);
            Check(result != null && result.Steps.Sum(s => (s.Departure-s.Arrival).TotalSeconds) > 1
                && result.Steps.Where(s => s.Departure>s.Arrival).All(s => s.Position.Z >= 272.5f), "Did not wait upstream of the unsafe junction.");
            CheckEdges(graph, result!);
        });
        run("Blunderville graph multiple custom stair entrances", () =>
        {
            var graph = Branches(); var right = graph.Nodes.Single(n => n.Exit && n.X > 0);
            var from = graph.Edges.Single(e => e.To == right.Id).From;
            foreach (var x in new[] { 5.5f, 7f })
            {
                var id = graph.Nodes.Max(n => n.Id) + 1;
                graph.Nodes.Add(new() { Id = id, X = x, Z = EndZ, Mechanism = "自定台阶" }); graph.Connect(from, id);
            }
            Check(graph.Validate() == null, graph.Validate() ?? "Custom entrances rejected.");
            Check(graph.Compile().Exits.Count == 4, "Entrances collapsed to two.");
            foreach (var x in new[] { -6f, 5.5f, 7f })
            {
                Check(BlundervilleRouteGraph.ValidStairLane(x), "Valid non-default stair lane rejected.");
                var jump = new BlundervilleJumpPlanner().Plan(BlundervilleCourse.Final, Epoch, new(x, 31.5f, 155.2f), new(x, 31.5f, 154), true, false, x);
                Check(jump?.Corridor.Takeoff.X == x && jump.Corridor.Landing.X == x, "Jump snapped to a legacy ±4 point.");
            }
            Check(!BlundervilleRouteGraph.ValidStairLane(0), "Middle gap accepted as a stair lane.");
        });
        run("Blunderville graph actual dispatch reselects at fork", () => Navigation());
        run("Blunderville graph stale forecast and save reset", () =>
        {
            var planner = new BlundervilleGraphPlanner(); var graph = Branches();
            planner.ConfigureCourse(new() { Graph = graph }); var state = State(); var p = new Vector3(0, 4, 290);
            planner.Apply(Normal(p), state, [], p, Epoch); planner.Pending!.GetAwaiter().GetResult();
            var hazard = new BlundervilleHazard("blocked-all", new CircleGeometry(p, 30), Epoch, Epoch.AddSeconds(60), Epoch);
            Check(planner.Apply(Normal(p), state, [hazard], p, Epoch.AddSeconds(.1)).Hold, "Published a stale unsafe plan.");
            planner.ConfigureCourse(new());
            Check(planner.Apply(Normal(p), state, [], p, Epoch.AddSeconds(.2)).Hold && planner.Pending == null, "Unassigned graph revived old movement.");
            planner.Reset();
        });
        run("Blunderville graph recorded forecasts stay on authored edges", () =>
        {
            using var replay = new BlundervilleRingRegression.Recording("final", "feedback-213132.json");
            var graph = Branches(); var compiled = graph.Compile(); var watch = System.Diagnostics.Stopwatch.StartNew();
            var count = 0; var accepted = 0; var expanded = 0;
            foreach (var time in new[] { 5d, 15, 25, 32.543, 40, 50 })
            {
                replay.Advance(time); var now = BlundervilleRingRegression.Epoch.AddSeconds(time);
                var hazards = replay.Encounter.TraversalHazards(now).ToArray();
                foreach (var z in new[] { 290f, 275, 236, 184 })
                {
                    var at = compiled.Positions.OrderBy(p => MathF.Abs(p.Value.Z - z)).First().Key;
                    var p = compiled.Positions[at]; var result = BlundervilleGraphPlanner.Find(compiled, at, null, hazards, p, now, 6); ++count;
                    if (result == null) continue;
                    ++accepted; expanded = Math.Max(expanded, result.Expanded); CheckEdges(compiled, result);
                    var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
                    foreach (var (a, b) in result.Steps.Zip(result.Steps.Skip(1)))
                    {
                        Check(index.Segment(a.Position, b.Position, (a.Departure - now).TotalSeconds, (b.Arrival - now).TotalSeconds), "Unsafe recorded edge accepted.");
                        if (a.Departure > a.Arrival) Check(index.Stand(a.Position, (a.Arrival - now).TotalSeconds, (a.Departure - now).TotalSeconds), "Unsafe recorded wait accepted.");
                    }
                }
            }
            Console.WriteLine($"GRAPH RECORDED accepted={accepted}/{count} maxExpanded={expanded} total={watch.Elapsed.TotalMilliseconds:F1}ms (includes replay/verification)");
            Check(accepted > 0, "No recorded graph path was usable.");
        });
    }

    internal static BlundervilleRouteGraph Branches()
    {
        var graph = BlundervilleRouteGraph.FromSections(new() { Routes = [Reference(-1)] });
        var previous = 2;
        foreach (var point in Reference(1).Points.Skip(2))
        {
            var id = graph.Nodes.Max(n => n.Id) + 1;
            graph.Nodes.Add(new() { Id = id, X = point.X, Z = point.Z, Mechanism = "另一分支" });
            graph.Connect(previous, id); previous = id;
        }
        Check(graph.Validate() == null, graph.Validate() ?? "Fixture invalid"); return graph;
    }

    private static void RecordedNavigation(BlundervilleRouteGraph graph, double phase, string file = "feedback-213132.json", Vector3? start = null, float heightOffset = 0)
    {
        var run = Traverse(graph, phase, file, start, heightOffset);
        Check(run.Finished, "Mechanism graph failed recorded traversal.");
        Check(run.Hits.Count == 0, "Graph contacted a recorded impact.");
    }

    internal static readonly string[] SpeedFixtures = ["feedback-213132.json", "feedback-234521.json", "graph-feedback-043407.json", "graph-feedback-194307.json", "graph-feedback-202148.json"];

    // Spawn-to-stair-entrance time across every recorded Final and three
    // start offsets. Prints the totals that speed work is judged by; any
    // recorded contact or unfinished run fails.
    internal static (double Total, int Runs) Benchmark(BlundervilleRouteGraph graph, bool quiet = true)
    {
        var total = 0d; var runs = 0; var failures = new List<string>();
        var jobs = SpeedFixtures.SelectMany(f => new[] { 0d, 1.5, 3 }.Select(phase => (File: f, Phase: phase))).ToArray();
        var results = new TraversalRun[jobs.Length];
        Parallel.For(0, jobs.Length, i =>
        {
            var spawn = jobs[i].File == "feedback-234521.json" ? new Vector3(-1.511f, 4.227f, 322.988f) : new Vector3(.473f, 4.227f, 322.988f);
            results[i] = Traverse(graph, jobs[i].Phase, jobs[i].File, spawn, quiet: true);
        });
        for (var i = 0; i < jobs.Length; ++i)
        {
            var r = results[i];
            Console.WriteLine($"BENCH {jobs[i].File} phase={jobs[i].Phase} finished={r.Finished} elapsed={r.Elapsed:F2} stopped={r.Stopped:F2} hits={string.Join(';', r.Hits)}");
            if (!r.Finished || r.Hits.Count > 0) failures.Add($"{jobs[i].File}@{jobs[i].Phase}");
            total += r.Elapsed; ++runs;
        }
        Console.WriteLine($"BENCH TOTAL {total:F2}s over {runs} runs, mean {total / runs:F2}s, failures={string.Join(',', failures)}");
        Check(failures.Count == 0, "Benchmark run failed: " + string.Join(',', failures));
        return (total, runs);
    }

    internal sealed record TraversalRun(bool Finished, double Elapsed, double Stopped, List<string> Hits);

    internal static TraversalRun Traverse(BlundervilleRouteGraph graph, double phase, string file = "feedback-213132.json", Vector3? start = null, float heightOffset = 0, bool quiet = false)
    {
        using var replay = new BlundervilleRingRegression.Recording("final", file);
        var now = BlundervilleRingRegression.Epoch;
        var move = new BlundervilleRingRegression.Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20017, 0, 0, 0, now);
        var planner = new BlundervilleGraphPlanner(); planner.ConfigureCourse(new() { Graph = graph });
        var p = start ?? graph.Nodes.Single(n => n.Id == graph.Start).Position;
        double ready = 0, scan = 0, stopped = 0; Task? pending = null; var histories = new Dictionary<string, int>();
        var end = 0d; var history = new List<(double T, Vector3 P)>();
        BlundervilleGraphPlanner.Result? tracedPlan = null;
        (double T, Vector3 P, string Status)? stopStart = null; var stops = new List<string>();
        for (var tick = 0; tick < 1800; ++tick)
        {
            var t = phase + tick / 30d; end = t-phase; now = BlundervilleRingRegression.Epoch.AddSeconds(t);
            move.Now = now; api.UtcNowForTest = now;
            if (t >= scan && t >= ready)
            {
                scan = t + .099; replay.Advance(t);
                var decision = planner.Apply(Normal(p), state, replay.Encounter.TraversalHazards(now).ToArray(), p, now, nav.GetDirectTraversalExecution(now));
                if (Environment.GetEnvironmentVariable("MP_GRAPH_TRACE") == "1" && planner.Plan is { } plan && tracedPlan != plan)
                {
                    tracedPlan = plan;
                    Console.WriteLine($"PLAN {t:F3} " + string.Join(";", plan.Steps.Select(s => $"{s.Node} {s.Position.X:F2}/{s.Position.Z:F2} {(s.Arrival-BlundervilleRingRegression.Epoch).TotalSeconds:F4}/{(s.Departure-BlundervilleRingRegression.Epoch).TotalSeconds:F4}")));
                }
                if (planner.Pending is { } job && job != pending) { pending = job; job.GetAwaiter().GetResult(); ready = t + .08; }
                if (planner.WakeAt is { } wake) scan = Math.Min(scan, (wake - BlundervilleRingRegression.Epoch).TotalSeconds);
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
                if (decision.Hold) histories[decision.Hint] = histories.GetValueOrDefault(decision.Hint) + 1;
                if (Environment.GetEnvironmentVariable("MP_GRAPH_SCAN") == "1")
                    Console.WriteLine($"SCAN {t-phase:F2} p=({p.X:F1},{p.Z:F1}) hold={decision.Hold} target={decision.Target} hint={decision.Hint} at={planner.CurrentNode} to={planner.NextNode} inv={planner.LastInvalidation} diag={planner.Diagnostics}");
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(new(p, 0, 6, true, false, 0, true, snapshot.Version), new(6,6,0,false,6,6), now);
            var next = move.Advance(p, 1f/30, 6);
            if (Distance(next,p) < .01) { stopped += 1d/30; stopStart ??= (t, p, planner.Status); }
            else if (stopStart is { } s0) { if (t - s0.T >= .1) stops.Add($"{s0.T-phase:F2}+{t-s0.T:F2}s z={s0.P.Z:F1} x={s0.P.X:F1} {s0.Status}"); stopStart = null; }
            if (Environment.GetEnvironmentVariable("MP_GRAPH_TRACE") == "1" && tick % 30 == 0)
                Console.WriteLine($"TRACE {t:F1} p={p} status={planner.Status} invalid={planner.LastInvalidation} exec={runtime.MovementStatus.PhaseText} direct={nav.GetDirectTraversalExecution(now)}");
            if (move.GetStatus().CurrentPoint is { } movingPoint) Check(movingPoint.IgnoreY, "Graph dispatch must ignore rendering Y.");
            p = next; p.Y = BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, p.Z) + heightOffset;
            history.Add((t + 1d/30, p));
            Check(Floor.Contains(p, 0), "Recorded graph left floor.");
            if (p.Z <= EndZ+.12f) break;
        }
        var hits = new List<string>();
        foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit"))
        {
            var t = e[0].GetDouble(); if (t < phase || t > phase + end) continue;
            if (!BlundervilleCatalog.TryGet(e[3].GetUInt32(), out var rule) || rule.Shape == null) continue;
            var v = e[4]; var shape = rule.Shape.Resolve(new(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle()),e[5].GetSingle(),null);
            var sample = history.MinBy(h => Math.Abs(h.T-t));
            if (shape?.Contains(sample.P,.3f) == true) hits.Add($"{e[3]}@{t:F2} p={sample.P} center={shape.Position}");
        }
        if (!quiet)
        {
            foreach (var stop in stops) Console.WriteLine($"STOP {stop}");
            Console.WriteLine($"HITS {string.Join(';',hits)}");
            Console.WriteLine($"MECHANISM RUN phase={phase} elapsed={end:F2} stop={stopped:F2} p={p} dispatch={runtime.MovementStatus.DispatchCount} status={planner.Status}");
            if (p.Z > EndZ+.12f) foreach (var h in histories.OrderByDescending(h=>h.Value).Take(6)) Console.WriteLine($"HOLD {h}");
        }
        planner.Reset(); nav.Clear();
        return new(p.Z <= EndZ+.12f, end, stopped, hits);
    }

    private static void Navigation()
    {
        var now = Epoch; var move = new BlundervilleRingRegression.Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = State();
        var graph = Branches(); var compiled = graph.Compile(); var planner = new BlundervilleGraphPlanner();
        planner.ConfigureCourse(new() { Graph = graph });
        var encounter = new BlundervilleEncounter(_ => { }); var p = new Vector3(0, 4, StartZ); var reachedFork = false;
        for (var tick = 0; tick < 1800; ++tick)
        {
            now = Epoch.AddSeconds(tick * .05); move.Now = now; api.UtcNowForTest = now;
            // The branch is blocked after the first plan was created, before reaching the fork.
            BlundervilleHazard[] hazards = p.Z < 292 ? [new("block-west", new CircleGeometry(new(-4, 5, 278), 5), Epoch, Epoch.AddSeconds(40), Epoch)] : [];
            var decision = planner.Apply(Normal(p), state, hazards, p, now, nav.GetDirectTraversalExecution(now));
            planner.Pending?.GetAwaiter().GetResult();
            reachedFork |= planner.CurrentNode == 2;
            if (planner.CurrentNode is { } at && planner.NextNode is { } to)
                Check(compiled.Next[at].Contains(to), $"Dispatch invented edge {at}->{to}");
            nav.Apply(state, decision, encounter, now, p, true, false, false);
            var snapshot = runtime.Manager.BuildSnapshot(now);
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(new(p, 0, 6, true, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), now);
            p = move.Advance(p, .05f, 6); p.Y = BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, p.Z);
            Check(Floor.Contains(p, 0), "Dispatch left the floor.");
            if (p.Z < 285 && p.Z > 260) Check(p.X > 0, "Used blocked west branch after fork.");
            if (p.Z <= EndZ + .1f) break;
        }
        Console.WriteLine($"GRAPH NAV p={p} fork={reachedFork} dispatch={runtime.MovementStatus.DispatchCount} status={planner.Status}");
        Check(reachedFork && p.Z <= EndZ + .1f && p.X > 0, "Graph traversal did not reach the connected exit.");
        planner.Reset(); nav.Clear(); Check(!move.IsRunning, "Movement leaked.");
    }

    private static void CheckEdges(BlundervilleRouteGraph.Compiled graph, BlundervilleGraphPlanner.Result result)
    {
        foreach (var (a, b) in result.Steps.Zip(result.Steps.Skip(1)))
            Check(graph.Next[a.Node].Contains(b.Node), $"Plan invented edge {a.Node}->{b.Node}.");
    }
    private static BlundervilleRaceState State()
    { var state = new BlundervilleRaceState(); state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20017, 0, 0, 0, Epoch); return state; }
    private static BlundervilleRaceDecision Normal(Vector3 p) => new(p - Vector3.UnitZ * 10, false, false, null, "normal");
    private static void Check(bool ok, string error) { if (!ok) throw new InvalidOperationException(error); }
}

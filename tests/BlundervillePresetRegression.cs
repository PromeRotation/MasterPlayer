using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Scripting;
using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

internal static class BlundervillePresetRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        foreach (var side in new[] { -1, 1 })
        {
            run($"Blunderville route edit replaces existing navigation {side}", () =>
            {
                var settings = new BlundervilleFinalRouteSettings();
                var original = BlundervilleRoutes.Nodes(BlundervilleCourse.Final, new(side, 0, 0));
                var draft = SectionDraft(side);
                Check(draft.Routes.Count == 5 && BlundervilleFinalRouteSettings.ValidateSections(draft) == null,
                    $"Mechanism sections count={draft.Routes.Count}: {BlundervilleFinalRouteSettings.ValidateSections(draft)}");
                var point = draft.Routes.SelectMany(r => r.Points).Single(p => p.Z == 273);
                point.X = side * 6.5f;
                Check(settings.AssignedRoute() == null, "Unsaved draft became executable.");
                var edited = BlundervilleFinalRouteSettings.FromEdit(draft);
                var read = JsonSerializer.Deserialize<BlundervilleFinalRouteSettings>(JsonSerializer.Serialize(edited))!;
                var nodes = read.Nodes(new(side, 0, 0));
                Check(nodes.Any(n => n.Position == point.Position), "Saved edit missing from base navigation.");
                Check(nodes.Where(n => n.Position.Z < EndZ).SequenceEqual(original.Where(n => n.Position.Z < EndZ)), "Edit altered stair or finish behavior.");
                var race = new BlundervilleRacePlanner(); var state = State();
                var p = draft.Routes[0].Points[^1].Position;
                race.ConfigureFinalRoutes(settings); race.Update(state, Epoch, p, true, false, false, null);
                race.ConfigureFinalRoutes(read);
                Check(race.Update(state, Epoch, p, true, false, false, null).Target == point.Position, "Base navigation retained an old target after save.");
                var timing = new BlundervillePresetPlanner(); timing.ConfigureCourse(read);
                timing.Apply(Normal(p), state, [], p, Epoch); timing.Pending!.GetAwaiter().GetResult();
                var decision = timing.Apply(Normal(p), state, [], p, Epoch.AddSeconds(.05));
                Check(decision.ContinuousPath?.Contains(point.Position) == true, "Timing planner did not use the edited waypoint.");
                timing.ConfigureCourse(settings);
                Check(timing.Apply(Normal(p), state, [], p, Epoch.AddSeconds(.1)).Hold, "Restoring route retained an old execution plan.");
                timing.Reset();
            });
            run($"Blunderville route edit actual navigation dispatch {side}", () => Navigation(side, true));
        }
        run("Blunderville route edit sections split merge and shared junction", () =>
        {
            var draft = SectionDraft(-1);
            var before = BlundervilleFinalRouteSettings.Join(draft).Points.Select(p => p.Position).ToArray();
            Check(BlundervilleFinalRouteSettings.Split(draft, 0, 1), "Cannot split mechanism.");
            Check(BlundervilleFinalRouteSettings.Join(draft).Points.Select(p => p.Position).SequenceEqual(before), "Splitting changed route geometry.");
            Check(BlundervilleFinalRouteSettings.MergeNext(draft, 0), "Cannot merge mechanism.");
            Check(BlundervilleFinalRouteSettings.Join(draft).Points.Select(p => p.Position).SequenceEqual(before), "Merging changed route geometry.");
            var end = draft.Routes[0].Points.Count - 1;
            BlundervilleFinalRouteSettings.SetPoint(draft, 0, end, new() { X = -5.1f, Z = 275 });
            Check(draft.Routes[1].Points[0].X == -5.1f && BlundervilleFinalRouteSettings.ValidateSections(draft) == null, "Joint edit disconnected adjacent mechanism.");
            draft.Routes[1].Points[0].X -= 1;
            Check(BlundervilleFinalRouteSettings.ValidateSections(draft) != null, "Disconnected mechanisms auto-connected.");
        });
        run("Blunderville route edit crosses sides only where authored", () =>
        {
            var draft = SectionDraft(-1);
            for (var section = 3; section < draft.Routes.Count; ++section)
                for (var point = section == 3 ? 1 : 0; point < draft.Routes[section].Points.Count; ++point)
                    draft.Routes[section].Points[point].X *= -1;
            Check(BlundervilleFinalRouteSettings.ValidateSections(draft) == null, "Authored cross-course segment rejected.");
            Navigation(1, true, BlundervilleFinalRouteSettings.FromEdit(draft));
        });
        run("Blunderville route edit never chooses alternate or unsaved route", () =>
        {
            var state = State(); var p = new Vector3(0, 4, 290); var normal = Normal(p);
            var planner = new BlundervillePresetPlanner();
            planner.ConfigureCourse(new() { Left = Reference(-1).Points, Right = Reference(1).Points });
            Check(planner.Apply(normal, state, [], p, Epoch).Hold && planner.Pending == null, "Legacy alternatives were silently enabled.");
            var assigned = BlundervilleFinalRouteSettings.FromEdit(SectionDraft(-1));
            planner.ConfigureCourse(assigned);
            var hazard = Hazard(new(-4, 5, 278), 5, 0, 40);
            Check(BlundervillePresetPlanner.Find([Route(Reference(1))], [hazard], p, Epoch, 6) != null, "Alternate route must be open for this test.");
            planner.Apply(normal, state, [hazard], p, Epoch); planner.Pending!.GetAwaiter().GetResult();
            Check(planner.Apply(normal, state, [hazard], p, Epoch.AddSeconds(.05)).Hold, "Selected an unauthorized alternate route.");
            planner.Reset();
            p = new(1, 4, 300);
            Check(planner.Apply(Normal(p), state, [], p, Epoch.AddSeconds(.1)).Hold && planner.Pending == null, "Created a 1m shortcut back to route.");
            var nodesFromWrongSide = assigned.Nodes(new(4, 31, 155));
            Check(nodesFromWrongSide.Last().Position.X < 0, "Player position mirrored assigned finish.");
            Check(planner.Apply(Normal(new(4, 31, 155)), state, [], new(4, 31, 155), Epoch.AddSeconds(.2)).Hold, "Wrong stair lane accepted.");
        });
        foreach (var side in new[] { -1, 1 })
        {
            run($"Blunderville preset reference terrain {side}", () =>
            {
                var route = Reference(side);
                Check(Validate(route) == null, Validate(route) ?? "");
                var compiled = Compile(route);
                Check(Stops.All(z => compiled.Any(p => MathF.Abs(p.Z - z) < .01f)), "Missing safe-strip split.");
                Check(compiled.Zip(compiled.Skip(1)).All(p => Link(p.First, p.Second)), "Compilation crossed terrain.");
            });
            run($"Blunderville preset full route follows vertices {side}", () => Traverse(side));
            run($"Blunderville preset actual navigation dispatch {side}", () => Navigation(side));
        }
        run("Blunderville preset rejects holes and invalid endpoints", () =>
        {
            var route = Reference(-1); route.Points = [new() { X = 0, Z = StartZ }, new() { X = -4, Z = EndZ }];
            Check(Validate(route) != null, "Straight path across pits accepted.");
            route = Reference(-1); route.Points[^1].X = 0;
            Check(Validate(route) != null, "Unsupported stair landing accepted.");
            route = Reference(-1); route.Points[2].Z = 324;
            Check(Validate(route) != null, "Backwards route accepted.");
            route = Reference(-1); route.Points[2].X = float.NaN;
            Check(Validate(route) != null, "NaN accepted.");
        });
        run("Blunderville preset settings round trip and draft isolation", () =>
        {
            var library = Library(-1); var copy = library.Copy(); copy.Routes[0].Points[0].X = 2;
            Check(library.Routes[0].Points[0].X == 0, "Draft changed live settings.");
            var read = JsonSerializer.Deserialize<BlundervilleRouteLibrary>(JsonSerializer.Serialize(library))!;
            Check(read.UsePresets && read.Routes[0].Enabled && Validate(read.Routes[0]) == null, "Route did not survive reload.");
        });
        run("Blunderville preset disabled and empty selection", () =>
        {
            var planner = new BlundervillePresetPlanner(); var state = State(); var p = new Vector3(0, 4, StartZ);
            var normal = Normal(p);
            planner.Configure(new()); Check(planner.Apply(normal, state, [], p, Epoch) == normal, "Off mode changed old behavior.");
            planner.Configure(new() { UsePresets = true });
            var hold = planner.Apply(normal, state, [], p, Epoch);
            Check(hold.Hold && !hold.DirectTraversal && planner.Pending == null, "Empty selection started free search.");
            var library = Library(-1); library.Routes[0].Enabled = false; planner.Configure(library);
            Check(planner.Apply(normal, state, [], p, Epoch).Hold && planner.Pending == null, "Disabled route selected.");
            planner.Configure(new() { Version = 99, UsePresets = true, Routes = [Reference(-1)] });
            Check(planner.Apply(normal, state, [], p, Epoch).Hold && planner.Pending == null, "Unknown schema accepted.");
        });
        run("Blunderville preset selects unblocked approved route", () =>
        {
            var left = Reference(-1); var right = Reference(1);
            var start = new Vector3(0, 4, 290);
            var hazard = Hazard(new(-4, 5, 278), 5, 0, 40);
            var result = BlundervillePresetPlanner.Find([Route(left), Route(right)], [hazard], start, Epoch, 6);
            Check(result is { Name: "右侧参考线" }, "Did not select the open approved route.");
            Check(BlundervillePresetPlanner.Find([Route(left)], [hazard], start, Epoch, 6) == null, "Escaped disabled routes by inventing a path.");
        });
        run("Blunderville preset waits at safe strip for timed obstacle", () =>
        {
            var route = Route(Reference(-1));
            var start = new Vector3(-6, 6, 275);
            var hazard = Hazard(new(-6, 6, 268), 2, 0, 3);
            var result = BlundervillePresetPlanner.Find([route], [hazard], start, Epoch, 6);
            Check(result != null && result.Plan[0].Departure > Epoch, "Did not wait for the obstacle.");
            var index = new BlundervilleFinalPlanner.HazardIndex([hazard], Epoch, BlundervilleFinalPlanner.Final);
            CheckPlan(result!, index);
        });
        run("Blunderville preset unsafe waiting point and off-route do not authorize motion", () =>
        {
            var route = Route(Reference(-1)); var start = new Vector3(-6, 6, 268);
            var hazard = Hazard(start, 3, 0, 30);
            Check(BlundervillePresetPlanner.Find([route], [hazard], start, Epoch, 6) == null, "Waited in a hazard.");
            Check(BlundervillePresetPlanner.Suffix(route.Points, new(15, 4, 300)) == null, "Joined a distant route.");
            Check(BlundervillePresetPlanner.Suffix(route.Points, new(0, 6, 270)) == null, "Joined through a hole.");
        });
        run("Blunderville preset asynchronous result rechecks latest hazards", () =>
        {
            var planner = new BlundervillePresetPlanner(); planner.Configure(Library(-1)); var state = State();
            var p = new Vector3(0, 4, StartZ); var normal = Normal(p);
            planner.Apply(normal, state, [], p, Epoch); planner.Pending!.GetAwaiter().GetResult();
            var decision = planner.Apply(normal, state, [Hazard(new(0, 4, 300), 20, 0, 60)], p, Epoch.AddSeconds(.1));
            Check(decision.Hold && !decision.DirectTraversal, "Stale plan bypassed a new hazard."); planner.Reset();
        });
        run("Blunderville preset reset discards completed result", () =>
        {
            var planner = new BlundervillePresetPlanner(); planner.Configure(Library(-1)); var state = State();
            var p = new Vector3(0, 4, StartZ); var normal = Normal(p);
            planner.Apply(normal, state, [], p, Epoch); planner.Pending!.GetAwaiter().GetResult();
            planner.Reset();
            Check(planner.Apply(normal, state, [], p, Epoch.AddSeconds(.1)).Hold, "Reset revived a stale plan.");
            planner.Reset();
        });
        run("Blunderville preset cancelled search", () =>
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Check(BlundervillePresetPlanner.Find([Route(Reference(-1))], [], new(0, 4, StartZ), Epoch, 6, cancellation.Token) == null, "Cancellation ignored.");
        });
        run("Blunderville preset stair handoff checks first flight", () =>
        {
            var p = new Vector3(-4, 31, 157); var route = Route(Reference(-1));
            var result = BlundervillePresetPlanner.Find([route], [], p, Epoch, 6);
            Check(result != null && MathF.Abs(result.Plan[^1].Position.Z - EndZ) < .01f, "Missing stair handoff.");
            Check(BlundervillePresetPlanner.Find([route], [Hazard(new(-4, 32, 150), 2, 0, 30)], p, Epoch, 6) == null, "Unsafe first flight accepted.");
        });
        run("Blunderville preset bounded candidate benchmark", () =>
        {
            var routes = Enumerable.Range(0, MaxRoutes).Select(i => Route(Reference(i % 2 == 0 ? -1 : 1))).ToArray();
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 10; ++i)
                Check(BlundervillePresetPlanner.Find(routes, [], new(0, 4, StartZ), Epoch, 6) != null, "Search did not finish.");
            Console.WriteLine($"PRESET {MaxRoutes} candidates average={watch.Elapsed.TotalMilliseconds / 10:F2}ms (worker search, synthetic empty hazards)");
        });
        run("Blunderville preset recorded forecast safety and cost", () =>
        {
            using var replay = new BlundervilleRingRegression.Recording("final", "feedback-213132.json");
            var routes = new[] { Route(Reference(-1)), Route(Reference(1)) };
            var watch = new Stopwatch(); var accepted = 0; var rejected = 0;
            foreach (var time in new[] { 5d, 10, 15, 20, 25, 30, 35 })
            {
                replay.Advance(time);
                var now = BlundervilleRingRegression.Epoch.AddSeconds(time);
                var hazards = replay.Encounter.TraversalHazards(now).ToArray();
                foreach (var p in new[] { new Vector3(0, 4, 290), new(-6, 6, 275), new(-3.5f, 13, 236), new(-10.6f, 28, 184) })
                {
                    watch.Start(); var result = BlundervillePresetPlanner.Find(routes, hazards, p, now, 6); watch.Stop();
                    if (result == null) { ++rejected; continue; }
                    ++accepted;
                    CheckPlan(result, new(hazards, now, BlundervilleFinalPlanner.Final));
                }
            }
            Check(accepted > 0, "No recorded snapshot admitted a route.");
            Console.WriteLine($"PRESET RECORDED accepted={accepted} held={rejected} average={watch.Elapsed.TotalMilliseconds / (accepted + rejected):F2}ms; fixed snapshots, not full-game completion");
        });
    }

    private static void Traverse(int side)
    {
        var route = Route(Reference(side)); var p = new Vector3(0, 4, StartZ); var now = Epoch; var iterations = 0;
        while (p.Z > EndZ + .01f && ++iterations < 20)
        {
            var result = BlundervillePresetPlanner.Find([route], [], p, now, 6);
            Check(result != null, $"No continuation from {p}.");
            CheckPlan(result!, new([], now, BlundervilleFinalPlanner.Final));
            foreach (var point in result!.Plan.Skip(1))
                Check(route.Points.Zip(route.Points.Skip(1)).Any(leg => GeometryHelper.DistanceSquaredPointToSegmentXZ(point.Position, leg.First, leg.Second) < .001f), "Created a point outside preset.");
            p = result.Plan[^1].Position; now = result.Plan[^1].Arrival;
        }
        Check(p.Z <= EndZ + .01f, "Never reached stair entrance.");
    }

    private static void Navigation(int side, bool editExisting = false, BlundervilleFinalRouteSettings? assigned = null)
    {
        var now = Epoch; var move = new BlundervilleRingRegression.Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        var state = State(); var race = new BlundervilleRacePlanner(); var planner = new BlundervillePresetPlanner();
        Vector3? editedPoint = null;
        if (assigned != null)
        {
            editedPoint = assigned.AssignedRoute()!.Points.Single(p => p.Z == 273).Position;
            race.ConfigureFinalRoutes(assigned); planner.ConfigureCourse(assigned);
        }
        else if (editExisting)
        {
            var draft = SectionDraft(side);
            var point = draft.Routes.SelectMany(r => r.Points).Single(p => p.Z == 273);
            point.X = side * 6.5f; editedPoint = point.Position;
            var settings = BlundervilleFinalRouteSettings.FromEdit(draft);
            race.ConfigureFinalRoutes(settings); planner.ConfigureCourse(settings);
        }
        else planner.Configure(Library(side));
        var encounter = new BlundervilleEncounter(_ => { }); var p = new Vector3(0, 4, StartZ);
        if (editExisting) p = new(side * 3.5f, BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, 278), 278);
        if (assigned != null) p = assigned.AssignedRoute()!.Points.Single(point => point.Z == 278).Position;
        var tick = 0; var seenMoving = false; var passedEdit = false;
        for (; tick < 2400; ++tick)
        {
            now = Epoch.AddSeconds(tick * .05); move.Now = now; api.UtcNowForTest = now;
            var normal = race.Update(state, now, p, true, false, false, null);
            var decision = planner.Apply(normal, state, [], p, now, nav.GetDirectTraversalExecution(now));
            // Complete only the worker between frames; no future hazards or positions.
            planner.Pending?.GetAwaiter().GetResult();
            nav.Apply(state, decision, encounter, now, p, true, false, false);
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, true, false, 0, true, snapshot.Version);
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new(6, 6, 0, false, 6, 6), now);
            var next = move.Advance(p, .05f, 6); next.Y = BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, next.Z);
            Check(Floor.Contains(next, 0), "Actual dispatch walked into a hole.");
            seenMoving |= Distance(p, next) > .1f; p = next;
            passedEdit |= editedPoint is { } changed && Distance(p, changed) < .3f;
            if (p.Z <= BlundervilleFinalPlanner.Bottom) break;
        }
        Console.WriteLine($"PRESET NAV side={side} time={tick*.05:F2} p={p} dispatch={runtime.MovementStatus.DispatchCount} status={planner.Status}");
        Check(seenMoving && p.Z <= BlundervilleFinalPlanner.Bottom && MathF.Abs(p.X - side * 4) < .5f, "Actual runtime failed to reach the chosen stair lane.");
        Check(!editExisting || passedEdit, "Actual movement skipped the edited waypoint.");
        planner.Reset(); nav.Clear();
        Check(!move.IsRunning, "Movement leaked after cleanup.");
    }

    private static void CheckPlan(BlundervillePresetPlanner.Result result, BlundervilleFinalPlanner.HazardIndex index)
    {
        var epoch = result.Plan[0].Arrival;
        for (var i = 1; i < result.Plan.Count; ++i)
        {
            var a = result.Plan[i - 1]; var b = result.Plan[i];
            Check(Link(a.Position, b.Position), "Path crosses terrain.");
            Check(index.Segment(a.Position, b.Position, (a.Departure - epoch).TotalSeconds, (b.Arrival - epoch).TotalSeconds), "Segment unsafe.");
            if (a.Departure > a.Arrival) Check(index.Stand(a.Position, (a.Arrival - epoch).TotalSeconds, (a.Departure - epoch).TotalSeconds), "Wait unsafe.");
        }
    }
    private static BlundervilleRouteLibrary Library(int side) => new() { UsePresets = true, Routes = [Reference(side)] };
    private static BlundervilleRouteLibrary SectionDraft(int side)
    {
        var draft = new BlundervilleFinalRouteSettings().Edit();
        if (side > 0)
            foreach (var p in draft.Routes.SelectMany(s => s.Points)) p.X = -p.X;
        return draft;
    }
    private static BlundervillePresetPlanner.Route Route(BlundervilleRoutePreset preset) => new(preset.Name, Compile(preset));
    private static BlundervilleRaceState State()
    {
        var state = new BlundervilleRaceState(); state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20017, 0, 0, 0, Epoch); return state;
    }
    private static BlundervilleRaceDecision Normal(Vector3 p) => new(p - Vector3.UnitZ * 10, false, false, null, "normal");
    private static BlundervilleHazard Hazard(Vector3 p, float radius, double from, double until)
        => new("preset-test", new CircleGeometry(p, radius), Epoch.AddSeconds(from), Epoch.AddSeconds(until), Epoch);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

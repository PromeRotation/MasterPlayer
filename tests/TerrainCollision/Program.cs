using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Spatial.Terrain;

var epoch = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
var failures = 0;
void Run(string name, Action test)
{
    try { test(); Console.WriteLine("PASS terrain: " + name); }
    catch (Exception ex) { ++failures; Console.WriteLine($"FAIL terrain: {name}: {ex}"); }
}
void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
LocalTerrainScanner Scan(AnalyticTerrainRaycaster rays, Vector3? player = null)
{
    var scan = new LocalTerrainScanner(new GameTerrainProbe(rays));
    for (var i = 0; i < 1000; ++i)
    {
        scan.Update(player ?? Vector3.Zero, 8, epoch.AddMilliseconds(i * 16), 32, 100);
        if (scan.Complete) return scan;
    }
    throw new InvalidOperationException("The bounded terrain scan did not finish.");
}

if (args.Contains("--benchmark") || args.Contains("--benchmark-budget"))
{
    TerrainScanBenchmark.Run(args.Contains("--benchmark-budget"));
    return;
}
if (args.Contains("--scene-benchmark"))
{
    SceneTerrainBenchmark.Run();
    return;
}
if (Array.IndexOf(args, "--terrain-scene") is >= 0 and var captureArgument)
{
    try
    {
        if (captureArgument + 1 >= args.Length) throw new ArgumentException("--terrain-scene 需要归档路径");
        if (!TerrainSceneArchiveTests.Rebuild(args[captureArgument + 1])) Environment.ExitCode = 1;
    }
    catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    return;
}

TerrainSceneArchiveTests.Run(Run, epoch);

Run("real sampling logic detects floor, thin walls, headroom, cliffs and slopes", () =>
{
    var rays = new AnalyticTerrainRaycaster();
    var probe = new GameTerrainProbe(rays);
    Check(probe.Sample(Vector3.Zero, out var floor) == TerrainCellKind.Walkable && floor == Vector3.Zero, "Flat ground was lost.");
    rays.Walls.Add((.25f, -10, 10, 3));
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Wall, "A zero-thickness wall through the cell was missed.");
    Check(!probe.CanTraverse(Vector3.Zero, Vector3.UnitX), "A live segment crossed the thin wall.");
    rays.Walls.Clear(); rays.Ceiling = 1;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Wall, "Low headroom was accepted.");
    rays.Ceiling = null; rays.CliffAtX = .25f;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Drop, "A corner without ground was accepted.");
    Check(!probe.CanTraverse(Vector3.Zero, new(1, 0, 0)), "A live segment walked over a cliff.");
    rays.CliffAtX = null; rays.Slope = .2f;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Walkable && probe.CanTraverse(Vector3.Zero, new(2, .4f, 0)), "A gentle ramp was rejected.");
    rays.Slope = 2;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Drop, "An unwalkable slope was accepted.");
});

Run("missing native normals use measured support instead of rejecting every floor cell", () =>
{
    var rays = new AnalyticTerrainRaycaster { OmitNormals = true };
    var probe = new GameTerrainProbe(rays);
    Check(probe.Sample(Vector3.Zero, out var floor) == TerrainCellKind.Walkable && floor == Vector3.Zero,
        "A valid floor hit without a native normal was incorrectly classified as a drop.");
    rays.Slope = .2f;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Walkable && probe.CanTraverse(Vector3.Zero, new(2, .4f, 0)),
        "Missing normals prevented movement on a measured gentle ramp.");
    rays.Slope = 2;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Drop, "A steep surface without normals was assumed flat.");
    rays.Slope = 0; rays.CliffAtX = .05f;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Drop && !probe.CanTraverse(Vector3.Zero, Vector3.UnitX),
        "Missing normals concealed unsupported floor at the edge.");
    rays.CliffAtX = null; rays.Walls.Add((.25f, -10, 10, 3));
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Wall && !probe.CanTraverse(Vector3.Zero, Vector3.UnitX),
        "Missing normals disabled wall detection.");
    rays.Walls.Clear(); rays.Ceiling = 1;
    Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Wall, "Missing normals disabled headroom detection.");
});

Run("normal-less floor expands beyond the five seed cells and publishes radar edges", () =>
{
    var rays = new AnalyticTerrainRaycaster { OmitNormals = true, CliffAtX = 3 };
    rays.Walls.Add((-2, -100, 100, 3));
    var map = Scan(rays).Current!;
    Check(map.WalkableCount > 100 && map.Contains(new(0, 0, 4)), "The map remained the five-cell cross seen in the radar.");
    Check(map.GetContours().Count > 4 && map.GetContours().All(edge => edge.Count == 2),
        "Expanded terrain did not expose drawable boundary edges.");
    Check(map.BlockedCells().Any(c => c.Kind == TerrainCellKind.Wall) && map.BlockedCells().Any(c => c.Kind == TerrainCellKind.Drop),
        "The expanded map lost real walls or cliffs.");
    Check(!ArenaBoundaryTraversal.ContainsSegment(map, Vector3.Zero, new(4, 0, 0)), "A displayed edge allowed traversal over the cliff.");
    map = Scan(rays, new(0, 0, 5)).Current!;
    Check(map.WalkableCount > 100 && map.Contains(new(0, 0, 9)), "Moving the scan origin brought back the empty cross.");
});

Run("an unsupported five-cell seed cross reports missing ground instead of successful scanning", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var rays = new AnalyticTerrainRaycaster { OmitNormals = true, CliffAtX = -100 };
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    for (var i = 0; i < 20; ++i) controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(i * 16));
    Check(controller.Current is { WalkableCount: 0 } && controller.Current.BlockedCells().Count() == 5,
        "A genuine missing-ground result was silently treated as a floor.");
    Check(controller.Current!.GetContours().Count == 0 && controller.StatusText.Contains("未找到可通行地面"),
        "An empty terrain map displayed a fictitious boundary or reported successful scanning.");
});

Run("walking up a cached smooth slope retains the terrain despite cumulative height change", () =>
{
    var rays = new AnalyticTerrainRaycaster { Slope = .6f };
    var scan = Scan(rays);
    var map = scan.Current!;
    Check(map.Contains(new(3, 1.8f, 0)), "The connected ramp was not modelled.");
    scan.Update(new(3, 1.8f, 0), 8, epoch.AddSeconds(1), 1, 100);
    Check(ReferenceEquals(scan.Current, map), "A smooth ramp was cleared just because its height exceeded the old origin.");
});

Run("jumping above cached ground preserves terrain and validates movement from the floor", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(new AnalyticTerrainRaycaster()));
    var expected = Scan(new AnalyticTerrainRaycaster()).Current!.WalkableCount;
    for (var i = 0; i < 500 && (controller.Current?.WalkableCount ?? 0) < expected; ++i)
        controller.Update(Vector3.Zero, 1, true, 8, epoch);
    var map = controller.Current!;
    Check(map.WalkableCount > 100, "Grounded terrain was not ready before the jump.");
    controller.Update(new(0, 2.5f, 0), 1, true, 8, epoch.AddMilliseconds(100));
    Check(ReferenceEquals(controller.Current, map), "The jump apex cleared the ground map.");
    Check(controller.ValidateStep(new(0, 2.5f, 0), new(1, 2.5f, 0)), "A safe floor route was rejected from an airborne origin.");
});

Run("a moving window reuses smooth terrain overlap and only samples its new frontier", () =>
{
    var rays = new AnalyticTerrainRaycaster { Slope = .5f };
    var scan = Scan(rays);
    var previous = scan.Current!;
    var count = previous.WalkableCount;
    var calls = rays.Calls;
    var position = new Vector3(4.5f, 2.25f, 0);
    scan.Update(position, 8, epoch.AddSeconds(1), 0, 100);
    var shifted = scan.Current!;
    Check(shifted.WalkableCount > count / 2 && scan.SampledCount == 0 && rays.Calls == calls,
        "Recentring discarded or resampled previously confirmed smooth ground.");
    Check(shifted.TryGetFloorPosition(position, out var floor) && MathF.Abs(floor.Y - 2.25f) < .001f,
        "Recentring lost the cached slope height.");
    Check(!shifted.Contains(new(11, 0, 0)) && previous.WalkableCount == count && previous.Position == Vector3.Zero,
        "The new frontier was assumed safe, or the old immutable map was mutated.");
    for (var i = 0; i < 1000 && !scan.Complete; ++i) scan.Update(position, 8, epoch.AddSeconds(1), 32, 100);
    Check(scan.Complete && scan.SampledCount < count && scan.Current!.Contains(new(10.5f, 5.25f, 0)),
        "The moving window rebuilt the entire old map instead of extending it.");
    Check(previous.TryGetFloorPosition(position, out floor) && MathF.Abs(floor.Y - 2.25f) < .001f,
        "Publishing later scans modified an earlier map's height data.");
});

Run("jump flags retain the ground layer while landing on another floor replaces it", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var rays = new AnalyticTerrainRaycaster { AdditionalFloorHeight = 4 };
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    for (var i = 0; i < 500 && controller.Current?.WalkableCount is not > 100; ++i)
        controller.Update(Vector3.Zero, 1, true, 8, epoch);
    var lower = controller.Current!;
    var calls = rays.Calls;
    controller.Update(new(0, 4, 0), 1, true, 8, epoch.AddMilliseconds(100), airborne: true);
    Check(ReferenceEquals(controller.Current, lower) && rays.Calls == calls, "A jump rebuilt terrain or selected the overhead floor.");
    Check(controller.ValidateStep(new(0, 4, 0), new(1, 4, 0)), "Airborne validation failed to use the existing ground layer.");
    controller.Update(new(0, 4, 0), 1, true, 8, epoch.AddMilliseconds(200));
    Check(!ReferenceEquals(controller.Current, lower) && controller.Current!.TryGetFloorPosition(Vector3.Zero, out var floor)
        && MathF.Abs(floor.Y - 4) < .001f, "Landing on the upper floor retained the stale lower layer.");
    var upper = controller.Current;
    controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(300));
    Check(!ReferenceEquals(controller.Current, upper) && controller.Current!.TryGetFloorPosition(Vector3.Zero, out floor)
        && MathF.Abs(floor.Y) < .001f, "Returning to the lower floor kept upper-floor terrain.");
});

Run("starting in midair waits for ground instead of seeding a false drop map", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var rays = new AnalyticTerrainRaycaster();
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    controller.Update(new(0, 2.5f, 0), 1, true, 8, epoch, airborne: true);
    Check(controller.Current is { WalkableCount: 0 } && !controller.Current.BlockedCells().Any() && rays.Calls == 0,
        "Airborne initialization sampled from the actor's jump height.");
    Check(!controller.ValidateStep(new(0, 2.5f, 0), Vector3.UnitX), "Unknown ground allowed movement while airborne.");
    controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(100));
    Check(controller.Current is { WalkableCount: > 0 } && controller.Current.TryGetFloorPosition(Vector3.Zero, out var floor)
        && floor.Y == 0, "Landing did not replace the uninitialized airborne origin.");
});

Run("jumping does not bypass a new wall or missing support and rejects from a ground origin", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var rays = new AnalyticTerrainRaycaster();
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    for (var i = 0; i < 500 && controller.Current?.WalkableCount is not > 100; ++i)
        controller.Update(Vector3.Zero, 1, true, 8, epoch);
    controller.Update(new(0, 2.5f, 0), 1, true, 8, epoch.AddMilliseconds(100), airborne: true);
    rays.Walls.Add((1, -10, 10, 3));
    Check(!controller.ValidateStep(new(0, 2.5f, 0), new(2, 2.5f, 0)), "Jumping disabled the live wall check.");
    rays.Walls.Clear(); rays.CliffAtX = 1;
    Check(!controller.ValidateStep(new(0, 2.5f, 0), new(2, 2.5f, 0)), "Jumping disabled the live cliff check.");
    controller.RejectRoute(new(0, 2.5f, 0), 8, epoch.AddMilliseconds(150));
    Check(controller.Current is { WalkableCount: 0 } && controller.Current.Position.Y == 0,
        "A real terrain rejection rebuilt the map around the airborne height.");
});

Run("cached floor projection agrees with shared boundary edges and copies height data", () =>
{
    var cells = new TerrainCellKind[9]; cells[4] = TerrainCellKind.Walkable;
    var heights = new float[9]; heights[4] = .5f;
    var map = new LocalTerrainBoundary(Vector3.Zero, 3, cells, heights);
    heights[4] = 100;
    var edge = new Vector3(.375f, 2.5f, .375f);
    Check(map.Contains(edge) && map.TryGetFloorPosition(edge, out var floor) && floor.Y == .5f,
        "Jump projection lost a valid grid corner or used mutable source heights.");
    Check(!map.TryGetFloorPosition(new(.38f, 2.5f, .38f), out _), "Floor projection leaked into an unknown neighbour.");
});

Run("incremental scan respects sample budget, unknown space and immutable publication", () =>
{
    var scan = new LocalTerrainScanner(new GameTerrainProbe(new AnalyticTerrainRaycaster()));
    scan.Update(Vector3.Zero, 8, epoch, 3, 100);
    var first = scan.Current!;
    Check(scan.SampledCount == 3 && !first.Contains(new(6, 0, 0)), "Unknown space was treated as floor or the per-frame cap was exceeded.");
    var count = first.WalkableCount;
    scan.Update(Vector3.Zero, 8, epoch.AddMilliseconds(120), 16, 100);
    Check(first.WalkableCount == count && !ReferenceEquals(first, scan.Current), "A published map was mutated or progress was not published.");
});

Run("walls and cliffs cannot connect floor islands or leak through contour holes", () =>
{
    var rays = new AnalyticTerrainRaycaster(); rays.Walls.Add((1.8f, -100, 100, 3));
    var map = Scan(rays).Current!;
    Check(map.Contains(Vector3.Zero) && !map.Contains(new(4, 0, 0)), "A disconnected floor beyond a wall was admitted.");
    Check(map.BlockedCells().Any(c => c.Kind == TerrainCellKind.Wall), "Wall cells were not retained for the radar.");
    Check(!ArenaBoundaryTraversal.ContainsSegment(map, Vector3.Zero, new(4, 0, 0)), "Exact segment traversal crossed a wall.");
    rays.Walls.Clear(); rays.CliffAtX = 2;
    map = Scan(rays).Current!;
    Check(!map.Contains(new(3, 0, 0)) && map.BlockedCells().Any(c => c.Kind == TerrainCellKind.Drop), "The cliff was not excluded/displayable.");
    var side = 7; var cells = Enumerable.Repeat(TerrainCellKind.Walkable, side * side).ToArray();
    cells[3 * side + 3] = TerrainCellKind.Drop;
    map = new(Vector3.Zero, side, cells, new float[cells.Length]);
    Check(!ArenaBoundaryTraversal.ContainsSegment(map, new(-2, 0, 0), new(2, 0, 0)), "An interior hole vanished from the boundary contours.");
});

Run("real solver routes around a wall and smoothing preserves terrain", () =>
{
    var rays = new AnalyticTerrainRaycaster(); rays.Walls.Add((1.8f, -2, 2, 3));
    var map = Scan(rays).Current!;
    Check(map.Contains(new(4.5f, 0, 0)), "Reachable ground around the wall was not discovered.");
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    manager.SetFallbackBoundary(map);
    manager.AddGoalArea(new CircleTemplate(.8f), new StaticTransform(new(4.5f, 0, 0), 0), [GoalWindow.Persistent(epoch, 1)]);
    var snapshot = manager.BuildSnapshot(epoch);
    var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
        new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = .3f, EnablePathSmoothing = true }, epoch);
    Check(solution.IsSuccess && solution.ExecutablePath.Count > 1, $"No terrain-respecting route: {solution.Status}");
    var previous = Vector3.Zero;
    foreach (var point in solution.ExecutablePath)
    {
        Check(ArenaBoundaryTraversal.ContainsSegment(map, previous, point.Position), "Smoothing cut through excluded terrain.");
        Check(new GameTerrainProbe(rays).CanTraverse(previous, point.Position), "The solver's output failed the independent collision check.");
        previous = point.Position;
    }
    Check(solution.ExecutablePath.Any(p => MathF.Abs(p.Position.Z) > 2.3f), "The path failed to go around the wall.");
});

Run("narrow corridor retains its usable centre without a second terrain margin", () =>
{
    var rays = new AnalyticTerrainRaycaster();
    rays.Walls.Add((-1, -100, 100, 3)); rays.Walls.Add((1, -100, 100, 3));
    var map = Scan(rays).Current!;
    Check(map.Contains(Vector3.Zero) && map.Contains(new(0, 0, 4)), "The narrow corridor's usable centre was discarded.");
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    manager.SetFallbackBoundary(map);
    manager.AddGoalArea(new CircleTemplate(.6f), new StaticTransform(new(0, 0, 4), 0), [GoalWindow.Persistent(epoch, 1)]);
    var snapshot = manager.BuildSnapshot(epoch);
    var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
        new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = .5f, ArenaSafetyMargin = 0 }, epoch);
    Check(solution.IsSuccess && solution.TargetPosition is { Z: > 3 }, $"Narrow corridor solve failed: {solution.Status}");
    Check(!map.Contains(new(1.1f, 0, 0)), "Removing duplicate margin opened the corridor wall.");
});

Run("generic AOE escape remains inside scanned floor when the nearer side is walled off", () =>
{
    var rays = new AnalyticTerrainRaycaster(); rays.Walls.Add((1.8f, -100, 100, 3));
    var map = Scan(rays).Current!;
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    manager.SetFallbackBoundary(map);
    manager.AddDangerArea(new CircleTemplate(3), new StaticTransform(Vector3.Zero, 0),
        [new DangerWindow(epoch.AddSeconds(3), epoch.AddSeconds(3.6))]);
    var snapshot = manager.BuildSnapshot(epoch);
    var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
        new(6, 6, 0, false, 6, 6), new SolveOptions { ArenaSafetyMargin = 0 }, epoch);
    Check(solution.IsSuccess && solution.TargetPosition is { } target && target.Length() > 3, $"No safe generic escape: {solution.Status}");
    var previous = Vector3.Zero;
    foreach (var point in solution.ExecutablePath)
    {
        Check(ArenaBoundaryTraversal.ContainsSegment(map, previous, point.Position) && new GameTerrainProbe(rays).CanTraverse(previous, point.Position),
            "Generic AOE escape crossed unknown floor or a wall.");
        previous = point.Position;
    }
});

Run("fixed arenas override terrain; unavailable scenes, new territory and clear discard stale floor", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var rays = new AnalyticTerrainRaycaster(); var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    controller.Update(Vector3.Zero, 1, true, 8, epoch);
    Check(manager.BuildSnapshot(epoch).ArenaBoundary == controller.Current, "Terrain was not published to the actual manager.");
    var fixedArena = new CircleGeometry(Vector3.Zero, 10);
    manager.SetArenaBoundary(fixedArena);
    var calls = rays.Calls;
    controller.Update(Vector3.Zero, 1, true, 8, epoch.AddSeconds(1));
    Check(ReferenceEquals(manager.BuildSnapshot(epoch).ArenaBoundary, fixedArena) && controller.Current == null && rays.Calls == calls,
        "Terrain overwrote or queried inside a registered mechanic arena.");
    manager.ClearArenaBoundary(); rays.IsAvailable = false;
    controller.Update(Vector3.Zero, 2, true, 8, epoch.AddSeconds(2));
    Check(controller.Current is { WalkableCount: 0 }, "An unavailable scene fell back to unrestricted movement.");
    rays.IsAvailable = true; controller.Update(Vector3.Zero, 2, true, 8, epoch.AddSeconds(3));
    rays.IsAvailable = false; controller.Update(Vector3.Zero, 2, true, 8, epoch.AddSeconds(4));
    Check(controller.Current is { WalkableCount: 0 }, "Previously scanned floor survived loss of collision data.");
    rays.IsAvailable = true; controller.Update(new(50, 0, 50), 3, true, 8, epoch.AddSeconds(5));
    Check(!controller.Current!.Contains(Vector3.Zero), "Old territory coordinates remained traversable.");
    controller.Clear();
    Check(controller.Current == null && manager.BuildSnapshot(epoch).ArenaBoundary == null, "Clear retained terrain registrations.");
});

Run("completed scans refresh, and a closing door invalidates the live route", () =>
{
    var rays = new AnalyticTerrainRaycaster();
    var scan = Scan(rays);
    var completed = scan.Current!;
    scan.Update(Vector3.Zero, 8, epoch.AddMinutes(1), 1, 100);
    Check(!scan.Complete && ReferenceEquals(scan.Current, completed), "Refresh did not start or replaced the useful old map with an incomplete scan.");
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    controller.Update(Vector3.Zero, 1, true, 8, epoch);
    Check(controller.ValidateStep(Vector3.Zero, new(5, 0, 0)), "An open path was rejected.");
    rays.Walls.Add((1, -10, 10, 3));
    Check(!controller.ValidateStep(Vector3.Zero, new(5, 0, 0)), "A newly closed door was not detected in front of the running route.");
    controller.RejectRoute(Vector3.Zero, 8, epoch.AddSeconds(1));
    Check(controller.Current is { WalkableCount: 0 }, "The old route retained its stale terrain after rejection.");
});
Run("query errors stop movement, expose the cause and back off before retrying", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var rays = new AnalyticTerrainRaycaster { ThrowOnCast = true };
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    controller.Update(Vector3.Zero, 1, true, 8, epoch);
    Check(controller.Current is { WalkableCount: 0 } && controller.LastError?.Contains("Synthetic") == true,
        "Query failure retained unsafe floor or swallowed the diagnostic.");
    var calls = rays.Calls;
    controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(100));
    Check(rays.Calls == calls, "Failed collision queries were retried every frame.");
    rays.ThrowOnCast = false;
    controller.Update(Vector3.Zero, 1, true, 8, epoch.AddSeconds(3));
    Check(controller.LastError == null && controller.Current is { WalkableCount: > 0 }, "A recovered scene did not resume scanning.");
});
Run("scan batches reuse ground queries but live checks and subsequent batches always see new terrain", () =>
{
    var rays = new AnalyticTerrainRaycaster { OmitNormals = true };
    var probe = new GameTerrainProbe(rays);
    probe.BeginScanBatch();
    try
    {
        Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Walkable, "Initial cached sample failed.");
        var calls = rays.Calls;
        Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Walkable && rays.Calls - calls <= 25,
            "The same sample repeated native ground/normal queries within one batch.");
    }
    finally { probe.EndScanBatch(); }
    rays.CliffAtX = -100;
    Check(!probe.TryGetGround(Vector3.Zero, out _), "A live ground check reused stale scan data.");
    probe.BeginScanBatch();
    try { Check(probe.Sample(Vector3.Zero, out _) == TerrainCellKind.Drop, "A new scan batch reused previous-frame floor."); }
    finally { probe.EndScanBatch(); }
});

Run("collinear body checks reduce rays without dropping thin walls, intermediate support or bends", () =>
{
    var rays = new AnalyticTerrainRaycaster();
    var probe = new GameTerrainProbe(rays);
    Check(probe.CanTraverse(Vector3.Zero, new(2, 0, 0)) && rays.Calls <= 24,
        "A straight flat route still repeated all six body rays for every support point.");
    rays.Walls.Add((.7f, -10, 10, 3));
    Check(!probe.CanTraverse(Vector3.Zero, new(2, 0, 0)), "Merged checks missed a thin wall between support points.");
    rays.Walls.Clear(); rays.RaisedFloor = (.6f, 1.2f, .3f); rays.Ceiling = 1.5f;
    Check(!probe.CanTraverse(Vector3.Zero, new(2, 0, 0)), "A bent floor path was flattened through low headroom.");
    rays.Ceiling = null;
    Check(probe.CanTraverse(Vector3.Zero, new(2, 0, 0)), "Removing the ceiling did not restore the low-step route.");
    rays.RaisedFloor = null; rays.CliffAtX = .7f;
    Check(!probe.CanTraverse(Vector3.Zero, new(2, 0, 0)), "Merged checks skipped intermediate ground support.");
});

Run("ready static terrain idles without full rescans and unchanged refreshes preserve snapshot versions", () =>
{
    var rays = new AnalyticTerrainRaycaster();
    var scan = Scan(rays);
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var map = scan.Current!;
    manager.SetFallbackBoundary(map);
    var version = manager.Version;
    var calls = rays.Calls;
    for (var i = 0; i < 120; ++i) scan.Update(Vector3.Zero, 8, epoch.AddSeconds(1).AddMilliseconds(i * 16));
    Check(rays.Calls == calls && ReferenceEquals(scan.Current, map), "Ready flat terrain immediately restarted a full scan.");
    for (var i = 0; i < 1000; ++i)
    {
        scan.Update(Vector3.Zero, 8, epoch.AddMinutes(1), 128, 100);
        manager.SetFallbackBoundary(scan.Current);
        if (scan.Complete) break;
    }
    Check(scan.Complete && rays.Calls > calls && ReferenceEquals(scan.Current, map) && manager.Version == version,
        "An unchanged background refresh replaced geometry and invalidated solver snapshots.");
});

Run("near blocked cells reopen a deep-dungeon passage without rebuilding the existing room", () =>
{
    var rays = new AnalyticTerrainRaycaster(); rays.CrossWalls.Add((2, -30, 30, 3));
    var scan = Scan(rays);
    var previous = scan.Current!;
    Check(!previous.Contains(new(0, 0, 3)), "The closed passage did not block the initial flood fill.");
    rays.CrossWalls.Clear();
    for (var i = 0; i < 100 && scan.Current?.Contains(new(0, 0, 3)) != true; ++i)
        scan.Update(Vector3.Zero, 8, epoch.AddSeconds(2), 128, 100);
    Check(scan.Current!.Contains(new(0, 0, 3)) && scan.Current.Contains(new(0, 0, -3)),
        "A newly opened passage stayed blocked or discarded the existing room.");
    Check(!previous.Contains(new(0, 0, 3)), "Reopening a passage mutated the earlier snapshot.");
});

Run("nearby deep-dungeon space is published first while the expanded cap still obeys the time budget", () =>
{
    var rays = TerrainScanBenchmark.Scene("room", true);
    var scan = new LocalTerrainScanner(new GameTerrainProbe(rays));
    scan.Update(Vector3.Zero, 20, epoch, budgetMilliseconds: 1000);
    Check(scan.SampledCount > 16 && scan.SampledCount <= 128 && scan.Current!.Contains(new(0, 0, 3)) && !scan.Complete,
        "The old cell cap delayed nearby usable floor behind the outer room.");
    var slow = new LocalTerrainScanner(new GameTerrainProbe(new AnalyticTerrainRaycaster { WorkMilliseconds = .1 }));
    slow.Update(Vector3.Zero, 20, epoch);
    Check(slow.SampledCount == 1 && !slow.Complete, "Raising the cell cap bypassed the native-query time budget.");
});

Run("deep-dungeon floor and mirrored layout changes invalidate terrain even at identical coordinates", () =>
{
    using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
    var rays = new AnalyticTerrainRaycaster();
    var controller = new LocalTerrainController(manager, new GameTerrainProbe(rays));
    controller.Update(Vector3.Zero, 1, true, 8, epoch, scene: ((nint)1234, 31, 0));
    var first = controller.Current!;
    Check(first.WalkableCount > 0, "The first dungeon floor did not scan.");
    rays.CliffAtX = -100;
    controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(100), scene: ((nint)1234, 32, 0));
    Check(!ReferenceEquals(controller.Current, first) && controller.Current is { WalkableCount: 0 },
        "A new floor reused the previous floor's geometry at the same XZ/Y coordinates.");
    rays.CliffAtX = null;
    controller.Update(Vector3.Zero, 1, true, 8, epoch.AddMilliseconds(200), scene: ((nint)1234, 32, 1));
    Check(controller.Current is { WalkableCount: > 0 }, "Switching mirrored layouts kept the old blocked map.");
});

SceneTerrainTests.Run(Run, epoch);
TerrainNavigationCacheTests.Run(Run);
Environment.ExitCode = failures == 0 ? 0 : 1;

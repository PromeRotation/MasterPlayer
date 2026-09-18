using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class LateDodgeRegression
{
    private static readonly DateTime Epoch = new(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("B32 ARR late escape spends preparation cushion without waiting inside three lines", () =>
        {
            var snapshot = RecordedSnapshot();
            var position = new Vector3(117.7539f, 0, -420.4318f);
            var now = Epoch.AddSeconds(50.3);
            var solution = new SafeZoneSolver().Solve(snapshot, new(position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), now);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, $"late escape stopped: {solution.Status}");
            var margin = typeof(SafeZoneSolution).GetProperty("ReducedDangerStartSafetyTime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(solution);
            Check(margin is 0.25f, "late route did not preserve its emergency safety margin");
            Check(!ShouldWait(solution, snapshot, position, now, 2), "emergency escape waited for an unrelated player cast");
            ValidateDamageWindows(snapshot, solution.ExecutablePath);
            var (movement, move) = CreateMovement();
            Dispatch(movement, solution, snapshot, position, now);
            Check(move.IsRunning, "the valid emergency escape was rejected by movement dispatch");
        });

        run("B32 emergency escape survives asynchronous solve latency", () =>
        {
            var snapshot = RecordedSnapshot();
            var position = new Vector3(117.7539f, 0, -420.4318f);
            var now = Epoch.AddSeconds(50.3);
            var solution = new SafeZoneSolver().Solve(snapshot, new(position,0,6,false,false,0,true,snapshot.Version),
                new(6,6,0,false,6,6), new SolveOptions(), now);
            var (movement, move) = CreateMovement();
            Dispatch(movement, solution, snapshot, position, now.AddSeconds(0.15));
            Check(move.IsRunning, "emergency escape became unusable during 150ms of computation/dispatch delay");
        });

        run("finite-deadline search prunes already-late branches before emergency replanning", () =>
        {
            var snapshot = RecordedSnapshot();
            var now = Epoch.AddSeconds(50.3);
            var position = new Vector3(117.7539f, 0, -420.4318f);
            var options = new SolveOptions();
            var map = RasterizedMap.Build(snapshot, position, options, now, 6);
            var search = typeof(BossModStyleAStar).GetMethod("Search", BindingFlags.Static | BindingFlags.NonPublic)!;
            var result = (BossModStyleAStar.Result)search.Invoke(null, [map, position, 6f, now, 0f, options, true, true])!;
            Check(result.BestScore < BossModStyleAStar.Score.Safe, "late fixture unexpectedly meets the ordinary preparation deadline");
            Check(result.Expansions < map.CellCount / 8, $"late branches flooded {result.Expansions} cells before replanning");
        });

        run("movement rejects a shortcut across a concave arena notch but accepts the interior detour", () =>
        {
            using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
            manager.SetArenaBoundary(new WorldPolygonGeometry([
                new(-5,0,-5), new(5,0,-5), new(5,0,5), new(1,0,5),
                new(1,0,-1), new(-1,0,-1), new(-1,0,5), new(-5,0,5)]));
            var from = new Vector3(-3,0,3); var to = new Vector3(3,0,3);
            manager.AddGoalArea(new CircleTemplate(0.5f), new StaticTransform(to,0), [new GoalWindow(Epoch,null,1,PositionPreference.Nearest)]);
            var snapshot = manager.BuildSnapshot(Epoch);
            var direct = SafeZoneSolution.Succeeded(snapshot.Version, Epoch, Epoch, to,
                [TimedWaypoint.Pass(from,Epoch), TimedWaypoint.Pass(to,Epoch.AddSeconds(1))]);
            var (movement, move) = CreateMovement();
            Dispatch(movement, direct, snapshot, from, Epoch);
            Check(!move.IsRunning, "route left the arena between two interior endpoints");
            var detour = SafeZoneSolution.Succeeded(snapshot.Version, Epoch, Epoch, to,
                [TimedWaypoint.Pass(from,Epoch), TimedWaypoint.Pass(new(-3,0,-3),Epoch.AddSeconds(1)),
                 TimedWaypoint.Pass(new(3,0,-3),Epoch.AddSeconds(2)), TimedWaypoint.Pass(to,Epoch.AddSeconds(3))]);
            Dispatch(movement, detour, snapshot, from, Epoch);
            Check(move.IsRunning, "valid detour through the arena was rejected");
        });

        run("path postprocessing cannot cut a blocked grid corner", () =>
        {
            var options = new SolveOptions { GridResolution = 1, HardSafetyMargin = 0 };
            var empty = new SafeZoneSnapshot(1,Epoch,new RectangleGeometry(Vector3.Zero,0,10,10),[],[],[]);
            var grid = RasterizedMap.Build(empty,Vector3.Zero,options,Epoch,6);
            var (col,row) = grid.WorldToCell(Vector3.Zero);
            var from = grid.CellCenter(col,row); var to = grid.CellCenter(col+1,row+1);
            var obstacle = new CircleGeometry(grid.CellCenter(col+1,row),0.1f);
            var blocked = new SafeZoneSnapshot(2,Epoch,empty.ArenaBoundary,[new ObstacleSnapshot(default,obstacle)],[],[]);
            var map = RasterizedMap.Build(blocked,Vector3.Zero,options,Epoch,6);
            var path = new[] { TimedWaypoint.Pass(from,Epoch),TimedWaypoint.Pass(to,Epoch.AddSeconds(0.24)) };
            var validate = typeof(SafeZoneSolver).GetMethod("IsProcessedPathSafe",BindingFlags.Static|BindingFlags.NonPublic)!;
            Check(!(bool)validate.Invoke(null,[map,path,Epoch])!, "smoothing cut the corner of a blocked side cell");
            Check((bool)validate.Invoke(null,[grid,path,Epoch])!, "smoothing rejected the same segment without the obstacle");
        });

        run("short escape inside an attack goal is not swallowed by arrival tolerance", () =>
        {
            using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
            manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 10));
            manager.AddDangerArea(new CircleTemplate(0.1f), new StaticTransform(Vector3.Zero, 0), [new DangerWindow(Epoch.AddSeconds(3), Epoch.AddSeconds(3.5))]);
            manager.AddGoalArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0), [new GoalWindow(Epoch, null, 1, PositionPreference.Nearest)]);
            var snapshot = manager.BuildSnapshot(Epoch);
            var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = 0.1f, HardSafetyMargin = 0, ArrivalRadius = 0.5f }, Epoch);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "future danger was suppressed as an already-settled short goal movement");
        });

        run("cast waiting accounts for a long detour rather than distance through an obstacle", () =>
        {
            using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
            manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 20));
            manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(Vector3.Zero, 0), [new DangerWindow(Epoch.AddSeconds(2), Epoch.AddSeconds(2.6))]);
            var snapshot = manager.BuildSnapshot(Epoch);
            var path = new[] { TimedWaypoint.Pass(Vector3.Zero, Epoch), TimedWaypoint.Pass(new(0,0,6), Epoch.AddSeconds(1)),
                TimedWaypoint.Pass(new(2,0,6), Epoch.AddSeconds(1.333)), TimedWaypoint.Pass(new(2,0,0), Epoch.AddSeconds(2.333)) };
            var solution = SafeZoneSolution.Succeeded(snapshot.Version, Epoch, Epoch, path[^1].Position, path);
            Check(!ShouldWait(solution, snapshot, Vector3.Zero, Epoch, 1), "straight-line estimate delayed a 14y escape as if it were 2y");
        });

        run("late escape still fails when the whole arena is covered", () =>
        {
            var snapshot = MakeSnapshot(new CircleGeometry(new(120,0,-420), 50));
            var now = Epoch.AddSeconds(1.5);
            var solution = new SafeZoneSolver().Solve(snapshot, new(new(120,0,-420), 0, 6, false, false, 0, true, snapshot.Version),
                new(6,6,0,false,6,6), new SolveOptions(), now);
            Check(!solution.IsSuccess, "emergency fallback invented a safe route outside the arena or through a hit");
        });

        run("snapshot replacement retains a safe escape but stops it when a new hazard blocks it", () =>
        {
            var snapshot = RecordedSnapshot();
            var position = new Vector3(117.8454f, 0, -419.7604f);
            var now = Epoch.AddSeconds(49.6);
            var solution = new SafeZoneSolver().Solve(snapshot, new(position,0,6,false,false,0,true,snapshot.Version), new(6,6,0,false,6,6), new SolveOptions(), now);
            var (movement, move) = CreateMovement();
            Dispatch(movement, solution, snapshot, position, now);
            Check(move.IsRunning, "initial escape was not dispatched");
            var next = new SafeZoneSnapshot(snapshot.Version + 1, now, snapshot.ArenaBoundary, snapshot.HardObstacles, snapshot.DangerAreas, snapshot.GoalAreas);
            Dispatch(movement, null, next, position, now.AddSeconds(0.01));
            Check(move.IsRunning, "waiting for a new snapshot's solve unnecessarily stopped a still-safe route");
            var blocked = new SafeZoneSnapshot(next.Version + 1, now, next.ArenaBoundary, next.HardObstacles,
                next.DangerAreas.Append(new DangerAreaSnapshot(default, new CircleGeometry(solution.TargetPosition!.Value, 2), [new DangerWindow(now, null)])).ToArray(), next.GoalAreas);
            Dispatch(movement, null, blocked, position, now.AddSeconds(0.02));
            Check(!move.IsRunning, "a newly unsafe route continued while its replacement was pending");
        });

        run("an asynchronously delayed ordinary escape is not executed after losing its deadline", () =>
        {
            var snapshot = RecordedSnapshot();
            var position = new Vector3(117.8454f, 0, -419.7604f);
            var now = Epoch.AddSeconds(49.6);
            var solution = new SafeZoneSolver().Solve(snapshot, new(position,0,6,false,false,0,true,snapshot.Version), new(6,6,0,false,6,6), new SolveOptions(), now);
            var (movement, move) = CreateMovement();
            Dispatch(movement, solution, snapshot, position, now.AddSeconds(1.2));
            Check(!move.IsRunning, "stale route ignored elapsed computation time");
        });

        run("B32 ARR short casts find a nearby safe escape without searching the whole arena", () =>
        {
            var snapshot = RecordedSnapshot();
            var position = new Vector3(117.8454f, 0, -419.7604f);
            var now = Epoch.AddSeconds(49.6);
            var options = new SolveOptions();
            var map = RasterizedMap.Build(snapshot, position, options, now, 6);
            var result = BossModStyleAStar.Search(map, position, 6, now, 0, options, true);
            Check(result.BestScore >= BossModStyleAStar.Score.Safe && result.HasPath, "recorded overlapping lines have no safe escape");
            Check(result.Expansions < map.CellCount / 8, $"short-cast escape searched {result.Expansions} cells before starting movement");
            var solution = new SafeZoneSolver().Solve(snapshot, new(position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), options, now);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "escape is not executable");
            foreach (var danger in snapshot.DangerAreas)
                Check(!danger.Geometry.Contains(solution.ExecutablePath[^1].Position), "escape stops inside a pending hit");
            Check(solution.ExecutablePath[^1].PlannedArrivalTime < Epoch.AddSeconds(51.218), "escape reaches safety after the recorded triple hit");
        });

        foreach (var degrees in new[] { 0f, 31.568f, -62.5818f, 90f })
            run($"rotated rectangle rasterization preserves thin edge intersections at {degrees} degrees", () =>
            {
                var geometry = new RectangleGeometry(new(121.17f, 0, -419.63f), degrees * MathF.PI / 180, 0.13f, 37.23f);
                var exact = MakeSnapshot(geometry);
                var generic = MakeSnapshot(new GenericGeometry(geometry));
                var options = new SolveOptions();
                var a = RasterizedMap.Build(exact, geometry.Position, options, Epoch, 6);
                var b = RasterizedMap.Build(generic, geometry.Position, options, Epoch, 6);
                for (var row = 0; row < a.Rows; row++)
                    for (var col = 0; col < a.Columns; col++)
                    {
                        Check(a.IsDangerCoreAt(col, row) == b.IsDangerCoreAt(col, row), $"thin rectangle footprint differs at ({col},{row})");
                        Check(a.PixelMaxGAt(col, row) == b.PixelMaxGAt(col, row), $"safety margin differs at ({col},{row})");
                    }
            });

        foreach (var shape in new IGeometryShape[] { new CircleGeometry(new(120.17f,0,-419.63f), 6.3f),
            new DonutGeometry(new(120.17f,0,-419.63f), 5.3f, 15.7f), new DonutGeometry(new(120,0,-420), 19, 100) })
            run($"analytic {shape.Kind} rasterization preserves the field and its central safe hole", () =>
            {
                var options = new SolveOptions();
                var fast = RasterizedMap.Build(MakeSnapshot(shape), shape.Position, options, Epoch, 6);
                var generic = RasterizedMap.Build(MakeSnapshot(new GenericGeometry(shape)), shape.Position, options, Epoch, 6);
                for (var row = 0; row < fast.Rows; ++row)
                    for (var col = 0; col < fast.Columns; ++col)
                        Check(fast.PixelMaxGAt(col,row) == generic.PixelMaxGAt(col,row), $"circle/donut footprint differs at {col},{row}");
            });
    }

    private static bool ShouldWait(SafeZoneSolution solution, SafeZoneSnapshot snapshot, Vector3 position, DateTime now, float castRemaining)
    {
        var type = typeof(SafeZoneSolver).Assembly.GetType("PromeRotation.Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem")!;
        var movement = RuntimeHelpers.GetUninitializedObject(type);
        type.GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(movement, new SolveOptions());
        return (bool)type.GetMethod("ShouldWaitForCast", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(movement,
            [solution, new PlayerState(position, 0, 6, false, true, castRemaining, true, snapshot.Version), snapshot, now])!;
    }

    private static (object Movement, PromeRotation.GreenMoveSystem.IGreenMoveSystem Move) CreateMovement()
    {
        var tests = typeof(SafeZoneTemporalRegression);
        var move = (PromeRotation.GreenMoveSystem.IGreenMoveSystem)Activator.CreateInstance(tests.GetNestedType("FakeMove", BindingFlags.NonPublic)!, true)!;
        var movement = tests.GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [move, null, null])!;
        return (movement, move);
    }
    private static void Dispatch(object movement, SafeZoneSolution? solution, SafeZoneSnapshot snapshot, Vector3 position, DateTime now)
        => movement.GetType().GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(movement,
            [solution, new PlayerState(position,0,6,false,false,0,true,snapshot.Version), snapshot, now]);

    private static void ValidateDamageWindows(SafeZoneSnapshot snapshot, IReadOnlyList<TimedWaypoint> path)
    {
        for (var i = 1; i < path.Count; ++i)
        {
            var a = path[i - 1]; var b = path[i];
            var steps = Math.Max(1, (int)Math.Ceiling((b.PlannedArrivalTime - a.PlannedDepartureTime).TotalSeconds * 100));
            for (var step = 0; step <= steps; ++step)
            {
                var p = Vector3.Lerp(a.Position, b.Position, (float)step / steps);
                var t = a.PlannedDepartureTime.AddTicks((b.PlannedArrivalTime - a.PlannedDepartureTime).Ticks * step / steps);
                Check(snapshot.ArenaBoundary!.Contains(p), "escape leaves the field");
                foreach (var danger in snapshot.DangerAreas)
                    Check(!danger.Geometry.Contains(p) || danger.DangerWindows.All(w => !new DangerWindow(w.DangerFrom.AddSeconds(-0.25), w.DangerUntil?.AddSeconds(0.3)).IsActiveAt(t)),
                        "emergency path crosses a still-active damage window");
            }
        }
    }

    // ARR 2026.09.18 23.02.30: three 49194 casts at 49.257s, 49193 at 49.538s.
    internal static SafeZoneSnapshot RecordedSnapshot()
    {
        using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
        manager.SetArenaBoundary(new CircleGeometry(new(120, 0, -420), 20));
        manager.AddDangerArea(new DonutTemplate(19.5f, 23.5f), new StaticTransform(new(120,0,-420),0), [new DangerWindow(Epoch,null)]);
        foreach (var line in new[] { (137.3159f, -430.0145f, -62.5818f), (131.7311f, -403.83f, -138.4326f), (112.2606f, -410.8186f, 149.3037f) })
            Add(line.Item1, line.Item2, line.Item3, 2, 100, 51.257);
        Add(119.9512f, -422.7817f, -38.3951f, 40, 50, 55.338);
        return manager.BuildSnapshot(Epoch.AddSeconds(49.6));

        void Add(float x, float z, float angle, float width, float length, double activation)
        {
            var rotation = angle * MathF.PI / 180;
            var center = new Vector3(x + MathF.Sin(rotation) * length / 2, 0, z + MathF.Cos(rotation) * length / 2);
            manager.AddDangerArea(new RectangleTemplate(width + 1, length + 1), new StaticTransform(center, rotation),
                [new DangerWindow(Epoch.AddSeconds(activation), Epoch.AddSeconds(activation + 0.6))]);
        }
    }

    private static SafeZoneSnapshot MakeSnapshot(IGeometryShape geometry)
    {
        using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
        manager.SetArenaBoundary(new RectangleGeometry(new(120, 0, -420), 0, 40, 40));
        manager.AddDangerArea(new GeometryTemplateForTest(geometry), new StaticTransform(geometry.Position, geometry.Rotation),
            [new DangerWindow(Epoch.AddSeconds(2), Epoch.AddSeconds(2.6))]);
        return manager.BuildSnapshot(Epoch);
    }
    private sealed record GeometryTemplateForTest(IGeometryShape Shape) : GeometryTemplate
    {
        public override GeometryKind Kind => Shape.Kind;
        public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => Shape;
    }
    private sealed class GenericGeometry(IGeometryShape shape) : IGeometryShape
    {
        public GeometryKind Kind => shape.Kind;
        public Vector3 Position => shape.Position;
        public float Rotation => shape.Rotation;
        public bool Contains(Vector3 point, float epsilon = 0.0001f) => shape.Contains(point, epsilon);
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = 0.0001f) => shape.IntersectsSegment(start, end, epsilon);
        public GeometryBounds GetBounds() => shape.GetBounds();
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => shape.GetContours(segments);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

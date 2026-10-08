using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class NavigationPerformance
{
    public static void Run()
    {
        var now = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
        var capability = new MovementCapability(6, 6, 0, false, 6, 6);
        foreach (var course in Enum.GetValues<BlundervilleCourse>().Where(c => c != BlundervilleCourse.None))
        {
            var from = BlundervilleRoutes.VerifiedStart(course);
            var target = BlundervilleRoutes.Nodes(course, from).First(n => Vector3.Distance(n.Position, from) > 3).Position;
            MeasureCase(course.ToString(), course, from, target, .8f, 28, .5f);
        }
        MeasureCase("FirstB narrow", BlundervilleCourse.FirstB, new(10.25f, 10, -124), new(10.25f, 10, -133), .2f, 16, .1f);

        void MeasureCase(string name, BlundervilleCourse course, Vector3 from, Vector3 target, float goalRadius, float radius, float margin)
        {
            var middle = (from + target) / 2;
            var center = new Vector3(MathF.Round(middle.X / 10) * 10, from.Y, MathF.Round(middle.Z / 10) * 10);
            var localType = typeof(BlundervilleNavigation).GetNestedType("LocalCorridor", BindingFlags.NonPublic)!;
            var boundary = (IGeometryShape)Activator.CreateInstance(localType, [BlundervilleRoutes.Boundary(course), center, radius])!;
            var settings = new SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true, RuntimeArenaSafetyMargin = margin };
            using var manager = new SafeZoneManager(settings);
            manager.SetArenaBoundary(boundary);
            manager.AddGoalArea(new CircleTemplate(goalRadius), new StaticTransform(target, 0), [GoalWindow.Persistent(now, 100, PositionPreference.Center)]);
            var snapshot = manager.BuildSnapshot(now);
            var player = new PlayerState(from, 0, 6, false, false, 0, true, snapshot.Version);
            var options = new SolveOptions { ArenaSafetyMargin = margin, GenerateDebugData = true };
            var solver = new SafeZoneSolver();
            SafeZoneSolution? solution = null;
            Measure(name + " raster", 12, () => RasterizedMap.Build(snapshot, from, options, now));
            Measure(name + " solve", 12, () => solution = solver.Solve(snapshot, player, capability, options, now));
            if (solution is not { IsSuccess: true } || solution.ExecutablePath.Count < 2)
                throw new InvalidOperationException(name + ": benchmark route unavailable");
            Console.WriteLine($"INFO {name}: contour edges={boundary.GetContours().Sum(c => c.Count)}, path points={solution.ExecutablePath.Count}");
            using var movement = new SafeZoneRebornMovementSystem(settings, manager, () => false, () => false);
            movement.Attach(new FakeMove());
            typeof(SafeZoneRebornMovementSystem).GetField("_latestSolution", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(movement, solution);
            // Freeze solve age below TTL: isolate real per-frame validation/dispatch,
            // without asynchronous work or game services contaminating the sample.
            Measure(name + " frame", 200, () => movement.Update(player, capability, now.AddMilliseconds(30)));
        }
    }

    public static void Radar()
    {
        foreach (var course in Enum.GetValues<BlundervilleCourse>().Where(c => c != BlundervilleCourse.None))
        {
            var boundary = BlundervilleRoutes.Boundary(course);
            var layout = MasterPlayer.Radar.RadarArenaLayout.Create(boundary, boundary.Position, Vector2.Zero, 150, 0);
            var clip = new MasterPlayer.Radar.RadarClipRegion(layout.Outline, Vector2.Zero, 150);
            Measure(course + " radar background clip", 5, () => { foreach (var part in clip.ClipPolygon(clip.Outline)) _ = part.Count; });
            var watch = Stopwatch.StartNew();
            foreach (var contour in boundary.GetContours())
                for (var i = 0; i < contour.Count; ++i)
                    foreach (var segment in clip.ClipSegment(layout.Projection.Project(contour[i]), layout.Projection.Project(contour[(i + 1) % contour.Count]))) _ = segment;
            Console.WriteLine($"PERF {course} old full boundary stroke: {watch.Elapsed.TotalMilliseconds:F2}ms");
            var center = BlundervilleRoutes.VerifiedStart(course);
            var projection = new MasterPlayer.Radar.RadarProjection(center, Vector2.Zero, 150f / 25, 0);
            var localClip = new MasterPlayer.Radar.RadarClipRegion(MasterPlayer.Radar.RadarClipRegion.Circle(Vector2.Zero, 150), Vector2.Zero, 150);
            Measure(course + " local boundary stroke", 12, () => {
                foreach (var (a, b) in MasterPlayer.Radar.RadarLocalGeometry.Segments(boundary, center, 25))
                    foreach (var segment in localClip.ClipSegment(projection.Project(a), projection.Project(b))) _ = segment;
            });
        }
    }

    private static void Measure(string name, int count, Action action)
    {
        for (var i = 0; i < 5; ++i) action();
        var times = new double[count];
        var allocations = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; ++i)
        {
            var start = Stopwatch.GetTimestamp();
            action();
            times[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }
        allocations = (GC.GetAllocatedBytesForCurrentThread() - allocations) / count;
        Array.Sort(times);
        Console.WriteLine($"PERF {name}: median={times[count / 2]:F3}ms p95={times[(int)(count * .95)]:F3}ms allocated={allocations}B/op");
    }

    private sealed class FakeMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _route = [];
        public bool IsRunning => _route.Count != 0;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _route[0] : null,
            IsRunning ? _route[^1] : null, 0, _route.Count, Math.Max(0, _route.Count - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) => _route = points;
        public void Stop() => _route = [];
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> points) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => throw new NotSupportedException();
    }
}

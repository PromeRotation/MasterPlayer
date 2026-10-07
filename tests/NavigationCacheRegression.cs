using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class NavigationCacheRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        foreach (var course in Enum.GetValues<BlundervilleCourse>().Where(c => c != BlundervilleCourse.None))
            run($"Blunderville performance {course}: indexed segments agree with complete contours", () =>
            {
                var source = BlundervilleRoutes.Boundary(course);
                var indexed = new Prepared(source);
                var random = new Random(1165);
                var bounds = source.GetBounds();
                Vector3 Point() => new(bounds.MinX + (float)random.NextDouble() * bounds.Width, 0,
                    bounds.MinZ + (float)random.NextDouble() * bounds.Height);
                for (var i = 0; i < 1200; ++i)
                {
                    var from = Point(); var to = Point();
                    Check(ArenaBoundaryTraversal.ContainsSegment(indexed, from, to) == ArenaBoundaryTraversal.ContainsSegment(source, from, to),
                        $"Indexed traversal differs for {from} -> {to}.");
                }
            });

        run("Blunderville performance: terrain is reused while hazards, obstacles and time change", () =>
        {
            var boundary = new Prepared(new RectangleGeometry(Vector3.Zero, 0, 12, 12));
            var options = new SolveOptions { GridResolution = .5f, ArenaSafetyMargin = .1f };
            var clear = Snapshot(boundary);
            var original = RasterizedMap.Build(clear, Vector3.Zero, options, Epoch);
            var queries = boundary.Queries;
            Compare(original, RasterizedMap.Build(clear, Vector3.UnitY * 2, options, Epoch));
            Check(queries == boundary.Queries, "Stable terrain was rasterized again.");
            var danger = new DangerAreaSnapshot(default, new CircleGeometry(new(2, 0, 1), 1), [new(Epoch.AddSeconds(1), Epoch.AddSeconds(3))]);
            var obstacle = new ObstacleSnapshot(default, new RectangleGeometry(new(-2, 0, 1), .3f, 1, 3));
            foreach (var time in new[] { Epoch, Epoch.AddSeconds(2), Epoch.AddSeconds(4) })
            {
                var updated = new SafeZoneSnapshot(1, time, boundary, [obstacle], [danger], []);
                var reference = new SafeZoneSnapshot(1, time, boundary.Source, [obstacle], [danger], []);
                Compare(RasterizedMap.Build(reference, Vector3.Zero, options, time), RasterizedMap.Build(updated, Vector3.Zero, options, time));
            }
            Check(queries == boundary.Queries, "Dynamic areas invalidated static terrain.");
            Compare(original, RasterizedMap.Build(clear, Vector3.Zero, options, Epoch));
        });

        run("Blunderville performance: changed grid, margin, bounds and boundary invalidate terrain", () =>
        {
            var boundary = new Prepared(new RectangleGeometry(Vector3.Zero, 0, 12, 12));
            foreach (var resolution in new[] { .5f, .2f, .5f })
                foreach (var margin in new[] { 0f, .1f, .5f })
                    foreach (var position in new[] { Vector3.Zero, Vector3.UnitX * 8 })
                    {
                        var options = new SolveOptions { GridResolution = resolution, ArenaSafetyMargin = margin };
                        Compare(RasterizedMap.Build(Snapshot(boundary.Source), position, options, Epoch),
                            RasterizedMap.Build(Snapshot(boundary), position, options, Epoch));
                    }
            var replacement = new Prepared(new RectangleGeometry(Vector3.Zero, 0, 4, 12));
            Compare(RasterizedMap.Build(Snapshot(replacement.Source), Vector3.Zero, new(), Epoch),
                RasterizedMap.Build(Snapshot(replacement), Vector3.Zero, new(), Epoch));
        });

        run("Blunderville performance: parallel cold and cached builds keep independent maps", () =>
        {
            var boundary = new Prepared(new RectangleGeometry(Vector3.Zero, .2f, 8, 10));
            var expected = RasterizedMap.Build(Snapshot(boundary.Source), Vector3.Zero, new(), Epoch);
            Parallel.For(0, 16, _ => Compare(expected, RasterizedMap.Build(Snapshot(boundary), Vector3.Zero, new(), Epoch)));
        });

        run("Blunderville performance: pooled searches do not retain an earlier route or damage map", () =>
        {
            var capability = new MovementCapability(6, 6, 0, false, 6, 6);
            var solver = new SafeZoneSolver();
            var options = new SolveOptions();
            var boundary = new RectangleGeometry(Vector3.Zero, 0, 12, 12);
            var goal = new GoalAreaSnapshot(default, new CircleGeometry(new(4, 0, 0), 1), [GoalWindow.Persistent(Epoch)]);
            var clear = new SafeZoneSnapshot(1, Epoch, boundary, [], [], [goal]);
            var player = new PlayerState(Vector3.Zero, 0, 6, false, false, 0, true, 1);
            var original = solver.Solve(clear, player, capability, options, Epoch);
            Check(original.IsSuccess && original.ExecutablePath.Count >= 2, "Expected a traversable reference route.");
            var saved = original.ExecutablePath.ToArray();
            for (var i = 0; i < 20; ++i)
            {
                var blocked = new SafeZoneSnapshot(2, Epoch, boundary,
                    [new(default, new RectangleGeometry(new(2, 0, 0), 0, 2, 16))], [], [goal]);
                var stop = solver.Solve(blocked, player, capability, options, Epoch);
                Check(stop.TargetPosition is not { X: > 1 }, "A previous pooled route leaked through the new wall.");
                var next = solver.Solve(clear, player, capability, options, Epoch);
                Check(next.IsSuccess && next.ExecutablePath.SequenceEqual(saved), "Pool reuse changed the clean route.");
                Check(original.ExecutablePath.SequenceEqual(saved), "Published route points refer to pooled scratch memory.");
            }
        });
    }

    private static SafeZoneSnapshot Snapshot(IGeometryShape boundary) => new(1, Epoch, boundary, [], [], []);
    private static void Compare(RasterizedMap expected, RasterizedMap actual)
    {
        Check(expected.Columns == actual.Columns && expected.Rows == actual.Rows && expected.Resolution == actual.Resolution, "Grid dimensions differ.");
        for (var row = 0; row < expected.Rows; ++row)
            for (var col = 0; col < expected.Columns; ++col)
                Check(expected.PixelMaxGAt(col, row) == actual.PixelMaxGAt(col, row)
                    && expected.PixelMinGAt(col, row) == actual.PixelMinGAt(col, row)
                    && expected.IsDangerCoreAt(col, row) == actual.IsDangerCoreAt(col, row), $"Grid differs at ({col},{row}).");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Prepared(IGeometryShape source) : IStaticArenaBoundary
    {
        public IGeometryShape Source => source;
        private int _queries;
        public int Queries => _queries;
        public ArenaBoundaryIndex TraversalIndex { get; } = new(source.GetContours());
        public GeometryKind Kind => source.Kind;
        public Vector3 Position => source.Position;
        public float Rotation => source.Rotation;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        { Interlocked.Increment(ref _queries); return source.Contains(point, epsilon); }
        public bool IntersectsSegment(Vector3 from, Vector3 to, float epsilon = GeometryHelper.DefaultEpsilon) => source.IntersectsSegment(from, to, epsilon);
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => source.GetContours(segments);
        public GeometryBounds GetBounds() => source.GetBounds();
    }
}

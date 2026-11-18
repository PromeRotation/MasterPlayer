using System.Numerics;
using MasterPlayer.Follow;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;

internal static class Pt30FollowFootprintTests
{
    public static void Run()
    {
        var center = new Vector3(-300, 0, -300);
        foreach (var blocked in new[] { false, true })
        {
            var boundary = new CountedBoundary(Pt30Arena.Boundary(0));
            var dangers = blocked ? new IGeometryShape[] { new CircleGeometry(center, 100) } : [];
            var template = new DangerAwareFollowGoalTemplate(39, dangers, boundary);
            var oldResult = Legacy(template.Resolve(center, 0, null), center, 39);
            var oldCalls = boundary.Calls;
            boundary.Calls = 0;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = template.HasSafeArea(center);
            if (result != oldResult || result == blocked || boundary.Calls >= oldCalls
                || !blocked && boundary.Calls >= oldCalls / 2)
                throw new InvalidOperationException($"PT30 footprint mismatch: blocked={blocked}, result={result}, calls={boundary.Calls}/{oldCalls}");
            Console.WriteLine($"PT30 follow footprint blocked={blocked}: boundary checks {oldCalls} -> {boundary.Calls}, {timer.Elapsed.TotalMilliseconds:F3}ms; original lattice result retained.");
        }
    }

    private static bool Legacy(IGeometryShape goal, Vector3 center, float radius)
    {
        var step = OptionalGoalSafety.SampleStep;
        var corner = step / 2;
        for (var z = -radius + corner; z < radius; z += step)
            for (var x = -radius + corner; x < radius; x += step)
            {
                var p = center + new Vector3(x, 0, z);
                if (goal.Contains(p) && goal.Contains(p + new Vector3(-corner, 0, -corner))
                    && goal.Contains(p + new Vector3(corner, 0, -corner))
                    && goal.Contains(p + new Vector3(-corner, 0, corner))
                    && goal.Contains(p + new Vector3(corner, 0, corner))) return true;
            }
        return false;
    }

    private sealed class CountedBoundary(IGeometryShape shape) : IGeometryShape
    {
        public int Calls;
        public GeometryKind Kind => shape.Kind;
        public Vector3 Position => shape.Position;
        public float Rotation => shape.Rotation;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) { Calls++; return shape.Contains(point, epsilon); }
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => shape.IntersectsSegment(start, end, epsilon);
        public GeometryBounds GetBounds() => shape.GetBounds();
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => shape.GetContours(segments);
    }
}

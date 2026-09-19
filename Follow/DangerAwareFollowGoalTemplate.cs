using System.Numerics;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Follow;

// Optional attack uptime must not outrank an announced hit just because it has not activated yet.
internal sealed record DangerAwareFollowGoalTemplate : GeometryTemplate
{
    private readonly float _radius;
    private readonly GoalExclusion[] _dangers;
    private readonly IGeometryShape? _boundary;
    public DangerAwareFollowGoalTemplate(float radius, IEnumerable<IGeometryShape> dangers, IGeometryShape? boundary = null)
    {
        _radius = radius;
        _dangers = dangers.Select(d => new GoalExclusion(d)).ToArray();
        _boundary = boundary;
    }
    public override GeometryKind Kind => GeometryKind.Circle;
    public bool HasSafeArea(Vector3 position)
    {
        if (_dangers.Length == 0 && _boundary == null) return true;
        var goal = Resolve(position, 0, null);
        var step = OptionalGoalSafety.SampleStep;
        var corner = step / 2;
        for (var z = -_radius + step / 2; z < _radius; z += step)
            for (var x = -_radius + step / 2; x < _radius; x += step)
            {
                var p = position + new Vector3(x,0,z);
                // A usable footprint matters, not its percentage of a large boss's attack circle.
                // Stop after the first witness; resolving worker snapshots never scans the whole circle.
                if (goal.Contains(p) && goal.Contains(p + new Vector3(-corner,0,-corner))
                    && goal.Contains(p + new Vector3(corner,0,-corner))
                    && goal.Contains(p + new Vector3(-corner,0,corner))
                    && goal.Contains(p + new Vector3(corner,0,corner))) return true;
            }
        return false;
    }
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
    {
        var circle = new CircleGeometry(position, _radius);
        return _dangers.Length == 0 && _boundary == null ? circle : new ExcludedGoalGeometry(circle, _dangers, _boundary);
    }

    private sealed class ExcludedGoalGeometry(CircleGeometry circle, GoalExclusion[] dangers, IGeometryShape? boundary) : IGeometryShape
    {
        public GeometryKind Kind => GeometryKind.Circle;
        public Vector3 Position => circle.Position;
        public float Rotation => 0;
        public bool Contains(Vector3 point, float epsilon = 0.0001f)
            => circle.Contains(point, epsilon) && (boundary == null || boundary.Contains(point, epsilon))
                && !dangers.Any(d => d.Contains(point, epsilon));
        public GeometryBounds GetBounds() => circle.GetBounds();
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = 0.0001f)
        {
            if (Contains(start, epsilon) || Contains(end, epsilon)) return true;
            var delta = end - start;
            var offset = start - Position;
            var a = delta.X * delta.X + delta.Z * delta.Z;
            if (a < 1e-10f) return false;
            var b = offset.X * delta.X + offset.Z * delta.Z;
            var radius = circle.Radius + epsilon;
            var c = offset.X * offset.X + offset.Z * offset.Z - radius * radius;
            var discriminant = b * b - a * c;
            if (discriminant < 0) return false;
            var root = MathF.Sqrt(discriminant);
            var lo = Math.Max(0, (-b - root) / a);
            var hi = Math.Min(1, (-b + root) / a);
            if (lo > hi) return false;
            // Goal rasterization uses exact Contains at every cell corner. Segment queries only
            // report a witnessed safe portion; complex masks must never create a false safe hit.
            var samples = Math.Max(1, (int)MathF.Ceiling((hi - lo) * MathF.Sqrt(a) / 0.05f));
            for (var i = 0; i <= samples; ++i)
                if (Contains(start + delta * (lo + (hi - lo) * i / samples), epsilon)) return true;
            return false;
        }
        // A circle outline would incorrectly mark excluded damage as a destination.
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
    }
}

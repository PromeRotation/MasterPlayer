using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Hunts;

// The dangerous region is the complement of the union, including beyond the candidates' finite bounds.
internal sealed class HuntStackJoinGeometry(Vector3[] centers, float radius) : GeometryShape(Vector3.Zero)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var safeRadius = MathF.Max(0, radius - epsilon);
        return !centers.Any(c => Vector2.DistanceSquared(new(c.X, c.Z), new(point.X, point.Z)) <= safeRadius * safeRadius);
    }

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var direction = new Vector2(end.X - start.X, end.Z - start.Z);
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared < .0000001f) return Contains(start, epsilon);
        var safeRadius = MathF.Max(0, radius - epsilon);
        var intervals = new List<(float From, float Until)>();
        foreach (var center in centers)
        {
            var offset = new Vector2(start.X - center.X, start.Z - center.Z);
            var b = Vector2.Dot(offset, direction);
            var c = offset.LengthSquared() - safeRadius * safeRadius;
            var discriminant = b * b - lengthSquared * c;
            if (discriminant < 0) continue;
            var root = MathF.Sqrt(discriminant);
            intervals.Add(((-b - root) / lengthSquared, (-b + root) / lengthSquared));
        }
        var covered = 0f;
        foreach (var interval in intervals.OrderBy(i => i.From))
        {
            if (interval.Until < covered) continue;
            if (interval.From > covered) return true;
            covered = interval.Until;
            if (covered >= 1) return false;
        }
        return true;
    }

    public override GeometryBounds GetBounds()
        => new(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}

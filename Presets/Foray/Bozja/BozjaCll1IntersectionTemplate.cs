using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// SDIntersection of inverted rectangles keeps the original axes, including
// non-finite zero-length input. It is consumed through the existing sampler.
internal sealed record BozjaCll1IntersectionTemplate(
    (Vector2 Origin, Vector2 Direction, float Length, float HalfWidth)[] Rectangles) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    internal float Distance(float x, float z)
    {
        var maximum = float.MinValue;
        foreach (var rect in Rectangles)
        {
            var dx = x - rect.Origin.X;
            var dz = z - rect.Origin.Y;
            var along = dx * rect.Direction.X + dz * rect.Direction.Y;
            var side = dx * rect.Direction.Y - dz * rect.Direction.X;
            var front = along - rect.Length;
            var back = -along;
            var left = side - rect.HalfWidth;
            var right = -side - rect.HalfWidth;
            var maxAlong = front > back ? front : back;
            var maxSide = left > right ? left : right;
            var distance = maxAlong > maxSide ? -maxAlong : -maxSide;
            if (distance > maximum) maximum = distance;
        }
        return maximum;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
        => GeometryGridSampler.Sample(center, radius, Distance);

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => throw new InvalidOperationException("CLL1 intersections require the finite navigation sampler.");
}

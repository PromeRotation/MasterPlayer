using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaSeekerShelterTemplate(IGeometryShape SafeShape) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    internal float Distance(float x, float z)
    {
        if (!SafeShape.Contains(new(x, SafeShape.Position.Y, z), 0)) return 0;
        var point = new Vector2(x, z);
        var squared = float.PositiveInfinity;
        foreach (var contour in SafeShape.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = new Vector2(contour[i].X, contour[i].Z);
                var b = new Vector2(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z);
                var delta = b - a;
                var t = delta.LengthSquared() == 0 ? 0 : Math.Clamp(Vector2.Dot(point - a, delta) / delta.LengthSquared(), 0, 1);
                squared = MathF.Min(squared, Vector2.DistanceSquared(point, a + delta * t));
            }
        var distance = MathF.Sqrt(squared);
        return distance;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
        => GeometryGridSampler.Sample(center, radius, Distance);

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => SafeShape;
}

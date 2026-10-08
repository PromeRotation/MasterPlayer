using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Radar;

internal static class RadarLocalGeometry
{
    public static bool Intersects(GeometryBounds bounds, Vector3 center, float radius)
        => bounds.MaxX >= center.X - radius && bounds.MinX <= center.X + radius
        && bounds.MaxZ >= center.Z - radius && bounds.MinZ <= center.Z + radius;

    public static IEnumerable<(Vector3 A, Vector3 B)> Segments(IGeometryShape boundary, Vector3 center, float radius)
    {
        foreach (var contour in boundary.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = contour[i]; var b = contour[(i + 1) % contour.Count];
                if (Intersects(new(MathF.Min(a.X, b.X), MathF.Min(a.Z, b.Z), MathF.Max(a.X, b.X), MathF.Max(a.Z, b.Z)), center, radius))
                    yield return (a, b);
            }
    }
}

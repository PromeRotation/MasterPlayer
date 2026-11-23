using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Radar;

internal readonly record struct RadarArenaLayout(RadarProjection Projection, IReadOnlyList<Vector2> Outline, float WorldRadius,
    IReadOnlyList<IReadOnlyList<Vector2>>? Outlines = null, IReadOnlyList<IReadOnlyList<Vector2>>? FillPolygons = null)
{
    public static RadarArenaLayout Create(IGeometryShape boundary, Vector3 center, Vector2 screenCenter,
        float halfSize, float rotation, IEnumerable<IGeometryShape>? extraBounds = null)
    {
        var contours = boundary.GetContours().Where(c => c.Count >= 3).ToArray();
        var contour = contours.FirstOrDefault() ?? [];
        var unitProjection = new RadarProjection(center, Vector2.Zero, 1, rotation);
        var extent = 1f;
        var worldRadius = 1f;
        var points = contours.SelectMany(c => c);
        if (extraBounds != null) points = points.Concat(extraBounds.SelectMany(s => s.GetContours()).SelectMany(c => c));
        foreach (var point in points)
        {
            var local = unitProjection.Project(point);
            extent = Math.Max(extent, Math.Max(Math.Abs(local.X), Math.Abs(local.Y)));
            worldRadius = Math.Max(worldRadius, local.Length());
        }
        var scale = Math.Max(halfSize - 4, 1) / extent;
        var projection = new RadarProjection(center, screenCenter, scale, rotation);
        var outlines = contours.Select(c => (IReadOnlyList<Vector2>)c.Select(projection.Project).ToArray()).ToArray();
        var fill = boundary is PolygonRegionGeometry region
            ? region.FillPolygons.Select(c => (IReadOnlyList<Vector2>)c.Select(projection.Project).ToArray()).ToArray() : null;
        return new(projection, contour.Select(projection.Project).ToArray(), worldRadius, outlines, fill);
    }
}

internal readonly record struct RadarViewportLayout(float Radius, float Margin)
{
    public float FullSize => 2 * (Radius + Margin);
    public static float CompassMargin(float fontSize) => Math.Max(20, 12 + fontSize / 2 + 3);
    public static RadarViewportLayout Create(float scale, float fontSize, Vector2 available)
    {
        var margin = CompassMargin(fontSize);
        var maximum = Math.Max(24, Math.Min(available.X, available.Y) / 2 - margin - 8);
        return new(Math.Min(150 * (float.IsFinite(scale) ? Math.Clamp(scale, 0.3f, 3) : 1), maximum), margin);
    }
}

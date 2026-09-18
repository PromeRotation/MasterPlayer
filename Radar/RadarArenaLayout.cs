using System.Numerics;
using PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Radar;

internal readonly record struct RadarArenaLayout(RadarProjection Projection, IReadOnlyList<Vector2> Outline, float WorldRadius)
{
    public static RadarArenaLayout Create(IGeometryShape boundary, Vector3 center, Vector2 screenCenter,
        float halfSize, float rotation)
    {
        var contour = boundary.GetContours().FirstOrDefault(c => c.Count >= 3) ?? [];
        var unitProjection = new RadarProjection(center, Vector2.Zero, 1, rotation);
        var extent = 1f;
        var worldRadius = 1f;
        foreach (var point in contour)
        {
            var local = unitProjection.Project(point);
            extent = Math.Max(extent, Math.Max(Math.Abs(local.X), Math.Abs(local.Y)));
            worldRadius = Math.Max(worldRadius, local.Length());
        }
        var scale = Math.Max(halfSize - 4, 1) / extent;
        var projection = new RadarProjection(center, screenCenter, scale, rotation);
        return new(projection, contour.Select(projection.Project).ToArray(), worldRadius);
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

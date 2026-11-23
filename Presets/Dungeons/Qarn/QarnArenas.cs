using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class QarnArenas
{
    internal static LocalPolygonGeometry Teratotaur(float y) => Polygon(y,
    [
        new(-94.9f, -59), new(-70.2f, -46.1f), new(-55.3f, -46.6f),
        new(-55.7f, -55.6f), new(-51.1f, -60.9f), new(-51.2f, -65),
        new(-58.1f, -67.7f), new(-64.7f, -70.6f), new(-88.4f, -72.2f),
        new(-89, -66.2f), new(-94.9f, -65.5f),
    ]);

    internal static LocalPolygonGeometry TempleGuardian(float y) => Polygon(y,
    [
        new(66.5f, -33.7f), new(58.6f, -25), new(51.4f, -22.5f),
        new(39.3f, -16.5f), new(36.6f, -5), new(39.3f, 5.7f),
        new(41.1f, 16), new(56.5f, 14.8f), new(63.6f, 7.1f),
        new(64.7f, 3.3f), new(70.3f, -3.9f), new(72.6f, -33.3f),
    ]);

    private static LocalPolygonGeometry Polygon(float y, Vector2[] points)
    {
        var center = new Vector2((points.Min(p => p.X) + points.Max(p => p.X)) / 2,
            (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2);
        // LocalPolygonGeometry's zero-rotation basis points towards -X/-Z.
        return new(new(center.X, y, center.Y), 0, points.Select(p => center - p));
    }
}

using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ShinryuArenas
{
    internal static IGeometryShape Broken(Vector3? activeCenter, IReadOnlyList<Vector3> broken, float y)
    {
        if (activeCenter == null) return new PolygonRegionGeometry(new(0, y, 0), [], [], []);
        var original = new LocalPolygonGeometry(activeCenter.Value, 0, [new(-30, -30), new(30, -30), new(30, 30), new(-30, 30)]);
        var rings = broken.Distinct().Select(p =>
        {
            var offset = new Vector2(p.X - original.Position.X, p.Z - original.Position.Z);
            return (Inner: Array.Empty<Vector2>(), Outer: new Vector2[]
                { offset + new Vector2(-10, -10), offset + new Vector2(10, -10), offset + new Vector2(10, 10), offset + new Vector2(-10, 10) });
        }).ToArray();
        var region = (PolygonRegionGeometry)DungeonPolygonDifference.Build(original, rings);
        var contours = region.GetContours();
        var points = contours.SelectMany(c => c).ToArray();
        var center = points.Length == 0 ? new Vector3(0, y, 0) : new Vector3(
            (points.Min(p => p.X) + points.Max(p => p.X)) / 2, y, (points.Min(p => p.Z) + points.Max(p => p.Z)) / 2);
        var triangles = region.FillPolygons.Select(p => p.Select(v => new Vector2(v.X - center.X, v.Z - center.Z)).ToArray()).ToArray();
        return new PolygonRegionGeometry(center, triangles, [], triangles, contours);
    }
}

using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// Source SDIntersection of inverted tile rectangles. An empty intersection
// has float.MinValue everywhere; the existing bridge rejects that full grid.
internal sealed record BozjaQueenChessTemplate(Vector3[] Tiles) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    internal float Distance(float x, float z)
    {
        var maximum = float.MinValue;
        foreach (var tile in Tiles)
        {
            var distance = -MathF.Max(MathF.Abs(z - tile.Z) - 5, MathF.Abs(x - tile.X) - 5);
            if (distance > maximum) maximum = distance;
        }
        return maximum;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
        => GeometryGridSampler.Sample(center, radius, Distance);

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new RectangleGeometry(position, 0, 10, 10);
}

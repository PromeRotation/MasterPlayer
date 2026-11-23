using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AitiascopeRhitahtynArena
{
    internal static IGeometryShape Create(float y, IReadOnlyList<Vector3> pockets)
    {
        var rectangles = new List<Vector2[]> { Rectangle(0, 0, 6.5f, 19.5f) };
        rectangles.AddRange(pockets.Select(p => Rectangle(p.X - 11, p.Z - 144, 2, 1.5f)));
        var triangles = rectangles.SelectMany(p => new Vector2[][] { [p[0], p[1], p[2]], [p[0], p[2], p[3]] }).ToArray();
        return new PolygonRegionGeometry(new(11, y, 144), rectangles.ToArray(), [], triangles);
    }

    private static Vector2[] Rectangle(float x, float z, float halfWidth, float halfLength)
        => [new(x - halfWidth, z - halfLength), new(x + halfWidth, z - halfLength),
            new(x + halfWidth, z + halfLength), new(x - halfWidth, z + halfLength)];
}

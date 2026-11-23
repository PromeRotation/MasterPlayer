using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NecronArenas
{
    internal static readonly Vector2[] PRISONS =
    [
        new(100, -100), new(300, -100), new(300, 100), new(300, 300),
        new(100, 300), new(-100, 300), new(-100, 100), new(-100, -100),
    ];

    internal static IGeometryShape Circle(float y) => Regions(new(100, y, 100), [Polygon(Vector2.Zero, 9)]);
    internal static IGeometryShape Split(float y) => Regions(new(100, y, 100),
    [
        [new(-18, -15), new(-6, -15), new(-6, 15), new(-18, 15)],
        [new(6, -15), new(18, -15), new(18, 15), new(6, 15)],
    ]);
    internal static IGeometryShape Prison(int index, float y)
    {
        var position = PRISONS[index];
        var centerOffset = new Vector2(6.875f, -8);
        return Regions(new(position.X + centerOffset.X, y, position.Y + centerOffset.Y),
        [
            Polygon(-centerOffset, 9.5f), Polygon(new Vector2(-5, -21) - centerOffset, 4.5f),
            Polygon(new Vector2(14, -14) - centerOffset, 4.5f), Polygon(new Vector2(20, 0) - centerOffset, 3.25f),
        ]);
    }

    private static Vector2[] Polygon(Vector2 center, float radius) => Enumerable.Range(0, 32)
        .Select(i => center + radius * new Vector2(MathF.Sin(MathF.Tau * i / 32), MathF.Cos(MathF.Tau * i / 32))).ToArray();

    private static IGeometryShape Regions(Vector3 center, Vector2[][] islands)
    {
        var triangles = new List<Vector2[]>();
        foreach (var island in islands)
            for (var i = 1; i + 1 < island.Length; ++i) triangles.Add([island[0], island[i], island[i + 1]]);
        return new PolygonRegionGeometry(center, islands, [], triangles.ToArray());
    }
}

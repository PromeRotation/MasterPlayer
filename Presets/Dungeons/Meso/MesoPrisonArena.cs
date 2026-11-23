using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MesoPrisonArena
{
    internal static IGeometryShape Build(float y, IReadOnlyList<Vector3> prisons)
    {
        var original = (LocalPolygonGeometry)MesoArenas.Headsmen(y);
        var center = original.Position;
        var rings = prisons.Select(p =>
        {
            var offset = new Vector2(p.X - center.X, p.Z - center.Z);
            Vector2[] Ring(float radius) => Enumerable.Range(0, 40).Select(i =>
            {
                var a = -i * MathF.Tau / 40;
                return offset + radius * new Vector2(MathF.Sin(a), MathF.Cos(a));
            }).ToArray();
            return (Inner: Ring(8), Outer: Ring(8.25f));
        }).ToArray();
        return DungeonPolygonDifference.Build(original, rings);
    }
}

using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal static class PalaceArena
{
    internal static readonly Vector3 Center = new(-300, 0, -300);
    // BMR SharedBounds: regular 64-gon, with the entrance rectangle removed.
    internal static readonly IGeometryShape Boundary = new WorldPolygonGeometry(Create());

    private static Vector3[] Create()
    {
        const float radius = 24.256f;
        const float entranceZ = -325.134f + 1.55f;
        var ring = Enumerable.Range(0, 64).Select(i => Center + new Vector3(
            MathF.Sin(i * MathF.Tau / 64) * radius, 0, MathF.Cos(i * MathF.Tau / 64) * radius)).ToArray();
        var clipped = new List<Vector3>();
        for (var i = 0; i < ring.Length; ++i)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var insideA = a.Z >= entranceZ;
            var insideB = b.Z >= entranceZ;
            if (insideA) clipped.Add(a);
            if (insideA != insideB) clipped.Add(Vector3.Lerp(a, b, (entranceZ - a.Z) / (b.Z - a.Z)));
        }
        return clipped.ToArray();
    }
}

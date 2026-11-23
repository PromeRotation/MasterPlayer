using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MistwakeRocks
{
    internal static readonly (float X, float Z, float Radius, float Degrees)[] Rocks =
    [
        (72.00001f, 357.50009f, 2.5f, 95.096f), (84.0001f, 359, 2, -4.837f),
        (85.99999f, 359, 1.5f, 40), (94.00008f, 354.99979f, 2, 140.886f),
        (97.00001f, 363, 1.5f, -60), (71.50004f, 370.00009f, 2, -69.402f),
        (74.49998f, 368.00009f, 2.5f, -39.322f), (90.5f, 370, 1.5f, 60),
        (93.00002f, 372, 2, -70), (74.99999f, 378, 2.5f, -128.896f),
        (89.50005f, 380, 2, 22.477f), (71.5f, 383, 2, 80), (96.99998f, 383, 2.5f, 175.832f),
    ];

    internal static IGeometryShape Build(float y, IReadOnlySet<int> removed)
    {
        var center = new Vector3(84, y, 370);
        var square = new LocalPolygonGeometry(center, 0, [new(-19.5f, -19.5f), new(19.5f, -19.5f), new(19.5f, 19.5f), new(-19.5f, 19.5f)]);
        var cuts = Rocks.Select((r, index) => (r, index)).Where(v => !removed.Contains(v.index)).Select(v =>
        {
            var r = v.r;
            var points = Enumerable.Range(0, 16).Select(i =>
            {
                var angle = (15 - i) * MathF.Tau / 16 + r.Degrees * MathF.PI / 180;
                var (sin, cos) = Math.SinCos(angle);
                return new Vector2(r.X - 84 + r.Radius * (float)sin, r.Z - 370 + r.Radius * (float)cos);
            }).ToArray();
            return (Inner: Array.Empty<Vector2>(), Outer: points);
        }).ToArray();
        return DungeonPolygonDifference.Build(square, cuts);
    }
}

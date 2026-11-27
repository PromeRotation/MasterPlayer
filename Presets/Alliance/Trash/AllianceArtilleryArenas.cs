// Generated from resolved runtime polygons using --alliance-artillery-arenas.
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AllianceArtilleryArenas
{
    internal const float Stage3aRadius = 44.5f;
    internal static IGeometryShape Stage3a(float y, Vector3? center = null) => new PolygonRegionGeometry(center ?? new(200f, y, -416.5f),
    [
        [new(-11.5f, 12.5f), new(11.5f, 12.5f), new(11.5f, 44.5f), new(-11.5f, 44.5f)],
        [new(-11.5f, -17.5f), new(11.5f, -17.5f), new(11.5f, 10.5f), new(-11.5f, 10.5f)],
        [new(11.5f, -44.5f), new(11.5f, -19.5f), new(-11.5f, -19.5f), new(-11.5f, -44.5f)],
    ],
    [
    ],
    [
        [new(-11.5f, 12.5f), new(11.5f, 12.5f), new(11.5f, 44.5f)],
        [new(-11.5f, 12.5f), new(11.5f, 44.5f), new(-11.5f, 44.5f)],
        [new(-11.5f, -17.5f), new(11.5f, -17.5f), new(11.5f, 10.5f)],
        [new(-11.5f, -17.5f), new(11.5f, 10.5f), new(-11.5f, 10.5f)],
        [new(11.5f, -44.5f), new(11.5f, -19.5f), new(-11.5f, -19.5f)],
        [new(11.5f, -44.5f), new(-11.5f, -19.5f), new(-11.5f, -44.5f)],
    ]);

    internal const float Stage3bRadius = 44.5f;
    internal static IGeometryShape Stage3b(float y, Vector3? center = null) => new PolygonRegionGeometry(center ?? new(200f, y, -416.5f),
    [
        [new(-11.5f, -17.5f), new(11.5f, -17.5f), new(11.5f, 44.5f), new(-11.5f, 44.5f)],
        [new(11.5f, -44.5f), new(11.5f, -19.5f), new(-11.5f, -19.5f), new(-11.5f, -44.5f)],
    ],
    [
    ],
    [
        [new(-11.5f, -17.5f), new(11.5f, -17.5f), new(11.5f, 44.5f)],
        [new(-11.5f, -17.5f), new(11.5f, 44.5f), new(-11.5f, 44.5f)],
        [new(11.5f, -44.5f), new(11.5f, -19.5f), new(-11.5f, -19.5f)],
        [new(11.5f, -44.5f), new(-11.5f, -19.5f), new(-11.5f, -44.5f)],
    ]);

}

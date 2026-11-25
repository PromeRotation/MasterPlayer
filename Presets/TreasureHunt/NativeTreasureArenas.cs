// Generated from resolved runtime polygons using --treasure-canals-lucky-arenas.
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NativeTreasureArenas
{
    internal static IGeometryShape Canals(float y) => new LocalPolygonGeometry(new(0f, y, -425.425f), 0, [
        new(-5.5f, 22.075012f),
        new(-5.5f, 14.085022f),
        new(-8.081306f, 14.085022f),
        new(-19.509996f, 2.656311f),
        new(-19.509996f, 0.07501221f),
        new(-27.66f, 0.07501221f),
        new(-27.66f, -10.924988f),
        new(-19.509996f, -10.924988f),
        new(-19.509996f, -13.506287f),
        new(-10.941317f, -22.074982f),
        new(10.941309f, -22.074982f),
        new(19.509995f, -13.506287f),
        new(19.509995f, -10.924988f),
        new(27.66f, -10.924988f),
        new(27.66f, 0.07501221f),
        new(19.509998f, 0.07501221f),
        new(19.51f, 2.656311f),
        new(8.081305f, 14.085022f),
        new(5.5f, 14.085022f),
        new(5.5f, 22.075012f),
    ]);

    internal static IGeometryShape LuckyFace(float y) => new LocalPolygonGeometry(new(0f, y, -460.25f), 0, [
        new(-3.8042612f, 18.875305f),
        new(-7.462324f, 17.765656f),
        new(-10.833618f, 15.963654f),
        new(-13.788582f, 13.538574f),
        new(-16.213655f, 10.583618f),
        new(-18.01565f, 7.2123413f),
        new(-19.125313f, 3.5542603f),
        new(-19.5f, -0.25f),
        new(-19.125313f, -4.0542603f),
        new(-18.01565f, -7.7123413f),
        new(-16.213657f, -11.083618f),
        new(-13.788582f, -14.038574f),
        new(-10.83362f, -16.463654f),
        new(-7.462328f, -18.265656f),
        new(-4.2173414f, -19.25f),
        new(4.2173395f, -19.25f),
        new(7.462327f, -18.265656f),
        new(10.833621f, -16.463654f),
        new(13.788577f, -14.038574f),
        new(16.213655f, -11.083618f),
        new(18.01565f, -7.7123413f),
        new(19.125313f, -4.0542603f),
        new(19.5f, -0.25f),
        new(19.125313f, 3.5542603f),
        new(18.015654f, 7.212311f),
        new(16.213661f, 10.583618f),
        new(13.788585f, 13.538574f),
        new(10.833621f, 15.963654f),
        new(7.462327f, 17.765656f),
        new(3.804265f, 18.875305f),
        new(1.9073486E-06f, 19.25f),
    ]);

}

// Generated from resolved runtime polygons using --heroes-arenas.
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HeroesArenas
{
    internal static IGeometryShape Necromancer(float y) => new LocalPolygonGeometry(new(-449.625f, y, -531f), 0, [
        new(-2.4001465f, 19.301514f),
        new(-5.1187744f, 18.710083f),
        new(-7.725586f, 17.737854f),
        new(-10.16748f, 16.404419f),
        new(-12.394775f, 14.737122f),
        new(-14.362122f, 12.769775f),
        new(-16.02945f, 10.54248f),
        new(-17.362823f, 8.100586f),
        new(-18.335114f, 5.4937744f),
        new(-18.926514f, 2.7751465f),
        new(-19.125f, -0f),
        new(-18.926514f, -2.7751465f),
        new(-18.335114f, -5.4937744f),
        new(-17.362823f, -8.100586f),
        new(-16.02945f, -10.54248f),
        new(-14.362122f, -12.769775f),
        new(-12.394775f, -14.737122f),
        new(-10.16748f, -16.404419f),
        new(-7.725586f, -17.737793f),
        new(-5.1187744f, -18.710083f),
        new(-2.4001465f, -19.301514f),
        new(0.375f, -19.5f),
        new(3.150116f, -19.301514f),
        new(5.8687744f, -18.710083f),
        new(8.475586f, -17.737854f),
        new(10.91748f, -16.404419f),
        new(13.144775f, -14.737122f),
        new(15.112122f, -12.769775f),
        new(16.77945f, -10.54248f),
        new(18.112823f, -8.100586f),
        new(19.085114f, -5.4937744f),
        new(19.125f, -5.310425f),
        new(19.125f, 5.310425f),
        new(19.085114f, 5.4937744f),
        new(18.112823f, 8.100586f),
        new(16.77945f, 10.54248f),
        new(15.112122f, 12.769775f),
        new(13.144775f, 14.737122f),
        new(10.917511f, 16.404419f),
        new(8.475586f, 17.737793f),
        new(5.8687744f, 18.710083f),
        new(3.1501465f, 19.301514f),
        new(0.375f, 19.5f),
    ]);

    internal static IGeometryShape Berserker(float y) => new LocalPolygonGeometry(new(750f, y, 482f), 0, [
        new(-10f, 20f),
        new(-10f, 10f),
        new(-20f, 10f),
        new(-20f, -10f),
        new(-10f, -10f),
        new(-10f, -20f),
        new(10f, -20f),
        new(10f, -10f),
        new(20f, -10f),
        new(20f, 10f),
        new(10f, 10f),
        new(10f, 20f),
    ]);

    internal static IGeometryShape BerserkerLanding(float y) => new LocalPolygonGeometry(new(750f, y, 482f), 0, [
        new(-9f, 9f),
        new(-19f, 9f),
        new(-19f, -9f),
        new(-9f, -9f),
        new(-9f, -19f),
        new(9f, -19f),
        new(9f, -9f),
        new(19f, -9f),
        new(19f, 9f),
        new(9f, 9f),
        new(9f, 19f),
        new(-9f, 19f),
    ]);

    internal static IGeometryShape BerserkerCollapse(float y) => new PolygonRegionGeometry(new(750f, y, 482f),
    [
        [new(23f, -23f), new(23f, 23f), new(-23f, 23f), new(-23f, -23f)],
    ],
    [
        [new(-10f, -10f), new(-20f, -10f), new(-20f, 10f), new(-10f, 10f), new(-10f, 20f), new(10f, 20f), new(10f, 10f), new(20f, 10f), new(20f, -10f), new(10f, -10f), new(10f, -20f), new(-10f, -20f)],
    ],
    [
        [new(-23f, 23f), new(-23f, -23f), new(-20f, -10f)],
        [new(-20f, 10f), new(-10f, 10f), new(-10f, 20f)],
        [new(10f, 20f), new(10f, 10f), new(20f, 10f)],
        [new(20f, -10f), new(10f, -10f), new(10f, -20f)],
        [new(-10f, -20f), new(-10f, -10f), new(-20f, -10f)],
        [new(-23f, 23f), new(-20f, -10f), new(-20f, 10f)],
        [new(-10f, -20f), new(-20f, -10f), new(-23f, -23f)],
        [new(-23f, 23f), new(-20f, 10f), new(-10f, 20f)],
        [new(10f, -20f), new(-10f, -20f), new(-23f, -23f)],
        [new(23f, 23f), new(-23f, 23f), new(-10f, 20f)],
        [new(10f, -20f), new(-23f, -23f), new(23f, -23f)],
        [new(23f, 23f), new(-10f, 20f), new(10f, 20f)],
        [new(20f, -10f), new(10f, -20f), new(23f, -23f)],
        [new(23f, 23f), new(10f, 20f), new(20f, 10f)],
        [new(20f, 10f), new(20f, -10f), new(23f, -23f)],
        [new(23f, -23f), new(23f, 23f), new(20f, 10f)],
    ]);

}

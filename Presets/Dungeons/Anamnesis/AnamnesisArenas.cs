// Generated from resolved runtime polygons using --anamnesis-arenas.
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AnamnesisArenas
{
    internal static IGeometryShape Unknown(float y) => new LocalPolygonGeometry(new(-40f, y, 290.417f), 0, [
        new(-6.1358986f, 18.916992f),
        new(-8.852812f, 17.791626f),
        new(-11.461811f, 16.19281f),
        new(-13.788582f, 14.205566f),
        new(-15.775831f, 11.878815f),
        new(-17.374626f, 9.269806f),
        new(-18.5456f, 6.44281f),
        new(-19.259924f, 3.4674683f),
        new(-19.5f, 0.4169922f),
        new(-19.259924f, -2.633484f),
        new(-18.545603f, -5.6088257f),
        new(-17.374628f, -8.435822f),
        new(-15.775831f, -11.04483f),
        new(-13.788582f, -13.371582f),
        new(-11.461811f, -15.358826f),
        new(-8.852814f, -16.957642f),
        new(-6.025833f, -18.128601f),
        new(-3.0504723f, -18.842926f),
        new(-2.1093903f, -18.916992f),
        new(2.1093864f, -18.916992f),
        new(3.0504684f, -18.842926f),
        new(6.0258293f, -18.128601f),
        new(8.852814f, -16.957642f),
        new(11.461807f, -15.358856f),
        new(13.788578f, -13.371582f),
        new(15.775829f, -11.04483f),
        new(17.374626f, -8.435822f),
        new(18.5456f, -5.608856f),
        new(19.259922f, -2.633484f),
        new(19.5f, 0.4169922f),
        new(19.259922f, 3.4674683f),
        new(18.5456f, 6.44281f),
        new(17.37463f, 9.269806f),
        new(15.775833f, 11.878815f),
        new(13.788586f, 14.205566f),
        new(11.461815f, 16.19281f),
        new(8.852814f, 17.791626f),
        new(6.1359024f, 18.916992f),
    ]);

    internal static IGeometryShape Split(float y) => new PolygonRegionGeometry(new(0f, y, -450f),
    [
        [new(-15.5f, 4f), new(15.5f, 4f), new(15.5f, 19.5f), new(-15.5f, 19.5f)],
        [new(15.5f, -19.5f), new(15.5f, -4f), new(-15.5f, -4f), new(-15.5f, -19.5f)],
    ],
    [
    ],
    [
        [new(-15.5f, 4f), new(15.5f, 4f), new(15.5f, 19.5f)],
        [new(-15.5f, 4f), new(15.5f, 19.5f), new(-15.5f, 19.5f)],
        [new(15.5f, -19.5f), new(15.5f, -4f), new(-15.5f, -4f)],
        [new(15.5f, -19.5f), new(-15.5f, -4f), new(-15.5f, -19.5f)],
    ]);

}

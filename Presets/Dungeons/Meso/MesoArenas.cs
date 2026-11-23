// Generated from resolved runtime polygons using --meso-arenas.
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MesoArenas
{
    internal static IGeometryShape Headsmen(float y) => new LocalPolygonGeometry(new(60f, y, -258f), 0, [
        new(-29.5f, 22f),
        new(-29.5f, -22f),
        new(-8f, -22f),
        new(-8f, -21.550003f),
        new(8f, -21.550003f),
        new(8f, -22f),
        new(29.5f, -22f),
        new(29.5f, -8.000427f),
        new(29.452793f, -8.0004425f),
        new(29.447208f, 7.9995728f),
        new(29.5f, 7.9996033f),
        new(29.5f, 22f),
    ]);

}

using System.Numerics;
using DotRecast.Recast;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainNavigationTile
{
    internal const int SIDE = 32;
    internal const float WIDTH = SIDE * SceneTerrainBuilder.CELL_SIZE;
    internal const float PADDING = SceneTerrainBuilder.BORDER * SceneTerrainBuilder.CELL_SIZE;
    internal readonly record struct Span(float Height, Vector4 Neighbors);
    internal int[] Offsets { get; } = new int[SIDE * SIDE + 1];
    internal Span[] Spans { get; }
    internal int[] SolidOffsets { get; } = new int[SIDE * SIDE + 1];
    internal Vector2[] Solids { get; }

    internal TerrainNavigationTile(SceneTerrainBuilder.Raster raster)
    {
        var compact = raster.Compact;
        var spans = new List<Span>();
        var solids = new List<Vector2>();
        for (var z = 0; z < SIDE; ++z)
            for (var x = 0; x < SIDE; ++x)
            {
                var index = x + z * SIDE;
                Offsets[index] = spans.Count;
                SolidOffsets[index] = solids.Count;
                var sx = x + SceneTerrainBuilder.BORDER;
                var sz = z + SceneTerrainBuilder.BORDER;
                var source = sx + sz * compact.width;
                for (var solid = raster.Solid.spans[source]; solid != null; solid = solid.next)
                    solids.Add(new(compact.bmin.Y + solid.smin * SceneTerrainBuilder.CELL_HEIGHT,
                        compact.bmin.Y + solid.smax * SceneTerrainBuilder.CELL_HEIGHT));
                var cell = compact.cells[source];
                for (var i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (compact.areas[i] == RcRecast.RC_NULL_AREA) continue;
                    var neighbors = new Vector4(float.NaN);
                    for (var direction = 0; direction < 4; ++direction)
                    {
                        var connection = RcRecast.GetCon(compact.spans[i], direction);
                        if (connection == RcRecast.RC_NOT_CONNECTED) continue;
                        var next = compact.cells[sx + RcRecast.GetDirOffsetX(direction)
                            + (sz + RcRecast.GetDirOffsetY(direction)) * compact.width].index + connection;
                        if (compact.areas[next] != RcRecast.RC_NULL_AREA)
                            neighbors[direction] = compact.bmin.Y + compact.spans[next].y * SceneTerrainBuilder.CELL_HEIGHT;
                    }
                    spans.Add(new(compact.bmin.Y + compact.spans[i].y * SceneTerrainBuilder.CELL_HEIGHT, neighbors));
                }
            }
        Offsets[^1] = spans.Count;
        SolidOffsets[^1] = solids.Count;
        Spans = spans.ToArray();
        Solids = solids.ToArray();
    }
}

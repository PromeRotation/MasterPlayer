using System.Diagnostics;
using System.Numerics;
using DotRecast.Recast;

namespace MasterPlayer.Spatial.Terrain;

internal sealed class TerrainNavigationCache
{
    private readonly record struct Key(int X, int Z, int Layer);
    private readonly record struct GeometryKey(ulong Id, TerrainMeshAsset Asset, Matrix4x4 World, ulong Material, ulong Mask)
    {
        internal GeometryKey(TerrainSceneSnapshot.Instance instance)
            : this(instance.Id, instance.Asset, instance.World, instance.Material, instance.MaterialMask) { }
    }
    private sealed record Entry(TerrainSceneSnapshot Scene, TerrainNavigationTile Tile);
    private readonly object gate = new();
    private readonly Dictionary<Key, Entry> tiles = new();
    private int revision;
    internal int Count { get { lock (gate) return tiles.Count; } }
    internal static float CaptureRadius(float radius) => radius + TerrainNavigationTile.WIDTH;

    internal void Clear()
    {
        lock (gate) { ++revision; tiles.Clear(); }
    }

    internal IReadOnlyList<(Vector2 Min, Vector2 Max)>? Invalidate(TerrainSceneSnapshot previous, TerrainSceneSnapshot current)
    {
        var before = previous.Instances.Select(i => new GeometryKey(i)).ToHashSet();
        var after = current.Instances.Select(i => new GeometryKey(i)).ToHashSet();
        var changed = previous.Instances.Where(i => !after.Contains(new(i)))
            .Concat(current.Instances.Where(i => !before.Contains(new(i))));
        var regions = new List<(Vector2 Min, Vector2 Max)>();
        foreach (var instance in changed)
        {
            // Native captures already carry bounds. Never decode a new PCB on the framework thread.
            if (instance.GetBounds(false) is not { } bounds) { Clear(); return null; }
            var pad = new Vector2(TerrainNavigationTile.PADDING + SceneTerrainBuilder.CELL_SIZE / 2);
            regions.Add((new Vector2(bounds.Min.X, bounds.Min.Z) - pad, new Vector2(bounds.Max.X, bounds.Max.Z) + pad));
        }
        lock (gate)
        {
            ++revision;
            foreach (var key in tiles.Keys.Where(key => regions.Any(region => Intersects(key, region.Min, region.Max))).ToArray())
                tiles.Remove(key);
        }
        return regions;
    }

    private static bool Intersects(Key key, Vector2 min, Vector2 max)
    {
        var x = key.X * TerrainNavigationTile.WIDTH - SceneTerrainBuilder.CELL_SIZE / 2;
        var z = key.Z * TerrainNavigationTile.WIDTH - SceneTerrainBuilder.CELL_SIZE / 2;
        return x <= max.X && x + TerrainNavigationTile.WIDTH >= min.X
            && z <= max.Y && z + TerrainNavigationTile.WIDTH >= min.Y;
    }

    internal SceneTerrainBuilder.Result Build(TerrainSceneSnapshot scene, Vector3 origin, float radius, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var clock = Stopwatch.StartNew();
        int startedRevision;
        lock (gate) startedRevision = revision;
        var side = 2 * (int)MathF.Ceiling(radius / SceneTerrainBuilder.CELL_SIZE) + 1;
        var cx = (int)MathF.Round(origin.X / SceneTerrainBuilder.CELL_SIZE);
        var cz = (int)MathF.Round(origin.Z / SceneTerrainBuilder.CELL_SIZE);
        var minX = cx - side / 2;
        var minZ = cz - side / 2;
        var layer = (int)MathF.Floor(origin.Y / 16);
        var baseY = layer * 16 - 64;
        var active = new Dictionary<(int X, int Z), TerrainNavigationTile>();
        var undecoded = scene.Instances.Select(i => i.Asset).Distinct().Where(a => a.Encoded && !a.Mesh.IsValueCreated).ToArray();
        var decodeStart = Stopwatch.GetTimestamp();
        var bounds = scene.Instances.Select(i =>
        {
            cancellation.ThrowIfCancellationRequested();
            return (Instance: i, Bounds: i.GetBounds(true)!.Value);
        }).ToArray();
        var boundsMs = Stopwatch.GetElapsedTime(decodeStart).TotalMilliseconds;
        var inputs = new Dictionary<GeometryKey, TerrainSceneSnapshot.Instance>();
        var built = 0;
        var reused = 0;
        var work = new List<TerrainBuildDiagnostics>();
        for (var tz = TileIndex(minZ); tz <= TileIndex(minZ + side - 1); ++tz)
            for (var tx = TileIndex(minX); tx <= TileIndex(minX + side - 1); ++tx)
            {
                cancellation.ThrowIfCancellationRequested();
                var key = new Key(tx, tz, layer);
                var x = tx * TerrainNavigationTile.WIDTH - SceneTerrainBuilder.CELL_SIZE / 2 - TerrainNavigationTile.PADDING;
                var z = tz * TerrainNavigationTile.WIDTH - SceneTerrainBuilder.CELL_SIZE / 2 - TerrainNavigationTile.PADDING;
                var size = TerrainNavigationTile.SIDE + SceneTerrainBuilder.BORDER * 2;
                var width = size * SceneTerrainBuilder.CELL_SIZE;
                var selected = new TerrainSceneSnapshot(bounds.Where(b => b.Bounds.Max.X >= x && b.Bounds.Min.X <= x + width
                    && b.Bounds.Max.Z >= z && b.Bounds.Min.Z <= z + width
                    && b.Bounds.Max.Y >= baseY && b.Bounds.Min.Y <= baseY + 128).Select(b => b.Instance).ToArray());
                foreach (var instance in selected.Instances) inputs.TryAdd(new(instance), instance);
                Entry? entry;
                lock (gate) tiles.TryGetValue(key, out entry);
                if (entry != null && entry.Scene.SameGeometry(selected)) ++reused;
                else
                {
                    var raster = SceneTerrainBuilder.Rasterize(selected, x, z, size, baseY, cancellation);
                    entry = new(selected, new(raster));
                    work.Add(raster.Diagnostics);
                    ++built;
                    lock (gate)
                        if (revision == startedRevision && !cancellation.IsCancellationRequested) tiles[key] = entry;
                }
                active.Add((tx, tz), entry.Tile);
            }
        cancellation.ThrowIfCancellationRequested();
        var stage = Stopwatch.GetTimestamp();
        var cells = new TerrainCellKind[side * side];
        var heights = Enumerable.Repeat(float.NaN, cells.Length).ToArray();
        var columns = new (TerrainNavigationTile Tile, int Cell)[cells.Length];
        for (var z = 0; z < side; ++z)
            for (var x = 0; x < side; ++x)
            {
                var index = x + z * side;
                var gx = minX + x;
                var gz = minZ + z;
                var tile = active[(TileIndex(gx), TileIndex(gz))];
                var cell = gx - TileIndex(gx) * TerrainNavigationTile.SIDE
                    + (gz - TileIndex(gz) * TerrainNavigationTile.SIDE) * TerrainNavigationTile.SIDE;
                columns[index] = (tile, cell);
                var dx = (x - side / 2) * SceneTerrainBuilder.CELL_SIZE;
                var dz = (z - side / 2) * SceneTerrainBuilder.CELL_SIZE;
                if (dx * dx + dz * dz > radius * radius) continue;
                var wall = false;
                for (var i = tile.SolidOffsets[cell]; i < tile.SolidOffsets[cell + 1]; ++i)
                    wall |= tile.Solids[i].X < origin.Y + 1.65f && tile.Solids[i].Y > origin.Y + .15f;
                cells[index] = wall ? TerrainCellKind.Wall : TerrainCellKind.Drop;
            }
        var seed = (Cell: -1, Span: -1);
        var score = float.MaxValue;
        for (var z = side / 2 - 2; z <= side / 2 + 2; ++z)
            for (var x = side / 2 - 2; x <= side / 2 + 2; ++x)
            {
                var index = x + z * side;
                var (tile, cell) = columns[index];
                for (var i = tile.Offsets[cell]; i < tile.Offsets[cell + 1]; ++i)
                {
                    var dy = MathF.Abs(tile.Spans[i].Height - origin.Y);
                    var distance = (x - side / 2) * (x - side / 2) + (z - side / 2) * (z - side / 2) + dy * dy;
                    if (dy > .7f || distance >= score) continue;
                    score = distance;
                    seed = (index, i);
                }
            }
        var pending = new Queue<(int Cell, int Span)>();
        var visited = new HashSet<(int Cell, int Span)>();
        if (seed.Cell >= 0) { pending.Enqueue(seed); visited.Add(seed); }
        while (pending.TryDequeue(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (cells[current.Cell] == TerrainCellKind.Unknown) continue;
            var span = columns[current.Cell].Tile.Spans[current.Span];
            if (cells[current.Cell] == TerrainCellKind.Walkable && MathF.Abs(heights[current.Cell] - span.Height) > .65f)
            {
                // A 2D route cannot choose between overlapping reachable support levels.
                cells[current.Cell] = TerrainCellKind.Unknown;
                heights[current.Cell] = float.NaN;
                continue;
            }
            cells[current.Cell] = TerrainCellKind.Walkable;
            heights[current.Cell] = span.Height;
            for (var direction = 0; direction < 4; ++direction)
            {
                if (!float.IsFinite(span.Neighbors[direction])) continue;
                var nx = current.Cell % side + RcRecast.GetDirOffsetX(direction);
                var nz = current.Cell / side + RcRecast.GetDirOffsetY(direction);
                if (nx < 0 || nz < 0 || nx >= side || nz >= side) continue;
                var index = nx + nz * side;
                var (tile, cell) = columns[index];
                for (var i = tile.Offsets[cell]; i < tile.Offsets[cell + 1]; ++i)
                {
                    if (MathF.Abs(tile.Spans[i].Height - span.Neighbors[direction]) > .001f) continue;
                    // Both tiles must agree at the seam; one stale border cannot invent a connection.
                    var reverse = tile.Spans[i].Neighbors[(direction + 2) % 4];
                    if (!float.IsFinite(reverse) || MathF.Abs(reverse - span.Height) > .001f) continue;
                    if (visited.Add((index, i))) pending.Enqueue((index, i));
                    break;
                }
            }
        }
        var reachableMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        stage = Stopwatch.GetTimestamp();
        var boundary = new LocalTerrainBoundary(new(cx * SceneTerrainBuilder.CELL_SIZE, origin.Y, cz * SceneTerrainBuilder.CELL_SIZE),
            side, cells, heights, SceneTerrainBuilder.CELL_SIZE);
        var boundaryMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        var elapsed = clock.Elapsed.TotalMilliseconds;
        var diagnostics = new TerrainBuildDiagnostics
        {
            TotalMilliseconds = elapsed,
            DecodeMilliseconds = boundsMs + work.Sum(d => d.DecodeMilliseconds),
            TransformMilliseconds = work.Sum(d => d.TransformMilliseconds),
            ClassifyMilliseconds = work.Sum(d => d.ClassifyMilliseconds),
            RasterizeMilliseconds = work.Sum(d => d.RasterizeMilliseconds),
            FilterMilliseconds = work.Sum(d => d.FilterMilliseconds),
            CompactMilliseconds = work.Sum(d => d.CompactMilliseconds),
            ForbiddenMilliseconds = work.Sum(d => d.ForbiddenMilliseconds),
            ErodeMilliseconds = work.Sum(d => d.ErodeMilliseconds),
            ReachabilityMilliseconds = reachableMs,
            BoundaryMilliseconds = boundaryMs,
            Instances = scene.Instances.Length,
            DecodedModels = undecoded.Count(a => a.Mesh.IsValueCreated),
            InputVertices = inputs.Values.Sum(i => i.Asset.Mesh.Value.Vertices.Length),
            InputTriangles = inputs.Values.Sum(i => i.Asset.Mesh.Value.Triangles.Length),
            RasterizedTriangles = work.Sum(d => d.RasterizedTriangles),
            ForbiddenTriangles = work.Sum(d => d.ForbiddenTriangles),
            CompactSpans = active.Values.Sum(t => t.Spans.Length),
            ReachableSpans = visited.Count,
            WalkableCells = boundary.WalkableCount,
            BuiltTiles = built,
            CachedTiles = reused,
            StoredTiles = Count,
        };
        return new(boundary, diagnostics.RasterizedTriangles, elapsed, diagnostics);
    }

    private static int TileIndex(int cell) => (int)MathF.Floor((float)cell / TerrainNavigationTile.SIDE);
}

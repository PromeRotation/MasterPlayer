using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.Terrain;

internal static class TerrainNavigationCacheTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("floor tile cache builds only new frontier and returning to built terrain performs no rasterization", () =>
        {
            var cache = new TerrainNavigationCache();
            var scene = SceneTerrainTests.Floor();
            var first = cache.Build(scene, Vector3.Zero, 20, default);
            var moved = cache.Build(scene, new(4.5f, 0, 0), 20, default);
            var returned = cache.Build(scene, Vector3.Zero, 20, default);
            Check(first.Diagnostics.BuiltTiles > 0 && first.Diagnostics.CachedTiles == 0, "The first map was not built.");
            Check(moved.Diagnostics.BuiltTiles > 0 && moved.Diagnostics.BuiltTiles < first.Diagnostics.BuiltTiles
                && moved.Diagnostics.CachedTiles > 0, "Moving rebuilt the overlap instead of only the new frontier.");
            Check(returned.Diagnostics.BuiltTiles == 0 && returned.Diagnostics.CachedTiles == first.Diagnostics.BuiltTiles
                && returned.Diagnostics.RasterizeMilliseconds == 0 && returned.Diagnostics.RasterizedTriangles == 0,
                "Returning to cached terrain repeated Recast construction.");
            Compare(first.Boundary, returned.Boundary);
            Compare(SceneTerrainBuilder.Build(scene, new(4.5f, 0, 0), 20, default).Boundary, moved.Boundary);
            Console.WriteLine($"CACHE cold={first.Diagnostics.BuiltTiles}, frontier={moved.Diagnostics.BuiltTiles}, reused={moved.Diagnostics.CachedTiles}, return={returned.Diagnostics.BuiltTiles}; cold_ms={first.Milliseconds:F2}, return_ms={returned.Milliseconds:F2}");
        });
        run("cached tiles reflood from the current player and cannot connect rooms through a wall at a tile seam", () =>
        {
            var cache = new TerrainNavigationCache();
            var scene = SceneTerrainTests.Floor(SceneTerrainTests.Box(2, new(.01f, 2, 30), new(8, 2, 0)));
            var left = cache.Build(scene, Vector3.Zero, 20, default).Boundary;
            var right = cache.Build(scene, new(12, 0, 0), 20, default);
            Check(left.Contains(Vector3.Zero) && !left.Contains(new(12, 0, 0)), "An unreachable room was admitted from the left.");
            Check(right.Diagnostics.CachedTiles > 0 && right.Boundary.Contains(new(12, 0, 0)) && !right.Boundary.Contains(Vector3.Zero),
                "A cached connected component was reused from the wrong side of the wall.");
            Check(!ArenaBoundaryTraversal.ContainsSegment(right.Boundary, new(12, 0, 0), Vector3.Zero), "A tile seam opened a wall.");
            Compare(left, cache.Build(scene, Vector3.Zero, 20, default).Boundary);
        });
        run("tile borders preserve climb connections and never switch to an overlapping overhead floor", () =>
        {
            var cache = new TerrainNavigationCache();
            var step = SceneTerrainTests.Box(2, new(10, .2f, 20), new(18, .2f, 0));
            var ceiling = SceneTerrainTests.Box(3, new(25, .2f, 25), new(0, 3.8f, 0));
            var scene = SceneTerrainTests.Floor(step, ceiling);
            cache.Build(scene, new(7, 0, 0), 20, default);
            var map = cache.Build(scene, new(9, .4f, 0), 20, default).Boundary;
            Check(map.TryGetFloorPosition(new(6, 0, 0), out var low) && low.Y < .1f
                && map.TryGetFloorPosition(new(10, .4f, 0), out var high) && MathF.Abs(high.Y - .4f) < .1f,
                "A tile seam severed a climbable step or selected the overhead floor.");
            Check(ArenaBoundaryTraversal.ContainsSegment(map, new(6, 0, 0), new(10, .4f, 0)), "Tile borders introduced a false gap in the step.");
        });
        run("a real local collision invalidates only its tile and opening it does not restore a stale cached obstacle", () =>
        {
            var cache = new TerrainNavigationCache();
            var open = SceneTerrainTests.Floor();
            var first = cache.Build(open, Vector3.Zero, 20, default);
            var closed = SceneTerrainTests.Floor(SceneTerrainTests.Box(2, new(.1f, 2, .1f), new(3, 2, 3)));
            var regions = cache.Invalidate(open, closed);
            var masked = first.Boundary.WithUnknownRegions(regions);
            Check(!masked.Contains(new(3, 0, 3)) && masked.Contains(new(-8, 0, -8)) && first.Boundary.Contains(new(3, 0, 3)),
                "Invalidation erased unrelated terrain, allowed the changed tile, or mutated the published map.");
            var blocked = cache.Build(closed, Vector3.Zero, 20, default);
            Check(blocked.Diagnostics.BuiltTiles == 1 && blocked.Diagnostics.CachedTiles == first.Diagnostics.BuiltTiles - 1
                && !blocked.Boundary.Contains(new(3, 0, 3)), "Local geometry changes rebuilt unrelated tiles or failed to block the obstacle.");
            cache.Invalidate(closed, open);
            var restored = cache.Build(open, Vector3.Zero, 20, default);
            Check(restored.Diagnostics.BuiltTiles == 1 && restored.Boundary.Contains(new(3, 0, 3))
                && !blocked.Boundary.Contains(new(3, 0, 3)), "An opened obstacle did not invalidate its cached tile immutably.");
        });
        run("moving colliders invalidate both old and new bounds even when layout IDs are shared", () =>
        {
            var cache = new TerrainNavigationCache();
            var wall = SceneTerrainTests.Box(0, new(.1f, 2, .1f), new(3, 2, 3));
            var other = SceneTerrainTests.Box(0, new(.1f, 2, .1f), new(-12, 2, -12));
            var before = SceneTerrainTests.Floor(wall, other);
            cache.Build(before, Vector3.Zero, 20, default);
            var after = SceneTerrainTests.Floor(wall with { World = Matrix4x4.CreateScale(.1f, 2, .1f) * Matrix4x4.CreateTranslation(12, 2, 3) }, other);
            cache.Invalidate(before, after);
            var result = cache.Build(after, Vector3.Zero, 20, default);
            Check(result.Diagnostics.BuiltTiles == 2 && result.Diagnostics.CachedTiles > 0
                && result.Boundary.Contains(new(3, 0, 3)) && !result.Boundary.Contains(new(12, 0, 3))
                && !result.Boundary.Contains(new(-12, 0, -12)), "A moved collider retained an old tile or lost a shared-ID obstacle.");
        });
        run("procedural replacements with identical keys and counts invalidate local tiles without decoding on the main thread", () =>
        {
            var cache = new TerrainNavigationCache();
            var mesh = TerrainPrimitives.Box.Mesh.Value;
            var original = new TerrainMeshAsset("same-key", mesh);
            var wall = new TerrainSceneSnapshot.Instance(2, original,
                Matrix4x4.CreateScale(.1f, 2, .1f) * Matrix4x4.CreateTranslation(3, 2, 3), 0x4000, ulong.MaxValue)
            {
                WorldBounds = (new(2.9f, 0, 2.9f), new(3.1f, 4, 3.1f)),
            };
            var before = SceneTerrainTests.Floor(wall);
            cache.Build(before, Vector3.Zero, 20, default);
            var replacement = new TerrainMeshAsset("same-key", new TerrainMesh(mesh.Vertices.Select(v => v + new Vector3(80, 0, 0)).ToArray(), mesh.Triangles));
            var after = SceneTerrainTests.Floor(wall with { Asset = replacement, WorldBounds = (new(10.9f, 0, 2.9f), new(11.1f, 4, 3.1f)) });
            cache.Invalidate(before, after);
            Check(!replacement.Mesh.IsValueCreated, "Invalidation decoded a replacement model on the framework thread.");
            var result = cache.Build(after, Vector3.Zero, 20, default);
            Check(result.Diagnostics.BuiltTiles == 2 && result.Diagnostics.CachedTiles > 0
                && result.Boundary.Contains(new(3, 0, 3)) && !result.Boundary.Contains(new(11, 0, 3)),
                "An identical key and triangle count concealed changed procedural geometry.");
        });
        run("cache clear and cancelled construction cannot return the previous floor", () =>
        {
            var cache = new TerrainNavigationCache();
            var open = SceneTerrainTests.Floor();
            cache.Build(open, Vector3.Zero, 20, default);
            cache.Clear();
            Check(cache.Count == 0, "Clearing a floor left navigation tiles behind.");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            try { cache.Build(open, Vector3.Zero, 20, cancelled.Token); throw new Exception("A cancelled build completed."); }
            catch (OperationCanceledException) { }
            Check(cache.Count == 0, "A cancelled build repopulated the cache.");
            var closed = SceneTerrainTests.Floor(SceneTerrainTests.Box(2, new(.01f, 2, 30), new(1.8f, 2, 0)));
            var result = cache.Build(closed, Vector3.Zero, 20, default);
            Check(result.Diagnostics.CachedTiles == 0 && !result.Boundary.Contains(new(4, 0, 0)), "The new floor restored old cached geometry.");
        });
    }

    private static void Compare(LocalTerrainBoundary expected, LocalTerrainBoundary actual)
    {
        Check(expected.Side == actual.Side && expected.Position == actual.Position, "Cache assembly shifted the grid.");
        for (var z = 0; z < expected.Side; ++z)
            for (var x = 0; x < expected.Side; ++x)
            {
                Check(expected.Cell(x, z) == actual.Cell(x, z), $"Cached cell differs at {x},{z}.");
                if (expected.Cell(x, z) == TerrainCellKind.Walkable)
                    Check(MathF.Abs(expected.Height(x, z) - actual.Height(x, z)) < .001f, "A cached support height changed.");
            }
    }
}

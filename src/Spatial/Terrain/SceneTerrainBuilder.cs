using System.Diagnostics;
using System.Numerics;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Recast;

namespace MasterPlayer.Spatial.Terrain;

internal static class SceneTerrainBuilder
{
    internal const float CELL_SIZE = .25f;
    internal const int VERSION = 2;
    internal const float CELL_HEIGHT = .05f;
    internal const int BORDER = 4;
    internal const int HEIGHT = 33;
    internal const int CLIMB = 13;
    internal const int EROSION = 2;
    internal const float MIN_NORMAL_Y = .65f;
    internal sealed record Result(LocalTerrainBoundary Boundary, int Triangles, double Milliseconds,
        TerrainBuildDiagnostics Diagnostics);
    internal sealed record Raster(RcHeightfield Solid, RcCompactHeightfield Compact, TerrainBuildDiagnostics Diagnostics);

    internal static Result Build(TerrainSceneSnapshot scene, Vector3 origin, float radius, CancellationToken cancellation)
        => new TerrainNavigationCache().Build(scene, origin, radius, cancellation);

    internal static Raster Rasterize(TerrainSceneSnapshot scene, float minX, float minZ, int size, float baseY,
        CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        var minimum = new RcVec3f(minX, baseY, minZ);
        var maximum = new RcVec3f(minimum.X + size * CELL_SIZE, baseY + 128, minimum.Z + size * CELL_SIZE);
        var solid = new RcHeightfield(size, size, minimum, maximum, CELL_SIZE, CELL_HEIGHT, 0);
        RcHeightfield? forbidden = null;
        var context = new RcContext();
        var triangles = 0;
        double decodeMs = 0, transformMs = 0, classifyMs = 0, rasterizeMs = 0;
        var decodedModels = 0;
        var inputVertices = 0;
        var inputTriangles = 0;
        var forbiddenTriangles = 0;
        foreach (var instance in scene.Instances)
        {
            cancellation.ThrowIfCancellationRequested();
            var phase = Stopwatch.GetTimestamp();
            if (instance.Asset.Encoded && !instance.Asset.Mesh.IsValueCreated) ++decodedModels;
            var mesh = instance.Asset.Mesh.Value;
            decodeMs += Stopwatch.GetElapsedTime(phase).TotalMilliseconds;
            inputVertices += mesh.Vertices.Length;
            inputTriangles += mesh.Triangles.Length;
            phase = Stopwatch.GetTimestamp();
            var vertices = new float[mesh.Vertices.Length * 3];
            for (var i = 0; i < mesh.Vertices.Length; ++i)
            {
                var vertex = Vector3.Transform(mesh.Vertices[i], instance.World);
                vertices[i * 3] = vertex.X; vertices[i * 3 + 1] = vertex.Y; vertices[i * 3 + 2] = vertex.Z;
            }
            transformMs += Stopwatch.GetElapsedTime(phase).TotalMilliseconds;
            phase = Stopwatch.GetTimestamp();
            var indices = new List<int>();
            var areas = new List<int>();
            var forbiddenIndices = new List<int>();
            var mirrored = instance.World.GetDeterminant() < 0;
            foreach (var triangle in mesh.Triangles)
            {
                var a = Vertex(vertices, triangle.A);
                var b = Vertex(vertices, triangle.B);
                var c = Vertex(vertices, triangle.C);
                if (MathF.Max(a.X, MathF.Max(b.X, c.X)) < minimum.X || MathF.Min(a.X, MathF.Min(b.X, c.X)) > maximum.X
                    || MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) < minimum.Z || MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) > maximum.Z) continue;
                var normal = Vector3.Cross(b - a, c - a);
                if (mirrored) normal = -normal;
                var material = TerrainSceneSnapshot.ResolveMaterial(triangle.Material, instance.Material, instance.MaterialMask);
                var forceUnwalkable = TerrainSceneSnapshot.IsUnwalkableMaterial(material);
                var walkable = normal.LengthSquared() > 1e-12f && normal.Y >= normal.Length() * MIN_NORMAL_Y && !forceUnwalkable;
                indices.Add(triangle.A); indices.Add(triangle.B); indices.Add(triangle.C);
                areas.Add(walkable ? RcRecast.RC_WALKABLE_AREA : RcRecast.RC_NULL_AREA);
                if (forceUnwalkable)
                {
                    forbiddenIndices.Add(triangle.A); forbiddenIndices.Add(triangle.B); forbiddenIndices.Add(triangle.C);
                }
            }
            classifyMs += Stopwatch.GetElapsedTime(phase).TotalMilliseconds;
            triangles += areas.Count;
            phase = Stopwatch.GetTimestamp();
            RcRasterizations.RasterizeTriangles(context, vertices, indices.ToArray(), areas.ToArray(), areas.Count, solid, 1);
            if (forbiddenIndices.Count > 0)
            {
                forbidden ??= new(size, size, minimum, maximum, CELL_SIZE, CELL_HEIGHT, 0);
                var count = forbiddenIndices.Count / 3;
                forbiddenTriangles += count;
                RcRasterizations.RasterizeTriangles(context, vertices, forbiddenIndices.ToArray(), new int[count], count, forbidden, 1);
            }
            rasterizeMs += Stopwatch.GetElapsedTime(phase).TotalMilliseconds;
        }
        cancellation.ThrowIfCancellationRequested();
        var stage = Stopwatch.GetTimestamp();
        RcFilters.FilterLedgeSpans(context, HEIGHT, CLIMB, solid);
        RcFilters.FilterWalkableLowHeightSpans(context, HEIGHT, solid);
        var filterMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        stage = Stopwatch.GetTimestamp();
        var compact = RcCompacts.BuildCompactHeightfield(context, HEIGHT, CLIMB, solid);
        var compactMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        stage = Stopwatch.GetTimestamp();
        if (forbidden != null)
        {
            // Recast merges coplanar spans using the more permissive area. Preserve explicit holes/barriers independently.
            for (var cellIndex = 0; cellIndex < size * size; ++cellIndex)
            {
                var cell = compact.cells[cellIndex];
                for (var i = cell.index; i < cell.index + cell.count; ++i)
                    for (var span = forbidden.spans[cellIndex]; span != null; span = span.next)
                        if (span.smax >= compact.spans[i].y - 1 && span.smin < compact.spans[i].y + HEIGHT)
                        {
                            compact.areas[i] = RcRecast.RC_NULL_AREA;
                            break;
                        }
            }
        }
        var forbiddenMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        stage = Stopwatch.GetTimestamp();
        RcAreas.ErodeWalkableArea(context, EROSION, compact);
        var erodeMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        var elapsed = clock.Elapsed.TotalMilliseconds;
        var diagnostics = new TerrainBuildDiagnostics
        {
            TotalMilliseconds = elapsed,
            DecodeMilliseconds = decodeMs,
            TransformMilliseconds = transformMs,
            ClassifyMilliseconds = classifyMs,
            RasterizeMilliseconds = rasterizeMs,
            FilterMilliseconds = filterMs,
            CompactMilliseconds = compactMs,
            ForbiddenMilliseconds = forbiddenMs,
            ErodeMilliseconds = erodeMs,
            Instances = scene.Instances.Length,
            DecodedModels = decodedModels,
            InputVertices = inputVertices,
            InputTriangles = inputTriangles,
            RasterizedTriangles = triangles,
            ForbiddenTriangles = forbiddenTriangles,
            CompactSpans = compact.spanCount,
        };
        return new(solid, compact, diagnostics);
    }

    private static Vector3 Vertex(float[] vertices, int index)
        => new(vertices[index * 3], vertices[index * 3 + 1], vertices[index * 3 + 2]);

}

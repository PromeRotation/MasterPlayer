using System.Diagnostics;
using System.Numerics;
using DotRecast.Core;
using DotRecast.Core.Numerics;
using DotRecast.Recast;

namespace MasterPlayer.Spatial.Terrain;

internal static class SceneTerrainBuilder
{
    internal const float CELL_SIZE = .25f;
    internal const int VERSION = 1;
    internal const float CELL_HEIGHT = .05f;
    internal const int BORDER = 4;
    internal const int HEIGHT = 33;
    internal const int CLIMB = 13;
    internal const int EROSION = 2;
    internal const float MIN_NORMAL_Y = .65f;
    internal sealed record Result(LocalTerrainBoundary Boundary, int Triangles, double Milliseconds,
        TerrainBuildDiagnostics Diagnostics);

    internal static Result Build(TerrainSceneSnapshot scene, Vector3 origin, float radius, CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        var side = 2 * (int)MathF.Ceiling(radius / CELL_SIZE) + 1;
        var size = side + BORDER * 2;
        var center = new Vector3(MathF.Round(origin.X / CELL_SIZE) * CELL_SIZE, origin.Y, MathF.Round(origin.Z / CELL_SIZE) * CELL_SIZE);
        var minimum = new RcVec3f(center.X - size * CELL_SIZE / 2, origin.Y - 64, center.Z - size * CELL_SIZE / 2);
        var maximum = new RcVec3f(minimum.X + size * CELL_SIZE, origin.Y + 64, minimum.Z + size * CELL_SIZE);
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
        stage = Stopwatch.GetTimestamp();
        var cells = new TerrainCellKind[side * side];
        var heights = Enumerable.Repeat(float.NaN, cells.Length).ToArray();
        for (var z = 0; z < side; ++z)
            for (var x = 0; x < side; ++x)
            {
                if (MathF.Pow((x - side / 2) * CELL_SIZE, 2) + MathF.Pow((z - side / 2) * CELL_SIZE, 2) > radius * radius) continue;
                var solidIndex = x + BORDER + (z + BORDER) * size;
                var wall = false;
                for (var span = solid.spans[solidIndex]; span != null; span = span.next)
                    wall |= minimum.Y + span.smin * CELL_HEIGHT < origin.Y + 1.65f && minimum.Y + span.smax * CELL_HEIGHT > origin.Y + .15f;
                cells[z * side + x] = wall ? TerrainCellKind.Wall : TerrainCellKind.Drop;
            }
        var seed = FindSeed(compact, origin);
        var pending = new Queue<(int X, int Z, int Span)>();
        var visited = new bool[compact.spanCount];
        var reachableSpans = 0;
        if (seed.Span >= 0) { pending.Enqueue(seed); visited[seed.Span] = true; }
        while (pending.TryDequeue(out var entry))
        {
            ++reachableSpans;
            cancellation.ThrowIfCancellationRequested();
            var x = entry.X - BORDER; var z = entry.Z - BORDER;
            if (x < 0 || z < 0 || x >= side || z >= side || cells[z * side + x] == TerrainCellKind.Unknown) continue;
            var index = z * side + x;
            var height = compact.bmin.Y + compact.spans[entry.Span].y * CELL_HEIGHT;
            if (cells[index] == TerrainCellKind.Walkable && MathF.Abs(heights[index] - height) > .65f)
            {
                // The host planner is 2D: overlapping reachable levels cannot share one column.
                cells[index] = TerrainCellKind.Unknown; heights[index] = float.NaN;
                continue;
            }
            cells[index] = TerrainCellKind.Walkable; heights[index] = height;
            for (var direction = 0; direction < 4; ++direction)
            {
                var connection = RcRecast.GetCon(compact.spans[entry.Span], direction);
                if (connection == RcRecast.RC_NOT_CONNECTED) continue;
                var nx = entry.X + RcRecast.GetDirOffsetX(direction);
                var nz = entry.Z + RcRecast.GetDirOffsetY(direction);
                var next = compact.cells[nx + nz * size].index + connection;
                if (visited[next] || compact.areas[next] == RcRecast.RC_NULL_AREA) continue;
                visited[next] = true;
                pending.Enqueue((nx, nz, next));
            }
        }
        var reachableMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        stage = Stopwatch.GetTimestamp();
        var boundary = new LocalTerrainBoundary(center, side, cells, heights, CELL_SIZE);
        var boundaryMs = Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
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
            ReachabilityMilliseconds = reachableMs,
            BoundaryMilliseconds = boundaryMs,
            Instances = scene.Instances.Length,
            DecodedModels = decodedModels,
            InputVertices = inputVertices,
            InputTriangles = inputTriangles,
            RasterizedTriangles = triangles,
            ForbiddenTriangles = forbiddenTriangles,
            CompactSpans = compact.spanCount,
            ReachableSpans = reachableSpans,
            WalkableCells = boundary.WalkableCount,
        };
        return new(boundary, triangles, elapsed, diagnostics);
    }

    private static Vector3 Vertex(float[] vertices, int index)
        => new(vertices[index * 3], vertices[index * 3 + 1], vertices[index * 3 + 2]);

    private static (int X, int Z, int Span) FindSeed(RcCompactHeightfield field, Vector3 origin)
    {
        var best = (X: 0, Z: 0, Span: -1);
        var score = float.MaxValue;
        var cx = (int)((origin.X - field.bmin.X) / CELL_SIZE);
        var cz = (int)((origin.Z - field.bmin.Z) / CELL_SIZE);
        for (var z = Math.Max(0, cz - 2); z <= Math.Min(field.height - 1, cz + 2); ++z)
            for (var x = Math.Max(0, cx - 2); x <= Math.Min(field.width - 1, cx + 2); ++x)
            {
                var cell = field.cells[x + z * field.width];
                for (var i = cell.index; i < cell.index + cell.count; ++i)
                {
                    if (field.areas[i] == RcRecast.RC_NULL_AREA) continue;
                    var dy = MathF.Abs(field.bmin.Y + field.spans[i].y * CELL_HEIGHT - origin.Y);
                    if (dy > .7f) continue;
                    var current = (x - cx) * (x - cx) + (z - cz) * (z - cz) + dy * dy;
                    if (current >= score) continue;
                    score = current; best = (x, z, i);
                }
            }
        return best;
    }
}

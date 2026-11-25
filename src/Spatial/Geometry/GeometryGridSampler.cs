using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

// Shared with native output: preserves the existing finite sampling and component selection.
internal static class GeometryGridSampler
{
    private const float SampleStep = .5f;
    private const float MaxSampleRadius = 60;
    private const float ComplementFillRatio = .65f;
    private const int MaxSampleComponents = 4;
    internal static IReadOnlyList<WorldPolygonGeometry> Sample(Vector3 center, float radius, Func<float, float, float> distance)
    {
        radius = Math.Clamp(radius, 5f, MaxSampleRadius);
        var half = (int)MathF.Ceiling(radius / SampleStep);
        var total = (2 * half + 1) * (2 * half + 1);
        if (total > 200 * 200)
        {
            half = 99;
            radius = half * SampleStep;
        }
        var width = 2 * half + 1;
        var filled = new bool[width * width];
        var filledCount = 0;
        var radiusSq = radius * radius;
        for (var gz = 0; gz < width; ++gz)
        {
            for (var gx = 0; gx < width; ++gx)
            {
                var dx = (gx - half) * SampleStep;
                var dz = (gz - half) * SampleStep;
                if (dx * dx + dz * dz > radiusSq)
                    continue;
                if (distance(center.X + dx, center.Z + dz) <= 0f)
                {
                    filled[gz * width + gx] = true;
                    ++filledCount;
                }
            }
        }
        if (filledCount == 0 || filledCount > ComplementFillRatio * MathF.PI * radiusSq / (SampleStep * SampleStep))
            return [];
        return Trace(filled, width, MaxSampleComponents, 2,
            (cx, cz) => new Vector3(center.X + (cx - half - 0.5f) * SampleStep, center.Y, center.Z + (cz - half - 0.5f) * SampleStep));
    }

    internal static IReadOnlyList<WorldPolygonGeometry> Trace(bool[] filled, int width, int maxComponents, int minCells,
        Func<int, int, Vector3> cornerToWorld)
    {
        var visited = new bool[filled.Length];
        var components = new List<List<int>>();
        for (var i = 0; i < filled.Length; ++i)
        {
            if (!filled[i] || visited[i])
                continue;
            var component = new List<int>();
            var stack = new Stack<int>();
            stack.Push(i);
            visited[i] = true;
            while (stack.Count > 0)
            {
                var cell = stack.Pop();
                component.Add(cell);
                var cx = cell % width;
                var cz = cell / width;
                foreach (var next in new[] { cx > 0 ? cell - 1 : -1, cx + 1 < width ? cell + 1 : -1, cz > 0 ? cell - width : -1, cz + 1 < width ? cell + width : -1 })
                {
                    if (next >= 0 && filled[next] && !visited[next])
                    {
                        visited[next] = true;
                        stack.Push(next);
                    }
                }
            }
            components.Add(component);
        }
        components.Sort(static (a, b) => b.Count.CompareTo(a.Count));

        var result = new List<WorldPolygonGeometry>();
        foreach (var component in components.Take(maxComponents))
        {
            if (component.Count < minCells)
                continue;
            var outline = TraceOutline(filled, width, component);
            if (outline.Count < 3)
                continue;
            var points = outline.Select(corner => cornerToWorld(corner.X, corner.Z)).ToList();
            var polygon = TryPolygon(points);
            if (polygon != null)
                result.Add(polygon);
        }
        return result;
    }

    // 角点网格（0..width）上的边界边链接：每个填充格对空邻居产出有向边，行进方向使填充格保持在同一侧；
    // 对角捏合点按 cross=+1 优先拆环，一个组件可能有多个环（孔洞），取边数最多的环作为外轮廓。
    private static List<(int X, int Z)> TraceOutline(bool[] filled, int width, List<int> component)
    {
        var edgeFrom = new Dictionary<int, List<int>>();
        foreach (var cell in component)
        {
            var cx = cell % width;
            var cz = cell / width;
            if (cz == 0 || !filled[cell - width])
                AddEdge(edgeFrom, cx, cz, cx + 1, cz);
            if (cx + 1 == width || !filled[cell + 1])
                AddEdge(edgeFrom, cx + 1, cz, cx + 1, cz + 1);
            if (cz + 1 == width || !filled[cell + width])
                AddEdge(edgeFrom, cx + 1, cz + 1, cx, cz + 1);
            if (cx == 0 || !filled[cell - 1])
                AddEdge(edgeFrom, cx, cz + 1, cx, cz);
        }
        if (edgeFrom.Count == 0)
            return [];

        var loops = new List<List<(int X, int Z)>>();
        var visitedEdges = new HashSet<long>();
        foreach (var (from, tos) in edgeFrom)
        {
            foreach (var to in tos)
            {
                if (!visitedEdges.Add(PackEdge(from, to)))
                    continue;
                var loop = new List<(int X, int Z)> { Unpack(from) };
                var prev = from;
                var current = to;
                var guard = edgeFrom.Count + 16;
                while (current != from && guard-- > 0)
                {
                    loop.Add(Unpack(current));
                    var (prevX, prevZ) = Unpack(prev);
                    var (curX, curZ) = Unpack(current);
                    var dirX = Math.Sign(curX - prevX);
                    var dirZ = Math.Sign(curZ - prevZ);
                    var next = -1;
                    var nextCross = int.MinValue;
                    foreach (var candidate in edgeFrom[current])
                    {
                        if (visitedEdges.Contains(PackEdge(current, candidate)))
                            continue;
                        var (candX, candZ) = Unpack(candidate);
                        var candDirX = Math.Sign(candX - curX);
                        var candDirZ = Math.Sign(candZ - curZ);
                        var cross = dirX * candDirZ - dirZ * candDirX;
                        if (cross > nextCross)
                        {
                            nextCross = cross;
                            next = candidate;
                        }
                    }
                    if (next < 0)
                        break;
                    visitedEdges.Add(PackEdge(current, next));
                    prev = current;
                    current = next;
                }
                if (current == from && loop.Count >= 3)
                    loops.Add(loop);
            }
        }
        return loops.Count == 0 ? [] : loops.OrderByDescending(l => l.Count).First();
    }

    private static void AddEdge(Dictionary<int, List<int>> edges, int fromX, int fromZ, int toX, int toZ)
    {
        var from = fromX * 1000003 + fromZ;
        if (!edges.TryGetValue(from, out var list))
            edges[from] = list = [];
        list.Add(toX * 1000003 + toZ);
    }

    private static long PackEdge(int from, int to) => ((long)from << 32) | (uint)to;

    private static (int X, int Z) Unpack(int vertex) => (vertex / 1000003, vertex % 1000003);

    private static WorldPolygonGeometry? TryPolygon(IReadOnlyList<Vector3> points)
    {
        if (points.Count < 3)
            return null;
        try
        {
            return new WorldPolygonGeometry(points);
        }
        catch (ArgumentException)
        {
            // Sampling may produce a degenerate outline; the existing adapter drops it.
            return null;
        }
    }
}

using System.Numerics;
using System.Runtime.Intrinsics.X86;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

// Resolved arenas need the source's relative-coordinate edge arithmetic.
// Converting to mirrored local points or rounded world vertices changes corners.
internal sealed partial class AllianceArenaGeometry : GeometryShape
{
    private const float EDGE_EPSILON = 1e-7f;
    private readonly Vector2[][] contours;
    private readonly Vector2[][]? triangles;
    internal IReadOnlyList<IReadOnlyList<Vector3>>? FillPolygons { get; }
    private readonly (float Low, float High, float StartX, float Slope, float Intercept, float MinX, float MaxX)[] edges;
    private readonly (float Y, float MinX, float MaxX)[] horizontal;
    private readonly Vector2 minimum;
    private readonly Vector2 maximum;
    private readonly float inverseCellHeight;
    private readonly float[] rowMinX;
    private readonly float[] rowMaxX;
    private readonly int[][] rowEdges;
    private readonly int[][] rowHorizontal;

    internal AllianceArenaGeometry(Vector3 center, Vector2[] points) : this(center, [points], null) { }

    internal AllianceArenaGeometry(Vector3 center, Vector2[][] contours, Vector2[][]? triangles) : base(center)
    {
        this.contours = contours;
        this.triangles = triangles;
        FillPolygons = triangles?.Select(t => (IReadOnlyList<Vector3>)t.Select(p =>
            center + new Vector3(p.X, 0, p.Y)).ToArray()).ToArray();
        var points = contours.SelectMany(c => c).ToArray();
        minimum = new(points.Min(p => p.X), points.Min(p => p.Y));
        maximum = new(points.Max(p => p.X), points.Max(p => p.Y));
        var sloped = new List<(float, float, float, float, float, float, float)>();
        var flat = new List<(float, float, float)>();
        foreach (var contour in contours)
        for (var i = 0; i < contour.Length; ++i)
        {
            var a = contour[(i + contour.Length - 1) % contour.Length];
            var b = contour[i];
            var minX = MathF.Min(a.X, b.X);
            var maxX = MathF.Max(a.X, b.X);
            if (MathF.Abs(a.Y - b.Y) <= EDGE_EPSILON)
            {
                flat.Add((a.Y, minX, maxX));
                continue;
            }
            if (a.Y > b.Y) (a, b) = (b, a);
            var slope = (b.X - a.X) / (b.Y - a.Y);
            sloped.Add((a.Y, b.Y, a.X, slope, a.X - slope * a.Y, minX, maxX));
        }
        edges = sloped.ToArray();
        horizontal = flat.ToArray();
        var rows = Math.Clamp((int)MathF.Round(MathF.Sqrt(Math.Max(edges.Length + horizontal.Length, 1)) * .9f) + 8, 32, 512);
        var cellHeight = Math.Max(maximum.Y - minimum.Y, EDGE_EPSILON) / rows;
        inverseCellHeight = 1f / cellHeight;
        rowMinX = Enumerable.Repeat(float.PositiveInfinity, rows).ToArray();
        rowMaxX = Enumerable.Repeat(float.NegativeInfinity, rows).ToArray();
        var rowSloped = Enumerable.Range(0, rows).Select(_ => new List<int>()).ToArray();
        var rowFlat = Enumerable.Range(0, rows).Select(_ => new List<int>()).ToArray();
        for (var i = 0; i < edges.Length; ++i)
        {
            var edge = edges[i];
            var first = Row(edge.Low);
            var last = Row(MathF.BitDecrement(edge.High));
            // Match the source's incremental binary32 row arithmetic, including
            // the top-exclusive clip. Recomputing each row changes corner ULPs.
            var rowLow = minimum.Y + first * cellHeight;
            for (var r = first; r <= last; ++r, rowLow += cellHeight)
            {
                rowSloped[r].Add(i);
                var low = Math.Max(edge.Low, rowLow);
                var high = MathF.BitDecrement(Math.Min(edge.High, rowLow + cellHeight));
                var start = edge.StartX + edge.Slope * (low - edge.Low);
                var end = edge.StartX + edge.Slope * (high - edge.Low);
                rowMinX[r] = Math.Min(rowMinX[r], Math.Min(start, end));
                rowMaxX[r] = Math.Max(rowMaxX[r], Math.Max(start, end));
            }
        }
        for (var i = 0; i < horizontal.Length; ++i)
        {
            var edge = horizontal[i];
            var r = Row(edge.Y);
            rowFlat[r].Add(i);
            rowMinX[r] = Math.Min(rowMinX[r], edge.MinX);
            rowMaxX[r] = Math.Max(rowMaxX[r], edge.MaxX);
        }
        rowEdges = rowSloped.Select(r => r.ToArray()).ToArray();
        rowHorizontal = rowFlat.Select(r => r.ToArray()).ToArray();
    }

    internal AllianceArenaGeometry AtHeight(float y) => new(Position with { Y = y }, contours, triangles);
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    private int Row(float y) => Math.Clamp((int)((y - minimum.Y) * inverseCellHeight), 0, rowMinX.Length - 1);

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var x = point.X - Position.X;
        var z = point.Z - Position.Z;
        if (x < minimum.X - EDGE_EPSILON || x > maximum.X + EDGE_EPSILON
            || z < minimum.Y - EDGE_EPSILON || z > maximum.Y + EDGE_EPSILON) return false;
        var row = Row(z);
        if (x < rowMinX[row] - EDGE_EPSILON || x > rowMaxX[row] + EDGE_EPSILON) return false;
        foreach (var i in rowHorizontal[row])
        {
            var edge = horizontal[i];
            if (edge.Y >= z - EDGE_EPSILON && edge.Y <= z + EDGE_EPSILON
                && x >= edge.MinX - EDGE_EPSILON && x <= edge.MaxX + EDGE_EPSILON) return true;
        }
        var inside = false;
        foreach (var i in rowEdges[row])
        {
            var edge = edges[i];
            var simd = Avx512F.IsSupported || Avx2.IsSupported;
            if (z < (simd ? edge.Low : edge.Low - EDGE_EPSILON)
                || z >= (simd ? edge.High : edge.High - EDGE_EPSILON)) continue;
            var crossing = Avx512F.IsSupported || Avx2.IsSupported && Fma.IsSupported
                ? MathF.FusedMultiplyAdd(edge.Slope, z, edge.Intercept)
                : edge.Slope * z + edge.Intercept;
            if (MathF.Abs(x - crossing) <= EDGE_EPSILON
                && x >= edge.MinX - EDGE_EPSILON && x <= edge.MaxX + EDGE_EPSILON) return true;
            if (crossing > x) inside = !inside;
        }
        return inside;
    }

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
        => contours.Select(c => (IReadOnlyList<Vector3>)c.Select(p => Position + new Vector3(p.X, 0, p.Y)).ToArray()).ToArray();
}

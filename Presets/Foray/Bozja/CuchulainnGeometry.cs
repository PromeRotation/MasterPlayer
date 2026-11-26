using System.Numerics;
using System.Runtime.Intrinsics.X86;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

// DAL2's clipped convex arena retains the source row traversal and arithmetic,
// including padded first-row SIMD queries and scalar newly encountered edges.
internal sealed class CuchulainnGeometry
{
    private const float EPSILON = 1e-7f;
    private readonly record struct Edge(float X, float Y, float Top, float Slope, float Dx, float Dy,
        float MinX, float MaxX, int First, int Last);
    private readonly record struct Horizontal(float Y, float MinX, float MaxX);
    private readonly Vector3 center;
    private readonly float minX, maxX, minY, maxY, cellHeight, inverseCellHeight;
    private readonly Edge[][] rows;
    private readonly Horizontal[][] horizontals;
    private readonly float[] rowMinX, rowMaxX;

    internal CuchulainnGeometry(IGeometryShape arena)
    {
        center = arena.Position;
        var points = ((LocalPolygonGeometry)arena).LocalPoints.Select(p => -p).ToArray();
        minX = points.Min(p => p.X); maxX = points.Max(p => p.X);
        minY = points.Min(p => p.Y); maxY = points.Max(p => p.Y);
        var count = Math.Clamp((int)MathF.Round(MathF.Sqrt(points.Length) * .9f) + 8, 32, 512);
        cellHeight = Math.Max(maxY - minY, EPSILON) / count;
        inverseCellHeight = 1f / cellHeight;
        var edges = new List<Edge>[count];
        var flat = new List<Horizontal>[count];
        rowMinX = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
        rowMaxX = Enumerable.Repeat(float.NegativeInfinity, count).ToArray();
        for (var r = 0; r < count; ++r) { edges[r] = []; flat[r] = []; }
        for (var i = 0; i < points.Length; ++i)
        {
            var a = points[(i + points.Length - 1) % points.Length]; var b = points[i];
            var lo = Math.Min(a.X, b.X); var hi = Math.Max(a.X, b.X);
            if (MathF.Abs(a.Y - b.Y) <= EPSILON)
            {
                var row = ClampRow(a.Y);
                flat[row].Add(new(a.Y, lo, hi));
                rowMinX[row] = Math.Min(rowMinX[row], lo); rowMaxX[row] = Math.Max(rowMaxX[row], hi);
                continue;
            }
            if (a.Y > b.Y) (a, b) = (b, a);
            var dy = b.Y - a.Y; var slope = (b.X - a.X) / dy;
            var first = Math.Max(0, (int)((a.Y - minY) * inverseCellHeight));
            var last = Math.Min(count - 1, (int)((MathF.BitDecrement(b.Y) - minY) * inverseCellHeight));
            var edge = new Edge(a.X, a.Y, b.Y, slope, slope * dy, dy, lo, hi, first, last);
            var rowBottom = minY + first * cellHeight;
            for (var r = first; r <= last; ++r, rowBottom += cellHeight)
            {
                edges[r].Add(edge);
                var start = Math.Max(a.Y, rowBottom);
                var end = MathF.BitDecrement(Math.Min(b.Y, rowBottom + cellHeight));
                var xs = a.X + slope * (start - a.Y);
                var xe = a.X + slope * (end - a.Y);
                rowMinX[r] = Math.Min(rowMinX[r], Math.Min(xs, xe));
                rowMaxX[r] = Math.Max(rowMaxX[r], Math.Max(xs, xe));
            }
        }
        rows = edges.Select((es, r) => es.OrderBy(e => Category(e, r)).ToArray()).ToArray();
        horizontals = flat.Select(es => es.OrderBy(e => e.Y).ToArray()).ToArray();
    }

    private static int Category(Edge e, int row)
    {
        if (e.First == row) return e.Last == row ? 3 : 2;
        return e.Last == row ? 1 : 0;
    }

    private int ClampRow(float y) => Math.Clamp((int)((y - minY) * inverseCellHeight), 0, rowMinX.Length - 1);

    internal float WallDistance(Vector3 from, Vector3 direction)
    {
        var ox = from.X - center.X; var oz = from.Z - center.Z;
        var dx = direction.X; var dz = direction.Z;
        var hasX = MathF.Abs(dx) > EPSILON; var hasZ = MathF.Abs(dz) > EPSILON;
        var inverseX = hasX ? 1f / dx : 0; var inverseZ = hasZ ? 1f / dz : 0;
        var tMin = -float.Epsilon; var tMax = float.MaxValue;
        if (!Clip(ox, inverseX, hasX, minX, maxX, ref tMin, ref tMax)
            || !Clip(oz, inverseZ, hasZ, minY, maxY, ref tMin, ref tMax)
            || tMax < 0 || tMin > tMax) return float.MaxValue;
        var best = float.MaxValue;
        var t0 = Math.Max(0, tMin);
        if (!hasZ)
        {
            if (!hasX) return best;
            var row = ClampRow(oz);
            foreach (var e in rows[row])
            {
                if (oz < e.Y - EPSILON || oz >= e.Top - EPSILON) continue;
                var intercept = e.X - e.Slope * e.Y;
                var crossing = Avx512F.IsSupported || Avx2.IsSupported && Fma.IsSupported
                    ? MathF.FusedMultiplyAdd(e.Slope, oz, intercept) : e.Slope * oz + intercept;
                var t = (crossing - ox) * inverseX;
                if (t >= t0 && t <= tMax && t < best && crossing >= e.MinX - EPSILON && crossing <= e.MaxX + EPSILON) best = t;
            }
            foreach (var e in horizontals[row])
            {
                if (e.Y < oz - EPSILON || e.Y > oz + EPSILON) continue;
                var x = dx > 0 ? Math.Max(ox, e.MinX) : Math.Min(ox, e.MaxX);
                if (x < e.MinX || x > e.MaxX) continue;
                var t = (x - ox) * inverseX;
                if (t >= t0 && t <= tMax && t < best) best = t;
            }
            return best;
        }
        var startY = oz + dz * t0;
        var current = ClampRow(startY);
        var step = dz > 0 ? 1 : -1;
        var nextY = minY + (step > 0 ? current + 1 : current) * cellHeight;
        var enterX = ox + dx * t0;
        var activeComplete = false; var hit = false; var rowTMin = t0;
        while ((uint)current < rows.Length)
        {
            var boundary = (nextY - oz) * inverseZ;
            var exitX = ox + dx * boundary;
            var rowTMax = Math.Min(tMax, best);
            if (!(Math.Max(enterX, exitX) + 2e-6f < rowMinX[current] || Math.Min(enterX, exitX) - 2e-6f > rowMaxX[current]))
            {
                var previous = best;
                var full = !activeComplete;
                foreach (var e in rows[current])
                {
                    if (!full && (step > 0 ? e.First != current : e.Last != current)) continue;
                    var vector = full && (Avx512F.IsSupported || Avx2.IsSupported);
                    Intersect(e, ox, oz, dx, dz, t0, rowTMax, vector, ref best);
                }
                activeComplete = true;
                if (best < previous) { hit = true; rowTMax = Math.Min(rowTMax, best); }
                var endY = oz + dz * rowTMax;
                var toY = Math.Max(oz + dz * rowTMin, endY) + EPSILON;
                foreach (var e in horizontals[current])
                {
                    // The source skips the lower binary-search bound for this
                    // arena's one horizontal per row, then checks t directly.
                    if (e.Y > toY) break;
                    var t = (e.Y - oz) * inverseZ;
                    if (t < t0 || t > rowTMax) continue;
                    var x = ox + t * dx;
                    if (x >= e.MinX - 1e-6f && x <= e.MaxX + 1e-6f)
                    { best = t; hit = true; rowTMax = t; }
                }
                if (hit && best <= boundary + 1e-6f) break;
            }
            else activeComplete = false;
            if (boundary >= tMax - 1e-6f) break;
            current += step; rowTMin = boundary; nextY += step * cellHeight; enterX = exitX;
        }
        return best;
    }

    private static bool Clip(float origin, float inverse, bool hasDirection, float min, float max, ref float tMin, ref float tMax)
    {
        if (!hasDirection) return origin >= min - EPSILON && origin <= max + EPSILON;
        var first = (min - origin) * inverse; var last = (max - origin) * inverse;
        if (first > last) (first, last) = (last, first);
        tMin = Math.Max(tMin, first); tMax = Math.Min(tMax, last);
        return true;
    }

    private static void Intersect(Edge e, float ox, float oz, float dx, float dz, float tMin, float tMax, bool vector, ref float best)
    {
        var fused = vector && (Avx512F.IsSupported || Fma.IsSupported);
        float ProductDifference(float a, float b, float c) => fused ? MathF.FusedMultiplyAdd(a, b, -c) : a * b - c;
        float ProductSum(float a, float b, float c) => fused ? MathF.FusedMultiplyAdd(a, b, c) : a * b + c;
        var x = e.X - ox; var z = e.Y - oz;
        // The source AVX2 denominator uses two multiplies and subtraction even
        // when its numerators use FMA; only AVX512 fuses this denominator.
        var den = vector && Avx512F.IsSupported
            ? MathF.FusedMultiplyAdd(dx, e.Dy, -(dz * e.Dx)) : dx * e.Dy - dz * e.Dx;
        var absDen = MathF.Abs(den);
        if (absDen > 1e-9f)
        {
            var tNum = ProductDifference(x, e.Dy, z * e.Dx);
            var uNum = ProductDifference(x, dz, z * dx);
            if (den < 0) { tNum = -tNum; uNum = -uNum; }
            if (tNum >= tMin * absDen && tNum <= Math.Min(tMax, best) * absDen
                && uNum >= 0 && uNum < (vector ? absDen : (1f - 1e-6f) * absDen)) best = tNum / absDen;
        }
        else if (MathF.Abs(ProductDifference(x, dz, z * dx)) <= EPSILON)
        {
            var inverse = 1f / (dx * dx + dz * dz + 1e-20f);
            var first = ProductSum(x, dx, z * dz) * inverse;
            var last = vector ? ProductSum(x + e.Dx, dx, (z + e.Dy) * dz) * inverse
                : ((e.X + e.Dx - ox) * dx + (e.Y + e.Dy - oz) * dz) * inverse;
            var t = Math.Max(Math.Min(first, last), tMin);
            if (t <= Math.Max(first, last) && t <= tMax && t < best) best = t;
        }
    }
}

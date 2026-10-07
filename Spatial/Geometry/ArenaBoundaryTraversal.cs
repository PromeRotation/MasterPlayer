using System.Numerics;
using System.Buffers;

namespace MasterPlayer.Spatial.Geometry;

internal static class ArenaBoundaryTraversal
{
    public static bool ContainsSegment(IGeometryShape boundary, Vector3 from, Vector3 to)
    {
        if (!boundary.Contains(from) || !boundary.Contains(to)) return false;
        var start = new Vector2(from.X, from.Z);
        var direction = new Vector2(to.X - from.X, to.Z - from.Z);
        var lengthSquared = direction.LengthSquared();
        if (lengthSquared < 0.00000001f) return true;
        if (boundary is IStaticArenaBoundary prepared)
            return ContainsIndexedSegment(boundary, prepared.TraversalIndex, from, to, start, direction, lengthSquared);
        var cuts = new List<float> { 0, 1 };
        foreach (var contour in boundary.GetContours())
            for (var i = 0; i < contour.Count; i++)
            {
                var a = new Vector2(contour[i].X, contour[i].Z);
                var b = new Vector2(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z);
                var edge = b - a;
                var offset = a - start;
                var divisor = Cross(direction, edge);
                if (MathF.Abs(divisor) > 0.000001f)
                {
                    var t = Cross(offset, edge) / divisor;
                    var u = Cross(offset, direction) / divisor;
                    if (t is >= 0 and <= 1 && u is >= 0 and <= 1) cuts.Add(t);
                }
                else if (MathF.Abs(Cross(offset, direction)) < 0.000001f)
                {
                    cuts.Add(Math.Clamp(Vector2.Dot(a - start, direction) / lengthSquared, 0, 1));
                    cuts.Add(Math.Clamp(Vector2.Dot(b - start, direction) / lengthSquared, 0, 1));
                }
            }
        cuts.Sort();
        for (var i = 1; i < cuts.Count; i++)
            if (cuts[i] - cuts[i - 1] > 0.000001f
                && !boundary.Contains(Vector3.Lerp(from, to, (cuts[i] + cuts[i - 1]) * 0.5f))) return false;
        return true;
    }

    private static bool ContainsIndexedSegment(IGeometryShape boundary, ArenaBoundaryIndex index,
        Vector3 from, Vector3 to, Vector2 start, Vector2 direction, float lengthSquared)
    {
        Span<float> cuts = stackalloc float[64];
        float[]? rented = null;
        var count = 2;
        cuts[0] = 0; cuts[1] = 1;
        var minX = MathF.Min(from.X, to.X); var maxX = MathF.Max(from.X, to.X);
        var minZ = MathF.Min(from.Z, to.Z); var maxZ = MathF.Max(from.Z, to.Z);
        var first = index.Bin(minZ); var last = index.Bin(maxZ);
        try
        {
            for (var bin = first; bin <= last; ++bin)
                foreach (var candidate in index.Edges(bin))
                {
                    if (bin != Math.Max(first, index.Bin(candidate.MinZ))
                        || candidate.MaxX < minX || candidate.MinX > maxX
                        || candidate.MaxZ < minZ || candidate.MinZ > maxZ) continue;
                    if (count + 2 > cuts.Length)
                    {
                        var next = ArrayPool<float>.Shared.Rent(cuts.Length * 2);
                        cuts[..count].CopyTo(next);
                        if (rented != null) ArrayPool<float>.Shared.Return(rented);
                        rented = next; cuts = next;
                    }
                    var edge = candidate.B - candidate.A;
                    var offset = candidate.A - start;
                    var divisor = Cross(direction, edge);
                    if (MathF.Abs(divisor) > 0.000001f)
                    {
                        var t = Cross(offset, edge) / divisor;
                        var u = Cross(offset, direction) / divisor;
                        if (t is >= 0 and <= 1 && u is >= 0 and <= 1) cuts[count++] = t;
                    }
                    else if (MathF.Abs(Cross(offset, direction)) < 0.000001f)
                    {
                        cuts[count++] = Math.Clamp(Vector2.Dot(candidate.A - start, direction) / lengthSquared, 0, 1);
                        cuts[count++] = Math.Clamp(Vector2.Dot(candidate.B - start, direction) / lengthSquared, 0, 1);
                    }
                }
            cuts = cuts[..count];
            cuts.Sort();
            for (var i = 1; i < cuts.Length; ++i)
                if (cuts[i] - cuts[i - 1] > 0.000001f
                    && !boundary.Contains(Vector3.Lerp(from, to, (cuts[i] + cuts[i - 1]) * .5f))) return false;
            return true;
        }
        finally { if (rented != null) ArrayPool<float>.Shared.Return(rented); }
    }
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}

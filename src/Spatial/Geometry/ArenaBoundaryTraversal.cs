using System.Numerics;
using System.Buffers;

namespace MasterPlayer.Spatial.Geometry;

internal static class ArenaBoundaryTraversal
{
    // The contours must describe the actual boundary, including holes. Extra
    // contours inside a union are treated conservatively as boundary edges.
    public static bool ContainsTube(IGeometryShape boundary, Vector3 from, Vector3 to, float radius)
    {
        if (!float.IsFinite(radius) || radius < 0 || !Finite(from) || !Finite(to)) return false;
        if (radius == 0) return ContainsSegment(boundary, from, to);
        if (!boundary.Contains(from, 0) || !boundary.Contains(to, 0)) return false;
        var start = new Vector2(from.X, from.Z);
        var end = new Vector2(to.X, to.Z);
        if (boundary is CircleGeometry circle)
            return FitsCircle(start, end, circle.Position, circle.Radius, radius);
        if (boundary is DonutGeometry donut)
            return FitsCircle(start, end, donut.Position, donut.OuterRadius, radius)
                && (donut.InnerRadius == 0 || PointSegmentDistanceSquared(new(donut.Position.X, donut.Position.Z), start, end)
                    >= ((double)donut.InnerRadius + radius) * ((double)donut.InnerRadius + radius));
        if (!ContainsSegment(boundary, from, to)) return false;
        if (boundary is FanGeometry fan)
        {
            if (!FitsCircle(start, end, fan.Position, fan.Radius, radius)) return false;
            if (fan.AngleRadians >= 2 * MathF.PI) return true;
            var center = new Vector2(fan.Position.X, fan.Position.Z);
            var left = GeometryHelper.PointFromRotationDistanceXZ(fan.Position, fan.Rotation - fan.HalfAngleRadians, fan.Radius);
            var right = GeometryHelper.PointFromRotationDistanceXZ(fan.Position, fan.Rotation + fan.HalfAngleRadians, fan.Radius);
            var squaredRadius = (double)radius * radius;
            return SegmentDistanceSquared(start, end, center, new(left.X, left.Z)) >= squaredRadius
                && SegmentDistanceSquared(start, end, center, new(right.X, right.Z)) >= squaredRadius;
        }
        var minX = Math.Min(from.X, to.X) - (double)radius;
        var maxX = Math.Max(from.X, to.X) + (double)radius;
        var minZ = Math.Min(from.Z, to.Z) - (double)radius;
        var maxZ = Math.Max(from.Z, to.Z) + (double)radius;
        var radiusSquared = (double)radius * radius;
        if (boundary is IStaticArenaBoundary prepared)
        {
            var index = prepared.TraversalIndex;
            var first = index.Bin((float)minZ); var last = index.Bin((float)maxZ);
            for (var bin = first; bin <= last; ++bin)
                foreach (var candidate in index.Edges(bin))
                {
                    if (bin != Math.Max(first, index.Bin(candidate.MinZ))
                        || candidate.MaxX < minX || candidate.MinX > maxX
                        || candidate.MaxZ < minZ || candidate.MinZ > maxZ) continue;
                    if (SegmentDistanceSquared(start, end, candidate.A, candidate.B) < radiusSquared) return false;
                }
        }
        else
        {
            foreach (var contour in boundary.GetContours())
                for (var i = 0; i < contour.Count; ++i)
                {
                    var a = new Vector2(contour[i].X, contour[i].Z);
                    var b = new Vector2(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z);
                    if (Math.Max(a.X, b.X) < minX || Math.Min(a.X, b.X) > maxX
                        || Math.Max(a.Y, b.Y) < minZ || Math.Min(a.Y, b.Y) > maxZ) continue;
                    if (SegmentDistanceSquared(start, end, a, b) < radiusSquared) return false;
                }
        }
        return true;
    }

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
    private static bool Finite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);

    private static bool FitsCircle(Vector2 from, Vector2 to, Vector3 position, float boundaryRadius, float radius)
    {
        var clearance = (double)boundaryRadius - radius;
        if (clearance < 0) return false;
        var center = new Vector2(position.X, position.Z);
        return Math.Max(PointSegmentDistanceSquared(from, center, center), PointSegmentDistanceSquared(to, center, center))
            <= clearance * clearance;
    }

    private static double SegmentDistanceSquared(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var abX = (double)b.X - a.X; var abZ = (double)b.Y - a.Y;
        var cdX = (double)d.X - c.X; var cdZ = (double)d.Y - c.Y;
        var acX = (double)c.X - a.X; var acZ = (double)c.Y - a.Y;
        var divisor = abX * cdZ - abZ * cdX;
        if (divisor != 0)
        {
            var t = (acX * cdZ - acZ * cdX) / divisor;
            var u = (acX * abZ - acZ * abX) / divisor;
            if (t is >= 0 and <= 1 && u is >= 0 and <= 1) return 0;
        }
        return Math.Min(Math.Min(PointSegmentDistanceSquared(a, c, d), PointSegmentDistanceSquared(b, c, d)),
            Math.Min(PointSegmentDistanceSquared(c, a, b), PointSegmentDistanceSquared(d, a, b)));
    }

    private static double PointSegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
    {
        var dx = (double)b.X - a.X; var dz = (double)b.Y - a.Y;
        var px = (double)point.X - a.X; var pz = (double)point.Y - a.Y;
        var lengthSquared = dx * dx + dz * dz;
        var t = lengthSquared == 0 ? 0 : Math.Clamp((px * dx + pz * dz) / lengthSquared, 0, 1);
        var offsetX = px - t * dx; var offsetZ = pz - t * dz;
        return offsetX * offsetX + offsetZ * offsetZ;
    }
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}

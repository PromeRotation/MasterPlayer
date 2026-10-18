using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleSegmentContacts
{
    // Fractions of a constant-speed segment occupied by the expanded shape.
    internal static (double From, double Until)? Find(IGeometryShape shape, Vector3 from, Vector3 to, float margin)
    {
        if (shape is CircleGeometry circle)
        {
            double x = (double)from.X - circle.Position.X;
            double z = (double)from.Z - circle.Position.Z;
            double dx = (double)to.X - from.X;
            double dz = (double)to.Z - from.Z;
            double radius = circle.Radius + margin;
            var length = dx * dx + dz * dz;
            var c = x * x + z * z - radius * radius;
            if (length <= 1e-20)
                return c <= 0 ? (0, 1) : null;
            var b = x * dx + z * dz;
            var discriminant = b * b - length * c;
            if (discriminant < 0)
                return null;
            var root = Math.Sqrt(discriminant);
            var first = Math.Max(0, (-b - root) / length);
            var last = Math.Min(1, (-b + root) / length);
            return first <= last ? (first, last) : null;
        }

        if (shape is RectangleGeometry or RayRectangleGeometry)
        {
            var a = GeometryHelper.WorldToLocal(from, shape.Position, shape.Rotation);
            var b = GeometryHelper.WorldToLocal(to, shape.Position, shape.Rotation);
            double first = 0, last = 1;
            if (shape is RectangleGeometry box)
            {
                if (Clip(a.X, (double)b.X - a.X, -box.Width / 2 - margin, box.Width / 2 + margin, ref first, ref last)
                    && Clip(a.Y, (double)b.Y - a.Y, -box.Length / 2 - margin, box.Length / 2 + margin, ref first, ref last))
                    return (first, last);
            }
            else if (shape is RayRectangleGeometry ray
                && Clip(a.X, (double)b.X - a.X, ray.MinLocalX - margin, ray.MaxLocalX + margin, ref first, ref last)
                && Clip(a.Y, (double)b.Y - a.Y, -ray.Length - margin, margin, ref first, ref last))
                return (first, last);
            return null;
        }

        // Other shapes may be non-convex: keep their complete intersecting
        // segment occupied rather than assume a single convex contact range.
        return shape.Contains(from, margin) || shape.Contains(to, margin) || shape.IntersectsSegment(from, to, margin)
            ? (0, 1) : null;
    }

    private static bool Clip(double origin, double delta, double minimum, double maximum, ref double first, ref double last)
    {
        if (Math.Abs(delta) <= 1e-12)
            return origin >= minimum && origin <= maximum;
        var a = (minimum - origin) / delta;
        var b = (maximum - origin) / delta;
        first = Math.Max(first, Math.Min(a, b));
        last = Math.Min(last, Math.Max(a, b));
        return first <= last;
    }
}

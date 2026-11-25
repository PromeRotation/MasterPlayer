using System.Numerics;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Drawing;

internal static class GroundProjection
{
    internal sealed record Mesh(Vector3[] Fill, Vector3[] Lines);

    internal static Mesh Project(DrawRenderItem item, GroundSurface surface)
    {
        var contours = item.Geometry.GetContours(64);
        var support = surface.FindSupport(item.Geometry.Position);
        // 圆环中心或凹多边形锚点可能恰好位于真实洞口；只从其自身轮廓补找支撑层。
        if (support < 0)
            foreach (var point in contours.SelectMany(contour => contour))
            {
                support = surface.FindSupport(point with { Y = item.Geometry.Position.Y });
                if (support >= 0) break;
            }
        if (support < 0) return new([], []);
        var layer = surface[support].Layer;
        var anchorHeight = item.Geometry.Position.Y;
        var fill = new List<Vector3>();
        Span<Vector3> polygon = stackalloc Vector3[128];
        var convex = item.Geometry is CircleGeometry or RectangleGeometry or RayRectangleGeometry
            || item.Geometry is FanGeometry { AngleRadians: > 0 and <= MathF.PI };
        if (convex && contours.Count == 1)
        {
            var contour = contours[0];
            var minimum = new Vector2(contour.Min(point => point.X), contour.Min(point => point.Z));
            var maximum = new Vector2(contour.Max(point => point.X), contour.Max(point => point.Z));
            var area = 0f;
            for (var i = 0; i < contour.Count; ++i)
                area += GroundTriangle.Cross(Xz(contour[i]), Xz(contour[(i + 1) % contour.Count]));
            var sign = MathF.Sign(area);
            var circleCenter = Xz(item.Geometry.Position);
            var innerRadius = item.Geometry is CircleGeometry circle ? circle.Radius * MathF.Cos(MathF.PI / contour.Count) : 0;
            var innerRadiusSquared = innerRadius * innerRadius;
            if (sign != 0)
                foreach (var index in surface.Query(minimum, maximum))
                {
                    var ground = surface[index];
                    if (ground.Layer != layer) continue;
                    polygon[0] = ground.A; polygon[1] = ground.B; polygon[2] = ground.C;
                    var count = 3;
                    // 圆形轮廓的内切圆内一定属于该凸多边形，内部三角面无需逐边裁剪。
                    var fullyInside = innerRadius > 0
                        && Vector2.DistanceSquared(Xz(ground.A), circleCenter) <= innerRadiusSquared
                        && Vector2.DistanceSquared(Xz(ground.B), circleCenter) <= innerRadiusSquared
                        && Vector2.DistanceSquared(Xz(ground.C), circleCenter) <= innerRadiusSquared;
                    if (!fullyInside)
                        for (var i = 0; i < contour.Count && count != 0; ++i)
                            count = ClipEdge(polygon, count, Xz(contour[i]), Xz(contour[(i + 1) % contour.Count]), sign);
                    AppendFill(surface, index, polygon[..count], anchorHeight, fill);
                }
        }
        else
        {
            var flat = new List<RenderBatch>();
            MeshBuilder.AddGeometry(flat, item with { Style = new DrawStyle(Vector4.One, null) }, Vector4.Zero, 0);
            foreach (var batch in flat)
            for (var i = 0; i + 2 < batch.Vertices.Length; i += 3)
            {
                var a = batch.Vertices[i].Position;
                var b = batch.Vertices[i + 1].Position;
                var c = batch.Vertices[i + 2].Position;
                var min = Vector2.Min(Xz(a), Vector2.Min(Xz(b), Xz(c)));
                var max = Vector2.Max(Xz(a), Vector2.Max(Xz(b), Xz(c)));
                foreach (var index in surface.Query(min, max))
                {
                    var ground = surface[index];
                    if (ground.Layer != layer) continue;
                    polygon[0] = ground.A; polygon[1] = ground.B; polygon[2] = ground.C;
                    var count = ClipTriangle(polygon, a, b, c);
                    AppendFill(surface, index, polygon[..count], anchorHeight, fill);
                }
            }
        }

        var lines = new List<Vector3>();
        for (var contourIndex = 0; contourIndex < contours.Count; ++contourIndex)
        {
            var contour = contours[contourIndex];
            var startIndex = item.Geometry is FanGeometry { AngleRadians: >= 6.2831755f } && contourIndex == 0 ? 1 : 0;
            for (var i = startIndex; i < contour.Count; ++i)
            {
                var a = contour[i];
                var b = contour[i + 1 < contour.Count ? i + 1 : startIndex];
                foreach (var index in surface.Query(Vector2.Min(Xz(a), Xz(b)), Vector2.Max(Xz(a), Xz(b))))
                {
                    var ground = surface[index];
                    if (ground.Layer != layer || !ClipLine(a, b, ground, out var start, out var end)) continue;
                    if (!surface.IsSelected(index, (start + end) / 2, anchorHeight)) continue;
                    // 共边上的轮廓可能同时落到两个面；去重在下方统一处理。
                    lines.Add(start); lines.Add(end);
                }
            }
        }
        var unique = new HashSet<(Vector3, Vector3)>();
        var deduplicated = new List<Vector3>();
        for (var i = 0; i < lines.Count; i += 2)
        {
            var a = lines[i]; var b = lines[i + 1];
            if (unique.Contains((b, a)) || !unique.Add((a, b))) continue;
            deduplicated.Add(a); deduplicated.Add(b);
        }
        return new(fill.ToArray(), deduplicated.ToArray());
    }

    private static void AppendFill(GroundSurface surface, int index, ReadOnlySpan<Vector3> polygon,
        float anchorHeight, List<Vector3> fill)
    {
        if (polygon.Length < 3) return;
        var center = Vector3.Zero;
        foreach (var point in polygon) center += point;
        if (!surface.IsSelected(index, center / polygon.Length, anchorHeight)) return;
        for (var j = 1; j + 1 < polygon.Length; ++j)
        {
            if (MathF.Abs(GroundTriangle.Cross(Xz(polygon[j] - polygon[0]), Xz(polygon[j + 1] - polygon[0]))) < 1e-8f)
                continue;
            fill.Add(polygon[0]); fill.Add(polygon[j]); fill.Add(polygon[j + 1]);
        }
    }

    internal static List<Vector3> ClipRectangle(Vector3 a, Vector3 b, Vector3 c, Vector2 min, Vector2 max)
    {
        Span<Vector3> polygon = stackalloc Vector3[12];
        polygon[0] = a; polygon[1] = b; polygon[2] = c;
        var count = ClipEdge(polygon, 3, min, new(max.X, min.Y), 1);
        count = ClipEdge(polygon, count, new(max.X, min.Y), max, 1);
        count = ClipEdge(polygon, count, max, new(min.X, max.Y), 1);
        count = ClipEdge(polygon, count, new(min.X, max.Y), min, 1);
        return polygon[..count].ToArray().ToList();
    }

    private static int ClipTriangle(Span<Vector3> polygon, Vector3 a, Vector3 b, Vector3 c)
    {
        var sign = MathF.Sign(GroundTriangle.Cross(Xz(b - a), Xz(c - a)));
        if (sign == 0) return 0;
        var count = ClipEdge(polygon, 3, Xz(a), Xz(b), sign);
        count = ClipEdge(polygon, count, Xz(b), Xz(c), sign);
        return ClipEdge(polygon, count, Xz(c), Xz(a), sign);
    }

    private static int ClipEdge(Span<Vector3> polygon, int count, Vector2 a, Vector2 b, float sign)
    {
        if (count == 0) return 0;
        var allInside = true;
        for (var i = 0; i < count; ++i)
            if (sign * GroundTriangle.Cross(b - a, Xz(polygon[i]) - a) < 0)
            {
                allInside = false;
                break;
            }
        if (allInside) return count;
        Span<Vector3> output = stackalloc Vector3[128];
        var written = 0;
        var previous = polygon[count - 1];
        var previousDistance = sign * GroundTriangle.Cross(b - a, Xz(previous) - a);
        for (var i = 0; i < count; ++i)
        {
            var current = polygon[i];
            var distance = sign * GroundTriangle.Cross(b - a, Xz(current) - a);
            if ((distance >= 0) != (previousDistance >= 0))
                output[written++] = Vector3.Lerp(previous, current, previousDistance / (previousDistance - distance));
            if (distance >= 0) output[written++] = current;
            previous = current;
            previousDistance = distance;
        }
        output[..written].CopyTo(polygon);
        return written;
    }

    private static bool ClipLine(Vector3 a, Vector3 b, GroundTriangle face, out Vector3 start, out Vector3 end)
    {
        start = end = default;
        var sign = MathF.Sign(GroundTriangle.Cross(Xz(face.B - face.A), Xz(face.C - face.A)));
        var minimum = 0f; var maximum = 1f;
        foreach (var (u, v) in (ReadOnlySpan<(Vector3, Vector3)>)[(face.A, face.B), (face.B, face.C), (face.C, face.A)])
        {
            var first = sign * GroundTriangle.Cross(Xz(v - u), Xz(a - u));
            var last = sign * GroundTriangle.Cross(Xz(v - u), Xz(b - u));
            if (first < 0 && last < 0) return false;
            if ((first < 0) == (last < 0)) continue;
            var t = first / (first - last);
            if (first < 0) minimum = MathF.Max(minimum, t);
            else maximum = MathF.Min(maximum, t);
        }
        if (maximum - minimum < 0.00001f) return false;
        start = Vector3.Lerp(a, b, minimum); end = Vector3.Lerp(a, b, maximum);
        face.TryHeight(Xz(start), out var startHeight);
        face.TryHeight(Xz(end), out var endHeight);
        start.Y = startHeight; end.Y = endHeight;
        return true;
    }

    private static Vector2 Xz(Vector3 value) => new(value.X, value.Z);
}

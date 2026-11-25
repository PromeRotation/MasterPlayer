using System.Numerics;
using Clipper2Lib;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

// PolygonBoundaryIndex2D.VisibilityFrom semantics, using the local geometry types.
// Keep the source vertex-angle jitter and merge tolerances; cylindrical shadows differ at wall ends.
internal static class DungeonPolygonVisibility
{
    internal static IGeometryShape Create(Vector3 center, Vector3 origin, Vector2[][] contours)
    {
        var localOrigin = new Vector2(origin.X - center.X, origin.Z - center.Z);
        var angles = contours.SelectMany(c => c).Select(v =>
        {
            var a = Math.Atan2(v.Y - localOrigin.Y, v.X - localOrigin.X);
            return a < 0 ? a + Math.Tau : a;
        }).Order().ToList();
        var unique = new List<double>();
        foreach (var a in angles)
            if (unique.Count == 0 || a - unique[^1] > 1e-12) unique.Add(a);
        if (unique.Count >= 2 && AngleDifference(unique[^1], unique[0]) < 1e-12) unique.RemoveAt(unique.Count - 1);
        var points = new List<(Vector2 Point, float Distance)>();
        for (var i = 0; i < unique.Count; ++i)
        {
            var start = unique[i];
            var end = unique[(i + 1) % unique.Count];
            var width = AngleDifference(start, end);
            if (width <= 1e-14) continue;
            var jitter = Math.Min(1e-4, .49 * width);
            var left = Ray(start + jitter);
            var right = Ray(end - jitter);
            if (left is { } l && right is { } r)
            {
                if (Vector2.DistanceSquared(l.Point, r.Point) <= 1e-12f)
                    points.Add(l.Distance >= r.Distance ? l : r);
                else
                {
                    points.Add(l);
                    points.Add(r);
                }
            }
            else if (left is { } lOnly) points.Add(lOnly);
            else if (right is { } rOnly) points.Add(rOnly);
            else if (Ray(start + .5 * width) is { } mid) points.Add(mid);
        }
        if (points.Count > 2)
        {
            var merged = new List<(Vector2 Point, float Distance)> { points[0] };
            foreach (var p in points.Skip(1))
            {
                if (Vector2.DistanceSquared(merged[^1].Point, p.Point) <= 1e-12f)
                {
                    if (p.Distance >= merged[^1].Distance) merged[^1] = p;
                }
                else merged.Add(p);
            }
            points = merged;
        }
        if (points.Count > 0 && Vector2.DistanceSquared(points[0].Point, points[^1].Point) <= 1e-12f)
            points[^1] = points[0].Distance >= points[^1].Distance ? points[0] : points[^1];
        var vertices = new List<Vector2>();
        for (var i = 0; i < points.Count; ++i)
        {
            var a = points[(i + points.Count - 1) % points.Count].Point;
            var b = points[i].Point;
            var c = points[(i + 1) % points.Count].Point;
            var ab = b - a;
            var bc = c - b;
            var scale = MathF.Abs(ab.X) + MathF.Abs(ab.Y) + MathF.Abs(bc.X) + MathF.Abs(bc.Y);
            if (MathF.Abs(Cross(ab, bc)) > 1e-6f * scale)
            {
                // AOEShapeCustom normalizes via PolygonClipper's 2^20 integer coordinates.
                var snapped = new Vector2(Snap(b.X), Snap(b.Y));
                if (vertices.Count == 0 || vertices[^1] != snapped) vertices.Add(snapped);
            }
        }
        if (vertices.Count > 1 && vertices[0] == vertices[^1]) vertices.RemoveAt(vertices.Count - 1);
        if (vertices.Count < 3) return new PolygonRegionGeometry(center, [], [], []);
        // Visibility can self-intersect when the caster is outside the blocker domain.
        // Reuse the vendored third-party clipper (no BossMod models/bridge outputs) with
        // the same integer scale and NonZero normalization as the source AOEShapeCustom.
        var clipper = new Clipper64 { PreserveCollinear = false };
        clipper.AddSubject(new Paths64 { new(vertices.Select(p => new Point64(p.X * 1048576f, p.Y * 1048576f))) });
        var tree = new PolyTree64();
        if (!clipper.Execute(ClipType.Union, FillRule.NonZero, tree))
            throw new InvalidOperationException("Line-of-sight visibility normalization failed.");
        var exteriors = new List<Vector2[]>();
        var holes = new List<Vector2[]>();
        var triangles = new List<Vector2[]>();
        AddParts(tree);
        return new PolygonRegionGeometry(center, exteriors.ToArray(), holes.ToArray(), triangles.ToArray());

        void AddParts(PolyPath64 parent)
        {
            for (var i = 0; i < parent.Count; ++i)
            {
                var child = parent[i];
                var polygon = child.Polygon!.Select(p => new Vector2(p.X / 1048576f, p.Y / 1048576f)).ToArray();
                if (child.IsHole)
                    holes.Add(polygon);
                else
                {
                    exteriors.Add(polygon);
                    if (child.Count != 0)
                        throw new InvalidOperationException("Line-of-sight visibility unexpectedly contains holes.");
                    var localPoints = polygon.Select(p => new Vector3(p.X, 0, p.Y)).ToArray();
                    if (!PolygonTriangulator.TryTriangulate(localPoints, out var indices, 1e-10f))
                        throw new InvalidOperationException("Normalized line-of-sight polygon could not be triangulated.");
                    triangles.AddRange(indices.Chunk(3).Select(t => t.Select(j => polygon[j]).ToArray()));
                }
                AddParts(child);
            }
        }

        (Vector2 Point, float Distance)? Ray(double angle)
        {
            var (sin, cos) = Math.SinCos(angle);
            var direction = new Vector2((float)cos, (float)sin);
            var distance = Raycast(localOrigin, direction, contours);
            return distance == float.MaxValue ? null : (localOrigin + distance * direction, distance);
        }
    }

    private static float Raycast(Vector2 origin, Vector2 direction, Vector2[][] contours)
    {
        var best = float.MaxValue;
        var minimum = new Vector2(float.MaxValue);
        var maximum = new Vector2(float.MinValue);
        foreach (var point in contours.SelectMany(c => c))
        {
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }
        var tMin = float.MinValue;
        var tMax = float.MaxValue;
        if (!Slab(origin.X, direction.X, minimum.X, maximum.X)
            || !Slab(origin.Y, direction.Y, minimum.Y, maximum.Y)
            || tMax < 0 || tMin > tMax) return best;
        tMin = MathF.Max(0, tMin);
        var horizontal = MathF.Abs(direction.Y) <= 1e-7f;
        foreach (var contour in contours)
            for (var i = 0; i < contour.Length; ++i)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Length];
                if (a.Y > b.Y) (a, b) = (b, a);
                var edge = b - a;
                if (horizontal)
                {
                    float t;
                    if (edge.Y != 0)
                    {
                        if (origin.Y < a.Y - 1e-7f || origin.Y >= b.Y - 1e-7f) continue;
                        var slope = edge.X / edge.Y;
                        var x = slope * origin.Y + (a.X - slope * a.Y);
                        if (x < MathF.Min(a.X, b.X) - 1e-7f || x > MathF.Max(a.X, b.X) + 1e-7f) continue;
                        t = (x - origin.X) / direction.X;
                    }
                    else
                    {
                        if (MathF.Abs(a.Y - origin.Y) > 1e-7f) continue;
                        var x = direction.X > 0
                            ? MathF.Max(origin.X, MathF.Min(a.X, b.X))
                            : MathF.Min(origin.X, MathF.Max(a.X, b.X));
                        if (x < MathF.Min(a.X, b.X) || x > MathF.Max(a.X, b.X)) continue;
                        t = (x - origin.X) / direction.X;
                    }
                    if (t >= tMin && t <= tMax && t < best) best = t;
                    continue;
                }
                // Match the source index's canonical non-horizontal edge representation.
                if (edge.Y != 0) edge.X = (edge.X / edge.Y) * edge.Y;
                var offset = a - origin;
                var denominator = Cross(direction, edge);
                var absDen = MathF.Abs(denominator);
                if (absDen > 1e-9f)
                {
                    var t = Cross(offset, edge);
                    var u = Cross(offset, direction);
                    if (denominator < 0) { t = -t; u = -u; }
                    if (t >= tMin * absDen && t <= MathF.Min(best, tMax) * absDen && u >= 0 && u < (1 - 1e-6f) * absDen)
                        best = t / absDen;
                }
                else if (MathF.Abs(Cross(offset, direction)) <= 1e-7f)
                {
                    var inverse = 1 / (direction.LengthSquared() + 1e-20f);
                    var tA = Vector2.Dot(offset, direction) * inverse;
                    var tB = Vector2.Dot(b - origin, direction) * inverse;
                    if (tA > tB) (tA, tB) = (tB, tA);
                    var t = MathF.Max(tA, tMin);
                    if (t <= tB && t <= tMax && t < best) best = t;
                }
            }
        return best;

        bool Slab(float value, float delta, float min, float max)
        {
            if (MathF.Abs(delta) <= 1e-7f) return value >= min - 1e-7f && value <= max + 1e-7f;
            var inverse = 1 / delta;
            var a = (min - value) * inverse;
            var b = (max - value) * inverse;
            if (a > b) (a, b) = (b, a);
            tMin = MathF.Max(tMin, a);
            tMax = MathF.Min(tMax, b);
            return true;
        }
    }

    private static double AngleDifference(double a, double b) => b >= a ? b - a : b - a + Math.Tau;
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static float Snap(float value) => (float)(Math.Round(value * 1048576d, MidpointRounding.AwayFromZero) / 1048576d);
}

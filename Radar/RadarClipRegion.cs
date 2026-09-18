using System.Numerics;
using PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Radar;

/// <summary>A simple arena outline intersected with the viewport, split into convex pieces for clipping.</summary>
internal sealed class RadarClipRegion
{
    private const float Epsilon = 0.0001f;
    private readonly IReadOnlyList<Vector2>[] _pieces;
    public IReadOnlyList<Vector2> Outline { get; }

    public RadarClipRegion(IReadOnlyList<Vector2> outline, Vector2 center, float halfSize)
    {
        Outline = outline.ToArray();
        Vector2[] viewport = [center + new Vector2(-halfSize, -halfSize), center + new Vector2(halfSize, -halfSize),
            center + new Vector2(halfSize, halfSize), center + new Vector2(-halfSize, halfSize)];
        _pieces = ConvexParts(Outline).Select(p => ClipConvex(p, viewport))
            .Where(p => p.Count >= 3 && MathF.Abs(SignedArea(p)) > Epsilon).ToArray();
    }

    public static Vector2[] Circle(Vector2 center, float radius, int segments = 64)
        => Enumerable.Range(0, Math.Max(12, segments)).Select(i => center + new Vector2(
            MathF.Cos(i * MathF.Tau / Math.Max(12, segments)), MathF.Sin(i * MathF.Tau / Math.Max(12, segments))) * radius).ToArray();

    public bool Contains(Vector2 point) => _pieces.Any(p => ContainsConvex(p, point));

    public IEnumerable<IReadOnlyList<Vector2>> ClipPolygon(IReadOnlyList<Vector2> polygon)
    {
        // A concave intersection can have disconnected islands. Split both operands before clipping.
        foreach (var input in ConvexParts(polygon))
            foreach (var piece in _pieces)
            {
                var clipped = ClipConvex(input, piece);
                if (clipped.Count >= 3 && MathF.Abs(SignedArea(clipped)) > Epsilon) yield return clipped;
            }
    }

    public IEnumerable<(Vector2 Start, Vector2 End)> ClipSegment(Vector2 start, Vector2 end)
    {
        var intervals = new List<(float Start, float End)>();
        foreach (var piece in _pieces)
        {
            var from = 0f; var to = 1f;
            var winding = MathF.Sign(SignedArea(piece));
            for (var i = 0; i < piece.Count && from <= to; ++i)
            {
                var a = piece[i]; var edge = piece[(i + 1) % piece.Count] - a;
                var v0 = winding * Cross(edge, start - a); var v1 = winding * Cross(edge, end - a);
                if (v0 < -Epsilon && v1 < -Epsilon) { from = 1; to = 0; break; }
                if (v0 < 0 && v1 >= 0) from = Math.Max(from, v0 / (v0 - v1));
                else if (v1 < 0 && v0 >= 0) to = Math.Min(to, v0 / (v0 - v1));
            }
            if (from <= to) intervals.Add((from, to));
        }
        intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
        // Merge triangle seams without bridging the gaps of a concave arena.
        for (var i = 0; i < intervals.Count; ++i)
        {
            var interval = intervals[i];
            while (i + 1 < intervals.Count && intervals[i + 1].Start <= interval.End + Epsilon)
                interval.End = Math.Max(interval.End, intervals[++i].End);
            yield return (Vector2.Lerp(start, end, interval.Start), Vector2.Lerp(start, end, interval.End));
        }
    }

    private static IEnumerable<IReadOnlyList<Vector2>> ConvexParts(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count < 3) yield break;
        var winding = MathF.Sign(SignedArea(polygon));
        if (winding == 0) yield break;
        var convex = true;
        for (var i = 0; i < polygon.Count; ++i)
            convex &= winding * Cross(polygon[(i + 1) % polygon.Count] - polygon[i],
                polygon[(i + 2) % polygon.Count] - polygon[(i + 1) % polygon.Count]) >= -Epsilon;
        if (convex) { yield return polygon; yield break; }
        var points = polygon.Select(p => new Vector3(p.X, 0, p.Y)).ToArray();
        if (!PolygonTriangulator.TryTriangulate(points, out var indices)) yield break;
        for (var i = 0; i < indices.Length; i += 3)
            yield return new[] { polygon[indices[i]], polygon[indices[i + 1]], polygon[indices[i + 2]] };
    }

    private static IReadOnlyList<Vector2> ClipConvex(IReadOnlyList<Vector2> input, IReadOnlyList<Vector2> clip)
    {
        var source = new List<Vector2>(input); var target = new List<Vector2>();
        var winding = MathF.Sign(SignedArea(clip));
        for (var edge = 0; edge < clip.Count && source.Count > 0; ++edge)
        {
            var a = clip[edge]; var delta = clip[(edge + 1) % clip.Count] - a;
            target.Clear();
            for (var i = 0; i < source.Count; ++i)
            {
                var current = source[i]; var next = source[(i + 1) % source.Count];
                var v0 = winding * Cross(delta, current - a); var v1 = winding * Cross(delta, next - a);
                if ((v0 >= 0) != (v1 >= 0)) target.Add(Vector2.Lerp(current, next, v0 / (v0 - v1)));
                if (v1 >= 0) target.Add(next);
            }
            (source, target) = (target, source);
        }
        return source;
    }

    private static bool ContainsConvex(IReadOnlyList<Vector2> polygon, Vector2 point)
    {
        var area = SignedArea(polygon);
        if (MathF.Abs(area) <= Epsilon) return false;
        var winding = MathF.Sign(area);
        for (var i = 0; i < polygon.Count; ++i)
            if (winding * Cross(polygon[(i + 1) % polygon.Count] - polygon[i], point - polygon[i]) < -Epsilon) return false;
        return true;
    }
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count < 3) return 0;
        var area = 0f;
        // Translate before summing: screen offsets otherwise cancel the area of thin triangles.
        var origin = polygon[0];
        for (var i = 1; i + 1 < polygon.Count; ++i) area += Cross(polygon[i] - origin, polygon[i + 1] - origin);
        return area * 0.5f;
    }
}

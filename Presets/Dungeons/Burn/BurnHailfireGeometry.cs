using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

// A moving beam with the locked crystal shadows subtracted. Convex pieces
// supply the same exact cutouts to radar and host world drawing.
internal sealed class BurnHailfireGeometry(IGeometryShape beam, IGeometryShape[] shadows) : GeometryShape(beam.Position)
{
    private IReadOnlyList<IReadOnlyList<Vector3>>? contours;
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override GeometryBounds GetBounds() => beam.GetBounds();
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => beam.Contains(point, epsilon) && !shadows.Any(s => s.Contains(point, epsilon));
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        if (contours != null) return contours;
        var pieces = beam.GetContours().Select(p => p.Select(v => new Vector2(v.X, v.Z)).ToList()).ToList();
        foreach (var shadow in shadows)
            foreach (var contour in shadow.GetContours())
            {
                var cutter = contour.Select(v => new Vector2(v.X, v.Z)).ToArray();
                var area = 0f;
                for (var i = 0; i < cutter.Length; ++i) area += Cross(cutter[i], cutter[(i + 1) % cutter.Length]);
                if (area < 0) Array.Reverse(cutter);
                pieces = pieces.SelectMany(p => Subtract(p, cutter)).ToList();
            }
        return contours = pieces.Where(p => p.Count >= 3)
            .Select(p => (IReadOnlyList<Vector3>)p.Select(v => new Vector3(v.X, Position.Y, v.Y)).ToArray()).ToArray();
    }

    private static IEnumerable<List<Vector2>> Subtract(List<Vector2> subject, Vector2[] cutter)
    {
        var remaining = subject;
        for (var i = 0; i < cutter.Length && remaining.Count >= 3; ++i)
        {
            var a = cutter[i]; var b = cutter[(i + 1) % cutter.Length];
            var outside = Clip(remaining, a, b, false);
            if (outside.Count >= 3) yield return outside;
            remaining = Clip(remaining, a, b, true);
        }
    }

    private static List<Vector2> Clip(List<Vector2> polygon, Vector2 a, Vector2 b, bool inside)
    {
        List<Vector2> result = [];
        if (polygon.Count == 0) return result;
        var previous = polygon[^1]; var prevDistance = Cross(b - a, previous - a);
        foreach (var current in polygon)
        {
            var distance = Cross(b - a, current - a);
            var prevInside = inside ? prevDistance >= 0 : prevDistance <= 0;
            var curInside = inside ? distance >= 0 : distance <= 0;
            if (prevInside != curInside)
                result.Add(Vector2.Lerp(previous, current, prevDistance / (prevDistance - distance)));
            if (curInside) result.Add(current);
            previous = current; prevDistance = distance;
        }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}

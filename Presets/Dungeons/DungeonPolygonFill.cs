using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DungeonPolygonFill
{
    // Between consecutive vertex X coordinates no boundary edges cross.
    // Pair scanline intersections to retain holes and disconnected islands.
    internal static Vector2[][] Create(Vector2[][] contours)
    {
        var edges = contours.SelectMany(p => p.Select((a, i) => (A: a, B: p[(i + 1) % p.Length])))
            .Where(e => e.A.X != e.B.X).ToArray();
        var xs = contours.SelectMany(p => p).Select(p => p.X).Distinct().Order().ToArray();
        var triangles = new List<Vector2[]>();
        for (var i = 1; i < xs.Length; ++i)
        {
            var left = xs[i - 1];
            var right = xs[i];
            var middle = ((double)left + right) * .5;
            var active = edges.Where(e => Math.Min(e.A.X, e.B.X) < middle && middle < Math.Max(e.A.X, e.B.X))
                .OrderBy(e => Height(e, middle)).ToArray();
            if ((active.Length & 1) != 0)
                throw new InvalidOperationException("Polygon fill has an unmatched boundary edge.");
            for (var j = 0; j < active.Length; j += 2)
            {
                var a = new Vector2(left, (float)Height(active[j], left));
                var b = new Vector2(right, (float)Height(active[j], right));
                var c = new Vector2(right, (float)Height(active[j + 1], right));
                var d = new Vector2(left, (float)Height(active[j + 1], left));
                Add(a, b, c);
                Add(a, c, d);
            }
        }
        return triangles.ToArray();

        static double Height((Vector2 A, Vector2 B) edge, double x) => edge.A.Y
            + (x - edge.A.X) / ((double)edge.B.X - edge.A.X) * ((double)edge.B.Y - edge.A.Y);
        void Add(Vector2 a, Vector2 b, Vector2 c)
        {
            var area = ((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X);
            if (area != 0) triangles.Add([a, b, c]);
        }
    }
}

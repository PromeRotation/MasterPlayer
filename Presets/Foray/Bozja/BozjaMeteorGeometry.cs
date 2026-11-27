using System.Numerics;
using System.Runtime.Intrinsics.X86;
using Clipper2Lib;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

// Freeze source Custom-AOE polygons at Start. Analytic shadows differ at the
// tessellated arcs, and would change the existing bridge's finite sampler.
internal sealed class BozjaMeteorGeometry
{
    private const float SCALE = 1048576f;
    private readonly Vector3 center;
    private readonly Vector2[][] exteriors;
    private readonly Vector2[][] contours;
    private readonly Vector2[][] displayFill;
    private readonly Vector2 minimum;
    private readonly Vector2 maximum;
    private readonly float inverseCellHeight;
    private readonly Vector2[] rowRanges;
    private readonly List<(Vector2 A, Vector2 B)>[] rowEdges;
    private readonly bool sourceIndexedDistance;

    internal BozjaMeteorGeometry(Vector3 center, Vector3 origin, IReadOnlyList<BozjaActor> blockers,
        float range = 30, bool excludeHitboxes = true, bool sourceIndexedDistance = false)
    {
        this.center = center;
        this.sourceIndexedDistance = sourceIndexedDistance;
        var shadows = new Paths64();
        var circles = new Paths64();
        foreach (var blocker in blockers)
        {
            var p = blocker.Actor.Position;
            var delta = new Vector2(p.X - origin.X, p.Z - origin.Z);
            var distance = delta.Length();
            var radius = blocker.Actor.HitboxRadius;
            var half = radius < distance ? (float)Math.Asin(radius / distance) : 90f * (MathF.PI / 180f);
            var direction = MathF.Atan2(delta.X, delta.Y);
            var offset = new Vector2(origin.X - center.X, origin.Z - center.Z);
            shadows.Add(Path(Arc(range, direction - half, direction + half)
                .Concat(Arc(distance + .2f, direction + half, direction - half)), offset));
            if (!excludeHitboxes) continue;
            var n = Segments(radius, 360f * (MathF.PI / 180f));
            circles.Add(Path(Enumerable.Range(0, n).Select(i => Polar(radius, i * (2 * MathF.PI / n))),
                new(p.X - center.X, p.Z - center.Z)));
        }
        // AOEShapeCustom normalizes the union, converts through float, subtracts
        // all hitboxes and normalizes the result once more.
        var union = Apply(ClipType.Union, shadows, []);
        var difference = Apply(ClipType.Difference, union, circles);
        var clipper = new Clipper64 { PreserveCollinear = false };
        clipper.AddSubject(difference);
        var tree = new PolyTree64();
        if (!clipper.Execute(ClipType.Union, FillRule.NonZero, tree))
            throw new InvalidOperationException("Meteor visibility union failed.");
        var outside = new List<Vector2[]>();
        var all = new List<Vector2[]>();
        Collect(tree);
        exteriors = outside.ToArray();
        contours = all.ToArray();
        // The existing DrawConvert consumes each exterior, omitting holes.
        // Keep that display contract separate from exact model containment.
        displayFill = exteriors.SelectMany(p => DungeonPolygonFill.Create([p])).ToArray();
        minimum = new(float.MaxValue);
        maximum = new(float.MinValue);
        foreach (var point in exteriors.SelectMany(p => p))
        {
            minimum = Vector2.Min(minimum, point);
            maximum = Vector2.Max(maximum, point);
        }
        var count = contours.Where(p => p.Length >= 2).Sum(p => p.Length);
        var rows = Math.Clamp((int)MathF.Round(MathF.Sqrt(Math.Max(count, 1)) * .9f) + 8, 32, 512);
        var cellHeight = Math.Max(maximum.Y - minimum.Y, 1e-7f) / rows;
        inverseCellHeight = 1f / cellHeight;
        rowRanges = Enumerable.Repeat(new Vector2(float.PositiveInfinity, float.NegativeInfinity), rows).ToArray();
        rowEdges = Enumerable.Range(0, rows).Select(_ => new List<(Vector2, Vector2)>()).ToArray();
        // Source row X hulls use a different rounding order from the FMA
        // crossing test. Keep both: the hull can reject a boundary point first.
        foreach (var contour in contours.Where(p => p.Length >= 2))
            for (var i = 0; i < contour.Length; ++i)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Length];
                if (MathF.Abs(a.Y - b.Y) <= 1e-7f)
                {
                    var row = Row(a.Y);
                    rowEdges[row].Add((a, b));
                    rowRanges[row] = new(MathF.Min(rowRanges[row].X, MathF.Min(a.X, b.X)),
                        MathF.Max(rowRanges[row].Y, MathF.Max(a.X, b.X)));
                    continue;
                }
                if (a.Y > b.Y) (a, b) = (b, a);
                var first = Math.Max(0, (int)((a.Y - minimum.Y) * inverseCellHeight));
                var last = Math.Min(rows - 1, (int)((MathF.BitDecrement(b.Y) - minimum.Y) * inverseCellHeight));
                var slope = (b.X - a.X) / (b.Y - a.Y);
                var rowMinimum = minimum.Y + first * cellHeight;
                for (var row = first; row <= last; ++row, rowMinimum += cellHeight)
                {
                    rowEdges[row].Add((a, b));
                    var start = Math.Max(a.Y, rowMinimum);
                    var end = MathF.BitDecrement(Math.Min(b.Y, rowMinimum + cellHeight));
                    var xStart = a.X + slope * (start - a.Y);
                    var xEnd = a.X + slope * (end - a.Y);
                    rowRanges[row] = new(Math.Min(rowRanges[row].X, Math.Min(xStart, xEnd)),
                        Math.Max(rowRanges[row].Y, Math.Max(xStart, xEnd)));
                }
            }

        void Collect(PolyPath64 parent)
        {
            for (var i = 0; i < parent.Count; ++i)
            {
                var child = parent[i];
                var points = child.Polygon!.Select(p => new Vector2(p.X / SCALE, p.Y / SCALE)).ToArray();
                all.Add(points);
                if (!child.IsHole) outside.Add(points);
                Collect(child);
            }
        }
    }

    internal bool HasDisplay => exteriors.Length > 0;

    internal IGeometryShape Display(float y)
        => new PolygonRegionGeometry(center with { Y = y }, exteriors, [], displayFill);

    internal IReadOnlyList<IReadOnlyList<Vector3>> NavigationContours(float y)
        => contours.Select(contour => (IReadOnlyList<Vector3>)contour
            .Select(p => new Vector3(center.X + p.X, y, center.Z + p.Y)).ToArray()).ToArray();

    private int Row(float y) => Math.Clamp((int)((y - minimum.Y) * inverseCellHeight), 0, rowRanges.Length - 1);

    internal bool Contains(Vector3 point)
    {
        var px = point.X - center.X;
        var py = point.Z - center.Z;
        var parity = false;
        const float EPSILON = 1e-7f;
        if (px < minimum.X - EPSILON || px > maximum.X + EPSILON
            || py < minimum.Y - EPSILON || py > maximum.Y + EPSILON) return false;
        var row = Row(py);
        if (px < rowRanges[row].X - EPSILON || px > rowRanges[row].Y + EPSILON) return false;
        var vectorized = Avx512F.IsSupported || Avx2.IsSupported;
        var fused = Avx512F.IsSupported || Avx2.IsSupported && Fma.IsSupported;
        foreach (var edge in rowEdges[row])
        {
            var a = edge.A;
            var b = edge.B;
            if (MathF.Abs(a.Y - b.Y) <= EPSILON)
            {
                if (MathF.Abs(py - a.Y) <= EPSILON && px >= MathF.Min(a.X, b.X) - EPSILON
                    && px <= MathF.Max(a.X, b.X) + EPSILON) return true;
                continue;
            }
            if (vectorized ? py < a.Y || py >= b.Y : py < a.Y - EPSILON || py >= b.Y - EPSILON) continue;
            var slope = (b.X - a.X) / (b.Y - a.Y);
            var intercept = a.X - slope * a.Y;
            var x = fused ? MathF.FusedMultiplyAdd(slope, py, intercept) : slope * py + intercept;
            if (MathF.Abs(px - x) <= EPSILON && px >= MathF.Min(a.X, b.X) - EPSILON
                && px <= MathF.Max(a.X, b.X) + EPSILON) return true;
            if (x > px) parity = !parity;
        }
        return parity;
    }

    internal float ForbiddenDistance(float x, float z)
    {
        if (!Contains(new(x, 0, z))) return 0;
        if (sourceIndexedDistance) return IndexedDistance(x, z);
        var best = float.MaxValue;
        foreach (var contour in contours)
            for (var i = 0; i < contour.Length; ++i)
            {
                var a = contour[i];
                var b = contour[(i + 1) % contour.Length];
                var ax = center.X + a.X;
                var az = center.Z + a.Y;
                var dx = b.X - a.X;
                var dz = b.Y - a.Y;
                var inverse = dx * dx + dz * dz > 0 ? 1 / (dx * dx + dz * dz) : 0;
                var t = Math.Clamp(((x - ax) * dx + (z - az) * dz) * inverse, 0, 1);
                var ex = x - (ax + t * dx);
                var ez = z - (az + t * dz);
                best = MathF.Min(best, ex * ex + ez * ez);
            }
        return MathF.Sqrt(best);
    }

    private float IndexedDistance(float x, float z)
    {
        // The source SDF queries only edges whose five-unit cells contain the
        // point. Preserve that finite candidate set, including empty cells.
        var cellX = (int)MathF.Floor(x * .2f);
        var cellZ = (int)MathF.Floor(z * .2f);
        var best = float.MaxValue;
        foreach (var contour in contours)
            for (var i = 0; i < contour.Length; ++i)
            {
                var a = contour[(i + contour.Length - 1) % contour.Length];
                var b = contour[i];
                var ax = center.X + a.X;
                var az = center.Z + a.Y;
                var dx = b.X - a.X;
                var dz = b.Y - a.Y;
                var bx = ax + dx;
                var bz = az + dz;
                if (cellX < (int)MathF.Floor(Math.Min(ax, bx) * .2f)
                    || cellX > (int)MathF.Floor(Math.Max(ax, bx) * .2f)
                    || cellZ < (int)MathF.Floor(Math.Min(az, bz) * .2f)
                    || cellZ > (int)MathF.Floor(Math.Max(az, bz) * .2f)) continue;
                var inverse = 1f / (dx * dx + dz * dz + 1e-8f);
                var t = Math.Clamp(((x - ax) * dx + (z - az) * dz) * inverse, 0f, 1f);
                var ex = x - (ax + t * dx);
                var ez = z - (az + t * dz);
                best = Math.Min(best, ex * ex + ez * ez);
            }
        return MathF.Sqrt(best);
    }

    private static int Segments(float radius, float angle)
    {
        var step = 2 * MathF.Acos(1 - Math.Min(.05f / radius, 1));
        var count = (int)MathF.Ceiling(MathF.Abs(angle) / step);
        return Math.Clamp((count + 1) & ~1, 4, 512);
    }

    private static IEnumerable<Vector2> Arc(float radius, float start, float end)
    {
        var count = Segments(radius, end - start);
        var increment = (end - start) / count;
        for (var i = 0; i <= count; ++i) yield return Polar(radius, start + i * increment);
    }

    private static Vector2 Polar(float radius, float angle)
    {
        var (sin, cos) = Math.SinCos(angle);
        return new(radius * (float)sin, radius * (float)cos);
    }

    private static Path64 Path(IEnumerable<Vector2> vertices, Vector2 offset)
        => new(vertices.Select(p => new Point64((p.X + offset.X) * SCALE, (p.Y + offset.Y) * SCALE)));

    private static Paths64 Apply(ClipType operation, Paths64 subject, Paths64 clip)
    {
        var clipper = new Clipper64 { PreserveCollinear = false };
        clipper.AddSubject(subject);
        clipper.AddClip(clip);
        var result = new Paths64();
        if (!clipper.Execute(operation, FillRule.NonZero, result))
            throw new InvalidOperationException("Meteor visibility polygon operation failed.");
        return new(result.Select(p => new Path64(p.Select(v =>
            new Point64((v.X / SCALE) * SCALE, (v.Y / SCALE) * SCALE)))));
    }
}

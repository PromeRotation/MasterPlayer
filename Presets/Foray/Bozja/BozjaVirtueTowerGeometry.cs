using System.Numerics;
using Clipper2Lib;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

// A tower join forbids the complement of the allowed circles. The legacy
// sampler rejects that mostly-filled region, so keep the source SDF directly.
// The finite exterior only bounds rasterization; the arena remains separate.
internal sealed class BozjaVirtueTowerGeometry(BozjaZone zone, Vector3 center, float radius) : GeometryShape(center)
{
    private const double SCALE = 1048576;
    private readonly CircleGeometry exterior = new(center, radius + 1);
    private IReadOnlyList<IReadOnlyList<Vector3>>? contours;
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => exterior.Contains(point, epsilon) && zone.Distance(point.X, point.Z) <= epsilon;

    public override GeometryBounds GetBounds() => exterior.GetBounds();

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (Contains(start, epsilon) || Contains(end, epsilon)) return true;
        var dx = (double)end.X - start.X;
        var dz = (double)end.Z - start.Z;
        var length = dx * dx + dz * dz;
        if (length == 0) return false;
        var cuts = new List<double> { 0, 1 };
        AddIntersections(Position, radius + 1 + epsilon);
        foreach (var p in zone.Join ?? []) AddIntersections(p, Math.Max(0, zone.JoinRadius - epsilon));
        cuts.Sort();
        for (var i = 1; i < cuts.Count; ++i)
        {
            if (At(cuts[i]) || At((cuts[i - 1] + cuts[i]) * .5)) return true;
        }
        return false;

        bool At(double t) => Contains(new((float)(start.X + dx * t), start.Y, (float)(start.Z + dz * t)), epsilon);
        void AddIntersections(Vector3 p, double r)
        {
            var x = (double)start.X - p.X;
            var z = (double)start.Z - p.Z;
            var b = x * dx + z * dz;
            var discriminant = b * b - length * (x * x + z * z - r * r);
            if (discriminant < 0) return;
            var root = Math.Sqrt(discriminant);
            Add((-b - root) / length);
            Add((-b + root) / length);
        }
        void Add(double t)
        {
            if (t > 0 && t < 1) cuts.Add(t);
        }
    }

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
        => segments == 64 ? contours ??= BuildContours(segments) : BuildContours(segments);

    private IReadOnlyList<IReadOnlyList<Vector3>> BuildContours(int segments)
    {
        // Subtract the union, so overlapping safe circles have no internal
        // boundary vertices for the rasterizer to mistake for dangerous cells.
        var clipper = new Clipper64();
        clipper.AddSubject([Circle(Position, radius + 1)]);
        clipper.AddClip(new Paths64((zone.Join ?? []).Distinct().Select(p => Circle(p, zone.JoinRadius))));
        var result = new Paths64();
        if (!clipper.Execute(ClipType.Difference, FillRule.NonZero, result))
            throw new InvalidOperationException("Virtue tower complement contours failed.");
        return result.Select(path => (IReadOnlyList<Vector3>)path.Select(p =>
            new Vector3(Position.X + (float)(p.X / SCALE), Position.Y, Position.Z + (float)(p.Y / SCALE))).ToArray()).ToArray();

        Path64 Circle(Vector3 p, float r) => new(new CircleGeometry(p, r).GetContours(segments).Single()
            .Select(v => new Point64((v.X - Position.X) * SCALE, (v.Z - Position.Z) * SCALE)));
    }
}

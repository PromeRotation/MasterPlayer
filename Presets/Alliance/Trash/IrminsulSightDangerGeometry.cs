using System.Numerics;
using Clipper2Lib;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

// The sampled complement exceeds the legacy fill threshold. Keep the source
// SDF for navigation; the outer circle only bounds rasterization to this arena.
internal sealed class IrminsulSightDangerGeometry(BozjaMeteorGeometry sight, Vector3 center, float radius)
    : GeometryShape(center)
{
    private const double SCALE = 1048576;
    private readonly CircleGeometry exterior = new(center, radius + 1);
    private IReadOnlyList<IReadOnlyList<Vector3>>? contours;
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => exterior.Contains(point, epsilon) && sight.ForbiddenDistance(point.X, point.Z) <= epsilon;

    public override GeometryBounds GetBounds() => exterior.GetBounds();

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
        => segments == 64 ? contours ??= BuildContours(segments) : BuildContours(segments);

    private IReadOnlyList<IReadOnlyList<Vector3>> BuildContours(int segments)
    {
        var clipper = new Clipper64();
        clipper.AddSubject(new Paths64(exterior.GetContours(segments).Select(Path)));
        clipper.AddClip(new Paths64(sight.NavigationContours(Position.Y).Select(Path)));
        var result = new Paths64();
        if (!clipper.Execute(ClipType.Difference, FillRule.NonZero, result))
            throw new InvalidOperationException("Irminsul sight complement contours failed.");
        return result.Select(path => (IReadOnlyList<Vector3>)path.Select(p =>
            new Vector3(Position.X + (float)(p.X / SCALE), Position.Y, Position.Z + (float)(p.Y / SCALE))).ToArray()).ToArray();

        Path64 Path(IReadOnlyList<Vector3> points) => new(points.Select(p =>
            new Point64((p.X - Position.X) * SCALE, (p.Z - Position.Z) * SCALE)));
    }
}

using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class PtCompositeGeometry : GeometryShape
{
    private readonly IGeometryShape[] include;
    private readonly IGeometryShape[] exclude;
    private readonly IReadOnlyList<IReadOnlyList<Vector3>> contours;
    public PtCompositeGeometry(IGeometryShape[] include, IGeometryShape[]? exclude = null) : base(include[0].Position)
    {
        this.include = include;
        this.exclude = exclude ?? [];
        contours = include.Concat(this.exclude).SelectMany(s => s.GetContours()).ToArray();
    }
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => include.Any(s => s.Contains(point, epsilon)) && !exclude.Any(s => s.Contains(point, -epsilon));
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => contours;
}

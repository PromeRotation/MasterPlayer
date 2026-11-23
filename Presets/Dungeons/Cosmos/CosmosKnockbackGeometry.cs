using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CosmosKnockbackGeometry(CosmosKnockbackTemplate template) : GeometryShape(template.Center)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => template.Dangers.Any(d => d.Contains(template.Landing(point), epsilon));
    public override GeometryBounds GetBounds() => new RectangleGeometry(Position, 0, 39, 39).GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}

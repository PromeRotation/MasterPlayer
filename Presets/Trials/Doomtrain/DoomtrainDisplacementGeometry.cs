using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class DoomtrainDisplacementGeometry(DoomtrainDisplacementTemplate template) : GeometryShape(template.Arena.Position)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => template.Unsafe(point);
    public override GeometryBounds GetBounds() => template.Arena.GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}

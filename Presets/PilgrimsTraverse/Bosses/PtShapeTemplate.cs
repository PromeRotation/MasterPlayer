using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed record PtShapeTemplate(IGeometryShape Shape) : GeometryTemplate
{
    public override GeometryKind Kind => Shape.Kind;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => Shape;
}

using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaMeteorTemplate(BozjaMeteorGeometry Geometry) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => Geometry.Display(position.Y);
}

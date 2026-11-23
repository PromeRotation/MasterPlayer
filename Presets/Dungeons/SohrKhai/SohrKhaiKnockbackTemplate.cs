using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record SohrKhaiKnockbackTemplate(IGeometryShape Arena, Vector3 Origin, float Direction, int Removed) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new SohrKhaiKnockbackGeometry(this);
}

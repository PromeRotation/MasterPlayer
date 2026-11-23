using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record XelphatolKnockbackTemplate(IGeometryShape Arena, Vector3 Origin, Vector3[] Whirlwinds, float Hitbox) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new XelphatolKnockbackGeometry(this);
}

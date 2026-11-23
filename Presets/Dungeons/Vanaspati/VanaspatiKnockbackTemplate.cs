using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record VanaspatiKnockbackTemplate(IGeometryShape Arena, Vector3 Origin, IGeometryShape[] Dangers, float Distance = 13) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new VanaspatiKnockbackGeometry(this);
    internal Vector3 Landing(Vector3 point)
    {
        var direction = point - Origin; direction.Y = 0;
        return direction.LengthSquared() > .000001f ? point + Vector3.Normalize(direction) * Distance : point;
    }
}

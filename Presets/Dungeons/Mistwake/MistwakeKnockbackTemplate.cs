using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record MistwakeKnockbackTemplate(Vector3 Center, bool AlongZ) : GeometryTemplate
{
    internal Vector3 Landing(Vector3 p)
        => AlongZ ? p + Vector3.UnitZ * (p.Z > Center.Z ? 12 : -12)
            : p + Vector3.UnitX * (p.X > Center.X ? 12 : -12);
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new MistwakeKnockbackGeometry(this);
}

using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// Source SDKnockbackInCircle returns 0/1, not a signed distance.
internal sealed record KefkaKnockbackTemplate(KefkaDisplacement Movement) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new KefkaKnockbackGeometry(Movement);
}

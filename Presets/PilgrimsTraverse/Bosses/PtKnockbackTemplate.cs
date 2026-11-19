using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed record PtKnockbackTemplate(IGeometryShape Arena, Vector3 Origin, float Distance,
    Vector3? FixedDirection = null, IGeometryShape[]? LandingDangers = null, IGeometryShape? LandingAllowed = null) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new PtKnockbackGeometry(this);
    public Vector3 Landing(Vector3 point)
    {
        var delta = FixedDirection ?? point - Origin;
        delta.Y = 0;
        return delta.LengthSquared() < .0001f ? point : point + Vector3.Normalize(delta) * Distance;
    }
}

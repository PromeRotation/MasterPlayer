using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CastrumKnockbackTemplate(IGeometryShape Arena, Vector3 Origin, float Distance,
    IGeometryShape[] LandingDangers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new CastrumKnockbackGeometry(this);

    internal Vector3 Landing(Vector3 point)
    {
        var delta = point - Origin;
        delta.Y = 0;
        if (delta.LengthSquared() < .0001f) return point;
        var destination = point + Vector3.Normalize(delta) * Distance;
        if (Arena.Contains(destination)) return destination;
        // Rahu Comet stops at the wall. The arena is convex, so a binary search
        // along the displacement finds the first wall without crossing a gap.
        var near = point;
        var far = destination;
        for (var i = 0; i < 16; ++i)
        {
            var middle = (near + far) * .5f;
            if (Arena.Contains(middle, 0)) near = middle;
            else far = middle;
        }
        return near;
    }
}

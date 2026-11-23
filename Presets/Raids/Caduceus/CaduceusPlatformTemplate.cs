using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CaduceusPlatformTemplate(int Mask, bool Inverted = false, float Inset = 0,
    float? ActorHeight = null) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new CaduceusPlatformGeometry(this, position.Y);
}

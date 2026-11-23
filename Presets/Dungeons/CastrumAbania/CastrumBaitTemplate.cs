using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CastrumBaitTemplate(IGeometryShape Arena, Vector3 Origin, Vector3[] Party, float Length = 20, float HalfWidth = 3) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new CastrumBaitGeometry(this);
}

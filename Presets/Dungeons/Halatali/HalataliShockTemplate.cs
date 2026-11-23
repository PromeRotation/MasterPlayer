using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record HalataliShockTemplate : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.LocalPolygon;

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => HalataliArenas.Shock(position.Y);
}

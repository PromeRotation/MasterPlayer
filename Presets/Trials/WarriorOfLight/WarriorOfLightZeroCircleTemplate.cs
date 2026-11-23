using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// 地火的首段确实为 R0；普通 CircleGeometry 要求正半径，单独保留此退化段。
internal sealed record WarriorOfLightZeroCircleTemplate : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.Circle;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new WarriorOfLightZeroCircleGeometry(position);
}

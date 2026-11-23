using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// 原始安全扇影与它的补集共用同一快照；规划不会将视线机制简化为整场圆形伤害。
internal sealed record DungeonLosTemplate(DungeonLosGeometry Geometry) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => Geometry;
}

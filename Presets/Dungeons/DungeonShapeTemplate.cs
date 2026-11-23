using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// 静态差集在导出时已经解析；双视图和解算共用该几何，不再次近似为基本形状。
internal sealed record DungeonShapeTemplate(IGeometryShape Shape) : GeometryTemplate
{
    public override GeometryKind Kind => Shape.Kind;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => Shape;
}

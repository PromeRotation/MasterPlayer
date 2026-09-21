using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>
/// Geometry 公共图形接口。
/// <para>该接口只负责纯几何判断，不管理危险区、安全区、绘制对象或生命周期。</para>
/// </summary>
public interface IGeometryShape
{
    /// <summary>几何图形类型。</summary>
    GeometryKind Kind { get; }

    /// <summary>几何图形的世界基准位置。具体含义由图形类型决定。</summary>
    Vector3 Position { get; }

    /// <summary>几何图形的世界朝向，使用 FFXIV Rotation 弧度。</summary>
    float Rotation { get; }

    /// <summary>判断世界坐标点是否位于图形内部或边界上。</summary>
    bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon);

    /// <summary>判断世界坐标线段在 X/Z 平面上是否与图形相交。</summary>
    bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon);

    /// <summary>获取图形在 X/Z 平面上的轴对齐边界盒。</summary>
    GeometryBounds GetBounds();

    /// <summary>获取图形轮廓点。多段轮廓用多个列表表示，例如月环的内外两圈。</summary>
    IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64);
}

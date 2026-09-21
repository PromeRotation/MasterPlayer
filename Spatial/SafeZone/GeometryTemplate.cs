using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 区域的几何模板。
/// <para>模板只保存形状参数（半径、角度、宽度等），实际世界位置和朝向在每帧由 <c>SafeZoneManager</c> 通过 <see cref="TransformBinding"/> 解析后注入 <see cref="Resolve"/>。</para>
/// <para>这样同一个模板可以多次实例化为不同位置的 <see cref="IGeometryShape"/>，模板自身保持不可变。</para>
/// </summary>
public abstract record GeometryTemplate
{
    /// <summary>模板对应的几何类型。</summary>
    public abstract GeometryKind Kind { get; }

    /// <summary>
    /// 用给定的世界位置和朝向把模板解析为可用于判断的 <see cref="IGeometryShape"/>。
    /// </summary>
    /// <param name="position">世界基准位置。具体语义由各 <see cref="GeometryKind"/> 决定。</param>
    /// <param name="rotation">世界基准朝向（弧度）。对忽略朝向的模板，该参数无效。</param>
    /// <param name="lengthOverride">
    /// 由 <see cref="BetweenTwoAnchors"/> 注入的长度覆盖值。
    /// 当前只有 <see cref="RectangleTemplate"/> 和 <see cref="RayRectangleTemplate"/> 会消费，其他模板忽略。
    /// </param>
    public abstract IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride);
}

/// <summary>圆形模板。Position 解析为圆心；rotation 和 lengthOverride 被忽略。</summary>
public sealed record CircleTemplate(float Radius) : GeometryTemplate
{
    /// <inheritdoc />
    public override GeometryKind Kind => GeometryKind.Circle;

    /// <inheritdoc />
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new CircleGeometry(position, Radius);
}

/// <summary>月环模板。Position 解析为圆心；rotation 和 lengthOverride 被忽略。</summary>
public sealed record DonutTemplate(float InnerRadius, float OuterRadius) : GeometryTemplate
{
    /// <inheritdoc />
    public override GeometryKind Kind => GeometryKind.Donut;

    /// <inheritdoc />
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new DonutGeometry(position, InnerRadius, OuterRadius);
}

/// <summary>扇形模板。Position 解析为圆心和射线起点，rotation 解析为扇形中心朝向；lengthOverride 被忽略。</summary>
public sealed record FanTemplate(float Radius, float AngleRadians) : GeometryTemplate
{
    /// <inheritdoc />
    public override GeometryKind Kind => GeometryKind.Fan;

    /// <inheritdoc />
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new FanGeometry(position, rotation, Radius, AngleRadians);
}

/// <summary>
/// 中心矩形模板。
/// <para>Position 解析为矩形中心，rotation 解析为矩形朝向。</para>
/// <para>当 <paramref name="lengthOverride"/> 不为空时使用该值作为长度（典型场景：BetweenTwoAnchors 注入两点距离作为连线矩形长度），否则使用模板自身的 <see cref="Length"/>。</para>
/// </summary>
public sealed record RectangleTemplate(float Width, float Length) : GeometryTemplate
{
    /// <inheritdoc />
    public override GeometryKind Kind => GeometryKind.Rectangle;

    /// <inheritdoc />
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new RectangleGeometry(position, rotation, Width, lengthOverride ?? Length);
}

/// <summary>
/// 射线矩形模板。
/// <para>Position 解析为后边中心，rotation 解析为前方朝向，图形沿 -LocalY 方向延伸。</para>
/// <para>当 <paramref name="lengthOverride"/> 不为空时使用该值作为长度（典型场景：BetweenTwoAnchors），否则使用模板自身的 <see cref="Length"/>。</para>
/// </summary>
public sealed record RayRectangleTemplate(float MinLocalX, float MaxLocalX, float Length) : GeometryTemplate
{
    /// <summary>使用左右对称半宽创建模板。</summary>
    public static RayRectangleTemplate Symmetric(float width, float length)
        => new(-width * 0.5f, width * 0.5f, length);

    /// <inheritdoc />
    public override GeometryKind Kind => GeometryKind.RayRectangle;

    /// <inheritdoc />
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new RayRectangleGeometry(position, rotation, MinLocalX, MaxLocalX, lengthOverride ?? Length);
}

/// <summary>
/// 局部多边形模板。Position 解析为局部坐标原点，rotation 解析为局部 -LocalY 对齐的世界朝向；lengthOverride 被忽略。
/// </summary>
public sealed record LocalPolygonTemplate : GeometryTemplate
{
    /// <summary>使用局部点列创建模板，构造时复制一份避免外部修改。</summary>
    public LocalPolygonTemplate(IEnumerable<Vector2> localPoints)
    {
        LocalPoints = localPoints.ToArray();
    }

    /// <summary>局部坐标点列。<c>LocalX &gt; 0</c> 是图形右侧，<c>LocalY &lt; 0</c> 是图形前方。</summary>
    public IReadOnlyList<Vector2> LocalPoints { get; }

    /// <inheritdoc />
    public override GeometryKind Kind => GeometryKind.LocalPolygon;

    /// <inheritdoc />
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new LocalPolygonGeometry(position, rotation, LocalPoints);
}

/// <summary>
/// 世界多边形模板。position 和 rotation 参数被忽略，永远返回构造时记录的世界多边形；建议搭配 <see cref="StaticTransform"/> 使用。
/// </summary>
public sealed record WorldPolygonTemplate : GeometryTemplate
{
    /// <summary>使用世界坐标点列创建模板，构造时复制一份避免外部修改。</summary>
    public WorldPolygonTemplate(IEnumerable<Vector3> worldPoints)
    {
        WorldPoints = worldPoints.ToArray();
    }

    /// <summary>世界坐标点列。</summary>
    public IReadOnlyList<Vector3> WorldPoints { get; }

    /// <inheritdoc />
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    /// <inheritdoc />
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new WorldPolygonGeometry(WorldPoints);
}

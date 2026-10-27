using System.Numerics;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 区域的位置/朝向绑定。
/// <para>所有子类都是纯数据（record），禁止持有 <c>IGameObject</c> / <c>IBattleChara</c> 引用；实体只通过 <see cref="uint"/> EntityId 间接表达。</para>
/// <para>真正的"实体 → Position/Rotation"解析在 <c>SafeZoneManager</c> 主线程帧内完成。</para>
/// </summary>
public abstract record TransformBinding
{
    /// <summary>是否需要每帧通过实体重新解析。</summary>
    public abstract bool IsDynamic { get; }
}

/// <summary>完全静态的位置和朝向。</summary>
public sealed record StaticTransform(Vector3 Position, float Rotation) : TransformBinding
{
    /// <inheritdoc />
    public override bool IsDynamic => false;
}

/// <summary>
/// 需要每帧通过实体解析的动态绑定基类。
/// <para>解析失败时按 <see cref="MissingPolicy"/> 处理。</para>
/// </summary>
public abstract record DynamicTransformBinding(MissingEntityPolicy MissingPolicy) : TransformBinding
{
    /// <inheritdoc />
    public override bool IsDynamic => true;
}

/// <summary>
/// 跟随实体位置。
/// <para>世界位置 = <c>entity.Position + (WorldOffsetXZ.X, 0, WorldOffsetXZ.Y)</c>，朝向固定为 <see cref="Rotation"/>。</para>
/// </summary>
public sealed record FollowEntityPosition(
    uint EntityId,
    Vector2 WorldOffsetXZ,
    float Rotation,
    MissingEntityPolicy MissingPolicy) : DynamicTransformBinding(MissingPolicy);

/// <summary>
/// 跟随实体位置和朝向。
/// <para>世界位置 = <c>GeometryHelper.LocalToWorld(LocalOffsetXZ, entity.Position, entity.Rotation)</c>，朝向 = <c>entity.Rotation</c>。</para>
/// <para>局部偏移遵循 Geometry 局部坐标：<c>LocalX &gt; 0</c> 是图形右侧，<c>LocalY &lt; 0</c> 是图形前方。</para>
/// </summary>
public sealed record FollowEntityPositionAndRotation(
    uint EntityId,
    Vector2 LocalOffsetXZ,
    MissingEntityPolicy MissingPolicy) : DynamicTransformBinding(MissingPolicy);

/// <summary>
/// 固定位置朝向目标实体。
/// <para>世界位置固定为 <see cref="Anchor"/>，朝向 = <c>GeometryHelper.AngleTo(Anchor, target.Position)</c>。</para>
/// <para>当 <see cref="TargetEntityId"/> 实体在主线程解析时不可用时，朝向使用 <see cref="FallbackRotation"/>。</para>
/// </summary>
public sealed record FixedPositionFaceEntity(
    Vector3 Anchor,
    uint TargetEntityId,
    float FallbackRotation,
    MissingEntityPolicy MissingPolicy) : DynamicTransformBinding(MissingPolicy);

/// <summary>
/// 两个锚点之间的连线绑定。
/// <para>位置 = 两个锚点 X/Z 中点（Y 沿用 A 的 Y），朝向 = <c>GeometryHelper.AngleTo(A, B)</c>，长度 = <c>GeometryHelper.DistanceXZ(A, B)</c>。</para>
/// <para>长度通过 <c>GeometryTemplate.Resolve</c> 的 <c>lengthOverride</c> 参数注入射线矩形等支持可变长度的模板。</para>
/// </summary>
public sealed record BetweenTwoAnchors(
    AnchorSource AnchorA,
    AnchorSource AnchorB,
    MissingEntityPolicy MissingPolicy) : DynamicTransformBinding(MissingPolicy);

/// <summary>
/// <see cref="BetweenTwoAnchors"/> 单个锚点的来源。
/// <para>不直接使用 <c>Vector3?</c> + <c>uint?</c> 组合，避免出现"两个都为 null / 两个都有值"的非法状态。</para>
/// </summary>
public abstract record AnchorSource;

/// <summary>实体锚点。每帧从 ObjectTable 解析为实体位置。</summary>
public sealed record EntityAnchor(uint EntityId) : AnchorSource;

/// <summary>世界锚点。位置不变。</summary>
public sealed record WorldAnchor(Vector3 Position) : AnchorSource;

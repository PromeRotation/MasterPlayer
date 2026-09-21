using System.Collections.Generic;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 区域在 Manager 内部的运行对象。
/// <para>该类型对外不可见，只在 <c>SafeZoneManager</c> 内部使用；对外暴露的不可变视图请见 <see cref="SafeZoneSnapshot"/>。</para>
/// <para>字段可变是因为 Manager 需要支持 <c>UpdateArea</c> 修改模板、绑定和窗口，以及在每帧解析后写入 <see cref="LastResolvedGeometry"/>。</para>
/// </summary>
internal sealed class RuntimeArea
{
    public RuntimeArea(
        SafeZoneAreaHandle handle,
        GeometryTemplate template,
        TransformBinding binding,
        IReadOnlyList<DangerWindow> dangerWindows,
        IReadOnlyList<GoalWindow> goalWindows)
    {
        Handle = handle;
        Template = template;
        Binding = binding;
        DangerWindows = dangerWindows;
        GoalWindows = goalWindows;
    }

    public SafeZoneAreaHandle Handle { get; }

    public AreaKind Kind => Handle.Kind;

    public GeometryTemplate Template { get; set; }

    public TransformBinding Binding { get; set; }

    /// <summary>仅 <see cref="AreaKind.Danger"/> 区域使用，其他类型可保留空列表。</summary>
    public IReadOnlyList<DangerWindow> DangerWindows { get; set; }

    /// <summary>仅 <see cref="AreaKind.Goal"/> 区域使用，其他类型可保留空列表。</summary>
    public IReadOnlyList<GoalWindow> GoalWindows { get; set; }

    /// <summary>最近一次主线程解析得到的 Geometry；动态绑定实体丢失时 <see cref="MissingEntityPolicy.KeepLastResolved"/> 会复用该值。</summary>
    public IGeometryShape? LastResolvedGeometry { get; set; }
}

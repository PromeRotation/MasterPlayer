namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// 动态绑定所需实体在主线程解析时不存在或不可定位时的处理策略。
/// </summary>
public enum MissingEntityPolicy
{
    /// <summary>本帧跳过该区域，不写入快照，但保留运行对象等待实体恢复。</summary>
    DisableArea,

    /// <summary>沿用上一次成功解析的 Geometry，直到下一次解析成功。</summary>
    KeepLastResolved,

    /// <summary>从 SafeZoneManager 中彻底移除该区域，下次需要时由调用方重新注册。</summary>
    RemoveArea
}

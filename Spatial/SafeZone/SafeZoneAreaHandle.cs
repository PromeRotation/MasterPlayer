namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 区域句柄。
/// <para><see cref="Id"/> 为 0 表示无效句柄；正常句柄由 <c>SafeZoneManager</c> 单调递增分配。</para>
/// <para><see cref="Kind"/> 记录该句柄归属的区域类型，使调用方可以按类型分发处理而无需查询 Manager。</para>
/// </summary>
public readonly record struct SafeZoneAreaHandle(ulong Id, AreaKind Kind)
{
    /// <summary>未分配/无效的句柄。</summary>
    public static readonly SafeZoneAreaHandle None = new(0UL, AreaKind.Danger);

    /// <summary>该句柄是否指向一个已分配的区域。</summary>
    public bool IsValid => Id != 0UL;
}

using System.Numerics;
using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>一次完整效果包的主目标和受击对象；不使用 PR 按目标展开事件推断主目标。</summary>
public sealed record NativeActionEffectEvent : LogSystemEvent
{
    public override LogSystemEventKind Kind => LogSystemEventKind.ActionEffect;
    public ulong SourceId { get; init; }
    /// <summary>Receive 当时复制的来源 OID/对象槽；旧录像或空来源指针时未知，不保存原生指针。</summary>
    public uint? SourceOid { get; init; }
    public int? SourceObjectIndex { get; init; }
    public uint ActionId { get; init; }
    /// <summary>原始效果包动作类型；旧录像缺失为 null，1 表示 Spell。</summary>
    public byte? ActionType { get; init; }
    public ulong MainTargetId { get; init; }
    public uint GlobalSequence { get; init; }
    public ulong[] Targets { get; init; } = [];
    /// <summary>与 Targets 同序的每目标八个原始效果槽；旧录像缺失时为 null，不能据此推断待确认状态。</summary>
    public ulong[][]? RawEffects { get; init; }
    /// <summary>Receive 的区域效果坐标副本；空指针或旧录像缺字段时为 null，不是受击对象位置。</summary>
    public Vector3? TargetPosition { get; init; }
}

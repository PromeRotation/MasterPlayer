using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>一次完整效果包的主目标和受击对象；不使用 PR 按目标展开事件推断主目标。</summary>
public sealed record NativeActionEffectEvent : LogSystemEvent
{
    public override LogSystemEventKind Kind => LogSystemEventKind.ActionEffect;
    public ulong SourceId { get; init; }
    public uint ActionId { get; init; }
    public ulong MainTargetId { get; init; }
    public uint GlobalSequence { get; init; }
    public ulong[] Targets { get; init; } = [];
}

using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

/// <summary>Director UnionData 更新；与普通场地 MapEffect 是两种不同的输入。</summary>
public sealed record LegacyMapEffectEvent : LogSystemEvent
{
    public override LogSystemEventKind Kind => LogSystemEventKind.MapEffect;
    public byte Sequence { get; init; }
    public byte Param { get; init; }
    public byte[] Data { get; init; } = [];
}

using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

public sealed record ReplaySystemLogEvent : LogSystemEvent
{
    // PR has no SystemLog kind. The recording discriminator and concrete type preserve its identity.
    public override LogSystemEventKind Kind => LogSystemEventKind.MorelogCompat;
    public uint MessageId { get; init; }
    public int?[] Arguments { get; init; } = [];
}

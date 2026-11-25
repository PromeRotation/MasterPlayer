namespace MasterPlayer.Scripting;

/// <summary>脚本持有的机制预测；预计结束时间只用于显示，不驱动真实机制结束。</summary>
public sealed record ScriptTimeline(
    int PhaseIndex, string PhaseName, float ElapsedInState, DateTime CapturedAt,
    IReadOnlyList<ScriptTimelineEntry> Entries);

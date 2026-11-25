namespace MasterPlayer.Scripting;

public sealed record ScriptTimelineEntry(
    uint StateId, string Name, string Comment, DateTime ExpectedEnd, ScriptTimelineHint Hints);

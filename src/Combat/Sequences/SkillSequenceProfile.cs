namespace MasterPlayer.Combat.Sequences;

public sealed class SkillSequenceProfile
{
    public uint JobId { get; set; }
    public string PresetId { get; set; } = Guid.NewGuid().ToString("N");
    public string PresetName { get; set; } = "预设";
    public bool IsActive { get; set; } = true;
    public bool EnableAutoRun { get; set; }
    public bool LoopExecution { get; set; }
    public string CompletionCommand { get; set; } = "";
    public int CompletionCommandDelayMs { get; set; } = 600;
    public List<SkillSequenceEntry> Entries { get; set; } = [];
}

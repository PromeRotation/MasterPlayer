namespace MasterPlayer.Combat.Sequences;

public sealed class SkillSequenceEntry
{
    public bool Enabled { get; set; } = true;
    public uint SkillId { get; set; }
    public bool TargetSelf { get; set; }
    public uint SkipIfHasStatusId { get; set; }
    public uint RequireStatusId { get; set; }
    public string Note { get; set; } = "";
}

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivaleEyeState
{
    internal bool ConstructorNeedsBootstrap { get; set; } = true;
    internal bool CombatNeedsBootstrap { get; set; } = true;
    internal CarnivaleBlindness DemonBlindness { get; } = new(571);
    internal CarnivaleBlindness TearyBlindness { get; } = new(571);
    internal CarnivaleBlindness ColdBlindness { get; } = new(571);
    internal CarnivaleBlindness DreadBlindness { get; } = new(571);
    internal List<DungeonPendingCast> Demons { get; } = [];
    internal List<DungeonPendingCast> Cold { get; } = [];
    internal List<DungeonPendingCast> Dread { get; } = [];
    internal List<CarnivaleEyeStack> Stacks { get; } = [];

    internal void Removed(ulong actor)
    {
        DemonBlindness.Removed(actor);
        TearyBlindness.Removed(actor);
        ColdBlindness.Removed(actor);
        DreadBlindness.Removed(actor);
    }
}

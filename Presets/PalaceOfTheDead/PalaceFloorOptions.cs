namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceFloorOptions
{
    internal PalaceClearBehavior ClearBehavior { get; init; } = PalaceClearBehavior.Leveling;
    internal bool AutoPassage { get; init; } = true;
    internal bool AutoMoveTreasure { get; init; } = true;
    internal bool OpenChestsFirst { get; init; }
    internal bool GoldCoffer { get; init; } = true;
    internal bool SilverCoffer { get; init; } = true;
    internal bool BronzeCoffer { get; init; } = true;
    internal bool FullClear { get; init; }
    internal bool TrapHints { get; init; } = true;
    internal int MaxPull { get; init; }
    internal IReadOnlySet<uint> AutoPomanders { get; init; } = new HashSet<uint>();
}

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivaleSnorterState
{
    internal bool NeedsBootstrap { get; set; } = true;
    internal long Generation { get; set; }
    internal List<(long Generation, AlphaActorState Actor)> Bombs { get; } = [];
    internal DateTime ExplosionActivation { get; set; }
    internal DateTime SnortEffectEnds { get; set; }
    internal DateTime FungahActivation { get; set; }
    internal bool OtherPatterns { get; set; }
    internal DateTime ExplosiveCreatedAt { get; set; }
}

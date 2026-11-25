namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivaleGoldorState
{
    internal bool NeedsBootstrap { get; set; } = true;
    internal List<CarnivaleQuakeSequence> Quakes { get; } = [];
    internal Dictionary<ulong, long> QuakeStarts { get; } = [];
    internal List<DungeonPendingCast> BeamInputs { get; } = [];
    internal DungeonPendingCast[] BeamOutput { get; set; } = [];
    internal DungeonPendingCast? Gravity { get; set; }
    internal DungeonPendingCast? Thunder { get; set; }
    internal List<(AlphaActorState Caster, DungeonPendingCast Cast)> Ruptures { get; } = [];
}

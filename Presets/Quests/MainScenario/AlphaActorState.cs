namespace MasterPlayer.Mechanics.Dungeons;

// A reference survives removal; a later actor with the same entity ID is a new instance.
internal sealed class AlphaActorState(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
    internal (uint Type, ulong Target) Tether { get; set; }
    internal bool Removed { get; set; }
}

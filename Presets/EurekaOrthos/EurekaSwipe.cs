namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaSwipe(long Generation, DungeonPendingCast Cast, DateTime Activation)
{
    internal bool Danger { get; set; }
}

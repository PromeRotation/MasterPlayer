namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivaleSiegfriedState
{
    internal bool NeedsBootstrap { get; set; } = true;
    internal List<(long Generation, DungeonPendingCast Cast)> Hyperdrives { get; } = [];
    internal List<(long Generation, DungeonPendingCast Cast)> Torches { get; } = [];
    internal List<(long Generation, DungeonPendingCast Cast)> Sparksteels { get; } = [];
    internal List<CarnivaleSpawnAoe> Bombs { get; } = [];
    internal List<CarnivaleSpawnAoe> Boulders { get; } = [];
    internal CarnivaleSpawnKnockback? RubberBullet { get; set; }
}

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivaleGogoState
{
    internal bool NeedsBootstrap { get; set; } = true;
    internal CarnivalePyreticState Pyretic { get; } = new();
    internal DungeonPendingCast? Blizzard { get; set; }
    internal DungeonPendingCast? Icestorm { get; set; }
    internal List<(long Generation, DungeonPendingCast Cast)> LargeMeteors { get; } = [];
}

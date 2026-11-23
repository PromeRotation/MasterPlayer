namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class ArkveldActorReference(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
}

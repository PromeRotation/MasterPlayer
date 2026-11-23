namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class EnuoActorReference(DungeonActor actor)
{
    internal DungeonActor Actor = actor;
    internal bool Destroyed;
}

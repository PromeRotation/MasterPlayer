namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class ZeleniaActorReference(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
    internal bool Destroyed { get; set; }
}

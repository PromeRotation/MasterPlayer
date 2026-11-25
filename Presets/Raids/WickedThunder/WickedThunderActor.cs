using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class WickedThunderActor(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
    internal DungeonCastObservation? Cast { get; set; }
    internal ReplayStatus?[] Statuses { get; } = new ReplayStatus?[60];
    internal float Elapsed { get; set; }
    internal DateTime Clock { get; set; }
    internal bool Destroyed { get; set; }
}

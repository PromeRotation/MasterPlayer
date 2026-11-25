using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class KefkaActor(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
    internal DungeonCastObservation? Cast { get; set; }
    internal float Elapsed { get; set; }
    internal DateTime ClockAt { get; set; }
    internal ReplayStatus?[] Statuses { get; } = new ReplayStatus?[60];
    internal bool Destroyed { get; set; }
}

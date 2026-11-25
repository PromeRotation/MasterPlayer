using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class QuantumActor(DungeonActor actor)
{
    internal DungeonActor Actor { get; set; } = actor;
    internal DungeonCastObservation? Cast { get; set; }
    internal float CastElapsed { get; set; }
    internal DateTime CastClockAt { get; set; }
    internal ReplayStatus?[] Statuses { get; } = new ReplayStatus?[60];
    internal bool Destroyed { get; set; }
}

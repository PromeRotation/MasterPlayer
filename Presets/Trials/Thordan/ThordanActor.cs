using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class ThordanActor(DungeonActor actor)
{
    internal DungeonActor Actor = actor;
    internal DungeonCastObservation? Cast;
    internal bool Destroyed;
    internal float Elapsed;
    internal DateTime Clock;
    internal readonly ReplayStatus?[] Statuses = new ReplayStatus?[60];
    internal ReplayTether? Tether;
}

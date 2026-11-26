using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Fates;

internal sealed class CrescentFateActor(ReplayActorState actor)
{
    internal ReplayActorState Actor = actor;
    internal ReplayCast? Cast;
    internal float Elapsed;
    internal DateTime Clock;
    internal bool Destroyed;
    internal bool EventHappened;
    internal readonly bool IsPet = actor.Kind == 2 && actor.SubKind == 2;
    internal ReplayStatus?[] Statuses = new ReplayStatus?[60];
}

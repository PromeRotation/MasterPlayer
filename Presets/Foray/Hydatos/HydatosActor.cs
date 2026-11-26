using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hydatos;

internal sealed class HydatosActor(ReplayActorState actor)
{
    internal ReplayActorState Actor = actor;
    internal readonly bool IsPet = actor.Kind == 2 && actor.SubKind == 2;
    internal readonly bool NativeNpc = HydatosEncounter.NativeNpc(actor);
    internal ReplayCast? Cast;
    internal float Elapsed;
    internal DateTime Clock;
    internal bool Destroyed;
    internal readonly ReplayStatus?[] Statuses = new ReplayStatus?[60];
}

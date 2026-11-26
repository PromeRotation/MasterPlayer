using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaActor(ReplayActorState actor)
{
    internal ReplayActorState Actor = actor;
    // The real Feed writes FateID only at OpCreate; later snapshots cannot mutate it.
    internal readonly uint CreationFateId = actor.FateId ?? 0;
    internal readonly bool NativeNpc = actor.Kind != 1 && !(actor.Kind == 2 && actor.SubKind is 2 or 3 or 9);
    internal ReplayCast? Cast;
    // CastWeakpoint can retain an original caster after one duplicate Finish,
    // and then observes its current non-Spell CastInfo as well.
    internal ReplayCast? RawCast;
    internal float RawElapsed;
    internal readonly bool IsPet = actor.Kind == 2 && actor.SubKind == 2;
    internal bool CreationTetherPending = true;
    internal ReplayStatus?[] Statuses = new ReplayStatus?[60];
    internal float Elapsed;
    internal DateTime Clock;
    internal bool Destroyed;
    internal ReplayTether? Tether => Actor.Tethers.FirstOrDefault(t => t.Id != 0);
    internal long BirthOrder;
    internal long PendingOrder;
    internal (uint Id, ulong Target) ObservedTether;
    internal (byte Model, byte First, byte Second) ObservedModelState;
}

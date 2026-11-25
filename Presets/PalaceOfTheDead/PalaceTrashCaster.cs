using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

// The object survives snapshot replacement, but never Actor destruction/recreation.
internal sealed class PalaceTrashCaster(ReplayActorState actor)
{
    internal ReplayActorState Actor = actor;
    internal ReplayCast? Cast;
    internal DateTime Clock;
    internal float Elapsed;
    internal float Extra;
    internal bool Observed;
    internal DateTime Start;
    internal uint Action;
    internal bool Responded;
    internal uint FinishedAction;
    internal DateTime FinishedStart;
}

using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

// Independent of the charge's target reference: GenericKnockback captures a raid slot.
internal sealed record PalaceBlackRiderKnockback(long Generation, AlphaActorState Source,
    DungeonPendingCast Cast, Vector3 Origin, int TargetSlot)
{
    internal float Distance => 25;
    internal string Kind => "AwayFromOrigin";
    internal bool StopAtWall => true;
    internal bool IgnoreImmunes => false;
}

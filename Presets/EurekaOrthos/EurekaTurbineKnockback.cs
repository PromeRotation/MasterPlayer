namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaTurbineKnockback(DungeonPendingCast Cast)
{
    internal float Distance => 15;
    internal bool IgnoreImmunes => true;
    internal bool StopAtWall => false;
}

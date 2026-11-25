namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaGroanKnockback(DungeonPendingCast Cast)
{
    internal float Distance => 15;
    internal bool IgnoreImmunes => false;
    internal bool StopAtWall => false;
}

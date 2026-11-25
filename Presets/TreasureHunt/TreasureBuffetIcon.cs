namespace MasterPlayer.Mechanics.Dungeons;

// The icon's target is the source Actor of the icon event, independently of the cast bait.
internal sealed record TreasureBuffetIcon(DungeonActor Target, DateTime StartedAt, DateTime Activation, bool Removed)
{
    public float Distance => 20;
    public bool StopAtWall => true;
}

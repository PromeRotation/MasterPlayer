using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasurePoleShift(DungeonPendingCast Cast, bool Pull)
{
    internal Vector3 Origin => Cast.Location;
    internal DateTime Activation => Cast.FinishAt;
    internal float Distance => 7;
    internal float MinDistance => 1;
    internal bool IgnoreImmunes => true;
    internal bool StopAtWall => true;
}

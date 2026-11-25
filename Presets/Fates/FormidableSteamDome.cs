using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record FormidableSteamDome(DungeonPendingCast Cast)
{
    internal float Distance => 15;
    internal bool StopAtWall => false;
    internal bool IgnoreImmunes => false;
    internal Vector3 Destination(Vector3 from)
    {
        var direction = from - Cast.Location;
        direction.Y = 0;
        return direction.LengthSquared() > 0 ? from + Vector3.Normalize(direction) * Distance : from;
    }
}

using System.Numerics;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntAglaopeKnockback(HuntPendingCast Cast)
{
    internal Vector3 Origin => Cast.Input.Location;
    internal DateTime Activation => Cast.Activation;
    internal const string KIND = "AwayFromOrigin";
    internal const float DISTANCE = 10;
    internal const float RADIUS = 30;
    internal const bool STOP_AT_WALL = false;
    internal const bool IGNORE_IMMUNES = false;
}

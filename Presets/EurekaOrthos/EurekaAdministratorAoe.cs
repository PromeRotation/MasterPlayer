using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaAdministratorAoe(long Generation, GeometryTemplate Shape, Vector3 Origin,
    float Rotation, DateTime Activation, DateTime PreparedAt, DungeonPendingCast? Cast = null,
    AlphaActorState? IconSource = null, uint Icon = 0)
{
    internal bool Danger { get; set; }
    internal bool Risky { get; set; } = true;
}

using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaVoice(long Generation, GeometryTemplate Shape, Vector3 Origin,
    DateTime PreparedAt, DateTime Activation, DungeonPendingCast? Cast = null,
    AlphaActorState? ModelSource = null, byte ModelState = 0)
{
    internal bool Danger { get; set; }
}

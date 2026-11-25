using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record MicaAoe(long Id, DungeonActor Source, DungeonPendingCast Cast, GeometryTemplate Shape,
    Vector3 Position, float Rotation, DateTime Activation, int Stage = 0, bool Risky = true, bool DangerColor = false);

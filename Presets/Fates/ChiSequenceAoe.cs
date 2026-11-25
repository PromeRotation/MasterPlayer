using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record ChiSequenceAoe(long Id, DungeonActor Source, DungeonPendingCast? Cast,
    GeometryTemplate Shape, Vector3 Position, float Rotation, DateTime CreatedAt, DateTime Activation,
    int Stage = 0, bool DangerColor = false);

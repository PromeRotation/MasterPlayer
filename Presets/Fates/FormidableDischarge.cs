using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record FormidableDischarge(DungeonActor Source, GeometryTemplate Shape, Vector3 Position,
    DateTime CreatedAt, DateTime Activation);

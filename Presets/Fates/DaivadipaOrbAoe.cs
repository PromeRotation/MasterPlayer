using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DaivadipaOrbAoe(DungeonActor Source, GeometryTemplate Shape, Vector3 Position,
    float Rotation, DateTime Activation);

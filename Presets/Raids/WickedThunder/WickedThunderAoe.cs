using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record WickedThunderAoe(long Generation, WickedThunderActor Source, uint Action,
    GeometryTemplate? Shape, Vector3 Origin, float Rotation, DateTime Activation,
    WickedThunderActor? Target = null, ulong TargetId = 0, bool Risky = true, bool Danger = false);

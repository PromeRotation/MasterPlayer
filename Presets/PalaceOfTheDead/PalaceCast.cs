using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceCast(ulong Source, uint Action, ulong Target, Vector3 Position, Vector3 SourcePosition,
    float? Rotation, DateTime Start, DateTime Hit, GeometryTemplate? Shape = null, string Name = "", bool LocationTargeted = false,
    bool Charge = false, float CastExtra = 0, bool GenericGaze = false);

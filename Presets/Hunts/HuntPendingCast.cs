using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntPendingCast(ulong Source, HuntDefinition Definition, HuntCastRule Rule,
    Vector3 Location, float Rotation, DateTime StartedAt, DateTime Activation, ulong TargetId, bool PacketPose, bool Observed,
    GeometryTemplate? Shape, ReplayCast Input, ReplayCast? Observation, bool Followup = false,
    string? SequenceId = null, bool Risky = true, Vector4? Color = null, long Order = 0, int Stage = -1,
    DateTime? ObservedAt = null, bool OutlineOnly = false, bool Safe = false)
{
    internal string AreaId => SequenceId ?? (Followup ? $"{Source}/followup/{Rule.Action}" : $"{Source}");
}

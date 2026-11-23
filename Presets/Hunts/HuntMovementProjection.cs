using System.Numerics;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntMovementProjection(ulong Source, Vector3 From, Vector3 To);

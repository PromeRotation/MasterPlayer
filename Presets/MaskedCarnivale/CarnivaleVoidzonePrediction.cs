using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CarnivaleVoidzonePrediction(long Generation, ulong Source, ulong MainTarget, uint Action,
    uint Sequence, DateTime OccurredAt, DateTime ObservedAt, DateTime Activation, Vector3 Position);

using System.Numerics;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaEye(long Generation, Vector3 Position, DateTime Activation, bool Inverted, ulong SourceId = 0,
    float Forward = 0, bool Precise = false, float Range = 10000f);

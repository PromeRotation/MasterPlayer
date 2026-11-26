using System.Numerics;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaMine(long generation, Vector3 position)
{
    internal long Generation { get; } = generation;
    internal Vector3 Position { get; } = position;
    internal ulong ForbiddenSoakers;
}

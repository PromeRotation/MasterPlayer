using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class QuantumBlazeLine(long generation, Vector3 next, Vector3 advance, DateTime activation, int count, float rotation)
{
    internal long Generation { get; } = generation;
    internal Vector3 Next { get; set; } = next;
    internal Vector3 Advance { get; } = advance;
    internal DateTime Activation { get; set; } = activation;
    internal int Remaining { get; set; } = count;
    internal int MaxShown { get; } = count;
    internal float Rotation { get; } = rotation;
}

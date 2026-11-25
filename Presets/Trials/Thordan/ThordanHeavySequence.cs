using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class ThordanHeavySequence(long generation, ThordanActor source, Vector3 origin,
    float rotation, DateTime activation)
{
    internal readonly long Generation = generation;
    internal readonly ThordanActor Source = source;
    internal readonly Vector3 Origin = origin;
    internal readonly float Rotation = rotation;
    internal DateTime Activation = activation;
    internal int Order;
}

using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaArtChaser(long generation, BozjaActor source, BozjaActor target, Vector3 previous, DateTime activation)
{
    internal readonly long Generation = generation;
    internal readonly BozjaActor Source = source;
    internal readonly BozjaActor Target = target;
    internal Vector3 Previous = previous;
    internal DateTime Activation = activation;
    internal float MoveDistance;
    internal int Remaining = 5;

    internal Vector3 Predicted()
    {
        var location = Target.Actor.Position with { Y = 0 };
        var offset = location - Previous;
        var distance = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
        var point = distance > MoveDistance ? Previous + (MoveDistance * offset) * (1f / distance) : location;
        return DungeonEncounter.QuantizedDungeonPosition(point);
    }
}

using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class EurekaChaser(long generation, AlphaActorState target, Vector3 origin, DungeonPendingCast cast)
{
    internal long Generation { get; } = generation;
    internal DungeonPendingCast Cast { get; } = cast;
    internal AlphaActorState Target { get; } = target;
    internal Vector3 Previous { get; set; } = origin;
    internal float MoveDistance { get; set; }
    internal int Remaining { get; set; } = 5;
    internal DateTime Activation { get; set; } = cast.FinishAt;
    internal Vector3? Destination { get; init; }

    internal Vector3 Predicted()
    {
        var position = Target.Actor.Position;
        var offset = new Vector2(position.X - Previous.X, position.Z - Previous.Z);
        var distance = offset.Length();
        if (distance > MoveDistance)
        {
            var step = MoveDistance * offset * (1f / distance);
            position = new(Previous.X + step.X, position.Y, Previous.Z + step.Y);
        }
        return DungeonEncounter.QuantizedDungeonPosition(position);
    }
}

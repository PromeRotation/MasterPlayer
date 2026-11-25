using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaCharge(long Generation, AlphaActorState Source, AlphaActorState Target, DungeonPendingCast Cast)
{
    internal float HalfWidth => 4;
    internal float Length => Vector2.Distance(new(Source.Actor.Position.X, Source.Actor.Position.Z),
        new(Target.Actor.Position.X, Target.Actor.Position.Z));
    internal float Rotation => ReferenceEquals(Source, Target) ? Source.Actor.Rotation
        : MathF.Atan2(Target.Actor.Position.X - Source.Actor.Position.X, Target.Actor.Position.Z - Source.Actor.Position.Z);
}

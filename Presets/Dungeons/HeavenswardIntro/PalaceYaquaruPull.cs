using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record PalaceYaquaruPull(AlphaActorState Source, IReadOnlyList<AlphaActorState> Puddles,
    float TankDistance = 2)
{
    internal float Radius => 9 + 2 * Source.Actor.HitboxRadius + TankDistance;
    internal float Score(Vector3 position) => Puddles.All(p =>
        Vector2.Distance(new(position.X, position.Z), new(p.Actor.Position.X, p.Actor.Position.Z)) > Radius) ? 1000 : 0;
}

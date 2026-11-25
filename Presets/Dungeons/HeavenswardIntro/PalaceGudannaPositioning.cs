using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record PalaceGudannaPositioning(AlphaActorState Source, int Index,
    PalaceGudannaNextAction NextAction, Vector3 ArenaCenter)
{
    internal const float WEIGHT = .5f;
    internal float Score(Vector3 position)
    {
        var matches = NextAction switch
        {
            PalaceGudannaNextAction.Thunderbolt => Vector2.DistanceSquared(
                new(position.X, position.Z), new(Source.Actor.Position.X, Source.Actor.Position.Z)) <= 36,
            PalaceGudannaNextAction.Charybdis => Vector2.DistanceSquared(
                new(position.X, position.Z), new(ArenaCenter.X, ArenaCenter.Z)) >= 484,
            PalaceGudannaNextAction.TrounceEast => position.X - ArenaCenter.X >= 20,
            PalaceGudannaNextAction.TrounceWest => position.X - ArenaCenter.X <= -20,
            _ => false,
        };
        return matches ? WEIGHT : 0;
    }
}

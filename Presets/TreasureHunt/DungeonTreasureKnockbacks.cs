using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<TreasureRadialKnockback> treasureWallKnockbacks = [];
    internal IReadOnlyList<TreasureRadialKnockback> TreasureWallKnockbackSources => treasureWallKnockbacks;

    private void TreasureWallKnockbackStart(DungeonPendingCast cast, bool correction)
    {
        if (!IsNativeTreasure || cast.Rule.Action is not (13314 or 13377 or 13440 or 21654 or 32258 or 21749 or 21677 or 21690 or 43684)) return;
        var index = correction ? treasureWallKnockbacks.FindIndex(k => k.Cast.Source == cast.Source) : -1;
        var pull = cast.Rule.Action is 21677 or 43684;
        var minDistance = index >= 0 ? treasureWallKnockbacks[index].MinDistance
            : (_actors.GetValueOrDefault(treasureParty.FirstOrDefault())?.HitboxRadius ?? 0)
                + (_actors.GetValueOrDefault(cast.Source)?.HitboxRadius ?? 0);
        if (cast.Rule.Action == 43684) minDistance = 4.9f;
        var distance = cast.Rule.Action switch { 43684 => 50, 21654 => 25, _ => 20 };
        var source = new TreasureRadialKnockback(cast, distance, !pull,
            cast.Rule.Action is 13377 or 21677 ? cast.Rule.Shape : null, pull, pull ? minDistance : 0);
        if (index >= 0) treasureWallKnockbacks[index] = source;
        else treasureWallKnockbacks.Add(source);
    }

    private void TreasureWallKnockbackFinish(DungeonPendingCast cast)
    {
        var index = treasureWallKnockbacks.FindIndex(k => k.Cast.Source == cast.Source && k.Cast.Rule.Action == cast.Rule.Action);
        if (index >= 0) treasureWallKnockbacks.RemoveAt(index);
    }

    internal IEnumerable<(TreasureRadialKnockback Source, Vector3 From, Vector3 To, bool Unsafe)> TreasureWallMovements(DungeonActor player)
    {
        if (!IsNativeTreasure || Arena == null) yield break;
        var from = player.Position;
        foreach (var source in treasureWallKnockbacks)
        {
            var cast = source.Cast;
            if (WhorlImmune(player, cast.FinishAt)
                || source.EffectShape?.Resolve(cast.Location, cast.Rotation, null).Contains(from, 0) == false)
                continue;
            var direction = from - cast.Location;
            direction.Y = 0;
            if (direction.LengthSquared() == 0) continue;
            direction = Vector3.Normalize(direction);
            var distance = source.Distance;
            if (source.TowardsOrigin)
            {
                direction = -direction;
                distance = MathF.Min(distance, Vector2.Distance(new(from.X, from.Z), new(cast.Location.X, cast.Location.Z)) - source.MinDistance);
            }
            if (distance <= 0) continue;
            var to = source.StopAtWall ? TreasureWallLanding(from, direction, distance, player.HitboxRadius) : from + direction * distance;
            var unsafeLanding = cast.Rule.Action == 13314 && _casts.Values.Any(c => c.Rule.Action == 13644
                && c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(to, 0))
                || cast.Rule.Action == 13440 && TreasureKelpieLandingUnsafe(to)
                || cast.Rule.Action == 32258 && _casts.Values.Any(c => c.Rule.Action == 32259
                    && c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(to, 0))
                || cast.Rule.Action == 21749 && _casts.Values.Any(c => c.Rule.Action == 21750
                    && c.Rule.Shape!.Resolve(c.Location, c.Rotation, null).Contains(to, 0))
                || source.TowardsOrigin && !Arena.Contains(to);
            yield return (source, from, to, unsafeLanding);
            from = to;
            if (cast.Rule.Action is 13377 or 21677) yield break;
        }
    }

    private Vector3 TreasureWallLanding(Vector3 from, Vector3 direction, float distance, float hitbox)
    {
        var wallDistance = QuestWindRay(from, direction) - Math.Clamp(hitbox - .499f, .5f - .499f, hitbox - .499f);
        return from + direction * MathF.Min(distance, wallDistance);
    }

    private IEnumerable<DungeonAmbientAoe> TreasureWallKnockbackAoes()
    {
        // The source publishes these directions even when the viewed actor is knockback immune.
        if (Arena != null && treasureWallKnockbacks.FirstOrDefault(k => k.Cast.Rule.Action is 32258 or 21749) is { } kb)
            foreach (var bomb in _casts.Values.Where(c => c.Rule.Action == (kb.Cast.Rule.Action == 21749 ? 21750 : 32259)))
            {
                var delta = bomb.Location - Arena.Position;
                yield return new($"treasure/turmoil/{bomb.Source}", "Wave of Turmoil / 避开水圈方向",
                    new FanTemplate(20, MathF.PI / 3), Arena.Position, MathF.Atan2(delta.X, delta.Z),
                    Display: false, Activation: kb.Cast.FinishAt);
            }
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var index = 0;
        foreach (var movement in TreasureWallMovements(player))
        {
            foreach (var aoe in QuestKnockbackProjection($"treasure/kb/{movement.Source.Cast.Source}/{index++}",
                player, movement.From, movement.To))
                yield return aoe;
        }
    }

    private IEnumerable<string> TreasureWallKnockbackHints()
    {
        if (_actors.GetValueOrDefault(PlayerId) is { } player && TreasureWallMovements(player).Any(m => m.Unsafe))
            yield return "About to be knocked into danger!";
    }
}

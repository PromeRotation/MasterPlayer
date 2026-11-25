using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed partial class PalaceEncounter
{
    private static readonly LocalPolygonTemplate actorMarker = new([new(-.45f, .4f), new(0, -.7f), new(.45f, .4f)]);
    private static readonly Vector4 enemyColor = new(1, 0, 0, 1);

    private ReplayCast? ObservedCast(PalaceCast cast)
        => actors.GetValueOrDefault(cast.Source)?.Cast is { } observed && observed.ActionId == cast.Action
            && Math.Abs((observed.StartedAt - cast.Start).TotalSeconds) < .25 ? observed : null;

    private IEnumerable<ReplayActorState> PartyActors()
    {
        var ids = Party.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).ToHashSet();
        return actors.Values.Where(a => !a.Dead && (ids.Contains(a.Id) || a.Kind == 2 && a.SubKind == 9));
    }

    internal IEnumerable<PalaceDamagePrediction> PredictedDamage()
    {
        foreach (var cast in casts.Values)
        {
            if (cast.Action == 7156 && chargeBaits.Contains(cast.Source)
                && Party.Any(p => p.Slot is >= 0 and < 64 && p.EntityId == cast.Target))
                yield return new(cast.Source, cast.Target, cast.Hit, "Tankbuster");
            if (cast.Action == 7166 && behemothPhase == 4)
                foreach (var target in PartyActors())
                    yield return new(cast.Source, target.Id, ObservedCast(cast)?.FinishAt ?? cast.Hit, "Raidwide");
        }
    }

    private void AddActorMarkers(List<PalaceArea> result, DateTime now)
    {
        if (Boss is not { } boss) return;
        foreach (var actor in actors.Values.Where(a => a.Targetable && !a.Dead
            && (a.Id == boss.Id || boss.Oid == 0x1820 && a.Oid is 0x18F1 or 0x18F2)))
        {
            var position = actor.Position;
            var outside = !PalaceArena.Boundary.Contains(position, 0);
            if (outside)
            {
                var point = new Vector2(position.X, position.Z);
                var closest = PalaceArena.Boundary.GetContours().SelectMany(c => Enumerable.Range(0, c.Count).Select(i =>
                {
                    var a = new Vector2(c[i].X, c[i].Z);
                    var b = new Vector2(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z);
                    var edge = b - a;
                    var t = edge.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1) : 0;
                    return a + edge * t;
                })).MinBy(p => Vector2.DistanceSquared(p, point));
                position = new(closest.X, position.Y, closest.Y);
            }
            result.Add(new($"enemy/{actor.Id}", "敌人朝向", actorMarker, position, actor.Rotation,
                now, DateTime.MaxValue, actor.Id, PreviewOnly: true, OutlineOnly: outside, Color: enemyColor, Unclipped: true));
        }
    }

    private IGeometryShape[] KnockbackDangers()
    {
        var result = new List<IGeometryShape>();
        foreach (var actor in actors.Values.Where(a => a.Oid == 0x1E858E && a.EventState != 7))
            result.Add(new CircleTemplate(9).Resolve(Quantized(actor.Position), 0, null));
        result.AddRange(pending.Values.Where(a => a.Action == 7155).Select(a => a.Geometry));
        foreach (var cast in casts.Values.Where(c => c.Action is 7090 or 7155))
        {
            var observed = ObservedCast(cast);
            var targetId = observed?.TargetId ?? cast.Target;
            var position = cast.Action == 7090 ? cast.Position
                : actors.GetValueOrDefault(targetId)?.Position ?? observed?.Location ?? cast.Position;
            result.Add(new CircleTemplate(cast.Action == 7090 ? 7 : 9).Resolve(position, 0, null));
        }
        return result.ToArray();
    }

    private void AddKnockbackDirections(List<PalaceArea> result, PalaceCast cast, ReplayActorState boss)
    {
        var origin = boss.Position;
        var centers = actors.Values.Where(a => a.Oid == 0x1E858E).Select(a => (a.Position, Radius: 9f))
            .Concat(casts.Values.Where(c => c.Action == 7090).Select(c => (c.Position, Radius: 7f)));
        var index = 0;
        foreach (var (position, radius) in centers)
        {
            var delta = position - origin;
            var distance = new Vector2(delta.X, delta.Z).Length();
            // Upstream Asin yields no finite sector when the source lies inside the circle.
            if (distance < radius) continue;
            var angle = MathF.Asin(radius / distance);
            result.Add(new($"kb-direction/{cast.Source}/{index++}", "击退方向避开毒圈",
                new FanTemplate(100, angle * 2), origin, MathF.Atan2(delta.X, delta.Z),
                cast.Hit, DateTime.MaxValue, cast.Source, cast.Action, NavigationOnly: true));
        }
    }

    internal IEnumerable<PalaceKnockbackProjection> Knockbacks(ReplayActorState player)
    {
        foreach (var cast in casts.Values.Where(c => c.Action == 7156 && c.Target == player.Id))
        {
            if (player.Statuses.Any(s => s.Id is 3054 or 160 or 1209 or 1722 or 2663 or 2345 && s.ExpiresAt > cast.Hit))
                continue;
            var from = player.Position;
            var template = new PalaceLandingTemplate(cast.SourcePosition, KnockbackDangers(), player.HitboxRadius);
            var to = template.Landing(from);
            if (from == to) continue;
            yield return new(cast.Source, from, to, template.Resolve(PalaceArena.Center, 0, null).Contains(from));
        }
    }

    internal IEnumerable<PalaceArea> KnockbackMarkers(DateTime now, ReplayActorState player)
    {
        foreach (var projection in Knockbacks(player))
            yield return new($"kb-landing/{projection.Source}", "击退落点", actorMarker, projection.To, player.Rotation,
                now, DateTime.MaxValue, projection.Source, PreviewOnly: true, Color: new(1, 1, 0, 1), Unclipped: true);
    }
}

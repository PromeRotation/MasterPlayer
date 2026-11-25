using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsHeavenOnHigh => HeavenOnHighCatalog.IsTerritory(territory);
    private bool heavenOnHighEnabled = true;
    private readonly List<HeavenOnHighKnockback> heavenOnHighKnockbacks = [];
    internal IReadOnlyList<HeavenOnHighKnockback> HeavenOnHighKnockbacks => heavenOnHighKnockbacks;

    private void HeavenOnHighCast(DungeonPendingCast cast, bool correction)
    {
        if (!IsHeavenOnHigh || cast.Rule.Action is not (11877 or 11904)) return;
        var index = correction ? heavenOnHighKnockbacks.FindIndex(k => k.Cast.Source == cast.Source) : -1;
        if (index >= 0)
            heavenOnHighKnockbacks[index] = heavenOnHighKnockbacks[index] with { Cast = cast, Origin = cast.Location };
        else if (!correction)
            heavenOnHighKnockbacks.Add(new(cast, cast.Location));
    }

    private void HeavenOnHighFinish(DungeonPendingCast cast)
    {
        HeavenOnHighSpawnFinish(cast);
        if (!IsHeavenOnHigh || cast.Rule.Action is not (11877 or 11904)) return;
        var index = heavenOnHighKnockbacks.FindIndex(k => k.Cast.Source == cast.Source);
        if (index >= 0) heavenOnHighKnockbacks.RemoveAt(index);
    }

    internal IEnumerable<(HeavenOnHighKnockback Source, Vector3 From, Vector3 To)> HeavenOnHighMovements(DungeonActor player)
    {
        var from = player.Position;
        foreach (var knockback in heavenOnHighKnockbacks)
        {
            var delta = from - knockback.Origin;
            delta.Y = 0;
            if (delta.LengthSquared() == 0) continue;
            var direction = Vector3.Normalize(delta);
            // SimpleKnockbacks deliberately ignores all immunity statuses here.
            var to = TreasureWallLanding(from, direction, knockback.Distance, player.HitboxRadius);
            yield return (knockback, from, to);
            from = to;
        }
    }

    private IEnumerable<DungeonAmbientAoe> HeavenOnHighAoes()
    {
        if (!IsHeavenOnHigh || !heavenOnHighEnabled) yield break;
        var primary = _actors.GetValueOrDefault(PrimaryId)
            ?? _actors.Values.FirstOrDefault(a => HeavenOnHighCatalog.Owns(territory, a.Oid));
        if (primary == null || _actors.GetValueOrDefault(PlayerId) is not { } player
            || Vector3.DistanceSquared(primary.Position, player.Position) > 500 * 500) yield break;
        var boundary = Arena ?? HeavenOnHighCatalog.Arena(territory, primary.Position.Y);
        if (Active == null)
            yield return new("hoh/precombat/arena", "场地", new LocalPolygonTemplate(
                boundary.GetContours()[0].Select(p => GeometryHelper.WorldToLocal(p, boundary.Position, 0))),
                boundary.Position, Navigate: false, OutlineOnly: true);
        if (!primary.Dead && primary.Targetable)
        {
            var outside = !boundary.Contains(primary.Position, 0);
            yield return new($"hoh/enemy/{primary.Id}", "首领及朝向",
                new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]),
                outside ? TtokrroneMarkerPosition(boundary, primary.Position) : primary.Position,
                primary.Rotation, Navigate: false, OutlineOnly: outside, Unclipped: true, Color: new(1, 0, 0, 1));
        }
        if (Active == null) yield break;
        foreach (var aoe in HeavenOnHighSpawnAreas()) yield return aoe;
        foreach (var aoe in KenkoAoes()) yield return aoe;
        foreach (var actor in _actors.Values.Where(a => territory == HeavenOnHighCatalog.FOURTH
            ? a.Oid == 0x23E3 && a.EventState != 7 : territory == HeavenOnHighCatalog.FIFTH && a.Oid == 0x23EA))
            yield return new($"hoh/voidzone/{actor.Id}", "持续危险地板",
                new CircleTemplate(territory == HeavenOnHighCatalog.FOURTH ? 6 : 8), actor.Position, Obstacle: true);
        var index = 0;
        foreach (var movement in HeavenOnHighMovements(player))
            foreach (var aoe in QuestKnockbackProjection($"hoh/kb/{movement.Source.Cast.Source}/{index++}",
                player, movement.From, movement.To))
                yield return aoe;
    }
}

using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonDamagePrediction> InnocenceDamage()
    {
        if (!IsInnocence) yield break;
        foreach (var action in new[] { 16035u, 16069u })
        {
            var baits = innocenceBaits.Where(b => b.Action == action).ToArray();
            if (baits.Length == 0) continue;
            foreach (var target in baits.Where(b => !b.Source.Dead).Select(b => b.Target).DistinctBy(t => t.Id).Where(t => t.PartySlot >= 0))
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, baits[0].At, "Raidwide");
        }
        if (innocenceBaits.Where(b => b.Action == 16070 && !b.Source.Dead).MinBy(b => b.At) is { } first)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, first.At, "Shared");
        if (innocenceTowers.Count > 0)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && innocenceTowers.Any(t => new CircleGeometry(t.Position, 5).Contains(a.Position))))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, default, "Raidwide");
    }

    private IEnumerable<string> InnocenceHints()
    {
        if (!IsInnocence || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (innocenceBaits.Any(b => b.Action == 16035)) yield return "正义之雷死刑";
        foreach (var bait in innocenceBaits.Where(b => b.Action != 16070 && !b.Source.Dead))
        {
            var shape = new CircleGeometry(bait.Target.Position, bait.Action == 16035 ? 3 : 10);
            var own = bait.Target.Id == PlayerId && !bait.TargetDestroyed;
            if (own ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))
                : shape.Contains(player.Position)) yield return own ? "引导远离队友" : "远离引导范围";
        }
        var lines = innocenceBaits.Where(b => b.Action == 16070 && !b.Source.Dead).ToArray();
        if (lines.Length > 0 && !lines.Any(l => l.Target.Id == PlayerId && !l.TargetDestroyed)
            && !lines.Any(l => RayRectangleTemplate.Symmetric(6, 40)
                .Resolve(l.Source.Position, InnocenceAngle(l), null).Contains(player.Position)))
            yield return "进入光柱直线分摊";
        if (lines.Any(l => l.Target.Id == PlayerId && !l.TargetDestroyed)
            && lines.Any(l => (l.Target.Id != PlayerId || l.TargetDestroyed)
                && RayRectangleTemplate.Symmetric(6, 40).Resolve(l.Source.Position, InnocenceAngle(l), null).Contains(player.Position)))
            yield return "远离其他光柱分摊";
        var occupied = innocenceTowers.FirstOrDefault(t => new CircleGeometry(t.Position, 5).Contains(player.Position));
        if (occupied.Key != 0)
        {
            var shape = new CircleGeometry(occupied.Position, 5);
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && shape.Contains(a.Position));
            if (count > 2) yield return "罪球塔超员";
        }
        else if (innocenceTowers.Any(t => _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead
            && new CircleGeometry(t.Position, 5).Contains(a.Position)) < 2))
            yield return "进入罪球塔";
    }
}

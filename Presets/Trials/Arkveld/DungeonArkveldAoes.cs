using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ArkAoes()
    {
        if (!IsArkveld || Active == null || Arena == null) yield break;
        if (_actors.GetValueOrDefault(PrimaryId) is { Dead: false, Targetable: true } boss)
            yield return new("ark/enemy", "护锁刃龙", new CircleTemplate(boss.HitboxRadius),
                boss.Position, Navigate: false, OutlineOnly: true);
        foreach (var aoe in arkCharges.Values) yield return aoe;
        foreach (var line in arkVengeance)
            yield return new($"ark/vengeance/{line.Key}", "龙之复仇连续圆", new CircleTemplate(6),
                QuantizedDungeonPosition(line.Position), Activation: line.At, Color: new Vector4(1, 1, 0, 1));
        foreach (var aoe in arkPulses) yield return aoe;
        if (arkLane != null && _now < arkLane.Activation) yield return arkLane;
        foreach (var aoe in ArkTowerAoes()) yield return aoe;
        foreach (var aoe in ArkPartyAoes()) yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> ArkDamage()
    {
        if (!IsArkveld || Active == null) yield break;
        foreach (var action in new[] { 43853u, 43854u })
        {
            var towers = arkTowers.Where(t => t.Rule.Action == action).ToArray();
            if (towers.Length == 0) continue;
            var radius = action == 43853 ? 2 : 4;
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && (action == 43853 || a.PartyRole == "Tank")
                && towers.Any(t => new CircleGeometry(t.Location, radius).Contains(a.Position))))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, towers[0].FinishAt,
                    action == 43853 ? "Raidwide" : "Tankbuster");
        }
    }

    private IEnumerable<string> ArkHints()
    {
        if (!IsArkveld || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (arkVengeance.Count > 0) yield return "避开龙之复仇连续圆";
        if (arkLane != null && _now < arkLane.Activation) yield return "龙之恩惠：避开扫射一侧";
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var action in new[] { 43853u, 43854u })
        {
            var towers = arkTowers.Where(t => t.Rule.Action == action).ToArray();
            var radius = action == 43853 ? 2 : 4;
            var allowed = action == 43853 || player.PartyRole == "Tank";
            bool TowerInside(DungeonPendingCast t) => new CircleGeometry(t.Location, radius).Contains(player.Position);
            int Count(DungeonPendingCast t) => party.Count(a => (action == 43853 || a.PartyRole == "Tank")
                && new CircleGeometry(t.Location, radius).Contains(a.Position));
            if (!allowed)
            {
                if (towers.Any(TowerInside)) yield return "非坦克离开大塔";
            }
            else if (towers.FirstOrDefault(TowerInside) is { } soaked)
            {
                if (Count(soaked) != 1) yield return "每塔一人";
            }
            else if (towers.Any(t => Count(t) < 1)) yield return "每塔一人";
        }
        var spreads = arkSpreads.Where(s => !s.Target.Actor.Dead).ToArray();
        foreach (var spread in spreads)
        {
            var position = QuantizedDungeonPosition(spread.Target.Actor.Position);
            if (spread.Target == arkActors.GetValueOrDefault(PlayerId)
                ? party.Any(a => a.Id != PlayerId && Inside(a.Position, position, 6))
                : Inside(player.Position, position, 6)) yield return "狂野能量：分散";
        }
        var stacks = arkStacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var own = stacks.Where(s => s.Target == arkActors.GetValueOrDefault(PlayerId)).ToArray();
        if (own.Length > 0)
        {
            var position = QuantizedDungeonPosition(own[0].Target.Actor.Position);
            var count = party.Count(a => Inside(a.Position, position, 6));
            if (count != 8) yield return $"八人分摊人数不足或超员：{count}/8";
            if (party.Any(a => a.Id != PlayerId && Inside(a.Position, position, 6)
                && stacks.Any(s => s.Target == arkActors.GetValueOrDefault(a.Id)))) yield return "分摊点名重叠";
        }
        else
        {
            var count = stacks.Count(s => Inside(player.Position, QuantizedDungeonPosition(s.Target.Actor.Position), 6));
            if (count > 1) yield return "同时位于多个分摊圈";
            if (count == 0 && stacks.Any(s => 1 + party.Count(a => arkActors.GetValueOrDefault(a.Id) != s.Target
                && Inside(a.Position, QuantizedDungeonPosition(s.Target.Actor.Position), 6)) < 8)) yield return "进入人数不足的分摊圈";
        }
        static bool Inside(Vector3 a, Vector3 b, float radius) =>
            Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z)) <= radius * radius;
    }
}

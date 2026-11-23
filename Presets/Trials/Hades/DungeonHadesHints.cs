using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonDamagePrediction> HadesDamage()
    {
        if (!IsHades) yield break;
        foreach (var group in hadesBaits.GroupBy(b => b.Action))
        {
            foreach (var target in group.Where(b => !b.Source.Dead).Select(b => b.Target).DistinctBy(t => t.Id).Where(t => t.PartySlot >= 0))
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, group.First().At,
                    group.Key is 16738 or 16755 ? "Tankbuster" : "Raidwide");
        }
        if (hadesTowers.Count > 0)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && hadesTowers.Any(t => new CircleGeometry(t.Position, 5).Contains(a.Position))))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, default, "Raidwide");
    }

    private IEnumerable<string> HadesHints()
    {
        if (!IsHades || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (HadesGaolAlive) yield return "攻击以太牢狱，暂勿攻击巨大哈迪斯";
        if (hadesBaits.Any(b => b.Action is 16728 or 16751)) yield return "范围死刑";
        foreach (var aoe in HadesAoes().Where(a => a.Display && a.Id.StartsWith("hades/bait/") && a.Id.EndsWith("/display")))
        {
            var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            if (aoe.OutlineOnly ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))
                : shape.Contains(player.Position)) yield return aoe.OutlineOnly ? "引导远离队友" : "远离引导范围";
        }
        var stacks = hadesStacks.Where(s => !s.Target.Dead).ToArray();
        foreach (var stack in stacks)
        {
            var shape = new CircleGeometry(QuantizedDungeonPosition(stack.Target.Position), 6);
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && shape.Contains(a.Position));
            var own = stack.Target.Id == PlayerId && !stack.Destroyed;
            if (own ? count is < 2 or > 8 || stacks.Any(s => s.Key != stack.Key && shape.Contains(s.Target.Position))
                : !shape.Contains(player.Position) && count < 2)
                yield return "至少两人分摊并分开点名";
        }
        var occupied = hadesTowers.FirstOrDefault(t => new CircleGeometry(t.Position, 5).Contains(player.Position));
        if (occupied.Key != 0)
        {
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && new CircleGeometry(occupied.Position, 5).Contains(a.Position));
            if (count > 2) yield return "死亡宣告塔超员";
        }
        else if (hadesTowers.Any(t => _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead
            && new CircleGeometry(t.Position, 5).Contains(a.Position)) < 2))
            yield return "进入死亡宣告塔";
    }
}

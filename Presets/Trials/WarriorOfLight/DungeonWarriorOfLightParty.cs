using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> WolPartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var marks = wolMarks.Where(m => !m.Target.Dead).ToArray();
        foreach (var spread in marks.Where(m => m.Action == 20251))
        {
            var own = spread.Target.Id == PlayerId && !spread.Destroyed;
            var id = $"wol/spread/{spread.Key}";
            yield return new(id + "/display", "光之陨石分散", new CircleTemplate(20), QuantizedDungeonPosition(spread.Target.Position),
                Navigate: false, OutlineOnly: true);
            if (!own)
                yield return new(id + "/avoid", "远离陨石", new CircleTemplate(21), QuantizedDungeonPosition(spread.Target.Position),
                    Display: false, Activation: spread.At);
            else
                foreach (var buddy in party.Where(a => a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "陨石点名远离队友", new CircleTemplate(21), QuantizedDungeonPosition(buddy.Position),
                        Display: false, Activation: spread.At);
        }
        foreach (var action in new[] { 20237u, 20253u })
        {
            var stacks = marks.Where(m => m.Action == action).ToArray();
            var joins = new List<IGeometryShape>();
            var marked = stacks.Any(m => m.Target.Id == PlayerId && !m.Destroyed);
            foreach (var stack in stacks)
            {
                var own = stack.Target.Id == PlayerId && !stack.Destroyed;
                var position = QuantizedDungeonPosition(stack.Target.Position);
                var shape = new CircleGeometry(position, 6);
                var inside = _actors.GetValueOrDefault(PlayerId) is { } player && shape.Contains(player.Position);
                var count = party.Count(a => a.Id != stack.ForbiddenPlayer && shape.Contains(a.Position));
                var max = action == 20253 ? 8 : int.MaxValue;
                var join = stack.ForbiddenPlayer != PlayerId && (inside ? count <= max : count < max);
                var id = $"wol/stack/{action}/{stack.Key}";
                yield return new(id + "/display", action == 20237 ? "绝对神圣至少六人分摊" : "火遁六至八人分摊",
                    new CircleTemplate(6), position, Navigate: false,
                    Safe: stack.ForbiddenPlayer != PlayerId && (own || !marked && join), OutlineOnly: true);
                if (party.Length < 2) continue;
                if (own)
                {
                    var buddies = party.Where(a => a.Id != PlayerId && a.Id != stack.ForbiddenPlayer
                        && !stacks.Any(s => s.Target.Id == a.Id && !s.Destroyed))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                    if (buddies.Length > 0)
                        yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                            Arena.Position, Display: false, Activation: stack.At);
                }
                else if (join) joins.Add(shape);
                else
                    yield return new(id + "/avoid", "避开禁入或满员分摊", new CircleTemplate(marked ? 12 : 6), position,
                        Display: false, Activation: stack.At);
            }
            if (joins.Count > 0)
                yield return new($"wol/stack/{action}/join", "进入六人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                    Arena.Position, Display: false, Activation: stacks[0].At);
        }
    }

    private IEnumerable<DungeonDamagePrediction> WolDamage()
    {
        if (!IsWarriorOfLight || Active == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var group in wolBaits.Where(b => b.Action != 20248).GroupBy(b => b.Action))
            foreach (var target in group.Where(b => !b.Source.Dead).Select(b => b.Target).DistinctBy(t => t.Id).Where(t => t.PartySlot >= 0))
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, group.First().At,
                    group.Key == 20260 ? "Tankbuster" : "Raidwide");
        var lines = wolBaits.Where(b => b.Action == 20248 && !b.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var actor in party)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, lines.Min(b => b.At), "Shared");
        var holy = wolMarks.Where(m => m.Action == 20237 && !m.Target.Dead).ToArray();
        // 源仅在合并后的 mask 不等于全员 mask 时发布神圣 Shared 预测。
        if (holy.Length > 0 && holy.All(m => m.ForbiddenPlayer != 0))
            foreach (var actor in party.Where(a => holy.Any(m => m.ForbiddenPlayer != a.Id)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, holy.Min(m => m.At), "Shared");
        if (wolTowers.Count > 0)
        {
            var tower = wolTowers.MinBy(t => t.At);
            var geometry = new CircleGeometry(tower.Position, tower.Radius);
            foreach (var actor in party.Where(a => geometry.Contains(a.Position)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, tower.At, "Raidwide");
        }
    }

    private IEnumerable<string> WolHints()
    {
        if (!IsWarriorOfLight || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (RequireWolStop) yield return "热病：停手、停读条、停止自动攻击并保持不动";
        if (RequireWolMovement) yield return "冰判定：保持移动";
        if (_casts.Values.Any(c => c.Rule.Action == 21628)) yield return "突破极限提示时使用坦克三级极限技";
        if (wolBaits.Any(b => b.Action == 20264)) yield return "范围死刑";
        if (Arena != null && WolKnockbackPaths().Any(k => !Arena.Contains(k.To))) yield return "水遁击退落点将出界";
        foreach (var aoe in WolAoes().Where(a => a.Display && a.Id.StartsWith("wol/bait/") && a.Id.EndsWith("/display")))
        {
            var geometry = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            if (aoe.OutlineOnly ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && geometry.Contains(a.Position))
                : geometry.Contains(player.Position)) yield return aoe.OutlineOnly ? "引导远离队友" : "远离引导范围";
        }
        foreach (var stack in wolMarks.Where(m => m.Action is 20237 or 20253 && !m.Target.Dead))
        {
            var geometry = new CircleGeometry(QuantizedDungeonPosition(stack.Target.Position), 6);
            var own = stack.Target.Id == PlayerId && !stack.Destroyed;
            if (stack.ForbiddenPlayer == PlayerId)
            {
                if (geometry.Contains(player.Position)) yield return "距离衰减点名勿参与分摊";
                continue;
            }
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && a.Id != stack.ForbiddenPlayer && geometry.Contains(a.Position));
            if (own ? count < 6 || stack.Action == 20253 && count > 8
                || wolMarks.Any(m => m.Action == stack.Action && m.Key != stack.Key && !m.Target.Dead && geometry.Contains(m.Target.Position))
                : !geometry.Contains(player.Position) && count < 6)
                yield return "至少六人分摊并分开点名";
        }
        var lines = wolBaits.Where(b => b.Action == 20248 && !b.Source.Dead).ToArray();
        if (lines.Length > 0)
        {
            var marked = lines.Any(WolOwn);
            var insideOther = lines.Any(b => !WolOwn(b) && RayRectangleTemplate.Symmetric(8, 60)
                .Resolve(b.Source.Position, WolAngle(b), null).Contains(player.Position));
            if (marked && insideOther) yield return "直线分摊点名互避";
            else if (!marked && !insideOther) yield return "进入光之亡命徒分摊";
        }
        if (wolTowers.Count > 0)
        {
            var t = wolTowers.MinBy(t => t.At);
            var shape = new CircleGeometry(t.Position, t.Radius);
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && shape.Contains(a.Position));
            if (shape.Contains(player.Position) && count > t.Max) yield return "双人塔超员";
            else if (!shape.Contains(player.Position) && count < t.Max) yield return "按顺序进入最早双人塔";
        }
    }
}

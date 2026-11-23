using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ArkPartyAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = arkSpreads.Where(m => !m.Target.Actor.Dead).ToArray();
        foreach (var spread in spreads)
        {
            var own = spread.Target == arkActors.GetValueOrDefault(PlayerId);
            var id = $"ark/spread/{spread.Key}";
            yield return new(id + "/display", "狂野能量分散", new CircleTemplate(6),
                QuantizedDungeonPosition(spread.Target.Actor.Position), Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (!own)
                yield return new(id + "/avoid", "避开狂野能量", new CircleTemplate(7),
                    QuantizedDungeonPosition(spread.Target.Actor.Position), Display: false, Activation: spread.At);
            else
                foreach (var buddy in party.Where(a => !spreads.Any(s => s.Target == arkActors.GetValueOrDefault(a.Id))))
                    yield return new(id + $"/buddy/{buddy.Id}", "分散远离队友", new CircleTemplate(7),
                        QuantizedDungeonPosition(buddy.Position), Display: false, Activation: spread.At);
        }
        var stacks = arkStacks.Where(m => !m.Target.Actor.Dead).ToArray();
        var marked = stacks.Any(m => m.Target == arkActors.GetValueOrDefault(PlayerId));
        var joins = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var own = stack.Target == arkActors.GetValueOrDefault(PlayerId);
            var position = QuantizedDungeonPosition(stack.Target.Actor.Position);
            var circle = new CircleGeometry(position, 6);
            var count = party.Count(a => circle.Contains(a.Position));
            var join = circle.Contains(player.Position) ? count <= 8 : count < 8;
            var id = $"ark/stack/{stack.Key}";
            yield return new(id + "/display", "锁刃突击八人分摊", new CircleTemplate(6), position,
                Navigate: false, Safe: own || !marked && join, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (own)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target == arkActors.GetValueOrDefault(a.Id)))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: stack.At);
            }
            else if (join) joins.Add(circle);
            else
                yield return new(id + "/avoid", "避开满员分摊", new CircleTemplate(marked ? 12 : 6), position, Display: false, Activation: stack.At);
        }
        if (joins.Count > 0)
            yield return new("ark/stack/join", "进入八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }

    private IEnumerable<DungeonAmbientAoe> ArkTowerAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        foreach (var action in new[] { 43853u, 43854u })
        {
            var towers = arkTowers.Where(t => t.Rule.Action == action).ToArray();
            var joins = new List<IGeometryShape>();
            var avoids = new List<DungeonPendingCast>();
            var radius = action == 43853 ? 2 : 4;
            var inCorrect = false;
            foreach (var tower in towers)
            {
                var circle = new CircleGeometry(tower.Location, radius);
                var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && (action == 43853 || a.PartyRole == "Tank") && circle.Contains(a.Position));
                var inside = circle.Contains(player.Position);
                var allowed = action == 43853 || player.PartyRole == "Tank";
                var safe = allowed && (count < 1 || inside && count == 1);
                yield return new($"ark/tower/{tower.Source}/display", action == 43853 ? "单人小塔" : "坦克单人大塔",
                    new CircleTemplate(radius), tower.Location, Navigate: false, Safe: safe, OutlineOnly: allowed && (safe || inside));
                if (inside && count == 1) inCorrect = true;
                if (safe) joins.Add(circle);
                else avoids.Add(tower);
                if (action == 43854 && !allowed)
                    yield return new($"ark/tower/{tower.Source}/nontank", "非坦克禁止进入大塔", new CircleTemplate(radius),
                        tower.Location, Display: false, Activation: tower.FinishAt);
            }
            if (joins.Count > 0)
                yield return new($"ark/tower/{action}/join", "进入空塔", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                    Arena.Position, Display: false, Activation: towers[0].FinishAt);
            else if (!inCorrect)
                foreach (var tower in avoids)
                    yield return new($"ark/tower/{tower.Source}/avoid", "离开不可参与的塔", new CircleTemplate(radius),
                        tower.Location, Display: false, Activation: towers[0].FinishAt);
        }
    }
}

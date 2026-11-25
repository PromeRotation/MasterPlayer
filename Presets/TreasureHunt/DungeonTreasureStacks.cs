using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TreasureEightStacks()
    {
        if (!HasTreasureEightStack || Arena == null) yield break;
        var stacks = jobQuestStacks.Where(s => !s.Target.Dead).ToArray();
        var party = treasureParty.Select(id => _actors.GetValueOrDefault(id)).OfType<DungeonActor>().Where(a => !a.Dead).ToArray();
        var marked = stacks.Any(s => s.Target.Id == PlayerId && !s.Removed);
        var viewerHasStoredStack = jobQuestStacks.Any(s => s.Target.Id == PlayerId && !s.Removed);
        var joins = new List<IGeometryShape>();
        foreach (var (mark, index) in stacks.Select((s, i) => (s, i)))
        {
            var position = QuantizedDungeonPosition(mark.Target.Position);
            var circle = new CircleGeometry(position, 6);
            var count = party.Count(a => circle.Contains(a.Position, 0));
            var inside = _actors.GetValueOrDefault(PlayerId) is { } player && circle.Contains(player.Position, 0);
            var canJoin = inside ? count <= 8 : count < 8;
            var self = mark.Target.Id == PlayerId && !mark.Removed;
            yield return new($"treasure/stack/{index}/display", $"{mark.Cast.Rule.Name} / 恰好8人分摊", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true, Safe: self || !viewerHasStoredStack && canJoin);
            if (party.Length < 2) continue;
            if (self)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => !s.Removed && s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"treasure/stack/{index}/buddy", "分摊目标靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: mark.Cast.FinishAt);
            }
            else if (canJoin)
                joins.Add(circle);
            else
                yield return new($"treasure/stack/{index}/avoid", "避免分摊超员或重叠", new CircleTemplate(marked ? 12 : 6), position,
                    Display: false, Activation: mark.Cast.FinishAt);
        }
        if (joins.Count > 0)
            yield return new("treasure/stack/join", "进入任一八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
    }

    private IEnumerable<string> TreasureEightStackHints(Vector3 player)
    {
        var stacks = jobQuestStacks.Where(s => !s.Target.Dead).ToArray();
        var party = treasureParty.Select(id => _actors.GetValueOrDefault(id)).OfType<DungeonActor>().Where(a => !a.Dead).ToArray();
        foreach (var mark in stacks.Where(s => !s.Removed && s.Target.Id == PlayerId))
        {
            var circle = new CircleGeometry(QuantizedDungeonPosition(mark.Target.Position), 6);
            var count = 1 + party.Count(a => a.Id != PlayerId && circle.Contains(a.Position, 0));
            var overlap = party.Any(a => a.Id != PlayerId && circle.Contains(a.Position, 0)
                && stacks.Any(s => !s.Removed && s.Target.Id == a.Id));
            yield return overlap || count != 8 ? $"Stack! ({count}/8)" : "Stack (8/8)";
            yield break;
        }
        var participating = 0;
        var unsatisfied = false;
        foreach (var mark in stacks)
        {
            var circle = new CircleGeometry(QuantizedDungeonPosition(mark.Target.Position), 6);
            if (circle.Contains(player, 0))
                ++participating;
            else if (1 + party.Count(a => (a.Id != mark.Target.Id || mark.Removed) && circle.Contains(a.Position, 0)) < 8)
                unsatisfied = true;
        }
        if (participating > 1 || participating == 0 && unsatisfied) yield return "Stack!";
        else if (participating == 1) yield return "Stack";
    }
}

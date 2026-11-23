using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ZeleniaPartyAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var marks = zeleniaMarks.Where(m => !m.Target.Actor.Dead).ToArray();
        foreach (var spread in marks.Where(m => m.Action == 581))
        {
            var own = spread.Target == zeleniaActors.GetValueOrDefault(PlayerId);
            var id = $"zelenia/spread/{spread.Key}";
            yield return new(id + "/display", "震击分散", new CircleTemplate(4),
                QuantizedDungeonPosition(spread.Target.Actor.Position), Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (!own)
                yield return new(id + "/avoid", "避开震击点名", new CircleTemplate(5),
                    QuantizedDungeonPosition(spread.Target.Actor.Position), Display: false, Activation: spread.At);
            else
                foreach (var buddy in party.Where(a => !marks.Any(m => m.Action == 581 && m.Target == zeleniaActors.GetValueOrDefault(a.Id))))
                    yield return new(id + $"/buddy/{buddy.Id}", "分散远离队友", new CircleTemplate(5),
                        QuantizedDungeonPosition(buddy.Position), Display: false, Activation: spread.At);
        }
        var stacks = marks.Where(m => m.Action == 590).ToArray();
        var marked = stacks.Any(m => m.Target == zeleniaActors.GetValueOrDefault(PlayerId));
        var joins = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var own = stack.Target == zeleniaActors.GetValueOrDefault(PlayerId);
            var position = QuantizedDungeonPosition(stack.Target.Actor.Position);
            var circle = new CircleGeometry(position, 6);
            var count = party.Count(a => circle.Contains(a.Position));
            var join = circle.Contains(player.Position) ? count <= 8 : count < 8;
            var id = $"zelenia/stack/{stack.Key}";
            yield return new(id + "/display", "四连八人分摊", new CircleTemplate(6), position,
                Navigate: false, Safe: own || !marked && join, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (own)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target == zeleniaActors.GetValueOrDefault(a.Id)))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: stack.At);
            }
            else if (join) joins.Add(circle);
            else
                yield return new(id + "/avoid", "避开满员分摊", new CircleTemplate(marked ? 12 : 6), position,
                    Display: false, Activation: stack.At);
        }
        if (joins.Count > 0)
            yield return new("zelenia/stack/join", "进入八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }
}

using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> EnuoPartyAoes()
    {
        if (Arena == null) yield break;
        var own = enuoActors.GetValueOrDefault(PlayerId);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var spreads = enuoSpreads.Where(s => !s.Target.Actor.Dead).ToArray();
        foreach (var spread in spreads)
        {
            var position = QuantizedDungeonPosition(spread.Target.Actor.Position);
            var id = $"enuo/spread/{spread.Key}";
            yield return new(id + "/display", "熔毁分散", new CircleTemplate(5), position, Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (spread.Target != own)
                yield return new(id + "/avoid", "避开熔毁分散", new CircleTemplate(6), position, Display: false, Activation: spread.At);
            else
                foreach (var buddy in party.Where(a => !spreads.Any(s => s.Target == enuoActors.GetValueOrDefault(a.Id))))
                    yield return new(id + $"/buddy/{buddy.Id}", "分散远离队友", new CircleTemplate(6),
                        QuantizedDungeonPosition(buddy.Position), Display: false, Activation: spread.At);
        }
        var stacks = enuoStacks.Where(s => !s.Target.Actor.Dead).ToArray();
        var marked = stacks.Any(s => s.Target == own);
        var joins = new List<IGeometryShape>();
        foreach (var stack in stacks)
        {
            var position = QuantizedDungeonPosition(stack.Target.Actor.Position);
            var id = $"enuo/stack/{stack.Key}";
            yield return new(id + "/display", "神圣四人分摊", new CircleTemplate(7), position,
                Navigate: false, OutlineOnly: true, Safe: stack.Target == own || !marked);
            if (party.Length < 2) continue;
            if (stack.Target == own)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target == enuoActors.GetValueOrDefault(a.Id)))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3.5f)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: stack.At);
            }
            else joins.Add(new CircleGeometry(position, 7));
        }
        if (joins.Count > 0)
            yield return new("enuo/stack/join", "进入神圣分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }

    private IEnumerable<DungeonAmbientAoe> EnuoLineAoes()
    {
        if (Arena == null) yield break;
        var lines = enuoLines.Where(b => !b.Source.Actor.Dead).ToArray();
        var own = enuoActors.GetValueOrDefault(PlayerId);
        var marked = lines.Any(l => l.Target == own);
        var joins = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var source = line.Source.Actor; var target = line.Target.Actor;
            var angle = source.Id == target.Id ? source.Rotation
                : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var shape = RayRectangleTemplate.Symmetric(8, 60);
            var id = $"enuo/line/{line.Key}";
            yield return new(id + "/display", "零式维度直线分摊", shape, source.Position, angle,
                Navigate: false, Safe: line.Target == own || !marked);
            if (!marked) joins.Add(shape.Resolve(source.Position, angle, null));
            else if (line.Target != own)
                yield return new(id + "/avoid", "直线分摊互不重叠", RayRectangleTemplate.Symmetric(16, 60),
                    source.Position, angle, Display: false, Activation: line.At);
            else
            {
                var buddies = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && !enuoLines.Any(l => l.Target == enuoActors.GetValueOrDefault(a.Id)))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "直线分摊靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (joins.Count > 0)
            yield return new("enuo/line/join", "进入直线分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }
}

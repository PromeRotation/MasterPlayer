using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TitaniaMarkAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var group in titaniaMarks.Where(m => !m.Target.Dead).GroupBy(m => m.Cast?.Rule.Action ?? 17267u))
        {
            var marks = group.ToArray();
            var stack = group.Key is 15703 or 17267;
            var radius = group.Key switch { 16927 => 8, 17267 => 5, _ => 6 };
            var max = group.Key == 17267 ? 8 : int.MaxValue;
            var marked = marks.Any(m => m.Target.Id == PlayerId && !m.Destroyed);
            var joins = new List<IGeometryShape>();
            foreach (var mark in marks)
            {
                var position = QuantizedDungeonPosition(mark.Target.Position);
                var own = mark.Target.Id == PlayerId && !mark.Destroyed;
                var circle = new CircleGeometry(position, radius);
                var count = party.Count(a => circle.Contains(a.Position));
                var inside = _actors.GetValueOrDefault(PlayerId) is { } player && circle.Contains(player.Position);
                var canJoin = inside ? count <= max : count < max;
                var id = $"titania/mark/{group.Key}/{mark.Key}";
                yield return new(id + "/display", stack ? "符文分摊" : "符文分散", new CircleTemplate(radius), position,
                    Navigate: false, OutlineOnly: true, Safe: stack && (own || !marked && canJoin));
                if (party.Length < 2) continue;
                if (!stack)
                {
                    if (!own)
                        yield return new(id + "/avoid", "远离分散目标", new CircleTemplate(radius + 1), position,
                            Display: false, Activation: mark.At);
                    else
                        foreach (var buddy in party.Where(a => !marks.Any(m => m.Target.Id == a.Id && !m.Destroyed)))
                            yield return new(id + $"/buddy/{buddy.Id}", "分散远离队友", new CircleTemplate(radius + 1),
                                QuantizedDungeonPosition(buddy.Position), Display: false, Activation: mark.At);
                }
                else if (own)
                {
                    var buddies = party.Where(a => a.Id != PlayerId && !marks.Any(m => m.Target.Id == a.Id && !m.Destroyed))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, radius * .5f)).ToArray();
                    if (buddies.Length > 0)
                        yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                            Arena.Position, Display: false, Activation: mark.At);
                }
                else if (canJoin) joins.Add(circle);
                else
                    yield return new(id + "/avoid", "避免分摊超员", new CircleTemplate(marked ? radius * 2 : radius),
                        position, Display: false, Activation: mark.At);
            }
            if (joins.Count > 0)
                yield return new($"titania/mark/{group.Key}/join", "进入符文分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                    Arena.Position, Display: false, Activation: marks[0].At);
        }
    }

    private IEnumerable<DungeonAmbientAoe> TitaniaTowerAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var joins = new List<IGeometryShape>();
        var avoids = new List<(int Key, Vector3 Position)>();
        var inCorrect = false;
        foreach (var tower in titaniaTowers)
        {
            var shape = new CircleGeometry(tower.Position, 5);
            var inside = shape.Contains(player.Position);
            var count = party.Count(a => shape.Contains(a.Position));
            var correct = count is 1 or 2;
            yield return new($"titania/tower/{tower.Key}/display", "水塔一至两人", new CircleTemplate(5), tower.Position,
                Navigate: false, Safe: count < 2 || inside && correct, OutlineOnly: count < 2 || inside);
            if (inside && correct) inCorrect = true;
            if (count < 2 || inside && correct) joins.Add(shape);
            else avoids.Add((tower.Key, tower.Position));
        }
        if (joins.Count > 0)
            yield return new("titania/tower/join", "进入可参与水塔", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false);
        else if (!inCorrect)
            foreach (var tower in avoids)
                yield return new($"titania/tower/{tower.Key}/avoid", "远离满员水塔", new CircleTemplate(5), tower.Position, Display: false);
    }
}

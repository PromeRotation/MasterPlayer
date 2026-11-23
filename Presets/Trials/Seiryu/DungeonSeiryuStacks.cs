using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool SeiryuForbidden(SeiryuBait line, int slot) => slot is >= 0 and < 64 && (line.Forbidden & (1UL << slot)) != 0;
    private static RayRectangleTemplate SeiryuLineShape(uint action, bool doubled = false)
        => RayRectangleTemplate.Symmetric((action == 15388 ? 5 : 8) * (doubled ? 2 : 1), action == 15388 ? 83 : 84.4f);

    private IEnumerable<DungeonAmbientAoe> SeiryuLineAoes()
    {
        if (Arena == null) yield break;
        var slot = _actors.GetValueOrDefault(PlayerId)?.PartySlot ?? -1;
        foreach (var action in new[] { 15388u, 15474u })
        {
            var lines = seiryuLines.Where(l => l.Action == action && !l.Source.Dead).ToArray();
            var marked = lines.Any(SeiryuOwn);
            var joins = new List<IGeometryShape>();
            foreach (var line in lines)
            {
                var angle = SeiryuAngle(line);
                var shape = SeiryuLineShape(action);
                var own = SeiryuOwn(line);
                var forbidden = SeiryuForbidden(line, slot);
                var id = $"seiryu/line/{action}/{line.Key}";
                yield return new(id + "/display", "直线分摊", shape, line.Source.Position, angle,
                    Navigate: false, Safe: !forbidden && (own || !marked));
                if (!own && !marked)
                {
                    if (!forbidden) joins.Add(shape.Resolve(line.Source.Position, angle, null));
                    else yield return new(id + "/forbidden", "不能参与此直线分摊", shape, line.Source.Position, angle,
                        Display: false, Activation: line.At);
                }
                else if (!own)
                    yield return new(id + "/avoid", "直线分摊点名互避", SeiryuLineShape(action, true),
                        line.Source.Position, angle, Display: false, Activation: line.At);
                else
                {
                    var buddies = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                        && !SeiryuForbidden(line, a.PartySlot)
                        && !seiryuLines.Any(l => l.Action == action && l.Target.Id == a.Id && !l.TargetDestroyed))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                    if (buddies.Length > 0)
                        yield return new(id + "/buddy", "分摊点名靠近可参与队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                            Arena.Position, Display: false, Activation: line.At);
                }
            }
            if (joins.Count > 0)
                yield return new($"seiryu/line/{action}/join", "进入可参与直线分摊",
                    new SohrKhaiWallHintTemplate(Arena, joins.ToArray()), Arena.Position,
                    Display: false, Activation: lines[0].At);
        }
    }

    private IEnumerable<DungeonAmbientAoe> SeiryuTowerAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var joins = new List<IGeometryShape>();
        var avoids = new List<(string Id, Vector3 Position)>();
        var inCorrect = false;
        foreach (var tower in seiryuTowers)
        {
            var shape = new CircleGeometry(tower.Position, 3);
            var count = party.Count(a => shape.Contains(a.Position));
            var inside = shape.Contains(player.Position);
            var correct = count == 1;
            var id = $"seiryu/tower/{tower.Key}";
            yield return new(id + "/display", "单人塔", new CircleTemplate(3), tower.Position,
                Navigate: false, Safe: count == 0 || inside && correct, OutlineOnly: count == 0 || inside);
            if (inside && correct) inCorrect = true;
            if (count == 0 || inside && correct) joins.Add(shape);
            else avoids.Add((id, tower.Position));
        }
        if (joins.Count > 0)
            yield return new("seiryu/tower/join", "进入未满单人塔", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: seiryuTowers[0].At);
        else if (!inCorrect)
            foreach (var (id, position) in avoids)
                yield return new(id + "/avoid", "远离满员塔", new CircleTemplate(3), position,
                    Display: false, Activation: seiryuTowers[0].At);
    }
}

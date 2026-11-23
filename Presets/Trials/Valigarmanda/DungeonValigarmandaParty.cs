using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static float ValigLineAngle(DungeonActor source, DungeonActor target) =>
        MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
    private static IGeometryShape ValigLineShape(DungeonActor source, DungeonActor target) =>
        RayRectangleTemplate.Symmetric(6, 60).Resolve(source.Position, ValigLineAngle(source, target), null);

    private IEnumerable<DungeonAmbientAoe> ValigLineAoes()
    {
        if (Arena == null) yield break;
        var own = valigActors.GetValueOrDefault(PlayerId);
        foreach (var marker in new[] { 26708u, 34722u })
        {
            var lines = valigLines.Where(l => l.Marker == marker && !l.Source.Actor.Dead).ToArray();
            var marked = lines.Any(l => l.Target == own);
            var joins = new List<IGeometryShape>();
            foreach (var line in lines)
            {
                var source = line.Source.Actor; var target = line.Target.Actor;
                var angle = ValigLineAngle(source, target);
                var id = $"valig/line/{line.Key}";
                yield return new(id + "/display", "灾祸直线分摊", RayRectangleTemplate.Symmetric(6, 60),
                    source.Position, angle, Navigate: false, Safe: line.Target == own || !marked);
                if (!marked) joins.Add(ValigLineShape(source, target));
                else if (line.Target != own)
                    yield return new(id + "/avoid", "分开直线分摊", RayRectangleTemplate.Symmetric(12, 60),
                        source.Position, angle, Display: false, Activation: line.At);
                else
                {
                    var buddies = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                        && !valigLines.Any(l => l.Marker == marker && l.Target == valigActors.GetValueOrDefault(a.Id)))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                    if (buddies.Length > 0)
                        yield return new(id + "/buddy", "直线分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                            Arena.Position, Display: false, Activation: line.At);
                }
            }
            if (joins.Count > 0)
                yield return new($"valig/line/{marker}/join", "进入直线分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                    Arena.Position, Display: false, Activation: lines[0].At);
        }
    }

    private IEnumerable<DungeonAmbientAoe> ValigTowerAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var joins = new List<IGeometryShape>();
        var avoids = new List<DungeonPendingCast>();
        var inCorrect = false;
        foreach (var tower in valigTowers)
        {
            var circle = new CircleGeometry(tower.Location, 6);
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && a.PartyRole == "Tank" && circle.Contains(a.Position));
            var inside = circle.Contains(player.Position);
            var allowed = player.PartyRole == "Tank";
            var safe = allowed && (count < 2 || inside && count == 2);
            if (!allowed || safe || inside || count >= 2)
                yield return new($"valig/tower/{tower.Source}/display", "双坦踩塔", new CircleTemplate(6), tower.Location,
                    Navigate: false, Safe: safe, OutlineOnly: allowed && (safe || inside));
            if (inside && count == 2) inCorrect = true;
            if (safe) joins.Add(circle);
            else avoids.Add(tower);
        }
        if (joins.Count > 0)
            yield return new("valig/tower/join", "坦克进入双人塔", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: valigTowers[0].FinishAt);
        else if (!inCorrect)
            foreach (var tower in avoids)
                yield return new($"valig/tower/{tower.Source}/avoid", "离开不可参与的塔", new CircleTemplate(6),
                    tower.Location, Display: false, Activation: valigTowers[0].FinishAt);
    }
}

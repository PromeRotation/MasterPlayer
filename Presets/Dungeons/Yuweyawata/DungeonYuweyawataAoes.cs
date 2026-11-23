using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> YuweAoes()
    {
        if (territory != YuweyawataCatalog.TERRITORY || Arena == null) yield break;
        if (yuweShrink != null) yield return yuweShrink;
        if (yuweCell != null && yuweVoltage.Count == 0) yield return yuweCell;
        foreach (var a in YuweBatch(yuweVoltage, 1.5)) yield return a;
        foreach (var a in YuweBatch(yuweSoul, 1.3)) yield return a;
        foreach (var group in yuweSimple.GroupBy(c => c.Rule.Action is 40656 or 40657 ? 40656 : c.Rule.Action))
            foreach (var c in group.Take(group.Key == 40656 ? 6 : int.MaxValue))
                yield return new($"yuwe/simple/{c.Source}/{c.StartedAt.Ticks}", c.Rule.Name, c.Rule.Shape!, c.Location, c.Rotation,
                    Navigate: c.Rule.Action != 40605 || _now >= c.FinishAt.AddSeconds(-2.5), Activation: c.FinishAt);
        if (yuweClaw != null) yield return yuweClaw;
        foreach (var a in yuweBoulderAoes.Concat(yuweEarth.Take(yuweEarthShown)).Concat(yuweRock.Take(8))) yield return a;
        if (yuwePrepare)
            yield return new("yuwe/prepare", "误导前靠近中心", new CircleTemplate(1), Arena.Position,
                Display: false, Safe: true, GoalPriority: 9);
        foreach (var a in YuwePartyAoes()) yield return a;
    }

    private static IEnumerable<DungeonAmbientAoe> YuweBatch(List<(DungeonPendingCast Cast, bool Risky, Vector4? Color)> list, double window)
    {
        if (list.Count == 0) yield break;
        var deadline = list[0].Cast.FinishAt.AddSeconds(window);
        var notLast = list[^1].Cast.FinishAt > deadline;
        for (var i = 0; i < list.Count; ++i)
        {
            var item = list[i];
            if (item.Cast.FinishAt < deadline)
            {
                item.Risky = true;
                if (notLast) item.Color = new(1, .15f, .15f, .45f);
                list[i] = item;
            }
            var c = item.Cast;
            yield return new($"yuwe/batch/{c.Source}/{c.StartedAt.Ticks}", c.Rule.Name, c.Rule.Shape!, c.Location, c.Rotation,
                Navigate: item.Risky, Color: item.Color, Activation: c.FinishAt);
        }
    }

    private IEnumerable<DungeonAmbientAoe> YuwePartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        DungeonActor Latest(DungeonActor a) => _actors.GetValueOrDefault(a.Id) ?? a;
        var componentGroups = yuweParty.GroupBy(s => s.Cast.Rule.Action);
        foreach (var component in componentGroups)
        {
        var active = component.Where(s => !Latest(s.Target).Dead).ToArray();
        var special = yuweClaw != null && component.Key == 40615 && !component.Any(s => s.Target.Id == PlayerId);
        if (special && _actors.TryGetValue(PrimaryId, out var boss))
        {
            var shelter = new RayRectangleTemplate(-50, 50, 1).Resolve(boss.Position, boss.Rotation + MathF.PI, null);
            yield return new("yuwe/jagged/behind", "躲在首领身后一小段区域", new SohrKhaiWallHintTemplate(Arena, [shelter]),
                Arena.Position, Display: false, Activation: component.First().Cast.FinishAt);
        }
        var markedStack = component.Any(s => s.Cast.Rule.PartyAoe == "分摊" && s.Target.Id == PlayerId);
        var markedSpread = component.Any(s => s.Cast.Rule.PartyAoe == "分散" && s.Target.Id == PlayerId);
        var player = _actors.GetValueOrDefault(PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var (cached, c) in active)
        {
            var target = Latest(cached);
            var stack = c.Rule.PartyAoe == "分摊";
            var radius = ((CircleTemplate)c.Rule.Shape!).Radius;
            var p = QuantizedDungeonPosition(target.Position);
            var key = $"yuwe/party/{c.Source}/{c.StartedAt.Ticks}";
            var numInside = party.Count(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(p.X, p.Z)) <= radius * radius);
            var inside = player != null && Vector2.DistanceSquared(new(player.Position.X, player.Position.Z), new(p.X, p.Z)) <= radius * radius;
            var safeColor = stack && (target.Id == PlayerId || (!markedStack && !markedSpread
                && (!inside && numInside < 4 || inside && numInside <= 4)));
            yield return new(key + "/display", c.Rule.Name, c.Rule.Shape, p, Navigate: false, Safe: safeColor, OutlineOnly: true);
            if (!stack)
            {
                if (special && c.Rule.Action == 40615) continue;
                if (target.Id != PlayerId)
                    yield return new(key + "/avoid", "避开分散", new CircleTemplate(radius + 1), p, Display: false, Activation: c.FinishAt);
                else
                    foreach (var other in party.Where(a => a.Id != PlayerId && !active.Any(s => s.Cast.Rule.PartyAoe == "分散" && s.Target.Id == a.Id)))
                        yield return new(key + $"/separate/{other.Id}", "分散远离队友", new CircleTemplate(radius + 1), QuantizedDungeonPosition(other.Position),
                            Display: false, Activation: c.FinishAt);
            }
            else if (target.Id == PlayerId)
            {
                var others = party.Where(a => a.Id != PlayerId && !active.Any(s => s.Cast.Rule.PartyAoe == "分摊" && s.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, radius * .5f)).ToArray();
                if (others.Length > 0)
                    yield return new(key + "/gather", "分摊靠近队友", new SohrKhaiWallHintTemplate(Arena, others),
                        Arena.Position, Display: false, Activation: c.FinishAt);
            }
            else if (!markedSpread && (!inside && numInside < 4 || inside && numInside <= 4)) joins.Add(new CircleGeometry(p, radius));
            else yield return new(key + "/avoid", "避开已满员或其他分摊", new CircleTemplate(markedStack ? radius * 2 : radius), p, Display: false, Activation: c.FinishAt);
        }
        if (joins.Count > 0)
            yield return new($"yuwe/stack/join/{component.Key}", "进入一个分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: active.First(s => s.Cast.Rule.PartyAoe == "分摊").Cast.FinishAt);
        }
    }
}

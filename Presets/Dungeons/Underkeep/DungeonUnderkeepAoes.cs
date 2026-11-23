using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> UnderAoes()
    {
        if (territory != UnderkeepCatalog.TERRITORY || Arena == null) yield break;
        foreach (var c in underSimple)
            yield return new($"under/simple/{c.Source}/{c.StartedAt.Ticks}", c.Rule.Name, c.Rule.Shape!, c.Location, c.Rotation, Activation: c.FinishAt);
        if (underSector != null) yield return underSector;
        if (underCrosses.Count > 0) yield return underCrosses[0];
        if (underSpheres.Count > 0)
        {
            var deadline = underSpheres[0].Activation!.Value.AddSeconds(1);
            foreach (var a in underSpheres)
            {
                if (a.Activation >= deadline) break;
                yield return a;
            }
        }
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var activeSpreads = underSpreads.Where(s => !s.Target.Dead).ToArray();
        foreach (var (target, cast) in activeSpreads)
        {
            var radius = ((CircleTemplate)cast.Rule.Shape!).Radius;
            var key = $"under/spread/{cast.Source}/{cast.StartedAt.Ticks}";
            yield return new(key + "/display", cast.Rule.Name, cast.Rule.Shape, QuantizedDungeonPosition(target.Position),
                Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new(key + "/avoid", "避开分散", new CircleTemplate(radius + 1), QuantizedDungeonPosition(target.Position),
                    Display: false, Activation: cast.FinishAt);
            else
                foreach (var other in party.Where(a => a.Id != PlayerId && !activeSpreads.Any(s => s.Target.Id == a.Id)))
                    yield return new(key + $"/separate/{other.Id}", "分散远离队友", new CircleTemplate(radius + 1), QuantizedDungeonPosition(other.Position),
                        Display: false, Activation: cast.FinishAt);
        }
        foreach (var b in underBaits.Where(b => !b.Source.Dead))
        {
            var radius = Active?.BossOid == 0x47AD ? 60 : 26;
            var angle = (Active?.BossOid == 0x47AD ? 30 : 50) * MathF.PI / 180;
            var delta = b.Target.Position - b.Source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var key = $"under/bait/{b.Serial}";
            var mine = b.Target.Id == PlayerId;
            yield return new(key + "/display", "诱导扇形", new FanTemplate(radius, angle), b.Source.Position, rotation,
                Navigate: false, OutlineOnly: mine, Activation: b.At);
            if (!mine)
                yield return new(key + "/avoid", "避开诱导扇形", new FanTemplate(radius, angle), b.Source.Position, rotation,
                    Display: false, Activation: b.At);
            else
                foreach (var other in party.Where(a => a.Id != PlayerId))
                {
                    var direction = other.Position - b.Source.Position;
                    yield return new(key + $"/away/{other.Id}", "扇形远离队友", new FanTemplate(100, angle), b.Source.Position,
                        MathF.Atan2(direction.X, direction.Z), Display: false, Activation: b.At);
                }
        }
        var lines = underLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var l in lines)
        {
            var delta = l.Target.Position - l.Source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(8, 40);
            var key = $"under/line/{l.Serial}";
            yield return new(key + "/display", "直线分摊", shape, l.Source.Position, rotation, Navigate: false,
                Safe: l.Target.Id == PlayerId || !marked, Activation: l.At);
            if (l.Target.Id == PlayerId)
            {
                var candidates = party.Where(a => !underLines.Any(b => b.Target.Id == a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (candidates.Length > 0)
                    yield return new(key + "/gather", "分摊目标靠近队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: l.At);
            }
            else if (!marked) joins.Add(shape.Resolve(l.Source.Position, rotation, null));
            else
                yield return new(key + "/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(16, 40), l.Source.Position, rotation,
                    Display: false, Activation: l.At);
        }
        if (joins.Count > 0)
            yield return new("under/line/join", "进入一个直线分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> UnderDamage()
    {
        if (territory != UnderkeepCatalog.TERRITORY) yield break;
        foreach (var s in underBusters)
            if (s.Target.PartySlot >= 0)
                yield return new(s.Target.Id, s.Target.PartySlot, s.Target.PartyRole, s.Target.Name, s.Cast.FinishAt, "Tankbuster");
        if (underBaits.Count > 0)
            foreach (var b in underBaits.Where(b => !b.Source.Dead && b.Target.PartySlot >= 0).DistinctBy(b => b.Target.Id))
                yield return new(b.Target.Id, b.Target.PartySlot, b.Target.PartyRole, b.Target.Name, underBaits[0].At, "Raidwide");
        var lines = underLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, lines.Min(l => l.At), "Shared");
    }

    private IEnumerable<string> UnderHints()
    {
        if (territory != UnderkeepCatalog.TERRITORY) yield break;
        if (underBaits.Any(b => !b.Source.Dead && b.Target.Id == PlayerId)) yield return "扇形远离队友";
        if (underLines.Any(l => !l.Source.Dead)) yield return "直线分摊";
        if (underBusters.Count > 0) yield return "死刑";
    }
}

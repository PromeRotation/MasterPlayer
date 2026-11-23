using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> KtisisAoes()
    {
        if (territory != KtisisCatalog.TERRITORY || Arena == null) yield break;
        if (ktisisSlice != null) yield return ktisisSlice;
        if (ktisisRing != null) yield return ktisisRing;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25375))
            yield return new($"ktisis/pillar/{cast.Source}", "冰柱贯穿", cast.Rule.Shape!, cast.Location, cast.Rotation,
                Activation: ktisisPillarClocks.GetValueOrDefault(cast.Source, cast.FinishAt));
        foreach (var breath in ktisisBreaths) yield return breath;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 25180 or 25742))
        {
            IGeometryShape[] regions = [];
            if (cast.Rule.Action == 25180 && _actors.Values.Any(a => a.Buddy && a.PartySlot >= 0))
            {
                regions = [new CircleGeometry(new(-139, Arena.Position.Y, 47), 5)];
                yield return new("ktisis/stack/gather", "支援队伍集合点", new CircleTemplate(5), new(-139, Arena.Position.Y, 47),
                    Display: false, Safe: true);
            }
            else if (cast.TargetId == PlayerId)
                regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            else if (_actors.TryGetValue(cast.TargetId, out var target))
                regions = [new CircleGeometry(target.Position, 6)];
            if (regions.Length > 0)
                yield return new($"ktisis/stack/{cast.Source}", "集合分摊", new SohrKhaiWallHintTemplate(Arena, regions),
                    Arena.Position, Display: false, Activation: cast.FinishAt);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 27837).OrderBy(c => ktisisWindOrder.GetValueOrDefault(c.Source)).Take(4))
            yield return new($"ktisis/wind/{cast.Source}", "连续风线", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        if (ktisisLos.Count > 0)
        {
            var shadow = ktisisLos[0].Shadow;
            yield return shadow;
            yield return new("ktisis/los/enter", "进入陨石直线遮挡", new SohrKhaiWallHintTemplate(Arena,
                [shadow.Shape.Resolve(shadow.Position, shadow.Rotation, null)]), Arena.Position, Display: false, Activation: shadow.Activation);
        }
        if (Active?.BossOid != 0x348A) yield break;
        var meteors = _actors.Values.Where(a => a.Oid == 0x348C).ToArray();
        if (meteors.Count(a => a.AnimationState2 == 1) == 3 && !_casts.Values.Any(c => c.Rule.Action == 25891)
            && meteors.LastOrDefault(a => a.AnimationState2 != 1) is { } intact)
            yield return new("ktisis/meteor/near", "靠近完好陨石", new CircleTemplate(5 + intact.HitboxRadius), intact.Position,
                Display: false, Safe: true);
        for (var i = 0; i < ktisisAeroBaits.Count; ++i)
        {
            var bait = ktisisAeroBaits[i];
            if (!_actors.TryGetValue(PrimaryId, out var boss) || !_actors.TryGetValue(bait.Target, out var target)) continue;
            var delta = target.Position - boss.Position; var rotation = MathF.Atan2(delta.X, delta.Z); var own = target.Id == PlayerId;
            yield return new($"ktisis/aero/{i}", "风线引导", RayRectangleTemplate.Symmetric(6, 40), boss.Position, rotation,
                Navigate: !own, OutlineOnly: own, Activation: bait.Activation);
            if (own)
                yield return new($"ktisis/aero/avoid/{i}", "风线远离队友", new CastrumBaitTemplate(Arena, boss.Position,
                    _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead).Select(a => a.Position).ToArray(), 40),
                    Arena.Position, Display: false, Activation: bait.Activation);
        }
        for (var i = 0; i < ktisisTornadoBaits.Count; ++i)
        {
            var bait = ktisisTornadoBaits[i];
            if (!_actors.TryGetValue(bait.Target, out var target)) continue;
            var own = target.Id == PlayerId;
            yield return new($"ktisis/tornado/{i}", "龙卷死刑引导", new CircleTemplate(4), target.Position,
                Navigate: !own, OutlineOnly: own, Activation: bait.Activation);
            if (own)
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"ktisis/tornado/{i}/away/{member.Id}", "死刑远离队友", new CircleTemplate(4), member.Position,
                        Display: false, Activation: bait.Activation);
        }
    }

    private IEnumerable<DungeonDamagePrediction> KtisisDamage()
    {
        if (territory != KtisisCatalog.TERRITORY) yield break;
        foreach (var bait in ktisisTornadoBaits)
            if (_actors.TryGetValue(bait.Target, out var target) && target.PartySlot >= 0)
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, bait.Activation, "Tankbuster");
    }
}

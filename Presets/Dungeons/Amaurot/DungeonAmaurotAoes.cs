using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> AmaurotAoes()
    {
        if (territory != AmaurotCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x27B6)
        {
            foreach (var (id, activation) in amaurotMeteors)
            {
                if (!_actors.TryGetValue(id, out var target)) continue;
                yield return new($"amaurot/meteor/{id}", "陨石诱导", new CircleTemplate(10), target.Position,
                    Navigate: id != PlayerId, Activation: activation);
                if (id != PlayerId) continue;
                yield return new("amaurot/meteor/inner", "在场内放陨石",
                    new SohrKhaiWallHintTemplate(Arena, [new RectangleGeometry(Arena.Position, 0, 32, 32)]), Arena.Position,
                    Display: false, Activation: activation);
                yield return new("amaurot/meteor/edge", "去南北边缘放陨石", new RectangleTemplate(268, 28),
                    Arena.Position, Display: false, Activation: activation);
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != id && !a.Dead))
                    yield return new($"amaurot/meteor/separate/{member.Id}", "陨石远离队友", new CircleTemplate(10), member.Position,
                        Display: false, Activation: activation);
            }
            foreach (var (source, los) in amaurotLos)
            {
                if (!_casts.TryGetValue(source, out var cast)) continue;
                yield return new($"amaurot/los/safe/{source}", "陨石后遮挡", los.Safe, cast.Location,
                    Navigate: false, Safe: true, Activation: cast.FinishAt);
                yield return new($"amaurot/los/enter/{source}", "躲到陨石后", los.Danger, cast.Location,
                    Display: false, Activation: cast.FinishAt);
            }
            if (amaurotLos.Count > 0)
                foreach (var star in _actors.Values.Where(a => a.Oid == 0x29DC))
                    yield return new($"amaurot/star/{star.Id}", "遮挡陨石实体", new CircleTemplate(star.HitboxRadius),
                        star.Position, Navigate: false, Safe: true, OutlineOnly: true);
        }
        if (Active?.BossOid != 0x27C1) yield break;
        if (amaurotApocalypse != null) yield return amaurotApocalypse;
        if (amaurotRay != null) yield return amaurotRay;
        var first = amaurotFaces.FirstOrDefault().Aoe?.Activation;
        foreach (var (aoe, _) in amaurotFaces)
            yield return aoe with { Navigate = aoe.Activation < first?.AddSeconds(1) };
        foreach (var index in amaurotBreaking)
        {
            var p = AmaurotPlatforms[index];
            yield return new($"amaurot/break/{index}", "下次末日光束将破坏的平台", new RectangleTemplate(4, 4),
                new(p.X, Arena.Position.Y, p.Y), Navigate: amaurotApocalypseHits == 0);
        }
    }
}

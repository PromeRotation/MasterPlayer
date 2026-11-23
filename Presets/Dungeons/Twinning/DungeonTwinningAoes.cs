using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TwinningAoes()
    {
        if (territory != TwinningCatalog.TERRITORY || Arena == null) yield break;
        foreach (var aoe in twinningShocks.Values) yield return aoe;
        foreach (var laser in twinningLasers.Values) { yield return laser.Short; yield return laser.Long; }
        if (Active?.BossOid != 0x27D1 || !_actors.TryGetValue(PrimaryId, out var boss)) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var cages = _actors.Values.Where(a => a.Oid == 0x27D3).ToArray();
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 15715))
            if (_actors.TryGetValue(cast.TargetId, out var target) && !target.Dead)
            {
                yield return new($"twinning/stack/{cast.Source}", "四人分摊", new CircleTemplate(8), target.Position,
                    Navigate: false, Safe: true, Activation: cast.FinishAt);
                if (PlayerId != target.Id)
                    yield return new($"twinning/stack/enter/{cast.Source}", "进入四人分摊",
                        new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(target.Position, 8)]), Arena.Position,
                        Display: false, Activation: cast.FinishAt);
            }
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E88F5 && a.EventState != 7))
            yield return new($"twinning/voidzone/{zone.Id}", "持续分摊火池", new CircleTemplate(8), zone.Position);
        for (var i = 0; i < twinningPounces.Count; ++i)
        {
            var pounce = twinningPounces[i];
            if (!_actors.TryGetValue(pounce.Target, out var target)) continue;
            yield return new($"twinning/pounce/{i}", "跳跃分散", new CircleTemplate(10), target.Position,
                Navigate: pounce.Target != PlayerId, OutlineOnly: pounce.Target == PlayerId, Activation: pounce.Activation);
            if (pounce.Target != PlayerId) continue;
            foreach (var member in party.Where(a => a.Id != PlayerId))
                yield return new($"twinning/pounce/{i}/separate/{member.Id}", "跳跃远离队友", new CircleTemplate(10),
                    member.Position, Display: false, Activation: pounce.Activation);
            foreach (var cage in cages)
                yield return new($"twinning/pounce/{i}/cage/{cage.Id}", "跳跃避开牢笼", new CircleTemplate(11),
                    cage.Position, Display: false, Activation: pounce.Activation);
        }
        for (var i = 0; i < twinningImpacts.Count; ++i)
        {
            var impact = twinningImpacts[i];
            if (!_actors.TryGetValue(impact.Target, out var target)) continue;
            var delta = target.Position - boss.Position;
            yield return new($"twinning/impact/{i}", "连续直线诱导", RayRectangleTemplate.Symmetric(6, 50),
                boss.Position, MathF.Atan2(delta.X, delta.Z), Navigate: impact.Target != PlayerId,
                OutlineOnly: impact.Target == PlayerId, Activation: impact.Activation);
            if (impact.Target != PlayerId) continue;
            foreach (var actor in party.Where(a => a.Id != PlayerId).Concat(cages))
            {
                var offset = actor.Position - boss.Position; var length = new Vector2(offset.X, offset.Z).Length();
                var radius = actor.Oid == 0x27D3 ? 3.5f : 3;
                var halfAngle = MathF.Asin(Math.Clamp(radius / MathF.Max(length, .001f), 0, 1));
                yield return new($"twinning/impact/{i}/avoid/{actor.Id}", "直线避开牢笼和队友", new FanTemplate(100, 2 * halfAngle),
                    boss.Position, MathF.Atan2(offset.X, offset.Z), Display: false, Activation: impact.Activation);
            }
        }
        if (!twinningPounces.Any(b => b.Target == PlayerId) && !twinningImpacts.Any(b => b.Target == PlayerId)) yield break;
        foreach (var cage in cages)
            yield return new($"twinning/cage/{cage.Id}", "受保护牢笼", new CircleTemplate(cage.HitboxRadius),
                cage.Position, Navigate: false, OutlineOnly: true);
    }
}

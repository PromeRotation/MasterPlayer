using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> BurnAoes()
    {
        if (territory != BurnCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x2419)
        {
            foreach (var spread in BurnSpreads(burnSpreads, "burn/spread", 5)) yield return spread;
            foreach (var crystal in _actors.Values.Where(a => a.Oid == 0x241A && !a.Dead))
            {
                yield return new($"burn/crystal/{crystal.Id}", "遮挡水晶", new CircleTemplate(crystal.HitboxRadius),
                    crystal.Position, Navigate: false, Safe: true, OutlineOnly: true);
                if (burnSpreads.Count > 0)
                    yield return new($"burn/crystal/{crystal.Id}/spread", "分散不要覆盖水晶", new CircleTemplate(6.6f),
                        crystal.Position, Navigate: burnSpreads.ContainsKey(PlayerId), OutlineOnly: true,
                        Activation: burnSpreads.Values.Min());
            }
            if (burnHailTarget != 0 && _actors.TryGetValue(burnHailTarget, out var target)
                && _actors.TryGetValue(PrimaryId, out var boss))
            {
                if (burnHailTarget == PlayerId)
                {
                    for (var i = 0; i < burnHailShadows.Length; ++i)
                        yield return new($"burn/hail/safe/{i}", "水晶后直线遮挡", new DungeonShapeTemplate(burnHailShadows[i]),
                            Arena.Position, Navigate: false, Safe: true);
                    yield return new("burn/hail/enter", "躲到水晶后", new SohrKhaiWallHintTemplate(Arena, burnHailShadows),
                        Arena.Position, Display: false);
                }
                else
                {
                    var delta = target.Position - boss.Position;
                    var ray = RayRectangleTemplate.Symmetric(4, 44.2f).Resolve(boss.Position, MathF.Atan2(delta.X, delta.Z), null);
                    yield return new("burn/hail/avoid", "避开穿透前的直线", new DungeonShapeTemplate(new BurnHailfireGeometry(ray, burnHailShadows)), boss.Position);
                }
            }
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 12689))
            {
                var pillars = _actors.Values.Where(a => a.Oid == 0x241A && !a.Dead).ToArray();
                yield return new($"burn/shardfall/{cast.Source}/safe", "水晶后的视线遮挡", new DungeonLosTemplate(new(cast.Location, pillars, true, 40, false, 0)),
                    cast.Location, Navigate: false, Safe: true, Activation: cast.FinishAt);
                yield return new($"burn/shardfall/{cast.Source}/avoid", "躲到水晶后", new DungeonLosTemplate(new(cast.Location, pillars, false, 40, false, 0)),
                    cast.Location, Display: false, Activation: cast.FinishAt);
            }
        }
        if (Active?.BossOid == 0x23AA)
        {
            foreach (var aoe in burnThrottles) yield return aoe;
            foreach (var bait in BurnSpreads(burnResidue, "burn/residue", 5)) yield return bait;
            foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1EA9EF && a.EventState != 7))
                yield return new($"burn/sludge/{zone.Id}", "污泥持续区", new CircleTemplate(2.5f), zone.Position);
        }
        if (Active?.BossOid != 0x2431) yield break;
        foreach (var (_, aoe) in burnCrosses) yield return aoe;
        if (burnColdActive)
            yield return new("burn/cold", "浓雾扩大", new CircleTemplate(4 + burnColdParam), QuantizedDungeonPosition(Arena.Position),
                Activation: burnColdCurrentAt == default ? null : burnColdCurrentAt);
        if (burnCauterize != null) yield return burnCauterize;
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E8713 && a.EventState != 7))
            yield return new($"burn/ice/{zone.Id}", "持续冰池", new CircleTemplate(6), zone.Position);
        if (!_actors.TryGetValue(PrimaryId, out var dragon)) yield break;
        var ownsBait = burnBaits.ContainsKey(PlayerId);
        foreach (var (id, bait) in burnBaits)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            var angle = MathF.Atan2(target.Position.X - dragon.Position.X, target.Position.Z - dragon.Position.Z);
            if (!bait.Cone)
                foreach (var line in DomaLineBait($"burn/bait/{id}", dragon, target, 3, 40))
                    yield return line with { Activation = bait.Activation };
            else
            {
                yield return new($"burn/bait/{id}/display", "冰息扇形诱导", new FanTemplate(21, MathF.PI * 2 / 3), dragon.Position,
                    angle, Navigate: id != PlayerId, OutlineOnly: id == PlayerId, Activation: bait.Activation);
                if (id == PlayerId)
                    foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != id && !a.Dead))
                        yield return new($"burn/bait/{id}/party/{other.Id}", "冰息不要覆盖队友", new FanTemplate(100, MathF.PI * 2 / 3),
                            dragon.Position, MathF.Atan2(other.Position.X - dragon.Position.X, other.Position.Z - dragon.Position.Z), Display: false, Activation: bait.Activation);
            }
            if (!ownsBait) continue;
            var predictions = bait.Cone
                ? new (float Distance, float Angle)[] { (7, angle), (15, angle), (15, angle + MathF.PI / 6), (15, angle - MathF.PI / 6) }
                : Enumerable.Range(0, 5).Select(i => (dragon.HitboxRadius + i * 9, angle)).ToArray();
            for (var i = 0; i < predictions.Length; ++i)
            {
                var (distance, direction) = predictions[i];
                var p = dragon.Position + new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction)) * distance;
                if (Vector2.DistanceSquared(new(p.X, p.Z), new(Arena.Position.X, Arena.Position.Z)) <= 400)
                    yield return new($"burn/bait/{id}/pool/{i}", "后续冰池预告", new CircleTemplate(6), p,
                        Navigate: false, OutlineOnly: true, Activation: bait.Activation);
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> BurnSpreads(Dictionary<ulong, DateTime> spreads, string key, float radius)
    {
        foreach (var (id, activation) in spreads)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            yield return new($"{key}/{id}", "点名分散", new CircleTemplate(radius), target.Position,
                Navigate: id != PlayerId, OutlineOnly: id == PlayerId, Activation: activation);
            if (id == PlayerId)
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != id && !a.Dead))
                    yield return new($"{key}/{id}/party/{other.Id}", "分散不要覆盖队友", new CircleTemplate(radius),
                        other.Position, Display: false, Activation: activation);
        }
    }
}

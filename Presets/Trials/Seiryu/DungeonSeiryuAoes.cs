using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static float SeiryuAngle(SeiryuBait bait) => bait.Source.Id == bait.Target.Id ? bait.Source.Rotation
        : MathF.Atan2(bait.Target.Position.X - bait.Source.Position.X, bait.Target.Position.Z - bait.Source.Position.Z);
    private bool SeiryuOwn(SeiryuBait bait) => bait.Target.Id == PlayerId && !bait.TargetDestroyed;
    private static GeometryTemplate SeiryuBaitShape(uint action) => action switch
    {
        14333 => new CircleTemplate(4),
        15392 => new FanTemplate(45, MathF.PI / 3),
        _ => RayRectangleTemplate.Symmetric(5, 82.6f),
    };

    private IEnumerable<DungeonAmbientAoe> SeiryuAoes()
    {
        if (!IsSeiryu || Active == null || Arena == null) yield break;
        if (seiryuSigil != null) yield return seiryuSigil;
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x25F4 or 0x25F6 or 0x25F7 && a.Targetable && !a.Dead))
            yield return new($"seiryu/enemy/{actor.Id}", "青龙与式神", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true);
        foreach (var bait in seiryuBaits.Where(b => !b.Source.Dead))
        {
            var shape = SeiryuBaitShape(bait.Action);
            var position = bait.Action == 14333 ? bait.Target.Position : bait.Source.Position;
            var angle = SeiryuAngle(bait);
            var own = SeiryuOwn(bait);
            var id = $"seiryu/bait/{bait.Action}/{bait.Key}";
            yield return new(id + "/display", "式神诱导", shape, position, angle, Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "远离诱导范围", shape, position, angle, Display: false, Activation: bait.At);
            else if (bait.Source.Id != bait.Target.Id)
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                {
                    var buddyAngle = MathF.Atan2(buddy.Position.X - bait.Source.Position.X, buddy.Position.Z - bait.Source.Position.Z);
                    if (bait.Action == 14333)
                        yield return new(id + $"/buddy/{buddy.Id}", "死刑远离队友", shape, buddy.Position, Display: false, Activation: bait.At);
                    else
                    {
                        var distance = Vector2.Distance(new(buddy.Position.X, buddy.Position.Z),
                            new(bait.Source.Position.X, bait.Source.Position.Z));
                        var fullAngle = bait.Action == 15392 ? MathF.PI / 3 : 2 * MathF.Asin(2.5f / distance);
                        if (!float.IsFinite(fullAngle)) continue;
                        yield return new(id + $"/buddy/{buddy.Id}", "诱导远离队友", new FanTemplate(100, fullAngle),
                            bait.Source.Position, buddyAngle, Display: false, Activation: bait.At);
                    }
                }
            if (own && bait.Action is 15392 or 15389)
            {
                var radius = bait.Action == 15392 ? 19 : seiryuPhase2 ? 5 : 18.5f;
                GeometryTemplate hint = bait.Action == 15389 && seiryuPhase2
                    ? new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(Arena.Position, radius)]) : new CircleTemplate(radius);
                yield return new(id + "/position", "式神连线站位", hint, Arena.Position,
                    Display: false, Activation: _now.AddSeconds(bait.Action == 15392 ? 5.9 : 6));
            }
            if (bait.Action is 15392 or 15389)
            {
                var delta = bait.Target.Position - bait.Source.Position; delta.Y = 0;
                if (delta.LengthSquared() > 0)
                    yield return new(id + "/tether", "式神连线", new RectangleTemplate(.08f, delta.Length()),
                        (bait.Source.Position + bait.Target.Position) * .5f, angle, Navigate: false);
            }
        }
        foreach (var aoe in SeiryuLineAoes()) yield return aoe;
        foreach (var aoe in SeiryuTowerAoes()) yield return aoe;
        foreach (var spread in seiryuSpreads)
        {
            var id = $"seiryu/spread/{spread.Key}";
            yield return new(id + "/display", "登龙分散", new CircleTemplate(5),
                QuantizedDungeonPosition(spread.Target.Position), Navigate: false, OutlineOnly: true);
            if (spread.Target.Id != PlayerId || spread.Destroyed)
                yield return new(id + "/avoid", "远离登龙点名", new CircleTemplate(6),
                    QuantizedDungeonPosition(spread.Target.Position), Display: false, Activation: spread.At);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                    && !seiryuSpreads.Any(s => s.Target.Id == a.Id && !s.Destroyed)))
                    yield return new(id + $"/buddy/{buddy.Id}", "登龙远离队友", new CircleTemplate(6),
                        QuantizedDungeonPosition(buddy.Position), Display: false, Activation: spread.At);
        }
        foreach (var aoe in SeiryuMovementAoes()) yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> SeiryuDamage()
    {
        if (!IsSeiryu) yield break;
        var spreads = seiryuSpreads.Where(s => !s.Target.Dead).ToArray();
        if (spreads.Length > 0)
            foreach (var actor in spreads.Select(s => s.Target).DistinctBy(a => a.Id).Where(a => a.PartySlot >= 0))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, spreads.Min(s => s.At), "Raidwide");
        foreach (var action in new[] { 14333u, 15392u, 15389u })
        {
            var all = seiryuBaits.Where(b => b.Action == action).ToArray();
            if (all.Length == 0) continue;
            foreach (var target in all.Where(b => !b.Source.Dead).Select(b => b.Target).DistinctBy(a => a.Id).Where(a => a.PartySlot >= 0))
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, all[0].At, "Tankbuster");
        }
        foreach (var action in new[] { 15388u, 15474u })
        {
            var lines = seiryuLines.Where(l => l.Action == action && !l.Source.Dead).ToArray();
            if (lines.Length == 0) continue;
            foreach (var actor in _actors.Values.Where(a => a.PartySlot is >= 0 and < 64
                && lines.Any(l => (l.Forbidden & (1UL << a.PartySlot)) == 0)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, lines.Min(l => l.At), "Shared");
        }
        if (seiryuTowers.Count > 0)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && seiryuTowers.Any(t => new CircleGeometry(t.Position, 3).Contains(a.Position))))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, seiryuTowers[0].At, "Raidwide");
    }
}

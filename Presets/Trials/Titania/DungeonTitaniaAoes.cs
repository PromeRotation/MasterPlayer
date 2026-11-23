using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonActor> TitaniaPuddles()
    {
        var burst = _actors.Values.Where(a => a.Oid == 0x1E9E3C && !titaniaWaitingRemoval.Contains(a.Id)).ToArray();
        return (burst.Length > 0 ? burst : _actors.Values.Where(a => a.Oid == 0x1EABE5 && !titaniaWaitingRemoval.Contains(a.Id)))
            .Where(a => !a.Dead);
    }

    private IEnumerable<DungeonAmbientAoe> TitaniaAoes()
    {
        if (!IsTitania || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => (a.Oid == 0x27ED || TitaniaCatalog.IsAdd(a.Oid))
            && !a.Dead && a.Targetable))
            yield return new($"titania/enemy/{actor.Id}", "缇坦妮雅与树妖", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true);
        if (titaniaFire)
        {
            var puddles = TitaniaPuddles().ToArray();
            foreach (var puddle in puddles)
                yield return new($"titania/water/{puddle.Id}/display", "水圈抗火", new CircleTemplate(5),
                    puddle.Position, Navigate: false, Safe: true);
            if (puddles.Length > 0)
                yield return new("titania/water/join", "进入水圈抗火", new SohrKhaiWallHintTemplate(Arena,
                    puddles.Select(p => (IGeometryShape)new CircleGeometry(p.Position, 4.5f)).ToArray()),
                    Arena.Position, Display: false, Activation: titaniaFireAt);
        }
        foreach (var root in titaniaRoots.Where(r => !r.Actor.Dead))
            yield return new($"titania/roots/{root.Key}", "根系八方向生长", new DungeonCrossTemplate(root.Length, 3),
                root.Actor.Position, root.Rotation ?? root.Actor.Rotation);
        foreach (var bait in titaniaBaits.Where(b => !b.Source.Dead))
        {
            var cone = bait.Action == 15707;
            GeometryTemplate shape = cone ? new FanTemplate(60, MathF.PI * 75 / 180) : new CircleTemplate(10);
            var position = cone ? bait.Source.Position : bait.Target.Position;
            var angle = bait.Source.Id == bait.Target.Id ? bait.Source.Rotation
                : MathF.Atan2(bait.Target.Position.X - bait.Source.Position.X, bait.Target.Position.Z - bait.Source.Position.Z);
            var own = bait.Target.Id == PlayerId && !bait.TargetDestroyed;
            var id = $"titania/bait/{bait.Action}/{bait.Key}";
            yield return new(id + "/display", cone ? "魔之符文扇形死刑" : "战争与和平引导",
                shape, position, angle, Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "远离引导范围", shape, position, angle, Display: false, Activation: bait.At);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "引导远离队友",
                        cone ? new FanTemplate(100, MathF.PI * 75 / 180) : shape,
                        cone ? bait.Source.Position : buddy.Position,
                        cone ? MathF.Atan2(buddy.Position.X - bait.Source.Position.X, buddy.Position.Z - bait.Source.Position.Z) : 0,
                        Display: false, Activation: bait.At);
        }
        foreach (var aoe in TitaniaMarkAoes()) yield return aoe;
        foreach (var aoe in TitaniaTowerAoes()) yield return aoe;
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (titaniaKnockbacks.FirstOrDefault() is { } first && !WhorlImmune(player, first.FinishAt))
            yield return new("titania/knockback/join", "击退前进入安全环", new SohrKhaiWallHintTemplate(Arena,
                [new DonutGeometry(first.Location, 5.1f, 9.8f)]), Arena.Position, Display: false);
        foreach (var (source, from, to) in TitaniaKnockbacks())
            foreach (var aoe in TrialKnockbackDrawing($"titania/knockback/{source}", "帕克击退落点", from, to))
                yield return aoe;
    }

    private IEnumerable<(ulong Source, Vector3 From, Vector3 To)> TitaniaKnockbacks()
    {
        if (!IsTitania || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var from = player.Position;
        foreach (var cast in titaniaKnockbacks)
        {
            if (WhorlImmune(player, cast.FinishAt)) continue;
            var delta = from - cast.Location; delta.Y = 0;
            if (delta.LengthSquared() == 0) continue;
            var to = from + Vector3.Normalize(delta) * 10;
            yield return (cast.Source, from, to);
            from = to;
        }
    }

    private IEnumerable<DungeonDamagePrediction> TitaniaDamage()
    {
        if (!IsTitania) yield break;
        foreach (var action in new[] { 15707u, 15789u })
        {
            var baits = titaniaBaits.Where(b => b.Action == action).ToArray();
            if (baits.Length == 0) continue;
            foreach (var target in baits.Where(b => !b.Source.Dead).Select(b => b.Target).DistinctBy(t => t.Id).Where(t => t.PartySlot >= 0))
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, baits[0].At, "Raidwide");
        }
        if (titaniaTowers.Count > 0)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && titaniaTowers.Any(t => new CircleGeometry(t.Position, 5).Contains(a.Position))))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, default, "Raidwide");
    }

    private IEnumerable<string> TitaniaHints()
    {
        if (!IsTitania || Active == null || Arena == null) yield break;
        if (titaniaFire && TitaniaPuddles().Any()) yield return "进入水圈获得抗火";
        if (titaniaBaits.Any(b => b.Action == 15707)) yield return "扇形死刑";
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (TitaniaKnockbacks().Any(k => !Arena.Contains(k.To))) yield return "击退落点将出界";
        foreach (var aoe in TitaniaAoes().Where(a => a.Display && (a.Id.StartsWith("titania/roots/") || a.Id.StartsWith("titania/bait/"))))
        {
            var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            if (aoe.OutlineOnly ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))
                : shape.Contains(player.Position)) yield return aoe.OutlineOnly ? "引导远离队友" : "远离引导范围";
        }
        foreach (var group in titaniaMarks.Where(m => !m.Target.Dead).GroupBy(m => m.Cast?.Rule.Action ?? 17267u))
        {
            var stack = group.Key is 15703 or 17267;
            var radius = group.Key switch { 16927 => 8, 17267 => 5, _ => 6 };
            foreach (var mark in group)
            {
                var shape = new CircleGeometry(QuantizedDungeonPosition(mark.Target.Position), radius);
                var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && shape.Contains(a.Position));
                if (stack && (mark.Target.Id == PlayerId && !mark.Destroyed ? count < (group.Key == 17267 ? 8 : 2)
                        || group.Any(other => other.Key != mark.Key && shape.Contains(other.Target.Position))
                    : !shape.Contains(player.Position) && count < (group.Key == 17267 ? 8 : 2)))
                    yield return group.Key == 17267 ? "八人火焰分摊" : "至少两人分摊";
                if (!stack && (mark.Target.Id == PlayerId && !mark.Destroyed
                    ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))
                    : shape.Contains(player.Position))) yield return "分散远离队友";
            }
        }
        var occupied = titaniaTowers.FirstOrDefault(t => new CircleGeometry(t.Position, 5).Contains(player.Position));
        if (occupied.Key != 0)
        {
            var shape = new CircleGeometry(occupied.Position, 5);
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && shape.Contains(a.Position));
            if (count > 2) yield return "水塔超员";
        }
        else if (titaniaTowers.Any(t => _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead
            && new CircleGeometry(t.Position, 5).Contains(a.Position)) < 2))
            yield return "进入水塔";
    }
}

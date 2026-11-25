using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static Vector3 TacticalChaserPosition(TacticalChaser chaser)
    {
        var delta = chaser.Target.Position - chaser.Previous;
        delta.Y = 0;
        var length = delta.Length();
        return QuantizedDungeonPosition(length > chaser.MoveDistance && length > 0
            ? chaser.Previous + delta / length * chaser.MoveDistance : chaser.Target.Position);
    }

    private IEnumerable<DungeonAmbientAoe> TacticalAoes()
    {
        if (!IsTactical || Active == null || Arena == null) yield break;
        if (tacticalReadyPosition is { } ready)
        {
            yield return new("tactical/ready/display", "准备位置", new CircleTemplate(tacticalReadyRadius), ready,
                Navigate: false, Safe: true, OutlineOnly: true);
            yield return TacticalEnter("ready/enter", [new CircleGeometry(ready, tacticalReadyRadius)]);
        }
        var fans = tacticalFans.Count == 0 ? [] : tacticalFans.TakeWhile(a => a.Activation < tacticalFans[0].Activation.AddSeconds(1));
        foreach (var aoe in tacticalRings.Take(1).Concat(tacticalSurges.Take(1)).Concat(fans))
            yield return new($"tactical/aoe/{aoe.Id}", aoe.Cast.Rule.Name, aoe.Shape, aoe.Position, aoe.Rotation, Activation: aoe.Activation);
        foreach (var aoe in TacticalPartyAoes()) yield return aoe;
        foreach (var aoe in TacticalBaitAoes()) yield return aoe;
        foreach (var group in tacticalChasers.GroupBy(c => c.Cast.Rule.Action))
        {
            yield return group.Key == 40689
                ? new("tactical/chaser/outer", "追踪站位", new CircleTemplate(30), Arena.Position + new Vector3(0, 0, 13), Display: false)
                : TacticalEnter("chaser/inner", [new CircleGeometry(Arena.Position, 10)]);
            foreach (var chaser in group)
            {
                var predicted = TacticalChaserPosition(chaser);
                yield return new($"tactical/chaser/{chaser.Id}/display", "Piercing Stone", new CircleTemplate(4), predicted,
                    Navigate: false, Activation: chaser.Activation);
                var self = chaser.Target.Id == PlayerId && !chaser.Removed;
                var direction = new Vector3(MathF.Sin(chaser.Target.Rotation), 0, MathF.Cos(chaser.Target.Rotation));
                yield return new($"tactical/chaser/{chaser.Id}/avoid", "Piercing Stone", new CircleTemplate(self ? 9 : 5),
                    predicted - (self ? direction * 4 : Vector3.Zero), Display: false, Activation: chaser.Activation);
                foreach (var line in TacticalLine($"chaser/{chaser.Id}/line", chaser.Previous, chaser.Target.Position)) yield return line;
            }
        }
        foreach (var aoe in TacticalTowerAoes()) yield return aoe;
        foreach (var aoe in TacticalKnockbackAoes()) yield return aoe;
        if (tacticalIntercepts.Count == 1 && _actors.GetValueOrDefault(PrimaryId) is { } primary)
        {
            var delta = primary.Position - tacticalIntercepts[0].Actor.Position;
            delta.Y = 0;
            var halfLength = delta.Length() / 2 / 3 * 2;
            var rect = new RectangleTemplate(2, halfLength * 2).Resolve(Arena.Position - delta * .5f,
                MathF.Atan2(delta.X, delta.Z), null);
            yield return TacticalEnter("intercept/enter", [rect]);
            if (NoviceActors.FirstOrDefault(a => a.Oid == 0x4640) is { } first)
                foreach (var line in TacticalLine("intercept/line", Arena.Position, first.Position, safe: true)) yield return line;
        }
    }

    private DungeonAmbientAoe TacticalEnter(string id, IGeometryShape[] areas, DateTime? activation = null)
        => new($"tactical/{id}", "进入机制站位", new SohrKhaiWallHintTemplate(Arena!, areas), Arena!.Position,
            Display: false, Activation: activation);

    private IEnumerable<DungeonAmbientAoe> TacticalLine(string id, Vector3 from, Vector3 to, bool safe = false)
    {
        var delta = to - from;
        delta.Y = 0;
        if (delta.LengthSquared() == 0) yield break;
        yield return new($"tactical/{id}", "机制连线", new RectangleTemplate(.1f, delta.Length()),
            (from + to) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: safe);
    }

    private IEnumerable<DungeonAmbientAoe> TacticalPartyAoes()
    {
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var group in tacticalMarks.Where(m => m.Icon is 139 or 375 or 318 or 317).GroupBy(m => m.Icon))
        {
            var marks = group.Where(m => !m.Target.Dead).ToArray();
            var spread = group.Key is 139 or 375;
            var selfMarked = marks.Any(m => m.Target.Id == PlayerId && !m.Removed);
            foreach (var mark in marks)
            {
                var self = mark.Target.Id == PlayerId && !mark.Removed;
                var pos = QuantizedDungeonPosition(mark.Target.Position);
                yield return new($"tactical/mark/{mark.Id}/display", spread ? "Spread" : "Stack", new CircleTemplate(6), pos,
                    Navigate: false, Safe: !spread && (!selfMarked || self), OutlineOnly: true, Activation: mark.Activation);
                if (party.Length < 2) continue;
                if (spread)
                {
                    if (!self)
                        yield return new($"tactical/mark/{mark.Id}/avoid", "Spread", new CircleTemplate(7), pos,
                            Display: false, Activation: mark.Activation);
                    else
                        foreach (var buddy in party.Where(a => !marks.Any(m => m.Target.Id == a.Id && !m.Removed)))
                            yield return new($"tactical/mark/{mark.Id}/separate/{buddy.Id}", "Spread", new CircleTemplate(7),
                                QuantizedDungeonPosition(buddy.Position), Display: false, Activation: mark.Activation);
                }
                else if (self)
                {
                    var buddies = party.Where(a => !marks.Any(m => m.Target.Id == a.Id && !m.Removed))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                    if (buddies.Length > 0) yield return TacticalEnter($"mark/{mark.Id}/buddy", buddies, mark.Activation);
                }
            }
            if (!spread && party.Length >= 2)
            {
                var others = marks.Where(m => m.Target.Id != PlayerId || m.Removed).ToArray();
                if (selfMarked)
                {
                    foreach (var mark in others)
                        yield return new($"tactical/mark/{mark.Id}/separate", "分摊点间隔", new CircleTemplate(12),
                            QuantizedDungeonPosition(mark.Target.Position), Display: false, Activation: mark.Activation);
                    continue;
                }
                var candidates = others
                    .Select(m => (IGeometryShape)new CircleGeometry(QuantizedDungeonPosition(m.Target.Position), 6)).ToArray();
                if (candidates.Length > 0) yield return TacticalEnter($"mark/{group.Key}/join", candidates, marks[0].Activation);
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> TacticalBaitAoes()
    {
        var boss = tacticalBaitSource;
        var baits = tacticalMarks.Where(m => m.Icon == 412).ToArray();
        if (baits.Length > 0)
        {
            var tank = _actors.GetValueOrDefault(PlayerId)?.PartyRole.Equals("Tank", StringComparison.OrdinalIgnoreCase) == true;
            yield return TacticalEnter("bait/role", [new CircleGeometry(Arena!.Position + new Vector3(0, 0, tank ? -15 : 15), 3)]);
        }
        if (boss != null && !boss.Dead)
            foreach (var bait in baits)
            {
                var from = boss.Position;
                var to = bait.Target.Position;
                var delta = to - from;
                var rotation = MathF.Atan2(delta.X, delta.Z);
                var self = bait.Target.Id == PlayerId && !bait.Removed;
                yield return new($"tactical/bait/{bait.Id}/display", "Scorching Streak", RayRectangleTemplate.Symmetric(10, 44),
                    from, rotation, Navigate: false, OutlineOnly: self, Activation: bait.Activation);
                if (!self)
                    yield return new($"tactical/bait/{bait.Id}/avoid", "Scorching Streak", RayRectangleTemplate.Symmetric(10, 44),
                        from, rotation, Display: false, Activation: bait.Activation);
                else
                    foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    {
                        var vector = member.Position - from;
                        var distance = new Vector2(vector.X, vector.Z).Length();
                        var half = MathF.Asin(5 / distance);
                        if (!float.IsFinite(half)) continue;
                        yield return new($"tactical/bait/{bait.Id}/direction/{member.Id}", "诱导避开同伴", new FanTemplate(100, 2 * half),
                            from, MathF.Atan2(vector.X, vector.Z), Display: false, Activation: bait.Activation);
                    }
            }
        var activeTethers = tacticalTethers.Where(t => !t.Source.Dead).ToArray();
        if (activeTethers.Length > 0)
            yield return new("tactical/tether/center", "连线站位", new CircleTemplate(19), Arena!.Position, Display: false);
        foreach (var tether in activeTethers)
        {
            var self = tether.Target.Id == PlayerId && !tether.TargetRemoved;
            var source = tether.Source.Position;
            yield return new($"tactical/tether/{tether.Id}/display", "Tether", new CircleTemplate(19), source,
                Navigate: false, OutlineOnly: self, Activation: tether.Activation);
            if (!self)
                yield return new($"tactical/tether/{tether.Id}/avoid", "Tether", new CircleTemplate(19), source,
                    Display: false, Activation: tether.Activation);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"tactical/tether/{tether.Id}/separate/{buddy.Id}", "Tether", new CircleTemplate(19),
                        buddy.Position, Display: false, Activation: tether.Activation);
            foreach (var line in TacticalLine($"tether/{tether.Id}/line", tether.Source.Position, tether.Target.Position)) yield return line;
        }
    }

    private IEnumerable<DungeonAmbientAoe> TacticalTowerAoes()
    {
        if (!_actors.TryGetValue(PlayerId, out var viewer)) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var group in tacticalTowers.GroupBy(t => t.Cast.Rule.Action))
        {
            var enter = new List<IGeometryShape>();
            var avoid = new List<TacticalAoe>();
            foreach (var tower in group)
            {
                var inside = NativeEnemyDistanceSquared(viewer.Position, tower.Position) <= 16;
                var count = party.Count(a => NativeEnemyDistanceSquared(a.Position, tower.Position) <= 16);
                var safe = count < 1 || inside && count == 1;
                yield return new($"tactical/tower/{tower.Id}/display", "Thundering Pillar", new CircleTemplate(4), tower.Position,
                    Navigate: false, Safe: safe, OutlineOnly: safe || inside, Activation: tower.Activation);
                if (safe) enter.Add(new CircleGeometry(tower.Position, 4));
                else avoid.Add(tower);
            }
            if (enter.Count > 0) yield return TacticalEnter($"tower/{group.Key}/enter", [.. enter], group.First().Activation);
            else
                foreach (var tower in avoid)
                    yield return new($"tactical/tower/{tower.Id}/avoid", "避开满塔", new CircleTemplate(4), tower.Position,
                        Display: false, Activation: tower.Activation);
        }
    }

    private IEnumerable<DungeonAmbientAoe> TacticalKnockbackAoes()
    {
        if (!_actors.TryGetValue(PlayerId, out var player)) yield break;
        foreach (var action in new uint[] { 40706, 40722, 40709, 40726 })
        {
            var current = player.Position;
            foreach (var kb in tacticalKnockbacks.Where(k => k.Cast.Rule.Action == action))
            {
                var origin = kb.Cast.Location;
                if (action != 40709)
                {
                    GeometryTemplate shape = action switch
                    {
                        40706 => new CircleTemplate(3),
                        40722 => new FanTemplate(3, 80 * MathF.PI / 180),
                        _ => new FanTemplate(8, 60 * MathF.PI / 180),
                    };
                    var area = shape.Resolve(origin, action == 40722 ? MathF.PI / 2 : 3 * MathF.PI / 2, null);
                    yield return TacticalEnter($"kb/{kb.Cast.Source}/enter", [area], kb.Cast.FinishAt);
                }
                if (WhorlImmune(player, kb.Cast.FinishAt) || kb.Radius is { } radius
                    && NativeEnemyDistanceSquared(current, origin) > radius * radius) continue;
                var delta = current - origin;
                delta.Y = 0;
                if (delta.LengthSquared() == 0) continue;
                var landing = current + Vector3.Normalize(delta) * kb.Distance;
                foreach (var projection in QuestKnockbackProjection($"tactical/kb/{kb.Cast.Source}", player, current, landing))
                    yield return projection;
                current = landing;
            }
        }
    }

    private IEnumerable<DungeonDamagePrediction> TacticalDamage()
    {
        if (!IsTactical || Active == null) yield break;
        foreach (var mark in tacticalMarks.Where(m => m.Icon == 412 && !m.Target.Dead))
            yield return new(mark.Target.Id, mark.Target.PartySlot, mark.Target.PartyRole, mark.Target.Name, mark.Activation, "Tankbuster");
        foreach (var group in tacticalMarks.Where(m => m.Icon is 139 or 375 && !m.Target.Dead).GroupBy(m => m.Icon))
            foreach (var mark in group.Where(m => m.Target.PartySlot >= 0).DistinctBy(m => m.Target.PartySlot))
                yield return new(mark.Target.Id, mark.Target.PartySlot, mark.Target.PartyRole, mark.Target.Name,
                    group.Min(m => m.Activation), "Raidwide");
        foreach (var tether in tacticalTethers.Where(t => !t.Source.Dead))
            yield return new(tether.Target.Id, tether.Target.PartySlot, tether.Target.PartyRole, tether.Target.Name, tether.Activation, "Tankbuster");
        foreach (var group in tacticalTowers.GroupBy(t => t.Cast.Rule.Action))
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && group.Any(t => NativeEnemyDistanceSquared(a.Position, t.Position) <= 16)))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, group.First().Activation, "Raidwide");
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 40761))
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Raidwide");
    }

    private IEnumerable<string> TacticalHints(Vector3? position)
    {
        if (!IsTactical || Active == null || position is not { } player) yield break;
        if (tacticalIntercepts.Count == 1) yield return "Grab the tether!";
        if (_casts.Values.Any(c => c.Rule.Action == 40761)) yield return "Raidwide";
        if (tacticalMarks.Any(m => m.Icon is 139 or 375 && m.Target.Id == PlayerId && !m.Removed)) yield return "Spread!";
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var group in tacticalMarks.Where(m => m.Icon is 318 or 317 && !m.Target.Dead).GroupBy(m => m.Icon))
        {
            var self = group.Any(m => m.Target.Id == PlayerId && !m.Removed);
            var participating = group.Count(m => NativeEnemyDistanceSquared(player, QuantizedDungeonPosition(m.Target.Position)) <= 36);
            var unsatisfied = group.Any(m => 1 + party.Count(a => a.Id != m.Target.Id
                && NativeEnemyDistanceSquared(a.Position, QuantizedDungeonPosition(m.Target.Position)) <= 36) < 2);
            if (self || participating > 0 || unsatisfied) yield return "Stack!";
        }
        if (tacticalMarks.Any(m => m.Icon is 139 or 375 && !m.Target.Dead
            && (m.Target.Id != PlayerId || m.Removed)
            && NativeEnemyDistanceSquared(player, QuantizedDungeonPosition(m.Target.Position)) <= 36))
            yield return "GTFO from spreads!";
        if (tacticalChasers.Any(c => NativeEnemyDistanceSquared(player, TacticalChaserPosition(c)) <= 16))
            yield return "GTFO from chasing aoe!";
        foreach (var group in tacticalTowers.GroupBy(t => t.Cast.Rule.Action))
        {
            var inside = group.FirstOrDefault(t => NativeEnemyDistanceSquared(player, t.Position) <= 16);
            if (inside != null)
                yield return party.Count(a => NativeEnemyDistanceSquared(a.Position, inside.Position) <= 16) > 1
                    ? "Too many soakers in the tower!" : "Soak the tower!";
            else if (group.Any(t => !party.Any(a => NativeEnemyDistanceSquared(a.Position, t.Position) <= 16)))
                yield return "Soak the tower!";
        }
        foreach (var bait in TacticalBaitAoes().Where(a => a.Id.EndsWith("/display")))
        {
            var shape = bait.Shape.Resolve(bait.Position, bait.Rotation, null);
            if (bait.OutlineOnly)
            {
                if (party.Any(a => a.Id != PlayerId && shape.Contains(a.Position, 0))) yield return "Bait away from raid!";
            }
            else if (shape.Contains(player, 0)) yield return "GTFO from baited aoe!";
        }
        if (TacticalAoes().Any(a => a.Display && a.Navigate && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player, 0)))
            yield return "GTFO from aoe!";
        if (_actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        foreach (var action in new uint[] { 40706, 40722, 40709, 40726 })
        {
            var current = player;
            foreach (var kb in tacticalKnockbacks.Where(k => k.Cast.Rule.Action == action))
            {
                if (WhorlImmune(viewer, kb.Cast.FinishAt) || kb.Radius is { } radius
                    && NativeEnemyDistanceSquared(current, kb.Cast.Location) > radius * radius) continue;
                var delta = current - kb.Cast.Location;
                delta.Y = 0;
                if (delta.LengthSquared() > 0) current += Vector3.Normalize(delta) * kb.Distance;
                if (Arena?.Contains(current, 0) == false) yield return "About to be knocked into danger!";
            }
        }
    }
}

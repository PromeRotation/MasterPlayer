using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> HydaAoes()
    {
        if (!IsHydaelyn || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Id == PrimaryId || a.Oid == 0x34EC))
            if (!actor.Dead && actor.Targetable)
                yield return new($"hyda/enemy/{actor.Id}", actor.Id == PrimaryId ? "海德林" : "光之水晶",
                    new CircleTemplate(actor.HitboxRadius), actor.Position, Navigate: false, OutlineOnly: true,
                    Color: actor.Id == PrimaryId ? null : new Vector4(1, 128f / 255, 0, 1));
        foreach (var aoe in hydaParhelic) yield return aoe;
        if (hydaWeapon != null) yield return hydaWeapon;
        foreach (var aoe in hydaCharges.Values) yield return aoe;
        foreach (var aoe in hydaBeacons.Values.Take(10)) yield return aoe;
        for (var i = 0; i < hydaWaves.Count; ++i)
        {
            var wave = hydaWaves[i].Actor;
            yield return new($"hyda/wave/{i}", "光波", new RectangleTemplate(16, 28),
                wave.Position + new Vector3(MathF.Sin(wave.Rotation), 0, MathF.Cos(wave.Rotation)) * 2,
                wave.Rotation, Activation: _now.AddSeconds(1.1));
        }
        foreach (var bait in hydaBaits.Where(b => !b.Source.Actor.Dead))
        {
            var source = bait.Source.Actor;
            var target = bait.Target.Actor;
            var angle = source.Id == target.Id ? source.Rotation
                : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var own = bait.Target == hydaActors.GetValueOrDefault(PlayerId);
            var id = $"hyda/sundering/{bait.Key}";
            yield return new(id + "/display", "英雄的斩击", new FanTemplate(40, MathF.PI / 2),
                source.Position, angle, Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "避开扇形死刑", new FanTemplate(40, MathF.PI / 2),
                    source.Position, angle, Display: false, Activation: bait.At);
            else if (source.Id != target.Id)
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "死刑引导远离队友", new FanTemplate(100, MathF.PI / 2),
                        source.Position, MathF.Atan2(buddy.Position.X - source.Position.X, buddy.Position.Z - source.Position.Z),
                        Display: false, Activation: bait.At);
        }
        foreach (var aoe in HydaSharedAoes()) yield return aoe;
        foreach (var aoe in HydaPartyAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> HydaSharedAoes()
    {
        if (Arena == null || hydaSharedSource == null || hydaSharedTarget == null
            || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var target = hydaSharedTarget.Actor;
        var own = hydaSharedTarget == hydaActors.GetValueOrDefault(PlayerId);
        var tank = player.PartyRole == "Tank";
        yield return new("hyda/shared/display", "坦克双人分摊", new CircleTemplate(4), target.Position,
            Navigate: false, Safe: tank && !own, OutlineOnly: tank);
        if (own)
        {
            foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && a.PartyRole != "Tank"))
                yield return new($"hyda/shared/buddy/{buddy.Id}", "坦克分摊远离其他队员", new CircleTemplate(4),
                    buddy.Position, Display: false, Activation: hydaSharedAt);
        }
        else if (tank)
            yield return new("hyda/shared/join", "与坦克分摊", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(target.Position, 4)]), Arena.Position, Display: false, Activation: hydaSharedAt);
        else
            yield return new("hyda/shared/avoid", "避开坦克分摊", new CircleTemplate(4), target.Position,
                Display: false, Activation: hydaSharedAt);
    }

    private IEnumerable<DungeonAmbientAoe> HydaPartyAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var marks = hydaMarks.Where(m => !m.Target.Actor.Dead).ToArray();
        foreach (var spread in marks.Where(m => m.Action == 27738))
        {
            var own = spread.Target == hydaActors.GetValueOrDefault(PlayerId);
            var id = $"hyda/spread/{spread.Key}";
            yield return new(id + "/display", "水晶冰分散", new CircleTemplate(5),
                QuantizedDungeonPosition(spread.Target.Actor.Position), Navigate: false, OutlineOnly: true);
            if (party.Length < 2) continue;
            if (!own)
                yield return new(id + "/avoid", "避开水晶冰", new CircleTemplate(6),
                    QuantizedDungeonPosition(spread.Target.Actor.Position), Display: false, Activation: spread.At);
            else
                foreach (var buddy in party.Where(a => !marks.Any(m => m.Action == 27738 && m.Target == hydaActors.GetValueOrDefault(a.Id))))
                    yield return new(id + $"/buddy/{buddy.Id}", "分散远离队友", new CircleTemplate(6),
                        QuantizedDungeonPosition(buddy.Position), Display: false, Activation: spread.At);
        }
        foreach (var action in new[] { 27737u, 26065u })
        {
            var stacks = marks.Where(m => m.Action == action).ToArray();
            var marked = stacks.Any(m => m.Target == hydaActors.GetValueOrDefault(PlayerId));
            var joins = new List<IGeometryShape>();
            foreach (var stack in stacks)
            {
                var own = stack.Target == hydaActors.GetValueOrDefault(PlayerId);
                var position = QuantizedDungeonPosition(stack.Target.Actor.Position);
                var circle = new CircleGeometry(position, 6);
                var count = party.Count(a => circle.Contains(a.Position));
                var join = circle.Contains(player.Position) ? count <= 8 : count < 8;
                var id = $"hyda/stack/{action}/{stack.Key}";
                yield return new(id + "/display", action == 26065 ? "回声五连分摊" : "水晶土八人分摊",
                    new CircleTemplate(6), position, Navigate: false, Safe: own || !marked && join, OutlineOnly: true);
                if (party.Length < 2) continue;
                if (own)
                {
                    var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target == hydaActors.GetValueOrDefault(a.Id)))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                    if (buddies.Length > 0)
                        yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                            Arena.Position, Display: false, Activation: stack.At);
                }
                else if (join) joins.Add(circle);
                else
                    yield return new(id + "/avoid", "避开满员分摊", new CircleTemplate(marked ? 12 : 6), position,
                        Display: false, Activation: stack.At);
            }
            if (joins.Count > 0)
                yield return new($"hyda/stack/{action}/join", "进入八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                    Arena.Position, Display: false, Activation: stacks[0].At);
        }
    }

    private IEnumerable<DungeonDamagePrediction> HydaDamage()
    {
        if (!IsHydaelyn || Active == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        if (hydaSharedSource != null)
            foreach (var actor in party.Where(a => a.PartyRole == "Tank"))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, hydaSharedAt, "Tankbuster");
        if (hydaExodusAt != default)
            foreach (var actor in party)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, hydaExodusAt, "Raidwide");
        foreach (var bait in hydaBaits.Where(b => !b.Source.Actor.Dead).DistinctBy(b => b.Target))
            if (bait.Target.Actor is { PartySlot: >= 0 } actor)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, hydaBaits[0].At, "Tankbuster");
    }

    private IEnumerable<string> HydaHints()
    {
        if (!IsHydaelyn || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (hydaExodusAt != default) yield return "出离：全屏伤害";
        if (hydaSharedSource != null && hydaSharedTarget != null)
            yield return player.PartyRole == "Tank" ? "与另一坦克分摊或使用无敌" : "远离坦克分摊";
        if (hydaBaits.Count > 0) yield return "扇形死刑";
        if (hydaMarks.Any(m => m.Action == 26065 && !m.Target.Actor.Dead)) yield return $"回声五连分摊：已结算 {hydaEchoHits}/5";
        if (hydaMarks.Any(m => m.Action == 27737 && !m.Target.Actor.Dead)) yield return "水晶土：八人分摊";
        if (hydaMarks.Any(m => m.Action == 27738 && !m.Target.Actor.Dead)) yield return "水晶冰：分散";
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var action in new[] { 27737u, 26065u })
        {
            var stacks = hydaMarks.Where(m => m.Action == action && !m.Target.Actor.Dead).ToArray();
            var own = stacks.Where(m => m.Target == hydaActors.GetValueOrDefault(PlayerId)).ToArray();
            if (own.Length > 0)
            {
                var position = QuantizedDungeonPosition(own[0].Target.Actor.Position);
                var inside = party.Where(a => Inside(a.Position, position)).ToArray();
                if (inside.Length != 8) yield return $"分摊人数不足或超员：{inside.Length}/8";
                if (inside.Any(a => a.Id != PlayerId && stacks.Any(m => m.Target == hydaActors.GetValueOrDefault(a.Id))))
                    yield return "分摊点名重叠：分开各自分摊圈";
            }
            else
            {
                var participating = stacks.Count(m => Inside(player.Position, QuantizedDungeonPosition(m.Target.Actor.Position)));
                if (participating > 1) yield return "同时位于多个分摊圈：只参与一个分摊";
                if (participating == 0 && stacks.Any(m => 1 + party.Count(a =>
                    hydaActors.GetValueOrDefault(a.Id) != m.Target && Inside(a.Position, QuantizedDungeonPosition(m.Target.Actor.Position))) < 8))
                    yield return "进入人数不足的分摊圈";
            }
        }
        static bool Inside(Vector3 a, Vector3 b) => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z)) <= 36;
    }
}

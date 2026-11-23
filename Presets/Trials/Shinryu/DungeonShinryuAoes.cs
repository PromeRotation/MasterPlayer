using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ShinryuAoes()
    {
        if (!IsShinryu || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x1983 or 0x1B16 or 0x1B13 or 0x1C86 or 0x1B14 or 0x1C83
            && a.Targetable && !a.Dead))
            yield return new($"shinryu/enemy/{actor.Id}", "神龙及增援", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true);
        var puddles = _actors.Values.Where(a => a.Oid == 0x1E950D && !a.Dead).ToArray();
        foreach (var puddle in puddles)
            yield return new($"shinryu/water/{puddle.Id}", "元素水圈", new CircleTemplate(5), puddle.Position,
                Navigate: shinryuLightning, OutlineOnly: !shinryuLightning && !shinryuFire,
                Safe: !shinryuLightning && shinryuFire, Activation: shinryuWaterAt,
                Color: !shinryuLightning && !shinryuFire ? new Vector4(1, 128f / 255, 0, 1) : null);
        if (shinryuFire && !shinryuLightning && puddles.Length > 0)
            yield return new("shinryu/fire/join", "火焰时进入任一水圈", new SohrKhaiWallHintTemplate(Arena,
                puddles.Select(p => (IGeometryShape)new CircleGeometry(p.Position, 4.5f)).ToArray()),
                Arena.Position, Display: false, Activation: shinryuWaterAt);
        // With both flags set, the source submits union and intersection of the same circles.
        // The union already forbids the whole intersection; retain its lightning geometry and shared clock.
        foreach (var ice in shinryuIce)
            yield return new($"shinryu/ice/{ice.Key}", "落冰", new CircleTemplate(6), ice.Position, Activation: ice.At);
        foreach (var cast in shinryuCastOrder.Where(c => c.Rule.Action == 8097).Take(3))
            yield return new($"shinryu/thrust/{cast.Source}", "冰柱突刺", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        foreach (var aoe in ShinryuIconAoes()) yield return aoe;
        foreach (var aoe in ShinryuTankAoes()) yield return aoe;
        foreach (var aoe in ShinryuMovementAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> ShinryuIconAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var stacks = shinryuIcons.Where(b => b.Icon == 62 && !b.Target.Dead).ToArray();
        var marked = stacks.Any(b => b.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var bait in shinryuIcons)
        {
            var target = bait.Target;
            if (target.Dead) continue;
            var id = $"shinryu/icon/{bait.Key}";
            if (bait.Icon == 23)
            {
                if ((_actors.GetValueOrDefault(PrimaryId) ?? shinryuActors.GetValueOrDefault(PrimaryId)) is not { Dead: false } boss) continue;
                var rotation = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
                var shape = new FanTemplate(60, MathF.PI / 3);
                yield return new(id + "/display", "大地吐息引导", shape, boss.Position, rotation,
                    Navigate: false, OutlineOnly: target.Id == PlayerId);
                if (target.Id != PlayerId)
                    yield return new(id + "/avoid", "远离他人吐息", shape, boss.Position, rotation, Display: false, Activation: bait.At);
                else
                    foreach (var buddy in party.Where(a => a.Id != PlayerId))
                        yield return new(id + "/buddy/" + buddy.Id, "吐息远离队友", new FanTemplate(100, MathF.PI / 3), boss.Position,
                            MathF.Atan2(buddy.Position.X - boss.Position.X, buddy.Position.Z - boss.Position.Z), Display: false, Activation: bait.At);
                continue;
            }
            var position = QuantizedDungeonPosition(target.Position);
            var stack = bait.Icon == 62;
            yield return new(id + "/display", stack ? "至少两人分摊" : "雷电分散", new CircleTemplate(stack ? 7 : 5),
                position, Navigate: false, OutlineOnly: true, Safe: stack && (target.Id == PlayerId || !marked));
            if (!stack)
            {
                if (target.Id != PlayerId)
                    yield return new(id + "/avoid", "避开雷电分散", new CircleTemplate(6), position, Display: false, Activation: bait.At);
                else
                    foreach (var buddy in party.Where(a => !shinryuIcons.Any(b => b.Icon == 24 && b.Target.Id == a.Id)))
                        yield return new(id + "/buddy/" + buddy.Id, "雷电分散远离队友", new CircleTemplate(6), QuantizedDungeonPosition(buddy.Position),
                            Display: false, Activation: bait.At);
            }
            else if (party.Length > 1)
            {
                if (target.Id == PlayerId)
                {
                    var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target.Id == a.Id))
                        .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3.5f)).ToArray();
                    if (buddies.Length > 0)
                        yield return new(id + "/buddy", "点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                            Arena.Position, Display: false, Activation: bait.At);
                }
                else joins.Add(new CircleGeometry(target.Position, 7));
            }
        }
        if (joins.Count > 0)
            yield return new("shinryu/stack/join", "进入分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].At);
    }

    private IEnumerable<DungeonAmbientAoe> ShinryuTankAoes()
    {
        if (Arena == null || shinryuTankCast is not { } cast || shinryuTankTarget is not { } target) yield break;
        var player = _actors.GetValueOrDefault(PlayerId);
        var tank = player != null && ThornTank(player);
        var shape = new CircleTemplate(5);
        yield return new("shinryu/tank/display", "坦克分摊", shape, target.Position,
            Navigate: false, OutlineOnly: tank, Safe: tank && target.Id != PlayerId);
        if (PlayerId != target.Id)
            yield return new("shinryu/tank/position", tank ? "坦克进入分摊" : "远离坦克分摊",
                tank ? new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(target.Position, 5)]) : shape,
                tank ? Arena.Position : target.Position, Display: false, Activation: cast.FinishAt);
        else
            foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !ThornTank(a)))
                yield return new($"shinryu/tank/avoid/{buddy.Id}", "坦克分摊远离非坦克", shape, buddy.Position,
                    Display: false, Activation: cast.FinishAt);
    }

    private IEnumerable<string> ShinryuHints()
    {
        if (!IsShinryu || Active == null) yield break;
        if (_actors.Values.Any(a => a.Oid == 0x1E950D && !a.Dead))
        {
            if (shinryuLightning) yield return "雷电时远离水圈";
            else if (shinryuFire) yield return "火焰时站进水圈";
        }
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (Arena != null && ShinryuKnockbacks().Any(k => !Arena.Contains(k.To))) yield return "击退落点将出界";
        if (ShinryuSlide(player) is { } landing && Arena?.Contains(landing) == false) yield return "薄冰滑行将出界";
        foreach (var tether in shinryuTethers.Values.Where(t => t.Player == PlayerId))
            if (shinryuActors.GetValueOrDefault(tether.Enemy) is { Dead: false } enemy
                && Vector3.DistanceSquared(player.Position, enemy.Position) < 900) yield return "拉开三十码断链";
        foreach (var bait in shinryuIcons.Where(b => !b.Target.Dead))
        {
            if (bait.Icon == 24)
            {
                if (bait.Target.Id == PlayerId
                    ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && Vector3.DistanceSquared(a.Position, player.Position) <= 25)
                    : Vector3.DistanceSquared(bait.Target.Position, player.Position) <= 25) yield return "雷电分散";
            }
            if (bait.Icon == 62)
            {
                var circle = new CircleGeometry(bait.Target.Position, 7);
                var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && circle.Contains(a.Position));
                if (bait.Target.Id == PlayerId && (count < 2 || shinryuIcons.Any(b => b.Icon == 62 && b.Key != bait.Key && !b.Target.Dead && circle.Contains(b.Target.Position)))
                    || bait.Target.Id != PlayerId && !circle.Contains(player.Position) && count < 2) yield return "至少两人分摊";
            }
        }
        foreach (var aoe in ShinryuIconAoes().Where(a => a.Id.EndsWith("/display", StringComparison.Ordinal) && a.Shape is FanTemplate))
        {
            var own = aoe.OutlineOnly;
            var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            if (own ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))
                : shape.Contains(player.Position)) yield return own ? "吐息引导远离队友" : "避开他人吐息";
        }
        if (shinryuTankTarget is { } target)
        {
            var circle = new CircleGeometry(target.Position, 5);
            if (target.Id == PlayerId && !_actors.Values.Any(a => a.Id != PlayerId && a.PartySlot >= 0 && ThornTank(a) && circle.Contains(a.Position)))
                yield return "与另一坦克分摊或使用无敌";
            else if (target.Id != PlayerId && (ThornTank(player) ? !circle.Contains(player.Position) : circle.Contains(player.Position)))
                yield return ThornTank(player) ? "与坦克分摊" : "远离坦克分摊";
        }
    }
}

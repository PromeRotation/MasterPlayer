using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> UltimaAoes()
    {
        var aoes = UltimaMechanicAoes().ToArray();
        foreach (var aoe in aoes) yield return aoe;
        if (!IsUltimaExtreme || Active == null || _actors.GetValueOrDefault(PrimaryId) is not { } boss
            || _actors.GetValueOrDefault(PlayerId) is not { } player || boss.TargetId == PlayerId
            || !aoes.Any(a => a.Id == "ultima/viscous/avoid")
            || aoes.Count(a => a.Navigate) + _casts.Values.Count(c => c.Rule.Shape != null) != 1
            || _actors.Values.Any(a => a.Oid == 0x915 && !a.Dead)) yield break;
        var range = CurrentAssignment is "M1" or "M2" ? 7 : 15;
        var degrees = CurrentAssignment switch
        {
            "M1" => 45, "M2" => -45, "R1" => 30, "R2" => -30, "H1" => 10, "H2" => -10, _ => 0,
        };
        var angle = degrees * MathF.PI / 180;
        var offset = degrees == 0 ? new Vector3(0, 0, UltimaTankSwap ? -2 : 7) : new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * range;
        // MaxValue in the source is an idle positioning preference, below live danger avoidance.
        yield return new("ultima/idle", "常态分工站位", new CircleTemplate(1.5f), boss.Position + offset,
            Display: false, Safe: true, GoalPriority: 10);
    }

    private IEnumerable<DungeonAmbientAoe> UltimaMechanicAoes()
    {
        if (!IsUltimaExtreme || Active == null || Arena == null || _actors.GetValueOrDefault(PrimaryId) is not { } boss) yield break;
        var player = _actors.GetValueOrDefault(PlayerId);
        if (UltimaMistralActive && ultimaGaruda is { } garuda)
        {
            yield return new("ultima/mistral/display", "寒风之歌", new FanTemplate(23.4f, MathF.PI * 5 / 6),
                garuda.Position, garuda.Rotation, Navigate: false, Activation: ultimaMistralAt);
            if (ultimaBurstImminent)
            {
                var a = garuda.Rotation + MathF.PI * 5 / 12; var b = garuda.Rotation - MathF.PI * 5 / 12;
                var p1 = Arena.Position + new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * 20;
                var p2 = Arena.Position + new Vector3(MathF.Sin(b), 0, MathF.Cos(b)) * 20;
                var a1 = MathF.Atan2(p1.X - boss.Position.X, p1.Z - boss.Position.Z);
                var a2 = MathF.Atan2(p2.X - boss.Position.X, p2.Z - boss.Position.Z);
                if (a2 > a1) a2 -= MathF.Tau;
                yield return new("ultima/mistral/avoid", "击退后避开寒风", new FanTemplate(40, a1 - a2), boss.Position,
                    (a1 + a2) / 2, Display: false, Activation: ultimaMistralAt);
                if (player != null)
                {
                    var delta = player.Position - boss.Position; delta.Y = 0;
                    var end = player.Position + (delta.LengthSquared() == 0 ? Vector3.Zero : Vector3.Normalize(delta) * 30);
                    var local = end - Arena.Position; local.Y = 0;
                    if (local.LengthSquared() > 400) end = Arena.Position + Vector3.Normalize(local) * 20;
                    foreach (var aoe in DungeonMovementPreview("ultima/mistral/knockback", "击退30码后寒风", player.Position, end)) yield return aoe;
                }
            }
            else
                yield return new("ultima/mistral/avoid", "避开寒风", new FanTemplate(23.4f, MathF.PI * 5 / 6),
                    garuda.Position, garuda.Rotation, Display: false, Activation: ultimaMistralAt);
        }
        if (ultimaEye is { } eye)
        {
            var at = _casts.GetValueOrDefault(eye.Id)?.FinishAt ?? eye.Cast?.FinishAt ?? default;
            yield return new("ultima/eye/display", "暴风圈", new DonutTemplate(12, 25), eye.Position, Navigate: false, Activation: at);
            yield return new("ultima/eye/inner", "站在暴风内缘", new CircleTemplate(10), eye.Position, Display: false, Activation: at);
            yield return new("ultima/eye/outer", "站在暴风内缘", new DonutTemplate(12, 100), eye.Position, Display: false, Activation: at);
        }
        else if (ultimaGeocrush is { } titan)
            yield return new("ultima/geocrush", "大地之怒距离衰减", new CircleTemplate(18), titan.Position,
                Activation: _casts.GetValueOrDefault(titan.Id)?.FinishAt ?? titan.Cast?.FinishAt ?? default);
        if (ultimaIfrit is { } ifrit)
        {
            var dir = new Vector3(MathF.Sin(ifrit.Rotation), 0, MathF.Cos(ifrit.Rotation));
            yield return new("ultima/cyclone", "深红旋风", new RectangleTemplate(12, 48), ifrit.Position + dir * 19,
                ifrit.Rotation, Activation: ultimaCycloneAt);
        }
        for (var i = 0; i < ultimaFreefires.Count; ++i)
        {
            var invuln = player is { ClassJob: 19 or 21 } && boss.TargetId == PlayerId && ultimaFreefireHits > 0;
            yield return new($"ultima/freefire/{i}", "飞空艇坠落", new CircleTemplate(15), ultimaFreefires[i].Position,
                Navigate: !invuln, Activation: ultimaFreefireAt);
        }
        foreach (var orb in _actors.Values.Where(a => a.Oid == 0x913 && !ultimaExploded.Contains(a.Id)))
        {
            yield return new($"ultima/kite/{orb.Id}/display", "追踪魔科学物质", new CircleTemplate(6), orb.Position,
                Navigate: false, OutlineOnly: true);
            yield return new($"ultima/kite/{orb.Id}/avoid", "远离追踪球", new CircleTemplate(7), orb.Position, Display: false);
            var candidates = _actors.Values.Where(a => a.PartySlot >= 0 && ultimaKiters.Contains(a.Id) && !a.Dead);
            float Dot(DungeonActor a)
            {
                var delta = a.Position - orb.Position; delta.Y = 0;
                if (delta.LengthSquared() > 0) delta = Vector3.Normalize(delta);
                return Vector3.Dot(delta, new(MathF.Sin(orb.Rotation), 0, MathF.Cos(orb.Rotation)));
            }
            var kiter = candidates.MaxBy(Dot);
            if (kiter == null) continue;
            var length = new Vector2(kiter.Position.X - orb.Position.X, kiter.Position.Z - orb.Position.Z).Length();
            if (length > 0)
            {
                var rotation = MathF.Atan2(kiter.Position.X - orb.Position.X, kiter.Position.Z - orb.Position.Z);
                yield return new($"ultima/kite/{orb.Id}/line", "追踪方向", RayRectangleTemplate.Symmetric(.08f, length),
                    orb.Position, rotation, Navigate: false, OutlineOnly: true);
                if (kiter.Id != PlayerId)
                    yield return new($"ultima/kite/{orb.Id}/corridor", "避开追踪路线", RayRectangleTemplate.Symmetric(4, length),
                        orb.Position, rotation, Display: false);
            }
        }
        foreach (var id in ultimaActiveOrbs.Distinct())
            if (_actors.GetValueOrDefault(id) is { } orb)
            {
                yield return new($"ultima/boom/orb/{id}", "魔科学爆弹分摊", new CircleTemplate(8), orb.Position, Navigate: false, OutlineOnly: true);
                if (UltimaOrbsActive && boss.TargetId == PlayerId)
                    yield return new($"ultima/boom/tank/{id}", "主坦远离爆弹", new CircleTemplate(3), orb.Position, Display: false);
            }
        if (UltimaOrbsActive && player != null && boss.TargetId != PlayerId)
        {
            if (UltimaBoomCasting && ultimaBoomHits < 2)
                yield return new("ultima/boom/south", "向首领南侧集合击退", new FanTemplate(50, MathF.PI * 17 / 9),
                    boss.Position, MathF.PI, Display: false);
            else if (UltimaBoomCasting || ultimaWaitingOrbs || ultimaBoomHits == 3)
            {
                var x = CurrentAssignment is "MT" or "H1" or "M1" or "R1" ? 1 : -1;
                var target = ultimaBoomHits >= 2 && UltimaBoomCasting || ultimaBoomHits == 3;
                var position = target && CurrentAssignment is "M1" or "M2" ? new Vector3(10 * x, player.Position.Y, -2) : new Vector3(2 * x, player.Position.Y, 10);
                yield return new("ultima/boom/preposition", "爆弹分组站位", new DonutTemplate(1.5f, 100), position, Display: false);
            }
            else if (ultimaPopOrbs.Count > 0 && _actors.GetValueOrDefault(ultimaPopOrbs[0]) is { } next)
            {
                var nearby = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead
                    && Vector2.Distance(new(a.Position.X, a.Position.Z), new(next.Position.X, next.Position.Z)) <= 8);
                var pop = (ThornTank(player) || TwinCombatRole(player) == "Melee") && nearby > 5;
                var offset = pop ? Vector3.Zero : new Vector3(MathF.Sin(next.Rotation), 0, MathF.Cos(next.Rotation));
                yield return new("ultima/boom/pop", pop ? "六人集合后触碰爆弹" : "跟随队伍接近爆弹",
                    new DonutTemplate(pop ? 1.5f : 6, 100), next.Position + offset, Display: false);
            }
        }
        if (boss.Targetable && _actors.GetValueOrDefault(boss.TargetId) is { } tank)
        {
            yield return new("ultima/viscous/display", "粘性魔科学物质顺劈", new CircleTemplate(2), QuantizedDungeonPosition(tank.Position),
                Navigate: false, OutlineOnly: true);
            if (tank.Id != PlayerId)
                yield return new("ultima/viscous/avoid", "远离主坦", new CircleTemplate(2), QuantizedDungeonPosition(tank.Position), Display: false);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"ultima/viscous/party/{member.Id}", "顺劈远离队友", new CircleTemplate(2),
                        QuantizedDungeonPosition(member.Position), Display: false);
        }
    }
}

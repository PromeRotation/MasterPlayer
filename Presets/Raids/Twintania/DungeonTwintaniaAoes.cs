using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TwinAoes()
    {
        if (!IsTwintania || Active == null || Arena == null) yield break;
        var boss = _actors.GetValueOrDefault(PrimaryId);
        var player = _actors.GetValueOrDefault(PlayerId);
        if (boss == null || player == null) yield break;
        var role = CoilRole(player);
        var isConflag = TwinP2 && twinConflag == PlayerId;
        if (TwinCommonActive)
            foreach (var aoe in TwinPlummet(boss, isConflag)) yield return aoe;
        if (twinStage == 0 || TwinP5)
        {
            var action = twinStage == 0 ? 1243u : 670u;
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == action))
                yield return new($"twin/liquid/cast/{cast.Source}", "液体地狱", new CircleTemplate(6),
                    _actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location, Activation: cast.FinishAt);
            foreach (var p in twinPuddles)
                yield return new($"twin/liquid/event/{p.Key}", "液体地狱生成", new CircleTemplate(6), p.Position, Activation: p.At);
            foreach (var a in _actors.Values.Where(a => a.Oid == 0x1E88FE && a.EventState != 7))
                yield return new($"twin/liquid/live/{a.Id}", "液体地狱", new CircleTemplate(6), QuantizedDungeonPosition(a.Position));
        }
        if (twinStage == 0 && !_actors.Values.Any(a => a.Oid == 0x7E7 && a.Hostile && a.Targetable && !a.Dead)
            && TwinCombatRole(player) != "Tank")
            yield return TwinInside("p1/gather", "靠近首领准备火球", boss.Position, 8, DateTime.MaxValue);
        if (TwinP2)
        {
            if (TwinMarked(twinFireball) is { } fireball)
                yield return new("twin/fireball/outline", "火球分摊", new CircleTemplate(4), fireball.Position, Safe: true, Navigate: false, OutlineOnly: true);
            if (isConflag)
                yield return TwinInside("conflag/personal", "火牢进入首领中心", boss.Position, 1, twinConflagAt);
            else
            {
                foreach (var a in _actors.Values.Where(a => a.Oid == 0x7F2 && a.Hostile && a.Targetable && !a.Dead))
                    yield return new($"twin/conflag/{a.Id}", "火牢", new CircleTemplate(6), a.Position, Display: false);
                if (role != TwinTankRole)
                {
                    var fireballRole = TwinMarked(twinFireball) is { } target ? TwinCombatRole(target) : "";
                    var combatRole = TwinCombatRole(player);
                    var left = combatRole == "Healer" || combatRole != "Ranged" && fireballRole == "Healer";
                    var tank = _actors.GetValueOrDefault(boss.TargetId);
                    var angle = tank != null ? MathF.Atan2(tank.Position.X - boss.Position.X, tank.Position.Z - boss.Position.Z) : boss.Rotation;
                    angle += (left ? 1 : -1) * MathF.PI * .75f;
                    var destination = boss.Position + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 8;
                    yield return TwinInside("fireball/position", "火球分组站位", destination, 2,
                        twinFireball != 0 ? twinFireballAt : DateTime.MaxValue);
                }
            }
        }
        if (TwinDiveActive && twinDiveTarget is { } aim)
        {
            var angle = MathF.Atan2(aim.X - boss.Position.X, aim.Z - boss.Position.Z);
            var at = twinDiveAt;
            if (_casts.GetValueOrDefault(PrimaryId) is { } cast)
            {
                angle = cast.Rotation; at = cast.FinishAt;
            }
            else if (boss.Cast is { } raw && CoilCastActive(boss))
            {
                angle = raw.Rotation ?? angle; at = raw.FinishAt;
            }
            yield return new("twin/divebomb", "俯冲", RayRectangleTemplate.Symmetric(12, 35), boss.Position, angle, Activation: at);
        }
        if (twinAddsActive)
        {
            foreach (var a in _actors.Values.Where(a => a.Oid == 0x7E8 && !a.Dead))
            {
                yield return new($"twin/hygieia/outline/{a.Id}", "小蛇死亡爆炸", new CircleTemplate(8), a.Position,
                    Navigate: false, OutlineOnly: true);
                var predicted = (long)(a.Hp ?? 0) + (a.IncomingEffects?.Sum(e => e.HpDelta) ?? 0);
                if (a.Hostile && a.Targetable && predicted <= (role == "OT" ? 1 : .1f * (a.MaxHp ?? 0)))
                    yield return new($"twin/hygieia/avoid/{a.Id}", "回避濒死小蛇", new CircleTemplate(9), a.Position, Display: false);
            }
            if (!boss.Targetable && !_actors.Values.Any(a => a.Oid is 0x7E8 or 0x7E9 && !a.Dead))
                yield return TwinInside("adds/gather", "集合首领重现点", new(-6.67f, boss.Position.Y, 5), 5, DateTime.MaxValue);
        }
        if (twinStage == 17)
        {
            var closest = TwinNeurolinks.MinBy(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(boss.Position.X, boss.Position.Z)));
            foreach (var a in TwinNeurolinks)
            {
                yield return new($"twin/profusion/outline/{a.Id}", "拘束装置", new CircleTemplate(2), a.Position,
                    Safe: true, Navigate: false, OutlineOnly: true);
                if ((a.Id == closest?.Id) == (role != "MT"))
                    yield return TwinInside($"profusion/position/{a.Id}", "以太失控进入拘束装置", a.Position, 2, twinProfusionAt);
            }
        }
        if (TwinP4)
        {
            for (var i = 0; i < twinTwisterPositions.Count; ++i)
            {
                var p = twinTwisterPositions[i];
                yield return new($"twin/twister/prediction/{i}", "旋风落点", new CircleTemplate(2), p.Position, Display: false, Activation: p.At);
            }
            foreach (var t in _actors.Values.Where(a => a.Oid == 0x1E8910 && a.EventState != 7))
            {
                yield return new($"twin/twister/outline/{t.Id}", "旋风", new CircleTemplate(t.HitboxRadius), t.Position,
                    Navigate: false, OutlineOnly: true);
                yield return new($"twin/twister/avoid/{t.Id}", "旋风避让", new CircleTemplate(t.HitboxRadius + 1), t.Position, Display: false);
            }
            foreach (var knight in _actors.Values.Where(a => a.Oid == 0x7EA && !a.Dead))
            {
                if (TwinMarked(twinDreadTarget) is { } target)
                {
                    if (TwinLine($"dreadknight/line/{knight.Id}", "恐慌骑士目标", knight.Position, target.Position) is { } line) yield return line;
                }
                else
                    yield return new($"twin/dreadknight/avoid/{knight.Id}", "远离未点名骑士", new CircleTemplate(15), knight.Position, Display: false);
            }
        }
        if (TwinP5)
            foreach (var aoe in TwinHatch(boss, player, role)) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> TwinPlummet(DungeonActor boss, bool navigationCleared)
    {
        foreach (var enemy in _actors.Values.Where(a => a.Oid == 0x7E5 && !a.Dead && a.Targetable))
        {
            if (_actors.GetValueOrDefault(enemy.TargetId) is not { } tank) continue;
            var origin = QuantizedDungeonPosition(enemy.Position);
            var angle = MathF.Atan2(tank.Position.X - enemy.Position.X, tank.Position.Z - enemy.Position.Z);
            yield return new($"twin/plummet/outline/{enemy.Id}", "垂直下落顺劈", RayRectangleTemplate.Symmetric(12, 20),
                origin, angle, Navigate: !navigationCleared && tank.Id != PlayerId, OutlineOnly: true, Activation: twinNextPlummet);
            if (navigationCleared || tank.Id != PlayerId) continue;
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                var distance = Vector2.Distance(new(a.Position.X, a.Position.Z), new(enemy.Position.X, enemy.Position.Z));
                var halfAngle = MathF.Asin(6 / distance);
                if (!float.IsFinite(halfAngle)) continue;
                var direction = MathF.Atan2(a.Position.X - enemy.Position.X, a.Position.Z - enemy.Position.Z);
                yield return new($"twin/plummet/bait/{enemy.Id}/{a.Id}", "顺劈远离队友", new FanTemplate(100, halfAngle * 2),
                    origin, direction, Display: false);
            }
        }
    }

    private IEnumerable<DungeonAmbientAoe> TwinHatch(DungeonActor boss, DungeonActor player, string role)
    {
        var target = TwinMarked(twinHatchTarget);
        var under = TwinNeurolinks.FirstOrDefault(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(boss.Position.X, boss.Position.Z)) <= 1);
        var forbid = true;
        foreach (var link in TwinNeurolinks)
            yield return new($"twin/hatch/outline/{link.Id}", "拘束装置", new CircleTemplate(2), link.Position,
                Safe: target?.Id == PlayerId, Navigate: false, OutlineOnly: true);
        if (target != null)
        {
            foreach (var orb in _actors.Values.Where(a => a.Oid == 0x7F3))
                if (TwinLine($"hatch/line/{orb.Id}", "孵化追踪", orb.Position, target.Position) is { } line) yield return line;
            if (target.Id == PlayerId)
            {
                forbid = false;
                var shelters = TwinNeurolinks.Where(a => a.Id != under?.Id).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                yield return new("twin/hatch/target", "孵化进入安全拘束装置", new SohrKhaiWallHintTemplate(Arena!, shelters), player.Position, Display: false);
            }
            else if (role == (twinTankedByOT ? "MT" : "OT") && under != null && twinLiquidTarget != PlayerId)
            {
                // Source skips the general orb-path branch for an OT even when the link is unsafe.
                if (!_actors.Values.Any(a => a.Oid == 0x1E88FE && a.EventState != 7
                    && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(under.Position.X, under.Position.Z)) <= 36))
                {
                    forbid = false;
                    yield return TwinInside("hatch/intercept", "副坦截球", under.Position, 2, default);
                }
            }
            else
            {
                foreach (var orb in _actors.Values.Where(a => a.Oid == 0x7F3))
                    if (TwinPath($"hatch/orb-path/{orb.Id}", orb.Position, target.Position) is { } path) yield return path;
                var closest = TwinNeurolinks.Where(a => a.Id != under?.Id).MinBy(a =>
                    Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(target.Position.X, target.Position.Z)));
                if (closest != null && TwinPath("hatch/target-path", target.Position, closest.Position) is { } targetPath) yield return targetPath;
            }
        }
        if (forbid)
            foreach (var link in TwinNeurolinks)
                yield return new($"twin/hatch/avoid/{link.Id}", "避开拘束装置", new CircleTemplate(5), link.Position, Display: false);
        if (twinLiquidTarget == PlayerId)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                yield return new($"twin/liquid/bait/{a.Id}", "液体地狱远离队友", new CircleTemplate(6), a.Position, Display: false);
    }

    private DungeonAmbientAoe TwinInside(string id, string name, Vector3 position, float radius, DateTime activation)
        => new("twin/" + id, name, new SohrKhaiWallHintTemplate(Arena!, [new CircleGeometry(position, radius)]), position,
            Display: false, Activation: activation);

    private static DungeonAmbientAoe? TwinLine(string id, string name, Vector3 start, Vector3 end)
    {
        var length = Vector2.Distance(new(start.X, start.Z), new(end.X, end.Z));
        if (length <= .001f) return null;
        return new("twin/" + id, name, RayRectangleTemplate.Symmetric(.06f, length), start,
            MathF.Atan2(end.X - start.X, end.Z - start.Z), Navigate: false);
    }

    private static DungeonAmbientAoe? TwinPath(string id, Vector3 start, Vector3 end)
    {
        var length = Vector2.Distance(new(start.X, start.Z), new(end.X, end.Z));
        if (length <= .001f) return null;
        return new("twin/" + id, "孵化路径避让", RayRectangleTemplate.Symmetric(4, length), start,
            MathF.Atan2(end.X - start.X, end.Z - start.Z), Display: false);
    }

    private IEnumerable<DungeonDamagePrediction> TwinDamage()
    {
        if (!IsTwintania || Active == null) yield break;
        if (twinStage == 17)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, twinProfusionAt, "Raidwide");
        if (TwinP2 && TwinMarked(twinFireball) is { } target)
        {
            var healer = TwinCombatRole(target) == "Healer";
            var bossTarget = _actors.GetValueOrDefault(PrimaryId)?.TargetId;
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
            {
                var role = TwinCombatRole(a);
                if (role == "Healer" ? healer : role == "Ranged" ? !healer : a.Id != bossTarget)
                    yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, twinFireballAt, "Shared");
            }
        }
    }

    private IEnumerable<string> TwinHints()
    {
        if (!IsTwintania || Active == null) yield break;
        if (TwinCommonActive) yield return $"下次死亡宣告约 {(twinNextSentence - _now).TotalSeconds:F1} 秒，当前坦克 {TwinTankRole}";
        if (TwinP2)
        {
            if (twinFireball == 0) yield return $"下次火球约 {(twinNextFireball - _now).TotalSeconds:F1} 秒";
            if (twinConflag == 0) yield return $"下次火牢约 {(twinNextConflag - _now).TotalSeconds:F1} 秒";
        }
        if (TwinP4 && _casts.GetValueOrDefault(PrimaryId)?.Rule.Action == 1249) yield return "旋风：保持移动";
        if (TwinP5 && twinHatchTarget == PlayerId) yield return "孵化：进入拘束装置";
        if (_actors.GetValueOrDefault(PlayerId) is { } player && TwinAoes().Any(a => a.Id.StartsWith("twin/plummet/outline")
            && a.Navigate && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "离开垂直下落顺劈范围";
    }
}

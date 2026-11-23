using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> NativeEnemyMarkers()
    {
        if (Active == null || !(IsUltimaExtreme || IsGarudaExtreme || IsTitanExtreme || IsIfritExtreme
            || IsCaduceus || IsTwintania)) yield break;
        var shape = new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]);
        foreach (var actor in _actors.Values)
        {
            var primary = actor.Id == PrimaryId;
            var objectColor = false;
            var allowUntargetable = primary && (IsTitanExtreme || IsTwintania);
            var included = primary;
            if (IsUltimaExtreme)
            {
                objectColor = actor.Oid == 0x913 || ultimaActiveOrbs.Contains(actor.Id);
                included |= actor.Oid == 0x915 || actor.Oid == 0x913 && !ultimaExploded.Contains(actor.Id)
                    || ultimaActiveOrbs.Contains(actor.Id);
                allowUntargetable |= objectColor;
            }
            else if (IsGarudaExtreme)
            {
                included |= actor.Oid is 0xF3 or 0xF4 or 0x600 or 0x601 or 0x620 or 0x621;
                objectColor = actor.Oid == 0xF3;
                allowUntargetable |= objectColor && !actor.Dead;
            }
            else if (IsTitanExtreme)
            {
                included |= actor.Oid is 0x8F2 or 0x5E2 or 0x5E0;
                objectColor = actor.Oid is 0x5E2 or 0x5E0;
            }
            else if (IsIfritExtreme)
            {
                included |= actor.Oid is 0xD4 or 0x91B;
                objectColor = actor.Oid is 0xD4 or 0x91B;
            }
            else if (IsCaduceus) included |= actor.Id == caduceusClone;
            else if (IsTwintania)
                included |= actor.Oid == 0x7E7 || twinAddsActive && actor.Oid is (0x7E8 or 0x7E9)
                    || TwinP4 && actor.Oid == 0x7EA;
            if (!included || !allowUntargetable && (actor.Dead || !actor.Targetable)) continue;
            yield return new($"enemy/marker/{actor.Id}", "实体与朝向", shape, actor.Position, actor.Rotation,
                Navigate: false, Color: objectColor ? new Vector4(1, 128f / 255, 0, 1) : new Vector4(1, 0, 0, 1));
        }
    }

    private DungeonEnemyDirective? NativeTankTarget(ulong current, Vector3 position)
    {
        if (_actors.GetValueOrDefault(PlayerId) is not { Dead: false } player || !ThornTank(player)) return null;
        var owned = EnemyDirectives().Where(e => e.ShouldBeTanked
            && !(IsGarudaExtreme && e.Actor.Oid == 0x600 && GarudaExPriority(e.Actor) < 0))
            .OrderByDescending(e => NativeEnemyDamagePriority(e.Actor)).ToArray();
        return owned.Where(e => e.Actor.TargetId != PlayerId).MinBy(e => NativeEnemyDistanceSquared(e.Actor.Position, position))
            ?? owned.FirstOrDefault(e => e.Actor.Id == current) ?? owned.FirstOrDefault();
    }

    private int NativeEnemyDamagePriority(DungeonActor actor)
    {
        if (IsCaduceus) return CaduceusPriority(actor);
        if (IsTwintania) return TwinPriority(actor);
        if (IsGarudaExtreme) return GarudaExPriority(actor);
        if (IsTitanExtreme) return TitanExPriority(actor);
        if (IsIfritExtreme) return IfritExPriority(actor);
        if (IsUltimaExtreme) return actor.Oid switch { 0x915 => 2, 0x90F => 1, _ => 0 };
        return 0;
    }

    internal IReadOnlyList<DungeonActionRequest> EnemyProvokeActions()
    {
        if (_actors.GetValueOrDefault(PlayerId) is not { Dead: false } player || !ThornTank(player)) return [];
        return EnemyDirectives().Where(e => e.ShouldBeTanked && e.PreferProvoking && e.Actor.TargetId != PlayerId
            && NativeEnemyDistanceSquared(player.Position, e.Actor.Position) <= MathF.Pow(25 + player.HitboxRadius + e.Actor.HitboxRadius, 2))
            .Select(e => new DungeonActionRequest(7533, e.Actor.Id, _now.AddSeconds(.5))).ToArray();
    }

    private IEnumerable<DungeonAmbientAoe> NativeEnemyGoals()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { Dead: false } player) yield break;
        var tank = NativeTankTarget(DamagePriorityTarget(player.TargetId, player.Position), player.Position);
        if (tank != null)
        {
            var range = tank.TankDistance + player.HitboxRadius + tank.Actor.HitboxRadius;
            var delta = tank.Actor.Position - tank.Position;
            delta.Y = 0;
            if (delta.LengthSquared() > 4)
            {
                var destination = ClampNativeTankGoal(tank.Position - Vector3.Normalize(delta) * range, tank.Actor.Position);
                yield return new("enemy/tank/pull", "拉首领至机制位置", new CircleTemplate(.5f), destination,
                    Display: false, Safe: true, GoalPriority: 2, OverrideDangerMargin: false);
            }
            else
            {
                yield return new("enemy/tank/range", "坦克攻击范围", new CircleTemplate(range), tank.Actor.Position,
                    Display: false, Safe: true, GoalPriority: 1, OverrideDangerMargin: false);
                if (tank.Actor.TargetId == PlayerId)
                {
                    var direction = new Vector3(MathF.Sin(tank.Rotation), 0, MathF.Cos(tank.Rotation));
                    yield return new("enemy/tank/front", "引导首领期望朝向", new CircleTemplate(.5f),
                        ClampNativeTankGoal(tank.Actor.Position + direction * range, tank.Actor.Position),
                        Display: false, Safe: true, GoalPriority: 3, OverrideDangerMargin: false);
                    if (range > 3)
                        yield return new("enemy/tank/axis", "沿期望朝向寻找安全站位", new RectangleTemplate(.5f, range - 3),
                            tank.Actor.Position + direction * ((range + 3) / 2), tank.Rotation,
                            Display: false, Safe: true, GoalPriority: 2, OverrideDangerMargin: false,
                            PreferredGoalPosition: tank.Actor.Position + direction * range);
                }
            }
            yield break;
        }
        if (TwinCombatRole(player) is not ("Ranged" or "Healer")) yield break;
        var directives = EnemyDirectives();
        var selected = PriorityTarget(player.TargetId, player.Position);
        var target = directives.FirstOrDefault(e => e.Actor.Id == selected);
        if (target?.StayAtLongRange == true)
            yield return new("enemy/ranged/range", "远程保持攻击范围", new CircleTemplate(25 + player.HitboxRadius + target.Actor.HitboxRadius),
                target.Actor.Position, Display: false, Safe: true, GoalPriority: 1, OverrideDangerMargin: false);
    }

    private Vector3 ClampNativeTankGoal(Vector3 destination, Vector3 inside)
    {
        if (Arena == null || Arena.Contains(destination, -.05f)) return destination;
        var candidates = new List<Vector3>();
        foreach (var contour in Arena.GetContours(128))
            for (var i = 0; i < contour.Count; ++i)
            {
                var from = contour[i];
                var edge = contour[(i + 1) % contour.Count] - from;
                edge.Y = 0;
                var delta = destination - from;
                delta.Y = 0;
                var t = edge.LengthSquared() > 0 ? Math.Clamp(Vector3.Dot(delta, edge) / edge.LengthSquared(), 0, 1) : 0;
                var candidate = Vector3.Lerp(from + edge * t, inside, .01f);
                if (Arena.Contains(candidate)) candidates.Add(candidate with { Y = destination.Y });
            }
        return candidates.Count == 0 ? inside : candidates.MinBy(p => NativeEnemyDistanceSquared(p, destination));
    }

    private IEnumerable<string> EnemyPressureHints()
    {
        foreach (var group in EnemyDirectives().Where(e => e.AttackStrength > 0 && e.Actor.TargetId != 0)
            .GroupBy(e => e.Actor.TargetId))
        {
            if (_actors.GetValueOrDefault(group.Key) is not { PartySlot: >= 0, Dead: false } member) continue;
            var pressure = group.Sum(e => e.AttackStrength);
            if (pressure >= .3f) yield return $"预计承伤：{member.Name}（{pressure:P0} 最大生命）";
        }
    }
}

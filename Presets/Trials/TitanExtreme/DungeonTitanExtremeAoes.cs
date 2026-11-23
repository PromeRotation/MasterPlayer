using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TitanExAoes()
    {
        if (!IsTitanExtreme || Active == null || Arena == null || TitanExBoss is not { } boss) yield break;
        if (titanExCrushRadius > 0)
        {
            yield return new("titan-ex/crush/outer", "大地粉碎落崖区", new DonutTemplate(titanExCrushRadius, 25), Arena.Position);
            yield return new("titan-ex/crush/inner", "远离大地粉碎中心", new CircleTemplate(titanExCrushRadius - 2), Arena.Position, Display: false);
            yield return new("titan-ex/crush/safe", "大地粉碎安全环", new DonutTemplate(titanExCrushRadius - 2, titanExCrushRadius),
                Arena.Position, Navigate: false, Safe: true);
        }
        if (!TitanExCombat) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 1467 or 1468 or 1475 or 1470))
            yield return new($"titan-ex/cast/{cast.Source}", cast.Rule.Name,
                cast.Rule.Action == 1470 ? new CircleTemplate(6) : RayRectangleTemplate.Symmetric(6, 40.25f),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        foreach (var source in titanExBursts.Take(titanExMaxBombs))
            if (_casts.GetValueOrDefault(source) is { } cast)
                yield return new($"titan-ex/burst/{source}", "爆破", new CircleTemplate(6.3f),
                    cast.Location, Activation: cast.FinishAt);
        if (titanExBuster != 0 && !boss.Dead && boss.Targetable && _actors.GetValueOrDefault(boss.TargetId) is { } target)
        {
            var shape = new FanTemplate(titanExBuster == 1463 ? 11.25f : 21.25f, MathF.PI * 2 / 3);
            var rotation = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
            var origin = QuantizedDungeonPosition(boss.Position);
            yield return new("titan-ex/buster/display", "山崩顺劈", shape, origin, rotation, Navigate: false, OutlineOnly: true);
            if (target.Id != PlayerId)
                yield return new("titan-ex/buster/avoid", "远离山崩顺劈", shape, origin, rotation, Display: false, Activation: titanExBusterAt);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new($"titan-ex/buster/party/{member.Id}", "顺劈避开队伍", new FanTemplate(100, MathF.PI * 2 / 3),
                        origin, MathF.Atan2(member.Position.X - boss.Position.X, member.Position.Z - boss.Position.Z), Display: false);
        }
        if (titanExHoldUntil > _now)
        {
            var toCenter = new Vector2(-boss.Position.X, -boss.Position.Z);
            if (toCenter.LengthSquared() > 0) toCenter = Vector2.Normalize(toCenter);
            var stand = boss.Position + new Vector3(toCenter.X, 0, toCenter.Y) * 2;
            yield return new("titan-ex/upheaval/stand", "地动后继续保持引导地裂", new DonutTemplate(1.5f, 100), stand,
                Display: false, Activation: titanExHoldUntil);
            if (_actors.GetValueOrDefault(PlayerId) is { } player && !KnockbackImmune(player, _now))
            {
                var direction = new Vector2(player.Position.X - boss.Position.X, player.Position.Z - boss.Position.Z);
                if (direction.LengthSquared() > 0) direction = Vector2.Normalize(direction);
                var end = player.Position + new Vector3(direction.X, 0, direction.Y) * 13;
                foreach (var preview in DungeonMovementPreview("titan-ex/upheaval/arrow", "地动击退13码", player.Position, end)) yield return preview;
            }
        }
        // AI is evaluated before the gaoler voidzone component in the source.
        var mechanicDangers = _casts.Values.Any(c => c.Rule.Action is 1467 or 1468 or 1475 or 1470)
            || titanExMaxBombs > 0 && titanExBursts.Count > 0
            || titanExBuster != 0 && boss.TargetId != PlayerId || titanExHoldUntil > _now || titanExCrushRadius > 0;
        var haveGaolers = _actors.Values.Any(a => a.Oid == 0x8F2 && !a.Dead && a.Targetable);
        if (!haveGaolers && !titanExKillBomb && boss.TargetId != PlayerId && !mechanicDangers
            && _actors.GetValueOrDefault(PlayerId) is { } memberPlayer)
        {
            if (titanExFetters.Contains(PlayerId))
            {
                var radius = titanExPhase == 0 ? 25 : titanExPhase <= 2 ? 15 : 12;
                var position = TwinCombatRole(memberPlayer) == "Healer"
                    ? Arena.Position + new Vector3(-.5f, 0, MathF.Sqrt(3) / 2) * radius
                    : boss.Position + Vector3.UnitZ;
                yield return new("titan-ex/gaol/stand", "牢狱分离站位", new DonutTemplate(2, 100), position,
                    Display: false, Activation: titanExFettersAt);
            }
            else
            {
                var angle = boss.Rotation + MathF.PI * .75f;
                var position = boss.Position + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 3;
                if (!Arena.Contains(position))
                {
                    angle = boss.Rotation - MathF.PI * .75f;
                    position = boss.Position + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 3;
                }
                if (Arena.Contains(position))
                    yield return new("titan-ex/idle", "首领身后近战位", new CircleTemplate(2), position,
                        Display: false, Safe: true, GoalPriority: 10);
            }
        }
        if (titanExPhase == 6)
            foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1E8F8F && a.EventState != 7))
                yield return new($"titan-ex/voidzone/{zone.Id}", "狱卒死亡领域", new CircleTemplate(5), zone.Position, Obstacle: true);
    }

    private IEnumerable<DungeonDamagePrediction> TitanExDamage()
    {
        if (!IsTitanExtreme || Active == null) yield break;
        var at = titanExCrushRadius > 0 ? default : titanExTumultAt;
        if (titanExCrushRadius <= 0 && !titanExTumult) yield break;
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
            yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, at, "Raidwide");
    }

    private IEnumerable<string> TitanExHints()
    {
        if (!IsTitanExtreme || Active == null) yield break;
        yield return $"泰坦阶段 {titanExPhase + 1}：{titanExSteps[titanExStep].Name}";
        if (titanExTumult) yield return $"激震：已结算 {titanExTumultHits}/4，下次约 {Math.Max(0, (titanExTumultAt - _now).TotalSeconds):F1} 秒";
        if (titanExCrushRadius > 0) yield return $"大地粉碎：全队伤害，进入 {titanExCrushRadius - 2}–{titanExCrushRadius} 码安全环";
        if (titanExFetters.Contains(PlayerId)) yield return "牢狱点名：治疗去后场，其他人靠首领";
        if (titanExKillBomb) yield return "优先击杀中央炸弹";
    }
}

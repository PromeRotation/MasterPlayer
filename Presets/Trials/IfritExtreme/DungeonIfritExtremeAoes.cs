using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> IfritExAoes()
    {
        if (!IsIfritExtreme || Active == null || IfritExBoss is not { } boss) yield break;
        if (IfritExIncinerate)
            foreach (var source in _actors.Values.Where(a => a.Oid == 0xD3 && !a.Dead && a.Targetable))
            {
                if (_actors.GetValueOrDefault(source.TargetId) is not { } target) continue;
                var origin = QuantizedDungeonPosition(source.Position);
                var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
                var shape = new FanTemplate(21, MathF.PI * 2 / 3);
                yield return new($"ifrit-ex/cleave/{source.Id}/display", "焚化", shape, origin, rotation, Navigate: false, OutlineOnly: true);
                if (target.Id != PlayerId)
                    yield return new($"ifrit-ex/cleave/{source.Id}/avoid", "远离焚化", shape, origin, rotation, Display: false);
                else
                    foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                        yield return new($"ifrit-ex/cleave/{source.Id}/party/{member.Id}", "焚化避开队伍", new FanTemplate(100, MathF.PI * 2 / 3),
                            origin, MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z), Display: false);
            }
        if (_actors.GetValueOrDefault(ifritExWindTarget) is { Dead: false } wind)
        {
            yield return new("ifrit-ex/wind/display", "灼热咆哮", new CircleTemplate(14), wind.Position,
                Navigate: false, OutlineOnly: true, Activation: ifritExWindAt);
            if (_now > ifritExWindShowAfter)
            {
                if (PlayerId != wind.Id)
                    yield return new("ifrit-ex/wind/avoid", "远离灼热咆哮", new CircleTemplate(15), wind.Position,
                        Display: false, Activation: ifritExWindAt);
                else
                    foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                        yield return new($"ifrit-ex/wind/party/{member.Id}", "灼热咆哮远离队伍", new CircleTemplate(15),
                            member.Position, Display: false, Activation: ifritExWindAt);
            }
        }
        var fettered = ifritExFetters.Select(id => _actors.GetValueOrDefault(id)).OfType<DungeonActor>().OrderBy(a => a.PartySlot).ToArray();
        if (IfritExFettersEnabled && fettered.Length >= 2)
        {
            var from = fettered[0].Position;
            var to = fettered[^1].Position;
            var delta = to - from;
            yield return new("ifrit-ex/fetters/line", "火狱锁链", new RectangleTemplate(.12f, new Vector2(delta.X, delta.Z).Length()),
                (from + to) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: ifritExFetterStrength <= 1);
        }
        if (IfritExFettersEnabled && ifritExFetters.Contains(PlayerId)
            && _actors.GetValueOrDefault(PlayerId) is { } me && !ThornTank(me)
            && fettered.FirstOrDefault(a => a.Id != PlayerId && !a.Dead) is { } partner)
            yield return new("ifrit-ex/fetters/near", "锁链保持10码以内", new DonutTemplate(10, 100), partner.Position, Display: false);
        if (!IfritExAi || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var bossAngle = MathF.Atan2(boss.Position.X, boss.Position.Z);
        if (IfritExHellfire)
        {
            var safe = IfritExDirection((150 - 40 * ifritExWave) * MathF.PI / 180) * 18;
            yield return new("ifrit-ex/hellfire/safe", "地狱之火后坦克安全点", new CircleTemplate(2), safe,
                Navigate: false, Safe: true, OutlineOnly: true);
            var stand = Vector3.Zero;
            if (player.Id == ifritExWindTarget) stand = -safe;
            else if (CurrentAssignment == IfritExTankRole) stand = safe;
            yield return IfritExStand(stand, "地狱之火重站位");
            yield break;
        }
        if (boss.TargetId == PlayerId)
            yield return new("ifrit-ex/tank/facing", "主坦朝外引导焚化", new FanTemplate(2, MathF.PI * 2 / 3),
                boss.Position, bossAngle, Navigate: false, Safe: true, OutlineOnly: true);
        if (IfritExNails && IfritExNextNail is { } nail)
        {
            yield return new("ifrit-ex/next-nail", "下一火狱之楔", new CircleTemplate(2), nail.Position,
                Navigate: false, Safe: true, OutlineOnly: true);
            foreach (var aoe in IfritExNailPosition(player, boss, bossAngle)) yield return aoe;
        }
        else
            foreach (var aoe in IfritExNormalPosition(player, boss, bossAngle)) yield return aoe;
    }

    private static Vector3 IfritExDirection(float radians) => new(MathF.Sin(radians), 0, MathF.Cos(radians));
    private static DungeonAmbientAoe IfritExStand(Vector3 at, string name)
        => new("ifrit-ex/stand", name, new DonutTemplate(2, 100), at, Display: false);

    private IEnumerable<DungeonAmbientAoe> IfritExBaitAvoidance()
    {
        foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && !ifritExBaiters.Contains(a.Id)))
            yield return new($"ifrit-ex/bait/{member.Id}", "地火远离非引导队员", new CircleTemplate(8), member.Position, Display: false);
    }

    private IEnumerable<DungeonAmbientAoe> IfritExNormalPosition(DungeonActor player, DungeonActor boss, float angle)
    {
        if (IfritExErupting)
        {
            if (!ifritExBaiters.Contains(player.Id)) yield break;
            if (TwinCombatRole(player) is "Melee" or "Ranged" && boss.Cast != null)
            {
                var forward = IfritExDirection(angle);
                yield return IfritExStand(boss.Position - forward * 11.5f + new Vector3(-forward.Z, 0, forward.X) * 11, "第二轮地火引导点");
            }
            else
                foreach (var aoe in IfritExBaitAvoidance()) yield return aoe;
            yield break;
        }
        if (boss.TargetId == player.Id) yield break;
        if (player.Id == ifritExWindTarget)
        {
            var dir = player.Position.X * player.Position.X + player.Position.Z * player.Position.Z > 100
                ? MathF.Atan2(player.Position.X, player.Position.Z) : angle + 105 * MathF.PI / 180;
            yield return IfritExStand(IfritExDirection(dir) * 18, "灼热咆哮外侧站位");
        }
        else if (ifritExFetters.Contains(player.Id))
            yield return IfritExStand(boss.Position + IfritExDirection(angle + (ThornTank(player) ? 75 : -135) * MathF.PI / 180) * 3, "火狱锁链站位");
        else if (ThornTank(player))
            yield return IfritExStand(boss.Position + IfritExDirection(angle + 75 * MathF.PI / 180) * 7.5f, "副坦待命点");
        else if (TwinCombatRole(player) == "Healer")
            yield return IfritExStand(Vector3.Zero, "治疗中央站位");
        else
        {
            var stand = boss.Position + IfritExDirection(angle - 135 * MathF.PI / 180) * 6;
            if (player.ClassJob is 7 or 25 or 26 or 27 or 35 or 42) stand -= IfritExDirection(angle) * 15;
            yield return IfritExStand(stand, "输出站位");
        }
    }

    private IEnumerable<DungeonAmbientAoe> IfritExNailPosition(DungeonActor player, DungeonActor boss, float angle)
    {
        if (ifritExBaiters.Contains(player.Id))
        {
            foreach (var aoe in IfritExBaitAvoidance()) yield return aoe;
            yield break;
        }
        if (boss.TargetId == player.Id) yield break;
        var inverted = ifritExNails.Count <= 1 + ifritExWave * 3;
        if (player.Id == ifritExWindTarget)
        {
            var dir = player.Position.X * player.Position.X + player.Position.Z * player.Position.Z > 100
                ? MathF.Atan2(player.Position.X, player.Position.Z) : angle + (inverted ? -105 : 105) * MathF.PI / 180;
            yield return IfritExStand(IfritExDirection(dir) * 18, "钉阶段灼热咆哮站位");
        }
        else if (CurrentAssignment == IfritExTankRole)
            yield return IfritExStand(boss.Position + IfritExDirection(angle + (inverted ? 75 : -75) * MathF.PI / 180) * 7.5f, "换坦待命点");
        else if (TwinCombatRole(player) == "Healer")
            yield return IfritExStand(Vector3.Zero, "治疗中央站位");
        else if (!IfritExErupting)
            yield return new("ifrit-ex/nails/center", "火狱之楔阶段远离中央", new CircleTemplate(8), Vector3.Zero, Display: false);
    }

    private IEnumerable<DungeonDamagePrediction> IfritExDamage()
    {
        if (!IsIfritExtreme || Active == null) yield break;
        if (ifritExStep is IfritExtremeStep.HellfireStart or IfritExtremeStep.HellfireEnd)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, ifritExHellfireAt, "Raidwide");
        if (_now > ifritExWindShowAfter && _actors.GetValueOrDefault(ifritExWindTarget) is { Dead: false, PartySlot: >= 0 } wind)
            yield return new(wind.Id, wind.PartySlot, wind.PartyRole, wind.Name, ifritExWindAt, "Raidwide");
        if (IfritExNails && IfritExNextNail is { Oid: 0x91B, Hp: { } hp, MaxHp: { } max } && hp < .5f * max)
            foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, default, "Raidwide");
    }

    private IEnumerable<string> IfritExHints()
    {
        if (!IsIfritExtreme || Active == null) yield break;
        yield return $"火狱之楔第 {Math.Min(3, ifritExWave + 1)} 轮：{ifritExStep}，坦克分工 {IfritExTankRole}";
        if (ifritExWindTarget == PlayerId) yield return $"灼热咆哮：远离队伍，剩余 {ifritExWindsLeft} 次";
        if (IfritExFettersEnabled && ifritExFetters.Contains(PlayerId)) yield return "火狱锁链：靠近连线队友";
        if (ifritExStep is IfritExtremeStep.HellfireStart or IfritExtremeStep.HellfireEnd) yield return "地狱之火：全队伤害";
        if (IfritExNails && IfritExNextNail is { Oid: 0x91B, Hp: { } hp, MaxHp: { } max } && hp < .5f * max)
            yield return "大型火狱之楔即将摧毁：准备全队减伤";
    }
}

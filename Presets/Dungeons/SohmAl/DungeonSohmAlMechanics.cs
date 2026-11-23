using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<(ulong Target, uint Action), DungeonPendingCast> sohmBaits = [];
    private readonly Dictionary<ulong, DungeonPendingCast> meteorImpacts = [];

    private void SohmIcon(ulong target, uint icon, DateTime timestamp)
    {
        var action = Active?.BossOid switch
        {
            0xE8F when icon == 46 => 5009u,
            0xE96 when icon == 10 => 3817u,
            0xE96 when icon == 7 => 4999u,
            _ => 0u,
        };
        if (action == 0) return;
        GeometryTemplate shape = action == 5009
            ? new FanTemplate(48.68f, MathF.PI / 4)
            : new CircleTemplate(action == 3817 ? 5 : 20);
        var delay = action switch { 5009 => 8.5, 3817 => 3.1, _ => 8.1 };
        var rule = new DungeonCastRule(action, Active!.BossOid, action == 5009 ? "FloralTrap" : action == 3817 ? "HeavensfallBait" : "MeteorBait", shape);
        sohmBaits[(target, action)] = new(PrimaryId, rule, default, 0, timestamp, timestamp.AddSeconds(delay), target);
    }

    private void SohmCast(DungeonPendingCast cast)
    {
        if (Active?.BossOid != 0xE96 || cast.Rule.Action != 4999) return;
        // 点名先跟随玩家；读条开始才锁定各陨石位置，命中晚于读条结束 1.5 秒。
        foreach (var key in sohmBaits.Keys.Where(k => k.Action == 4999).ToArray()) sohmBaits.Remove(key);
        meteorImpacts[cast.Source] = cast with { FinishAt = cast.FinishAt.AddSeconds(1.5) };
    }

    private void SohmEffect(uint action, ulong target)
    {
        if (Active?.BossOid is not (0xE8F or 0xE96)) return;
        if (action == 4997) meteorImpacts.Clear();
        if (action is not (5009 or 3817)) return;
        var key = sohmBaits.Keys.FirstOrDefault(k => k.Action == action && (target == 0 || k.Target == target));
        if (key == default) key = sohmBaits.Keys.FirstOrDefault(k => k.Action == action);
        sohmBaits.Remove(key);
    }

    private void SynchronizeSohm()
    {
        foreach (var key in sohmBaits.Keys.Where(k => !_actors.TryGetValue(k.Target, out var target) || target.Dead).ToArray())
            sohmBaits.Remove(key);
        foreach (var source in meteorImpacts.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray())
            meteorImpacts.Remove(source);
    }

    private IEnumerable<DungeonAmbientAoe> SohmAoes()
    {
        foreach (var ((id, action), bait) in sohmBaits)
        {
            if (!_actors.TryGetValue(id, out var target) || !_actors.TryGetValue(bait.Source, out var source)) continue;
            var cone = action == 5009;
            var origin = cone ? source.Position : target.Position;
            var rotation = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            yield return new($"sohm/bait/{action}/{id}", bait.Rule.Name, bait.Rule.Shape!, origin, rotation,
                Navigate: false, OutlineOnly: id == PlayerId);
            if (id != PlayerId)
                yield return new($"sohm/bait/avoid/{action}/{id}", bait.Rule.Name, bait.Rule.Shape!, origin, rotation,
                    Display: false, Activation: bait.FinishAt);
            else
            {
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != id && !a.Dead))
                {
                    var direction = MathF.Atan2(member.Position.X - source.Position.X, member.Position.Z - source.Position.Z);
                    GeometryTemplate avoid = cone ? new FanTemplate(100, MathF.PI / 4) : bait.Rule.Shape!;
                    yield return new($"sohm/bait/party/{action}/{id}/{member.Id}", bait.Rule.Name, avoid,
                        cone ? source.Position : member.Position, direction, Display: false, Activation: bait.FinishAt);
                }
                if (!cone)
                    // 上游希望站到 27 码外；MP 另有 0.5 码墙体余量及整格判定。
                    // 目标带向内预留一格，保证默认解算仍会前往场边，真实场地边界保持不变。
                    yield return new($"sohm/bait/edge/{action}/{id}", "前往场边诱导", new DonutTemplate(26, 27.5f),
                        new(-104, target.Position.Y, -395), Display: false, Safe: true);
            }
        }
        foreach (var cast in meteorImpacts.Values)
            yield return new($"sohm/meteor/{cast.Source}", cast.Rule.Name, cast.Rule.Shape!, cast.Location,
                Activation: cast.FinishAt);
    }
}

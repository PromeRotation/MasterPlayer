using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> corruptingBursts = [];
    private readonly HashSet<ulong> resolvedCorruptingBursts = [];

    private static bool KnockbackImmune(DungeonActor actor, DateTime activation)
        => actor.StatusExpirations?.Any(s => s.Value > activation && s.Key is 160 or 1209 or 2663 or 3054 or 1722 or 2345) == true;

    private void PharosStatus(ulong target, uint status, bool present, ushort param, DateTime timestamp)
    {
        if (Active?.BossOid != 0x8FB || status != 374) return;
        if (present && param == 3)
        {
            if (!resolvedCorruptingBursts.Contains(target)) corruptingBursts.TryAdd(target, timestamp.AddSeconds(2.5));
        }
        else { corruptingBursts.Remove(target); resolvedCorruptingBursts.Remove(target); }
    }

    private void SynchronizePharos()
    {
        if (Active?.BossOid != 0x8FB) return;
        foreach (var actor in _actors.Values)
            if (!actor.Dead)
                PharosStatus(actor.Id, 374, true, StatusParam(actor, 374), _now);
        foreach (var id in corruptingBursts.Keys.Where(id => !_actors.TryGetValue(id, out var actor) || actor.Dead).ToArray())
            corruptingBursts.Remove(id);
    }

    private IEnumerable<DungeonAmbientAoe> PharosAoes()
    {
        if (Active?.BossOid == 0x8FB)
        {
            foreach (var zone in _actors.Values.Where(a => !a.Dead && a.Oid is >= 0x1E9058 and <= 0x1E905A))
            {
                var radius = zone.Oid switch { 0x1E9058 => 9, 0x1E9059 => 12, _ => 15 };
                yield return new($"crystal-zone/{zone.Id}", "成长水晶毒区", new CircleTemplate(radius), zone.Position);
            }
            foreach (var (id, activation) in corruptingBursts)
            {
                if (!_actors.TryGetValue(id, out var target) || target.Dead) continue;
                yield return new($"burst/display/{id}", "三层水晶：远离队友爆炸", new CircleTemplate(6),
                    target.Position, Navigate: false, OutlineOnly: true);
                if (PlayerId == id)
                {
                    foreach (var member in _actors.Values.Where(a => a.Id != id && a.PartySlot >= 0 && !a.Dead))
                        yield return new($"burst/party/{id}/{member.Id}", "远离队友爆炸", new CircleTemplate(6), member.Position,
                            Display: false, Activation: activation);
                }
                else
                    yield return new($"burst/avoid/{id}", "避开水晶爆炸", new CircleTemplate(6), target.Position, Display: false, Activation: activation);
            }
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 1502))
            {
                if (!_actors.TryGetValue(PlayerId, out var player) || KnockbackImmune(player, cast.FinishAt)) continue;
                var offset = new Vector2(player.Position.X - cast.Location.X, player.Position.Z - cast.Location.Z);
                if (offset.LengthSquared() is <= .0001f or > 400) continue;
                var direction = Vector2.Normalize(offset);
                var landing = player.Position + new Vector3(direction.X, 0, direction.Y) * 10;
                var safe = Arena?.Contains(landing) == true;
                yield return new("gigaslash/landing", "击退10码落点", new CircleTemplate(.5f), landing,
                    Navigate: false, Safe: safe, OutlineOnly: true);
                yield return new("gigaslash/path", "击退轨迹", new RectangleTemplate(.12f, 10), (player.Position + landing) * .5f,
                    MathF.Atan2(direction.X, direction.Y), Navigate: false, Safe: safe);
            }
        }
        if (Active?.BossOid == 0x8F4)
            foreach (var egg in _actors.Values.Where(a => !a.Dead && a.Oid is 0x8F7 or 0x8F8))
                yield return new($"egg/{egg.Id}", "远离鸟蛋，避免误伤", new CircleTemplate(8), egg.Position);
    }
}

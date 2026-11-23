namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DateTime hadesTetherAt;
    private readonly Dictionary<ulong, ulong> hadesTethers = [];
    private readonly Dictionary<ulong, ulong> hadesTetherPending = [];
    private static bool HadesPlayer(DungeonActor actor) => actor.Oid == 0 || actor.Buddy;

    private void HadesTether(ulong source, ulong target, uint tether, bool present, DateTime at, bool snapshot = false)
    {
        if (!IsHades || Active == null || tether != 17 || hadesWaitingRemoval.Contains(source)) return;
        if (!snapshot) hadesTetherPending[source] = present ? target : 0;
        var old = hadesTethers.GetValueOrDefault(source);
        // 快照去重；真实重新连线与运行库一样先撤旧再建立，即使两端没有变化。
        if (snapshot && old == (present ? target : 0)) return;
        if (old != 0) HadesTetherChange(source, old, false);
        hadesTethers.Remove(source);
        if (!present) return;
        hadesTethers[source] = target;
        HadesTetherChange(source, target, true, at);
    }

    private void HadesTetherChange(ulong source, ulong target, bool present, DateTime? at = null)
    {
        if ((_actors.GetValueOrDefault(source) ?? hadesActors.GetValueOrDefault(source)) is not { } first
            || (_actors.GetValueOrDefault(target) ?? hadesActors.GetValueOrDefault(target)) is not { } second) return;
        var player = HadesPlayer(first) ? first : second;
        var enemy = HadesPlayer(first) ? second : first;
        if (!HadesPlayer(player) || HadesPlayer(enemy)) return;
        if (present)
        {
            if (hadesTetherAt == default) hadesTetherAt = (at ?? _now).AddSeconds(4.7);
            hadesBaits.Add(new(enemy, player, 16755, hadesTetherAt, ++hadesKey));
        }
        else
        {
            var index = hadesBaits.FindIndex(b => b.Action == 16755 && b.Source.Id == enemy.Id && b.Target.Id == player.Id);
            if (index >= 0) hadesBaits.RemoveAt(index);
        }
    }

    private void HadesObserveTethers()
    {
        foreach (var actor in _actors.Values.Where(a => !hadesWaitingRemoval.Contains(a.Id)))
        {
            var raw = actor.Tethers?.GetValueOrDefault(17u) ?? 0;
            if (hadesTetherPending.TryGetValue(actor.Id, out var pending))
            {
                if (raw != pending) continue;
                hadesTetherPending.Remove(actor.Id);
            }
            if (raw != hadesTethers.GetValueOrDefault(actor.Id))
                HadesTether(actor.Id, raw, 17, raw != 0, _now, snapshot: true);
        }
    }
}

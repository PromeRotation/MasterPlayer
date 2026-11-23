namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, ulong> seiryuTethers = [];
    private readonly Dictionary<ulong, ulong> seiryuTetherPending = [];
    private DateTime seiryuKanaboAt;
    private DateTime seiryuRedAt;
    private ulong seiryuForbidden;

    private void SeiryuTether(ulong source, ulong target, uint tether, bool present, DateTime at, bool snapshot = false)
    {
        if (!IsSeiryu || Active == null || tether != 17) return;
        if (!snapshot) seiryuTetherPending[source] = present ? target : 0;
        var previous = seiryuTethers.GetValueOrDefault(source);
        if (previous == (present ? target : 0)) return;
        if (previous != 0) SeiryuTetherChange(source, previous, false, at);
        seiryuTethers.Remove(source);
        if (present)
        {
            seiryuTethers[source] = target;
            SeiryuTetherChange(source, target, true, at);
        }
    }

    private void SeiryuTetherChange(ulong fromId, ulong toId, bool present, DateTime at)
    {
        var from = _actors.GetValueOrDefault(fromId) ?? seiryuActors.GetValueOrDefault(fromId);
        var to = _actors.GetValueOrDefault(toId) ?? seiryuActors.GetValueOrDefault(toId);
        if (from == null || to == null) return;
        var fromPlayer = from.Oid == 0 || from.Buddy;
        var player = fromPlayer ? from : to;
        var enemy = fromPlayer ? to : from;
        if (player.Oid != 0 && !player.Buddy || enemy.Oid == 0 || enemy.Buddy) return;
        // RedRush explicitly accepts only enemy-to-player input; Kanabo keeps both directions.
        var action = enemy.Oid == 0x2788 ? 15392u : from.Oid == 0x2786 ? 15389u : 0;
        if (action == 0) return;
        if (action == 15389 && player.PartySlot is >= 0 and < 64)
        {
            var bit = 1UL << player.PartySlot;
            seiryuForbidden = present ? seiryuForbidden | bit : seiryuForbidden & ~bit;
        }
        if (!present)
        {
            var index = seiryuBaits.FindIndex(b => b.Action == action && b.Source.Id == enemy.Id && b.Target.Id == player.Id);
            if (index >= 0) seiryuBaits.RemoveAt(index);
            return;
        }
        if (action == 15392 && seiryuKanaboAt == default) seiryuKanaboAt = at.AddSeconds(5.9);
        if (action == 15389 && seiryuRedAt == default) seiryuRedAt = at.AddSeconds(6);
        seiryuBaits.Add(new(enemy, player, action == 15392 ? seiryuKanaboAt : seiryuRedAt, ++seiryuKey, action));
    }

    private void SeiryuObserveTethers()
    {
        foreach (var source in _actors.Keys.Concat(seiryuTethers.Keys).Distinct().ToArray())
        {
            if (seiryuWaitingRemoval.Contains(source)) continue;
            var raw = _actors.GetValueOrDefault(source)?.Tethers?.GetValueOrDefault(17u) ?? 0;
            if (seiryuTetherPending.TryGetValue(source, out var pending))
            {
                if (raw != pending) continue;
                seiryuTetherPending.Remove(source);
            }
            if (raw != seiryuTethers.GetValueOrDefault(source))
                SeiryuTether(source, raw, 17, raw != 0, _now, snapshot: true);
        }
    }
}

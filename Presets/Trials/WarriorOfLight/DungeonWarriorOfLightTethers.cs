namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DateTime wolTetherAt;
    private readonly Dictionary<ulong, ulong> wolTethers = [];
    private readonly Dictionary<ulong, ulong> wolTetherPending = [];

    private void WolTether(ulong source, ulong target, uint tether, bool present, DateTime at, bool snapshot = false)
    {
        if (!IsWarriorOfLight || Active == null || tether != 17 || wolWaitingRemoval.Contains(source)) return;
        if (!snapshot) wolTetherPending[source] = present ? target : 0;
        var old = wolTethers.GetValueOrDefault(source);
        if (snapshot && old == (present ? target : 0)) return;
        if (old != 0) WolTetherChange(source, old, false);
        wolTethers.Remove(source);
        if (!present) return;
        wolTethers[source] = target;
        WolTetherChange(source, target, true, at);
    }

    private void WolTetherChange(ulong source, ulong target, bool present, DateTime? at = null)
    {
        if ((_actors.GetValueOrDefault(source) ?? wolActors.GetValueOrDefault(source)) is not { } first
            || (_actors.GetValueOrDefault(target) ?? wolActors.GetValueOrDefault(target)) is not { } second) return;
        var player = first.Oid == 0 || first.Buddy ? first : second;
        var enemy = first.Oid == 0 || first.Buddy ? second : first;
        if (!(player.Oid == 0 || player.Buddy) || enemy.Oid == 0 || enemy.Buddy) return;
        if (present)
        {
            if (wolTetherAt == default) wolTetherAt = at ?? _now;
            wolBaits.Add(new(enemy, player, 20260, wolTetherAt, ++wolKey));
        }
        else
        {
            var i = wolBaits.FindIndex(b => b.Action == 20260 && b.Source.Id == enemy.Id && b.Target.Id == player.Id);
            if (i >= 0) wolBaits.RemoveAt(i);
        }
    }

    private void WolObserveTethers()
    {
        foreach (var actor in _actors.Values.Where(a => !wolWaitingRemoval.Contains(a.Id)))
        {
            var raw = actor.Tethers?.GetValueOrDefault(17u) ?? 0;
            if (wolTetherPending.TryGetValue(actor.Id, out var pending))
            {
                if (raw != pending) continue;
                wolTetherPending.Remove(actor.Id);
            }
            if (raw != wolTethers.GetValueOrDefault(actor.Id)) WolTether(actor.Id, raw, 17, raw != 0, _now, snapshot: true);
        }
    }
}

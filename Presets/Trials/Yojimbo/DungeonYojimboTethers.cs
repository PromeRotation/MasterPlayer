namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<(ulong Source, uint Tether), ulong> yojimboTethers = [];
    private readonly Dictionary<(ulong Source, uint Tether), ulong> yojimboTetherPending = [];
    private readonly List<ulong> yojimboEnchain = [];
    private readonly List<ulong> yojimboBound = [];
    private readonly List<(DungeonActor Target, bool Destroyed, int Key)> yojimboChains = [];

    private void YojimboTether(ulong source, ulong target, uint tether, bool present, bool snapshot = false)
    {
        if (!IsYojimbo || Active == null || tether is not (1 or 57 or 81)
            || (_actors.GetValueOrDefault(source) ?? yojimboActors.GetValueOrDefault(source)) is not { Oid: 0 }) return;
        var key = (source, tether);
        if (!snapshot) yojimboTetherPending[key] = present ? target : 0;
        var old = yojimboTethers.GetValueOrDefault(key);
        if (old == (present ? target : 0)) return;
        var targets = tether == 81 ? yojimboBound : yojimboEnchain;
        if (old != 0) targets.Remove(source);
        yojimboTethers.Remove(key);
        if (present) { targets.Add(source); yojimboTethers[key] = target; }
        else if (tether == 81) YojimboPruneChains();
    }

    private void YojimboObserveTethers()
    {
        foreach (var actor in _actors.Values.Where(a => a.Oid == 0 && !yojimboWaitingRemoval.Contains(a.Id)))
            foreach (var tether in new[] { 1u, 57u, 81u })
            {
                var key = (actor.Id, tether);
                var raw = actor.Tethers?.GetValueOrDefault(tether) ?? 0;
                if (yojimboTetherPending.TryGetValue(key, out var pending))
                {
                    if (raw != pending) continue;
                    yojimboTetherPending.Remove(key);
                }
                if (raw != yojimboTethers.GetValueOrDefault(key))
                    YojimboTether(actor.Id, raw, tether, raw != 0, snapshot: true);
            }
    }

    private void YojimboPruneChains()
        => yojimboChains.RemoveAll(chain => chain.Destroyed || chain.Target.Dead);
}

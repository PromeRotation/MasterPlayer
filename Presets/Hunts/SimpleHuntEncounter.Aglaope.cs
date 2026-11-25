using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed partial class SimpleHuntEncounter
{
    private readonly Dictionary<ulong, HuntAglaopeState> aglaopes = [];
    private readonly HuntAglaopePendingStatuses aglaopePending = new();
    internal bool AglaopePayloadMissing { get; private set; }
    internal IEnumerable<HuntAglaopeKnockback> AglaopeKnockbackSources => aglaopes.Values.SelectMany(s => s.KnockbackSources);

    private void InitializeAglaope()
    {
        foreach (var id in aglaopes.Keys.Where(id => !active.Contains(id)).ToArray()) aglaopes.Remove(id);
        foreach (var actor in ActiveActors.Where(a => a.Oid == 0x281E))
            if (aglaopes.TryAdd(actor.Id, new(actor.Id))) initializingComponentOwners.Add(actor.Id);
        foreach (var state in aglaopes.Values) state.Observe(actors, partyMembers);
    }

    internal void AglaopeEffect(ulong source, uint action, uint sequence, IReadOnlyList<ulong> targets,
        ulong[][]? rawEffects, DateTime now)
    {
        aglaopePending.Effect(source, sequence, targets, rawEffects, actors, now);
        if (action != 16824 || !actors.TryGetValue(source, out var caster) || !RaintrillerCaster(caster)) return;
        if (aglaopes.Count > 0 && rawEffects == null) AglaopePayloadMissing = true;
        foreach (var state in aglaopes.Values) state.Effect(action);
    }
}

using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

// FangsEnd latches raid slots in Update; only status loss clears them.
internal sealed class PalaceYaquaruHeavy
{
    internal ulong ShouldCleanse { get; private set; }
    private readonly Dictionary<(ulong Actor, int Slot), ReplayStatus> observed = [];
    private readonly Dictionary<ulong, int> previousRoster = [];
    private readonly Dictionary<ulong, List<ReplayStatus>> packetLosses = [];
    private bool initialized;

    internal void Lose(ulong actor, int slot, IReadOnlyList<ReplayStatus>? current, ulong source)
    {
        Clear(slot);
        if (!packetLosses.TryGetValue(actor, out var losses))
            packetLosses[actor] = losses = [];
        foreach (var status in (current ?? []).Select((s, index) => s with { SlotIndex = s.SlotIndex ?? index })
            .Where(s => s.Id == 14 && (source == 0 || s.SourceId == source)))
        {
            losses.RemoveAll(old => old.SlotIndex == status.SlotIndex && old.SourceId == status.SourceId);
            losses.Add(status);
        }
    }

    internal void Gain(ulong actor, ulong source)
    {
        if (!packetLosses.TryGetValue(actor, out var losses)) return;
        losses.RemoveAll(s => source == 0 || s.SourceId == source);
        if (losses.Count == 0) packetLosses.Remove(actor);
    }

    internal void Observe(IEnumerable<DungeonActor> actors, IReadOnlyDictionary<ulong, int> roster, DateTime now)
    {
        var members = actors.ToArray();
        var current = new Dictionary<(ulong Actor, int Slot), ReplayStatus>();
        var heavyByActor = new Dictionary<ulong, ReplayStatus[]>();
        // Feed destruction/status callbacks all precede FangsEnd.Update.
        foreach (var removed in observed.Keys.Select(k => k.Actor).Distinct().Where(id => members.All(a => a.Id != id)))
        {
            if (observed.Any(p => p.Key.Actor == removed && p.Value.Id == 14))
                Clear(previousRoster.GetValueOrDefault(removed, -1));
            packetLosses.Remove(removed);
        }
        foreach (var actor in members)
        {
            var slots = actor.StatusSlots ?? (actor.Statuses ?? [])
                .Select((id, slot) => new ReplayStatus(id, 0, actor.StatusSources?.GetValueOrDefault(id) ?? 0,
                    actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue) { SlotIndex = slot }).ToArray();
            var bySlot = slots.Select((status, index) => (status, index))
                .ToDictionary(p => p.status.SlotIndex ?? p.index, p => p.status);
            var eventSlot = (initialized ? previousRoster : roster).GetValueOrDefault(actor.Id, -1);
            foreach (var slot in bySlot.Keys.Concat(observed.Keys.Where(k => k.Actor == actor.Id).Select(k => k.Slot)).Distinct())
            {
                var key = (actor.Id, slot);
                var old = observed.GetValueOrDefault(key);
                var status = bySlot.GetValueOrDefault(slot);
                if (old?.Id == 14 && (status == null || status.Id != 14 || status.SourceId != old.SourceId))
                    Clear(eventSlot);
                if (status != null) current[key] = status;
            }
            var heavy = bySlot.OrderBy(p => p.Key).Select(p => p.Value with { SlotIndex = p.Key }).Where(s => s.Id == 14).ToArray();
            if (packetLosses.TryGetValue(actor.Id, out var losses))
            {
                // Acknowledging one native slot must not revive another lost
                // copy still present in the same stale snapshot.
                losses.RemoveAll(old => !heavy.Any(s => old.SlotIndex == s.SlotIndex
                    && old.SourceId == s.SourceId && s.ExpiresAt <= old.ExpiresAt.AddSeconds(1)));
                heavy = heavy.Where(s => !losses.Any(old =>
                    old.SlotIndex == s.SlotIndex && old.SourceId == s.SourceId)).ToArray();
                if (losses.Count == 0) packetLosses.Remove(actor.Id);
            }
            heavyByActor[actor.Id] = heavy;
        }
        // All losses are now applied; Update reads the first native status slot
        // using the newly committed roster, even for dead players.
        foreach (var actor in members)
        {
            var first = heavyByActor.GetValueOrDefault(actor.Id)?.FirstOrDefault();
            var updateSlot = roster.GetValueOrDefault(actor.Id, -1);
            if (updateSlot is >= 0 and < 4 && first != null && (first.ExpiresAt - now).TotalSeconds > 8)
                ShouldCleanse |= 1ul << updateSlot;
        }
        observed.Clear();
        foreach (var pair in current) observed.Add(pair.Key, pair.Value);
        previousRoster.Clear();
        foreach (var pair in roster) previousRoster.Add(pair.Key, pair.Value);
        initialized = true;
    }

    private void Clear(int slot)
    {
        if (slot is >= 0 and < 64) ShouldCleanse &= ~(1ul << slot);
    }
}

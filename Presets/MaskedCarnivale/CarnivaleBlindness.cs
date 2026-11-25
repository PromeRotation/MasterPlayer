using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

// ExplosiveDehiscence stores Blind gains and losses in raid slots.
// Slot changes alone do not move bits or synthesize status callbacks.
internal sealed class CarnivaleBlindness(uint statusId = 15)
{
    private ulong blinded;
    private readonly Dictionary<(ulong Actor, int Slot), ReplayStatus> observed = [];
    private readonly Dictionary<ulong, int> previousRoster = [];

    internal void Status(int slot, bool present)
    {
        if (slot is < 0 or >= 64) return;
        var bit = 1ul << slot;
        if (present) blinded |= bit;
        else blinded &= ~bit;
    }

    internal bool Blinded(int slot) => slot is >= 0 and < 64 && (blinded & (1ul << slot)) != 0;

    internal void Observe(IEnumerable<DungeonActor> actors, IReadOnlyDictionary<ulong, int> roster, bool bootstrap,
        IReadOnlyDictionary<ulong, int>? bootstrapRoster = null)
    {
        var current = new Dictionary<(ulong Actor, int Slot), ReplayStatus>();
        foreach (var actor in actors)
        {
            // Existing components receive status changes before this frame's roster
            // commit. Activation replay runs afterwards and uses the new roster.
            var eventSlot = (bootstrap ? bootstrapRoster ?? roster : previousRoster).GetValueOrDefault(actor.Id, -1);
            var statuses = actor.StatusSlots ?? (actor.Statuses ?? actor.StatusExpirations?.Keys.ToArray() ?? [])
                .Select((id, slot) => new ReplayStatus(id, 0, 0, actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue)
                    { SlotIndex = slot }).ToArray();
            var bySlot = new Dictionary<int, ReplayStatus>();
            for (var i = 0; i < statuses.Count; ++i) bySlot[statuses[i].SlotIndex ?? i] = statuses[i];
            foreach (var slot in bySlot.Keys.Concat(observed.Keys.Where(k => k.Actor == actor.Id).Select(k => k.Slot)).Distinct().Order())
            {
                var key = (actor.Id, slot);
                var status = bySlot.GetValueOrDefault(slot);
                var old = observed.GetValueOrDefault(key);
                if (status != null)
                {
                    current[key] = status;
                    if (old != null && old.Id == status.Id && old.SourceId == status.SourceId && old.Param == status.Param
                        && (status.ExpiresAt - old.ExpiresAt).TotalSeconds <= 1) continue;
                }
                if (old?.Id == statusId && (status == null || old.Id != status.Id || old.SourceId != status.SourceId))
                    Status(eventSlot, false);
                if (status?.Id == statusId) Status(eventSlot, true);
            }
        }
        observed.Clear();
        foreach (var entry in current) observed.Add(entry.Key, entry.Value);
        previousRoster.Clear();
        foreach (var entry in roster) previousRoster.Add(entry.Key, entry.Value);
    }

    internal void Removed(ulong actor)
    {
        var slot = previousRoster.GetValueOrDefault(actor, -1);
        foreach (var key in observed.Keys.Where(k => k.Actor == actor).ToArray())
        {
            if (observed[key].Id == statusId) Status(slot, false);
            observed.Remove(key);
        }
    }

    internal void Clear()
    {
        blinded = 0;
        observed.Clear();
        previousRoster.Clear();
    }
}

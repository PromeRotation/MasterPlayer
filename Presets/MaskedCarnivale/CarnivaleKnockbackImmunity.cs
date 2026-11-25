using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

// GenericKnockback has three overwrite slots per raid member, not an OR of SIDs.
internal sealed class CarnivaleKnockbackImmunity
{
    private readonly Dictionary<(int Slot, int Category), DateTime> expiries = [];
    private readonly Dictionary<(ulong Actor, int Slot), ReplayStatus> observed = [];
    private readonly Dictionary<ulong, int> observedSlots = [];
    private bool initialized;
    internal static int Category(uint status) => status switch
    {
        160 or 1209 or 3054 => 0,
        1722 or 2663 => 1,
        2345 => 2,
        _ => -1,
    };

    internal void Status(int slot, uint status, bool present, DateTime expiry)
    {
        var category = Category(status);
        if (slot is >= 0 and < 64 && category >= 0)
            expiries[(slot, category)] = present ? expiry : default;
    }

    internal void Observe(IEnumerable<DungeonActor> actors, IReadOnlyDictionary<ulong, int>? roster = null)
    {
        var members = actors.ToArray();
        roster ??= members.Where(a => a.PartySlot >= 0).ToDictionary(a => a.Id, a => a.PartySlot);
        foreach (var removed in observed.Keys.Select(k => k.Actor).Distinct().Where(id => members.All(a => a.Id != id)).ToArray())
            Removed(removed);
        var current = new Dictionary<(ulong Actor, int Slot), ReplayStatus>();
        foreach (var actor in members)
        {
            // The source feed commits Party after actor/status callbacks; activation
            // replay is the exception and already sees the current roster.
            var eventSlot = (initialized ? observedSlots : roster).GetValueOrDefault(actor.Id, -1);
            var statuses = actor.StatusSlots ?? (actor.Statuses ?? actor.StatusExpirations?.Keys.ToArray() ?? [])
                .Select((s, i) => new ReplayStatus(s, 0, 0, actor.StatusExpirations?.GetValueOrDefault(s) ?? DateTime.MaxValue)
                    { SlotIndex = i }).ToArray();
            var bySlot = new Dictionary<int, ReplayStatus>();
            for (var i = 0; i < statuses.Count; ++i)
                bySlot[statuses[i].SlotIndex ?? i] = statuses[i];
            // The feed applies native status slots in order. A replacement loses the
            // previous status before gaining the new one in that same slot.
            foreach (var slot in bySlot.Keys.Concat(observed.Keys.Where(k => k.Actor == actor.Id).Select(k => k.Slot)).Distinct().Order())
            {
                var key = (actor.Id, slot);
                var status = bySlot.GetValueOrDefault(slot);
                var old = observed.GetValueOrDefault(key);
                if (status != null)
                {
                    current[key] = status;
                    if (old != null && old.Id == status.Id && old.SourceId == status.SourceId && old.Param == status.Param
                        && (status.ExpiresAt - old.ExpiresAt).TotalSeconds <= 1)
                        continue;
                }
                if (old != null && (status == null || old.Id != status.Id || old.SourceId != status.SourceId))
                    Status(eventSlot, old.Id, false, default);
                if (status != null)
                    Status(eventSlot, status.Id, true, status.ExpiresAt);
            }
        }
        observed.Clear();
        foreach (var pair in current) observed.Add(pair.Key, pair.Value);
        observedSlots.Clear();
        foreach (var member in roster) observedSlots[member.Key] = member.Value;
        initialized = true;
    }

    internal bool ImmuneAt(int slot, DateTime activation) =>
        Enumerable.Range(0, 3).Any(category => expiries.GetValueOrDefault((slot, category)) > activation);

    internal void Removed(ulong actor)
    {
        if (!observedSlots.TryGetValue(actor, out var slot)) return;
        foreach (var key in observed.Keys.Where(k => k.Actor == actor).ToArray())
        {
            Status(slot, observed[key].Id, false, default);
            observed.Remove(key);
        }
    }

    internal void Clear()
    {
        expiries.Clear();
        observed.Clear();
        observedSlots.Clear();
        initialized = false;
    }
}

using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntMisdirectionState
{
    // PR priority slots can be reordered; use actor identity for the source's stable party-slot bit.
    private readonly HashSet<ulong> affected = [];
    private readonly HashSet<(ulong Actor, uint Status)> observed = [];
    private readonly Dictionary<(ulong Actor, uint Status), bool> pending = [];

    internal static bool Matches(uint status) => status is 1422 or 2936 or 3694 or 3909;

    internal void Status(ulong target, uint status, bool present, IReadOnlyList<ReplayPartyMember> party)
    {
        if (!Matches(status)) return;
        pending[(target, status)] = present;
        Apply(target, status, present, party);
    }

    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, IReadOnlyList<ReplayPartyMember> party)
    {
        var current = party.Where(p => p.Slot is >= 0 and < 64 && actors.ContainsKey(p.EntityId))
            .SelectMany(p => actors[p.EntityId].Statuses.Where(s => Matches(s.Id)).Select(s => (Actor: p.EntityId, Status: s.Id)))
            .ToHashSet();
        // The source mask is cleared by status loss, not by death, missing objects or a timer.
        current.UnionWith(observed.Where(p => !actors.ContainsKey(p.Actor)));
        foreach (var (key, present) in pending.ToArray())
        {
            if (current.Contains(key) == present) pending.Remove(key);
            else if (present) current.Add(key);
            else current.Remove(key);
        }
        foreach (var key in observed.Except(current).ToArray()) Apply(key.Actor, key.Status, false, party);
        foreach (var key in current.Except(observed).ToArray()) Apply(key.Actor, key.Status, true, party);
    }

    internal bool Active(ulong target, IReadOnlyList<ReplayPartyMember> party)
        => party.Any(p => p.EntityId == target && p.Slot is >= 0 and < 64) && affected.Contains(target);

    private void Apply(ulong target, uint status, bool present, IReadOnlyList<ReplayPartyMember> party)
    {
        if (present) observed.Add((target, status));
        else observed.Remove((target, status));
        if (present) affected.Add(target);
        else affected.Remove(target);
    }

    internal void Clear()
    {
        affected.Clear();
        observed.Clear();
        pending.Clear();
    }
}

using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntKerState
{
    private readonly Dictionary<int, (DateTime At, long Order)> stays = [];
    private readonly Dictionary<(ulong Actor, uint Id), ReplayStatus> statuses = [];
    private readonly Dictionary<(ulong Actor, uint Id), ReplayStatus?> pending = [];
    private readonly Dictionary<(ulong Actor, uint Id), DateTime> watermarks = [];
    private int mirrors;
    internal string? GlobalHint => mirrors > 0 ? $"The next {mirrors} interments will be mirrored!" : null;
    internal static bool Flare(uint action) => action is 27704 or 27638 or 27706;

    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors,
        IReadOnlyList<ReplayPartyMember> party)
    {
        if (cast.Rule.Action is 27927 or 27928) mirrors = cast.Rule.Action == 27927 ? 3 : 4;
        if (!Flare(cast.Rule.Action)) return;
        foreach (var member in party.Where(p => p.Slot is >= 0 and < 8
            && actors.TryGetValue(p.EntityId, out var actor) && !actor.Dead))
            stays[member.Slot] = (cast.Activation, cast.Order);
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        if (!Flare(updated.Rule.Action)) return;
        foreach (var (slot, state) in stays.Where(p => p.Value.Order == previous.Order).ToArray())
            stays[slot] = (updated.Activation, state.Order);
    }

    internal void Effect(DateTime at, IEnumerable<ulong> targets, IReadOnlyList<ReplayPartyMember> party)
    {
        stays.Clear();
        foreach (var target in targets)
            if (Slot(target, party) is var slot && slot >= 0) stays[slot] = (at, 0);
    }

    internal void Status(ulong actor, uint id, ReplayStatus? value, DateTime at, IReadOnlyList<ReplayPartyMember> party)
    {
        if (id is not (2848 or 960)) return;
        var key = (actor, id);
        if (watermarks.TryGetValue(key, out var previous) && at < previous) return;
        watermarks[key] = at;
        pending[key] = value;
        Apply(actor, id, value, party);
        if (value == null) statuses.Remove(key);
        else statuses[key] = value;
    }

    internal void RemoveMissingActors(IReadOnlyDictionary<ulong, ReplayActorState> actors, IReadOnlyList<ReplayPartyMember> party)
    {
        foreach (var actor in statuses.Keys.Select(k => k.Actor).Concat(pending.Keys.Select(k => k.Actor)).Distinct()
            .Where(id => !actors.ContainsKey(id)).ToArray())
            Remove(actor, party);
    }

    internal void Observe(ReplayActorState actor, IReadOnlyList<ReplayPartyMember> party)
    {
        var current = actor.Statuses.Where(s => s.Id is 2848 or 960)
            .GroupBy(s => s.Id).ToDictionary(g => (Actor: actor.Id, Id: g.Key), g => g.First());
        foreach (var (key, value) in pending.Where(p => p.Key.Actor == actor.Id).ToArray())
        {
            current.TryGetValue(key, out var observed);
            if (value == null && observed == null
                || value != null && observed != null && value.SourceId == observed.SourceId && value.Param == observed.Param
                    && Math.Abs((value.ExpiresAt - observed.ExpiresAt).TotalSeconds) <= 1)
                pending.Remove(key);
            else if (value == null) current.Remove(key);
            else current[key] = value;
        }
        foreach (var (key, old) in statuses.Where(p => p.Key.Actor == actor.Id).ToArray())
            if (!current.TryGetValue(key, out var value) || value.SourceId != old.SourceId)
                Apply(key.Actor, key.Id, null, party);
        foreach (var (key, value) in current)
            if (!statuses.TryGetValue(key, out var old) || value.SourceId != old.SourceId || value.Param != old.Param
                || (value.ExpiresAt - old.ExpiresAt).TotalSeconds > 1)
                Apply(key.Actor, key.Id, value, party);
        foreach (var key in statuses.Keys.Where(k => k.Actor == actor.Id).ToArray()) statuses.Remove(key);
        foreach (var pair in current) statuses[pair.Key] = pair.Value;
    }

    internal void Remove(ulong actor, IReadOnlyList<ReplayPartyMember> party)
    {
        foreach (var key in statuses.Keys.Where(k => k.Actor == actor).ToArray())
        {
            Apply(actor, key.Id, null, party);
            statuses.Remove(key);
        }
        foreach (var key in pending.Keys.Where(k => k.Actor == actor).ToArray()) pending.Remove(key);
        foreach (var key in watermarks.Keys.Where(k => k.Actor == actor).ToArray()) watermarks.Remove(key);
    }

    private void Apply(ulong actor, uint id, ReplayStatus? value, IReadOnlyList<ReplayPartyMember> party)
    {
        if (id == 2848) mirrors = value?.Param is >= 1 and <= 4 ? value.Param : 0;
        else if (value == null && Slot(actor, party) is var slot && slot >= 0) stays.Remove(slot);
    }

    internal string? Hint(ReplayActorState player, IReadOnlyList<ReplayPartyMember> party, DateTime now)
        => State(player, party) is { } state && state <= now.AddSeconds(2.5) ? "Stop everything!" : null;

    internal bool Hold(ReplayActorState player, IReadOnlyList<ReplayPartyMember> party, DateTime now)
        => State(player, party) is { } state && state != default && state <= now;

    private DateTime? State(ReplayActorState player, IReadOnlyList<ReplayPartyMember> party)
    {
        var slot = Slot(player.Id, party);
        if (player.Dead)
        {
            stays.Remove(slot);
            return null;
        }
        return stays.TryGetValue(slot, out var state) ? state.At : null;
    }

    private static int Slot(ulong actor, IReadOnlyList<ReplayPartyMember> party)
        => party.Where(p => p.EntityId == actor && p.Slot is >= 0 and < 64).Select(p => p.Slot).DefaultIfEmpty(-1).First();
}

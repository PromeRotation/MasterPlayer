using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntGunittState
{
    private readonly List<HuntStackState> stacks = [];
    private readonly Dictionary<HuntStackState, ulong> masks = [];
    private readonly Dictionary<ulong, ReplayStatus> statuses = [];
    private readonly Dictionary<ulong, ReplayStatus?> pending = [];
    private readonly Dictionary<ulong, DateTime> watermarks = [];
    private ulong forbidden;
    internal IEnumerable<HuntStackState> Stacks => stacks;

    internal void Icon(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors,
        IReadOnlyList<ReplayPartyMember> party)
    {
        var stack = new HuntStackState(int.MaxValue, 10);
        stack.Start(cast, actors, Players(forbidden, party));
        stacks.Add(stack);
        masks[stack] = forbidden;
    }

    internal void Finish()
    {
        if (stacks.Count == 0) return;
        masks.Remove(stacks[0]);
        stacks.RemoveAt(0);
    }

    internal void Status(ulong actor, ReplayStatus? value, DateTime at, IReadOnlyList<ReplayPartyMember> party)
    {
        if (watermarks.TryGetValue(actor, out var previous) && at < previous) return;
        watermarks[actor] = at;
        pending[actor] = value;
        Apply(actor, value != null, party);
        if (value == null) statuses.Remove(actor);
        else statuses[actor] = value;
    }

    internal void Observe(ReplayActorState actor, IReadOnlyList<ReplayPartyMember> party, DateTime? now)
    {
        var current = actor.Statuses.FirstOrDefault(s => s.Id == 1138);
        if (pending.TryGetValue(actor.Id, out var value))
        {
            if (value == null && current == null || value != null && current != null
                && value.SourceId == current.SourceId && value.Param == current.Param
                && current.ExpiresAt >= value.ExpiresAt.AddSeconds(-1)
                || value != null && current == null && value.ExpiresAt < now)
                pending.Remove(actor.Id);
            else current = value;
        }
        statuses.TryGetValue(actor.Id, out var old);
        if (old != null && (current == null || current.SourceId != old.SourceId)) Apply(actor.Id, false, party);
        if (current != null && (old == null || old.SourceId != current.SourceId || old.Param != current.Param
            || (current.ExpiresAt - old.ExpiresAt).TotalSeconds > 1))
            Apply(actor.Id, true, party);
        if (current == null) statuses.Remove(actor.Id);
        else statuses[actor.Id] = current;
    }

    internal void Remove(ulong actor, IReadOnlyList<ReplayPartyMember> party)
    {
        if (statuses.Remove(actor)) Apply(actor, false, party);
        pending.Remove(actor);
        watermarks.Remove(actor);
        foreach (var stack in stacks) stack.Remove(actor);
    }

    internal void RemoveMissingActors(IReadOnlyDictionary<ulong, ReplayActorState> actors, IReadOnlyList<ReplayPartyMember> party)
    {
        foreach (var actor in statuses.Keys.Concat(pending.Keys).Distinct().Where(id => !actors.ContainsKey(id)).ToArray())
            Remove(actor, party);
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors, IReadOnlyList<ReplayPartyMember> party)
    {
        // Only the raw FIFO head receives new debuffs. Dead targets still occupy that head.
        if (stacks.Count != 0) masks[stacks[0]] = forbidden;
        foreach (var stack in stacks)
        {
            stack.Update(actors);
            stack.SetForbidden(Players(masks[stack], party));
        }
    }

    internal IEnumerable<HuntDamagePrediction> PredictedDamage(IReadOnlyList<ReplayPartyMember> party)
    {
        var active = stacks.Where(s => s.Active).ToArray();
        if (active.Length == 0) yield break;
        var intersection = ulong.MaxValue;
        foreach (var stack in active) intersection &= masks[stack];
        if (intersection == 0) yield break;
        var first = active[0].Cast!;
        var activation = active.Min(s => s.Cast!.Activation);
        foreach (var member in party.Where(p => p.Slot is >= 0 and < 64 && p.EntityId is not (0 or 0xE0000000)
            && (intersection & (1ul << p.Slot)) == 0).DistinctBy(p => p.EntityId))
            yield return new(first.Source, member.EntityId, activation, "Shared");
    }

    private void Apply(ulong actor, bool present, IReadOnlyList<ReplayPartyMember> party)
    {
        var slot = party.Where(p => p.EntityId == actor && p.Slot is >= 0 and < 64)
            .Select(p => p.Slot).DefaultIfEmpty(-1).First();
        if (slot < 0) return;
        if (present) forbidden |= 1ul << slot;
        else forbidden &= ~(1ul << slot);
    }

    private static IEnumerable<ulong> Players(ulong mask, IReadOnlyList<ReplayPartyMember> party)
        => party.Where(p => p.Slot is >= 0 and < 64 && (mask & (1ul << p.Slot)) != 0).Select(p => p.EntityId);
}

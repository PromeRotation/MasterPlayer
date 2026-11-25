using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

// Event delivery and raw actor sampling acknowledge the same transitions in either order.
internal sealed class HuntStatusInput(uint statusId, Action<ReplayActorState, ReplayStatus> gain, Action<ulong> lose)
{
    private readonly uint observedStatusId = statusId;
    private readonly Dictionary<(ulong Actor, ulong Source), ReplayStatus?> sampled = [];
    private readonly Dictionary<(ulong Actor, ulong Source), (DateTime At, long Order)> sampledAt = [];
    private readonly Dictionary<(ulong Actor, ulong Source), ReplayStatus?> eventStatuses = [];
    private readonly Dictionary<(ulong Actor, ulong Source), (DateTime At, long Order)> currentEvent = [];
    private readonly Dictionary<(ulong Actor, ulong Source), List<(ReplayStatus? Before, ReplayStatus? After, DateTime At)>> pending = [];
    private readonly Dictionary<(ulong Actor, ulong Source), List<(ReplayStatus? Before, ReplayStatus? After, DateTime At)>> receipts = [];
    private readonly Dictionary<(ulong Actor, ulong Source), DateTime> watermarks = [];
    private long inputOrder;

    internal void Status(ReplayActorState actor, ReplayStatus? value, ulong source, DateTime at)
    {
        var key = (actor.Id, source);
        if (watermarks.TryGetValue(key, out var previous) && at < previous) return;
        watermarks[key] = at;
        var known = eventStatuses.TryGetValue(key, out var previousEvent);
        if (!known)
        {
            previousEvent = sampledAt.TryGetValue(key, out var rawStamp) && at < rawStamp.At
                ? null : sampled.GetValueOrDefault(key);
        }
        // A historical event value can still be empty after sampling a complete new cycle.
        // Match the sampled removal before replaying a queued Lose into newer mechanic state.
        var occupied = sampled.GetValueOrDefault(key) != null
            || previousEvent != null && currentEvent.ContainsKey(key);
        DateTime? removalFloor = value == null && occupied ? at : null;
        if (value == null && previousEvent == null && receipts.TryGetValue(key, out var history))
            previousEvent = history.FirstOrDefault(t => t.After == null && (removalFloor == null || t.At >= removalFloor)).Before;
        eventStatuses[key] = value;
        if (Consume(receipts, key, previousEvent, value, removalFloor))
        {
            currentEvent.Remove(key);
            return;
        }
        if (known && value != null && RetargetRemoval(key, previousEvent, value, at))
        {
            gain(actor, value!);
            currentEvent.Remove(key);
            return;
        }
        Apply(actor, value);
        Add(pending, key, previousEvent, value, at);
        currentEvent[key] = (at, ++inputOrder);
    }

    internal void RemoveMissing(IReadOnlySet<ulong> actors)
    {
        foreach (var id in sampled.Keys.Concat(eventStatuses.Keys).Select(k => k.Actor).Distinct()
            .Where(id => !actors.Contains(id)).ToArray()) Remove(id);
    }

    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now, ulong? onlyActor = null)
    {
        if (onlyActor == null) RemoveMissing(actors.Keys.ToHashSet());
        var raw = actors.Values.Where(a => onlyActor == null || a.Id == onlyActor)
            .SelectMany(a => a.Statuses.Where(s => s.Id == observedStatusId).Select(s => (Actor: a, Status: s)))
            .ToDictionary(p => (Actor: p.Actor.Id, Source: p.Status.SourceId), p => p.Status);
        foreach (var key in sampled.Keys.Concat(raw.Keys).Where(k => onlyActor == null || k.Actor == onlyActor).Distinct().ToArray())
        {
            var current = raw.GetValueOrDefault(key);
            var previous = sampled.GetValueOrDefault(key);
            sampled[key] = current;
            if (actors.TryGetValue(key.Actor, out var actor)) ObserveSample(actor, key, previous, current, now);
        }
    }

    internal static void ObserveOrdered(IReadOnlyDictionary<ulong, ReplayActorState> actors, ulong actorId,
        DateTime now, params HuntStatusInput[] inputs)
    {
        if (!actors.TryGetValue(actorId, out var actor)) return;
        var transitions = inputs.SelectMany(input =>
        {
            var raw = actor.Statuses.Where(s => s.Id == input.observedStatusId)
                .ToDictionary(s => (Actor: actorId, Source: s.SourceId));
            return input.sampled.Keys.Where(k => k.Actor == actorId).Concat(raw.Keys).Distinct()
                .SelectMany(key =>
                {
                    var current = raw.GetValueOrDefault(key);
                    var previous = input.sampled.GetValueOrDefault(key);
                    input.sampled[key] = current;
                    if (previous?.SlotIndex is { } oldSlot && current?.SlotIndex is { } newSlot && oldSlot != newSlot)
                        return new[] { (Input: input, Key: key, Current: (ReplayStatus?)null, Previous: previous),
                            (Input: input, Key: key, Current: current, Previous: (ReplayStatus?)null) };
                    return new[] { (Input: input, Key: key, Current: current, Previous: previous) };
                }).ToArray();
        }).OrderBy(t => t.Current?.SlotIndex ?? t.Previous?.SlotIndex ?? int.MaxValue)
            .ThenBy(t => t.Current == null ? 0 : 1).ToArray();
        // Slot replacement loses the old status before gaining the new one. Legacy recordings
        // lack native slot indices; process removals first rather than inventing an index.
        foreach (var (input, key, current, previous) in transitions)
            input.ObserveSample(actor, key, previous, current, now);
    }

    private void ObserveSample(ReplayActorState actor, (ulong Actor, ulong Source) key,
        ReplayStatus? previous, ReplayStatus? current, DateTime now)
    {
        if (Same(previous, current)) return;
        var stamp = (At: now, Order: ++inputOrder);
        sampledAt[key] = stamp;
        if (currentEvent.TryGetValue(key, out var overlay) && !Later(overlay, stamp)) currentEvent.Remove(key);
        if (ConsumeObserved(key, previous, current))
        {
            if (Same(current, eventStatuses.GetValueOrDefault(key))) currentEvent.Remove(key);
            return;
        }
        Apply(actor, current);
        Add(receipts, key, previous, current, now);
    }

    internal void Remove(ulong actor)
    {
        // Destroy emits one Lose per occupied slot, never one per historical refresh.
        foreach (var key in sampled.Keys.Concat(eventStatuses.Keys).Where(k => k.Actor == actor).Distinct().ToArray())
        {
            var raw = sampled.GetValueOrDefault(key);
            var eventValue = eventStatuses.GetValueOrDefault(key);
            var eventIsNewer = currentEvent.TryGetValue(key, out var overlay) && Later(overlay, sampledAt.GetValueOrDefault(key));
            var value = eventIsNewer ? eventValue : raw;
            if (value != null && !Consume(pending, key, value, null)) lose(actor);
        }
        foreach (var key in pending.Keys.Where(k => k.Actor == actor).ToArray()) pending.Remove(key);
        foreach (var key in receipts.Keys.Where(k => k.Actor == actor).ToArray()) receipts.Remove(key);
        foreach (var key in watermarks.Keys.Where(k => k.Actor == actor).ToArray()) watermarks.Remove(key);
        foreach (var key in sampled.Keys.Where(k => k.Actor == actor).ToArray()) sampled.Remove(key);
        foreach (var key in sampledAt.Keys.Where(k => k.Actor == actor).ToArray()) sampledAt.Remove(key);
        foreach (var key in eventStatuses.Keys.Where(k => k.Actor == actor).ToArray()) eventStatuses.Remove(key);
        foreach (var key in currentEvent.Keys.Where(k => k.Actor == actor).ToArray()) currentEvent.Remove(key);
    }

    private static bool Later((DateTime At, long Order) value, (DateTime At, long Order) baseline)
        => value.At > baseline.At || value.At == baseline.At && value.Order > baseline.Order;

    private bool RetargetRemoval((ulong Actor, ulong Source) key, ReplayStatus? before, ReplayStatus after, DateTime at)
    {
        if (!receipts.TryGetValue(key, out var transitions)) return false;
        var index = transitions.FindIndex(t => t.After == null && Same(t.Before, before) && at <= t.At);
        if (index < 0) return false;
        var removal = transitions[index];
        transitions[index] = (after, null, removal.At);
        return true;
    }

    private bool ConsumeObserved((ulong Actor, ulong Source) key, ReplayStatus? before, ReplayStatus? after)
    {
        if (!pending.TryGetValue(key, out var transitions)) return false;
        // Confirm complete paths when sampling skips a refresh and its removal.
        for (var start = 0; start < transitions.Count; ++start)
        {
            var cursor = before;
            for (var end = start; end < transitions.Count && Same(transitions[end].Before, cursor); ++end)
            {
                cursor = transitions[end].After;
                if (!Same(cursor, after)) continue;
                transitions.RemoveRange(start, end - start + 1);
                if (transitions.Count == 0) pending.Remove(key);
                return true;
            }
        }
        return Consume(pending, key, before, after);
    }

    private static void Add(
        Dictionary<(ulong Actor, ulong Source), List<(ReplayStatus? Before, ReplayStatus? After, DateTime At)>> ledger,
        (ulong Actor, ulong Source) key, ReplayStatus? before, ReplayStatus? after, DateTime at)
    {
        if (!ledger.TryGetValue(key, out var transitions)) ledger[key] = transitions = [];
        transitions.Add((before, after, at));
    }

    private static bool Consume(
        Dictionary<(ulong Actor, ulong Source), List<(ReplayStatus? Before, ReplayStatus? After, DateTime At)>> ledger,
        (ulong Actor, ulong Source) key, ReplayStatus? before, ReplayStatus? after, DateTime? notBefore = null)
    {
        if (!ledger.TryGetValue(key, out var transitions)) return false;
        var index = transitions.FindIndex(t => (notBefore == null || t.At >= notBefore)
            && (after == null ? t.After == null && Same(t.Before, before) : Same(t.After, after)));
        if (index < 0) return false;
        transitions.RemoveAt(index);
        if (transitions.Count == 0) ledger.Remove(key);
        return true;
    }

    private void Apply(ReplayActorState actor, ReplayStatus? status)
    {
        if (status != null) gain(actor, status);
        else lose(actor.Id);
    }

    private static bool Same(ReplayStatus? a, ReplayStatus? b)
        => a == null ? b == null : b != null && a.SourceId == b.SourceId && a.Param == b.Param
            && Math.Abs((a.ExpiresAt - b.ExpiresAt).TotalSeconds) <= .1;
}

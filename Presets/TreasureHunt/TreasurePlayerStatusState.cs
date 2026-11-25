using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

// Keeps raw observations separate from unacknowledged network transitions. Timers do not synthesize Lose.
internal sealed class TreasurePlayerStatusState(uint[]? observedStatuses = null)
{
    private readonly uint[] statuses = observedStatuses ?? [1958, 1960, 1959, 1961, 1257, 3629, 1422, 2936, 3694, 3909];
    internal Action<ulong, uint, ReplayStatus?>? Changed { get; set; }
    private readonly HuntMarchState march = new();
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus?> current = [];
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus?> sampled = [];
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus?> removedValues = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, ReplayStatus? Value)> pending = [];
    private readonly Dictionary<(ulong Actor, uint Status), DateTime> watermarks = [];
    private readonly Dictionary<(ulong Actor, uint Status), List<(ReplayStatus? Before, ReplayStatus? Value, DateTime At)>> receipts = [];
    private readonly Dictionary<ulong, DateTime> destroyed = [];
    private readonly Dictionary<(ulong Actor, uint Status), ReplayStatus?> destroyedSamples = [];
    private readonly HashSet<ulong> seen = [];
    private readonly HashSet<ulong> misdirected = [];

    internal bool Misdirected(ulong actor) => misdirected.Contains(actor);
    internal void BeginActorGeneration(ulong actor, DateTime at)
    {
        if (destroyed.TryGetValue(actor, out var end) && at <= end) return;
        destroyed.Remove(actor);
        foreach (var key in destroyedSamples.Keys.Where(k => k.Actor == actor).ToArray())
            destroyedSamples.Remove(key);
    }
    internal IEnumerable<(Vector3 From, Vector3 To, float Rotation)> Movements(ReplayActorState actor, DateTime now)
        => march.Movements(actor, now);

    internal void Status(ulong actor, uint status, bool present, DateTime at, ushort param,
        DateTime? expiration, ulong source)
    {
        if (!statuses.Contains(status) || destroyed.TryGetValue(actor, out var end) && at <= end) return;
        var key = (actor, status);
        if (watermarks.TryGetValue(key, out var previous) && at < previous) return;
        watermarks[key] = at;
        var value = present ? new ReplayStatus(status, param, source, expiration ?? DateTime.MaxValue) : null;
        if (receipts.TryGetValue(key, out var history))
        {
            var match = history.FindIndex(r => at <= r.At && Equivalent(r.Value, value));
            if (match >= 0)
            {
                var receipt = history[match];
                history.RemoveAt(match);
                if (!present && current.GetValueOrDefault(key) == null)
                    removedValues[key] = receipt.Before;
                // Raw may have applied the replacement before its queued Lose/Gain pair arrived.
                // The matching Gain must undo that pair's intervening Lose, without replaying an
                // already active Gain a second time.
                if (pending.TryGetValue(key, out var overlay)
                    && (overlay.Present != present || !Equivalent(overlay.Value, value)))
                {
                    var latestRaw = sampled.GetValueOrDefault(key);
                    if (history.Any(r => r.At > receipt.At && Equivalent(r.Value, latestRaw)))
                    {
                        // An even newer raw transition already superseded this historical pair.
                        pending.Remove(key);
                        removedValues.Remove(key);
                        Deliver(actor, status, latestRaw);
                    }
                    else
                    {
                        pending[key] = (present, value);
                        if (present) removedValues.Remove(key);
                        Deliver(actor, status, value);
                    }
                }
                return;
            }
        }
        if (!present)
            removedValues[key] = current.GetValueOrDefault(key)
                ?? (pending.TryGetValue(key, out var before) ? before.Value : null);
        else
            removedValues.Remove(key);
        pending[key] = (present, value);
        destroyedSamples.Remove(key);
        // A missing duration cannot establish a forced movement's activation/end clock.
        if (present && expiration == null && NeedsClock(status)) return;
        Deliver(actor, status, value);
    }

    internal void ObserveMissing(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        foreach (var id in seen.Where(id => !actors.ContainsKey(id)).ToArray()) Remove(id, now);
    }

    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        ObserveMissing(actors, now);
        foreach (var actor in actors.Values) ObserveActor(actor, now);
    }

    internal void ObserveActor(ReplayActorState actor, DateTime now)
    {
        seen.Add(actor.Id);
        foreach (var status in statuses)
        {
            var key = (actor.Id, status);
            var raw = actor.Statuses.FirstOrDefault(s => s.Id == status);
            if (destroyedSamples.TryGetValue(key, out var oldGeneration))
            {
                if (Equivalent(raw, oldGeneration)) continue;
                destroyedSamples.Remove(key);
            }
            var previous = sampled.GetValueOrDefault(key);
            sampled[key] = raw;
            if (pending.TryGetValue(key, out var expected))
            {
                var removed = removedValues.GetValueOrDefault(key);
                var sameRemoved = SameRemovedGeneration(raw, removed);
                if (!expected.Present && raw != null && !sameRemoved && !Equivalent(previous, raw))
                {
                    // Sampling may skip the complete old Gain/Lose cycle. A distinct new raw
                    // generation must not wait forever for the old empty-slot acknowledgement.
                    pending.Remove(key);
                    removedValues.Remove(key);
                    Deliver(actor.Id, status, raw);
                    if (!receipts.TryGetValue(key, out var newHistory)) receipts[key] = newHistory = [];
                    newHistory.Add((previous, raw, now));
                    continue;
                }
                var acknowledged = expected.Present
                    ? raw != null && raw.SourceId == expected.Value!.SourceId && raw.Param == expected.Value.Param
                        && (expected.Value.ExpiresAt == DateTime.MaxValue || Equivalent(raw, expected.Value))
                    : raw == null;
                if (acknowledged)
                {
                    pending.Remove(key);
                    if (expected.Present && expected.Value!.ExpiresAt == DateTime.MaxValue)
                        Deliver(actor.Id, status, raw);
                }
                // An old raw frame cannot revoke or replay the authoritative event.
                continue;
            }
            if (raw != null && removedValues.TryGetValue(key, out var removedGeneration))
            {
                // An empty-slot acknowledgement ends the overlay, not the old generation's
                // barrier: its delayed occupied frame must still not undo the real Lose.
                if (SameRemovedGeneration(raw, removedGeneration)) continue;
                removedValues.Remove(key);
            }
            if (Equivalent(previous, raw)) continue;
            Deliver(actor.Id, status, raw);
            if (!receipts.TryGetValue(key, out var history)) receipts[key] = history = [];
            history.Add((previous, raw, now));
        }
    }

    private void Deliver(ulong actor, uint status, ReplayStatus? value)
    {
        if (value != null && (value.ExpiresAt == DateTime.MaxValue || value.ExpiresAt == default)
            && NeedsClock(status)) return;
        var key = (actor, status);
        var previous = current.GetValueOrDefault(key);
        current[key] = value;
        // The old world feed updates small or earlier expiry corrections without emitting Gain.
        if (previous == null ? value == null : value != null && previous.SourceId == value.SourceId
            && previous.Param == value.Param && (value.ExpiresAt - previous.ExpiresAt).TotalSeconds <= 1) return;
        if (Changed != null)
        {
            Changed(actor, status, value);
        }
        else if (HuntMisdirectionState.Matches(status))
        {
            if (value != null) misdirected.Add(actor);
            else misdirected.Remove(actor);
        }
        else
        {
            march.ApplyObservedStatus(actor, status, value);
        }
    }

    private static bool NeedsClock(uint status) => !HuntMisdirectionState.Matches(status) && status is not (4551 or 4552);

    private static bool Equivalent(ReplayStatus? a, ReplayStatus? b)
        => a == null ? b == null : b != null && a.SourceId == b.SourceId && a.Param == b.Param
            && Math.Abs((a.ExpiresAt - b.ExpiresAt).TotalSeconds) <= 1;

    private static bool SameRemovedGeneration(ReplayStatus? raw, ReplayStatus? removed)
        => raw != null && removed != null && raw.SourceId == removed.SourceId && raw.Param == removed.Param
            && (removed.ExpiresAt == DateTime.MaxValue || Equivalent(raw, removed));

    internal void Remove(ulong actor, DateTime at)
    {
        if (destroyed.TryGetValue(actor, out var previousEnd) && at <= previousEnd) return;
        destroyed[actor] = at;
        foreach (var key in current.Keys.Where(k => k.Actor == actor).ToArray())
            if (current[key] != null) Changed?.Invoke(actor, key.Status, null);
        seen.Remove(actor);
        march.Remove(actor);
        misdirected.Remove(actor);
        foreach (var key in current.Keys.Concat(sampled.Keys).Concat(pending.Keys).Concat(removedValues.Keys).Concat(watermarks.Keys)
            .Concat(receipts.Keys).Where(k => k.Actor == actor).Distinct().ToArray())
        {
            var previous = sampled.GetValueOrDefault(key) ?? current.GetValueOrDefault(key);
            if (previous != null) destroyedSamples.TryAdd(key, previous);
            current.Remove(key);
            sampled.Remove(key);
            removedValues.Remove(key);
            pending.Remove(key);
            watermarks.Remove(key);
            receipts.Remove(key);
        }
    }

    internal void Clear()
    {
        march.Clear();
        current.Clear();
        sampled.Clear();
        removedValues.Clear();
        pending.Clear();
        watermarks.Clear();
        receipts.Clear();
        destroyed.Clear();
        destroyedSamples.Clear();
        seen.Clear();
        misdirected.Clear();
    }
}

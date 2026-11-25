using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

// Sonata observes the world's pending status list, including entries created
// before its component activates. Actual statuses only acknowledge that list.
internal sealed class HuntAglaopePendingStatuses
{
    private readonly List<HuntAglaopePendingEffect> pending = [];
    private readonly Dictionary<ulong, ReplayActorState> observed = [];
    internal IReadOnlyList<HuntAglaopePendingEffect> Pending => pending;

    internal void Effect(ulong source, uint sequence, IReadOnlyList<ulong> targets, ulong[][]? raw,
        IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        if (!actors.ContainsKey(source) || raw == null || raw.Length != targets.Count) return;
        for (var i = 0; i < targets.Count; ++i)
        {
            var target = targets[i];
            if (!actors.ContainsKey(target) || raw[i] is not { Length: 8 }) continue;
            foreach (var effect in raw[i])
            {
                if ((byte)effect is not (14 or 15)) continue;
                var effectSource = (effect & (1ul << 45)) != 0 ? target : source;
                var recipient = (effect & (1ul << 47)) != 0 ? source : target;
                pending.Add(new(recipient, sequence, i, effectSource, now.AddSeconds(3),
                    (ushort)(effect >> 48), (byte)(effect >> 24)));
            }
        }
    }

    internal void Observe(IReadOnlyList<ReplayActorState> actors, DateTime now)
    {
        var ids = actors.Select(a => a.Id).ToHashSet();
        pending.RemoveAll(p => p.Expiration < now || !ids.Contains(p.Actor));
        foreach (var actor in actors)
        {
            observed.TryGetValue(actor.Id, out var before);
            var previousStatuses = Slots(before?.Statuses ?? []);
            foreach (var (slot, status) in Slots(actor.Statuses).OrderBy(p => p.Key))
            {
                var old = previousStatuses.GetValueOrDefault(slot);
                if (status.Id == 0 || old != null && old.Id == status.Id && old.SourceId == status.SourceId
                    && old.Param == status.Param && (status.ExpiresAt - old.ExpiresAt).TotalSeconds <= 1)
                    continue;
                RemoveFirst(p => p.Actor == actor.Id && p.StatusId == status.Id && p.Source == status.SourceId);
            }
            var previousIncoming = (before?.IncomingEffects ?? []).ToDictionary(s => s.Slot);
            foreach (var incoming in actor.IncomingEffects.OrderBy(s => s.Slot))
            {
                var previous = previousIncoming.GetValueOrDefault(incoming.Slot);
                // Match the current feed: a previously confirmed slot replaced by
                // another confirmed slot does not issue a second EffectResult.
                if (incoming.GlobalSequence != 0 && incoming.TargetConfirmed && previous?.TargetConfirmed != true)
                    RemoveFirst(p => p.Actor == actor.Id && p.GlobalSequence == incoming.GlobalSequence
                        && p.TargetIndex == incoming.TargetIndex);
            }
        }
        observed.Clear();
        foreach (var actor in actors) observed[actor.Id] = actor;
    }

    private static Dictionary<int, ReplayStatus> Slots(IReadOnlyList<ReplayStatus> statuses) =>
        statuses.Select((s, i) => (Slot: s.SlotIndex ?? i, Status: s)).ToDictionary(p => p.Slot, p => p.Status);

    private void RemoveFirst(Predicate<HuntAglaopePendingEffect> match)
    {
        var index = pending.FindIndex(match);
        if (index >= 0) pending.RemoveAt(index);
    }

    internal bool HasSeduced(ulong actor) => pending.Any(p => p.Actor == actor && p.StatusId == 991);
    internal void Advance(DateTime now) => pending.RemoveAll(p => p.Expiration < now);
    internal void Remove(ulong actor)
    {
        pending.RemoveAll(p => p.Actor == actor);
        observed.Remove(actor);
    }
    internal void Clear()
    {
        pending.Clear();
        observed.Clear();
    }
}

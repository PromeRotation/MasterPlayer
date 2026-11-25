using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivaleDraugarState
{
    internal bool NeedsBootstrap { get; set; } = true;
    internal DungeonPendingCast? Inversion { get; set; }
    internal HuntMarchState March { get; } = new();
    internal Dictionary<(ulong Actor, int Slot), ReplayStatus> StatusSlots { get; } = [];

    internal void Observe(DungeonActor actor)
    {
        var statuses = actor.StatusSlots ?? (actor.Statuses ?? actor.StatusExpirations?.Keys.ToArray() ?? [])
            .Select((id, slot) => new ReplayStatus(id, actor.StatusParams?.GetValueOrDefault(id) ?? 0,
                actor.StatusSources?.GetValueOrDefault(id) ?? 0,
                actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue) { SlotIndex = slot }).ToArray();
        var bySlot = new Dictionary<int, ReplayStatus>();
        for (var i = 0; i < statuses.Count; ++i) bySlot[statuses[i].SlotIndex ?? i] = statuses[i];
        foreach (var slot in bySlot.Keys.Concat(StatusSlots.Keys.Where(k => k.Actor == actor.Id).Select(k => k.Slot)).Distinct().Order())
        {
            var key = (actor.Id, slot);
            var value = bySlot.GetValueOrDefault(slot);
            var previous = StatusSlots.GetValueOrDefault(key);
            if (value != null) StatusSlots[key] = value;
            else StatusSlots.Remove(key);
            if (previous != null && value != null && previous.Id == value.Id && previous.SourceId == value.SourceId
                && previous.Param == value.Param && (value.ExpiresAt - previous.ExpiresAt).TotalSeconds <= 1) continue;
            if (previous != null && (value == null || previous.Id != value.Id || previous.SourceId != value.SourceId))
                Apply(actor.Id, previous.Id, null);
            if (value != null) Apply(actor.Id, value.Id, value);
        }
    }

    internal void Removed(ulong actor)
    {
        foreach (var key in StatusSlots.Keys.Where(k => k.Actor == actor).OrderBy(k => k.Slot).ToArray())
        {
            Apply(actor, StatusSlots[key].Id, null);
            StatusSlots.Remove(key);
        }
    }

    private void Apply(ulong actor, uint status, ReplayStatus? value)
    {
        if (status is not (1958 or 1959 or 1960 or 1961 or 1257 or 3629)) return;
        // The source owns forced march by player and direction, not by status source.
        // Keep the real per-slot identity above; prevent the shared reducer from inventing
        // an extra loss when a second occupied slot has the same ID and another source.
        March.ApplyObservedStatus(actor, status, value == null ? null : value with { SourceId = 0 });
    }
}

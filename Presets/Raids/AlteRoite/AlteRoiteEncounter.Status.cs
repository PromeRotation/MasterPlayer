using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class AlteRoiteEncounter
{
    private void Status(AlteRoiteActor actor, ReplayStatus status, bool gain, bool hot)
    {
        if (!Active || hot && actor.Actor.NativeType == 0x202) return;
        StatusComponent(actor, status, gain, 1);
        StatusComponent(actor, status, gain, 4);
    }

    private void StatusComponent(AlteRoiteActor actor, ReplayStatus status, bool gain, int component)
    {
        var slot = party.FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
        if (slot is < 0 or >= 64) return;
        if (component == 1)
        {
            if (status.Id == 911)
            {
                if (gain) IceMask |= 1ul << slot;
                else IceMask &= ~(1ul << slot);
            }
            return;
        }
        var category = status.Id switch
        {
            160 or 1209 or 3054 => 0,
            1722 or 2663 => 1,
            2345 => 2,
            _ => -1,
        };
        if (category >= 0) immunities[slot, category] = gain ? status.ExpiresAt : default;
    }

    internal bool Immune(DateTime activation)
        => Slot >= 0 && Enumerable.Range(0, 3).Any(i => immunities[Slot, i] > activation);

    private void ObserveStatuses(AlteRoiteActor actor)
    {
        var next = new ReplayStatus?[60];
        var values = actor.Actor.StatusSlots ?? (actor.Actor.Statuses ?? []).Select((id, i) => new ReplayStatus(id,
            actor.Actor.StatusParams?.GetValueOrDefault(id) ?? 0, actor.Actor.StatusSources?.GetValueOrDefault(id) ?? 0,
            actor.Actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue) { SlotIndex = i }).ToArray();
        for (var i = 0; i < values.Count; ++i)
        {
            var slot = values[i].SlotIndex ?? i;
            if (slot is >= 0 and < 60 && values[i].Id != 0)
                next[slot] = values[i] with { SourceId = Sanitize(values[i].SourceId) };
        }
        for (var i = 0; i < 60; ++i)
        {
            var previous = actor.Statuses[i];
            var value = next[i];
            if (previous != null && value != null && previous.Id == value.Id && previous.Param == value.Param
                && previous.SourceId == value.SourceId && (value.ExpiresAt - previous.ExpiresAt).TotalSeconds <= 1d)
            { actor.Statuses[i] = value; continue; }
            if (previous != null && (previous.Id != value?.Id || previous.SourceId != value?.SourceId))
                Status(actor, previous, false, true);
            actor.Statuses[i] = value;
            if (value != null) Status(actor, value, true, true);
        }
    }
}

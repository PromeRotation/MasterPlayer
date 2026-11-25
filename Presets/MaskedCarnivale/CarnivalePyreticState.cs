using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class CarnivalePyreticState
{
    internal DungeonPendingCast?[] Requirements { get; } = new DungeonPendingCast?[64];
    private readonly Dictionary<(ulong Actor, int Slot), ReplayStatus> statuses = [];

    internal void Started(DungeonPendingCast cast) => Array.Fill(Requirements, cast);

    internal void Observe(DungeonActor actor)
    {
        var slots = actor.StatusSlots ?? (actor.Statuses ?? actor.StatusExpirations?.Keys.ToArray() ?? [])
            .Select((id, slot) => new ReplayStatus(id, 0, actor.StatusSources?.GetValueOrDefault(id) ?? 0,
                actor.StatusExpirations?.GetValueOrDefault(id) ?? DateTime.MaxValue) { SlotIndex = slot }).ToArray();
        var bySlot = new Dictionary<int, ReplayStatus>();
        for (var i = 0; i < slots.Count; ++i) bySlot[slots[i].SlotIndex ?? i] = slots[i];
        foreach (var slot in bySlot.Keys.Concat(statuses.Keys.Where(k => k.Actor == actor.Id).Select(k => k.Slot)).Distinct().Order())
        {
            var key = (actor.Id, slot);
            var old = statuses.GetValueOrDefault(key);
            var value = bySlot.GetValueOrDefault(slot);
            if (old != null && (value?.Id != 960 || value.SourceId != old.SourceId))
                Array.Clear(Requirements);
            if (value?.Id == 960) statuses[key] = value;
            else statuses.Remove(key);
        }
    }

    internal void Removed(ulong actor)
    {
        foreach (var key in statuses.Keys.Where(k => k.Actor == actor).ToArray())
        {
            Array.Clear(Requirements);
            statuses.Remove(key);
        }
    }
}

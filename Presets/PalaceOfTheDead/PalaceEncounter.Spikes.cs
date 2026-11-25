using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed partial class PalaceEncounter
{
    private readonly Dictionary<ulong, ReplayStatus?[]> spikeStatusSlots = [];

    private void ObserveSpikes(ReplayActorState actor, DateTime now, bool observe)
    {
        if (!observe) return;
        if (!spikeStatusSlots.TryGetValue(actor.Id, out var previous))
            spikeStatusSlots[actor.Id] = previous = new ReplayStatus?[60];
        var next = new ReplayStatus?[60];
        for (var i = 0; i < actor.Statuses.Length; ++i)
        {
            var status = actor.Statuses[i];
            var slot = status.SlotIndex ?? i;
            if (slot is >= 0 and < 60 && status.Id != 0)
                next[slot] = status with
                {
                    SourceId = status.SourceId == 0xE0000000 ? 0 : status.SourceId,
                    ExpiresAt = status.ZeroRemainingTime == true ? now : status.ExpiresAt
                };
        }
        for (var i = 0; i < 60; ++i)
        {
            var old = previous[i];
            var value = next[i];
            if (old != null && value != null && old.Id == value.Id && old.SourceId == value.SourceId
                && old.Param == value.Param && (value.ExpiresAt - old.ExpiresAt).TotalSeconds <= 1d)
            {
                previous[i] = value;
                continue;
            }
            if (old is { Id: 197 or 198 } && (old.Id != value?.Id || old.SourceId != value?.SourceId))
                spikes.Remove(actor.Id);
            previous[i] = value;
            if (value is { Id: 197 or 198 })
                spikes[actor.Id] = now.AddSeconds(10);
        }
    }
}

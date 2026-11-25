namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly Dictionary<ulong, DateTime> ioCaution = [];
    private readonly HashSet<ulong> ioDeadCaution = [];
    private bool IsIo => territory == JobQuestCatalog.POWER_TO_PROTECT;
    private DateTime? IoStopActivation => IsIo && Active != null
        && _actors.GetValueOrDefault(PlayerId) is { Dead: false, PartySlot: >= 0 } actor
        && HasStatus(actor, 1269) && ioCaution.TryGetValue(actor.Id, out var activation) ? activation : null;
    private bool IoRequireAllStop => IoStopActivation is { } activation && activation < _now.AddSeconds(1);

    private void IoRemoved(ulong id)
    {
        ioCaution.Remove(id);
        ioDeadCaution.Remove(id);
    }

    private void IoStatus(ulong target, uint status, bool present, DateTime? expiresAt)
    {
        if (!IsIo || Active == null || status != 1269 || _actors.GetValueOrDefault(target)?.PartySlot is not >= 0) return;
        if (present && expiresAt != null)
        {
            ioDeadCaution.Remove(target);
            ioCaution[target] = expiresAt.Value;
        }
        if (!present)
        {
            ioCaution.Remove(target);
            ioDeadCaution.Remove(target);
        }
    }

    private void IoObserve()
    {
        if (!IsIo || Active == null) return;
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0))
        {
            if (!HasStatus(actor, 1269))
            {
                ioCaution.Remove(actor.Id);
                ioDeadCaution.Remove(actor.Id);
            }
            else if (actor.Dead)
            {
                ioCaution.Remove(actor.Id);
                ioDeadCaution.Add(actor.Id);
            }
            else if (!ioDeadCaution.Contains(actor.Id) && actor.StatusExpirations?.TryGetValue(1269, out var activation) == true)
                ioCaution[actor.Id] = activation;
        }
        foreach (var id in ioCaution.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) ioCaution.Remove(id);
        ioDeadCaution.RemoveWhere(id => !_actors.ContainsKey(id));
    }
}

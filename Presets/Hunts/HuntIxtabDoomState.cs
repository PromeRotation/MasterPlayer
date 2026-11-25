using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntIxtabDoomState
{
    private readonly List<(ReplayActorState Actor, bool Attached)> doomed = [];
    private readonly HuntStatusInput input;

    internal HuntIxtabDoomState()
    {
        input = new(1769, (actor, _) => doomed.Add((actor, true)), Lose);
    }

    internal void Status(ReplayActorState actor, ReplayStatus? value, ulong source, DateTime at)
        => input.Status(actor, value, source, at);

    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        input.Observe(actors, now);
        for (var i = 0; i < doomed.Count; ++i)
        {
            var entry = doomed[i];
            if (entry.Attached && actors.TryGetValue(entry.Actor.Id, out var current)) doomed[i] = (current, true);
            else if (entry.Attached) doomed[i] = (entry.Actor, false);
        }
    }

    internal void Remove(ulong actor)
    {
        input.Remove(actor);
        for (var i = 0; i < doomed.Count; ++i)
            if (doomed[i].Actor.Id == actor) doomed[i] = (doomed[i].Actor, false);
    }

    private void Lose(ulong id)
    {
        var index = doomed.FindIndex(d => d.Attached && d.Actor.Id == id);
        if (index >= 0) doomed.RemoveAt(index);
    }

    internal IEnumerable<string> Hints(ReplayActorState player)
    {
        if (doomed.Count == 0) yield break;
        var healer = player.ClassJob is 6 or 24 or 28 or 33 or 40;
        if (doomed.Any(d => d.Attached && d.Actor.Id == player.Id))
            yield return healer ? "Heal yourself to full! (Doom)." : "You were doomed! Get healed to full fast.";
        else if (healer)
            foreach (var entry in doomed) yield return $"Heal to full {entry.Actor.Name}! (Doom)";
    }
}

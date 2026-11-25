using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Hunts;

// Preparation Lose only removes its countdown; the resolving status Lose ends the requirement.
internal sealed class HuntStayMoveState
{
    private readonly uint preparation;
    private readonly string label;
    private readonly string mode;
    private readonly double hintWindow;
    private readonly HuntStatusInput preparationInput;
    private readonly HuntStatusInput resolvingInput;
    private readonly Dictionary<int, DateTime> requirements = [];
    private readonly HashSet<int> prepared = [];
    private IReadOnlyList<ReplayPartyMember> party = [];

    internal HuntStayMoveState(uint preparation, uint resolving, string label, string mode, double hintWindow)
    {
        this.preparation = preparation;
        this.label = label;
        this.mode = mode;
        this.hintWindow = hintWindow;
        preparationInput = new(preparation, (actor, status) =>
        {
            var slot = Slot(actor.Id);
            if (slot < 0) return;
            prepared.Add(slot);
            requirements[slot] = status.ExpiresAt;
        }, actor => prepared.Remove(Slot(actor)));
        resolvingInput = new(resolving, static (_, _) => { }, actor => requirements.Remove(Slot(actor)));
    }

    private int Slot(ulong actor) => party.FirstOrDefault(p => p.EntityId == actor && p.Slot is >= 0 and < 64)?.Slot ?? -1;
    internal void SetParty(IReadOnlyList<ReplayPartyMember> members) => party = members;
    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, ulong actor, DateTime now)
    {
        HuntStatusInput.ObserveOrdered(actors, actor, now, preparationInput, resolvingInput);
    }
    internal void RemoveMissing(IReadOnlySet<ulong> actors)
    {
        preparationInput.RemoveMissing(actors);
        resolvingInput.RemoveMissing(actors);
    }
    internal void Remove(ulong actor)
    {
        preparationInput.Remove(actor);
        resolvingInput.Remove(actor);
    }
    internal void Status(ReplayActorState actor, uint status, ReplayStatus? value, ulong source, DateTime at)
    {
        if (status == preparation) preparationInput.Status(actor, value, source, at);
        else resolvingInput.Status(actor, value, source, at);
    }
    internal IEnumerable<string> Hints(ReplayActorState player, DateTime now)
    {
        var slot = Slot(player.Id);
        if (requirements.TryGetValue(slot, out var activation))
        {
            if (activation <= now.AddSeconds(hintWindow)) yield return mode == "Freezing" ? "Move!" : "Stop everything!";
            if (player.Dead) requirements.Remove(slot);
            else yield return $"SpecialMode: {mode} activation={activation:O}";
        }
        if (prepared.Contains(slot))
        {
            var deadline = requirements.GetValueOrDefault(slot);
            yield return $"{label} on you in {(deadline - now).TotalSeconds:f1}s. ({(mode == "Freezing" ? "Freezing" : "Pyretic")}!)";
        }
    }
}

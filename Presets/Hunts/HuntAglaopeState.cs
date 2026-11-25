using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntAglaopeState(ulong owner)
{
    private readonly CarnivaleKnockbackImmunity immunity = new();
    private readonly List<HuntAglaopeKnockback> knockbacks = [];
    private HuntPendingCast? sonata;
    private bool done;
    private IReadOnlyList<ReplayPartyMember> party = [];
    internal IEnumerable<HuntPendingCast> Areas => sonata == null ? [] : [sonata];
    internal IReadOnlyList<HuntAglaopeKnockback> KnockbackSources => knockbacks;

    internal void Observe(IReadOnlyDictionary<ulong, ReplayActorState> actors, IReadOnlyList<ReplayPartyMember> roster)
    {
        party = roster;
        immunity.Observe(actors.Values.Select(a => new DungeonActor(a.Id, a.Oid, a.Position, StatusSlots: a.Statuses)),
            roster.Where(p => p.Slot is >= 0 and < 64).ToDictionary(p => p.EntityId, p => p.Slot));
    }

    internal void Start(HuntPendingCast cast)
    {
        if (cast.Rule.Action == 16824)
            sonata = cast with { Shape = new CircleTemplate(16.2f), Rotation = 0, SequenceId = $"{owner}/aglaope/sonata" };
        // Starting another Sonata does not reset the source's done latch.
        if (cast.Rule.Action == 18056) knockbacks.Add(new(cast));
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated)
    {
        if (sonata?.Source == previous.Source && sonata.Order == previous.Order)
            sonata = sonata with { Location = updated.Location, Activation = updated.Activation, Input = updated.Input };
        var index = knockbacks.FindIndex(k => k.Cast.Source == previous.Source && k.Cast.Order == previous.Order);
        if (index >= 0) knockbacks[index] = new(updated);
    }

    internal void Finish(ulong source, uint action)
    {
        if (action != 18056) return;
        var index = knockbacks.FindIndex(k => k.Cast.Source == source);
        if (index >= 0) knockbacks.RemoveAt(index);
    }

    internal void Effect(uint action)
    {
        if (action == 16824) done = true;
    }

    internal void Update(HuntAglaopePendingStatuses pending)
    {
        if (!done) return;
        var player = party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0;
        if (pending.HasSeduced(player)) return;
        sonata = null;
        done = false;
    }

    internal IEnumerable<HuntMovementProjection> Knockbacks(ReplayActorState player)
    {
        var slot = party.FirstOrDefault(p => p.EntityId == player.Id && p.Slot is >= 0 and < 64)?.Slot ?? -1;
        if (slot < 0) yield break;
        var from = player.Position;
        foreach (var kb in knockbacks)
        {
            if (immunity.ImmuneAt(slot, kb.Activation)) continue;
            var delta = from - kb.Origin;
            delta.Y = 0;
            var length = delta.Length();
            if (length == 0 || length > HuntAglaopeKnockback.RADIUS) continue;
            var to = from + delta / length * HuntAglaopeKnockback.DISTANCE;
            yield return new(kb.Cast.Source, from, to);
            from = to;
        }
    }
}

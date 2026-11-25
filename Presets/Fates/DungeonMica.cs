using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsMica => territory == NativeFateCatalog.MICA;
    private readonly List<DungeonActor> micaActorOrder = [];
    private readonly HashSet<ulong> micaObservedActors = [];
    private readonly List<MicaAoe?> micaCards = [];
    private readonly Queue<(int Safe, DungeonPendingCast Cast)> micaCachedCards = [];
    private readonly List<MicaAoe> micaFlourish = [];
    private readonly List<MicaAoe> micaPizzas = [];
    private readonly List<MicaAoe> micaRolling = [];
    private readonly List<MicaAoe> micaHoops = [];
    private long micaAoeId;
    private int micaRollingVersion;

    private void MicaObserveActors()
    {
        if (!IsMica) return;
        for (var i = micaActorOrder.Count - 1; i >= 0; --i)
        {
            var old = micaActorOrder[i];
            if (_actors.TryGetValue(old.Id, out var raw) && FateCreatedActor(raw) is { } current && current.Oid == old.Oid)
                micaActorOrder[i] = current;
            else if (micaObservedActors.Contains(old.Id))
            {
                micaActorOrder.RemoveAt(i);
                micaObservedActors.Remove(old.Id);
            }
        }
        foreach (var raw in _actors.Values)
        {
            var actor = FateCreatedActor(raw);
            if (!fateCreatedActors.ContainsKey(actor.Id)) micaObservedActors.Add(actor.Id);
            MicaCreated(actor);
        }
    }

    private void MicaCreated(DungeonActor actor)
    {
        if (!IsMica) return;
        var previous = micaActorOrder.FindIndex(a => a.Id == actor.Id);
        if (previous >= 0 && micaActorOrder[previous].Oid == actor.Oid) return;
        if (previous >= 0)
        {
            micaActorOrder.RemoveAt(previous);
            micaObservedActors.Remove(actor.Id);
        }
        micaActorOrder.Add(actor);
    }

    private void MicaAddCards(int safe, DungeonPendingCast cast)
    {
        if (micaCards.Count > 0)
        {
            micaCachedCards.Enqueue((safe, cast));
            return;
        }
        for (var card = 0; card < 6; ++card)
        {
            if (card == safe) continue;
            var actor = micaActorOrder.FirstOrDefault(a => a.Oid == 0x43EEu + card);
            if (actor == null)
            {
                micaCards.Add(null);
                continue;
            }
            micaCards.Add(new(++micaAoeId, actor, cast, RayRectangleTemplate.Symmetric(14, 20),
                QuantizedDungeonPosition(actor.Position + new Vector3(0, 0, -10)), -.003f * (MathF.PI / 180), default, Risky: false));
        }
    }

    private void MicaCast(DungeonPendingCast cast, bool correction, DungeonPendingCast? previousCast)
    {
        if (!IsMica) return;
        var action = cast.Rule.Action;
        if (action == 38678)
        {
            for (var i = 0; i < micaCards.Count; ++i)
                if (micaCards[i] is { } card)
                    micaCards[i] = card with { Activation = cast.FinishAt.AddSeconds(.6), Risky = true };
            return;
        }
        if (action is >= 38666 and <= 38677)
        {
            if (!correction) MicaAddCards((int)(action - 38666) % 6, cast);
            return;
        }
        var source = _actors[cast.Source];
        if (action is >= 38996 and <= 38999)
        {
            var index = correction ? micaRolling.FindIndex(a => ReferenceEquals(a.Cast, previousCast)) : -1;
            if (correction && index < 0) return;
            var old = index >= 0 ? micaRolling[index] : null;
            var stage = old?.Stage ?? micaRolling.Count;
            var length = action switch { 38996 => 20, 38997 => 29, 38998 => 43, _ => 52 };
            var aoe = new MicaAoe(old?.Id ?? ++micaAoeId, source, cast, RayRectangleTemplate.Symmetric(10, length),
                cast.Location, cast.Rotation, cast.FinishAt.AddSeconds(5 + stage * .6), stage,
                DangerColor: old?.DangerColor ?? false);
            if (index >= 0) micaRolling[index] = aoe;
            else micaRolling.Add(aoe);
            return;
        }
        var list = action switch
        {
            38697 or 38698 or 38700 or 38701 => micaFlourish,
            38694 or 38695 => micaPizzas,
            38692 => micaHoops,
            _ => null,
        };
        if (list == null) return;
        var previous = correction ? list.FindIndex(a => ReferenceEquals(a.Cast, previousCast)) : -1;
        if (correction && previous < 0) return;
        GeometryTemplate shape = action switch
        {
            38697 or 38698 => new CircleTemplate(10),
            38700 or 38701 => new DonutTemplate(10, 30),
            38692 => RayRectangleTemplate.Symmetric(5, 42),
            _ => new FanTemplate(40, MathF.PI / 3),
        };
        var oldAoe = previous >= 0 ? list[previous] : null;
        var next = new MicaAoe(oldAoe?.Id ?? ++micaAoeId, source, cast, shape, cast.Location,
            cast.Rotation, cast.FinishAt);
        if (previous >= 0) list[previous] = next;
        else list.Add(next);
        if (!correction && list == micaFlourish && list.Count > 1 && list[0].Activation > list[1].Activation)
            (list[0], list[1]) = (list[1], list[0]);
        if (!correction && list == micaPizzas && list.Count >= 6)
            DungeonAoeOrder.Sort(list, a => a.Activation);
    }

    private void MicaFinish(DungeonPendingCast cast)
    {
        if (!IsMica) return;
        if (cast.Rule.Action == 39156)
        {
            micaCards.Clear();
            if (micaCachedCards.TryDequeue(out var cached)) MicaAddCards(cached.Safe, cached.Cast);
        }
        if (cast.Rule.Action is 38697 or 38698 or 38700 or 38701 && micaFlourish.Count > 0)
            micaFlourish.RemoveAt(0);
        // SimpleAOEGroups removes a matching source on any observed cast finish.
        var pizza = micaPizzas.FindIndex(a => a.Cast.Source == cast.Source);
        if (pizza >= 0) micaPizzas.RemoveAt(pizza);
        if (cast.Rule.Action == 38692)
        {
            var hoop = micaHoops.FindIndex(a => a.Cast.Source == cast.Source);
            if (hoop >= 0) micaHoops.RemoveAt(hoop);
        }
    }

    private void MicaEffect(uint action)
    {
        if (IsMica && action is 38687 or 38688 && micaRolling.Count > 0) micaRolling.RemoveAt(0);
    }

    private IEnumerable<DungeonAmbientAoe> MicaAoes()
    {
        if (!IsMica || Active == null) yield break;
        if (micaRollingVersion != micaRolling.Count)
        {
            if (micaRolling.Count > 1) micaRolling[0] = micaRolling[0] with { DangerColor = true };
            micaRollingVersion = micaRolling.Count;
        }
        var all = micaCards.OfType<MicaAoe>().Concat(micaFlourish.Take(1)).Concat(micaPizzas.Take(3))
            .Concat(micaRolling).Concat(micaHoops.Take(4));
        foreach (var aoe in all)
            yield return new($"mica/aoe/{aoe.Id}", aoe.Cast.Rule.Name, aoe.Shape, aoe.Position, aoe.Rotation,
                Navigate: aoe.Risky, Activation: aoe.Activation,
                Color: aoe.DangerColor ? new(1, 1, 0, 1) : null);
    }

    private IEnumerable<string> MicaHints(Vector3 player)
    {
        if (MicaAoes().Any(a => a.Navigate && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player, 0)))
            yield return "GTFO from aoe!";
    }

    private void ResetMica()
    {
        micaCards.Clear();
        micaCachedCards.Clear();
        micaFlourish.Clear();
        micaPizzas.Clear();
        micaRolling.Clear();
        micaHoops.Clear();
        micaAoeId = 0;
        micaRollingVersion = 0;
    }
}

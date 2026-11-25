using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<EurekaSwipe> eurekaSwipes = [];
    private readonly List<EurekaShock> eurekaShocks = [];
    private readonly HashSet<AlphaActorState> eurekaShockCreated = [];
    private readonly List<EurekaGroanKnockback> eurekaGroans = [];
    internal IReadOnlyList<EurekaSwipe> EurekaSwipes => eurekaSwipes;
    internal IReadOnlyList<EurekaShock> EurekaShocks => eurekaShocks;
    internal IReadOnlyList<EurekaGroanKnockback> EurekaGroans => eurekaGroans;

    private void EurekaMinotaurCreated(DungeonActor actor)
    {
        if (territory != EurekaOrthosCatalog.MINOTAUR || actor.Oid != 0x3DA2
            || !eurekaActors.TryGetValue(actor.Id, out var state) || !eurekaShockCreated.Add(state)) return;
        eurekaShocks.Add(new(++eurekaGeneration, state, QuantizedDungeonPosition(actor.Position),
            _now, _now.AddSeconds(13d)));
    }

    private void EurekaMinotaurCast(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.MINOTAUR || !cast.Observed) return;
        if (cast.Rule.Action == 31867)
        {
            var count = eurekaSwipes.Count;
            var at = count == 0 ? cast.FinishAt.AddSeconds(8.7d) : eurekaSwipes[0].Activation.AddSeconds(count * 2d);
            eurekaSwipes.Add(new(++eurekaGeneration, cast, at));
            if (count == 1) eurekaSwipes[0].Danger = true;
        }
        else if (cast.Rule.Action == 31876)
        {
            var index = eurekaGroans.FindIndex(k => k.Cast.Source == cast.Source);
            if (index >= 0) eurekaGroans[index] = new(cast);
            else eurekaGroans.Add(new(cast));
        }
    }

    private void EurekaMinotaurFinish(DungeonPendingCast cast)
    {
        if (territory != EurekaOrthosCatalog.MINOTAUR || !cast.Observed) return;
        if (cast.Rule.Action == 31874) eurekaShocks.Clear();
        if (cast.Rule.Action == 31876)
        {
            var index = eurekaGroans.FindIndex(k => k.Cast.Source == cast.Source);
            if (index >= 0) eurekaGroans.RemoveAt(index);
        }
    }

    private void EurekaMinotaurEffect(uint action)
    {
        if (action is not (31872 or >= 31868 and <= 31871) || eurekaSwipes.Count == 0) return;
        eurekaSwipes.RemoveAt(0);
        if (eurekaSwipes.Count != 0) eurekaSwipes[0].Danger = true;
    }

    internal IEnumerable<(EurekaGroanKnockback Source, Vector3 From, Vector3 To, bool Unsafe)> EurekaGroanMovements(DungeonActor player)
    {
        if (territory != EurekaOrthosCatalog.MINOTAUR || Arena == null) yield break;
        var from = player.Position;
        foreach (var source in eurekaGroans)
        {
            if (KnockbackImmune(player, source.Cast.FinishAt)) continue;
            var direction = from - source.Cast.Location;
            direction.Y = 0;
            if (direction.LengthSquared() == 0) continue;
            var to = from + Vector3.Normalize(direction) * source.Distance;
            yield return (source, from, to, !Arena.Contains(to, 0));
            from = to;
        }
    }

    private IEnumerable<DungeonAmbientAoe> EurekaMinotaurAoes()
    {
        if (territory != EurekaOrthosCatalog.MINOTAUR) yield break;
        foreach (var swipe in eurekaSwipes.Take(2))
            yield return new($"eureka/swipe/{swipe.Generation}", "Octuple Swipe", new FanTemplate(40, MathF.PI / 2),
                swipe.Cast.Location, swipe.Cast.Rotation, Activation: swipe.Activation,
                Color: swipe.Danger ? new Vector4(1, 0, 0, 1) : null);
        foreach (var shock in eurekaShocks)
            yield return new($"eureka/shock/{shock.Generation}", "Shock", new CircleTemplate(5),
                shock.Origin, Activation: shock.Activation);
        if (eurekaGroans.Count != 0)
            yield return new("eureka/groan/navigation", "Disorienting Groan", new DonutTemplate(5, 100),
                eurekaGroans[0].Cast.Location, Display: false, Activation: eurekaGroans[0].Cast.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var index = 0;
        foreach (var movement in EurekaGroanMovements(player))
            foreach (var aoe in QuestKnockbackProjection($"eureka/groan/{index++}", player, movement.From, movement.To))
                yield return aoe;
    }

    private IEnumerable<string> EurekaMinotaurHints()
    {
        if (territory != EurekaOrthosCatalog.MINOTAUR || Active == null || !eurekaEnabled
            || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (EurekaMinotaurAoes().Where(a => a.Id.StartsWith("eureka/swipe/") || a.Id.StartsWith("eureka/shock/"))
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(player.Position)))
            yield return "GTFO from aoe!";
        if (EurekaGroanMovements(player).Any(m => m.Unsafe)) yield return "About to be knocked into danger!";
    }
}

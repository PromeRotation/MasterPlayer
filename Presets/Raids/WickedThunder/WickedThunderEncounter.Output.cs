using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class WickedThunderEncounter
{
    // Source ActiveAOEs mutates the cannon and hunt lists, but SidewiseSpark
    // returns copies. Keeping that distinction preserves colors after promotion.
    internal IEnumerable<WickedThunderAoe> VisibleCannons()
    {
        if (Cannons.Count == 0) yield break;
        Cannons[0] = Cannons[0] with { Risky = true, Danger = Cannons[0].Danger || Cannons.Count > 1 };
        foreach (var aoe in Cannons.Take(2)) yield return aoe;
    }

    internal IEnumerable<WickedThunderAoe> VisibleSides()
    {
        for (var i = 0; i < Math.Min(2, Sides.Count); ++i)
            yield return Sides[i] with
            {
                Danger = i == 0 && Sides.Count > 1 && Sides[0].Activation != Sides[1].Activation,
                Risky = i == 0 || Sides[i].Activation == Sides[0].Activation,
            };
    }

    internal IEnumerable<WickedThunderAoe> VisibleHunts()
    {
        for (var i = 0; i < Math.Min(12, Hunts.Count); ++i)
            Hunts[i] = Hunts[i] with { Risky = true, Danger = Hunts[i].Danger || Hunts.Count > 12 };
        return Hunts;
    }

    internal static float BaitRotation(WickedThunderAoe bait)
        => ReferenceEquals(bait.Source, bait.Target) ? bait.Source.Actor.Rotation
            : MathF.Atan2(bait.Target!.Actor.Position.X - bait.Source.Actor.Position.X,
                bait.Target.Actor.Position.Z - bait.Source.Actor.Position.Z);

    internal IEnumerable<DungeonDamagePrediction> Damage()
    {
        if (!Active || Slot < 0) yield break;
        if (Hyper is { } hyper)
            foreach (var target in Party.Where(p => !p.Actor.Dead))
                yield return Prediction(target, hyper.Activation, "Raidwide");
        if (Baits.Count > 0)
            foreach (var targetId in Baits.Where(b => !b.Source.Actor.Dead).Select(b => b.TargetId).Distinct())
                if (party.FirstOrDefault(p => p.EntityId == targetId) is { } member)
                {
                    var actor = Actors.GetValueOrDefault(targetId)?.Actor;
                    yield return new(targetId, member.Slot, actor?.PartyRole ?? "", actor?.Name ?? "",
                        Baits[0].Activation, "Tankbuster");
                }
        foreach (var cast in Ordinary.Where(m => m.Action == 37575))
            foreach (var target in Party.Where(p => !p.Actor.Dead))
                yield return Prediction(target, Activation(cast.Source), "Raidwide");
    }

    private DungeonDamagePrediction Prediction(WickedThunderActor actor, DateTime activation, string type)
        => new(actor.Actor.Id, party.First(p => p.EntityId == actor.Actor.Id).Slot,
            actor.Actor.PartyRole, actor.Actor.Name, activation, type);

    internal IEnumerable<string> Hints()
    {
        if (!Active) yield break;
        if (InputDiagnostic != null) yield return InputDiagnostic;
        if (Ordinary.Any(m => m.Action == 37575)) yield return "Raidwide";
        if (Actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var point = viewer.Actor.Position;
        var aoes = VisibleCannons().Concat(VisibleSides()).Concat(VisibleHunts())
            .Concat(Ordinary.Where(m => m.Shape != null)).Concat(Stampede is { } stamp ? [stamp] : []);
        if (aoes.Any(m => m.Risky && DungeonSourceGeometry.Contains(m.Shape!, m.Origin, m.Rotation, point)))
            yield return "GTFO from aoe!";
        foreach (var bait in Baits.Where(b => !b.Source.Actor.Dead))
        {
            var rotation = BaitRotation(bait);
            if (bait.TargetId == PlayerId && Party.Any(p => p.Actor.Id != PlayerId && !p.Actor.Dead
                && DungeonSourceGeometry.Contains(bait.Shape!, bait.Source.Actor.Position, rotation, p.Actor.Position)))
            { yield return "Bait away from raid!"; break; }
            if (!ReferenceEquals(bait.Target, viewer)
                && DungeonSourceGeometry.Contains(bait.Shape!, bait.Source.Actor.Position, rotation, point))
            { yield return "GTFO from baited aoe!"; break; }
        }
        foreach (var action in new uint[] { 37571, 37569 })
            foreach (var hint in StackHints(viewer, action)) yield return hint;
        var spreads = Ordinary.Where(m => m.Action == 37573 && m.Target?.Actor.Dead == false).ToArray();
        if (spreads.Any(m => ReferenceEquals(m.Target, viewer)))
            yield return Party.Any(p => !ReferenceEquals(p, viewer) && !p.Actor.Dead
                && Inside(viewer.Actor.Position, p.Actor.Position, 6)) ? "Spread!" : "Spread!（已分散）";
        if (spreads.Any(m => !ReferenceEquals(m.Target, viewer) && Inside(m.Target!.Actor.Position, point, 6)))
            yield return "GTFO from spreads!";
    }

    private IEnumerable<string> StackHints(WickedThunderActor viewer, uint action)
    {
        var stacks = Stacks.Where(s => s.Action == action && s.Target?.Actor.Dead == false).ToArray();
        var members = Party.Where(p => !p.Actor.Dead).ToArray();
        if (stacks.FirstOrDefault(s => ReferenceEquals(s.Target, viewer)) is { } self)
        {
            var radius = action == 37571 ? 5 : 6;
            var near = members.Where(p => !ReferenceEquals(p, viewer)
                && Inside(viewer.Actor.Position, p.Actor.Position, radius)).ToArray();
            var count = near.Length + 1;
            yield return count != 8 || near.Any(p => stacks.Any(s => ReferenceEquals(s.Target, p)))
                ? $"Stack! ({count}/8)" : "Stack (8/8)";
        }
        else if (stacks.Length > 0)
        {
            var radius = action == 37571 ? 5 : 6;
            var participating = stacks.Count(s => Inside(s.Target!.Actor.Position, viewer.Actor.Position, radius));
            var unsatisfied = stacks.Any(s => 1 + members.Count(p => !ReferenceEquals(p, s.Target)
                && Inside(s.Target!.Actor.Position, p.Actor.Position, radius)) < 8);
            if (participating > 1 || participating == 0 && unsatisfied) yield return "Stack!";
            else if (participating == 1) yield return "Stack";
        }
    }

    internal static bool Inside(Vector3 center, Vector3 point, float radius)
        => KefkaEncounter.DistanceSquared(DungeonEncounter.QuantizedDungeonPosition(center), point) <= radius * radius;
}

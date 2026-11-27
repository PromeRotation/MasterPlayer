using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private List<BozjaActor>? dimCrystals;
    private List<BozjaActor>? corruptedCrystals;
    private List<BozjaActor>? irminsulBlockers;
    private readonly Dictionary<BozjaRule, BozjaSight> crystalSights = [];
    private readonly List<(BozjaCast Cast, int Stage)> earthSequences = [];
    private readonly List<BozjaCast> earthPublished = [];
    internal List<BozjaRush> Hailfires { get; } = [];
    internal int NumEarthCasts { get; private set; }
    internal int NumFinishedSpreads { get; private set; }
    private int earthVersion;
    private int earthCount;
    private static readonly GeometryTemplate[] earthShapes =
        [new CircleTemplate(10), new DonutTemplate(10, 20), new DonutTemplate(20, 30)];
    private static readonly GeometryTemplate[] arachneEarthShapes =
        [new CircleTemplate(10.5f), new DonutTemplate(10.5f, 20.5f), new DonutTemplate(20.5f, 30.5f)];
    internal bool IsCrystals => !IsIrminsul && Definition.Rules.Any(r => r.Kind == BozjaRuleKind.CrystalSight);
    private List<BozjaActor> IrminsulBlockers => irminsulBlockers ??= Actors.Values.Where(a => a.Actor.Oid == 0x13F1).ToList();
    private List<BozjaActor> DimCrystals => dimCrystals ??= Actors.Values.Where(a => a.Actor.Oid == 0x31CD).ToList();
    private List<BozjaActor> CorruptedCrystals => corruptedCrystals ??= Actors.Values.Where(a => a.Actor.Oid == 0x31CE).ToList();
    internal IEnumerable<BozjaActor> Crystals => DimCrystals.Concat(CorruptedCrystals);

    internal void ObserveCrystalDrawing()
    {
        if (IsCrystals && Primary != null) _ = Crystals.ToArray();
    }

    private void ClearCrystals()
    {
        dimCrystals = corruptedCrystals = null;
        crystalSights.Clear();
        earthSequences.Clear();
        earthPublished.Clear();
        Hailfires.Clear();
        NumEarthCasts = NumFinishedSpreads = earthVersion = earthCount = 0;
    }

    private void CrystalCreated(BozjaActor actor)
    {
        if (actor.Actor.Oid == 0x31CD) dimCrystals?.Add(actor);
        if (actor.Actor.Oid == 0x31CE) corruptedCrystals?.Add(actor);
        if (actor.Actor.Oid == 0x13F1) irminsulBlockers?.Add(actor);
    }

    private void CrystalDestroyed(BozjaActor actor)
    {
        dimCrystals?.Remove(actor);
        corruptedCrystals?.Remove(actor);
        irminsulBlockers?.Remove(actor);
    }

    private void RefreshCrystalSight(BozjaSight sight)
    {
        BozjaActor? earliest = null;
        var remaining = double.MaxValue;
        foreach (var caster in sight.Casters)
        {
            var cast = IsIrminsul ? caster.RawCast : caster.Cast;
            var elapsed = IsIrminsul ? caster.RawElapsed : caster.Elapsed;
            if (cast != null && Total(cast) - elapsed < remaining)
            {
                remaining = Total(cast) - elapsed;
                earliest = caster;
            }
        }
        sight.Origin = IsIrminsul ? earliest?.RawCast?.Location : earliest?.Cast?.Location;
    }

    private void StartCrystals(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.CrystalSight)
        {
            if (!crystalSights.TryGetValue(rule, out var sight)) crystalSights[rule] = sight = new();
            sight.Casters.Add(actor);
            RefreshCrystalSight(sight);
            List<BozjaActor> candidates;
            if (IsIrminsul) candidates = IrminsulBlockers;
            else if (rule.EnemyOid == 0x31CD) candidates = DimCrystals;
            else candidates = CorruptedCrystals;
            var blockers = candidates
                .Where(a => !a.Actor.Dead).ToArray();
            if (sight.Origin is { } origin && blockers.Length != 0)
                sight.Safezones.Add(new(++serial, rule, actor, Center, 0, Activation(actor))
                {
                    ShapeOverride = new BozjaMeteorTemplate(new(Center, origin, blockers, IsIrminsul ? 70 : 60, false,
                        sourceIndexedDistance: IsIrminsul)),
                });
        }
        else if (rule.Kind == BozjaRuleKind.Earthbreaker
            && (spell.ActionId == rule.Actions[0] || IsCll3 && spell.ActionId == 20359))
            earthSequences.Add((new(++serial, rule, actor, spell.Location, 0, Activation(actor)), 0));
    }

    private void FinishCrystals(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.CrystalSight)
        {
            if (!crystalSights.TryGetValue(rule, out var sight)) return;
            sight.Casters.Remove(actor);
            RefreshCrystalSight(sight);
            if (sight.Safezones.Count > 0) sight.Safezones.RemoveAt(0);
        }
        else if (rule.Kind == BozjaRuleKind.Earthbreaker && earthSequences.Count > 0)
        {
            var stage = Array.IndexOf(rule.Actions, spell.ActionId);
            if (IsCll3 && stage >= 0) stage %= 3;
            if (stage < 0) return;
            ++NumEarthCasts;
            var index = earthSequences.FindIndex(s => s.Stage == stage && AlmostEqual(s.Cast.Origin, spell.Location));
            if (index < 0) return;
            var sequence = earthSequences[index];
            if (stage == 2) earthSequences.RemoveAt(index);
            else earthSequences[index] = (sequence.Cast with { Activation = Now.AddSeconds(IsCll4 ? 1.5d : 2d) }, stage + 1);
        }
        else if (rule.Kind is BozjaRuleKind.CrystalSpread or BozjaRuleKind.Spread)
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Target?.Actor.Id == spell.TargetId);
            if (index < 0) return;
            Casts.RemoveAt(index);
            ++NumFinishedSpreads;
        }
    }

    private void UpdateCrystals()
    {
        if (earthCount == earthSequences.Count && earthVersion == NumEarthCasts) return;
        earthCount = earthSequences.Count;
        earthVersion = NumEarthCasts;
        earthPublished.Clear();
        foreach (var (cast, stage) in earthSequences)
            earthPublished.Add(cast with { ShapeOverride = (IsArachne ? arachneEarthShapes : earthShapes)[stage] });
    }

    internal void Icon(ulong source, uint icon, ulong target)
    {
        if (Actors.GetValueOrDefault(source) is { } iconActor)
        {
            DeathgazeIcon(iconActor, icon);
            Cll1Icon(iconActor, icon, target);
            ArtIcon(iconActor, icon, target);
            OwainIcon(iconActor, icon);
            RaidenIcon(iconActor, icon);
            DahuIcon(iconActor, icon);
            RobotIcon(iconActor, icon);
            BurnIcon(iconActor, icon);
            DahuSavageIcon(iconActor, icon, target);
            SeekerIcon(iconActor, icon, target);
            GauntletIcon(iconActor, icon);
        }
        if (!Active || Primary == null || icon is < 79 or > 82
            || Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.Hailfire) is not { } rule
            || Actors.GetValueOrDefault(source) is not { } actor) return;
        Hailfires.Add(new(++serial, rule, Primary, actor, 0, Now.AddSeconds(8.2d + (icon - 79) * 2.1d)));
        Hailfires.Sort(static (a, b) => a.Activation.CompareTo(b.Activation));
    }

    private IEnumerable<BozjaAoe> CrystalAoes(BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.Earthbreaker)
            foreach (var cast in earthPublished) yield return new(cast, false, true);
        if (rule.Kind == BozjaRuleKind.CrystalSight && crystalSights.TryGetValue(rule, out var sight)
            && sight.Safezones.FirstOrDefault() is { } safe) yield return new(safe, false, true, IsIrminsul);
    }

    internal IEnumerable<BozjaCast> CrystalSpreads()
        => Casts.Where(c => c.Rule.Kind == BozjaRuleKind.CrystalSpread && c.Target is { Actor.Dead: false });

    internal IEnumerable<BozjaCast> Spreads(BozjaRule rule)
        => Casts.Where(c => c.Rule == rule && c.Target is { Actor.Dead: false });

    internal bool IsCrystalSpreadTarget(BozjaActor viewer)
        => Casts.Any(c => c.Rule.Kind == BozjaRuleKind.CrystalSpread && ReferenceEquals(c.Target, viewer));

    private IEnumerable<BozjaZone> CrystalSpreadNavigation(BozjaActor viewer, BozjaRule rule)
    {
        var spreads = Spreads(rule).ToArray();
        var radius = ((CircleTemplate)rule.Shape!).Radius + 1;
        var party = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
            .Select(p => Actors.GetValueOrDefault(p.EntityId))
            .OfType<BozjaActor>().Where(a => !a.Actor.Dead).ToArray();
        if (party.Length >= 2)
            foreach (var spread in spreads)
            {
                if (!ReferenceEquals(spread.Target, viewer))
                    yield return new($"spread/{spread.Generation}/avoid", rule.Component, spread.Activation,
                        new CircleTemplate(radius), DungeonEncounter.QuantizedDungeonPosition(spread.Target!.Actor.Position), 0);
                else
                    foreach (var member in party.Where(p => !spreads.Any(s => ReferenceEquals(s.Target, p))))
                        yield return new($"spread/{spread.Generation}/party/{member.BirthOrder}", rule.Component,
                            spread.Activation, new CircleTemplate(radius), DungeonEncounter.QuantizedDungeonPosition(member.Actor.Position), 0);
            }
        if (rule.Kind != BozjaRuleKind.CrystalSpread || !IsCrystalSpreadTarget(viewer)) yield break;
        var activation = Casts.First(c => c.Rule == rule).Activation;
        var crystals = Crystals.Select(c => c.Actor.Position).ToArray();
        if (crystals.Length != 0)
            yield return new("spread/crystals", rule.Component, activation,
                new CircleTemplate(6.6f), default, 0, Union: crystals);
    }

    private IEnumerable<BozjaHint> SpreadHints(BozjaActor viewer, BozjaRule rule)
    {
        var spreads = Spreads(rule).ToArray();
        if (spreads.FirstOrDefault(c => ReferenceEquals(c.Target, viewer)) is { } own)
            yield return new("Spread!", Party.Any(p => Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } actor
                && !ReferenceEquals(actor, viewer) && DistanceSquared(actor.Actor.Position,
                    DungeonEncounter.QuantizedDungeonPosition(own.Target!.Actor.Position))
                    <= MathF.Pow(((CircleTemplate)own.Shape).Radius, 2)));
        if (spreads.Any(c => !ReferenceEquals(c.Target, viewer) && DistanceSquared(viewer.Actor.Position,
            DungeonEncounter.QuantizedDungeonPosition(c.Target!.Actor.Position)) <= MathF.Pow(((CircleTemplate)c.Shape).Radius, 2)))
            yield return new("GTFO from spreads!");
    }

    private IEnumerable<BozjaZone> HailfireNavigation(BozjaActor viewer)
    {
        foreach (var bait in Hailfires.Where(b => !b.Source.Actor.Dead))
        {
            var cast = HornView(bait);
            if (!ReferenceEquals(bait.Target, viewer))
            {
                yield return new($"hailfire/{bait.Generation}/avoid", bait.Rule.Component, bait.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
                continue;
            }
            if (ReferenceEquals(bait.Source, bait.Target)) continue;
            foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot))
            {
                if (Actors.GetValueOrDefault(member.EntityId) is not { Actor.Dead: false } actor
                    || ReferenceEquals(actor, viewer)) continue;
                var delta = actor.Actor.Position - cast.Origin;
                var half = (float)Math.Asin(2f / MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z));
                yield return new($"hailfire/{bait.Generation}/party/{member.Slot}", bait.Rule.Component, bait.Activation,
                    new FanTemplate(100, 2 * half), cast.Origin, MathF.Atan2(delta.X, delta.Z));
            }
        }
    }
}

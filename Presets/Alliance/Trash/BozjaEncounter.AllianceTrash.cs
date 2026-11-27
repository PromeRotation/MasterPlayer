using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsAllianceStandard => AllianceStandardCatalog.Definitions.Concat(AllianceKnockbackCatalog.Definitions)
        .Any(d => d.Key == Definition.Key);
    internal bool IsLions => Definition.Key == "A10Lions";
    internal bool IsIrminsul => Definition.PrimaryOid == 0x13F1 && Definition.Territory == 508;
    internal bool IsAllianceTrash => IsDeathgaze || IsArachne || IsIrminsul || IsOzma || IsAllianceStandard || IsLightArtillery || IsHeavyArtillery || IsRhalgr || IsLions || Definition.Territory is 1248 or 1178 or 1304
        && AllianceTrashCatalog.Definitions.Any(d => d.Key == Definition.Key);
    internal bool UsesRawNpcClock => IsBaldesion || IsAllianceTrash;
    internal BozjaActor? AllianceLioness { get; private set; }
    internal BozjaDelubrumTimeline? LionsTimeline { get; private set; }
    private DateTime AllianceRaidwideActivation(BozjaActor actor)
    {
        if (actor.RawCast is not { } raw) return default;
        return Now.AddSeconds(BurnRawTotal(raw) + .3f - actor.RawElapsed);
    }
    internal int AlliancePhase { get; private set; }
    private IEnumerable<BozjaActor> AllianceEnemies(IEnumerable<uint> oids)
        => Actors.Values.Where(a => oids.Contains(a.Actor.Oid));
    private bool AllianceAllDead(IEnumerable<uint> oids)
        => AllianceEnemies(oids).All(a => a.Destroyed || a.Actor.Dead);
    private bool AlliancePullReady => IsHeavyArtillery
        ? Actors.Values.Any(a => a.Actor.Oid == 0x2E85 && a.Actor.Targetable || a.Actor.Oid == 0x2E83 && a.Actor.InCombat)
        : Definition.Key == "A10Despot"
        ? AllianceEnemies(AllianceTrashCatalog.Trash(Definition.Key)).Any(a => a.Actor.InCombat)
        : Primary is { Actor.Targetable: true, Actor.InCombat: true };
    private IEnumerable<BozjaRule> AllianceRules => Definition.Key == "A30Trash1"
        ? Definition.Rules.Where(r => (r.Component == "WaterIII") == (AlliancePhase == 0)) : Definition.Rules;

    private void UpdateAllianceTrash()
    {
        if (!IsAllianceTrash || !Active) return;
        if (IsRhalgr) { UpdateRhalgr(); return; }
        if (Definition.Key == "A30Trash1" && AlliancePhase == 0 && AllianceAllDead([0x4010]))
        {
            AlliancePhase = 1;
            Casts.Clear();
            interruptHints.Clear();
            // New phase components replay current casts in component order.
            foreach (var rule in AllianceRules)
                foreach (var actor in Actors.Values)
                    if (actor.Cast != null) Start(actor, rule);
        }
        var complete = Definition.Key switch
        {
            "A24Ozma" => OzmaComplete(),
            _ when IsIrminsul || IsArachne || IsDeathgaze => Primary is { } tree && PrimaryPhaseComplete(tree),
            "A30Trash" when IsAllianceStandard => AllianceEnemies([0x3193, 0x3194]).All(a => a.Destroyed),
            _ when IsAllianceStandard => Primary is { } standard && PrimaryPhaseComplete(standard),
            "A20LightArtilleryUnit" => LightSelfDestructCount == 3 || AllianceEnemies([0x2EE4]).All(a => a.Destroyed),
            "A20HeavyArtilleryUnit" => HeavyComplete(),
            "A10Lions" => Primary is { } lion && (lion.Destroyed || lion.Actor.Dead)
                && (AllianceLioness == null || AllianceLioness.Destroyed || AllianceLioness.Actor.Dead),
            "A20Kraken" => AllianceEnemies([0x4885, 0x4888, 0x488A, 0x4887, 0x4886, 0x4889]).All(a => a.Destroyed)
                && AllianceAllDead([0x488B, 0x488D, 0x488C]),
            "A30Trash1" => AlliancePhase == 1 && AllianceAllDead(AllianceTrashCatalog.Trash(Definition.Key))
                && !AllianceEnemies([0x4010]).Any(),
            "A30Trash2Pack1" => Primary is { } primary && (primary.Destroyed || primary.Actor.Dead)
                && AllianceAllDead([0x4014]),
            _ => AllianceAllDead(AllianceTrashCatalog.Trash(Definition.Key)),
        };
        if (!complete)
        {
            // UpdateModule runs only after the state machine's completion check.
            // Keep the first original Actor even if it is later destroyed.
            if (IsLions) AllianceLioness ??= Actors.Values.FirstOrDefault(a => !a.Destroyed && a.Actor.Oid == 0x38DB);
            return;
        }
        Active = false;
        Completed = true;
        LionsTimeline?.Stop();
        HeavyEnergy.Clear();
        Casts.Clear();
        Eyes.Clear();
    }

    private bool StartAllianceTrash(BozjaActor actor, BozjaRule rule)
    {
        if (IsLightArtillery && rule.Kind is BozjaRuleKind.Artillery or BozjaRuleKind.CastCounter) return true;
        if (IsHeavyArtillery && rule.Kind == BozjaRuleKind.Artillery) return true;
        if (IsLions && rule.Component is "SlashAndBurn" or "RoaringBlaze")
        {
            var spell = actor.Cast!;
            var shape = rule.Shape;
            if (rule.Component == "SlashAndBurn")
                shape = spell.ActionId is 29138 or 29142 or 29377 ? new DonutTemplate(6, 30) : new CircleTemplate(14);
            Casts.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor))
                { ShapeOverride = shape, Action = spell.ActionId });
            var sorted = Casts.Where(c => c.Rule == rule).ToArray();
            SortActivations(sorted, 0, sorted.Length - 1, c => c.Activation);
            var index = 0;
            for (var i = 0; i < Casts.Count; ++i)
                if (Casts[i].Rule == rule) Casts[i] = sorted[index++];
            return true;
        }
        if (IsAllianceTrash && rule.Component == "Flood")
        {
            if (actor.Cast?.ActionId == 43791)
            {
                Casts.RemoveAll(c => c.Rule == rule);
                Casts.Add(new(++serial, rule, actor, actor.Cast.Location, 0, Activation(actor, 3.5d)));
            }
            return true;
        }
        if (!IsAllianceTrash || rule.Component != "ScraplineTyphoon") return false;
        if (actor.Cast?.ActionId == 40650)
        {
            Casts.Add(new(++serial, rule, actor, actor.Cast.Location, 0, Activation(actor, 2.1d))
                { ShapeOverride = new CircleTemplate(10) });
            Casts.Add(new(++serial, rule, actor, actor.Cast.Location, 0, Activation(actor, 5.6d))
                { ShapeOverride = new DonutTemplate(8, 40) });
        }
        return true;
    }

    private bool FinishAllianceTrash(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (IsLightArtillery && rule.Kind is BozjaRuleKind.Artillery or BozjaRuleKind.CastCounter) return true;
        if (IsHeavyArtillery && rule.Kind == BozjaRuleKind.Artillery) return true;
        if (IsLions && rule.Component is "SlashAndBurn" or "RoaringBlaze")
        {
            if (rule.Actions.Contains(spell.ActionId))
            {
                var head = Casts.FindIndex(c => c.Rule == rule
                    && (rule.Component == "SlashAndBurn" || c.Source.Actor.Id == actor.Actor.Id));
                if (head >= 0) Casts.RemoveAt(head);
            }
            return true;
        }
        if (IsAllianceTrash && rule.Component == "Flood")
        {
            if (spell.ActionId == 43792) Casts.RemoveAll(c => c.Rule == rule);
            return true;
        }
        if (!IsAllianceTrash || rule.Component != "ScraplineTyphoon") return false;
        if (spell.ActionId is 41393 or 41902)
        {
            var head = Casts.FindIndex(c => c.Rule == rule);
            if (head >= 0) Casts.RemoveAt(head);
        }
        return true;
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> AlliancePullMovements(BozjaActor viewer)
    {
        if (!IsAllianceTrash || !Active) yield break;
        var from = viewer.Actor.Position with { Y = 0 };
        foreach (var cast in Casts.Where(c => c.Rule.Kind == BozjaRuleKind.AlliancePull))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var delta = cast.Origin - from;
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            if (!(length > 0)) continue;
            var inverse = 1f / length;
            var distance = cast.Rule.Component == "Sucker" ? 15 : 10;
            var to = from + Math.Min(distance, length) * new Vector3(delta.X * inverse, 0, delta.Z * inverse);
            yield return (cast, from, to);
            from = to;
        }
    }

    private bool AlliancePullUnsafe(Vector3 point)
        => Casts.FirstOrDefault(c => c.Rule.Component == (Definition.Key == "A20Kraken" ? "Flood" : "ScraplineTyphoon"))
            ?.Contains(point) == true;
}

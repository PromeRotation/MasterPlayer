using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsSavageDahu => Definition.Key == "DRS3Dahu";
    internal BozjaDelubrumTimeline? DahuTimeline { get; private set; }
    private readonly List<BozjaRule> dahuActiveRules = [];
    private readonly Dictionary<string, int> dahuCounts = [];
    private readonly BozjaActor?[] dahuSpitTargets = new BozjaActor?[4];
    private List<BozjaActor>? dahuAdds;
    private List<BozjaActor>? dahuCrowned;
    internal List<BozjaCast> DahuBurns { get; } = [];
    internal IReadOnlyList<BozjaActor> DahuCrowned
        => dahuCrowned ??= Actors.Values.Where(a => a.Actor.Oid == 0x30AA).ToList();
    internal bool DahuHas(string component) => dahuActiveRules.Any(r => r.Component == component);
    internal int DahuCasters(string component) => Casts.Count(c => c.Rule.Component == component);
    internal int DahuCount(string component) => dahuCounts.GetValueOrDefault(component);

    internal void ActivateDahu(string name)
    {
        if (DahuHas(name)) return;
        var rule = Definition.Rules.Single(r => r.Component == name);
        dahuActiveRules.Add(rule);
        dahuCounts[name] = 0;
        if (name == "SpitFlame")
        {
            Array.Clear(dahuSpitTargets);
            dahuAdds ??= Actors.Values.Where(a => a.Actor.Oid == 0x30A9).ToList();
        }
        foreach (var actor in Actors.Values)
            if (actor.Cast != null) Start(actor, rule);
    }

    internal void DeactivateDahu(string name)
    {
        var rule = Definition.Rules.Single(r => r.Component == name);
        dahuActiveRules.Remove(rule);
        dahuCounts.Remove(name);
        Casts.RemoveAll(c => c.Rule == rule);
        if (rule.Kind == BozjaRuleKind.DahuShockwave) dahuShockwaves.Clear();
        if (rule.Kind == BozjaRuleKind.DahuRotation)
        {
            dahuSequences.Clear();
            dahuRotatingAoes.Clear();
            dahuRotation = dahuIncrement = 0;
            dahuActivation = default;
            dahuRotationVersion = dahuRotationCount = NumDahuRotations = 0;
        }
        if (rule.Kind == BozjaRuleKind.DahuBurn) DahuBurns.Clear();
        if (rule.Kind == BozjaRuleKind.DahuSpit) Array.Clear(dahuSpitTargets);
    }

    internal void ClearDahuComponents()
    {
        dahuActiveRules.Clear();
        dahuCounts.Clear();
        Array.Clear(dahuSpitTargets);
        DahuBurns.Clear();
        Casts.Clear();
        ClearDahu();
    }

    private void ClearSavageDahu()
    {
        DahuTimeline?.Stop();
        DahuTimeline = null;
        dahuActiveRules.Clear();
        dahuCounts.Clear();
        Array.Clear(dahuSpitTargets);
        DahuBurns.Clear();
        dahuAdds = dahuCrowned = null;
    }

    private void DahuSavageCreated(BozjaActor actor)
    {
        if (actor.Actor.Oid == 0x30A9) dahuAdds?.Add(actor);
        if (actor.Actor.Oid == 0x30AA) dahuCrowned?.Add(actor);
    }

    private void StartSavageDahuKnockback(BozjaActor actor, BozjaRule rule)
    {
        if (actor.Cast?.ActionId != rule.Actions[0]) return;
        Casts.RemoveAll(c => c.Rule == rule);
        Casts.Add(new(++serial, rule, actor, actor.Actor.Position, 0,
            Activation(actor, rule.Component == "FeralHowl" ? 2.1d : .9d)));
    }

    private void DahuSavageIcon(BozjaActor actor, uint icon, ulong target)
    {
        if (!Active || !IsSavageDahu || Primary == null) return;
        if (icon is >= 79 and <= 82 && ActiveRules.FirstOrDefault(r => r.Kind == BozjaRuleKind.DahuSpit) is { } spit)
        {
            Casts.Add(new(++serial, spit, actor, default, 0, default) { Target = actor });
            dahuSpitTargets[icon - 79] = actor;
        }
        if (icon == 87 && ActiveRules.FirstOrDefault(r => r.Kind == BozjaRuleKind.DahuBurn) is { } burn)
            DahuBurns.Add(new(++serial, burn, Primary, default, 0, Now.AddSeconds(8.2f))
                { Target = Actors.GetValueOrDefault(target) ?? actor });
    }

    private void DahuSavageEffect(uint action, ulong target)
    {
        if (!IsSavageDahu) return;
        foreach (var rule in dahuActiveRules)
        {
            var watched = rule.Kind == BozjaRuleKind.DahuSavageKnockback
                ? rule.Actions[1] : rule.Actions.FirstOrDefault();
            if (action != watched) continue;
            // SimpleAOEs/CastCounter count actual Effects, independently of
            // normal Finish removing their caster lists.
            if (rule.Kind != BozjaRuleKind.DahuSpit) ++dahuCounts[rule.Component];
            if (rule.Kind == BozjaRuleKind.DahuSpit)
                Casts.RemoveAll(c => c.Rule == rule && c.Target?.Actor.Id == target);
            if (rule.Kind == BozjaRuleKind.DahuBurn && DahuBurns.Count > 0) DahuBurns.RemoveAt(0);
        }
    }

    private void UpdateSavageDahu()
    {
        if (!IsSavageDahu) return;
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.DahuSpit && c.Target!.Actor.Dead);
        DahuBurns.RemoveAll(c => c.Target!.Actor.Dead);
    }

    internal static BozjaCast DahuBurnView(BozjaCast bait)
    {
        var delta = bait.Target!.Actor.Position - bait.Source.Actor.Position;
        var rotation = ReferenceEquals(bait.Source, bait.Target) ? bait.Source.Actor.Rotation : MathF.Atan2(delta.X, delta.Z);
        return bait with { Origin = bait.Target.Actor.Position, Rotation = rotation };
    }

    private IEnumerable<BozjaHint> SavageDahuHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DahuSpit)
        {
            foreach (var hint in SpreadHints(viewer, rule)) yield return hint;
            var order = Array.IndexOf(dahuSpitTargets, viewer);
            if (order >= 0)
            {
                yield return new($"Order: {order + 1}", false);
                if (dahuAdds?.Any(a => DistanceSquared(a.Actor.Position, viewer.Actor.Position) <= 16) != true)
                    yield return new("Hit at least one add!");
            }
        }
        if (rule.Kind != BozjaRuleKind.DahuBurn) yield break;
        var baits = DahuBurns.Where(b => !b.Source.Actor.Dead).ToArray();
        if (baits.Any(b => b.Target!.Actor.Id == viewer.Actor.Id && Party.Any(p => p.EntityId != b.Target.Actor.Id
            && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } actor && DahuBurnView(b).Contains(actor.Actor.Position))))
            yield return new("Bait away from raid!");
        if (baits.Any(b => b.Target!.Actor.Id != viewer.Actor.Id && DahuBurnView(b).Contains(viewer.Actor.Position)))
            yield return new("GTFO from baited aoe!");
    }

    private IEnumerable<BozjaZone> SavageDahuNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.DahuSpit)
            foreach (var zone in CrystalSpreadNavigation(viewer, rule)) yield return zone;
        if (rule.Kind != BozjaRuleKind.DahuBurn) yield break;
        foreach (var bait in DahuBurns.Where(b => !b.Source.Actor.Dead))
        {
            if (!ReferenceEquals(bait.Target, viewer))
            {
                var cast = DahuBurnView(bait);
                yield return new($"dahu/burn/{bait.Generation}", rule.Component, bait.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
            }
            else if (!ReferenceEquals(bait.Source, bait.Target))
                foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot))
                {
                    if (Actors.GetValueOrDefault(member.EntityId) is not { Actor.Dead: false } actor
                        || ReferenceEquals(actor, viewer)) continue;
                    yield return new($"dahu/burn/{bait.Generation}/{member.Slot}", rule.Component, bait.Activation,
                        bait.Shape, actor.Actor.Position, 0);
                }
        }
    }

    private IEnumerable<BozjaDamage> SavageDahuBurnDamage()
    {
        ulong mask = 0;
        foreach (var bait in DahuBurns.Where(b => !b.Source.Actor.Dead))
        {
            var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
                .FirstOrDefault(p => p.EntityId == bait.Target!.Actor.Id)?.Slot;
            if (slot is { } value) mask |= 1UL << value;
        }
        if (mask != 0) yield return new(mask, DahuBurns[0].Activation, "Raidwide");
    }
}

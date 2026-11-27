using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsSavagePhantom => Definition.Key == "DRS5Phantom";
    internal BozjaDelubrumTimeline? PhantomTimeline { get; private set; }
    private readonly HashSet<string> phantomComponents = [];
    private readonly Dictionary<BozjaRule, int> phantomFinished = [];
    private readonly Dictionary<long, ulong> phantomCreepingHits = [];
    private readonly Dictionary<BozjaRule, int> phantomVersions = [];
    private readonly Dictionary<BozjaRule, (int Count, int Version, List<BozjaAoe>? Aoes)> phantomPublished = [];
    private List<BozjaActor>? bloodyWraiths;
    private List<BozjaActor>? mistyWraiths;
    internal int PhantomAgonyCount { get; private set; }
    internal int PhantomMiasmaCount => phantomFinished.Values.Sum();
    internal IEnumerable<BozjaActor> PhantomActiveBloody
        => (bloodyWraiths ?? []).Where(a => a.Actor.Targetable && !a.Actor.Dead);
    internal IReadOnlySet<string> PhantomComponents => phantomComponents;
    internal IReadOnlyList<BozjaActor> PhantomBloodyActors => bloodyWraiths ?? [];
    internal IReadOnlyList<BozjaActor> PhantomMistyActors => mistyWraiths ?? [];
    internal IEnumerable<BozjaRule> ActiveRules
    {
        get
        {
            if (HasConstructionDrawing) return [BozjaPyromancerCatalog.ThermalShock];
            if (IsGauntlet) return gauntletRules;
            if (IsSaunion) return saunionRules;
            if (IsSavageDahu) return dahuActiveRules;
            if (IsSavagePhantom) return Definition.Rules.Where(r => phantomComponents.Contains(r.Component));
            if (IsOwain) return Definition.Rules.Where(r => r.Component != "LegendaryImbas" || OwainImbasActive);
            if (IsRaiden) return raidenRules;
            if (IsVirtue) return virtueRules;
            if (IsRhalgr) return rhalgrRules;
            if (IsAllianceTrash) return AllianceRules;
            return Definition.Rules;
        }
    }

    internal void ActivatePhantom(string name)
    {
        if (!phantomComponents.Add(name)) return;
        if (name == "MaledictionOfAgony") PhantomAgonyCount = 0;
        if (name == "BloodyWraith")
            bloodyWraiths ??= Actors.Values.Where(a => a.Actor.Oid == 0x30B0).ToList();
        if (name == "MistyWraith")
            mistyWraiths ??= Actors.Values.Where(a => a.Actor.Oid == 0x30B1).ToList();
        var rule = Definition.Rules.Single(r => r.Component == name);
        phantomFinished[rule] = 0;
        phantomVersions[rule] = 0;
        // ActivateComponent replays current actors/casts, but no historical
        // EAnim or Icons. Component order is fixed by the native state chain.
        foreach (var actor in Actors.Values)
            if (actor.Cast != null) Start(actor, rule);
    }

    internal void DeactivatePhantom(string name)
    {
        phantomComponents.Remove(name);
        var rule = Definition.Rules.Single(r => r.Component == name);
        foreach (var cast in Casts.Where(c => c.Rule == rule))
            phantomCreepingHits.Remove(cast.Generation);
        Casts.RemoveAll(c => c.Rule == rule);
        Exaflares.RemoveAll(l => l.Cast.Rule == rule);
        exaflarePublished.RemoveAll(a => a.Cast.Rule == rule);
        phantomFinished.Remove(rule);
        phantomVersions.Remove(rule);
        phantomPublished.Remove(rule);
    }

    internal void ClearPhantomComponents()
    {
        phantomComponents.Clear();
        phantomFinished.Clear();
        phantomCreepingHits.Clear();
        phantomVersions.Clear();
        phantomPublished.Clear();
        Casts.Clear();
        ClearHashmal();
    }

    private void ClearSavagePhantom()
    {
        PhantomTimeline?.Stop();
        PhantomTimeline = null;
        phantomComponents.Clear();
        phantomFinished.Clear();
        phantomCreepingHits.Clear();
        phantomVersions.Clear();
        phantomPublished.Clear();
        bloodyWraiths = mistyWraiths = null;
        PhantomAgonyCount = 0;
    }

    private void PhantomSavageCreated(BozjaActor actor)
    {
        if (actor.Actor.Oid == 0x30B0) bloodyWraiths?.Add(actor);
        if (actor.Actor.Oid == 0x30B1) mistyWraiths?.Add(actor);
    }

    private void StartPhantomCreeping(BozjaActor actor, BozjaRule rule)
    {
        if (actor.Cast is not { ActionId: 22452 } spell) return;
        var cast = new BozjaCast(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor));
        Casts.Add(cast);
        phantomCreepingHits.Add(cast.Generation, 0);
    }

    private void FinishPhantomCreeping(BozjaRule rule, ReplayCast spell)
    {
        var index = Casts.FindIndex(c => c.Rule == rule
            && c.Origin.X == spell.Location.X && c.Origin.Z == spell.Location.Z);
        if (index < 0) return;
        var cast = Casts[index];
        if (++phantomCreepingHits[cast.Generation] != 3) return;
        phantomCreepingHits.Remove(cast.Generation);
        Casts.RemoveAt(index);
        phantomFinished[rule] = phantomFinished.GetValueOrDefault(rule) + 1;
    }

    private void PhantomSavageEffect(uint action)
    {
        if (IsSavagePhantom && action == 23350 && phantomComponents.Contains("MaledictionOfAgony"))
            ++PhantomAgonyCount;
    }

    private void UpdateSavagePhantomExaflares()
    {
        foreach (var rule in ActiveRules.Where(r => r.Kind == BozjaRuleKind.PhantomExaflare))
        {
            var lines = Exaflares.Where(l => l.Cast.Rule == rule).ToArray();
            var cache = phantomPublished.GetValueOrDefault(rule);
            var version = phantomVersions.GetValueOrDefault(rule);
            if (cache.Aoes != null && cache.Count == lines.Length && cache.Version == version) continue;
            var output = new List<BozjaAoe>();
            PublishExaflares(lines, output);
            phantomPublished[rule] = (lines.Length, version, output);
        }
    }
}

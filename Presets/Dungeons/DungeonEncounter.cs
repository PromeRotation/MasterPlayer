using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonCastObservation(uint Action, Vector3 Location, float? Rotation, DateTime StartedAt, DateTime FinishAt, ulong TargetId = 0, Vector3? SourcePosition = null, float? TotalTime = null, bool? IsSpell = null);
internal sealed record DungeonActor(ulong Id, uint Oid, Vector3 Position, bool Dead = false, bool Targetable = true, bool InCombat = false,
    DungeonCastObservation? Cast = null, ulong TargetId = 0, string Name = "", int PartySlot = -1, string PartyRole = "",
    uint[]? Statuses = null, bool Hostile = true, int? RenderFlags = null,
    IReadOnlyDictionary<uint, ushort>? StatusParams = null, float HitboxRadius = .5f, float Rotation = 0,
    byte EventState = 0, IReadOnlyDictionary<uint, ulong>? Tethers = null, IReadOnlyDictionary<uint, DateTime>? StatusExpirations = null,
    byte AnimationState1 = 0, byte ModelState = 0, uint ClassJob = 0, uint? EventAnimation = null, uint NameId = 0,
    byte AnimationState2 = 0, bool Buddy = false, IReadOnlyList<MasterPlayer.Replay.ReplayIncomingEffect>? IncomingEffects = null,
    IReadOnlyDictionary<uint, ulong>? StatusSources = null, uint? Hp = null, uint? MaxHp = null, bool AggroPlayer = false,
    bool FriendlyNpc = false, bool? IsEnemy = null, ushort? ObjectIndex = null, bool Player = false, uint? FateId = null, IReadOnlyList<MasterPlayer.Replay.ReplayStatus>? StatusSlots = null, ushort NativeType = 0,
    MasterPlayer.Replay.ReplayTether? FirstTether = null, ulong GameObjectId = 0);
internal sealed record DungeonPendingCast(ulong Source, DungeonCastRule Rule, Vector3 Location, float Rotation, DateTime StartedAt, DateTime FinishAt, ulong TargetId = 0)
{
    public bool EffectSeen { get; set; }
    public bool UntilObservedEnd { get; init; }
    public bool Observed { get; set; }
    public Vector3? SourcePosition { get; init; }
    public DateTime FinishEventBoundary { get; init; }
    public DateTime? InputFinishAt { get; init; }
}

internal sealed record DungeonDamagePrediction(ulong TargetId, int PartySlot, string Role, string Name, DateTime Activation, string DamageType);

internal sealed partial class DungeonEncounter(uint territory, Action<string>? warning = null)
{
    public DungeonDefinition[] Definitions { get; } = ArrDungeonCatalog.Create(territory);
    public DungeonDefinition? Active { get; private set; }
    public ulong PrimaryId { get; private set; }
    public IGeometryShape? Arena { get; private set; }
    public IReadOnlyDictionary<ulong, DungeonPendingCast> Casts => _casts;
    public IReadOnlyDictionary<ulong, DungeonActor> Actors => _actors;
    internal uint? AllowedFateId { get; set; }
    internal bool PreserveFightAcrossPlayerDeath => Active != null && (IsHades || IsInnocence || IsWarriorOfLight || IsHydaelyn || IsAsura || IsZelenia || IsValigarmanda || IsArkveld || IsEnuo || IsNecron || IsQueenEternal || IsZoraalJa || IsDoomtrain || IsCoilNodes || IsCoilGauntlet || IsCaduceus || IsTwintania || IsLabyrinth || IsSyrcus || IsDarkness || IsArrQuest || IsUltimaExtreme || IsGarudaExtreme || IsTitanExtreme || IsIfritExtreme || IsNativeQuestBoss || IsCarnivale || IsNativeTreasure || IsHeavenOnHigh || IsEurekaOrthos || IsQuantum || IsKefka || IsThordan || IsWickedThunder || IsHalicarnassus || IsAlteRoite);
    public ulong[] ForbiddenTargets => _actors.Values.Where(a => a.Hostile && !a.Dead && a.Targetable
        && (Active?.BossOid == 0x4645 && HasStatus(a, HalataliCatalog.BURNING_WARD)
            || Active?.BossOid == 0x4A8D && a.Id == PrimaryId && HasStatus(a, DzemaelCatalog.INVINCIBILITY)
            || Active?.BossOid == 0x8F4 && a.Oid is 0x8F7 or 0x8F8
            || AeryBossForbidden && a.Id == PrimaryId || GubalBossForbidden(a)
            || Active?.BossOid == 0x1432 && HasStatus(a, 152) || AntParryForbidden(a)
            || ArchonParryForbidden(a)
            || Active?.BossOid == 0x15FB && HasStatus(a, 325)
            || Active?.BossOid == 0x179B && HasStatus(a, 775)
            || Active?.BossOid == 0x1AA5 || Active?.BossOid == 0x1FAC && a.Id == PrimaryId && HasStatus(a, 325)
            || HolminsterBossForbidden(a) || DohnForbidden(a) || MesoForbidden(a) || PortaForbidden(a) || HadesForbidden(a)))
        .Select(a => a.Id).Concat(NoviceForbiddenTargets).Concat(GodmotherForbiddenTargets).Distinct().ToArray();
    public IEnumerable<KeyValuePair<string, DungeonPendingCast>> OutputCasts
    {
        get
        {
            if (IsQuantum || IsKefka || IsThordan || IsWickedThunder || IsHalicarnassus || IsAlteRoite) yield break;
            var fireflows = _casts.Values.Where(c => c.Rule.Action is 40588 or 40589)
                .OrderBy(c => c.FinishAt).ThenBy(c => c.Source).Take(4).Select(c => c.Source).ToHashSet();
            var bursts = _casts.Values.Where(c => c.Rule.Action == 28658).Take(4).Select(c => c.Source).ToHashSet();
            var voidMatters = _casts.Values.Where(c => c.Rule.Action == 45568).Take(6).Select(c => c.Source).ToHashSet();
            var infiniteReach = _casts.Values.Where(c => c.Rule.Action == 28794).Take(6).Select(c => c.Source).ToHashSet();
            foreach (var cast in _casts.Values)
            {
                if (IsEurekaOrthos && cast.Rule.Action == 32700) continue;
                if (territory == EurekaOrthosCatalog.TIAMAT && cast.Rule.Action is 31844 or 31846 or 31850) continue;
                if (IsDoomtrain || !BarPassageCastVisible(cast) || !CarnivaleCastVisible(cast)) continue;
                if (IsTreasureNarkissos && cast.Rule.Action == 32336) continue;
                if (IsNativeTreasure && (cast.Rule.Action is 32294 or 38271 || NativeTreasureCatalog.VaultGroupAction(cast.Rule.Action))) continue;
                if (Active?.BossOid == NativeTreasureCatalog.MOBILE_FORTRESS
                    && cast.Rule.Action is 43732 or >= 43734 and <= 43741) continue;
                if (IsPortrait && cast.Rule.Action is 37535 or 37529) continue;
                if (IsBekko && cast.Rule.Action == 8446) continue;
                if (IsTitanExtreme) continue;
                if (IsIfritExtreme && !IfritExCastAllowed(cast.Rule.Action)) continue;
                if (cast.Rule.PartyAoe != null) continue;
                if (cast.Rule.Action == 942) continue;
                if (cast.Rule.Action is 40599 or 40600 or 40601) continue;
                if (cast.Rule.Action is 29586 or 29591 or 29601) continue;
                if (cast.Rule.Action is 3389 or 29286 or 29287 or 29288) continue;
                if (territory == AnyderCatalog.TERRITORY && cast.Rule.Action == 15888) continue;
                if (territory == HeroesCatalog.TERRITORY && cast.Rule.Action == 20388) continue;
                if (territory == MatoyaCatalog.TERRITORY && cast.Rule.Action is 21646 or 21647 or 21648 or 22814 or 22815 or 20045) continue;
                if (territory == PaglthanCatalog.TERRITORY && cast.Rule.Action == 24773) continue;
                if (territory == BabilCatalog.TERRITORY && cast.Rule.Action == 25357) continue;
                if (territory == KtisisCatalog.TERRITORY && cast.Rule.Action is 25375 or 25177 or 25734 or 25735 or 25736 or 27836 or 27837) continue;
                if (territory == AitiascopeCatalog.TERRITORY && cast.Rule.Action is 25673 or 25670 or 25671 or 25680 or 25684 or 25691 or 25692) continue;
                if (territory == DeadEndsCatalog.TERRITORY && cast.Rule.Action is 25919 or 28348 or 28347 or 28360 or 25935 or 25939) continue;
                if (territory == AlzadaalCatalog.TERRITORY && cast.Rule.Action is 29556 or 29467 or 28519) continue;
                if (cast.Rule.Action == 4999) continue;
                if (cast.Rule.Action == 5230) continue;
                if (territory == BardamCatalog.TERRITORY && cast.Rule.Action is >= 9591 and <= 9596) continue;
                if (territory == DomaCatalog.TERRITORY && cast.Rule.Action == 8360) continue;
                if (cast.Rule.Action == 7372 && !BaelCorrosionVisible) continue;
                if (cast.Rule.Action is 40588 or 40589 && !fireflows.Contains(cast.Source)) continue;
                if (cast.Rule.Action == 28658 && !bursts.Contains(cast.Source)) continue;
                if (cast.Rule.Action == 45568 && !voidMatters.Contains(cast.Source)) continue;
                if (cast.Rule.Action == 28794 && !infiniteReach.Contains(cast.Source)) continue;
                if (IsMainScenarioQuest && cast.Rule.Action is 24054 or 24046 or 18035 or 16290 or 16286 or 30021 or 34888 or 38200 or 38197 or 38205 or 38209) continue;
                if (IsIseultalon && cast.Rule.Action == 14603) continue;
                if (!CastrumCastVisible(cast)) continue;
                if (!TroiaCastVisible(cast)) continue;
                if (!IhuyCastVisible(cast)) continue;
                if (!WorqorCastVisible(cast)) continue;
                if (!SkydeepCastVisible(cast)) continue;
                if (territory is VanguardCatalog.TERRITORY or OrigenicsCatalog.TERRITORY or AlexandriaCatalog.TERRITORY or TenderValleyCatalog.TERRITORY or StrayboroughCatalog.TERRITORY or YuweyawataCatalog.TERRITORY or UnderkeepCatalog.TERRITORY or MesoCatalog.TERRITORY or MistwakeCatalog.TERRITORY or ClyteumCatalog.TERRITORY) continue;
                if (territory == LapisCatalog.TERRITORY && cast.Rule.Action is 32622 or 32624 or 31377 or 31379) continue;
                if (territory == AetherfontCatalog.TERRITORY && cast.Rule.Action is 33339 or 33340) continue;
                if (territory == MalikahCatalog.TERRITORY && MalikahCatalog.IsStreak(cast.Rule.Action)) continue;
                if (IsShinryu && cast.Rule.Action == 8097) continue;
                if (IsWarriorOfLight && cast.Rule.Action == 20259) continue;
                if (IsAsura && cast.Rule.Action is 36020 or 36021) continue;
                if (IsZelenia && cast.Rule.Action is 43126 or 43083) continue;
                if (IsEnuo && cast.Rule.Action is 49952 or 49953) continue;
                if (IsNecron && cast.Rule.Action is 44523 or 44532 or 44611 or 44534) continue;
                if (IsQueenEternal && cast.Rule.Action == 36647) continue;
                if (IsZoraalJa && cast.Rule.Action is 37714 or 38184 or 37712 or 37720 or 37738) continue;
                yield return new($"cast/{cast.Source}", cast);
            }
            if (IsEurekaOrthos)
                foreach (var source in eurekaMandraOrder.Take(6))
                    if (_casts.TryGetValue(source, out var mine))
                        yield return new($"cast/{source}", mine);
            if (IsMainScenarioQuest)
                foreach (var source in mainScenarioCastOrder)
                    if (_casts.TryGetValue(source, out var ordered) && ordered.Rule.Action is 24054 or 24046 or 18035 or 16290 or 30021 or 34888 or 38200
                        && MainScenarioCastVisible(ordered))
                        yield return new($"cast/{source}", ordered);
            if (IsPortrait)
                foreach (var source in portraitIvyOrder.Take(9))
                    if (_casts.TryGetValue(source, out var ivy))
                        yield return new($"cast/{source}", ivy);
            if (IsBekko)
                foreach (var source in bekkoYukikaze.Take(8))
                    if (_casts.TryGetValue(source, out var yukikaze))
                        yield return new($"cast/{source}", yukikaze);
            foreach (var sequence in sequences)
                yield return new($"concentric/{sequence.Source}", sequence.Current);
            for (var i = 0; i < sandPillars.Count; ++i)
                yield return new($"sand/{i}", sandPillars[i]);
            foreach (var line in typhoons)
                for (var i = 0; i < line.Predictions.Length; ++i)
                    if (line.Predictions[i].FinishAt.AddSeconds(2) > _now)
                        yield return new($"typhoon/{line.Source}/{i}", line.Predictions[i]);
            if (touchdown != null) yield return new("touchdown", touchdown);
            foreach (var cast in terminusEst.Values) yield return new($"terminus/{cast.Source}", cast);
            if (goobbuesGrief != null) yield return new("goobbue/grief", goobbuesGrief);
            foreach (var cast in frozenPillars.Values) yield return new($"frozenpillar/{cast.Source}", cast);
            foreach (var cast in mirageRays.Values) yield return new($"mirage/{cast.Source}", cast);
            for (var i = 0; i < shiningBlades.Count; ++i) yield return new($"vault/blade/{i}", shiningBlades[i]);
            foreach (var cast in brightSpheres) yield return new($"vault/sphere/{cast.Source}", cast);
            if (disclosureSpin != null) yield return new("gubal/spin", disclosureSpin);
            foreach (var cast in voidSpheres) yield return new($"gubal/sphere/{cast.Source}", cast);
            foreach (var cast in ResearchStarOutputs()) yield return cast;
            foreach (var cast in ArboretumCasts()) yield return cast;
            for (var i = 0; i < malboroCasts.Count; ++i) yield return new($"malboro/{i}", malboroCasts[i]);
        }
    }
    public IEnumerable<DungeonActor> ActiveRipples => _ripples.Where(r => (r.Value & 0x40000) != 0)
        .Select(r => _actors.GetValueOrDefault(r.Key)).OfType<DungeonActor>().Where(a => !a.Dead);
    public string CoralColor { get; private set; } = "Unknown";
    public IEnumerable<DungeonDamagePrediction> PredictedDamage
    {
        get
        {
            if (IsAlteRoite)
            {
                foreach (var prediction in AlteRoite.Damage()) yield return prediction;
                yield break;
            }
            if (IsThordan)
            {
                foreach (var prediction in Thordan.Damage()) yield return prediction;
                yield break;
            }
            if (IsWickedThunder)
            {
                foreach (var prediction in WickedThunder.Damage()) yield return prediction;
                yield break;
            }
            if (IsKefka)
            {
                foreach (var prediction in Kefka.Damage()) yield return prediction;
                yield break;
            }
            if (IsQuantum)
            {
                foreach (var prediction in Quantum.Damage()) yield return prediction;
                yield break;
            }
            foreach (var cast in _casts.Values.Where(c => c.Rule.Hint is "死刑" or "可打断死刑" or "单体伤害")
                .Where(c => !IsDaivadipa || c.Rule.Action != 26510)
                .Where(c => !IsNativeTreasure || !NativeTreasureCatalog.SingleTargetAction(c.Rule.Action))
                .Where(c => c.Rule.Action is not (45742 or 45997 or 29620 or 30205 or 31837)
                    && !(territory == AitiascopeCatalog.TERRITORY && c.Rule.Action is 25673 or 25686)
                    && !(territory == DeadEndsCatalog.TERRITORY && c.Rule.Action is 25920 or 25949 or 25935)
                    && !(territory == AlzadaalCatalog.TERRITORY && c.Rule.Action == 28530)
                    && !(territory == VanguardCatalog.TERRITORY && c.Rule.Action == 36595)
                    && !(territory == OrigenicsCatalog.TERRITORY && c.Rule.Action == 36518)
                    && !(territory == UnderkeepCatalog.TERRITORY && c.Rule.Action == 42548)
                    && !(territory == MesoCatalog.TERRITORY && c.Rule.Action == 43589)
                    && !(territory == MtGulgCatalog.TERRITORY && c.Rule.Action == 15611)
                    && !(territory == AnyderCatalog.TERRITORY && c.Rule.Action == 15876)
                    && !(territory == CosmosCatalog.TERRITORY && c.Rule.Action == 18276)
                    && !(territory == AnamnesisCatalog.TERRITORY && c.Rule.Action is 19305 or 19314 or 19340)
                    && !(territory == HeroesCatalog.TERRITORY && c.Rule.Action == 20427)
                    && !(territory == QitanaCatalog.TERRITORY && c.Rule.Action is 15497 or 15505 or 15513)).Concat(delayedDamage.Values))
            {
                var target = cast.TargetId == cast.Source ? _actors.GetValueOrDefault(cast.Source)?.TargetId ?? 0 : cast.TargetId;
                if (_actors.TryGetValue(target, out var actor) && !actor.Dead && actor.PartySlot >= 0)
                    yield return new(target, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Tankbuster");
            }
            foreach (var prediction in RaidwideDamage) yield return prediction;
            foreach (var prediction in FateDamage()) yield return prediction;
            foreach (var prediction in TacticalDamage()) yield return prediction;
            foreach (var prediction in KenkoDamage()) yield return prediction;
            foreach (var prediction in PalacePlantDamage()) yield return prediction;
            foreach (var prediction in NybethDamage()) yield return prediction;
            foreach (var prediction in GodmotherDamage()) yield return prediction;
            foreach (var prediction in BlackRiderDamage()) yield return prediction;
            foreach (var prediction in EurekaChimeraDamage()) yield return prediction;
            foreach (var prediction in EurekaAeturnaDamage()) yield return prediction;
            foreach (var prediction in EurekaKaliyaDamage()) yield return prediction;
            foreach (var prediction in EurekaAdministratorDamage()) yield return prediction;
            foreach (var prediction in TreasureDamage()) yield return prediction;
            foreach (var prediction in FallenDamage()) yield return prediction;
            foreach (var prediction in SageTowerDamage()) yield return prediction;
            foreach (var prediction in GuildivainDamage()) yield return prediction;
            foreach (var prediction in SleepBeamDamage()) yield return prediction;
            foreach (var prediction in SlickshellDamage()) yield return prediction;
            foreach (var prediction in FoxshotDamage()) yield return prediction;
            foreach (var prediction in HeroesPretendersDamage()) yield return prediction;
            foreach (var prediction in IseultalonDamage()) yield return prediction;
            if (IsDikaiosyne && Active != null)
                foreach (var prediction in QuestTowerDamage(fractureTowers)) yield return prediction;
            foreach (var prediction in MainScenarioDamage()) yield return prediction;
            foreach (var prediction in CarnivaleDamage()) yield return prediction;
            foreach (var prediction in TitanExDamage()) yield return prediction;
            foreach (var prediction in IfritExDamage()) yield return prediction;
            foreach (var prediction in ThornDamage()) yield return prediction;
            foreach (var prediction in SusanoDamage()) yield return prediction;
            foreach (var prediction in ShinryuDamage()) yield return prediction;
            foreach (var prediction in TsukuyomiDamage()) yield return prediction;
            foreach (var prediction in YojimboDamage()) yield return prediction;
            foreach (var prediction in SeiryuDamage()) yield return prediction;
            foreach (var prediction in TitaniaDamage()) yield return prediction;
            foreach (var prediction in InnocenceDamage()) yield return prediction;
            foreach (var prediction in HadesDamage()) yield return prediction;
            foreach (var prediction in WolDamage()) yield return prediction;
            foreach (var prediction in HydaDamage()) yield return prediction;
            foreach (var prediction in AsuraDamage()) yield return prediction;
            foreach (var prediction in ZeleniaDamage()) yield return prediction;
            foreach (var prediction in ValigDamage()) yield return prediction;
            foreach (var prediction in ArkDamage()) yield return prediction;
            foreach (var prediction in EnuoDamage()) yield return prediction;
            foreach (var prediction in NecronDamage()) yield return prediction;
            foreach (var prediction in QueenDamage()) yield return prediction;
            foreach (var prediction in TwinDamage()) yield return prediction;
            foreach (var prediction in ZoraalDamage()) yield return prediction;
            foreach (var prediction in DoomDamage()) yield return prediction;
            foreach (var prediction in GulgDamage()) yield return prediction;
            foreach (var prediction in AnyderDamage()) yield return prediction;
            foreach (var prediction in CosmosDamage()) yield return prediction;
            foreach (var prediction in AnamnesisDamage()) yield return prediction;
            foreach (var prediction in HeroesDamage()) yield return prediction;
            foreach (var prediction in MatoyaDamage()) yield return prediction;
            foreach (var prediction in PaglthanDamage()) yield return prediction;
            foreach (var prediction in BabilDamage()) yield return prediction;
            foreach (var prediction in KtisisDamage()) yield return prediction;
            foreach (var prediction in AitiascopeDamage()) yield return prediction;
            foreach (var prediction in DeadEndsDamage()) yield return prediction;
            foreach (var prediction in TroiaDamage()) yield return prediction;
            foreach (var prediction in LapisDamage()) yield return prediction;
            foreach (var prediction in LunarDamage()) yield return prediction;
            foreach (var prediction in IhuyDamage()) yield return prediction;
            foreach (var prediction in WorqorDamage()) yield return prediction;
            foreach (var prediction in SkydeepDamage()) yield return prediction;
            foreach (var prediction in VanguardDamage()) yield return prediction;
            foreach (var prediction in OrigenicsDamage()) yield return prediction;
            foreach (var prediction in AlexDamage()) yield return prediction;
            foreach (var prediction in TenderDamage()) yield return prediction;
            foreach (var prediction in StrayDamage()) yield return prediction;
            foreach (var prediction in UnderDamage()) yield return prediction;
            foreach (var prediction in MesoDamage()) yield return prediction;
            foreach (var prediction in MistDamage()) yield return prediction;
            foreach (var prediction in ClyDamage()) yield return prediction;
            if (arbBuster != null) yield return arbBuster;
            foreach (var prediction in sohrBusters) yield return prediction;
            foreach (var (id, activation) in baelPrey)
                if (_actors.TryGetValue(id, out var target) && !target.Dead && target.PartySlot >= 0)
                    yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, activation, "Tankbuster");
            foreach (var activation in xelStacks.Values)
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                    yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, activation, "Raidwide");
            if (sohrStack != null)
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                    yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, sohrStack.FinishAt, "Raidwide");
            foreach (var (key, stack) in researchStacks)
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                    yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, stack.Activation, "Raidwide");
            foreach (var activation in researchDonuts.Values.Concat(researchSpreads.Values)
                .Concat(researchLineStacks.Values.Select(l => l.Activation))
                .Concat(_casts.Values.Where(c => c.Rule.Action is 31925 or 31900).Select(c => c.FinishAt)))
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                    yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, activation, "Raidwide");
            foreach (var tower in gubalTowers.Values)
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                    && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(tower.Position.X, tower.Position.Z)) <= 9))
                    yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, tower.Activation, "Raidwide");
            foreach (var bait in sohmBaits.Values)
                if (_actors.TryGetValue(bait.TargetId, out var target) && !target.Dead && target.PartySlot >= 0)
                    yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, bait.FinishAt, "Raidwide");
        }
    }
    private readonly Dictionary<ulong, DungeonActor> _actors = [];
    private readonly Dictionary<ulong, DungeonPendingCast> _casts = [];
    private readonly Dictionary<ulong, (uint Action, DateTime StartedAt, DateTime EndedAt, HashSet<uint> ActionsAtEnd)> _finished = [];
    private readonly Dictionary<(ulong Source, uint Action, uint Sequence), DateTime> _effects = [];
    private readonly Dictionary<ulong, uint> _ripples = [];
    private readonly HashSet<(ulong Source, uint Action)> _missingRotation = [];
    private readonly List<DungeonConcentricSequence> sequences = [];
    private readonly Dictionary<(ulong Actor, uint Status), (bool Present, DateTime ChangedAt, ushort Param)> statusChanges = [];
    private DateTime _now;
    private ulong glowingPlatform;

    public void Synchronize(IEnumerable<DungeonActor> actors, Vector3? player, DateTime now, Func<string, bool>? enabled = null, ulong playerId = 0)
    {
        if (IsAlteRoite)
        {
            AlteRoiteSynchronize(actors, now, enabled, playerId);
            return;
        }
        if (IsHalicarnassus)
        {
            HalicarnassusSynchronize(actors, now, enabled, playerId);
            return;
        }
        if (IsThordan)
        {
            ThordanSynchronize(actors, now, enabled, playerId);
            return;
        }
        if (IsWickedThunder)
        {
            WickedThunderSynchronize(actors, now, enabled, playerId);
            return;
        }
        if (IsKefka)
        {
            KefkaSynchronize(actors, now, enabled, playerId);
            return;
        }
        if (IsQuantum)
        {
            QuantumSynchronize(actors, now, enabled, playerId);
            return;
        }
        _now = now;
        BarP3AdvanceClocks(now);
        PlayerId = playerId;
        if (IsHeavenOnHigh)
            heavenOnHighEnabled = enabled?.Invoke(Definitions[0].Name) != false;
        var treasurePreviousDefinition = Active;
        var previousPlatforms = _actors.Values.Where(a => a.Oid is >= 0x1E87E2 and <= 0x1E87E4)
            .ToDictionary(a => a.Id, a => a.RenderFlags);
        var previousMines = Active?.BossOid == 0x1018
            ? _actors.Values.Where(a => a.Oid == 0x101A).ToArray() : [];
        _actors.Clear();
        foreach (var actor in actors) _actors[actor.Id] = actor;
        EurekaObserve(enabled);
        PalaceGuideObserve(enabled);
        PalaceEarlyPairUpdate();
        GodmotherInput();
        SleepSynchronizeActors();
        if (IsAlphaAlligator || IsBarPassage || IsCarnivale) ObserveJobQuestParty();
        AlphaInput();
        BarPassageInput();
        BarPassageObserve();
        CarnivaleInput(enabled);
        ObserveMaxQuest(enabled);
        HopeObserve();
        TimeBetweenInput();
        HeroesPretendersInput();
        IseultalonInput();
        DikaiosyneInput();
        TimeBetweenObserve();
        ObserveNovice(enabled);
        ObserveFate(enabled, player);
        if (IsFormidable && Active != null && _actors.GetValueOrDefault(PrimaryId)?.InCombat == false)
            ResetFight();
        TreasureYanObserve();
        treasureSackDestroyed.RemoveWhere(id => !_actors.ContainsKey(id));
        TreasureRoomsObserve();
        ViperObserve();
        TreasureCachedBossObserve(false);
        tisiphoneEnabled = territory == HeavenswardIntroCatalog.TISIPHONE && enabled?.Invoke("Tisiphone") != false;
        foreach (var mine in previousMines)
            if (!_actors.ContainsKey(mine.Id)) FractalRemoveMine(mine.Position);
        if (territory == 1036) UpdateCoral();
        if (player == null && PreserveFightAcrossPlayerDeath && _actors.GetValueOrDefault(PlayerId) is { } observer)
            player = observer.Position;
        if (player == null && IsViper)
            player = ViperSourcePlayer?.Position;
        if (player == null && IsEurekaOrthos)
            player = _actors.Values.FirstOrDefault(a => a.Player && a.PartySlot == 0)?.Position;
        if (player == null)
        {
            // Carnivale module state is independent of the viewing player. Hide outputs
            // while the perspective is absent, retaining cast receipts for reconciliation.
            if (IsCarnivale)
            {
                foreach (var actor in _actors.Values.ToArray())
                {
                    CarnivaleSiegfriedObserveCreated(actor.Id);
                    ObserveCast(actor, now);
                    CarnivalePyreticObserve(actor);
                }
                CarnivaleEyeUpdate();
                CarnivaleVoltageBootstrap();
                CarnivaleLastSongBootstrap();
                CarnivaleBallastBootstrap();
                CarnivaleChargeBootstrap();
                CarnivaleRebekkahUpdate();
                CarnivaleClawUpdate();
                CarnivaleTyphonBootstrap();
                CarnivaleChimeraBootstrap();
                CarnivaleSapBootstrap();
                CarnivalePitBootstrap();
                CarnivaleAzulmagiaBootstrap();
                CarnivalePapaBootstrap();
                CarnivaleSnorterUpdate();
                CarnivaleDraugarUpdate();
                CarnivaleRedUpdate();
                CarnivaleSiegfriedUpdate();
                CarnivaleGogoUpdate();
                CarnivaleGoldorUpdate();
                CarnivaleLastSongComplete(enabled);
                CarnivaleUnselect();
            }
            else if (IsEurekaOrthos && Active != null && eurekaEnabled)
            {
                foreach (var source in _casts.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) RemoveActor(source);
                foreach (var actor in EurekaInputActors().ToArray())
                {
                    EurekaTwintaniaCreated(actor);
                    EurekaMinotaurCreated(actor);
                    var eurekaCreated = EurekaChimeraBeforeCast(actor);
                    ObserveCast(actor, now);
                    EurekaChimeraAfterCast(actor, eurekaCreated);
                }
                EurekaChimeraInputComplete();
                EurekaUpdate();
            }
            else if (IsPalaceManagedBoss && palaceGuideEnabled && palaceGuideLoaded is { Active: true } loadedPalace)
            {
                // Keep global component history while no viewing player is available.
                // Personal output is gated by the script; world events still advance.
                Active ??= Definitions[0];
                PrimaryId = loadedPalace.Primary.Actor.Id;
                Arena ??= Active.Arena(loadedPalace.Primary.Actor.Position);
                BlackRiderRestoreCasts();
                foreach (var source in _casts.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) RemoveActor(source);
                foreach (var actor in _actors.Values.ToArray())
                {
                    GodmotherCreated(actor);
                    ObserveCast(actor, now);
                }
                PalaceEarlyPairCommitRoster();
            }
            else ResetFight();
            return;
        }
        if (IsFractalTrash && !FractalEncounterAlive(player.Value)) ResetFight();
        if (IsArboretumTrash && !ArboretumTrashAlive(player.Value)) ResetFight();
        if (IsAntDolls && !_actors.Values.Any(AntDollAlive)) ResetFight();
        if (IsSohrTrash && !SohrTrashAlive()) ResetFight();
        if (IsXelTrash && !XelTrashAlive()) ResetFight();
        GhimlytSelectPrimary();
        GulgSelectPrimary();
        AnyderSelectPrimary();
        AnamnesisSelectPrimary();
        ZotSelectPrimary();
        TroiaSelectPrimary();
        LapisSelectPrimary();
        AetherfontSelectPrimary();
        VanguardSelectPrimary();
        OrigenicsSelectPrimary();
        YuweSelectPrimary();
        MesoSelectPrimary();
        if (IsCaduceus && !CaduceusAlive) ResetFight();
        var primary = _actors.GetValueOrDefault(PrimaryId);
        if ((IsHeavenOnHigh || IsEurekaOrthos) && primary?.Hp == 0)
        {
            ResetFight();
            primary = null;
        }
        if ((IsSleep || IsOtis) && Active != null && (primary == null || primary.Dead || primary.Hp == 0))
            sleepOtisCompleted.Add(PrimaryId);
        if (IsViper && Active != null && (viperGowrowDead || primary == null || primary.Dead))
        {
            viperCompleted = true;
            ResetFight();
        }
        if (IsJobQuestGroup && !JobQuestGroupAlive) ResetFight();
        if (IsNativeTreasure && Active != null && !TreasureGroupAlive) ResetFight();
        if ((IsCoilNodes || IsTwintania || IsLabyrinth && !IsLabyrinthBone || IsSyrcus || IsDarkness || IsArrQuest || IsUltimaExtreme || IsGarudaExtreme || IsTitanExtreme || IsIfritExtreme || IsNativeQuestBoss && !IsViper && !IsSlickshell && !IsHopeOnTheWaves && !IsTimeBetween && !IsAlphaAlligator && !IsBarPassage && !IsCarnivale) && primary?.Hp == 0 && !DivinePairAlive && !JobQuestGroupAlive)
        {
            ResetFight();
            primary = null;
        }
        if (IsZoraalJa && (Active?.BossOid == 0x42A9 && primary?.Targetable == false
            || Active?.BossOid == 0x42B4 && primary?.Hp == 0))
        {
            ResetFight();
            primary = null;
        }
        if (IsXelTrash && (primary == null || !XelTrashMember(primary)))
        {
            var replacement = _actors.Values.FirstOrDefault(XelTrashMember);
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (IsAntDolls && (primary == null || !AntDollAlive(primary)))
        {
            var replacement = _actors.Values.FirstOrDefault(AntDollAlive);
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (IsFractalTrash && (primary == null || !FractalTrashMember(primary)))
        {
            var replacement = _actors.Values.FirstOrDefault(FractalTrashMember);
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (IsResearchTrash && (primary == null || !ResearchTrashMember(primary)))
        {
            var replacement = _actors.Values.FirstOrDefault(ResearchTrashMember);
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (IsArboretumTrash && (primary == null || !ArboretumTrashMember(primary)))
        {
            var replacement = _actors.Values.FirstOrDefault(ArboretumTrashMember);
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (IsSohrTrash && Active?.BossOid != 0x1612 && (primary == null || !SohrTrashMember(primary)))
        {
            var replacement = _actors.Values.FirstOrDefault(SohrTrashMember);
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (Active?.BossOid is 0x1051 or 0x1054 && (primary == null || primary.Dead || !primary.InCombat))
        {
            var replacement = _actors.Values.FirstOrDefault(a => !a.Dead && a.InCombat
                && (a.Oid == Active.BossOid || Active.PrimaryAliases?.Contains(a.Oid) == true));
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (Active?.BossOid == 0xD1E && (primary == null || primary.Dead || !primary.InCombat))
        {
            var replacement = _actors.Values.FirstOrDefault(a => !a.Dead && a.InCombat && IsSnowcloakTrash(a));
            if (replacement != null) { PrimaryId = replacement.Id; primary = replacement; }
        }
        if (Active?.BossOid == 0x5D1 && (primary == null || primary.Dead || !primary.InCombat))
        {
            var phaseTwo = _actors.Values.FirstOrDefault(a => a.Oid == 0x5D0 && !a.Dead && a.InCombat);
            if (phaseTwo != null) { PrimaryId = phaseTwo.Id; primary = phaseTwo; }
        }
        if (Active == null || primary == null && !IsSlickshell && !IsHopeOnTheWaves && !IsTimeBetween && !IsAlphaAlligator && !IsBarPassage && !IsCarnivale && !IsCaduceus && !IsCoilGauntlet && !IsDoomtrain && !IsNecron && !IsMalboro && !IsSohrTrash && !IsTroiaGroup && !IsLapisCarriage && !IsAetherfontTrash && !IsVanguardTrash && !IsOrigenicsTrash && !IsYuweTrash && !IsMesoHeadsmen && !InnocencePhaseAlive && !HadesPhaseAlive && !DivinePairAlive && !JobQuestGroupAlive && !TreasureGroupAlive || primary != null && (primary.Dead || !primary.InCombat)
                && !IsNativeTreasure && !IsSlickshell && !IsHopeOnTheWaves && !IsTimeBetween && !IsAlphaAlligator && !IsBarPassage && !IsCarnivale
                && !DivinePairAlive
                && !JobQuestGroupAlive
                && !IsCaduceus
                && (!IsDoomtrain || doomPhase2)
                && !IsResearchTrash && !IsFractalTrash && !IsArboretumTrash && !IsAntDolls && !IsSohrTrash && !IsXelTrash
                && !IsGhimlytGroup && !IsGulgPrejudice && !IsAnyderSharks && !IsAnamnesisPair && !IsPaglthanFortress && !IsZotSisters && !IsTroiaGroup && !IsLapisCarriage && !IsAetherfontTrash && !IsVanguardTrash && !IsOrigenicsTrash && !IsYuweTrash
                && (!IsAetherfontOcto || primary.Dead)
                && (!IsNativeTrial || primary.Dead)
                && (!IsNativeQuestBoss || primary.Dead)
                && (!IsHeavenOnHigh || primary.Dead)
                && (!IsEurekaOrthos || primary.Dead)
                && !IsLabyrinthBone
                && !IsThornmarch
                && !InnocencePhaseAlive
                && !HadesPhaseAlive
                && (!IsBabilAnima || primary.Dead)
                && (territory != VanguardCatalog.TERRITORY || primary.Dead)
                && (territory != OrigenicsCatalog.TERRITORY || primary.Dead)
                && (territory != AlexandriaCatalog.TERRITORY || primary.Dead)
                && (territory != TenderValleyCatalog.TERRITORY || primary.Dead)
                && (territory != StrayboroughCatalog.TERRITORY || primary.Dead)
                && (territory != YuweyawataCatalog.TERRITORY || primary.Dead)
                && (territory != UnderkeepCatalog.TERRITORY || primary.Dead)
                && (territory != MesoCatalog.TERRITORY || primary.Dead && !IsMesoHeadsmen)
                && (territory != MistwakeCatalog.TERRITORY || primary.Dead)
                && (territory != ClyteumCatalog.TERRITORY || primary.Dead)
            || enabled?.Invoke(Active.Name) == false)
        {
            if (IsBarPassage) BarPassageUnselect();
            else if (IsCarnivale) CarnivaleUnselect();
            else ResetFight();
            if (player is { } position)
            {
                IEnumerable<DungeonActor> candidates = _actors.Values;
                if (IsBarPassage) candidates = BarPassageCandidates;
                else if (IsCarnivale) candidates = CarnivaleCandidates;
                else if (IsNovice) candidates = NoviceActors;
                primary = candidates.Where(a => (!a.Dead || IsBarPassage || IsCarnivale || IsAlphaAlligator || IsSlickshell || IsHopeOnTheWaves || IsTimeBetween || IsTreasureYanPrimary(a) || IsTreasureRoomPrimary(a) || territory == ArboretumCatalog.TERRITORY && ArboretumCatalog.MALBORO.Contains(a.Oid) || territory == YuweyawataCatalog.TERRITORY && YuweDefinition(a) is { } yd && YuweyawataCatalog.IsTrash(yd.BossOid)) && (a.Targetable || IsBarPassage || IsCarnivale || IsTreasureYanPrimary(a) || IsTreasureRoomPrimary(a) || territory == ArboretumCatalog.TERRITORY && ArboretumCatalog.MALBORO.Contains(a.Oid) || territory == FractalCatalog.TERRITORY && FractalCatalog.IsControl(a.Oid)
                    || territory == SohrKhaiCatalog.TERRITORY && a.Oid == 0x1612
                    || territory == BardamCatalog.TERRITORY && a.Oid == 0x1AA5
                    || territory == PaglthanCatalog.TERRITORY && a.Oid == 0x32FE
                    || IsCoilGauntlet || IsTactical || IsOtis
                    || IsStepsOfFaith
                    || IsViper && a.Oid == 0x429F
                    || IsScarmiglioneQuest && a.Oid == 0x39B3
                    || territory == MainScenarioQuestCatalog.FADED_MEMORIES && a.Oid == 0x2F28
                    || territory is AetherfontCatalog.TERRITORY or YuweyawataCatalog.TERRITORY)
                    && (territory switch
                    {
                        FractalCatalog.TERRITORY => FractalCanStart(a, position),
                        ArboretumCatalog.TERRITORY => ArboretumCanStart(a, position),
                        SohrKhaiCatalog.TERRITORY => SohrCanStart(a, position),
                        XelphatolCatalog.TERRITORY => XelCanStart(a),
                        MtGulgCatalog.TERRITORY => GulgCanStart(a),
                        PaglthanCatalog.TERRITORY => PaglthanCanStart(a),
                        TroiaCatalog.TERRITORY => TroiaCanStart(a),
                        AetherfontCatalog.TERRITORY => AetherfontCanStart(a, position),
                        VanguardCatalog.TERRITORY => VanguardCanStart(a),
                        YuweyawataCatalog.TERRITORY => a.InCombat && YuweDefinition(a) != null,
                        NecronCatalog.TERRITORY => NecronCanStart(a),
                        ZoraalJaCatalog.TERRITORY => !zoraalWiping && a.InCombat && (a.Oid != 0x42B4 || a.Hp != 0),
                        DoomtrainCatalog.TERRITORY => !doomWiping && a.InCombat && a.Hp != 0,
                        CoilNodesCatalog.FIRST or CoilNodesCatalog.SECOND => CoilCanStart(a),
                        CoilGauntletCatalog.TERRITORY => GauntletCanStart(a),
                        TwintaniaCatalog.TERRITORY => !twinWiping && a.InCombat && a.Hp != 0,
                        LabyrinthCatalog.TERRITORY => !labyrinthWiping && a.InCombat && a.Hp != 0,
                        SyrcusCatalog.TERRITORY => !syrcusWiping && a.InCombat && a.Hp != 0,
                        DarknessCatalog.TERRITORY => !darknessWiping && a.InCombat && a.Hp != 0,
                        UltimaExtremeCatalog.TERRITORY => !ultimaWiping && a.InCombat && a.Hp != 0,
                        GarudaExtremeCatalog.TERRITORY => !garudaExWiping && a.Targetable && a.InCombat && a.Hp != 0,
                        TitanExtremeCatalog.TERRITORY => !titanExWiping && a.Targetable && a.InCombat && a.Hp != 0,
                        IfritExtremeCatalog.TERRITORY => !ifritExWiping && a.Targetable && a.InCombat && a.Hp != 0,
                        ArrQuestCatalog.ARCHON or ArrQuestCatalog.ULTIMATE_WEAPON or ArrQuestCatalog.STEPS_OF_FAITH => !arrQuestWiping && a.InCombat && a.Hp != 0,
                        _ when IsBarPassage => BarPassageCanPull(a),
                        _ when IsCarnivale => CarnivalePull(a),
                        _ when IsAlphaAlligator => a.InCombat && !alphaWiping && !alphaCompleted.Contains(a.Id),
                        _ when IsSlickshell => a.InCombat && !slickshellWiping,
                        _ when IsHopeOnTheWaves => a.InCombat && !hopeWiping,
                        _ when IsTimeBetween => a.InCombat && !timeWiping && !timeCompleted.Contains(a.Id),
                        _ when IsHeroesPretenders => a.InCombat && !heroesPretendersWiping && !heroesPretendersCompleted.Contains(a.Id),
                        _ when IsIseultalon => a.InCombat && !iseultalonWiping && !iseultalonCompleted.Contains(a.Id),
                        _ when IsDikaiosyne => a.InCombat && !dikaiosyneWiping && !dikaiosyneCompleted.Contains(a.Id),
                        _ when IsMaxQuest => a.InCombat && a.Hp != 0 && !maxQuestWiping && !maxQuestCompleted.Contains(a.Id),
                        _ when IsSleep || IsOtis => SleepOtisPull(a),
                        _ when IsViper => ViperPull,
                        _ when IsScarmiglioneQuest => a.Hp != 0 && ScarmiglioneQuestPull,
                        MainScenarioQuestCatalog.FADED_MEMORIES when a.Oid == 0x2F28 => a.Hp != 0 && FadedZenosPull,
                        _ when IsNativeFate => FatePull(a, position) && a.Hp != 0 && !fateCompleted.Contains(a.Id),
                        _ when IsNovice => (IsTactical || (territory == NoviceCatalog.TANK04 ? a.Id == noviceTank04Primary : a.InCombat))
                            && a.Hp != 0 && !noviceCompleted.Contains(a.Id),
                        _ when IsPalaceManagedBoss => PalaceGuideCanPull(a),
                        _ when IsNativeQuestBoss => a.InCombat && a.Hp != 0,
                        _ when IsHeavenOnHigh => a.InCombat && a.Hp != 0,
                        _ when IsEurekaOrthos => !eurekaWiping && a.Id == eurekaLoadedPrimary && a.InCombat && a.Hp != 0,
                        _ when IsTreasureRoomPrimary(a) => TreasureRoomCanStart(a),
                        _ when IsTreasureYanPrimary(a) => true,
                        _ => ResearchCanStart(a, position),
                    })
                    && Definitions.Any(d => (d.BossOid == a.Oid || d.PrimaryAliases?.Contains(a.Oid) == true) && enabled?.Invoke(d.Name) != false
                        && (territory != ResearchFacilityCatalog.TERRITORY || !ResearchTrashCatalog.IsPrimary(d.BossOid) || d.BossOid == a.Oid)
                        && (territory != FractalCatalog.TERRITORY || !FractalCatalog.IsTrash(d.BossOid) || d.BossOid == a.Oid)
                        && (d.BossOid != 0xD1E || IsSnowcloakTrash(a))))
                    .OrderBy(a => IsZoraalJa && a.Oid == 0x42B4 ? 0 : 1)
                    .ThenBy(a => IsTreasureRoomPrimary(a) ? TreasureRoomOrder(a) : 0)
                    .ThenBy(a => IsBarPassage ? BarPassageOrder(a) : 0)
                    .ThenBy(a => IsBarPassage || IsNovice || IsTreasureRoomPrimary(a) ? 0 : Vector3.DistanceSquared(a.Position, position)).FirstOrDefault();
                if (primary != null)
                {
                    if (territory == TroiaCatalog.TERRITORY) Active = TroiaDefinition(primary);
                    else if (territory == AetherfontCatalog.TERRITORY) Active = AetherfontDefinition(primary);
                    else if (territory == VanguardCatalog.TERRITORY) Active = VanguardDefinition(primary);
                    else if (territory == YuweyawataCatalog.TERRITORY) Active = YuweDefinition(primary);
                    else Active = Definitions.FirstOrDefault(d => d.BossOid == primary.Oid)
                        ?? Definitions.First(d => d.PrimaryAliases?.Contains(primary.Oid) == true);
                    PrimaryId = primary.Id;
                    TimeBetweenStarted(primary);
                    if (!IsBarPassage) AlphaStarted();
                    BarPassageStarted(primary);
                    if (IsCaduceus) caduceusStarted = now;
                    if (IsMalboro) malboroStarted = true;
                    Arena = Active!.BossOid == 0x3873 && neroArenaChanged
                        ? PraetoriumArenas.Nero(primary.Position.Y) : Active.Arena(primary.Position);
                    if (territory == TroiaCatalog.TERRITORY) Arena = TroiaArena(primary);
                    if (Active.BossOid == 0xD1E && northernWallRemoved) Arena = SnowcloakArenas.Northern(primary.Position.Y);
                    if (Active.BossOid == 0xD07 && wandilArenaShrunk) Arena = SnowcloakArenas.Wandil(primary.Position.Y);
                    if (Active.BossOid == 0x3970 && gyascutusShrunk)
                        Arena = new CircleGeometry(new(11.978f, primary.Position.Y, 67.979f), 19.9f);
                    if (Active.BossOid == 0x1612 && sohrBridge) Arena = SohrKhaiArenas.BlizzardBridge(primary.Position.Y);
                    ResearchTrashStarted(primary);
                }
            }
        }
        AlphaObserve();
        BarPassageObserve();
        if (Active == null)
        {
            if (IsCarnivale)
            {
                if (carnivaleModules.Any(m => m.Eyes != null))
                    foreach (var actor in _actors.Values.ToArray()) ObserveCast(actor, now);
                CarnivaleEyeUpdate();
            }
            return;
        }
        MicaObserveActors();
        TreasureYanObserve();
        TreasureBaitObserve();
        TreasureBuffetObserve();
        TreasureCachedBossObserve(true);
        ObserveJobQuestParty();
        TacticalObserve();
        FallenObserve();
        GuildivainObserve();
        SleepOtisObserve();
        SlickshellObserve();
        TimeBetweenObserve();
        IoObserve();
        NodeObserve();
        SteelObserve();
        QuestWindObserve();
        HeroesPretendersObserve();
        IseultalonObserve();
        RhalgrObserve();
        EndwalkerQuestObserve();
        ViperCacheGowrow();
        PortraitObserve();
        ObserveHeavenswardQuest();
        LapisSelectPrimary();
        AetherfontSelectPrimary();
        VanguardSelectPrimary();
        OrigenicsSelectPrimary();
        YuweSelectPrimary();
        MesoSelectPrimary();
        if (Active == null) return;
        if (Active.BossOid == 0x477A)
        {
            foreach (var platform in _actors.Values.Where(a => a.Oid is >= 0x1E87E2 and <= 0x1E87E4 && a.RenderFlags == 0 && !a.Dead))
                if (glowingPlatform == 0 || !previousPlatforms.TryGetValue(platform.Id, out var flags) || flags != 0)
                    glowingPlatform = platform.Id;
            if (!_actors.TryGetValue(glowingPlatform, out var glowing) || glowing.RenderFlags != 0 || glowing.Dead)
                glowingPlatform = 0;
        }
        // A restored cast can depend on an earlier wall/bridge event retained in
        // the same world frame. Restore those states before creating cast snapshots.
        PrepareShisuiChests();
        SynchronizeGulg(player.Value);
        foreach (var actor in _actors.Values)
            if (actor.EventAnimation is { } state) Animation(actor.Id, state);
        SynchronizeQitana();
        SynchronizeAmaurot();
        SynchronizeTwinning();
        SynchronizeAnyder();
        SynchronizeCosmos();
        SynchronizeAnamnesis();
        SynchronizeHeroes();
        SynchronizeMatoya();
        SynchronizePaglthan();
        SynchronizeZot();
        SynchronizeBabil();
        SynchronizeVanaspati();
        SynchronizeKtisis();
        // Event line stacks retain the last actor state, including a death update, after object cleanup.
        SynchronizeLunarLines();
        IhuyObserveActors();
        WorqorObserveActors();
        SkydeepObserveActors();
        AlexObserveActors();
        TenderObserveActors();
        StrayObserveActors();
        YuweObserveStatuses();
        UnderObserveActors();
        MesoObserveActors();
        MistObserve();
        ClyObserve();
        ArrTrialObserve();
        PortaObserve();
        HardTrialObserve();
        ThornObserve();
        WhorlObserve();
        SusanoObserve();
        LakshmiObserve();
        ShinryuObserve();
        TsukuyomiObserve();
        YojimboObserve();
        ByakkoObserve();
        SeiryuObserve();
        TitaniaObserve();
        InnocenceObserve();
        HadesObserve();
        WolObserve();
        HydaObserve();
        AsuraObserve();
        ZeleniaObserve();
        ValigObserve();
        ArkObserve();
        EnuoObserve();
        NecronObserve();
        QueenObserve();
        ZoraalObserve();
        DoomObserve();
        CoilObserve();
        CaduceusObserve();
        TwinObserve();
        DarknessTick();
        StepsObserve();
        UltimaObserve();
        GarudaExObserve();
        TitanExObserve();
        IfritExObserve();
        TrialVoidzoneObserve();
        if (Active.BossOid == 0x1AFE)
            foreach (var actor in _actors.Values.Where(a => a.EventState is 1 or 2)) SirenEventState(actor.Id, actor.EventState);
        foreach (var source in malboroCasts.Select(c => c.Source).Where(id => !_actors.ContainsKey(id)).Distinct().ToArray())
            MalboroFinish(source, 0);
        if (IsNativeTreasure && !ReferenceEquals(treasurePreviousDefinition, Active))
            treasureParty = [.. TreasurePartyInput];
        TreasureHexObserve();
        TreasureHydrosphereObserveMissing();
        TreasureCreatedCircleObserveMissing();
        TreasureBeamObserveMissing();
        TreasureRotationObserveMissing();
        TreasurePlayerGazeObserve();
        TreasureMolterSynchronize();
        // The world feed destroys missing actors before applying this frame's creates/casts.
        // A disappearing sphere must not consume a replacement created at the same position.
        if (UsesDeepDungeonCastIdentity)
            foreach (var source in _casts.Keys.Concat(treasureObservedCasts.Keys).Concat(heavenOnHighSpawnActors.Keys)
                .Distinct().Where(id => !_actors.ContainsKey(id)).ToArray()) RemoveActor(source);
        BlackRiderRestoreCasts();
        KenkoInput();
        GodmotherInput();
        foreach (var actor in (IsEurekaOrthos
            ? EurekaInputActors()
            : _actors.Values.AsEnumerable()).ToArray())
        {
            if (IsAlphaAlligator) continue;
            // Upstream bootstrap interleaves each actor's Created and CastStarted in world order.
            ChiCreated(FateCreatedActor(actor), observed: !fateCreatedActors.ContainsKey(actor.Id));
            SteelCreated(actor);
            HeavenOnHighObserveSpawn(actor);
            KenkoObserveCreated(actor);
            EurekaTwintaniaCreated(actor);
            EurekaMinotaurCreated(actor);
            GodmotherCreated(actor);
            TreasureHexMineCreated(actor.Id, actor.Oid, actor.Position);
            TreasureHydrosphereCreated(actor.Id, actor.Oid, actor.Position, observed: true);
            TreasureCreatedCircle(actor.Id, actor.Oid, actor.Position, observed: true);
            TreasureBeamCreated(actor.Id, actor.Oid, actor.Position, actor.Rotation, observed: true);
            TreasureRotationCreated(actor.Id, actor.Oid, actor.Rotation, observed: true);
            if (IsMalboro) { MalboroObserve(actor); continue; }
            // Retain dead dreamers for the group gate, while still observing cast endings below.
            if (actor.Dead && !UsesDeepDungeonCastIdentity && !IsBarPassage && !IsCarnivale && !IsNativeFate && !IsTactical && !IsNativeTreasure && !IsNativeTrial && !IsCarnivale && !JobQuestCatalog.IsTerritory(territory) && !IsMainScenarioQuest && !(territory == TroiaCatalog.TERRITORY && actor.Oid == 0x3968)
                && !(territory == LapisCatalog.TERRITORY && actor.Oid == 0x3D04)
                && territory is not (LunarCatalog.TERRITORY or IhuykatumuCatalog.TERRITORY or WorqorCatalog.TERRITORY or SkydeepCatalog.TERRITORY or VanguardCatalog.TERRITORY or OrigenicsCatalog.TERRITORY or AlexandriaCatalog.TERRITORY or TenderValleyCatalog.TERRITORY or StrayboroughCatalog.TERRITORY or YuweyawataCatalog.TERRITORY or UnderkeepCatalog.TERRITORY or MesoCatalog.TERRITORY or MistwakeCatalog.TERRITORY or ClyteumCatalog.TERRITORY)
                && !(IsLapisCarriage && LapisCatalog.IsCarriage(actor.Oid)))
            {
                if (IsNativeQuestBoss)
                {
                    if (_casts.TryGetValue(actor.Id, out var deadCast)) Finish(actor.Id, deadCast.Rule.Action);
                    continue;
                }
                // Vanaspati keeps dead bubbles in the fire union until destruction or Withdraw.
                if (territory == VanaspatiCatalog.TERRITORY && actor.Oid == 0x3731) { _casts.Remove(actor.Id); continue; }
                if (IsSohrTrash && Active?.BossOid == 0x1612 && actor.Id == PrimaryId) continue;
                if (Active?.BossOid == 0x179F && actor.Oid == 0x17A0) continue;
                if (Active?.BossOid == 0x193D && actor.Oid == 0x193F) { _casts.Remove(actor.Id); continue; }
                if (Active?.BossOid == 0x1023 && actor.Oid is 0x1023 or 0x1021) _casts.Remove(actor.Id);
                else RemoveActor(actor.Id, destroyed: false);
                continue;
            }
            CarnivaleSiegfriedObserveCreated(actor.Id);
            var eurekaCreated = EurekaChimeraBeforeCast(actor);
            ObserveCast(actor, now);
            EurekaChimeraAfterCast(actor, eurekaCreated);
            CarnivalePyreticObserve(actor);
            TreasureMolterObserve(actor);
            TreasureHexObserveActor(actor);
        }
        EurekaChimeraInputComplete();
        PalaceEarlyPairCommitRoster();
        foreach (var source in _casts.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) RemoveActor(source);
        foreach (var source in delayedDamage.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) delayedDamage.Remove(source);
        foreach (var source in _ripples.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) _ripples.Remove(source);
        foreach (var created in fateCreatedActors.Values.Where(a => !_actors.ContainsKey(a.Id)))
            ChiCreated(created, observed: false);
        SynchronizeFate();
        SynchronizeCuttersCry();
        SynchronizeStoneVigil();
        SynchronizePraetorium();
        SynchronizeWanderer();
        SynchronizeAmdapor();
        SynchronizePharos();
        SynchronizeSnowcloak();
        SynchronizeKeeper();
        SynchronizeSohm();
        SynchronizeAery();
        SynchronizeVault();
        SynchronizeGubal();
        SynchronizeResearch();
        SynchronizeFractal();
        SynchronizeArboretum();
        SynchronizeAntitower();
        SynchronizeSohr();
        SynchronizeXel();
        SynchronizeBael();
        SynchronizeSiren();
        SynchronizeShisui();
        SynchronizeBardam();
        SynchronizeDoma();
        SynchronizeAlaMhigo();
        SynchronizeSkalla();
        SynchronizeBurn();
        SynchronizeGhimlyt();
        SynchronizeHolminster();
        SynchronizeDohn();
        UpdateArboretumTrashArena(player);
        SynchronizeAitiascope();
        SynchronizeDeadEnds();
        SynchronizeStigma();
        SynchronizeAlzadaal();
        SynchronizeTroia();
        SynchronizeLapis();
        SynchronizeLunar();
        SynchronizeIhuy();
        SynchronizeWorqor();
        SkydeepObserveActors();
        SynchronizeVanguard();
        SynchronizeOrigenics();
        SynchronizeAlex();
        SynchronizeTender();
        SynchronizeStray();
        SynchronizeGraoully();
        if (IsNativeTreasure)
        {
            treasureParty = [.. TreasurePartyInput];
            TreasureCastBaitObserve();
            TreasurePlayerStatusesObserve();
        }
        if (territory == AetherfontCatalog.TERRITORY)
            foreach (var id in aetherfontStacks.Keys.Where(id => !_actors.TryGetValue(id, out var a) || a.Dead).ToArray())
                aetherfontStacks.Remove(id);
        KenkoUpdate();
        EurekaUpdate();
        CarnivaleEyeUpdate();
        CarnivaleVoltageBootstrap();
        CarnivaleLastSongBootstrap();
        CarnivaleBallastBootstrap();
        CarnivaleChargeBootstrap();
        CarnivaleRebekkahUpdate();
        CarnivaleClawUpdate();
        CarnivaleTyphonBootstrap();
        CarnivaleChimeraBootstrap();
        CarnivaleSapBootstrap();
        CarnivalePitBootstrap();
        CarnivaleAzulmagiaBootstrap();
        CarnivalePapaBootstrap();
        CarnivaleSnorterUpdate();
        CarnivaleDraugarUpdate();
        CarnivaleRedUpdate();
        CarnivaleSiegfriedUpdate();
        CarnivaleGogoUpdate();
        CarnivaleGoldorUpdate();
        CarnivaleLastSongComplete(enabled);
        Tick(now);
    }

    private void ObserveCast(DungeonActor actor, DateTime now)
    {
        if (actor.Cast is { } cast)
        {
            StartCast(actor.Id, cast, now, fromPacket: false);
        }
        else
        {
            if (IsCarnivale || UsesDeepDungeonCastIdentity) treasureObservedCasts.Remove(actor.Id);
            _missingRotation.RemoveWhere(k => k.Source == actor.Id);
            if (_casts.TryGetValue(actor.Id, out var old) && (old.Observed || now >= old.StartedAt.AddSeconds(0.2))
                && (IsNativeTreasure || IsCarnivale || IsHeavenOnHigh || IsEurekaOrthos || IsNativeQuestBoss || old.EffectSeen || now >= old.FinishAt.AddSeconds(-0.35) || now < old.FinishAt.AddSeconds(-0.5)))
            {
                Finish(actor.Id, old.Rule.Action, completed: old.EffectSeen || now >= old.FinishAt.AddSeconds(-0.35));
                if (IsCarnivale && !old.Observed)
                    carnivaleUnobservedEnds[actor.Id] = old;
            }
        }
    }

    public void StartCast(ulong source, DungeonCastObservation cast, DateTime now, bool fromPacket = true)
    {
        _now = now;
        var definition = Active;
        if (IsBarPassage) definition = BarPassageCastDefinition(cast.Action);
        else if (IsCarnivale) definition = CarnivaleCastDefinition(cast.Action);
        if (IsMalboro) { MalboroStart(source, cast, fromPacket); return; }
        if (definition == null || !_actors.TryGetValue(source, out var actor)
            || actor.Dead && !UsesDeepDungeonCastIdentity && !IsBarPassage && !IsCarnivale && !IsNativeFate && !IsTactical && !IsNativeTreasure && !IsMainScenarioQuest && !IsHeroesPretenders && !IsDikaiosyne && !(IsGarudaExtreme && cast.Action == 1555)
            || !UsesObservedCastEnd && cast.FinishAt.AddSeconds(2) <= now) return;
        if (IsTimeBetween && cast.Action == 8993 && TimeBetweenPhase == 0) return;
        if (IsTitania && titaniaWaitingRemoval.Contains(source) || IsInnocence && innocenceWaitingRemoval.Contains(source)) return;
        if (IsHades && hadesWaitingRemoval.Contains(source)) return;
        if (IsWarriorOfLight && wolWaitingRemoval.Contains(source)) return;
        if (!fromPacket && territory == JobQuestCatalog.FACE_OF_TRUE_EVIL
            && cast.Action is 8419 or 8643 or 8642 && cast.StartedAt <= arashiResolvedAt) return;
        if (IsEurekaOrthos && !EurekaSpellSource(source)) return;
        var eurekaFirstRaw = IsEurekaOrthos && !fromPacket
            && _casts.TryGetValue(source, out var eurekaPacket) && !eurekaPacket.Observed
            && eurekaPacket.Rule.Action == cast.Action && !treasureObservedCasts.ContainsKey(source);
        if (IsEurekaOrthos && fromPacket && treasureObservedCasts.ContainsKey(source)) return;
        // Reject stale generations before a CastInfo change can finish the accepted cast.
        if (_casts.TryGetValue(source, out var latest)
            && !eurekaFirstRaw && cast.StartedAt < latest.StartedAt.AddSeconds(latest.Rule.Action == cast.Action ? -.25 : 0)) return;
        var acceptedGeneration = _casts.TryGetValue(source, out var accepted) && accepted.Rule.Action == cast.Action
            && Math.Abs((accepted.StartedAt - cast.StartedAt).TotalSeconds) < .25
            && Math.Abs(((accepted.InputFinishAt ?? accepted.FinishAt) - cast.FinishAt).TotalSeconds) < .75;
        // Once the authoritative CastInfo is observed, a delayed packet cannot retarget that generation.
        if ((IsNybeth || IsBlackRider || IsEurekaOrthos) && fromPacket && acceptedGeneration && accepted!.Observed) return;
        var barP3FirstRaw = IsBarPassage && !fromPacket && cast.Action is 40917 or 40922 or 40916 or 40927
            && _casts.TryGetValue(source, out var barP3Packet) && !barP3Packet.Observed;
        var kenkoFirstRaw = IsKenko && !fromPacket && cast.Action == 12206
            && _casts.TryGetValue(source, out var kenkoPacket) && !kenkoPacket.Observed ? kenkoPacket : null;
        (DungeonPendingCast Cast, HeavenOnHighBait? Charge, HeavenOnHighBait? Devour)? kenkoFirstRawBaits = kenkoFirstRaw == null ? null
            : (kenkoFirstRaw, kenkoCharges.FirstOrDefault(b => ReferenceEquals(b.Cast, kenkoFirstRaw)),
                kenkoDevours.FirstOrDefault(b => ReferenceEquals(b.Cast, kenkoFirstRaw)));
        var observedCasts = IsNativeFate || IsTactical ? fateObservedCasts : treasureObservedCasts;
        if (UsesDeepDungeonCastIdentity && !fromPacket && treasureObservedCasts.TryGetValue(source, out var hohRaw)
            && !NativeCastChanged(hohRaw, cast)) return;
        // The native feed changes CastInfo only when its identity/shape fields change.
        // Elapsed-clock drift alone must not end and restart Carnivale components.
        if (IsCarnivale && !fromPacket && (CarnivaleEyeAction(cast.Action) || cast.Action == 14710 || CarnivaleEarlyAction(cast.Action) || CarnivaleSunsetAction(cast.Action) || CarnivaleClawAction(cast.Action) || CarnivaleKegAction(cast.Action) || CarnivaleRebekkahAction(cast.Action) || CarnivaleTyphonAction(cast.Action) || CarnivaleChimeraAction(cast.Action) || CarnivaleBoomAction(cast.Action) || CarnivaleBehemothAction(cast.Action) || CarnivalePitAction(cast.Action) || CarnivaleAzulmagiaAction(cast.Action) || CarnivalePapaAction(cast.Action) || CarnivaleSnorterAction(cast.Action) || CarnivaleDraugarAction(cast.Action) || CarnivaleRedAction(cast.Action) || CarnivaleSiegfriedAction(cast.Action) || CarnivaleGogoAction(cast.Action) || CarnivaleGoldorAction(cast.Action))
            && treasureObservedCasts.TryGetValue(source, out var sunsetRaw) && !NativeCastChanged(sunsetRaw, cast)
            // A pre-pull Stage22/23 observation had no component to claim it. The new
            // component must still receive the current cast during activation.
            && !(!_casts.ContainsKey(source)
                && (CarnivaleEyeBootstrap(cast.Action)
                    || cast.Action == 14710 && carnivaleLoaded.Any(m => m.VoltageNeedsBootstrap)
                    || CarnivaleEarlyBootstrap(cast.Action)
                    || CarnivaleBoomAction(cast.Action) && carnivaleLoaded.Any(m => m.SapNeedsBootstrap)
                    || CarnivaleBehemothAction(cast.Action) && carnivaleLoaded.Any(m => m.Started && m.Primary.Actor.Oid == 0x2732)
                    || CarnivalePitAction(cast.Action) && carnivaleLoaded.Any(m => m.PitNeedsBootstrap)
                    || CarnivaleAzulmagiaAction(cast.Action) && carnivaleLoaded.Any(m => m.AzulmagiaNeedsBootstrap)
                    || CarnivalePapaAction(cast.Action) && carnivaleLoaded.Any(m => m.PapaNeedsBootstrap)
                    || CarnivaleSnorterAction(cast.Action) && carnivaleLoaded.Any(m => m.Snorter is { NeedsBootstrap: true })
                    || CarnivaleDraugarAction(cast.Action) && carnivaleLoaded.Any(m => m.Draugar is { NeedsBootstrap: true })
                    || CarnivaleRedAction(cast.Action) && carnivaleLoaded.Any(m => m.Red is { NeedsBootstrap: true })
                    || CarnivaleSiegfriedAction(cast.Action) && carnivaleLoaded.Any(m => m.Siegfried is { NeedsBootstrap: true })
                    || CarnivaleGogoAction(cast.Action) && carnivaleLoaded.Any(m => m.Gogo is { NeedsBootstrap: true })
                    || CarnivaleGoldorAction(cast.Action) && carnivaleLoaded.Any(m => m.Goldor is { NeedsBootstrap: true }))))
            return;
        var blackRiderFirstRaw = IsBlackRider && !fromPacket && cast.Action == 7089
            && acceptedGeneration && !accepted!.Observed;
        var carnivaleChargeFirstRaw = IsCarnivale && !fromPacket && CarnivaleChargeAction(cast.Action)
            && _casts.TryGetValue(source, out var chargePacket) && !chargePacket.Observed;
        var rawNativeChanged = (IsNativeTreasure || IsNativeFate || IsTactical) && !fromPacket
            && observedCasts.TryGetValue(source, out var previousRaw) && NativeCastChanged(previousRaw, cast);
        var questRawChanged = (IsAlphaAlligator || IsBarPassage || IsCarnivale || UsesDeepDungeonCastIdentity) && !fromPacket
            && (treasureObservedCasts.TryGetValue(source, out var questRaw) && NativeCastChanged(questRaw, cast)
                || !eurekaFirstRaw && (IsBarPassage || IsCarnivale || UsesDeepDungeonCastIdentity) && _casts.TryGetValue(source, out var packetCast) && !packetCast.Observed
                    && (packetCast.Location != cast.Location || packetCast.Rotation != cast.Rotation
                        || packetCast.TargetId != cast.TargetId
                        || Math.Abs(((packetCast.FinishAt - packetCast.StartedAt)
                            - (cast.FinishAt - cast.StartedAt)).TotalSeconds) > .00001));
        DungeonPendingCast? recoveredCarnivaleCast = null;
        if (IsCarnivale && !fromPacket && carnivaleUnobservedEnds.TryGetValue(source, out var unobserved)
            && cast.StartedAt >= unobserved.StartedAt.AddSeconds(-.25))
        {
            questRawChanged = true;
            if (cast.Action == unobserved.Rule.Action && Math.Abs((cast.StartedAt - unobserved.StartedAt).TotalSeconds) < .25
                && Math.Abs((cast.FinishAt - unobserved.FinishAt).TotalSeconds) < .75)
                recoveredCarnivaleCast = unobserved;
        }
        if (_finished.TryGetValue(source, out var finished)
            && (cast.StartedAt < finished.EndedAt
                || cast.StartedAt == finished.EndedAt && finished.ActionsAtEnd.Contains(cast.Action))
            && !eurekaFirstRaw && !questRawChanged && !((IsNativeFate || IsTactical) && rawNativeChanged)
            && !(acceptedGeneration && cast.StartedAt > finished.StartedAt && accepted!.StartedAt > finished.StartedAt)) return;
        if ((IsAlphaAlligator || IsBarPassage || IsCarnivale || UsesDeepDungeonCastIdentity) && !fromPacket) treasureObservedCasts[source] = cast;
        var firstTreasureRaw = IsNativeTreasure && !fromPacket && !observedCasts.ContainsKey(source);
        if ((IsNativeTreasure || IsNativeFate || IsTactical) && !fromPacket) observedCasts[source] = cast;
        var nativeReplacement = false;
        if (_casts.TryGetValue(source, out var existing))
        {
            var tolerance = existing.Rule.Action == cast.Action ? 0.25 : 0;
            if (!eurekaFirstRaw && cast.StartedAt < existing.StartedAt.AddSeconds(-tolerance)) return;
            var partyTargetChanged = (UsesQuestPartyMarks && JobQuestPartyAction(existing.Rule.Action))
                && existing.TargetId != cast.TargetId
                && !(IsNativeTreasure && definition.BossOid == NativeTreasureCatalog.DAEN_ECHIDNA
                    && TreasurePlayerGazeAction(cast.Action) && fromPacket && acceptedGeneration)
                || rawNativeChanged || questRawChanged;
            if (existing.Rule.Action != cast.Action
                || partyTargetChanged
                || (UsesObservedCastEnd || territory is StrayboroughCatalog.TERRITORY or YuweyawataCatalog.TERRITORY or UnderkeepCatalog.TERRITORY or MesoCatalog.TERRITORY or MistwakeCatalog.TERRITORY or ClyteumCatalog.TERRITORY)
                && !eurekaFirstRaw && (Math.Abs((existing.StartedAt - cast.StartedAt).TotalSeconds) >= .25
                    || Math.Abs(((existing.InputFinishAt ?? existing.FinishAt) - cast.FinishAt).TotalSeconds) >= .75))
            {
                Finish(source, existing.Rule.Action, completed: existing.EffectSeen || now >= existing.FinishAt.AddSeconds(-0.35));
                // A target handoff finishes the old component record, while this cast generation remains live.
                if (partyTargetChanged && acceptedGeneration) _finished.Remove(source);
                if (rawNativeChanged) observedCasts[source] = cast;
                nativeReplacement = UsesDeepDungeonCastIdentity || IsBarPassage || IsCarnivale || IsMainScenarioQuest || IsHeroesPretenders || IsDikaiosyne || IsNativeTreasure || IsNativeFate || IsTactical || QuestWindAction(cast.Action);
                // A replacement starts a fresh observation baseline, including packet-only generations.
                if (UsesDeepDungeonCastIdentity || IsBarPassage || IsCarnivale || IsMainScenarioQuest || IsHeroesPretenders || IsDikaiosyne || QuestWindAction(cast.Action)) questWindObserved.Remove(source);
            }
        }
        if ((IsPalaceSerpent || IsOrcus && cast.Action == 27593 || IsIo && cast.Action == 2175
            || IsSteel && source == PrimaryId || (IsRhalgr && cast.Action == 8346 || IsSlickshell && cast.Action == 1087) || IsNativeTreasure || IsNativeFate || IsTactical) && fromPacket)
        {
            if (actor.Cast != null && actor.Cast.StartedAt > cast.StartedAt.AddSeconds(.25)) return;
            _actors[source] = actor with { Cast = cast };
        }
        var rule = definition.Casts.FirstOrDefault(r => r.Action == cast.Action
            && (r.CasterOid == actor.Oid || UsesObservedCastEnd && r.CasterOid == 0 || territory is ZotCatalog.TERRITORY or LapisCatalog.TERRITORY or AetherfontCatalog.TERRITORY or LunarCatalog.TERRITORY or IhuykatumuCatalog.TERRITORY or WorqorCatalog.TERRITORY or SkydeepCatalog.TERRITORY or VanguardCatalog.TERRITORY or OrigenicsCatalog.TERRITORY or AlexandriaCatalog.TERRITORY or TenderValleyCatalog.TERRITORY or StrayboroughCatalog.TERRITORY or YuweyawataCatalog.TERRITORY or UnderkeepCatalog.TERRITORY or MesoCatalog.TERRITORY or MistwakeCatalog.TERRITORY or ClyteumCatalog.TERRITORY && r.CasterOid == 0));
        if (territory == AnyderCatalog.TERRITORY && definition.BossOid == 0x249E)
            anyderLashes.RemoveAll(l => l.Visual != cast.Action);
        // Unclaimed casts still deliver the source's arbitrary-action Finish for grouped waves.
        if (rule == null && (IsTactical || IsOtis)) rule = new(cast.Action, 0, "Unclaimed cast observation", null);
        if (rule == null) return;
        if (IsTwintania && !TwinCastAllowed(rule.Action)) return;
        if (IsGarudaExtreme && !GarudaExCastAllowed(rule.Action)) return;
        if (IsTitanExtreme && !TitanExCastAllowed(rule.Action)) return;
        if (IsIfritExtreme && !IfritExCastAllowed(rule.Action)) return;
        if (territory == LapisCatalog.TERRITORY && !LapisAcceptCast(rule.Action)) return;
        if (!float.IsFinite(cast.Location.X) || !float.IsFinite(cast.Location.Y) || !float.IsFinite(cast.Location.Z))
        {
            warning?.Invoke($"{rule.Name}: 无有效读条落点，跳过预测。");
            return;
        }
        var directional = IsNativeFate || IsTactical || IsCarnivale && rule.Action is 14692 or 14694 or 14893 or 14894 or 14895 or 14896 or 15325 or 18689 or 18961 or 18971 or 18984 or 18838 or 18839 or 34464 || IsBarPassage && rule.Action == 40917 || IsHeroesPretenders && rule.Action == 37467 || territory == MainScenarioQuestCatalog.ORACLE_OF_LIGHT && rule.Action == 18036
            || rule.Action is 5823 or 8265 or 8266 or 9809 or 9810 or 14196 or 15848 or 15834 or 15837
            || IsBekko && rule.Action is >= 8439 and <= 8442
            || IsOrcus && rule.Action is 27602 or 27600 or 27603 or 27601
            || IsSteel && rule.Action == 17561
            || IsTreasureMegakantha && rule.Action == 32242
            || IsTreasureBasket && rule.Action is 21724 or 21725 or 21726
            || IsTreasurePorxie && rule.Action == 22937
            || IsNativeTreasure && rule.Action is 43687 or 43688 or 21706 or 32858 or 33164 or 32342 or 32343 or 32344
            || IsShinryu && rule.Action == 8075
            || IsSeiryu && rule.Action == 14349
            || IsHades && rule.Action == 16751
            || IsWarriorOfLight && rule.Action == 20252
            || IsHydaelyn && rule.Action == 26063
            || IsZelenia && rule.Action is 43102 or 43439 or 43084 or 43085 or 43446 or 43447
            || IsValigarmanda && rule.Action is 36160 or 36156 or 36187
            || IsNecron && rule.Action is 44524 or 44525
            || IsQueenEternal && rule.Action is >= 36638 and <= 36641 or 36612
            || IsDoomtrain && rule.Action == 45618
            || IsCoilGauntlet && rule.Action is 1236 or 1237
            || IsTwintania && rule.Action == 1247
            || IsStepsOfFaith && rule.Action is 30887 or 26411 or 26401
            || IsTitanExtreme && rule.Action is 1467 or 1468 or 1475
            || IsAsura && rule.Action is 36011 or 36012 or 36013
            || territory == HardTrialCatalog.IFRIT && rule.Action == 457
            || territory == DohnMhegCatalog.TERRITORY && rule.Action is 8906 or 13547
            || territory == QitanaCatalog.TERRITORY && rule.Action is 15512 or 15520
            || territory == MalikahCatalog.TERRITORY && rule.Action is 15591 or 15599
            || territory == AmaurotCatalog.TERRITORY && rule.Action is 15575 or 17107
            || territory == CosmosCatalog.TERRITORY && rule.Action == 18209
            || territory == MatoyaCatalog.TERRITORY && rule.Action is 22809 or 22810
            || territory == SmiletonCatalog.TERRITORY && rule.Action == 27742
            || territory == LunarCatalog.TERRITORY && rule.Action == 34819
            || territory == WorqorCatalog.TERRITORY && rule.Action is 36307 or 36310 or 39074
            || territory == SkydeepCatalog.TERRITORY && rule.Action is 36679 or 36680 or 36681 or 36682
            || territory == OrigenicsCatalog.TERRITORY && rule.Action == 36367
            || territory == AlexandriaCatalog.TERRITORY && rule.Action == 39136
            || territory == StrayboroughCatalog.TERRITORY && rule.Action == 39683
            || territory == YuweyawataCatalog.TERRITORY && rule.Action is 40661 or 40662 or 40611
            || territory == GhimlytCatalog.TERRITORY && GhimlytCatalog.IsCrosshatch(rule.Action)
            || rule.Shape is FanTemplate or RayRectangleTemplate or DungeonCrossTemplate or Mechanics.PilgrimsTraverse.PtDonutSectorTemplate;
        if (directional && (cast.Rotation is not { } r || !float.IsFinite(r)))
        {
            if (_missingRotation.Add((source, cast.Action))) warning?.Invoke($"{rule.Name}: 缺少锁定施法朝向，跳过预测。");
            return;
        }
        var rotation = directional || rule.Action == 3519 ? cast.Rotation ?? actor.Rotation : 0;
        if (!fromPacket) QuestWindObserveCast(source, cast);
        if (IsDivineIntervention && rule.Action == 3993 || IsTreasureYan && rule.Action == 44269 || IsOtis && rule.Action == 38209)
        {
            if (IsDivineIntervention && _casts.TryGetValue(source, out var charge) && charge.Rule.Action == cast.Action
                && Math.Abs((charge.StartedAt - cast.StartedAt).TotalSeconds) < .25
                && Math.Abs((charge.FinishAt - cast.FinishAt).TotalSeconds) < .75)
            {
                charge.Observed |= !fromPacket;
                return;
            }
            var chargeOrigin = cast.SourcePosition ?? actor.Position;
            if ((IsTreasureYan || IsOtis && cast.SourcePosition == null) && _casts.TryGetValue(source, out var lockedRush)
                && lockedRush.Rule.Action == cast.Action
                && Math.Abs((lockedRush.StartedAt - cast.StartedAt).TotalSeconds) < .25
                && Math.Abs((lockedRush.FinishAt - cast.FinishAt).TotalSeconds) < .75)
                chargeOrigin = lockedRush.SourcePosition ?? chargeOrigin;
            var direction = cast.Location - chargeOrigin;
            var width = (IsOtis, IsTreasureYan) switch { (true, _) => 5, (_, true) => 8, _ => 4 };
            rule = rule with { Shape = RayRectangleTemplate.Symmetric(width, new Vector2(direction.X, direction.Z).Length()) };
            rotation = MathF.Atan2(direction.X, direction.Z);
            cast = cast with { Location = QuantizedDungeonPosition(chargeOrigin), SourcePosition = chargeOrigin };
        }
        var start = cast.StartedAt;
        var finish = cast.FinishAt;
        var sourcePosition = cast.SourcePosition ?? actor.Position;
        var observed = !fromPacket;
        var treasureCorrection = false;
        var fateCorrection = false;
        var tacticalCorrection = false;
        var slickshellCorrection = false;
        DungeonPendingCast? previousTacticalCast = null;
        DungeonPendingCast? previousFateCast = null;
        DungeonPendingCast? previousSleepOtisCast = null;
        if (_casts.TryGetValue(source, out var previous) && previous.Rule.Action == cast.Action
            && (eurekaFirstRaw || Math.Abs(((previous.InputFinishAt ?? previous.FinishAt) - finish).TotalSeconds) < 0.75 && Math.Abs((previous.StartedAt - start).TotalSeconds) < 0.25))
        {
            previous.Observed |= observed;
            observed = previous.Observed;
            if (IsTreasurePorxie && cast.Action == 22937 && cast.SourcePosition == null)
                sourcePosition = previous.SourcePosition ?? sourcePosition;
            if (IsBarPassage && cast.Action == 40917 && fromPacket && cast.SourcePosition == null)
                sourcePosition = previous.SourcePosition ?? sourcePosition;
            // First raw confirms packet-only origins. Later raw movement does not move a locked cast.
            var keepLockedRaw = !fromPacket && !(firstTreasureRaw && previous.TargetId != cast.TargetId);
            var samePacket = previous.Location == cast.Location && previous.Rotation == rotation && previous.TargetId == cast.TargetId
                && (!(IsTreasureYan || IsOtis && cast.Action == 38209) || previous.Rule.Shape == rule.Shape)
                && (!(IsPortrait && cast.Action == 37535 || IsTreasurePorxie && cast.Action == 22937
                    || IsOtis && cast.Action == 38209 || IsBarPassage && cast.Action == 40917)
                    || previous.SourcePosition == sourcePosition);
            if (!barP3FirstRaw && !carnivaleChargeFirstRaw && !blackRiderFirstRaw && !eurekaFirstRaw && (keepLockedRaw || samePacket)) return;
            treasureCorrection = IsNativeTreasure;
            slickshellCorrection = IsSlickshell;
            if (!carnivaleChargeFirstRaw && !blackRiderFirstRaw && !eurekaFirstRaw)
            {
                start = previous.StartedAt;
                finish = previous.FinishAt;
            }
            InnocenceFinish(previous);
            HadesFinish(previous);
            WolFinish(previous);
            HydaFinish(source, previous.Rule.Action);
            AsuraFinish(source, previous.Rule.Action);
            ZeleniaFinish(source, previous.Rule.Action);
            ValigFinish(source, previous.Rule.Action);
            ArkFinish(source, previous.Rule.Action, previous.TargetId);
            EnuoFinish(previous);
            NecronFinish(previous);
            QueenFinish(previous);
            DoomCorrectTarget(previous, cast.TargetId);
            fateCorrection = IsNativeFate;
            tacticalCorrection = IsTactical;
            previousTacticalCast = tacticalCorrection ? previous : null;
            previousFateCast = fateCorrection ? previous : null;
            previousSleepOtisCast = IsOtis ? previous : null;
            FateFinish(previous, correction: true);
        }
        var finishEventBoundary = start;
        if (recoveredCarnivaleCast != null) finishEventBoundary = recoveredCarnivaleCast.FinishEventBoundary;
        else if (acceptedGeneration && !nativeReplacement) finishEventBoundary = accepted!.FinishEventBoundary;
        else if (!fromPacket && questRawChanged) finishEventBoundary = now;
        DateTime? inputFinishAt = null;
        if (UsesDeepDungeonCastIdentity)
        {
            inputFinishAt = cast.FinishAt;
            if (acceptedGeneration && !nativeReplacement && !eurekaFirstRaw)
                inputFinishAt = accepted!.InputFinishAt ?? accepted.FinishAt;
        }
        if (UsesDeepDungeonCastIdentity && !fromPacket && (!acceptedGeneration || nativeReplacement || blackRiderFirstRaw || eurekaFirstRaw))
        {
            // A newly observed NPC cast caps elapsed time at its total, leaving the
            // source's NPC finish delay even when the recorded deadline is already past.
            var total = cast.TotalTime ?? (float)Math.Max(0, (cast.FinishAt - cast.StartedAt).TotalSeconds - .3);
            var minimumFinish = now.AddSeconds((total + .3f) - total);
            if (finish < minimumFinish) finish = minimumFinish;
        }
        if (IsCarnivale && !fromPacket && !_casts.ContainsKey(source)
            && (CarnivaleEyeBootstrap(cast.Action)
                    || cast.Action == 14710 && carnivaleLoaded.Any(m => m.VoltageNeedsBootstrap)
                    || CarnivaleEarlyBootstrap(cast.Action)
                    || CarnivaleBoomAction(cast.Action) && carnivaleLoaded.Any(m => m.SapNeedsBootstrap)
                || CarnivaleBehemothAction(cast.Action) && carnivaleLoaded.Any(m => m.Started && m.Primary.Actor.Oid == 0x2732)
                || CarnivalePitAction(cast.Action) && carnivaleLoaded.Any(m => m.PitNeedsBootstrap)
                    || CarnivaleAzulmagiaAction(cast.Action) && carnivaleLoaded.Any(m => m.AzulmagiaNeedsBootstrap)
                    || CarnivalePapaAction(cast.Action) && carnivaleLoaded.Any(m => m.PapaNeedsBootstrap)
                    || CarnivaleSnorterAction(cast.Action) && carnivaleLoaded.Any(m => m.Snorter is { NeedsBootstrap: true })
                    || CarnivaleDraugarAction(cast.Action) && carnivaleLoaded.Any(m => m.Draugar is { NeedsBootstrap: true })
                    || CarnivaleRedAction(cast.Action) && carnivaleLoaded.Any(m => m.Red is { NeedsBootstrap: true })
                    || CarnivaleSiegfriedAction(cast.Action) && carnivaleLoaded.Any(m => m.Siegfried is { NeedsBootstrap: true })
                    || CarnivaleGogoAction(cast.Action) && carnivaleLoaded.Any(m => m.Gogo is { NeedsBootstrap: true })
                    || CarnivaleGoldorAction(cast.Action) && carnivaleLoaded.Any(m => m.Goldor is { NeedsBootstrap: true }))
            && finish < now.AddSeconds((double).3f))
            finish = now.AddSeconds((double).3f);
        _casts[source] = new(source, rule, cast.Location, rotation, start, finish, cast.TargetId)
        {
            Observed = observed,
            FinishEventBoundary = finishEventBoundary,
            InputFinishAt = inputFinishAt,
            SourcePosition = sourcePosition,
            UntilObservedEnd = UsesObservedCastEnd || territory is TroiaCatalog.TERRITORY or LapisCatalog.TERRITORY or AetherfontCatalog.TERRITORY or LunarCatalog.TERRITORY or IhuykatumuCatalog.TERRITORY or WorqorCatalog.TERRITORY or SkydeepCatalog.TERRITORY or VanguardCatalog.TERRITORY or OrigenicsCatalog.TERRITORY or AlexandriaCatalog.TERRITORY or TenderValleyCatalog.TERRITORY or StrayboroughCatalog.TERRITORY or YuweyawataCatalog.TERRITORY or UnderkeepCatalog.TERRITORY or MesoCatalog.TERRITORY or MistwakeCatalog.TERRITORY or ClyteumCatalog.TERRITORY,
        };
        CarnivaleCastStarted(_casts[source], acceptedGeneration && !nativeReplacement, carnivaleChargeFirstRaw);
        HeavenOnHighCast(_casts[source], acceptedGeneration && !nativeReplacement);
        EurekaCast(_casts[source]);
        KenkoCast(_casts[source], acceptedGeneration && !nativeReplacement, kenkoFirstRawBaits);
        NybethCast(_casts[source], acceptedGeneration && !nativeReplacement);
        BlackRiderCast(_casts[source], acceptedGeneration && !nativeReplacement, blackRiderFirstRaw, !fromPacket);
        if (IsCarnivale) carnivaleUnobservedEnds.Remove(source);
        PortraitCastStarted(_casts[source]);
        TreasureCastStarted(_casts[source], treasureCorrection, firstTreasureRaw);
        SamuraiCast(_casts[source]);
        FateCast(_casts[source], fateCorrection, previousFateCast);
        TacticalCast(_casts[source], tacticalCorrection, previousTacticalCast);
        SleepOtisCast(_casts[source], previousSleepOtisCast);
        SlickshellCast(_casts[source], slickshellCorrection);
        HopeCast(_casts[source]);
        JobQuestPartyCast(_casts[source]);
        HeroesPretendersCast(_casts[source]);
        IseultalonCast(_casts[source]);
        AlphaCast(_casts[source]);
        DikaiosyneCast(_casts[source]);
        BarP3Cast(_casts[source], cast.TotalTime, !fromPacket, cast.StartedAt);
        BekkoCast(_casts[source]);
        OrcusCast(_casts[source]);
        SageLifeCast(_casts[source]);
        SteelCast(_casts[source]);
        QuestWindCast(_casts[source]);
        MainScenarioCastStarted(_casts[source]);
        RhalgrCast(_casts[source]);
        EndwalkerQuestCast(_casts[source]);
        // Clear the replaced record's tombstone only after a valid new cast has been committed.
        if (nativeReplacement || questRawChanged || (IsNativeFate || IsTactical) && rawNativeChanged) _finished.Remove(source);
        if (rule.Action is 45742 or 45997 or 29620 or 30205 or 31837
            || territory == QitanaCatalog.TERRITORY && rule.Action is 15497 or 15505 or 15513) delayedDamage[source] = _casts[source];
        if (rule.Action == 29268) incendiarySupport = _casts[source] with { FinishAt = finish.AddSeconds(1) };
        if (territory == HeavenswardIntroCatalog.REGULA && rule.Action == 6032 && !regulaDetonating.Contains(source))
            regulaDetonating.Add(source);
        if (territory == HeavenswardIntroCatalog.REGULA_VITH && rule.Action == 4005) questTerminusActive = true;
        WandererCast(_casts[source]);
        AmdaporCast(_casts[source]);
        SnowcloakCast(_casts[source]);
        KeeperCast(_casts[source]);
        SohmCast(_casts[source]);
        AeryCast(_casts[source]);
        GubalCast(_casts[source]);
        FractalCast(_casts[source]);
        ArboretumCast(_casts[source]);
        AntitowerCast(_casts[source]);
        SohrCast(_casts[source]);
        XelCast(_casts[source]);
        SirenCast(_casts[source]);
        ShisuiCast(_casts[source]);
        BardamCast(_casts[source]);
        DomaCast(_casts[source]);
        AlaMhigoCast(_casts[source]);
        BurnCast(_casts[source]);
        GhimlytCast(_casts[source]);
        HolminsterCast(_casts[source]);
        DohnCast(_casts[source]);
        QitanaCast(_casts[source]);
        MalikahCast(_casts[source]);
        GulgCast(_casts[source]);
        AmaurotCast(_casts[source]);
        AnyderCast(_casts[source]);
        CosmosCast(_casts[source]);
        AnamnesisCast(_casts[source]);
        HeroesCast(_casts[source]);
        MatoyaCast(_casts[source]);
        PaglthanCast(_casts[source]);
        ZotCast(_casts[source]);
        BabilCast(_casts[source]);
        VanaspatiCast(_casts[source]);
        KtisisCast(_casts[source]);
        AitiascopeCast(_casts[source]);
        DeadEndsCast(_casts[source]);
        StigmaCast(_casts[source]);
        AlzadaalCast(_casts[source]);
        TroiaCast(_casts[source]);
        LapisCast(_casts[source]);
        AetherfontCast(_casts[source]);
        LunarCast(_casts[source], now);
        IhuyCast(_casts[source]);
        WorqorCast(_casts[source]);
        SkydeepCast(_casts[source], now);
        VanguardCast(_casts[source]);
        OrigenicsCast(_casts[source], now);
        AlexCast(_casts[source]);
        TenderCast(_casts[source]);
        StrayCast(_casts[source]);
        YuweCast(_casts[source]);
        UnderCast(_casts[source]);
        MesoCast(_casts[source]);
        MistCast(_casts[source]);
        ClyCast(_casts[source]);
        ArrTrialCast(_casts[source]);
        PortaCast(_casts[source]);
        HardTrialCast(_casts[source]);
        ThornCast(_casts[source]);
        LakshmiCast(_casts[source]);
        ShinryuCast(_casts[source]);
        TsukuyomiCast(_casts[source]);
        YojimboCast(_casts[source]);
        ByakkoCast(_casts[source]);
        SeiryuCast(_casts[source]);
        TitaniaCast(_casts[source]);
        InnocenceCast(_casts[source]);
        HadesCast(_casts[source]);
        WolCast(_casts[source]);
        HydaCast(_casts[source]);
        AsuraCast(_casts[source]);
        ZeleniaCast(_casts[source]);
        ValigCast(_casts[source]);
        ArkCast(_casts[source]);
        EnuoCast(_casts[source]);
        NecronCast(_casts[source]);
        QueenCast(_casts[source]);
        ZoraalCast(_casts[source]);
        DoomCast(_casts[source]);
        CoilCast(_casts[source]);
        TwinCast(_casts[source]);
        LabyrinthCast(_casts[source]);
        SyrcusCast(_casts[source]);
        DarknessCast(_casts[source]);
        ArrQuestCast(_casts[source]);
        UltimaCast(_casts[source]);
        GarudaExCast(_casts[source]);
        TitanExCast(_casts[source]);
        IfritExCast(_casts[source]);
        if (rule.Action is 40599 or 29586 or 29286)
        {
            sequences.RemoveAll(s => s.Source == source);
            sequences.Add(new(_casts[source]));
        }
    }

    private void RecordCastFinish(ulong source, uint action, DateTime startedAt, DateTime endedAt)
    {
        // Several distinct casts can end in the same feed frame. Permit a new
        // action at that boundary without allowing any already closed action back.
        var actions = _finished.TryGetValue(source, out var previous) && previous.EndedAt == endedAt
            ? previous.ActionsAtEnd : [];
        actions.Add(action);
        _finished[source] = (action, startedAt, endedAt, actions);
    }

    public void Finish(ulong source, uint action = 0, DateTime? endedAt = null, bool completed = false)
    {
        // These components may activate after a raw cast was first seen. A real
        // end before activation must prevent the unchanged snapshot being claimed.
        if (IsCarnivale && !_casts.ContainsKey(source)
            && _actors.GetValueOrDefault(source)?.Cast is { } unclaimed
            && (action == 0 || action == unclaimed.Action)
            && (CarnivaleEyeAction(unclaimed.Action) || unclaimed.Action == 14710 || CarnivaleEarlyAction(unclaimed.Action) || CarnivaleBoomAction(unclaimed.Action) || CarnivaleBehemothAction(unclaimed.Action)
                || CarnivalePitAction(unclaimed.Action) || CarnivaleAzulmagiaAction(unclaimed.Action) || CarnivalePapaAction(unclaimed.Action) || CarnivaleSnorterAction(unclaimed.Action) || CarnivaleDraugarAction(unclaimed.Action) || CarnivaleRedAction(unclaimed.Action) || CarnivaleSiegfriedAction(unclaimed.Action) || CarnivaleGogoAction(unclaimed.Action) || CarnivaleGoldorAction(unclaimed.Action))
            && (endedAt ?? _now) >= unclaimed.StartedAt)
            RecordCastFinish(source, unclaimed.Action, unclaimed.StartedAt, endedAt ?? _now);
        if (IsCarnivale && carnivaleUnobservedEnds.TryGetValue(source, out var unobserved)
            && (action == 0 || action == unobserved.Rule.Action)
            && (endedAt ?? _now) >= unobserved.FinishEventBoundary)
            carnivaleUnobservedEnds.Remove(source);
        if (IsMalboro) { MalboroFinish(source, action); return; }
        if ((IsPalaceSerpent || IsSteel && source == PrimaryId) && _actors.GetValueOrDefault(source) is { Cast: { } observed } serpent
            && (action == 0 || action == observed.Action) && (endedAt ?? _now) >= observed.StartedAt)
        {
            _actors[source] = serpent with { Cast = null };
            RecordCastFinish(source, observed.Action, observed.StartedAt, endedAt ?? _now);
        }
        if (_casts.TryGetValue(source, out var cast) && (action == 0 || action == cast.Rule.Action
            || IsCarnivale && (cast.Rule.Action is 14888 or 14884 or 14871 or 14876 or 14906 or 14907 or 19059 or 19064 or 19067 or 18696 or 18895 or 18971 or 18984 or 18836 or 18837 or 18838 or 18839 or 18841 or 18897 or 23101 or 23105 or 23131 or 23122 or 20055 or 23123 or 23129 or 34448
                || carnivaleLoaded.Any(m => m.Started && m.Primary.Actor.Oid == 0x272C) && cast.Rule.Action is 14747 or 14706
                || cast.Rule.Action == 34449 && carnivaleLoaded.Any(m => m.Started && m.Primary.Actor.Oid == 0x3FA5))))
        {
            var predictedEnd = cast.FinishAt.AddSeconds(-0.3);
            var time = endedAt ?? (_now < predictedEnd ? _now : predictedEnd);
            if (time < cast.StartedAt) return;
            // A newer observed native generation cannot be ended by an older queued event.
            if ((IsCarnivale || UsesDeepDungeonCastIdentity) && endedAt is { } eventTime && eventTime < cast.FinishEventBoundary) return;
            if (IsNativeTreasure)
            {
                TreasureSequenceFinish(cast, _now);
                TreasureGymnasionFinish(cast, _now);
                TreasureNarkissosFinish(cast);
                TreasureBitterFinish(cast);
                TreasureFortressFinish(cast);
                TreasureHexFinish(cast);
                TreasureIceFinish(cast, _now);
                TreasureCreatedCircleFinish(cast);
                TreasureVaultGroupFinish(cast);
                TreasureCastBaitFinish(cast);
                TreasureBeamFinish(cast);
                TreasurePlayerGazeFinish(cast);
                TreasureWallKnockbackFinish(cast);
                TreasureBuffetFinish(cast);
                if (cast.Rule.Action == 13443) treasureHydrospheres.Clear();
                treasureCastBaitCurrent.Remove(source);
                treasureLightOrder.Remove(source);
                treasureCurrentCasts.Remove(source);
                treasureObservedCasts.Remove(source);
            }
            CarnivaleCastFinished(cast);
            HeavenOnHighFinish(cast);
            EurekaFinish(cast, completed);
            KenkoFinish(cast);
            NybethFinish(cast);
            GodmotherFinish(cast);
            BlackRiderFinish(cast);
            PortraitFinish(cast);
            FateFinish(cast);
            TacticalFinish(cast);
            SleepOtisFinish(cast);
            HopeFinish(cast);
            JobQuestPartyFinish(cast);
            BekkoFinish(cast);
            OrcusFinish(cast);
            SageLifeFinish(cast);
            SteelFinish(cast);
            QuestWindFinish(cast);
            HeroesPretendersFinish(cast);
            IseultalonFinish(cast);
            AlphaFinish(cast);
            DikaiosyneFinish(cast);
            BarP3Finish(cast);
            RhalgrFinish(cast);
            EndwalkerQuestFinish(cast);
            arashiStarted.Remove(source);
            HydaFinish(source, cast.Rule.Action);
            AsuraFinish(source, cast.Rule.Action);
            ZeleniaFinish(source, cast.Rule.Action);
            ValigFinish(source, cast.Rule.Action);
            ArkFinish(source, cast.Rule.Action, cast.TargetId);
            EnuoFinish(cast);
            NecronFinish(cast);
            QueenFinish(cast);
            ZoraalFinish(cast);
            DoomFinish(cast);
            if (cast.Rule.Action is 40599 or 40600 or 40601 or 29586 or 29591 or 29286 or 29287 or 29288)
            {
                var sequence = sequences.FirstOrDefault(s => s.Matches(cast));
                if (sequence != null)
                {
                    if (completed && Active != null)
                    {
                        sequence.Advance(Active, endedAt ?? _now);
                        if (sequence.Finished) sequences.Remove(sequence);
                    }
                    else sequences.Remove(sequence);
                }
            }
            _casts.Remove(source);
            mainScenarioCastOrder.Remove(source);
            regulaDetonating.Remove(source);
            SnowcloakFinish(cast, completed, endedAt ?? _now);
            KeeperFinish(cast, completed);
            AeryFinish(cast, completed);
            GubalFinish(cast, completed);
            ResearchStarFinish(cast, completed);
            ArboretumFinish(cast, completed, endedAt ?? _now);
            SohrFinish(cast, completed, endedAt ?? _now);
            XelFinish(cast, completed);
            BaelFinish(cast, completed);
            SirenFinish(cast, completed);
            ShisuiFinish(cast, completed);
            BardamFinish(cast, completed, endedAt ?? _now);
            DomaFinish(cast, completed);
            AlaMhigoFinish(cast, completed);
            BurnFinish(cast, completed);
            GhimlytFinish(cast, completed);
            HolminsterFinish(cast, completed, endedAt ?? _now);
            DohnFinish(cast, completed);
            QitanaFinish(cast, completed);
            MalikahFinish(cast, completed);
            GulgFinish(cast, completed);
            AmaurotFinish(cast, completed);
            AnyderFinish(cast, completed);
            CosmosFinish(cast, completed);
            AnamnesisFinish(cast, completed);
            HeroesFinish(cast, completed);
            MatoyaFinish(cast, endedAt ?? _now);
            ZotFinish(cast);
            BabilFinish(cast);
            VanaspatiFinish(cast);
            KtisisFinish(cast);
            AitiascopeFinish(cast, endedAt ?? _now);
            DeadEndsFinish(cast, endedAt ?? _now);
            if (territory == StigmaCatalog.TERRITORY) stigmaCharges.Remove(source);
            AlzadaalFinish(cast);
            TroiaFinish(cast);
            LapisFinish(cast, completed, endedAt ?? _now);
            AetherfontFinish(cast);
            LunarFinish(cast);
            IhuyFinish(cast, endedAt ?? _now);
            WorqorFinish(cast);
            SkydeepFinish(cast, endedAt ?? _now);
            VanguardFinish(cast);
            OrigenicsFinish(cast);
            AlexFinish(cast, endedAt ?? _now);
            TenderFinish(cast);
            StrayFinish(cast);
            YuweFinish(cast);
            UnderFinish(cast);
            MesoFinish(cast);
            MistFinish(cast);
            ClyFinish(cast);
            ArrTrialFinish(cast);
            LabyrinthFinish(cast);
            SyrcusFinish(cast);
            ArrQuestFinish(cast, endedAt ?? _now);
            UltimaFinish(cast.Rule.Action);
            GarudaExFinish(cast, endedAt ?? _now);
            TitanExFinish(cast, endedAt ?? _now);
            PortaFinish(cast);
            HardTrialFinish(cast);
            ThornFinish(cast, endedAt ?? _now);
            LakshmiFinish(cast);
            ShinryuFinish(cast);
            TsukuyomiFinish(cast);
            YojimboFinish(cast);
            ByakkoFinish(cast);
            SuzakuFinish(cast);
            SeiryuFinish(cast);
            TitaniaFinish(cast);
            InnocenceFinish(cast);
            HadesFinish(cast);
            WolFinish(cast);
            if (!completed && cast.Rule.Action == 5557) antParryPredictions.Remove(source);
            if (!completed && cast.Rule.Action == 4999) meteorImpacts.Remove(source);
            if (!completed) delayedDamage.Remove(source);
            if (!completed && cast.Rule.Action == 29268) incendiarySupport = null;
            if (cast.Rule.Action == 942 || cast.Rule.Action == 950 && !completed) goobbuesGrief = null;
            losSnapshots.Remove(source);
            RecordCastFinish(source, cast.Rule.Action, cast.StartedAt, time);
        }
    }

    public void Effect(ulong source, uint action, uint sequence, DateTime now, ulong target = 0, Vector3? location = null, DateTime? occurredAt = null,
        float? sourceRotation = null, bool? hasTargets = null, Vector3? sourcePosition = null)
    {
        _now = now;
        if (UsesEurekaNativeEffect(action))
        {
            EurekaEffect(source, action, target, location, now);
            return;
        }
        if (Active?.BossOid == 0x477A && action == 42229 && target != 0)
            StatusChanged(target, QarnCatalog.DOOM, true, now);
        if (territory == BurnCatalog.TERRITORY && action == 12697) burnSpreads.Remove(target);
        CarnivalePapaEffect(source, action);
        CarnivaleSnorterEffect(source, action);
        CarnivaleRedEffect(source, action);
        CarnivaleSiegfriedEffect(source, action);
        CarnivaleGogoEffect(source, action);
        CarnivaleGoldorEffect(source, action);
        FateEffect(source, action, sequence, target, occurredAt ?? now);
        TacticalEffect(source, action, sequence, target, occurredAt ?? now, location);
        if (UsesBarP2NativeEffect(action))
        {
            BarP2Effect(source, action, sequence, occurredAt ?? now);
            return;
        }
        if (UsesCarnivaleNativeEffect(action))
        {
            // The adapter already excludes flattened legacy effects. Each native
            // packet is an event, including separate packets with sequence zero.
            CarnivaleClawEffect(source, action, sequence, target, location, now, occurredAt ?? now);
            return;
        }
        if (UsesPalaceEarlyPairNativeEffect(action))
        {
            TrialVoidzoneEffect(action, target, location, now);
            return;
        }
        if (UsesGudannaNativeEffect(action))
        {
            GudannaEffect(action);
            return;
        }
        if (UsesGodmotherNativeEffect(action))
        {
            if (Active == null) return;
            // Native capture emits once per complete packet; the adapter excludes
            // flattened legacy targets. Source counters consume every packet.
            ++godmotherBombIndex;
            return;
        }
        if (UsesHeavenOnHighNativeEffect(action))
        {
            if (IsKenko)
            {
                if (sequence == 0 || _effects.TryAdd((source, action, sequence), now))
                    KenkoEffect(action, target, now);
            }
            else HeavenOnHighSpawnEffect(source, sequence);
            return;
        }
        if (!_effects.TryAdd((source, action, sequence), now)) return;
        HeroesPretendersEffect(source, action, now);
        if (IsIseultalon && action == 15083) thunderboltSpreads.Clear();
        if (IsIseultalon && action == 14604) deathRayActivation = null;
        PortraitEffect(source, action, sourcePosition);
        FallenEffect(action, target);
        GuildivainEffect(action, target);
        SteelEffect(source, action);
        if (territory == JobQuestCatalog.ORPHANS_AND_BROKEN_BLADE && action == 9298)
            jobQuestStacks.Clear();
        if (territory == JobQuestCatalog.FACE_OF_TRUE_EVIL && action == 8419)
        {
            arashiActivation = null;
            arashiResolvedAt = now;
        }
        PalaceSerpentEffect(action);
        AlzadaalEffect(action);
        TroiaEffect(action, occurredAt ?? now);
        LapisEffect(source, action, target, occurredAt ?? now);
        AetherfontEffect(action, target);
        LunarEffect(source, action, target, occurredAt ?? now);
        IhuyEffect(action, target, location, occurredAt ?? now);
        WorqorEffect(source, action, target, occurredAt ?? now);
        SkydeepEffect(action);
        VanguardEffect(source, action, target, occurredAt ?? now);
        OrigenicsEffect(source, action, target, location, occurredAt ?? now);
        AlexEffect(source, action, target, occurredAt ?? now);
        TenderEffect(action, target);
        StrayEffect(source, action, target, location, occurredAt ?? now);
        YuweEffect(action);
        UnderEffect(source, action, target);
        MesoEffect(source, action, occurredAt ?? now, hasTargets ?? target is not (0 or 0xE0000000), sourcePosition, sourceRotation);
        MistEffect(action, target);
        ClyEffect(action, target);
        KeeperEffect(action, occurredAt ?? now, target, location);
        SohmEffect(action, target);
        AeryEffect(action);
        VaultEffect(action, target, location, occurredAt ?? now);
        GubalEffect(action, target);
        ResearchEffect(source, action);
        ResearchLateEffect(source, action, target, occurredAt ?? now);
        FractalEffect(source, action);
        ArboretumEffect(source, action, target, location, sourceRotation, occurredAt ?? now);
        ArrTrialEffect(action);
        WhorlEffect(action, occurredAt ?? now);
        SusanoEffect(action, target);
        LakshmiEffect(action);
        ShinryuEffect(source, action, target, occurredAt ?? now, sourcePosition);
        TsukuyomiEffect(source, action, target, occurredAt ?? now);
        YojimboEffect(action);
        SeiryuEffect(source, action, target, occurredAt ?? now);
        TitaniaEffect(action, target);
        InnocenceEffect(source, action, target, now);
        HadesEffect(source, action);
        WolEffect(source, action, target, now);
        HydaEffect(source, action);
        AsuraEffect(action);
        ZeleniaEffect(source, action);
        ValigEffect(source, action, target, occurredAt ?? now);
        ArkEffect(source, action, target, location, now);
        EnuoEffect(source, action, target, location);
        NecronEffect(action);
        QueenEffect(source, action);
        ZoraalEffect(source, action, target);
        DoomEffect(source, action);
        CoilEffect(action, target, location);
        CaduceusEffect(source, action, target, location);
        TwinEffect(source, action, target, location);
        if (IsCoilGauntlet && gauntletStage >= 5 && action == 1258) ++gauntletEmergencyHits;
        RhalgrEffect(source, action);
        if (TreasureVoidzoneAction == 0 || action != TreasureVoidzoneAction)
            TrialVoidzoneEffect(action, target, location, IsOrcus || IsRhalgr || IsSlickshell ? now : occurredAt ?? now);
        AntitowerEffect(source, action, target, sourceRotation);
        SohrEffect(source, action, target, occurredAt ?? now);
        XelEffect(action, target);
        BaelEffect(action, target);
        SirenEffect(source, action, target, occurredAt ?? now, sourceRotation);
        ShisuiEffect(source, action, target, sequence, location, occurredAt ?? now);
        DarknessEffect(source, action, target, location, occurredAt ?? now);
        StepsEffect(source, action, occurredAt ?? now);
        UltimaEffect(source, action, target);
        GarudaExEffect(action);
        TitanExEffect(action, target, occurredAt ?? now);
        IfritExEffect(action, occurredAt ?? now);
        BardamEffect(action, target);
        DomaEffect(action, target);
        AlaMhigoEffect(source, action, target, sequence, location, occurredAt ?? now);
        SkallaEffect(source, action);
        BurnEffect(source, action, target, occurredAt ?? now);
        GhimlytEffect(source, action, occurredAt ?? now, location);
        HolminsterEffect(source, action, target, location, sourceRotation, occurredAt ?? now);
        DohnEffect(source, action);
        QitanaEffect(source, action, target, location, occurredAt ?? now);
        MalikahEffect(action, occurredAt ?? now);
        GulgEffect(action, occurredAt ?? now);
        AmaurotEffect(source, action, location, sourceRotation, occurredAt ?? now);
        TwinningEffect(source, action, location);
        AnyderEffect(source, action, target, occurredAt ?? now);
        CosmosEffect(source, action, target, location, occurredAt ?? now);
        AnamnesisEffect(source, action, target, occurredAt ?? now);
        HeroesEffect(action, target);
        MatoyaEffect(source, action, target);
        PaglthanEffect(source, action, target);
        ZotEffect(action);
        BabilEffect(source, action, target, location, occurredAt ?? now);
        VanaspatiEffect(action, occurredAt ?? now);
        KtisisEffect(action, target, occurredAt ?? now);
        AitiascopeEffect(source, action);
        DeadEndsEffect(source, action, target, location, occurredAt ?? now);
        if (delayedDamage.TryGetValue(source, out var delayed) && delayed.Rule.Action == action)
            delayedDamage.Remove(source);
        if (action == 29269) incendiarySupport = null;
        PraetoriumEffect(action);
        if (Active?.BossOid == 0x41D && source == PrimaryId && action == 943) bavaroisKiter = 0;
        if (Active?.BossOid == 0x41C && action == 942) goobbuesGrief = null;
        if (Active?.BossOid == 0x8FB && action == 1500)
        {
            resolvedCorruptingBursts.UnionWith(corruptingBursts.Keys);
            corruptingBursts.Clear();
        }
        CuttersCryEffect(source, action, now);
        StoneVigilEffect(source, action, now);
        if (action == 3079) { snowDrift = null; snowMoveAnchor = null; }
        if (action == 29601) frozenPillars.Clear();
        if (_casts.TryGetValue(source, out var cast) && cast.Rule.Action == action) cast.EffectSeen = true;
    }

    public void Animation(ulong source, uint state, DateTime? timestamp = null)
    {
        TitaniaAnimation(source, state, timestamp.HasValue);
        HadesAnimation(source, state, timestamp.HasValue);
        ShinryuAnimation(source, state, timestamp.HasValue);
        SnowcloakAnimation(source, state);
        GubalAnimation(source, state);
        ResearchTrashAnimation(source, state);
        ArboretumAnimation(source, state);
        SohrAnimation(source, state);
        XelAnimation(source, state);
        ShisuiAnimation(source, state);
        AlaMhigoAnimation(source, state);
        GhimlytAnimation(source, state, timestamp ?? _now);
        DohnAnimation(source, state, timestamp ?? _now, timestamp.HasValue);
        QitanaAnimation(source, state, timestamp ?? _now, timestamp.HasValue);
        GulgAnimation(source, state, timestamp ?? _now, timestamp.HasValue);
        AmaurotAnimation(source, state, timestamp ?? _now, timestamp.HasValue);
        AnyderAnimation(source, state, timestamp ?? _now);
        AnamnesisAnimation(source, state, timestamp ?? _now, timestamp.HasValue);
        HeroesAnimation(source, state);
        if (Active?.BossOid == 0x1A1 && _actors.TryGetValue(source, out var actor) && actor.Oid is >= 0x1E8615 and <= 0x1E8618)
            _ripples[source] = state;
    }

    public void StatusChanged(ulong target, uint status, bool present, DateTime timestamp, ushort param = 0, DateTime? expiresAt = null, ulong sourceId = 0)
    {
        CarnivaleEyeStatus(target, status, present);
        if (Active == null) return;
        var key = (target, status);
        if (!statusChanges.TryGetValue(key, out var previous) || timestamp >= previous.ChangedAt)
        {
            statusChanges[key] = (present, timestamp, param);
            ValigStatus(target, status, present, param, expiresAt ?? default, sourceId, at: timestamp);
            PharosStatus(target, status, present, param, timestamp);
            GraoullyStatus(target, status, present, param);
            ResearchStatus(target, status, present, timestamp);
            FractalStatus(target, status, present, timestamp);
            AntitowerStatus(target, status, present);
            BaelStatus(target, status, present, timestamp, expiresAt);
            IoStatus(target, status, present, expiresAt);
            if (IsDaivadipa)
                daivadipaStatuses.Status(target, status, present, timestamp, param, expiresAt, sourceId);
            if (IsFormidable)
            {
                formidableStatuses.Changed = FormidableStatusObserved;
                formidableStatuses.Status(target, status, present, timestamp, param, expiresAt, sourceId);
            }
            TreasureMolterStatus(target, status, present, timestamp, param, expiresAt, sourceId);
            if (IsTreasureNarkissos)
                treasurePlayerStatuses.Status(target, status, present, timestamp, param, expiresAt, sourceId);
            TreasureHexStatus(target, status, present, timestamp, param, expiresAt, sourceId);
            SirenStatus(target, status, present, expiresAt);
            DomaStatus(target, status, present, expiresAt);
            BurnStatus(target, status, present, param);
            HolminsterStatus(target, status, present);
            HeroesStatus(target, status, present, param, timestamp);
            KtisisStatus(target, status, present);
            DeadEndsStatus(target, status, present, timestamp);
            StigmaStatus(target, status, present, expiresAt);
            AlzadaalStatus(target, status, present, param, timestamp);
            TroiaStatus(target, status, present);
            LapisStatus(target, status, present, expiresAt);
            LunarStatus(target, status, present);
            VanguardStatus(target, status, present, expiresAt);
            StrayStatus(target, status, present, param);
            YuweStatus(target, status, present);
            MesoStatus(target, status, present, param);
            MistStatus(target, status, present, timestamp);
            ClyStatus(target, status, present, expiresAt);
            WhorlStatus(target, status, present, timestamp, expiresAt);
            CarnivaleChargeStatus(target, status, present, expiresAt);
            PalaceEarlyPairStatus(target, status, present, expiresAt, sourceId);
            CarnivaleBlindStatus(target, status, present);
            ShinryuStatus(target, status, present);
            TsukuyomiStatus(target, status, present, param, expiresAt);
            WolStatus(target, status, present, timestamp, expiresAt);
            QueenStatus(target, status, present, expiresAt, param, sourceId);
            DoomStatus(target, status, present, param, expiresAt, sourceId);
            CoilStatus(target, status, present, expiresAt, param, sourceId);
            TwinStatus(target, status, present, expiresAt);
            if (IsTitanExtreme && status == 292 && present) titanExFetters.Remove(target);
            if (status == 377) IfritExStatus(target, present, param);
            if (status == 2273) HydaStatus(target, present, param, timestamp, expiresAt ?? default, sourceId);
            if (territory == PaglthanCatalog.TERRITORY && status == 2574) PaglthanStatus(target, present, timestamp);
            if (Active.BossOid == 0x39CA && status == 3324 && present) sableTarget = target;
        }
    }

    private bool HasStatus(DungeonActor actor, uint status)
        => statusChanges.TryGetValue((actor.Id, status), out var change) && _now < change.ChangedAt.AddSeconds(0.5)
            ? change.Present
            : actor.Statuses?.Contains(status) == true;

    public void RemoveActor(ulong source, DateTime? timestamp = null, bool destroyed = true)
    {
        if (UsesDeepDungeonCastIdentity && _casts.TryGetValue(source, out var hohCast))
            Finish(source, hohCast.Rule.Action, timestamp ?? _now);
        if (UsesDeepDungeonCastIdentity && destroyed) treasureObservedCasts.Remove(source);
        if (IsHeavenOnHigh && destroyed) HeavenOnHighSpawnRemoved(source);
        if (IsPalaceManagedBoss && destroyed) PalaceGuideRemoved(source);
        if (IsGodmother && destroyed) GodmotherRemoved(source);
        if (destroyed) KenkoRemoved(source);
        if (destroyed)
        {
            noviceCompleted.Remove(source);
            noviceTank04Known.Remove(source);
            noviceTank04Pending.Remove(source);
        }
        if (destroyed) FateRemoved(source);
        if (destroyed) TacticalRemoved(source);
        if (destroyed) SleepOtisRemoved(source);
        if (destroyed) SlickshellRemoved(source);
        if (destroyed) maxQuestCompleted.Remove(source);
        if (destroyed && IsHopeOnTheWaves) hopeActors.Remove(source);
        fateObservedCasts.Remove(source);
        if (destroyed) TreasureYanRemoved(source);
        if (destroyed) TreasureHexRemoved(source, timestamp ?? _now);
        if (destroyed) TreasureRoomDestroyed(source);
        if (destroyed) TreasureCreatedCircleDestroyed(source);
        if (destroyed) TreasureRotationDestroyed(source);
        if (destroyed) TreasureBeamDestroyed(source);
        if (destroyed) TreasurePlayerGazeRemoved(source);
        if (destroyed && IsTreasureNarkissos) treasurePlayerStatuses.Remove(source, timestamp ?? _now);
        if (destroyed) TreasureMolterRemoved(source);
        if (IsNativeTreasure)
        {
            if (destroyed) TreasureBaitRemoved(source);
            if (destroyed && treasureCachedBoss?.Id == source) treasureCachedBossRemoved = true;
            if (destroyed) TreasureCastBaitRemoved(source);
            if (destroyed) TreasureBuffetRemoved(source);
            if (destroyed) treasureHydrosphereActors.Remove(source);
            treasureLightOrder.Remove(source);
            treasureCurrentCasts.Remove(source);
            treasureObservedCasts.Remove(source);
        }
        if (destroyed) GuildivainRemoved(source);
        if (destroyed) NodeRemoved(source);
        if (destroyed) SteelRemoved(source);
        if (destroyed) IoRemoved(source);
        if (destroyed) QuestWindRemoved(source);
        if (destroyed) RhalgrRemoved(source);
        if (destroyed) EndwalkerQuestRemoved(source);
        mainScenarioCastOrder.Remove(source);
        if (IsViper && source == viperGowrow && destroyed) viperGowrowRemoved = true;
        if (IsViper && source == viperPrimary && destroyed) viperPrimary = 0;
        if (IsPortrait && _casts.TryGetValue(source, out var portraitCast))
            PortraitFinish(portraitCast);
        portraitFlowers.Remove(source);
        if (destroyed) portraitFlowerEvents.Remove(source);
        portraitStarted.Remove(source);
        arashiStarted.Remove(source);
        if (destroyed) JobQuestPartyRemoved(source);
        if (destroyed) FallenObserve(source);
        if (destroyed) GraoullyStatus(source, 904, false);
        if (destroyed && territory == HeavenswardIntroCatalog.REGULA_VITH && questTerminusIds.Remove(source))
            questTerminusActive = false;
        regulaDetonating.Remove(source);
        CoilRemoved(source);
        ShinryuRemoved(source);
        YojimboRemoved(source);
        if (_casts.TryGetValue(source, out var endingCast))
        {
            if (IsNativeTreasure)
            {
                TreasureSequenceFinish(endingCast, _now);
                TreasureGymnasionFinish(endingCast, _now);
                TreasureNarkissosFinish(endingCast);
                TreasureBitterFinish(endingCast);
                TreasureFortressFinish(endingCast);
                TreasureHexFinish(endingCast);
                TreasureIceFinish(endingCast, _now);
                TreasureCreatedCircleFinish(endingCast);
                TreasureVaultGroupFinish(endingCast);
                TreasureCastBaitFinish(endingCast);
                TreasureBeamFinish(endingCast);
                TreasurePlayerGazeFinish(endingCast);
                TreasureWallKnockbackFinish(endingCast);
                TreasureBuffetFinish(endingCast);
                if (endingCast.Rule.Action == 13443) treasureHydrospheres.Clear();
            }
            FateFinish(endingCast);
            TacticalFinish(endingCast);
            SleepOtisFinish(endingCast);
            JobQuestPartyFinish(endingCast);
            BekkoFinish(endingCast);
            OrcusFinish(endingCast);
            SageLifeFinish(endingCast);
            SteelFinish(endingCast);
            QuestWindFinish(endingCast);
            HeroesPretendersFinish(endingCast);
            IseultalonFinish(endingCast);
            AlphaFinish(endingCast);
            DikaiosyneFinish(endingCast);
            BarP3Finish(endingCast);
            RhalgrFinish(endingCast);
            EndwalkerQuestFinish(endingCast);
            ByakkoFinish(endingCast);
            SuzakuFinish(endingCast);
            DoomFinish(endingCast);
        }
        SeiryuRemoved(source);
        TitaniaRemoved(source);
        InnocenceRemoved(source);
        HadesRemoved(source);
        WolRemoved(source);
        HydaRemoved(source, timestamp ?? _now);
        AsuraRemoved(source);
        ZeleniaRemoved(source);
        ValigRemoved(source);
        ArkRemoved(source);
        EnuoRemoved(source);
        NecronRemoved(source);
        QueenRemoved(source);
        if (IsTsukuyomi)
        {
            if (_casts.TryGetValue(source, out var tsukuyomiRemoved)) TsukuyomiFinish(tsukuyomiRemoved);
            TsukuyomiRemoveMoon(source);
        }
        if (IsMalboro) MalboroFinish(source, 0);
        if (IsNativeTrial && _casts.TryGetValue(source, out var trialRemoved))
        {
            ArrTrialFinish(trialRemoved);
            LabyrinthFinish(trialRemoved);
            SyrcusFinish(trialRemoved);
            ArrQuestFinish(trialRemoved, timestamp ?? _now);
            UltimaFinish(trialRemoved.Rule.Action);
            GarudaExFinish(trialRemoved, timestamp ?? _now);
            TitanExFinish(trialRemoved, timestamp ?? _now);
        }
        if (IsPorta && _casts.TryGetValue(source, out var portaRemoved)) PortaFinish(portaRemoved);
        if (IsHardTrial && _casts.TryGetValue(source, out var hardRemoved)) HardTrialFinish(hardRemoved);
        if (IsHardTrial) hardTethers.Remove(source);
        if (IsThornmarch && _casts.TryGetValue(source, out var thornRemoved)) ThornFinish(thornRemoved, timestamp ?? _now);
        if (IsLakshmi && _casts.TryGetValue(source, out var lakshmiRemoved)) LakshmiFinish(lakshmiRemoved);
        if (IsWhorleater)
        {
            whorlSeen.Remove(source);
            whorlBirths.Remove(source);
        }
        if (_casts.TryGetValue(source, out var lapisRemoved)) LapisFinish(lapisRemoved, true, timestamp ?? _now);
        if (_casts.TryGetValue(source, out var aetherfontRemoved)) AetherfontFinish(aetherfontRemoved);
        if (_casts.TryGetValue(source, out var lunarRemoved)) LunarFinish(lunarRemoved);
        if (_casts.TryGetValue(source, out var ihuyRemoved)) IhuyFinish(ihuyRemoved, timestamp ?? _now);
        if (_casts.TryGetValue(source, out var worqorRemoved)) WorqorFinish(worqorRemoved);
        SkydeepRemoved(source);
        if (_casts.TryGetValue(source, out var vanguardRemoved)) VanguardFinish(vanguardRemoved);
        if (_casts.TryGetValue(source, out var origenicsRemoved)) OrigenicsFinish(origenicsRemoved);
        if (_casts.TryGetValue(source, out var alexRemoved)) AlexFinish(alexRemoved, timestamp ?? _now);
        if (_casts.TryGetValue(source, out var tenderRemoved)) TenderFinish(tenderRemoved);
        StrayRemoved(source, timestamp ?? _now);
        YuweRemoved(source);
        UnderRemoved(source, timestamp ?? _now);
        MesoRemoved(source);
        if (_casts.TryGetValue(source, out var clyRemoved)) ClyFinish(clyRemoved);
        if (territory == MistwakeCatalog.TERRITORY)
        {
            if (_casts.TryGetValue(source, out var mistRemoved)) MistFinish(mistRemoved);
            mistRocks.RemoveAll(a => a.Id == source);
        }
        if (territory == YuweyawataCatalog.TERRITORY && _casts.TryGetValue(source, out var yuweRemoved)) YuweFinish(yuweRemoved);
        if (_casts.TryGetValue(source, out var skydeepRemoved)) SkydeepFinish(skydeepRemoved, timestamp ?? _now);
        lapisRoars.Remove(source);
        lapisEyes.Remove(source); lapisEyeUnconfirmed.Remove(source); lapisEyeLosses.Remove(source);
        lapisDoom.Remove(source); lapisDoomUnconfirmed.Remove(source); lapisDoomLosses.Remove(source);
        lapisTethers.Remove(source);
        lapisTetherPending.Remove(source);
        foreach (var id in lapisTethers.Keys.Where(id => lapisTethers[id].Target == source).ToArray()) lapisTethers.Remove(id);
        if (Active?.BossOid == 0x1018 && _actors.GetValueOrDefault(source) is { Oid: 0x101A } mine) FractalRemoveMine(mine.Position);
        _actors.Remove(source);
        if (_casts.TryGetValue(source, out var troiaRemoved)) TroiaFinish(troiaRemoved);
        troiaDoom.Remove(source); troiaDoomUnconfirmed.Remove(source); troiaDoomLosses.Remove(source);
        if (_casts.TryGetValue(source, out var alzadaalRemoved)) AlzadaalFinish(alzadaalRemoved);
        alzadaalModels.Remove(source);
        alzadaalStates.Remove(source);
        alzadaalOrders.Remove(source);
        alzadaalDrudges.RemoveAll(id => id == source);
        if (alzadaalManaTarget == source) alzadaalManaTarget = 0;
        stigmaCharges.Remove(source);
        foreach (var key in stigmaMarches.Keys.Where(k => k.Actor == source).ToArray()) stigmaMarches.Remove(key);
        stigmaUnconfirmedStatuses.RemoveWhere(k => k.Actor == source);
        XelRemoved(source);
        BaelRemoved(source);
        SirenRemoved(source);
        ShisuiRemoved(source);
        BardamRemoved(source);
        DomaRemoved(source);
        AlaMhigoRemoved(source);
        SkallaRemoved(source);
        BurnRemoved(source);
        GhimlytRemoved(source);
        HolminsterRemoved(source);
        DohnRemoved(source);
        QitanaRemoved(source);
        MalikahRemoved(source);
        GulgRemoved(source);
        AmaurotRemoved(source);
        TwinningRemoved(source);
        AnyderRemoved(source);
        CosmosRemoved(source);
        AnamnesisRemoved(source);
        HeroesRemoved(source);
        MatoyaRemoved(source, timestamp ?? _now);
        if (_casts.TryGetValue(source, out var zotRemoved)) ZotFinish(zotRemoved);
        BabilRemoved(source);
        vanaspatiBubbles.Remove(source);
        if (_casts.TryGetValue(source, out var vanaspatiRemoved)) VanaspatiFinish(vanaspatiRemoved);
        if (_casts.TryGetValue(source, out var ktisisRemoved)) KtisisFinish(ktisisRemoved);
        if (_casts.TryGetValue(source, out var aitiascopeRemoved)) AitiascopeFinish(aitiascopeRemoved, timestamp ?? _now);
        if (_casts.TryGetValue(source, out var deadEndsRemoved)) DeadEndsFinish(deadEndsRemoved, timestamp ?? _now);
        if (deadEndsElimination?.Source == source || deadEndsElimination?.TargetId == source) deadEndsElimination = null;
        aitiascopeReturnSources.Remove(source);
        if (aitiascopeBite?.Source == source || aitiascopeBite?.TargetId == source) aitiascopeBite = null;
        paglthanMissiles.Remove(source);
        sohrSpreads.Remove(source);
        sohrCloudBaits.Remove(source);
        sohrSeen.Remove(source);
        sohrBirths.Remove(source);
        sohrBusters.RemoveAll(b => b.TargetId == source);
        if (sohrBright?.TargetId == source || sohrBright?.Source == source) sohrBright = null;
        if (sohrStack?.TargetId == source) sohrStack = null;
        if (antCroakSource == source) { antCroak = null; antCroakHits = 0; }
        antChirps.Remove(source);
        antSeenChirps.Remove(source);
        antBirths.Remove(source);
        antParryPredictions.Remove(source);
        antObservedParries.Remove(source);
        antGazeBaits.Remove(source);
        foreach (var id in antGazeBaits.Where(b => b.Value.Source == source).Select(b => b.Key).ToArray()) antGazeBaits.Remove(id);
        foreach (var key in antTethers.Where(t => t.Key.Source == source || t.Value == source).Select(t => t.Key).ToArray()) antTethers.Remove(key);
        if (antMeteorTarget == source) antMeteorTarget = 0;
        if (_casts.Remove(source, out var removed))
            sequences.RemoveAll(s => s.Matches(removed));
        _finished.Remove(source);
        _ripples.Remove(source);
        _missingRotation.RemoveWhere(k => k.Source == source);
        sequences.RemoveAll(s => s.Source == source);
        orbs.Remove(source);
        typhoons.RemoveAll(l => l.Source == source);
        terminusEst.Remove(source);
        finishedTerminus.Remove(source);
        delayedDamage.Remove(source);
        if (incendiarySupport?.Source == source) incendiarySupport = null;
        if (goobbuesGrief?.Source == source) goobbuesGrief = null;
        if (bavaroisKiter == source) bavaroisKiter = 0;
        losSnapshots.Remove(source);
        if (plagueTarget == source) plagueTarget = 0;
        corruptingBursts.Remove(source); resolvedCorruptingBursts.Remove(source);
        frozenPillars.Remove(source);
        snowWinds.Remove(source);
        mirageRays.Remove(source);
        meteorImpacts.Remove(source);
        foreach (var key in sohmBaits.Keys.Where(k => k.Target == source).ToArray()) sohmBaits.Remove(key);
        electrocutionBaits.Remove(source);
        if (source == chargeTarget) chargeTarget = 0;
        if (source == sableTarget) sableTarget = 0;
        brightSpheres.RemoveAll(c => c.Source == source);
        voidSpheres.RemoveAll(c => c.Source == source);
        voidSparkBaits.Remove(source);
        gubalTowers.Remove(source);
        whaleOilTethers.Remove(source);
        foreach (var id in whaleOilTethers.Where(t => t.Value.Target == source).Select(t => t.Key).ToArray()) whaleOilTethers.Remove(id);
        researchLasers.Remove(source);
        foreach (var key in researchStacks.Keys.Where(k => k.Target == source).ToArray()) researchStacks.Remove(key);
        executions.Remove(source);
        if (holyChains.TryGetValue(source, out var partner)) VaultTether(source, partner, 9, false);
        if (flamethrower?.Source == source) flamethrower = null;
        if (akhMorn?.Source == source || akhMorn?.TargetId == source) akhMorn = null;
        if (keeperDisgust?.Source == source) keeperDisgust = null;
        foreach (var key in statusChanges.Keys.Where(k => k.Actor == source).ToArray()) statusChanges.Remove(key);
        if (IsDivineIntervention && Active != null && !DivinePairAlive)
        {
            ResetFight();
            return;
        }
        if (IsJobQuestGroup && !JobQuestGroupAlive)
        {
            ResetFight();
            return;
        }
        if (source == PrimaryId)
        {
            if (IsBarPassage || IsCarnivale || IsAlphaAlligator || IsSlickshell || IsHopeOnTheWaves || IsTimeBetween || DivinePairAlive || JobQuestGroupAlive || TreasureGroupAlive) return;
            if (IsCaduceus)
            {
                if (!CaduceusAlive) ResetFight();
                return;
            }
            if (IsCoilGauntlet) { GauntletTick(); return; }
            if (IsNecron || IsDoomtrain && !doomPhase2 || InnocencePhaseAlive || HadesPhaseAlive) return;
            if (IsYuweTrash)
            {
                YuweSelectPrimary();
                return;
            }
            if (IsOrigenicsTrash)
            {
                OrigenicsSelectPrimary();
                return;
            }
            if (IsVanguardTrash)
            {
                VanguardSelectPrimary();
                return;
            }
            if (IsAetherfontTrash)
            {
                AetherfontSelectPrimary();
                return;
            }
            if (IsLapisCarriage)
            {
                LapisSelectPrimary();
                return;
            }
            if (IsTroiaGroup)
            {
                TroiaSelectPrimary();
                return;
            }
            if (IsZotSisters && _actors.Values.FirstOrDefault(ZotSisterAlive) is { } sister)
            {
                PrimaryId = sister.Id;
                return;
            }
            if (IsAnamnesisPair && _actors.Values.FirstOrDefault(AnamnesisPairAlive) is { } unknown)
            {
                PrimaryId = unknown.Id;
                return;
            }
            if (IsAnyderSharks && _actors.Values.FirstOrDefault(AnyderSharkAlive) is { } shark)
            {
                PrimaryId = shark.Id;
                return;
            }
            if (IsGulgPrejudice && _actors.Values.FirstOrDefault(GulgPrejudiceMember) is { } gulgCompanion)
            {
                PrimaryId = gulgCompanion.Id;
                return;
            }
            if (IsGhimlytGroup && _actors.Values.FirstOrDefault(GhimlytGroupMember) is { } companion)
            {
                PrimaryId = companion.Id;
                return;
            }
            if (IsMalboro || IsSohrTrash && SohrTrashAlive()) return;
            var phaseTwo = _actors.Values.FirstOrDefault(a => AntDollAlive(a) || ResearchTrashMember(a) || FractalTrashMember(a) || ArboretumTrashMember(a) || XelTrashMember(a) || !a.Dead && a.InCombat
                && (Active?.BossOid == 0x5D1 && a.Oid == 0x5D0 || Active?.BossOid == 0xD1E && IsSnowcloakTrash(a)
                    || Active?.BossOid is 0x1051 or 0x1054 && (a.Oid == Active.BossOid || Active.PrimaryAliases?.Contains(a.Oid) == true)));
            if (phaseTwo != null) PrimaryId = phaseTwo.Id;
            else ResetFight();
        }
    }

    public void Tick(DateTime now)
    {
        if (IsAlteRoite)
        {
            _now = now;
            AlteRoite.Update(now);
            AlteRoitePublish();
            return;
        }
        if (IsHalicarnassus)
        {
            _now = now;
            Halicarnassus.Update(now);
            HalicarnassusPublish();
            return;
        }
        if (IsThordan)
        {
            _now = now;
            Thordan.Update(now);
            ThordanPublish();
            return;
        }
        if (IsWickedThunder)
        {
            WickedThunder.Update(now);
            WickedThunderPublish();
            return;
        }
        if (IsKefka)
        {
            Kefka.Update(now);
            KefkaPublish();
            return;
        }
        if (IsQuantum)
        {
            Quantum.Update(now);
            QuantumPublishState();
            return;
        }
        _now = now;
        TreasureFortressUpdate();
        BarP3Update();
        TickResearch(now);
        TickZot();
        UnderTick(now);
        MesoTick();
        MistTick();
        ClyTick();
        YojimboTick();
        WolTick();
        if (IsValigarmanda) ValigTickMovement();
        if (IsArkveld) ArkTick();
        if (IsEnuo) EnuoTick();
        if (IsNecron) NecronTick();
        if (IsQueenEternal) QueenTick();
        if (IsZoraalJa) ZoraalTick();
        if (IsDoomtrain) DoomTick();
        if (IsCoilGauntlet) GauntletTick();
        TwinTick();
        DarknessTick();
        UltimaObserve();
        GarudaExObserve();
        TitanExObserve();
        IfritExObserve();
        if (sirenShadow.Count > 0 && sirenShadow[0].Activation < now) { sirenShadow.Clear(); sirenShadowCones = 0; }
        // These sequences advance on observed cast endings, never on elapsed prediction time.
        foreach (var cast in _casts.Values.Where(c => now >= c.FinishAt.AddSeconds(2) && !UsesObservedCastEnd && territory != DeadEndsCatalog.TERRITORY
            && territory != AlzadaalCatalog.TERRITORY && territory != TroiaCatalog.TERRITORY && territory != LapisCatalog.TERRITORY
            && territory != AetherfontCatalog.TERRITORY && territory != LunarCatalog.TERRITORY && territory != IhuykatumuCatalog.TERRITORY
            && territory != WorqorCatalog.TERRITORY && territory != SkydeepCatalog.TERRITORY && territory != VanguardCatalog.TERRITORY
            && territory != OrigenicsCatalog.TERRITORY && territory != AlexandriaCatalog.TERRITORY && territory != TenderValleyCatalog.TERRITORY
            && territory != StrayboroughCatalog.TERRITORY && territory != YuweyawataCatalog.TERRITORY && territory != UnderkeepCatalog.TERRITORY
            && territory != MesoCatalog.TERRITORY && territory != MistwakeCatalog.TERRITORY && territory != ClyteumCatalog.TERRITORY).ToArray())
            Finish(cast.Source, cast.Rule.Action, completed: cast.EffectSeen);
        sequences.RemoveAll(s => now >= s.Current.FinishAt.AddSeconds(2));
        sandPillars.RemoveAll(c => now >= c.FinishAt.AddSeconds(2));
        if (touchdown != null && now >= touchdown.FinishAt.AddSeconds(2)) touchdown = null;
        if (goobbuesGrief != null && now >= goobbuesGrief.FinishAt.AddSeconds(2)) goobbuesGrief = null;
        foreach (var cast in terminusEst.Values.Where(c => now >= c.FinishAt.AddSeconds(2)).ToArray())
        {
            terminusEst.Remove(cast.Source);
            finishedTerminus.Add(cast.Source);
        }
        foreach (var id in _finished.Keys.Where(id => !_actors.ContainsKey(id)).ToArray()) _finished.Remove(id);
        foreach (var key in _effects.Where(e => now - e.Value > TimeSpan.FromSeconds(10)).Select(e => e.Key).ToArray()) _effects.Remove(key);
    }

    public IEnumerable<string> Hints(Vector3? player)
    {
        if (IsAlteRoite)
        {
            foreach (var hint in AlteRoite.Hints()) yield return hint;
            yield break;
        }
        if (IsHalicarnassus)
        {
            if (Halicarnassus.Actors.GetValueOrDefault(PlayerId) is { } viewer
                && Halicarnassus.Visible().Any(a => DungeonSourceGeometry.Contains(a.Shape, a.Origin,
                    a.Rotation, viewer.Actor.Position)))
                yield return "GTFO from aoe!";
            yield break;
        }
        if (IsThordan)
        {
            foreach (var hint in Thordan.Hints()) yield return hint;
            yield break;
        }
        if (IsWickedThunder)
        {
            foreach (var hint in WickedThunder.Hints()) yield return hint;
            yield break;
        }
        if (IsKefka)
        {
            foreach (var hint in Kefka.Hints()) yield return hint;
            yield break;
        }
        if (IsQuantum)
        {
            foreach (var hint in Quantum.Hints()) yield return hint;
            yield break;
        }
        if (IsHeavenOnHigh && player is { } hohPlayer && HeavenOnHighAoes().Any(a => a.Obstacle
            && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(hohPlayer, 0)))
            yield return "GTFO from voidzone!";
        if (IsHeavenOnHigh && player is { } hohSpawnPlayer && HeavenOnHighSpawnAreas()
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(hohSpawnPlayer, 0)))
            yield return "GTFO from aoe!";
        foreach (var hint in TacticalHints(player)) yield return hint;
        if (player is { } kenkoPlayer)
            foreach (var hint in KenkoHints(kenkoPlayer)) yield return hint;
        foreach (var hint in TreasureWallKnockbackHints()) yield return hint;
        foreach (var hint in TreasureBuffetHints(player)) yield return hint;
        if (player is { } kelpiePlayer && TreasureKelpieLandingUnsafe(kelpiePlayer)) yield return "GTFO from aoe!";
        if (player is { } createdPlayer && TreasureCreatedCircleAoes().Concat(TreasureIceAoes())
            .Concat(TreasureVaultGroupAoes().Where(a => a.Navigate)).Concat(TreasureBeamAoes()).Concat(TreasureRotationAoes())
            .Concat(TreasureGymnasionSequenceAoes().Where(a => a.Navigate && a.Display))
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(createdPlayer, 0)))
            yield return "GTFO from aoe!";
        foreach (var hint in TreasureCastBaitHints(player)) yield return hint;
        if (treasureEarthquake != null) yield return "Raidwide";
        if (player is { } sequencePlayer && treasureSweepAoes.Any(a =>
            a.Shape.Resolve(a.Position, a.Rotation, null).Contains(sequencePlayer)))
            yield return "GTFO from exaflare!";
        if (player is { } strikePlayer && TreasureSequenceAoes().Where(a => a.Id.StartsWith("treasure/strike/"))
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(strikePlayer)))
            yield return "GTFO from aoe!";
        if (IsNativeTreasure && treasureDelayedHits.Count > 0) yield return "Tankbuster";
        if (player is { } treasurePosition && (TreasureObstacles().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(treasurePosition))
            || TreasureVoidzoneAction != 0 && TrialVoidzoneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(treasurePosition))))
            yield return "GTFO from voidzone!";
        if (IoStopActivation is { } ioActivation && ioActivation <= _now.AddSeconds(1000))
            yield return "Stop everything!";
        if (player is { } jobPosition && NodeAoes().Concat(SteelAoes())
            .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(jobPosition)))
            yield return "GTFO from aoe!";
        foreach (var hint in JobQuestPartyHints(player)) yield return hint;
        foreach (var hint in HeroesPretendersHints(player)) yield return hint;
        foreach (var hint in BarP2Hints(player)) yield return hint;
        foreach (var hint in BarP3Hints(player)) yield return hint;
        foreach (var hint in CarnivaleHints(player)) yield return hint;
        foreach (var hint in AlphaHints(player)) yield return hint;
        foreach (var hint in IseultalonHints(player)) yield return hint;
        foreach (var hint in DikaiosyneHints(player)) yield return hint;
        foreach (var hint in FallenHints(player)) yield return hint;
        foreach (var hint in SageLifeHints(player)) yield return hint;
        foreach (var hint in GuildivainHints(player)) yield return hint;
        foreach (var hint in SleepOtisHints(player)) yield return hint;
        foreach (var hint in SlickshellHints(player)) yield return hint;
        foreach (var hint in MaxQuestHints()) yield return hint;
        foreach (var hint in PalacePlantHints(player)) yield return hint;
        foreach (var hint in NybethHints(player)) yield return hint;
        foreach (var hint in GodmotherHints(player)) yield return hint;
        foreach (var hint in GudannaHints(player)) yield return hint;
        foreach (var hint in BlackRiderHints(player)) yield return hint;
        foreach (var hint in YaquaruHints(player)) yield return hint;
        foreach (var hint in HopeHints()) yield return hint;
        foreach (var hint in QuestWindHints(player)) yield return hint;
        foreach (var hint in MainScenarioHints(player)) yield return hint;
        foreach (var hint in RhalgrHints(player)) yield return hint;
        foreach (var hint in EndwalkerQuestHints(player)) yield return hint;
        if (player is { } bekkoPlayer)
            foreach (var hint in BekkoAoes().Where(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(bekkoPlayer))
                .Select(a => a.Obstacle ? "GTFO from voidzone!" : "GTFO from aoe!").Distinct())
                yield return hint;
        if (player is { } orcusPlayer)
        {
            if (OrcusAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(orcusPlayer)))
                yield return "GTFO from aoe!";
            if ((IsOrcus || IsIo) && TrialVoidzoneAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(orcusPlayer)))
                yield return "GTFO from voidzone!";
        }
        if (player is { } portraitPlayer)
            foreach (var hint in PortraitHints(portraitPlayer)) yield return hint;
        if (player is { } samuraiPlayer && SamuraiAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(samuraiPlayer)))
            yield return "GTFO from aoe!";
        foreach (var hint in EnemyPressureHints()) yield return hint;
        if (RegulaDetonationHint is { } detonationHint) yield return detonationHint;
        foreach (var hint in NoviceHints()) yield return hint;
        foreach (var hint in FateHints(player)) yield return hint;
        if (TisiphonePrecombatHint is { } precombatHint) yield return precombatHint;
        foreach (var hint in PalaceSerpentHints(player)) yield return hint;
        if (player is { } gazePlayer && EddaGazeDanger(gazePlayer)) yield return "Turn away from gaze!";
        if (player is { } poxPlayer && PalacePoxSources.Any(a => Vector2.DistanceSquared(
            new(a.Position.X, a.Position.Z), new(poxPlayer.X, poxPlayer.Z)) <= 16))
            yield return "GTFO from voidzone!";
        if (player is { } questPlayer && HeavenswardQuestVoidzones.Any(a => Vector2.DistanceSquared(
            new(a.Position.X, a.Position.Z), new(questPlayer.X, questPlayer.Z))
            <= (territory == HeavenswardIntroCatalog.ALDYNN ? 16 : 64)))
            yield return "GTFO from voidzone!";
        foreach (var hint in ArrTrialHints(player)) yield return hint;
        foreach (var hint in PortaHints(player)) yield return hint;
        if (HardTrialCountdown is { } hardCountdown) yield return hardCountdown;
        foreach (var hint in HardTrialHints(player)) yield return hint;
        foreach (var hint in ThornHints(player)) yield return hint;
        foreach (var hint in WhorlHints()) yield return hint;
        foreach (var hint in SusanoHints()) yield return hint;
        foreach (var hint in LakshmiHints()) yield return hint;
        foreach (var hint in ShinryuHints()) yield return hint;
        foreach (var hint in TsukuyomiHints()) yield return hint;
        foreach (var hint in YojimboHints()) yield return hint;
        foreach (var hint in ByakkoHints()) yield return hint;
        foreach (var hint in SuzakuHints()) yield return hint;
        foreach (var hint in SeiryuHints()) yield return hint;
        foreach (var hint in TitaniaHints()) yield return hint;
        foreach (var hint in InnocenceHints()) yield return hint;
        foreach (var hint in HadesHints()) yield return hint;
        foreach (var hint in WolHints()) yield return hint;
        foreach (var hint in HydaHints()) yield return hint;
        foreach (var hint in AsuraHints()) yield return hint;
        foreach (var hint in ZeleniaHints()) yield return hint;
        foreach (var hint in ValigHints()) yield return hint;
        foreach (var hint in ArkHints()) yield return hint;
        foreach (var hint in EnuoHints()) yield return hint;
        foreach (var hint in NecronHints()) yield return hint;
        foreach (var hint in QueenHints()) yield return hint;
        foreach (var hint in ZoraalHints()) yield return hint;
        foreach (var hint in DoomHints()) yield return hint;
        foreach (var hint in CoilHints()) yield return hint;
        foreach (var hint in CaduceusHints()) yield return hint;
        foreach (var hint in TwinHints()) yield return hint;
        foreach (var hint in LabyrinthHints(player)) yield return hint;
        foreach (var hint in SyrcusHints(player)) yield return hint;
        foreach (var hint in DarknessHints(player)) yield return hint;
        foreach (var hint in UltimaHints()) yield return hint;
        foreach (var hint in GarudaExHints(player)) yield return hint;
        foreach (var hint in TitanExHints()) yield return hint;
        foreach (var hint in IfritExHints()) yield return hint;
        if (IsCoilGauntlet && Active != null)
        {
            yield return $"邂逅4：第{gauntletStage + 1}波";
            if (gauntletEmergencyHits > 0) yield return "紧急模式：连续全屏伤害";
            if (_actors.GetValueOrDefault(PlayerId) is { } viewer && GauntletAoes().Any(a => a.Id.StartsWith("coil4/rook")
                && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(viewer.Position)))
                yield return "绕到车背后";
            if (_actors.GetValueOrDefault(PlayerId) is { } cleaveViewer && GauntletAoes().Any(a => a.Id.StartsWith("coil/cleave/outline")
                && a.Navigate && a.Shape.Resolve(a.Position, a.Rotation, null).Contains(QuantizedDungeonPosition(cleaveViewer.Position))))
                yield return "离开顺劈范围";
        }
        foreach (var hint in DeadEndsHints()) yield return hint;
        foreach (var hint in TroiaHints()) yield return hint;
        foreach (var hint in LapisHints()) yield return hint;
        foreach (var hint in LunarHints()) yield return hint;
        foreach (var hint in WorqorHints()) yield return hint;
        foreach (var hint in SkydeepHints()) yield return hint;
        foreach (var hint in VanguardHints()) yield return hint;
        foreach (var hint in OrigenicsHints()) yield return hint;
        foreach (var hint in AlexHints()) yield return hint;
        foreach (var hint in TenderHints()) yield return hint;
        foreach (var hint in StrayHints()) yield return hint;
        foreach (var hint in UnderHints()) yield return hint;
        foreach (var hint in MesoHints()) yield return hint;
        foreach (var hint in MistHints()) yield return hint;
        foreach (var hint in ClyHints()) yield return hint;
        foreach (var hint in BabilHints()) yield return hint;
        if (territory == ZotCatalog.TERRITORY)
        {
            if (zotDhrupad) yield return "三次单体伤害及持续伤害";
            if (_casts.Values.Any(c => c.Rule.Action is 25248 or 25290)) yield return "死刑后中毒：可驱散";
            if (_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 18))) yield return "中毒：可驱散";
        }
        if (territory == 1036 && CoralColor != "Resolved") yield return "正确开关：" + CoralColor switch { "Blue" => "蓝", "Red" => "红", "Green" => "绿", _ => "未知" };
        foreach (var hint in _casts.Values.Where(BarPassageCastVisible).Where(CarnivaleCastVisible).Select(c => c.Rule.Hint).OfType<string>().Distinct()) yield return hint;
        if (FractalInteractionTarget != null) yield return Active?.BossOid == 0x1013 ? "使用孵化器眩晕首领" : "交互控制单元以继续前进";
        foreach (var cast in _casts.Values.Where(c => Active?.BossOid == 0x1023 && c.Rule.Hint == "打断或眩晕"))
            if (_actors.TryGetValue(cast.Source, out var caster)) yield return $"{caster.Name}：打断或眩晕 {cast.Rule.Name}";
        if (fractalBombs.Count != 0) yield return "以太化学炸弹：可驱散，保持分散";
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 316))
            if (_actors.TryGetValue(cast.Source, out var medicus)) yield return $"{medicus.Name}：打断或眩晕石肤";
        foreach (var hint in SastashaHardHints()) yield return hint;
        foreach (var hint in AntitowerHints()) yield return hint;
        foreach (var hint in SohrHints()) yield return hint;
        foreach (var hint in BaelHints()) yield return hint;
        if (anyderCollectOrbs.Count > 0) yield return "吸收雷球，避免首领获得强化";
        if (territory == MatoyaCatalog.TERRITORY)
        {
            if (Active?.BossOid == 0x300C && matoyaBaits.Values.Any(b => b.Target == PlayerId)) yield return "将泥球引入洞口，避开队友";
            if (Active?.BossOid == 0x307F && matoyaGeysersActive && !matoyaCloud) yield return "进入靠近云台的间歇泉，等待喷发";
            if (Active?.BossOid == 0x300B && matoyaImminent) yield return "前往场地对侧，持续抵抗吸入";
        }
        if (territory == PaglthanCatalog.TERRITORY)
        {
            if (paglthanLightning.ContainsKey(PlayerId)) yield return "将雷电传给空闲避雷针";
            if (paglthanPlatform && !paglthanCore) yield return "进入发光圈前往核心平台";
            if (paglthanAkhMorn != null) yield return $"保持四人分摊：剩余 {4 - paglthanAkhHits} 次";
            if (paglthanBaits.ContainsKey(PlayerId)) yield return "十字点名：远离队友诱导后离开";
        }
        if (Active?.BossOid == 0x2DF1 && _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 910)))
            yield return "死宣：及时驱散队友";
        if (CosmosSeedTarget != null) yield return "搬走绿地上的种子";
        if (CosmosTilesActive && CosmosTransporting) yield return CosmosReadyToDrop ? "在此放下种子" : "将种子搬出易发芽绿地";
        if (Active?.BossOid == 0x2C13 && _actors.TryGetValue(PlayerId, out var burning) && HasStatus(burning, 2136))
            yield return "将火焰传给家具";
        if (territory == QitanaCatalog.TERRITORY && _casts.Values.Any(c => c.Rule.Action == 15507))
            yield return "连续11次全屏伤害";
        if (holminsterChainTarget != 0)
            yield return holminsterChained ? "击破锁链救援队友" : "队友即将被锁链束缚";
        if (Active?.BossOid == 0x179B && _actors.Values.Any(a => a.Oid == 0x179D && !a.Dead))
            yield return "击破气石以解除无敌";
        if (xelStacks.Count > 0) yield return "Ingurgitate：四人分摊";
        if (Active?.BossOid == 0x17B8 && _casts.Values.Any(c => c.Rule.Action == 6629)) yield return "打断或眩晕 IxaliAeroIII";
        foreach (var prediction in PredictedDamage.Where(p => p.DamageType == "Tankbuster")) yield return $"死刑：{prediction.Role} {prediction.Name}";
        if (incendiarySupport != null) yield return "即将受到全屏三连伤害";
        if (ActiveRipples.Any()) yield return "小怪即将出现";
        if (Active?.BossOid == 0x4C && _actors.Values.Any(a => a.Oid == 0x7D && !a.Dead)) yield return "先击杀小恶魔";
        if (ForbiddenTargets.Length != 0)
            yield return Active?.BossOid switch
            {
                0x4A8D => "无敌：停止攻击首领，优先击杀腐化水晶",
                0x8F4 => "不要攻击鸟蛋，避免触发狂暴",
                0x39CA => "先打破黑锁救人，暂时停止攻击首领",
                0xE83 => "无敌：停止攻击无敌目标，优先击杀书页",
                0x1432 => "物理护盾：停止攻击护盾目标，优先击杀骑士蜂",
                0x1503 => "定向招架：停止从受保护方向攻击，前往未招架方向",
                0x15FB => "无敌：停止攻击无敌目标，处理莫古力",
                0x179B => "无敌：停止攻击无敌目标，优先击破气石",
                0x1FAC => "无敌：停止攻击首领，变身后使用碎岩击破仆从",
                _ => "燃烧护盾：停止攻击护盾目标",
            };
        if (Active?.BossOid == 0x1AB && _actors.Values.Any(a => a.PartySlot >= 0 && HasStatus(a, 280)))
            yield return "击杀水泡，解救被束缚队友";
        if (Active?.BossOid == 0x477A && _actors.TryGetValue(PlayerId, out var doomed) && HasStatus(doomed, QarnCatalog.DOOM))
            yield return glowingPlatform == 0 ? "死宣：寻找发光平台（当前缺少平台状态）" : "死宣：前往发光平台";
        if (Active?.BossOid == 0x4900 && orbTarget == PlayerId && orbTarget != 0 && orbs.Count != 0)
            yield return "被雷球点名：风筝雷球";
        if (Active?.BossOid == 0x48FF && _actors.TryGetValue(PlayerId, out var muddied) && HasStatus(muddied, 270))
            yield return "污泥：可驱散";
        if (CleansingFruits.Any()) yield return "减益已达三层：食用果实解除";
        foreach (var partyHint in _casts.Values.Select(c => c.Rule.PartyAoe).OfType<string>().Distinct()) yield return partyHint;
        if (GaiusEnrageSeconds is { } remaining) yield return $"优先击杀分身：狂暴剩余 {remaining:F1} 秒";
        if (Active?.BossOid == 0x41D && bavaroisKiter == PlayerId && PlayerId != 0) yield return "被布丁点名：远离首领风筝";
        if (corruptingBursts.ContainsKey(PlayerId)) yield return "三层水晶：远离队友爆炸";
        if (snowDrift != null && snowDrift.FinishAt <= _now.AddSeconds(3)) yield return "雪飘：持续移动，暂禁读条";
        if (akhMorn != null) yield return $"死亡轮回：保持四人分摊，剩余 {4 - akhMornHits} 次";
        if (flamethrower != null) yield return $"持续喷火：保持离开扇形，剩余 {6 - flameHits} 次";
        if (sohmBaits.Keys.Any(k => k.Target == PlayerId))
            yield return Active?.BossOid == 0xE8F ? "花香点名：将扇形诱导至队友外" : "前往场边诱导，远离队友";
        if (Active?.BossOid == 0xEA6 && chargeTarget == PlayerId && chargeTarget != 0)
            yield return $"将雷电连线传给石像：剩余 {Math.Max(0, (chargeActivation - _now).TotalSeconds):F1} 秒";
        if (Active?.BossOid == 0x39CA && _actors.TryGetValue(sableTarget, out var chained) && HasStatus(chained, 3324))
            yield return $"打破 {chained.Name} 的黑锁";
        if (Active?.BossOid is 0x1056 or 0x3DA7 && holyChains.ContainsKey(PlayerId)) yield return "锁链：持续远离连线队友";
        if (Active?.BossOid == 0xE82 && _actors.TryGetValue(PlayerId, out var skating) && HasStatus(skating, 911))
            yield return "薄冰：移动将滑行15码，确认预测落点";
        if (whaleOilTethers.Values.Any(t => t.Target == PlayerId)) yield return "将油球引至首领";
        if (voidSparkBaits.ContainsKey(PlayerId)) yield return "虚无火花：远离队友诱导";
        if (gubalTowers.Count > 0) yield return $"召唤塔：每塔需要 {GubalTowerSoakers} 人";
        foreach (var stack in researchStacks.Values.Distinct()) yield return $"分摊：恰好 {stack.Count} 人";
        if (researchLasers.Values.Any(t => t.Target == PlayerId)) yield return "激光连线：避免扫到队友";
        if (Active?.BossOid == 0x8EF && _actors.TryGetValue(PlayerId, out var sirenTarget))
        {
            if (HasStatus(sirenTarget, 370)) yield return "海妖之歌：治疗至满血";
            if (HasStatus(sirenTarget, 273)) yield return "出血：可驱散";
        }
        if (Active?.BossOid == 0x4B0D && _actors.TryGetValue(PlayerId, out var afflicted)
            && (HasStatus(afflicted, 3779) || HasStatus(afflicted, 3463)))
            yield return "触电/麻痹：可驱散";
        if (player is { } cleavePlayer && TreasureCleaveDanger(cleavePlayer)) yield return "GTFO from cleave!";
        if (TreasureInterruptTargets.Any()) yield return "Interrupt!";
        var bitterInterrupts = _casts.Values.Where(c => IsNativeTreasure && c.Rule.Action == 43677).ToArray();
        if (bitterInterrupts.Length == 1 && _actors.GetValueOrDefault(bitterInterrupts[0].Source) is { } bitterCaster)
            yield return $"Interrupt {bitterCaster.Name}!";
        else if (bitterInterrupts.Length > 1)
            yield return "Interrupt!";
        foreach (var hint in TreasureNarkissosHints()) yield return hint;
        foreach (var hint in TreasureYanHints()) yield return hint;
        foreach (var hint in TreasureHexHints()) yield return hint;
        if (player is { } eurekaPlayer)
        {
            foreach (var hint in EurekaTiamatHints(eurekaPlayer)) yield return hint;
            foreach (var hint in EurekaTwintaniaHints(eurekaPlayer)) yield return hint;
            foreach (var hint in EurekaMinotaurHints()) yield return hint;
            foreach (var hint in EurekaChimeraHints(eurekaPlayer)) yield return hint;
            foreach (var hint in EurekaAeturnaHints(eurekaPlayer)) yield return hint;
            foreach (var hint in EurekaKaliyaHints(eurekaPlayer)) yield return hint;
            foreach (var hint in EurekaAdministratorHints(eurekaPlayer)) yield return hint;
            foreach (var hint in EurekaExcaliburHints(eurekaPlayer)) yield return hint;
        }
        if (TreasureSprintIntent != null) yield return "疾跑接近隐藏目标（9码内）";
        if (player is { } baitPlayer)
            foreach (var hint in TreasureBaitHints(baitPlayer)) yield return hint;
        if (player is { } p && (OutputCasts.Any(c => c.Value.Rule.Shape?.Resolve(c.Value.Location, c.Value.Rotation, null).Contains(p) == true)
            || TreasureLights().Concat(TreasureOdiousAoes()).Concat(TreasureNarkissosAoes().Where(a => a.Navigate))
                .Concat(TreasureBitterAoes()).Concat(TreasureFortressAoes().Where(a => a.Navigate))
                .Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(p))
            || EurekaBreathDanger(p)))
            yield return "离开范围！";
    }

    public void ResetFight()
    {
        if (IsThordan) Thordan.Clear();
        if (IsHalicarnassus) Halicarnassus.Clear();
        if (IsAlteRoite) AlteRoite.Clear();
        if (IsWickedThunder) WickedThunder.Clear();
        if (IsKefka) Kefka.Clear();
        if (IsQuantum) Quantum.Clear();
        ResetEurekaOutput();
        heavenOnHighKnockbacks.Clear();
        ResetHeavenOnHighSpawns();
        ResetKenko();
        ResetNybeth();
        ResetGodmother();
        fateStacks.Clear();
        fateKnockbacks.Clear();
        fateObservedCasts.Clear();
        fateEffects.Clear();
        ResetDaivadipa();
        ResetFormidable();
        ResetChi();
        ResetMica();
        ResetTtokrrone();
        ResetTactical();
        ResetSleepOtis();
        ResetSlickshell();
        fateCastBaits.Clear();
        fateCastBaitId = 0;
        treasureVaultGroups.Clear();
        treasureHighVoltage.Clear();
        ResetTreasureBeams();
        treasurePlayerGaze = null;
        treasureIceSequences.Clear();
        treasureIceId = 0;
        treasureCreatedCircles.Clear();
        treasureCreatedCircleActors.Clear();
        treasureDestroyedCircleActors.Clear();
        ResetTreasureSequences();
        ResetTreasureRotations();
        ResetTreasureGymnasionSequences();
        ResetTreasureNarkissos();
        ResetTreasureMolter();
        treasureBitterPrediction = null;
        treasureBitterDonut = false;
        treasureBitterLeapTimes.Clear();
        ResetTreasureYan();
        ResetTreasureHex();
        ResetTreasureFortress();
        treasureWallKnockbacks.Clear();
        treasureHydrospheres.Clear();
        treasureHydrosphereActors.Clear();
        treasureBuffetBaits.Clear();
        treasureBuffetIcon = null;
        treasureBuffetPrimary = null;
        treasureBuffetPrimaryRemoved = false;
        treasureDelayedHits.Clear();
        treasureLightOrder.Clear();
        treasureBaits.Clear();
        treasureBaitPrimary = null;
        treasureBaitPrimaryRemoved = false;
        treasureBaitStarts = 0;
        treasureCachedBoss = null;
        treasureCachedBossRemoved = false;
        treasureCastBaits.Clear();
        treasureCastBaitCurrent.Clear();
        treasureCastBaitId = 0;
        treasureEffects.Clear();
        treasureCurrentCasts.Clear();
        treasureObservedCasts.Clear();
        treasureParty = [];
        treasureOdious = null;
        treasureOdiousHits = 0;
        Active = null; PrimaryId = 0; Arena = null;
        ResetPortrait();
        arashiActivation = null;
        arashiResolvedAt = default;
        arashiStarted.Clear();
        jobQuestNpcSlots.Clear();
        jobQuestStacks.Clear();
        jobQuestSpreads.Clear();
        jobQuestPartyStarted.Clear();
        treasureMissingStackTargets.Clear();
        bekkoRings.Clear();
        bekkoStarted.Clear();
        bekkoYukikaze.Clear();
        bekkoRingId = 0;
        orcusRings.Clear();
        orcusStarted.Clear();
        orcusRingId = 0;
        fallenSpreads.Clear();
        fallenStacks.Clear();
        fallenMarkId = 0;
        sageRocks.Clear();
        sageRockStarted.Clear();
        sageTowers.Clear();
        sageRockId = 0;
        guildivainBaits.Clear();
        guildivainTethers.Clear();
        guildivainObservedTethers.Clear();
        guildivainStates.Clear();
        guildivainActivation = default;
        guildivainBaitId = 0;
        guildivainInitialized = false;
        nodeExplosives.Clear();
        nodeObjects.Clear();
        nodeExplosiveId = 0;
        steelSwords.Clear();
        steelObjects.Clear();
        steelBolts.Clear();
        steelBoltStarted.Clear();
        steelObjectId = 0;
        steelCasting = false;
        ioCaution.Clear();
        ioDeadCaution.Clear();
        questWindStarted.Clear();
        questWindObserved.Clear();
        mainScenarioCastOrder.Clear();
        hopeDetonators.Clear();
        ResetTimeBetween();
        ResetHeroesPretenders();
        ResetIseultalon();
        ResetAlpha();
        ResetBarPassage();
        ResetDikaiosyne();
        frostyLockPredictions.Clear();
        frostyLockCasts.Clear();
        frostyLockKnown.Clear();
        endwalkerCharges.Clear();
        frostyLockKey = 0;
        rhalgrTermini.Clear();
        rhalgrKnown.Clear();
        rhalgrTerminusKey = 0;
        rhalgrTerminusActivation = null;
        rhalgrGunblade = null;
        rhalgrStarted.Clear();
        foxshotBaits.Clear();
        questWindKnockback = null;
        regulaDetonating.Clear();
        questTerminusActive = false;
        questTerminusIds.Clear();
        palaceSerpentHits = 0;
        graoullyPrey = 0;
        graoullyObservedPrey.Clear();
        graoullyPartners.Clear();
        graoullyIce.Clear();
        glowingPlatform = 0;
        sandPillars.Clear(); orbs.Clear(); detonatedOrbs.Clear(); orbTarget = 0;
        typhoons.Clear(); finishedTyphoons.Clear(); touchdown = null;
        delayedDamage.Clear();
        incendiarySupport = null;
        terminusEst.Clear(); finishedTerminus.Clear(); gaiusEnrage = default; neroRingActivation = default;
        goobbuesGrief = null; bavaroisKiter = 0;
        losSnapshots.Clear(); plagueTarget = 0; plagueActivation = default; plagueEventTime = default;
        corruptingBursts.Clear(); resolvedCorruptingBursts.Clear();
        snowDrift = null; tundra = null; frozenPillars.Clear(); snowMoveAnchor = null; snowMovePlayer = 0;
        flamethrower = null; flameHits = 0; akhMorn = null; akhMornHits = 0; keeperDisgust = null;
        mirageRays.Clear(); garleanFires.Clear();
        sohmBaits.Clear(); meteorImpacts.Clear();
        electrocutionBaits.Clear(); electrocutionHits = 0; fumesShrink = null;
        chargeTarget = 0; chargeActivation = default; chargeEventTime = default; sableTarget = 0;
        shiningBlades.Clear(); brightSpheres.Clear(); seenBrightSpheres.Clear(); brightSphereBirths.Clear();
        executions.Clear(); dimensionalRips.Clear(); holyChains.Clear(); holyChainEvents.Clear();
        disclosureSpin = null; voidSparkBaits.Clear(); voidSpheres.Clear(); gubalSeen.Clear(); gubalBirths.Clear();
        gubalTowers.Clear(); whaleOilTethers.Clear(); whaleOilCancelled.Clear();
        researchLasers.Clear(); researchLaserEvents.Clear(); researchLaserChanged.Clear(); researchStacks.Clear(); researchSeenBinds.Clear();
        researchSeenStars.Clear(); researchBirths.Clear(); researchUnfusedStars.Clear(); researchStars.Clear(); researchStarPairs.Clear();
        researchTutorialFire = false; researchTutorialIce = false;
        researchDonuts.Clear(); researchSpreads.Clear(); researchLineStacks.Clear();
        researchResolvedMarkers.Clear(); researchStarResolutions.Clear();
        fractalBrokenTiles = 0; fractalMines.Clear(); fractalBombs.Clear(); fractalSeenBombs.Clear();
        ResetArboretum();
        ResetAntitower();
        ResetSohr();
        ResetXel();
        ResetBael();
        ResetSiren();
        ResetShisui();
        ResetBardam();
        ResetDoma();
        ResetAlaMhigo();
        ResetSkalla();
        ResetBurn();
        ResetGhimlyt();
        ResetHolminster();
        ResetDohn();
        ResetQitana();
        ResetMalikah();
        ResetGulg();
        ResetAmaurot();
        ResetTwinning();
        ResetAnyder();
        ResetCosmos();
        ResetAnamnesis();
        ResetHeroes();
        ResetMatoya();
        ResetPaglthan();
        ResetZot();
        ResetBabil();
        ResetVanaspati();
        ResetKtisis();
        ResetAitiascope();
        ResetDeadEnds();
        stigmaCharges.Clear();
        stigmaMarches.Clear();
        stigmaUnconfirmedStatuses.Clear();
        ResetAlzadaal();
        ResetTroia();
        ResetLapis();
        ResetAetherfont();
        ResetLunar();
        ResetIhuy();
        ResetWorqor();
        ResetSkydeep();
        ResetVanguard();
        ResetOrigenics();
        ResetAlex();
        ResetTender();
        ResetStray();
        ResetYuwe();
        ResetUnder();
        ResetMeso();
        ResetMist();
        ResetCly();
        ResetArrTrial();
        ResetHardTrial();
        ResetThorn();
        ResetWhorl();
        ResetSusano();
        ResetLakshmi();
        ResetShinryu();
        ResetTsukuyomi();
        ResetYojimbo();
        byakkoSpreads.Clear();
        suzakuRing = false;
        ResetSeiryu();
        ResetTitania();
        ResetInnocence();
        ResetHades();
        ResetWol();
        ResetHyda();
        ResetAsura();
        ResetZelenia();
        ResetValig();
        ResetArk();
        ResetEnuo();
        ResetNecron();
        ResetQueen();
        ResetZoraal();
        ResetDoom();
        ResetCoilNodes();
        ResetCaduceus();
        ResetTwintania();
        labyrinthLos.Clear();
        syrcusLos.Clear();
        darknessChasers.Clear(); darknessTargets.Clear(); darknessChaseSeen.Clear(); darknessSerial = 0;
        lahabreaArenaAt = null; lahabreaSpheresAt = null; lahabreaArenaChanged = false;
        ResetSteps();
        ResetUltima();
        ResetGarudaEx();
        ResetTitanEx();
        ResetIfritEx();
        ResetGauntlet();
        trialVoidzones.Clear(); trialVoidzoneKey = 0;
        portaParty.Clear();
        carnivaleUnobservedEnds.Clear();
        foreach (var module in carnivaleModules)
        {
            module.Interrupts.Clear();
            module.LastSongs.Clear();
            module.LastSongCasting = false;
            module.LastSongNeedsBootstrap = false;
            module.Ballasts.Clear();
            module.BallastVisuals.Clear();
            module.BallastNeedsBootstrap = false;
            module.Charges.Clear();
            module.ChargeKegs.Clear();
            module.ChargeInputs.Clear();
            module.ChargeKnockback = null;
            module.ChargeNeedsBootstrap = false;
            module.ChargeImmunity.Clear();
            module.VoidzoneNeedsBootstrap = false;
            module.PuddingNeedsBootstrap = false;
            module.VoltageNeedsBootstrap = false;
            module.Voltages.Clear();
            if (module.Eyes != null) module.Eyes = new();
            module.PitNeedsBootstrap = false;
            module.SapNeedsBootstrap = false;
            module.Saps.Clear();
            module.ChimeraNeedsBootstrap = false;
            module.TyphonNeedsBootstrap = false;
            module.CastKnockbacks.Clear();
            module.TyphonFireballs.Clear();
            module.ImpSongHint = "Interrupt!";
            module.RebekkahNeedsBootstrap = false;
            module.ReflectCast = null;
            module.ReflectCasting = false;
            module.ReflectActive = false;
            module.BlindTrigger = null;
            module.BlindCasting = false;
            module.Blindness.Clear();
            module.RayCasts.Clear();
            module.RayPredictions.Clear();
        }
        _casts.Clear(); _finished.Clear(); _effects.Clear(); _ripples.Clear(); _missingRotation.Clear(); sequences.Clear(); statusChanges.Clear();
    }

    private void UpdateCoral()
    {
        if (CoralColor == "Unknown")
            CoralColor = _actors.Values.FirstOrDefault(a => a.Targetable && a.Oid is 0x1E8554 or 0x1E8A8C or 0x1E8A8D)?.Oid switch
            {
                0x1E8554 => "Blue", 0x1E8A8C => "Red", 0x1E8A8D => "Green", _ => "Unknown"
            };
        else if (CoralColor != "Resolved" && _actors.Values.Any(a => a.Targetable && a.Oid == 0x1E8558)) CoralColor = "Resolved";
    }
}

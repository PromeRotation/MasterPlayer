using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter(BozjaDefinition definition)
{
    internal BozjaDefinition Definition { get; } = definition;
    internal bool IsAlbeleo => Definition.Key == "CLL2AlbeleoTheMaleficent";
    internal IGeometryShape Arena { get; private set; } = definition.Arena;
    internal IGeometryShape HostArena => IsSeeker ? SeekerHostArena : Arena is BozjaControlArena control ? control.HostBoundary : Arena;
    internal Vector3 Center { get; private set; } = definition.Center;
    internal float SampleRadius => IsArachne ? ArachneRadius : IsOzma ? OzmaRadius : IsHeavyArtillery ? HeavyRadius : IsRaiden && RaidenReduced ? BaldesionArenas.RaidenDefaultRadius
        : IsCll4 ? Cll4Radius : IsCll3 && ReferenceEquals(Arena, BozjaCll3Catalog.Reduced) ? 25
        : IsQueen ? queenRadius : IsGuard ? GuardSampleRadius : IsSeeker ? SeekerSampleRadius : Arena is BozjaControlArena control ? control.Radius
        : (IsGauntlet || IsCll1) && Arena is RectangleGeometry rectangle ? rectangle.Width * .5f
        : Arena is CircleGeometry circle ? circle.Radius : Definition.SampleRadius;
    internal Dictionary<ulong, BozjaActor> Actors { get; } = [];
    internal List<BozjaCast> Casts { get; } = [];
    internal BozjaActor? Primary { get; private set; }
    internal bool Active { get; private set; }
    internal bool Completed { get; private set; }
    internal ulong SourcePlayerId { get; private set; }
    internal ReplayPartyMember[] Party { get; private set; } = [];
    internal DateTime Now { get; private set; }
    private readonly List<BozjaActor> pending = [];
    private readonly HashSet<BozjaActor> retired = [];
    private long serial;
    private bool wipeInProgress;
    internal int NumFinishedStacks { get; private set; }
    private List<BozjaActor>? hands;
    private List<BozjaActor>? pheromones;
    private ulong tenderStatuses;
    private ulong jealousStatuses;
    private readonly DateTime[,] immunity = new DateTime[64, 3];
    private readonly Dictionary<BozjaRule, string> interruptHints = [];
    internal long LoadVersion { get; private set; }
    internal long LoadOrder { get; private set; }
    internal bool LoadedFromPending { get; private set; }
    internal bool LoadedFromReset { get; private set; }
    private long birthOrder;

    internal void Advance(DateTime time)
    {
        Now = time;
        foreach (var actor in Actors.Values)
        {
            if (actor.Cast is { } cast && time > actor.Clock)
                actor.Elapsed = MathF.Min(actor.Elapsed + (float)(time - actor.Clock).TotalSeconds, Total(cast));
            if ((IsBurn || IsGauntlet || IsAllianceTrash) && actor.RawCast is { } raw && time > actor.Clock)
                actor.RawElapsed = MathF.Min(actor.RawElapsed + (float)(time - actor.Clock).TotalSeconds, BurnRawTotal(raw));
            actor.Clock = time;
        }
    }

    internal void Synchronize(IEnumerable<ReplayActorState> input, ReplayPartyMember[] roster,
        ulong sourcePlayer, DateTime now, bool update = true, IReadOnlyDictionary<ulong, long>? births = null,
        Func<long>? nextPendingOrder = null, bool observeTethers = true)
    {
        BeginCll3Frame();
        Advance(now);
        // This component reads creation order and the previous Party in its
        // callbacks. Only the Framework snapshot may commit those inputs;
        // event pre-scans can run several times before that snapshot.
        if (!observeTethers && Definition.Rules.Any(r => r.Kind == BozjaRuleKind.ChargeTether)) return;
        var values = input.ToArray();
        var present = values.ToDictionary(a => a.Id);
        foreach (var old in Actors.Values.Where(a => !present.TryGetValue(a.Actor.Id, out var next)
            || NewActor(a.Actor, next)).ToArray())
            RemoveActor(old.Actor.Id);
        foreach (var value in values)
            SynchronizeActor(value, births?.GetValueOrDefault(value.Id), observeTethers);
        CompleteSynchronize(roster, sourcePlayer);
        if (update) Update(now, nextPendingOrder);
    }

    internal void RemoveActor(ulong id)
    {
        PrepareRemoveActor(id);
        FinishRemovedActor(id);
        CompleteRemoveActor(id);
    }

    internal void PrepareRemoveActor(ulong id)
    {
        if (!Actors.TryGetValue(id, out var old)) return;
        // ActorState marks the original reference before firing Finish. A
        // failing Finish leaves this flag, old cast and dictionary entry.
        old.Destroyed = true;
        old.Actor = old.Actor with { InCombat = false };
        if (old.ObservedTether.Target != 0)
        {
            Cll1Tether(old, old.ObservedTether, true);
            CavalryUntethered(old, old.ObservedTether);
            SeekerTether(old, old.ObservedTether, true);
            OwainTether(old, old.ObservedTether, true);
            RaidenTether(old, old.ObservedTether, true);
            VirtueTether(old, old.ObservedTether, true);
            old.ObservedTether = default;
        }
    }

    internal void FinishRemovedActor(ulong id)
    {
        if (Actors.TryGetValue(id, out var old) && old.Cast is { } cast) Finish(old, cast);
    }

    internal void CompleteRemoveActor(ulong id)
    {
        if (!Actors.TryGetValue(id, out var old)) return;
        if (Active && !old.IsPet)
            foreach (var status in old.Statuses.OfType<ReplayStatus>()) Status(old, status, false);
        if (IsQueen) Array.Clear(old.Statuses);
        old.Cast = null;
        old.RawCast = null;
        hands?.Remove(old);
        pheromones?.Remove(old);
        graveSpheres?.Remove(old);
        bits?.Remove(old);
        Cll1Destroyed(old);
        Cll4Destroyed(old);
        ArtDestroyed(old);
        OwainDestroyed(old);
        BehemothDestroyed(old);
        SaunionDestroyed(old);
        DiabloDestroyed(old);
        if (gauntletEnemies.TryGetValue(old.Actor.Oid, out var gauntletList)) gauntletList.Remove(old);
        GuardRemoved(old);
        FireDestroyed(old);
        CrystalDestroyed(old);
        HeavyEnergy.Remove(old);
        lightFire?.Remove(old);
        bloodyWraiths?.Remove(old);
        mistyWraiths?.Remove(old);
        dahuAdds?.Remove(old);
        dahuCrowned?.Remove(old);
        timeBombs?.Remove(old);
        Actors.Remove(old.Actor.Id);
        diabloCommitted.Remove(old.Actor.Id);
    }

    internal void SynchronizeActor(ReplayActorState value, long? birth = null, bool observeTethers = true)
    {
        PrepareActor(value, birth, observeTethers);
        FinishActorCast(value);
        var castChanged = InstallActorCast(value);
        CompleteActor(value, observeTethers, castChanged);
        CommitActorInput(value);
    }

    internal void PrepareActor(ReplayActorState value, long? birth, bool observeTethers = true)
    {
        PrepareDiabloInput(value);
        if (!Actors.TryGetValue(value.Id, out var actor))
        {
            actor = new(value) { BirthOrder = birth ?? ++birthOrder };
            Actors.Add(value.Id, actor);
            BehemothCreated(actor);
            FireCreated(actor);
            CrystalCreated(actor);
            PhantomSavageCreated(actor);
            DahuSavageCreated(actor);
            GuardCreated(actor);
            if (value.Oid == 0x1EB17B) timeBombs?.Add(actor);
            if (value.Oid == 0x2DBC) hands?.Add(actor);
            if (value.Oid == 0x2DB6) bits?.Add(actor);
            if (Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Pheromones && r.EnemyOid == value.Oid))
                pheromones?.Add(actor);
            Created(actor);
            SaunionCreated(actor);
            DiabloCreated(actor);
            GauntletCreated(actor);
            PyromancerCreated(actor);
            Cll1Created(actor);
            Cll3Created(actor);
            Cll4Created(actor);
            ArtCreated(actor);
            OwainCreated(actor);
            if (value.Oid == Definition.PrimaryOid)
            {
                ConstructHeavy(actor);
                if (!IsSaunion && !IsGauntlet && Primary is { Destroyed: true } previous && previous.Actor.Id == value.Id)
                    Primary = actor;
                else if (Primary == null && !Definition.OnlyLoadIfTargetable
                    && Actors.GetValueOrDefault(UsesManagerParty0 ? Party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0 : SourcePlayerId) is { Destroyed: false } oldSource
                    && Vector3.DistanceSquared(oldSource.Actor.Position, value.Position) <= 500 * 500)
                    Load(actor);
                else
                {
                    actor.PendingOrder = actor.BirthOrder;
                    pending.Add(actor);
                }
            }
        }
        var becameUntargetable = actor.Actor.Targetable && !value.Targetable;
        actor.Actor = value;
        if (becameUntargetable) GuardUntargetable(actor);
        if (observeTethers) ObserveModelState(actor);
    }

    private static ReplayCast? ObservedCast(BozjaActor actor, ReplayActorState value)
        => actor.NativeNpc && value.Cast is { IsSpell: not false } observed ? observed with
        {
            TargetId = observed.TargetId == 0xE0000000 ? 0 : observed.TargetId,
            // Legacy replay normalization uses its recorded Actor rotation,
            // as ReplayFeedAdapter does. Never consult live objects here.
            Rotation = observed.Rotation ?? value.Rotation,
        } : null;

    internal static bool InputCastChanged(ReplayActorState? committed, ReplayActorState value, bool rawTotal = false)
    {
        var previous = committed?.Cast;
        var current = value.Cast;
        static ulong Target(ReplayCast cast) => cast.TargetId == 0xE0000000 ? 0 : cast.TargetId;
        return previous == null ? current != null : current == null
            || previous.ActionId != current.ActionId || (previous.IsSpell != false) != (current.IsSpell != false)
            || Target(previous) != Target(current) || previous.Location != current.Location
            || (previous.Rotation ?? committed!.Rotation) != (current.Rotation ?? value.Rotation)
            || (rawTotal ? BurnRawTotal(previous) != BurnRawTotal(current) : LegacyTotal(previous) != LegacyTotal(current));
    }

    internal void FinishActorCast(ReplayActorState value, bool? inputCastChanged = null)
    {
        var actor = Actors[value.Id];
        if ((inputCastChanged ?? (IsDiablo ? diabloCastChanged : !SameCast(actor.Cast, ObservedCast(actor, value)))) && actor.Cast is { } cast) Finish(actor, cast);
    }

    internal bool InstallActorCast(ReplayActorState value, bool? inputCastChanged = null)
    {
        var actor = Actors[value.Id];
        var nextCast = ObservedCast(actor, value);
        var changed = inputCastChanged ?? (IsDiablo ? diabloCastChanged : !SameCast(actor.Cast, nextCast));
        if (changed)
        {
            actor.Cast = nextCast;
            if (nextCast != null)
            {
                actor.Elapsed = Math.Clamp((float)(Now - nextCast.StartedAt).TotalSeconds, 0, Total(nextCast));
                actor.Clock = Now;
            }
        }
        return changed;
    }

    internal void CompleteActor(ReplayActorState value, bool observeTethers = true, bool? installedCastChanged = null)
    {
        var actor = Actors[value.Id];
        ObserveBurnRawCast(actor);
        var changed = installedCastChanged ?? InstallActorCast(value);
        if (changed && actor.Cast != null)
        {
            PyromancerThermalStart(actor);
            if (Active) Start(actor);
        }
        ObserveStatuses(actor);
        if (observeTethers) ObservePatriotTether(actor);
    }

    internal void CompleteSynchronize(ReplayPartyMember[] roster, ulong sourcePlayer)
    {
        Party = UsesManagerParty0 ? roster.Where(p => p.Slot >= 0 && p.Slot < (IsDeathgaze ? 64 : 8)).OrderBy(p => p.Slot).ToArray() : roster;
        SourcePlayerId = sourcePlayer;
        ThermalSequenceError = null;
        DiabloSequenceError = null;
    }

    internal void Update(DateTime now, Func<long>? nextPendingOrder = null)
    {
        Advance(now);
        // BossModuleManager pauses module Update while the actual Party[0] actor is absent.
        var viewerId = UsesManagerParty0 ? Party.FirstOrDefault(p => p.Slot == 0)?.EntityId ?? 0 : SourcePlayerId;
        if (Actors.GetValueOrDefault(viewerId) is not { } viewer) return;
        for (var i = pending.Count - 1; i >= 0; --i)
        {
            var candidate = pending[i];
            if (candidate.Destroyed || candidate.Actor.Dead || retired.Contains(candidate))
            {
                pending.RemoveAt(i);
                PyromancerConstructions.Remove(candidate);
                Cll1Constructions.Remove(candidate);
                heavyConstructions.Remove(candidate);
            }
            else if (Primary == null && (!Definition.OnlyLoadIfTargetable || candidate.Actor.Targetable)
                && Vector3.DistanceSquared(viewer.Actor.Position, candidate.Actor.Position) <= 500 * 500)
            {
                Load(candidate, true);
                pending.RemoveAt(i);
            }
        }
        if (Primary is not { } primary) return;
        var wasActive = Active;
        if (!wipeInProgress) UpdateSkirmishArena();
        if (!Active && !Completed && !wipeInProgress
            && (IsAllianceTrash ? AlliancePullReady : (primary.Actor.Targetable || Definition.PullWhileUntargetable) && primary.Actor.InCombat)
            && (!IsSkirmish || currentFate != null) && InPullArea(viewer.Actor.Position))
        {
            Active = true;
            if (IsArt)
            {
                ArtTimeline = new(this);
                ArtTimeline.Begin();
            }
            if (IsOwain)
            {
                OwainTimeline = new(this);
                OwainTimeline.Begin();
            }
            if (IsRaiden) BeginRaiden();
            if (IsVirtue) BeginVirtue();
            if (IsAllianceTrash) AlliancePhase = 0;
            if (IsRhalgr) BeginRhalgr();
            if (IsLions)
            {
                LionsTimeline = new(this);
                LionsTimeline.Begin();
            }
            PyromancerEnterPhase();
            if (IsSavagePhantom)
            {
                PhantomTimeline = new(this);
                PhantomTimeline.Begin();
            }
            if (IsSavageDahu)
            {
                DahuTimeline = new(this);
                DahuTimeline.Begin();
            }
            if (IsCrystals) _ = Crystals.ToArray();
            if (IsIrminsul) _ = IrminsulBlockers.Count;
            if (Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Time))
                timeBombs = Actors.Values.Where(a => a.Actor.Oid == 0x1EB17B).ToList();
            if (Definition.Rules.Any(r => r.Component == "GraspingRancor"))
                hands = Actors.Values.Where(a => a.Actor.Oid == 0x2DBC).ToList();
            if (Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Meteor)) RefreshMeteor();
            if (IsSaunion) BeginSaunion();
            if (IsDiablo) BeginDiablo();
            if (IsGauntlet) ActivateGauntletPhase();
            foreach (var rule in ActiveRules.Where(_ => !IsSavageDahu && !IsSaunion && !IsDiablo && !IsGauntlet))
            {
                PrepareCll1Rule(rule);
                foreach (var actor in Actors.Values)
                {
                    if (IsHeavyArtillery && rule.Component == "Energy") HeavyCreated(actor);
                    if (IsCll1)
                    {
                        BeginCll1Rule(rule, actor);
                        continue;
                    }
                    if (IsCll3)
                    {
                        BeginCll3Rule(actor, rule);
                        continue;
                    }
                    if (IsCll4)
                    {
                        BeginCll4Rule(actor, rule);
                        continue;
                    }
                    if (rule.Kind == BozjaRuleKind.ArenaChange) Created(actor);
                    if (rule.Kind == BozjaRuleKind.GraveArena) GraveCreated(actor);
                    if (rule.Kind == BozjaRuleKind.RobotArena) RobotCreated(actor);
                    if (rule.Kind == BozjaRuleKind.Tower && rule.EnemyOid != 0) CreatedTower(actor, rule);
                    if (rule.Kind is BozjaRuleKind.ArenaOnFinish or BozjaRuleKind.Glimmer) HoofCreatedForRule(actor, rule);
                    if (rule.Kind == BozjaRuleKind.Hammer) HashmalCreated(actor);
                    if (rule.Kind == BozjaRuleKind.SeekerMoon && actor.NativeNpc) SeekerCreated(actor);
                    if (rule.Kind == BozjaRuleKind.GuardAboveBoard && actor.NativeNpc) GuardBoardCreated(actor);
                    FrostCreated(actor, rule);
                    if (IsVirtue && actor.ObservedTether.Id != 0)
                        VirtueTether(actor, actor.ObservedTether, false, rule);
                    if (actor.Cast != null) Start(actor, rule);
                    if (rule.Kind is BozjaRuleKind.BurnEscort or BozjaRuleKind.BurnWeakpoint)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) BurnStatus(actor, status, true, rule.Kind);
                    if (rule.Kind == BozjaRuleKind.OzmaBomb)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) OzmaStatus(actor, status, true);
                    if (rule.Kind == BozjaRuleKind.GallowsMarch)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) GraveStatus(actor, status, true);
                    if (rule.Kind == BozjaRuleKind.RobotMarch)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) RobotStatus(actor, status, true);
                    if (IsCuchulainn)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) CuchStatus(actor, status, true, rule);
                    if (rule.Kind == BozjaRuleKind.GuardParry)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) GuardStatus(actor, status, true);
                    if (rule.Kind is BozjaRuleKind.QueenDoom or BozjaRuleKind.QueenChess or BozjaRuleKind.QueenMeansEnds)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) QueenStatus(actor, status, true, rule.Kind);
                    if (rule.Kind is BozjaRuleKind.QueenKnockback or BozjaRuleKind.AlliancePull or BozjaRuleKind.AllianceKnockback)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) ImmuneStatus(actor, status, true);
                    if (actor.NativeNpc && !actor.Actor.Targetable) GuardUntargetable(actor, rule);
                    if (rule.Kind is BozjaRuleKind.DeadLeaves or BozjaRuleKind.Wind or BozjaRuleKind.BracingWind or BozjaRuleKind.BombStack or BozjaRuleKind.Stormcall or BozjaRuleKind.SeekerMercy)
                        foreach (var status in actor.Statuses.OfType<ReplayStatus>()) Status(actor, status, true);
                    if (rule.Kind == BozjaRuleKind.ElectrochemicalReaction && actor.ObservedTether.Id != 0)
                        PatriotTethered(actor, actor.ObservedTether.Id);
                    if (rule.Kind == BozjaRuleKind.ChargeTether && actor.ObservedTether.Target != 0)
                        CavalryTethered(actor, actor.ObservedTether);
                    if (rule.Kind == BozjaRuleKind.OwainGaze && actor.ObservedTether.Id != 0)
                        OwainTether(actor, actor.ObservedTether, false);
                    if (rule.Kind == BozjaRuleKind.SeekerChains && actor.ObservedTether.Target != 0)
                        SeekerTether(actor, actor.ObservedTether, false);
                }
            }
        }
        if (Active && !wipeInProgress) UpdateSaunion();
        if (Active && !wipeInProgress) UpdateGauntlet();
        if (Active && IsFire && !wipeInProgress && FireComplete())
        {
            Active = false;
            Completed = true;
            Casts.Clear();
        }
        if (Active && IsArt && PrimaryPhaseComplete(primary))
        {
            Active = false;
            Completed = true;
            ClearArt();
            Casts.Clear();
        }
        if (Active && IsOwain && PrimaryPhaseComplete(primary))
        {
            Active = false;
            Completed = true;
            ClearOwain();
            Casts.Clear();
        }
        if (Active)
        {
            UpdateAllianceTrash();
            UpdateRaiden();
            UpdateVirtue();
            UpdateArt();
            OwainTimeline?.Update();
            PhantomTimeline?.Update();
            DahuTimeline?.Update();
            if (IsFire) UpdateFire();
            UpdateMines();
            UpdateRushes();
            UpdateBehemoth();
            UpdateCrystals();
            UpdateHashmal();
            UpdateCavalry();
            UpdateFrost();
            UpdateDahu();
            UpdateSavageDahu();
            UpdateSeeker();
            UpdateGuard();
            UpdateQueen();
            UpdateRobots();
            UpdateColossus();
            UpdateCll3();
        }
        if (Active && !IsAllianceTrash && !IsVirtue && !IsRaiden && !IsFire && !IsSaunion && !IsGauntlet && !wipeInProgress && PrimaryPhaseComplete(primary))
        {
            Active = false;
            Completed = true;
            Casts.Clear();
        }
        UpdateCll1();
        if (Active && SkirmishWantsReset(viewer))
        {
            // Manager appends the reconstructed module to its live loop and
            // updates it again in this frame, including the cold cast replay.
            primary.PendingOrder = nextPendingOrder?.Invoke() ?? ++birthOrder;
            ReconstructPyromancer(primary);
            Load(primary, fromReset: true);
            Update(now, nextPendingOrder);
            return;
        }
        // Manager checks distance after phase update and before completed-module
        // retirement. A non-destroyed actor can return through pending from here.
        if (!Active && Vector3.DistanceSquared(viewer.Actor.Position, primary.Actor.Position) > 500 * 500)
        {
            PyromancerConstructions.Remove(primary);
            Cll1Constructions.Remove(primary);
            heavyConstructions.Remove(primary);
            if (!primary.Destroyed)
            {
                // Manager ActorAdded appends a newly constructed module after
                // all Created callbacks; its queue age is no longer actor age.
                primary.PendingOrder = nextPendingOrder?.Invoke() ?? ++birthOrder;
                ReconstructPyromancer(primary);
                ConstructCll1(primary);
                ConstructHeavy(primary);
                pending.Add(primary);
            }
            Primary = null;
            Completed = false;
            Casts.Clear();
        }
        else if (!Active && (primary.Destroyed || wasActive))
        {
            retired.Add(primary);
            PyromancerConstructions.Remove(primary);
            Cll1Constructions.Remove(primary);
            heavyConstructions.Remove(primary);
            Primary = null;
            Active = false;
            Completed = true;
            Casts.Clear();
        }
    }

    private void Load(BozjaActor primary, bool fromPending = false, bool fromReset = false)
    {
        Casts.Clear();
        AllianceLioness = null;
        LionsTimeline?.Stop();
        LionsTimeline = null;
        ClearRhalgr();
        HeavyEnergy.Clear();
        ClearLightArtillery();
        ClearPatriot();
        ClearMetalFox();
        ClearHoof();
        ClearBehemoth();
        ClearFire();
        ClearShadows();
        ClearTime();
        ClearCrystals();
        irminsulBlockers = null;
        ClearHashmal();
        ClearAida();
        ClearCavalry();
        ClearFrost();
        ClearDahu();
        ClearSavagePhantom();
        ClearSavageDahu();
        ClearSeeker();
        ClearGuard();
        ClearQueen();
        ClearBurn();
        ClearGrave();
        ClearRobots();
        ClearColossus();
        ClearCuchulainn();
        ClearSaunion();
        ClearDiablo();
        ClearGauntlet();
        ClearCll3();
        ClearCll4();
        ClearArt();
        ClearOwain();
        ClearRaiden();
        ClearVirtue();
        ClearOzma();
        ClearArachne();
        ClearDeathgaze();
        hands = null;
        pheromones = null;
        tenderStatuses = jealousStatuses = 0;
        Array.Clear(immunity);
        Arena = Definition.Arena;
        Center = Definition.Center;
        interruptHints.Clear();
        NumFinishedStacks = 0;
        Primary = primary;
        ResetHeavyArena(primary);
        ResetSkirmishArena(primary);
        RestoreCll1Arena();
        ++LoadVersion;
        LoadOrder = fromPending || fromReset ? primary.PendingOrder : primary.BirthOrder;
        LoadedFromPending = fromPending;
        LoadedFromReset = fromReset;
        Active = false;
        Completed = false;
    }

    internal void Start(BozjaActor actor)
    {
        foreach (var rule in ActiveRules) Start(actor, rule);
    }

    private void Start(BozjaActor actor, BozjaRule rule)
    {
        if (!Active || !actor.NativeNpc || actor.Cast is not { } cast || !rule.Actions.Contains(cast.ActionId)) return;
        if (StartAllianceTrash(actor, rule) || StartRhalgr(rule)) return;
        if (rule.Kind == BozjaRuleKind.TimedCastHint)
        {
            Casts.Add(new(++serial, rule, actor, cast.Location, 0, Activation(actor)));
            return;
        }
        if (rule.Kind == BozjaRuleKind.CircleBait)
        {
            StartCircleBait(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.VirtueAurora
            or BozjaRuleKind.VirtueRays or BozjaRuleKind.VirtueCounter)
        {
            StartVirtue(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.RaidenArena or BozjaRuleKind.RaidenSpread or BozjaRuleKind.RaidenLancing
            or BozjaRuleKind.RaidenChains)
        {
            StartRaiden(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.OwainElemental or BozjaRuleKind.OwainCastHint or BozjaRuleKind.OwainCleave)
        {
            StartOwain(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.ArtLegend or BozjaRuleKind.ArtPredict or BozjaRuleKind.ArtChase)
        {
            StartArt(actor, rule);
            return;
        }
        if (rule.Kind == BozjaRuleKind.Tower && rule.EnemyOid != 0) return;
        if (rule.Kind is BozjaRuleKind.Cll4Arena or BozjaRuleKind.Cll4Hammer or BozjaRuleKind.Cll4Plumes
            or BozjaRuleKind.Cll4Obey or BozjaRuleKind.Cll4Wind)
        {
            StartCll4(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.Cll3Arena or BozjaRuleKind.Cll3Water or BozjaRuleKind.Cll3StayMove)
        {
            StartCll3(actor, rule);
            return;
        }
        if (IsCll1 && rule.Kind == BozjaRuleKind.Cll1Custom)
        {
            StartCll1(actor, rule);
            return;
        }
        if (IsGauntlet && rule.Kind is BozjaRuleKind.GauntletArena or BozjaRuleKind.GauntletImpact
            or BozjaRuleKind.GauntletTerminus or BozjaRuleKind.GauntletUnseen or BozjaRuleKind.GauntletDonut
            or BozjaRuleKind.GauntletTurbine or BozjaRuleKind.GauntletStorm or BozjaRuleKind.GauntletWind or BozjaRuleKind.GauntletPain)
        {
            StartGauntlet(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.DiabloArena or BozjaRuleKind.DiabloRuinous or BozjaRuleKind.DiabloNox or BozjaRuleKind.DiabloKnockback)
        {
            StartDiablo(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.SaunionMobile or BozjaRuleKind.SaunionSpiral or BozjaRuleKind.SaunionBait or BozjaRuleKind.SaunionKnockback)
        {
            StartSaunion(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.SaunionPlumes or BozjaRuleKind.SaunionMind or BozjaRuleKind.SaunionObey) return;
        if (IsCuchulainn && rule.Kind is BozjaRuleKind.CuchFlesh or BozjaRuleKind.CuchJumps or BozjaRuleKind.CuchMarch or BozjaRuleKind.Raidwide)
        {
            StartCuchulainn(actor, rule);
            return;
        }
        if (rule.Kind == BozjaRuleKind.ColossusRotation)
        {
            StartColossus(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.RobotArena or BozjaRuleKind.RobotMarch) return;
        if (rule.Kind is BozjaRuleKind.RobotOrder or BozjaRuleKind.RobotTrain)
        {
            StartRobot(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.GraveArena or BozjaRuleKind.SoulPurge)
        {
            StartGrave(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.BurnMissile or BozjaRuleKind.BurnEscort or BozjaRuleKind.BurnBoss or BozjaRuleKind.BurnWeakpoint)
        {
            StartBurn(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.QueenArena or BozjaRuleKind.QueenKnockback or BozjaRuleKind.QueenMeansEnds)
        {
            StartQueen(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.GuardParry or BozjaRuleKind.GuardAboveBoard or BozjaRuleKind.GuardTurrets)
        {
            StartGuard(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.SeekerArena or BozjaRuleKind.SeekerBlade or BozjaRuleKind.SeekerSplitter)
        {
            StartSeeker(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.SeekerMercy or BozjaRuleKind.SeekerMoon or BozjaRuleKind.SeekerIconBait or BozjaRuleKind.SeekerChains or BozjaRuleKind.SeekerLine) return;
        if (rule.Kind == BozjaRuleKind.DahuSavageKnockback)
        {
            StartSavageDahuKnockback(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.DahuSpit or BozjaRuleKind.DahuBurn) return;
        if (rule.Kind == BozjaRuleKind.PhantomCreeping)
        {
            StartPhantomCreeping(actor, rule);
            return;
        }
        if (rule.Kind == BozjaRuleKind.PhantomCounter) return;
        if (rule.Kind is BozjaRuleKind.PhantomExaflare or BozjaRuleKind.PhantomKnockback)
        {
            StartPhantom(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.DahuShockwave or BozjaRuleKind.DahuRotation or BozjaRuleKind.DahuKnockback or BozjaRuleKind.DahuBait)
        {
            StartDahu(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.BracingWind or BozjaRuleKind.LunarCry or BozjaRuleKind.ThermalGust or BozjaRuleKind.EndlessFrost)
        {
            StartFrost(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.LightningRod or BozjaRuleKind.ElectrochemicalReaction) return;
        if (rule.Kind is BozjaRuleKind.ArenaOnFinish or BozjaRuleKind.Glimmer) return;
        if (rule.Kind == BozjaRuleKind.Thunderbolt) return;
        if (rule.Kind == BozjaRuleKind.Hailfire) return;
        if (rule.Kind is BozjaRuleKind.Charge or BozjaRuleKind.ForwardKnockback or BozjaRuleKind.SideKnockback or BozjaRuleKind.ChargeTether)
        {
            StartCavalry(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.LightningClap or BozjaRuleKind.Burn)
        {
            StartAida(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.ControlTower or BozjaRuleKind.Exaflare or BozjaRuleKind.Hammer)
        {
            StartHashmal(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.CrystalSight or BozjaRuleKind.Earthbreaker)
        {
            StartCrystals(actor, rule);
            return;
        }
        if (rule.Kind == BozjaRuleKind.Time)
        {
            StartTime(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.Stormcall or BozjaRuleKind.Foreshadowing)
        {
            StartShadows(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.BombStack or BozjaRuleKind.Cleave) return;
        if (rule.Kind is BozjaRuleKind.Countdown or BozjaRuleKind.TooHot)
        {
            StartFire(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.Meteor or BozjaRuleKind.Juice or BozjaRuleKind.DelayedRaidwide or BozjaRuleKind.Horn)
        {
            StartBehemoth(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.Gaze or BozjaRuleKind.CastGaze or BozjaRuleKind.DelayedTankbuster)
        {
            StartHoof(actor, rule);
            return;
        }
        if (rule.Kind is BozjaRuleKind.MagitekBitLasers or BozjaRuleKind.Rush)
        {
            StartMetalFox(actor, rule);
            return;
        }
        var target = rule.Kind is BozjaRuleKind.Stack or BozjaRuleKind.CrystalSpread or BozjaRuleKind.Spread ? Actors.GetValueOrDefault(cast.TargetId) : null;
        if (rule.Kind is BozjaRuleKind.Stack or BozjaRuleKind.CrystalSpread or BozjaRuleKind.Spread && target == null) return;
        if (rule.Kind == BozjaRuleKind.Hellpounce)
        {
            ActivatePounce(rule, actor, actor.Actor.Position, cast.Location, Activation(actor));
            return;
        }
        if (rule.Kind is BozjaRuleKind.ArenaChange or BozjaRuleKind.Wind)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor,
                rule.Kind == BozjaRuleKind.ArenaChange ? Center : cast.Location,
                rule.Kind == BozjaRuleKind.ArenaChange ? 0 : cast.Rotation ?? 0,
                Activation(actor).AddSeconds(rule.Kind == BozjaRuleKind.ArenaChange ? .8d : .1d))
                { Action = cast.ActionId });
            return;
        }
        GeometryTemplate? shape = rule.Kind == BozjaRuleKind.VoidQuake ? cast.ActionId switch
        {
            20549 => new CircleTemplate(10),
            20550 => new DonutTemplate(10, 20),
            20551 => new DonutTemplate(20, 30),
            _ => null,
        } : null;
        Casts.Add(new(++serial, rule, actor, cast.Location,
            rule.Kind == BozjaRuleKind.VoidQuake ? 0 : cast.Rotation ?? 0, Activation(actor))
            { Target = target, ShapeOverride = shape, Action = cast.ActionId });
        if (rule.Kind == BozjaRuleKind.Interrupt) UpdateInterruptHint(rule);
        if (rule.GroupedFinish && Casts.Count(c => c.Rule == rule) >= rule.ExpectedNumCasters)
        {
            var sorted = Casts.Where(c => c.Rule == rule).ToArray();
            SortActivations(sorted, 0, sorted.Length - 1, c => c.Activation);
            var index = 0;
            for (var i = 0; i < Casts.Count; ++i)
                if (Casts[i].Rule == rule) Casts[i] = sorted[index++];
        }
    }

    private void ActivatePounce(BozjaRule rule, BozjaActor actor, Vector3 source, Vector3 target, DateTime activation)
    {
        var delta = new Vector2(target.X - source.X, target.Z - source.Z);
        Casts.RemoveAll(c => c.Rule == rule);
        Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(source),
            MathF.Atan2(delta.X, delta.Y), activation)
            { ShapeOverride = RayRectangleTemplate.Symmetric(10, delta.Length()) });
    }

    private static void SortActivations<T>(T[] list, int left, int right, Func<T, DateTime> activation)
    {
        // Match source RefSort's right-pivot partition. Equal clocks are
        // observably reordered, so a stable LINQ sort changes queue order.
        while (left < right)
        {
            var pivot = activation(list[right]);
            var store = left;
            for (var i = left; i < right; ++i)
                if (activation(list[i]) < pivot)
                {
                    (list[store], list[i]) = (list[i], list[store]);
                    ++store;
                }
            (list[store], list[right]) = (list[right], list[store]);
            if (store - left < right - store)
            {
                SortActivations(list, left, store - 1, activation);
                left = store + 1;
            }
            else
            {
                SortActivations(list, store + 1, right, activation);
                right = store - 1;
            }
        }
    }

    internal void Finish(BozjaActor actor, ReplayCast spell)
    {
        PyromancerThermalFinish(actor, spell);
        if (!Active) return;
        foreach (var rule in ActiveRules)
        {
            if (FinishAllianceTrash(actor, rule, spell)
                || IsRhalgr && rule.Component is "BoltsFromTheBlue" or "DestructiveCharge") continue;
            if (rule.Kind is BozjaRuleKind.OwainElemental or BozjaRuleKind.OwainSpirit or BozjaRuleKind.OwainGaze or BozjaRuleKind.OwainCleave) continue;
            if (rule.Kind == BozjaRuleKind.OwainCastHint)
            {
                if (rule.Actions.Contains(spell.ActionId))
                {
                    var hintIndex = Casts.FindIndex(c => c.Rule == rule && ReferenceEquals(c.Source, actor));
                    if (hintIndex >= 0) Casts.RemoveAt(hintIndex);
                }
                continue;
            }
            if (rule.Kind is BozjaRuleKind.ArtLegend or BozjaRuleKind.ArtPredict or BozjaRuleKind.ArtStay or BozjaRuleKind.ArtChase) continue;
            if (rule.Kind is BozjaRuleKind.Cll4Arena or BozjaRuleKind.Cll4Hammer or BozjaRuleKind.Cll4Plumes
                or BozjaRuleKind.Cll4Obey or BozjaRuleKind.Cll4Wind)
            {
                FinishCll4(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.Cll3Arena or BozjaRuleKind.Cll3StayMove) continue;
            if (IsCll1 && rule.Kind == BozjaRuleKind.Cll1Custom)
            {
                FinishCll1(spell, rule);
                continue;
            }
            if (IsQueen) FinishQueen(actor, spell, rule);
            if (IsGauntlet && rule.Kind is BozjaRuleKind.GauntletArena or BozjaRuleKind.GauntletImpact
                or BozjaRuleKind.GauntletTerminus or BozjaRuleKind.GauntletUnseen or BozjaRuleKind.GauntletDonut
                or BozjaRuleKind.GauntletTurbine or BozjaRuleKind.GauntletStorm or BozjaRuleKind.GauntletWind or BozjaRuleKind.GauntletPain)
            {
                FinishGauntlet(actor, spell, rule);
                continue;
            }
            if (IsDiablo && rule.Kind is BozjaRuleKind.DiabloArena or BozjaRuleKind.DiabloRuinous or BozjaRuleKind.DiabloNox or BozjaRuleKind.DiabloOrbs or BozjaRuleKind.DiabloKnockback or BozjaRuleKind.DiabloBomb)
            {
                DiabloFinish(actor, spell, rule);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.SaunionKnockback)
            {
                if (rule.Actions.Contains(spell.ActionId))
                {
                    var kb = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
                    if (kb >= 0) Casts.RemoveAt(kb);
                }
                continue;
            }
            if (rule.Kind == BozjaRuleKind.CircleBait)
            {
                FinishCircleBait(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.VirtueAurora or BozjaRuleKind.VirtueRays)
            {
                FinishVirtue(actor, rule, spell);
                continue;
            }
            if (!rule.GroupedFinish && !rule.Actions.Contains(spell.ActionId)) continue;
            if (rule.Kind is BozjaRuleKind.RaidenArena or BozjaRuleKind.RaidenSpread or BozjaRuleKind.RaidenLancing
                or BozjaRuleKind.RaidenChains) continue;
            if (rule.Kind == BozjaRuleKind.TimedCastHint)
            {
                var hintIndex = Casts.FindIndex(c => c.Rule == rule && ReferenceEquals(c.Source, actor));
                if (hintIndex >= 0) Casts.RemoveAt(hintIndex);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.Tower && rule.EnemyOid != 0) continue;
            if (rule.Kind == BozjaRuleKind.SaunionPlumes)
            {
                Casts.RemoveAll(c => c.Rule == rule);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.SaunionMobile or BozjaRuleKind.SaunionSpiral or BozjaRuleKind.SaunionMind or BozjaRuleKind.SaunionObey) continue;
            if (rule.Kind is BozjaRuleKind.CuchFlesh or BozjaRuleKind.CuchJumps or BozjaRuleKind.CuchMarch) continue;
            if (IsCuchulainn && rule.Kind == BozjaRuleKind.Raidwide)
            {
                foreach (var action in rule.Actions.Where(a => a == spell.ActionId))
                {
                    var rawIndex = Casts.FindIndex(c => c.Rule == rule && ReferenceEquals(c.Source, actor));
                    if (rawIndex >= 0) Casts.RemoveAt(rawIndex);
                }
                continue;
            }
            if (rule.Kind == BozjaRuleKind.ColossusRotation) continue;
            if (rule.Kind is BozjaRuleKind.RobotArena or BozjaRuleKind.RobotOrder or BozjaRuleKind.RobotTrain)
            {
                FinishRobot(actor, spell, rule);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.GraveArena or BozjaRuleKind.SoulPurge) continue;
            if (rule.Kind is BozjaRuleKind.BurnMissile or BozjaRuleKind.BurnEscort or BozjaRuleKind.BurnBoss) continue;
            if (rule.Kind == BozjaRuleKind.BurnWeakpoint)
            {
                burnWeakpointCasters.Remove(actor);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.QueenArena or BozjaRuleKind.QueenKnockback or BozjaRuleKind.QueenMeansEnds or BozjaRuleKind.QueenChess or BozjaRuleKind.QueenDoom) continue;
            if (rule.Kind is BozjaRuleKind.GuardArena or BozjaRuleKind.GuardParry or BozjaRuleKind.GuardAboveBoard or BozjaRuleKind.GuardTurrets) continue;
            if (IsGuard && rule.Kind is BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster or BozjaRuleKind.Enrage)
            {
                var casterIndex = Casts.FindIndex(c => c.Rule == rule && ReferenceEquals(c.Source, actor));
                if (casterIndex >= 0) Casts.RemoveAt(casterIndex);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.SeekerSplitter)
            {
                Casts.RemoveAll(c => c.Rule == rule);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.SeekerArena or BozjaRuleKind.SeekerBlade or BozjaRuleKind.SeekerMercy or BozjaRuleKind.SeekerMoon or BozjaRuleKind.SeekerIconBait or BozjaRuleKind.SeekerChains or BozjaRuleKind.SeekerLine) continue;
            if (rule.Kind is BozjaRuleKind.DahuSavageKnockback or BozjaRuleKind.DahuSpit or BozjaRuleKind.DahuBurn) continue;
            if (rule.Kind == BozjaRuleKind.PhantomCreeping)
            {
                FinishPhantomCreeping(rule, spell);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.PhantomCounter) continue;
            if (rule.Kind is BozjaRuleKind.PhantomExaflare or BozjaRuleKind.PhantomKnockback)
            {
                FinishPhantom(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.DahuShockwave or BozjaRuleKind.DahuRotation or BozjaRuleKind.DahuKnockback or BozjaRuleKind.DahuBait)
            {
                FinishDahu(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.BracingWind or BozjaRuleKind.LunarCry or BozjaRuleKind.ThermalGust or BozjaRuleKind.EndlessFrost)
            {
                FinishFrost(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.ArenaChange or BozjaRuleKind.Wind) continue;
            if (rule.Kind is BozjaRuleKind.LightningRod or BozjaRuleKind.ElectrochemicalReaction) continue;
            if (rule.Kind == BozjaRuleKind.MagitekBitLasers) continue;
            if (rule.Kind == BozjaRuleKind.Time) continue;
            if (rule.Kind == BozjaRuleKind.Hailfire) continue;
            if (rule.Kind == BozjaRuleKind.ChargeTether) continue;
            if (rule.Kind is BozjaRuleKind.ForwardKnockback or BozjaRuleKind.SideKnockback)
            {
                FinishCavalry(actor, rule);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.LightningClap or BozjaRuleKind.Burn) continue;
            if (rule.Kind is BozjaRuleKind.ControlTower or BozjaRuleKind.Exaflare or BozjaRuleKind.Hammer)
            {
                FinishHashmal(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.CrystalSight or BozjaRuleKind.Earthbreaker or BozjaRuleKind.CrystalSpread or BozjaRuleKind.Spread)
            {
                FinishCrystals(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.Stormcall or BozjaRuleKind.Foreshadowing)
            {
                FinishShadows(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.Glimmer or BozjaRuleKind.DelayedTankbuster) continue;
            if (rule.Kind is BozjaRuleKind.DelayedRaidwide or BozjaRuleKind.Horn) continue;
            if (rule.Kind is BozjaRuleKind.BombStack or BozjaRuleKind.Cleave) continue;
            if (rule.Kind == BozjaRuleKind.Countdown)
            {
                FinishCountdown(spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.Meteor or BozjaRuleKind.Juice or BozjaRuleKind.Thunderbolt)
            {
                FinishBehemoth(actor, rule, spell);
                continue;
            }
            if (rule.Kind is BozjaRuleKind.Gaze or BozjaRuleKind.CastGaze or BozjaRuleKind.ArenaOnFinish)
            {
                FinishHoof(actor, rule, spell);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.Rush)
            {
                var bait = Rushes.FindIndex(b => b.Source.Actor.Id == actor.Actor.Id);
                if (bait >= 0) Rushes.RemoveAt(bait);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.DeadLeaves)
            {
                Casts.RemoveAll(c => c.Rule == rule);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.Hellpounce)
            {
                if (spell.ActionId == 20539)
                    ActivatePounce(rule, actor, spell.Location, 2 * Definition.Center - spell.Location, Now.AddSeconds(3.7d));
                else Casts.RemoveAll(c => c.Rule == rule);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.VoidQuake)
            {
                var head = Casts.FindIndex(c => c.Rule == rule);
                if (head >= 0) Casts.RemoveAt(head);
                continue;
            }
            if (rule.Kind == BozjaRuleKind.Stack)
            {
                var stack = Casts.FindIndex(c => c.Rule == rule && c.Target?.Actor.Id == spell.TargetId);
                if (stack >= 0)
                {
                    Casts.RemoveAt(stack);
                    ++NumFinishedStacks;
                }
                continue;
            }
            var reference = rule.Kind is BozjaRuleKind.Raidwide or BozjaRuleKind.Tankbuster or BozjaRuleKind.CastHint or BozjaRuleKind.Interrupt or BozjaRuleKind.Enrage;
            var index = Casts.FindIndex(c => c.Rule == rule && (reference
                ? ReferenceEquals(c.Source, actor) : c.Source.Actor.Id == actor.Actor.Id));
            if (index >= 0) Casts.RemoveAt(index);
            if (rule.Kind == BozjaRuleKind.Interrupt) UpdateInterruptHint(rule);
        }
    }

    internal void Director(uint command)
    {
        if (command == 0x40000005)
        {
            wipeInProgress = true;
            if (Primary != null)
            {
                retired.Add(Primary);
                PyromancerConstructions.Remove(Primary);
                Cll1Constructions.Remove(Primary);
                heavyConstructions.Remove(Primary);
            }
            Primary = null;
            Active = false;
            Casts.Clear();
            ClearRhalgr();
            HeavyEnergy.Clear();
            ClearLightArtillery();
            ClearPatriot();
            ClearMetalFox();
            ClearHoof();
            ClearBehemoth();
            ClearFire();
            ClearShadows();
            ClearTime();
            ClearCrystals();
            ClearHashmal();
            ClearAida();
            ClearCavalry();
            ClearFrost();
            ClearDahu();
            ClearSavagePhantom();
            ClearSavageDahu();
            ClearSeeker();
            ClearGuard();
            ClearQueen();
            ClearBurn();
            ClearGrave();
            ClearRobots();
            ClearSaunion();
            ClearDiablo();
            ClearGauntlet();
            ClearCll3();
            ClearCll4();
            ClearArt();
            ClearOwain();
            ClearRaiden();
            ClearVirtue();
            ClearOzma();
            ClearArachne();
            ClearDeathgaze();
            interruptHints.Clear();
        }
        else if (command == 0x40000011) wipeInProgress = false;
    }

    internal void Clear()
    {
        Primary = null;
        currentFate = null;
        ResetSkirmishArena(null);
        PyromancerConstructions.Clear();
        Cll1Constructions.Clear();
        heavyConstructions.Clear();
        Active = Completed = wipeInProgress = false;
        Casts.Clear(); Actors.Clear(); pending.Clear(); retired.Clear(); Party = [];
        irminsulBlockers = null;
        cll3PreviousPositions.Clear();
        diabloCommitted.Clear();
        ClearRhalgr();
        HeavyEnergy.Clear();
        ClearLightArtillery();
        ClearPatriot();
        ClearMetalFox();
        ClearHoof();
        ClearBehemoth();
        ClearFire();
        ClearShadows();
        ClearTime();
        ClearCrystals();
        ClearHashmal();
        ClearAida();
        ClearCavalry();
        ClearFrost();
        ClearDahu();
        ClearSavagePhantom();
        ClearSavageDahu();
        ClearSeeker();
        SourcePlayerId = 0;
        ClearGuard();
        ClearQueen();
        ClearBurn();
        ClearGrave();
        ClearRobots();
        ClearColossus();
        NumFinishedStacks = 0;
        ClearCuchulainn();
        ClearSaunion();
        ClearDiablo();
        ClearGauntlet();
        ClearCll3();
        ClearCll4();
        ClearArt();
        ClearOwain();
        ClearRaiden();
        ClearVirtue();
        ClearOzma();
        ClearArachne();
        ClearDeathgaze();
        hands = null;
        pheromones = null;
        tenderStatuses = jealousStatuses = 0;
        Array.Clear(immunity);
        Arena = Definition.Arena;
        Center = Definition.Center;
        interruptHints.Clear();
    }

    internal DateTime Activation(BozjaActor actor, double extra = 0)
        => actor.Cast == null ? default : Now.AddSeconds(Total(actor.Cast) + .3f - actor.Elapsed + extra);
    private bool InPullArea(Vector3 position)
        => IsArt || IsOwain ? DistanceSquared(position, Center) < 1e4f
            : IsSkirmish ? DistanceSquared(position, Center) <= SampleRadius * SampleRadius : Definition.SquarePull
            ? MathF.Abs(position.X - Center.X) <= Definition.PullRadius && MathF.Abs(position.Z - Center.Z) <= Definition.PullRadius
            : DistanceSquared(position, Center) <= Definition.PullRadius * Definition.PullRadius;
    private float Total(ReplayCast cast)
        => UsesRawNpcClock ? BurnRawTotal(cast) : LegacyTotal(cast);
    private static float LegacyTotal(ReplayCast cast)
        => Math.Max(.05f, BurnRawTotal(cast));
    private bool SameCast(ReplayCast? a, ReplayCast? b)
        => a == null ? b == null : b != null && a.ActionId == b.ActionId && a.TargetId == b.TargetId
            && a.Location == b.Location && a.Rotation == b.Rotation && Total(a) == Total(b);
    internal static bool NewActor(ReplayActorState a, ReplayActorState b)
        => a.Oid != b.Oid || a.GameObjectId != 0 && b.GameObjectId != 0 && a.GameObjectId != b.GameObjectId
            || a.ObjectIndex != null && b.ObjectIndex != null && a.ObjectIndex != b.ObjectIndex;
    internal static float DistanceSquared(Vector3 a, Vector3 b)
        => (a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z);
}

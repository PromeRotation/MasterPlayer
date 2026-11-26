using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.MechanicHost;
using MasterPlayer.Scripting;
using MasterPlayer.Replay;
using MasterPlayer.Settings;
using PromeRotation.LogSystem;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Updaters;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace MasterPlayer.Mechanics.Dungeons;

public abstract class DungeonScript(uint territory) : MasterPlayerScript
{
    private DungeonEncounter? _encounter;
    private DungeonOutput? _output;
    private DungeonEventAdapter? _events;
    private readonly Dictionary<string, DateTime> _messages = [];
    private string? previousPrecombatHint;
    private DateTime _nextScan;
    private bool _suspended;
    private long _replayVersion = -1;
    private readonly DungeonObjectInteraction fruitInteraction = new();
    private DateTime nextSlimeCleanse;
    private DateTime nextSirenImmunity;
    private readonly SkallaDutyControl skallaDuty = new();
    private DateTime nextDohnSprint;
    private DateTime nextGulgSprint;
    private readonly CosmosSeedDrop cosmosSeedDrop = new();
    private VanaspatiMisdirectionControl? vanaspatiMisdirection;
    private DateTime nextKtisisInterrupt;
    private DateTime nextLapisStun;
    private readonly LakshmiVrilControl lakshmiVril = new();
    private readonly CaduceusJumpControl caduceusJump = new();
    private readonly DungeonActionControl twintaniaActions = new();
    public bool EncounterActive => _encounter?.Active != null;
    public override ScriptTimeline? GetTimeline()
        => !_suspended && _encounter is { IsThordan: true, Active: not null }
            ? _encounter.Thordan.Timeline.Capture(CurrentTime) : null;
    internal bool ObservePalaceBoss(DateTime now)
    {
        if (!MasterPlayer.Mechanics.PalaceOfTheDead.PalaceCatalog.IsEarlyTerritory(territory)) return false;
        // The floor layer can observe before this script's scheduled update.
        // Publish the corresponding output here too; otherwise Scan advances
        // its throttle and the later update would skip Boss rendering/cleanup.
        Scan(now);
        Render(now);
        return EncounterActive;
    }
    internal bool ObserveHeavenOnHighBoss(DateTime now)
        => ObserveDeepDungeonBoss(now, 2);

    internal bool ObserveDeepDungeonBoss(DateTime now, byte dungeonId)
    {
        if (!(dungeonId == 2 ? HeavenOnHighCatalog.IsTerritory(territory)
            : dungeonId == 3 && EurekaOrthosCatalog.IsTerritory(territory))) return false;
        Scan(now);
        Render(now);
        return EncounterActive;
    }
    public bool PreservesTerritoryMechanics => HeavenswardIntroCatalog.IsTerritory(territory) || JobQuestCatalog.IsTerritory(territory) || MainScenarioQuestCatalog.IsTerritory(territory)
        || NativeTreasureCatalog.IsTerritory(territory) || NoviceCatalog.IsTerritory(territory) || NativeFateCatalog.IsTerritory(territory) || HeavenOnHighCatalog.IsTerritory(territory) || (EurekaOrthosCatalog.IsTerritory(territory) || territory is QuantumCatalog.TERRITORY or HalicarnassusCatalog.TERRITORY or AlteRoiteCatalog.TERRITORY or WickedThunderCatalog.TERRITORY or KefkaCatalog.TERRITORY or ThordanCatalog.TERRITORY) || territory == CarnivaleCatalog.TERRITORY;

    internal bool HandlesCast(LogSystemCastStartEvent cast)
    {
        if (!PreservesTerritoryMechanics || _suspended || _encounter == null) return false;
        var thordanInputScan = _encounter.ThordanInputScan;
        var halicarnassusInputScan = _encounter.HalicarnassusInputScan;
        var alteRoiteInputScan = _encounter.AlteRoiteInputScan;
        _encounter.ThordanInputScan = thordanInputScan || _encounter.IsThordan;
        _encounter.HalicarnassusInputScan = halicarnassusInputScan || _encounter.IsHalicarnassus;
        _encounter.AlteRoiteInputScan = alteRoiteInputScan || _encounter.IsAlteRoite;
        try { Scan(CurrentTime); }
        finally
        {
            _encounter.ThordanInputScan = thordanInputScan;
            _encounter.HalicarnassusInputScan = halicarnassusInputScan;
            _encounter.AlteRoiteInputScan = alteRoiteInputScan;
        }
        if (_encounter.IsAlteRoite)
            return _encounter.Active != null && _encounter.AlteRoite.Actors.TryGetValue(cast.SourceId, out var alteRoiteSource)
                && alteRoiteSource.NativeNpc && alteRoiteSource.Cast?.IsSpell != false
                && AlteRoiteCatalog.ACTIONS.Contains(cast.ActionId);
        if (_encounter.IsHalicarnassus)
            return _encounter.Active != null && _encounter.Halicarnassus.Actors.TryGetValue(cast.SourceId, out var halicarnassusSource)
                && halicarnassusSource.NativeNpc && halicarnassusSource.Cast?.IsSpell != false
                && HalicarnassusCatalog.ACTIONS.Contains(cast.ActionId);
        if (_encounter.IsThordan)
            return _encounter.Active != null && _encounter.Actors.TryGetValue(cast.SourceId, out var thordanSource)
                && !thordanSource.Player && !thordanSource.Buddy
                && thordanSource.NativeType is not (0x104 or 0x202 or 0x203 or 0x209)
                && thordanSource.Cast?.IsSpell != false && ThordanCatalog.ACTIONS.Contains(cast.ActionId);
        return _encounter.Active is { } active
            && _encounter.Actors.TryGetValue(cast.SourceId, out var actor) && (!actor.Dead || _encounter.UsesDeepDungeonCastIdentity || _encounter.IsBarPassage || _encounter.IsCarnivale || NativeFateCatalog.IsTerritory(territory))
            && (!(NativeTreasureCatalog.IsTerritory(territory) || _encounter.IsCarnivale) || !actor.Player && !actor.Buddy && !actor.FriendlyNpc)
            && (active.Casts.Any(r => r.Action == cast.ActionId && (r.CasterOid == actor.Oid || r.CasterOid == 0))
                || active.BossOid == NativeTreasureCatalog.GYMNASIOU_MEGAKANTHA && NativeTreasureCatalog.OdiousEffect(cast.ActionId)
                || active.BossOid == NativeTreasureCatalog.GWYDDNEU && actor.Oid == 0x48A6 && cast.ActionId == 43655
                || _encounter.IsCarnivale && CarnivaleCatalog.IgnoredCast(active.BossOid, cast.ActionId)
                || _encounter.IsBarPassageP2 && cast.ActionId is 40907 or 40908
                || _encounter.IsBarPassageP1 && cast.ActionId is 41958 or 42026 or 40902);
    }
    public string? PredictedDamageText
    {
        get
        {
            if (_suspended || _encounter?.Active == null || CurrentPlayer is not { Dead: false }) return null;
            var groups = _encounter.PredictedDamage.GroupBy(p => p.DamageType).OrderBy(g => g.Key).ToArray();
            if (groups.Length == 0) return null;
            return string.Join("，", groups.Select(g =>
                $"{g.Key}：{g.Select(p => p.TargetId).Distinct().Count()}人/{g.Select(p => p.Activation).Distinct().Count()}次"));
        }
    }
    protected virtual string MechanicAssignment => "Unassigned";
    protected virtual IReadOnlyList<string> PartyMechanicAssignments => [];

    public override IEnumerable<string> GetMechanisms() => ArrDungeonCatalog.Create(territory).Select(d => d.Name)
        .Concat(territory == 1036 ? ["水纹预览", "文字提示"] : ["文字提示"]);

    protected override void OnStart()
    {
        _encounter = new(territory, LogError);
        _output = new(SafeZone, Draw, MasterPlayerPlugin.Instance!.SafeZone.Manager,
            teleporters => MasterPlayerPlugin.Instance?.SafeZone.SetTeleporters(this, teleporters));
        _events = new(_encounter, () => Scan(CurrentTime), e => CastRotation.TryGet(e, out var r) ? r : null, LogError);
        if (!PreservesTerritoryMechanics)
            SuppressGenericDodge(ArrDungeonCatalog.ClaimedActions(_encounter.Definitions).ToArray());
        OnLogEvent(HandleEvent);
        OnBattleEnded(() =>
        {
            if (_encounter?.PreserveStageAcrossBattleEnd == true)
            {
                _output?.Clear();
                _nextScan = default;
                Render(CurrentTime);
            }
            else Suspend();
        });
        OnPlayerDied(() =>
        {
            if (_encounter?.PreserveFightAcrossPlayerDeath == true)
            {
                _nextScan = default;
                Hints.ClearActionRestrictions();
                Hints.ClearFacing();
                Hints.SetTarget(0);
                Hints.SetMovementHold(false);
            }
            else Suspend();
        });
        OnBattleStarted(() => { _suspended = false; _nextScan = default; });
        OnPlayerRevived(() => { _suspended = false; _nextScan = default; });
        Scan(CurrentTime);
        Render(CurrentTime);
    }

    protected override void OnUpdate(float deltaTime)
    {
        var now = CurrentTime;
        UpdateVanaspatiMisdirection();
        if (now < _nextScan && (ReplayWorld == null || ReplayWorld.Version == _replayVersion))
        {
            if (_encounter?.IsEurekaOrthos == true)
            {
                _encounter.UpdateEurekaFrame(now);
                Render(now);
                return;
            }
            // Uplift's per-AOE risk window advances between object scans.
            if (_encounter?.IsBarPassage == true
                || _encounter?.Active?.BossOid == NativeTreasureCatalog.FASTITOCALON && _encounter.TreasureVaultGroups.Count > 0)
            {
                _encounter.Tick(now);
                Render(now);
            }
            return;
        }
        Scan(now);
        Render(now);
    }

    protected override void OnStop()
    {
        if (previousPrecombatHint != null && MasterPlayerPlugin.Instance?.Scripts?.StatusMessage == previousPrecombatHint)
            MasterPlayerPlugin.Instance.Scripts.SetStatus("");
        previousPrecombatHint = null;
        caduceusJump.Clear();
        twintaniaActions.Clear();
        vanaspatiMisdirection?.Dispose();
        vanaspatiMisdirection = null;
        cosmosSeedDrop.Clear();
        fruitInteraction.Clear();
        skallaDuty.Clear();
        lakshmiVril.Clear();
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        _output?.Clear();
        _encounter?.ResetFight();
        _output = null;
        _encounter = null;
        _events = null;
        _messages.Clear();
        Hints.ClearActionRestrictions();
        Hints.ClearFacing();
        Hints.SetMovementHold(false);
        Hints.SetTarget(0);
    }

    protected override void OnCurrentPlayerChanged(ulong previous, ulong current)
    {
        caduceusJump.Clear();
        _encounter?.ClearCaduceusMovementHistory();
        twintaniaActions.Clear();
        vanaspatiMisdirection?.Clear();
        cosmosSeedDrop.Clear();
        _nextScan = default; _messages.Clear(); fruitInteraction.Clear(); skallaDuty.Clear();
        lakshmiVril.Clear();
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }

    protected override void OnMovementDisabled()
    {
        twintaniaActions.Clear();
        caduceusJump.Clear();
        lakshmiVril.Clear();
        vanaspatiMisdirection?.Clear();
        cosmosSeedDrop.Clear();
        fruitInteraction.Clear();
        skallaDuty.Clear();
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }

    private void Suspend()
    {
        twintaniaActions.Clear();
        caduceusJump.Clear();
        lakshmiVril.Clear();
        vanaspatiMisdirection?.Clear();
        cosmosSeedDrop.Clear();
        _suspended = true;
        fruitInteraction.Clear();
        skallaDuty.Clear();
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        _encounter?.ResetFight();
        _output?.Clear();
        Hints.ClearActionRestrictions();
        Hints.ClearFacing();
        Hints.SetMovementHold(false);
        Hints.SetTarget(0);
    }

    private unsafe void Scan(DateTime now)
    {
        // These models consume per-frame actor lifetimes or ordered NPC/bait positions.
        _nextScan = (EurekaOrthosCatalog.IsTerritory(territory) || territory is QuantumCatalog.TERRITORY or HalicarnassusCatalog.TERRITORY or AlteRoiteCatalog.TERRITORY or WickedThunderCatalog.TERRITORY or KefkaCatalog.TERRITORY or ThordanCatalog.TERRITORY) || territory == CarnivaleCatalog.TERRITORY || territory is MainScenarioQuestCatalog.SLEEP_DISTURBED or MainScenarioQuestCatalog.HOPE_ON_THE_WAVES
            or MainScenarioQuestCatalog.TIME_BETWEEN or MainScenarioQuestCatalog.ISEULTALON or MainScenarioQuestCatalog.WARMTH_OF_THE_FAMILY or JobQuestCatalog.BAR_THE_PASSAGE
            or JobQuestCatalog.TO_HAVE_LOVED_AND_LOST or JobQuestCatalog.HEROES_AND_PRETENDERS && ReplayWorld == null
            ? now : now.AddSeconds(0.1);
        if (_encounter == null) return;
        if (territory == JobQuestCatalog.BAR_THE_PASSAGE)
            _encounter.CurrentCfcId = MasterPlayer.MechanicHost.LiveWorldSampler.ResolveCfc((ushort)territory);
        _encounter.CurrentAssignment = MechanicAssignment;
        _encounter.CurrentPartyAssignments = PartyMechanicAssignments;
        if (ReplayWorld is { } world)
        {
            // A repeated recorded frame is not a new status or cast observation.
            if (territory == HeavenswardIntroCatalog.GRAOULLY && _replayVersion == world.Version)
            {
                _encounter.Tick(now);
                return;
            }
            _replayVersion = world.Version;
            _encounter.LegacyMovement = world.LegacyMovement;
            _encounter.CameraAzimuth = world.CameraAzimuth;
            _encounter.DutyActionPlayerId = world.DutyActionPlayerId;
            _encounter.SourcePlayerId = world.SourcePlayerId;
            if (territory == CarnivaleCatalog.TERRITORY)
                _encounter.CarnivaleSourcePlayerId = world.LocalPlayerId;
            _encounter.FateSourcePlayerId = world.LocalPlayerId;
            _encounter.AllowedFateId = world.AllowedFateId;
            if (NativeTreasureCatalog.IsTerritory(territory))
                _encounter.TreasurePartyInput = world.Party.Where(p => p.EntityId == world.LocalPlayerId).Take(1)
                    .Concat(world.Party.Where(p => p.EntityId != world.LocalPlayerId)).Take(8).Select(p => p.EntityId).ToArray();
            _encounter.DutyActions = world.DutyActions;
            var party = world.Party.ToDictionary(p => p.EntityId);
            _encounter.CurrentPartyMembers = world.Party;
            var replayActors = world.Actors.Values.Select(a =>
            {
                party.TryGetValue(a.Id, out var member);
                var cast = a.Cast is { } c ? new DungeonCastObservation(c.ActionId, c.Location, c.Rotation, c.StartedAt, c.FinishAt, c.TargetId, TotalTime: c.TotalTime, IsSpell: c.IsSpell) : null;
                // Source slot diff consumers use native zero remaining time;
                // generic status lifetimes retain the recording's MaxValue.
                var rawStatusSlots = a.Statuses.Select(s => s.ZeroRemainingTime == true ? s with { ExpiresAt = now } : s).ToArray();
                return new DungeonActor(a.Id, a.Oid, a.Position, a.Dead, a.Targetable, a.InCombat, cast, a.TargetId, a.Name,
                    member?.Slot ?? -1, member?.Role ?? "", a.Statuses.Where(s => s.ExpiresAt > now || NoviceCatalog.IsTerritory(territory)
                        || territory is JobQuestCatalog.POWER_TO_PROTECT or NativeFateCatalog.FORMIDABLE && s.Id == 1269).Select(s => s.Id).ToArray(), a.Hostile, a.RenderFlags,
                    a.Statuses.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.Max(s => s.Param)), a.HitboxRadius, a.Rotation,
                    a.EventState, a.Tethers.GroupBy(t => (uint)t.Id).ToDictionary(g => g.Key, g => g.First().TargetId),
                    a.Statuses.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.Max(s => s.ExpiresAt)), a.AnimationState1, a.ModelState, a.ClassJob, a.EventAnimation, a.NameId,
                    a.AnimationState2, a.Kind == 2 && a.SubKind == 9, a.IncomingEffects,
                    a.Statuses.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.MaxBy(s => s.ExpiresAt)!.SourceId), a.Hp, a.MaxHp, a.AggroPlayer,
                    a.Kind == 2 && a.SubKind == 5 && a.IsEnemy == false, a.IsEnemy, a.ObjectIndex, a.Kind == 1, a.FateId, territory is CarnivaleCatalog.TERRITORY or HeavenswardIntroCatalog.BLACK_RIDER or HeavenswardIntroCatalog.YAQUARU or EurekaOrthosCatalog.PROTO_KALIYA or EurekaOrthosCatalog.EXCALIBUR or QuantumCatalog.TERRITORY or HalicarnassusCatalog.TERRITORY or AlteRoiteCatalog.TERRITORY or WickedThunderCatalog.TERRITORY or KefkaCatalog.TERRITORY or ThordanCatalog.TERRITORY ? rawStatusSlots : null,
                    (ushort)((a.Kind << 8) | a.SubKind),
                    a.Tethers.Any(t => t.SlotIndex != null) ? a.Tethers.FirstOrDefault(t => t.SlotIndex == 0)
                        : a.Tethers.FirstOrDefault(), a.GameObjectId);
            });
            _encounter.Synchronize(replayActors, !_suspended && CurrentPlayer is { } p
                && (!p.Dead || territory == NoviceCatalog.TANK04 || NoviceTacticalCatalog.IsTerritory(territory) || NativeFateCatalog.IsTerritory(territory)) ? p.Position : null,
                now, IsMechanismEnabled, CurrentPlayerId);
            return;
        }
        var actors = new List<DungeonActor>();
        if (territory == CarnivaleCatalog.TERRITORY)
            _encounter.CarnivaleSourcePlayerId = ReplayWorldCapture.Id(Svc.Objects.LocalPlayer);
        _encounter.FateSourcePlayerId = NativeFateCatalog.IsTerritory(territory)
            ? ReplayWorldCapture.Id(Svc.Objects.LocalPlayer) : 0;
        _encounter.AllowedFateId = NativeTreasureCatalog.IsTerritory(territory) || NativeFateCatalog.IsTerritory(territory)
            || territory is MainScenarioQuestCatalog.HIS_FORGOTTEN_HOME or MainScenarioQuestCatalog.MAX_QUEST
            ? ReplayWorldCapture.CaptureAllowedFate(CurrentPlayer?.Level) : null;
        if (NativeTreasureCatalog.IsTerritory(territory))
        {
            // Match the original live feed: local player first, then PR's UI party order.
            // Role-priority slots are a separate input and can be reordered by the user.
            var localId = ReplayWorldCapture.Id(Svc.Objects.LocalPlayer);
            var sourceParty = new List<ulong>();
            if (localId is not (0 or 0xE0000000)) sourceParty.Add(localId);
            foreach (var member in PartyHelper.GetParty())
            {
                var id = ReplayWorldCapture.Id(member);
                if (sourceParty.Count == 8) break;
                if (id is not (0 or 0xE0000000) && !sourceParty.Contains(id)) sourceParty.Add(id);
            }
            _encounter.TreasurePartyInput = sourceParty;
        }
        var playerEnmity = PreservesTerritoryMechanics ? ReplayWorldCapture.CapturePlayerEnmity() : null;
        _encounter.LegacyMovement = Svc.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1;
        _encounter.CameraAzimuth = ReplayWorldCapture.CaptureCameraAzimuth();
        _encounter.DutyActionPlayerId = CurrentPlayerId;
        _encounter.SourcePlayerId = territory == JobQuestCatalog.FANGS_OF_THE_VIPER
            ? ReplayWorldCapture.CaptureSourcePlayerId() : null;
        _encounter.DutyActions = territory == LakshmiCatalog.TERRITORY ? ReplayWorldCapture.CaptureDutyActions(CurrentPlayerId) : null;
        var liveParty = ReplayWorldCapture.CaptureParty().ToDictionary(p => p.EntityId);
        _encounter.CurrentPartyMembers = liveParty.Values.ToArray();
        foreach (var obj in Svc.Objects)
        {
            if (obj.Address == 0) continue;
            var id = obj.EntityId is not (0 or 0xE0000000) ? obj.EntityId : obj.GameObjectId;
            if (id is 0 or 0xE0000000) continue;
            if (territory is MainScenarioQuestCatalog.SLEEP_DISTURBED or MainScenarioQuestCatalog.HOPE_ON_THE_WAVES && (id >> 56) == 0xFF) continue;
            var combat = false;
            var hostile = false;
            uint[] statuses = [];
            ReplayStatus[]? statusSlots = null;
            Dictionary<uint, ushort> statusParams = [];
            Dictionary<uint, ulong> tethers = [];
            ReplayTether? firstTether = null;
            Dictionary<uint, DateTime> statusExpirations = [];
            Dictionary<uint, ulong> statusSources = [];
            byte animationState1 = 0;
            byte modelState = 0;
            byte animationState2 = 0;
            DungeonCastObservation? cast = null;
            ReplayIncomingEffect[] incomingEffects = [];
            if (obj is IBattleChara battle)
            {
                combat = (battle.StatusFlags & StatusFlags.InCombat) != 0;
                hostile = (battle.StatusFlags & StatusFlags.Hostile) != 0;
                statuses = battle.StatusList.Where(s => s.StatusId != 0).Select(s => s.StatusId).ToArray();
                if (territory is CarnivaleCatalog.TERRITORY or HeavenswardIntroCatalog.BLACK_RIDER or HeavenswardIntroCatalog.YAQUARU or EurekaOrthosCatalog.PROTO_KALIYA or EurekaOrthosCatalog.EXCALIBUR or QuantumCatalog.TERRITORY or HalicarnassusCatalog.TERRITORY or AlteRoiteCatalog.TERRITORY or WickedThunderCatalog.TERRITORY or KefkaCatalog.TERRITORY or ThordanCatalog.TERRITORY)
                    statusSlots = battle.StatusList.Select((s, index) => s == null || s.StatusId == 0 ? null
                        : new ReplayStatus(s.StatusId, s.Param, s.SourceId, now.AddSeconds(Math.Min(MathF.Abs(s.RemainingTime), 100000f)))
                            { SlotIndex = index }).OfType<ReplayStatus>().ToArray();
                statusParams = battle.StatusList.Where(s => s.StatusId != 0).GroupBy(s => (uint)s.StatusId)
                    .ToDictionary(g => g.Key, g => g.Max(s => s.Param));
                statusSources = battle.StatusList.Where(s => s.StatusId != 0).GroupBy(s => (uint)s.StatusId)
                    .ToDictionary(g => g.Key, g => (ulong)g.MaxBy(s => s.RemainingTime)!.SourceId);
                statusExpirations = battle.StatusList.Where(s => s.StatusId != 0 && float.IsFinite(s.RemainingTime))
                    .GroupBy(s => (uint)s.StatusId).ToDictionary(g => g.Key, g => now.AddSeconds(Math.Max(0, g.Max(s => s.RemainingTime))));
                var nativeCharacter = (NativeBattleChara*)battle.Address;
                incomingEffects = ReplayWorldCapture.CaptureIncomingEffects(nativeCharacter);
                animationState1 = nativeCharacter->Timeline.AnimationState[0];
                modelState = nativeCharacter->Timeline.ModelState;
                animationState2 = nativeCharacter->Timeline.AnimationState[1];
                foreach (var tether in nativeCharacter->Vfx.Tethers)
                    if (tether.Id != 0) tethers[tether.Id] = tether.TargetId;
                var nativeFirstTether = nativeCharacter->Vfx.Tethers[0];
                if (nativeFirstTether.Id != 0)
                    firstTether = new(nativeFirstTether.Id, nativeFirstTether.TargetId, nativeFirstTether.Progress) { SlotIndex = 0 };
                if (battle.IsCasting && battle.CastActionId != 0)
                {
                    var native = (NativeBattleChara*)battle.Address;
                    var location = native->CastInfo.TargetLocation;
                    var remaining = Math.Max(0, battle.TotalCastTime - battle.CurrentCastTime);
                    cast = new(battle.CastActionId, new(location.X, location.Y, location.Z),
                        float.IsFinite(native->CastRotation) ? native->CastRotation : null,
                        now.AddSeconds(-battle.CurrentCastTime), now.AddSeconds(remaining + 0.3), battle.CastTargetObjectId,
                        TotalTime: battle.TotalCastTime, IsSpell: battle.CastActionType == 1);
                }
            }
            liveParty.TryGetValue(id, out var member);
            actors.Add(new(id, obj.BaseId, obj.Position, obj.IsDead, obj.IsTargetable, combat, cast,
                obj.TargetObjectId, obj.Name.TextValue, member?.Slot ?? -1, member?.Role ?? "", statuses, hostile,
                (int)((NativeGameObject*)obj.Address)->RenderFlags, statusParams, obj.HitboxRadius, obj.Rotation,
                ((NativeGameObject*)obj.Address)->EventState, tethers, statusExpirations, animationState1, modelState,
                obj is IBattleChara character ? character.ClassJob.RowId : 0, null, obj is IBattleChara named ? named.NameId : 0,
                animationState2, obj.ObjectKind == ObjectKind.BattleNpc && obj.SubKind == 9, incomingEffects, statusSources,
                obj is IBattleChara health ? health.CurrentHp : null, obj is IBattleChara maximum ? maximum.MaxHp : null,
                playerEnmity?.Contains(obj.EntityId) == true,
                obj.ObjectKind == ObjectKind.BattleNpc && obj.SubKind == 5
                    && NativeActionManager.ClassifyTarget((NativeCharacter*)obj.Address) != NativeActionManager.TargetCategory.Enemy,
                ((NativeGameObject*)obj.Address)->IsCharacter()
                    && NativeActionManager.ClassifyTarget((NativeCharacter*)obj.Address) == NativeActionManager.TargetCategory.Enemy,
                ((NativeGameObject*)obj.Address)->ObjectIndex, obj.ObjectKind == ObjectKind.Pc,
                obj is IBattleChara ? ((NativeBattleChara*)obj.Address)->FateId : null, statusSlots,
                (ushort)(((byte)obj.ObjectKind << 8) | obj.SubKind), firstTether, obj.GameObjectId));
        }
        var player = CurrentPlayer;
        _encounter.Synchronize(actors, !_suspended && player != null
            && (!player.Dead || territory == NoviceCatalog.TANK04 || NoviceTacticalCatalog.IsTerritory(territory) || NativeFateCatalog.IsTerritory(territory)) ? player.Position : null,
            now, IsMechanismEnabled, CurrentPlayerId);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        if (_encounter == null) return;
        if (ev is LegacyMapEffectEvent legacy)
        {
            _encounter.LegacyMapEffect(legacy.Sequence, legacy.Param, legacy.Data);
            if (!_suspended) Render(CurrentTime);
            return;
        }
        if (_suspended && ev is LogSystemDirectorUpdateEvent nybethDirector && _encounter.IsPalaceManagedBoss)
        {
            _encounter.PalaceGuideDirector(nybethDirector.Command);
            Render(CurrentTime);
        }
        if (_suspended && ev is LogSystemDirectorUpdateEvent eurekaDirector && _encounter.IsEurekaOrthos)
        {
            _encounter.EurekaDirector(eurekaDirector.Command);
            Render(CurrentTime);
        }
        if (_suspended) return;
        var now = CurrentTime;
        _events?.Handle(ev, now);
        Render(now);
    }

    private void Render(DateTime now)
    {
        if (_encounter == null) return;
        var precombatHint = IsMechanismEnabled("文字提示") ? _encounter.TisiphonePrecombatHint ?? _encounter.CarnivalePrecombatHint ?? _encounter.PalaceGuidePrecombatHint : null;
        if (precombatHint != null && precombatHint != previousPrecombatHint)
            _messages.Remove(precombatHint);
        if (previousPrecombatHint != null && precombatHint != previousPrecombatHint
            && MasterPlayerPlugin.Instance?.Scripts?.StatusMessage == previousPrecombatHint)
            MasterPlayerPlugin.Instance.Scripts.SetStatus("");
        previousPrecombatHint = precombatHint;
        _output?.Apply(_encounter, now, IsMechanismEnabled("水纹预览"));
        var player = CurrentPlayer;
        caduceusJump.Update(_encounter.RequireCaduceusJump, !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true, player, now, CaduceusJumpControl.Jump);
        twintaniaActions.Update(_encounter.TwintaniaActions().Concat(_encounter.UltimaActions()).Concat(_encounter.IfritExActions())
            .Concat(_encounter.EnemyProvokeActions()).DistinctBy(request => (request.Action, request.Target)).ToArray(), !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true, player, now, ExecuteTwintaniaAction);
        if (_encounter.PreserveFightAcrossPlayerDeath && player is not { Dead: false })
        {
            Hints.ClearActionRestrictions();
            Hints.ClearFacing();
            Hints.SetTarget(0);
            Hints.SetMovementHold(false);
            return;
        }
        fruitInteraction.Prepare(_encounter.FruitTarget ?? _encounter.FractalInteractionTarget ?? _encounter.CosmosSeedTarget, player?.Position,
            !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true && player is { Dead: false });
        var allStop = _encounter.RequireAllStop;
        Hints.SetActionRestrictions(holdActions: fruitInteraction.Holding || allStop, blockCasts: fruitInteraction.Holding || allStop || _encounter.RequireSnowMovement || _encounter.RequireStigmaMovementHold || _encounter.RequireClyteumMovement || _encounter.RequireWolMovement || _encounter.RequireValigMovement,
            cancelCast: (fruitInteraction.Holding && !fruitInteraction.Attempted || allStop || _encounter.RequireSnowMovement || _encounter.RequireStigmaMovementHold || _encounter.RequireClyteumMovement || _encounter.RequireWolMovement || _encounter.RequireValigMovement) && player?.Cast != null,
            forbiddenTargets: _encounter.ForbiddenTargets);
        Hints.SetMovementHold(allStop || _encounter.RequireSirenMovementHold || _encounter.RequireStigmaMovementHold || _encounter.DohnPulling);
        fruitInteraction.Update(now, player?.Cast != null, DungeonObjectInteraction.Interact);
        var priorityTarget = player == null || allStop ? 0 : _encounter.PriorityTarget(player.TargetId, player.Position);
        var clearTarget = player != null && !allStop && priorityTarget == 0 ? _encounter.TargetToClear(player.TargetId) : 0;
        Hints.SetTarget(priorityTarget, clearTarget);
        Hints.SetFacing(player == null || allStop ? null : _encounter.GazeFacing(player.Position, player.Rotation));
        TryCleanseSlime(now, player);
        TrySirenImmunity(now, player);
        TryKtisisInterrupt(now, player);
        TryLapisStun(now, player);
        lakshmiVril.Update(_encounter.ConsumeLakshmiVrilRequest(), _encounter.Active != null && !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true, player,
            _encounter.DutyActionPlayerId, _encounter.DutyActions, now, CastVril);
        skallaDuty.Update(_encounter, player, !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true, now, SkallaDutyControl.Cast);
        UpdateDohnBridge(now, player);
        TryGulgSprint(now, player);
        cosmosSeedDrop.Update(now, _encounter.CosmosReadyToDrop, !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true && player is { Dead: false }, CosmosSeedDrop.Drop);
        UpdateVanaspatiMisdirection();
        if (!IsMechanismEnabled("文字提示")) return;
        var position = CurrentPlayer?.Position;
        var countdown = _encounter.ArrTrialCountdown ?? _encounter.PortaCountdown ?? _encounter.HardTrialCountdown;
        if (countdown != null) MasterPlayerPlugin.Instance?.Scripts?.SetStatus(countdown);
        foreach (var hint in _encounter.Hints(position))
        {
            // 倒数每帧更新状态，日志按机制键节流，避免每个小数值都创建一条日志。
            var messageKey = hint == countdown ? "arr-trial/countdown" : hint;
            if (_messages.TryGetValue(messageKey, out var last) && now - last < TimeSpan.FromSeconds(10)) continue;
            _messages[messageKey] = now;
            Log(hint);
        }
    }

    private void UpdateVanaspatiMisdirection()
    {
        var authorized = (_encounter?.RequireVanaspatiMisdirection == true || _encounter?.RequireYuweyawataMisdirection == true)
            && !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed == true;
        if (authorized) (vanaspatiMisdirection ??= new()).Renew(true);
        else vanaspatiMisdirection?.Clear();
    }

    private void UpdateDohnBridge(DateTime now, ReplayActorState? player)
    {
        if (territory != DohnMhegCatalog.TERRITORY || MasterPlayerPlugin.Instance?.SafeZone is not { } runtime) return;
        var active = _encounter?.DohnChasm == true && !_encounter.DohnPulling && !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && runtime.Setting.MovementAllowed && player is { Dead: false };
        runtime.RequestAutomaticMovement(this, active, arenaSafetyMargin: .05f);
        var route = active ? _encounter!.DohnBridgeRoute() : [];
        runtime.RequestDirectTraversal(this, route.Length > 0 ? route[^1] : null, now, route);
        if (!active || route.Length < 2 || player!.Cast != null || now < nextDohnSprint) return;
        var livePlayer = Svc.Objects.FirstOrDefault(a => a.EntityId == CurrentPlayerId) as IBattleChara;
        if (livePlayer == null || !ActionHelper.CanCast(3, livePlayer)) return;
        nextDohnSprint = now.AddSeconds(2);
        ActionUpdaterRouter.UseAction(new PAction(3, PromeRotation.Data.ActionType.OffGcd, ActionTargetType.Self)
            { NetworkTid = (uint)player.Id });
    }

    private void TryCleanseSlime(DateTime now, ReplayActorState? player)
    {
        if (_encounter == null || IsReplay || !MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || player is not { Dead: false, Cast: null } || now < nextSlimeCleanse) return;
        var target = _encounter.SlimeCleanseTarget(player.ClassJob) ?? _encounter.FractalCleanseTarget(player.ClassJob)
            ?? _encounter.HeroesCleanseTarget(player.ClassJob) ?? _encounter.ZotCleanseTarget(player.ClassJob)
            ?? _encounter.DeadEndsCleanseTarget(player.ClassJob) ?? _encounter.TroiaCleanseTarget(player.ClassJob)
            ?? _encounter.LapisCleanseTarget(player.ClassJob)
            ?? _encounter.LunarCleanseTarget(player.ClassJob) ?? _encounter.MesoCleanseTarget(player.ClassJob);
        if (target == null) return;
        var action = player.ClassJob == 23 ? 3561u : 7568u;
        var liveTarget = Svc.Objects.FirstOrDefault(a => a.EntityId == target.Id) as IBattleChara;
        if (liveTarget == null || !ActionHelper.CanCast(action, liveTarget)) return;
        nextSlimeCleanse = now.AddSeconds(.5);
        ActionUpdaterRouter.UseAction(new PAction(action, action == 7568 ? PromeRotation.Data.ActionType.Gcd : PromeRotation.Data.ActionType.OffGcd,
            ActionTargetType.Self) { NetworkTid = (uint)target.Id });
    }

    private void TryGulgSprint(DateTime now, ReplayActorState? player)
    {
        if ((_encounter?.WantsGulgSprint != true && _encounter?.WantsWorqorSprint != true && _encounter?.WantsPortaSprint != true) || IsReplay || !MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || player is not { Dead: false, Cast: null } || now < nextGulgSprint) return;
        var livePlayer = Svc.Objects.FirstOrDefault(a => a.EntityId == CurrentPlayerId) as IBattleChara;
        if (livePlayer == null || !ActionHelper.CanCast(3, livePlayer)) return;
        nextGulgSprint = now.AddSeconds(2);
        ActionUpdaterRouter.UseAction(new PAction(3, PromeRotation.Data.ActionType.OffGcd, ActionTargetType.Self)
            { NetworkTid = (uint)player.Id });
    }

    private void TrySirenImmunity(DateTime now, ReplayActorState? player)
    {
        if ((_encounter?.WantsSirenKnockbackImmunity != true && _encounter?.WantsBabilKnockbackImmunity != true && _encounter?.WantsPortaKnockbackImmunity != true && _encounter?.WantsValigKnockbackImmunity != true) || IsReplay
            || !MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || player is not { Dead: false, Cast: null } || now < nextSirenImmunity) return;
        var livePlayer = Svc.Objects.FirstOrDefault(a => a.EntityId == CurrentPlayerId) as IBattleChara;
        if (livePlayer == null) return;
        // 宿主 CanCast 只检查等级、任务及冷却，职业需要先单独筛选。
        var action = SirensongCatalog.KnockbackAction(player.ClassJob);
        if (action == 0 || !ActionHelper.CanCast(action, livePlayer)) return;
        nextSirenImmunity = now.AddSeconds(.5);
        ActionUpdaterRouter.UseAction(new PAction(action, PromeRotation.Data.ActionType.OffGcd, ActionTargetType.Self)
            { NetworkTid = (uint)player.Id });
    }

    private void TryKtisisInterrupt(DateTime now, ReplayActorState? player)
    {
        if (IsReplay || !MasterPlayerSettings.Instance.EnableSmartMovement || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || player is not { Dead: false, Cast: null } || now < nextKtisisInterrupt
            || (_encounter?.KtisisInterruptTarget(player.ClassJob) ?? _encounter?.MesoInterruptTarget(player.ClassJob)
                ?? _encounter?.CoilInterruptTarget(player.ClassJob)) is not { } target) return;
        var action = KtisisCatalog.InterruptAction(player.ClassJob);
        var liveTarget = Svc.Objects.FirstOrDefault(a => a.EntityId == target.Id) as IBattleChara;
        if (liveTarget is not { IsCasting: true, IsCastInterruptible: true } || liveTarget.CastActionId is not (25907 or 44856 or 1447 or 1216)
            || !ActionHelper.CanCast(action, liveTarget)) return;
        nextKtisisInterrupt = now.AddSeconds(.5);
        ActionUpdaterRouter.UseAction(new PAction(action, PromeRotation.Data.ActionType.OffGcd, ActionTargetType.Target)
            { NetworkTid = (uint)target.Id });
    }

    private void TryLapisStun(DateTime now, ReplayActorState? player)
    {
        if (IsReplay || !MasterPlayerSettings.Instance.EnableSmartMovement || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || player is not { Dead: false, Cast: null } || now < nextLapisStun
            || _encounter?.LapisStunTarget(player.ClassJob) is not { } target) return;
        var action = LapisCatalog.StunAction(player.ClassJob);
        var liveTarget = Svc.Objects.FirstOrDefault(a => a.EntityId == target.Id) as IBattleChara;
        if (liveTarget is not { IsCasting: true } || liveTarget.CastActionId != 32785 || !ActionHelper.CanCast(action, liveTarget)) return;
        nextLapisStun = now.AddSeconds(.5);
        ActionUpdaterRouter.UseAction(new PAction(action, PromeRotation.Data.ActionType.OffGcd, ActionTargetType.Target)
            { NetworkTid = (uint)target.Id });
    }

    private static unsafe bool CastVril(ulong player)
    {
        if (PlayerPerspective.IsReplay || !MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || Svc.Objects.LocalPlayer is not { IsDead: false, IsCasting: false } local || local.EntityId != player
            || MechanicFacingControl.HoldActions || !MechanicFacingControl.AllowsTarget(player)
            || ActionHelper.GetAnimationLock() > 0 || PromeRotation.Core.GameData.IsPlayerOccupied()) return false;
        if (MechanicFacingControl.BlockCasts
            && FFXIVClientStructs.FFXIV.Client.Game.ActionManager.GetAdjustedCastTime(
                FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 9345) > 0) return false;
        var manager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (manager == null || manager->GetActionStatus(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, 9345) != 0) return false;
        ActionUpdaterRouter.UseAction(new PAction(9345, PromeRotation.Data.ActionType.OffGcd, ActionTargetType.Self)
            { NetworkTid = (uint)player });
        return true;
    }

    private unsafe bool ExecuteTwintaniaAction(DungeonActionRequest request)
    {
        if (IsReplay || !MasterPlayerSettings.Instance.EnableSmartMovement
            || MasterPlayerPlugin.Instance?.SafeZone.Setting.MovementAllowed != true
            || CurrentPlayer is not { Dead: false, Cast: null } player
            || Svc.Objects.LocalPlayer?.EntityId != player.Id || ActionHelper.GetAnimationLock() > 0) return false;
        var target = Svc.Objects.FirstOrDefault(a => a.EntityId == request.Target) as IBattleChara;
        if (target is not { IsDead: false, IsTargetable: true } || !ActionHelper.CanCast(request.Action, target)) return false;
        var actionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (actionManager == null
            || actionManager->GetActionStatus(FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, request.Action, target.GameObjectId) != 0)
            return false;
        // PR resolves the enum target before applying NetworkTid; Self is always available
        // here, so explicit enemy requests also work when the user has no selected target.
        ActionUpdaterRouter.UseAction(new PAction(request.Action, PromeRotation.Data.ActionType.OffGcd,
            ActionTargetType.Self) { NetworkTid = (uint)request.Target });
        return true;
    }
}

using ECommons.DalamudServices;
using MasterPlayer.Replay;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.LogSystem;
using PromeRotation.Updaters;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

public abstract class PalaceScript : MasterPlayerScript
{
    private readonly PalaceEncounter encounter = new();
    private readonly PalaceFloorHazards floor = new();
    private readonly PalaceCombatControl control = new();
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, PalaceArea Area)> output = [];
    private readonly Dictionary<string, DateTime> messages = [];
    private readonly HashSet<string> knockbackLines = [];
    private DateTime nextScan;
    private DateTime nextInterrupt;
    private bool ownsBoundary;
    private long replayObservationVersion = -1;
    private bool otherBossActive;
    private DungeonDefinition[] otherBosses = [];

    protected virtual bool IncludesBossMechanics => true;

    [UserSetting("显示隐藏陷阱")]
    public bool TrapHints { get => Settings.GetBool("TrapHints", true); set => Settings.Set("TrapHints", value); }

    public bool EncounterActive => encounter.BossActive;
    public string? PredictedDamageText
    {
        get
        {
            if (!IsMechanismEnabled("Boss机制") || CurrentPlayer is not { Dead: false }) return null;
            var groups = encounter.PredictedDamage().GroupBy(p => p.DamageType).ToArray();
            return groups.Length == 0 ? null : string.Join("，", groups.Select(g =>
                $"{g.Key}：{g.Select(p => p.TargetId).Distinct().Count()}人/{g.Select(p => p.Activation).Distinct().Count()}次"));
        }
    }
    public string? MechanicHintsText { get; private set; }
    internal bool HasFloorMap => floor.HasMap;
    internal void IgnoreNearestFloorTrap()
    {
        if (CurrentPlayer is { } player && floor.IgnoreNearestTrap(player.Position)) Render(CurrentTime);
    }
    public int DangerCount => output.Values.Count(v => !v.Area.Safe && !v.Area.PreviewOnly && !v.Area.Obstacle);
    public override IEnumerable<string> GetMechanisms() => IncludesBossMechanics
        ? ["道中范围与视线", "Boss机制", "战斗控制"] : ["道中范围与视线", "战斗控制"];

    internal bool HandlesCast(LogSystemCastStartEvent cast)
    {
        var source = encounter.Actors.FirstOrDefault(a => a.Id == cast.SourceId);
        if (source != null && IsOtherBossCast(source, cast.ActionId)) return false;
        return source != null ? source.Hostile || PalaceCatalog.IsEnemy(source.Oid)
            : ReplayWorld == null && Svc.Objects.SearchByEntityId((uint)cast.SourceId) is { } live
                && PalaceCatalog.IsEnemy(live.BaseId);
    }

    protected override void OnStart()
    {
        encounter.BossMechanicsEnabled = IncludesBossMechanics;
        if (!IncludesBossMechanics)
            otherBosses = HeavenswardIntroCatalog.Create(ReplayWorld?.Territory ?? Svc.ClientState.TerritoryType);
        SuppressGenericDodge(IncludesBossMechanics ? PalaceCatalog.SpecialActions
            : [6351, 6356, 6953, 6365]);
        OnLogEvent(HandleEvent);
        OnTerritoryChanged(_ => Clear());
        OnSystemLog(id =>
        {
            if (id == 7248)
            {
                ClearOutput();
                encounter.Reset();
                nextScan = default;
            }
            floor.SystemLog(id);
        });
        Scan(CurrentTime);
        Render(CurrentTime);
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (CurrentTime < nextScan && (ReplayWorld == null || ReplayWorld.Version == replayObservationVersion)) return;
        Scan(CurrentTime);
        Render(CurrentTime);
    }

    private void Scan(DateTime now)
    {
        nextScan = now.AddSeconds(.1);
        var enmity = ReplayWorld == null ? ReplayWorldCapture.CapturePlayerEnmity() : null;
        var observations = ReplayWorld is { } world ? world.Actors.Values.ToArray()
            : Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now, playerEnmity: enmity)).ToArray();
        var observeCasts = ReplayWorld == null || ReplayWorld.Version != replayObservationVersion;
        var snapshot = ReplayWorld is { } replay ? replay.DeepDungeon : ReplayWorldCapture.CaptureDeepDungeon();
        floor.Synchronize(snapshot, observations, ReplayWorld?.Territory ?? (ushort)Svc.ClientState.TerritoryType);
        // Query native Boss state using the same observation first, independent of
        // script discovery/update order. This never reads a BMR session.
        otherBossActive = !IncludesBossMechanics && MasterPlayerPlugin.Instance?.Scripts?.Running.Values
            .Any(r => r.Script is DungeonScript dungeon && dungeon.ObservePalaceBoss(now)) == true;
        encounter.ZoneMechanicsEnabled = IsMechanismEnabled("道中范围与视线") && !floor.BetweenFloors && !otherBossActive
            && !(snapshot is { DungeonId: 1, Floor: > 0 } knownFloor && knownFloor.Floor % 10 == 0);
        encounter.Party = ReplayWorld?.Party ?? ReplayWorldCapture.CaptureParty();
        encounter.Synchronize(observations, now, observeCasts, ResolveObservedTrashCast);
        if (ReplayWorld != null) replayObservationVersion = ReplayWorld.Version;
    }

    private bool IsOtherBossCast(ReplayActorState source, uint action)
        => otherBosses.Any(b => b.BossOid == source.Oid
            || b.Casts.Any(r => r.Action == action && (r.CasterOid == 0 || r.CasterOid == source.Oid)));

    private PalaceCast? ResolveObservedTrashCast(ReplayActorState source)
    {
        if (source.Cast is not { } cast) return null;
        if (IsOtherBossCast(source, cast.ActionId)) return null;
        var row = Svc.Data?.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(cast.ActionId);
        var gaze = row?.VFX.RowId == 25;
        var eligible = source.Kind == 2 && source.SubKind is 5 or 11 && source.IsEnemy == true
            && cast.ActionId is not (27503 or 33626);
        var shape = row is { } data && eligible
            && (gaze || cast.IsSpell == true && data.CastType != 1 && !(data.CastType is 2 or 5 && data.EffectRange >= 30))
                ? PalaceTrashSpell.Shape(data.CastType, data.EffectRange, data.XAxisModifier, source.HitboxRadius,
                    data.Omen.ValueNullable?.Path.ToString())
                : null;
        return new(source.Id, cast.ActionId, cast.TargetId, cast.Location, source.Position, cast.Rotation,
            cast.StartedAt, cast.FinishAt, shape, row?.Name.ToString() ?? "", row?.TargetArea == true, row?.CastType == 8,
            (row?.ExtraCastTime100ms ?? 0) * .1f, gaze && eligible);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        var now = CurrentTime;
        var eventTime = ev.Timestamp.ToUniversalTime();
        if (ev is LogSystemCastStartEvent cast)
        {
            Scan(now);
            var source = encounter.Actors.FirstOrDefault(a => a.Id == cast.SourceId);
            if (source == null || !source.Hostile && !PalaceCatalog.IsEnemy(source.Oid)
                || IsOtherBossCast(source, cast.ActionId))
            {
                // Scan may have handed the floor to its independent native Boss.
                // Publish that handoff even when this packet is not ours.
                Render(now);
                return;
            }
            var rotation = CastRotation.TryGet(cast, out var locked) ? locked : (float?)null;
            GeometryTemplate? shape = PalaceCatalog.SpecialShape(cast.ActionId);
            var special = PalaceCatalog.SpecialActions.Contains(cast.ActionId) && cast.ActionId != 6365;
            var row = special ? null : Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(cast.ActionId);
            var location = special ? cast.ActionId is 7155 or 7161 or 7163 or 7169 : row?.TargetArea == true;
            var position = cast.EffectPosition;
            // Cast packets report the raw duration; captured ReplayCast.FinishAt already includes this delay.
            const float delay = .3f;
            // Generic hints are driven by observed CastInfo, including target
            // references and actual Finish. Packets only stage Zone responses.
            // Valfodr is a baited charge, resolved dynamically by PalaceEncounter.
            if (cast.ActionId == 7156) shape = RayRectangleTemplate.Symmetric(6, 1);
            encounter.StartCast(new(cast.SourceId, cast.ActionId, cast.TargetId, position, cast.SourcePosition ?? source.Position,
                rotation, eventTime, eventTime.AddSeconds(cast.CastTime + delay), shape, row?.Name.ToString() ?? "", location, row?.CastType == 8,
                (row?.ExtraCastTime100ms ?? 0) * .1f));
        }
        else encounter.Apply(ev, eventTime);
        Render(now);
    }

    private void Render(DateTime now)
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        if (CurrentPlayer is not { Dead: false } player || floor.BetweenFloors || otherBossActive)
        {
            ClearOutput();
            return;
        }
        var bossEnabled = IsMechanismEnabled("Boss机制");
        var trashEnabled = IsMechanismEnabled("道中范围与视线");
        var manager = runtime.Manager;
        if (encounter.BossActive && bossEnabled)
        {
            if (!ReferenceEquals(manager.BuildSnapshot(now).ArenaBoundary, PalaceArena.Boundary))
                manager.SetArenaBoundary(PalaceArena.Boundary);
            ownsBoundary = true;
        }
        else ReleaseBoundary();
        var enabled = encounter.BossActive ? bossEnabled : trashEnabled;
        var wanted = enabled ? encounter.Collect(now, player.Id).Concat(encounter.KnockbackMarkers(now, player)).Concat(
            encounter.BossActive ? [] : floor.Collect(now, player, TrapHints)).ToArray() : [];
        var keys = wanted.Select(a => a.Key).ToHashSet();
        foreach (var key in output.Keys.Where(k => !keys.Contains(k)).ToArray()) Remove(key);
        foreach (var area in wanted)
        {
            using var group = SafeZone.BeginDrawingGroup(area.Name);
            var binding = new StaticTransform(area.Position, area.Rotation);
            var handle = output.TryGetValue(area.Key, out var old) ? old.Handle : default;
            if (area.PreviewOnly && handle.IsValid)
            {
                SafeZone.Remove(handle);
                handle = default;
            }
            if (!area.PreviewOnly)
            {
                if (!manager.ContainsArea(handle))
                {
                    if (area.Obstacle) handle = SafeZone.AddObstacle(area.Shape, binding);
                    else if (area.Safe) handle = SafeZone.AddPersistentSafeArea(area.Shape, binding, priority: area.Priority);
                    else handle = SafeZone.AddExactDangerSnapshot(area.Shape, binding, [area.Window]);
                    SafeZone.SetNavigationOnly(handle, true);
                }
                manager.UpdateArea(handle, template: area.Shape, binding: binding,
                    dangerWindows: area.Safe || area.Obstacle ? null : [area.Window],
                    goalWindows: area.Safe ? [new GoalWindow(area.From, area.Until, area.Priority, PositionPreference.Nearest)
                    {
                        OverrideDangerMargin = area.OverrideDangerMargin,
                        RequiresMovementHandoff = area.RequiresMovementHandoff,
                    }] : null,
                    incrementVersion: false);
            }
            output[area.Key] = (handle, area);
            if (!area.NavigationOnly)
                Draw.AddAoePreview(area.Key, area.Geometry, area.Safe, area.OutlineOnly, .5f, area.Unclipped, area.Color);
            else Draw.Remove(area.Key);
        }
        var projections = enabled ? encounter.Knockbacks(player).ToArray() : [];
        var lineKeys = projections.Select(k => $"kb-line/{k.Source}").ToHashSet();
        foreach (var key in knockbackLines.Where(k => !lineKeys.Contains(k)).ToArray()) Draw.Remove(key);
        knockbackLines.Clear();
        foreach (var projection in projections)
        {
            var key = $"kb-line/{projection.Source}";
            Draw.AddLinePreview(key, projection.From, projection.To, false);
            knockbackLines.Add(key);
        }
        runtime.RequestAutomaticMovement(this, enabled && !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0, goalDirectedTemporalPlanning: true);
        if (enabled && !IsReplay && ReplayWorld == null && MasterPlayerSettings.Instance.EnableSmartMovement
            && IsMechanismEnabled("战斗控制"))
        {
            var forbidden = encounter.ForbiddenTargets(now).ToArray();
            var facing = trashEnabled ? encounter.Facing(now, player.Id) : null;
            var restrictions = encounter.CastRestrictions(now, player.Id, trashEnabled);
            var cancelCast = restrictions.CancelCast && player.Cast is { } observed
                && Svc.Objects.LocalPlayer is { IsCasting: true } live && live.CastActionId == observed.ActionId
                && live.CastTargetObjectId == observed.TargetId
                && Math.Abs((now.AddSeconds(-live.CurrentCastTime) - observed.StartedAt).TotalSeconds) < .25
                && encounter.TakeCastCancellation(player.Id);
            control.Apply(0, forbidden,
                facing, false, blockCasts: restrictions.BlockCasts, cancelCast: cancelCast,
                forbiddenAutoAttackTargets: trashEnabled ? encounter.ForbiddenAutoAttackTargets(now) : []);
            if (trashEnabled && encounter.ZoneMechanicsEnabled && !encounter.BossActive) TryInterrupt(now, player);
        }
        else control.Clear();
        var hints = enabled ? encounter.Hints(now, player.Id).ToArray() : [];
        MechanicHintsText = hints.Length == 0 ? null : string.Join("；", hints);
        foreach (var message in hints)
        {
            var key = message;
            if (message.EndsWith("s left", StringComparison.Ordinal))
                key = message[..message.LastIndexOf(' ', message.LastIndexOf(' ') - 1)];
            if (messages.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(10)) continue;
            messages[key] = now;
            Log(message);
        }
    }

    private void TryInterrupt(DateTime now, ReplayActorState player)
    {
        if (now < nextInterrupt || player.Cast != null) return;
        var target = encounter.InterruptCandidates.FirstOrDefault(a => !a.Dead);
        if (target == null) return;
        uint action = player.ClassJob switch
        {
            1 or 3 or 19 or 21 or 32 or 37 => 7538,
            5 or 23 or 31 or 38 => 7551,
            _ => 0,
        };
        if (action == 0 || !ActionHelper.IsActionAvailableByLevelAndQuest(action) || ActionHelper.GetActionCooldown(action) > 0
            || System.Numerics.Vector3.Distance(player.Position, target.Position) > (action == 7538 ? 3 : 25) + target.HitboxRadius) return;
        nextInterrupt = now.AddSeconds(.5);
        ActionUpdaterRouter.UseAction(new PAction(action, PromeRotation.Data.ActionType.OffGcd, ActionTargetType.Self)
        {
            NetworkTid = (uint)target.Id,
        });
    }

    private void Remove(string key)
    {
        if (output.Remove(key, out var old) && old.Handle.IsValid) SafeZone.Remove(old.Handle);
        Draw.Remove(key);
    }
    private void ReleaseBoundary()
    {
        var manager = MasterPlayerPlugin.Instance?.SafeZone.Manager;
        if (ownsBoundary && ReferenceEquals(manager?.BuildSnapshot(CurrentTime).ArenaBoundary, PalaceArena.Boundary)) manager.ClearArenaBoundary();
        ownsBoundary = false;
    }
    private void ClearOutput()
    {
        control.Clear();
        MechanicHintsText = null;
        foreach (var key in knockbackLines) Draw.Remove(key);
        knockbackLines.Clear();
        foreach (var key in output.Keys.ToArray()) Remove(key);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        ReleaseBoundary();
    }
    private void Clear()
    {
        ClearOutput();
        encounter.Reset();
        floor.Reset();
        otherBossActive = false;
        messages.Clear();
        nextScan = default;
    }
    protected override void OnStop() => Clear();
    protected override void OnMovementDisabled()
    {
        control.Clear();
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current)
    {
        ClearOutput();
        messages.Clear();
        nextScan = default;
    }
}

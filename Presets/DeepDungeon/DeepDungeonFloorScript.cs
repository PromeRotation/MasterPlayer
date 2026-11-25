using ECommons.DalamudServices;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PalaceOfTheDead;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.DeepDungeon;

public abstract class DeepDungeonFloorScript : MasterPlayerScript
{
    private DeepDungeonFloorEncounter encounter = null!;
    private PalaceFloorHazards floor = null!;
    internal abstract DeepDungeonFloorEncounter CreateEncounter();
    internal abstract PalaceFloorHazards CreateFloor();
    internal abstract DungeonDefinition[] CreateBosses(uint territory);
    internal abstract uint[] SpecialActions { get; }
    internal abstract byte DungeonId { get; }
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, PalaceArea Area)> output = [];
    private DungeonDefinition[] bosses = [];
    private DateTime nextScan;
    private long observationVersion = -1;
    private bool bossActive;
    private bool bossFloor;
    private readonly DeepDungeonEffectInput effects = new();
    private bool ambiguousSource;
    public string? MechanicHintsText { get; private set; }
    internal float? PlannedFacing { get; private set; }
    protected virtual string? InputDiagnostic(IEnumerable<ReplayActorState> actors) => null;

    [UserSetting("显示隐藏陷阱")]
    public bool TrapHints { get => Settings.GetBool("TrapHints", true); set => Settings.Set("TrapHints", value); }

    public override IEnumerable<string> GetMechanisms() => ["道中范围与视线", "面向控制"];

    protected override void OnStart()
    {
        encounter = CreateEncounter();
        floor = CreateFloor();
        bosses = CreateBosses(ReplayWorld?.Territory ?? Svc.ClientState.TerritoryType);
        SuppressGenericDodge(SpecialActions);
        OnLogEvent(HandleEvent);
        OnSystemLog(id =>
        {
            floor.SystemLog(id);
            if (id == 7248)
            {
                ClearOutput();
                encounter.ClearFloor();
                nextScan = default;
            }
        });
        OnTerritoryChanged(_ => Clear());
        Scan(CurrentTime);
        Render(CurrentTime);
    }

    protected override void OnUpdate(float deltaTime)
    {
        if (CurrentTime >= nextScan || ReplayWorld != null && ReplayWorld.Version != observationVersion)
            Scan(CurrentTime);
        Render(CurrentTime);
    }

    protected virtual bool BossCast(ReplayActorState actor, uint action)
        => bosses.Any(b => b.BossOid == actor.Oid
            || b.Casts.Any(c => c.Action == action && (c.CasterOid == 0 || c.CasterOid == actor.Oid)));

    internal bool HandlesCast(LogSystemCastStartEvent cast)
        => !encounter.PreservesGenericDodge(cast.ActionId) && AcceptsCast(cast);

    private bool AcceptsCast(LogSystemCastStartEvent cast)
        => encounter.Actors.FirstOrDefault(a => a.Id == cast.SourceId) is { } actor && !BossCast(actor, cast.ActionId)
            && actor.Kind == 2 && actor.SubKind is 5 or 11 && actor.IsEnemy == true;

    private void Scan(DateTime now)
    {
        nextScan = now.AddSeconds(.1);
        var observations = ReplayWorld is { } world ? world.Actors.Values.ToArray()
            : Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now)).ToArray();
        var snapshot = ReplayWorld?.DeepDungeon ?? (ReplayWorld == null ? ReplayWorldCapture.CaptureDeepDungeon() : null);
        floor.Synchronize(snapshot, observations, ReplayWorld?.Territory ?? (ushort)Svc.ClientState.TerritoryType);
        bossActive = MasterPlayerPlugin.Instance?.Scripts?.Running.Values.Any(r => r.Script switch
        {
            DungeonScript dungeon => dungeon.ObserveDeepDungeonBoss(now, DungeonId),
            PilgrimsTraverse.PilgrimsBossScript pilgrim when DungeonId == 4 => pilgrim.ObserveFloorBoss(now),
            PilgrimsTraverse.PilgrimsTraverse30Script pilgrim when DungeonId == 4 => pilgrim.ObserveFloorBoss(now),
            _ => false,
        }) == true;
        bossFloor = snapshot is { Floor: > 0 } map && map.DungeonId == DungeonId
            && (map.Floor % 10 == 0 || DungeonId is 3 or 4 && map.Floor == 99);
        encounter.BossActive = bossActive;
        encounter.ZoneEnabled = IsMechanismEnabled("道中范围与视线") && floor.HasMap && !floor.BetweenFloors && !bossFloor;
        if (encounter is EurekaOrthos.EurekaFloorEncounter eureka)
        {
            eureka.Floor = snapshot?.Floor ?? 0;
            eureka.Territory = ReplayWorld?.Territory ?? Svc.ClientState.TerritoryType;
            eureka.AutoLOS = this is EurekaOrthos.EurekaFloorScript script && script.AutoLOS;
        }
        var observe = ReplayWorld == null || ReplayWorld.Version != observationVersion;
        encounter.SynchronizeFloor(observations, now, observe, ResolveCast, floor.ObserveCreated,
            (actor, action) => !BossCast(actor, action), Party());
        if (ReplayWorld != null) observationVersion = ReplayWorld.Version;
    }

    private protected virtual PalaceCast? ResolveCast(ReplayActorState actor)
    {
        if (actor.Cast is not { } cast || BossCast(actor, cast.ActionId)) return null;
        var row = Svc.Data?.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(cast.ActionId);
        var gaze = row?.VFX.RowId == 25;
        var eligible = actor.Kind == 2 && actor.SubKind is 5 or 11 && actor.IsEnemy == true
            && cast.ActionId is not (27503 or 33626);
        var shape = row is { } data && eligible
            && (gaze || cast.IsSpell != false && data.CastType != 1 && !(data.CastType is 2 or 5 && data.EffectRange >= 30))
                ? PalaceTrashSpell.Shape(data.CastType, data.EffectRange, data.XAxisModifier, actor.HitboxRadius,
                    data.Omen.ValueNullable?.Path.ToString()) : null;
        return new(actor.Id, cast.ActionId, cast.TargetId, cast.Location, actor.Position, cast.Rotation,
            cast.StartedAt, cast.FinishAt, shape, row?.Name.ToString() ?? $"技能 {cast.ActionId}",
            row?.TargetArea == true, row?.CastType == 8, (row?.ExtraCastTime100ms ?? 0) * .1f, gaze && eligible);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        if (ev is NativeActionEffectEvent effect)
        {
            Scan(CurrentTime);
            if (AcceptsEffectSource(effect.SourceId, effect.SourceOid, effect.SourceObjectIndex)
                && effects.Native(effect.SourceId, effect.ActionId, effect.GlobalSequence, effect.Timestamp))
                encounter.Effect(effect.SourceId, effect.ActionId);
            Render(CurrentTime);
            return;
        }
        if (ev is LogSystemActionEffectEvent expanded)
        {
            // Live/ARR already has the complete native capture. Legacy-only MP
            // recordings still carry the original per-packet timestamp on every
            // expanded target; no main target needs to be guessed for these lists.
            if (ReplayWorld == null) return;
            Scan(CurrentTime);
            if (AcceptsEffectSource(expanded.SourceId)
                && effects.Legacy(expanded.SourceId, expanded.ActionId, expanded.GlobalSequence, expanded.Timestamp))
                encounter.Effect(expanded.SourceId, expanded.ActionId);
            Render(CurrentTime);
            return;
        }
        if (ev is not LogSystemCastStartEvent cast) return;
        Scan(CurrentTime);
        if (!AcceptsCast(cast))
        {
            Render(CurrentTime);
            return;
        }
        var source = encounter.Actors.First(a => a.Id == cast.SourceId);
        var row = Svc.Data?.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(cast.ActionId);
        var time = cast.Timestamp.ToUniversalTime();
        encounter.StartPacket(new(cast.SourceId, cast.ActionId, cast.TargetId, cast.EffectPosition,
            cast.SourcePosition ?? source.Position, CastRotation.TryGet(cast, out var facing) ? facing : null,
            time, time.AddSeconds(cast.CastTime + .3f), CastExtra: (row?.ExtraCastTime100ms ?? 0) * .1f));
        Render(CurrentTime);
    }

    private bool AcceptsEffectSource(ulong source, uint? oid = null, int? objectIndex = null)
    {
        var current = encounter.Actors.FirstOrDefault(a => a.Id == source);
        if (current == null) return false;
        if (oid != null && current.Oid != oid || objectIndex != null && current.ObjectIndex != objectIndex) return false;
        if (oid == null && objectIndex == null && encounter.RequiresEffectIdentity(source))
        {
            // A preceding world frame may already have replaced the shell.
            // Validate before cross-stream pairing so rejected legacy packets
            // cannot consume a later complete packet with proven identity.
            ambiguousSource = true;
            return false;
        }
        return true;
    }

    private void Render(DateTime now)
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        if (CurrentPlayer is not { Dead: false } player || !IsMechanismEnabled("道中范围与视线")
            || floor.BetweenFloors || bossActive)
        {
            ClearOutput();
            return;
        }
        var wanted = encounter.Collect(now, player).Concat(bossFloor ? [] : floor.Collect(now, player, TrapHints)).ToArray();
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
                if (!runtime.Manager.ContainsArea(handle))
                {
                    handle = area.Obstacle ? SafeZone.AddObstacle(area.Shape, binding)
                        : area.Safe ? SafeZone.AddPersistentSafeArea(area.Shape, binding, area.Priority)
                        : SafeZone.AddExactDangerSnapshot(area.Shape, binding, [area.Window]);
                    SafeZone.SetNavigationOnly(handle, true);
                }
                runtime.Manager.UpdateArea(handle, template: area.Shape, binding: binding,
                    dangerWindows: area.Obstacle || area.Safe ? null : [area.Window],
                    goalWindows: area.Safe ? [new GoalWindow(area.From, null, area.Priority, PositionPreference.Nearest)
                    {
                        OverrideDangerMargin = area.OverrideDangerMargin,
                    }] : null, incrementVersion: false);
            }
            output[area.Key] = (handle, area);
            if (!area.NavigationOnly)
                Draw.AddAoePreview(area.Key, area.Geometry, area.Safe, area.OutlineOnly, .5f, area.Unclipped, area.Color);
        }
        PlannedFacing = IsMechanismEnabled("面向控制") ? encounter.Facing(now, player) : null;
        Hints.SetAutoAttackRestrictions(encounter.SpikedTargets(now));
        Hints.SetFacing(PlannedFacing);
        // The public runtime keeps replay planning isolated from native movement.
        runtime.RequestAutomaticMovement(this, !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0,
            goalDirectedTemporalPlanning: true);
        var messages = encounter.Hints(now, player)
            .Concat(InputDiagnostic(encounter.Actors) is { } input ? [input] : [])
            .Concat(encounter is EurekaOrthos.EurekaFloorEncounter { LosDiagnostic: { } diagnostic } ? [diagnostic] : [])
            .Concat(effects.AmbiguousZeroStreams ? ["录像的两路零序号效果时间不同，无法确认是否同包"] : [])
            .Concat(ambiguousSource ? ["效果包缺少来源代际，无法确认换代期间的归属"] : []).ToArray();
        MechanicHintsText = messages.Length == 0 ? null : string.Join("；", messages);
    }

    private void Remove(string key)
    {
        if (output.Remove(key, out var old) && old.Handle.IsValid) SafeZone.Remove(old.Handle);
        Draw.Remove(key);
    }

    private void ClearOutput()
    {
        Hints.SetAutoAttackRestrictions();
        Hints.SetFacing(null);
        PlannedFacing = null;
        MechanicHintsText = null;
        foreach (var key in output.Keys.ToArray()) Remove(key);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }

    private void Clear()
    {
        ClearOutput();
        encounter.Reset();
        floor.Reset();
        effects.Reset();
        ambiguousSource = false;
        nextScan = default;
        observationVersion = -1;
        bossActive = false;
        bossFloor = false;
    }

    protected override void OnStop() => Clear();
    protected override void OnMovementDisabled()
    {
        Hints.SetAutoAttackRestrictions();
        Hints.SetFacing(null);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current)
    {
        ClearOutput();
        nextScan = default;
    }

    private ReplayPartyMember[] Party()
    {
        if (ReplayWorld is { } world)
        {
            // Feed party slots are contiguous, independently of PR's role slots.
            // ReplayFeedAdapter anchors slot zero to the recording's local player,
            // not the currently selected viewing actor.
            var result = new List<ReplayPartyMember>(8);
            if (world.Party.FirstOrDefault(p => p.EntityId == world.LocalPlayerId) is { } local)
                result.Add(local with { Slot = 0 });
            foreach (var member in world.Party)
            {
                if (member.EntityId == world.LocalPlayerId || result.Count >= 8) continue;
                result.Add(member with { Slot = result.Count });
            }
            return result.ToArray();
        }
        var live = new List<ReplayPartyMember>(8);
        var localId = ReplayWorldCapture.Id(Svc.Objects.LocalPlayer);
        if (localId != 0)
            live.Add(new(0, "", localId));
        foreach (var member in PromeRotation.Helpers.PartyHelper.GetParty())
        {
            var id = ReplayWorldCapture.Id(member);
            if (id is 0 or 0xE0000000 || live.Any(p => p.EntityId == id) || live.Count >= 8) continue;
            live.Add(new(live.Count, "", id));
        }
        return live.ToArray();
    }
}

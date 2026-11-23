using ECommons.DalamudServices;
using MasterPlayer.Dodge;
using MasterPlayer.Replay;
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
    private readonly PalaceFloorNavigation floor = new();
    private readonly PalaceCombatControl control = new();
    private readonly PalaceFloorInteraction interaction = new();
    private readonly PalacePomanderControl pomanders = new();
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, PalaceArea Area)> output = [];
    private readonly Dictionary<string, DateTime> messages = [];
    private DateTime nextScan;
    private DateTime nextInterrupt;
    private bool ownsBoundary;

    [UserSetting("楼层清怪")]
    public PalaceClearBehavior ClearBehavior
    {
        get => Settings.Get("ClearBehavior", PalaceClearBehavior.Leveling);
        set => Settings.Set("ClearBehavior", value);
    }
    [UserSetting("自动前往转移石塔")]
    public bool AutoPassage { get => Settings.GetBool("AutoPassage", true); set => Settings.Set("AutoPassage", value); }
    [UserSetting("自动前往宝箱")]
    public bool AutoMoveTreasure { get => Settings.GetBool("AutoMoveTreasure", true); set => Settings.Set("AutoMoveTreasure", value); }
    [UserSetting("优先开启宝箱")]
    public bool OpenChestsFirst { get => Settings.GetBool("OpenChestsFirst", false); set => Settings.Set("OpenChestsFirst", value); }
    [UserSetting("开启金箱")]
    public bool GoldCoffer { get => Settings.GetBool("GoldCoffer", true); set => Settings.Set("GoldCoffer", value); }
    [UserSetting("开启银箱")]
    public bool SilverCoffer { get => Settings.GetBool("SilverCoffer", true); set => Settings.Set("SilverCoffer", value); }
    [UserSetting("开启铜箱")]
    public bool BronzeCoffer { get => Settings.GetBool("BronzeCoffer", true); set => Settings.Set("BronzeCoffer", value); }
    [UserSetting("探索所有房间")]
    public bool FullClear { get => Settings.GetBool("FullClear", false); set => Settings.Set("FullClear", value); }
    [UserSetting("显示隐藏陷阱")]
    public bool TrapHints { get => Settings.GetBool("TrapHints", true); set => Settings.Set("TrapHints", value); }
    [UserSetting("导航暂停前的拉怪数量（0为战斗即暂停）")]
    public int MaxPull { get => Settings.Get("MaxPull", 0); set => Settings.Set("MaxPull", Math.Clamp(value, 0, 15)); }

    public bool EncounterActive => encounter.BossActive;
    internal IReadOnlyList<ushort> FloorRooms => floor.Rooms;
    internal int FloorCurrentRoom => floor.CurrentRoom(CurrentPlayerId);
    internal int FloorDestination => floor.DesiredRoom;
    internal bool IsAutoPomanderEnabled(uint id) => Settings.GetBool($"AutoPomander{id}", false);
    internal void SetAutoPomanderEnabled(uint id, bool enabled) => Settings.Set($"AutoPomander{id}", enabled);
    internal int? RequestedPomanderSlot { get; private set; }
    internal void SelectFloorRoom(int room)
    {
        if (floor.SelectRoom(room, CurrentPlayerId)) Render(CurrentTime);
    }
    internal void ClearFloorDestination()
    {
        floor.ClearDestination();
        Render(CurrentTime);
    }
    internal void IgnoreNearestFloorTrap()
    {
        if (CurrentPlayer is { } player && floor.IgnoreNearestTrap(player.Position)) Render(CurrentTime);
    }
    public int DangerCount => output.Values.Count(v => !v.Area.Safe && !v.Area.PreviewOnly && !v.Area.Obstacle);
    public override IEnumerable<string> GetMechanisms() => ["道中范围与视线", "Boss机制", "战斗控制"];

    internal bool HandlesCast(LogSystemCastStartEvent cast)
    {
        var source = encounter.Actors.FirstOrDefault(a => a.Id == cast.SourceId);
        return source != null ? source.Hostile || PalaceCatalog.IsEnemy(source.Oid)
            : ReplayWorld == null && Svc.Objects.SearchByEntityId((uint)cast.SourceId) is { } live
                && PalaceCatalog.IsEnemy(live.BaseId);
    }

    protected override void OnStart()
    {
        SuppressGenericDodge(PalaceCatalog.SpecialActions);
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
        if (CurrentTime < nextScan) return;
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
        encounter.Synchronize(observations, now);
        var snapshot = ReplayWorld is { } replay ? replay.DeepDungeon : ReplayWorldCapture.CaptureDeepDungeon();
        floor.Synchronize(snapshot, observations, ReplayWorld?.Territory ?? (ushort)Svc.ClientState.TerritoryType);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        var now = CurrentTime;
        var eventTime = ev.Timestamp.ToUniversalTime();
        if (ev is ReplaySystemLogEvent { MessageId: 7222 } systemLog)
        {
            floor.SystemLog(systemLog.MessageId, systemLog.Arguments);
            Render(now);
            return;
        }
        if (ev is LogSystemCastStartEvent cast)
        {
            Scan(now);
            var source = encounter.Actors.FirstOrDefault(a => a.Id == cast.SourceId);
            if (source == null || !source.Hostile && !PalaceCatalog.IsEnemy(source.Oid)) return;
            var rotation = CastRotation.TryGet(cast, out var locked) ? locked : (float?)null;
            GeometryTemplate? shape = PalaceCatalog.SpecialShape(cast.ActionId);
            var special = PalaceCatalog.SpecialActions.Contains(cast.ActionId);
            var row = special ? null : Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(cast.ActionId);
            var location = special ? cast.ActionId is 7155 or 7161 or 7163 or 7169 : row?.TargetArea == true;
            var position = cast.EffectPosition;
            var delay = .3f;
            if (!special
                && AoeShapeDatabase.TryGetShape(cast.ActionId, source.HitboxRadius, out var descriptor) && descriptor != null)
            {
                shape = AoeShapeDatabase.CreateTemplate(descriptor, cast);
                if (descriptor.IsCharge && cast.SourcePosition is { } origin)
                {
                    position = origin;
                    rotation = MathF.Atan2(cast.EffectPosition.X - origin.X, cast.EffectPosition.Z - origin.Z);
                }
            }
            // Valfodr is a baited charge, resolved dynamically by PalaceEncounter.
            if (cast.ActionId == 7156) shape = RayRectangleTemplate.Symmetric(6, 1);
            encounter.StartCast(new(cast.SourceId, cast.ActionId, cast.TargetId, position, cast.SourcePosition ?? source.Position,
                rotation, eventTime, eventTime.AddSeconds(cast.CastTime + delay), shape, row?.Name.ToString() ?? "", location));
        }
        else encounter.Apply(ev, eventTime);
        Render(now);
    }

    private void Render(DateTime now)
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        if (CurrentPlayer is not { Dead: false } player || floor.BetweenFloors)
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
        var floorOptions = new PalaceFloorOptions
        {
            ClearBehavior = ClearBehavior, AutoPassage = AutoPassage, AutoMoveTreasure = AutoMoveTreasure,
            OpenChestsFirst = OpenChestsFirst, GoldCoffer = GoldCoffer, SilverCoffer = SilverCoffer,
            BronzeCoffer = BronzeCoffer, FullClear = FullClear, TrapHints = TrapHints,
            MaxPull = MaxPull,
            AutoPomanders = Enumerable.Range(1, 16).Select(i => (uint)i).Where(IsAutoPomanderEnabled).ToHashSet(),
        };
        var wanted = enabled ? encounter.Collect(now, player.Id).Concat(
            encounter.BossActive ? [] : floor.Collect(now, player, floorOptions)).ToArray() : [];
        var playerInDanger = wanted.Any(a => !a.Safe && !a.PreviewOnly && !a.Obstacle && a.Geometry.Contains(player.Position));
        if (playerInDanger)
            wanted = wanted.Where(a => a.Key != "floor/goal/coffer/interact").ToArray();
        var pomander = enabled && !encounter.BossActive && !playerInDanger ? floor.PomanderToUse : null;
        RequestedPomanderSlot = pomander?.Slot;
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
                    }] : null,
                    incrementVersion: false);
            }
            output[area.Key] = (handle, area);
            if (!area.NavigationOnly) Draw.AddAoePreview(area.Key, area.Geometry, area.Safe, area.OutlineOnly, .5f);
            else Draw.Remove(area.Key);
        }
        runtime.RequestAutomaticMovement(this, enabled && !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0, goalDirectedTemporalPlanning: true);
        if (enabled && !IsReplay && ReplayWorld == null && MasterPlayerSettings.Instance.EnableSmartMovement
            && IsMechanismEnabled("战斗控制"))
        {
            var forbidden = encounter.ForbiddenTargets(now).ToArray();
            var facing = trashEnabled ? encounter.Facing(now, player.Id) : null;
            var restrictions = encounter.CastRestrictions(now, player.Id, trashEnabled);
            var target = encounter.BossActive ? encounter.PreferredTarget(player.Id, now)
                : floor.PreferredTarget(now, player, ClearBehavior);
            control.Apply(target, forbidden,
                facing, false, blockCasts: restrictions.BlockCasts, cancelCast: restrictions.CancelCast,
                forbiddenAutoAttackTargets: encounter.ForbiddenAutoAttackTargets(now));
            TryInterrupt(now, player);
            if (!encounter.BossActive) interaction.Update(now, player, floor.InteractionTarget, wanted);
            pomanders.Update(now, pomander);
        }
        else control.Clear();
        foreach (var message in encounter.Hints(now))
        {
            if (messages.TryGetValue(message, out var last) && now - last < TimeSpan.FromSeconds(10)) continue;
            messages[message] = now;
            Log(message);
        }
    }

    private void TryInterrupt(DateTime now, ReplayActorState player)
    {
        if (now < nextInterrupt || player.Cast != null) return;
        var target = encounter.Actors.FirstOrDefault(a => a.Cast is { Interruptible: true }
            && PalaceCatalog.Interrupts.Contains(a.Cast.ActionId) && a.Cast.FinishAt > now && !a.Dead);
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
        RequestedPomanderSlot = null;
        pomanders.Reset();
        foreach (var key in output.Keys.ToArray()) Remove(key);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        ReleaseBoundary();
    }
    private void Clear()
    {
        ClearOutput();
        encounter.Reset();
        floor.Reset();
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

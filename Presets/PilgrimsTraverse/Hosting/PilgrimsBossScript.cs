using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

public abstract class PilgrimsBossScript(int floor) : MasterPlayerScript
{
    private readonly PtBossEncounter encounter = floor switch
    {
        10 => new Pt10Encounter(), 20 => new Pt20Encounter(), 40 => new Pt40Encounter(),
        50 => new Pt50Encounter(), 60 => new Pt60Encounter(), 70 => new Pt70Encounter(),
        80 => new Pt80Encounter(), 90 => new Pt90Encounter(), 99 => new Pt99Encounter(),
        _ => throw new ArgumentOutOfRangeException(nameof(floor))
    };
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, PtBossEncounter.Area Area)> output = [];
    private readonly Dictionary<string, DateTime> messages = [];
    private IGeometryShape? boundary;
    private DateTime nextScan;
    private bool suspended;
    public bool EncounterActive => encounter.Active;
    public int DangerCount => output.Values.Count(v => !v.Area.Safe && !v.Area.PreviewOnly && !v.Area.Obstacle);
    public bool TargetControlActive => floor == 99 && !suspended && encounter.Active && !IsReplay
        && CurrentPlayer is { Dead: false } player && encounter.Boundary.Contains(player.Position);
    public ulong PreferredTargetId { get; private set; }

    protected override void OnStart()
    {
        SuppressGenericDodge(encounter.Actions);
        OnLogEvent(HandleEvent);
        OnBattleEnded(Suspend);
        OnPlayerDied(Suspend);
        OnBattleStarted(() => { suspended = false; nextScan = default; });
        OnPlayerRevived(() => { suspended = false; nextScan = default; });
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
        var observations = ReplayWorld is { } world ? world.Actors.Values.ToArray()
            : Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now)).ToArray();
        if (suspended)
        {
            if (!observations.Any(a => a.Oid == encounter.BossOid && a.InCombat && !a.Dead)) suspended = false;
            else return;
        }
        encounter.Synchronize(observations, now);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        if (suspended) return;
        var now = CurrentTime;
        // 两批水晶之间会切换方向；先采集已生成的上一批，避免周期扫描晚到后锁错方向。
        if (!encounter.Active || floor == 99 && ev is LogSystemActionEffectEvent
            { ActionId: 44074 or 44075 or 44076 or 44077 })
            Scan(now);
        if (ev is LogSystemCastStartEvent cast)
        {
            var rotation = CastRotation.TryGet(cast, out var locked) ? locked : (float?)null;
            encounter.StartCast(new(cast.SourceId, cast.ActionId, cast.EffectPosition, rotation, now,
                now.AddSeconds(cast.CastTime + .3), cast.TargetId, cast.SourcePosition));
            if (rotation == null && encounter.Actions.Contains(cast.ActionId))
                LogError($"PT{floor} 技能 {cast.ActionId} 缺少锁定朝向；有方向的 AOE 不使用普通朝向代替。");
        }
        else encounter.Apply(ev, now);
        Render(now);
    }

    private void Render(DateTime now)
    {
        var runtime = MasterPlayerPlugin.Instance!.SafeZone;
        var manager = runtime.Manager;
        if (suspended || (!encounter.Active && !encounter.Preparing) || CurrentPlayer is not { Dead: false } player)
        { ClearOutput(); return; }
        if (!encounter.Active)
        {
            ClearOutput();
            if (!encounter.Boundary.Contains(player.Position)) return;
            foreach (var area in encounter.Collect(now, player.Id).Where(a => a.PreviewOnly))
            {
                output[area.Key] = (default, area);
                Draw.AddAoePreview(area.Key, area.Geometry, area.Safe, area.Obstacle, .5f);
            }
            ShowHints(now, player.Id);
            return;
        }
        var wantedBoundary = encounter.Boundary;
        if (!ReferenceEquals(boundary, wantedBoundary) || !ReferenceEquals(manager.BuildSnapshot(now).ArenaBoundary, boundary))
        {
            boundary = wantedBoundary;
            manager.SetArenaBoundary(boundary);
        }
        var wanted = encounter.Collect(now, player.Id);
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
                if (manager.ContainsArea(handle) && (old.Area.Safe != area.Safe || old.Area.Obstacle != area.Obstacle))
                { SafeZone.Remove(handle); handle = default; }
                if (!manager.ContainsArea(handle))
                {
                    handle = area.Obstacle ? SafeZone.AddTerrainObstacle(area.Shape, binding)
                        : area.Safe ? SafeZone.AddPersistentSafeArea(area.Shape, binding, area.Priority)
                        : SafeZone.AddExactDangerSnapshot(area.Shape, binding, [area.Window]);
                    SafeZone.SetNavigationOnly(handle, true);
                }
                if (old.Area != area || old.Handle != handle)
                    manager.UpdateArea(handle, template: area.Shape, binding: binding,
                        dangerWindows: !area.Safe && !area.Obstacle ? [area.Window] : null,
                        goalWindows: area.Safe ? [new GoalWindow(area.From, area.Until == DateTime.MaxValue ? null : area.Until, area.Priority, PositionPreference.Nearest)] : null,
                        incrementVersion: false);
            }
            output[area.Key] = (handle, area);
            if (!area.NavigationOnly)
                Draw.AddAoePreview(area.Key, area.Geometry, area.Safe, area.Obstacle, .5f);
            else Draw.Remove(area.Key);
        }
        runtime.RequestAutomaticMovement(this, !IsReplay, hold: encounter.HoldMovement(now, player.Id),
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0, goalDirectedTemporalPlanning: true);
        UpdateTarget(now, player.Id);
        ShowHints(now, player.Id);
    }

    private void UpdateTarget(DateTime now, ulong player)
    {
        PreferredTargetId = 0;
        if (!TargetControlActive || encounter is not Pt99Encounter pt99) return;
        var current = Svc.Targets.Target;
        var target = pt99.SelectTarget(now, player, ReplayWorldCapture.Id(current));
        if (target == null)
        {
            if (current != null && Pt99Encounter.IsCombatTarget(current.BaseId)) Svc.Targets.Target = null;
            return;
        }
        if (ReplayWorldCapture.Id(current) == target.Id)
        {
            PreferredTargetId = target.Id;
            return;
        }
        var live = Svc.Objects.OfType<IBattleChara>().FirstOrDefault(a => a.Address != 0
            && ReplayWorldCapture.Id(a) == target.Id && a.BaseId == target.Oid
            && !a.IsDead && a.IsTargetable && a.CurrentHp > 1);
        if (live != null)
        {
            PreferredTargetId = target.Id;
            Svc.Targets.Target = live;
        }
    }

    private void ShowHints(DateTime now, ulong player)
    {
        foreach (var hint in encounter.Hints(now, player))
        {
            if (messages.TryGetValue(hint, out var last) && now - last < TimeSpan.FromSeconds(10)) continue;
            messages[hint] = now;
            Log(hint);
        }
    }

    private void Remove(string key)
    {
        if (output.Remove(key, out var old) && old.Handle.IsValid) SafeZone.Remove(old.Handle);
        Draw.Remove(key);
    }
    private void ClearOutput()
    {
        PreferredTargetId = 0;
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        runtime?.RequestAutomaticMovement(this, false);
        foreach (var key in output.Keys.ToArray()) Remove(key);
        if (boundary != null && ReferenceEquals(runtime?.Manager.BuildSnapshot(CurrentTime).ArenaBoundary, boundary)) runtime.Manager.ClearArenaBoundary();
        boundary = null;
    }
    private void Suspend() { suspended = true; encounter.Reset(); ClearOutput(); }
    protected override void OnStop() { Suspend(); messages.Clear(); }
    protected override void OnMovementDisabled() => MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current) { ClearOutput(); messages.Clear(); nextScan = default; }
}

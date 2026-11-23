using ECommons.DalamudServices;
using MasterPlayer.Replay;
using MasterPlayer.Settings;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

namespace MasterPlayer.Mechanics.Dungeons;

public abstract class CastrumAbaniaScript : MasterPlayerScript
{
    private readonly CastrumAbaniaEncounter encounter = new();
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, CastrumArea Area)> output = [];
    private readonly HashSet<string> drawLines = [];
    private readonly Dictionary<string, DateTime> messages = [];
    private IGeometryShape? boundary;
    private DateTime nextScan;
    private bool suspended;
    private long replayVersion;
    private readonly CastrumCannonControl cannonControl = new();

    public bool EncounterActive => encounter.Active;

    public override IEnumerable<string> GetMechanisms()
        => ["Magna Roader", "Subject Number XXIV", "Inferno", "连线与点名"];

    protected override void OnStart()
    {
        SuppressGenericDodge(CastrumAbaniaCatalog.Actions);
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
        if (CurrentTime < nextScan && (ReplayWorld == null || ReplayWorld.Version == replayVersion)) return;
        Scan(CurrentTime);
        Render(CurrentTime);
    }

    protected override void OnMovementDisabled()
    {
        cannonControl.Clear();
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
    }

    protected override void OnCurrentPlayerChanged(ulong previous, ulong current)
    {
        ClearOutput();
        cannonControl.Clear();
        messages.Clear();
        nextScan = default;
    }

    protected override void OnStop()
    {
        ClearOutput();
        encounter.Reset();
        messages.Clear();
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        if (suspended) return;
        var now = CurrentTime;
        var eventTime = ev.Timestamp.ToUniversalTime();
        switch (ev)
        {
            case LogSystemCastStartEvent cast:
            {
                Scan(now);
                var rotation = CastRotation.TryGet(cast, out var locked) ? locked : (float?)null;
                var actor = encounter.Actors.GetValueOrDefault(cast.SourceId);
                if (actor != null)
                {
                    encounter.StartCast(cast.SourceId, actor.Oid, cast.ActionId, cast.EffectPosition, rotation,
                        eventTime, eventTime.AddSeconds(cast.CastTime + .3f), cast.TargetId, cast.SourcePosition ?? actor.Position);
                    if (rotation == null && encounter.IsHandledCast(actor.Oid, cast.ActionId)
                        && CastrumAbaniaEncounter.IsDirectional(cast.ActionId))
                        LogError($"白山堡技能 {cast.ActionId} 缺少锁定朝向。");
                }
                break;
            }
            case LogSystemActionEffectEvent effect:
            {
                encounter.Apply(effect, eventTime);
                break;
            }
            case LogSystemTargetIconEvent icon:
            {
                if (!encounter.Active) Scan(now);
                encounter.Icon(icon, eventTime);
                break;
            }
            case LogSystemStatusAddEvent status:
            {
                encounter.Status(status);
                break;
            }
            case LogSystemTetherEvent tether:
            {
                encounter.Tether(tether, eventTime);
                break;
            }
            case LogSystemTetherCancelEvent cancel:
            {
                encounter.TetherCancel(cancel);
                break;
            }
            case LogSystemCancelCastEvent cancel:
            {
                encounter.Cancel(cancel.SourceId, cancel.ActionId);
                break;
            }
            case LogSystemInterruptCastEvent interrupt:
            {
                encounter.Cancel(interrupt.SourceId, interrupt.ActionId);
                break;
            }
        }
        Render(now);
    }

    private void Scan(DateTime now)
    {
        nextScan = now.AddSeconds(.1);
        var observations = ReplayWorld is { } world
            ? world.Actors.Values.ToArray()
            : Svc.Objects.Where(o => o.Address != 0 && ReplayWorldCapture.Id(o) is not (0 or 0xE0000000))
                .Select(o => ReplayWorldCapture.CaptureActor(o, now)).ToArray();
        replayVersion = ReplayWorld?.Version ?? 0;
        if (suspended)
        {
            if (observations.Any(a => CastrumAbaniaCatalog.IsBoss(a.Oid) && a.InCombat && !a.Dead)) return;
            suspended = false;
        }
        var party = (ReplayWorld?.Party ?? ReplayWorldCapture.CaptureParty()).Select(p => p.EntityId).ToHashSet();
        party.Add(CurrentPlayerId);
        encounter.Synchronize(observations, now, party);
    }

    private void Render(DateTime now)
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null || CurrentPlayer is not { Dead: false } player || suspended || !encounter.Active
            || !IsMechanismEnabled(CastrumAbaniaCatalog.Name(encounter.ActiveBossOid)))
        {
            ClearOutput();
            return;
        }

        var manager = runtime.Manager;
        if (!ReferenceEquals(boundary, encounter.Boundary)
            || !ReferenceEquals(manager.BuildSnapshot(now).ArenaBoundary, encounter.Boundary))
        {
            boundary = encounter.Boundary;
            if (boundary != null) manager.SetArenaBoundary(boundary);
        }

        var wanted = encounter.Collect(now, player.Id)
            .Where(IsAreaEnabled)
            .ToArray();
        var keys = wanted.Select(a => a.Key).ToHashSet();
        foreach (var key in output.Keys.Where(k => !keys.Contains(k)).ToArray())
            Remove(key);

        foreach (var area in wanted)
        {
            using var group = SafeZone.BeginDrawingGroup(area.Name);
            var binding = new StaticTransform(area.Position, area.Rotation);
            var handle = output.TryGetValue(area.Key, out var old) ? old.Handle : default;
            if (area.PreviewOnly)
            {
                if (handle.IsValid) SafeZone.Remove(handle);
                handle = default;
            }
            else
            {
                if (manager.ContainsArea(handle) && old.Area.Safe != area.Safe)
                {
                    SafeZone.Remove(handle);
                    handle = default;
                }
                if (!manager.ContainsArea(handle))
                {
                    handle = area.Safe
                        ? SafeZone.AddPersistentSafeArea(area.Shape, binding, priority: 5)
                        : SafeZone.AddExactDangerSnapshot(area.Shape, binding, [area.Window]);
                    SafeZone.SetNavigationOnly(handle, true);
                }
                manager.UpdateArea(handle, template: area.Shape, binding: binding,
                    dangerWindows: area.Safe ? null : [area.Window],
                    goalWindows: area.Safe ? [new GoalWindow(area.From, area.Until, 5, PositionPreference.Nearest)] : null,
                    incrementVersion: false);
            }
            output[area.Key] = (handle, area);
            if (!area.NavigationOnly) Draw.AddAoePreview(area.Key, area.Geometry, area.Safe, area.OutlineOnly,
                Math.Clamp(1 - (float)(area.From - now).TotalSeconds / 6, 0, 1));
            else Draw.Remove(area.Key);
        }

        var wantedLines = new HashSet<string>();
        foreach (var landing in wanted.Where(a => a.Key.StartsWith("knockback-landing/", StringComparison.Ordinal)))
        {
            var id = landing.Key + "/line";
            wantedLines.Add(id);
            Draw.AddTether(id, player.Position, landing.Position, landing.Safe
                ? PromeRotation.Spatial.Drawing.DrawStyle.Safe : PromeRotation.Spatial.Drawing.DrawStyle.Danger, durationMs: 500);
        }
        foreach (var (source, tether) in encounter.ClawTethers.Where(_ => IsMechanismEnabled("连线与点名")))
        {
            if (!encounter.Actors.TryGetValue(source, out var start)
                || !encounter.Actors.TryGetValue(tether.Target, out var end)) continue;
            var id = $"claw-line/{source}/{tether.Target}";
            wantedLines.Add(id);
            Draw.AddTether(id, start.Position, end.Position, PromeRotation.Spatial.Drawing.DrawStyle.Danger, durationMs: 500);
        }
        foreach (var id in drawLines.Where(id => !wantedLines.Contains(id)).ToArray())
            Draw.Remove(id);
        drawLines.Clear();
        drawLines.UnionWith(wantedLines);

        runtime.RequestAutomaticMovement(this, !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin,
            arenaSafetyMargin: 0, goalDirectedTemporalPlanning: true);
        Hints.SetActionRestrictions(forbiddenTargets: encounter.ForbiddenTargets);
        Hints.SetTarget(encounter.PriorityTarget(player.TargetId, player.Position));
        cannonControl.Update(encounter, player, !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement
            && runtime.Setting.MovementAllowed, now, CastrumCannonControl.Interact, CastrumCannonControl.Pulse);

        foreach (var hint in encounter.Hints(now, player.Id))
        {
            if (!IsMechanismEnabled("连线与点名")
                && (hint.StartsWith("Rahu Ray", StringComparison.Ordinal)
                    || hint.StartsWith("Death Grip", StringComparison.Ordinal)
                    || hint.StartsWith("Sparking Current", StringComparison.Ordinal))) continue;
            if (messages.TryGetValue(hint, out var last) && now - last < TimeSpan.FromSeconds(10)) continue;
            messages[hint] = now;
            Log(hint);
        }
    }

    private void Remove(string key)
    {
        if (output.Remove(key, out var old) && old.Handle.IsValid)
            SafeZone.Remove(old.Handle);
        Draw.Remove(key);
    }

    private bool IsAreaEnabled(CastrumArea area)
    {
        if (area.Action is 7978 or 33467 || area.Key.StartsWith("claw/", StringComparison.Ordinal))
            return IsMechanismEnabled("连线与点名");
        return true;
    }

    private void ClearOutput()
    {
        cannonControl.Clear();
        Hints.ClearActionRestrictions();
        Hints.SetTarget(0);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        foreach (var key in output.Keys.ToArray())
            Remove(key);
        foreach (var id in drawLines.ToArray())
            Draw.Remove(id);
        drawLines.Clear();
        var manager = MasterPlayerPlugin.Instance?.SafeZone.Manager;
        if (boundary != null && ReferenceEquals(manager?.BuildSnapshot(CurrentTime).ArenaBoundary, boundary))
            manager.ClearArenaBoundary();
        boundary = null;
    }

    private void Suspend()
    {
        suspended = true;
        ClearOutput();
        encounter.Reset();
    }
}

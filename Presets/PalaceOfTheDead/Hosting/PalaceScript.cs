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
    private readonly PalaceCombatControl control = new();
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, PalaceArea Area)> output = [];
    private readonly Dictionary<string, DateTime> messages = [];
    private DateTime nextScan;
    private DateTime nextInterrupt;
    private bool ownsBoundary;

    public bool EncounterActive => encounter.BossActive;
    public int DangerCount => output.Values.Count(v => !v.Area.Safe && !v.Area.PreviewOnly);
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
        OnSystemLog(id => { if (id == 7248) Clear(); });
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
        encounter.Synchronize(observations, now);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        var now = CurrentTime;
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
                rotation, now, now.AddSeconds(cast.CastTime + delay), shape, row?.Name.ToString() ?? "", location));
        }
        else encounter.Apply(ev, now);
        Render(now);
    }

    private void Render(DateTime now)
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        if (CurrentPlayer is not { Dead: false } player)
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
        var wanted = enabled ? encounter.Collect(now, player.Id) : [];
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
                    handle = area.Safe ? SafeZone.AddPersistentSafeArea(area.Shape, binding, priority: 5)
                        : SafeZone.AddExactDangerSnapshot(area.Shape, binding, [area.Window]);
                    SafeZone.SetNavigationOnly(handle, true);
                }
                manager.UpdateArea(handle, template: area.Shape, binding: binding,
                    dangerWindows: area.Safe ? null : [area.Window],
                    goalWindows: area.Safe ? [new GoalWindow(area.From, area.Until, 5, PositionPreference.Nearest)] : null,
                    incrementVersion: false);
            }
            output[area.Key] = (handle, area);
            if (!area.NavigationOnly) Draw.AddAoePreview(area.Key, area.Geometry, area.Safe, false, .5f);
            else Draw.Remove(area.Key);
        }
        runtime.RequestAutomaticMovement(this, enabled && !IsReplay,
            hardSafetyMargin: MasterPlayerSettings.Instance.DodgeDangerMargin, arenaSafetyMargin: 0, goalDirectedTemporalPlanning: true);
        if (enabled && !IsReplay && MasterPlayerSettings.Instance.EnableSmartMovement && IsMechanismEnabled("战斗控制"))
        {
            var forbidden = encounter.ForbiddenTargets(now).ToArray();
            control.Apply(encounter.PreferredTarget(player.Id, now), forbidden,
                trashEnabled ? encounter.Facing(now, player.Id) : null, false);
            TryInterrupt(now, player);
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
        foreach (var key in output.Keys.ToArray()) Remove(key);
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        ReleaseBoundary();
    }
    private void Clear()
    {
        ClearOutput();
        encounter.Reset();
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

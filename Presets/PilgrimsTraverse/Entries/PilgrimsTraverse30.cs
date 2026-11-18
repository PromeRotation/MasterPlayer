using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;

namespace MasterPlayer.Scripts.PilgrimsTraverse;

[MechanicScript("朝圣之路 30层 — Forgiven Treachery", [1283u], "promeradar.pt30", "1.0.0", "XSZ", "光轮、左右连招、危险球与八连追踪；独立于 BMR")]
public sealed class PilgrimsTraverse30 : MasterPlayerScript
{
    private readonly Pt30Encounter encounter = new();
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, Pt30Encounter.Hazard Hazard)> hazards = [];
    private SafeZoneAreaHandle hole;
    private SafeZoneAreaHandle goal;
    private IGeometryShape? boundary;
    private DateTime nextScan;
    private DateTime nextLog;
    private Vector3? lastGoal;
    private GeometryTemplate? lastGoalTemplate;
    private float lastGoalRotation;
    public bool EncounterActive => encounter.Active;
    public bool NavigationReady => encounter.Active && boundary != null && hole.IsValid;
    public int DangerCount => hazards.Count;
    public int PreviewCount { get; private set; }
    public int BallCount => encounter.BallCount;
    public int ChaseRemaining => encounter.Remaining;
    public int GoalCount => goal.IsValid ? 1 : 0;
    public string DiagnosticState => encounter.Status;

    protected override void OnStart()
    {
        SuppressGenericDodge(Pt30Encounter.Actions);
        OnLogEvent(HandleEvent);
        OnBattleEnded(Reset);
        OnPlayerDied(Reset);
        Scan(CurrentTime);
        Log("PT30 MP 自有脚本已启动，等待 Boss；不执行 BMR PT30 绘制/导航。");
    }

    protected override void OnUpdate(float deltaTime)
    {
        var now = CurrentTime;
        if (now < nextScan) return;
        nextScan = now.AddSeconds(.1);
        Scan(now);
        Render(now);
        if (encounter.Active && now >= nextLog)
        {
            nextLog = now.AddSeconds(2);
            Log($"[PT30] {encounter.Status}; dangers={hazards.Count}; player={CurrentPlayer?.Position}; hp={CurrentPlayer?.Hp}; replay={IsReplay}");
        }
    }

    private unsafe void Scan(DateTime now)
    {
        var actors = new List<Pt30Encounter.Actor>();
        if (ReplayWorld is { } world)
        {
            foreach (var actor in world.Actors.Values)
            {
                if (actor.Oid is Pt30Encounter.BOSS_OID or Pt30Encounter.BALL_OID)
                    actors.Add(new(actor.Id, actor.Oid, actor.Position, actor.Rotation, actor.Dead, actor.InCombat));
                // Self-target helper casts have a zero native TargetLocation. Their packet carries
                // the actual offset origin (notably Grip), so never lock a native zero before it.
                if (actor.Cast is { ActionId: 44918 } cast && cast.Rotation is { } rotation)
                    encounter.Cast(actor.Id, cast.ActionId, cast.Location, rotation, cast.StartedAt, cast.FinishAt);
                if (actor.EventAnimation is { } animation) encounter.Timeline(actor.Id, (ushort)animation);
            }
        }
        else
        {
            foreach (var obj in Svc.Objects)
            {
                if (obj.BaseId is Pt30Encounter.BOSS_OID or Pt30Encounter.BALL_OID)
                    actors.Add(new(obj.EntityId, obj.BaseId, obj.Position, obj.Rotation, obj.IsDead,
                        obj is IBattleChara b && (b.StatusFlags & StatusFlags.InCombat) != 0));
                if (obj is not IBattleChara { IsCasting: true, CastActionId: 44918 } caster) continue;
                var native = (NativeBattleChara*)caster.Address;
                if (native == null || !float.IsFinite(native->CastRotation)) continue;
                var p = native->CastInfo.TargetLocation;
                encounter.Cast(caster.EntityId, caster.CastActionId, new(p.X, p.Y, p.Z), native->CastRotation,
                    now.AddSeconds(-caster.CurrentCastTime), now.AddSeconds(Math.Max(0, caster.TotalCastTime - caster.CurrentCastTime) + .3));
            }
        }
        encounter.Synchronize(actors, now);
    }

    private void HandleEvent(LogSystemEvent ev)
    {
        var now = CurrentTime;
        switch (ev)
        {
            case LogSystemCastStartEvent cast when Pt30Encounter.Actions.Contains(cast.ActionId):
                if (CastRotation.TryGet(cast, out var rotation))
                {
                    encounter.Cast(cast.SourceId, cast.ActionId, cast.EffectPosition, rotation, now, now.AddSeconds(cast.CastTime + .3));
                    Log($"[PT30] cast action={cast.ActionId} source={cast.SourceId:X} loc={cast.EffectPosition} rotation={rotation:F3} seconds={cast.CastTime:F2}");
                }
                else LogError($"[PT30] 缺少锁定朝向 action={cast.ActionId}, source={cast.SourceId:X}；未发布该危险区，检查读条包捕获。");
                break;
            case LogSystemTargetIconEvent icon when icon.MarkerId == 197:
                encounter.Icon(icon.TargetId is 0 or 0xE0000000 ? icon.SourceId : icon.TargetId, icon.MarkerId, now);
                Log($"[PT30] Divine Favor icon source={icon.SourceId:X} target={icon.TargetId:X}");
                break;
            case LogSystemPlayActionTimelineEvent timeline:
                encounter.Timeline(timeline.SourceId, (ushort)timeline.TimelineId);
                break;
            case LogSystemCancelCastEvent cancel:
                encounter.Cancel(cancel.SourceId);
                break;
            case LogSystemInterruptCastEvent interrupt:
                encounter.Cancel(interrupt.SourceId);
                break;
            case LogSystemActionEffectEvent effect when Pt30Encounter.Actions.Contains(effect.ActionId):
                encounter.Effect(effect.SourceId, effect.GlobalSequence, effect.ActionId, effect.EffectPosition, now);
                if (effect.TargetIndex <= 0 || effect.TargetId == CurrentPlayerId)
                    Log($"[PT30] effect action={effect.ActionId} seq={effect.GlobalSequence} loc={effect.EffectPosition} targetSelf={effect.TargetId == CurrentPlayerId} remaining={encounter.Remaining}");
                break;
        }
    }

    private void Render(DateTime now)
    {
        var runtime = MasterPlayerPlugin.Instance!.SafeZone;
        var manager = runtime.Manager;
        if (!encounter.Active || CurrentPlayer is not { Dead: false } player)
        {
            ClearOutput();
            return;
        }
        if (boundary == null || !ReferenceEquals(manager.BuildSnapshot(now).ArenaBoundary, boundary))
        {
            boundary = Pt30Arena.Boundary(player.Position.Y);
            manager.SetArenaBoundary(boundary);
        }
        if (!manager.ContainsArea(hole))
            hole = SafeZone.AddTerrainObstacle(Pt30Arena.Hole(player.Position.Y), new StaticTransform(Vector3.Zero, 0));
        var wanted = encounter.Hazards(now, CurrentPlayerId, player.Position);
        var nextHalo = wanted.Where(h => h.Shape is DonutTemplate).MinBy(h => h.From);
        var keys = wanted.Select(h => h.Key).ToHashSet();
        foreach (var key in hazards.Keys.Where(k => !keys.Contains(k)).ToArray()) Remove(key);
        PreviewCount = 0;
        foreach (var hazard in wanted)
        {
            using var group = SafeZone.BeginDrawingGroup(hazard.Name);
            var windows = new[] { hazard.Window(now) };
            var binding = new StaticTransform(hazard.Position, hazard.Rotation);
            if (hazards.TryGetValue(hazard.Key, out var old) && manager.ContainsArea(old.Handle))
            {
                if (old.Hazard != hazard)
                    manager.UpdateArea(old.Handle, template: Dodge.DangerMarginInflator.Inflate(hazard.Shape, MasterPlayer.Settings.MasterPlayerSettings.Instance.DodgeDangerMargin),
                        binding: binding, dangerWindows: windows, incrementVersion: false);
                hazards[hazard.Key] = (old.Handle, hazard);
            }
            else
            {
                var handle = SafeZone.AddDangerSnapshot(hazard.Shape, binding, windows);
                SafeZone.SetNavigationOnly(handle, true);
                hazards[hazard.Key] = (handle, hazard);
            }
            var visible = (!hazard.Key.StartsWith("ball/", StringComparison.Ordinal) || hazard.Key.EndsWith("/0", StringComparison.Ordinal))
                && (hazard.Shape is not DonutTemplate || hazard == nextHalo)
                && (!hazard.Key.StartsWith("reach/", StringComparison.Ordinal) || encounter.ReachGoal != null);
            if (visible)
            {
                var preview = hazard.BallPreviewPosition is { } ballPosition
                    ? new CircleTemplate(4).Resolve(ballPosition, 0, null)
                    : hazard.Shape.Resolve(hazard.Position, hazard.Rotation, null);
                Draw.AddAoePreview(hazard.Key, preview, false, false, 1);
                ++PreviewCount;
            }
            else Draw.Remove(hazard.Key);
        }
        var chaseGoal = encounter.Goal;
        var desiredGoal = chaseGoal is { } chaseDestination
            ? new Pt30Encounter.Hazard("chase-goal", "Divine Favor 连续移动", new CircleTemplate(1), chaseDestination, 0, now, now.AddSeconds(1))
            : encounter.ReachGoal;
        runtime.RequestAutomaticMovement(this, !IsReplay, goalDirectedTemporalPlanning: encounter.ReachGoal != null,
            hardSafetyMargin: Pt30Encounter.NAVIGATION_GRID_MARGIN, arenaSafetyMargin: Pt30Encounter.NAVIGATION_GRID_MARGIN,
            noEscapeDelay: encounter.BallCount > 0 || encounter.Remaining > 0 || encounter.ReachGoal != null,
            preferSafeRefuge: encounter.BallCount > 0);
        if (desiredGoal is { } destination)
        {
            var binding = new StaticTransform(destination.Position, destination.Rotation);
            var priority = chaseGoal.HasValue ? 10 : 5;
            var preference = chaseGoal.HasValue ? PositionPreference.Center : PositionPreference.Nearest;
            if (!manager.ContainsArea(goal))
                goal = SafeZone.AddPersistentSafeArea(destination.Shape, binding, priority, preference);
            else if (destination.Position != lastGoal || destination.Shape != lastGoalTemplate || destination.Rotation != lastGoalRotation)
                manager.UpdateArea(goal, template: destination.Shape, binding: binding,
                    goalWindows: [new GoalWindow(now, null, priority, preference)]);
            lastGoal = destination.Position;
            lastGoalTemplate = destination.Shape;
            lastGoalRotation = destination.Rotation;
        }
        else if (goal.IsValid)
        {
            SafeZone.Remove(goal);
            goal = default;
            lastGoal = null;
            lastGoalTemplate = null;
        }
    }

    private void Remove(string key)
    {
        if (hazards.Remove(key, out var old)) SafeZone.Remove(old.Handle);
        Draw.Remove(key);
    }

    private void ClearOutput()
    {
        MasterPlayerPlugin.Instance?.SafeZone.RequestAutomaticMovement(this, false);
        foreach (var key in hazards.Keys.ToArray()) Remove(key);
        if (goal.IsValid) SafeZone.Remove(goal);
        if (hole.IsValid) SafeZone.Remove(hole);
        var manager = MasterPlayerPlugin.Instance?.SafeZone.Manager;
        if (boundary != null && ReferenceEquals(manager?.BuildSnapshot(CurrentTime).ArenaBoundary, boundary)) manager.ClearArenaBoundary();
        hole = goal = default;
        boundary = null;
        lastGoal = null;
        lastGoalTemplate = null;
        PreviewCount = 0;
    }

    private void Reset() { encounter.Reset(); ClearOutput(); nextScan = default; }
    protected override void OnStop() => Reset();
    protected override void OnCurrentPlayerChanged(ulong previous, ulong current) { ClearOutput(); nextScan = default; }
}

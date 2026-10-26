using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Mechanics.Crucible;

public abstract class CrucibleScript(int firstGroup, int lastGroup) : MasterPlayerScript
{
    private readonly WorldState _world = new();
    private CrucibleEngine? _engine;
    private CrucibleOutput? _output;
    private CrucibleEventAdapter? _events;
    private bool _suspended;
    private CrucibleHostControl? _control;
    private readonly CrucibleInteraction _interaction = new();
    private DateTime _nextScan, _nextDraw;
    private DateTime _nextDiagnostic;
    private bool _dirty;
    internal (MasterPlayer.Spatial.Geometry.IGeometryShape Boundary, Vector3 Center)? GetRadarArena(MasterPlayer.Spatial.Geometry.IGeometryShape? navigationBoundary)
        => _output?.GetRadarArena(navigationBoundary);
    public override IEnumerable<string> GetMechanisms() => ["危险区域", "走位引导", "场地边界", "文字提示"];

    protected override void OnStart()
    {
        _engine = new(_world, firstGroup, lastGroup);
        _control = new(Log);
        _output = new(SafeZone, Draw, MasterPlayerPlugin.Instance!.SafeZone.Manager, text => { if (IsMechanismEnabled("文字提示")) Log(text); },
            () => MasterPlayer.Settings.MasterPlayerSettings.Instance.DodgeDangerMargin);
        _suspended = false;
        _events = new(_engine, Find, e => CastRotation.TryGet(e, out var r) ? r : null, Scan, LogError);
        SuppressGenericDodge(CrucibleActionCatalog.ClaimedActions);
        OnLogEvent(OnEvent);
        OnPlayerDied(Suspend);
        OnBattleEnded(Suspend);
        OnBattleStarted(Resume);
        OnPlayerRevived(Resume);
        Scan();
    }
    protected override void OnUpdate(float deltaTime)
    {
        if (_suspended) return;
        _world.CurrentTime = DateTime.UtcNow;
        _events?.Flush();
        if (_world.CurrentTime >= _nextScan) Scan();
        if (_engine == null || _output == null || (!_dirty && _world.CurrentTime < _nextDraw)) return;
        _dirty = false;
        _nextDraw = _world.FutureTime(0.1);
        _engine.Update();
        _world.NavigationMargin = MasterPlayer.Settings.MasterPlayerSettings.Instance.DodgeDangerMargin;
        var frame = _engine.Frame();
        if (MasterPlayer.Settings.MasterPlayerSettings.Instance.EnableSmartMovement && IsMechanismEnabled("走位引导")
            && MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive != true && _world.Player is { IsDeadOrDestroyed: false } player)
        {
            var plan = CrucibleControlPlan.Build(frame.Hints, player, _world.CurrentTime);
            if (frame.Hints.InteractWithTarget is { } interact)
            {
                // Once interaction was sent, never cancel its progress as an attack cast.
                plan = plan with { HoldActions = true, BlockCasts = true, CancelCast = !_interaction.Attempted && plan.CancelCast };
                _control?.Apply(plan);
                _interaction.Update(interact, player, _world.CurrentTime, PromeRotation.Plugin.Instance.GreenMoveSystem.IsRunning,
                    Svc.Objects.LocalPlayer?.IsCasting == true, CrucibleInteraction.Interact);
            }
            else
            {
                _interaction.Clear();
                _control?.Apply(plan);
            }
        }
        else { _control?.Clear(); _interaction.Clear(); }
        _output.Apply(_engine, frame, Svc.Objects.LocalPlayer?.Position.Y ?? 0,
            IsMechanismEnabled("危险区域"), IsMechanismEnabled("走位引导"), IsMechanismEnabled("场地边界"));
        ApplyDirectRoute(frame.Hints.DirectRoute);
        DiagnoseMovement();
    }
    protected override void OnStop() { ApplyDirectRoute(null); Reset(); }

    // A component's timed route (B31 breath through drifting clouds) runs as a direct traversal;
    // the runtime still validates it against the registered dangers every frame.
    private bool _routing;
    private void ApplyDirectRoute(List<WPos>? route)
    {
        var runtime = MasterPlayerPlugin.Instance?.SafeZone;
        if (runtime == null) return;
        var allowed = route is { Count: > 0 } && MasterPlayer.Settings.MasterPlayerSettings.Instance.EnableSmartMovement
            && IsMechanismEnabled("走位引导") && MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive != true;
        if (allowed)
        {
            var y = Svc.Objects.LocalPlayer?.Position.Y ?? 0;
            runtime.RequestAutomaticMovement(this, true);
            runtime.RequestDirectTraversal(this, route![^1].ToVec3(y), _world.CurrentTime, route.Select(p => p.ToVec3(y)).ToArray());
            _routing = true;
        }
        else if (_routing)
        {
            runtime.RequestDirectTraversal(this, null, _world.CurrentTime);
            runtime.RequestAutomaticMovement(this, false);
            _routing = false;
        }
    }
    protected override void OnMovementDisabled() { ApplyDirectRoute(null); _control?.Clear(); _interaction.Clear(); }
    private void Reset() { _control?.Clear(); _interaction.Clear(); _events?.Clear(); _output?.Clear(); _engine?.Reset(); _world.Actors.Clear(); _world.Player = null; }
    private void Suspend() { _suspended = true; ApplyDirectRoute(null); Reset(); }
    private void Resume() { _suspended = false; _nextScan = default; _dirty = true; }

    private void DiagnoseMovement()
    {
        if (_engine?.Module is not { } module || _world.Player is not { } player || _world.CurrentTime < _nextDiagnostic) return;
        var movement = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus;
        var moveRunning = PromeRotation.Plugin.Instance.GreenMoveSystem.IsRunning;
        if (movement.HasControl && moveRunning) return;
        var snapshot = MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(_world.CurrentTime);
        var solution = movement.LatestSolution;
        var dangers = snapshot.DangerAreas.Where(a => a.Geometry.Contains(player.Position.ToVec3())
            && a.DangerWindows.Any(w => w.DangerUntil == null || w.DangerUntil > _world.CurrentTime)).ToArray();
        if (dangers.Length == 0) return;
        _nextDiagnostic = _world.FutureTime(1);
        var deadline = dangers.SelectMany(a => a.DangerWindows).Min(w => (w.DangerFrom - _world.CurrentTime).TotalSeconds);
        var casts = string.Join(",", _world.Actors.Where(a => a.CastInfo != null).Select(a =>
            $"{a.CastInfo!.Action.ID}@{a.CastInfo.Rotation.Deg:F1}°({a.CastInfo.LocXZ.X:F1},{a.CastInfo.LocXZ.Z:F1})"));
        Log($"[斗兽躲避诊断 B{module.Group}] 覆盖危险={dangers.Length}, 判定余量={deadline:F2}s, "
            + $"位置=({player.Position.X:F1},{player.Position.Z:F1}), 状态={movement.PhaseText}, "
            + $"解算={solution?.Status.ToString() ?? "无"}, 路点={solution?.ExecutablePath.Count ?? 0}, "
            + $"快照={snapshot.Version}/{solution?.SourceSnapshotVersion}, "
            + $"解算开始/接受/丢弃={movement.AsyncSolveStartedCount}/{movement.AsyncSolveAcceptedCount}/{movement.AsyncSolveDiscardedCount}, "
            + $"结果年龄={(solution == null ? -1 : (_world.CurrentTime - solution.SolveTime).TotalMilliseconds):F0}ms, "
            + $"自动移动={MasterPlayerPlugin.Instance!.SafeZone.Setting.EnableAutoMovement}, 持有/执行={movement.HasControl}/{moveRunning}, 读条={casts}");
    }

    private unsafe void Scan()
    {
        if (_engine == null) return;
        _world.CurrentTime = DateTime.UtcNow;
        _nextScan = _world.FutureTime(0.1);
        var seen = new HashSet<ulong>();
        var created = new List<Actor>();
        var casts = new List<(Actor, ActorCastInfo)>();
        for (var i = 0; i < Svc.Objects.Length; ++i)
        {
            if (Svc.Objects[i] is not { } obj || obj.EntityId is 0 or 0xE0000000 || obj.Address == 0) continue;
            var id = (ulong)obj.EntityId;
            seen.Add(id);
            var actor = _world.Actors.Find(id);
            var isNew = actor == null;
            if (actor == null) { actor = new() { InstanceID = id }; _world.Actors.Add(actor); created.Add(actor); }
            var wasDead = actor.IsDead;
            var oldState = actor.EventState;
            actor.OID = obj.BaseId;
            actor.Name = obj.Name.TextValue;
            actor.Position = WPos.FromVec3(obj.Position);
            actor.Rotation = new(obj.Rotation);
            actor.TargetID = obj.TargetObjectId;
            actor.HitboxRadius = obj.HitboxRadius;
            actor.IsTargetable = obj.IsTargetable;
            actor.EventState = ((NativeGameObject*)obj.Address)->EventState;
            actor.IsDestroyed = false;
            actor.Type = obj.ObjectKind == ObjectKind.Pc ? ActorType.Player : obj.ObjectKind == ObjectKind.BattleNpc ? ActorType.Enemy : obj.ObjectKind == ObjectKind.EventObj ? ActorType.EventObj : ActorType.Other;
            if (obj is IBattleChara battle)
            {
                actor.NameID = battle.NameId;
                actor.IsDead = battle.IsDead;
                actor.HPMP = (battle.CurrentHp, battle.MaxHp);
                actor.OwnerID = battle.OwnerId;
                actor.InCombat = (battle.StatusFlags & StatusFlags.InCombat) != 0;
                actor.Role = battle.ClassJob.Value.Role switch { 1 => Role.Tank, 2 => Role.Melee, 4 => Role.Healer, 3 => Role.Ranged, _ => Role.None };
                var native = (NativeBattleChara*)battle.Address;
                var model = (native->Timeline.ModelState, native->Timeline.AnimationState[0], native->Timeline.AnimationState[1]);
                if (actor.ModelState != model)
                {
                    actor.ModelState = model;
                    if (!isNew) _engine.Dispatch(c => c.OnActorModelStateChange(actor, model.Item1, model.Item2, model.Item3));
                }
                var statuses = battle.StatusList.Where(s => s.StatusId != 0).Select(s => new ActorStatus
                {
                    ID = s.StatusId, Extra = s.Param, SourceID = s.SourceId,
                    ExpireAt = s.RemainingTime > 0 ? _world.FutureTime(s.RemainingTime) : DateTime.MaxValue
                }).ToArray();
                if (!SameStatuses(actor.Statuses, statuses))
                {
                    foreach (var old in actor.Statuses.Where(s => !statuses.Any(n => n.ID == s.ID && n.SourceID == s.SourceID)))
                    {
                        var lost = old;
                        _engine.Dispatch(c => c.OnStatusLose(actor, ref lost));
                    }
                    foreach (var item in statuses.Where(s => !actor.Statuses.Any(o => o.ID == s.ID && o.SourceID == s.SourceID && o.Extra == s.Extra)))
                    {
                        var gained = item;
                        if (!isNew) _engine.Dispatch(c => c.OnStatusGain(actor, ref gained));
                    }
                }
                actor.Statuses = statuses;
                if (battle.IsCasting && battle.CastActionId != 0)
                {
                    if (actor.CastInfo == null || actor.CastInfo.Action.ID != battle.CastActionId)
                    {
                        var info = &native->CastInfo;
                        var remaining = Math.Max(0, battle.TotalCastTime - battle.CurrentCastTime);
                        if (float.IsFinite(native->CastRotation))
                        {
                            var cast = new ActorCastInfo
                            {
                                Action = new(battle.CastActionId), TargetID = battle.CastTargetObjectId,
                                LocXZ = new(info->TargetLocation.X, info->TargetLocation.Z), Rotation = new(native->CastRotation),
                                FinishAt = _world.FutureTime(remaining + 0.3), RemainingTime = remaining, NPCRemainingTime = remaining + 0.3f,
                                EventHappened = info->ResponseActionId == battle.CastActionId && info->ResponseGlobalSequence != 0
                            };
                            actor.CastInfo = cast;
                            casts.Add((actor, cast));
                        }
                    }
                    else
                    {
                        actor.CastInfo.NPCRemainingTime = (float)(actor.CastInfo.FinishAt - _world.CurrentTime).TotalSeconds;
                        actor.CastInfo.RemainingTime = actor.CastInfo.NPCRemainingTime - 0.3f;
                    }
                }
                else if (actor.CastInfo is { } previous && _world.CurrentTime > previous.FinishAt.AddSeconds(0.6)) _engine.FinishCast(actor);
            }
            if (obj.EntityId == Svc.Objects.LocalPlayer?.EntityId) _world.Player = actor;
            if (!wasDead && actor.IsDead) _engine.Destroy(actor, false);
            if (oldState != actor.EventState) _engine.Dispatch(c => { c.OnActorEventStateChange(actor, actor.EventState); c.OnActorEState(actor, actor.EventState); });
        }
        if (Svc.Objects.LocalPlayer == null) _world.Player = null;
        foreach (var actor in _world.Actors.Where(a => !seen.Contains(a.InstanceID)).ToArray()) _engine.Destroy(actor, true);
        var generation = _engine.Generation;
        _engine.SelectGroup();
        if (generation == _engine.Generation)
        {
            foreach (var actor in created)
            {
                _engine.Dispatch(c => c.OnActorCreated(actor));
                if (actor.ModelState != default) _engine.Dispatch(c => c.OnActorModelStateChange(actor, actor.ModelState.ModelState, actor.ModelState.AnimState1, actor.ModelState.AnimState2));
                foreach (var item in actor.Statuses)
                {
                    var gained = item;
                    _engine.Dispatch(c => c.OnStatusGain(actor, ref gained));
                }
            }
            foreach (var (actor, cast) in casts) _engine.Dispatch(c => c.OnCastStarted(actor, cast));
        }
    }

    private static bool SameStatuses(ActorStatus[] previous, ActorStatus[] current)
    {
        if (previous.Length != current.Length) return false;
        for (var i = 0; i < previous.Length; ++i)
            if (previous[i].ID != current[i].ID || previous[i].SourceID != current[i].SourceID || previous[i].Extra != current[i].Extra) return false;
        return true;
    }

    private Actor? Find(ulong id)
    {
        if (_world.Actors.Find((uint)id) is { } actor) return actor;
        // Scan never registers these ids, so rescanning cannot resolve them.
        if ((uint)id is 0 or 0xE0000000) return null;
        Scan();
        return _world.Actors.Find((uint)id);
    }
    private void OnEvent(LogSystemEvent e)
    {
        if (_suspended || _events == null) return;
        _world.CurrentTime = DateTime.UtcNow;
        _dirty = true;
        _events.Handle(e);
    }
}

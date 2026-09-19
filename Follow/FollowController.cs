using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.GreenMoveSystem;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Follow;

public sealed class FollowController : IDisposable
{
    private const float ReissueDistanceSq = 0.5f * 0.5f;
    private const float TeleportDistanceSq = 10f * 10f;
    private static readonly TimeSpan DebugLogInterval = TimeSpan.FromSeconds(2);

    private readonly IGreenMoveSystem _greenMove;
    private readonly Func<DateTime> _utcNow;
    private bool _hasControl;
    private Vector3 _lastIssuedTarget;
    private float _lastIssuedDistance;
    private Vector3 _lastMasterPos;
    private bool _hasLastMasterPos;
    private DateTime _nextDebugLogUtc = DateTime.MinValue;
    private bool _disposed;
    private SafeZoneManager? _goalManager;
    private SafeZoneAreaHandle _followGoal;
    private uint _goalTarget;
    private float _goalDistance;
    private bool _followGoalActive;

    public FollowController() : this(PromeRotation.Plugin.Instance.GreenMoveSystem)
    {
    }

    internal FollowController(IGreenMoveSystem greenMove, Func<DateTime>? utcNow = null)
    {
        _greenMove = greenMove;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public void Update()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            UpdateInternal();
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] FollowController.Update 失败");
        }
    }

    private void UpdateInternal()
    {
        var settings = MasterPlayerSettings.Instance;
        var player = Svc.Objects.LocalPlayer;

        if (!settings.EnableFollow || player == null || player.IsDead)
        {
            ReleaseControl();
            _hasLastMasterPos = false;
            return;
        }

        var inCombat = PromeRotation.Core.GameData.IsInCombat();
        if (inCombat ? !settings.FollowDuringCombat : !settings.FollowOutOfCombat)
        {
            ReleaseControl();
            return;
        }

        var master = ResolveMaster(settings);
        if (master == null || master.EntityId == player.EntityId)
        {
            ReleaseControl();
            return;
        }

        var masterPos = master.Position;
        var teleported = _hasLastMasterPos
            && Vector3.DistanceSquared(masterPos, _lastMasterPos) > TeleportDistanceSq;
        _lastMasterPos = masterPos;
        _hasLastMasterPos = true;

        var followDistance = float.IsFinite(settings.FollowDistance) ? Math.Clamp(settings.FollowDistance, 0.2f, 10f) : 1.5f;
        if (master.ObjectKind == ObjectKind.BattleNpc)
        {
            // Leave room for GreenMove's arrival tolerance instead of stopping at the skill's exact range limit.
            var attackRange = settings.CombatDistance == CombatDistanceMode.Ranged
                ? (float.IsFinite(settings.RangedAttackRange) ? Math.Clamp(settings.RangedAttackRange, 5f, 25f) : 25f)
                : 3f;
            followDistance = attackRange - 0.5f + player.HitboxRadius + master.HitboxRadius;
        }

        // A settled dodge route releases HasControl while the danger is still present.
        // With auto-dodge enabled, following is a goal for that same planner, never a raw return route.
        if (settings.EnableAutoDodge)
        {
            ReleaseDirectControl();
            UpdatePlannedFollow(master.EntityId, masterPos, followDistance, player);
            return;
        }
        ClearFollowGoal();

        // Direct following is retained when auto-dodge is explicitly disabled.
        if (PromeRotation.Plugin.Instance.SafeZoneMovementStatus.HasControl ||
            PromeRotation.Plugin.Instance.SafeZoneRebornMovementStatus.HasControl)
        {
            _hasControl = false;
            return;
        }
        var dx = masterPos.X - player.Position.X;
        var dz = masterPos.Z - player.Position.Z;
        var distanceSq = dx * dx + dz * dz;

        if (distanceSq > followDistance * followDistance)
        {
            if (!_hasControl || !_greenMove.IsRunning || teleported
                || followDistance != _lastIssuedDistance
                || Vector3.DistanceSquared(masterPos, _lastIssuedTarget) > ReissueDistanceSq)
            {
                _greenMove.MoveTo(new GreenMovePoint(
                    masterPos,
                    Tolerance: followDistance,
                    IgnoreY: true));
                _hasControl = true;
                _lastIssuedTarget = masterPos;
                _lastIssuedDistance = followDistance;
                DebugLog($"[MasterPlayer] 跟随下发 MoveTo master={master.Name} pos={masterPos:F1}");
            }
        }
        else if (_hasControl)
        {
            _greenMove.Stop();
            _hasControl = false;
            DebugLog("[MasterPlayer] 跟随到位，Stop");
        }
    }

    private void UpdatePlannedFollow(uint targetId, Vector3 targetPosition, float distance, IBattleChara player)
    {
        var plugin = PromeRotation.Plugin.Instance;
        if (!plugin.SafeZoneRebornSetting.EnableSafeZone || !plugin.SafeZoneRebornSetting.EnableAutoMovement)
        {
            ClearFollowGoal();
            return;
        }
        var manager = plugin.SafeZoneRebornManager;
        if (_goalManager != null && !ReferenceEquals(_goalManager, manager)) ClearFollowGoal();
        _goalManager = manager;
        var now = _utcNow();
        var snapshot = manager.BuildSnapshot(now);
        // Mechanic destinations (bait, knockback, number tiles) take precedence over attack uptime.
        if ((player.IsCasting && !OptionalGoalSafety.NeedsEscape(snapshot, player.Position, now))
            || snapshot.GoalAreas.Any(g => g.Handle != _followGoal && g.GoalWindows.Any(w => w.IsActiveAt(now))))
        {
            ClearFollowGoal();
            return;
        }
        var template = new DangerAwareFollowGoalTemplate(distance, snapshot.DangerAreas
            .Where(a => a.DangerWindows.Any(w => OptionalGoalSafety.Pending(w.DangerFrom, w.DangerUntil, now)))
            .Select(a => a.Geometry).Concat(snapshot.HardObstacles.Select(a => a.Geometry)), snapshot.ArenaBoundary);
        var active = template.HasSafeArea(targetPosition);
        GoalWindow[] windows = active ? [new GoalWindow(now, null, 0, PositionPreference.Nearest)] : [];
        if (_followGoal.IsValid && manager.ContainsArea(_followGoal) && _goalTarget == targetId && _goalDistance == distance)
        {
            // Like FollowEntityPosition, changing resolved geometry must not cancel every in-flight solve.
            // An empty shape still attracts the host's goal heuristic: deactivate its window as well.
            manager.UpdateArea(_followGoal, template: template, goalWindows: windows, incrementVersion: active != _followGoalActive);
            _followGoalActive = active;
            return;
        }
        ClearFollowGoal();
        _goalManager = manager;
        _goalTarget = targetId;
        _goalDistance = distance;
        _followGoalActive = active;
        _followGoal = manager.AddGoalArea(template,
            new FollowEntityPosition(targetId, Vector2.Zero, 0, MissingEntityPolicy.RemoveArea),
            windows);
    }

    private void ClearFollowGoal()
    {
        if (_followGoal.IsValid) _goalManager?.RemoveArea(_followGoal);
        _followGoal = default;
        _goalManager = null;
    }

    private static IBattleChara? ResolveMaster(MasterPlayerSettings settings)
    {
        var slot = settings.FollowSlot;
        if (slot is >= 1 and <= 7)
        {
            var party = PromeRotation.Helpers.PartyHelper.GetParty();
            if (slot < party.Count && party[slot] is { IsDead: false } member)
            {
                return member;
            }
        }
        else if (slot != 0)
        {
            return null;
        }

        if (!settings.FollowTargetWhenNoSlot)
        {
            return null;
        }

        if (Svc.Targets.Target is IBattleChara target
            && target.ObjectKind is ObjectKind.Pc or ObjectKind.BattleNpc
            && target.IsTargetable
            && !target.IsDead)
        {
            return target;
        }

        return null;
    }

    private void ReleaseControl()
    {
        ClearFollowGoal();
        ReleaseDirectControl();
    }

    private void ReleaseDirectControl()
    {
        if (!_hasControl)
        {
            return;
        }
        _hasControl = false;

        try
        {
            var plugin = PromeRotation.Plugin.Instance;
            if (!plugin.SafeZoneMovementStatus.HasControl && !plugin.SafeZoneRebornMovementStatus.HasControl)
                _greenMove.Stop();
        }
        catch (Exception ex)
        {
            SafeLogError(ex, "[MasterPlayer] 跟随松手 Stop 失败");
        }
    }

    private void DebugLog(string message)
    {
        if (!MasterPlayerSettings.Instance.DebugMode)
        {
            return;
        }

        var now = _utcNow();
        if (now < _nextDebugLogUtc)
        {
            return;
        }
        _nextDebugLogUtc = now + DebugLogInterval;

        try
        {
            Svc.Log.Debug(message);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        ReleaseControl();
    }

    private static void SafeLogError(Exception ex, string message)
    {
        try
        {
            Svc.Log.Error(ex, message);
        }
        catch
        {
        }
    }
}

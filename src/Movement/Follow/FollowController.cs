using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Follow;

public sealed class FollowController : IDisposable
{
    private readonly Func<DateTime> _utcNow;
    private bool _disposed;
    private SafeZoneManager? _goalManager;
    private SafeZoneAreaHandle _followGoal;
    private uint _goalTarget;
    private float _goalDistance;
    private bool _followGoalActive;

    public FollowController() : this(null)
    {
    }

    internal FollowController(Func<DateTime>? utcNow)
    {
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

        if (player == null || player.IsDead || MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true)
        {
            ClearFollowGoal();
            return;
        }

        var inCombat = PromeRotation.Core.GameData.IsInCombat();
        if (inCombat ? !settings.FollowDuringCombat : !settings.FollowOutOfCombat)
        {
            ClearFollowGoal();
            return;
        }

        var master = ResolveMaster(settings);
        if (master == null || master.EntityId == player.EntityId)
        {
            ClearFollowGoal();
            return;
        }

        var masterPos = master.Position;

        var followDistance = float.IsFinite(settings.FollowDistance) ? Math.Clamp(settings.FollowDistance, 0.2f, 10f) : 1.5f;
        if (master.ObjectKind == ObjectKind.BattleNpc)
        {
            // Leave room for GreenMove's arrival tolerance instead of stopping at the skill's exact range limit.
            var attackRange = settings.CombatDistance == CombatDistanceMode.Ranged
                ? (float.IsFinite(settings.RangedAttackRange) ? Math.Clamp(settings.RangedAttackRange, 5f, 25f) : 25f)
                : 3f;
            followDistance = attackRange - 0.5f + player.HitboxRadius + master.HitboxRadius;
        }

        UpdatePlannedFollow(master.EntityId, masterPos, followDistance, player);
    }

    private void UpdatePlannedFollow(uint targetId, Vector3 targetPosition, float distance, IBattleChara player)
    {
        if (!MasterPlayerPlugin.Instance!.SafeZone.Setting.EnableSafeZone)
        {
            ClearFollowGoal();
            return;
        }
        var manager = MasterPlayerPlugin.Instance!.SafeZone.Manager;
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
        GoalWindow[] windows = active ? [new GoalWindow(now, null, -1, PositionPreference.Nearest)] : [];
        if (_followGoal.IsValid && manager.ContainsArea(_followGoal) && _goalTarget == targetId && _goalDistance == distance)
        {
            // Like FollowEntityPosition, changing resolved geometry must not cancel every in-flight solve.
            // An empty shape still attracts the goal heuristic: deactivate its window as well.
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        ClearFollowGoal();
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

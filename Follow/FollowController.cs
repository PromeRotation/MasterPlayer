using System;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.GreenMoveSystem;

namespace MasterPlayer.Follow;

public sealed class FollowController : IDisposable
{
    private const float ReissueDistanceSq = 0.5f * 0.5f;
    private const float TeleportDistanceSq = 10f * 10f;
    private static readonly TimeSpan DebugLogInterval = TimeSpan.FromSeconds(2);

    private readonly IGreenMoveSystem _greenMove;
    private bool _hasControl;
    private Vector3 _lastIssuedTarget;
    private float _lastIssuedDistance;
    private Vector3 _lastMasterPos;
    private bool _hasLastMasterPos;
    private DateTime _nextDebugLogUtc = DateTime.MinValue;
    private bool _disposed;

    public FollowController() : this(PromeRotation.Plugin.Instance.GreenMoveSystem)
    {
    }

    internal FollowController(IGreenMoveSystem greenMove)
    {
        _greenMove = greenMove;
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

        // 躲避优先：安全区自动移动接管中则让位，不发跟随指令，也不 Stop 它的路线
        if (PromeRotation.Plugin.Instance.SafeZoneMovementStatus.HasControl ||
            PromeRotation.Plugin.Instance.SafeZoneRebornMovementStatus.HasControl)
        {
            _hasControl = false;
            return;
        }

        var followDistance = MathF.Max(0.2f, settings.FollowDistance);
        if (master.ObjectKind == ObjectKind.BattleNpc)
        {
            followDistance += player.HitboxRadius + master.HitboxRadius;
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

        var now = DateTime.UtcNow;
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

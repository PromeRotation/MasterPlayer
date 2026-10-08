using System.Numerics;
using System.Reflection;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using MasterPlayer.Spatial;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Mechanics.Blunderville;

internal readonly record struct BlundervilleNativeState(bool Grounded, bool Jumping, bool GapAhead, float? ArrowDirection);

// Only the final calls touch native game state; the race planner supplies all decisions.
internal sealed unsafe class BlundervilleNativeControl : IDisposable
{
    private delegate void FacingRequest(string owner, ulong target, ulong clearTarget, ulong[] forbiddenTargets,
        float? heading, bool holdActions, bool blockCasts, bool cancelCast);

    private const uint Territory = 1165;
    private const uint JumpProhibitedStatus = 3703;
    private readonly string _owner = "MasterPlayer/Blunderville/" + Guid.NewGuid().ToString("N");
    private nint _arrowAddress;
    private bool _arrowResolved, _facingResolved, _facingActive, _collisionWarned, _disposed, _movementCapabilitiesResolved;
    private PropertyInfo? _misdirectionCapability;
    private FacingRequest? _hostFacing;
    private Action<string>? _hostClear;
    private DateTime _nextJump, _nextInteraction, _stillSince, _lastSample;
    private Vector3? _lastPosition;

    public DateTime LastInteractionAt { get; private set; }
    public ulong LastInteractionId { get; private set; }

    public bool SupportsMisdirection
    {
        get
        {
            if (_disposed) return false;
            try
            {
                var movement = PromeRotation.Plugin.Instance?.GreenMoveSystem;
                if (movement == null) return false;
                if (!_movementCapabilitiesResolved)
                {
                    _movementCapabilitiesResolved = true;
                    var property = movement.GetType().GetProperty("SupportsMisdirection", BindingFlags.Public | BindingFlags.Instance);
                    if (property is { PropertyType: not null, GetMethod.IsPublic: true }
                        && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                        _misdirectionCapability = property;
                }
                // Metadata is stable, but hook availability can change during host disposal.
                return _misdirectionCapability?.GetValue(movement) is true;
            }
            catch { return false; }
        }
    }

    public BlundervilleNativeState Capture(DateTime now, Vector3 desiredDirection)
    {
        if (_disposed || LivePlayer() is not { } player)
        {
            _lastPosition = null;
            _stillSince = _lastSample = default;
            return default;
        }

        if (_lastPosition is not { } last || now < _lastSample || Vector3.DistanceSquared(last, player.Position) > 0.0025f)
            _stillSince = now;
        _lastPosition = player.Position;
        _lastSample = now;

        var jumping = Svc.Condition[ConditionFlag.Jumping61] || Svc.Condition[ConditionFlag.Jumping];
        var grounded = !jumping && HasFloor(player.Position, 0.35f, 0.35f);
        return new(grounded, jumping, false, ArrowDirection(player));
    }

    public float? ReadArrowDirection() => !_disposed && LivePlayer() is { } player ? ArrowDirection(player) : null;

    public bool Apply(DateTime now, bool authorized, bool jump, float? facing, ulong? interactId)
    {
        if (_disposed || !authorized || HostMovement.HasControl || HostMovement.AutomaticMovementEnabled || LivePlayer() is not { } player)
        {
            ClearFacing();
            return false;
        }

        var launched = false;
        ApplyFacing(player, facing);
        var jumping = Svc.Condition[ConditionFlag.Jumping61] || Svc.Condition[ConditionFlag.Jumping];
        if (jump && !jumping && now >= _nextJump && !player.IsCasting
            && !player.StatusList.Any(s => s.StatusId == JumpProhibitedStatus)
            && HasFloor(player.Position, 0.35f, 0.35f))
        {
            var manager = ActionManager.Instance();
            if (manager != null && manager->GetActionStatus(ActionType.GeneralAction, 2) == 0)
            {
                _nextJump = now.AddSeconds(0.7);
                launched = manager->UseAction(ActionType.GeneralAction, 2);
            }
        }

        if (interactId is { } id && id is not (0 or 0xE0000000) && !jumping && !jump
            && !player.IsCasting && now >= _nextInteraction && _stillSince != default
            && now >= _stillSince.AddSeconds(0.2) && now >= _lastSample && now - _lastSample <= TimeSpan.FromSeconds(0.2)
            && !PromeRotation.Plugin.Instance.GreenMoveSystem.IsRunning)
            Interact(player, id, now);
        return launched;
    }

    public void Dispose()
    {
        if (_disposed) return;
        ClearFacing();
        _disposed = true;
        _arrowAddress = 0;
        _lastPosition = null;
    }

    private static IPlayerCharacter? LivePlayer()
    {
        if (Svc.ClientState.TerritoryType != Territory || MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true
            || Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51]
            || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Svc.Condition[ConditionFlag.WatchingCutscene]
            || Svc.Condition[ConditionFlag.WatchingCutscene78]) return null;
        return Svc.Objects.LocalPlayer is { IsDead: false, Address: not 0 } player ? player : null;
    }

    private bool HasFloor(Vector3 position, float rise, float drop)
    {
        if (!IsFinite(position)) return false;
        try
        {
            var origin = position + Vector3.UnitY * (rise + 0.1f);
            if (!BGCollisionModule.RaycastMaterialFilter(origin, -Vector3.UnitY, out var hit, rise + drop + 0.2f)
                || !IsFinite(hit.Point) || !IsFinite(hit.Normal)) return false;
            // Some primitive colliders leave Normal empty; the downward material-filtered hit still identifies their top.
            if (hit.Normal.LengthSquared() > 0.0001f && hit.Normal.Y < 0.45f) return false;
            var height = hit.Point.Y - position.Y;
            return height >= -drop && height <= rise;
        }
        catch (Exception ex)
        {
            if (!_collisionWarned)
            {
                _collisionWarned = true;
                Svc.Log.Warning($"[MasterPlayer 糖豆人] 落脚碰撞检测不可用：{ex.Message}");
            }
            return false;
        }
    }

    private float? ArrowDirection(IPlayerCharacter player)
    {
        if (!player.StatusList.Any(s => s.StatusId is 3693 or 3694)) return null;
        if (!_arrowResolved)
        {
            _arrowResolved = true;
            // BMR MovementOverride.ForcedMovementDirection; the value is a world heading in radians.
            if (!Svc.SigScanner.TryGetStaticAddressFromSig("F3 0F 11 0D ?? ?? ?? ?? 48 85 DB", out _arrowAddress))
                Svc.Log.Warning("[MasterPlayer 糖豆人] 未找到旋转箭头方向，自动竞速将在箭头阶段等待；绘制继续工作。");
        }
        if (_arrowAddress == 0) return null;
        var value = *(float*)_arrowAddress;
        return float.IsFinite(value) ? MathF.IEEERemainder(value, MathF.Tau) : null;
    }

    private void ApplyFacing(IPlayerCharacter player, float? facing)
    {
        if (facing is not { } heading || !float.IsFinite(heading))
        {
            ClearFacing();
            return;
        }
        ResolveFacing();
        heading = MathF.IEEERemainder(heading, MathF.Tau);
        if (_hostFacing != null)
        {
            // PR requests expire after 500 ms, so renew while the planner keeps requesting this heading.
            _hostFacing(_owner, 0, 0, [], heading, false, false, false);
            _facingActive = true;
        }
        else if (player.StatusList.Any(s => s.StatusId is 3693 or 3694 or >= 3698 and <= 3701))
        {
            ((NativeGameObject*)player.Address)->SetRotation(heading);
        }
    }

    private void ResolveFacing()
    {
        if (_facingResolved) return;
        _facingResolved = true;
        var type = typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl");
        MethodInfo? Find(string name, Type[] parameters) => type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == name && m.ReturnType == typeof(void) && !m.ContainsGenericParameters
                && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
        var update = Find("Update", [typeof(string), typeof(ulong), typeof(ulong), typeof(ulong[]), typeof(float?), typeof(bool), typeof(bool), typeof(bool)]);
        var clear = Find("Clear", [typeof(string)]);
        if (update != null && clear != null)
        {
            _hostFacing = update.CreateDelegate<FacingRequest>();
            _hostClear = clear.CreateDelegate<Action<string>>();
        }
    }

    private void ClearFacing()
    {
        if (!_facingActive) return;
        _hostClear?.Invoke(_owner);
        _facingActive = false;
    }

    private void Interact(IPlayerCharacter player, ulong id, DateTime now)
    {
        var target = Svc.Objects.SearchById(id);
        if (target == null && id <= uint.MaxValue) target = Svc.Objects.SearchByEntityId((uint)id);
        if (target is not { IsTargetable: true, Address: not 0, ObjectKind: ObjectKind.EventObj }
            || target.EntityId != id && target.GameObjectId != id || target.IsDead) return;
        var system = TargetSystem.Instance();
        var events = EventFramework.Instance();
        if (system == null || events == null
            || !events->CheckInteractRange((NativeGameObject*)player.Address, (NativeGameObject*)target.Address, 1, false)) return;
        _nextInteraction = now.AddSeconds(1.1);
        system->InteractWithObject((NativeGameObject*)target.Address, true);
        LastInteractionAt = now;
        LastInteractionId = id;
    }

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

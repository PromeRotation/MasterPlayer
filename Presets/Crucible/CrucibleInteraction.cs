using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Mechanics.Crucible;

// Keep interaction policy testable; only the final call touches native objects.
internal sealed class CrucibleInteraction
{
    private ulong _target;
    private DateTime _nextAttempt;
    private WPos _lastPosition;
    private DateTime _stillSince;
    public bool Attempted { get; private set; }
    public void Clear() { _target = 0; _nextAttempt = _stillSince = default; Attempted = false; }
    public bool Update(Actor? target, Actor player, DateTime now, bool moving, bool casting, Func<ulong, bool> interact)
    {
        if (target is not { OID: 2015459, IsTargetable: true, IsDeadOrDestroyed: false } || target.EventState == 7)
        { Clear(); return false; }
        if (_target != target.InstanceID) { Clear(); _target = target.InstanceID; }
        if (_stillSince == default || (_lastPosition - player.Position).LengthSq() > 0.0025f) _stillSince = now;
        _lastPosition = player.Position;
        if (!player.Position.InCircle(target.Position, 2) || moving || casting || now < _nextAttempt || now < _stillSince.AddSeconds(0.2)) return false;
        _nextAttempt = now.AddSeconds(1.1);
        if (!interact(_target)) return false;
        Attempted = true;
        return true;
    }
    public static unsafe bool Interact(ulong id)
    {
        var player = Svc.Objects.LocalPlayer;
        var target = Svc.Objects.SearchByEntityId((uint)id);
        if (player == null || player.IsDead || player.Address == 0 || target == null || target.EntityId != id
            || target.BaseId != 2015459 || !target.IsTargetable || target.Address == 0) return false;
        var targetSystem = TargetSystem.Instance();
        var events = EventFramework.Instance();
        if (targetSystem == null || events == null || !events->CheckInteractRange((NativeGameObject*)player.Address, (NativeGameObject*)target.Address, 1, false)) return false;
        targetSystem->InteractWithObject((NativeGameObject*)target.Address, false);
        return true;
    }
}

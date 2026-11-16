using ECommons.DalamudServices;
using Dalamud.Game.ClientState.Objects.Types;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Updaters;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed class EnuoActions
{
    private readonly Dictionary<uint, DateTime> _attempted = [];
    public void Clear() => _attempted.Clear();
    public void Apply(EnuoPlan plan, DateTime now, ReplayActorState? me, bool replay)
    {
        if (replay || plan.HoldActions || me is not { Dead: false, Cast: null }) return;
        foreach (var request in plan.Actions)
        {
            if (now >= request.Until || _attempted.TryGetValue(request.Id, out var last) && now < last.AddSeconds(1)) continue;
            if (request.Id == 7568 && plan.BlockCasts) continue;
            var target = Svc.Objects.OfType<IBattleChara>().FirstOrDefault(a => a.EntityId == request.Target);
            if (target == null || target.IsDead || !target.IsTargetable) continue;
            if (request.Id == 7538 && (!target.IsCasting || target.CastActionId != 50018 || !target.IsCastInterruptible
                || System.Numerics.Vector3.Distance(me.Position, target.Position) > 3 + target.HitboxRadius)) continue;
            if (request.Id == 7568 && !me.Statuses.Any(s => s.Id == 3943)) continue;
            if (!ActionHelper.CanCast(request.Id, target) || ActionHelper.GetAnimationLock() > 0) continue;
            if (request.Id == 7568 && ActionHelper.GetGcdRemain() > 0.1f) continue;
            _attempted[request.Id] = now;
            // Synchronous dispatch: disabling the preset cannot leave delayed actions in PR's global queue.
            ActionUpdaterRouter.UseAction(new PAction(request.Id, request.Id == 7568 ? ActionType.Gcd : ActionType.OffGcd, ActionTargetType.Self)
                { NetworkTid = target.EntityId });
            break;
        }
    }
}

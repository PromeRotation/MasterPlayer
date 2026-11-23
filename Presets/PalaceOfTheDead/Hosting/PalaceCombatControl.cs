using System.Reflection;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed class PalaceCombatControl
{
    private delegate void UpdateRequest(string owner, ulong target, ulong clearTarget, ulong[] forbiddenTargets,
        float? heading, bool holdActions, bool blockCasts, bool cancelCast);

    private readonly string owner = "MasterPlayer/Palace/" + Guid.NewGuid().ToString("N");
    private UpdateRequest? update;
    private Action<string>? clear;
    private bool resolved;
    private bool warned;

    internal void Apply(ulong target, ulong[] forbidden, float? heading, bool hold, bool blockCasts = false, bool cancelCast = false,
        IReadOnlyList<ulong>? forbiddenAutoAttackTargets = null)
    {
        StopForbiddenAutoAttacks(forbidden);
        if (forbiddenAutoAttackTargets != null) StopForbiddenAutoAttacks(forbiddenAutoAttackTargets);
        Resolve();
        if (update == null)
        {
            if (!warned && (target != 0 || forbidden.Length != 0 || heading != null || hold))
            {
                warned = true;
                Svc.Log.Warning("[PromeRadar] 当前 PR 缺少 MechanicControl；死宫停手、禁攻与朝向请求不可用。请升级宿主。");
            }
            return;
        }
        // Requests expire after 500ms in PR; refresh even when their value is unchanged.
        update(owner, target, 0, forbidden, heading, hold, blockCasts, cancelCast);
    }

    internal void Clear()
    {
        if (resolved) clear?.Invoke(owner);
    }

    private static unsafe void StopForbiddenAutoAttacks(IReadOnlyList<ulong> forbidden)
    {
        if (Svc.Targets.Target is not { } target || !forbidden.Contains(ReplayWorldCapture.Id(target))) return;
        var ui = UIState.Instance();
        if (ui != null && ui->WeaponState.AutoAttackState.Get()) ui->WeaponState.AutoAttackState.Set(false);
    }

    private void Resolve()
    {
        if (resolved) return;
        resolved = true;
        var type = typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl");
        var signature = new[] { typeof(string), typeof(ulong), typeof(ulong), typeof(ulong[]), typeof(float?), typeof(bool), typeof(bool), typeof(bool) };
        var method = type?.GetMethod("Update", BindingFlags.Public | BindingFlags.Static, signature);
        var clearMethod = type?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        if (method != null && clearMethod != null)
        {
            update = method.CreateDelegate<UpdateRequest>();
            clear = clearMethod.CreateDelegate<Action<string>>();
        }
    }
}

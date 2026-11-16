using System.Reflection;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed class EnuoHostControl(Action<string> log, Type? type = null)
{
    private delegate void UpdateRequest(string owner, ulong target, ulong clearTarget, ulong[] forbidden,
        float? heading, bool holdActions, bool blockCasts, bool cancelCast);
    private readonly string _owner = "MasterPlayer/Enuo/" + Guid.NewGuid().ToString("N");
    private UpdateRequest? _update;
    private Action<string>? _clear;
    private bool _resolved, _warned;

    public void Apply(EnuoPlan plan)
    {
        if (!_resolved)
        {
            _resolved = true;
            var host = type ?? typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl");
            var update = host?.GetMethod("Update", BindingFlags.Public | BindingFlags.Static, null,
                [typeof(string), typeof(ulong), typeof(ulong), typeof(ulong[]), typeof(float?), typeof(bool), typeof(bool), typeof(bool)], null);
            var clear = host?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static, null, [typeof(string)], null);
            if (update?.ReturnType == typeof(void) && clear?.ReturnType == typeof(void))
            {
                _update = update.CreateDelegate<UpdateRequest>(); _clear = clear.CreateDelegate<Action<string>>();
            }
        }
        if (_update != null)
            _update(_owner, plan.Target, 0, plan.ForbiddenTargets, plan.Facing, plan.HoldActions, plan.BlockCasts, plan.BlockCasts);
        else if (!_warned && (plan.Target != 0 || plan.Facing != null || plan.BlockCasts || plan.ForbiddenTargets.Length != 0))
        {
            _warned = true;
            log("恩欧：当前 PR 缺少 MechanicControl 接口，自动转火、禁打与背对暂停不可用；请更新配套 PR。");
        }
    }
    public void Clear() => _clear?.Invoke(_owner);
}

using System.Reflection;

namespace MasterPlayer.Mechanics.Crucible;

// Optional host capability: existing PR versions still compile and run movement/drawing.
internal sealed class CrucibleHostControl(Action<string> log, Type? hostType = null)
{
    private delegate void UpdateRequest(string owner, ulong target, ulong clearTarget, ulong[] forbiddenTargets,
        float? heading, bool holdActions, bool blockCasts, bool cancelCast);
    private readonly string _owner = "MasterPlayer/Crucible/" + Guid.NewGuid().ToString("N");
    private readonly Type? _type = hostType ?? typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl");
    private UpdateRequest? _update;
    private Action<string>? _clear;
    private bool _resolved, _warned;
    public void Apply(CrucibleControlPlan plan)
    {
        if (!_resolved)
        {
            _resolved = true;
            MethodInfo? Find(string name, Type[] parameters) => _type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == name && m.ReturnType == typeof(void) && !m.ContainsGenericParameters
                    && m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
            var update = Find("Update", [typeof(string), typeof(ulong), typeof(ulong), typeof(ulong[]), typeof(float?), typeof(bool), typeof(bool), typeof(bool)]);
            var clear = Find("Clear", [typeof(string)]);
            if (update != null && clear != null)
            {
                _update = update.CreateDelegate<UpdateRequest>();
                _clear = clear.CreateDelegate<Action<string>>();
            }
        }
        if (_update != null && _clear != null)
            _update(_owner, plan.Target, plan.ClearTarget, plan.ForbiddenTargets, plan.Heading?.Rad,
                plan.HoldActions, plan.BlockCasts, plan.CancelCast);
        else if (!_warned && (plan.Target != 0 || plan.ClearTarget != 0 || plan.Heading != null || plan.BlockCasts || plan.HoldActions || plan.ForbiddenTargets.Length != 0))
        {
            _warned = true;
            log("当前 PR 本体未提供机制控制接口：自动转火、背对和停止读条不可用；请更新配套本体。机制绘制与走位继续工作。");
        }
    }
    public void Clear() => _clear?.Invoke(_owner);
}

using System.Reflection;
using ECommons.DalamudServices;

namespace MasterPlayer.MechanicHost;

// 经宿主可选 PromeRotation.Hosting.MechanicControl 接口请求朝向控制（反射解析，接口缺失一次性警告并跳过）。
// 机制导航桥与脚本 Hints 门面共用；每实例独立 owner，Clear 撤销自身请求。
internal sealed class MechanicFacingControl : IDisposable
{
    private delegate void UpdateRequest(string owner, ulong target, ulong clearTarget, ulong[] forbiddenTargets,
        float? heading, bool holdActions, bool blockCasts, bool cancelCast);

    private readonly string _owner;
    private readonly string _warnText;
    private UpdateRequest? _update;
    private Action<string>? _clear;
    private bool _resolved;
    private float? _appliedHeading;
    private bool _warned;

    public MechanicFacingControl(string ownerScope = "MechanicNav",
        string warnText = "[MasterPlayer] 当前 PR 本体未提供机制控制接口：机制背对引导不可用；走位与绘制继续工作。")
    {
        _owner = $"MasterPlayer/{ownerScope}/" + Guid.NewGuid().ToString("N");
        _warnText = warnText;
    }

    public void Apply(float? heading)
    {
        Resolve();
        if (_update == null)
        {
            if (!_warned && heading != null)
            {
                _warned = true;
                Svc.Log.Warning(_warnText);
            }
            return;
        }
        if (heading == _appliedHeading)
            return;
        if (heading == null)
        {
            _clear?.Invoke(_owner);
        }
        else
        {
            _update(_owner, 0, 0, [], heading, false, false, false);
        }
        _appliedHeading = heading;
    }

    public void Clear() => Apply(null);

    public void Dispose() => Clear();

    private void Resolve()
    {
        if (_resolved)
            return;
        _resolved = true;
        var type = typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl");
        MethodInfo? Find(string name, Type[] parameters) => type?.GetMethods(BindingFlags.Public | BindingFlags.Static)
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
}

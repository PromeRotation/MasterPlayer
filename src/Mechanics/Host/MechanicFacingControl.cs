using System.Reflection;
using ECommons.DalamudServices;

namespace MasterPlayer.MechanicHost;

// 经宿主可选 PromeRotation.Hosting.MechanicControl 接口请求朝向、选敌和动作限制（反射解析，接口缺失一次性警告）。
// 机制导航桥与脚本 Hints 门面共用；每实例独立 owner，Clear 撤销自身请求。
// PR 请求租约为 500ms，调用方逐帧发布时必须续期，即使内容没有变化。
internal sealed class MechanicFacingControl : IDisposable
{
    private static readonly Type? HostControl = typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl");
    private static readonly Func<bool>? ReadHoldActions = HostControl?.GetMethod("get_HoldActions", Type.EmptyTypes)?.CreateDelegate<Func<bool>>();
    private static readonly Func<bool>? ReadBlockCasts = HostControl?.GetMethod("get_BlockCasts", Type.EmptyTypes)?.CreateDelegate<Func<bool>>();
    private static readonly Func<ulong, bool>? CheckTarget = HostControl?.GetMethod("AllowsTarget", [typeof(ulong)])?.CreateDelegate<Func<ulong, bool>>();

    // 发布SDK不含这个可选类型；执行前读取运行宿主的现有约束，避免直接引用。
    internal static bool HoldActions => ReadHoldActions?.Invoke() ?? false;
    internal static bool BlockCasts => ReadBlockCasts?.Invoke() ?? false;
    internal static bool AllowsTarget(ulong target) => CheckTarget?.Invoke(target) ?? true;

    internal delegate void UpdateRequest(string owner, ulong target, ulong clearTarget, ulong[] forbiddenTargets,
        float? heading, bool holdActions, bool blockCasts, bool cancelCast);

    private readonly string _owner;
    private readonly string _warnText;
    private UpdateRequest? _update;
    private Action<string>? _clear;
    private bool _resolved;
    private bool _hasRequest;
    private bool _warned;

    public MechanicFacingControl(string ownerScope = "MechanicNav",
        string warnText = "[MasterPlayer] 当前 PR 本体未提供机制控制接口：机制背对引导不可用；走位与绘制继续工作。")
    {
        _owner = $"MasterPlayer/{ownerScope}/" + Guid.NewGuid().ToString("N");
        _warnText = warnText;
    }

    internal MechanicFacingControl(UpdateRequest update, Action<string> clear) : this("ScriptHints")
    {
        _update = update;
        _clear = clear;
        _resolved = true;
    }

    public void Apply(float? heading, bool holdActions = false, bool blockCasts = false,
        bool cancelCast = false, ulong[]? forbiddenTargets = null, ulong target = 0)
    {
        if (PlayerPerspective.IsReplay)
        {
            Clear();
            return;
        }
        if (heading is { } angle && !float.IsFinite(angle))
            heading = null;
        if (heading == null && !holdActions && !blockCasts && !cancelCast
            && forbiddenTargets is not { Length: > 0 } && target == 0)
        {
            Clear();
            return;
        }
        Resolve();
        if (_update == null)
        {
            if (!_warned)
            {
                _warned = true;
                Svc.Log.Warning(_warnText);
            }
            return;
        }
        _hasRequest = true;
        _update(_owner, target, 0, forbiddenTargets ?? [], heading, holdActions, blockCasts, cancelCast);
    }

    public void Clear()
    {
        if (!_hasRequest)
            return;
        _clear?.Invoke(_owner);
        _hasRequest = false;
    }

    public void Dispose() => Clear();

    private void Resolve()
    {
        if (_resolved)
            return;
        _resolved = true;
        MethodInfo? Find(string name, Type[] parameters) => HostControl?.GetMethods(BindingFlags.Public | BindingFlags.Static)
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

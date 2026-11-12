using System.Reflection;

namespace MasterPlayer.RemoteControlTests;

public class TestIpcProxy : DispatchProxy
{
    public static Dictionary<string, Delegate> Handlers { get; } = [];
    public static List<(string Channel, object?[] Arguments)> Calls { get; } = [];
    public string Channel { get; set; } = "";

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name is "get_HasFunction" or "get_HasAction")
            return Handlers.ContainsKey(Channel);
        if (targetMethod.Name is not ("InvokeFunc" or "InvokeAction"))
            throw new NotSupportedException(targetMethod.Name);
        if (!Handlers.TryGetValue(Channel, out var handler))
            throw new InvalidOperationException("IPC provider is not registered: " + Channel);
        Calls.Add((Channel, args ?? []));
        return handler.DynamicInvoke(args);
    }
}

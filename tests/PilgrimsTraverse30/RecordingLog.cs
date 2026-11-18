using System.Reflection;

public class RecordingLog : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.ReturnType == typeof(void)) return null;
        return method?.ReturnType.IsValueType == true ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

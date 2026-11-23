using System.Reflection;
using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

internal static class SystemLogParameterRegression
{
    internal static void Run(Assembly assembly)
    {
        var original = Svc.Chat;
        var callbacks = new List<IChatGui.OnLogMessageDelegate>();
        var chat = CatalogServiceProxy.Create<IChatGui>((name, args) =>
        {
            if (name == "add_LogMessage") callbacks.Add((IChatGui.OnLogMessageDelegate)args[0]!);
            else if (name == "remove_LogMessage") callbacks.Remove((IChatGui.OnLogMessageDelegate)args[0]!);
            else throw new NotSupportedException(name);
            return null;
        });
        var type = assembly.GetType("MasterPlayer.Scripting.SystemLogParameterEvents", true)!;
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var eventType = assembly.GetType("MasterPlayer.Replay.ReplaySystemLogEvent", true)!;
        var messages = new List<object>();
        var receive = typeof(SystemLogParameterRegression).GetMethod(nameof(Receive), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType);
        var handler = receive.Invoke(null, [messages])!;
        object Call(string name, params object[] args) => type.GetMethod(name, flags)!.Invoke(null, args)!;
        IDisposable Subscribe(object owner) => (IDisposable)Call("Subscribe", owner, handler, null!);
        void Drain(bool replay = false) => Call("Drain", replay);
        var owner = new object();
        var other = new object();
        try
        {
            typeof(Svc).GetProperty(nameof(Svc.Chat))!.SetValue(null, chat);
            var values = new int?[] { 8, null, 42 };
            uint messageId = 7222;
            var message = CatalogServiceProxy.Create<ILogMessage>((name, args) => name switch
            {
                "get_LogMessageId" => messageId,
                "get_ParameterCount" => values.Length,
                "TryGetIntParameter" => Parameter(args, values),
                _ => throw new NotSupportedException(name),
            });
            using var first = Subscribe(owner);
            using var second = Subscribe(other);
            foreach (var callback in callbacks.ToArray()) callback(message);
            Check(messages.Count == 0, "System log escaped Framework queue.");
            values[0] = 16;
            Drain();
            Check(messages.Count == 2, "Independent owners lost a system log.");
            Check(messages.All(m => ((int?[])eventType.GetProperty("Arguments")!.GetValue(m)!).SequenceEqual(new int?[] { 8, null, 42 })),
                "System log retained callback-owned parameter memory or changed parameter positions.");
            messages.Clear();
            foreach (var callback in callbacks.ToArray()) callback(message);
            Call("UnsubscribeAll", owner);
            Drain();
            Check(messages.Count == 1 && callbacks.Count == 1, "Owner cleanup dispatched queued work or removed another owner.");
            messages.Clear();
            foreach (var callback in callbacks.ToArray()) callback(message);
            Drain(true);
            Check(messages.Count == 0, "Live system log entered an MP replay.");
            messageId = 7255;
            foreach (var callback in callbacks.ToArray()) callback(message);
            Drain();
            Check(messages.Count == 0, "Supplemental parameter route duplicated an existing PR system log.");
            second.Dispose();
            Check(callbacks.Count == 0, "System log handler survived disposal.");
            Console.WriteLine("PASS production system log parameters: copied integers/null slots, deferred dispatch, owner isolation, queued disposal, replay and duplicate suppression");
        }
        finally
        {
            Call("UnsubscribeAll", owner);
            Call("UnsubscribeAll", other);
            Drain(true);
            typeof(Svc).GetProperty(nameof(Svc.Chat))!.SetValue(null, original);
        }
    }

    private static Action<T> Receive<T>(List<object> messages) where T : class => message => messages.Add(message);

    private static bool Parameter(object?[] args, int?[] values)
    {
        var value = values[(int)args[0]!];
        args[1] = value ?? 0;
        return value.HasValue;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

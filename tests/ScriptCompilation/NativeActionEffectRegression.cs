using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;

internal static class NativeActionEffectRegression
{
    private delegate void CaptureEffect(ulong source, uint action, ulong mainTarget, uint sequence, ReadOnlySpan<ulong> targets, DateTime? at);

    internal static void Run(Assembly assembly, string config)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags staticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        var captureType = assembly.GetType("MasterPlayer.Scripting.NativeActionEffectCapture", true)!;
        var eventType = assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvent", true)!;
        var bus = assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvents", true)!;
        var routerType = assembly.GetType("MasterPlayer.Scripting.LiveScriptEventRouter", true)!;
        var router = routerType.GetField("Instance", staticHidden)!.GetValue(null)!;
        var received = new List<LogSystemEvent>();
        var handler = typeof(NativeActionEffectRegression).GetMethod(nameof(Handler), staticHidden)!
            .MakeGenericMethod(eventType).Invoke(null, [received])!;
        var owner = new object();
        using var subscription = (IDisposable)routerType.GetMethod("Subscribe")!
            .Invoke(router, [owner, "LogEvent", handler, eventType])!;
        var capture = Activator.CreateInstance(captureType, hidden, null, [false], null)!;
        using var lifetime = (IDisposable)capture;
        var emit = captureType.GetMethod("Capture", hidden)!.CreateDelegate<CaptureEffect>(capture);
        void Drain(bool replay = false) => captureType.GetMethod("Drain", hidden)!.Invoke(capture, [replay, null]);
        void Clear() => captureType.GetMethod("Clear", hidden)!.Invoke(capture, null);
        void Replay(bool active) => captureType.GetMethod("SetReplayMode", hidden)!.Invoke(capture, [active]);
        var now = DateTime.UtcNow;
        ulong[] targets = [101, 100];
        emit(1, 16964, 100, 5, targets, now);
        targets[0] = 999;
        Check(received.Count == 0, "Native capture published on the producer thread.");
        Drain();
        Check(received.Count == 1 && received[0].Timestamp == now
            && (ulong)eventType.GetProperty("MainTargetId")!.GetValue(received[0])! == 100
            && ((ulong[])eventType.GetProperty("Targets")!.GetValue(received[0])!).SequenceEqual([101UL, 100UL]),
            "Native capture lost the main target, order, immutable input copy or timestamp.");
        emit(1, 16964, 100, 6, [], now); Clear(); Drain();
        emit(1, 16964, 100, 7, [], now); Replay(true); Drain();
        emit(1, 16964, 100, 8, [], now); Replay(false); Drain();
        emit(1, 16964, 100, 9, [], now); Drain(true); Drain();
        Check(received.Count == 1, "Epoch/replay isolation leaked a native effect.");
        var errors = 0;
        using var failing = SubscribeThrowing(bus, eventType);
        bus.GetMethod("Publish", staticHidden)!.Invoke(null, [received[0], (Action<Exception>)(_ => ++errors)]);
        Check(errors == 1 && received.Count == 2, "A failed subscriber blocked another native subscriber.");
        bus.GetMethod("UnsubscribeAll", staticHidden)!.Invoke(null, [owner]);
        emit(1, 16964, 100, 10, [], now); Drain();
        Check(received.Count == 2, "Owner cleanup retained a native subscriber.");
        failing.Dispose();
        lifetime.Dispose();
        emit(1, 16964, 100, 11, [], now); Drain();
        Check(received.Count == 2, "Disposed capture accepted a native effect.");

        RunRecorder(assembly, config, captureType, eventType);
        Console.WriteLine("PASS native action effects: copied ordered targets/main target, queued delivery, epoch/replay/dispose isolation, owner cleanup, exception isolation and production recorder/readback");
    }

    private static IDisposable SubscribeThrowing(Type bus, Type eventType)
    {
        var handler = typeof(NativeActionEffectRegression).GetMethod(nameof(ThrowingHandler), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(eventType).Invoke(null, null)!;
        return (IDisposable)bus.GetMethod("Subscribe", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [new object(), handler, null])!;
    }

    private static Action<T> Handler<T>(List<LogSystemEvent> received) where T : LogSystemEvent => ev => received.Add(ev);
    private static Action<T> ThrowingHandler<T>() => _ => throw new InvalidOperationException("expected subscriber failure");
    private static Func<DateTime, T> World<T>(T frame) => _ => frame;

    private static void RunRecorder(Assembly assembly, string config, Type captureType, Type eventType)
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var oldClient = Svc.ClientState;
        var oldChat = Svc.Chat;
        var oldInterface = Svc.PluginInterface;
        var prProperty = typeof(PromeRotation.Plugin).GetProperty("Instance")!;
        var oldPr = prProperty.GetValue(null);
        var pluginType = assembly.GetType("MasterPlayer.MasterPlayerPlugin", true)!;
        var pluginProperty = pluginType.GetProperty("Instance")!;
        var oldPlugin = pluginProperty.GetValue(null);
        var capture = Activator.CreateInstance(captureType, hidden, null, [false], null)!;
        using var captureLifetime = (IDisposable)capture;
        var emit = captureType.GetMethod("Capture", hidden)!.CreateDelegate<CaptureEffect>(capture);
        void Set(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);
        try
        {
            Set(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) =>
                name == "get_TerritoryType" ? 956u : throw new NotSupportedException(name)));
            Set(nameof(Svc.Chat), CatalogServiceProxy.Create<IChatGui>((_, _) => null));
            var directory = Directory.CreateDirectory(Path.Combine(config, "native-effects-recorder"));
            Set(nameof(Svc.PluginInterface), CatalogServiceProxy.Create<IDalamudPluginInterface>((name, _) =>
                name == "get_ConfigDirectory" ? directory : throw new NotSupportedException(name)));
            var callbacks = new List<Delegate>();
            var source = CatalogServiceProxy.Create<ILogSystemEventSource>((name, args) =>
            {
                if (name != "Subscribe") throw new NotSupportedException(name);
                callbacks.Add((Delegate)args[0]!);
                return new MemoryStream();
            });
            var service = RuntimeHelpers.GetUninitializedObject(typeof(LogSystemService));
            typeof(LogSystemService).GetField("<Events>k__BackingField", hidden)!.SetValue(service, source);
            var pr = RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
            typeof(PromeRotation.Plugin).GetProperty("LogSystem")!.SetValue(pr, service);
            prProperty.SetValue(null, pr);
            var plugin = RuntimeHelpers.GetUninitializedObject(pluginType);
            pluginType.GetField("nativeActionEffects", hidden)!.SetValue(plugin, capture);
            pluginProperty.SetValue(null, plugin);
            var recorderType = assembly.GetType("MasterPlayer.Replay.ReplayRecorder", true)!;
            var frameType = assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame", true)!;
            var frame = Activator.CreateInstance(frameType)!;
            frameType.GetProperty("Full")!.SetValue(frame, true);
            var world = typeof(NativeActionEffectRegression).GetMethod(nameof(World), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(frameType).Invoke(null, [frame])!;
            dynamic recorder = Activator.CreateInstance(recorderType, hidden, null, [world], null)!;
            using var lifetime = (IDisposable)recorder;
            emit(1, 16964, 100, 1, [101, 100], DateTime.UtcNow);
            Check((bool)recorder.StartRecording(false), (string)recorder.StatusMessage);
            var start = ((DateTime)recorderType.GetField("_startLocal", hidden)!.GetValue(recorder)!).ToUniversalTime();
            recorderType.GetField("_playerSourceCacheAt", hidden)!.SetValue(recorder, DateTime.UtcNow.AddHours(1));
            var players = (HashSet<uint>)recorderType.GetField("_playerSourceIds", hidden)!.GetValue(recorder)!;
            players.Add(100);
            players.Add(200); // The filter receives the already resolved player/pet source set.
            emit(1, 16964, 100, 2, [101, 100], start.AddTicks(-1));
            emit(100, 16964, 100, 3, [101, 100], start);
            emit(200, 16964, 100, 4, [101, 100], start);
            emit(1, 16964, 100, 5, [101, 100], start);
            emit(1, 16964, 101, 6, [100, 101], start.AddTicks(2));
            var status = new LogSystemStatusAddEvent { SourceId = 1, TargetId = 100, StatusId = 1, Timestamp = start.AddTicks(1).ToLocalTime() };
            foreach (var callback in callbacks) callback.DynamicInvoke(status);
            var path = (string)recorder.CurrentFilePath;
            recorder.StopRecording();
            Check((int)recorder.EventCount == 3 && (int)recorder.FilteredCount == 2
                && !((string)recorder.StatusMessage).Contains("中止"), "Native recorder boundaries/filter/queued stop flush failed.");
            var reader = assembly.GetType("MasterPlayer.Replay.ReplayRecordingReader", true)!;
            var recording = reader.GetMethod("Load", [typeof(string)])!.Invoke(null, [path])!;
            var entries = ((IEnumerable)recording.GetType().GetProperty("Events")!.GetValue(recording)!).Cast<object>()
                .Select(e => e.GetType().GetProperty("Event")!.GetValue(e)!).ToArray();
            Check(entries.Length == 3 && entries[0].GetType() == eventType && entries[1] is LogSystemStatusAddEvent
                && entries[2].GetType() == eventType
                && new[] { entries[0], entries[2] }.Select(e => (ulong)eventType.GetProperty("MainTargetId")!.GetValue(e)!)
                    .SequenceEqual([100UL, 101UL])
                && ((ulong[])eventType.GetProperty("Targets")!.GetValue(entries[2])!).SequenceEqual([100UL, 101UL]),
                "Native effects failed production serialization/readback or crossed a later/earlier PR event.");
            Check((bool)recorder.StartRecording(false), (string)recorder.StatusMessage);
            path = (string)recorder.CurrentFilePath;
            emit(1, 16964, 100, 7, [101, 100], DateTime.UtcNow);
            pluginType.GetField("_replayRecorder", hidden)!.SetValue(plugin, (object)recorder);
            pluginType.GetMethod("DisposeRuntime", hidden)!.Invoke(plugin, null);
            recording = reader.GetMethod("Load", [typeof(string)])!.Invoke(null, [path])!;
            entries = ((IEnumerable)recording.GetType().GetProperty("Events")!.GetValue(recording)!).Cast<object>().ToArray();
            Check(entries.Length == 1 && !(bool)recorder.IsRecording,
                "Runtime disposal cleared the capture queue before flushing the recording tail.");
        }
        finally
        {
            pluginProperty.SetValue(null, oldPlugin);
            prProperty.SetValue(null, oldPr);
            Set(nameof(Svc.ClientState), oldClient);
            Set(nameof(Svc.Chat), oldChat);
            Set(nameof(Svc.PluginInterface), oldInterface);
        }
    }

    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}

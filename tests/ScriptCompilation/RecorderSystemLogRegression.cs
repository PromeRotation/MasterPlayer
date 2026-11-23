using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Game.Chat;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;

internal static class RecorderSystemLogRegression
{
    internal static void Run(Assembly assembly, string config)
    {
        var originalChat = Svc.Chat;
        var originalClient = Svc.ClientState;
        var originalInterface = Svc.PluginInterface;
        var originalLog = Svc.Log;
        var prProperty = typeof(PromeRotation.Plugin).GetProperty("Instance")!;
        var originalPr = prProperty.GetValue(null);
        var callbacks = new List<IChatGui.OnLogMessageDelegate>();
        var errors = 0;
        void Set(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);
        try
        {
            Set(nameof(Svc.Chat), CatalogServiceProxy.Create<IChatGui>((name, args) =>
            {
                if (name == "add_LogMessage") callbacks.Add((IChatGui.OnLogMessageDelegate)args[0]!);
                else if (name == "remove_LogMessage") callbacks.Remove((IChatGui.OnLogMessageDelegate)args[0]!);
                else throw new NotSupportedException(name);
                return null;
            }));
            Set(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => name == "get_TerritoryType" ? 603u : throw new NotSupportedException(name)));
            Set(nameof(Svc.Log), CatalogServiceProxy.Create<IPluginLog>((name, _) => { if (name == "Error") ++errors; return null; }));
            var source = CatalogServiceProxy.Create<ILogSystemEventSource>((name, _) =>
                name == "Subscribe" ? new MemoryStream() : throw new NotSupportedException(name));
            var service = RuntimeHelpers.GetUninitializedObject(typeof(LogSystemService));
            typeof(LogSystemService).GetField("<Events>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, source);
            var pr = RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
            typeof(PromeRotation.Plugin).GetProperty("LogSystem")!.SetValue(pr, service);
            prProperty.SetValue(null, pr);
            var recorderType = assembly.GetType("MasterPlayer.Replay.ReplayRecorder")!;
            var frameType = assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!;
            var eventType = assembly.GetType("MasterPlayer.Replay.ReplaySystemLogEvent")!;
            var frame = Activator.CreateInstance(frameType)!;
            frameType.GetProperty("Full")!.SetValue(frame, true);
            frameType.GetProperty("Territory")!.SetValue(frame, (ushort)603);
            var capture = typeof(RecorderSystemLogRegression).GetMethod(nameof(Capture), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(frameType).Invoke(null, [frame])!;
            void Emit(int value)
            {
                var message = CatalogServiceProxy.Create<ILogMessage>((name, args) =>
                {
                    if (name == "get_LogMessageId") return 7222u;
                    if (name == "get_ParameterCount") return 1;
                    if (name == "TryGetIntParameter") { args[1] = value; return true; }
                    throw new NotSupportedException(name);
                });
                foreach (var callback in callbacks.ToArray()) callback(message);
            }
            for (var scenario = 0; scenario < 4; ++scenario)
            {
                var directory = Directory.CreateDirectory(Path.Combine(config, "recorder-" + scenario));
                Set(nameof(Svc.PluginInterface), CatalogServiceProxy.Create<IDalamudPluginInterface>((name, _) =>
                    name == "get_ConfigDirectory" ? directory : throw new NotSupportedException(name)));
                dynamic recorder = Activator.CreateInstance(recorderType, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [capture], null)!;
                object recorderObject = recorder;
                using var lifetime = (IDisposable)recorder;
                if (scenario == 0) Emit(8);
                Check((bool)recorder.StartRecording(false), (string)recorder.StatusMessage);
                var path = (string)recorder.CurrentFilePath;
                var start = ((DateTime)recorderType.GetField("_startLocal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(recorder)!).ToUniversalTime();
                object Event(uint id, DateTime time)
                {
                    var ev = Activator.CreateInstance(eventType)!;
                    eventType.GetProperty("MessageId")!.SetValue(ev, id);
                    eventType.GetProperty("Timestamp")!.SetValue(ev, time);
                    eventType.GetProperty("Arguments")!.SetValue(ev, new int?[] { 8 });
                    return ev;
                }
                if (scenario == 1) { Emit(8); Emit(16); }
                if (scenario == 2)
                {
                    var receive = recorderType.GetMethod("OnLogEvent", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    receive.Invoke(recorderObject, [Event(7222, start.AddTicks(-1))]);
                    receive.Invoke(recorderObject, [Event(7222, start)]);
                }
                if (scenario == 3)
                {
                    var queue = recorderType.GetField("pendingSystemLogs", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(recorderObject)!;
                    queue.GetType().GetMethod("Enqueue")!.Invoke(queue, [Event(9999, start)]);
                    Emit(8);
                }
                recorder.StopRecording();
                Check(!(bool)recorder.IsRecording && !((string)recorder.StatusMessage).Contains("中止") && errors == 0, "Recorder did not stop cleanly with typed log delivery.");
                var lines = File.ReadAllLines(path).Select(line => JsonDocument.Parse(line)).ToArray();
                try
                {
                    var events = lines.Where(d => d.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "SystemLog").Select(d => d.RootElement).ToArray();
                    Check(events.Length == (scenario is 1 or 3 ? 2 : scenario == 2 ? 1 : 0), "Recorder lost/duplicated logs at the recording boundary.");
                    Check(lines.Any(d => d.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "WorldFrame"), "Recorder omitted the real world-frame writer path.");
                    if (scenario == 1)
                        Check(events.Select(e => e.GetProperty("data").GetProperty("Arguments")[0].GetInt32()).SequenceEqual([8, 16]), "Consecutive overcaps were deduplicated or parameters changed.");
                    if (scenario == 3)
                        Check(events.Select(e => e.GetProperty("data").GetProperty("MessageId").GetUInt32()).SequenceEqual([9999u, 7222u]), "Separate system-log queues reversed capture order.");
                }
                finally { foreach (var line in lines) line.Dispose(); }
                Emit(8);
                lifetime.Dispose();
                assembly.GetType("MasterPlayer.Scripting.SystemLogParameterEvents")!.GetMethod("Drain", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [false]);
                Check(callbacks.Count == 0, "Recorder disposal retained a live parameter subscription.");
            }
            Console.WriteLine("PASS production recorder: pre-start exclusion, queued stop flush, repeated overcaps, exact start timestamp, cross-source ordering, typed parameters, world-frame writing and disposal without native sampling");
        }
        finally
        {
            prProperty.SetValue(null, originalPr);
            Set(nameof(Svc.Chat), originalChat);
            Set(nameof(Svc.ClientState), originalClient);
            Set(nameof(Svc.PluginInterface), originalInterface);
            Set(nameof(Svc.Log), originalLog);
        }
    }

    private static Func<DateTime, T> Capture<T>(T frame) => _ => frame;
    private static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
    }
}

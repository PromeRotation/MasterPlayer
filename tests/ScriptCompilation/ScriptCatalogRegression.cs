using System.Collections;
using System.Reflection;
using System.Numerics;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PromeRotation.GreenMoveSystem;

internal static class ScriptCatalogRegression
{
    public static void Run(string pluginPath, IReadOnlyList<MetadataReference> references)
    {
        var config = Directory.CreateTempSubdirectory("MasterPlayer-ScriptCatalog-");
        var originalInterface = Svc.PluginInterface;
        var originalLog = Svc.Log;
        try
        {
            SetService(nameof(Svc.PluginInterface), CatalogServiceProxy.Create<IDalamudPluginInterface>((name, _) => name switch
            {
                "get_ConfigDirectory" => config,
                "get_AssemblyLocation" => new FileInfo(typeof(PromeRotation.Plugin).Assembly.Location),
                _ => throw new NotSupportedException(name),
            }));
            SetService(nameof(Svc.Log), CatalogServiceProxy.Create<IPluginLog>((_, _) => null));

            var package = Directory.CreateDirectory(Path.Combine(config.FullName, "Plugins", "MasterPlayer"));
            File.Copy(pluginPath, Path.Combine(package.FullName, "MasterPlayer.dll"));
            using var stream = File.OpenRead(pluginPath);
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            Check(string.IsNullOrEmpty(assembly.Location), "Exercise the host's stream-loaded plugin path.");
            var managerType = assembly.GetType("MasterPlayer.Scripting.ScriptManager", throwOnError: true)!;
            dynamic manager = Activator.CreateInstance(managerType)!;
            using var lifetime = (IDisposable)manager;
            dynamic[] Scripts() => ((IEnumerable)manager.Scripts).Cast<dynamic>().ToArray();
            void Refresh()
            {
                manager.RefreshCatalog();
                Check(manager.ErrorMessage == "", (string)manager.ErrorMessage);
            }
            dynamic Find(string id) => Scripts().Single(d => d.Metadata.Id == id);
            void ExpectOverride(string id, string source)
            {
                Check(Scripts().Length == 9, "An override must replace its built-in entry.");
                Check(Find(id).Source.ToString() == source, "The configuration directory must win.");
            }
            void Pass(string name) => Console.WriteLine($"PASS script catalog: {name}");

            Refresh();
            Check(Scripts().Length == 9 && Scripts().All(d => d.Source.ToString() == "BuiltIn"),
                "An empty configuration must discover five boards, three dungeons and Blunderville, excluding examples.");
            foreach (var (id, territory) in new[] { ("sastasha", 1036u), ("tamtara", 1037u), ("copperbell", 1038u) })
                Check(((uint[])Find("masterplayer.dungeon." + id).Metadata.Territorys).SequenceEqual([territory]), "Dungeon territory identity must match the remade duty.");
            var race = Find("masterplayer.goldsaucer.blunderville");
            Check(((uint[])race.Metadata.Territorys).SequenceEqual([1165u]), "Blunderville must activate in the actual minigame territory.");
            Check(((IEnumerable<string>)race.CreateInstance().GetMechanisms()).Count() == 7, "Expose six obstacle groups and the race controls for the minigame.");
            Check(Scripts().All(d => d.CreateInstance().GetType().Assembly == assembly), "Factories must use the loaded plugin assembly.");
            Check(!Directory.EnumerateFiles((string)manager.ScriptsDirectory, "*", SearchOption.AllDirectories).Any(),
                "Discovery must not copy scripts into the configuration directory.");
            var baseline = Scripts()[0];
            string builtInId = baseline.Metadata.Id;
            string builtInName = baseline.Metadata.Name;
            Pass("empty configuration discovers built-ins from a stream-loaded plugin");

            var sourcePath = Path.Combine((string)manager.ScriptsDirectory, "override.cs");
            File.WriteAllText(sourcePath, Source(builtInName, "external.override"));
            Refresh();
            ExpectOverride("external.override", "LocalSource");
            Check(!Scripts().Any(d => d.Metadata.Id == builtInId), "Name matching must work with a different GUID.");
            Check(!manager.IsScriptEnabled("external.override"), "A disabled external override must still shadow the built-in.");
            Check(Find("external.override").CreateInstance().GetType().Assembly != assembly, "Use the external factory.");
            Pass("same name, different GUID uses external source even when disabled");

            File.WriteAllText(sourcePath, Source("Renamed external script", builtInId.ToUpperInvariant()));
            Refresh();
            ExpectOverride(builtInId.ToUpperInvariant(), "LocalSource");
            Pass("same GUID ignores case and overrides a renamed built-in");

            File.WriteAllText(sourcePath, Source(builtInName, builtInId));
            Refresh();
            ExpectOverride(builtInId, "LocalSource");
            manager.ReloadScripts();
            ExpectOverride(builtInId, "LocalSource");
            Check(manager.ErrorMessage == "", "Refreshing an override must not report a duplicate ID.");
            Pass("same name and GUID remain unique after reload");

            File.WriteAllText(sourcePath, "this is not valid C#;");
            manager.RefreshCatalog();
            Check(manager.ErrorMessage.Contains("源码脚本编译失败"), "Invalid external source must report its error.");
            Check(Find(builtInId).Source.ToString() == "BuiltIn", "A failed external load must leave the built-in available.");
            File.Delete(sourcePath);
            Refresh();
            Check(Scripts().Length == 9 && Find(builtInId).Source.ToString() == "BuiltIn", "Removing an override must restore the built-in.");
            Pass("compile failure reports an error; removal restores the built-in");

            File.WriteAllText(sourcePath, Source("Independent external script", "external.independent"));
            Refresh();
            Check(Scripts().Length == 10 && Find(builtInId).Source.ToString() == "BuiltIn", "Unrelated scripts must coexist.");
            File.Delete(sourcePath);
            Pass("unrelated external scripts coexist with built-ins");

            var dllPath = Path.Combine((string)manager.ScriptsDirectory, "override.dll");
            var compilation = CSharpCompilation.Create("CatalogOverride",
                [CSharpSyntaxTree.ParseText(Source(builtInName, "external.dll"))], references,
                new(OutputKind.DynamicallyLinkedLibrary));
            using (var output = File.Create(dllPath))
            {
                var result = compilation.Emit(output);
                Check(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
            }
            Refresh();
            ExpectOverride("external.dll", "LocalDll");
            File.Delete(dllPath);
            Refresh();
            Pass("external DLL overrides by name and can be removed");

            dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
            settings.EnableScripts = false;
            dynamic definition = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripting.Visual.VisualScriptDefinition")!)!;
            definition.Name = builtInName;
            definition.TerritoryId = 1339;
            string visualId = definition.Id;
            var visualPath = Path.Combine((string)manager.ScriptsDirectory, "override.mechanic.json");
            var save = managerType.GetMethod("SaveVisualScript", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var delete = managerType.GetMethod("DeleteVisualScript", BindingFlags.Instance | BindingFlags.NonPublic)!;
            string json = (string)save.Invoke((object)manager, [visualPath, (object)definition, null])!;
            ExpectOverride(visualId, "Visual");
            Refresh();
            ExpectOverride(visualId, "Visual");
            Pass("visual save and directory scan both override by name");

            definition.Name = "Renamed visual script";
            json = (string)save.Invoke((object)manager, [visualPath, (object)definition, json])!;
            Check(Scripts().Length == 10 && Find(builtInId).Source.ToString() == "BuiltIn", "Renaming away must restore the built-in immediately.");
            definition.Name = builtInName;
            json = (string)save.Invoke((object)manager, [visualPath, (object)definition, json])!;
            ExpectOverride(visualId, "Visual");
            delete.Invoke((object)manager, [(object)Find(visualId), json]);
            Check(Scripts().Length == 9 && Find(builtInId).Source.ToString() == "BuiltIn", "Deleting a visual override must restore the built-in immediately.");
            Check(manager.ErrorMessage == "", (string)manager.ErrorMessage);
            Pass("visual rename and deletion restore built-ins without a full reload");
            CheckLocalSafeZone(assembly, (object)manager);
        }
        finally
        {
            SetService(nameof(Svc.PluginInterface), originalInterface);
            SetService(nameof(Svc.Log), originalLog);
            config.Delete(recursive: true);
        }
    }

    private static string Source(string name, string id) => $$"""
        using MasterPlayer.Scripting;
        [MechanicScript({{JsonSerializer.Serialize(name)}}, new uint[0], {{JsonSerializer.Serialize(id)}}, "1.0", "test")]
        public sealed class OverrideScript : MasterPlayerScript { }
        """;

    private static void CheckLocalSafeZone(Assembly assembly, object manager)
    {
        var pluginType = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!;
        var instance = pluginType.GetProperty("Instance")!;
        var previous = instance.GetValue(null);
        var runtimeType = assembly.GetType("MasterPlayer.Spatial.SafeZoneRuntime")!;
        var moveCalls = 0;
        var movement = CatalogServiceProxy.Create<IGreenMoveSystem>((name, _) =>
        {
            moveCalls++;
            throw new InvalidOperationException("Registration/preview must not call GreenMove: " + name);
        });
        dynamic runtime = Activator.CreateInstance(runtimeType, BindingFlags.NonPublic | BindingFlags.Instance,
            null, [movement, (Func<bool>)(() => false), (Func<bool>)(() => false)], null)!;
        using var lifetime = (IDisposable)runtime;
        try
        {
            var plugin = Activator.CreateInstance(pluginType)!;
            pluginType.GetField("_safeZone", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, (object)runtime);
            pluginType.GetField("_scripts", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, manager);
            instance.SetValue(null, plugin);
            runtime.Setting.EnableSafeZone = true;
            var apiType = assembly.GetType("MasterPlayer.Scripting.ScriptSafeZoneApi")!;
            dynamic api = Activator.CreateInstance(apiType, BindingFlags.NonPublic | BindingFlags.Instance,
                null, ["local-migration-test", "Local migration test"], null)!;
            dynamic template = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.CircleTemplate")!, [3f])!;
            dynamic binding = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.StaticTransform")!, [Vector3.Zero, 0f])!;
            dynamic handle = api.AddPersistentDanger(template, binding);
            Check(runtime.Manager.ContainsArea(handle), "The actual script facade must register in the plugin runtime.");
            dynamic snapshot = runtime.Manager.BuildSnapshot(DateTime.UtcNow);
            var dangers = ((IEnumerable)snapshot.DangerAreas).Cast<dynamic>().ToArray();
            Check(dangers.Length == 1 && dangers[0].Geometry.GetType().Assembly == assembly,
                "The production snapshot and geometry must belong to MasterPlayer.");
            apiType.GetMethod("Cleanup", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke((object)api, null);
            Check(!runtime.Manager.ContainsArea(handle), "The actual facade cleanup must remove its local handle.");
            foreach (var method in new[] { "EnterReplayMode", "ExitReplayMode" })
            {
                handle = api.AddPersistentDanger(template, binding);
                manager.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(manager, null);
                Check(!runtime.Manager.ContainsArea(handle), "Replay transitions must clear the previous session's areas.");
            }
            Check(moveCalls == 0, "Registration and replay preview must not invoke movement.");
            Console.WriteLine("PASS production local runtime: script registration, cleanup and replay transitions");
            CheckReplaySession(assembly, manager, runtime, plugin);
            Check(moveCalls == 0, "Recorded playback must never invoke movement.");
        }
        finally { instance.SetValue(null, previous); }
    }

    private static void CheckReplaySession(Assembly assembly, dynamic manager, dynamic runtime, object plugin)
    {
        var originalClient = Svc.ClientState;
        var originalObjects = Svc.Objects;
        var prInstance = typeof(PromeRotation.Plugin).GetProperty("Instance")!;
        var originalPr = prInstance.GetValue(null);
        dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var enabled = (bool)settings.EnableScripts;
        var file = Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "world.mpreplay.jsonl");
        var liveObjectReads = 0;
        IDisposable? draw = null;
        dynamic? controller = null;
        try
        {
            SetService(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => name switch
            {
                "add_TerritoryChanged" or "remove_TerritoryChanged" => null,
                "get_TerritoryType" => 999u,
                _ => throw new NotSupportedException(name),
            }));
            SetService(nameof(Svc.Objects), CatalogServiceProxy.Create<IObjectTable>((name, _) =>
            {
                liveObjectReads++;
                throw new InvalidOperationException("Replay accessed live objects: " + name);
            }));
            draw = new PromeRotation.Spatial.Drawing.DrawManager();
            var pr = RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
            typeof(PromeRotation.Plugin).GetField("_drawManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pr, draw);
            prInstance.SetValue(null, pr);
            var visibilityType = assembly.GetType("MasterPlayer.Radar.AoeVisibilityRegistry")!;
            var visibility = Activator.CreateInstance(visibilityType, [(object)settings.AoeVisibility, (Func<DateTime>)(() => (DateTime)runtime.CurrentTime)])!;
            plugin.GetType().GetProperty("AoeVisibility", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, visibility);
            var recorder = RuntimeHelpers.GetUninitializedObject(assembly.GetType("MasterPlayer.Replay.ReplayRecorder")!);
            controller = Activator.CreateInstance(assembly.GetType("MasterPlayer.Replay.ReplayController")!,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [(object)manager, recorder, null!], null)!;
            plugin.GetType().GetField("_replayController", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, (object)controller);
            settings.EnableScripts = true;
            manager.SetScriptEnabled("masterplayer.dungeon.copperbell", true);
            File.WriteAllText(file, """
                {"v":2,"territory":1038,"startedUtc":"2026-09-21T01:00:00Z","pluginVersion":"test"}
                {"t":0,"kind":"WorldFrame","data":{"Full":true,"Territory":1038,"LocalPlayerId":2,"Actors":[{"Id":1,"Oid":14448,"InCombat":true,"Targetable":true,"Position":{"X":27,"Y":0,"Z":114}},{"Id":2,"Kind":1,"Position":{"X":27,"Y":0,"Z":114}}]}}
                {"t":1,"kind":"WorldFrame","data":{"Territory":1038,"LocalPlayerId":2,"Actors":[{"Id":1,"Oid":14448,"InCombat":true,"Targetable":true,"Position":{"X":27,"Y":0,"Z":114},"Cast":{"ActionId":28462,"Location":{"X":3,"Y":0,"Z":4},"Rotation":1.5707964,"StartedAt":"2026-09-21T01:00:01Z","FinishAt":"2026-09-21T01:00:05.3Z"}}]}}
                {"t":1,"kind":"CastStart","type":"PromeRotation.LogSystem.LogSystemCastStartEvent","packetFacing":1.5707964,"data":{"SourceId":1,"ActionId":28462,"CastTime":4,"Timestamp":"2026-09-21T01:00:01Z","EffectPosition":{"X":3,"Y":0,"Z":4}}}
                {"t":6,"kind":"WorldFrame","data":{"Territory":1038,"LocalPlayerId":2,"Removed":[1]}}
                """);
            int Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count();
            int Previews() => ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Count();
            Check(controller.Load(file), (string)controller.StatusMessage);
            controller.Play();
            Check(controller.ReplayScriptCount == 1 && controller.World.HasSnapshot, (string)manager.ErrorMessage);
            controller.Update(2f);
            Check(controller.Clock == 2 && Dangers() == 1 && Previews() == 1, "Production playback did not register the recorded AOE.");
            var pausedTime = (DateTime)runtime.CurrentTime;
            controller.Pause();
            controller.Update(30f);
            manager.Update(30f);
            void TickRuntime() => ((object)runtime).GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke((object)runtime, [1f, false]);
            TickRuntime();
            plugin.GetType().GetMethod("ClearLiveSafeZone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);
            Check(runtime.CurrentTime == pausedTime && Dangers() == 1 && Previews() == 1, "Paused production replay changed the world or expired its output.");
            Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null, "A live lifecycle event cleared the replay arena.");
            controller.Seek(6d);
            Check(Dangers() == 0 && Previews() == 0, "Forward seek left the removed boss AOE behind.");
            controller.Seek(2d);
            Check(runtime.CurrentTime == pausedTime && Dangers() == 1 && Previews() == 1, "Backward seek failed to reconstruct production output.");
            controller.SetSpeed(2f);
            dynamic template = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.CircleTemplate")!, [3f])!;
            dynamic binding = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZone.StaticTransform")!, [Vector3.Zero, 0f])!;
            dynamic timed = runtime.Registry.AddDelayedHit(template, binding, 0.1f, 0.1f);
            TickRuntime();
            Check(runtime.Manager.ContainsArea(timed), "Replay pruned a timed area before its activation.");
            controller.Play();
            controller.Update(1f);
            TickRuntime();
            Check(!runtime.Manager.ContainsArea(timed), "An expired timed replay area was never pruned.");
            Check(controller.Clock == 4 && Dangers() == 1, "Playback speed advanced the wrong clock.");
            controller.Update(1f);
            Check(controller.Clock == 6 && !controller.Playing && Dangers() == 0 && Previews() == 0, "Playback stopped before the final world frame.");
            controller.StopSession();
            Check(controller.World == null && Dangers() == 0 && Previews() == 0
                && runtime.GetType().GetProperty("ReplayClock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue((object)runtime) == null
                && runtime.Manager.GetType().GetProperty("EntityPoseOverride", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue((object)runtime.Manager) == null,
                "Stopping replay retained a world override or output.");
            File.WriteAllText(file, "{\"v\":1,\"territory\":1038,\"startedUtc\":\"2026-09-21T01:00:00Z\",\"pluginVersion\":\"old\"}");
            Check(controller.Load(file), "Legacy recording should remain readable.");
            controller.Play();
            Check(((string)controller.StatusMessage).Contains("缺少对象状态"), "Legacy playback hid its missing-world warning.");
            manager.ReloadScripts();
            Check(!controller.SessionActive && manager.Running.Count == 0, "Refresh started live scripts before leaving replay mode.");
            manager.Update(0.1f);
            Check(liveObjectReads == 0, "Replay substituted today's object table for recorded actors.");
            Check(controller.RouterErrorCount == 0 && manager.ErrorMessage == "", "Production replay reported script failures.");
            Console.WriteLine("PASS production replay: recorded objects, pause, speed, forward/backward seek, final frame, cleanup and legacy isolation");
        }
        finally
        {
            if (controller != null) ((IDisposable)controller).Dispose();
            manager.SetScriptEnabled("masterplayer.dungeon.copperbell", false);
            settings.EnableScripts = enabled;
            draw?.Dispose();
            prInstance.SetValue(null, originalPr);
            SetService(nameof(Svc.ClientState), originalClient);
            SetService(nameof(Svc.Objects), originalObjects);
        }
    }

    private static void SetService(string name, object? value)
        => typeof(Svc).GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

public class CatalogServiceProxy : DispatchProxy
{
    private Func<string, object?[], object?> _invoke = null!;

    internal static T Create<T>(Func<string, object?[], object?> invoke) where T : class
    {
        var proxy = Create<T, CatalogServiceProxy>();
        ((CatalogServiceProxy)(object)proxy)._invoke = invoke;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args) => _invoke(method!.Name, args ?? []);
}

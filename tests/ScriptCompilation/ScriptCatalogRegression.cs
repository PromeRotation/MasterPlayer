using System.Collections;
using System.Reflection;
using System.Numerics;
using System.Runtime.Loader;
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
                Check(Scripts().Length == 5, "An override must replace its built-in entry.");
                Check(Find(id).Source.ToString() == source, "The configuration directory must win.");
            }
            void Pass(string name) => Console.WriteLine($"PASS script catalog: {name}");

            Refresh();
            Check(Scripts().Length == 5 && Scripts().All(d => d.Source.ToString() == "BuiltIn"),
                "An empty configuration must discover all five built-in boards, excluding examples.");
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
            Check(Scripts().Length == 5 && Find(builtInId).Source.ToString() == "BuiltIn", "Removing an override must restore the built-in.");
            Pass("compile failure reports an error; removal restores the built-in");

            File.WriteAllText(sourcePath, Source("Independent external script", "external.independent"));
            Refresh();
            Check(Scripts().Length == 6 && Find(builtInId).Source.ToString() == "BuiltIn", "Unrelated scripts must coexist.");
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
            Check(Scripts().Length == 6 && Find(builtInId).Source.ToString() == "BuiltIn", "Renaming away must restore the built-in immediately.");
            definition.Name = builtInName;
            json = (string)save.Invoke((object)manager, [visualPath, (object)definition, json])!;
            ExpectOverride(visualId, "Visual");
            delete.Invoke((object)manager, [(object)Find(visualId), json]);
            Check(Scripts().Length == 5 && Find(builtInId).Source.ToString() == "BuiltIn", "Deleting a visual override must restore the built-in immediately.");
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
        }
        finally { instance.SetValue(null, previous); }
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

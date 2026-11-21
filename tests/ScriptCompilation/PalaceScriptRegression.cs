using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using PromeRotation.GreenMoveSystem;

internal static class PalaceScriptRegression
{
    internal static void Run(string pluginPath)
    {
        var originalInterface = Svc.PluginInterface;
        var originalLog = Svc.Log;
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pluginPath))!, "palace-smoke-config");
        var config = Directory.CreateDirectory(root);
        try
        {
            Set(nameof(Svc.PluginInterface), CatalogServiceProxy.Create<IDalamudPluginInterface>((name, _) => name switch
            {
                "get_ConfigDirectory" => config,
                "get_AssemblyLocation" => new FileInfo(typeof(PromeRotation.Plugin).Assembly.Location),
                _ => throw new NotSupportedException(name),
            }));
            Set(nameof(Svc.Log), CatalogServiceProxy.Create<IPluginLog>((_, _) => null));
            using var stream = File.OpenRead(pluginPath);
            var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
            var managerType = assembly.GetType("MasterPlayer.Scripting.ScriptManager", true)!;
            dynamic manager = Activator.CreateInstance(managerType)!;
            using var lifetime = (IDisposable)manager;
            manager.RefreshCatalog();
            if ((string)manager.ErrorMessage != "") throw new Exception((string)manager.ErrorMessage);
            var scripts = ((IEnumerable)manager.Scripts).Cast<dynamic>().ToArray();
            dynamic palace = scripts.Single(s => s.Metadata.Id == "promeradar.potd151-200");
            if (!((uint[])palace.Metadata.Territorys).SequenceEqual(new uint[] { 603, 604, 605, 606, 607 })) throw new Exception("Wrong Palace territory IDs");
            if (!(bool)manager.IsScriptEnabled("promeradar.potd151-200")) throw new Exception("Palace script must default enabled");
            var script = palace.CreateInstance();
            if (((IEnumerable<string>)script.GetMechanisms()).Count() != 3) throw new Exception("Missing Palace mechanism groups");
            if ((bool)script.EncounterActive) throw new Exception("Unstarted script has active boss state");
            Console.WriteLine("PASS production Palace entry: independent compilation, streamed DLL discovery, real territory IDs, default activation and settings preview");
            Replay(assembly, manager, config.FullName);
        }
        finally
        {
            Set(nameof(Svc.PluginInterface), originalInterface);
            Set(nameof(Svc.Log), originalLog);
        }
    }

    private static void Set(string name, object? value)
        => typeof(Svc).GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);

    private static void Replay(Assembly assembly, dynamic manager, string config)
    {
        var originalClient = Svc.ClientState;
        var originalObjects = Svc.Objects;
        var prProperty = typeof(PromeRotation.Plugin).GetProperty("Instance")!;
        var originalPr = prProperty.GetValue(null);
        var pluginType = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!;
        var pluginProperty = pluginType.GetProperty("Instance")!;
        var originalPlugin = pluginProperty.GetValue(null);
        var moves = 0;
        var objectReads = 0;
        var movement = CatalogServiceProxy.Create<IGreenMoveSystem>((name, _) =>
        {
            ++moves;
            throw new InvalidOperationException("Palace replay called GreenMove: " + name);
        });
        dynamic runtime = Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.SafeZoneRuntime")!,
            BindingFlags.NonPublic | BindingFlags.Instance, null, [movement, (Func<bool>)(() => false), (Func<bool>)(() => false)], null)!;
        dynamic? controller = null;
        using var runtimeLifetime = (IDisposable)runtime;
        PromeRotation.Spatial.Drawing.DrawManager? draw = null;
        try
        {
            Set(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => name switch
            {
                "add_TerritoryChanged" or "remove_TerritoryChanged" => null,
                "get_TerritoryType" => 999u,
                _ => throw new NotSupportedException(name),
            }));
            Set(nameof(Svc.Objects), CatalogServiceProxy.Create<IObjectTable>((name, _) =>
            {
                ++objectReads;
                throw new InvalidOperationException("Palace replay accessed live objects: " + name);
            }));
            draw = new PromeRotation.Spatial.Drawing.DrawManager();
            var pr = RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
            typeof(PromeRotation.Plugin).GetField("_drawManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pr, draw);
            prProperty.SetValue(null, pr);
            var plugin = Activator.CreateInstance(pluginType)!;
            pluginType.GetField("_safeZone", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, (object)runtime);
            pluginType.GetField("_scripts", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, (object)manager);
            pluginProperty.SetValue(null, plugin);
            runtime.Setting.EnableSafeZone = true;
            dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
            settings.EnableMechanics = true;
            var visibilityType = assembly.GetType("MasterPlayer.Radar.AoeVisibilityRegistry")!;
            var visibility = Activator.CreateInstance(visibilityType, [(object)settings.AoeVisibility, (Func<DateTime>)(() => (DateTime)runtime.CurrentTime)])!;
            pluginType.GetProperty("AoeVisibility", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, visibility);
            var recorder = RuntimeHelpers.GetUninitializedObject(assembly.GetType("MasterPlayer.Replay.ReplayRecorder")!);
            controller = Activator.CreateInstance(assembly.GetType("MasterPlayer.Replay.ReplayController")!,
                BindingFlags.Instance | BindingFlags.NonPublic, null, [(object)manager, recorder, null!], null)!;
            pluginType.GetField("_replayController", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, (object)controller);
            var file = Path.Combine(config, "palace.mpreplay.jsonl");
            File.WriteAllText(file, """
                {"v":2,"territory":603,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"palace-test"}
                {"t":0,"kind":"WorldFrame","data":{"Full":true,"Territory":603,"LocalPlayerId":2,"Actors":[{"Id":1,"Oid":6173,"Hp":1000,"MaxHp":1000,"InCombat":true,"Targetable":true,"Position":{"X":-300,"Y":0,"Z":-300}},{"Id":3,"Oid":6383,"InCombat":true,"Position":{"X":-310,"Y":0,"Z":-310}},{"Id":2,"Kind":1,"Position":{"X":-300,"Y":0,"Z":-300}}]}}
                {"t":1,"kind":"CastStart","type":"PromeRotation.LogSystem.LogSystemCastStartEvent","packetFacing":0,"data":{"SourceId":3,"ActionId":7090,"CastTime":6.2,"Timestamp":"2026-11-19T00:00:01Z","SourcePosition":{"X":-310,"Y":0,"Z":-310},"EffectPosition":{"X":-310,"Y":0,"Z":-310}}}
                {"t":3,"kind":"CancelCast","type":"PromeRotation.LogSystem.LogSystemCancelCastEvent","data":{"SourceId":3,"Timestamp":"2026-11-19T00:00:03Z"}}
                {"t":5,"kind":"WorldFrame","data":{"Territory":603,"LocalPlayerId":2,"Removed":[1,3]}}
                """);
            int Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count();
            void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
            Check((bool)controller.Load(file), (string)controller.StatusMessage);
            controller.Play();
            controller.Update(2f);
            Check(controller.ReplayScriptCount == 1 && Dangers() == 1, "Palace replay did not register native Infatuation");
            controller.Pause();
            var clock = (DateTime)runtime.CurrentTime;
            controller.Update(10f);
            Check(runtime.CurrentTime == clock && Dangers() == 1, "Palace pause advanced its mechanism clock");
            controller.Seek(3.1d);
            Check(Dangers() == 0, "Palace cancel retained a danger");
            controller.Seek(2d);
            Check(Dangers() == 1, "Palace seek did not reconstruct its state");
            controller.Seek(5d);
            Check(Dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Palace boss removal retained output");
            controller.StopSession();
            Check(Dangers() == 0 && moves == 0 && objectReads == 0 && controller.RouterErrorCount == 0 && (string)manager.ErrorMessage == "",
                "Palace replay leaked state or crossed live-execution boundaries");
            Console.WriteLine("PASS production Palace replay: real script routing, exact danger, pause, cancel, seek, boss boundary cleanup, no live reads or movement");
            PalaceRecordedReplay.Run((object)controller, (object)runtime, config);
            Check(moves == 0 && objectReads == 0, "Recorded Palace replay crossed the live execution boundary");
        }
        finally
        {
            if (controller != null) ((IDisposable)controller).Dispose();
            draw?.Dispose();
            pluginProperty.SetValue(null, originalPlugin);
            prProperty.SetValue(null, originalPr);
            Set(nameof(Svc.ClientState), originalClient);
            Set(nameof(Svc.Objects), originalObjects);
        }
    }
}

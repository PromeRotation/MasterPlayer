using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

internal static class ScriptSettingsRegression
{
    private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Assembly assembly, dynamic manager)
    {
        const string id = "test.script-settings";
        var path = Path.Combine((string)manager.ScriptsDirectory, "settings-test.cs");
        var settingsType = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!;
        dynamic settings = settingsType.GetProperty("Instance")!.GetValue(null)!;
        var previousEnabled = (bool)settings.EnableMechanics;
        var originalClient = Svc.ClientState;
        var originalObjects = Svc.Objects;
        var prInstance = typeof(PromeRotation.Plugin).GetProperty("Instance")!;
        var originalPr = prInstance.GetValue(null);
        var managerType = ((object)manager).GetType();
        var getSettings = managerType.GetMethod("GetScriptSettings", Internal)!;
        var getMechanisms = managerType.GetMethod("GetScriptMechanisms", Internal)!;
        dynamic Find() => ((IEnumerable)manager.Scripts).Cast<dynamic>().Single(d => d.Metadata.Id == id);
        dynamic Store(dynamic descriptor) => getSettings.Invoke((object)manager, [(object)descriptor])!;
        string[] Mechanisms(dynamic descriptor) => ((IEnumerable<string>)getMechanisms.Invoke((object)manager, [(object)descriptor])!).ToArray();
        JsonElement Saved()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "MasterPlayer.json")));
            return document.RootElement.GetProperty("ScriptSettings").GetProperty(id).Clone();
        }
        try
        {
            // Inspection must work with no client, local player or running script.
            SetService(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => throw new InvalidOperationException("Preview accessed client state: " + name)));
            SetService(nameof(Svc.Objects), CatalogServiceProxy.Create<IObjectTable>((name, _) => throw new InvalidOperationException("Preview accessed live objects: " + name)));
            settings.EnableMechanics = false;
            File.WriteAllText(path, Source(id, false));
            manager.RefreshCatalog();
            Check(manager.ErrorMessage == "", (string)manager.ErrorMessage);
            dynamic descriptor = Find();
            var scriptType = ((object)descriptor.CreateInstance()).GetType();
            dynamic preview = Store(descriptor);
            Check((bool)preview.GetBool("Enabled") && (int)preview.GetInt("Counter") == 7,
                "Inspection must preserve defaults from Settings-backed and automatic properties.");
            Check(preview.RegisteredSettings.ContainsKey("Enabled") && Mechanisms(descriptor).SequenceEqual(["Original mechanism"]),
                "A disabled, unstarted script must expose settings and mechanism metadata.");
            Check((int)manager.Running.Count == 0 && (int)scriptType.GetField("Starts")!.GetValue(null)! == 0,
                "Inspecting metadata must not start the script or subscribe its events.");
            preview.SetPropertyValue("Enabled", false);
            preview.SetPropertyValue("Counter", 9);
            Check(!Saved().GetProperty("Enabled").GetBoolean() && Saved().GetProperty("Counter").GetInt32() == 9,
                "Editing an unstarted script must immediately persist to disk.");
            Check((int)manager.Running.Count == 0 && (int)scriptType.GetField("Starts")!.GetValue(null)! == 0,
                "Editing settings must not start the script.");
            Pass("disabled metadata inspection, true defaults and immediate persistence without a runtime host");

            File.WriteAllText(path, Source(id, true));
            manager.ReloadScripts();
            descriptor = Find();
            dynamic reloaded = Store(descriptor);
            Check(!(bool)reloaded.GetBool("Enabled") && (int)reloaded.GetInt("Counter") == 9,
                "Reload must preserve saved values.");
            Check(reloaded.RegisteredSettings.ContainsKey("Extra") && (bool)reloaded.GetBool("Extra")
                && Mechanisms(descriptor).SequenceEqual(["Updated mechanism", "Added mechanism"]),
                "Same-GUID reload must expose new settings and mechanisms with their defaults.");
            preview.SetPropertyValue("Counter", 100);
            Check(Saved().GetProperty("Counter").GetInt32() == 9 && (int)reloaded.GetInt("Counter") == 9,
                "A retired preview must not overwrite the refreshed settings.");
            using (var anotherManager = (IDisposable)Activator.CreateInstance(managerType)!)
            {
                dynamic another = anotherManager;
                another.RefreshCatalog();
                var anotherDescriptor = ((IEnumerable)another.Scripts).Cast<dynamic>().Single(d => d.Metadata.Id == id);
                dynamic persisted = getSettings.Invoke(anotherManager, [(object)anotherDescriptor])!;
                Check(!(bool)persisted.GetBool("Enabled") && (int)persisted.GetInt("Counter") == 9,
                    "A new manager must load persisted settings.");
                another.Dispose();
                persisted.SetPropertyValue("Counter", 101);
                Check(Saved().GetProperty("Counter").GetInt32() == 9, "A disposed manager must not save stale values.");
            }
            Pass("same-GUID reload refreshes settings and mechanisms; retired stores cannot save");

            SetService(nameof(Svc.ClientState), CatalogServiceProxy.Create<IClientState>((name, _) => name switch
            {
                "add_TerritoryChanged" or "remove_TerritoryChanged" => null,
                "get_TerritoryType" => 0u,
                _ => throw new NotSupportedException(name),
            }));
            using var drawing = new PromeRotation.Spatial.Drawing.DrawManager();
            var pr = RuntimeHelpers.GetUninitializedObject(typeof(PromeRotation.Plugin));
            typeof(PromeRotation.Plugin).GetField("_drawManager", Internal)!.SetValue(pr, drawing);
            prInstance.SetValue(null, pr);
            settings.EnableMechanics = true;
            var routerType = assembly.GetType("MasterPlayer.Replay.ReplayScriptEventRouter")!;
            using var router = (IDisposable)Activator.CreateInstance(routerType, [null])!;
            var start = managerType.GetMethod("StartScript", Internal)!;
            Check((bool)start.Invoke((object)manager, [id, router])!, (string)manager.ErrorMessage);
            dynamic run = manager.Running[id];
            Check(!(bool)run.Script.StartedWithEnabled && (int)run.Script.StartedWithCounter == 9,
                "Replay start must use the values edited before running.");
            Check(ReferenceEquals((object)Store(descriptor), (object)run.Settings), "The running panel must use the runtime store.");
            Store(descriptor).SetPropertyValue("Enabled", true);
            Check((bool)run.Script.Enabled, "Editing the running panel must immediately update script reads.");
            reloaded.SetPropertyValue("Counter", 102);
            Check((int)run.Settings.GetInt("Counter") == 9 && Saved().GetProperty("Counter").GetInt32() == 9,
                "Starting must retire the old preview store.");
            routerType.GetMethod("InvokeNamed", Internal)!.Invoke(router, ["BattleStarted", Array.Empty<object>()]);
            Check((int)run.Script.Events == 1, "OnStart must subscribe only after an actual start.");
            run.Settings.SetPropertyValue("Counter", 12);
            manager.StopScript(id);
            dynamic stopped = Store(descriptor);
            Check((bool)stopped.GetBool("Enabled") && (int)stopped.GetInt("Counter") == 13,
                "Stopping must expose the latest values, including changes made in OnStop.");
            run.Settings.SetPropertyValue("Counter", 103);
            Check(Saved().GetProperty("Counter").GetInt32() == 13, "A stopped runtime store must not overwrite the panel.");
            Pass("replay start, running edits and stop share persisted values and retire obsolete stores");

            SetService(nameof(Svc.Objects), CatalogServiceProxy.Create<IObjectTable>((name, _) => name == "get_LocalPlayer" ? null : throw new NotSupportedException(name)));
            Check((bool)manager.StartScript(id), (string)manager.ErrorMessage);
            run = manager.Running[id];
            Check((bool)run.Script.StartedWithEnabled && (int)run.Script.StartedWithCounter == 13,
                "Live start must also use the saved values.");
            Pass("live start uses the same persisted configuration");

            manager.SetScriptEnabled(id, true);
            File.WriteAllText(path, Source(id, false));
            manager.ReloadScripts();
            dynamic replacement = manager.Running[id];
            descriptor = Find();
            Check(!ReferenceEquals((object)run.Script, (object)replacement.Script)
                && ((object)run.Script).GetType().Assembly != ((object)replacement.Script).GetType().Assembly,
                "Reload must recompile and replace the entire running script, not just its panel metadata.");
            Check((bool)((object)run.Context).GetType().GetField("_disposed", Internal)!.GetValue((object)run.Context)!,
                "Reload must dispose the old runtime context and event subscriptions.");
            Check((int)replacement.Script.StartedWithCounter == 14 && !Store(descriptor).RegisteredSettings.ContainsKey("Extra")
                && Mechanisms(descriptor).SequenceEqual(["Original mechanism"]),
                "The replacement logic and panel schema must both come from the new source, with saved values retained.");
            run.Settings.SetPropertyValue("Counter", 105);
            Check(Saved().GetProperty("Counter").GetInt32() == 14, "The old running store must not write after reload.");
            File.WriteAllText(path, Source(id, true, 999));
            manager.ReloadScripts();
            descriptor = Find();
            Check(!manager.Running.ContainsKey(id) && Store(descriptor).RegisteredSettings.ContainsKey("Extra"),
                "Reload must honor the new source's territory metadata instead of starting it in the old territory.");
            Pass("running reload replaces source, logic, subscriptions and metadata; new territory rules apply");

            dynamic removed = Store(descriptor);
            File.Delete(path);
            manager.RefreshCatalog();
            removed.SetPropertyValue("Counter", 104);
            Check(Saved().GetProperty("Counter").GetInt32() == 15, "A deleted descriptor must retire its preview store.");
            File.WriteAllText(path, Source(id, false));
            manager.RefreshCatalog();
            descriptor = Find();
            dynamic restored = Store(descriptor);
            Check(!restored.RegisteredSettings.ContainsKey("Extra") && Mechanisms(descriptor).SequenceEqual(["Original mechanism"]),
                "Direct catalog refresh after removal must use the replacement descriptor's schema.");
            Check((int)restored.GetInt("Counter") == 15, "Removing and restoring a source must preserve its saved values.");
            Pass("direct catalog refresh and source removal invalidate metadata and stale writes");
        }
        finally
        {
            manager.StopScript(id);
            File.Delete(path);
            manager.RefreshCatalog();
            settings.ScriptSettings.Remove(id);
            settings.ScriptEnabled.Remove(id);
            settings.EnableMechanics = previousEnabled;
            SetService(nameof(Svc.ClientState), originalClient);
            SetService(nameof(Svc.Objects), originalObjects);
            prInstance.SetValue(null, originalPr);
        }
    }

    private static string Source(string id, bool updated, uint? territory = null) => $$"""
        using System.Collections.Generic;
        using MasterPlayer.Scripting;
        [MechanicScript("Settings regression", {{(territory is uint t ? $"new uint[] {{ {t} }}" : "new uint[0]")}}, {{JsonSerializer.Serialize(id)}}, "1.0", "test")]
        public sealed class SettingsScript : MasterPlayerScript
        {
            public static int Starts;
            public int Events;
            public bool StartedWithEnabled;
            public int StartedWithCounter;
            [UserSetting("Enabled by default")]
            public bool Enabled { get => Settings.GetBool(nameof(Enabled), true); set => Settings.Set(nameof(Enabled), value); }
            [UserSetting("Counter")]
            public int Counter { get; set; } = 7;
            {{(updated ? "[UserSetting(\"New setting\")] public bool Extra { get; set; } = true;" : "")}}
            public override IEnumerable<string> GetMechanisms() => {{(updated ? "new[] { \"Updated mechanism\", \"Added mechanism\" }" : "new[] { \"Original mechanism\" }")}};
            protected override void OnStart()
            {
                Starts++;
                StartedWithEnabled = Enabled;
                StartedWithCounter = Settings.GetInt(nameof(Counter));
                OnBattleStarted(() => Events++);
            }
            protected override void OnStop() => Settings.Set(nameof(Counter), Settings.GetInt(nameof(Counter)) + 1);
        }
        """;

    private static void SetService(string name, object? value) => typeof(Svc).GetProperty(name)!.SetValue(null, value);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pass(string name) => Console.WriteLine("PASS script settings: " + name);
}

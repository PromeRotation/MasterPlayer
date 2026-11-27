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
    internal const int BUILT_IN_COUNT = 328;
    public static void Run(string pluginPath, IReadOnlyList<MetadataReference> references, bool scriptsOnly = false, bool dungeonsOnly = false)
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
                Check(Scripts().Length == BUILT_IN_COUNT, "An override must replace its built-in entry.");
                Check(Find(id).Source.ToString() == source, "The configuration directory must win.");
            }
            void Pass(string name) => Console.WriteLine($"PASS script catalog: {name}");

            Refresh();
            Check(Scripts().Length == BUILT_IN_COUNT && Scripts().All(d => d.Source.ToString() == "BuiltIn"),
                $"An empty configuration must discover {BUILT_IN_COUNT} built-in scripts, excluding examples; found {Scripts().Length}.");
            foreach (var (floor, territory) in new[] { (10, 1281u), (20, 1282u), (30, 1283u), (40, 1284u), (50, 1285u),
                (60, 1286u), (70, 1287u), (80, 1288u), (90, 1289u), (99, 1290u) })
            {
                var id = $"promeradar.pt{floor}";
                uint[] expectedTerritories = floor == 99 ? [1290u, 1333u] : [territory];
                Check(((uint[])Find(id).Metadata.Territorys).SequenceEqual(expectedTerritories), "Pilgrims scripts must use real territory IDs, including Final Verse normal for PT99, not CFC IDs.");
                Check((bool)manager.IsScriptEnabled(id), "Migrated Pilgrims scripts are enabled by default.");
            }
            foreach (var (id, territory) in new[] { ("sastasha", 1036u), ("tamtara", 1037u), ("copperbell", 1038u) })
                Check(((uint[])Find("masterplayer.dungeon." + id).Metadata.Territorys).SequenceEqual([territory]), "Dungeon territory identity must match the remade duty.");
            foreach (var (suffix, territory) in new[] { ("vanguard", 1248u), ("aquarius", 1248u), ("groundskeeper", 1248u),
                ("despot", 1248u), ("serpent", 1178u), ("angelosfirst", 1178u), ("angelossecond", 1178u),
                ("detector", 1304u), ("jack", 1304u), ("kraken", 1304u), ("lions", 1054u), ("rhalgr", 1054u), ("heavyartillery", 917u), ("lightartillery", 917u),
                ("cuchulainn", 508u), ("forgall", 556u), ("headstone", 556u), ("ferdiadhollow", 627u), ("protoultima", 627u), ("diabolos", 627u), ("paradigmtrash", 966u), ("cetus", 508u), ("echidna", 508u), ("calofisteri", 556u), ("ozma", 556u), ("irminsulsawtooth", 508u), ("arachneeve", 556u), ("deathgazehollow", 627u) })
            {
                var id = "masterplayer.alliance." + suffix;
                Check(((uint[])Find(id).Metadata.Territorys).SequenceEqual([territory]) && (bool)manager.IsScriptEnabled(id),
                    "Alliance trash exact territory and empty configuration default " + suffix);
                manager.SetScriptEnabled(id, false); Refresh();
                Check(!(bool)manager.IsScriptEnabled(id), "Alliance explicit disable survives refresh " + suffix);
                manager.SetScriptEnabled(id, true);
            }
            foreach (var (floor, territory) in new[] { (10, 770u), (20, 771u), (30, 772u), (40, 782u), (50, 773u),
                (60, 783u), (70, 774u), (80, 784u), (90, 775u) })
                Check(((uint[])Find($"masterplayer.hoh.{floor}").Metadata.Territorys).SequenceEqual([territory])
                    && (bool)manager.IsScriptEnabled($"masterplayer.hoh.{floor}"),
                    "HoH scripts must be enabled at their true territories, independently of CFC and ordinary floors.");
            foreach (var id in new[] { "masterplayer.hoh.floors", "masterplayer.eureka.floors", "masterplayer.pilgrims.floors" })
            {
                Check((bool)manager.IsScriptEnabled(id), $"Native floor protection must start with an empty configuration: {id}.");
                manager.SetScriptEnabled(id, false);
                Refresh();
                Check(!(bool)manager.IsScriptEnabled(id), $"Explicit user disable must survive catalog refresh: {id}.");
                manager.SetScriptEnabled(id, true);
            }
            foreach (var (name, territory) in new[] { ("thewingedterror", 1252u), ("crescentgremlin", 1346u),
                ("crescentfan", 1346u), ("eyetoeye", 1346u), ("demimedusa", 1346u),
                ("arottenaffair", 1346u), ("adelicatebalance", 1252u), ("kingofthecrescent", 1252u),
                ("madmudarch", 1252u), ("allureoftheoccult", 1346u), ("anunendingduty", 1252u),
                ("galeforceencounter", 1346u), ("relicicewolf", 1346u), ("thunderregnum", 1346u),
                ("apryingeye", 1252u), ("servingdarkness", 1252u) })
            {
                var id = $"masterplayer.fate.{name}";
                Check(((uint[])Find(id).Metadata.Territorys).SequenceEqual([territory])
                    && (bool)manager.IsScriptEnabled(id), $"Crescent FATE default and metadata: {id}.");
                manager.SetScriptEnabled(id, false);
                Refresh();
                Check(!(bool)manager.IsScriptEnabled(id), $"Crescent explicit disable survives refresh: {id}.");
                manager.SetScriptEnabled(id, true);
            }
            var race = Find("masterplayer.goldsaucer.blunderville");
            Check(((uint[])Find("masterplayer.foray.hydatos").Metadata.Territorys).SequenceEqual([827u])
                && (bool)manager.IsScriptEnabled("masterplayer.foray.hydatos"), "Hydatos metadata/default enable.");
            foreach (var (id, territory, other) in new[]
            {
                ("masterplayer.foray.bozja.south", 920u, "masterplayer.foray.zadnor"),
                ("masterplayer.foray.zadnor", 975u, "masterplayer.foray.bozja.south"),
                ("masterplayer.foray.delubrum.normal", 936u, "masterplayer.foray.zadnor"),
                ("masterplayer.foray.delubrum.savage", 937u, "masterplayer.foray.delubrum.normal"),
            })
            {
                Check(((uint[])Find(id).Metadata.Territorys).SequenceEqual([territory])
                    && (bool)manager.IsScriptEnabled(id), "Bozja/Zadnor empty configuration default and exact territory: " + id);
                manager.SetScriptEnabled(id, false);
                Refresh();
                Check(!(bool)manager.IsScriptEnabled(id) && (bool)manager.IsScriptEnabled(other),
                    "Bozja/Zadnor independent explicit disable survives refresh: " + id);
                manager.SetScriptEnabled(id, true);
            }
            foreach (var (name, territory) in new[] { ("spurge", 562u), ("kirtimukha", 599u), ("nybeth", 597u) })
                Check(((uint[])Find($"masterplayer.palace.{name}").Metadata.Territorys).SequenceEqual([territory])
                    && (bool)manager.IsScriptEnabled($"masterplayer.palace.{name}"), "Palace plant true territory/default enabled.");
            Check(((uint[])race.Metadata.Territorys).SequenceEqual([1165u]), "Blunderville must activate in the actual minigame territory.");
            Check(((IEnumerable<string>)race.CreateInstance().GetMechanisms()).Count() == 7, "Expose six obstacle groups and the race controls for the minigame.");
            var castrum = Find("masterplayer.dungeon.castrum-abania");
            Check(((uint[])castrum.Metadata.Territorys).SequenceEqual([1145u])
                && (bool)manager.IsScriptEnabled("masterplayer.dungeon.castrum-abania"),
                "Castrum Abania must activate in territory 1145 and be enabled by default.");
            Check(Scripts().All(d => d.CreateInstance().GetType().Assembly == assembly), "Factories must use the loaded plugin assembly.");
            ReplayPartyCaptureRegression.Run(assembly);
            NativeActionEffectRegression.Run(assembly, config.FullName);
            foreach (var (id, territory) in new[] { ("halatali", 1245u), ("totorak", 1039u), ("haukke", 1040u), ("brayflox", 1041u),
                ("qarn", 1267u), ("cutters-cry", 1303u), ("stone-vigil", 1042u), ("dzemael", 1330u), ("aurum-vale", 1331u),
                ("castrum-meridianum", 1043u), ("praetorium", 1044u), ("wanderers-palace", 159u), ("amdapor-keep", 167u),
                ("pharos-sirius", 160u), ("snowcloak", 1062u), ("sastasha-hard", 387u), ("keeper-of-the-lake", 1063u),
                ("sohm-al", 1064u), ("aery", 1065u), ("vault", 1066u), ("gubal-library", 1109u), ("research-facility", 1110u),
                ("fractal-continuum", 430u), ("arboretum", 511u), ("antitower", 1111u), ("sohr-khai", 1112u), ("xelphatol", 1113u),
                ("baelsars-wall", 1114u), ("sirensong-sea", 1142u), ("shisui", 1367u), ("bardam", 1143u), ("doma-castle", 1144u),
                ("ala-mhigo", 1146u), ("skalla", 1172u), ("burn", 1173u), ("ghimlyt", 1174u), ("holminster", 837u), ("dohn-mheg", 821u), ("qitana", 823u), ("malikah", 836u), ("mt-gulg", 822u), ("amaurot", 838u), ("twinning", 840u), ("anyder", 841u), ("cosmos", 884u) })
                Check(((uint[])Find("masterplayer.dungeon." + id).Metadata.Territorys).SequenceEqual([territory])
                    && (bool)manager.IsScriptEnabled("masterplayer.dungeon." + id), "Migrated native dungeon discovery/default enable failed: " + id);
            if (dungeonsOnly)
            {
                CheckLocalSafeZone(assembly, (object)manager, true);
                return;
            }
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
            Check(Scripts().Length == BUILT_IN_COUNT && Find(builtInId).Source.ToString() == "BuiltIn", "Removing an override must restore the built-in.");
            Pass("compile failure reports an error; removal restores the built-in");

            File.WriteAllText(sourcePath, Source("Independent external script", "external.independent"));
            Refresh();
            Check(Scripts().Length == BUILT_IN_COUNT + 1 && Find(builtInId).Source.ToString() == "BuiltIn", "Unrelated scripts must coexist.");
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
            settings.EnableMechanics = false;
            dynamic definition = Activator.CreateInstance(assembly.GetType("MasterPlayer.Scripting.Visual.VisualScriptDefinition")!)!;
            definition.Name = builtInName;
            definition.TerritoryId = 1339;
            string visualId = definition.Id;
            var visualPath = Path.Combine((string)manager.ScriptsDirectory, "override.mechanic.json");
            var save = managerType.GetMethod("SaveVisualScript", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var delete = managerType.GetMethod("DeleteVisualScript", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var getSettings = managerType.GetMethod("GetScriptSettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
            string json = (string)save.Invoke((object)manager, [visualPath, (object)definition, null])!;
            ExpectOverride(visualId, "Visual");
            Refresh();
            ExpectOverride(visualId, "Visual");
            Pass("visual save and directory scan both override by name");

            dynamic visualPreview = getSettings.Invoke((object)manager, [(object)Find(visualId)])!;
            visualPreview.Set("preview-marker", 1);
            definition.Name = "Renamed visual script";
            json = (string)save.Invoke((object)manager, [visualPath, (object)definition, json])!;
            visualPreview.Set("preview-marker", 2);
            Check((int)settings.ScriptSettings[visualId]["preview-marker"].GetValue<int>() == 1,
                "Saving a visual replacement must retire its previous settings preview without a full reload.");
            Check(Scripts().Length == BUILT_IN_COUNT + 1 && Find(builtInId).Source.ToString() == "BuiltIn", "Renaming away must restore the built-in immediately.");
            definition.Name = builtInName;
            json = (string)save.Invoke((object)manager, [visualPath, (object)definition, json])!;
            ExpectOverride(visualId, "Visual");
            visualPreview = getSettings.Invoke((object)manager, [(object)Find(visualId)])!;
            delete.Invoke((object)manager, [(object)Find(visualId), json]);
            visualPreview.Set("preview-marker", 3);
            Check(!settings.ScriptSettings.ContainsKey(visualId), "A deleted visual preview must not recreate removed settings.");
            Check(Scripts().Length == BUILT_IN_COUNT && Find(builtInId).Source.ToString() == "BuiltIn", "Deleting a visual override must restore the built-in immediately.");
            Check(manager.ErrorMessage == "", (string)manager.ErrorMessage);
            Pass("visual rename and deletion restore built-ins without a full reload");
            CheckLocalSafeZone(assembly, (object)manager);
            PilgrimsTentacleRegression.Run(assembly);
            PilgrimsTrashFacingRegression.Run(assembly);
            if (scriptsOnly) return;
            IntegrationRegression.Run(assembly);
            SkillSequenceRuntimeRegression.Run(assembly);
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

    private static void CheckLocalSafeZone(Assembly assembly, object manager, bool dungeonsOnly = false)
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
            if (dungeonsOnly)
            {
                CheckReplaySession(assembly, manager, runtime, plugin, true);
                Check(moveCalls == 0, "Native dungeon replay must never execute movement.");
                return;
            }
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
            ScriptMovementRegression.Run(assembly, manager, (object)runtime);
            Check(moveCalls == 0, "Script holds must not issue GreenMove commands.");
            Console.WriteLine("PASS production local runtime: script registration, cleanup and replay transitions");
            PilgrimsFollowUpRegression.Run(assembly, (object)runtime);
            PilgrimsFollowUpRegression.Run(assembly, (object)runtime, "dive");
            PilgrimsFollowUpRegression.Run(assembly, (object)runtime, "water");
            PilgrimsTrashCoverageRegression.Run(assembly, (object)runtime);
            ScriptSettingsRegression.Run(assembly, manager);
            CheckReplaySession(assembly, manager, runtime, plugin);
            Check(moveCalls == 0, "Recorded playback must never invoke movement.");
        }
        finally { instance.SetValue(null, previous); }
    }

    private static void CheckReplaySession(Assembly assembly, dynamic manager, dynamic runtime, object plugin, bool dungeonsOnly = false)
    {
        var originalClient = Svc.ClientState;
        var originalObjects = Svc.Objects;
        var prInstance = typeof(PromeRotation.Plugin).GetProperty("Instance")!;
        var originalPr = prInstance.GetValue(null);
        dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var enabled = (bool)settings.EnableMechanics;
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
            // Replay exercises registration and owner cleanup without creating a game renderer.
            var worldDrawingType = assembly.GetType("MasterPlayer.Drawing.WorldDrawing")!;
            var worldDrawing = RuntimeHelpers.GetUninitializedObject(worldDrawingType);
            worldDrawingType.GetField("<Manager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(worldDrawing, draw);
            plugin.GetType().GetField("worldDrawing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, worldDrawing);
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
            settings.EnableMechanics = true;
            if (!Environment.GetCommandLineArgs().Contains("--pilgrims-replay"))
                NativeDungeonReplayRegression.Run(assembly, manager, runtime, controller, file);
            if (dungeonsOnly)
            {
                Check(liveObjectReads == 0, "Native dungeon replay read live objects.");
                return;
            }
            manager.SetScriptEnabled("masterplayer.dungeon.copperbell", true);
            File.WriteAllText(file, """
                {"v":2,"territory":1038,"startedUtc":"2026-09-21T01:00:00Z","pluginVersion":"test"}
                {"t":0,"kind":"WorldFrame","data":{"Full":true,"Territory":1038,"LocalPlayerId":2,"Actors":[{"Id":1,"Oid":14448,"HitboxRadius":3,"InCombat":true,"Targetable":true,"Position":{"X":27,"Y":0,"Z":114}},{"Id":2,"Kind":1,"Position":{"X":27,"Y":0,"Z":114}}]}}
                {"t":1,"kind":"WorldFrame","data":{"Territory":1038,"LocalPlayerId":2,"Actors":[{"Id":1,"Oid":14448,"HitboxRadius":3,"InCombat":true,"Targetable":true,"Position":{"X":27,"Y":0,"Z":114},"Cast":{"ActionId":28462,"Location":{"X":3,"Y":0,"Z":4},"Rotation":1.5707964,"StartedAt":"2026-09-21T01:00:01Z","FinishAt":"2026-09-21T01:00:05.3Z"}}]}}
                {"t":1,"kind":"CastStart","type":"PromeRotation.LogSystem.LogSystemCastStartEvent","packetFacing":1.5707964,"data":{"SourceId":1,"ActionId":28462,"CastTime":4,"Timestamp":"2026-09-21T01:00:01Z","EffectPosition":{"X":3,"Y":0,"Z":4}}}
                {"t":6,"kind":"WorldFrame","data":{"Territory":1038,"LocalPlayerId":2,"Removed":[1]}}
                """);
            int Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count();
            object[] AllPreviews() => ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().ToArray();
            bool EnemyOutline(object preview) => ((string)preview.GetType().GetProperty("Id")!.GetValue(preview)!)
                .Contains("early-arr/enemy/", StringComparison.Ordinal);
            int Previews() => AllPreviews().Count(p => !EnemyOutline(p));
            Check(controller.Load(file), (string)controller.StatusMessage);
            settings.TerritoryEnabled[1038u] = false;
            controller.Play();
            Check(controller.ReplayScriptCount == 0 && manager.Running.Count == 0 && Dangers() == 0,
                "A disabled recording territory ran script startup before the initial world frame.");
            controller.StopSession();
            settings.TerritoryEnabled.Remove(1038u);
            controller.Play();
            Check(controller.ReplayScriptCount == 1 && controller.World.HasSnapshot, (string)manager.ErrorMessage);
            controller.Update(2f);
            Check(controller.Clock == 2 && Dangers() == 1 && Previews() == 1,
                $"Production playback did not register the recorded AOE: clock={controller.Clock}, danger={Dangers()}, previews={Previews()}, status={controller.StatusMessage}, errors={manager.ErrorMessage}, ids={string.Join(',', AllPreviews().Select(p => p.GetType().GetProperty("Id")!.GetValue(p)))}.");
            Check(AllPreviews().Count(EnemyOutline) == 1
                && AllPreviews().Where(EnemyOutline).All(p => (bool)p.GetType().GetProperty("OutlineOnly")!.GetValue(p)!),
                "Copperbell production replay did not publish the native boss outline.");
            var pausedTime = (DateTime)runtime.CurrentTime;
            controller.Pause();
            controller.Update(30f);
            manager.Update(30f);
            void TickRuntime() => ((object)runtime).GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke((object)runtime, [1f, false]);
            TickRuntime();
            foreach (var preserveTerrain in new[] { false, true })
                plugin.GetType().GetMethod("ClearLiveSafeZone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, [preserveTerrain]);
            Check(runtime.CurrentTime == pausedTime && Dangers() == 1 && Previews() == 1, "Paused production replay changed the world or expired its output.");
            Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null, "A live lifecycle event cleared the replay arena.");
            settings.TerritoryEnabled[1038u] = false;
            manager.Update(0f);
            Check(manager.Running.Count == 0 && Dangers() == 0 && Previews() == 0,
                "Disabling a paused recording territory retained script output.");
            controller.Update(0f);
            settings.TerritoryEnabled.Remove(1038u);
            controller.Play(); controller.Seek(2d); controller.Pause();
            Check(Dangers() == 1 && Previews() == 1, "Re-enabling the recording territory did not rebuild replay output.");
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
            var previousMargin = (float)settings.DodgeDangerMargin;
            settings.DodgeDangerMargin = 3f;
            try
            {
                foreach (var (territory, oid, action, radius) in new[] { (1281, 0x460D, 44057, 15f), (1283, 0x4609, 39642, 14f), (1285, 0x4872, 43534, 6f) })
                {
                    settings.DodgeDangerMargin = 3f;
                    File.WriteAllText(file, $$$$"""
                        {"v":2,"territory":{{{{territory}}}},"startedUtc":"2026-09-21T01:00:00Z","pluginVersion":"pilgrims-test"}
                        {"t":0,"kind":"WorldFrame","data":{"Full":true,"Territory":{{{{territory}}}},"LocalPlayerId":2,"Actors":[{"Id":1,"Oid":{{{{oid}}}},"Hp":1000,"MaxHp":1000,"InCombat":true,"Targetable":true,"Position":{"X":-300,"Y":0,"Z":-300}},{"Id":2,"Kind":1,"Position":{"X":-300,"Y":0,"Z":-300}}]}}
                        {"t":1,"kind":"CastStart","type":"PromeRotation.LogSystem.LogSystemCastStartEvent","packetFacing":0,"data":{"SourceId":1,"ActionId":{{{{action}}}},"CastTime":4,"Timestamp":"2026-09-21T01:00:01Z","EffectPosition":{"X":-300,"Y":0,"Z":-300}}}
                        {"t":10,"kind":"WorldFrame","data":{"Territory":{{{{territory}}}},"LocalPlayerId":2,"Removed":[1]}}
                        """);
                    Check(controller.Load(file), (string)controller.StatusMessage);
                    controller.Play(); controller.Update(2f); controller.Pause();
                    Check(controller.ReplayScriptCount == 2 && manager.Running.ContainsKey("masterplayer.pilgrims.floors")
                        && Dangers() == 1 && Previews() == 1,
                        $"Production Pilgrims Boss and Floor must coexist with one exact AOE and preview: scripts={controller.ReplayScriptCount}, dangers={Dangers()}, previews={Previews()}.");
                    dynamic danger = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>().Single();
                    Check((bool)danger.Geometry.Contains(new Vector3(-300 + radius - .1f, 0, -300), 0f)
                        && !(bool)danger.Geometry.Contains(new Vector3(-300 + radius + .1f, 0, -300), 0f),
                        "Pilgrims script output must retain the physical radius despite a nonzero DodgeDangerMargin.");
                    dynamic running = manager.Running[$"promeradar.pt{(territory - 1280) * 10}"];
                    object script = running.Script;
                    var hostProperty = script.GetType().GetProperty("Host")!;
                    var originalHost = hostProperty.GetValue(script)!;
                    var hostInterface = assembly.GetType("MasterPlayer.Scripting.IScriptHostContext")!;
                    var liveHost = typeof(CatalogServiceProxy).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
                        .MakeGenericMethod(hostInterface).Invoke(null, [(Func<string, object?[], object?>)((name, values) =>
                            name == "get_IsReplay" ? false : hostInterface.GetMethod(name)!.Invoke(originalHost, values))])!;
                    var runtimeSetting = (object)runtime.Setting;
                    var allowedProperty = runtimeSetting.GetType().GetProperty("MovementAllowed", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var wasAllowed = allowedProperty.GetValue(runtimeSetting);
                    try
                    {
                        hostProperty.SetValue(script, liveHost);
                        allowedProperty.SetValue(runtimeSetting, true);
                        foreach (var margin in new[] { .5f, 1f, 0f })
                        {
                            settings.DodgeDangerMargin = margin;
                            script.GetType().BaseType!.GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(script, [(object)runtime.CurrentTime]);
                            var movement = (object)runtime.MovementStatus;
                            dynamic options = movement.GetType().GetMethod("CaptureSolveOptions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(movement, null)!;
                            Check((float)options.HardSafetyMargin == margin && (float)options.ArenaSafetyMargin == 0f,
                                "Live Pilgrims planning must apply the configured safety margin exactly once and preserve the arena edge.");
                        }
                    }
                    finally
                    {
                        hostProperty.SetValue(script, originalHost);
                        allowedProperty.SetValue(runtimeSetting, wasAllowed);
                    }
                    controller.StopSession();
                    Check(Dangers() == 0 && Previews() == 0, "Pilgrims script stop must clear all output.");
                }
            }
            finally { settings.DodgeDangerMargin = previousMargin; }
            Console.WriteLine("PASS production Pilgrims: exact R15/R14/R6, live configured margins 0/0.5/1 applied once, original arena edges, replay previews and cleanup");
            using (var pt20 = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "PilgrimsTraverse", "pt20-bursts-200454.json"))))
            {
                foreach (var cycle in pt20.RootElement.GetProperty("cycles").EnumerateArray())
                {
                    var lines = new List<string>
                    {
                        """{"v":2,"territory":1282,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"pt20-regression"}""",
                        """{"t":0,"kind":"WorldFrame","data":{"Full":true,"Territory":1282,"LocalPlayerId":2,"Actors":[{"Id":1,"Oid":18524,"Hp":1000,"MaxHp":1000,"InCombat":true,"Position":{"X":-300,"Y":0,"Z":-300}},{"Id":2,"Kind":1,"Position":{"X":-286.03,"Y":0,"Z":-296.925}}]}}""",
                    };
                    foreach (var ev in cycle.GetProperty("events").EnumerateArray())
                    {
                        var time = 14 + ev.GetProperty("t").GetDouble();
                        var kind = ev.GetProperty("kind").GetString();
                        var data = new Dictionary<string, object?>
                        {
                            ["Timestamp"] = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc).AddSeconds(time),
                        };
                        if (kind is "status" or "statusRemove")
                        {
                            data["TargetId"] = 1;
                            data["StatusId"] = 2056;
                            data["Param"] = ev.GetProperty("param").GetUInt16();
                            data["Duration"] = 3;
                        }
                        else
                        {
                            var p = ev.GetProperty("position");
                            var position = new { X = p[0].GetSingle(), Y = p[1].GetSingle(), Z = p[2].GetSingle() };
                            data["SourceId"] = ev.GetProperty("source").GetUInt64();
                            data["ActionId"] = ev.GetProperty("action").GetUInt32();
                            data["SourcePosition"] = position;
                            data["EffectPosition"] = kind == "cast" ? position : new { X = 0f, Y = 0f, Z = 0f };
                            if (kind == "cast") data["CastTime"] = ev.GetProperty("seconds").GetSingle();
                            else data["GlobalSequence"] = ev.GetProperty("sequence").GetUInt32();
                        }
                        var eventName = kind switch { "cast" => "CastStart", "effect" => "ActionEffect", "status" => "StatusAdd", _ => "StatusRemove" };
                        lines.Add(JsonSerializer.Serialize(new { t = time, kind = eventName,
                            type = $"PromeRotation.LogSystem.LogSystem{eventName}Event", packetFacing = 0f, data }));
                    }
                    File.WriteAllText(file, string.Join('\n', lines));
                    Check(controller.Load(file), (string)controller.StatusMessage);
                    controller.Play(); controller.Seek(15.9d); controller.Pause();
                    Check(Dangers() == 4 && Previews() == 3, "PT20 must retain all four exact navigation circles and preview the next three bursts.");
                    foreach (var ev in cycle.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "effect"))
                    {
                        controller.Seek(14 + ev.GetProperty("t").GetDouble() + .001);
                        var expected = 43459 - ev.GetProperty("action").GetInt32();
                        Check(Dangers() == expected && Previews() == Math.Min(expected, 3),
                            "A self-target effect desynchronized production PT20 previews and unresolved dangers.");
                        object Bounds(object geometry) => geometry.GetType().GetMethod("GetBounds")!.Invoke(geometry, null)!;
                        var previewBounds = ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!)
                            .Cast<object>().Select(p => Bounds(p.GetType().GetProperty("Geometry")!.GetValue(p)!)).ToArray();
                        if (expected > 0)
                        {
                            var nextBursts = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>()
                                .OrderBy(a => ((IEnumerable)a.DangerWindows).Cast<dynamic>().Min(w => (DateTime)w.DangerFrom))
                                .Take(3).Select(a => Bounds((object)a.Geometry)).ToList();
                            foreach (var bounds in previewBounds)
                                Check(nextBursts.Remove(bounds), "PT20 preview must match one of the next three navigation circles exactly.");
                            Check(nextBursts.Count == 0, "PT20 omitted an exact preview from the next three navigation circles.");
                        }
                    }
                    controller.StopSession();
                    Check(Dangers() == 0 && Previews() == 0, "PT20 retained output after stopping replay.");
                }
            }
            Console.WriteLine("PASS production PT20: three recorded sequences retain full navigation and preview the next three exact circles");
            using (var pt50 = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "PilgrimsTraverse", "pt50-chase-155404.json"))))
            {
                foreach (var cycle in pt50.RootElement.GetProperty("cycles").EnumerateArray())
                {
                    object Pose(JsonElement p) => new { X = p[0].GetSingle(), Y = p[1].GetSingle(), Z = p[2].GetSingle() };
                    var lines = new List<string>
                    {
                        """{"v":2,"territory":1285,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"pt50-regression"}""",
                        JsonSerializer.Serialize(new { t = 0, kind = "WorldFrame", data = new { Full = true, Territory = 1285, LocalPlayerId = 100,
                            Actors = new object[] {
                                new { Id = 1, Oid = 0x4872, Hp = 1000, MaxHp = 1000, InCombat = true, Position = Pose(cycle.GetProperty("initial").GetProperty("boss")) },
                                new { Id = 100, Kind = 1, Position = Pose(cycle.GetProperty("initial").GetProperty("player")) } } } }),
                        """{"t":0.5,"kind":"TargetIcon","type":"PromeRotation.LogSystem.LogSystemTargetIconEvent","data":{"TargetId":100,"MarkerId":640}}""",
                    };
                    var state = cycle.GetProperty("arenaState").GetUInt32();
                    if (state != 0)
                        lines.Add(JsonSerializer.Serialize(new { t = .75, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                            data = new { Position = 1, Param1 = (ushort)state, Param2 = (ushort)(state >> 16) } }));
                    foreach (var ev in cycle.GetProperty("events").EnumerateArray())
                    {
                        var time = 1 + ev.GetProperty("t").GetDouble();
                        var kind = ev.GetProperty("kind").GetString();
                        if (kind == "frame")
                        {
                            var actor = ev.GetProperty("actor").GetInt32();
                            lines.Add(JsonSerializer.Serialize(new { t = time, kind = "WorldFrame", data = new { Territory = 1285, LocalPlayerId = 100,
                                Actors = new[] { new { Id = actor, Oid = actor == 1 ? 0x4872 : 0, Kind = actor == 1 ? 2 : 1,
                                    Hp = 1000, MaxHp = 1000, InCombat = true, Position = Pose(ev.GetProperty("position")) } } } }));
                            continue;
                        }
                        var data = new Dictionary<string, object?>
                        {
                            ["SourceId"] = 1, ["ActionId"] = ev.GetProperty("action").GetUInt32(),
                            ["EffectPosition"] = Pose(ev.GetProperty("position")),
                            ["SourcePosition"] = Pose(ev.GetProperty("sourcePosition")),
                        };
                        if (kind == "cast") { data["CastTime"] = ev.GetProperty("seconds").GetSingle(); data["TargetId"] = 1; }
                        else
                        {
                            data["GlobalSequence"] = ev.GetProperty("sequence").GetUInt32();
                            data["TargetId"] = ev.GetProperty("target").GetInt32();
                            data["TargetPosition"] = ev.GetProperty("targetPosition").ValueKind == JsonValueKind.Null ? null : Pose(ev.GetProperty("targetPosition"));
                        }
                        var eventName = kind == "cast" ? "CastStart" : "ActionEffect";
                        lines.Add(JsonSerializer.Serialize(new { t = time, kind = eventName, type = $"PromeRotation.LogSystem.LogSystem{eventName}Event", packetFacing = 0f, data }));
                    }
                    File.WriteAllText(file, string.Join('\n', lines));
                    Check(controller.Load(file), (string)controller.StatusMessage);
                    controller.Play(); controller.Pause();
                    var remaining = 4;
                    foreach (var ev in cycle.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "effect"))
                    {
                        controller.Seek(1 + ev.GetProperty("t").GetDouble() + .001);
                        --remaining;
                        Check(Dangers() == (remaining == 0 ? 0 : 1), $"PT50 cycle {cycle.GetProperty("castTime")} action {ev.GetProperty("action")}: expected {(remaining == 0 ? 0 : 1)} danger, got {Dangers()}, previews={Previews()}; old first-hit areas must be removed when switching to a preview.");
                        if (remaining == 0) continue;
                        dynamic danger = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>().Single();
                        dynamic bounds = danger.Geometry.GetBounds();
                        var origin = ev.GetProperty(ev.GetProperty("action").GetInt32() == 43534 ? "sourcePosition" : "position");
                        Check(Math.Abs((float)bounds.MinX - (origin[0].GetSingle() - 11)) < .001f
                            && Math.Abs((float)bounds.MaxX - (origin[0].GetSingle() + 11)) < .001f, "PT50 production escape must be centered on the actual previous hit with a raw R11 envelope.");
                        var circles = ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>()
                            .Select(p => p.GetType().GetProperty("Geometry")!.GetValue(p)!).Where(g => g.GetType().Name == "CircleGeometry").ToArray();
                        Check(circles.Count(g => Math.Abs((float)((dynamic)g).GetBounds().MaxX - (float)((dynamic)g).GetBounds().MinX - 12) < .001f) == 1,
                            "PT50 production preview must retain exactly one actual R6 circle.");
                    }
                    controller.StopSession();
                    Check(Dangers() == 0 && Previews() == 0, "PT50 retained chase output after stopping replay.");
                }
            }
            Console.WriteLine("PASS production PT50: three ARR sequences, R6 previews, R11 self-chase navigation, no stale first-hit areas and stop cleanup");
            using (var pt60 = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "PilgrimsTraverse", "pt60-spin-214526.json"))))
            {
                foreach (var cycle in pt60.RootElement.GetProperty("cycles").EnumerateArray())
                {
                    object Pose(JsonElement p) => new { X = p[0].GetSingle(), Y = p[1].GetSingle(), Z = p[2].GetSingle() };
                    var lines = new List<string>
                    {
                        """{"v":2,"territory":1286,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"pt60-regression"}""",
                        JsonSerializer.Serialize(new { t = 0, kind = "WorldFrame", data = new { Full = true, Territory = 1286, LocalPlayerId = 100,
                            Actors = new object[] {
                                new { Id = 1, Oid = 0x49DE, Hp = 1000, MaxHp = 1000, InCombat = true, Position = Pose(cycle.GetProperty("position")) },
                                new { Id = 100, Kind = 1, Position = Pose(cycle.GetProperty("initialPlayer")) } } } }),
                        JsonSerializer.Serialize(new { t = .1, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                            packetFacing = cycle.GetProperty("rotation").GetSingle(), data = new { SourceId = 1, TargetId = 1,
                                ActionId = cycle.GetProperty("action").GetUInt32(), CastTime = cycle.GetProperty("seconds").GetSingle(),
                                EffectPosition = Pose(cycle.GetProperty("position")) } }),
                    };
                    foreach (var effect in cycle.GetProperty("effects").EnumerateArray())
                        lines.Add(JsonSerializer.Serialize(new { t = .1 + effect.GetProperty("t").GetDouble(), kind = "ActionEffect",
                            type = "PromeRotation.LogSystem.LogSystemActionEffectEvent", data = new {
                                SourceId = effect.GetProperty("source").GetUInt64(), ActionId = 44909,
                                GlobalSequence = effect.GetProperty("sequence").GetUInt32(), SourceRotation = effect.GetProperty("rotation").GetSingle(),
                                SourcePosition = Pose(effect.GetProperty("position")) } }));
                    File.WriteAllText(file, string.Join('\n', lines));
                    Check(controller.Load(file), (string)controller.StatusMessage);
                    controller.Play(); controller.Seek(1d); controller.Pause();
                    Check(Dangers() == 21 && Previews() == 5, "PT60 must publish all 20 navigation pulses, the center constraint, and current/next cones on both sides.");
                    dynamic first = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<dynamic>()
                        .MinBy(a => ((IEnumerable)a.DangerWindows).Cast<dynamic>().Min(w => (DateTime)w.DangerFrom))!;
                    var firstAt = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc).AddSeconds(.1 + cycle.GetProperty("effects")[0].GetProperty("t").GetDouble());
                    Check(((IEnumerable)first.DangerWindows).Cast<dynamic>().Any(w => (DateTime)w.DangerFrom <= firstAt && (DateTime?)w.DangerUntil >= firstAt),
                        $"PT60 production timing misses {firstAt:O}; windows={string.Join(';', ((IEnumerable)first.DangerWindows).Cast<object>())}.");
                    foreach (var pair in cycle.GetProperty("effects").EnumerateArray().GroupBy(e => (e.GetProperty("sequence").GetUInt32() - 1) / 2))
                    {
                        controller.Seek(.1 + pair.Max(e => e.GetProperty("t").GetDouble()) + .001);
                        var remaining = 20 - ((int)pair.Key + 1) * 2;
                        Check(Dangers() == remaining + (remaining == 0 ? 0 : 1)
                            && Previews() == (remaining == 0 ? 0 : Math.Min(remaining, 4) + 1),
                            "PT60 production previews retain a resolved sweep or omit the next sweep.");
                    }
                    controller.StopSession();
                    Check(Dangers() == 0 && Previews() == 0, "PT60 retained output after stopping replay.");
                }
            }
            Console.WriteLine("PASS production PT60: both ARR sequences, calibrated first hit, current/next cone previews, strict center constraint, complete navigation and cleanup");
            Pt99BlazeCaptureRegression.Run(assembly, manager, runtime, controller, file);
            CastrumAbaniaReplayRegression.Run(assembly, manager, runtime, controller, file);
            File.WriteAllText(file, string.Join('\n', new[]
            {
                """{"v":2,"territory":1287,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"pt70-halo-regression"}""",
                """{"t":0,"kind":"WorldFrame","data":{"Full":true,"Territory":1287,"LocalPlayerId":100,"Actors":[{"Id":1,"Oid":18508,"Hp":1000,"MaxHp":1000,"InCombat":true,"Position":{"X":-300,"Y":0,"Z":-300}},{"Id":100,"Kind":1,"Position":{"X":-300,"Y":0,"Z":-300}},{"Id":20,"Oid":18509,"Position":{"X":-305,"Y":0,"Z":-300}},{"Id":25,"Oid":18509,"Position":{"X":-295,"Y":0,"Z":-300}}]}}""",
                """{"t":0.1,"kind":"CastStart","type":"PromeRotation.LogSystem.LogSystemCastStartEvent","packetFacing":0,"data":{"SourceId":1,"ActionId":43411,"CastTime":3,"EffectPosition":{"X":-300,"Y":0,"Z":-300}}}""",
                """{"t":9,"kind":"CastStart","type":"PromeRotation.LogSystem.LogSystemCastStartEvent","packetFacing":0,"data":{"SourceId":20,"ActionId":43417,"CastTime":2.1,"EffectPosition":{"X":-305,"Y":0,"Z":-300}}}""",
                """{"t":11.5,"kind":"ActionEffect","type":"PromeRotation.LogSystem.LogSystemActionEffectEvent","data":{"SourceId":20,"ActionId":43417,"GlobalSequence":1}}""",
                """{"t":11.5,"kind":"ActionEffect","type":"PromeRotation.LogSystem.LogSystemActionEffectEvent","data":{"SourceId":20,"ActionId":43417,"GlobalSequence":1}}""",
                """{"t":14,"kind":"ActionEffect","type":"PromeRotation.LogSystem.LogSystemActionEffectEvent","data":{"SourceId":25,"ActionId":43417,"GlobalSequence":2}}""",
            }));
            Check(controller.Load(file), (string)controller.StatusMessage);
            controller.Play(); controller.Seek(1d); controller.Pause();
            foreach (var (at, dangers, centerX) in new[] { (1d, 2, -305f), (9.1, 2, -305f), (11.501, 1, -295f) })
            {
                controller.Seek(at);
                Check(Dangers() == dangers && Previews() == 1, "PT70 must keep every future halo in navigation but preview only the next hit.");
                var preview = ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Single();
                dynamic geometry = preview.GetType().GetProperty("Geometry")!.GetValue(preview)!;
                Check(geometry.GetType().Name == "DonutGeometry" && Math.Abs((float)geometry.GetBounds().MinX - (centerX - 20)) < .001f,
                    "PT70 preview did not advance to the next sphere with its original outer radius.");
            }
            controller.Seek(14.001d);
            Check(Dangers() == 0 && Previews() == 0, "PT70 retained a resolved halo.");
            controller.Seek(1d); controller.StopSession();
            Check(Dangers() == 0 && Previews() == 0, "PT70 retained halo output after stopping replay.");
            Console.WriteLine("PASS production PT70: next halo preview, complete navigation, cast replacement, duplicate effects, seek and stop cleanup");
            PilgrimsPreviewReplayRegression.Run(controller, runtime, visibility, visibilityType, file);
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
            settings.TerritoryEnabled.Remove(1038u);
            settings.EnableMechanics = enabled;
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

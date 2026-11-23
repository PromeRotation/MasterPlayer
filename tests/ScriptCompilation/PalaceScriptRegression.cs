using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
            SystemLogParameterRegression.Run(assembly);
            RecorderSystemLogRegression.Run(assembly, config.FullName);
            var managerType = assembly.GetType("MasterPlayer.Scripting.ScriptManager", true)!;
            dynamic manager = Activator.CreateInstance(managerType)!;
            using var lifetime = (IDisposable)manager;
            manager.RefreshCatalog();
            if ((string)manager.ErrorMessage != "") throw new Exception((string)manager.ErrorMessage);
            var scripts = ((IEnumerable)manager.Scripts).Cast<dynamic>().ToArray();
            dynamic palace = scripts.Single(s => s.Metadata.Id == "promeradar.potd151-200");
            dynamic palaceSettings = managerType.GetMethod("GetScriptSettings", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke((object)manager, [(object)palace])!;
            palaceSettings.Set("AutoMoveTreasure", true);
            palaceSettings.Set("GoldCoffer", true);
            palaceSettings.Set("AutoPomander8", false);
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
            var rooms = new ushort[25];
            rooms[12] = 0x80;
            object FloorFrame(int floor, ushort[] map, bool full = false) => new
            {
                Full = full, Territory = 603, LocalPlayerId = 2, Removed = new ulong[] { 5 },
                DeepDungeon = new { DungeonId = 1, Floor = floor, Tileset = 0, PassageProgress = 0, Rooms = map,
                    PartyRooms = new Dictionary<ulong, int> { [2] = 12 } },
                Actors = new[] { new { Id = 2, Kind = 1, Position = new { X = 260.2039f, Y = 0, Z = -347.074f } } },
            };
            string Frame(double time, object data) => JsonSerializer.Serialize(new { t = time, kind = "WorldFrame", data });
            string SystemLog(int time, uint id) => JsonSerializer.Serialize(new
            {
                t = time, kind = "SystemLog", type = "MasterPlayer.Replay.ReplaySystemLogEvent",
                data = new { MessageId = id, Timestamp = new DateTime(2026, 11, 19, 0, 0, time, DateTimeKind.Utc) },
            });
            var connectedRooms = rooms.ToArray();
            connectedRooms[12] = 0x88;
            File.WriteAllLines(file,
            [
                """{"v":2,"territory":603,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"palace-floor-test"}""",
                Frame(0, FloorFrame(151, rooms, true)),
                SystemLog(1, 7255),
                SystemLog(2, 7248),
                JsonSerializer.Serialize(new
                {
                    t = 2.1, kind = "WorldFrame", data = new { Territory = 603, LocalPlayerId = 2,
                        Actors = new[] { new { Id = 5, Oid = 0x1EA08E, EventState = 7,
                            Position = new { X = 260f, Y = 0, Z = -347f } } } },
                }),
                Frame(3, FloorFrame(152, rooms)),
                Frame(4, FloorFrame(152, connectedRooms)),
                Frame(5, new { Territory = 603, LocalPlayerId = 2 }),
                Frame(6, new
                {
                    Territory = 603, LocalPlayerId = 2,
                    DeepDungeon = new { DungeonId = 1, Floor = 152, Tileset = 0, PassageProgress = 0, Rooms = connectedRooms, TrapsHidden = false },
                    Actors = new object[]
                    {
                        new { Id = 2, Kind = 1, Hp = 100, MaxHp = 100, Position = new { X = -300, Y = 0, Z = 300 } },
                        new { Id = 4, Oid = 0x1EA13E, Targetable = true, Position = new { X = -300, Y = 0, Z = 305 } },
                    },
                }),
                Frame(6.7, new
                {
                    Territory = 603, LocalPlayerId = 2,
                    DeepDungeon = new { DungeonId = 1, Floor = 152, Tileset = 0, PassageProgress = 0, Rooms = connectedRooms },
                    Actors = new[] { new { Id = 6, Oid = 0x1E858E, Position = new { X = -300, Y = 0, Z = 300 } } },
                }),
                Frame(6.9, new
                {
                    Territory = 603, LocalPlayerId = 2, Removed = new[] { 6 },
                    DeepDungeon = new { DungeonId = 1, Floor = 152, Tileset = 0, PassageProgress = 0, Rooms = connectedRooms },
                }),
                Frame(7, new
                {
                    Territory = 603, LocalPlayerId = 2,
                    DeepDungeon = new { DungeonId = 1, Floor = 152, Tileset = 0, PassageProgress = 0, Rooms = connectedRooms },
                    Actors = new[] { new { Id = 4, Oid = 0x1EA13E, Targetable = true, OpenedTreasure = true,
                        Position = new { X = -300, Y = 0, Z = 305 } } },
                }),
            ]);
            int Walls() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
            int Previews() => ((IEnumerable)visibilityType.GetMethod("GetPreviews")!.Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().Count();
            Check((bool)controller.Load(file), (string)controller.StatusMessage);
            controller.Play();
            controller.Seek(.5d);
            Check(Walls() == 4 && Dangers() > 0 && Previews() > 4, "Native floor failed to register map walls, trap dangers and dual-view previews.");
            controller.Seek(1.5d);
            Check(Walls() == 4 && Dangers() == 0 && Previews() == 4, "Recorded Safety cleared walls or retained hidden traps.");
            controller.Seek(2.5d);
            Check(Walls() == 0 && Dangers() == 0 && Previews() == 0, "Transfer recreated stale map output.");
            controller.Seek(3.5d);
            Check(Walls() == 4 && Dangers() > 0, "New floor failed to restore walls/traps.");
            controller.Seek(4d);
            Check(Walls() == 3, "Room connection update retained the closed east wall.");
            controller.Seek(1.5d);
            Check(Walls() == 4 && Dangers() == 0, "Reverse Seek retained later floor trap state.");
            controller.Seek(5d);
            Check(Walls() == 0 && Dangers() == 0 && Previews() == 0, "Missing map retained previous floor output.");
            int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
            controller.Seek(6.5d);
            Check(Goals() == 2 && Walls() == 3, $"Native chest proximity and interaction goals were not published from recorded object data (goals={Goals()}, walls={Walls()}, dangers={Dangers()}).");
            controller.Seek(6.8d);
            Check(Goals() == 1 && Dangers() > 0, "Coffer interaction approach remained active while the player stood in a dangerous pool.");
            controller.Seek(6.95d);
            Check(Goals() == 2, "Leaving the dangerous pool did not restore the coffer interaction approach.");
            controller.Seek(7d);
            Check(Goals() == 0, "Opened treasure did not clear the production navigation goal.");
            controller.Seek(6.5d);
            Check(Goals() == 2, "Reverse Seek retained opened-treasure state.");
            controller.StopSession();
            Check(Walls() == 0 && Previews() == 0 && Goals() == 0, "Floor script stop leaked owner output.");
            var manualRooms = new ushort[25];
            manualRooms[6] = 0x88;
            manualRooms[7] = 0x84;
            File.WriteAllLines(file,
            [
                """{"v":2,"territory":603,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"palace-commands"}""",
                Frame(0, new
                {
                    Full = true, Territory = 603, LocalPlayerId = 2,
                    DeepDungeon = new { DungeonId = 1, Floor = 151, Tileset = 0, PassageProgress = 0,
                        Rooms = manualRooms, PartyRooms = new Dictionary<ulong, int> { [2] = 6 } },
                    Actors = new[] { new { Id = 2, Kind = 1, Position = new { X = 260.2039f, Y = 0, Z = -347.074f } } },
                }),
                Frame(1, new { Territory = 603, LocalPlayerId = 2 }),
            ]);
            Check((bool)controller.Load(file), (string)controller.StatusMessage);
            controller.Play(); controller.Seek(.5d); controller.Pause();
            object runningPalace = ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
            var palaceType = assembly.GetType("MasterPlayer.Mechanics.PalaceOfTheDead.PalaceScript")!;
            void Command(string name, params object[] arguments)
                => palaceType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(runningPalace, arguments);
            Check(Goals() == 0, "Manual destination was active before selection.");
            Command("SelectFloorRoom", 7);
            Check(Goals() == 1, "The production room command did not publish a navigation goal.");
            Command("ClearFloorDestination");
            Check(Goals() == 0, "The production clear command retained the navigation goal.");
            var originalTraps = Dangers();
            Command("IgnoreNearestFloorTrap");
            Check(originalTraps > 0 && Dangers() < originalTraps, "The production trap command did not remove the nearest history location.");
            controller.StopSession();
            Check(Walls() == 0 && Previews() == 0 && Goals() == 0, "Manual floor commands leaked owner output after stop.");
            object PomanderFrame(int count, int floor = 151, bool full = false) => new
            {
                Full = full, Territory = 603, LocalPlayerId = 2,
                DeepDungeon = new
                {
                    DungeonId = 1, Floor = floor, Tileset = 0, Rooms = rooms, TrapsHidden = false,
                    PartyRooms = new Dictionary<ulong, int> { [2] = 12 },
                    Items = Enumerable.Range(0, 16).Select(i => new { PomanderId = i == 5 ? 8 : 0, Count = i == 5 ? count : 0, Flags = 1 }).ToArray(),
                },
                Actors = new object[]
                {
                    new { Id = 2, Kind = 1, Hp = 100, MaxHp = 100, InCombat = true,
                        Position = new { X = -300, Y = 0, Z = 300 },
                        Statuses = new[] { new { Id = 1087, ExpiresAt = "2026-11-19T00:01:00Z" } } },
                    new { Id = 4, Oid = 0x1EA13E, Targetable = true, Position = new { X = -300, Y = 0, Z = 302 } },
                },
            };
            File.WriteAllLines(file,
            [
                """{"v":2,"territory":603,"startedUtc":"2026-11-19T00:00:00Z","pluginVersion":"palace-pomander-test"}""",
                Frame(0, PomanderFrame(3, full: true)),
                """{"t":1,"kind":"SystemLog","type":"MasterPlayer.Replay.ReplaySystemLogEvent","data":{"MessageId":7222,"Arguments":[8],"Timestamp":"2026-11-19T00:00:01Z"}}""",
                Frame(2, PomanderFrame(2)),
                Frame(3, PomanderFrame(3)),
                SystemLog(4, 7248),
                Frame(5, PomanderFrame(3, 152)),
                Frame(6, new { Territory = 603, LocalPlayerId = 2 }),
            ]);
            Check((bool)controller.Load(file), (string)controller.StatusMessage);
            controller.Play(); controller.Seek(.5d); controller.Pause();
            runningPalace = ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
            int? Requested() => (int?)palaceType.GetProperty("RequestedPomanderSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runningPalace);
            Check(Requested() == null, "Full inventory used a pomander before overcap confirmation.");
            Command("SetAutoPomanderEnabled", 8u, true);
            controller.Play(); controller.Update(1f); controller.Pause();
            Check(Requested() == 5, "Recorded 7222 arguments did not select the mapped inventory slot.");
            ((dynamic)runningPalace).AutoMoveTreasure = false;
            ((dynamic)runningPalace).GoldCoffer = false;
            controller.Play(); controller.Update(.3f); controller.Pause();
            Check(Requested() == 5, "Confirmed AutoPoms incorrectly depended on treasure movement or gold settings.");
            controller.Play(); controller.Update(.7f); controller.Pause();
            Check(Requested() == null, "Inventory consumption retained an AutoPoms request.");
            controller.Play(); controller.Update(1f); controller.Pause();
            Check(Requested() == 5, "Confirmed coffer lost its pomander association.");
            controller.Play(); controller.Update(1f); controller.Pause();
            Check(Requested() == null, "Transfer retained a native pomander request.");
            controller.Play(); controller.Update(1f); controller.Pause();
            Check(Requested() == null, "New floor reused previous coffer contents.");
            ((dynamic)runningPalace).GoldCoffer = true;
            controller.Seek(.5d);
            runningPalace = ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
            Check(Requested() == null, "Reverse Seek retained future overcap knowledge.");
            controller.StopSession();
            Check(Requested() == null && Goals() == 0 && Previews() == 0, "Stop leaked AutoPoms state or owner output.");
            ((dynamic)runningPalace).AutoMoveTreasure = true;
            Command("SetAutoPomanderEnabled", 8u, false);
            Console.WriteLine("PASS production Palace AutoPoms: recorded typed 7222, exact mapped slot, independent settings/combat, inventory changes, transfer, reverse Seek, stop and replay execution isolation");
            Check(moves == 0 && objectReads == 0, "Recorded Palace replay crossed the live execution boundary");
            Console.WriteLine("PASS production Palace floor: native map walls, all-territory trap data, radar/world previews, Safety, transfer, map connections, coffers/opened treasure, manual room/trap commands, reverse Seek and owner cleanup");
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

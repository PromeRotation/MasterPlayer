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
            IntegrationRegression.Run(assembly);
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
            palaceSettings.Set("AutoPassage", true);
            palaceSettings.Set("FullClear", true);
            palaceSettings.Set("ClearBehavior", 3);
            palaceSettings.Set("AutoPomander8", true);
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
            var worldDrawingType = assembly.GetType("MasterPlayer.Drawing.WorldDrawing")!;
            var worldDrawing = RuntimeHelpers.GetUninitializedObject(worldDrawingType);
            worldDrawingType.GetField("<Manager>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(worldDrawing, draw);
            pluginType.GetField("worldDrawing", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, worldDrawing);
            pluginType.GetField("_safeZone", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, (object)runtime);
            pluginType.GetField("_scripts", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(plugin, (object)manager);
            pluginProperty.SetValue(null, plugin);
            runtime.Setting.EnableSafeZone = true;
            dynamic settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
            settings.EnableMechanics = true;
            settings.EnableSmartMovement = true;
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
            BossParity(assembly, (object)controller, (object)runtime, visibility, (object)manager, config);
            TrashParity(assembly, (object)controller, (object)runtime, visibility, (object)manager, config);
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
            Check(Goals() == 0 && Walls() == 3, "Standalone Palace published chest goals despite removing ordinary automation.");
            var runtimeType = ((object)runtime).GetType();
            var setFsd = runtimeType.GetMethod("SetFsdSessionActive", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var active in new[] { true, false })
            {
                setFsd.Invoke((object)runtime, [active]);
                controller.Seek(6.8d);
                Check(Goals() == 0 && Dangers() > 0 && Walls() == 3,
                    "FSD session state enabled ordinary goals or suppressed real floor hazards.");
            }
            controller.Seek(6.95d);
            Check(Goals() == 0, "Leaving danger recreated chest goals.");
            controller.Seek(7d);
            Check(Goals() == 0, "Opened treasure published a movement goal.");
            controller.Seek(6.5d);
            Check(Goals() == 0, "Reverse Seek recreated ordinary floor navigation.");
            controller.StopSession();
            Check(Walls() == 0 && Previews() == 0 && Goals() == 0, "Floor script stop leaked owner output.");
            Console.WriteLine("PASS production Palace mechanics only: no chest goals with legacy settings or either FSD session state, real walls/hazards preserved, reverse Seek and owner cleanup");
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
            var removedProperties = new[] { "ClearBehavior", "AutoPassage", "AutoMoveTreasure", "OpenChestsFirst",
                "GoldCoffer", "SilverCoffer", "BronzeCoffer", "FullClear", "MaxPull", "FloorRooms",
                "FloorDestination", "RequestedPomanderSlot" };
            foreach (var name in removedProperties)
                Check(palaceType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) == null,
                    $"Removed Palace setting/output still exists: {name}");
            foreach (var name in new[] { "SelectFloorRoom", "ClearFloorDestination", "SetAutoPomanderEnabled", "IsAutoPomanderEnabled" })
                Check(palaceType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance) == null,
                    $"Removed Palace command still exists: {name}");
            foreach (var name in new[] { "PalaceFloorInteraction", "PalacePomanderControl", "PalaceFloorNavigation", "PalaceFloorOptions", "PalaceClearBehavior" })
                Check(assembly.GetType("MasterPlayer.Mechanics.PalaceOfTheDead." + name) == null,
                    $"Removed Palace implementation still ships in the production DLL: {name}");
            Check(assembly.GetType("MasterPlayer.Mechanics.PalaceOfTheDead.PalaceEncounter")!
                .GetMethod("PreferredTarget", BindingFlags.NonPublic | BindingFlags.Instance) == null,
                "The production encounter retained ordinary floor target selection.");
            Check(Goals() == 0, "Map alone generated an exploration goal.");
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
            Check(Goals() == 0, "Full inventory generated an ordinary goal.");
            controller.Play(); controller.Update(1f); controller.Pause();
            Check(Goals() == 0 && controller.RouterErrorCount == 0, "Legacy overcap log generated navigation or a routed error.");
            controller.Seek(3.5d);
            Check(Goals() == 0, "Inventory refill revived ordinary work.");
            controller.Seek(4.5d);
            Check(Goals() == 0 && Walls() == 0, "Transfer retained ordinary work or stale floor constraints.");
            controller.Seek(5.5d);
            Check(Goals() == 0, "New floor revived ordinary work.");
            controller.Seek(.5d);
            Check(Goals() == 0, "Reverse Seek revived inventory automation.");
            controller.StopSession();
            Check(Goals() == 0 && Previews() == 0, "Stop leaked owner output.");
            Console.WriteLine("PASS production Palace removed automation: no settings, room commands, item/chest executors or floor targets in actual DLL; legacy overcap logs, refill, transfer and Seek remain inert");
            Check(moves == 0 && objectReads == 0, "Recorded Palace replay crossed the live execution boundary");
            Console.WriteLine("PASS production Palace floor: native map walls, all-territory trap data, radar/world previews, Safety, transfer, map connections, inert coffers/opened treasure, manual trap correction, reverse Seek and owner cleanup");
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

    private static void TrashParity(Assembly assembly, object controllerObject, object runtimeObject,
        object visibility, object managerObject, string config)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        dynamic manager = managerObject;
        var origin = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
        var encounterField = assembly.GetType("MasterPlayer.Mechanics.PalaceOfTheDead.PalaceScript")!
            .GetField("encounter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        object Position(float z) => new { X = 0f, Y = 0f, Z = z };
        object Raw(double start, double finish) => new
        {
            ActionId = 6953, TargetId = 2ul, Location = Position(0), Rotation = 0f,
            StartedAt = origin.AddSeconds(start), FinishAt = origin.AddSeconds(finish), TotalTime = (float)(finish - start - .3),
        };
        object Source(float z, object? cast, object[]? statuses = null) => new
        {
            Id = 10ul, Oid = 0x1890u, Kind = 2, Hp = 100u, MaxHp = 100u, Hostile = true,
            Position = Position(z), Rotation = 0f, Cast = cast, Statuses = statuses ?? [],
        };
        object Status(uint id, int slot, double expire, bool? zero = null)
            => new { Id = id, SlotIndex = slot, SourceId = 10ul, ExpiresAt = zero == true ? DateTime.MaxValue : origin.AddSeconds(expire),
                ZeroRemainingTime = zero };
        object[] Previews() => ((IEnumerable)visibility.GetType().GetMethod("GetPreviews")!
            .Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().ToArray();
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        foreach (var territory in new ushort[] { 603, 604, 605, 606, 607 })
        {
            string Frame(double t, object[] actors, bool full = false, ulong local = 2, ulong[]? removed = null)
                => JsonSerializer.Serialize(new { t, kind = "WorldFrame", data = new
                {
                    Full = full, Territory = territory, LocalPlayerId = local, Actors = actors, Removed = removed ?? [],
                    Party = new[] { new { Slot = 0, EntityId = 2ul }, new { Slot = 1, EntityId = 3ul } },
                    DeepDungeon = t >= 35 ? new { DungeonId = 1, Floor = 160, Rooms = new ushort[25] } : null,
                } });
            object Player(ulong id, float z) => new
            {
                Id = id, Kind = 1, Hp = 100u, MaxHp = 100u, Position = Position(z), Rotation = MathF.PI,
            };
            var file = Path.Combine(config, $"palace-raw-{territory}.mpreplay.jsonl");
            File.WriteAllLines(file,
            [
                JsonSerializer.Serialize(new { v = 2, territory, startedUtc = origin, pluginVersion = "palace-raw" }),
                Frame(0, [Source(0, null), Player(2, 29), Player(3, 1000)], true),
                Frame(.1, [Source(0, Raw(0, 4))]),
                Frame(3.4, [Source(0, Raw(0, 4))]),
                Frame(3.5, [Source(-2, Raw(0, 4))]),
                Frame(3.6, [Source(0, Raw(0, 4))]),
                JsonSerializer.Serialize(new { t = 4, kind = "ActionEffect", type = "PromeRotation.LogSystem.LogSystemActionEffectEvent",
                    data = new { SourceId = 10ul, ActionId = 6953u, GlobalSequence = 1u, Timestamp = origin.AddSeconds(4) } }),
                Frame(8, [Source(0, Raw(0, 4))]),
                Frame(8.1, [Source(0, null)]),
                Frame(8.2, [], removed: [10]),
                Frame(8.3, [Source(0, Raw(8.3, 12))]),
                JsonSerializer.Serialize(new { t = 8.4, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                    data = new { SourceId = 10ul, ActionId = 6953u, Timestamp = origin.AddSeconds(.2) } }),
                Frame(10, [], local: 3),
                Frame(10.2, [], local: 2),
                Frame(12.1, [Source(0, null)]),
                Frame(13, [Source(0, null, [Status(999, 2, 43), Status(197, 4, 43)])]),
                Frame(14, [Source(0, null, [Status(197, 4, 43)])]),
                Frame(21, [Source(0, null, [Status(197, 4, 60)])]),
                Frame(22, [Source(0, null, [Status(197, 4, 60), Status(198, 59, 60)])]),
                Frame(22.1, [Source(0, null, [Status(198, 59, 60)])]),
                Frame(23, [Source(0, null)]),
                Frame(24, [Source(0, null, [Status(197, 4, 60)])]),
                Frame(30, [Source(0, null, [Status(197, 4, 0, true)])]),
                Frame(31, []),
                Frame(32, []),
                Frame(33, []),
                Frame(34, []),
                JsonSerializer.Serialize(new { t = 34.1, kind = "WorldFrame", data = new
                {
                    Territory = territory, DeepDungeon = new { DungeonId = 1, Floor = 159, Rooms = new ushort[25] },
                } }),
                JsonSerializer.Serialize(new { t = 34.2, kind = "SystemLog",
                    type = "MasterPlayer.Replay.ReplaySystemLogEvent",
                    data = new { MessageId = 7248u, Timestamp = origin.AddSeconds(34.2) } }),
                JsonSerializer.Serialize(new { t = 35, kind = "WorldFrame", data = new
                {
                    Territory = territory, DeepDungeon = new { DungeonId = 1, Floor = 160, Rooms = new ushort[25] },
                    Actors = new[] { Source(0, Raw(35, 39), [Status(197, 4, 90)]) },
                } }),
                Frame(38.5, []),
                Frame(39, [new
                {
                    Id = 20ul, Oid = 0x181Du, Kind = 2, Hp = 100u, MaxHp = 100u, Targetable = true,
                    InCombat = true, Hostile = true, Position = Position(0), Rotation = 0f,
                    Cast = new
                    {
                        ActionId = 7155u, TargetId = 2ul, Location = Position(29), Rotation = 0f,
                        StartedAt = origin.AddSeconds(39), FinishAt = origin.AddSeconds(43), IsSpell = true,
                    },
                }]),
            ]);
            Check((bool)controller.Load(file), (string)controller.StatusMessage);
            controller.Play();
            object Encounter()
            {
                object script = ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
                return encounterField.GetValue(script)!;
            }
            object? Facing(ulong viewer)
            {
                var encounter = Encounter();
                return encounter.GetType().GetMethod("Facing", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(encounter, [(object)runtime.CurrentTime, viewer]);
            }
            controller.Seek(3.4d);
            Check(Previews().Length == 3 && Facing(2) != null, "Production trash gaze did not publish eye/pupil/arc and heading.");
            Check(((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count() == 0,
                "Gaze drawing invented a movement danger zone.");
            controller.Seek(3.5d);
            Check(Previews().Length == 0 && Facing(2) == null, "Production gaze did not follow its moving source.");
            controller.Seek(8d);
            Check(Previews().Length == 3 && Facing(2) != null, "Effect or original predicted clock removed a still-present raw gaze.");
            controller.Seek(8.1d);
            Check(Previews().Length == 0 && Facing(2) == null, "Observed raw null retained gaze drawing or heading.");
            controller.Seek(11.5d);
            Check(Previews().Length == 3 && Facing(2) != null, "Old cancel or actor recreation lost the new gaze.");
            controller.Seek(10d);
            Check(Previews().Length == 0 && Facing(3) == null, "Viewing a distant player retained the previous personal gaze.");
            controller.Seek(10.2d);
            Check(Previews().Length == 3, "Changing perspective failed to restore valid gaze outputs.");
            controller.Seek(12.1d);
            Check(Previews().Length == 0, "Second raw Finish retained gaze output.");
            bool Spikes()
            {
                var encounter = Encounter();
                return ((IEnumerable)encounter.GetType().GetMethod("ForbiddenAutoAttackTargets", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(encounter, [(object)runtime.CurrentTime])!).Cast<ulong>().Contains(10ul);
            }
            controller.Seek(14d);
            Check(Spikes(), "Removing an unrelated actual status slot removed Spikes.");
            controller.Seek(21.1d);
            Check(Spikes(), "Production raw status refresh failed to restart the fixed ten-second window.");
            controller.Seek(22.2d);
            Check(!Spikes(), "Lose197 did not clear all source Spikes while198 remained at native slot59.");
            controller.Seek(33.9d);
            Check(Spikes(), "Zero remaining value removed the source's original fixed window.");
            controller.Seek(34d);
            Check(!Spikes(), "Zero remaining value caused a false refresh through permanent expiry normalization.");
            controller.Seek(38.5d);
            Check(!Spikes() && Previews().Length == 0 && Facing(2) == null,
                "Actual Boss floor metadata failed to gate Zone gaze and Spikes before Boss activation.");
            controller.Seek(39d);
            Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null && Previews().Length > 0,
                "A transfer into the real Boss map left all Boss drawing and navigation isolated.");
            controller.Seek(3.4d);
            Check(Previews().Length == 3 && Facing(2) != null, "Reverse Seek failed to rebuild native gaze state.");
            controller.StopSession();
            Check(Previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
                "Stopping the trash session retained its owner outputs.");
        }
        Console.WriteLine("PASS production Palace five mixed Zones mechanics: raw-only Start/Finish dynamic source overdue NPC clock Effect isolation generation old Cancel view switch reverse Seek shared eye pupil arc native status slots refresh either Lose no gaze movement danger owner cleanup");
        GenericTrashParity(assembly, controllerObject, runtimeObject, visibility, managerObject, config);
    }

    private static void GenericTrashParity(Assembly assembly, dynamic controller, dynamic runtime,
        object visibility, dynamic manager, string config)
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var path = Environment.GetEnvironmentVariable("PALACE_GAME_DATA")
            ?? throw new InvalidOperationException("Public Palace gaze verification requires PALACE_GAME_DATA.");
        using var game = new Lumina.GameData(path, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
            PanicOnSheetChecksumMismatch = true,
        });
        var original = Svc.Data;
        Set(nameof(Svc.Data), new PalaceGameData(game));
        try
        {
            var start = new DateTime(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
            object Pos(float z) => new { X = 0f, Y = 0f, Z = z };
            object Player(ulong id, float z) => new { Id = id, Kind = 1, Hp = 100u, Position = Pos(z), Rotation = MathF.PI };
            object Caster(uint action = 0, bool? spell = true, bool? enemy = true) => new
            {
                Id = 10ul, Oid = 0x1890, Kind = 2, SubKind = 5, Hp = 100u, MaxHp = 100u,
                Hostile = false, IsEnemy = enemy, Position = Pos(0), Rotation = 0f, HitboxRadius = 2.2f,
                Cast = action == 0 ? null : new
                {
                    ActionId = action, TargetId = action == 6356 ? 3ul : 2ul, Location = Pos(0), Rotation = 0f,
                    StartedAt = start.AddSeconds(action == 6356 ? 0 : 4),
                    FinishAt = start.AddSeconds(action == 6356 ? 4 : 8), TotalTime = 3.7f, IsSpell = spell,
                },
            };
            string Frame(double t, object[] actors, bool full = false, ulong[]? removed = null)
                => JsonSerializer.Serialize(new { t, kind = "WorldFrame", data = new
                {
                    Full = full, Territory = 603, LocalPlayerId = 2ul, Actors = actors, Removed = removed ?? [],
                    Party = new[] { new { Slot = 0, EntityId = 2ul }, new { Slot = 1, EntityId = 3ul } },
                } });
            var file = Path.Combine(config, "palace-public-gaze.mpreplay.jsonl");
            File.WriteAllLines(file,
            [
                JsonSerializer.Serialize(new { v = 2, territory = 603, startedUtc = start }),
                Frame(0, [Player(3, 7), Caster(), Player(2, 12)], true),
                Frame(.1, [Caster(6356)]),
                Frame(1.5, [Player(3, 8)]),
                Frame(2, [], removed: [3]),
                Frame(2.1, [Player(3, -100)]),
                JsonSerializer.Serialize(new { t = 2.2, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                    data = new { SourceId = 10ul, ActionId = 6356u, Timestamp = start.AddSeconds(2.2) } }),
                Frame(4, [Caster(6386)]),
                Frame(7, [Caster()]),
                Frame(8, [Caster(6386, false)]),
                Frame(9, [Caster(6386, null)]),
                Frame(10, [Player(3, 7), Caster(6356, false)]),
                Frame(11, [Caster(6386, true, false)]),
                Frame(12, [Caster(6388, true, null)]),
                Frame(13, [Caster(6386)]),
            ]);
            Check((bool)controller.Load(file), (string)controller.StatusMessage);
            controller.Play();
            object[] Previews() => ((IEnumerable)visibility.GetType().GetMethod("GetPreviews")!
                .Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().ToArray();
            float EyeZ()
            {
                var eye = Previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).EndsWith("/eye"));
                var geometry = eye.GetType().GetProperty("Geometry")!.GetValue(eye)!;
                return ((System.Numerics.Vector3)geometry.GetType().GetProperty("Position")!.GetValue(geometry)!).Z;
            }
            controller.Seek(1d);
            Check(Previews().Length == 3 && EyeZ() == 7, "Public VFX25 gaze outside the Zone cone lost its original target.");
            controller.Seek(2.5d);
            Check(EyeZ() == 8 && controller.RouterErrorCount == 0,
                "Public gaze rebound a destroyed target or Cancel log ended non-null raw cast.");
            controller.Seek(4.1d);
            Check(Previews().Length == 1 && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count() == 1,
                "Raw replacement failed to remove public gaze and publish the source's ordinary circle.");
            controller.Seek(7.1d);
            Check(Previews().Length == 0, "Observed Finish retained generic raw outputs.");
            controller.Seek(8.1d);
            Check(Previews().Length == 0, "A non-Spell raw cast acquired a guessed ordinary AOE.");
            controller.Seek(9.1d);
            Check(Previews().Length == 0, "An old raw recording guessed an unknown Spell type.");
            controller.Seek(10.1d);
            Check(Previews().Length == 3 && EyeZ() == 7,
                "VFX25 gaze was incorrectly subjected to the later ordinary IsSpell filter.");
            controller.Seek(11.1d);
            Check(Previews().Length == 0, "An allied source acquired a public AOE.");
            controller.Seek(12.1d);
            Check(Previews().Length == 0, "An unknown old enemy classification fell back to Hostile.");
            controller.Seek(13.1d);
            Check(Previews().Length == 1, "A true enemy with Hostile false was incorrectly filtered.");
            controller.Seek(2.5d);
            Check(EyeZ() == 8, "Reverse Seek failed to reconstruct the frozen original gaze target.");
            controller.StopSession();
            Check(Previews().Length == 0, "Owner cleanup retained public gaze output.");
            Console.WriteLine("PASS production Palace public VFX25 gaze real Action shape original target dynamic freeze same-ID recreation Cancel raw replacement reverse Seek dual-view owner");
        }
        finally
        {
            Set(nameof(Svc.Data), original);
        }
    }

    private static void BossParity(Assembly assembly, dynamic controller, dynamic runtime, object visibility, dynamic manager, string config)
    {
        var start = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
        object Position(float x, float z) => new { X = x, Y = 0, Z = z };
        object Actor(ulong id, uint oid, float z = -300, object? cast = null, uint hp = 1000, bool combat = true, ulong target = 0,
            byte kind = 2, byte state = 0) => new
        {
            Id = id, Oid = oid, Position = Position(-300, z), Cast = cast, Hp = hp, MaxHp = 373545, InCombat = combat,
            TargetId = target, Kind = kind, Targetable = true, EventState = state, HitboxRadius = .5f,
        };
        object Cast(uint action, ulong target = 0, double finish = 4) => new
        {
            ActionId = action, TargetId = target, Location = Position(-300, -300), Rotation = 0,
            StartedAt = start, FinishAt = start.AddSeconds(finish),
        };
        object Frame(ushort territory, object[] actors, bool full = false, ulong player = 2, ulong[]? removed = null) => new
        {
            Full = full, Territory = territory, LocalPlayerId = player, Actors = actors, Removed = removed ?? [],
            Party = new[] { new { Slot = 0, Role = "", EntityId = 2ul }, new { Slot = 1, Role = "", EntityId = 3ul } },
        };
        string Row(double t, object data) => JsonSerializer.Serialize(new { t, kind = "WorldFrame", data });
        void Load(ushort territory, params string[] rows)
        {
            controller.StopSession();
            var file = Path.Combine(config, $"palace-{territory}-parity.mpreplay.jsonl");
            File.WriteAllLines(file, new[] { JsonSerializer.Serialize(new { v = 2, territory, startedUtc = start, pluginVersion = "parity" }) }.Concat(rows));
            if (!(bool)controller.Load(file)) throw new Exception((string)controller.StatusMessage);
            controller.Play();
        }
        dynamic Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        object[] Previews() => ((IEnumerable)visibility.GetType().GetMethod("GetPreviews")!
            .Invoke(visibility, [(object)runtime.CurrentTime])!).Cast<object>().ToArray();
        int Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().Count();
        bool Preview(string suffix) => Previews().Any(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).EndsWith(suffix, StringComparison.Ordinal));
        int Dangers() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count();
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        DateTime[] Predictions()
        {
            var encounter = assembly.GetType("MasterPlayer.Mechanics.PalaceOfTheDead.PalaceScript")!
                .GetField("encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue((object)Script())!;
            return ((IEnumerable)encounter.GetType().GetMethod("PredictedDamage", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(encounter, null)!).Cast<object>().Select(p => (DateTime)p.GetType()
                    .GetProperty("Activation")!.GetValue(p)!).ToArray();
        }
        void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }

        var player = Actor(2, 0, -290, kind: 1);
        var ally = Actor(3, 0, -295, kind: 1);
        Load(603,
            Row(0, Frame(603, [Actor(1, 0x181D, cast: Cast(7156, 2)), player, ally, Actor(4, 0x1E858E, -280, state: 7)], true)),
            Row(1, Frame(603, [Actor(1, 0x181D), player, ally])),
            Row(2, Frame(603, [], removed: [1, 4])));
        controller.Seek(.5d);
        Check(Lines() == 1 && Preview("kb-landing/1") && Preview("cast/1/7156") && Preview("enemy/1")
            && ((string?)Script().PredictedDamageText)?.Contains("Tankbuster") == true && Dangers() >= 2,
            "DD160 actual DLL lost charge preview, displacement line, projected actor, navigation or tankbuster prediction.");
        controller.Seek(1.1d);
        Check(Lines() == 0 && !Preview("kb-landing/1") && (string?)Script().PredictedDamageText == null,
            "DD160 observed cast end retained projection or prediction.");
        Console.WriteLine("PASS production Palace DD160 parity: real dual-view charge and knockback previews, damage prediction, source directions and observed cleanup");

        Load(604,
            Row(0, Frame(604, [Actor(1, 0x181E, target: 2), player, ally, Actor(4, 0x1E9998)], true)),
            Row(1, Frame(604, [], player: 3)),
            Row(2, Frame(604, [], removed: [4])),
            Row(3, Frame(604, [Actor(4, 0x1E9998, -270)], player: 2)),
            Row(4, Frame(604, [])));
        controller.Seek(.5d);
        Check(Goals() == 1 && Preview("puddle-boss/1") && ((string?)Script().MechanicHintsText)?.Contains("拉出水圈") == true,
            "DD170 did not publish tank pull preference and shared boss outline.");
        controller.Seek(1.5d);
        Check(Goals() == 0 && !(((string?)Script().MechanicHintsText)?.Contains("拉出水圈") ?? false) && Preview("puddle-boss/1"),
            "DD170 player switch leaked the tank-specific preference or hid the shared outline.");
        controller.Seek(3.5d);
        Check(Goals() == 1, "DD170 recycled entity id replaced the frozen initial puddle.");
        Console.WriteLine("PASS production Palace DD170 parity: frozen puddle identities, personal pull goal and hint, viewpoint switch and shared dual-view outline");

        Load(605,
            Row(0, Frame(605, [Actor(1, 0x181F, hp: 373545), player, ally], true)),
            Row(1, Frame(605, [Actor(1, 0x181F, cast: Cast(7166, finish: 8), hp: 56031)])),
            Row(2, Frame(605, [Actor(1, 0x181F, cast: Cast(7166, finish: 9), hp: 373545)])),
            JsonSerializer.Serialize(new
            {
                t = 2.2, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent", packetFacing = 0,
                data = new { SourceId = 1ul, ActionId = 7166, CastTime = 6f, Timestamp = start.AddSeconds(2.2),
                    SourcePosition = Position(-300, -300), EffectPosition = Position(-300, -300) },
            }),
            Row(3, Frame(605, [Actor(1, 0x181F, hp: 373545)])),
            Row(4, Frame(605, [])));
        controller.Seek(1.5d);
        Check(((string?)Script().PredictedDamageText)?.Contains("Raidwide：2人/1次") == true
            && !(((string?)Script().MechanicHintsText)?.Contains("层循环") ?? false) && Dangers() == 0,
            "DD180 did not enter final phase immediately, retained rotation, or drew meteor as a danger circle.");
        controller.Seek(2.5d);
        var activations = Predictions();
        Check(((string?)Script().PredictedDamageText)?.Contains("Raidwide") == true
            && activations.Length == 2 && activations.All(t => Math.Abs((t - start).TotalSeconds - 8.5) < .00001),
            $"DD180 healing rewound final phase or packet generation borrowed stale cast timing / lost NPC finish delay: {string.Join(',', activations.Select(t => (t - start).TotalSeconds))}.");
        controller.Seek(3.5d);
        Check((string?)Script().PredictedDamageText == null, "DD180 observed meteor end retained prediction.");
        Console.WriteLine("PASS production Palace DD180 parity: persistent final phase, immediate rotation removal, ongoing meteor discovery and raidwide prediction without false AOE");

        Load(606,
            Row(0, Frame(606, [Actor(1, 0x1820), player, ally, Actor(4, 0x18F3), Actor(5, 0x18F3, -280),
                Actor(6, 0x18F2), Actor(7, 0x18F1)], true)),
            Row(1, Frame(606, [Actor(4, 0x18F3, cast: Cast(7172))])),
            Row(2, Frame(606, [], removed: [4])),
            Row(3, Frame(606, [])));
        controller.Seek(.5d);
        Check(Preview("bomb/4") && Preview("bomb/5") && Preview("enemy/6") && Preview("enemy/7") && !Preview("enemy/4"),
            "DD190 bomb birth prediction or correct add marker selection missing.");
        controller.Seek(2.5d);
        Check(!Preview("bomb/4") && !Preview("bomb/5") && Dangers() == 0,
            "DD190 destroying a casting minion retained part of its explosion wave.");
        controller.StopSession();
        Check(Previews().Length == 0 && Lines() == 0 && (int)controller.RouterErrorCount == 0,
            "Palace parity stop leaked previews or raised routed errors.");
        Console.WriteLine("PASS production Palace DD190 parity: native bomb wave lifecycle, actor-destroy cast finish, dual-view add markers and owner cleanup");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunArachneReplay(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        RunAllianceStandardReplay(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers, arachne: true);
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.alliance.arachneeve";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object? Read(object obj, string key)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(key, flags) is { } p) return p.GetValue(obj);
                if (type.GetField(key, flags) is { } f) return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, key);
        }
        object[] Items(object obj, string key) => ((IEnumerable)Read(obj, key)!).Cast<object>().ToArray();
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Views() => previews().Where(p => !((string)Read(p, "Id")!).EndsWith("bozja/arena", StringComparison.Ordinal)).ToArray();
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var registry = Read(plugin, "AoeVisibility")!;
            return ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>().ToArray();
        }
        var center = new Vector3(16, 0, -55);
        object Boss() => Actor(1, 0x1637, center, combat: true, subKind: 5);
        object Player(Vector3? point = null, ulong actor = 100, bool dead = false)
            => Actor(actor, 0, point ?? new Vector3(16, 0, -39), dead: dead, subKind: 4);
        object Npc(uint action = 0, double at = 0, Vector3? point = null, ulong actor = 20)
        {
            var cast = action == 0 ? null : JsonSerializer.SerializeToNode(Cast(action, 5, point ?? center, .47f, 100, at));
            if (cast != null) { cast["IsSpell"] = true; cast["TotalTime"] = 5; }
            return Actor(actor, 0x1677, point ?? center, cast: cast, subKind: 5);
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null,
            ulong viewer = 100, ulong slot0 = 100)
        {
            var node = JsonNode.Parse(Frame(556, at, actors, full, removed,
                party: [new { Slot = 0, EntityId = slot0 }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100))!;
            node["data"]!["LocalPlayerId"] = viewer;
            return node.ToJsonString();
        }
        string Director(double at, uint command) => JsonSerializer.Serialize(new
        {
            t = at, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
            data = new { Command = command, Timestamp = Start.AddSeconds(at) },
        });
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Arachne actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        void Render() => Script().GetType().BaseType!.GetMethod("Render", flags)!.Invoke(Script(), null);
        void Apply(double at, IEnumerable<object> actors, ulong viewer = 100, ulong slot0 = 100)
        {
            var world = Read(Script(), "ReplayWorld")!;
            var json = JsonNode.Parse(World(at, actors, viewer: viewer, slot0: slot0))!["data"]!.ToJsonString();
            world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                [JsonSerializer.Deserialize(json, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
            world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(at));
            Update();
        }
        bool IsWeb() => (bool)Read(Model(), "ArachneWebArena")!;
        bool[] Slots() => (bool[])Read(Model(), "ArachneArenaStatus")!;
        void Boundary(bool web, bool published = true)
        {
            var geometry = Read(published ? Snapshot() : Model(), published ? "ArenaBoundary" : "Arena")!;
            Check(geometry.GetType().Name == "AllianceArenaGeometry", "Arachne exact relative polygon boundary");
            var triangles = Items(geometry, "FillPolygons");
            Check(triangles.Length == (web ? 467 : 84), "Arachne source ground/web triangles retained");
            var converted = assembly.GetType("MasterPlayer.Radar.HostDrawingGeometry")!.GetMethod("Convert")!.Invoke(null, [geometry])!;
            Check(Items(converted, "FillPolygons").Length == triangles.Length,
                "Arachne actual world adapter retains every triangle and hole");
            var contours = ((IEnumerable)geometry.GetType().GetMethod("GetContours")!.Invoke(geometry, [64])!).Cast<object>().Count();
            Check(contours == (web ? 18 : 1), "Arachne actual normalized ground/web contours retained");
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Arachne replay never executes facing target or action hold");
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null && Items(Snapshot(), "GoalAreas").Length == 0,
                "Arachne complete owner cleanup");
        }
        void Solver(Vector3 origin)
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [origin, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            ((object)options).GetType().GetProperty("GridResolution")!.SetValue(options, .25f);
            ((object)options).GetType().GetProperty("ArenaSafetyMargin")!.SetValue(options, 0f);
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!,
                "Arachne actual solver finds safe web position");
            dynamic arena = Read(snapshot, "ArenaBoundary")!;
            var previous = origin with { Y = 0 };
            var moved = false;
            foreach (var point in (IEnumerable)result.ExecutablePath)
            {
                var next = (Vector3)Read(point, "Position")! with { Y = 0 };
                Check((bool)arena.Contains(next, 0f), "Arachne actual route vertices stay in web");
                var delta = next - previous;
                var length = delta.Length();
                if (length > 0)
                {
                    moved = true;
                    Check((float)((object)arena).GetType().GetMethod("Raycast", flags)!
                        .Invoke((object)arena, [previous, delta / length])! >= length,
                        "Arachne actual route segment does not cross a web boundary");
                }
                previous = next;
            }
            Check(moved, "Arachne web solver check contains actual movement");
        }

        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101), Npc(6280),
            Npc(6181, actor: 21)], true), World(30, [])]);
        Seek(.3); Boundary(false);
        Vector3 PullEnd()
        {
            var currentModel = Model();
            var currentViewer = ((IDictionary)Read(currentModel, "Actors")!)[100UL]!;
            var currentRule = Items(Read(currentModel, "Definition")!, "Rules")
                .Single(r => (string)Read(r, "Component")! == "TheWidowsEmbrace");
            var currentMovement = ((IEnumerable)currentModel.GetType().GetMethod("AllianceSimpleMovements", flags)!
                .Invoke(currentModel, [currentViewer, currentRule])!).Cast<object>().Single();
            return (Vector3)Read(currentMovement, "Item3")!;
        }
        var groundEnd = PullEnd();
        Apply(1, [Player(new(16, 11, -39))]);
        Check(IsWeb() && Slots()[0], "Arachne first high background changes slot0 and shared arena");
        Boundary(true, false); Boundary(false);
        var webEnd = PullEnd();
        Check(groundEnd != webEnd && Lines().Length == 1 && (Vector3)Read(Lines()[0], "End")! == webEnd,
            "Arachne first draw uses new web wall for pull line after publishing old ground AI");
        Apply(1.1, []); Boundary(true); Solver(new(16, 11, -39));
        Apply(2, [Player(new(16, 0, -39), actor: 101)], viewer: 101);
        Check(IsWeb() && !Slots()[1], "Arachne unchanged ground slot leaves shared web intact");
        Apply(3, [Player(new(16, 11, -39), actor: 101)], viewer: 101);
        Apply(4, [Player(new(16, 9, -39))]);
        Check(!IsWeb() && !Slots()[0] && Slots()[1], "Arachne separate slot histories share one arena");
        Apply(5, [], viewer: 101);
        Check(!IsWeb() && Slots()[1], "Arachne unchanged high slot cannot correct shared ground");
        Apply(6, [Player(new(16, 10, -39), actor: 101)], viewer: 101);
        Check(!IsWeb() && Slots()[1], "Arachne exact Y10 preserves true history");
        Apply(7, [Player(new(16, 9, -39), actor: 101)], viewer: 101);
        Apply(8, [Player(new(16, 11, -39), actor: 101, dead: true)], viewer: 101);
        Check(IsWeb() && Slots()[1], "Arachne dead viewer still runs background without navigation");
        Check(Read(Snapshot(), "ArenaBoundary") == null, "Arachne dead viewer releases planning boundary");
        Release();
        Console.WriteLine("PASS native Arachne actual first frame old AI new web KB triangles holes solver shared slots strict heights dead viewer");

        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101), Player(actor: 102)], true), World(30, [])]);
        Seek(.3);
        Apply(1, [Player(new(16, 11, -39))]);
        Check(Slots()[0], "Arachne slot0 acquires high history");
        Apply(2, [Player(new(16, 0, -39), actor: 102)], viewer: 102, slot0: 102);
        Check(!Slots()[0] && !IsWeb(), "Arachne replacement actor inherits slot0 history and restores ground");
        Apply(3, [Player(new(16, 11, -39), actor: 102)], viewer: 102, slot0: 102);
        Check(Slots()[0] && IsWeb(), "Arachne replacement actor transitions slot0 to web");
        Release();
        Console.WriteLine("PASS native Arachne actual same Party slot replacement actor inherits draw history");

        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var radar = settings.GetType().GetField("EnableRadar")!;
        var overlay = settings.GetType().GetField("EnableRadarOverlay")!;
        var savedRadar = radar.GetValue(settings); var savedOverlay = overlay.GetValue(settings);
        try
        {
            load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101)], true), World(30, [])]);
            Seek(.3);
            radar.SetValue(settings, false); overlay.SetValue(settings, false);
            Apply(1, [Player(new(16, 11, -39))]);
            Check(!IsWeb() && !Slots()[0], "Arachne both sleeping views preserve ground history");
            overlay.SetValue(settings, true); Render();
            Check(!IsWeb(), "Arachne event redraw does not run background side effects");
            Update(); Check(IsWeb(), "Arachne overlay alone runs source background");
            radar.SetValue(settings, true); overlay.SetValue(settings, false);
            Apply(2, [Player(new(16, 9, -39))]);
            Check(!IsWeb(), "Arachne world alone runs source background");
            Release();
        }
        finally { radar.SetValue(settings, savedRadar); overlay.SetValue(settings, savedOverlay); }
        Console.WriteLine("PASS native Arachne actual sleeping views event redraw and each view Framework side effect");

        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101), Npc(6199)], true),
            World(1, [Npc()]), World(2, [Npc(6198, 2)]), World(3, [Npc()]),
            World(4, [Npc(6197, 4)]), World(5, [Npc()]), World(6, [])]);
        Seek(.3);
        Check(Views().Length == 1 && Read(Read(Views()[0], "Geometry")!, "Radius") is float r && r == 10.5f,
            "Arachne actual first ring R10.5");
        Seek(1.1);
        Check((int)Read(Model(), "NumEarthCasts")! == 1
            && (float)Read(Read(Views().Single(), "Geometry")!, "InnerRadius")! == 10.5f
            && (float)Read(Read(Views().Single(), "Geometry")!, "OuterRadius")! == 20.5f,
            "Arachne Finish advances first exact donut");
        Seek(3.1);
        Check((int)Read(Model(), "NumEarthCasts")! == 2
            && (float)Read(Read(Views().Single(), "Geometry")!, "InnerRadius")! == 20.5f
            && (float)Read(Read(Views().Single(), "Geometry")!, "OuterRadius")! == 30.5f,
            "Arachne Finish advances final exact donut");
        Seek(5.1); Check(Views().Length == 0 && dangers() == 0 && (int)Read(Model(), "NumEarthCasts")! == 3,
            "Arachne third Finish retires sequence");
        Seek(1.1); Check(Views().Length == 1 && (int)Read(Model(), "NumEarthCasts")! == 1,
            "Arachne reverse Seek reconstructs ring stage");
        Release();
        Console.WriteLine("PASS native Arachne actual three exact Tremblor shapes watched Finish reverse Seek");

        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101), Npc(6198)], true),
            EffectEvent(1, 20, 6199, 1), World(2, []), World(1000, [])]);
        Seek(2.1); Check(Views().Length == 0 && (int)Read(Model(), "NumEarthCasts")! == 0,
            "Arachne cold second cast and Effect cannot invent initial sequence");
        Release();
        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101), Npc(6199)], true),
            EffectEvent(1, 20, 6199, 1), World(1000, [])]);
        Seek(999);
        Check(Views().Length == 1 && dangers() == 1 && (int)Read(Model(), "NumEarthCasts")! == 0,
            "Arachne Effect and overdue never consume ring without Finish");
        Release();
        Console.WriteLine("PASS native Arachne actual cold later ring Effect and overdue preserve precise sequence contract");

        load(id, [Header(556), World(0, [Boss(), Player(new(16, 11, -39)), Player(actor: 101), Npc(6199)], true),
            Director(1, 0x40000005), World(1.1, []), Director(2, 0x40000011),
            World(2.1, []), World(3, [], removed: [1]), World(4, [Boss(), Player()]), World(5, [])]);
        Seek(.3); Check(IsWeb() && Slots()[0], "Arachne pre-wipe high history");
        var oldLoadVersion = (long)Read(Model(), "LoadVersion")!;
        Seek(1.2);
        Check(!(bool)Read(Model(), "Active")! && Slots().All(s => !s) && Views().Length == 0 && dangers() == 0,
            "Arachne Director clears history queues and outputs");
        Seek(2.2); Check(!(bool)Read(Model(), "Active")!, "Arachne Director unlock never revives retired primary");
        Seek(4.1); Boundary(false);
        var restored = Items(Model(), "earthPublished").Single();
        Check((bool)Read(Model(), "Active")! && !IsWeb() && Slots().All(s => !s)
            && (long)Read(Model(), "LoadVersion")! > oldLoadVersion
            && Views().Length == 1 && (int)Read(Model(), "NumEarthCasts")! == 0
            && (Vector3)Read(restored, "Origin")! == center
            && (float)Read(Read(restored, "Shape")!, "Radius")! == 10.5f
            && (DateTime)Read(restored, "Activation")! > Start.AddSeconds(4),
            "Arachne fresh primary Load restores ground, empty slot history and cold active first cast");
        Release();
        Console.WriteLine("PASS native Arachne actual Director wipe retired generation unlock fresh Load restores ground");

        const string otherId = "masterplayer.alliance.calofisteri";
        var otherDefinition = ((IEnumerable)assembly.GetType("MasterPlayer.Mechanics.Bozja.AllianceKnockbackCatalog")!
            .GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>()
            .Single(d => (uint)Read(d, "PrimaryOid")! == 0x1661);
        var otherCenter = (Vector3)Read(otherDefinition, "Center")!;
        manager.SetScriptEnabled(otherId, true);
        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101), Npc(6180),
            Actor(2, 0x1661, otherCenter, combat: true, subKind: 5),
            Npc(6172, point: otherCenter, actor: 21)], true), World(1, [])]);
        Seek(.3);
        Check(dangers() == 2 && manager.Running.ContainsKey(otherId), "Arachne same territory owners independently publish");
        manager.SetScriptEnabled(id, false); controller.Seek(.4);
        Check(dangers() == 1 && manager.Running.ContainsKey(otherId), "Arachne release preserves Calofisteri");
        manager.SetScriptEnabled(id, true); Seek(.3); Check(dangers() == 2, "Arachne reverse Seek restores both owners");
        manager.SetScriptEnabled(otherId, false); Seek(.4); Check(dangers() == 1, "Calofisteri release preserves Arachne");
        Release(); manager.SetScriptEnabled(otherId, true);
        Console.WriteLine("PASS native Arachne actual same territory dual owner bidirectional release reverse Seek");
    }
}

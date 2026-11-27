using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ECommons.DalamudServices;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunOzmaReplay(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        var sqpack = Environment.GetEnvironmentVariable("FFXIV_SQPACK");
        Check(!string.IsNullOrWhiteSpace(sqpack) && Directory.Exists(sqpack), "Ozma requires actual client tables.");
        using var game = new Lumina.GameData(sqpack!, new Lumina.LuminaOptions
        {
            DefaultExcelLanguage = Lumina.Data.Language.ChineseSimplified,
        });
        Check(game.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()!.GetRow(556).ContentFinderCondition.RowId == 168,
            "Ozma actual territory and CFC identity");
        var sampler = runtimeObject.GetType().Assembly.GetType("MasterPlayer.MechanicHost.LiveWorldSampler")!;
        var territory = sampler.GetField("_cachedTerritory", BindingFlags.Static | BindingFlags.NonPublic)!;
        var cfc = sampler.GetField("_cachedCfc", BindingFlags.Static | BindingFlags.NonPublic)!;
        var savedTerritory = territory.GetValue(null);
        var savedCfc = cfc.GetValue(null);
        var savedData = Svc.Data;
        typeof(Svc).GetProperty(nameof(Svc.Data))!.SetValue(null, new PalaceGameData(game));
        territory.SetValue(null, ushort.MaxValue);
        try
        {
            OzmaReplayBody(managerObject, runtimeObject, controllerObject, load, stop, previews, dangers);
        }
        finally
        {
            typeof(Svc).GetProperty(nameof(Svc.Data))!.SetValue(null, savedData);
            territory.SetValue(null, savedTerritory);
            cfc.SetValue(null, savedCfc);
        }
    }

    private static void OzmaReplayBody(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.alliance.ozma";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
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
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.OzmaCatalog", true)!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = (Vector3)Read(definition, "Center")!;
        var name = (string)Read(definition, "Name")!;
        var pc = new Vector3(280, 0, -390);
        object Boss(bool combat = true, bool dead = false, uint hp = 1000)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x1560, center, combat: combat, dead: dead, subKind: 5))!;
            node["Hp"] = hp;
            return node;
        }
        object Player(Vector3? position = null, ulong actor = 100, bool dead = false, uint status = 0, ulong target = 0)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, 0, position ?? pc, dead: dead, subKind: 4))!;
            node["HitboxRadius"] = .5f;
            node["TargetId"] = target;
            if (status != 0) node["Statuses"] = JsonSerializer.SerializeToNode(new[] {
                new { Id = status, SourceId = 100UL, ExpiresAt = Start.AddSeconds(100), SlotIndex = 59 } });
            return node;
        }
        object Npc(uint action = 0)
        {
            var cast = action == 0 ? null : JsonSerializer.SerializeToNode(
                Cast(action, 5, action == 6190 ? pc + new Vector3(0, 0, 4) : pc, .47f, 100, 0));
            if (cast != null) { cast["IsSpell"] = true; cast["TotalTime"] = 5; }
            return Actor(20, 0x233C, pc + new Vector3(0, 0, 4), cast: cast, subKind: 5);
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null, bool party = true)
            => Frame(556, at, actors, full, removed, party: party
                ? [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }] : [],
                sourcePlayerId: 100);
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Ozma actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Render() => Script().GetType().BaseType!.GetMethod("Render", flags)!.Invoke(Script(), null);
        void Update() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        void Apply(double at, IEnumerable<object> actors)
        {
            var world = Read(Script(), "ReplayWorld")!;
            var json = System.Text.Json.Nodes.JsonNode.Parse(World(at, actors))!["data"]!.ToJsonString();
            world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                [JsonSerializer.Deserialize(json, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
            world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(at));
            Update();
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Ozma diagnostics and knockback never execute PR control in replay");
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null && Items(Snapshot(), "GoalAreas").Length == 0,
                "Ozma complete owner cleanup");
        }
        void Boundary(bool published = true)
        {
            var geometry = published ? Read(Snapshot(), "ArenaBoundary")! : Read(Model(), "Arena")!;
            Check(geometry.GetType().Name == "AllianceArenaGeometry", "Ozma relative multipolygon geometry retained");
            var converted = assembly.GetType("MasterPlayer.Radar.HostDrawingGeometry")!
                .GetMethod("Convert")!.Invoke(null, [geometry])!;
            var host = Items(converted, "FillPolygons");
            Check(host.Length > 0 && Items(geometry, "FillPolygons").Length == host.Length,
                "Ozma world geometry adapter carries source triangles instead of filling holes");
        }
        void Solver()
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [pc, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            ((object)options).GetType().GetProperty("GridResolution")!.SetValue(options, .5f);
            ((object)options).GetType().GetProperty("ArenaSafetyMargin")!.SetValue(options, 0f);
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Ozma real solver safe route");
            dynamic arena = Read(snapshot, "ArenaBoundary")!;
            foreach (var point in (IEnumerable)result.ExecutablePath)
                Check((bool)arena.Contains((Vector3)Read(point, "Position")!, 0f), "Ozma actual solver stays out of arena hole");
        }
        foreach (var action in new uint[] { 6453, 6185, 6190 })
        {
            load(id, [Header(556), World(0, [Boss(false), Player(), Player(actor: 101), Npc(action)], true),
                World(1, [Boss()]), World(2, []), World(6, [Npc()]), World(7, [])]);
            Seek(.3); Check(previews().Length == 0 && dangers() == 0, "Ozma cold inactive output");
            Seek(2.1);
            Check((bool)Read(Model(), "Active")! && (ushort)Read(Model(), "ContentFinderId")! == 168,
                "Ozma replay CFC uses actual table resolver shared by live sampling");
            Boundary();
            Check(Views().Length == (action == 6190 ? 0 : 1) && dangers() == (action == 6190 ? 0 : 1)
                && Lines().Length == (action == 6190 ? 1 : 0) && Items(Snapshot(), "GoalAreas").Length == 0,
                $"Ozma ordinary display navigation and Holy independent raidwide plus line: action={action} previews={Views().Length} dangers={dangers()} lines={Lines().Length}");
            if (action == 6190)
                Check(Items(Script(), "PredictedDamage").Length == 1, "Ozma Holy damage diagnostic retained");
            else Solver();
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                "Ozma matching NPC action takeover");
            manager.SetMechanismEnabled(id, name, false); Seek(2.2);
            Check(dangers() == 0 && previews().Length == 0 && Lines().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "Ozma mechanism disabled releases outputs");
            manager.SetMechanismEnabled(id, name, true); Seek(2.1);
            Seek(6.1); Check(Views().Length == 0 && dangers() == 0 && Lines().Length == 0
                && Items(Script(), "PredictedDamage").Length == 0, "Ozma ordinary watched Finish");
            Release();
            Console.WriteLine($"PASS native Ozma actual action{action} cold Finish solver triangles toggle reverse Seek owner replay isolation");
        }
        load(id, [Header(556), World(0, [Boss(), Player(status: 1209), Player(actor: 101), Npc(6190)], true),
            World(1, []), World(2, [Player()]), World(3, []), World(6, [Npc()]), World(7, [])]);
        Seek(.5); Check(Lines().Length == 0 && Items(Script(), "PredictedDamage").Length == 1,
            "Ozma cold immunity60 does not suppress independent Holy raidwide");
        Seek(2.1); Check(Lines().Length == 1, "Ozma real immunity Lose restores line");
        Release();
        Console.WriteLine("PASS native Ozma actual Holy cold immunity59 real Lose independent raidwide");

        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101)], true),
            World(1, [Player(new(300, 0, 260))]), World(2, []),
            World(3, [Player(new(301.5f, 0, 205.5f))]), World(4, []),
            World(5, [Player()]), World(6, [])]);
        Seek(.5);
        var initialPosition = (Vector3)Read(Read(Model(), "Arena")!, "Position")!;
        Apply(1, [Player(new(300, 0, 260))]);
        Check((Vector3)Read(Read(Model(), "Arena")!, "Position")! != initialPosition
            && (Vector3)Read(Read(Snapshot(), "ArenaBoundary")!, "Position")! == initialPosition,
            "Ozma first split draw updates visible arena after old navigation publication");
        Boundary(false);
        Apply(2.1, []); Boundary();
        Check(((IEnumerable)((dynamic)Read(Snapshot(), "ArenaBoundary")!).GetContours(64)).Cast<object>().Count() == 3,
            "Ozma following frame publishes all three split islands");
        Apply(3, [Player(new(301.5f, 0, 205.5f))]); Boundary(false);
        Apply(4.1, []); Boundary();
        Check(((IEnumerable)((dynamic)Read(Snapshot(), "ArenaBoundary")!).GetContours(64)).Cast<object>().Count() == 2,
            "Ozma shade outer and inner contours retained");
        Apply(5, [Player()]);
        Apply(6.1, []); Boundary();
        Check((Vector3)Read(Read(Snapshot(), "ArenaBoundary")!, "Position")! == initialPosition,
            "Ozma original arena restored by slot history");
        Seek(2.1); Boundary();
        Release();
        Console.WriteLine("PASS native Ozma actual three arenas prior navigation new display holes split reverse Seek triangles");

        load(id, [Header(556), World(0, [Boss(false), Player(status: 1072), Player(actor: 101)], true),
            World(1, [Boss()]), World(2, []), World(3, [Player(dead: true, status: 1072)]), World(4, []),
            World(5, [Player()]), World(6, [])]);
        Seek(2.1);
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Stop everything!") == true
            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Pyretic") == true, "Ozma cold bomb actual diagnostics");
        Apply(3, [Player(dead: true, status: 1072)]);
        Check(((DateTime[])Read(Model(), "OzmaBomb")!)[0] == default
            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Stop everything!") != true
            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Pyretic") == true,
            "Ozma first dead query clears before public hint while prepared AI mode survives this frame");
        Apply(4.1, []); Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Pyretic") != true, "Ozma next AI publication no mode");
        Seek(2.1);
        Seek(5.1); Check(((DateTime[])Read(Model(), "OzmaBomb")!)[0] == default, "Ozma real status Lose");
        Release();
        Console.WriteLine("PASS native Ozma actual bomb slot59 cold death two queries prepared diagnostic real Lose reverse Seek isolation");

        var settings = assembly.GetType("MasterPlayer.Settings.MasterPlayerSettings")!.GetProperty("Instance")!.GetValue(null)!;
        var radar = settings.GetType().GetField("EnableRadar")!;
        var overlay = settings.GetType().GetField("EnableRadarOverlay")!;
        var savedRadar = radar.GetValue(settings); var savedOverlay = overlay.GetValue(settings);
        try
        {
            load(id, [Header(556), World(0, [Boss(), Player(status: 1072), Player(actor: 101)], true),
                World(1, []), World(10, [])]);
            Seek(.3);
            var initialArena = Read(Model(), "Arena");
            radar.SetValue(settings, false); overlay.SetValue(settings, false);
            Apply(1.1, [Player(new(300, 0, 260), dead: true, status: 1072)]);
            Check(((DateTime[])Read(Model(), "OzmaBomb")!)[0] != default
                && ReferenceEquals(initialArena, Read(Model(), "Arena")), "Ozma asleep views preserve dead bomb and arena history");
            overlay.SetValue(settings, true); Render();
            Check(((DateTime[])Read(Model(), "OzmaBomb")!)[0] != default
                && ReferenceEquals(initialArena, Read(Model(), "Arena")),
                "Ozma dead event redraw before Framework preserves bomb for AI capture");
            Update();
            Check(((DateTime[])Read(Model(), "OzmaBomb")!)[0] == default
                && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Pyretic") == true,
                "Ozma Framework captures AI mode before consuming retained dead bomb");
            Seek(.3);
            initialArena = Read(Model(), "Arena");
            overlay.SetValue(settings, false);
            Apply(1.1, [Player(new(300, 0, 260), dead: true, status: 1072)]);
            Apply(2.1, [Player(new(300, 0, 260), status: 1072)]);
            overlay.SetValue(settings, true); Render();
            Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Stop everything!") == true
                && ReferenceEquals(initialArena, Read(Model(), "Arena")), "Ozma event render restores hint without extra arena background");
            Update(); Boundary(false);
            Check(!ReferenceEquals(initialArena, Read(Model(), "Arena")), "Ozma overlay alone runs source background on Framework");
            overlay.SetValue(settings, false); radar.SetValue(settings, true);
            Apply(3.1, [Player(new(301.5f, 0, 205.5f), status: 1072)]); Boundary(false);
            Check((uint[])Read(Model(), "OzmaArenaStatus")! is { } statuses && statuses[0] == 2,
                "Ozma world view alone runs source background");
            Release();
        }
        finally { radar.SetValue(settings, savedRadar); overlay.SetValue(settings, savedOverlay); }
        Console.WriteLine("PASS native Ozma actual both views asleep dead retained revive one view event render Framework arena timing");

        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101), Npc(6453)], true, party: false),
            World(1, []), World(2, [])]);
        Seek(.5);
        Check(!(bool)Read(Model(), "Active")! && dangers() == 0 && previews().Length == 0 && Lines().Length == 0,
            "Ozma absent Party0 prevents module activation and output");
        Release();
        Console.WriteLine("PASS native Ozma actual absent Party0 no module outputs replay isolation owner");

        load(id, [Header(556), World(0, [Boss(), Player(), Player(actor: 101)], true),
            World(1, [Boss(dead: true, hp: 0)]), World(2, [], removed: [1]),
            World(3, [Player(new(2000, 0, 2000))]), World(4, [])]);
        Seek(1.1); Check((bool)Read(Model(), "Active")!, "Ozma HP0 dead retains phase");
        Seek(2.1); Check((bool)Read(Model(), "Active")!, "Ozma Destroyed inside retains phase");
        Seek(3.1); Check(!(bool)Read(Model(), "Active")! && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
            "Ozma Destroyed plus Party0 outside completes and clears");
        Seek(2.1); Check((bool)Read(Model(), "Active")!, "Ozma reverse Seek reconstructs destroyed-but-active original");
        Release();
        Console.WriteLine("PASS native Ozma actual custom Destroyed AND Party0 squares completion reverse Seek cleanup");

        const string otherId = "masterplayer.alliance.calofisteri";
        var otherDefinition = ((IEnumerable)assembly.GetType("MasterPlayer.Mechanics.Bozja.AllianceKnockbackCatalog")!
            .GetField("Definitions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)
            .Cast<object>().Single(d => (uint)Read(d, "PrimaryOid")! == 0x1661);
        var otherCenter = (Vector3)Read(otherDefinition, "Center")!;
        var middle = (center + otherCenter) * .5f;
        manager.SetScriptEnabled(otherId, true);
        load(id, [Header(556), World(0, [Boss(), Player(middle), Player(middle, actor: 101),
            Actor(2, 0x1661, otherCenter, combat: true, subKind: 5),
            Npc(6453), Actor(21, 0x233C, otherCenter,
                cast: Cast(6172, 5, otherCenter, startedAt: 0), subKind: 5)], true), World(1, [])]);
        Seek(.3);
        Check(dangers() == 2 && manager.Running.ContainsKey(otherId),
            "Ozma and Calofisteri same territory publish independent owners");
        manager.SetScriptEnabled(id, false); controller.Seek(.4);
        Check(dangers() == 1 && manager.Running.ContainsKey(otherId), "Ozma release preserves Calofisteri owner");
        manager.SetScriptEnabled(id, true); Seek(.3);
        Check(dangers() == 2, "Ozma reverse Seek reconstructs both owners");
        manager.SetScriptEnabled(otherId, false); Seek(.4);
        Check(dangers() == 1, "Calofisteri release preserves Ozma owner");
        Release();
        manager.SetScriptEnabled(otherId, true);
        Console.WriteLine("PASS native Ozma actual same territory Calofisteri dual owner bidirectional release reverse Seek");
    }
}

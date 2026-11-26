using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBozjaPyromancer(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const string name = "最强的火焰法师";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
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
        object Model() => Items(Read(Script(), "region")!, "Encounters").Single(e => (bool)Read(e, "IsPyromancer")!);
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object[] Aoes() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")).ToArray();
        object Spell(uint action, double at = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, center, .4f, 100, at))!;
            node["TotalTime"] = 5; node["IsSpell"] = true; return node;
        }
        object Boss(ulong actor = 1, bool combat = true, bool targetable = true, uint hp = 1000)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, 0x2EF7, center, combat: combat,
                targetable: targetable, subKind: 5, isEnemy: true, objectIndex: 2))!;
            node["Hp"] = hp; return node;
        }
        object Helper(ulong actor, uint action = 0, double at = 0)
            => Actor(actor, 0x2EA1, center, cast: action == 0 ? null : Spell(action, at), subKind: 5);
        string World(double at, IEnumerable<object> actors, bool full = false, bool known = true, bool hasParty = true)
        {
            var node = JsonSerializer.SerializeToNode(new { t = at, kind = "WorldFrame", data = new {
                Full = full, Territory = 920, LocalPlayerId = 100, SourcePlayerId = 100, AllowedFateId = 1616,
                Actors = actors.ToArray(), Party = Enumerable.Range(0, hasParty ? 1 : 0)
                    .Select(i => new { Slot = i, EntityId = 100UL }).ToArray() } })!;
            if (known) node["data"]!["ActiveFate"] = JsonSerializer.SerializeToNode(new { Id = 1616, Center = Pose(center), Radius = 50 });
            return node.ToJsonString();
        }
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Pyromancer actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")!, "Pyromancer replay does not execute PR requests");
            stop();
            Check(dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && previews().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Pyromancer owner cleanup");
        }
        void Solver()
        {
            var snapshot = Snapshot();
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Pyromancer actual solver");
        }
        var definition = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaPyromancerCatalog")!
            .GetField("Definition", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        foreach (var rule in Items(definition, "Rules"))
            foreach (var action in (uint[])Read(rule, "Actions")!)
            {
                var expectedDangers = (string)Read(rule, "Component")! == "GrandCrossflame" ? 2 : 1;
                load(id, [Header(920), World(0, [Boss(), Actor(100, 0, center), Helper(20, action)], true),
                    World(1, [Helper(20)]), World(2, [])]);
                Seek(.3);
                Check((bool)Read(Model(), "Active")! && Aoes().Length == 1 && dangers() == expectedDangers
                    && Items(Snapshot(), "GoalAreas").Length == 0 && Items(Script(), "PredictedDamage").Length == 0,
                    "Pyromancer actual phase AOE display navigation " + action);
                Check((ulong)Read(Script(), "PlannedTargetId")! == 1
                    && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                    "Pyromancer phase actual target plan and generic suppression");
                Solver(); Seek(1.1); Check(Aoes().Length == 0 && dangers() == 0, "Pyromancer real Finish");
                Seek(.3); manager.SetMechanismEnabled(id, name, false); Seek(.4);
                Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Pyromancer disabled cleanup");
                manager.SetMechanismEnabled(id, name, true); Seek(.5); Release();
                Console.WriteLine("PASS native Pyromancer actual DLL phase geometry solver Finish Seek owner " + action);
            }
        load(id, [Header(920), World(0, [Boss(combat: false), Actor(100, 0, center), Helper(20, 21022)], true),
            World(1, [Boss()]), World(2, [Boss(hp: 0)]), World(3, [Boss(targetable: false)]),
            World(4, [Boss()]), World(5, [])]);
        Seek(.3);
        dynamic thermal = Read(Aoes().Single(), "Geometry")!;
        Check(!(bool)Read(Model(), "Active")! && (bool)Read(Model(), "HasConstructionDrawing")!
            && (bool)thermal.Contains(center + Vector3.UnitX * 20, 0f)
            && !(bool)thermal.Contains(center + Vector3.UnitX * 20.01f, 0f)
            && dangers() == 0 && Items(Snapshot(), "GoalAreas").Length == 0 && Read(Snapshot(), "ArenaBoundary") == null
            && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("GTFO") == true
            && (ulong)Read(Script(), "PlannedTargetId")! == 0
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 21022 }),
            "Pyromancer constructor R20 drawing hint only, no new navigation or generic suppression");
        Seek(1.1); Check((bool)Read(Model(), "Active")! && Aoes().Length == 0
            && !(bool)Read(Model(), "HasConstructionDrawing")!, "Pyromancer phase deactivates constructor ThermalShock");
        Seek(2.1); Check((bool)Read(Model(), "Active")!, "Pyromancer HP0 alive phase remains");
        Seek(3.1); Check(!(bool)Read(Model(), "Active")! && previews().Length == 0, "Pyromancer targetable completion");
        Seek(4.1); Check(!(bool)Read(Model(), "Active")!, "Pyromancer same original cannot repull");
        Seek(.3); Check(Aoes().Length == 1, "Pyromancer reverse Seek reconstructs constructor state"); Release();
        Console.WriteLine("PASS native Pyromancer actual constructor display hint R20 no navigation phase deactivation HP0 untargetable reverse Seek");

        load(id, [Header(920), World(0, [Boss(), Actor(100, 0, center), Helper(20), Helper(21)], true),
            World(1, [Boss(2, combat: false)]), World(2, [Helper(21, 21022, 2)]),
            World(3, [Helper(20, 21022, 3)]), World(4, [Boss(3, combat: false)]),
            World(5, [Boss(targetable: false)]), World(6, []), World(7, [Boss(3)]), World(8, [])]);
        Seek(4.1);
        object Context(ulong actor)
        {
            // Seek reconstructs the script and disposes its previous dictionary.
            var dictionary = (IDictionary)Read(Model(), "PyromancerConstructions")!;
            return dictionary[dictionary.Keys.Cast<object>().Single(key =>
                (ulong)Read(Read(key, "Actor")!, "Id")! == actor)]!;
        }
        ulong[] CasterOrder(object construction) => Items(construction, "Casts")
            .Select(c => (ulong)Read(Read(Read(c, "Source")!, "Actor")!, "Id")!).ToArray();
        Check(CasterOrder(Context(2)).SequenceEqual(new ulong[] { 21, 20 })
            && CasterOrder(Context(3)).SequenceEqual(new ulong[] { 20, 21 }), "Pyromancer actual independent pending histories");
        Seek(6.1); Check((ulong)Read(Read(Read(Model(), "Primary")!, "Actor")!, "Id")! == 3
            && Aoes().Length == 2 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null,
            "Pyromancer actual reverse promotion preserves constructor display");
        Seek(7.1); Check(Aoes().Length == 0 && !(bool)Read(Context(3), "ThermalActive")!
            && (bool)Read(Context(2), "ThermalActive")!, "Pyromancer enters only selected instance phase");
        Release(); Console.WriteLine("PASS native Pyromancer actual independent pending hot cold histories promotion and local deactivation");
        load(id, [Header(920), World(0, [Boss(combat: false), Actor(100, 0, center), Helper(20, 21022)], true),
            World(1, [], hasParty: false), World(2, []), World(3, [])]);
        Seek(.3); Check(Aoes().Length == 1, "Pyromancer constructor visible with actual Party viewer");
        Seek(1.1); Check(Aoes().Length == 0 && (bool)Read(Model(), "HasConstructionDrawing")! && dangers() == 0,
            "Pyromancer absent Party viewer withdraws display without clearing component history");
        Seek(2.1); Check(Aoes().Length == 1, "Pyromancer Party viewer restore publishes retained constructor state");
        Release(); Console.WriteLine("PASS native Pyromancer actual Party viewer absent restore display isolation preserves constructor history");

        // These objects only supply the controller's selection properties;
        // no upstream module, manager Update or game action is invoked.
        var hostType = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var sessionField = hostType.GetField("_replaySession", flags)!;
        var sourceAssembly = sessionField.FieldType.Assembly;
        var sourceConfig = sourceAssembly.GetType("BossMod.Service")!
            .GetField("Config", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var configNodes = (IDictionary)sourceConfig.GetType().GetField("_nodes")!.GetValue(sourceConfig)!;
        foreach (var configName in new[] { "BossMod.BossModuleConfig", "BossMod.ZoneModuleConfig" })
        {
            var configType = sourceAssembly.GetType(configName)!;
            if (!configNodes.Contains(configType)) configNodes.Add(configType, Activator.CreateInstance(configType)!);
        }
        object Empty(string type) => RuntimeHelpers.GetUninitializedObject(sourceAssembly.GetType(type)!);
        void Field(object obj, string key, object? value)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
                if (type.GetField(key, flags) is { } field) { field.SetValue(obj, value); return; }
            throw new MissingFieldException(obj.GetType().Name, key);
        }
        var session = Empty("MasterPlayer.MechanicRuntime.MechanicSession");
        var bosses = Empty("BossMod.BossModuleManager");
        var zones = Empty("BossMod.ZoneModuleManager");
        var sourceModule = Empty("BossMod.Shadowbringers.Foray.Skirmish.PyromancerSupreme.PyromancerSupreme");
        var primary = Empty("BossMod.Actor"); Field(primary, "OID", 0x2EF7u);
        Field(sourceModule, "PrimaryActor", primary);
        var machineType = sourceAssembly.GetType("BossMod.StateMachine")!;
        var phasesType = machineType.GetField("Phases", flags)!.FieldType;
        var phases = (IList)Activator.CreateInstance(phasesType)!;
        phases.Add(RuntimeHelpers.GetUninitializedObject(phasesType.GenericTypeArguments[0]));
        var machine = Activator.CreateInstance(machineType, [phases])!;
        Field(sourceModule, "StateMachine", machine);
        Field(bosses, "_activeModule", sourceModule);
        Field(session, "<BossModules>k__BackingField", bosses);
        Field(session, "<ZoneModules>k__BackingField", zones);
        var host = RuntimeHelpers.GetUninitializedObject(hostType);
        sessionField.SetValue(host, session); Field(host, "_replayTerritory", (ushort)920);
        var gate = hostType.GetMethod("SleepMpOwnedBoss", flags)!;
        Check((bool)gate.Invoke(host, null)!, "Pyromancer inactive no Zone suppresses only migrated boss bridge");
        var zone = Empty("BossMod.Shadowbringers.Foray.Bozja");
        Field(zones, "ActiveModule", zone);
        Check(!(bool)gate.Invoke(host, null)!, "Pyromancer inactive Zone keeps unconverted Zone bridge alive");
        Field(machine, "ActivePhaseIndex", 0);
        Check((bool)gate.Invoke(host, null)!, "Pyromancer active phase retains native bridge ownership");
        Field(machine, "ActivePhaseIndex", -1);

        load(id, [Header(920), World(0, [Boss(combat: false), Actor(100, 0, center), Helper(20, 21022)], true), World(1, [])]);
        Seek(.3);
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var pluginHostField = plugin.GetType().GetField("_mechanicHost", flags)!;
        var previousHost = pluginHostField.GetValue(plugin);
        try
        {
            pluginHostField.SetValue(plugin, host);
            var scriptType = Script().GetType();
            MethodInfo? render = null;
            for (var type = scriptType; type != null && render == null; type = type.BaseType)
                render = type.GetMethod("Render", flags);
            render!.Invoke(Script(), null);
            Check(Aoes().Length == 0 && dangers() == 0, "Pyromancer actual script defers constructor drawing to existing Zone");
            Field(zones, "ActiveModule", null); render.Invoke(Script(), null);
            Check(Aoes().Length == 1 && dangers() == 0, "Pyromancer actual script resumes constructor display without new navigation");
        }
        finally { pluginHostField.SetValue(plugin, previousHost); }
        Release(); Console.WriteLine("PASS native Pyromancer actual controller Zone bridge precedence and complementary script display gate");
    }
}

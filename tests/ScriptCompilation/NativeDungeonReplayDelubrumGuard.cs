using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDelubrumGuard(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.delubrum.normal";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(244, 0, -162.104f);
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } property) return property.GetValue(obj)!;
                if (type.GetField(name, flags) is { } field) return field.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        object[] Casts() => ((IEnumerable)Read(Model(), "Casts")).Cast<object>().ToArray();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")).Contains("bozja/aoe/")).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object Spell(uint action, double at = 0, Vector3? location = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, location ?? center, .4f, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Boss(object? cast = null) => Actor(1, 0x30BA, center, combat: true, isEnemy: true, cast: cast);
        object Player(ulong actor = 100)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actor, 0, center + Vector3.UnitZ * (actor == 100 ? 10 : -12)))!;
            node["TargetId"] = 30UL;
            return node;
        }
        object Helper(object? cast = null) => Actor(20, 0x233C, center + Vector3.UnitX * 10, cast: cast, subKind: 5);
        string World(double at, IEnumerable<object> actors, bool full = false)
            => Frame(936, at, actors, full, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }],
                sourcePlayerId: 100);
        string Effect(double at, uint action, ulong source = 20) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                GlobalSequence = (uint)(at * 100), Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DRN3 actual replay error: " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(previews().Length == 0 && dangers() == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "DRN3 owner release");
        }
        foreach (var action in new uint[] { 22514, 22515, 22534 })
        {
            load(id, [Header(936), World(0, [Boss(), Player(), Player(101), Helper(Spell(action))], true),
                Effect(1, action), World(20, []), World(21, [Helper()]), World(22, [])]);
            Seek(.1);
            Check((bool)Read(Model(), "Active") && Shown().Length == 1 && dangers() == 1,
                "DRN3 actual standard shape " + action);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 23453 }),
                "DRN3 exact real source and unknown action ownership");
            if (action == 22534)
            {
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = Snapshot();
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                var target = (Vector3)Read((object)result, "TargetPosition");
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance(center, target) > 20
                    && (bool)((dynamic)Read(snapshot, "ArenaBoundary")).Contains(target, 0f), "DRN3 actual R20 solver");
            }
            Seek(20.1); Check(Shown().Length == 1, "DRN3 no Effect or TTL");
            Seek(21.1); Check(Shown().Length == 0 && dangers() == 0, "DRN3 ordinary Finish");
            Seek(.1); Release();
            Console.WriteLine($"PASS native DRN3 {action} actual standard geometry ownership Finish owner reverse Seek");
        }
        var map = JsonSerializer.Serialize(new
        {
            t = 21, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 0x18, Param1 = 1, Param2 = 2, Timestamp = Start.AddSeconds(21) },
        });
        load(id, [Header(936), World(0, [Boss(), Player(), Player(101), Helper(),
            Actor(33, 0x1EA1A1, new(244, 0, -129))], true), World(20, []), map, World(22, [])]);
        Seek(20.1); Check(Shown().Length == 1 && dangers() == 1, "DRN3 feature ring remains overdue");
        Seek(21.1); Check(Shown().Length == 0 && dangers() == 0, "DRN3 Map only clear ring");
        dynamic boundary = Read(Snapshot(), "ArenaBoundary");
        Check(!(bool)boundary.Contains(center + Vector3.UnitX * 27, 0f), "DRN3 actual reduced polygon");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN3 actual feature ring no TTL Map low high words reduced polygon reverse Seek owner");

        load(id, [Header(936), World(0, [Boss(), Player(), Player(101), Helper(Spell(23187)),
            Actor(31, 0x30BE, center + Vector3.UnitZ * 8), Actor(32, 0x30BD, center + Vector3.UnitX * 8)], true),
            World(1, [Helper()]), Effect(2, 23403), World(20, []), Effect(21, 22525), World(22, [])]);
        Seek(20.1); Check(Shown().Length == 2 && dangers() == 2, "DRN3 cold Created Start order two circles no TTL Finish short effect");
        Seek(21.1); Check(Shown().Length == 0 && dangers() == 0, "DRN3 long effect clears all");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN3 actual AboveBoard cold bomb ordering original clocks no Finish short effect long clears owner");

        object Ward(object[] statuses, object? cast = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(30, 0x30BB, center, cast: cast, subKind: 5))!;
            node["Statuses"] = JsonSerializer.SerializeToNode(statuses);
            return node;
        }
        var status = new { Id = 680, Param = 3, SourceId = 0UL, ExpiresAt = Start.AddSeconds(100), SlotIndex = 59 };
        load(id, [Header(936), World(0, [Boss(), Player(), Player(101), Helper(), Ward([status], Spell(22519))], true),
            World(1, [Ward([])]), World(2, [Ward([], Spell(22519, 2))]), World(3, [])]);
        Seek(.1);
        Check(Lines().Length > 0 && dangers() == 2 && ((string)Read(Script(), "MechanicHintsText")).Contains("unshielded")
            && (ulong)Read(Script(), "PlannedTargetId") == 0 && Read(Script(), "PlannedFacing") == null,
            "DRN3 actual cold true status overwrites prediction arcs target conditioned navigation diagnostic only");
        Seek(1.1); Check(Lines().Length == 0 && dangers() == 0, "DRN3 Lose actual bits clear");
        Seek(2.1);
        Check(Lines().Length > 0 && dangers() == 0, "DRN3 prediction side safe position retains arcs no nav");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN3 actual CoatOfArms cold status Gain Lose predicted arcs safe side navigation no new PR target execution");

        load(id, [Header(936), World(0, [Boss(), Player(), Player(101), Helper(Spell(22541, location: center + Vector3.UnitZ * 20)),
            Actor(40, 0x30C2, center), Actor(41, 0x30C2, center + Vector3.UnitZ * 15)], true),
            World(1, [Helper()]), Effect(2, 22542), World(20, []), Effect(21, 22543, 40),
            Effect(22, 22543, 41), World(23, [])]);
        Seek(.1); Check(Shown().Length == 3 && dangers() == 3, "DRN3 half count expands all turrets");
        Seek(20.1); Check(Shown().Length == 2 && dangers() == 2, "DRN3 first source actual Effect no TTL");
        Seek(22.1); Check(Shown().Length == 0 && dangers() == 0, "DRN3 each turret effect consumes");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN3 actual TurretsTour half count expands exact chain source Effect no Finish TTL reverse Seek owner");
    }
}

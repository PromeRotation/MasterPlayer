using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDelubrumQueen(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.delubrum.normal";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-272, 0, -415);
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
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")).Contains("bozja/aoe/")).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        object Spell(uint action, double at = 0, Vector3? location = null)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, location ?? center, .4f, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Boss(object? cast = null) => Actor(1, 0x310B, center, combat: true, isEnemy: true, cast: cast);
        object Player(object[]? statuses = null)
        {
            var node = JsonSerializer.SerializeToNode(Actor(100, 0, center))!;
            node["Statuses"] = JsonSerializer.SerializeToNode(statuses ?? []);
            return node;
        }
        object Helper(object? cast = null) => Actor(20, 0x233C, center + Vector3.UnitX * 10, cast: cast, subKind: 5);
        string World(double at, IEnumerable<object> actors, bool full = false)
            => Frame(936, at, actors, full, party: [new { Slot = 0, EntityId = 100UL }], sourcePlayerId: 100);
        string Effect(double at, uint action, ulong source = 20) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                GlobalSequence = (uint)(at * 100), Timestamp = Start.AddSeconds(at) },
        });
        string Map(double at, ushort low, ushort high) => JsonSerializer.Serialize(new
        {
            t = at, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 0x19, Param1 = low, Param2 = high, Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DRN6 replay error: " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(previews().Length == 0 && dangers() == 0 && Lines().Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "DRN6 owner release");
        }
        foreach (var action in new uint[] { 22980, 23002, 22988, 22989, 23426, 23427, 22983 })
        {
            load(id, [Header(936), World(0, [Boss(), Player(), Helper(Spell(action))], true),
                Effect(1, action), World(20, []), World(21, [Helper()]), World(22, [])]);
            Seek(.1);
            Check((bool)Read(Model(), "Active") && Shown().Length == 1 && dangers() >= 1, "DRN6 standard geometry");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 23453 }),
                "DRN6 exact source ownership");
            if (action == 22980)
            {
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = Snapshot();
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                var target = (Vector3)Read((object)result, "TargetPosition");
                var castOrigin = center;
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance(castOrigin, target) > 20
                    && (bool)((dynamic)Read(snapshot, "ArenaBoundary")).Contains(target, 0f), "DRN6 real R20 solver");
            }
            Seek(20.1); Check(Shown().Length == 1, "DRN6 no Effect TTL");
            Seek(21.1); Check(Shown().Length == 0 && dangers() == 0, "DRN6 Finish all ordinary or KB clears");
            Seek(.1); Release();
            Console.WriteLine($"PASS native DRN6 {action} real DLL geometry ownership Finish owner reverse Seek");
        }
        load(id, [Header(936), World(0, [Boss(Spell(22984)), Player(), Helper()], true), World(20, []),
            Map(21, 1, 2), Map(22, 0x10, 0x20), World(23, [])]);
        Seek(20.1); Check(Shown().Length == 1 && dangers() == 1, "DRN6 ring survives overdue");
        Seek(21.1); Check(Shown().Length == 0 && dangers() == 0, "DRN6 shrink clears preview");
        dynamic boundary = Read(Snapshot(), "ArenaBoundary");
        Check(!(bool)boundary.Contains(center + new Vector3(24, 0, 24), 0f), "DRN6 default polygon excludes corner");
        Seek(22.1); boundary = Read(Snapshot(), "ArenaBoundary");
        Check((bool)boundary.Contains(center + new Vector3(24, 0, 24), 0f), "DRN6 chess square restores corner");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN6 arena ring raw clock Map words polygon and square reverse Seek owner");

        load(id, [Header(936), World(0, [Boss(), Player(), Helper(Spell(23360)),
            Actor(31, 0x310F, center + Vector3.UnitZ * 8), Actor(32, 0x310E, center + Vector3.UnitX * 8)], true),
            World(1, [Helper()]), Effect(2, 22995), World(20, []), Effect(21, 22994), World(22, [])]);
        Seek(20.1); Check(Shown().Length == 2 && dangers() == 2, "DRN6 cold bombs retain short Effect and Finish");
        Seek(21.1); Check(Shown().Length == 0 && dangers() == 0, "DRN6 long Effect clears");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN6 AboveBoard cold Created 21s replacement 15.1f actual Effect no TTL owner");

        load(id, [Header(936), World(0, [Boss(), Player(), Helper(Spell(23008, location: center + Vector3.UnitZ * 20)),
            Actor(40, 0x3113, center), Actor(41, 0x3113, center + Vector3.UnitZ * 15)], true),
            World(1, [Helper()]), Effect(2, 23009), World(20, []), Effect(21, 23010, 40), Effect(22, 23010, 41), World(23, [])]);
        Seek(.1); Check(Shown().Length == 3 && dangers() == 3, "DRN6 half turret count expands");
        Seek(20.1); Check(Shown().Length == 2 && dangers() == 2, "DRN6 first source Effect");
        Seek(22.1); Check(Shown().Length == 0 && dangers() == 0, "DRN6 all source Effect consumed");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN6 TurretsTour exact half cache source FIFO actual Effect owner reverse Seek");

        object Status(uint sid, double expires = 100) => new { Id = sid, Param = 0, SourceId = 0UL, ExpiresAt = Start.AddSeconds(expires), SlotIndex = 59 };
        load(id, [Header(936), World(0, [Boss(), Player([Status(2474)]), Helper()], true),
            World(1, [Player([Status(2480)])]), World(2, [Player([])]), World(3, [])]);
        Seek(.1);
        Check(Shown().Length == 8 && dangers() == 0 && ((string)Read(Script(), "MechanicHintsText")).Contains("Prepare"), "DRN6 all eight pending tiles display zero converted nav");
        Check(Shown().All(p => (bool)Read(p, "Safe")), "DRN6 safe color tiles");
        Seek(1.1);
        Check(Shown().Length == 8 && dangers() == 0 && ((string)Read(Script(), "MechanicHintsText")).Contains("Move to a safe tile"),
            "DRN6 eight real tiles preserve source intersection sampling rejection above 65 percent");
        Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DRN6 state Lose clears");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN6 Chess actual slot59 pending display real tiles sampled intersection Lose owner reverse Seek");

        load(id, [Header(936), World(0, [Boss(Spell(22981)), Player([Status(910)]), Helper()], true),
            World(1, [Boss(Spell(22985, 1)), Player([])]), World(2, [Boss()]), World(3, [])]);
        Seek(.1);
        Check(((string)Read(Script(), "MechanicHintsText")).Contains("doomed")
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Tankbuster with doom")
            && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1, "DRN6 Doom plus original damage");
        Seek(1.1); Check(((string)Read(Script(), "MechanicHintsText")).Contains("Raidwide"), "DRN6 global raidwide");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN6 Doom real status original damage global cast hints diagnostic only replay isolation");

        var kb = JsonSerializer.SerializeToNode(Spell(22983))!;
        kb["Rotation"] = 0;
        var immune = JsonSerializer.SerializeToNode(Player([Status(160)]))!;
        immune["Position"] = JsonSerializer.SerializeToNode(new { X = center.X + 15, Y = center.Y, Z = center.Z });
        load(id, [Header(936), World(0, [Boss(), immune,
            Actor(20, 0x233C, center - Vector3.UnitX * 15, cast: kb, subKind: 5)], true),
            World(1, [Actor(100, 0, center + Vector3.UnitX * 15)]), World(2, [])]);
        Seek(.1);
        Check(Lines().Length == 0
            && !((string?)Read(Script(), "MechanicHintsText") ?? "").Contains("knocked into danger")
            && ((IEnumerable)Read(Snapshot(), "GoalAreas")).Cast<object>().Count() == 1,
            "DRN6 cold immune removes projection while source triangle Goal persists");
        Seek(1.1); Check(Lines().Length > 0, "DRN6 real immunity Lose restores projection");
        Seek(.1); Release();
        Console.WriteLine("PASS native DRN6 cold knockback immunity slot59 suppresses lines hint preserves triangle Goal hot Lose reverse Seek owner");
    }
}

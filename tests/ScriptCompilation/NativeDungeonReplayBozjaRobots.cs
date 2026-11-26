using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBozjaRobots(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const string mechanism = "新型铁巨人——魔导劳工X式";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(104, 0, 237);
        object? Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj);
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj);
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script")!;
        object Model() => Read(Script(), "encounter")!;
        object Snapshot() => runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        int Goals() => ((IEnumerable)Read(Snapshot(), "GoalAreas")!).Cast<object>().Count();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")).ToArray();
        string Text() => (string?)Read(Script(), "MechanicHintsText") ?? "";
        object Spell(uint action, double at = 0, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 8, center, rotation, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 8;
            return node;
        }
        object Boss(object? cast = null) => Actor(1, 0x2E8C, center, combat: true, cast: cast, subKind: 5, isEnemy: true);
        object Helper(ulong actorId = 20, object? cast = null, uint oid = 0x233C)
            => Actor(actorId, oid, center, cast: cast);
        object Player(uint hp = 7, int marches = 0, double expiry = 15)
        {
            var node = JsonSerializer.SerializeToNode(Actor(100, 0, center))!;
            node["Hp"] = 1; node["MaxHp"] = hp;
            node["Statuses"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, marches).Select(i =>
                new { Id = i == 0 ? 1293u : 1295u, SlotIndex = i == 0 ? 0 : 59,
                    ExpiresAt = Start.AddSeconds(expiry + i), SourceId = 1UL }).ToArray());
            return node;
        }
        string World(double at, IEnumerable<object> actors, bool full = false)
            => Frame(920, at, actors, full, party: [
                new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Event(double at, uint value, bool icon = false) => JsonSerializer.Serialize(new
        {
            t = at, kind = icon ? "TargetIcon" : "ActionEffect",
            type = icon ? "PromeRotation.LogSystem.LogSystemTargetIconEvent" : "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = icon ? (object)new { SourceId = 1UL, TargetId = 100UL, MarkerId = value, Timestamp = Start.AddSeconds(at) }
                : new { SourceId = 20UL, ActionId = value, ActionType = 1, MainTargetId = 100UL,
                    GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "CE32 actual replay error " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(dangers() == 0 && Goals() == 0 && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "CE32 owner release");
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [920u, 0x2E8Cu])! && !(bool)owns.Invoke(null, [975u, 0x2E8Cu])!
            && !(bool)owns.Invoke(null, [920u, 0x2EE8u])!, "CE32 exact region primary ownership");
        load(id, [Header(920), World(0, [Boss(Spell(20671)), Player()], true),
            World(1, [Boss()]), World(2, [Helper(30, oid: 0x1EB032)]),
            World(3, [Boss(Spell(20671, 3))]), World(4, [Boss()]), World(5, [])]);
        Seek(.1); Check(Shown().Length == 0 && dangers() == 0, "CE32 arena waits Finish");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = 20671 }), "CE32 source suppression");
        Seek(1.1); Check(Shown().Length == 1 && dangers() == 1, "CE32 Finish donut");
        Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "CE32 Created clears donut");
        Seek(4.1); Check(Shown().Length == 1 && dangers() == 1, "CE32 after wall Finish reappears");
        Seek(.1); Check(Shown().Length == 0, "CE32 reverse Seek arena");
        Release();
        Console.WriteLine("PASS native CE32 actual Finish arena wall Created later Finish reverse Seek ownership suppression");

        foreach (var hp in new uint[] { 6, 7 })
        {
            load(id, [Header(920), World(0, [Boss(), Player(hp), Helper(cast: Spell(20677))], true),
                World(1, [Helper()]), World(2, [])]);
            Seek(.1);
            Check(Shown().Length == 1 && (bool)Read(Shown()[0], "Safe")! == (hp == 7), "CE32 math safe display color");
            Check(Goals() == 0 && (hp == 7 ? dangers() == 0 : dangers() > 0), "CE32 safe polygon sampling rejection not invented Goal");
            Check(Text().Contains(hp == 6 ? "Avoid marked towers!" : "Move into a marked tower!"), "CE32 math current MaxHP text");
            if (hp == 6)
            {
                var snapshot = Snapshot();
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                var origin = center - Vector3.UnitZ * 20;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [origin, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!,
                    "CE32 math actual solver valid exit");
            }
            Seek(1.1); Check(Shown().Length == 0 && dangers() == 0, "CE32 true Finish clears towers");
            Seek(.1); manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0, "CE32 disabled release");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1);
            Release();
            Console.WriteLine($"PASS native CE32 actual math MaxHP={hp} polygon color separate risk host conversion solver Finish owner");
        }

        load(id, [Header(920), World(0, [Boss(Spell(20672)), Player(), Helper(cast: Spell(20674, rotation: MathF.PI))], true),
            Event(1, 236, true), Event(2, 20675),
            Frame(920, 2.5, [], party: [], sourcePlayerId: 100), World(2.75, []),
            Event(3, 20673), World(30, [])]);
        Seek(.1); Check(Shown().Length == 0, "CE32 cold missing icon no guesses");
        Seek(1.1); Check(Shown().Length == 4 && dangers() == 4 && Goals() == 1, "CE32 double future imminent plus fixed near3");
        Seek(2.1); Check((int)Read(Model(), "RobotNumCasts")! == 0 && Shown().Length == 4, "CE32 back Effect ignored");
        Seek(2.6); Check(previews().Length == 0 && dangers() == 0 && Goals() == 0
            && Read(Snapshot(), "ArenaBoundary") == null, "CE32 no-party viewer sleeps outputs");
        Seek(3.1); Check((int)Read(Model(), "RobotNumCasts")! == 2 && Shown().Length == 4, "CE32 front Effect consumes both");
        Seek(30); Check(Shown().Length == 4, "CE32 overdue retained without Effect");
        Seek(.1); Check(Shown().Length == 0, "CE32 reverse Seek clears absent icon");
        Release();
        Console.WriteLine("PASS native CE32 actual cold no icon dual rotating front only Effect hidden cache overdue fixed goal3 reverse Seek owner");

        load(id, [Header(920), World(0, [Boss(), Player(marches: 2), Helper(cast: Spell(20677))], true),
            World(14.5, []), World(17, [Player(marches: 0)]), World(18, [])]);
        Seek(.1); Check(Goals() == 1 && Read(Script(), "PlannedFacing") == null, "CE32 first pending goal before lead window");
        Seek(14.5); Check(Read(Script(), "PlannedFacing") != null, "CE32 planned center plus PI in lead window");
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility")!;
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>()
            .Where(l => ((string)Read(l, "Id")!).Contains("bozja/robots/march/")).ToArray();
        Check(Lines().Length == 2 && Equals(Read(Lines()[0], "End"), Read(Lines()[1], "Start")), "CE32 true slots ordered two march lines");
        Seek(17.1); Check(Goals() == 0 && Read(Script(), "PlannedFacing") == null && Lines().Length == 0, "CE32 Lose releases march outputs");
        Release();
        Console.WriteLine("PASS native CE32 actual march slots first pending safe fan goal ordered lines lead facing plan replay execution isolation");

        load(id, [Header(920), World(0, [Boss(), Player(6), Helper(cast: Spell(20677))], true),
            World(1, [Player(10, 1), Helper(21, Spell(20678, 1))]),
            World(2, [Player(7, 1), Helper(21, Spell(20679, 2))]), World(3, [])]);
        Seek(1.1); Check(Read(Model(), "RobotNavigationError") != null && Text().Contains("缺少数学候选")
            && dangers() == 0 && Goals() == 0 && Read(Snapshot(), "ArenaBoundary") == null && Shown().Length == 1,
            "CE32 source failing math withdraws whole navigation retains drawing");
        Seek(2.1); Check(Read(Model(), "RobotNavigationError") == null && Goals() == 1
            && Read(Snapshot(), "ArenaBoundary") != null, "CE32 valid math recovers navigation");
        Release();
        Console.WriteLine("PASS native CE32 actual source missing candidates diagnostic whole navigation boundary facing withdrawal preserves drawings recovers");

        foreach (var action in new uint[] { 20687, 20681, 21030 })
        {
            load(id, [Header(920), World(0, [Boss(), Player(), Helper(cast: Spell(action))], true),
                World(1, [Helper()]), World(2, [])]);
            Seek(.1);
            Check((Read(Script(), "PredictedDamageText") != null) == (action != 21030)
                && Shown().Length == 0 && dangers() == 0, "CE32 Incinerate actual watched set");
            Seek(1.1); Check(Read(Script(), "PredictedDamageText") == null, "CE32 dynamic Finish");
            Release();
            Console.WriteLine($"PASS native CE32 actual raidwide watched={action} dynamic diagnosis no invented circle true Finish owner");
        }
    }
}

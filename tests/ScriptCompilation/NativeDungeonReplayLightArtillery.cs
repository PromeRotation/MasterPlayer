using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunLightArtilleryReplay(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.alliance.lightartillery";
        const string heavy = "masterplayer.alliance.heavyartillery";
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
        var center = new Vector3(-230, 0, -15);
        object Boss(bool combat = true, bool dead = false, bool hp0 = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x2EE4, center, combat: combat, dead: dead, subKind: 5))!;
            if (hp0) node["Hp"] = 0;
            return node;
        }
        object Player(ulong actor = 100) => Actor(actor, 0, center + Vector3.UnitX * (actor == 100 ? 8 : 12), subKind: 4);
        object Npc(uint action = 0, ulong actor = 20, ulong target = 100, bool dead = false)
        {
            var cast = action == 0 ? null : JsonSerializer.SerializeToNode(Cast(action, 5, center, 0, target, 0));
            if (cast != null) { cast["IsSpell"] = true; cast["TotalTime"] = 5; }
            return Actor(actor, 0x233C, center, cast: cast, subKind: 5, dead: dead);
        }
        object Fire(byte state = 0, Vector3? point = null, bool dead = false)
            => Actor(30, 0x1E8D9B, point ?? center, rotation: 1.37f, eventState: state, dead: dead, subKind: 5);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(917, at, actors, full, removed,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Effect(double at, uint action, uint sequence = 1)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 20UL, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                    GlobalSequence = sequence, Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Light actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Light replay never executes PR facing target or action hold");
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                && Items(Snapshot(), "HardObstacles").Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Light owner release");
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
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Light actual solver safe route");
        }
        foreach (var action in new uint[] { 21007, 21010, 21009, 21011 })
        {
            var shape = action is 21007 or 21010;
            load(id, [Header(917), World(0, [Boss(false), Player(), Player(101), Npc(action)], true),
                World(1, [Boss()]), World(2, []), World(3, [Npc()]), World(4, [])]);
            Seek(.3); Check(dangers() == 0 && previews().Length == 0, "Light inactive cold input no output");
            Seek(1.1); Check(Views().Length == (shape ? 1 : 0) && dangers() == (shape ? 1 : 0)
                && Read(Snapshot(), "ArenaBoundary") != null, "Light cold current cast reaches exact output family");
            if (shape) Solver(center + Vector3.UnitX * 8);
            if (action == 21009)
                Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Interrupt") == true
                    && Items(Script(), "PredictedDamage").Length == 0, "Light VoltArray interrupt not fabricated raidwide");
            if (action == 21011)
                Check(Items(Script(), "PredictedDamage").Length == 1
                    && ((string?)Read(Script(), "MechanicHintsText"))?.Contains("Tankbuster") == true,
                    "Light MartialArm original raw target damage diagnostic and global hint");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Light actual NPC source suppression");
            manager.SetMechanismEnabled(id, "轻型陆战装置", false); Seek(1.2);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Light switch releases owner");
            manager.SetMechanismEnabled(id, "轻型陆战装置", true); Seek(1.1);
            Seek(3.1); Check(Views().Length == 0 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 0,
                "Light ordinary watched Finish clears output");
            Release();
            Console.WriteLine($"PASS native Alliance trash actual Light cold standard {action} Finish solver diagnostic switch owner Seek");
        }
        foreach (var target in new ulong[] { 100, 101 })
        {
            load(id, [Header(917), World(0, [Boss(), Player(), Player(101), Npc(21008, target: target)], true),
                Effect(.5, 21008), World(1, []), World(2, [Npc(21008, target: target, dead: true)]), World(3, [Npc()]), World(4, [])]);
            Seek(1.1); Check(Views().Length == 1 && (bool)Read(Views()[0], "OutlineOnly")! == (target == 100)
                && dangers() == (target == 100 ? 2 : 1) && Items(Script(), "PredictedDamage").Length == 1
                && (int)Read(Model(), "LightHomingCount")! == 1 && Items(Model(), "Casts").Length == 1,
                "Light original Homing target outline or fill and Effect retains bait");
            if (target == 100) Solver(center + Vector3.UnitX * 8);
            Seek(2.1); Check(Views().Length == 0 && dangers() == 0 && Items(Model(), "Casts").Length == 1,
                "Light source Dead hides every view and custom edge while preserving raw bait");
            Seek(1.1); Check(Views().Length == 1, "Light reverse Seek rebuilds original Homing target");
            Seek(3.1); Check(Views().Length == 0 && dangers() == 0, "Light real watched Finish consumes Homing");
            Release();
            Console.WriteLine($"PASS native Alliance trash actual Light Homing target {target} outline fill edge solver Effect source death Seek");
        }
        var raw = center + new Vector3(.137f, 0, -.193f);
        load(id, [Header(917), World(0, [Boss(), Player(), Fire(point: raw, dead: true)], true),
            World(1, [Fire(7)]), World(2, [Fire(point: raw + Vector3.UnitZ)]), World(3, [], removed: [30]), World(4, [])]);
        Seek(.3); Check(Views().Length == 1 && dangers() == 0 && Items(Snapshot(), "HardObstacles").Length == 1
            && (Vector3)Read(Read(Views()[0], "Geometry")!, "Position")! == raw, "Light fire raw circle visual plus obstacle without predicted danger");
        Solver(center + Vector3.UnitX * 8);
        Seek(1.1); Check(Views().Length == 0 && Items(Snapshot(), "HardObstacles").Length == 0, "Light EventState seven releases obstacle");
        Seek(2.1); Check(Views().Length == 1 && Items(Snapshot(), "HardObstacles").Length == 1, "Light current raw fire position reappears");
        Seek(3.1); Check(Views().Length == 0 && Items(Snapshot(), "HardObstacles").Length == 0, "Light real Destroy releases obstacle");
        Seek(.3); Check(Views().Length == 1, "Light raw fire reverse Seek");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Light fire raw dead actor EventState7 real obstacle solver Destroy reverse Seek");

        foreach (var count in new[] { 3, 4 })
        {
            var rows = new List<string> { Header(917), World(0, [Boss(), Player(), Npc()], true) };
            rows.Add(World(2, [Boss(dead: true, hp0: true)])); rows.Add(World(3, []));
            rows.Add(World(4, [], removed: [1])); rows.Add(World(5, []));
            load(id, rows); Seek(.3);
            // Playback updates after every row, even at equal timestamps.
            // Route the real packets without intervening ticks to test a live batch.
            var pair = ((IEnumerable)Read(controllerObject, "_routers")!).Cast<object>()
                .Single(p => (string)Read(p, "Item1")! == id);
            var router = Read(pair, "Item2")!;
            foreach (var sequence in Enumerable.Range(1, count))
            {
                using var packet = JsonDocument.Parse(Effect(.3, 21013, (uint)sequence));
                var effect = JsonSerializer.Deserialize(packet.RootElement.GetProperty("data").GetRawText(),
                    assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvent")!)!;
                router.GetType().GetMethod("Raise")!.Invoke(router, [effect]);
            }
            Check(Items(Script(), "pendingEvents").Length == count
                && (int)Read(Model(), "LightSelfDestructCount")! == 0,
                "Light all routed Effects await one Framework commit");
            managerObject.GetType().GetMethod("UpdateReplayScripts", flags)!.Invoke(managerObject, [0f]);
            Check(Items(Script(), "pendingEvents").Length == 0 && (int)Read(router, "ErrorCount")! == 0,
                "Light one real Framework commit consumes entire native Effect batch");
            Check((bool)controllerObject.GetType().GetMethod("DispatchUpTo", flags)!.Invoke(controllerObject, [2.1])!,
                "Light forward playback retains injected batch");
            Check((bool)Read(Model(), "Active")! == (count == 4) && (int)Read(Model(), "LightSelfDestructCount")! == count,
                "Light exact third Effect completes while fourth in same commit overshoots; HP0 Dead alone retained");
            Check((bool)controllerObject.GetType().GetMethod("DispatchUpTo", flags)!.Invoke(controllerObject, [4.1])!,
                "Light forward playback reaches Destroy");
            Check(!(bool)Read(Model(), "Active")!, "Light all Boss Destroyed completes remaining module");
            Release();
            Console.WriteLine($"PASS native Alliance trash actual Light {count} same commit self destruct exact equality Dead HP0 Destroy");
        }

        var heavyCenter = new Vector3(200, 0, -216.25f);
        manager.SetScriptEnabled(heavy, true);
        load(id, [Header(917), World(0, [Boss(), Player(),
            Actor(2, 0x2E83, heavyCenter, combat: true, subKind: 5), Npc(21007),
            Actor(21, 0x233C, heavyCenter, cast: Cast(20657, 5, heavyCenter), subKind: 5)], true),
            World(1, [])]);
        Seek(.3); Check(dangers() == 2 && manager.Running.ContainsKey(heavy), "Light Heavy distinct keys share territory with active owners");
        manager.SetScriptEnabled(id, false); controller.Seek(.4); Check(dangers() == 1, "Light stop preserves Heavy owner");
        manager.SetScriptEnabled(id, true); Seek(.3); Check(dangers() == 2, "Light Heavy reverse Seek restores owners");
        manager.SetScriptEnabled(heavy, false); Seek(.4); Check(dangers() == 1, "Heavy stop preserves Light owner");
        Release(); manager.SetScriptEnabled(heavy, true);
        Console.WriteLine("PASS native Alliance trash actual Light Heavy same territory two owner bidirectional release reverse Seek");
    }
}

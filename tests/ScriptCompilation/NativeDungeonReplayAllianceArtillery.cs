using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAllianceArtillery(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.alliance.heavyartillery";
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
        var center = new Vector3(200, 0, -216.25f);
        object Boss(bool combat = true, Vector3? point = null, bool dead = false, bool hp0 = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x2E83, point ?? center, combat: combat, dead: dead, subKind: 5))!;
            if (hp0) node["Hp"] = 0;
            return node;
        }
        object Player(Vector3? point = null) => Actor(100, 0, point ?? center + Vector3.UnitX * 8, subKind: 4);
        object Npc(uint action = 0, ulong actor = 20, double at = 0, float total = 5)
        {
            var cast = action == 0 ? null : JsonSerializer.SerializeToNode(Cast(action, total, center, 0, 100, at));
            if (cast != null) { cast["IsSpell"] = true; cast["TotalTime"] = total; }
            return Actor(actor, 0x233C, center, cast: cast, subKind: 5);
        }
        object Energy(ulong actor = 30, Vector3? point = null, float angle = 0, bool dead = false)
            => Actor(actor, 0x2E84, point ?? center, rotation: angle, dead: dead, subKind: 5);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(917, at, actors, full, removed, party: [new { Slot = 0, EntityId = 100UL }], sourcePlayerId: 100);
        string Map(double at, byte index, uint state)
            => JsonSerializer.Serialize(new { t = at, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(at) } });
        string Effect(double at, ulong source = 30)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = 20658u, ActionType = 1, MainTargetId = 100UL,
                    GlobalSequence = (uint)(at * 10000), Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Heavy actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Heavy replay never executes PR facing target or action hold");
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Heavy owner release");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Heavy actual solver safe route");
        }

        load(id, [Header(917), World(0, [Boss(false), Player(), Npc(20657)], true),
            World(1, [Boss()]), World(2, []), World(3, [Npc()]), World(4, [])]);
        Seek(.3); Check(dangers() == 0 && previews().Length == 0, "Heavy inactive laser has no output");
        Seek(1.1); Check(Views().Length == 1 && dangers() == 1 && Read(Snapshot(), "ArenaBoundary") != null,
            "Heavy cold current laser reaches view and real navigation");
        Solver(center + Vector3.UnitX * 8);
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 20657 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = 20657 }),
            "Heavy suppression actual NPC source only");
        manager.SetMechanismEnabled(id, "重型陆战装置", false); Seek(1.2);
        Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Heavy mechanism releases owner");
        manager.SetMechanismEnabled(id, "重型陆战装置", true); Seek(1.1);
        Check(Views().Length == 1 && dangers() == 1, "Heavy reverse Seek restores cold laser");
        Seek(3.1); Check(Views().Length == 0 && dangers() == 0, "Heavy actual Finish clears laser");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Heavy cold laser solver mechanism switch Seek owner");

        load(id, [Header(917), World(0, [Boss(), Player(), Npc(20657, total: 3),
            Npc(20657, 21, total: 4), Npc(20657, 22, total: 2), Npc(20657, 23, total: 3.5f)], true),
            World(1, [Npc(actor: 21)]), World(2, []), World(3, [Npc()]),
            World(4, [Npc(actor: 22)]), World(5, [Npc(actor: 23)]), World(6, [])]);
        Seek(.3); Check(Views().Length == 1 && Items(Model(), "Casts").Length == 4, "Heavy strict one second prefix hides earlier tail");
        Seek(1.1); Check(Views().Length == 3 && Items(Model(), "Casts").Length == 3, "Heavy hidden Finish promotes continuous prefix");
        Seek(5.1); Check(Views().Length == 0 && dangers() == 0, "Heavy every original Finish consumed");
        Seek(.3); Check(Items(Model(), "Casts").Length == 4, "Heavy reverse Seek rebuilds hidden list");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Heavy strict window hidden Finish queue and reverse Seek");

        load(id, [Header(917), World(0, [Boss(false), Player(), Energy()], true), World(1, [Boss()]),
            World(2, [Energy(point: center + new Vector3(.137f, 0, -.193f), angle: 1.37f, dead: true)]),
            World(100, []), Effect(101, 20), Effect(102), World(103, [])]);
        Seek(.3); Check(Views().Length == 0 && dangers() == 0, "Heavy inactive Energy no output");
        Seek(1.1); Check(Views().Length == 1 && dangers() >= 2 && Items(Model(), "HeavyEnergy").Length == 1,
            "Heavy cold Created reaches capsule visual and two separate union navigation clocks");
        Solver(center);
        Seek(100.1); Check(Views().Length == 1 && dangers() >= 2 && (DateTime)Read(Model(), "Now")! > Start.AddSeconds(99),
            "Heavy dead Energy no TTL and future geometry keeps refreshing");
        Seek(101.1); Check(Views().Length == 1, "Heavy foreign Effect leaves original Energy");
        Seek(102.1); Check(Views().Length == 0 && dangers() == 0, "Heavy real original Effect clears both union clocks");
        Seek(2.1); Check(Views().Length == 1 && dangers() >= 2, "Heavy reverse Seek restores raw Energy");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Heavy cold Energy raw move death two clocks no TTL Effect solver");

        load(id, [Header(917), World(0, [Boss(), Player(), Energy()], true),
            World(1, [], removed: [30]), World(2, [Energy(point: center + Vector3.UnitZ * 8)]), Effect(3), World(100, [])]);
        Seek(1.1); Check(Views().Length == 0 && dangers() == 0, "Heavy Destroy removes original Energy");
        Seek(2.1); Check(Views().Length == 1 && Items(Model(), "HeavyEnergy").Length == 1, "Heavy same ID new generation Created once");
        Seek(100.1); Check(Views().Length == 0 && dangers() == 0, "Heavy consumed present Energy does not respawn on scans");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Heavy Energy Destroy same ID generation consumed present object");

        foreach (var z in new[] { -216.25f, -316.532f, -416.5f })
        {
            var original = new Vector3(200, 0, z);
            load(id, [Header(917), World(0, [Boss(point: original), Player(original + Vector3.UnitX * 8)], true),
                Map(.5, 0x0D, 0), World(1, []), Map(2, 0x0E, 0xFFFFFFFF), World(3, [])]);
            Seek(1.1);
            dynamic boundary = Read(Snapshot(), "ArenaBoundary")!;
            Check((Vector3)Read(Model(), "Center")! == original && (int)Read(Model(), "HeavyArenaStage")! == 3,
                "Heavy Map any state preserves construction center");
            Check(!(bool)boundary.Contains(original + Vector3.UnitZ * -18.5f, 0f)
                && (bool)boundary.Contains(original, 0f), "Heavy mapped third-b barrier remains relative to original center");
            Solver(original + Vector3.UnitX * 8);
            Seek(2.1); boundary = Read(Snapshot(), "ArenaBoundary")!;
            Check((int)Read(Model(), "HeavyArenaStage")! == 4
                && (bool)boundary.Contains(original + Vector3.UnitZ * -18.5f, 0f), "Heavy Map second index removes final barrier");
            Seek(1.1); Check((int)Read(Model(), "HeavyArenaStage")! == 3, "Heavy Map reverse Seek");
            Release();
            Console.WriteLine($"PASS native Alliance trash actual Heavy stage {z} exact Map barrier original center solver Seek");
        }

        load(id, [Header(917), World(0, [Boss(false), Player(new(2000, 0, 0))], true),
            World(1, [Boss(false, new(200, 0, -416.5f))]), World(2, [Player(new(200, 0, -416.5f))]),
            World(3, [Boss(point: new(200, 0, -416.5f))]), World(4, [])]);
        Seek(2.1); Check((Vector3)Read(Model(), "Center")! == center, "Heavy pending arena frozen before primary moves");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Heavy pending constructor arena frozen before position changes");

        load(id, [Header(917), World(0, [Boss(false), Player()], true), Map(.5, 0x0E, 0),
            World(1, [Actor(25, 0x2E85, center, subKind: 5)]), World(2, [Boss(false, hp0: true)]),
            World(3, [Boss(false, dead: true)]), World(4, [])]);
        Seek(1.1); Check((bool)Read(Model(), "Active")! && (int)Read(Model(), "HeavyArenaStage")! == 0,
            "Heavy targetable barricade pulls but cold activation never invents historical Map");
        Seek(2.1); Check((bool)Read(Model(), "Active")!, "Heavy HP0 is not Dead completion");
        Seek(3.1); Check(!(bool)Read(Model(), "Active")! && previews().Length == 0
            && Read(Snapshot(), "ArenaBoundary") == null, "Heavy real Dead completion releases owner");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Heavy barricade pull no historical Map HP0 preserved Dead completion");
    }
}

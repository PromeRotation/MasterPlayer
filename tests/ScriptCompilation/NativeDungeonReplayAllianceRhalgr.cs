using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAllianceRhalgr(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.alliance.rhalgr";
        const string lions = "masterplayer.alliance.lions";
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
        var catalog = assembly.GetType("MasterPlayer.Mechanics.Bozja.AllianceTrashCatalog")!;
        var definition = catalog.GetField("Rhalgr", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = (Vector3)Read(definition, "Center")!;
        object Spell(uint action, double at = 0, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, center, .47f, target, at))!;
            node["TotalTime"] = 5; node["IsSpell"] = true; return node;
        }
        object Boss(bool combat = true, object? cast = null, bool hp0 = false)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x38FA, center, combat: combat, cast: cast, subKind: 5))!;
            if (hp0) node["Hp"] = 0;
            return node;
        }
        object Npc(uint action = 0, ulong actor = 20, double at = 0, ulong target = 100)
            => Actor(actor, 0x233C, center, cast: action == 0 ? null : Spell(action, at, target), subKind: 5);
        object Player(ulong actor = 100) => Actor(actor, 0, center + Vector3.UnitX * (actor == 100 ? 10 : 15), subKind: 4);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(1054, at, actors, full, removed, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }],
                sourcePlayerId: 100);
        string Map(double at, uint state)
            => JsonSerializer.Serialize(new { t = at, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
                data = new { Position = 0x25, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(at) } });
        string Effect(double at, uint action, ulong source = 20)
            => JsonSerializer.Serialize(new { t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                    GlobalSequence = (uint)(at * 10000), Timestamp = Start.AddSeconds(at) } });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "Rhalgr actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            var hints = Read(Script(), "Hints")!;
            Check(!(bool)Read(Read(hints, "_facing")!, "_hasRequest")! && (ulong)Read(hints, "target")! == 0
                && !(bool)Read(hints, "holdActions")! && (ulong)Read(Script(), "PlannedTargetId")! == 0,
                "Rhalgr replay never executes PR facing target or action hold");
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Items(Snapshot(), "GoalAreas").Length == 0
                && Read(Snapshot(), "ArenaBoundary") == null, "Rhalgr owner release");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "Rhalgr actual solver safe route");
        }
        foreach (var action in new uint[] { 28898, 28896 })
        {
            load(id, [Header(1054), World(0, [Boss(false), Player(), Player(101), Npc(action)], true),
                World(1, [Boss()]), World(2, []), World(3, [Npc()]), World(4, [])]);
            Seek(.3); Check(dangers() == 0 && previews().Length == 0, "Rhalgr inactive current cast has no output");
            Seek(1.1);
            Check(Views().Length == 1 && dangers() == 1 && Read(Snapshot(), "ArenaBoundary") != null,
                "Rhalgr current NPC cold replay reaches both views and navigation");
            Solver(center + Vector3.UnitX * 12);
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Rhalgr suppression actual NPC source only");
            manager.SetMechanismEnabled(id, "拉尔戈神使", false); Seek(1.2);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "Rhalgr mechanism releases owner");
            manager.SetMechanismEnabled(id, "拉尔戈神使", true); Seek(1.1);
            Check(Views().Length == 1 && dangers() == 1, "Rhalgr reverse Seek restores current cast");
            Seek(3.1); Check(Views().Length == 0 && dangers() == 0, "Rhalgr real Finish clears ordinary AOE");
            Release();
            Console.WriteLine($"PASS native Alliance trash actual Rhalgr standard {action} cold Finish solver switch Seek owner");
        }
        foreach (var state in new uint[] { 0x00080004, 0x00100004 })
        {
            load(id, [Header(1054), World(0, [Boss(), Player(), Npc()], true), Map(.5, state), Map(.6, state),
                World(1, []), World(20, []), Effect(20.1, 28890), Effect(21, 28891), Effect(22, 28891), World(23, [])]);
            Seek(1.1); Check(Views().Length == 4 && dangers() == 4 && (int)Read(Model(), "RhalgrChargeAoes")! == 4,
                "Rhalgr Map repeated double cones append and reach actual navigation");
            Solver(center + Vector3.UnitX * 12);
            Seek(20.2); Check(Views().Length == 4 && dangers() == 4, "Rhalgr predicted clock and visual Effect do not consume");
            Seek(21.1); Check(Views().Length == 0 && dangers() == 0 && (int)Read(Model(), "RhalgrChargeCount")! == 1,
                "Rhalgr real Native Effect clears all cones");
            Seek(22.1); Check((int)Read(Model(), "RhalgrChargeCount")! == 2, "Rhalgr empty Effect counts independently");
            Seek(1.1); Check(Views().Length == 4, "Rhalgr reverse Seek rebuilds actual Map history");
            Release();
            Console.WriteLine($"PASS native Alliance trash actual Rhalgr Map {state:X} repeat no TTL real Effect solver reverse Seek");
        }
        load(id, [Header(1054), World(0, [Boss(), Player(), Npc(28893)], true),
            World(1, [Npc()]), World(2, [Npc(28894, actor: 21, at: 2)]), World(3, [Npc(actor: 21)]),
            World(4, [Npc(28895, actor: 22, at: 4)]), World(5, [Npc(actor: 22)]), World(6, [])]);
        Seek(.3); Check(Views().Length == 1 && dangers() == 1, "Rhalgr Boltloop circle published");
        Seek(1.1); Check(Views().Length == 1 && dangers() == 1 && (int)Read(Model(), "NumEarthCasts")! == 1, "Rhalgr first cross source sequence stage");
        Seek(3.1); Check(Views().Length == 1 && dangers() == 1 && (int)Read(Model(), "NumEarthCasts")! == 2, "Rhalgr second cross source sequence stage");
        Seek(5.1); Check(Views().Length == 0 && dangers() == 0 && (int)Read(Model(), "NumEarthCasts")! == 3, "Rhalgr final real stage clears");
        Seek(.3); Check(Views().Length == 1, "Rhalgr Boltloop reverse Seek");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Rhalgr Boltloop three shapes independent Finish sources cache and Seek");

        load(id, [Header(1054), World(0, [Boss(), Player(), Player(101), Npc(28889)], true),
            World(1, [Npc()]), World(2, [], removed: [100]), World(3, [Player()]),
            Effect(4, 28889), Effect(5, 28889), World(6, [])]);
        Seek(.3); Check(Views().Length == 1 && (bool)Read(Views()[0], "OutlineOnly")!
            && dangers() == 1 && Items(Script(), "PredictedDamage").Length == 1,
            "Rhalgr target own outline and navigation plus tankbuster diagnostic");
        Seek(1.1); Check(Views().Length == 1 && dangers() == 1 && (int)Read(Model(), "RhalgrStrikeCount")! == 0,
            "Rhalgr Strike Finish retains original bait");
        Seek(3.1); Check(Items(Model(), "DahuBaits").Length == 1, "Rhalgr removed target remains original reference");
        Seek(4.1); Check(Views().Length == 0 && dangers() == 0 && (int)Read(Model(), "RhalgrStrikeCount")! == 1,
            "Rhalgr real Strike Effect consumes bait");
        Seek(5.1); Check((int)Read(Model(), "RhalgrStrikeCount")! == 2, "Rhalgr empty Strike source counts");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Rhalgr Strike outline raw target references Finish retained native Effect");

        load(id, [Header(1054), World(0, [Boss(), Player(), Player(101), Npc(28889, target: 101)], true), World(1, [])]);
        Seek(.3); Check(Views().Length == 1 && !(bool)Read(Views()[0], "OutlineOnly")!
            && dangers() == 1 && Items(Script(), "PredictedDamage").Length == 1,
            "Rhalgr other target filled bait and own avoid");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Rhalgr Strike other target fill and navigation");

        load(id, [Header(1054), World(0, [Boss(false), Player(), Npc()], true), Map(.2, 0x00080004), Effect(.3, 28900),
            World(1, [Boss()]), Effect(2, 28900), World(3, []), World(4, [Boss(hp0: true)]), World(5, [])]);
        Seek(1.1); Check(Views().Length == 0 && dangers() == 0 && (int)Read(Model(), "RhalgrChargeAoes")! == 0
            && (int)Read(Model(), "RhalgrBlueCount")! == 0, "Rhalgr cold activation does not replay historical Map or Effect");
        Seek(2.1); Check((int)Read(Model(), "RhalgrBlueCount")! == 1 && Items(Script(), "PredictedDamage").Length == 0
            && Views().Length == 0 && dangers() == 0, "Rhalgr Blue counter has no fabricated Raidwide output");
        Seek(4.1); Check(!(bool)Read(Model(), "Active")! && Read(Model(), "RhalgrTimeline") == null && Read(Snapshot(), "ArenaBoundary") == null,
            "Rhalgr default HP0 completion clears owner");
        Release();
        Console.WriteLine("PASS native Alliance trash actual Rhalgr cold missing Map counter only Blue HP0 complete");

        var lionDef = catalog.GetField("Lions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var lionCenter = (Vector3)Read(lionDef, "Center")!;
        manager.SetScriptEnabled(lions, true);
        load(id, [Header(1054), World(0, [Boss(), Actor(2, 0x38DA, lionCenter, combat: true, subKind: 5),
            Actor(100, 0, lionCenter + Vector3.UnitX * 10, subKind: 4), Npc(28898),
            Actor(21, 0x233C, lionCenter, cast: Cast(29136, 5, lionCenter, startedAt: 0), subKind: 5)], true),
            World(.1, [Player()]), World(1, [])]);
        Seek(.3); Check(dangers() == 2 && manager.Running.ContainsKey(lions), "Rhalgr Lions same territory independent active models");
        manager.SetScriptEnabled(id, false); controller.Seek(.4); Check(dangers() == 1 && manager.Running.ContainsKey(lions), "Rhalgr release preserves Lions");
        manager.SetScriptEnabled(id, true); Seek(.3); Check(dangers() == 2, "Rhalgr two owners reverse Seek");
        manager.SetScriptEnabled(lions, false); Seek(.4); Check(dangers() == 1, "Lions release preserves Rhalgr");
        Release(); manager.SetScriptEnabled(lions, true);
        Console.WriteLine("PASS native Alliance trash actual Rhalgr Lions same territory two owner bidirectional release reverse Seek");
    }
}

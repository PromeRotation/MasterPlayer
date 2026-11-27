using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBozja(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.bozja.south";
        const string mechanism = "高火力陆战魔导兵器——守夜";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var jsonOptions = (JsonSerializerOptions)assembly.GetType("MasterPlayer.Replay.ReplayJson")!
            .GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var center = new Vector3(451, 0, 830);
        object[] roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        object Read(object obj, string name)
        {
            for (var type = obj.GetType(); type != null; type = type.BaseType)
            {
                if (type.GetProperty(name, flags) is { } p) return p.GetValue(obj)!;
                if (type.GetField(name, flags) is { } f) return f.GetValue(obj)!;
            }
            throw new MissingMemberException(obj.GetType().Name, name);
        }
        object Script() => Read(manager.Running[id], "Script");
        object Model() => Read(Script(), "encounter");
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(920, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, Vector3 location, double at = 0, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, location, .4f, target, at))!;
            node["IsSpell"] = true;
            node["TotalTime"] = 4;
            return node;
        }
        object[] Initial(object? cast = null, Vector3? helper = null) =>
            [Actor(1, 0x2DBD, center, combat: true, isEnemy: true),
                Actor(100, 0, center), Actor(101, 0, center + new Vector3(-20, 0, 0)),
                Actor(20, 0x233C, helper ?? center, cast: cast, subKind: 5)];
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0, $"Bozja callback error: {controller.StatusMessage}");
        }
        var host = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var bridge = RuntimeHelpers.GetUninitializedObject(host);
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.NonPublic | BindingFlags.Static)!;
        Check((bool)owns.Invoke(null, [920u, 0x2DBDu])! && !(bool)owns.Invoke(null, [921u, 0x2DBDu])!
            && (bool)owns.Invoke(null, [920u, 0x2DBBu])!
            && (bool)owns.Invoke(null, [920u, 0x2E2Fu])!
            && !(bool)owns.Invoke(null, [921u, 0x2E2Fu])!, "Bozja exact native ownership territory.");

        // Observe real registry enumeration after late insertion and after an
        // in-place same-key sort; calling previews in the right order is insufficient.
        var pluginInstance = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var registry = Read(pluginInstance, "AoeVisibility");
        registry.GetType().GetMethod("AddPreview")!.Invoke(registry, ["external-before-bozja",
            Activator.CreateInstance(assembly.GetType("MasterPlayer.Spatial.Geometry.CircleGeometry")!, [center, 1f]),
            false, 0f, "external-order-test", "external-order-test", false, 1f, false, null, null]);
        load(id, [Header(920), World(0, Initial(Spell(21268, center)), true), World(10, [])]);
        Seek(.1);
        registry.GetType().GetMethod("RemovePreview")!.Invoke(registry, ["external-before-bozja"]);
        var world = Read(Script(), "ReplayWorld");
        void Apply(double t, IEnumerable<object> actors, bool update = true)
        {
            var json = JsonNode.Parse(World(t, actors))!["data"]!.ToJsonString();
            world.GetType().GetMethod("Apply", flags)!.Invoke(world,
                [JsonSerializer.Deserialize(json, assembly.GetType("MasterPlayer.Replay.ReplayWorldFrame")!, jsonOptions)!]);
            world.GetType().GetProperty("CurrentTime")!.SetValue(world, Start.AddSeconds(t));
            Script().GetType().BaseType!.GetMethod("Scan", flags)!.Invoke(Script(), [false]);
            if (update) Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.02f]);
            else Script().GetType().BaseType!.GetMethod("Render", flags)!.Invoke(Script(), null);
        }
        Apply(.15, [Actor(21, 0x233C, center, cast: Spell(20146, center))], false);
        Check(Items("Casts").Length == 1 && (bool)manager.IsGenericDodgeSuppressed(
            new LogSystemCastStartEvent { SourceId = 21, ActionId = 20146 }),
            "New helper source gate uses sample without committing its cast before Framework");
        Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.02f]);
        var sampled = ((IDictionary)Read(world, "Actors"))[21UL]!;
        Check((Vector3)Read(sampled, "Position") == center && (Vector3)Read(Read(sampled, "Cast"), "Location") == center,
            "Bozja manual frame uses real JSON vector conversion");
        var ordered = Shown("bozja/aoe/").Select(p => Read(p, "Geometry").GetType().Name).ToArray();
        Check(ordered.SequenceEqual(new[] { "CircleGeometry", "RayRectangleGeometry" }),
            "Late first-component preview lost source order: " + string.Join(",", ordered));
        stop();
        Console.WriteLine("PASS native Bozja CE14 real registry external earlier vacated slot late component order");

        load(id, [Header(920), World(0, Initial(), true), World(10, [])]);
        Seek(.1);
        world = Read(Script(), "ReplayWorld");
        for (var i = 0; i < 100; ++i)
        {
            Apply(.11 + i * .001, [Actor((ulong)(1000 + i), 0x233C, center + new Vector3(i % 4, 0, i % 5),
                cast: Spell(i % 2 == 0 ? 20146u : 20152u, center + new Vector3(i % 4, 0, i % 5)))]);
            if (i is 97 or 98 or 99)
            {
                var expected = Items("Casts").Select(c => $"bozja/aoe/{Read(c, "Generation")}").ToArray();
                var actual = Shown("bozja/aoe/").Select(p => (string)Read(p, "Id")).ToArray();
                Check(expected.Length == actual.Length && expected.Select((k, n) => actual[n].EndsWith(k)).All(b => b),
                    $"Actual registry same-key group reorder at {i + 1}.");
            }
        }
        stop();
        Console.WriteLine("PASS native Bozja CE14 real registry 98 99 100 equal-clock same-key reorder");

        // Change Version on every event to force full sampling, then call the
        // normal update with that same Version. This reproduces deadline
        // starvation without the replay unchanged-Version fast return.
        load(id, [Header(920), World(0, [Actor(1, 0x2DBD, center), Actor(100, 0, center)], true), World(10, [])]);
        Seek(.01);
        world = Read(Script(), "ReplayWorld");
        var deadline = (DateTime)Read(Script(), "nextScan");
        for (var i = 1; i <= 8; ++i)
        {
            var t = (deadline - Start).TotalSeconds - .09 + i * .02;
            Apply(t, [Actor(1, 0x2DBD, center, combat: true)], false);
            Check((DateTime)Read(Script(), "nextScan") == deadline || (bool)Read(Script(), "EncounterActive"),
                "Event sampling postponed Framework deadline.");
            Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.02f]);
        }
        Check((bool)Read(Script(), "EncounterActive"), "Busy event input starved pull lifecycle.");
        stop();
        Console.WriteLine("PASS native Bozja full event sampling preserves Framework deadline under changing worlds");

        foreach (var (action, radius) in new (uint, float)[] { (20146, 10), (20148, 12), (20150, 30),
            (20151, 16), (20152, 10), (21268, 0) })
        {
            load(id, [Header(920), World(0, Initial(Spell(action, center)), true),
                CancelHoH(.2, 20, action), World(20, []), World(21, [Actor(20, 0x233C, center)]), World(22, [])]);
            Seek(.1);
            Check((bool)Read(Script(), "EncounterActive") && dangers() == 1 && Shown("bozja/aoe/").Length == 1,
                $"Bozja {action} cold output.");
            var geometry = Read(Shown("bozja/aoe/").Single(), "Geometry");
            Check(radius == 0 ? (float)Read(geometry, "Width") == 4 && (float)Read(geometry, "Length") == 50
                : (float)Read(geometry, "Radius") == radius, $"Bozja {action} geometry.");
            Check(!((bool)host.GetMethod("IsReplaySuppressed", flags)!.Invoke(bridge, null)!),
                "Bozja suppressed every territory module.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 999, ActionId = (ushort)action }),
                "Bozja exact source generic suppression.");
            Seek(20.1); Check(dangers() == 1 && Items("Casts").Length == 1, "Bozja packet cancel/timeout consumed raw queue.");
            Seek(21.1); Check(dangers() == 0 && Items("Casts").Length == 0, "Bozja raw Finish did not remove queue.");
            Seek(.1); Check(dangers() == 1, "Bozja reverse Seek failed.");
            manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(dangers() == 0 && previews().Length == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Bozja mechanism cleanup.");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1);
            stop();
            Console.WriteLine($"PASS native Bozja CE14 {action} real DLL cold raw geometry source gate no territory suppression cancel TTL actual Finish Seek mechanism owner");
        }
        var towerPoint = center + new Vector3(12, 0, 0);
        load(id, [Header(920), World(0, [.. Initial(Spell(21266, towerPoint), towerPoint),
            Actor(102, 0, center + new Vector3(25, 0, 0))], true),
            World(.2, [Actor(100, 0, towerPoint)]),
            World(.4, [Actor(102, 0, towerPoint)]), World(1, [])]);
        Seek(.1);
        Check(Items("Towers").Length == 1 && dangers() == 0 && Shown("/outline").Length == 1
            && Shown("/background").Length == 0, "Empty tower source sampled-complement rejection or safe outline.");
        Seek(.25);
        var damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().ToArray();
        Check(damage.Length == 1 && (ulong)Read(damage[0], "Mask") == 1
            && Shown("/background").Length == 0, "Own tower mask/display.");
        Seek(.45);
        Check(((string)Read(Script(), "MechanicHintsText")).Contains("Too many")
            && Shown("/outline").Length == 1 && Shown("/background").Length == 0 && dangers() == 1,
            "NonParty initial OID0 did not overfill tower or exact normal hazard.");
        stop();
        Console.WriteLine("PASS native Bozja CE14 all-world tower live positions nonParty overfill split background foreground masks source conversion");

        load(id, [Header(920), World(0, Initial(Spell(20146, center)), true), World(1, [])]);
        Seek(.1);
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback"),
            $"Bozja real solver failed: {result.DescribeOutcome()}");
        var end = (Vector3)Read((object)result, "TargetPosition");
        Check(Vector3.Distance(end, center) >= 10 && Vector3.Distance(end, center) <= 29.5f,
            "Bozja solver did not leave actual R10 inside fixed arena.");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions")
            && !(bool)Read(Read(Script(), "Hints"), "blockCasts"), "Bozja replay emitted execution hints.");
        stop();
        Console.WriteLine("PASS native Bozja CE14 actual SafeZoneSolver R10 escape no fallback fixed polygon owner replay execution isolation");

        foreach (var action in new uint[] { 20153, 20154 })
        {
            load(id, [Header(920), World(0, Initial(Spell(action, center)), true), World(10, []),
                World(11, [Actor(20, 0x233C, center)]), World(12, [])]);
            Seek(.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0
                && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1, "Bozja unavoidable invented geometry/missing prediction.");
            Seek(11.1);
            Check(!((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "Bozja damage Finish leak.");
            stop();
            Console.WriteLine($"PASS native Bozja CE14 damage {action} raw current cast prediction diagnostics no invented AOE actual Finish owner");
        }

        center = new(644, 0, 228);
        const string furlong = "怨念死灵——地生人";
        object[] Furlong(object? cast = null) =>
            [Actor(1, 0x2DBB, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + new Vector3(-15, 0, 0)), Actor(20, 0x233C, center, cast: cast)];
        int Goals() => ((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "GoalAreas")).Cast<object>().Count();
        foreach (var (action, count, safe) in new[] { (20132u, 0, true), (20126u, 1, false), (21019u, 1, false) })
        {
            load(id, [Header(920), World(0, Furlong(Spell(action, center)), true),
                World(1, [Actor(20, 0x233C, center)]), World(2, [])]);
            Seek(.1);
            var preview = Shown("bozja/aoe/").Single();
            Check((float)Read(Read(preview, "Geometry"), "Radius") == (safe ? 12 : 6)
                && (bool)Read(preview, "Safe") == safe && dangers() == count && Goals() == 0,
                $"CE21 {action} safe/risk/geometry.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }),
                "CE21 exact generic dodge ownership.");
            Seek(1.1); Check(Shown("bozja/aoe/").Length == 0 && dangers() == 0, "CE21 raw Finish leak.");
            Seek(.1);
            manager.SetMechanismEnabled(id, furlong, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0 && Goals() == 0, "CE21 disabled mechanism output.");
            manager.SetMechanismEnabled(id, furlong, true); Seek(.1);
            stop();
            Console.WriteLine($"PASS native Bozja CE21 {action} safe display geometry raw Finish source gate mechanism Seek owner");
        }

        object Hand(ulong n, Vector3 p, uint tether, bool dead = false) => Actor(n, 0x2DBC, p,
            dead: dead, targetable: !dead, tethers: [new { Id = tether, TargetId = 100ul }]);
        object[] Lines()
        {
            var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
            var visibility = Read(plugin, "AoeVisibility");
            return ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        }
        load(id, [Header(920), World(0, [.. Furlong(), Hand(30, center, 1), Hand(31, center + new Vector3(20, 0, 0), 2)], true),
            World(.2, [Actor(20, 0x233C, center, cast: Spell(20132, center))]),
            World(.4, [Hand(30, center, 1, true)]),
            World(.6, [], removed: [30]), World(.8, [Hand(30, center, 1)]),
            World(1, [Actor(20, 0x233C, center)]), World(2, [])]);
        Seek(.1);
        Check(Lines().Length == 1 && !(bool)Read(Lines()[0], "Safe") && Read(Script(), "MechanicHintsText") == null,
            "CE21 hand line without caster.");
        Seek(.3); Check(((string)Read(Script(), "MechanicHintsText")).Contains("Face the hand") && dangers() == 0 && Goals() == 0, "CE21 first hand hint.");
        Seek(.5); Check(!(bool)Read(Lines().Single(), "Safe"), "Dead hand was removed.");
        Seek(.9); Check((bool)Read(Lines().Single(), "Safe") && ((string)Read(Script(), "MechanicHintsText")).Contains("kite into safezone"),
            "Rebuilt hand changed persistent list order.");
        Seek(1.1); Check(Lines().Length == 1 && Read(Script(), "MechanicHintsText") == null, "CE21 Finish removed hand line.");
        stop(); Check(Lines().Length == 0, "CE21 line owner leaked.");
        Console.WriteLine("PASS native Bozja CE21 real line no caster dead hand destruction generation first hand hint no auto facing");

        load(id, [Header(920), World(0, [.. Furlong(Spell(20129, center + new Vector3(10, 0, 0))),
            Actor(21, 0x233C, center, cast: Spell(20129, center)),
            Actor(22, 0x233C, center, cast: Spell(20129, center + new Vector3(-10, 0, 0)))], true),
            World(1, [Actor(20, 0x233C, center)]), World(2, [])]);
        Seek(.1);
        Check(Shown("bozja/aoe/").Length == 3 && dangers() == 2 && Goals() == 1, "CE21 Coffin tail visibility/risk/R4 goal.");
        var goal = ((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "GoalAreas")).Cast<object>().Single();
        Check((Vector3)Read(Read(goal, "Geometry"), "Position") == center + new Vector3(10, 0, 0)
            && (float)Read(Read(goal, "Geometry"), "Radius") == 4, "Coffin goal must use first cast origin.");
        Seek(1.1); Check(dangers() == 2 && Goals() == 0 && Shown("bozja/aoe/").Length == 2, "CE21 Coffin hidden promotion.");
        stop();
        Console.WriteLine("PASS native Bozja CE21 Coffin three previews two risky first-origin R4 real goal and Finish promotion");

        load(id, [Header(920), World(0, Furlong(Spell(20125, center, target: 101)), true),
            World(.2, [Actor(101, 0, center + new Vector3(5, 0, 0), dead: true)]),
            World(.4, [Actor(101, 0, center + new Vector3(5, 0, 0))]),
            World(.6, [], removed: [101]), World(.8, [Actor(101, 0, center + new Vector3(20, 0, 0))]),
            World(1, [Actor(20, 0x233C, center)]), World(2, [])]);
        Seek(.1);
        Check(Shown("bozja/stack/").Length == 1 && Goals() == 1
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE21 stack goal or invented damage.");
        snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
            && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center + new Vector3(-15, 0, 0)) <= 6,
            "CE21 actual solver did not join R6 stack goal.");
        Seek(.3); Check(Shown("bozja/stack/").Length == 0 && Goals() == 0 && Items("Casts").Length == 1, "Dead stack removed underlying cast.");
        Seek(.5); Check(Shown("bozja/stack/").Length == 1, "Revived original stack not restored.");
        Seek(.9);
        var oldStackTarget = Read(Items("Casts").Single(), "Target");
        var oldTargetPosition = (Vector3)Read(Read(oldStackTarget, "Actor"), "Position");
        var quantizedOld = (Vector3)assembly.GetType("MasterPlayer.Mechanics.Dungeons.DungeonEncounter")!
            .GetMethod("QuantizedDungeonPosition", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [oldTargetPosition])!;
        Check((bool)Read(oldStackTarget, "Destroyed") && oldTargetPosition == center + new Vector3(5, 0, 0)
            && (Vector3)Read(Read(Shown("bozja/stack/").Single(), "Geometry"), "Position") == quantizedOld,
            "Same-ID target rebound destroyed original stack.");
        Seek(1.1); Check(Shown("bozja/stack/").Length == 0 && Goals() == 0, "CE21 stack Finish by target ID leak.");
        stop();
        Console.WriteLine("PASS native Bozja CE21 original stack death revival generation target Finish actual SafeZoneSolver R6 goal no fallback");

        foreach (var action in new uint[] { 20130, 20131 })
        {
            load(id, [Header(920), World(0, Furlong(Spell(action, center)), true),
                World(1, [Actor(20, 0x233C, center)]), World(2, [])]);
            Seek(.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0
                && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1, "CE21 damage geometry or missing prediction.");
            Seek(1.1); Check(!((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE21 damage Finish.");
            stop();
            Console.WriteLine($"PASS native Bozja CE21 damage {action} diagnostics only raw Finish owner");
        }

        center = new(154, 0, 785);
        const string hounds = "战争妖犬——恐惧妖犬";
        object[] Hounds(object? cast = null, Vector3? location = null) =>
            [Actor(1, 0x2E66, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + new Vector3(-15, 0, 0)), Actor(20, 0x233C, location ?? center, cast: cast)];
        Check((bool)owns.Invoke(null, [920u, 0x2E66u])! && !(bool)owns.Invoke(null, [921u, 0x2E66u])!,
            "CE12 exact ownership.");
        foreach (var action in new uint[] { 20535, 20538, 20553, 20544, 20545, 20542 })
        {
            load(id, [Header(920), World(0, Hounds(Spell(action, center)), true),
                World(6, [Actor(20, 0x233C, center)]), World(7, [])]);
            Seek(.1);
            var geometry = Read(Shown("bozja/aoe/").Single(), "Geometry");
            if (action is 20535 or 20544 or 20545 or 20542)
                Check((float)Read(geometry, "Radius") == (action == 20535 ? 19 : 60)
                    && (float)Read(geometry, "AngleRadians") == 2 * ((action == 20535 ? 45 : 30) * MathF.PI / 180),
                    "CE12 source cone half angle/radius.");
            if (action == 20538) Check((float)Read(geometry, "Radius") == 5, "CE12 R5.");
            if (action == 20553) Check((float)Read(geometry, "Length") == 50
                && (float)Read(geometry, "MaxLocalX") - (float)Read(geometry, "MinLocalX") == 10, "CE12 ray50 width10.");
            Check(dangers() == 1 && Goals() == 0 && (bool)manager.IsGenericDodgeSuppressed(
                new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action }), "CE12 navigation/suppression.");
            Seek(5); Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1, "CE12 prediction became TTL.");
            Seek(6.1); Check(Shown("bozja/aoe/").Length == 0 && dangers() == 0, "CE12 Finish leak.");
            Seek(.1); manager.SetMechanismEnabled(id, hounds, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0, "CE12 disabled output.");
            manager.SetMechanismEnabled(id, hounds, true); Seek(.1);
            stop();
            Console.WriteLine($"PASS native Hounds encounter ordinary {action} exact source geometry real navigation raw Finish overdue switch Seek owner");
        }

        var landing = center + new Vector3(10, 0, 0);
        var opposite = center + new Vector3(-10, 0, 0);
        load(id, [Header(920), World(0, Hounds(Spell(20539, landing)), true),
            World(1, [Actor(20, 0x233C, landing)]),
            World(2, [Actor(20, 0x233C, landing, cast: Spell(20540, opposite, 2))]),
            World(3, [Actor(20, 0x233C, opposite)]), World(5, [])]);
        Seek(.1);
        Check((float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Length") == 10
            && dangers() == 1, "CE12 first charge.");
        Seek(1.1);
        Check((float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Length") == 20
            && (DateTime)Read(Items("Casts").Single(), "Activation") == Start.AddSeconds(1).AddSeconds(3.7d),
            "CE12 finish return mirror length/callback clock.");
        Seek(2.1); Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1, "CE12 second Start must overwrite.");
        Seek(3.1); Check(Shown("bozja/aoe/").Length == 0 && dangers() == 0, "CE12 second Finish clear.");
        Seek(1.1); Check(Shown("bozja/aoe/").Length == 1, "CE12 return reverse Seek.");
        stop();
        Console.WriteLine("PASS native Hounds encounter two charge mirror return callback3point7 clock second overwrite Finish reverse Seek");

        load(id, [Header(920), World(0, Hounds(Spell(20539, center)), true), World(2, [])]);
        Seek(.1);
        var zero = Items("Casts").Single();
        Check((float)Read(Read(zero, "Shape"), "Length") == 0 && !(bool)Read(zero, "Drawable")
            && Shown("bozja/aoe/").Length == 0, "CE12 zero charge model/display boundary.");
        var runtimeAssembly = Assembly.Load("MasterPlayer.MechanicRuntime");
        var zeroOrigin = (Vector3)Read(zero, "Origin");
        var sourceOrigin = Activator.CreateInstance(runtimeAssembly.GetType("BossMod.WPos")!, [zeroOrigin.X, zeroOrigin.Z])!;
        var sourceDirection = Activator.CreateInstance(runtimeAssembly.GetType("BossMod.Angle")!, [0f])!;
        var sourceRect = Activator.CreateInstance(runtimeAssembly.GetType("BossMod.SDRect")!,
            [sourceOrigin, sourceDirection, 0f, 0f, 5f])!;
        var converter = assembly.GetType("MasterPlayer.MechanicHost.MechanicHintsConvert")!
            .GetMethod("ConvertForbiddenZone", BindingFlags.Public | BindingFlags.Static)!;
        object?[] conversionArgs = [sourceRect, 0f, center, 24.5f, null];
        var sourceZones = ((IEnumerable)converter.Invoke(null, conversionArgs)!).Cast<object>().ToArray();
        Check(dangers() == sourceZones.Length, "CE12 zero charge actual source sampled navigation count.");
        stop();
        Console.WriteLine("PASS native Hounds encounter zero length charge source model preview refusal actual source navigation conversion count");

        load(id, [Header(920), World(0, [.. Hounds(Spell(20551, center)),
            Actor(21, 0x233C, center, cast: Spell(20550, center)),
            Actor(22, 0x233C, center, cast: Spell(20549, center))], true),
            World(1, [Actor(22, 0x233C, center)]),
            World(2, [Actor(20, 0x233C, center)]),
            World(3, [Actor(21, 0x233C, center)]), World(4, [])]);
        Seek(.1);
        Check(Items("Casts").Length == 3 && Shown("bozja/aoe/").Length == 1 && dangers() == 1
            && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "InnerRadius") == 20, "CE12 outer head.");
        Seek(1.1);
        Check(Items("Casts").Length == 2 && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "InnerRadius") == 10,
            "CE12 unrelated helper must pop global outer head.");
        Seek(2.1);
        Check(Items("Casts").Length == 1 && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Radius") == 10,
            "CE12 hidden circle promotion.");
        snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        var houndsArena = Read(snapshot, "ArenaBoundary");
        Check(!(bool)houndsArena.GetType().GetMethod("Contains")!.Invoke(houndsArena, [center + new Vector3(25, 0, 0), 0f])!,
            "CE12 actual boundary retained R29.5.");
        player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
            && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) > 10,
            "CE12 actual solver R10 within small arena.");
        Seek(3.1); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE12 quake Finish leak.");
        stop();
        Console.WriteLine("PASS native Hounds encounter three reversed quake FIFO hidden navigation cross helper Finish promotion");

        foreach (var action in new uint[] { 20534, 20546 })
        {
            load(id, [Header(920), World(0, Hounds(Spell(action, center)), true),
                World(1, [Actor(20, 0x233C, center)]), World(2, [])]);
            Seek(.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0 && Goals() == 0, "CE12 diagnostic invented geometry.");
            Check(((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == (action == 20534 ? 1 : 0),
                "CE12 hint fabricated damage or buster missing.");
            if (action == 20546) Check(((string)Read(Script(), "MechanicHintsText")).Contains("Set hp to 1"), "CE12 global hint.");
            Seek(1.1);
            Check(!((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any()
                && Read(Script(), "MechanicHintsText") == null, "CE12 watched Finish diagnostic leak.");
            stop();
            Console.WriteLine($"PASS native Hounds encounter {action} original caster diagnostic true watched Finish no invented geometry");
        }

        const string fire = "腐烂蔬菜——皮里福尔";
        center = new(-90, 0, 700);
        object[] Fire(object? cast = null) =>
            [Actor(1, 0x2E2F, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + new Vector3(-15, 0, 0)), Actor(20, 0x233C, center, cast: cast)];
        foreach (var action in new uint[] { 20520, 20522, 20525, 20527 })
        {
            load(id, [Header(920), World(0, Fire(Spell(action, center)), true),
                World(6, [Actor(20, 0x233C, center)]), World(7, [])]);
            Seek(.1);
            if (action is 20520 or 20522)
            {
                Check(dangers() == 1 && Shown("bozja/aoe/").Length == 1, "CE13 ordinary real nav.");
                var geometry = Read(Shown("bozja/aoe/").Single(), "Geometry");
                if (action == 20520) Check((float)Read(geometry, "Length") == 30
                    && (float)Read(geometry, "MaxLocalX") - (float)Read(geometry, "MinLocalX") == 2, "CE13 ray dimensions.");
                else Check((float)Read(geometry, "Radius") == 6, "CE13 puddle.");
                Seek(5); Check(dangers() == 1 && Shown("bozja/aoe/").Length == 1, "CE13 not TTL.");
            }
            else Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0
                && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1,
                "CE13 raidwide/buster diagnostic.");
            Seek(6.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0
                && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE13 actual Finish.");
            stop();
            Console.WriteLine($"PASS native Bozja CE13 {action} real DLL exact geometry diagnostic raw Finish no TTL owner");
        }

        object Colored(ulong actor, uint status) => Actor(actor, 0, center, stackedStatus: status);
        load(id, [Header(920), World(0, [Actor(1, 0x2E2F, center, combat: true, isEnemy: true),
            Actor(20, 0x233C, center), Colored(100, 2301), Colored(101, 2302),
            Actor(21, 0x233C, center + new Vector3(5, 0, 0), cast: Spell(20518, center)),
            Actor(22, 0x233C, center, cast: Spell(20519, center + new Vector3(5, 0, 0)))], true),
            World(1, [Colored(100, 2302)]), World(2, [Actor(21, 0x233C, center)]), World(3, [])]);
        Seek(.1);
        Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1
            && (Vector3)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Position") == center, "CE13 tender selected.");
        Seek(1.1);
        Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1
            && (Vector3)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Position") == center + new Vector3(5, 0, 0),
            "CE13 jealous selected.");
        Seek(2.1); Check(Shown("bozja/aoe/").Length == 0 && dangers() == 0, "CE13 cross-color Finish clear both.");
        Seek(.1);
        manager.SetMechanismEnabled(id, fire, false);
        Seek(.1);
        Check(previews().Length == 0 && dangers() == 0, "CE13 disabled owner leak.");
        manager.SetMechanismEnabled(id, fire, true); Seek(.1);
        Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1, "CE13 reenable Seek restore.");
        stop();
        Console.WriteLine("PASS native Bozja CE13 DeadLeaves true current viewer state change cross color Finish switch reverse Seek owner");

        load(id, [Header(920), World(0, [.. Fire(), Actor(30, 0x2E31, center + new Vector3(10.123f, 0, .257f), rotation: .731f)], true),
            World(1, [Actor(30, 0x2E31, center + new Vector3(12.123f, 0, 2.257f), rotation: 1.1f,
                dead: true, targetable: false, eventState: 7)]),
            World(2, [], removed: [30]), World(3, [])]);
        Seek(.1);
        snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        Check(dangers() == 4 && ((IEnumerable)Read(snapshot, "HardObstacles")).Cast<object>().Count() == 1
            && Shown("bozja/aoe/").Length == 1, "CE13 moving capsule real 4 dangerous +1 obstacle +1 preview.");
        object[] NativeZones()
        {
            var model = Model();
            var actors = Read(model, "Actors");
            var viewer = actors.GetType().GetProperty("Item")!.GetValue(actors, [100UL])!;
            return ((IEnumerable)model.GetType().GetMethod("Navigation", flags)!.Invoke(model, [viewer])!).Cast<object>().ToArray();
        }
        var zones = NativeZones().Where(z => !(bool)Read(z, "Obstacle")).ToArray();
        DateTime[] expectedClocks = [((DateTime)runtime.CurrentTime).AddSeconds(1.1d),
            ((DateTime)runtime.CurrentTime).AddSeconds(3d), ((DateTime)runtime.CurrentTime).AddSeconds(10d), DateTime.MaxValue];
        Check(zones.Select(z => (DateTime)Read(z, "Activation")).SequenceEqual(expectedClocks), "CE13 relative forecast clocks.");
        Check(zones.Select(z => (float)Read(Read(z, "Shape"), "Length")).SequenceEqual(new[] { 1.5f, 3, 6, 9 }), "CE13 forecast lengths.");
        var firstOrigin = (Vector3)Read(zones[0], "Origin");
        Seek(1.1);
        Check(dangers() == 4 && NativeZones().Count(z => (bool)Read(z, "Obstacle")) == 1
            && (Vector3)Read(NativeZones()[0], "Origin") != firstOrigin && Shown("bozja/aoe/").Length == 1,
            "CE13 dead/state7 moving source must remain.");
        Seek(2.1);
        Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0
            && !((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "HardObstacles")).Cast<object>().Any(),
            "CE13 true destroyed cleanup.");
        Seek(.1); Check(dangers() == 4, "CE13 inverse Seek restore capsule.");
        stop();
        Check(dangers() == 0 && previews().Length == 0
            && !((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "HardObstacles")).Cast<object>().Any(),
            "CE13 capsule owner stop.");
        Console.WriteLine("PASS native Bozja CE13 moving capsule four real forecasts separate obstacle dead state7 destruction inverse Seek owner");

        load(id, [Header(920), World(0, Fire(Spell(20524, center)), true),
            World(1, [Actor(101, 0, center)]), World(2, [Actor(20, 0x233C, center)]), World(3, [])]);
        Seek(.1);
        Check(Shown("bozja/stack/").Length == 1 && ((string)Read(Script(), "MechanicHintsText")).Contains("Stack!")
            && !((string)Read(Script(), "MechanicHintsText")).Contains("已满足")
            && NativeZones().Any(), "CE13 target needs eight and two members retain buddy navigation.");
        Seek(1.1);
        Check(Shown("bozja/stack/").Length == 1 && ((string)Read(Script(), "MechanicHintsText")).Contains("Stack!")
            && !((string)Read(Script(), "MechanicHintsText")).Contains("已满足"), "CE13 two members cannot satisfy min8.");
        Seek(2.1); Check(Shown("bozja/stack/").Length == 0 && Goals() == 0 && dangers() == 0, "CE13 stack Finish.");
        stop();
        Console.WriteLine("PASS native Bozja CE13 min8 target stack true two member buddy navigation Finish owner");

        center = new(825, 0, 640);
        object[] DeathHand(object? cast = null) =>
            [Actor(1, 0x2DA7, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + new Vector3(-20, 0, 0)), Actor(20, 0x233C, center, cast: cast, subKind: 5)];
        Check((bool)owns.Invoke(null, [920u, 0x2DA7u])! && !(bool)owns.Invoke(null, [921u, 0x2DA7u])!,
            "CE11 exact ownership.");
        foreach (var action in new uint[] { 20172, 20191, 20174, 20188, 20163, 20166, 20171, 20175 })
        {
            load(id, [Header(920), World(0, DeathHand(Spell(action, center)), true),
                World(10, []), World(11, [Actor(20, 0x233C, center)]), World(12, [])]);
            Seek(.1);
            var geometric = action is 20172 or 20191 or 20174 or 20188;
            Check(dangers() == (geometric ? 1 : 0) && Shown("bozja/aoe/").Length == (geometric ? 1 : 0),
                "CE11 actual ordinary navigation/diagnostic boundary.");
            if (action == 20163) Check(((string)Read(Script(), "MechanicHintsText")).Contains("Summon crows"), "CE11 summon text.");
            if (action == 20166)
                Check(((string)Read(Script(), "MechanicHintsText")).StartsWith("Interrupt ")
                    && ((IEnumerable)Read(Script(), "InterruptTargetIds")).Cast<ulong>().SequenceEqual(new[] { 20UL }),
                    "CE11 script publishes original interrupt diagnostic without action execution.");
            if (action == 20175)
                Check(((string)Read(Script(), "MechanicHintsText")).Contains("Raidwide + deathwall appears"),
                    "CE11 custom raidwide text.");
            Seek(10.1);
            Check(Items("Casts").Length == 1 && dangers() == (geometric ? 1 : 0), "CE11 ordinary no TTL.");
            Seek(11.1);
            Check(Items("Casts").Length == 0 && dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE11 watched raw Finish.");
            Check(!((IEnumerable)Read(Script(), "InterruptTargetIds")).Cast<ulong>().Any(), "CE11 Finish clears interrupt diagnostic.");
            var stoppedScript = Script();
            stop();
            Check(!((IEnumerable)Read(stoppedScript, "InterruptTargetIds")).Cast<ulong>().Any(), "CE11 Stop clears interrupt diagnostic.");
            Console.WriteLine($"PASS native CE11 {action} real DLL geometry hints diagnostics exact source Finish no TTL");
        }
        load(id, [Header(920), World(0, DeathHand(Spell(20182, center)), true),
            World(1, [Actor(30, 0x1EB02E, center)]), World(2, [], removed: [30]), World(3, [])]);
        Seek(.1);
        snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        Check(dangers() == 1 && Shown("bozja/aoe/").Length == 1
            && Read(snapshot, "ArenaBoundary").GetType().Name == "WorldPolygonGeometry", "CE11 initial polygon donut.");
        Seek(1.1);
        snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        Check(dangers() == 0 && Read(snapshot, "ArenaBoundary").GetType().Name == "CircleGeometry"
            && (float)Read(Read(snapshot, "ArenaBoundary"), "Radius") == 25,
            "CE11 same-center actual Created switches boundary.");
        Seek(2.1);
        Check((float)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, "Radius") == 25,
            "CE11 destroyed wall does not restore.");
        Seek(.1); Check(dangers() == 1, "CE11 reverse Seek restores donut.");
        stop(); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "CE11 arena owner release.");
        Console.WriteLine("PASS native CE11 real deathwall Created same center circle boundary inverse Seek owner");

        object[] WindLines() => ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>()
            .Where(p => ((string)Read(p, "Id")).Contains("bozja/wind/")).ToArray();
        string WindEffect(double t, uint action, byte type = 1)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 20UL, ActionId = action, ActionType = type, MainTargetId = 100UL,
                    GlobalSequence = 0, Targets = new[] { 100UL } } });
        foreach (var action in new uint[] { 20184, 20185, 20186, 20187 })
        {
            load(id, [Header(920), World(0, DeathHand(Spell(20183, center)), true),
                World(1, [Actor(100, 0, center, stackedStatus: 1209)]),
                World(2, [Actor(100, 0, center)]), World(3, [Actor(20, 0x233C, center)]),
                WindEffect(3.2, action, 2), WindEffect(4, action), World(5, [])]);
            Seek(.1);
            Check(dangers() == 1 && WindLines().Length == 1 && Shown("bozja/aoe/").Length == 0,
                "CE11 Wind actual cone and line without invented visual rectangle.");
            var locked = Items("Casts").Single();
            var line = WindLines().Single();
            var rotation = (float)Read(locked, "Rotation");
            var (sin, cos) = Math.SinCos(rotation);
            Check((Vector3)Read(line, "Start") == center
                && (Vector3)Read(line, "End") == center + 30 * new Vector3((float)sin, 0, (float)cos)
                && !(bool)Read(line, "Safe"), "CE11 real line exact start end danger.");
            var activation = (DateTime)Read(locked, "Activation");
            Check(activation == Start.AddSeconds(4.3f).AddSeconds(.1d), "CE11 Wind actual locked clock.");
            Seek(1.1); Check(dangers() == 0 && WindLines().Length == 0, "CE11 actual immunity hides cone and line.");
            Seek(2.1); Check(dangers() == 1 && WindLines().Length == 1, "CE11 immunity loss restores saved Wind.");
            Seek(3.5); Check(dangers() == 1 && WindLines().Length == 1, "CE11 Finish and nonspell effect do not consume Wind.");
            Seek(4.5); Check(dangers() == 0 && WindLines().Length == 0, "CE11 actual NPC spell effect clears Wind.");
            Seek(.1); Check(dangers() == 1 && WindLines().Length == 1, "CE11 inverse Seek Wind.");
            stop(); Check(dangers() == 0 && WindLines().Length == 0 && previews().Length == 0, "CE11 Wind stop owner.");
            Console.WriteLine($"PASS native CE11 Wind {action} real cone line immunity Finish retention actual native Effect Seek owner");
        }
        load(id, [Header(920), World(0, [.. DeathHand(), Actor(30, 0x2DAA, center)], true),
            World(1, [Actor(30, 0x2DAA, center + Vector3.UnitX, rotation: .731f, dead: true, eventState: 7)]),
            World(2, [], removed: [30]), World(3, [])]);
        Seek(.1);
        Check(dangers() == 4 && Shown("bozja/aoe/").Length == 1
            && ((IEnumerable)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "HardObstacles")).Cast<object>().Count() == 1,
            "CE11 whirlwind actual 4 forecasts obstacle.");
        Seek(1.1); Check(dangers() == 4 && Shown("bozja/aoe/").Length == 1, "CE11 whirlwind dead/state7 moving retained.");
        Seek(2.1); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE11 whirlwind true Destroy.");
        stop();
        Console.WriteLine("PASS native CE11 whirlwind parameterized source OID four clocks obstacle moving dead true Destroy");
        load(id, [Header(920), World(0, DeathHand(Spell(20173, center)), true),
            World(1, [Actor(101, 0, center)]), World(2, [Actor(20, 0x233C, center)]), World(3, [])]);
        Seek(1.1);
        Check(Shown("bozja/stack/").Length == 1 && ((string)Read(Script(), "MechanicHintsText")).Contains("Stack!")
            && !((string)Read(Script(), "MechanicHintsText")).Contains("已满足"), "CE11 Helldive min8.");
        Seek(2.1); Check(Shown("bozja/stack/").Length == 0 && dangers() == 0, "CE11 Helldive Finish.");
        stop();
        Console.WriteLine("PASS native CE11 Helldive min8 actual current player target hints Finish owner");

        center = new(-239.999f, 0, 413.999f);
        object PatriotMember(uint job, uint hp)
        {
            var node = JsonSerializer.SerializeToNode(Actor(100, 0, center, classJob: job))!;
            node["Hp"] = hp;
            node["MaxHp"] = 100000;
            return node;
        }
        object[] Patriot(object? cast = null) =>
            [Actor(1, 0x2E14, center, combat: true, isEnemy: true), PatriotMember(19, 1000),
                Actor(101, 0, center + new Vector3(-20, 0, 0), classJob: 19),
                Actor(20, 0x233C, center, cast: cast)];
        Check((bool)owns.Invoke(null, [920u, 0x2E14u])! && !(bool)owns.Invoke(null, [921u, 0x2E14u])!,
            "CE24 exact ownership.");
        foreach (var action in new uint[] { 20444, 20446, 20455, 20451, 20453 })
        {
            load(id, [Header(920), World(0, Patriot(Spell(action, center)), true),
                World(10, []), World(11, [Actor(20, 0x233C, center)]), World(12, [])]);
            Seek(.1);
            var aoe = action is 20444 or 20446 or 20455;
            Check(dangers() == (aoe ? 1 : 0) && Shown("bozja/aoe/").Length == (aoe ? 1 : 0),
                "CE24 ordinary actual navigation.");
            if (aoe)
            {
                var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
                Check((float)Read(shape, "Radius") == (action == 20444 ? 30 : action == 20446 ? 10 : 15),
                    "CE24 exact source radius.");
                if (action == 20444) Check((float)Read(shape, "AngleRadians") == 2 * (60 * MathF.PI / 180),
                    "CE24 source halfangle60.");
            }
            else Check(((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1,
                "CE24 both SingleTargetCast diagnostic no raidwide geometry.");
            var arena = Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "ArenaBoundary");
            Check((bool)arena.GetType().GetMethod("Contains")!.Invoke(arena, [center + new Vector3(24, 0, 24), 0f])!
                && !(bool)arena.GetType().GetMethod("Contains")!.Invoke(arena, [center + new Vector3(24.6f, 0, 0), 0f])!,
                "CE24 real registry square includes corner.");
            Seek(10.1); Check(Items("Casts").Length == 1, "CE24 raw no TTL.");
            Seek(11.1); Check(Items("Casts").Length == 0 && dangers() == 0, "CE24 watched Finish.");
            stop();
            Console.WriteLine($"PASS native CE24 {action} exact real geometry single target diagnostic square boundary no TTL Finish");
        }

        object Laser(ulong actor, ushort tether, ulong target = 1)
            => Actor(actor, 0x2F02, center + new Vector3(10, 0, 0), rotation: .731f,
                tethers: [new { Id = tether, TargetId = target }]);
        load(id, [Header(920), World(0, [.. Patriot().Take(3), Laser(20, 114), Laser(21, 115)], true),
            WindEffect(.5, 20448), World(1, [PatriotMember(24, 39999)]),
            World(2, [PatriotMember(24, 40000)]), WindEffect(2.5, 20443),
            World(3, [PatriotMember(24, 39999), Laser(20, 114, 100), Laser(21, 115, 100)]),
            WindEffect(4, 20449), World(5, [])]);
        Seek(.1);
        Check(Items("Casts").Length == 1 && Shown("bozja/aoe/").Length == 1 && dangers() == 0,
            "CE24 cold two tethers display but no mine no navigation.");
        var reaction = Items("Casts").Single();
        Check((DateTime)Read(reaction, "Activation") == Start.AddSeconds(10.2d), "CE24 cold second tether exact clock.");
        Seek(1.1);
        Check(Items("Mines").Length == 1 && dangers() == 2 && Shown("bozja/mine/").Length == 1
            && (float)Read(Read(Shown("bozja/mine/").Single(), "Geometry"), "Radius") == 8
            && Shown("bozja/aoe/").Length == 1, "CE24 forbidden actual R8 mine + rect navigation.");
        Seek(2.1);
        Check(dangers() == 0 && Shown("bozja/aoe/").Length == 1
            && (float)Read(Read(Shown("bozja/mine/").Single(), "Geometry"), "Radius") == 4,
            "CE24 HP exactly40000 allows mine suppresses rect navigation keeps display.");
        Seek(2.6); Check(Items("Casts").Length == 0 && Items("Mines").Length == 1, "CE24 Effect clears only AOEs.");
        Seek(3.1);
        Check(Items("Casts").Length == 1 && dangers() == 2
            && (DateTime)Read(Items("Casts").Single(), "Activation") == Start.AddSeconds(3).AddSeconds(10.2d),
            "CE24 retained spatial charge state produces next pair.");
        var patriotName = (string)Read(Read(Model(), "Definition"), "Name");
        manager.SetMechanismEnabled(id, patriotName, false); Seek(3.1);
        Check(previews().Length == 0 && dangers() == 0, "CE24 mechanism disabled owner cleanup.");
        manager.SetMechanismEnabled(id, patriotName, true); Seek(3.1);
        Check(dangers() == 2, "CE24 enabled reconstruction.");
        Seek(4.1);
        Check(Items("Mines").Length == 0 && Shown("bozja/aoe/").Length == 1 && dangers() == 0,
            "CE24 explosion removes mine and suppresses rect navigation keeps display.");
        Seek(1.1); Check(Items("Mines").Length == 1 && dangers() == 2, "CE24 reverse Seek rebuilt mine/tethers.");
        stop();
        Check(dangers() == 0 && previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE24 Stop owner cleanup.");
        Console.WriteLine("PASS native CE24 real effects mine R4 R8 HP40000 conditional laser navigation cold tether pair retained charges switch reverse Seek owner");

        string PatriotTether(double t, ushort tether, ulong target)
            => JsonSerializer.Serialize(new { t, kind = "Tether", type = typeof(LogSystemTetherEvent).FullName,
                data = new { SourceId = 20UL, TargetId = target, TetherId = tether } });
        load(id, [Header(920), World(0, Patriot(), true),
            PatriotTether(1, 114, 1), PatriotTether(2, 114, 1),
            PatriotTether(3, 115, 0), PatriotTether(4, 115, 0xE0000000),
            PatriotTether(5, 114, 1), WindEffect(6, 20443),
            PatriotTether(7, 114, 1), World(8, [])]);
        Seek(1.1); Check(Items("Casts").Length == 0, "CE24 first hot event half charge.");
        Seek(2.1);
        Check(Items("Casts").Length == 1 && Shown("bozja/aoe/").Length == 1
            && (DateTime)Read(Items("Casts").Single(), "Activation") == Start.AddSeconds(2).AddSeconds(10.2d),
            "CE24 repeated identical actual event must charge twice.");
        Seek(4.1); Check(Items("Casts").Length == 1, "CE24 hot zero and sentinel cannot charge.");
        Seek(6.1); Check(Items("Casts").Length == 0, "CE24 actual clear effect.");
        Seek(7.1);
        Check(Items("Casts").Length == 1
            && (DateTime)Read(Items("Casts").Single(), "Activation") == Start.AddSeconds(7).AddSeconds(10.2d),
            "CE24 actual effect retains half charge.");
        Seek(2.1); Check(Items("Casts").Length == 1, "CE24 duplicate real event reverse Seek.");
        stop(); Check(previews().Length == 0 && dangers() == 0, "CE24 hot events owner release.");
        Console.WriteLine("PASS native CE24 real duplicate Tether events zero sentinel targets effect half charge reverse Seek owner");

        void RaisePatriot(LogSystemEvent e)
        {
            var context = Read(manager.Running[id], "Context");
            var router = Read(context, "_router");
            router.GetType().GetMethod("Raise")!.Invoke(router, [e]);
            Check((int)Read(router, "ErrorCount") == 0, "CE24 actual batched router error.");
        }
        void UpdatePatriot() => Script().GetType().BaseType!.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.02f]);
        load(id, [Header(920), World(0, Patriot(), true), World(10, [])]);
        Seek(.1); world = Read(Script(), "ReplayWorld");
        Apply(.2, [Laser(20, 114, 100)], false);
        RaisePatriot(new LogSystemTetherEvent { SourceId = 20, TetherId = 114, TargetId = 1 });
        RaisePatriot(new LogSystemTetherEvent { SourceId = 20, TetherId = 114, TargetId = 100 });
        Check(Items("Casts").Length == 0, "CE24 queued packets must await the frame pass.");
        UpdatePatriot();
        Check(Items("Casts").Length == 1
            && (DateTime)Read(Items("Casts").Single(), "Activation") == Start.AddSeconds(.2).AddSeconds(10.2d),
            "CE24 real script one snapshot then two packets must charge three times.");
        RaisePatriot(new LogSystemTetherEvent { SourceId = 20, TetherId = 114, TargetId = 100 });
        UpdatePatriot();
        Check(Items("Casts").Length == 2, "CE24 event batch retained the odd half charge.");
        RaisePatriot(new LogSystemTetherEvent { SourceId = 20, TetherId = 114, TargetId = 100 });
        var clearEvent = (LogSystemEvent)JsonSerializer.Deserialize(
            JsonNode.Parse(WindEffect(.2, 20443))!["data"]!.ToJsonString(),
            assembly.GetType("MasterPlayer.Scripting.NativeActionEffectEvent")!)!;
        RaisePatriot(clearEvent);
        RaisePatriot(new LogSystemTetherEvent { SourceId = 20, TetherId = 114, TargetId = 100 });
        UpdatePatriot();
        Check(Items("Casts").Length == 1, "CE24 mixed tether Effect tether preserves actual queue order and half charge.");
        stop();
        Console.WriteLine("PASS native CE24 actual router multiple packets stale fixed snapshot exact mixed effect batch order half charge");

        load(id, [Header(920), World(0, [.. Patriot().Take(3), Laser(20, 114)], true), World(10, [])]);
        Seek(.1); world = Read(Script(), "ReplayWorld");
        RaisePatriot(new LogSystemTetherCancelEvent { SourceId = 20 });
        RaisePatriot(new LogSystemWeatherChangedEvent { WeatherId = 7 });
        UpdatePatriot();
        Check(Items("Casts").Length == 0, "CE24 Cancel then unrelated event must not resample stale tether.");
        stop();
        Console.WriteLine("PASS native CE24 actual router Cancel unrelated event no invented stale snapshot charge");

        load(id, [Header(920), World(0, [Actor(1, 0x2E14, center, isEnemy: true),
            PatriotMember(19, 1000), Actor(101, 0, center), Actor(20, 0x2F02, center),
            Actor(21, 0x2F02, center)], true), World(10, [])]);
        Seek(.1); world = Read(Script(), "ReplayWorld");
        Apply(.2, [Actor(1, 0x2E14, center, combat: true, isEnemy: true)], false);
        RaisePatriot(new LogSystemTetherEvent { SourceId = 20, TetherId = 114, TargetId = 1 });
        RaisePatriot(new LogSystemTetherEvent { SourceId = 21, TetherId = 115, TargetId = 1 });
        Check(!(bool)Read(Script(), "EncounterActive"), "CE24 event scans cannot activate the module.");
        UpdatePatriot();
        Check((bool)Read(Script(), "EncounterActive") && Items("Casts").Length == 1,
            "CE24 actual packets before cold activation survive empty snapshots.");
        stop(); Check(previews().Length == 0 && dangers() == 0, "CE24 batch stop owner release.");
        Console.WriteLine("PASS native CE24 actual router packets before cold activation effective tether state owner");

        center = new(-234, 0, 262);
        object[] MetalFox(object? cast = null, Vector3? sourcePosition = null) =>
            [Actor(1, 0x2DB5, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + new Vector3(-20, 0, 0)),
                Actor(20, 0x233C, sourcePosition ?? center, rotation: .731f, cast: cast)];
        Check((bool)owns.Invoke(null, [920u, 0x2DB5u])! && !(bool)owns.Invoke(null, [921u, 0x2DB5u])!,
            "CE31 exact source ownership.");
        foreach (var action in new uint[] { 20137, 20138, 20140 })
        {
            load(id, [Header(920), World(0, MetalFox(Spell(action, center)), true),
                World(10, []), World(11, [Actor(20, 0x233C, center)]), World(12, [])]);
            Seek(.1);
            Check(dangers() == (action == 20137 ? 0 : 1) && Shown("bozja/aoe/").Length == (action == 20137 ? 0 : 1),
                "CE31 real ordinary geometry or raidwide diagnostic.");
            if (action == 20137)
                Check(((string)Read(Script(), "MechanicHintsText")).Contains("all lasers fire at the same time")
                    && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1, "CE31 custom raidwide hint/damage.");
            var arena = Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime), "ArenaBoundary");
            Check((bool)arena.GetType().GetMethod("Contains")!.Invoke(arena, [center + new Vector3(29, 0, 29), 0f])!
                && !(bool)arena.GetType().GetMethod("Contains")!.Invoke(arena, [center + new Vector3(29.6f, 0, 0), 0f])!,
                "CE31 actual half29point5 square.");
            if (action == 20140)
            {
                snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) > 10,
                    "CE31 actual square solver R10 escape.");
            }
            Seek(10.1);
            Check(Items("Casts").Length == 1, "CE31 ordinary no TTL.");
            Seek(11.1);
            Check(Items("Casts").Length == 0 && dangers() == 0, "CE31 ordinary watched Finish.");
            stop();
            Console.WriteLine($"PASS native CE31 {action} real geometry diagnostic custom text square boundary no TTL Finish solver");
        }

        object Bit(ulong actor, float rotation, bool dead = false)
            => Actor(actor, 0x2DB6, center + new Vector3((actor - 30) * 4, 0, -15), rotation: rotation, dead: dead);
        var bitRecords = new List<string>
        {
            Header(920), World(0, [.. MetalFox(Spell(20140, center)),
                Bit(30, .731f), Bit(31, .731f + MathF.PI / 2), Bit(32, .731f + MathF.PI)], true),
            World(.2, [Actor(20, 0x233C, center, rotation: .731f)]),
        };
        bitRecords.AddRange(Enumerable.Range(0, 14).Select(i => WindEffect(1 + i * .01, 20141)));
        bitRecords.Add(World(2, []));
        load(id, bitRecords); Seek(.3);
        Check(Shown("bozja/aoe/").Length == 2 && dangers() == 2
            && Shown("bozja/aoe/").Count(p => Read(p, "Color") == null) == 1,
            "CE31 default colour bit remains risky and registered.");
        Seek(1.041);
        Check(Shown("bozja/aoe/").Length == 2 && dangers() == 2
            && Shown("bozja/aoe/").Count(p => Read(p, "Color") == null) == 1, "CE31 five Effects switches directions/colour.");
        var foxZones = NativeZones();
        Check(foxZones.Select(z => (DateTime)Read(z, "Activation")).SequenceEqual(
            new[] { Start.AddSeconds(8.3d), Start.AddSeconds(10.1d) }), "CE31 raw side/rear source order.");
        Seek(1.081);
        Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1
            && Read(Shown("bozja/aoe/").Single(), "Color") == null, "CE31 nine Effects leaves risky rear default colour.");
        Seek(1.2); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE31 exactly fourteen Effects resets.");
        Seek(.3); Check(dangers() == 2, "CE31 bit reverse Seek restores.");
        stop(); Check(dangers() == 0 && previews().Length == 0, "CE31 bit owner release.");
        Console.WriteLine("PASS native CE31 real bit mode actor rotation default risky exact five nine fourteen Effects colour clock reverse Seek owner");

        var rushOrigin = center + new Vector3(0, 0, -10);
        load(id, [Header(920), World(0, MetalFox(Spell(20139, center), rushOrigin), true),
            World(1, [Actor(100, 0, center + new Vector3(10, 0, 0))]), WindEffect(1.5, 20139),
            World(2, [], removed: [100]), World(3, [Actor(100, 0, center + new Vector3(-10, 0, 0))]),
            World(4, [Actor(20, 0x233C, rushOrigin)]), World(5, [])]);
        Seek(.1);
        Check(Items("Rushes").Length == 1 && Shown("bozja/rush/").Length == 1 && dangers() == 1
            && (bool)Read(Shown("bozja/rush/").Single(), "OutlineOnly")
            && (Vector4)Read(Shown("bozja/rush/").Single(), "Color") == new Vector4(1, 0, 0, 1),
            "CE31 own Rush red outline and buddy cone.");
        Check(Read(NativeZones().Single(), "Shape").GetType().Name == "FanTemplate", "CE31 original target navigation is buddy cone.");
        var rushClock = (DateTime)Read(Items("Rushes").Single(), "Activation");
        Seek(1.1);
        Check((float)Read(Read(Shown("bozja/rush/").Single(), "Geometry"), "Length") == MathF.Sqrt(200)
            && (DateTime)Read(Items("Rushes").Single(), "Activation") == rushClock, "CE31 Update length but fixed activation.");
        Seek(1.6); Check(Items("Rushes").Length == 1, "CE31 actual Effect cannot consume Rush.");
        Seek(3.1);
        Check((bool)Read(Shown("bozja/rush/").Single(), "OutlineOnly")
            && Read(NativeZones().Single(), "Shape").GetType().Name == "RayRectangleTemplate"
            && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1,
            "CE31 new sameID viewer display by ID navigation by original reference damage by current slot.");
        Seek(4.1); Check(Items("Rushes").Length == 0 && dangers() == 0 && Shown("bozja/rush/").Length == 0,
            "CE31 actual watched Finish clears Rush.");
        Seek(.1); Check(Items("Rushes").Length == 1, "CE31 Rush reverse Seek.");
        var foxName = (string)Read(Read(Model(), "Definition"), "Name");
        manager.SetMechanismEnabled(id, foxName, false); Seek(.1);
        Check(previews().Length == 0 && dangers() == 0, "CE31 disabled owner cleanup.");
        manager.SetMechanismEnabled(id, foxName, true); Seek(.1); Check(dangers() == 1, "CE31 reenable replay restore.");
        stop(); Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE31 owner Stop boundary release.");
        Console.WriteLine("PASS native CE31 Rush original target dynamic length fixed clock real Effect retention ID display reference navigation Finish switch reverse Seek owner");

        load(id, [Header(920), World(0, MetalFox(Spell(20139, center), rushOrigin), true),
            World(.5, [Actor(21, 0x233C, center + new Vector3(0, 0, -15), cast: Spell(20139, center, .5, 101)),
                Actor(22, 0x233C, center, cast: Spell(20140, center, .5))]),
            World(1, [Actor(101, 0, rushOrigin + new Vector3(6, 0, 0))]), World(2, [])]);
        Seek(.6);
        var foxDraw = previews().Where(p => ((string)Read(p, "Id")).Contains("bozja/aoe/")
            || ((string)Read(p, "Id")).Contains("bozja/rush/")).ToArray();
        Check(foxDraw.Length == 3 && ((string)Read(foxDraw[0], "Id")).Contains("bozja/aoe/")
            && ((string)Read(foxDraw[1], "Id")).EndsWith("/background")
            && !(bool)Read(foxDraw[1], "OutlineOnly")
            && ((string)Read(foxDraw[2], "Id")).EndsWith("/outline")
            && (bool)Read(foxDraw[2], "OutlineOnly"), "CE31 actual component background pass before own foreground: "
                + string.Join(",", foxDraw.Select(p => $"{Read(p, "Id")}:{Read(p, "OutlineOnly")}")));
        Seek(1.1);
        Check(NativeZones().Count(z => Read(z, "Shape").GetType().Name == "FanTemplate") == 1
            && dangers() == 2 && Shown("bozja/rush/").Length == 2,
            "CE31 near buddy NaN fan registers zero handle while other rectangle and circle remain.");
        stop();
        Console.WriteLine("PASS native CE31 mixed component other filled and own outline draw pass NaN zero navigation handle");

        load(id, [Header(920), World(0, MetalFox(Spell(20139, center, target: 999), rushOrigin), true),
            World(1, [Actor(999, 0, center)]), World(2, [])]);
        Seek(1.1);
        Check(Items("Rushes").Length == 0 && Shown("bozja/rush/").Length == 0 && dangers() == 0,
            "CE31 cold missing target is not retroactively resolved.");
        stop();
        load(id, [Header(920), World(0, MetalFox(Spell(20139, center), rushOrigin), true),
            World(1, [], removed: [20]), World(2, [])]);
        Seek(.1); Check(Items("Rushes").Length == 1, "CE31 source destroy fixture activated.");
        Seek(1.1); Check(Items("Rushes").Length == 0 && Shown("bozja/rush/").Length == 0 && dangers() == 0,
            "CE31 actual source destroy synthesizes watched Finish.");
        stop();
        Console.WriteLine("PASS native CE31 cold missing target no late bait source destroyed real Feed Finish owner");

        center = new(-450, 0, 262);
        object[] Hoof(object? cast = null, Vector3? position = null) =>
            [Actor(1, 0x2ECF, center, combat: true, isEnemy: true, objectIndex: 2),
                Actor(100, 0, center - Vector3.UnitZ * 10), Actor(101, 0, center + Vector3.UnitX * 7),
                Actor(20, 0x233C, position ?? center, cast: cast, subKind: 5)];
        object HoofDying(object actor)
        {
            var node = JsonSerializer.SerializeToNode(actor)!;
            node["Hp"] = 1;
            return node;
        }
        string HoofEffectRecord(double t, uint action, ulong source = 20, ulong target = 100, byte type = 1)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = type, MainTargetId = target,
                    GlobalSequence = 0, Targets = new[] { target } } });
        Check((bool)owns.Invoke(null, [920u, 0x2ECFu])! && !(bool)owns.Invoke(null, [921u, 0x2ECFu])!,
            "CE34 exact source ownership.");
        foreach (var action in new uint[] { 20966, 20964, 20967 })
        {
            load(id, [Header(920), World(0, Hoof(Spell(action, center)), true),
                World(10, []), World(11, [Actor(20, 0x233C, center)]), World(12, [])]);
            Seek(.1);
            var shown = Shown(action == 20967 ? "bozja/stack/" : "bozja/aoe/");
            Check(shown.Length == 1 && Read(shown.Single(), "Geometry").GetType().Name
                == (action == 20964 ? "FanGeometry" : "CircleGeometry"), "CE34 ordinary and R8 stack actual geometry.");
            if (action != 20964)
                Check((float)Read(Read(shown.Single(), "Geometry"), "Radius") == 8, "CE34 actual R8.");
            Seek(10.1); Check(Shown(action == 20967 ? "bozja/stack/" : "bozja/aoe/").Length == 1, "CE34 no TTL.");
            Seek(11.1); Check(Items("Casts").Length == 0 && dangers() == 0, "CE34 watched Finish.");
            stop();
            Console.WriteLine($"PASS native CE34 {action} real R8 circle stack or cone no TTL watched Finish owner");
        }

        load(id, [Header(920), World(0, Hoof(Spell(20968, center)), true),
            World(1, [Actor(20, 0x233C, center)]),
            World(2, [Actor(30, 0x1EB030, center)]), World(3, [], removed: [30]),
            HoofEffectRecord(4, 20968, target: 101), HoofEffectRecord(5, 20968, source: 1),
            World(6, [])]);
        Seek(.1);
        Check(Items("DelayedHits").Length == 1 && dangers() == 0, "CE34 start locks delayed hit without premature arena donut.");
        var hoofClock = (DateTime)Read(Items("DelayedHits").Single(), "Activation");
        Seek(1.1);
        Check(Items("DelayedHits").Length == 1 && dangers() == 1
            && (DateTime)Read(Items("Casts").Single(), "Activation") == Start.AddSeconds(4.3f).AddSeconds(6d),
            "CE34 early Finish creates exact remaining clock plus6 and retains delayed damage.");
        Seek(2.1);
        Check(dangers() == 0 && Items("DelayedHits").Length == 1
            && (float)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, "Radius") == 20,
            "CE34 actual wall Created clears donut switches same center R20.");
        Seek(3.1); Check((float)Read((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary, "Radius") == 20,
            "CE34 wall Destroy preserves circle.");
        Seek(4.1); Check(Items("DelayedHits").Length == 1
            && (DateTime)Read(Items("DelayedHits").Single(), "Activation") == hoofClock, "CE34 other Effect target cannot consume.");
        Seek(5.1); Check(Items("DelayedHits").Length == 0, "CE34 real MainTargetId any NPC consumes delayed hit.");
        Seek(1.1); Check(dangers() == 1 && Items("DelayedHits").Length == 1, "CE34 arena and delay inverse Seek.");
        stop();
        Console.WriteLine("PASS native CE34 real early Finish residual NPC clock donut Created shrink delayed original target MainTargetId reverse Seek owner");

        var glimmer = center + new Vector3(2.123f, 0, 3.257f);
        var glimmerRecords = new List<string>
        {
            Header(920), World(0, [.. Hoof(), Actor(30, 0x2ED1, glimmer), Actor(31, 0x2ED1, glimmer)], true),
            World(.5, [], removed: [30, 31]),
            World(.6, [Actor(20, 0x233C, glimmer)]),
        };
        glimmerRecords.AddRange(Enumerable.Range(0, 10).Select(i => HoofEffectRecord(1 + i * .01, 20963)));
        glimmerRecords.Add(World(10, []));
        load(id, glimmerRecords); Seek(.7);
        Check(Items("Bursts").Length == 2 && dangers() == 2 && Shown("bozja/aoe/").Length == 2,
            "CE34 original birth circles survive source Destroy.");
        Seek(1.081); Check(Items("Bursts").Length == 2 && dangers() == 2, "CE34 ninth Effect remains.");
        Seek(1.091); Check(Items("Bursts").Length == 1 && dangers() == 1, "CE34 tenth Effect consumes first same-position only.");
        Seek(9); Check(Items("Bursts").Length == 1 && dangers() == 1, "CE34 glimmer no TTL.");
        stop();
        Console.WriteLine("PASS native CE34 real Glimmer duplicate births destroyed actors arbitrary source ninth tenth first position no TTL owner");

        object[] EyeLines() => ((IEnumerable)registry.GetType().GetMethod("GetLines")!.Invoke(registry, null)!).Cast<object>()
            .Where(p => ((string)Read(p, "Id")).Contains("bozja/eye/")).ToArray();
        load(id, [Header(920), World(0, Hoof(Spell(20962, center)), true),
            World(4, [Actor(1, 0x2ECF, center, combat: true, cast: Spell(20961, center, 4),
                    isEnemy: true, objectIndex: 2), Actor(100, 0, center - Vector3.UnitZ * 10 + Vector3.UnitY * 5, rotation: MathF.PI)]),
            World(5, [Actor(20, 0x233C, center)]), World(6, [])]);
        Seek(3.7);
        Check(Items("Eyes").Length == 1 && EyeLines().Length >= 12
            && Shown("bozja/eye/").Length == 1 && dangers() == 0
            && Read(Script(), "PlannedFacing") is float away && MathF.Cos(away) < .707107f,
            "CE34 normal eye actual shape lines and imminent facing without fabricated danger zone.");
        Seek(4.1);
        Check((bool)Read(Items("Eyes").Single(), "Inverted") && EyeLines().Length >= 36
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Face the eye!")
            && Read(Script(), "PlannedFacing") is float toward && MathF.Cos(toward) >= .707107f,
            "CE34 inverted eye updates arc personal hint and joint facing.");
        Check((Vector3)Read(Read(Shown("bozja/eye/").Single(), "Geometry"), "Position") is { Y: 5 }
            && EyeLines().All(l => ((Vector3)Read(l, "Start")).Y == 5 && ((Vector3)Read(l, "End")).Y == 5),
            "CE34 eyes pupils and arcs use current viewer height.");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0, "CE34 replay planning emits no facing or target execution.");
        Seek(5.1); Check(Items("Eyes").Length == 0 && EyeLines().Length == 0 && Read(Script(), "PlannedFacing") == null,
            "CE34 any FalseDemonEye Finish clears all eyes lines facing.");
        Seek(4.1);
        var hoofName = (string)Read(Read(Model(), "Definition"), "Name");
        manager.SetMechanismEnabled(id, hoofName, false); Seek(4.1);
        Check(previews().Length == 0 && EyeLines().Length == 0 && Read(Script(), "PlannedFacing") == null,
            "CE34 disabled mechanism owner clears eye and facing.");
        manager.SetMechanismEnabled(id, hoofName, true); Seek(4.1);
        stop(); Check(EyeLines().Length == 0 && previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE34 Stop clears all eyes boundary and handles.");
        Console.WriteLine("PASS native CE34 normal inverted eyes real lines hints imminent facing replay isolation disable reverse Seek owner");

        var highPerspective = JsonNode.Parse(World(1,
            [HoofDying(Actor(41, 0x2D8A, center + Vector3.UnitX * 5, isEnemy: true, objectIndex: 6)),
                Actor(101, 0, center + Vector3.UnitY * 20)]))!;
        highPerspective["data"]!["LocalPlayerId"] = 101;
        load(id, [Header(920), World(0, [.. Hoof(), Actor(40, 0x2D89, center + Vector3.UnitX * 20,
                isEnemy: true, objectIndex: 4), Actor(41, 0x2D8A, center + Vector3.UnitX * 5,
                isEnemy: true, objectIndex: 6)], true),
            highPerspective.ToJsonString(),
            World(2, [Actor(1, 0x2ECF, center, dead: true, isEnemy: true, objectIndex: 2)]), World(3, [])]);
        Seek(.1); Check((ulong)Read(Script(), "PlannedTargetId") == 41
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0, "CE34 legal add priority nearest XZ target planned only in replay.");
        Seek(1.1); Check((ulong)Read(Script(), "PlannedTargetId") == 40, "CE34 PendingDead add cannot overwrite pointless.");
        Seek(2.1); Check((ulong)Read(Script(), "PlannedTargetId") == 0 && Read(Script(), "PlannedFacing") == null
            && previews().Length == 0 && dangers() == 0, "CE34 primary completion clears outputs while adds live.");
        stop();
        Console.WriteLine("PASS native CE34 actual target priority legal adds nearest Pointless protection replay execution isolation primary completion owner");

        center = new(231, 0, 95);
        object[] Behemoth(object? cast = null, Vector3? helper = null) =>
            [Actor(1, 0x2DCB, center, combat: true, isEnemy: true),
                Actor(100, 0, center), Actor(101, 0, center + new Vector3(-15, 0, 0)),
                Actor(20, 0x233C, helper ?? center, cast: cast, subKind: 5)];
        string BehemothEffect(double t, uint action, Vector3 pos, ulong target = 999, ulong source = 20)
            => JsonSerializer.Serialize(new { t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = target,
                    GlobalSequence = 0, Targets = Array.Empty<ulong>(), TargetPosition = new { pos.X, pos.Y, pos.Z } } });
        object Corpse(ulong actorId, Vector3 p, float rotation = .4f, float radius = 4.35f)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0x2DCC, p, rotation: rotation, dead: true))!;
            node["HitboxRadius"] = radius;
            node["EventState"] = 7;
            return node;
        }
        Check((bool)owns.Invoke(null, [920u, 0x2DCBu])! && !(bool)owns.Invoke(null, [921u, 0x2DCBu])!
            && !(bool)owns.Invoke(null, [920u, 0x2DBEu])!, "CE33 precise source ownership.");

        load(id, [Header(920), World(0, Behemoth(Spell(20198, center)), true),
            World(10, []), World(11, [Actor(20, 0x233C, center)]), World(12, [])]);
        Seek(.1);
        Check(dangers() == 1 && Shown("bozja/aoe/").Length == 1
            && Read(Shown("bozja/aoe/").Single(), "Geometry").GetType().Name == "RayRectangleGeometry",
            "CE33 actual R60 width6 rectangle navigation and preview.");
        snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback"),
            "CE33 actual rectangle solver must escape without fallback.");
        Seek(10.1); Check(dangers() == 1, "CE33 ordinary no TTL.");
        Seek(11.1); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE33 watched raw Finish.");
        Seek(.1); Check(dangers() == 1, "CE33 rectangle reverse Seek.");
        stop();
        Console.WriteLine("PASS native CE33 actual ordinary rectangle dual view navigation real solver no fallback no TTL raw Finish reverse Seek owner");

        load(id, [Header(920), World(0, Behemoth(Spell(20194, center)), true), World(10, [])]);
        Seek(.1);
        Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0
            && !((string?)Read(Script(), "MechanicHintsText") ?? "").Contains("Hide behind"),
            "CE33 no corpse cannot invent blanket meteor danger.");
        stop();
        load(id, [Header(920), World(0, [.. Behemoth(Spell(20194, center)),
            Corpse(30, center + Vector3.UnitZ * 5, radius: 0)], true), World(10, [])]);
        Seek(.1);
        Check(Items("Meteors").Length == 1 && dangers() == 0 && Shown("bozja/aoe/").Length == 0,
            "CE33 degenerate corpse shadow retains raw queue without empty display or invented navigation.");
        stop();
        load(id, [Header(920), World(0, [.. Behemoth(Spell(20194, center + Vector3.UnitX * 10)),
                Corpse(30, center + new Vector3(4.123f, 0, .257f))], true),
            World(1, [], removed: [30]), World(2, [Actor(20, 0x233C, center)]), World(3, [])]);
        Seek(.1);
        var safeMeteor = Shown("bozja/aoe/").Single();
        Check((bool)Read(safeMeteor, "Safe") && Read(safeMeteor, "Geometry").GetType().Name == "PolygonRegionGeometry",
            "CE33 exact tessellated safe preview.");
        var meteorHandleCount = dangers();
        Seek(1.1);
        Check(Items("Meteors").Length == 1 && dangers() == meteorHandleCount
            && Shown("bozja/aoe/").Length == 1, "CE33 destroyed dead corpse does not rebuild frozen visibility.");
        Seek(2.1); Check(Items("Meteors").Length == 0 && Shown("bozja/aoe/").Length == 0
            && dangers() == 0, "CE33 raw meteor Finish removes first safe zone.");
        Seek(.1); Check(Items("Meteors").Length == 1, "CE33 meteor reverse Seek.");
        stop();
        Console.WriteLine("PASS native CE33 meteor no blockers exact safe polygon dead state7 corpse locked visibility sampled navigation true Finish reverse Seek");

        var juicePoint = center + new Vector3(-8.123f, 0, 7.257f);
        load(id, [Header(920), World(0, Behemoth(Spell(20196, center)), true),
            BehemothEffect(.5, 20196, juicePoint), BehemothEffect(.6, 20196, juicePoint),
            BehemothEffect(.7, 20199, juicePoint),
            World(1, [Actor(20, 0x233C, center)]),
            World(2, [Actor(30, 0x1E972C, juicePoint)]), World(3, [])]);
        Seek(.65);
        Check(Items("JuiceEvents").Length == 2 && dangers() == 3 && Shown("bozja/aoe/").Length == 3,
            "CE33 actual complete repeated zero-sequence Effects retain cast plus event predictions.");
        var eventOrigin = (Vector3)Read(Items("JuiceEvents")[0], "Origin");
        Check(eventOrigin == juicePoint, "CE33 missing MainTarget uses real complete packet ground position.");
        Seek(.75); Check(Items("JuiceEvents").Length == 2, "CE33 repeat20199 not predicted.");
        Seek(1.1); Check(dangers() == 2 && Items("JuiceEvents").Length == 2, "CE33 Finish only removes cast prediction.");
        Seek(2.1);
        Check(Items("JuiceEvents").Length <= 1 && Shown("bozja/aoe/").Length == Items("JuiceEvents").Length + 1,
            "CE33 live voidzone replaces one prediction per actual Update.");
        Seek(.65); Check(Items("JuiceEvents").Length == 2 && dangers() == 3, "CE33 Juice reverse Seek restores exact overlap.");
        stop();
        Console.WriteLine("PASS native CE33 Juice real complete MainTarget fallback ground point repeated packets cast event live overlap Repeat ignored reverse Seek owner");

        load(id, [Header(920), World(0, Behemoth(), true),
            HoofEffectRecord(.5, 20196, target: 999),
            BehemothEffect(1, 20196, Vector3.Zero), World(2, [])]);
        Seek(.6);
        Check(Items("JuiceEvents").Length == 0 && dangers() == 0
            && (int)Read(Model(), "MissingJuicePositionCount") == 1
            && ((string)Read(Script(), "MechanicHintsText")).Contains("缺少目标位置"),
            "CE33 unknown complete packet ground point diagnoses without fabricating zero.");
        Seek(1.1);
        Check((Vector3)Read(Items("JuiceEvents").Single(), "Origin") == Vector3.Zero,
            "CE33 explicitly recorded zero ground point remains valid.");
        stop();
        Console.WriteLine("PASS native CE33 actual nullable complete packet point missing diagnostics distinct from explicitly recorded zero");

        load(id, [Header(920), World(0, Behemoth(Spell(20200, center)), true),
            World(1, [], removed: [20]), World(10, []),
            BehemothEffect(11, 20201, center, source: 1), World(12, [])]);
        Seek(.1);
        var bolt = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
        var boltClock = (DateTime)Read(bolt, "Activation");
        Check(((string)Read(Script(), "MechanicHintsText")).Contains("Raidwide") && dangers() == 0
            && Shown("bozja/aoe/").Length == 0, "CE33 delayed raidwide diagnostic no invented circle.");
        Seek(10.1);
        Check((DateTime)Read(((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single(), "Activation") == boltClock,
            "CE33 visual Destroy and timeout preserve fixed single clock.");
        Seek(11.1); Check(!((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
            "CE33 actual arbitrary source Effect clears delayed raidwide.");
        stop();
        Console.WriteLine("PASS native CE33 WildBolt fixed delayed single clock source Destroy timeout retained arbitrary NPC Effect diagnostic only");

        load(id, [Header(920), World(0, Behemoth(Spell(20193, center + Vector3.UnitX * 10), center + Vector3.UnitX * 10), true),
            World(1, [Actor(20, 0x233C, center + Vector3.UnitX * 10)]),
            World(2, [Actor(100, 0, center + Vector3.UnitZ * 10)]),
            BehemothEffect(3, 20193, center, source: 1),
            BehemothEffect(4, 20193, center), World(5, [])]);
        Seek(.1);
        Check(Items("Horns").Length == 1 && Shown("bozja/horn/").Length == 1
            && (bool)Read(Shown("bozja/horn/").Single(), "OutlineOnly") && dangers() > 0
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Tankbuster cleave"),
            "CE33 own cone outline real party R100 avoidance and global cleave.");
        Seek(1.1); Check(Items("Horns").Length == 1, "CE33 Horn raw Finish retains bait.");
        Seek(2.1);
        Check(MathF.Abs((float)Read(Read(Shown("bozja/horn/").Single(), "Geometry"), "Rotation") + MathF.PI / 4) < .000001f,
            "CE33 original target movement dynamically turns cone.");
        Seek(3.1); Check(Items("Horns").Length == 1, "CE33 another source cannot consume Horn.");
        Seek(4.1); Check(Items("Horns").Length == 0 && dangers() == 0 && Shown("bozja/horn/").Length == 0,
            "CE33 matching source Effect consumes Horn.");
        Seek(1.1); Check(Items("Horns").Length == 1, "CE33 Horn reverse Seek.");
        stop();
        Console.WriteLine("PASS native CE33 Horn actual original target dynamic cone own outline party navigation Finish retained matching source Effect reverse Seek owner");

        load(id, [Header(920), World(0, [.. Behemoth(Spell(20196, center)),
                Corpse(30, center), Corpse(31, center + Vector3.UnitX)], true),
            World(1, [Actor(20, 0x233C, center)]),
            World(2, [], removed: [30, 31]),
            World(3, [Actor(40, 0x233C, center, cast: Spell(20197, center, 3))]),
            World(4, [Actor(40, 0x233C, center)]), World(5, [])]);
        Seek(.1); Check(dangers() == 1, "CE33 no invented cold Thunderbolt.");
        Seek(1.1); Check(dangers() == 1 && Shown("bozja/aoe/").Length == 1,
            "CE33 Juice Finish generates one first corpse locked cone.");
        Seek(2.1); Check(dangers() == 1, "CE33 corpse destruction does not clear Thunderbolt forecast.");
        Seek(3.1); Check(dangers() == 1, "CE33 Thunderbolt Start does not append forecast.");
        Seek(4.1); Check(dangers() == 0, "CE33 any Thunderbolt Finish clears all forecasts.");
        Seek(2.1);
        var behemothName = (string)Read(Read(Model(), "Definition"), "Name");
        manager.SetMechanismEnabled(id, behemothName, false); Seek(2.1);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE33 disabled mechanism clears owner outputs and boundary.");
        manager.SetMechanismEnabled(id, behemothName, true); Seek(2.1);
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE33 replay has no execution leases.");
        stop();
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE33 Stop releases owner outputs.");
        Console.WriteLine("PASS native CE33 Thunderbolt first corpse Finish lock Destroy retention any20197 Finish all clear disable inverse Seek replay isolation owner");

        center = new(83, 0, 563);
        object[] War(object? cast = null, Vector3? origin = null) =>
            [Actor(1, 0x2E75, center, combat: true, isEnemy: true),
                Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 15),
                Actor(20, 0x233C, origin ?? center, cast: cast, subKind: 5)];
        object WarBomb(ulong actorId, Vector3 point, bool status = true)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0x2E76, point, isEnemy: true, subKind: 5))!;
            node["Statuses"] = JsonSerializer.SerializeToNode(status
                ? new object[] { new { Id = 2056u, Param = 0xB9, SourceId = 0UL, ExpiresAt = Start.AddSeconds(30), SlotIndex = 59 } }
                : []);
            return node;
        }
        object WarBoss(ulong target)
        {
            var node = JsonSerializer.SerializeToNode(Actor(1, 0x2E75, center, combat: true, isEnemy: true))!;
            node["TargetId"] = target;
            return node;
        }
        Check((bool)owns.Invoke(null, [920u, 0x2E75u])! && !(bool)owns.Invoke(null, [921u, 0x2E75u])!,
            "CE23 exact territory and source ownership.");
        var hotOrigin = center + Vector3.UnitX * 15;
        load(id, [Header(920), World(0, War(Spell(20668, hotOrigin), hotOrigin), true),
            World(10, []), World(11, [Actor(20, 0x233C, hotOrigin)]), World(12, [])]);
        Seek(.1);
        Check(Shown("bozja/aoe/").Length == 1 && dangers() == 1
            && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Radius") == 24,
            "CE23 actual R24 instead of stale R30 comment.");
        snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback"),
            "CE23 real R24 solver escapes within R19.5 arena without fallback.");
        Seek(10.1); Check(dangers() == 1, "CE23 R24 no TTL.");
        Seek(11.1); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE23 raw watched Finish.");
        Seek(.1); Check(dangers() == 1, "CE23 reverse Seek reconstructs R24.");
        stop();
        Console.WriteLine("PASS native CE23 actual HotTemper24 dual view exact owner arena real solver no fallback raw Finish no TTL reverse Seek");

        load(id, [Header(920), World(0, War(Spell(20659, center)), true),
            World(1, [Actor(100, 0, center + Vector3.UnitX * 5)]),
            World(2, [Actor(20, 0x233C, center)]), World(3, [])]);
        Seek(.1);
        Check(Shown("bozja/tower/").Length > 0 && Shown("bozja/tower/").All(p => (float)Read(Read(p, "Geometry"), "Radius") == 4),
            "CE23 real tower background and foreground are R4.");
        Seek(1.1);
        Check(((string)Read(Script(), "MechanicHintsText")).Contains("Soak the tower")
            && Shown("bozja/tower/").All(p => (float)Read(Read(p, "Geometry"), "Radius") == 4),
            "CE23 five yards outside R4 hints and complete displayed radius.");
        Seek(2.1); Check(Shown("bozja/tower/").Length == 0 && dangers() == 0 && Goals() == 0, "CE23 tower actual Finish clears.");
        stop();
        Console.WriteLine("PASS native CE23 actual R4 open world tower full geometry hints five yard boundary Finish owner");

        load(id, [Header(920), World(0, [.. War(), WarBomb(30, center + Vector3.UnitX * 3)], true),
            World(1, [WarBomb(30, center + Vector3.UnitX * 3, false)]),
            BehemothEffect(2, 20661, center, source: 30), World(3, [])]);
        Seek(.1);
        Check(Items("Casts").Length == 1 && Shown("bozja/stack/").Length == 1 && Goals() == 1
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Touch bomb")
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE23 true NPC slot59 stack goal and text no fabricated Shared.");
        Seek(1.1); Check(Shown("bozja/stack/").Length == 1 && Goals() == 1, "CE23 status loss retains original stack.");
        Seek(2.1); Check(Shown("bozja/stack/").Length == 0 && Goals() == 0, "CE23 complete matching source Effect consumes NPC stack.");
        Seek(.1); Check(Shown("bozja/stack/").Length == 1, "CE23 inverse Seek recreates original NPC stack.");
        stop();
        Console.WriteLine("PASS native CE23 actual NPC true slot59 original stack lose retained complete Effect consumed reverse Seek owner");

        load(id, [Header(920), World(0, War(Spell(20665, center)), true),
            World(1, [Actor(20, 0x233C, center)]),
            World(2, [Actor(21, 0x233C, center, cast: Spell(20664, center, 2))]),
            World(3, [Actor(21, 0x233C, center)]),
            World(4, [Actor(22, 0x233C, center, cast: Spell(20663, center, 4))]),
            World(5, [Actor(22, 0x233C, center)]), World(6, [])]);
        Seek(.1);
        Check(Read(Shown("bozja/aoe/").Single(), "Geometry").GetType().Name == "DonutGeometry"
            && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "InnerRadius") == 11, "CE23 initial11-18 donut.");
        Seek(1.1); Check((float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "InnerRadius") == 5
            && (DateTime)Read(Items("PublishedCountdowns").Single(), "Activation") == Start.AddSeconds(5), "CE23 real Finish callback+4 stage1.");
        Seek(3.1); Check(Read(Shown("bozja/aoe/").Single(), "Geometry").GetType().Name == "CircleGeometry"
            && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Radius") == 6, "CE23 third circle.");
        Seek(5.1); Check(Shown("bozja/aoe/").Length == 0 && dangers() == 0, "CE23 third Finish clears sequence.");
        stop();
        Console.WriteLine("PASS native CE23 actual Concentric ordered stage shapes callback plus4 clocks other caster Finish complete clear");

        var hotActors = Enumerable.Range(0, 9).Select(i => Actor((ulong)(20 + i), 0x233C,
            center + Vector3.UnitX * i, cast: Spell(20667, center + Vector3.UnitX * i), subKind: 5)).ToArray();
        load(id, [Header(920), World(0, [.. War().Take(3), .. hotActors,
                Actor(50, 0x233C, center, cast: Spell(20666, center), subKind: 5)], true),
            World(1, [Actor(40, 0x2E74, center, isEnemy: true)]), World(2, [])]);
        Seek(.1);
        Check(Shown("bozja/aoe/").Length == 0 && dangers() == 0 && Goals() == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null
            && ((string)Read(Script(), "MechanicHintsText")).Contains("缺少Mater")
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Raidwide") && Shown("bozja/arena").Length == 1
            && (ulong)Read(Script(), "PlannedTargetId") == 0, "CE23 missing Mater true failure withdraws navigation and target lease.");
        Seek(1.1); Check(Shown("bozja/aoe/").Length == 8 && dangers() == 8
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null, "CE23 Mater arrival restores eight circles and arena.");
        stop();
        Console.WriteLine("PASS native CE23 actual missing Mater diagnosed all navigation boundary target withdrawal original history next frame recovery");

        load(id, [Header(920), World(0, [WarBoss(101), Actor(100, 0, center), Actor(101, 0, center - Vector3.UnitX * 5)], true),
            World(1, [WarBoss(100)]), BehemothEffect(2, 21255, center, source: 1), World(3, [])]);
        Seek(.1); Check(Shown("bozja/cleave/").Length == 1 && dangers() == 1
            && (bool)Read(Shown("bozja/cleave/").Single(), "OutlineOnly"), "CE23 permanent R8 foreground outline without Start.");
        Seek(1.1); Check(Shown("bozja/cleave/").Length == 1 && dangers() == 1, "CE23 current target own cone avoidance.");
        Seek(2.1); Check(Shown("bozja/cleave/").Length == 1 && dangers() == 1
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE23 actual autoattack Effect does not clear Cleave or invent damage.");
        var warName = (string)Read(Read(Model(), "Definition"), "Name");
        manager.SetMechanismEnabled(id, warName, false); Seek(2.1);
        Check(previews().Length == 0 && dangers() == 0, "CE23 disable releases all geometry.");
        manager.SetMechanismEnabled(id, warName, true); Seek(.1);
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE23 replay planning has no execution leases.");
        stop();
        Console.WriteLine("PASS native CE23 actual permanent Cleave current target no Start no Effect clear outline native navigation disable reverse Seek execution isolation");

        foreach (var action in new uint[] { 20666, 20669 })
        {
            load(id, [Header(920), World(0, War(Spell(action, center)), true), World(10, []),
                World(11, [Actor(20, 0x233C, center)]), World(12, [])]);
            Seek(.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE23 unavoidable/timed hint no invented geometry.");
            var count = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count();
            Check(count == (action == 20666 ? 1 : 0), "CE23 Raidwide diagnostic separate from Enrage.");
            Seek(10.1);
            if (action == 20669)
                Check(((string)Read(Script(), "MechanicHintsText")).Contains("Enrage! 0.3s left"), "CE23 timed hint capped residual.");
            else
            {
                var hit = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
                var clockType = Assembly.Load("MasterPlayer.MechanicRuntime").GetType("BossMod.ActorCastInfo")!;
                var clock = Activator.CreateInstance(clockType)!;
                clockType.GetField("TotalTime")!.SetValue(clock, 4f);
                clockType.GetField("ElapsedTime")!.SetValue(clock, 4f);
                var remaining = (float)clockType.GetProperty("NPCRemainingTime")!.GetValue(clock)!;
                Check((DateTime)Read(hit, "Activation") == runtime.CurrentTime.AddSeconds(remaining),
                    "CE23 current NPC clock moves with world after cap and equals actual source float operations.");
            }
            Seek(11.1);
            Check(!((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any()
                && !((string?)Read(Script(), "MechanicHintsText") ?? "").Contains("Enrage!"), "CE23 ordinary watched Finish clears diagnostics.");
            stop();
            Console.WriteLine($"PASS native CE23 {action} actual dynamic NPC diagnostic timed text no invented geometry watched Finish");
        }

        var highWarViewer = JsonSerializer.SerializeToNode(Actor(100, 0, center))!;
        highWarViewer["Hp"] = 30001;
        highWarViewer["MaxHp"] = 40000;
        load(id, [Header(920), World(0, [.. War(Spell(20668, hotOrigin), hotOrigin).Where(a =>
                (ulong)Read(a, "Id") != 101), Actor(101, 0, center + Vector3.UnitX * 50),
                WarBomb(30, center + Vector3.UnitX * 2), WarBomb(31, center + Vector3.UnitX * 4)], true),
            World(1, []), World(2, [highWarViewer]), World(3, [])]);
        Seek(1.1);
        Check(((string)Read(Script(), "MechanicHintsText")).Contains("stack=1/2")
            && dangers() == 0 && Goals() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null
            && Shown("bozja/aoe/").Length == 1 && Shown("bozja/stack/").Length == 2 && Shown("bozja/arena").Length == 1,
            "CE23 source participant exception clears all navigation while preserving complete drawing/history.");
        ((IList)Read(Model(), "fireParticipants")).Clear();
        var warBase = Script().GetType().BaseType!;
        warBase.GetMethod("OnCurrentPlayerChanged", flags)!.Invoke(Script(), [101UL, 100UL]);
        Check(((IList)Read(Model(), "fireParticipants")).Count == 0,
            "CE23 perspective callback must not consume AI before the Framework lifecycle update.");
        warBase.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        Check(Read(Model(), "FireNavigationError") == null, "CE23 first zero-count output succeeds after perspective callback.");
        var warDeadline = (DateTime)Read(Script(), "nextScan");
        warBase.GetMethod("OnUpdate", flags)!.Invoke(Script(), [.01f]);
        Check((DateTime)Read(Script(), "nextScan") == warDeadline
            && ((string)Read(Model(), "FireNavigationError")).Contains("stack=1/2") && dangers() == 0,
            "CE23 next Framework output must recompute despite unchanged scan deadline.");
        Seek(2.1);
        Check(dangers() == 1 && Goals() > 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null
            && !((string)Read(Script(), "MechanicHintsText")).Contains("索引异常"), "CE23 HP strictly above30000 recovers next frame.");
        stop();
        Console.WriteLine("PASS native CE23 actual source participant index failure complete navigation withdrawal drawing retained high HP recovery without fabricated weak goal");
    }
}

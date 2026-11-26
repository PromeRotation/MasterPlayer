using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnor(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "异界废王——汉比";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-390, 0, 230);
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
        object[] Items(string member) => ((IEnumerable)Read(Model(), member)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, Vector3 origin, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, origin, .4f, target))!;
            node["IsSpell"] = true;
            node["TotalTime"] = 4;
            return node;
        }
        object[] Initial(object? cast = null, uint primary = 0x319A) =>
            [Actor(1, primary, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitZ * 20), Actor(20, 0x233C, center, cast: cast, subKind: 5)];
        void Seek(double time)
        {
            controller.Seek(time);
            Check((int)controller.RouterErrorCount == 0, "Zadnor callback: " + controller.StatusMessage);
        }
        var catalog = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!;
        var owns = catalog.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x319Au])! && !(bool)owns.Invoke(null, [920u, 0x319Au])!
            && !(bool)owns.Invoke(null, [975u, 0x2DBDu])! && !(bool)owns.Invoke(null, [975u, 0x319Bu])!,
            "Zadnor exact region/OID ownership excludes south, Soul and pending CEs.");
        var host = assembly.GetType("MasterPlayer.MechanicHost.MechanicHostController")!;
        var bridge = RuntimeHelpers.GetUninitializedObject(host);
        foreach (var (action, kind, a, b) in new (uint, string, float, float)[]
        {
            (23568, "CircleGeometry", 15, 0), (23569, "DonutGeometry", 6, 40),
            (23570, "CircleGeometry", 5, 0), (21475, "RayRectangleGeometry", 12, 30),
            (23579, "FanGeometry", 30, MathF.PI / 2), (24322, "CircleGeometry", 6, 0),
            (23572, "RayRectangleGeometry", 10, 55),
        })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, center)), true),
                CancelHoH(.2, 20, action), World(10, []), World(11, [Actor(20, 0x233C, center, subKind: 5)]), World(12, [])]);
            Seek(.1);
            var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
            Check((bool)Read(Script(), "EncounterActive") && dangers() == 1 && shape.GetType().Name == kind,
                "CE51 cold shape/active " + action);
            Check(kind switch
            {
                "CircleGeometry" => (float)Read(shape, "Radius") == a,
                "DonutGeometry" => (float)Read(shape, "InnerRadius") == a && (float)Read(shape, "OuterRadius") == b,
                "FanGeometry" => (float)Read(shape, "Radius") == a && (float)Read(shape, "AngleRadians") == b,
                _ => (float)Read(shape, "Width") == a && (float)Read(shape, "Length") == b,
            }, "CE51 exact raw shape dimensions " + action);
            var arena = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
            Check(arena.GetType().Name == "CircleGeometry" && (float)Read(arena, "Radius") == 25,
                "CE51 actual R25 circle boundary.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 999, ActionId = (ushort)action })
                && !(bool)host.GetMethod("IsReplaySuppressed", flags)!.Invoke(bridge, null)!,
                "CE51 source-specific suppression leaves other region mechanisms.");
            Seek(10.1);
            Check(dangers() == 1 && Items("Casts").Length == 1, "CE51 cancel/timeout cannot consume raw cast.");
            Seek(11.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE51 true watched Finish.");
            Seek(.1);
            Check(dangers() == 1, "CE51 reverse Seek rebuilds.");
            manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "CE51 disable releases output.");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1);
            stop();
            Console.WriteLine($"PASS native CE51 {action} actual raw geometry circular arena source gate Finish no TTL reverse Seek mechanism owner");
        }
        load(id, [Header(975), World(0, Initial(Spell(23568, center)), true), World(1, [])]);
        Seek(.1);
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
            && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) >= 15,
            "CE51 actual solver escapes R15 within true circle without fallback.");
        stop();
        Console.WriteLine("PASS native CE51 actual SafeZoneSolver R15 escape circular boundary no fallback owner");

        foreach (var action in new uint[] { 23469, 23470 })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, center)), true), World(10, []),
                World(11, [Actor(20, 0x233C, center, subKind: 5)]), World(12, [])]);
            Seek(10.1);
            var damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
            var clockType = Assembly.Load("MasterPlayer.MechanicRuntime").GetType("BossMod.ActorCastInfo")!;
            var clock = Activator.CreateInstance(clockType)!;
            clockType.GetField("TotalTime")!.SetValue(clock, 4f);
            clockType.GetField("ElapsedTime")!.SetValue(clock, 4f);
            Check((DateTime)Read(damage, "Activation") == runtime.CurrentTime.AddSeconds(
                    (float)clockType.GetProperty("NPCRemainingTime")!.GetValue(clock)!)
                && (ulong)Read(damage, "Mask") == (action == 23469 ? 1UL : 3UL)
                && dangers() == 0 && Shown("bozja/aoe/").Length == 0,
                "CE51 unavoidable dynamic source NPC clock/mask diagnostic only.");
            Check(((string)Read(Script(), "MechanicHintsText")) == (action == 23469 ? "Tankbuster" : "Raidwide"),
                "CE51 actual global hint text is displayed without a personal satisfied suffix.");
            Seek(11.1);
            Check(!((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "CE51 diagnostic watched Finish.");
            stop();
            Console.WriteLine($"PASS native CE51 {action} dynamic raw NPC clock target mask global hint watched Finish diagnostic only");
        }

        load(id, [Header(975), World(0, [.. Initial(Spell(23573, center + Vector3.UnitZ * 10)),
            Actor(21, 0x319B, center + Vector3.UnitX * 10, cast: Spell(23573, center + Vector3.UnitX * 10), subKind: 5)], true),
            World(4, []), World(5, [Actor(20, 0x233C, center, subKind: 5)]),
            World(6, [Actor(21, 0x319B, center + Vector3.UnitX * 15, subKind: 5)]), World(7, [])]);
        Seek(4.1);
        Check(Items("Eyes").Length == 2 && Shown("bozja/eye/").Count(p => !((string)Read(p, "Id")).Contains("/arc/")) == 2
            && dangers() == 0 && Read(Script(), "PlannedFacing") != null && (ulong)Read(Script(), "PlannedTargetId") == 0,
            "CE51 two ordinary gaze symbols and facing plan without target override or invented AOE.");
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE51 replay facing plan never executes.");
        Seek(5.1); Check(Items("Eyes").Length == 1, "CE51 one watched Finish cannot clear other eye.");
        Seek(6.1); Check(Items("Eyes").Length == 0, "CE51 second true Finish.");
        Seek(4.1); Check(Items("Eyes").Length == 2, "CE51 gaze reverse Seek.");
        stop();
        Console.WriteLine("PASS native CE51 ordinary gaze multiple locked eyes first caster Finish facing plan replay isolation reverse Seek owner");

        load(id, [Header(975), World(0, Initial(Spell(20146, center), 0x2DBD), true), World(1, [])]);
        Seek(.1);
        Check(!(bool)Read(Script(), "EncounterActive") && dangers() == 0 && previews().Length == 0,
            "CE51 region cannot activate south CE14 despite matching legacy AID.");
        stop();
        Console.WriteLine("PASS native CE51 region world with south primary no model activation no geometry ownership");

        center = new(-480, 0, -690);
        const string shadowsMechanism = "有角凶鸟——阿尔科诺斯特";
        Check((bool)owns.Invoke(null, [975u, 0x31D0u])! && !(bool)owns.Invoke(null, [920u, 0x31D0u])!
            && !(bool)owns.Invoke(null, [975u, 0x31D1u])!, "CE63 exact primary/region ownership.");
        object ShadowSpell(uint action, double at, Vector3 position)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, position, .4f, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 4;
            return node;
        }
        object Orb(ulong actorId, Vector3 point, ushort param, object? cast = null, bool status = true)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0x3239, point, cast: cast, subKind: 5))!;
            node["Statuses"] = JsonSerializer.SerializeToNode(status
                ? new object[] { new { Id = 2234u, Param = param, SourceId = 0UL, ExpiresAt = Start.AddSeconds(60), SlotIndex = 59 } } : []);
            return node;
        }
        object Primary(object? cast = null) => Actor(1, 0x31D0, center, combat: true, cast: cast, isEnemy: true);
        object Shadow(ulong actorId, object? cast, bool reborn = false) => Actor(actorId, 0x31D1,
            center + (reborn ? Vector3.UnitZ * 20 : Vector3.UnitX * ((int)actorId - 7)), cast: cast, subKind: 5);
        object[] PartyActors() => [Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitZ * 20)];
        load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
                Orb(20, center + Vector3.UnitX * 8, 0x1E),
                Orb(21, center + Vector3.UnitZ * 8, 0x49),
                Orb(22, center - Vector3.UnitX * 8, 0x1E)], true),
            World(1, [Orb(22, center - Vector3.UnitX * 8, 0x1E, ShadowSpell(24122, 1, center))]),
            EffectEvent(2, 22, 24122, 63, center), World(2.5, []),
            World(3, [Orb(22, center - Vector3.UnitX * 8, 0x1E)]),
            World(4, [Orb(20, center + Vector3.UnitX * 8, 0x1E, status: false),
                Orb(21, center + Vector3.UnitZ * 8, 0x49, status: false)]), World(40, [])]);
        Seek(.1);
        Check(Items("Casts").Length == 3 && Shown("bozja/aoe/").Length == 2 && dangers() == 2,
            "CE63 Stormcall three raw predictions publish first two.");
        var projections = Items("Casts").Select(c => (Vector3)Read(c, "Origin")).ToArray();
        Check(projections.Contains(center + Vector3.UnitX * 30) && projections.Contains(center + Vector3.UnitZ * 30)
            && projections.Contains(center - Vector3.UnitX * 30), "CE63 exact unquantized radial projections.");
        Check(Shown("bozja/aoe/").All(p => (float)Read(Read(p, "Geometry"), "Radius") == 35),
            "CE63 source explosion R35.");
        Seek(1.1);
        var corrected = Items("Casts").Single(c => (ulong)Read(Read(Read(c, "Source"), "Actor"), "Id") == 22);
        Check((Vector3)Read(corrected, "Origin") == center && Items("Casts").Length == 3,
            "CE63 actual cast repairs first existing prediction without append.");
        Seek(2.6); Check(Items("Casts").Length == 3, "CE63 Effect cannot consume Stormcall.");
        Seek(3.1); Check(Items("Casts").Length == 2 && dangers() == 2, "CE63 true Finish promotes hidden item.");
        Seek(40); Check(Items("Casts").Length == 2 && dangers() == 2, "CE63 status loss and TTL do not consume.");
        Seek(.1); Check(Items("Casts").Length == 3, "CE63 Stormcall reverse Seek.");
        stop();
        Console.WriteLine("PASS native CE63 Stormcall true slot59 exact projection correction Effect Finish promotion no TTL reverse Seek");
        load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
            Orb(20, new(-498.1258544921875f, 0, -707.086181640625f), 0x1E)], true), World(1, [])]);
        Seek(.1);
        Check((Vector3)Read(Items("Casts").Single(), "Origin") == new Vector3(-501.830047607421875f, 0, -710.577880859375f),
            "CE63 actual DLL reciprocal multiplication preserves source ULP.");
        dynamic edgeGeometry = Read(Shown("bozja/aoe/").Single(), "Geometry");
        Check(!(bool)edgeGeometry.Contains(new Vector3(-466.83001708984375f, 0, -710.577880859375f), 0f),
            "CE63 actual DLL R35 edge stays outside.");
        stop();
        Console.WriteLine("PASS native CE63 actual reciprocal multiplication exact ULP R35 boundary no relaxed tolerance");

        foreach (var action in new uint[] { 24130, 24131, 24132 })
        {
            object[] FourShadows() => Enumerable.Range(10, 4).Select(i => Shadow((ulong)i,
                ShadowSpell(24129, 0, center + Vector3.UnitZ * i))).ToArray();
            load(id, [Header(975), World(0, [Primary(ShadowSpell(action, 0, center)), .. PartyActors(), .. FourShadows()], true),
                World(1, [Primary()]), World(2, [Primary(ShadowSpell(24131, 2, center))]),
                World(3, [], removed: [10]), World(4, [Shadow(10, null, true)]),
                World(5, [Primary()]), World(6, [Primary(ShadowSpell(24132, 6, center))]),
                World(7, [Shadow(10, ShadowSpell(24133, 7, center), true)]),
                World(8, [Shadow(10, null, true)]), World(9, [Primary()]),
                World(10, [Primary(ShadowSpell(24130, 10, center))]), World(40, [])]);
            Seek(.1);
            var geometry = Read(Shown("bozja/aoe/").Single(), "Geometry");
            Check(Items("Casts").Length == 5 && dangers() == 1 && (action switch
            {
                24130 => geometry.GetType().Name == "DonutGeometry" && (float)Read(geometry, "InnerRadius") == 8 && (float)Read(geometry, "OuterRadius") == 25,
                24131 => geometry.GetType().Name == "FanGeometry" && (float)Read(geometry, "Radius") == 36
                    && (float)Read(geometry, "AngleRadians") == 2 * (65f * (MathF.PI / 180f)),
                _ => geometry.GetType().Name == "CircleGeometry" && (float)Read(geometry, "Radius") == 20,
            }), "CE63 exactly five hides four snapshots; actual source shape dimensions " + action);
            Check(((object)runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary).GetType().Name == "WorldPolygonGeometry",
                "CE63 true polygon arena.");
            if (action == 24132)
            {
                snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) >= 20,
                    "CE63 actual solver escapes visible R20 inside polygon while four future copies stay hidden.");
                Console.WriteLine("PASS native CE63 actual SafeZoneSolver visible R20 polygon escape hidden four excluded no fallback");
            }
            Seek(1.1); Check(Items("Casts").Length == 4 && Shown("bozja/aoe/").Length == 4 && dangers() == 4,
                "CE63 boss Finish publishes four shadows.");
            Seek(2.1); Check(Items("Casts").Length == 9 && Shown("bozja/aoe/").Length == 9 && dangers() == 9,
                "CE63 four retained records initialize again without once-only latch.");
            Seek(6.1);
            var oldCopies = Items("Casts").Where(c => (ulong)Read(Read(Read(c, "Source"), "Actor"), "Id") == 10).ToArray();
            Check(Items("Casts").Length == 13 && oldCopies.Length == 3
                && oldCopies.All(c => ((Vector3)Read(c, "Origin")).Z < center.Z + 1),
                "CE63 destroyed original Actor pose cannot rebind same-ID generation.");
            Seek(10.1); Check(Items("Casts").Length == 12 && dangers() == 12,
                "CE63 real shadow Finish clears records then subsequent boss adds one only.");
            Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
                && (ulong)Read(Read(Script(), "Hints"), "target") == 0
                && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE63 replay no execution requests.");
            Seek(.1); Check(Items("Casts").Length == 5 && dangers() == 1, "CE63 reverse Seek rebuilds hidden state.");
            manager.SetMechanismEnabled(id, shadowsMechanism, false); Seek(.1);
            Check(dangers() == 0 && previews().Length == 0
                && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "CE63 disable clears owner outputs.");
            manager.SetMechanismEnabled(id, shadowsMechanism, true); Seek(.1);
            stop(); Check(dangers() == 0 && previews().Length == 0, "CE63 stop releases owner outputs.");
            Console.WriteLine($"PASS native CE63 {action} actual hidden5 visible4 repeated9 original generation real Finish reverse Seek owner replay isolation");
        }
        foreach (var action in new uint[] { 24123, 24124, 24127, 24135 })
        {
            load(id, [Header(975), World(0, [Primary(), .. PartyActors(),
                Actor(20, 0x233C, center, cast: ShadowSpell(action, 0, center), subKind: 5)], true),
                World(10, []), World(11, [Actor(20, 0x233C, center, subKind: 5)]), World(12, [])]);
            Seek(10.1);
            if (action is 24123 or 24124)
            {
                var damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
                Check((ulong)Read(damage, "Mask") == (action == 24123 ? 1UL : 3UL) && dangers() == 0
                    && (string)Read(Script(), "MechanicHintsText") == (action == 24123 ? "Tankbuster" : "Raidwide"),
                    "CE63 correct diagnostic mask/global text and no invented geometry.");
            }
            else if (action == 24127)
                Check(dangers() == 1 && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Radius") == 3,
                    "CE63 Fantod actual R3.");
            else Check(dangers() == 0 && !Items("Casts").Any(), "CE63 current shadow spell cannot fabricate absent history.");
            Seek(11.1); Check(dangers() == 0 && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
                "CE63 watched Finish clears standard outputs.");
            stop();
            Console.WriteLine($"PASS native CE63 {action} actual standard diagnostic shape or missing history no fabrication true Finish owner");
        }
    }
}

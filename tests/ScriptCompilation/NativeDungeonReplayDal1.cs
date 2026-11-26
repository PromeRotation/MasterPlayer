using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDal1(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "铁胆狱火 萨托瓦尔";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(631, 0, 157);
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
        object[] Shown(string key = "bozja/aoe/") => previews().Where(p => ((string)Read(p, "Id")!).Contains(key)).ToArray();
        object[] Items(string key) => ((IEnumerable)Read(Model(), key)!).Cast<object>().ToArray();
        object Spell(uint action, double at = 0, double total = 5, Vector3? p = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, p ?? center, 0, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = total;
            return node;
        }
        object Boss() => Actor(1, 0x3250, center, combat: true, subKind: 5, isEnemy: true);
        object Helper(ulong actorId, object? cast = null, Vector3? p = null, uint oid = 0x233C)
            => Actor(actorId, oid, p ?? center, cast: cast);
        object Player(Vector3? p = null, ulong actorId = 100) => Actor(actorId, 0, p ?? center);
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, at, actors, full, removed, party: [
                new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Event(double at, uint action, Vector3? p = null) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                TargetPosition = Pose(p ?? center), GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DAL1 actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null, "DAL1 owner released");
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x3250u])! && !(bool)owns.Invoke(null, [920u, 0x3250u])!
            && !(bool)owns.Invoke(null, [975u, 0x3251u])!, "DAL1 precise ownership");
        var cases = new (uint Action, string Shape, float Width, float Length)[]
        {
            (24176, "rect", 20, 20), (24177, "rect", 20, 20), (24174, "rect", 20, 20), (24175, "rect", 20, 20),
            (24181, "rect", 10, 44), (24187, "cross", 18, 40), (24179, "rect", 5, 55),
            (24201, "rect", 8, 50), (24202, "circle", 10, 0), (24204, "cone", 40, 0), (24203, "cone", 40, 0),
        };
        foreach (var test in cases)
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Helper(20, Spell(test.Action))], true),
                Event(1, test.Action), World(2, [Helper(20)]), World(3, [])]);
            Seek(.1);
            Check(Shown().Length == 1 && dangers() > 0 && Read(Snapshot(), "ArenaBoundary") != null, "DAL1 actual geometry published " + test.Action);
            var geometry = Read(Shown()[0], "Geometry")!;
            var contains = geometry.GetType().GetMethod("Contains", [typeof(Vector3), typeof(float)])!;
            for (var x = -18f; x <= 18; x += .731f)
                for (var z = -18f; z <= 18; z += 1.137f)
                {
                    var expected = test.Shape switch
                    {
                        "rect" => z >= 0 && z <= test.Length && MathF.Abs(x) <= test.Width / 2,
                        "circle" => x * x + z * z <= test.Width * test.Width,
                        "cross" => MathF.Abs(x) <= test.Length && MathF.Abs(z) <= test.Length
                            && (MathF.Abs(x) <= test.Width / 2 || MathF.Abs(z) <= test.Width / 2),
                        _ => x * x + z * z <= test.Width * test.Width && z >= 0,
                    };
                    Check((bool)contains.Invoke(geometry, [center + new Vector3(x, 0, z), 0f])! == expected, "DAL1 actual shape " + test.Action);
                }
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)test.Action }), "DAL1 suppression " + test.Action);
            Seek(1.1); Check(Shown().Length == 1, "DAL1 Effect does not Finish");
            Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DAL1 real Finish");
            Seek(.1); Check(Shown().Length == 1, "DAL1 reverse Seek");
            manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "DAL1 mechanism disabled");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1);
            Release();
            Console.WriteLine($"PASS native DAL1 actual geometry action={test.Action} suppression Effect Finish reverse Seek owner");
        }

        load(id, [Header(975), World(0, [Boss(), Player(),
            Helper(20, Spell(24174, total: 7)), Helper(21, Spell(24175, total: 3)),
            Helper(22, Spell(24176, total: 5)), Helper(23, Spell(24177, total: 1))], true),
            World(1, [Helper(23)]), World(2, [Helper(21)]), World(3, [])]);
        Seek(.1);
        Check(Items("Casts").Length == 4 && Shown().Length == 2, "DAL1 actual4 threshold hidden2");
        var firstClock = (DateTime)Read(Items("Casts")[0], "Activation")!;
        Check(firstClock == Start.AddSeconds(1.3f), "DAL1 threshold sort first clock");
        Seek(1.1); Check(Items("Casts").Length == 3 && Shown().Length == 2, "DAL1 first promotion");
        Seek(2.1); Check(Items("Casts").Length == 2 && Shown().Length == 2, "DAL1 second promotion");
        Seek(.1); Check(Items("Casts").Length == 4, "DAL1 sort rebuild reverse Seek");
        Release();
        Console.WriteLine("PASS native DAL1 actual TimeEruption four clock sort hidden2 each Finish promotes original queue");

        var tower = center + Vector3.UnitX * 10;
        load(id, [Header(975), World(0, [Boss(), Player(), Helper(20), Helper(30, p: tower, oid: 0x1EB214),
            Helper(31, p: tower, oid: 0x1EB214), Helper(32, oid: 0x1EB213)], true),
            Event(1, 24184, tower), World(2, [Helper(20, p: tower)], removed: [30, 31]),
            Event(3, 24183), Event(4, 24184), World(30, [])]);
        Seek(.1);
        Check(Items("Towers").Length == 2 && Shown("bozja/tower/").Length == 2, "DAL1 cold only correct OID two outlines");
        Check(Items("Towers").All(t => (DateTime)Read(t, "Activation")! == Start.AddSeconds(8.7d)), "DAL1 Created current+8.7");
        Check(((string?)Read(Script(), "MechanicHintsText"))?.Contains("Soak the tower!") == true, "DAL1 actual empty tower hint");
        var snapshot = Snapshot();
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic solverPlayer = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, solverPlayer, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "DAL1 actual tower solver no fallback");
        Seek(1.1); Check(Items("Towers").Length == 2, "DAL1 target-position Effect far caster ignored");
        Seek(2.1); Check(Items("Towers").Length == 2, "DAL1 visual destroyed retains snapshot");
        Seek(3.1); Check(Items("Towers").Length == 1, "DAL1 first caster-position Effect only head");
        Seek(4.1); Check(Items("Towers").Length == 0 && Shown("bozja/tower/").Length == 0, "DAL1 second effect consumes last");
        Seek(.1); Check(Items("Towers").Length == 2, "DAL1 tower reverse Seek");
        Release();
        Console.WriteLine("PASS native DAL1 actual Created towers R4 first caster spatial Effect ignored target snapshot destroy solver owner");

        foreach (var action in new uint[] { 24189, 24200, 24208, 24207, 24206 })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Player(center + Vector3.UnitX * 15, 101),
                Helper(20, Spell(action))], true), World(1, [Player(center + Vector3.UnitX * 3)]),
                World(2, [Helper(20)]), World(3, [])]);
            Seek(.1);
            if (action is 24207 or 24206)
            {
                var key = action == 24207 ? "bozja/spread/" : "bozja/stack/";
                Check(Shown(key).Length == 1 && ((IEnumerable)Read(Script(), "PredictedDamage")!).Cast<object>().Count() == 0,
                    "DAL1 stack spread no invented damage");
                const float grid = 2000f / 65535f;
                const float inverse = 1f / grid;
                var expectedTarget = new Vector3(((int)MathF.Round((center.X + 3) * inverse) - .5f) * grid,
                    center.Y, ((int)MathF.Round(center.Z * inverse) - .5f) * grid);
                Seek(1.1); Check(Shown(key).Length == 1
                    && (Vector3)Read(Read(Shown(key)[0], "Geometry")!, "Position")! == expectedTarget,
                    "DAL1 moving target outputs");
            }
            else
                Check(((IEnumerable)Read(Script(), "PredictedDamage")!).Cast<object>().Count() == 1
                    && Shown().Length == 0, "DAL1 real dynamic damage diagnostic only");
            Seek(2.1); Check(Items("Casts").Length == 0 && previews().Length == 1
                && ((string)Read(previews()[0], "Id")!).EndsWith("bozja/arena", StringComparison.Ordinal)
                && !((IEnumerable)Read(Script(), "PredictedDamage")!).Cast<object>().Any(),
                "DAL1 target/raw Finish clears " + action + " casts=" + Items("Casts").Length
                    + " previews=" + string.Join(",", previews().Select(p => Read(p, "Id")))
                    + " damage=" + ((IEnumerable)Read(Script(), "PredictedDamage")!).Cast<object>().Count());
            Seek(.1); Release();
            Console.WriteLine($"PASS native DAL1 actual target or dynamic diagnostic action={action} original current target no execution owner");
        }
    }
}

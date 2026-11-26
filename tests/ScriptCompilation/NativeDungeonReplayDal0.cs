using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDal0(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "第四军团魔导巨兵";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(650, 0, -551.07495f);
        var origin = center + new Vector3(2, 0, 1);
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
        object[] Items(object obj, string key) => ((IEnumerable)Read(obj, key)!).Cast<object>().ToArray();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")!).Contains("bozja/aoe/")).ToArray();
        object Spell(uint action, double at = 0, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 5, origin, rotation, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 5;
            return node;
        }
        object Boss(object? cast = null, Vector3? p = null) => Actor(1, 0x326C, p ?? center, cast: cast, combat: true, subKind: 5, isEnemy: true);
        object Helper(object? cast = null, Vector3? p = null) => Actor(20, 0x233C, p ?? center, cast: cast, subKind: 5);
        object Player() => Actor(100, 0, origin + Vector3.UnitZ * 3);
        string World(double at, IEnumerable<object> actors, bool full = false)
            => Frame(975, at, actors, full, party: [new { Slot = 0, EntityId = 100UL }], sourcePlayerId: 100);
        string Effect(double at, uint action, ulong source = 20) => JsonSerializer.Serialize(new
        {
            t = at, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, MainTargetId = 100UL,
                GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) },
        });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DAL0 actual replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(dangers() == 0 && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null
                && Items(Snapshot(), "GoalAreas").Length == 0, "DAL0 owner released");
        }
        foreach (var action in new uint[] { 24225, 24226 })
        {
            var rows = new List<string> { Header(975), World(0, [Boss(Spell(action)), Helper(), Player()], true),
                Effect(.5, 24227, 100), World(1, [Boss(p: center + Vector3.UnitX * 12), Helper()]), World(2, []) };
            for (var i = 0; i < 7; ++i) rows.Add(Effect(3 + i, i == 0 ? action : 24227));
            rows.Add(World(100, []));
            load(id, rows); Seek(.1);
            Check(Shown().Length == 2 && dangers() == 2 && Items(Snapshot(), "GoalAreas").Length == 1,
                "DAL0 actual future/current and R5 goal");
            var published = Items(Model(), "colossusPublished");
            Check(published.Select(a => ((bool)Read(a, "Danger")!, (bool)Read(a, "Risky")!))
                .SequenceEqual(new[] { (false, true), (true, true) }), "DAL0 initial colors and risk");
            Check(published.All(a => (Vector3)Read(Read(a, "Cast")!, "Origin")! == origin), "DAL0 raw spell LocXZ locked");
            var geometry = Read(Shown()[1], "Geometry")!;
            var contains = geometry.GetType().GetMethod("Contains", [typeof(Vector3), typeof(float)])!;
            for (var x = -22f; x <= 22; x += .731f)
                for (var z = -22f; z <= 22; z += 1.137f)
                {
                    var expected = x * x + z * z <= 400 && z >= 0
                        && MathF.Abs(MathF.Atan2(x, z)) <= MathF.PI / 6;
                    Check((bool)contains.Invoke(geometry, [origin + new Vector3(x, 0, z), 0f])! == expected,
                        "DAL0 actual R20 half30 cone");
                }
            dynamic arena = Read(Snapshot(), "ArenaBoundary")!;
            Check((bool)arena.Contains(new Vector3(650, 0, -574), 0f)
                && !(bool)arena.Contains(new Vector3(660, 0, -570), 0f), "DAL0 narrow tail union notch");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 1, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "DAL0 valid source suppression");
            const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
            var snapshot = Snapshot();
            dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
            dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                [origin + Vector3.UnitZ * 3, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")!])!;
            dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
            dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
            dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!,
                "DAL0 actual rotating cone solver no fallback");
            Seek(2.1);
            Check(Shown().Length == 2 && (int)Read(Model(), "ColossusNumCasts")! == 0
                && Items(Model(), "colossusPublished").All(a => (Vector3)Read(Read(a, "Cast")!, "Origin")! == origin),
                "DAL0 Player Effect ignored Finish and moving caster preserve sequence");
            for (var i = 0; i < 6; ++i)
            {
                Seek(3.1 + i);
                Check((int)Read(Model(), "ColossusNumCasts")! == i + 1 && Shown().Length == (i < 4 ? 2 : i == 4 ? 1 : 0),
                    "DAL0 six cross source actual Effects");
                if (i == 4)
                    Check(!(bool)Read(Items(Model(), "colossusPublished").Single(), "Danger")!
                        && (bool)Read(Items(Model(), "colossusPublished").Single(), "Risky")!, "DAL0 last future color still risky");
            }
            Seek(100);
            Check((int)Read(Model(), "ColossusNumCasts")! == 7 && dangers() == 0
                && Items(Snapshot(), "GoalAreas").Length == 0, "DAL0 empty queue increments and clears goal");
            Seek(.1);
            manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "DAL0 mechanism release");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1); Release();
            Console.WriteLine($"PASS native DAL0 actual rotating action={action} zero angle cone union arena R5 solver true Effects Finish ignored colors source gate Seek owner");
        }
        load(id, [Header(975), World(0, [Boss(Spell(24224)), Helper(), Player()], true),
            World(1, [Boss(Spell(24224))]), World(2, [Boss()]), World(3, [])]);
        Seek(.1);
        Check(Shown().Length == 0 && dangers() == 0 && Items(Script(), "PredictedDamage").Length == 1,
            "DAL0 raw dynamic damage diagnostics only");
        Seek(2.1); Check(Items(Script(), "PredictedDamage").Length == 0, "DAL0 raidwide Finish clears");
        Seek(.1); Release();
        Console.WriteLine("PASS native DAL0 actual dynamic raidwide original NPC diagnostic no invented R40 danger Finish owner");
        load(id, [Header(975), World(0, [Boss(Spell(24227)), Helper(), Player()], true), Effect(1, 24227), World(100, [])]);
        Seek(100);
        Check(Shown().Length == 0 && dangers() == 0 && (int)Read(Model(), "ColossusNumCasts")! == 1
            && Items(Snapshot(), "GoalAreas").Length == 0, "DAL0 cold Rest no inferred First");
        Release();
        Console.WriteLine("PASS native DAL0 actual cold Rest has no invented First sequence or near goal");
    }
}

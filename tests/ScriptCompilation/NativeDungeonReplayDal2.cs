using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDal2(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic runtime = runtimeObject; dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(650, 0, -187.5f);
        var island = new Vector3(637.27f, 0, -174.667f);
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
        object[] Shown(string key = "bozja/aoe/") => previews().Where(p => ((string)Read(p, "Id")!).Contains(key)).ToArray();
        int Goals() => Items(Snapshot(), "GoalAreas").Length;
        string Text() => (string?)Read(Script(), "MechanicHintsText") ?? "";
        object Spell(uint action, double at = 0, double total = 5)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, total, center, 0, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = total;
            return node;
        }
        object Boss(object? cast = null) => Actor(1, 0x31AB, center, cast: cast, combat: true, subKind: 5, isEnemy: true);
        object Helper(ulong actorId = 20, object? cast = null) => Actor(actorId, 0x233C, center, cast: cast);
        object Player(Vector3? p = null, ulong actorId = 100, params (uint Id, int Slot, double Expiry)[] statuses)
        {
            var node = JsonSerializer.SerializeToNode(Actor(actorId, 0, p ?? center))!;
            node["Statuses"] = JsonSerializer.SerializeToNode(statuses.Select(s =>
                new { s.Id, SlotIndex = s.Slot, ExpiresAt = Start.AddSeconds(s.Expiry), SourceId = 1UL }).ToArray());
            return node;
        }
        string World(double at, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, at, actors, full, removed, party: [
                new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        string Event(double at, uint value, bool icon = false, ulong target = 100, Vector3? p = null, ulong source = 20)
            => JsonSerializer.Serialize(new
            {
                t = at, kind = icon ? "TargetIcon" : "ActionEffect",
                type = icon ? "PromeRotation.LogSystem.LogSystemTargetIconEvent" : "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = icon ? (object)new { SourceId = source, TargetId = target, MarkerId = value, Timestamp = Start.AddSeconds(at) }
                    : new { SourceId = source, ActionId = value, ActionType = 1, MainTargetId = target,
                        TargetPosition = Pose(p ?? center), GlobalSequence = (uint)(at * 1000), Timestamp = Start.AddSeconds(at) },
            });
        void Seek(double at)
        {
            controller.Seek(at);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DAL2 replay " + controller.StatusMessage + "/" + manager.ErrorMessage);
        }
        void Release()
        {
            stop();
            Check(dangers() == 0 && Goals() == 0 && previews().Length == 0 && Read(Snapshot(), "ArenaBoundary") == null,
                "DAL2 owner release");
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
            Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")!, "DAL2 actual solver no fallback");
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!.GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x31ABu])! && !(bool)owns.Invoke(null, [920u, 0x31ABu])!, "DAL2 exact ownership");
        foreach (var action in new uint[] { 23691, 23694, 23687 })
        {
            load(id, [Header(975), World(0, [Boss(), Player(), Helper(cast: Spell(action))], true),
                Event(1, action), World(2, [Helper()]), World(3, [])]);
            Seek(.1);
            Check(Shown().Length == 1 && dangers() > 0 && Read(Snapshot(), "ArenaBoundary") != null, "DAL2 AOE published " + action);
            var geometry = Read(Shown()[0], "Geometry")!;
            var contains = geometry.GetType().GetMethod("Contains", [typeof(Vector3), typeof(float)])!;
            for (var x = -25f; x <= 25; x += .731f)
                for (var z = -25f; z <= 25; z += 1.137f)
                {
                    var r = action == 23694 ? 12 : 8;
                    var expected = action == 23691 ? x * x + z * z <= 2500
                        && MathF.Abs(MathF.Atan2(x, z)) <= 60f * (MathF.PI / 180f) : x * x + z * z <= r * r;
                    Check((bool)contains.Invoke(geometry, [center + new Vector3(x, 0, z), 0f])! == expected, "DAL2 actual shape " + action);
                }
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }), "DAL2 source gate");
            Solver(center);
            Seek(1.1); Check(Shown().Length == 1, "DAL2 Effect not Finish");
            Seek(2.1); Check(Shown().Length == 0 && dangers() == 0, "DAL2 true Finish");
            Seek(.1);
            var mechanism = (string)Read(Read(Model(), "Definition")!, "Name")!;
            manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
            Check(previews().Length == 0 && dangers() == 0 && Read(Snapshot(), "ArenaBoundary") == null, "DAL2 disabled owner");
            manager.SetMechanismEnabled(id, mechanism, true); Seek(.1);
            Release();
            Console.WriteLine($"PASS native DAL2 actual AOE action={action} geometry solver source gate Effect Finish mechanism reverse Seek owner");
        }
        var morphRows = new List<string> { Header(975),
            World(0, [Boss(Spell(23695)), Player(), Helper()], true),
            World(1, [Boss(), Helper(cast: Spell(23682, 1))]), World(2, [Helper()]) };
        for (var i = 0; i < 7; ++i) morphRows.Add(Event(3 + i, i % 2 == 0 ? 23683u : 24953u, p: island));
        morphRows.Add(Event(10, 23685)); morphRows.Add(Event(11, 23684)); morphRows.Add(World(100, []));
        load(id, morphRows); Seek(.1);
        Check(Shown().Length == 1 && dangers() > 0 && Goals() == 0, "DAL2 ordinary four voidzones sampled");
        var firstClock = (DateTime)Read(Read(Model(), "cuchVoidzone")!, "Activation")!;
        Solver(center);
        Seek(2.1);
        Check(Shown().Length == 1 && (bool)Read(Shown()[0], "Safe")! && dangers() == 0 && Goals() == 0
            && Text().Contains("pudding"), "DAL2 inversion safe display high fill sampling rejection no invented safe targets");
        Seek(7.1);
        Check((int)Read(Model(), "CuchNumJumps")! == 0 && Shown().Length == 2
            && Shown().All(p => !(bool)Read(p, "Safe")!), "DAL2 exactly fifth restores ordinary voidzone plus jump");
        Seek(10.1); Check(Shown().Length == 2, "DAL2 23685 does not clear jump");
        Seek(11.1); Check(Shown().Length == 1, "DAL2 only23684 clears jump");
        Seek(100); Check(Shown().Length == 1 && dangers() > 0
            && (DateTime)Read(Read(Model(), "cuchVoidzone")!, "Activation")! == firstClock, "DAL2 permanent voidzone no TTL or re-clock");
        Seek(2.1); Check((bool)Read(Shown().Single(), "Safe")!, "DAL2 inversion reverse Seek");
        Release();
        Console.WriteLine("PASS native DAL2 actual permanent40 polygon inversion raw sampling no invented Goal five Effects jump TargetXZ only23684 clears solver owner");

        load(id, [Header(975), World(0, [Boss(), Player(), Player(center + Vector3.UnitZ * 15, 101), Helper()], true),
            Event(.5, 40, true), Event(1, 40, true), Event(2, 23692, target: 101),
            World(3, [], removed: [100]), World(4, [Player(center + Vector3.UnitX * 4)]),
            Event(5, 23692), World(100, [])]);
        Seek(1.1);
        Check(Items(Model(), "SeekerBaits").Length == 2 && Shown("bozja/seeker/bait/").Length == 2
            && Items(Script(), "PredictedDamage").Length == 1 && dangers() > 0, "DAL2 actual duplicate own bait R100");
        Seek(2.1); Check(Items(Model(), "SeekerBaits").Length == 1, "DAL2 cross target FIFO");
        Seek(4.1);
        var bait = Items(Model(), "SeekerBaits").Single();
        var actors = (IDictionary)Read(Model(), "Actors")!;
        Check(!ReferenceEquals(Read(bait, "Target"), actors[100UL]), "DAL2 actual sameID original target reference");
        Seek(5.1); Check(Items(Model(), "SeekerBaits").Length == 0, "DAL2 true Effect consumes retained bait");
        Seek(1.1); Release();
        Console.WriteLine("PASS native DAL2 actual duplicate Icon own outline R100 danger FIFO original target generation damage plan Seek owner");

        load(id, [Header(975), World(0, [Boss(), Helper(), Player(center, 100, (2161u, 0, 15), (1422u, 59, 20))], true),
            World(14.5, []), World(17, [Player()]), World(100, [])]);
        Seek(.1);
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility")!;
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>()
            .Where(l => ((string)Read(l, "Id")!).Contains("bozja/cuch/march/")).ToArray();
        Check(Lines().Length == 1 && Text().Contains("Misdirection") && Read(Script(), "PlannedFacing") == null, "DAL2 real status0/59 line diagnostic");
        Seek(14.5); Check(Read(Script(), "PlannedFacing") != null && Goals() == 0 && dangers() == 0, "DAL2 march facing plan only no invented safe zone");
        Seek(17.1); Check(Read(Script(), "PlannedFacing") == null && Lines().Length == 0 && !Text().Contains("Misdirection"), "DAL2 Lose clears outputs");
        Release();
        Console.WriteLine("PASS native DAL2 actual wall march real slots0/59 line lead facing plan misdirection diagnostic hot Lose owner replay isolation");

        foreach (var action in new uint[] { 23695, 23696, 23688, 23689, 24909, 24910, 23698 })
        {
            load(id, [Header(975), World(0, [Boss(Spell(action)), Player(), Helper()], true),
                World(1, [Boss()]), World(2, [])]);
            Seek(.1);
            Check(Items(Script(), "PredictedDamage").Length == (action == 23695 ? 2 : action == 23696 ? 0 : 1), "DAL2 actual duplicate watched damage " + action);
            if (action == 24909) Check(Text().Contains("misdirection"), "DAL2 actual GhastlyAura cast hint");
            Seek(1.1); Check(Items(Script(), "PredictedDamage").Length == 0, "DAL2 damage true Finish");
            Release();
            Console.WriteLine($"PASS native DAL2 actual damage action={action} duplicate watched NPC clocks cast hints no execution owner");
        }
        load(id, [Header(975), World(0, [Boss(), Player(), Helper()], true), World(100, [])]);
        Seek(100);
        Check(Shown().Length == 0 && dangers() == 0 && Goals() == 0 && Items(Model(), "SeekerBaits").Length == 0
            && Items(Script(), "PredictedDamage").Length == 0, "DAL2 cold session cannot recover absent history");
        Release();
        Console.WriteLine("PASS native DAL2 actual cold session no invented prior voidzone morph Icon or jump history");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorCavalry(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "钢铁人马——魔导骑兵大队";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-750, 0, 790);
        object[] roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
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
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        var plugin = assembly.GetType("MasterPlayer.MasterPlayerPlugin")!.GetProperty("Instance")!.GetValue(null)!;
        var visibility = Read(plugin, "AoeVisibility");
        object[] Lines() => ((IEnumerable)visibility.GetType().GetMethod("GetLines")!.Invoke(visibility, null)!).Cast<object>().ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, double at, Vector3? origin = null, ulong target = 100, float rotation = 0)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, origin ?? center, rotation, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 4;
            return node;
        }
        object Helper(object? cast = null, ushort tether = 0, ulong target = 100, Vector3? p = null)
            => Actor(20, 0x233C, p ?? center - Vector3.UnitZ * 10, cast: cast, subKind: 5,
                tethers: tether == 0 ? [] : [new { Id = tether, TargetId = target }]);
        object Primary() => Actor(1, 0x31C7, center, combat: true, isEnemy: true, targetable: false);
        object[] Initial(object? cast = null) =>
            [Primary(), Actor(100, 0, center), Actor(101, 0, center - Vector3.UnitX * 15), Helper(cast)];
        string Effect(double t, uint action, uint sequence, Vector3? position = null, ulong target = 100)
            => JsonSerializer.Serialize(new
            {
                t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
                data = new { SourceId = 1UL, ActionId = action, ActionType = 1, GlobalSequence = sequence,
                    MainTargetId = target, Targets = Array.Empty<ulong>(),
                    TargetPosition = position.HasValue ? Pose(position.Value) : null, Timestamp = Start.AddSeconds(t) },
            });
        string Tether(double t, ushort tether, ulong target = 100)
            => JsonSerializer.Serialize(new { t, kind = "Tether", type = typeof(LogSystemTetherEvent).FullName,
                data = new { SourceId = 20UL, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(t) } });
        void Seek(double time)
        {
            controller.Seek(time);
            Check((int)controller.RouterErrorCount == 0 && (string)manager.ErrorMessage == "",
                "CE53 actual replay callback: " + controller.StatusMessage + "; " + manager.ErrorMessage);
        }
        var owns = assembly.GetType("MasterPlayer.Mechanics.Bozja.BozjaCatalog")!
            .GetMethod("Owns", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((bool)owns.Invoke(null, [975u, 0x31C7u])! && !(bool)owns.Invoke(null, [920u, 0x31C7u])!
            && !(bool)owns.Invoke(null, [975u, 0x31C6u])!, "CE53 exact primary ownership");
        foreach (var action in new uint[] { 23931, 23932, 23936, 24872, 23939, 23948, 23945, 23947 })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, 0, center + Vector3.UnitZ * 10)), true),
                World(10, []), World(11, [Helper()]), World(12, [])]);
            Seek(10.1);
            Check((bool)Read(Model(), "Active"), "CE53 untargetable primary still pulls");
            if (action == 23948)
                Check(dangers() == 0 && ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Count() == 1
                    && (string)Read(Script(), "MechanicHintsText") == "Multi raidwide", "CE53 visual true raidwide diagnostic");
            else
            {
                var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
                Check(dangers() == 1, "CE53 one real cast hazard");
                if (action == 23936)
                    Check((float)Read(shape, "Width") == 6 && (float)Read(shape, "Length") == 20,
                        "CE53 raw actor to Loc true charge not package angle or fixed length");
                if (action == 23939) Check((float)Read(shape, "Width") == 10 && (float)Read(shape, "Length") == 60,
                    "CE53 RideDown source visual width10 not comment width60");
            }
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "CE53 actual source gate");
            if (action == 23945)
            {
                Seek(.1);
                const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
                var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
                dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
                dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
                    [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
                dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!,
                    [6f, 6f, 0f, false, 6f, 6f])!;
                dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
                dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
                var target = (Vector3)Read((object)result, "TargetPosition");
                var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
                Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
                    && !(bool)shape.GetType().GetMethod("Contains")!.Invoke(shape, [target, 0f])!
                    && Vector3.Distance(center, target) <= 25, "CE53 actual solver leaves R15 inside arena no fallback");
            }
            Seek(11.1); Check(dangers() == 0 && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
                "CE53 true watched Finish no TTL");
            Seek(.1); stop();
            Console.WriteLine($"PASS native CE53 {action} exact source geometry or diagnostic untargetable pull source-specific gate Finish no TTL owner Seek");
        }
        foreach (var action in new uint[] { 23934, 23940 })
        {
            load(id, [Header(975), World(0, [Primary(), Actor(100, 0, center - Vector3.UnitX * 10),
                    Actor(101, 0, center), Helper(Spell(action, 0))], true),
                Effect(1, action, 301), World(10, []), World(11, [Helper()]), World(12, [])]);
            Seek(10.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0 && Lines().Length == 1,
                "CE53 only projected displacement line no invented navigation");
            var line = Lines().Single();
            var from = (Vector3)Read(line, "Start"); var to = (Vector3)Read(line, "End");
            Check(Vector3.Distance(from, to) == (action == 23934 ? 35 : 12)
                && (action != 23940 || to.X < from.X), "CE53 first applicable side projects away from center");
            Seek(11.1); Check(Lines().Length == 0, "CE53 watched Finish clears projected output");
            stop();
            Console.WriteLine($"PASS native CE53 {action} actual knockback line only no forbidden zones no Effect TTL clear first applicable side Finish");
        }
        foreach (var action in new uint[] { 23951, 23952 })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, 0)), true),
                World(1, [Actor(100, 0, center, dead: true)]), World(2, [Helper()]), World(3, [])]);
            Seek(.1);
            var key = action == 23951 ? "bozja/spread/" : "bozja/stack/";
            if (action == 23952)
            {
                var nativeActors = (IDictionary)Read(Model(), "Actors");
                var viewer = nativeActors[100UL]!;
                var zones = ((IEnumerable)Model().GetType().GetMethod("NavigationForHost", flags)!.Invoke(Model(), [viewer])!).Cast<object>().ToArray();
                Check(zones.Length == 1 && (float)Read(zones[0], "JoinRadius") == 4
                    && ((Vector3[])Read(zones[0], "Join")).SequenceEqual([center - Vector3.UnitX * 15])
                    && !(bool)Read(zones[0], "Goal"), "CE53 raw own-target R4 buddy complement");
                // The existing bridge's finite sampler drops complements
                // covering more than 65% of the arena. Preserve that adapter.
                Check(dangers() == 0 && !((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Any(),
                    "CE53 own-target buddy complement preserves source bridge sampler drop");
            }
            Check(Shown(key).Length == 1 && (action != 23951 || dangers() == 1),
                "CE53 true marker and source spread hazard");
            Seek(2.1); Check(Items("Casts").Length == 0, "CE53 spread stack targetID first Finish");
            stop();
            Console.WriteLine($"PASS native CE53 {action} actual spread or stack outline targetID Finish source and owner isolation");
        }
        load(id, [Header(975), World(0, Initial(Spell(23952, 0, target: 101)), true),
            World(1, [Helper()]), World(2, [])]);
        Seek(.1);
        var stackGoals = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().ToArray();
        Check(Shown("bozja/stack/").Length == 1 && dangers() == 0 && stackGoals.Length == 1,
            "CE53 other-target stack publishes one R8 join goal");
        Seek(1.1); Check(Shown("bozja/stack/").Length == 0
            && !((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Any(),
            "CE53 other-target watched Finish clears join goal");
        stop();
        Console.WriteLine("PASS native CE53 stack own-target raw buddy complement sampler and other-target actual R8 join goal Finish");
        load(id, [Header(975), World(0, Initial(Spell(23937, 0)), true),
            Effect(.5, 23937, 310, center), Effect(.6, 23937, 311, center),
            World(1, [Actor(30, 0x1EB1D3, center)]), World(1.1, []),
            World(2, [Actor(30, 0x1EB1D3, center, eventState: 7, dead: true)]),
            World(3, [Helper()]), World(10, [])]);
        Seek(.7); Check(Items("JuiceEvents").Length == 2 && dangers() == 3, "CE53 current cast plus duplicate actual Effect pool");
        Seek(1.2); Check(Items("JuiceEvents").Length == 0 && dangers() == 2, "CE53 real object consumes first per Framework");
        Seek(2.1); Check(dangers() == 1, "CE53 state7 pool hides independent cast");
        Seek(3.1); Check(dangers() == 0, "CE53 true Finish clears remaining prediction");
        stop();
        Console.WriteLine("PASS native CE53 FireShot actual target-follow cast duplicate zero-delay native Effects first-per-object Update state7 actor list Finish");

        load(id, [Header(975), World(0, [Primary(), Actor(100, 0, center), Helper()], true),
            World(1, [Actor(200, 0x233C, center, tethers: [new { Id = 57, TargetId = 101UL }]),
                Actor(101, 0, center + Vector3.UnitX * 15)]), World(2, [])]);
        Seek(1.1);
        Check(Items("SteelBaits").Length == 0 && Items("SteelTethers").Length == 0
            && Lines().Length == 0 && dangers() == 0, "CE53 first preScan cannot see target created later in same frame");
        Seek(.1); Seek(1.1);
        Check(Items("SteelBaits").Length == 0, "CE53 inverse Seek preserves skipped initial tether");
        stop();
        var oldRoster = World(0, [.. Initial(), Actor(102, 0, center + Vector3.UnitX * 15)], true);
        roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 102UL }];
        var changedRoster = World(1, [Actor(102, 0, center + Vector3.UnitX * 15,
            tethers: [new { Id = 57, TargetId = 20UL }])]);
        load(id, [Header(975), oldRoster, changedRoster, World(2, [])]);
        Seek(1.1);
        var partyBait = Items("SteelBaits").Single();
        Check((ulong)Read(Read(Read(partyBait, "Source"), "Actor"), "Id") == 102
            && (ulong)Read(Read(Read(partyBait, "Target"), "Actor"), "Id") == 20,
            "CE53 first preScan tether uses old Party before frame roster changes");
        stop();
        roster = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        Console.WriteLine("PASS native CE53 actual preScan later-created target not replayed same-frame old Party tether sides reverse Seek");

        load(id, [Header(975), World(0, Initial(), true),
            Tether(.1, 57), World(.2, [Helper(tether: 57)]),
            Tether(1, 1), World(1.01, [Helper(tether: 1)]),
            World(2, [Actor(100, 0, center + Vector3.UnitZ * 15)]),
            Effect(3, 23943, 320), World(4, [])]);
        Seek(.3);
        Check(Items("SteelBaits").Length == 1 && Shown("bozja/steel/").Length == 1
            && Lines().Length == 1 && !(bool)Read(Lines().Single(), "Safe")
            && dangers() == 2, "CE53 own R100 bait cone plus R15 stretch raw bad line");
        var first = Items("SteelBaits").Single();
        Check((DateTime)Read(first, "Activation") == Start.AddSeconds(.1).AddSeconds(5d),
            "CE53 true queued packet clock plus5");
        Seek(1.1);
        Check(Items("SteelBaits").Length == 1 && Items("SteelTethers").Length == 1
            && (DateTime)Read(Items("SteelBaits").Single(), "Activation") == (DateTime)Read(first, "Activation")
            && (bool)Read(Lines().Single(), "Safe"), "CE53 true op colour replacement keeps initial target clock good line");
        Seek(2.1);
        Check((string)Read(Script(), "MechanicHintsText") == "Tether is stretched!（已满足）",
            "CE53 dynamic current target stretch exact hint");
        Seek(3.1); Check(Items("SteelBaits").Length == 0 && dangers() == 0 && Lines().Length == 0,
            "CE53 arbitrary source native Effect first MainTargetID consumption");
        Seek(.3);
        manager.SetMechanismEnabled(id, mechanism, false); Seek(.3);
        Check(previews().Length == 0 && Lines().Length == 0 && dangers() == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "CE53 mechanism owner cleanup");
        manager.SetMechanismEnabled(id, mechanism, true); Seek(.3);
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0 && !(bool)Read(Read(Script(), "Hints"), "holdActions"),
            "CE53 replay has no control requests");
        stop();
        Check(Lines().Length == 0, "CE53 final stop releases raw tether line owner");
        Console.WriteLine("PASS native CE53 RawSteel actual queued raw tether old-clock good-bad line exact dynamic hints NativeEffect owner reverse Seek replay no execution");
    }
}

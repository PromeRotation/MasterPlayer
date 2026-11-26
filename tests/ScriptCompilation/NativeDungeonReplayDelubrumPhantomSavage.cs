using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDelubrumPhantomSavage(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.delubrum.savage";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(202, 0, -374);
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
        uint State() => (uint)Read(Read(Model(), "PhantomTimeline"), "StateId");
        object[] Items(string name) => ((IEnumerable)Read(Model(), name)).Cast<object>().ToArray();
        object[] Shown() => previews().Where(p => ((string)Read(p, "Id")).Contains("bozja/aoe/")).ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false)
            => Frame(937, t, actors, full, party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }], sourcePlayerId: 100);
        object Spell(uint action, double at, Vector3? location = null)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 10, location ?? center, 0, 100, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 10;
            return node;
        }
        object Boss(object? cast = null, bool targetable = true, bool dead = false)
            => Actor(1, 0x30AE, center, combat: true, isEnemy: true, cast: cast, targetable: targetable, dead: dead);
        object Helper(ulong number, object? cast = null) => Actor(number, 0x233C, center, cast: cast, subKind: 5);
        object Add(bool targetable, bool dead = false) => Actor(30, 0x30B0, center, targetable: targetable, dead: dead, isEnemy: true);
        string Effect(double t, uint action) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = 20UL, ActionId = action, ActionType = 1, GlobalSequence = (uint)(t * 1000),
                MainTargetId = 100UL, Targets = Array.Empty<ulong>(), Timestamp = Start.AddSeconds(t) },
        });
        string Animation(double t, uint state) => JsonSerializer.Serialize(new
        {
            t, kind = "EObjAnimation", type = "PromeRotation.LogSystem.LogSystemEObjAnimationEvent",
            data = new { SourceId = 40UL, Param1 = (ushort)(state >> 16), Param2 = (ushort)state, Timestamp = Start.AddSeconds(t) },
        });
        void Seek(double t)
        {
            controller.Seek(t);
            Check((int)controller.RouterErrorCount == 0 && manager.Running.ContainsKey(id) && (string)manager.ErrorMessage == "",
                "DRS5 actual replay callback: " + controller.StatusMessage + " / " + manager.ErrorMessage);
        }
        object Feature()
        {
            var node = JsonSerializer.SerializeToNode(Actor(40, 0x1EA1A1, center))!;
            node["Kind"] = 7;
            return node;
        }
        object Ability(uint action, double at)
        {
            var node = JsonSerializer.SerializeToNode(Spell(action, at))!;
            node["IsSpell"] = false;
            return node;
        }
        load(id, [Header(937), World(0, [Boss(Ability(999, 0)), Actor(100, 0, center), Helper(20)], true),
            World(1, [Boss(Spell(22461, 1))]), World(2, [Boss(Ability(998, 2))]), World(3, [Boss()]), World(4, [])]);
        foreach (var at in new[] { .1, 1.1, 2.1 })
        {
            Seek(at);
            Check(State() == 1 && dangers() == 0 && Shown().Length == 0,
                "DRS5 actual raw Ability/Spell transitions hold CastEnd " + at);
        }
        Seek(3.1);
        Check(State() == 2 && (int)Read(Model(), "PhantomAgonyCount") == 0,
            "DRS5 actual raw null activates counter without inventing Effect");
        stop();
        Console.WriteLine("PASS native DRS5 Phantom actual raw Ability Start unexpected diagnostic raw nonempty End Spell callback isolation");
        var events = new List<string>
        {
            Header(937), World(0, [Boss(), Actor(100, 0, center), Actor(101, 0, center + Vector3.UnitX * 15),
                Helper(10), Helper(11), Helper(12), Helper(20), Helper(21), Feature()], true),
            Effect(.05, 23350), World(.1, [Boss(Spell(22461, .1))]), World(.2, [Boss()]),
            Effect(.3, 22461), World(.4, []), Effect(.5, 23350), World(.6, []),
            World(1, [Boss(Spell(22450, 1)), Helper(10, Spell(22452, 1)),
                Helper(11, Spell(22452, 1)), Helper(12, Spell(22452, 1)),
                Helper(20, Spell(22454, 1)), Helper(21, Spell(22456, 1))]),
            World(1.1, [Boss()]), World(1.2, [Boss(Spell(22451, 1.2))]), World(1.3, [Boss()]),
            Effect(1.4, 22452), Effect(1.5, 22454), Effect(1.6, 22456),
            World(2, [Helper(10), Helper(11), Helper(12)]),
            World(26, [Helper(20), Helper(21)]),
            World(27, [Boss(Spell(22450, 27)), Helper(10, Spell(22452, 27)), Helper(20, Spell(22454, 27)),
                Helper(21, Spell(22456, 27))]),
            World(28, [Boss()]), World(29, [Boss(Spell(23022, 29))]), World(30, [Boss()]),
            World(54, [Helper(10), Helper(20), Helper(21)]),
            World(55, [Boss(Spell(22464, 55))]), World(56, [Boss()]),
            World(57, [Boss(targetable: false)]), World(58, [Add(false)]), World(59, [Add(true)]),
            World(60, [Boss(Spell(22465, 60), false)]), World(104, []),
            Animation(105, 0x10002), Animation(106, 0x80010), World(1100, []),
            World(1101, [Boss(targetable: false, dead: true)]), World(1102, []),
        };
        load(id, events);
        Seek(.15);
        Check(State() == 1 && dangers() == 0 && Shown().Length == 0,
            $"DRS5 cast end state no Raidwide state={State():X} dangers={dangers()} aoes={Shown().Length}");
        Seek(.45);
        Check(State() == 2 && (int)Read(Model(), "PhantomAgonyCount") == 0
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(), "DRS5 visual does not resolve Agony");
        Seek(.65);
        Check(State() == 0x10000 && (int)Read(Model(), "PhantomAgonyCount") == 1,
            "DRS5 actual Effect consumed before deactivation");
        Console.WriteLine("PASS native DRS5 Phantom actual eight component entry precise helper Effect counter no raidwide output");
        Seek(1.05);
        Check(State() == 0x10001 && Items("Exaflares").Length == 2 && dangers() == 6 && Shown().Length == 6,
            "DRS5 cold three rects and independent circle donut caches");
        Seek(1.7);
        Check(Items("Exaflares").All(l => (int)Read(l, "Left") == 8) && Items("Casts").Length == 3,
            "DRS5 real Effects ignored by all three miasmas");
        Seek(2.1);
        Check(Items("Casts").Length == 2 && (int)Read(Model(), "PhantomMiasmaCount") == 1
            && dangers() == 6 && Shown().Length == 6, "DRS5 three cross-source Finish exact same location consumes one rect");
        // Use the real solver against a single ray rectangle from the actual registry.
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        object snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic solution = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        var target = (Vector3)Read((object)solution, "TargetPosition");
        Check((bool)solution.IsSuccess && !(bool)Read((object)solution, "IsLeastDangerFallback")
            && MathF.Abs(target.X - center.X) > 6 && (bool)((dynamic)Read(snapshot, "ArenaBoundary")).Contains(target, 0f),
            "DRS5 actual solver escapes rectangle and both exaflare shapes within arena");
        Seek(26.1);
        Check(State() == 0x20000 && dangers() == 0 && Shown().Length == 0 && Items("Exaflares").Length == 0,
            "DRS5 first overdue condition removes all four components");
        Console.WriteLine("PASS native DRS5 Phantom actual three miasmas cold activation exact triple Finish independent cache real solver overdue release");
        Seek(27.1);
        Check(State() == 0x20001 && dangers() == 5 && Shown().Length == 5
            && (int)Read(Model(), "PhantomMiasmaCount") == 0, "DRS5 second independent component instances reset");
        Seek(54.1);
        Check(State() == 0x30000 && dangers() == 0 && Shown().Length == 0, "DRS5 second overdue deactivation");
        Seek(58.1);
        Check(State() == 0x30020 && !Items("PhantomActiveBloody").Any(), "DRS5 untargetable adds not active");
        Seek(59.1);
        Check(State() == 0x30030 && Items("PhantomActiveBloody").Length == 1
            && Shown().Length == 0 && dangers() == 0, "DRS5 targetable adds old actor marker not converted into AOE");
        Seek(104.1); Check(State() == 0xFF0000, "DRS5 explicit 43 second Timeout");
        Seek(105.1); Check(dangers() == 2 && Shown().Length == 1, "DRS5 Arena stays active in unknown tail");
        Seek(1100.1);
        Check(State() == 0xFF0000 && Items("PhantomComponents").Contains("BloodyWraith")
            && dangers() == 0 && (Vector3)Read(Model(), "Center") == new Vector3(202, 0, -370), "DRS5 permanent unknown with reduced arena");
        Seek(1101.1);
        Check(!(bool)Read(Script(), "EncounterActive") && dangers() == 0 && previews().Length == 0, "DRS5 death release");
        Console.WriteLine("PASS native DRS5 Phantom actual second wave counter reset adds lifecycle explicit timeout permanent unknown late Arena event death");
        Seek(1.05);
        manager.SetMechanismEnabled(id, "博兹雅幻灵", false);
        Seek(1.06);
        Check(dangers() == 0 && previews().Length == 0
            && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "DRS5 mechanism disable releases boundary and output");
        manager.SetMechanismEnabled(id, "博兹雅幻灵", true);
        Seek(1.05);
        Check(dangers() == 6 && Shown().Length == 6 && Script().GetType().GetMethod("GetTimeline")!.Invoke(Script(), null) != null,
            "DRS5 reverse Seek reconstruction and native timeline");
        Check(!(bool)Read(Read(Script(), "Move"), "_hasControl") && Read(Script(), "PlannedFacing") == null
            && (ulong)Read(Script(), "PlannedTargetId") == 0
            && !(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest"),
            "DRS5 offline owns no GreenMove or PR facing/target request");
        stop();
        Console.WriteLine("PASS native DRS5 Phantom actual mechanism switch owner reverse Seek timeline and replay execution isolation");
    }
}

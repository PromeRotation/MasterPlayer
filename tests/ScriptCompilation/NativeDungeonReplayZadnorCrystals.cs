using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZadnorCrystals(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        var assembly = runtimeObject.GetType().Assembly;
        const string id = "masterplayer.foray.zadnor";
        const string mechanism = "焦土魔蝎——赫德提特";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(-220, 0, 530);
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
        object[] Items(string member) => ((IEnumerable)Read(Model(), member)).Cast<object>().ToArray();
        object[] Shown(string key) => previews().Where(p => ((string)Read(p, "Id")).Contains(key)).ToArray();
        string World(double t, IEnumerable<object> actors, bool full = false, ulong[]? removed = null)
            => Frame(975, t, actors, full, removed, roster, sourcePlayerId: 100);
        object Spell(uint action, double at, Vector3? origin = null, ulong target = 100)
        {
            var node = JsonSerializer.SerializeToNode(Cast(action, 4, origin ?? center, 0, target, at))!;
            node["IsSpell"] = true; node["TotalTime"] = 4;
            return node;
        }
        object[] Initial(object? cast = null) =>
            [Actor(1, 0x31CC, center, combat: true, isEnemy: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 18), Actor(20, 0x233C, center, cast: cast, subKind: 5)];
        object Crystal(ulong actor, uint oid, Vector3 position, bool dead = false)
            => Actor(actor, oid, position, dead: dead, hitboxRadius: 1.6f, targetable: false);
        string Icon(double t, ulong source, uint icon, ulong target = 101) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, MarkerId = icon, TargetId = target, Timestamp = Start.AddSeconds(t) },
        });
        string Effect(double t, uint action, uint sequence, ulong source = 20) => JsonSerializer.Serialize(new
        {
            t, kind = "ActionEffect", type = "MasterPlayer.Scripting.NativeActionEffectEvent",
            data = new { SourceId = source, ActionId = action, ActionType = 1, GlobalSequence = sequence,
                MainTargetId = 101UL, Targets = Array.Empty<ulong>(), TargetPosition = Pose(center), Timestamp = Start.AddSeconds(t) },
        });
        void Seek(double time)
        {
            controller.Seek(time);
            Check((int)controller.RouterErrorCount == 0 && (string)manager.ErrorMessage == "",
                "CE41 actual replay callback: " + controller.StatusMessage + "; " + manager.ErrorMessage);
        }
        foreach (var (action, radius) in new (uint, float)[] { (24072, 4), (24073, 6), (24074, 6), (24080, 8), (24085, 0) })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, 0)), true),
                World(10, []), World(11, [Actor(20, 0x233C, center, subKind: 5)]), World(12, [])]);
            Seek(10.1);
            if (radius != 0)
                Check(dangers() == 1 && (float)Read(Read(Shown("bozja/aoe/").Single(), "Geometry"), "Radius") == radius,
                    "CE41 exact source standard radius " + action);
            else
            {
                var damage = ((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Single();
                Check((ulong)Read(damage, "Mask") == 1 && dangers() == 0
                    && (string)Read(Script(), "MechanicHintsText") == "Tankbuster", "CE41 raw single target diagnostic only.");
            }
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "CE41 source-specific generic suppression.");
            Seek(11.1); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE41 true watched Finish releases standard.");
            stop();
            Console.WriteLine($"PASS native CE41 {action} actual source radius or dynamic raw diagnostic true Finish source gate owner");
        }

        foreach (var (action, oid) in new[] { (24077u, 0x31CDu), (24078u, 0x31CEu) })
        {
            load(id, [Header(975), World(0, [.. Initial(Spell(action, 0)),
                    Crystal(30, oid, center + Vector3.UnitZ),
                    Crystal(31, oid == 0x31CD ? 0x31CEu : 0x31CDu, center + Vector3.UnitX * 8)], true),
                World(1, [Crystal(30, oid, center + Vector3.UnitZ * 15, true)]),
                World(2, [], removed: [30]), World(10, []),
                World(11, [Actor(20, 0x233C, center, subKind: 5)]), World(12, [])]);
            Seek(.1);
            var safe = Shown("bozja/aoe/").Single();
            Check((bool)Read(safe, "Safe") && dangers() > 0 && Shown("/actor").Length == 0,
                $"CE41 LoS real safe fill; old bridge does not consume actor markers: safe={Read(safe, "Safe")} dangers={dangers()}.");
            var shape = Read(safe, "Geometry");
            Check((bool)shape.GetType().GetMethod("Contains")!.Invoke(shape, [center + Vector3.UnitZ * 20, 0f])!
                && !(bool)shape.GetType().GetMethod("Contains")!.Invoke(shape, [center - Vector3.UnitZ * 20, 0f])!,
                "CE41 only matching crystal creates shadow.");
            Seek(1.1);
            var after = Read(Shown("bozja/aoe/").Single(), "Geometry");
            Check((bool)after.GetType().GetMethod("Contains")!.Invoke(after, [center + Vector3.UnitZ * 20, 0f])!,
                "CE41 blocker movement/death cannot change frozen shadow.");
            Seek(2.1); Check(Shown("bozja/aoe/").Length == 1, "CE41 destroyed blocker preserves frozen LoS.");
            Seek(11.1); Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0, "CE41 watched LoS Finish consumes safe FIFO.");
            Seek(.1);
            stop();
            Console.WriteLine($"PASS native CE41 {action} actual live typed crystal R60 frozen LoS safe label dead destroy Finish reverse Seek");
        }
        load(id, [Header(975), World(0, [.. Initial(Spell(24077, 0)),
                Crystal(30, 0x31CD, center + Vector3.UnitZ * 5)], true), World(1, [])]);
        Seek(.1);
        Check(dangers() == 0 && Shown("bozja/aoe/").Length == 1
            && (bool)Read(Shown("bozja/aoe/").Single(), "Safe"),
            "CE41 sparse shadow preserves safe drawing while source bridge finite sampler suppresses danger fill above65percent.");
        stop();
        Console.WriteLine("PASS native CE41 actual sparse LoS greater65percent danger finite sampler omits navigation retains safe preview");

        load(id, [Header(975), World(0, Initial(Spell(24082, 0)), true),
            World(1, [Actor(20, 0x233C, center, cast: Spell(24083, 1), subKind: 5)]),
            World(2, [Actor(20, 0x233C, center, cast: Spell(24084, 2), subKind: 5)]),
            World(3, [Actor(20, 0x233C, center, subKind: 5)]), World(4, [])]);
        foreach (var (time, kind, inner, outer) in new[] { (.1, "CircleGeometry", 0f, 10f),
                     (1.1, "DonutGeometry", 10f, 20f), (2.1, "DonutGeometry", 20f, 30f) })
        {
            Seek(time);
            var shape = Read(Shown("bozja/aoe/").Single(), "Geometry");
            Check(shape.GetType().Name == kind && dangers() == 1
                && (kind == "CircleGeometry" ? (float)Read(shape, "Radius") == outer
                    : (float)Read(shape, "InnerRadius") == inner && (float)Read(shape, "OuterRadius") == outer),
                "CE41 actual cached Earthbreaker stage " + time);
        }
        Seek(3.1); Check(dangers() == 0, "CE41 third true Finish consumes sequence.");
        Seek(.1);
        const string ns = "MasterPlayer.Spatial.SafeZoneReborn.Solver.";
        var snapshot = (object)runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        dynamic solver = Activator.CreateInstance(assembly.GetType(ns + "SafeZoneSolver")!)!;
        dynamic player = Activator.CreateInstance(assembly.GetType(ns + "PlayerState")!,
            [center, 0f, 6f, false, false, 0f, true, (int)Read(snapshot, "Version")])!;
        dynamic capability = Activator.CreateInstance(assembly.GetType(ns + "MovementCapability")!, [6f, 6f, 0f, false, 6f, 6f])!;
        dynamic options = Activator.CreateInstance(assembly.GetType(ns + "SolveOptions")!)!;
        dynamic result = solver.Solve((dynamic)snapshot, player, capability, options, runtime.CurrentTime, null);
        Check((bool)result.IsSuccess && !(bool)Read((object)result, "IsLeastDangerFallback")
            && Vector3.Distance((Vector3)Read((object)result, "TargetPosition"), center) >= 10,
            "CE41 actual solver escapes R10 within real polygon.");
        stop();
        Console.WriteLine("PASS native CE41 actual three cached Earthbreaker stages Finish source circle10 donuts10to20to30 real solver no fallback");

        load(id, [Header(975), World(0, [.. Initial(Spell(24087, 0)),
                Crystal(30, 0x31CE, center + Vector3.UnitX * 10, true),
                Crystal(31, 0x31CD, center + Vector3.UnitZ * 10)], true),
            World(1, [Actor(100, 0, center + Vector3.UnitZ * 3, dead: true)]),
            World(2, [Actor(20, 0x233C, center, subKind: 5)]), World(3, [])]);
        Seek(.1);
        Check(dangers() == 3 && Shown("bozja/spread/").Length == 1 && Shown("/hitbox").Length == 2
            && Shown("/actor").Length == 0 && ((string)Read(Script(), "MechanicHintsText")).Contains("Spread, avoid intersecting cage hitboxes!")
            && !((IEnumerable)Read(Script(), "PredictedDamage")).Cast<object>().Any(),
            "CE41 own spread buddyR6 plus two crystalR6.6, dead crystal outline, custom warning no damage and no unconsumed actor markers.");
        Seek(1.1);
        Check(Shown("bozja/spread/").Length == 0 && Shown("/hitbox").Length == 2
            && ((string)Read(Script(), "MechanicHintsText")).Contains("Spread, avoid intersecting cage hitboxes!"),
            "CE41 dead raw own target retains custom crystal outline/hint but hides active spread.");
        Seek(2.1); Check(Shown("/hitbox").Length == 0 && dangers() == 0, "CE41 true targetID Finish clears raw spread.");
        Seek(.1);
        manager.SetMechanismEnabled(id, mechanism, false); Seek(.1);
        Check(dangers() == 0 && previews().Length == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "CE41 mechanism disable releases display/navigation/marker/boundary.");
        manager.SetMechanismEnabled(id, mechanism, true); Seek(.1); stop();
        Console.WriteLine("PASS native CE41 actual Shardstrike quantized spread raw dead target custom hints dead crystal outlines independent navigation owner");

        load(id, [Header(975), World(0, Initial(), true), Icon(.1, 100, 82, 101), Icon(.2, 101, 79, 100),
            Icon(.3, 100, 79, 101), World(1, [Actor(100, 0, center + Vector3.UnitZ * 10, dead: true)]),
            Effect(2, 24089, 61, 1), Effect(3, 24089, 62), Effect(4, 24089, 63),
            Effect(5, 24089, 64), World(6, [])]);
        Seek(.5);
        var baits = Items("Hailfires");
        Check(baits.Length == 3 && (ulong)Read(Read(Read(baits[0], "Target"), "Actor"), "Id") == 101
            && Shown("/background").Length == 1 && Shown("/outline").Length == 2,
            "CE41 raw icon uses SourceId actor, absolute clock sort, all other fill/own outlines.");
        Seek(1.1);
        Check(Items("Hailfires").Length == 3 && Shown("/outline").Length == 2, "CE41 dead target baits remain.");
        Seek(2.1); Check(Items("Hailfires").Length == 2, "CE41 arbitrary caster Effect pops raw FIFO.");
        Seek(5.1); Check(Items("Hailfires").Length == 0, "CE41 empty Effect no-op.");
        Seek(.5);
        Check(!(bool)Read(Read(Read(Script(), "Hints"), "_facing"), "_hasRequest")
            && (ulong)Read(Read(Script(), "Hints"), "target") == 0
            && !(bool)Read(Read(Script(), "Hints"), "holdActions"), "CE41 replay no execution.");
        stop();
        Console.WriteLine("PASS native CE41 actual icon79to82 source actor raw FIFO absolute sort all baits target death empty Effect reverse Seek replay execution isolation");

        load(id, [Header(975), World(0, Initial(), true), World(.05, [Actor(100, 0, center + Vector3.UnitZ * 10)]),
            Icon(.1, 100, 79, 101), World(1, [], removed: [100]),
            World(2, [Actor(100, 0, center - Vector3.UnitZ * 10)]), World(3, [])]);
        Seek(2.1);
        var original = Read(Items("Hailfires").Single(), "Target");
        Check((bool)Read(original, "Destroyed") && !(bool)Read(Read(original, "Actor"), "Dead")
            && Shown("/outline").Length == 1 && Shown("/background").Length == 0 && dangers() == 1,
            "CE41 new same ID viewer retains old live target reference: own outline by ID and avoid rectangle by reference.");
        stop();
        Console.WriteLine("PASS native CE41 actual destroyed live old target same ID new viewer displays own outline but navigates other bait rectangle");

        foreach (var action in new uint[] { 24083, 24084, 24088 })
        {
            load(id, [Header(975), World(0, Initial(Spell(action, 0)), true), World(10, [])]);
            Seek(.1);
            Check(dangers() == 0 && Shown("bozja/aoe/").Length == 0 && Items("Hailfires").Length == 0,
                "CE41 cold later stage/iconless visual cannot recreate history.");
            stop();
        }
        Console.WriteLine("PASS native CE41 actual missing first Earthbreaker or Hailfire icon history creates no fabricated output");
    }
}

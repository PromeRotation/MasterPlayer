using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void BarApyaahiBody(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.quest.bar-the-passage-p2";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj)!;
        object Script() => Value(manager.Running[guid], "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", flags)!.GetValue(Script())!;
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")).Contains(prefix)).ToArray();
        object[] Model(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object Field(object obj, string name) => obj.GetType().GetField(name)!.GetValue(obj)!;
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        var center = new Vector3(0, 0, 379.27f);
        var origin = center + new Vector3(.12345f, 0, .54321f);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null) =>
            Frame(1246, t, actors, full: full, removed: removed, party: party);
        string Map(double t, uint state) => JsonSerializer.Serialize(new
        {
            t, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 6u, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(t) },
        });
        object Primary(bool dead = false) => Actor(1, 0x466A, center, dead: dead);
        object Tender(bool combat = false, object? cast = null) => Actor(10, 0x4670, center + Vector3.UnitX * 8, combat: combat, cast: cast);
        object Player() => Actor(100, 0, center + Vector3.UnitX * 10);
        object Ally() => Actor(101, 0, center - Vector3.UnitX * 2);
        load(guid, [Header(1246),
            At(0, [Primary(), Tender(), Actor(20, 0x4671, origin), Actor(30, 0x233C, center), Player(), Ally()], true),
            At(1, [Tender(true), Actor(20, 0x4671, origin, cast: Cast(40917, 10, origin, rotation: .41321f, startedAt: 1))]),
            EffectEvent(1.2, 20, 40917, 1),
            At(2, [], removed: [20]),
            At(3, [Actor(30, 0x233C, center, cast: Cast(40918, 4,
                origin + new Vector3(MathF.Sin(.41321f), 0, MathF.Cos(.41321f)) * 5, startedAt: 3))]),
            At(4, [Actor(30, 0x233C, center)]),
            Map(4.5, 0x00020001),
            At(5, [Tender(true, Cast(40909, 11, center, startedAt: 5))]),
            Map(5.5, 0x00008004),
            At(6, [Actor(30, 0x233C, center, cast: Cast(40914, 13, center, startedAt: 6))]),
            At(7, [Actor(30, 0x233C, center)]),
            At(8, [Primary(true)]),
            At(9, [])]);
        controller.Seek(0);
        Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0, "P3 pre-pull output.");
        controller.Seek(1.1);
        Check(Shown("bar-p3/burst/").Length == 4 && Model("BarP3Bursts").Length == 1
            && (int)Field(Model("BarP3Bursts")[0], "Item6") == 9, "P3 initial cached four bursts.");
        Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 20, ActionId = 40917 })
            && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 30, ActionId = 40925 }),
            "P3 source generic dodge boundary.");
        controller.Seek(1.3);
        Check((int)Field(Model("BarP3Bursts")[0], "Item6") == 9, "P3 effect advanced finish-based burst.");
        controller.Seek(2.1);
        Check((int)Field(Model("BarP3Bursts")[0], "Item6") == 8 && Shown("bar-p3/burst/").Length == 4,
            "P3 destroyed source stopped exaflare.");
        controller.Seek(4.1);
        Check((int)Field(Model("BarP3Bursts")[0], "Item6") == 7, "P3 later helper Finish could not continue.");
        controller.Seek(5.1);
        Check((bool)Value(Encounter(), "BarP3SmallArena") && Goals() == 1
            && Shown("bar-p3/knockback/").Length > 0, "P3 shrink/independent first R6 goal/dual view projection.");
        var encounter = Encounter();
        var actualLanding = (Vector3)encounter.GetType().GetMethod("BarP3Landing", flags)!.Invoke(encounter,
            new[] { ((IDictionary)Value(encounter, "Actors"))[100ul] })!;
        Check(Vector3.Distance(actualLanding, center + Vector3.UnitX * 23) < .0001f, "P3 no-wall13 model lost.");
        controller.Seek(6.1);
        Check(!(bool)Value(Encounter(), "BarP3SmallArena") && Shown("bar-p3/arena").Length == 1,
            "P3 restored bounds/ring.");
        controller.Seek(7.1);
        Check(Shown("bar-p3/arena").Length == 1, "P3 cancelled cast cleared independent arena ring.");
        controller.Seek(8.1);
        Check(Value(Encounter(), "Active") == null && previews().Length == 0 && Goals() == 0 && dangers() == 0,
            "P3 primary completion did not release all outputs.");
        controller.Seek(5.1);
        Check(Goals() == 1 && Model("BarP3Knockbacks").Length == 1 && Shown("bar-p3/burst/").Length == 4,
            "P3 reverse Seek failed model reconstruction.");
        var hints = Value(Script(), "Hints");
        Check(!(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld")
            && !((IEnumerable)hints.GetType().GetField("forbiddenTargets", flags)!.GetValue(hints)!).Cast<object>().Any(),
            "P3 replay leaked execution control.");
        stop();
        Console.WriteLine("PASS native Bar the Passage P3 production lazy pull four cached bursts raw destroy helper finish effect isolation radial13 first safe goal map shrink restore ring cancellation default completion reverse Seek and owner cleanup");

        load(guid, [Header(1246),
            At(0, [Primary(), Tender(true), Actor(20, 0x4671, origin), Actor(30, 0x233C, center),
                Actor(40, 0x233C, center), Player(), Ally()], true),
            At(1, [Tender(true, Cast(40910, 10, center, startedAt: 1)),
                Actor(20, 0x4671, origin, cast: Cast(40910, 5, origin, startedAt: 1)),
                Actor(30, 0x233C, center, cast: Cast(40910, 15, center, startedAt: 1))]),
            EffectEvent(1.2, 10, 40910, 1),
            At(2, [Actor(20, 0x4671, origin)]),
            At(3, [Actor(30, 0x233C, center)]),
            At(4, [Tender(true)]), At(5, [])]);
        controller.Seek(1.1);
        var powers = Model("BarP3Power");
        Check(powers.Length == 3 && (bool)Field(powers[0], "Item3") && (bool)Field(powers[1], "Item3")
            && !(bool)Field(powers[2], "Item3") && Shown("bar-p3/power/").Length == 3,
            "P3 power insertion order/strict prefix projection.");
        controller.Seek(1.3);
        Check(Model("BarP3Power").Length == 3, "P3 effect removed finish-only power.");
        controller.Seek(2.1);
        powers = Model("BarP3Power");
        Check(powers.Length == 2 && (ulong)Value(Field(powers[0], "Item2"), "Source") == 20
            && (bool)Field(powers[0], "Item3") && !(bool)Field(powers[1], "Item3"),
            "P3 finish removed matching caster instead of global first or reset sticky risk.");
        controller.Seek(4.1);
        Check(Model("BarP3Power").Length == 0 && Shown("bar-p3/power/").Length == 0,
            "P3 power FIFO final cleanup.");
        stop();
        Console.WriteLine("PASS native Bar the Passage P3 production unsorted power global FIFO sticky risk all previews and effect isolation");

        load(guid, [Header(1246),
            At(0, [Primary(), Tender(true), Actor(30, 0x233C, center), Player(), Ally()], true),
            At(1, [Tender(true, Cast(40930, 5, center, target: 100, startedAt: 1)),
                Actor(30, 0x233C, center, cast: Cast(40932, 7, center, target: 101, startedAt: 1))]),
            At(2, [Actor(101, 0, center - Vector3.UnitX * 4)]),
            At(3, [], removed: [101]),
            At(4, [Actor(101, 0, center + Vector3.UnitX * 20)]),
            At(5, [Actor(30, 0x233C, center)]),
            At(6, [Tender(true)]), At(7, [])]);
        controller.Seek(1.1);
        Check(Shown("quest/stack/").Length > 0 && Shown("quest/spread/").Length > 0
            && Model("PredictedDamage").Length == 0 && Goals() == 0,
            "P3 independent party components fabricated prediction or goals.");
        controller.Seek(2.1);
        var spread = Shown("quest/spread/").Single();
        dynamic spreadGeometry = Value(spread, "Geometry");
        Check((bool)spreadGeometry.Contains(center - Vector3.UnitX * 4, 0f), "P3 spread did not follow original target.");
        controller.Seek(4.1);
        spreadGeometry = Value(Shown("quest/spread/").Single(), "Geometry");
        Check((bool)spreadGeometry.Contains(center - Vector3.UnitX * 4, 0f)
            && !(bool)spreadGeometry.Contains(center + Vector3.UnitX * 20, 0f), "P3 spread rebound same-ID birth.");
        controller.Seek(5.1);
        Check(Shown("quest/spread/").Length == 0 && Shown("quest/stack/").Length > 0,
            "P3 independent spread Finish removed stack.");
        controller.Seek(6.1);
        Check(!Shown("quest/stack/").Any() && dangers() == 0, "P3 party final cleanup.");
        stop();
        Console.WriteLine("PASS native Bar the Passage P3 production independent stack spread original target movement destroy same ID replacement finish and no fabricated damage");

        load(guid, [Header(1246),
            At(0, [Primary(), Tender(true), Actor(30, 0x233C, center), Player(), Ally()], true),
            CastEvent(.1, 10, 40922, 5, center),
            CastEvent(.1, 30, 40927, 5, center),
            At(1, [Tender(true, Cast(40922, 5, center, startedAt: .2)),
                Actor(30, 0x233C, center, cast: Cast(40927, 5, center, startedAt: .2))]),
            At(8, [Actor(100, 0, center + Vector3.UnitX * 10, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(30))]),
            At(9, [Actor(40, 0x233C, center, cast: Cast(40909, 10, center, startedAt: 9))]),
            At(10, [Tender(true), Actor(30, 0x233C, center)]), At(11, [])]);
        controller.Seek(1.1);
        Check(Model("PredictedDamage").All(d => Math.Abs(((DateTime)Value(d, "Activation") - Start.AddSeconds(5.5)).TotalSeconds) < .0001),
            "P3 production first raw elapsed retained packet clock.");
        controller.Seek(7.1);
        var predictions = Model("PredictedDamage");
        Check(predictions.Length > 0 && predictions.All(d => Math.Abs(((DateTime)Value(d, "Activation") - runtime.CurrentTime).TotalSeconds - .3) < .0001)
            && !Shown("cast/").Any(), "P3 production saturated NPC clock or raidwide geometry.");
        controller.Seek(9.1);
        Check(Goals() == 1 && Shown("bar-p3/knockback/").Length == 0,
            "P3 immune player lost independent start goal or received fake knockback preview.");
        controller.Seek(10.1);
        Check(Model("PredictedDamage").Length == 0, "P3 real Finish retained raidwide clock.");
        stop();
        Console.WriteLine("PASS native Bar the Passage P3 production packet raw elapsed correction saturated clock unchanged world immunity and independent safe goal");

        var sourceAssembly = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "MasterPlayer.MechanicRuntime");
        var sourceType = sourceAssembly.GetType("BossMod.Dawntrail.Quest.Role.BarThePassage.Apyaahi.ApyaahiTheArchitect")!;
        int samples = 0;
        load(guid, [Header(1246), At(0, [Primary(), Tender(true), Player(), Ally()], true),
            Map(1, 0x00020001), At(2, [])]);
        for (var phase = 0; phase < 2; ++phase)
        {
            controller.Seek(phase == 0 ? 0 : 1.1);
            var source = sourceType.GetField(phase == 0 ? "StartingArena" : "DefaultArena")!.GetValue(null)!;
            var sourceCenter = source.GetType().GetField("Center")!.GetValue(source)!;
            var sourceCenterType = sourceCenter.GetType();
            float sourceX = (float)sourceCenterType.GetField("X")!.GetValue(sourceCenter)!;
            float sourceZ = (float)sourceCenterType.GetField("Z")!.GetValue(sourceCenter)!;
            var direction = sourceAssembly.GetType("BossMod.WDir")!;
            var contains = source.GetType().GetMethod("Contains", [direction.MakeByRefType()])!;
            dynamic arena = Value(Encounter(), "Arena");
            for (var ix = -30; ix <= 30; ++ix)
                for (var iz = -30; iz <= 30; ++iz)
                {
                    var world = new Vector3(ix + .137f, 0, 379.27f + iz + .271f);
                    var offset = Activator.CreateInstance(direction, world.X - sourceX, world.Z - sourceZ)!;
                    Check((bool)contains.Invoke(source, [offset])! == (bool)arena.Contains(world, 0f),
                        $"P3 arena source oracle phase{phase} at {ix},{iz}.");
                    ++samples;
                }
        }
        stop();
        Console.WriteLine($"PASS native Bar the Passage P3 production source polygon arenas {samples} point oracle initial20 small64 and exact centres");
    }
}

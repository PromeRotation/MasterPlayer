using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleGogo(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string guid = "masterplayer.carnivale.stage01";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var center = new Vector3(100, 0, 100);
        object? Value(object obj, string name) => obj.GetType().GetProperty(name, flags)!.GetValue(obj);
        object Encounter()
        {
            object script = Value(manager.Running[guid], "Script")!;
            return script.GetType().BaseType!.GetField("_encounter", flags)!.GetValue(script)!;
        }
        object State() => Value(Encounter(), "CarnivaleGogo")!;
        object[] Values(string property) => ((IEnumerable)Value(Encounter(), property)!).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")!).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed,
                party: [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x30F7, bool combat = true) => [Actor(1, oid, center, combat: combat),
            Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 5), Actor(101, 0, center + Vector3.UnitX * 3)];

        foreach (var (oid, name) in new[] { (0x30F5u, "黄金头巾——模仿师格格·第一幕"), (0x30F7u, "黄金头巾——模仿师格格·第二幕") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(1, oid, center, combat: true), Actor(3, 0x30F6, center)]),
                At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.01);
            Check(Value(Encounter(), "Active") == null && Shown("carnivale/precombat/arena").Length == 1
                && (Value(Encounter(), "CarnivalePrecombatHint") != null) == (oid == 0x30F5), "Gogo preparation/arena.");
            controller.Seek(.5);
            Check(Shown("carnivale/enemy/").Length == 1, "Gogo primary marker only.");
            var source = Assembly.Load("MasterPlayer.MechanicRuntime");
            var bounds = source.GetType("BossMod.Global.MaskedCarnivale.Layouts")!.GetField("CircleSmall")!.GetValue(null)!;
            var wdir = source.GetType("BossMod.WDir")!;
            dynamic arena = Value(Encounter(), "Arena")!;
            var count = 0;
            for (var x = 74.13f; x < 126; x += .67f)
                for (var z = 74.17f; z < 126; z += .71f)
                {
                    var expected = (bool)bounds.GetType().GetMethod("Contains")!.Invoke(bounds,
                        [Activator.CreateInstance(wdir, [x - 100, z - 100])])!;
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Gogo source arena.");
                    ++count;
                }
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null, "Gogo primary lifecycle.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0, "Gogo disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage31 {oid:X} source arena oracle {count} points preparation only act1 primary marker default lifecycle disabled owner reverse Seek");
        }

        foreach (var (oid, action, hazard, prediction) in new[] { (0x30F5u, 23112u, true, 0), (0x30F5u, 23110u, true, 0),
            (0x30F5u, 23101u, true, 0), (0x30F5u, 23105u, true, 0), (0x30F7u, 23121u, true, 0),
            (0x30F5u, 23097u, false, 0), (0x30F5u, 23113u, false, 0), (0x30F5u, 23114u, false, 0),
            (0x30F5u, 23115u, false, 0), (0x30F5u, 23098u, false, 0), (0x30F5u, 23100u, false, 0),
            (0x30F5u, 23102u, false, 0), (0x30F5u, 23103u, false, 0),
            (0x30F7u, 23128u, false, 2), (0x30F7u, 23130u, false, 2), (0x30F7u, 23124u, false, 2) })
        {
            load(guid, [Header(796), At(0, Initial(oid), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center, rotation: MathF.PI / 2))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == (hazard ? 1 : 0) && Values("PredictedDamage").Length == prediction, "Gogo actual action scope.");
            controller.Seek(8.5);
            Check(dangers() == (hazard ? 1 : 0), "Gogo effect/timeout.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Values("PredictedDamage").Length == 0, "Gogo real Finish.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage31 Sap23101 and23105 Protean50 total30 ray70point5 ordinary raw Finish defense and mimic pure hints FlareHoly Big64 predictions no fabricated helper actions");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(23131, 4, center)),
                Actor(3, 0x233C, center, cast: Cast(20055, 4, center)),
                Actor(4, 0x233C, center, cast: Cast(23123, 4, center)),
                Actor(5, 0x233C, center, cast: Cast(23129, 4, center))]),
            Cancel(1, 2, 23131), Cancel(2, 4, 23123), At(3, [])]);
        controller.Seek(.5);
        Check(dangers() == 4 && Shown("carnivale/sap/").Length == 2 && Shown("carnivale/gogo/meteor/").Length == 2, "Two independent meteor groups.");
        controller.Seek(2.5);
        Check(dangers() == 2 && Shown("carnivale/sap/").Length == 1 && Shown("carnivale/gogo/meteor/").Length == 1, "Per-group Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage31 independent R8 medium plus Charybdis20055 and R16 large meteor groups native danger and preview real source Finish reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(23117, 4, center))]), Cancel(1, 2, 23117),
            At(2, [Actor(3, 0x233C, center + Vector3.UnitX * 3, cast: Cast(23117, 4, center + Vector3.UnitX * 3, startedAt: 2))]),
            At(3, [Actor(4, 0x233C, center, cast: Cast(23118, 4, center, startedAt: 3))]),
            EffectEvent(4, 4, 23118, 1), At(20, []), Cancel(21, 4, 23118), At(22, [])]);
        controller.Seek(2.5);
        var blizzard = Value(State(), "Blizzard")!;
        dynamic danger = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Single();
        Check((ulong)Value(blizzard, "Source")! == 3 && dangers() == 1
            && ((IEnumerable)danger.DangerWindows).Cast<dynamic>().Single().DangerFrom == ((DateTime)Value(blizzard, "FinishAt")!).AddSeconds(5.1d),
            "Global Blizzard overwrite and delay.");
        controller.Seek(20.5);
        Check(Value(State(), "Blizzard") != null && Hints().Contains("Stop everything!"), "No timeout/effect cancellation.");
        controller.Seek(21.5);
        Check(Value(State(), "Blizzard") == null && dangers() == 0 && Hints().Contains("Stop everything!"), "Any Blizzard Finish only delayed circle.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage31 Fire Start globally replaces R8 Blizzard plus5point1 danger clock Fire cancel retains any Blizzard Finish clears only AOE Pyretic64 survives reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(23126, 4, center))]), Cancel(1, 2, 23126),
            TreasureNativeEffect(2, 999, 23127, 1, 0, []), At(20, []),
            TreasureNativeEffect(21, 2, 23127, 2, 0, []), At(22, [])]);
        controller.Seek(20.5);
        var ice = Value(State(), "Icestorm")!;
        Check(Values("PredictedDamage").Length == 2 && Values("PredictedDamage").All(d =>
            (DateTime)Value(d, "Activation")! == ((DateTime)Value(ice, "FinishAt")!).AddSeconds(.9d)) && dangers() == 0,
            "Icestorm delayed prediction survives cancel/expiry/missing source.");
        controller.Seek(21.5);
        Check(Value(State(), "Icestorm") == null && Values("PredictedDamage").Length == 0, "Zero target real effect must clear.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage31 delayed Icestorm raw deadline plus0point9 cancel timeout retention zero-target known native effect clear no ground hazard reverse Seek");

        var raw = center + new Vector3(1.12345f, 0, 2.37654f);
        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(3, 0x30F9, raw, dead: true, targetable: false, eventState: 7)]),
            At(1, [Actor(3, 0x30F9, raw + Vector3.UnitX)]), At(2, [], removed: [3]), At(3, [])]);
        controller.Seek(.5);
        dynamic obstacle = ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Single();
        Check((Vector3)obstacle.Geometry.Position == raw && dangers() == 0 && Shown("carnivale/gogo/maelstrom/").Length == 1, "Raw Maelstrom obstacle all states.");
        controller.Seek(2.5);
        Check(((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count() == 0
            && Shown("carnivale/gogo/maelstrom/").Length == 0, "Destroyed obstacle.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage31 all Maelstrom references raw R8 hard obstacle dead untargetable state7 dynamic position destroy reverse Seek no inferred pull execution");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(23119, 4, center, target: 100))]),
            TreasureNativeEffect(.2, 999, 23119, 1, 100, [100]),
            TreasureNativeEffect(.3, 2, 23119, 2, 100, [100]), Cancel(.6, 2, 23119),
            At(1, [Actor(3, 0x1E9684, center + Vector3.UnitZ * 5, dead: true, targetable: false)]),
            At(2, []), At(3, [Actor(3, 0x1E9684, center, eventState: 7)]), At(4, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleRayCasts").Length == 1 && Values("CarnivaleRayPredictions").Length == 1
            && Shown("carnivale/ray/").Length == 2 && dangers() == 2, "Thunder cast and native paths.");
        controller.Seek(2.5);
        Check(Values("CarnivaleRayPredictions").Length == 0 && dangers() == 1, "Live confirms predicted pool.");
        controller.Seek(3.5);
        Check(dangers() == 0, "State7.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage31 ThunderIII three independent R6 cast effect live paths source gate target identity state7 Finish reverse Seek");
    }
}

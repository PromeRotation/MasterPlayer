using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunCarnivaleSiegfried(object managerObject, object runtimeObject, object controllerObject,
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
        object State() => Value(Encounter(), "CarnivaleSiegfried")!;
        object[] List(string property) => ((IEnumerable)Value(State(), property)!).Cast<object>().ToArray();
        object[] Values(string property) => ((IEnumerable)Value(Encounter(), property)!).Cast<object>().ToArray();
        object[] Shown(string prefix) => previews().Where(a => ((string)Value(a, "Id")!).Contains(prefix)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [center])!).Cast<string>().ToArray();
        string At(double t, object[] actors, bool full = false, ulong[]? removed = null)
            => Frame(796, t, actors, full: full, removed: removed, party: [new { Slot = 0, EntityId = 100ul }]);
        string Cancel(double t, ulong source, uint action) => JsonSerializer.Serialize(new { t, kind = "CancelCast",
            type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
            data = new { SourceId = source, ActionId = action, Timestamp = Start.AddSeconds(t) } });
        object[] Initial(uint oid = 0x2C6C, bool combat = true) => [Actor(1, oid, center, combat: combat),
            Actor(2, 0x233C, center), Actor(100, 0, center + Vector3.UnitZ * 5)];

        foreach (var (oid, name) in new[] { (0x2C67u, "世界第一剑士——齐格弗里德·第一幕"),
            (0x2C6Au, "世界第一剑士——齐格弗里德·第二幕"), (0x2C6Cu, "世界第一剑士——齐格弗里德·第三幕") })
        {
            load(guid, [Header(796), At(0, Initial(oid, false), true),
                At(.1, [Actor(1, oid, center, combat: true), Actor(10, 0x2C6E, center),
                    Actor(11, 0x2C6D, center), Actor(12, 0x2C6F, center)]),
                At(1, [Actor(1, oid, center, dead: true)]), At(2, [])]);
            controller.Seek(.01);
            Check(Value(Encounter(), "Active") == null && Shown("carnivale/precombat/arena").Length == 1
                && (Value(Encounter(), "CarnivalePrecombatHint") != null) == (oid == 0x2C67), "Siegfried precombat arena/hint.");
            controller.Seek(.5);
            Check(Shown("carnivale/enemy/").Length == (oid == 0x2C6C ? 4 : 1), "Siegfried markers.");
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
                    Check((bool)arena.Contains(new Vector3(x, 0, z), 0f) == expected, "Siegfried source arena.");
                    ++count;
                }
            controller.Seek(1.5);
            Check(Value(Encounter(), "Active") == null, "Siegfried primary lifecycle.");
            controller.Seek(.5);
            manager.SetMechanismEnabled(guid, name, false);
            controller.Seek(.6);
            Check(previews().Length == 0 && dangers() == 0, "Siegfried disabled owner.");
            manager.SetMechanismEnabled(guid, name, true);
            stop();
            Console.WriteLine($"PASS native Carnivale Stage30 {oid:X} source arena oracle {count} points preparation arena all acts clone markers primary lifecycle disabled owner reverse Seek");
        }

        var raw = center + new Vector3(1.12345f, 0, 2.37654f);
        load(guid, [Header(796), At(0, Initial(0x2C67), true), At(.1, [Actor(3, 0x2C68, raw)]),
            At(1, [Actor(1, 0x2C67, center + Vector3.UnitX * 4, combat: true), Actor(3, 0x2C68, center, dead: true)]),
            At(2, [], removed: [3]), At(3, [Actor(3, 0x2C68, center + Vector3.UnitX * 10)]),
            At(4, [Actor(2, 0x233C, center, cast: Cast(18847, 4, center, startedAt: 4))]),
            EffectEvent(5, 2, 18847, 1), Cancel(6, 2, 18847),
            At(7, [Actor(3, 0x2C68, center, cast: Cast(18888, 4, center, startedAt: 7))]),
            Cancel(8, 3, 18888), At(9, [])]);
        controller.Seek(.5);
        var birth = List("Bombs").Single();
        Check(List("Bombs").Length == 1 && (float)Value(birth, "Radius")! == 8
            && (Vector3)Value(birth, "Position")! != raw && dangers() == 1
            && (DateTime)Value(birth, "Activation")! == ((DateTime)Value(birth, "CreatedAt")!).AddSeconds(8.4d)
            && (Vector3)Value(Value(State(), "RubberBullet")!, "Origin")! == center
            && Shown("carnivale/siegfried/rubber/kb").Length > 0, "Bomb creation model/native projections.");
        controller.Seek(2.5);
        Check(List("Bombs").Length == 1 && Value(State(), "RubberBullet") != null, "Dead/Destroy must retain predictions.");
        controller.Seek(5.5);
        Check(List("Bombs").Length == 2 && Value(State(), "RubberBullet") != null
            && (Vector3)Value(Value(State(), "RubberBullet")!, "Origin")! == center + Vector3.UnitX * 4, "New generation and non-clearing effect.");
        controller.Seek(6.5);
        Check(Value(State(), "RubberBullet") == null && List("Bombs").Length == 2, "18847 Finish only KB.");
        controller.Seek(8.5);
        Check(List("Bombs").Length == 0 && dangers() == 0, "18888 Finish all Bombs.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage30 Bomb birth identity position clock8point4 Rubber20 locked primary6point3 no timeout dead Destroy persistent new generation Finish scopes no KB navigation reverse Seek");

        load(guid, [Header(796), At(0, Initial(0x2C6A), true),
            At(.1, [Actor(3, 0x2CC6, raw), Actor(4, 0x2CC6, raw + Vector3.UnitX * 6)]),
            At(1, [], removed: [3]), TreasureNativeEffect(2, 999, 18986, 1, 100, [100]),
            At(20, []), TreasureNativeEffect(21, 2, 18986, 2, 100, [100]), At(22, [])]);
        controller.Seek(20.5);
        Check(List("Boulders").Length == 2 && List("Boulders").All(b => (float)Value(b, "Radius")! == 10)
            && dangers() == 2, "Boulder persistence/source gate.");
        controller.Seek(21.5);
        Check(List("Boulders").Length == 0 && dangers() == 0, "Native known source clears all Boulders.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage30 Boulder R10 birth clock retained after Destroy expiry unknown source effect only known native sphere effect clears all reverse Seek");

        load(guid, [Header(796), At(0, Initial(), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(18841, 10, center)),
                Actor(3, 0x233C, center, cast: Cast(18897, 20, center)),
                Actor(4, 0x233C, center, cast: Cast(18841, 10, center)),
                Actor(5, 0x233C, center, cast: Cast(18836, 4, center)),
                Actor(6, 0x233C, center, cast: Cast(18838, 4, center, rotation: MathF.PI / 2))]),
            Cancel(1, 3, 18897), At(2, [])]);
        controller.Seek(.5);
        Check(List("Sparksteels").Length == 3 && Shown("carnivale/siegfried/sparksteel/").Length == 1
            && Shown("carnivale/siegfried/hyperdrive/").Length == 1
            && Shown("carnivale/siegfried/torch/").Length == 1 && dangers() == 3, "Three groups strict one second prefix.");
        controller.Seek(1.5);
        Check(Shown("carnivale/siegfried/sparksteel/").Length == 2 && dangers() == 4, "Prefix after middle source Finish.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage30 shared HyperdriveR5 Torch34 total20 SparksteelR8 independent FIFO strict1second prefix matching Finish native previews danger adapter reverse Seek");

        load(guid, [Header(796), At(0, Initial(0x2C6A), true),
            At(.1, [Actor(2, 0x233C, center, cast: Cast(18840, 4, center, target: 100))]),
            TreasureNativeEffect(.2, 999, 18840, 1, 100, [100]),
            TreasureNativeEffect(.3, 2, 18840, 2, 100, [100]),
            TreasureNativeEffect(.4, 2, 18840, 3, 100, [100]), Cancel(.6, 2, 18840),
            At(1, [Actor(3, 0x1E8D9B, center + Vector3.UnitZ * 5, dead: true, targetable: false)]),
            At(2, []), At(3, [Actor(3, 0x1E8D9B, center, eventState: 7)]), At(4, [])]);
        controller.Seek(.5);
        Check(Values("CarnivaleRayCasts").Length == 1 && Values("CarnivaleRayPredictions").Length == 2
            && Shown("carnivale/ray/").Length == 3 && dangers() == 3, "Three paths and source gate.");
        controller.Seek(2.5);
        Check(Values("CarnivaleRayCasts").Length == 0 && Values("CarnivaleRayPredictions").Length == 0
            && Shown("carnivale/ray/live/").Length == 1 && dangers() == 1
            && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count() == 0,
            "Repeated raw match live dead/untargetable danger only.");
        controller.Seek(3.5);
        Check(Shown("carnivale/ray/").Length == 0 && dangers() == 0, "State7.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage30 Sparksteel three paths current target native packet single prediction per event processing clock live repeat match source absent blocked state7 no hard obstacle reverse Seek");

        foreach (var action in new uint[] { 18843, 18844, 19027, 18890, 18846, 18842 })
        {
            var hazard = action is 18843 or 18844 or 19027;
            load(guid, [Header(796), At(0, Initial(0x2C6A), true),
                At(.1, [Actor(2, 0x233C, center, cast: Cast(action, 4, center))]),
                EffectEvent(1, 2, action, 1), At(8, []), Cancel(9, 2, action), At(10, [])]);
            controller.Seek(.5);
            Check(dangers() == (hazard ? 1 : 0), "Swift shapes/pure hints.");
            if (action == 18842) Check(Shown("carnivale/siegfried/swift/kb/").Length > 0, "Swift KB projection.");
            controller.Seek(8.5);
            Check(dangers() == (hazard ? 1 : 0), "Swift Effect/timeout.");
            controller.Seek(9.5);
            Check(dangers() == 0 && Shown("carnivale/siegfried/swift/kb/").Length == 0, "Swift real Finish.");
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage30 ordinary R4 ring8to20 ShatterR5 SwiftAway10 pure MagicDrain Bind hints Finish only no raidwide or extra controls");

        foreach (var action in new uint[] { 18847, 18888 })
        {
            load(guid, [Header(796), At(0, Initial(0x2C67), true),
                At(.1, [Actor(1, 0x2C67, center, combat: true, cast: Cast(action, 4, center))]),
                At(1, [Actor(1, 0x2C67, center + Vector3.UnitX * 2, combat: true),
                    Actor(3, 0x2C68, center)]), At(2, [])]);
            controller.Seek(1.5);
            Check(List("Bombs").Length == 1 && Value(State(), "RubberBullet") != null
                && (Vector3)Value(Value(State(), "RubberBullet")!, "Origin")! == center + Vector3.UnitX * 2,
                "Old Finish before new Bomb must preserve birth state.");
            controller.Seek(.5);
            stop();
        }
        Console.WriteLine("PASS native Carnivale Stage30 same raw frame earlier18847 or18888 Finish followed by new Bomb retains newly created model and locked primary reverse Seek");

        load(guid, [Header(796), At(0, Initial(0x2C67), true),
            At(.1, [Actor(3, 0x2C68, center),
                Actor(1, 0x2C67, center, combat: true, stackedStatus: 2166, statusExpiresAt: Start),
                Actor(100, 0, center + Vector3.UnitZ * 5, stackedStatus: 564, statusExpiresAt: Start)]),
            At(.2, [Actor(100, 0, center + Vector3.UnitZ * 5, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(20))]),
            At(1, [], removed: [100]),
            At(2, [Actor(100, 0, center + Vector3.UnitZ * 5, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(20))]),
            At(3, [Actor(100, 0, center + Vector3.UnitZ * 5)]), At(4, [])]);
        controller.Seek(.15);
        Check(Hints().Any(h => h.Contains("reflect all magic damage")) && Hints().Any(h => h.Contains("bound")),
            "Occupied expired reflection/bind slots pure hints.");
        controller.Seek(.5);
        Check(Shown("carnivale/siegfried/rubber/kb").Length == 0 && dangers() == 1, "Immunity must preserve ordinary hazards.");
        controller.Seek(2.5);
        Check(Shown("carnivale/siegfried/rubber/kb").Length == 0 && dangers() == 1, "Full roster retained missing actor reappearance immunity.");
        controller.Seek(3.5);
        Check(Shown("carnivale/siegfried/rubber/kb").Length > 0, "Real immunity loss restores projection.");
        controller.Seek(.5);
        stop();
        Console.WriteLine("PASS native Carnivale Stage30 full roster retained actor Destroy and sameID status reappearance immunity restored actual loss removes immunity ordinary hazard retained reflection Bind pure hints reverse Seek");
    }
}

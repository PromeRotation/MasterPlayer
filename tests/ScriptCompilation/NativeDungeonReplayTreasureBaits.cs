using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureBaits(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [new Vector3(100, 0, 100)])!).Cast<string>().ToArray();
        int Obstacles() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).HardObstacles).Cast<object>().Count();
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        const string id = "masterplayer.treasure.shifting-altars";
        foreach (var (oid, action, live, radius, delay) in new[]
        {
            (0x2539u, 13396u, 0x1E8D9Cu, 6f, .9f),
            (0x253Au, 13219u, 0x1EA140u, 6f, .7f),
            (0x2534u, 13452u, 0x1EA8BBu, 10f, 1.2f),
        })
        {
            string Icon(double time, ulong source, ulong target) => JsonSerializer.Serialize(new
            {
                t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
                data = new { SourceId = source, TargetId = target, MarkerId = 23, Timestamp = Start.AddSeconds(time) },
            });
            var moved = c + Vector3.UnitZ * 9;
            load(id,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, c, combat: true), Actor(3, 0x2545, c), Actor(2, 0x233C, c),
                    Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 2)], full: true, party: party),
                Icon(.1, 100, 101), Icon(.2, 101, 100),
                Frame(794, 1, [Actor(100, 0, moved, dead: true)], party: party),
                Frame(794, 2, [Actor(100, 0, moved)], party: party),
                Frame(794, 3, [], removed: [101], party: party),
                Frame(794, 3.5, [Actor(101, 0, c + Vector3.UnitX * 20)], party: party),
                Frame(794, 4, [Actor(1, oid, c, dead: true)], party: party),
                Frame(794, 5, [Actor(1, oid, c, combat: true)], party: party),
                Frame(794, 6, [], removed: [1], party: party),
                Frame(794, 7, [Actor(1, oid, c, dead: true)], party: party),
                CastEvent(8.1, 2, action, .3, c), CastEvent(9.1, 2, action, .3, c), CastEvent(10.1, 2, action, .3, c),
                Frame(794, 11, [], party: party),
            ]);
            controller.Seek(.3);
            Check(Find("treasure/bait/").Length == 2 && dangers() == 3
                && (bool)Value(Find("treasure/bait/0/display").Single(), "OutlineOnly")
                && !(bool)Value(Find("treasure/bait/1/display").Single(), "OutlineOnly")
                && (float)Value(Value(Find("treasure/bait/0/display").Single(), "Geometry"), "Radius") == radius,
                "Icon Source/Target, own outline, other fill or independent edge navigation differs.");
            Check((bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })
                && !(bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 100, ActionId = (ushort)action }),
                "Treasure voidzone claimed player cast or failed helper claim.");
            controller.Seek(1.1);
            Check(Find("treasure/bait/").Length == 2
                && (Vector3)Value(Value(Find("treasure/bait/0/display").Single(), "Geometry"), "Position") == moved,
                "Dead target lost moving bait.");
            controller.Seek(3.6);
            Check((Vector3)Value(Value(Find("treasure/bait/1/display").Single(), "Geometry"), "Position") == c + Vector3.UnitX * 2,
                "Recreated target ID moved frozen old circle.");
            controller.Seek(4.1);
            Check(Find("treasure/bait/").Length == 0 && dangers() == 1 && Hints().Any(s => s.StartsWith("Bait away!")),
                "Source death failed base hide or removed derived edge/text.");
            controller.Seek(7.1);
            Check(Find("treasure/bait/").Length == 2 && dangers() == 3,
                "Source destruction/new dead generation changed frozen live source.");
            controller.Seek(8.2);
            Check(Find("treasure/bait/").Length == (oid == 0x2539 ? 0 : 2), "First Start clearing differs.");
            controller.Seek(10.2);
            Check(Find("treasure/bait/").Length == 0 && !Hints().Any(s => s.StartsWith("Bait away!")), "Third real Start did not clear group.");
            controller.Seek(.3);
            Check(Find("treasure/bait/").Length == 2 && dangers() == 3, "Reverse Seek did not rebuild icon order.");
            stop();
            Check(dangers() == 0 && previews().Length == 0, "Bait owner stop leaked navigation/display.");
            Console.WriteLine("PASS native treasure production altar baits source identity, death, generations, real starts and Seek " + oid);

            var target = c + Vector3.UnitX * 10;
            var cast = Cast(action, 3, c, target: 101);
            load(id,
            [
                Header(794), Frame(794, 0, [Actor(1, oid, c, combat: true), Actor(2, 0x233C, c),
                    Actor(100, 0, c), Actor(101, 0, target)], full: true, party: party),
                Frame(794, .1, [Actor(2, 0x233C, c, cast: cast)], party: party),
                TreasureNativeEffect(.5, 2, action, 1, 101, [100], c - Vector3.UnitZ * 10),
                EffectEvent(.6, 2, action, 1, c, target: 100),
                Frame(794, 1, [Actor(101, 0, moved)], party: party),
                Frame(794, 2, [Actor(2, 0x233C, c)], party: party),
                Frame(794, 3, [Actor(4, live, target, targetable: false, dead: true, eventState: 7)], party: party),
                Frame(794, 4, [Actor(4, live, target, targetable: false, dead: true)], party: party),
                Frame(794, 5, [], removed: [4], party: party),
                Frame(794, 6, [], party: party),
            ]);
            controller.Seek(1.1);
            Check(Find("trial/voidzone/").Length == 2 && dangers() == 2 && Obstacles() == 0
                && (Vector3)Value(Value(Find("trial/voidzone/cast/2").Single(), "Geometry"), "Position") == moved
                && (Vector3)Value(Value(Find("trial/voidzone/event/").Single(), "Geometry"), "Position") == target,
                "Current cast, locked native main target and PR expanded effect were merged or duplicated.");
            var ambient = ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>()
                .Single(a => ((string)Value(a, "Id")).StartsWith("trial/voidzone/event/"));
            Check((DateTime)Value(ambient, "Activation") == Start.AddSeconds(.5).AddSeconds((double)delay), "Native float spawn delay changed.");
            controller.Seek(3.1);
            Check(Find("trial/voidzone/").Length == 1 && Find("trial/voidzone/event/").Length == 1, "EventState7 consumed event or actual finish synthesized more.");
            controller.Seek(4.1);
            Check(Find("trial/voidzone/live/4").Length == 1 && Find("trial/voidzone/event/").Length == 0
                && dangers() == 1 && Obstacles() == 0, "Dead unselectable entity failed forbidden-zone handoff.");
            controller.Seek(5.1);
            Check(dangers() == 0 && Find("trial/voidzone/").Length == 0, "Destroyed live zone retained output.");
            controller.Seek(1.1);
            Check(dangers() == 2, "Reverse Seek did not rebuild cast/event paths.");
            stop();
            Console.WriteLine("PASS native treasure production altar voidzone cast, native event, live filter, dual view and navigation " + action);
        }

        load(id,
        [
            Header(794), Frame(794, 0, [Actor(1, 0x2539, c, combat: true), Actor(2, 0x256A, c),
                Actor(100, 0, c)], full: true, party: party),
            Frame(794, .1, [Actor(2, 0x256A, c, cast: Cast(13394, 5, c))], party: party),
            Frame(794, 6, [Actor(2, 0x256A, c)], party: party),
            Frame(794, 7, [], party: party),
        ]);
        controller.Seek(5.5);
        Check(Hints().Contains("Interrupt!") && dangers() == 1
            && ((IEnumerable)Encounter().GetType().GetProperty("TreasureInterruptTargets", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(Encounter())!).Cast<ulong>().SequenceEqual([2ul]), "Independent interrupt model expired before real finish.");
        controller.Seek(6.1);
        Check(!Hints().Contains("Interrupt!") && dangers() == 0, "Interrupt intent retained after real finish.");
        stop();
        Console.WriteLine("PASS native treasure production Chimera interrupt intent retains independent AOE through real finish");
    }
}

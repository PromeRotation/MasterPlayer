using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTsukuyomi(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        bool MoonAt(float x)
        {
            var a = Group("tsukuyomi/moon").Single();
            dynamic shape = Value(a, "Shape");
            return shape.Resolve((Vector3)Value(a, "Position"), (float)Value(a, "Rotation"), null).Contains(new Vector3(x, 0, 100), 0f);
        }
        const string id = "masterplayer.trial.tsukuyomi";
        var c = new Vector3(100, 0, 100);
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Healer", EntityId = 101UL } };
        var first = new List<object> { Actor(1, 0x2210, c, combat: true), Actor(100, 0, c - Vector3.UnitX * 10, classJob: 19),
            Actor(101, 0, c + Vector3.UnitX * 10, classJob: 24), Actor(30, 0x18D6, c) };
        for (ulong i = 10; i < 18; ++i)
            first.Add(Actor(i, 0x2241, c, cast: Cast(11245, 5, c)));
        load(id,
        [
            Header(778), Frame(778, 0, first.ToArray(), full: true, party: party),
            EffectEvent(.1, 30, 11441, 1, target: 101),
            Frame(778, .2, [Actor(100, 0, c - Vector3.UnitX * 10, classJob: 19, stackedStatus: 1538, statusParam: 3)], party: party),
            Frame(778, 1, [Actor(1, 0x2210, c, targetable: false), Actor(10, 0x2241, c),
                Actor(30, 0x18D6, c, cast: Cast(11235, 4, c, target: 101, startedAt: 1)),
                Actor(100, 0, c + Vector3.UnitX * 10, classJob: 19, stackedStatus: 1538, statusParam: 3)], party: party),
            Frame(778, 2, [Actor(100, 0, c + Vector3.UnitX * 10, classJob: 19, stackedStatus: 1538, statusParam: 4),
                Actor(1, 0x2210, c, targetable: false, cast: Cast(11259, 4, c, target: 101, startedAt: 2))], party: party),
            EffectEvent(2.5, 1, 11260, 2, target: 101), EffectEvent(3, 1, 11238, 3, target: 100),
            StatusEvent(3.1, 100, 1538, 0, false),
            Frame(778, 3.2, [Actor(100, 0, c + Vector3.UnitX * 10, classJob: 19)], party: party),
            Frame(778, 4.5, [Actor(1, 0x2210, c, targetable: false), Actor(30, 0x18D6, c)], party: party),
            Frame(778, 5.5, Enumerable.Range(11, 7).Select(i => Actor((ulong)i, 0x2241, c)).ToArray(), party: party),
            Frame(778, 6, [Actor(1, 0x2210, c, targetable: false, dead: true)], party: party),
            Frame(778, 7, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("tsukuyomi/fan").Length == 7 && Group("tsukuyomi/line").Any() && MoonAt(90) && !MoonAt(110),
            "Tsukuyomi first seven, line marker or west gain missing.");
        Check(Group("tsukuyomi/moon").All(a => !(bool)Value(a, "Display")), "Tsukuyomi moon invented visible half arena.");
        controller.Seek(1.5);
        Check(Group("tsukuyomi/fan").Length == 7 && Group("tsukuyomi/torment").Any() && MoonAt(90),
            "Tsukuyomi pending eighth/target-follow bait or historical moon side differs.");
        controller.Seek(2.8);
        Check(Group("tsukuyomi/lunacy").Any() && MoonAt(110), "Tsukuyomi follow-up hit consumed stack or gain failed.");
        controller.Seek(3.5);
        Check(!Group("tsukuyomi/line").Any() && !Group("tsukuyomi/moon").Any() && Group("tsukuyomi/lunacy").Any(),
            "Tsukuyomi resolve correction/status loss crossed independent stack.");
        controller.Seek(4.7);
        Check(!Group("tsukuyomi/lunacy").Any() && !Group("tsukuyomi/torment").Any() && Group("tsukuyomi/fan").Length == 7,
            "Tsukuyomi actual cast ends crossed fan component.");
        controller.Seek(5.7);
        Check(Group("tsukuyomi/fan").Length == 0 && dangers() == 0, "Tsukuyomi actual endings leaked navigation.");
        controller.Seek(6.5); Check(previews().Length == 0, "Tsukuyomi death leaked graphics.");
        controller.Seek(.5);
        Check(Group("tsukuyomi/line").Any() && Group("tsukuyomi/fan").Length == 7 && MoonAt(90), "Tsukuyomi reverse Seek lost history.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Tsukuyomi disable leaked outputs."); stop();
        Console.WriteLine("PASS Tsukuyomi actual DLL replay fan FIFO, dynamic torment, effect line stack, cast Lunacy, moon history, independent real endings, Seek, death and isolation");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntMisdirection(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const uint territory = 1187;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        bool Active() => (bool)Script().GetType().GetProperty("MisdirectionActive")!.GetValue(Script())!;
        var party = new object[] { new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL } };
        foreach (var sid in new uint[] { 1422, 2936, 3694, 3909 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x41FC, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero),
                    Actor(101, 0, Vector3.Zero)], full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x41FC, Vector3.Zero, combat: true, cast: Cast(37040, 3, Vector3.Zero)),
                    Actor(2, 0x41FC, Vector3.Zero, combat: true, cast: Cast(37038, 5, Vector3.Zero)),
                    Actor(3, 0x41FC, Vector3.Zero, combat: true, cast: Cast(37039, 5, Vector3.Zero)),
                    Actor(4, 0x41FC, Vector3.Zero, combat: true, cast: Cast(37305, 4, Vector3.Zero)),
                    Actor(5, 0x41FC, Vector3.Zero, combat: true, cast: Cast(37163, 4, Vector3.Zero))], party: party),
                StatusEvent(1, 101, sid, 0, true), StatusEvent(2, 100, sid, 0, true),
                Frame(territory, 3.5, [Actor(1, 0x41FC, Vector3.Zero, combat: true)], party: party),
                StatusEvent(4, 100, 2936, 0, true), StatusEvent(4.1, 100, sid, 0, false),
                Frame(territory, 5, [Actor(100, 0, Vector3.Zero, stackedStatus: 3694)], party: party),
                Frame(territory, 6, Enumerable.Range(1, 5).Select(i => Actor((ulong)i, 0x41FC, Vector3.Zero, combat: true)), party: party),
                Frame(territory, 7, [Actor(100, 0, Vector3.Zero)], party: party),
                Frame(territory, 8, Enumerable.Range(1, 5).Select(i => Actor((ulong)i, 0x41FC, Vector3.Zero)), party: party),
                Frame(territory, 9, [], party: party)
            ]);
            controller.Seek(1.5d);
            Check(!Active() && dangers() == 3 && previews().Length == 8, "Starcrier cast/teammate status enabled personal misdirection or lost ordinary shapes.");
            controller.Seek(3.6d);
            Check(Active() && Script().GetType().BaseType!.GetField("misdirectionInput", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Script()) == null, "Starcrier lost gain-before-snapshot at cast finish or initialized native hooks during replay.");
            controller.Seek(4.5d);
            Check(!Active(), "Starcrier overlapping statuses were refcounted instead of cleared by any loss.");
            controller.Seek(5.5d);
            Check(Active(), "Starcrier failed to acquire an existing snapshot status.");
            controller.Seek(7.5d);
            Check(!Active() && dangers() == 0 && previews().Length == 5, "Starcrier lost status or cast finish retained output.");
            controller.Seek(8.5d);
            Check(previews().Length == 0, "Disengaged Starcrier retained enemy drawing.");
            controller.Seek(3.6d);
            Check(Active(), "Starcrier reverse Seek failed to rebuild personal status.");
            stop();
        }
        Console.WriteLine("PASS HuntMisdirection actual DLL Starcrier five rules, four status IDs, teammate/single-bit semantics, gain before snapshot, cast finish, native replay isolation, reverse Seek and cleanup");
    }

    private static void RunHuntStored(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 956;
        dynamic Geometry() => previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/"))
            .GetType().GetProperty("Geometry")!.GetValue(previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/")))!;
        foreach (var action in new uint[] { 27354, 27355, 27356 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory), Frame(territory, 0, [Actor(1, 0x35DE, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
                Frame(territory, .1, [Actor(1, 0x35DE, Vector3.Zero, combat: true, cast: Cast(action, 3, Vector3.Zero))]),
                Frame(territory, 4, [Actor(1, 0x35DE, new(30, 0, 40), combat: true, rotation: MathF.PI / 2)]),
                Frame(territory, 8, [Actor(1, 0x35DE, new(30, 0, 40), combat: true, cast: Cast(27358, 1, Vector3.Zero, startedAt: 8))]),
                Frame(territory, 10, [Actor(1, 0x35DE, new(30, 0, 40), combat: true)]), Frame(territory, 11, [])
            ]);
            controller.Seek(4.5d);
            Check(dangers() == 1 && previews().Length == 2, "Storsie prepare finish discarded the aspect.");
            Check((bool)Geometry().Contains(new Vector3(45, 0, 40), 0f), "Storsie aspect did not move/rotate with the boss.");
            controller.Seek(10.5d);
            Check(dangers() == 0 && previews().Length == 1, "Storsie actual attack did not clear its aspect.");
            controller.Seek(4.5d);
            Check(dangers() == 1, "Storsie reverse Seek lost the aspect.");
            stop();
        }
        foreach (var (sid, offset) in new[] { (2840u, -MathF.PI / 2), (2841u, MathF.PI / 2), (2842u, MathF.PI), (2958u, 0f) })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory), Frame(territory, 0, [Actor(1, 0x360B, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
                HuntBossStatusEvent(.05, sid),
                Frame(territory, .1, [Actor(1, 0x360B, Vector3.Zero, combat: true, cast: Cast(27318, 4, Vector3.Zero))]),
                Frame(territory, 5, [Actor(1, 0x360B, new(30, 0, 40), combat: true, rotation: 2)]),
                Frame(territory, 6, [Actor(1, 0x360B, new(30, 0, 40), combat: true, cast: Cast(27319, 1, Vector3.Zero, startedAt: 6))]),
                Frame(territory, 8, [Actor(1, 0x360B, new(30, 0, 40), combat: true)]), Frame(territory, 9, [])
            ]);
            controller.Seek(5.5d);
            var inside = new Vector3(30, 0, 40) + new Vector3(MathF.Sin(offset), 0, MathF.Cos(offset)) * 39.99f;
            Check(dangers() == 1 && (bool)Geometry().Contains(inside, 0f), "MoussePrincess lost event-ahead Whimsy, fixed direction/live position or prepare persistence.");
            controller.Seek(8.5d);
            Check(dangers() == 0, "MoussePrincess actual resolve did not clear the cone.");
            stop();
        }
        foreach (var (jump, final) in new[] { (27341u, 27346u), (27342u, 27347u) })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory), Frame(territory, 0, [Actor(1, 0x35DC, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
                Frame(territory, .1, [Actor(1, 0x35DC, Vector3.Zero, combat: true, cast: Cast(jump, 6, new(10, 0, 20)))]),
                Frame(territory, 7, [Actor(1, 0x35DC, new(100, 0, 100), combat: true)]),
                Frame(territory, 10, [Actor(1, 0x35DC, new(100, 0, 100), combat: true, cast: Cast(final, 1, new(30, 0, 40), startedAt: 10))]),
                Frame(territory, 12, [Actor(1, 0x35DC, Vector3.Zero, combat: true)]), Frame(territory, 13, [])
            ]);
            controller.Seek(7.5d);
            Check(dangers() == 1 && (bool)Geometry().Contains(new Vector3(10, 0, 29), 0f), "Ophioneus jump finish lost the locked prediction.");
            controller.Seek(10.5d);
            Check(dangers() == 1 && (bool)Geometry().Contains(new Vector3(30, 0, 49), 0f), "Ophioneus final cast failed to replace its prediction.");
            controller.Seek(12.5d);
            Check(dangers() == 0, "Ophioneus final cast leaked its area.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x35BD, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
            Frame(territory, .1, [Actor(1, 0x35BD, Vector3.Zero, combat: true, cast: Cast(26876, 2.5, Vector3.Zero))]),
            Frame(territory, 3, [Actor(1, 0x35BD, Vector3.Zero, combat: true, cast: Cast(26877, 2.5, Vector3.Zero, startedAt: 3))]),
            Frame(territory, 6, [Actor(1, 0x35BD, Vector3.Zero, combat: true, cast: Cast(26915, 3, Vector3.Zero, startedAt: 6))]),
            Frame(territory, 10, [Actor(1, 0x35BD, new(30, 0, 40), combat: true)]),
            EffectEvent(11, 1, 26915, 1), EffectEvent(11, 1, 26915, 1), EffectEvent(12, 1, 26167, 2),
            Frame(territory, 13, [])
        ]);
        controller.Seek(4d);
        Check(dangers() == 0 && previews().Length == 1, "Ruminator trace became damage before activation.");
        controller.Seek(10.5d);
        Check(dangers() == 1 && (bool)Geometry().Contains(new Vector3(30, 0, 60), 0f), "Ruminator reversal/cast finish/dynamic origin changed.");
        CheckHuntPersistentWindows(runtimeObject);
        controller.Seek(11.5d);
        Check(dangers() == 1 && !(bool)Geometry().Contains(new Vector3(30, 0, 60), 0f)
            && (bool)Geometry().Contains(new Vector3(30, 0, 47), 0f), "Ruminator duplicate effect advanced more than one shape.");
        controller.Seek(12.5d);
        Check(dangers() == 0, "Ruminator last effect did not empty the queue.");
        controller.Seek(10.5d);
        Check(dangers() == 1, "Ruminator reverse Seek lost stored shapes.");
        stop();

        var sphatika = new List<string>
        {
            Header(territory), Frame(territory, 0, [Actor(1, 0x3670, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
            HuntBossStatusEvent(.01, 2838), HuntBossStatusEvent(.02, 2835),
            HuntBossStatusEvent(.03, 2837), HuntBossStatusEvent(.04, 2836),
            Frame(territory, .1, [Actor(1, 0x3670, Vector3.Zero, combat: true, cast: Cast(27624, 5, Vector3.Zero))]),
            Frame(territory, 6, [Actor(1, 0x3670, Vector3.Zero, combat: true)])
        };
        for (var i = 0; i < 8; ++i)
        {
            sphatika.Add(Frame(territory, 10 + 2 * i, [Actor(1, 0x3670, Vector3.Zero, combat: true,
                cast: Cast(i % 2 == 0 ? 27626u : 27715u, 1, Vector3.Zero, startedAt: 10 + 2 * i))]));
            sphatika.Add(Frame(territory, 11.5 + 2 * i, [Actor(1, 0x3670, Vector3.Zero, combat: true)]));
        }
        sphatika.Add(Frame(territory, 27, []));
        load("masterplayer.hunts.simple", sphatika);
        controller.Seek(6.5d);
        Check(dangers() == 1 && (bool)Geometry().Contains(Vector3.UnitZ * 39.99f, 0f), "Sphatika did not use UI-sorted status order after stance finish.");
        CheckHuntPersistentWindows(runtimeObject);
        controller.Seek(11.6d);
        Check(dangers() == 1 && (bool)Geometry().Contains(-Vector3.UnitZ * 39.99f, 0f), "Sphatika first real finish did not promote its opposite half.");
        controller.Seek(25.6d);
        Check(dangers() == 0 && previews().Length == 1, "Sphatika eighth real finish retained a pending cleave.");
        controller.Seek(6.5d);
        Check(dangers() == 1, "Sphatika reverse Seek lost the bearing queue.");
        stop();
        Console.WriteLine("PASS HuntStored actual DLL Storsie aspects, MoussePrincess four status directions, Ophioneus jump/final clocks, Ruminator record/reverse/effects, Sphatika eight UI-ordered cleaves, double-view navigation, reverse Seek and cleanup");
    }

    private static string HuntBossStatusEvent(double t, uint status) => JsonSerializer.Serialize(new
    {
        t, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
        data = new { SourceId = 1, TargetId = 1, StatusId = status, Duration = 30, Timestamp = Start.AddSeconds(t) }
    });

    private static void CheckHuntPersistentWindows(object runtimeObject)
    {
        dynamic runtime = runtimeObject;
        foreach (dynamic area in (IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas)
            foreach (var window in (IEnumerable)area.DangerWindows)
            {
                var buffered = window.GetType().GetMethod("Buffered", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [.5f, .1f])!;
                Check((DateTime)buffered.GetType().GetProperty("DangerFrom")!.GetValue(buffered)! <= (DateTime)runtime.CurrentTime,
                    "Unknown source activation did not become a bufferable, already active native danger.");
            }
    }

    private static void RunHuntRotations(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 956;
        foreach (var (action, count) in new[] { (27493u, 2), (27494u, 2), (27497u, 8), (27498u, 8), (27521u, 8), (27522u, 8) })
        {
            var lines = new List<string>
            {
                Header(territory), Frame(territory, 0, [Actor(1, 0x361B, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
                Frame(territory, .1, [Actor(1, 0x361B, Vector3.Zero, combat: true, cast: Cast(action, 9, Vector3.Zero))]),
                Frame(territory, 10, [Actor(1, 0x361B, new(100, 0, 100), combat: true)])
            };
            for (var i = 1; i < count; ++i)
            {
                lines.Add(Frame(territory, 12 + 4 * i, [Actor(1, 0x361B, new(100, 0, 100), combat: true,
                    cast: Cast(count == 2 ? 27495u : 27499u, 1, new(100, 0, 100), startedAt: 12 + 4 * i))]));
                lines.Add(Frame(territory, 14 + 4 * i, [Actor(1, 0x361B, new(100, 0, 100), combat: true)]));
            }
            lines.Add(Frame(territory, 16 + 4 * count, []));
            load("masterplayer.hunts.simple", lines);
            controller.Seek(1d);
            Check(dangers() == (count == 2 ? 1 : 2) && previews().Length == (count == 2 ? 2 : 3),
                "Gurangatch first/next display did not preserve both risky stages or double-strike limit.");
            controller.Seek(10.5d);
            Check(dangers() == (count == 2 ? 1 : 2), "Gurangatch initial finish removed the whole sequence.");
            controller.Seek(15d + 4 * (count - 1));
            Check(dangers() == 0 && previews().Length == 1, "Gurangatch final actual finish retained a sequence.");
            controller.Seek(1d);
            Check(dangers() == (count == 2 ? 1 : 2), "Gurangatch reverse Seek lost the sequence.");
            stop();
        }
        var sneeze = new List<string>
        {
            Header(territory), Frame(territory, 0, [Actor(1, 0x360A, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true)
        };
        for (var i = 0; i < 5; ++i)
            sneeze.Add(Frame(territory, .1 + 4 * i, [Actor(1, 0x360A, Vector3.Zero, combat: true,
                cast: Cast(i == 0 ? 27307u : 27308u, 3, Vector3.Zero, i * .4f, startedAt: .1 + 4 * i))]));
        sneeze.Add(Frame(territory, 20, [Actor(1, 0x360A, Vector3.Zero, combat: true,
            cast: Cast(27309, 3, Vector3.Zero, MathF.PI / 2, startedAt: 20))]));
        sneeze.Add(Frame(territory, 24, [Actor(1, 0x360A, new(30, 0, 40), combat: true)]));
        for (var i = 1; i < 5; ++i)
        {
            sneeze.Add(Frame(territory, 24 + 4 * i, [Actor(1, 0x360A, new(30, 0, 40), combat: true,
                cast: Cast(i % 2 == 0 ? 27693u : 27310u, 1, Vector3.Zero, startedAt: 24 + 4 * i))]));
            sneeze.Add(Frame(territory, 26 + 4 * i, [Actor(1, 0x360A, new(30, 0, 40), combat: true)]));
        }
        sneeze.Add(Frame(territory, 44, []));
        load("masterplayer.hunts.simple", sneeze);
        controller.Seek(18d);
        Check(dangers() == 0 && previews().Length == 1, "Burfurlur inhale created a damage zone.");
        controller.Seek(21d);
        Check(dangers() == 2 && previews().Length == 3, "Burfurlur failed to show two risky sneeze directions.");
        controller.Seek(24.5d);
        Check(dangers() == 2 && previews().Length == 3, "Burfurlur first real finish consumed its whole memory.");
        controller.Seek(42.5d);
        Check(dangers() == 0 && previews().Length == 1, "Burfurlur fifth real finish retained an attack.");
        controller.Seek(21d);
        Check(dangers() == 2, "Burfurlur reverse Seek lost remembered directions.");
        stop();
        var party = new object[] { new { Slot = 0, EntityId = 100UL } };
        load("masterplayer.hunts.simple",
        [
            Header(territory), Frame(territory, 0, [Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x361B, Vector3.Zero, combat: true, cast: Cast(27512, 4, Vector3.Zero)),
                Actor(2, 0x360A, Vector3.Zero, combat: true, cast: Cast(27314, 4, Vector3.Zero)),
                Actor(3, 0x360A, Vector3.Zero, combat: true, cast: Cast(27313, 4, Vector3.Zero))], party: party),
            Frame(territory, 5, [Actor(1, 0x361B, Vector3.Zero, combat: true),
                Actor(2, 0x360A, Vector3.Zero, combat: true), Actor(3, 0x360A, Vector3.Zero, combat: true)], party: party),
            Frame(territory, 6, [], party: party)
        ]);
        controller.Seek(1d);
        Check(dangers() == 2 && previews().Length == 5, "BoneShaker/Uppercut/RottenSpores lost pure damage or independent geometry.");
        controller.Seek(5.5d);
        Check(dangers() == 0 && previews().Length == 3, "Ordinary Gurangatch/Burfurlur cast finishes leaked output.");
        stop();
        Console.WriteLine("PASS HuntRotations actual DLL Gurangatch all six starters full two/eight-hit sequences and two risky previews, Burfurlur five remembered directions actual finishes, dynamic origin, reverse Seek and cleanup");
    }
}

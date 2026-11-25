using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntNeyoozoteel(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object[] Sap() => previews().Where(p => ((string)Value(p, "Id")).Contains("/neyoozoteel/sap/")).ToArray();
        object[] Roots() => previews().Where(p => ((string)Value(p, "Id")).Contains("/neyoozoteel/roots/")).ToArray();
        DateTime[] Deadlines()
        {
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            return ((IEnumerable)snapshot.DangerAreas).Cast<object>()
                .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
                .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        }
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        foreach (var action in new uint[] { 37377, 37376, 37378, 37379 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4233, new(7, 0, 9), combat: true), Actor(999, 0x233C, new(50, 0, 60)),
                    Actor(100, 0, default), Actor(101, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, new(50, 0, 60), cast: Cast(action, 3, new(10, 0, 20)))], party: party),
                Frame(territory, 4, [Actor(999, 0x233C, new(50, 0, 60)), Actor(1, 0x4233, new(70, 0, 90), combat: true)], party: party),
                EffectEvent(5, 1, 37308, 1),
                EffectEvent(6, 999, 37370, 2), EffectEvent(6.01, 999, 37370, 2),
                EffectEvent(7, 1, 42172, 3), EffectEvent(8, 999, 37396, 4),
                Frame(territory, 9, [], removed: [1], party: party),
            ]);
            controller.Seek(1d);
            Check(Sap().Length == 3 && dangers() == 3, "Neyoozoteel lost full risky Sap predictions.");
            var finish = Start.AddSeconds(.1 + 3 + .3);
            Check(Deadlines().SequenceEqual(Enumerable.Range(0, 3).Select(i => finish.AddSeconds(14.6f + 2.2f * i))),
                "Neyoozoteel changed exact float Sap deadlines.");
            foreach (var preview in Sap())
            {
                dynamic shape = Value(preview, "Geometry");
                Check((bool)shape.Contains(new Vector3(7, 0, 9), 0f)
                    && !(bool)shape.Contains(new Vector3(50, 0, 60), 0f), "Sap origin followed helper/ground location instead of primary actor.");
            }
            controller.Seek(5.5);
            Check(Sap().Length == 3 && dangers() == 3, "Visual Finish or ordinary NoxiousSap effect consumed the queue.");
            controller.Seek(6.5);
            Check(Sap().Length == 2 && dangers() == 2, "Sap helper effect failed FIFO/deduplication.");
            controller.Seek(8.5);
            Check(Sap().Length == 0 && dangers() == 0, "Sap final effect retained output.");
            controller.Seek(1d);
            Check(Sap().Length == 3 && dangers() == 3, "Sap reverse Seek retained future consumption.");
            controller.Seek(9.1);
            Check(previews().Length == 0 && dangers() == 0, "Neyoozoteel owner removal retained auxiliary output.");
            stop();
        }

        foreach (var action in new uint[] { 37373, 37374 })
        {
            var events = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4233, default, combat: true), Actor(999, 0x233C, default),
                    Actor(100, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(action, 5, default))], party: party),
                Frame(territory, 6, [Actor(999, 0x233C, default)], party: party)
            };
            for (var i = 0; i < 8; ++i)
            {
                events.Add(EffectEvent(7 + i * 3, 1, 37375, (uint)(10 + i)));
                events.Add(EffectEvent(7.01 + i * 3, 1, 37375, (uint)(10 + i)));
            }
            events.Add(Frame(territory, 31, [], party: party));
            load("masterplayer.hunts.simple", events);
            controller.Seek(1d);
            Check(Roots().Length == 2 && dangers() == 2 && Deadlines().SequenceEqual(new[]
                { Start.AddSeconds(.1 + 5 + .3), Start.AddSeconds(.1 + 5 + .3).AddSeconds(2.4f) }),
                "Roots lost its two risky crosses or exact float interval.");
            controller.Seek(6.5);
            Check(Roots().Length == 2 && Deadlines()[1] == Start.AddSeconds(.1 + 5 + .3).AddSeconds(2.4f),
                "Roots Finish or idle cache advanced its timeline.");
            for (var i = 0; i < 8; ++i)
            {
                controller.Seek(7.5 + i * 3);
                Check(Roots().Length == (i == 7 ? 0 : i == 6 ? 1 : 2)
                    && dangers() == Roots().Length, "Roots did not consume exactly eight real effects.");
                if (i != 7) Check(Deadlines()[0] == Start.AddSeconds(7 + i * 3).AddSeconds(2.4f),
                    "Roots next activation was not based on the actual effect.");
            }
            controller.Seek(1d);
            Check(Roots().Length == 2, "Roots reverse Seek retained an advanced sequence.");
            stop();
        }

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x4233, default, combat: true), Actor(100, 0, new(30, 0, 0)),
                Actor(101, 0, default), Actor(998, 0x233C, default), Actor(999, 0x233C, default)], full: true, party: party),
            Frame(territory, .1, [Actor(998, 0x233C, default, cast: Cast(37307, 5, default, target: 101))], party: party),
            Frame(territory, .2, [Actor(999, 0x233C, default, cast: Cast(37307, 5, default, target: 101, startedAt: .2))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 2, [], removed: [998], party: party),
            Frame(territory, 3, [Actor(1, 0x4233, default, combat: true, cast: Cast(38331, 5, default, startedAt: 3))], party: party),
            Frame(territory, 4, [], removed: [1], party: party),
        ]);
        controller.Seek(.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 2 && dangers() == 1,
            "Cocopult did not publish two target circles and one navigation union.");
        controller.Seek(1.5);
        Check(previews().Count(p => ((string)Value(p, "Id")).Contains("/stack/preview/")) == 1,
            "Cocopult Finish failed component target FIFO.");
        controller.Seek(2.5);
        Check(dangers() == 0 && !previews().Any(p => ((string)Value(p, "Id")).Contains("/stack/preview/")),
            "Cocopult helper destruction retained stack output.");
        controller.Seek(3.5);
        var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        Check((string?)Value(script, "PredictedDamageText") == "Raidwide：2人/1次" && dangers() == 0,
            "Neurotoxify prediction differs or invented a raidwide circle.");
        controller.Seek(4.1);
        Check(previews().Length == 0 && dangers() == 0, "Neyoozoteel cleanup retained output.");
        stop();
        Console.WriteLine("PASS Neyoozoteel actual DLL four Omens, exact float clocks, primary origin, full risky Sap FIFO, eight effect-driven roots, Cocopult target FIFO, reverse Seek and owner cleanup");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunIfritExtreme(object managerObject, object runtimeObject, object controllerObject,
        Action<string, string[]> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.trial.ifrit-extreme";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("ifrit-ex/" + prefix)).ToArray();
        string Stage() => Encounter().GetType().GetField("ifritExStep", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Encounter())!.ToString()!;
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }, new { Slot = 2, EntityId = 102ul }];
        var boss = new Vector3(0, 0, -13);
        load(id,
        [
            Header(295),
            Frame(295, 0, [Actor(1, 0xD3, boss, combat: true, targetId: 101),
                Actor(100, 0, new(8, 0, 0), classJob: 21), Actor(101, 0, new(0, 0, -18), classJob: 19),
                Actor(102, 0, new(-8, 0, 0), classJob: 24)], full: true, party: party),
            Frame(295, 1, [Actor(1, 0xD3, boss, combat: true, targetId: 101,
                cast: Cast(1529, 2, boss, target: 102, startedAt: 1))], party: party),
            Frame(295, 3.4, [Actor(1, 0xD3, boss, combat: true, targetId: 101)], party: party),
            EffectEvent(5.4, 20, 1530, 1),
            Frame(295, 6, [Actor(20, 0xD4, new(15, 0, 0))], party: party),
            Frame(295, 7, [Actor(1, 0xD3, boss, combat: true, targetId: 101, targetable: false, stackedStatus: 775),
                Actor(20, 0xD4, new(15, 0, 0), dead: true)], party: party),
            Frame(295, 8, [Actor(1, 0xD3, boss, combat: true, targetId: 101, targetable: false, stackedStatus: 775,
                cast: Cast(1536, 2, boss, startedAt: 8))], party: party),
            Frame(295, 10.4, [Actor(1, 0xD3, boss, combat: true, targetId: 101, targetable: false, stackedStatus: 775)], party: party),
            Frame(295, 11, [Actor(1, 0xD3, boss, combat: true, targetId: 101)], party: party),
            Frame(295, 12, [Actor(21, 0x908, new(6, 0, 0), cast: Cast(1359, 3, new(6, 0, 0), startedAt: 12))], party: party),
            Frame(295, 12.1, [Actor(1, 0xD3, boss, combat: true, targetId: 101,
                cast: Cast(1356, 2.2, boss, startedAt: 12.1))], party: party),
            Frame(295, 14.7, [Actor(1, 0xD3, boss, combat: true, targetId: 101)], party: party),
            Frame(295, 15.4, [Actor(21, 0x908, new(6, 0, 0))], party: party),
            Frame(295, 16, [Actor(1, 0xD3, boss, combat: true, targetId: 101, targetable: false)], party: party),
            Frame(295, 17, [Actor(30, 0xD3, new(0, 0, -18), cast: Cast(1532, 3, new(0, 0, -18), .7f, startedAt: 17))], party: party),
            Frame(295, 18, [Actor(100, 0, new(8, 0, 0), classJob: 21, dead: true)], party: party),
            Frame(295, 20.4, [Actor(30, 0xD3, new(0, 0, -18))], party: party),
            Frame(295, 21, [Actor(1, 0xD3, boss, combat: true, targetId: 101),
                Actor(100, 0, new(8, 0, 0), classJob: 21)], party: party),
            Frame(295, 22, [Actor(20, 0xD4, new(15, 0, 0))], party: party),
            StatusEvent(22.1, 100, 377, 3, true),
            StatusEvent(22.2, 101, 377, 3, true),
            Frame(295, 23, [Actor(1, 0xD3, boss, combat: true, dead: true)], party: party),
            Frame(295, 24, [], party: party),
        ]);
        controller.Seek(1.1); Check(Group("wind/display").Length == 1 && Group("wind/avoid").Length == 0, "Ifrit wind visual.");
        controller.Seek(4.399); Check(Group("wind/avoid").Length == 0, "Ifrit before3.4.");
        controller.Seek(4.5); Check(Group("wind/avoid").Length == 1, "Ifrit delayed wind navigation.");
        controller.Seek(5.5); Check((DateTime)Value(Group("wind/display").Single(), "Activation") == Start.AddSeconds(11.4), "Ifrit actual wind cadence.");
        controller.Seek(6.1); Check(Stage() == "Nails" && Group("next-nail").Length == 1, "Ifrit nail phase.");
        controller.Seek(10.5); Check(Stage() == "InvincibleEnd" && Group("hellfire/safe").Length == 1, "Ifrit Hellfire finish.");
        controller.Seek(12.05); Check(Stage() == "PlumeStart", "Ifrit helper before activation.");
        controller.Seek(12.2);
        var casts = (IDictionary)Value(Encounter(), "Casts");
        Check(Stage() == "PlumeEnd" && casts.Contains(21ul), "Ifrit restores existing plume helper.");
        controller.Seek(15.5); Check(Stage() == "Disappear", "Ifrit actual plume ending.");
        controller.Seek(17.1); Check(Stage() == "Cyclone" && ((IDictionary)Value(Encounter(), "Casts")).Contains(30ul), "Ifrit clone charge.");
        controller.Seek(18.1); Check(Value(Encounter(), "Active") != null, "Ifrit personal death preserved phase.");
        controller.Seek(21.1); Check(Stage() == "BeforeNails", "Ifrit second round.");
        controller.Seek(22.3); Check(Stage() == "Nails" && Group("fetters/line").Length == 1
            && !(bool)Value(Group("fetters/line").Single(), "Safe"), "Ifrit status377 event route.");
        controller.Seek(23.1); Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Ifrit death cleanup.");
        controller.Seek(4.5); Check(Group("wind/avoid").Length == 1 && Stage() == "BeforeNails", "Ifrit reverse Seek.");
        var actionControl = Script().GetType().BaseType!.GetField("twintaniaActions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        Check((DateTime)actionControl.GetType().GetField("nextAttempt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(actionControl)! == default,
            "Ifrit replay executed control.");
        manager.SetScriptEnabled(id, false); Check(previews().Length == 0 && dangers() == 0, "Ifrit owner cleanup.");
        stop();
        Console.WriteLine("PASS Ifrit Extreme actual DLL wind clocks, nail phases, Hellfire, existing plume helpers, Cyclone clones, fetters, personal death, reverse Seek and owner cleanup");
    }
}

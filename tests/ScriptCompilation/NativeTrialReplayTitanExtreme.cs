using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTitanExtreme(object managerObject, object runtimeObject, object controllerObject,
        Action<string, string[]> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.trial.titan-extreme";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("titan-ex/" + prefix)).ToArray();
        float Radius() => (float)Value(Value(Encounter(), "Arena"), "Radius");
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(id,
        [
            Header(296),
            Frame(296, 0, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetId: 101),
                Actor(100, 0, new(0, 0, 4), classJob: 21), Actor(101, 0, new(0, 0, -10))], full: true, party: party),
            Frame(296, 1, [Actor(20, 0x1B2, new(4, 0, 0), cast: Cast(1470, 2, new(4, 0, 0), startedAt: 1))], party: party),
            Frame(296, 2, [Actor(100, 0, new(0, 0, 4), classJob: 21, dead: true)], party: party),
            Frame(296, 3.4, [Actor(20, 0x1B2, new(4, 0, 0)), Actor(100, 0, new(0, 0, 4), classJob: 21)], party: party),
            Frame(296, 4, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetable: false, targetId: 101)], party: party),
            Frame(296, 5, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetable: false, targetId: 101,
                cast: Cast(1472, 3, Vector3.Zero, startedAt: 5))], party: party),
            Frame(296, 8.4, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetable: false, targetId: 101)], party: party),
            Frame(296, 9, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetId: 101)], party: party),
            StatusEvent(9.1, 101, 126, 2, true),
            Frame(296, 10, [Actor(1, 0xF8, new(0, 0, -9), combat: true, targetId: 101,
                cast: Cast(1466, 5, new(0, 0, -9), startedAt: 10))], party: party),
            Frame(296, 15.4, [Actor(1, 0xF8, new(0, 0, -9), combat: true, targetId: 101)], party: party),
            Frame(296, 17, [Actor(1, 0xF8, new(0, 0, -9), combat: true, targetId: 101, targetable: false)], party: party),
            Frame(296, 18, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetId: 101, targetable: false,
                cast: Cast(1472, 3, Vector3.Zero, startedAt: 18))], party: party),
            Frame(296, 21.4, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetId: 101, targetable: false)], party: party),
            Frame(296, 22, [Actor(30, 0x5E5, Vector3.Zero)], party: party),
            Frame(296, 23, [Actor(21, 0x1B2, Vector3.Zero, cast: Cast(1468, 3, Vector3.Zero, startedAt: 23))], party: party),
            Frame(296, 24, [Actor(30, 0x5E5, Vector3.Zero, targetable: false)], party: party),
            Frame(296, 25, [Actor(1, 0xF8, Vector3.Zero, combat: true, targetId: 101),
                Actor(31, 0x1E8F8F, new(5, 0, 0))], party: party),
            Frame(296, 26, [Actor(31, 0x1E8F8F, new(5, 0, 0), eventState: 7)], party: party),
            Frame(296, 27, [Actor(1, 0xF8, Vector3.Zero, combat: true, dead: true)], party: party),
            Frame(296, 28, [], party: party),
        ]);
        controller.Seek(1.1);
        var puddlePreviews = previews().Select(p => (string)Value(p, "Id")).ToArray();
        Check(Group("cast/20").Length == 1 && dangers() == 1 && puddlePreviews.Length == 2
            && puddlePreviews.Count(p => p.EndsWith("enemy/marker/1", StringComparison.Ordinal)) == 1
            && puddlePreviews.Count(p => p.EndsWith("titan-ex/cast/20", StringComparison.Ordinal)) == 1, "Titan puddle and boss marker.");
        controller.Seek(2.1); Check(Value(Encounter(), "Active") != null && Group("cast/20").Length == 1, "Titan personal death.");
        controller.Seek(4.1); Check(Group("crush").Length == 3 && Radius() == 25, "Titan first disappearance.");
        controller.Seek(8.5); Check(Group("crush").Length == 0 && Radius() == 15, "Titan actual first shrink.");
        controller.Seek(9.15);
        var actions = ((IEnumerable)Encounter().GetType().GetMethod("EnemyProvokeActions", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        Check(actions.Length == 1, "Titan immediate two-stack swap.");
        controller.Seek(15.5); Check(Group("upheaval/stand").Length == 1, "Titan post-end1 hold.");
        controller.Seek(16.5); Check(Group("upheaval").Length == 0, "Titan hold expiry.");
        controller.Seek(21.5); Check(Radius() == 12, "Titan second shrink.");
        controller.Seek(23.1); Check(Group("cast/21").Length == 1, "Titan heart helpers.");
        controller.Seek(24.1); Check(Group("cast").Length == 0, "Titan heart exit.");
        controller.Seek(25.1); Check(Group("voidzone").Length == 1, "Titan final voidzone.");
        controller.Seek(26.1); Check(Group("voidzone").Length == 0, "Titan state7.");
        controller.Seek(27.1); Check(Value(Encounter(), "Active") == null && previews().Length == 0 && dangers() == 0, "Titan death cleanup.");
        controller.Seek(4.1); Check(Radius() == 25 && Group("crush").Length == 3, "Titan reverse Seek.");
        var actionControl = Script().GetType().BaseType!.GetField("twintaniaActions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        Check((DateTime)actionControl.GetType().GetField("nextAttempt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(actionControl)! == default,
            "Titan replay executed provoke.");
        manager.SetScriptEnabled(id, false); Check(previews().Length == 0 && dangers() == 0, "Titan owner cleanup.");
        stop();
        Console.WriteLine("PASS Titan Extreme actual DLL seven phases, actual shrink15/12, independent helpers, two-stack provoke, post-Upheaval hold, personal death, voidzone, reverse Seek and owner cleanup");
    }
}

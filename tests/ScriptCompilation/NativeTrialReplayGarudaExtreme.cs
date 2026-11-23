using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunGarudaExtreme(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.trial.garuda-extreme";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("garuda-ex/" + prefix)).ToArray();
        ulong[] Forbidden() => (ulong[])Value(Encounter(), "ForbiddenTargets");
        object[] Actions() => ((IEnumerable)Encounter().GetType().GetMethod("EnemyProvokeActions", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(id,
        [
            Header(297), Frame(297, 0, [Actor(1, 0xF5, Vector3.Zero, combat: true),
                Actor(100, 0, new(10, 0, 0), classJob: 21), Actor(101, 0, new(0, 0, 10)),
                Actor(30, 0xF3, Vector3.Zero, hitboxRadius: 2.3f)], full: true, party: party),
            Frame(297, .1, [Actor(1, 0xF5, Vector3.Zero, combat: true, targetable: false)], party: party),
            Frame(297, 1, [Actor(1, 0xF5, new(0, 0, -13), combat: true, cast: Cast(1761, 1, new(0, 0, -13), startedAt: 1))], party: party),
            Frame(297, 2.4, [Actor(1, 0xF5, Vector3.Zero, combat: true)], party: party),
            Frame(297, 3, [Actor(1, 0xF5, Vector3.Zero, combat: true, targetable: false)], party: party),
            Frame(297, 4, [Actor(1, 0xF5, new(13, 0, 0), combat: true, cast: Cast(1761, 1, new(13, 0, 0), startedAt: 4))], party: party),
            Frame(297, 5.4, [Actor(1, 0xF5, Vector3.Zero, combat: true)], party: party),
            Frame(297, 6, [Actor(10, 0x621, new(0, 0, 15)), Actor(20, 0x600, new(0, 0, 10), targetId: 101),
                Actor(21, 0x1E8F68, new(-10, 0, 0)), Actor(22, 0x1B2, Vector3.Zero, cast: Cast(1549, 80, Vector3.Zero, startedAt: 6)),
                Actor(25, 0x600, new(0, 0, -10), dead: true, cast: Cast(1555, 4, new(0, 0, -10), startedAt: 6))], party: party),
            StatusEvent(7, 101, 379, 2, true), StatusEvent(7.2, 101, 379, 0, false),
            Frame(297, 8, [Actor(100, 0, new(10, 0, 0), dead: true, classJob: 21)], party: party),
            Frame(297, 9, [Actor(100, 0, new(10, 0, 0), classJob: 21)], party: party),
            Frame(297, 10.4, [Actor(25, 0x600, new(0, 0, -10), dead: true)], party: party),
            Frame(297, 65.9, [Actor(1, 0xF5, Vector3.Zero, combat: true, targetable: false)], party: party),
            Frame(297, 66.1, [Actor(1, 0xF5, Vector3.Zero, combat: true, targetable: false)], party: party),
            Frame(297, 67, [Actor(1, 0xF5, Vector3.Zero, combat: true, cast: Cast(1554, 4, Vector3.Zero, startedAt: 67))], party: party),
            EffectEvent(71.1, 1, 1554, 1),
            Frame(297, 71.4, [Actor(1, 0xF5, Vector3.Zero, combat: true)], party: party),
            Frame(297, 72, [Actor(23, 0x626, Vector3.Zero, cast: Cast(1671, 2, Vector3.Zero, startedAt: 72))], party: party),
            Frame(297, 74.4, [Actor(23, 0x626, Vector3.Zero)], party: party),
            Frame(297, 75, [Actor(1, 0xF5, Vector3.Zero, combat: true, targetable: false)], party: party),
            Frame(297, 76, [Actor(1, 0xF5, Vector3.Zero, combat: true, cast: Cast(1384, 3, Vector3.Zero, startedAt: 76))], party: party),
            Frame(297, 79.4, [Actor(1, 0xF5, Vector3.Zero, combat: true)], party: party),
            Frame(297, 80, [Actor(1, 0xF5, Vector3.Zero, combat: true, targetable: false)], party: party),
            Frame(297, 81, [Actor(1, 0xF5, Vector3.Zero, combat: true)], party: party),
            Frame(297, 82, [Actor(24, 0x625, new(0, 0, 10), cast: Cast(1777, 10, new(0, 0, 10), startedAt: 82))], party: party),
            Frame(297, 83, [Actor(1, 0xF5, Vector3.Zero, combat: true, targetable: false)], party: party),
            Frame(297, 84, [Actor(1, 0xF5, Vector3.Zero, combat: true)], party: party),
            Frame(297, 85, [Actor(1, 0xF5, Vector3.Zero, combat: true, dead: true)], party: party),
            Frame(297, 86, [], party: party),
        ]);
        controller.Seek(.5);
        Check((Vector3)Value(Group("song/shelter").Single(), "Position") == new Vector3(0, 0, -13) && Group("song/avoid").Length == 0, "Garuda predicted song.");
        controller.Seek(1.1); Check(Group("song/avoid").Length == 1 && previews().Length > 0, "Garuda actual cast shelter and navigation.");
        controller.Seek(2.5); Check(Group("song").Length == 0, "Garuda actual song end.");
        controller.Seek(3.1); Check((Vector3)Value(Group("song/shelter").Single(), "Position") == new Vector3(13, 0, 0), "Garuda second origin.");
        controller.Seek(6.1); Check(Group("song").Length == 0 && Group("shield").Length == 2
            && !Forbidden().Contains(20ul) && Actions().Length == 0, "Garuda phase2 shield and soft spiny priority.");
        Check(((IDictionary)Value(Encounter(), "Casts")).Contains(25ul), "Garuda dead spiny deathcast.");
        controller.Seek(7.05); Check(!Forbidden().Contains(20ul) && Actions().Length == 1
            && (uint)Value(Actions().Single(), "Action") == 7533, "Garuda two-stack status packet.");
        controller.Seek(7.25); Check(Actions().Length == 0, "Garuda status loss stops provoke.");
        controller.Seek(8.1); Check(Value(Encounter(), "Active") != null && Group("shield").Length == 2, "Garuda death retained fight.");
        controller.Seek(10.5); Check(!((IDictionary)Value(Encounter(), "Casts")).Contains(25ul), "Garuda deathcast actual finish.");
        controller.Seek(65.95); Check(Encounter().GetType().GetProperty("Casts")!.GetValue(Encounter()) is IDictionary casts && casts.Contains(22ul), "Garuda first adds60 gate.");
        controller.Seek(66.2); Check(!((IDictionary)Value(Encounter(), "Casts")).Contains(22ul), "Garuda observed hide disables friction.");
        controller.Seek(74.5); Check(Group("eye").Length == 1, "Garuda recurring eye after actual finish.");
        controller.Seek(79.5); Check(Group("eye").Length == 1, "Garuda eye must survive Shriek.");
        controller.Seek(80.1); Check(Group("eye").Length == 0, "Garuda second hide disables Eye.");
        controller.Seek(82.1); Check(((IDictionary)Value(Encounter(), "Casts")).Contains(24ul), "Garuda whirlwind combat.");
        controller.Seek(83.1); Check(((IDictionary)Value(Encounter(), "Casts")).Contains(24ul), "Garuda whirlwind survives hide.");
        controller.Seek(84.1); Check(!((IDictionary)Value(Encounter(), "Casts")).Contains(24ul), "Garuda whirlwind ends on actual reappear.");
        controller.Seek(85.1); Check(Value(Encounter(), "Active") == null && dangers() == 0 && previews().Length == 0, "Garuda boss death.");
        controller.Seek(7.05); Check(Group("shield").Length == 2 && !Forbidden().Contains(20ul) && Actions().Length == 1, "Garuda reverse Seek state.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!, "Garuda replay executed PR controls.");
        var actionControl = Script().GetType().BaseType!.GetField("twintaniaActions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        Check((DateTime)actionControl.GetType().GetField("nextAttempt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(actionControl)! == default,
            "Garuda replay executed provoke.");
        manager.SetScriptEnabled(id, false); Check(previews().Length == 0 && dangers() == 0, "Garuda owner cleanup.");
        stop();
        Console.WriteLine("PASS Garuda Extreme actual DLL both Mistral shelters, phase2 60 gate, spiny soft priority and provoke, shield, death preservation, Eye/Shriek/Whirlwind lifetime, replay isolation and Seek cleanup");
    }
}

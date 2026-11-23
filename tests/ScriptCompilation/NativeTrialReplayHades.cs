using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHades(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object a, string name) => a.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(a)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("hades/" + prefix, StringComparison.Ordinal)).ToArray();
        ulong[] Forbidden() => (ulong[])Value(Encounter(), "ForbiddenTargets");
        string Icon(double time, ulong target, uint icon) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = target, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.hades";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        load(id,
        [
            Header(847), Frame(847, 0, [Actor(1, 0x2949, c, combat: true),
                Actor(100, 0, c + Vector3.UnitX * 5), Actor(101, 0, c + Vector3.UnitZ * 10),
                Actor(10, 0x233C, c), Actor(11, 0x233C, c), Actor(12, 0x233C, c),
                Actor(20, 0x29D8, c - Vector3.UnitX * 15),
                Actor(30, 0x1EAD28, c, eventAnimation: 0x00010002),
                Actor(31, 0x1EAD29, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(847, .1, [Actor(10, 0x233C, c, cast: Cast(16728, 8, c, target: 101)),
                Actor(11, 0x233C, c, cast: Cast(16738, 8, c, target: 100)),
                Actor(12, 0x233C, c, cast: Cast(17812, 8, c, target: 101))], party: party),
            Icon(.2, 100, 139), Icon(.21, 101, 139), Icon(.22, 101, 120),
            Frame(847, .3, [Actor(100, 0, c + Vector3.UnitX * 5, tethers: [new { Id = 17, TargetId = 20UL }])], party: party),
            EffectEvent(.5, 10, 16723, 1),
            Frame(847, 1, [Actor(2, 0x294A, c, combat: true), Actor(40, 0x294B, c + Vector3.UnitX * 10),
                Actor(1, 0x2949, c, dead: true), Actor(10, 0x233C, c), Actor(11, 0x233C, c)], party: party),
            EffectEvent(1.2, 11, 16738, 2, target: 100), EffectEvent(1.3, 10, 16728, 3, target: 101),
            Frame(847, 1.6, [Actor(100, 0, c + Vector3.UnitX * 5, dead: true)], removed: [1], party: party),
            Frame(847, 1.8, [Actor(100, 0, c + Vector3.UnitX * 5)], party: party),
            Frame(847, 2, [Actor(40, 0x294B, c, dead: true), Actor(100, 0, c + Vector3.UnitX * 5)],
                removed: [1, 31], party: party),
            Frame(847, 3, [Actor(12, 0x233C, c)], removed: [30], party: party),
            Frame(847, 4, [Actor(2, 0x294A, c, dead: true)], party: party),
            Frame(847, 5, [], party: party)
        ]);
        controller.Seek(.4);
        Check(Group("faith").Length == 1 && Group("tower").Any() && Group("stack").Any()
            && Group("bait/16723").Count(a => (bool)Value(a, "Display")) == 2
            && Group("bait/16755").Any(a => ((string)Value(a, "Id")).EndsWith("/tether")),
            "Hades native initial animation/icon/tether/cast inputs missing.");
        controller.Seek(.8); Check(!Group("bait/16723").Any() && Group("bait/16745").Any(), "Hades source double-FIFO or independent captivity differs.");
        controller.Seek(1.1);
        Check(Forbidden().SequenceEqual([2UL]) && Group("gaol/goal").Single() is { } goal
            && (Vector3)Value(goal, "Position") == new Vector3(110, 0, 101)
            && ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Any(),
            "Hades live giant/gaol target restriction or goal differs.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!,
            "Hades replay executed PR action controls.");
        controller.Seek(1.5); Check(!Group("bait/16728").Any() && !Group("bait/16738").Any(), "Hades event-ended baits leaked.");
        controller.Seek(1.7); Check(Forbidden().SequenceEqual([2UL]) && Group("faith").Any(), "Hades personal death lost giant state.");
        controller.Seek(1.9); Check(Group("gaol/goal").Any() && Group("stack").Any(), "Hades revival failed after initial primary removal.");
        controller.Seek(2.5);
        Check(Forbidden().Length == 0 && !Group("gaol/goal").Any() && !Group("tower").Any()
            && !Group("bait/16755").Any() && Group("faith").Any() && Group("stack").Any(),
            "Hades independent gaol/doom/tether endings or P1 removal lost giant state.");
        controller.Seek(3.5); Check(!Group("faith").Any() && !Group("stack").Any(), "Hades faith destruction or stack finish leaked.");
        controller.Seek(4.5); Check(previews().Length == 0 && dangers() == 0, "Hades final giant death leaked.");
        controller.Seek(1.1); Check(Forbidden().SequenceEqual([2UL]) && Group("faith").Any(), "Hades reverse Seek failed to rebuild control/geometry.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Hades disabled script leaked boundary or outputs.");
        stop();
        Console.WriteLine("PASS Hades actual DLL replay giant handoff, gaol restriction/goal, independent effect/cast baits, double FIFO, raw animation, doom towers, tethers, Seek and isolation");
    }
}

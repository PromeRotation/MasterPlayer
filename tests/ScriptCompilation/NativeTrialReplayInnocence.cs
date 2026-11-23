using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunInnocence(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("innocence/" + prefix, StringComparison.Ordinal)).ToArray();
        string Icon(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 101UL, TargetId = 100UL, MarkerId = 138u, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.innocence";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        load(id,
        [
            Header(846), Frame(846, 0, [Actor(1, 0x28FB, c, combat: true),
                Actor(100, 0, c + Vector3.UnitX * 5), Actor(101, 0, c + Vector3.UnitZ * 10),
                Actor(10, 0x233C, c), Actor(11, 0x233C, c), Actor(12, 0x233C, c),
                Actor(20, 0x1EAD40, c - Vector3.UnitX * 10),
                Actor(21, 0x1EAD40, c + Vector3.UnitX * 10), Actor(30, 0x2900, c)], full: true, party: party),
            Frame(846, .1, [Actor(1, 0x28FB, c, combat: true, cast: Cast(16025, 8, c)),
                Actor(10, 0x233C, c, cast: Cast(16049, 8, c, rotation: .2f)),
                Actor(11, 0x233C, c, cast: Cast(16035, 8, c, target: 101)),
                Actor(12, 0x233C, c, cast: Cast(16051, 8, c))], party: party),
            Icon(.2), EffectEvent(.3, 12, 14588, 1, target: 101),
            Frame(846, 1, [Actor(2, 0x28FE, c, combat: true), Actor(1, 0x28FB, c, dead: true)], party: party),
            Frame(846, 2, [], removed: [20, 1], party: party),
            EffectEvent(4, 10, 16121, 2),
            EffectEvent(4.1, 2, 16070, 3, target: 100),
            Frame(846, 5, [Actor(11, 0x233C, c), Actor(12, 0x233C, c)], removed: [30], party: party),
            Frame(846, 6, [Actor(2, 0x28FE, c, dead: true), Actor(10, 0x233C, c)], party: party),
            Frame(846, 7, [], party: party)
        ]);
        controller.Seek(.8);
        Check(Group("tower").Count(a => (bool)Value(a, "Display")) == 2 && Group("gaze").Length == 3
            && Group("wave").Length == 8 && Group("line").Any() && Group("bait/16069").Any()
            && Group("bait/16035").Any() && dangers() > 0, "Innocence initial native inputs missing.");
        var first = (DateTime)Value(Group("wave")[0], "Activation");
        controller.Seek(1.5);
        Check(Group("wave").Length == 8 && !Group("bait/16069").Any()
            && (ulong)Value(Encounter(), "PrimaryId") == 1, "Innocence P1 death replaced primary or ended P2 outputs.");
        controller.Seek(2.5);
        Check(!Group("tower").Any() && Group("wave").Length == 8 && Group("line").Any(),
            "Innocence destroyed P1/tower handling differs.");
        controller.Seek(4.5);
        Check(Group("wave").Length == 8 && (DateTime)Value(Group("wave")[0], "Activation") > first && !Group("line").Any(),
            "Innocence watched instant or valid final-target line correction failed.");
        controller.Seek(5.5);
        Check(!Group("wave").Any() && !Group("bait/16035").Any(), "Innocence centered nail destruction or cast end leaked.");
        controller.Seek(6.5);
        Check(previews().Length == 0 && dangers() == 0, "Innocence final P2 death leaked.");
        controller.Seek(.8);
        Check(Group("wave").Length == 8 && Group("tower").Count(a => (bool)Value(a, "Display")) == 2,
            "Innocence reverse Seek did not rebuild original state.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Innocence disabled owner leaked boundary or outputs.");
        stop();
        Console.WriteLine("PASS Innocence actual DLL replay two-phase lifetime, wave queue actual resolution, tower destruction, gaze, dynamic baits, line correction, Seek and isolation");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunArkveld(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("ark/" + prefix, StringComparison.Ordinal)).ToArray();
        string Icon(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 102UL, TargetId = 0UL, MarkerId = 100, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.arkveld";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x48E2, c, combat: true) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: i < 2 ? 19u : 20u)));
        actors.AddRange(Enumerable.Range(10, 15).Select(i => Actor((ulong)i, 0x233C, c)));
        load(id,
        [
            Header(1300), Frame(1300, 0, actors, full: true, party: party),
            Frame(1300, .1, [
                Actor(1, 0x48E2, c, combat: true, cast: Cast(45047, 5, c)),
                Actor(10, 0x233C, c, cast: Cast(43842, 5, c + Vector3.UnitX * 15)),
                Actor(11, 0x233C, c, cast: Cast(43860, 5, c)),
                Actor(12, 0x233C, c, cast: Cast(43853, 8, c)),
                Actor(13, 0x233C, c, cast: Cast(43854, 8, c)),
                Actor(14, 0x233C, c, cast: Cast(43866, 5, c, target: 101)),
                Actor(20, 0x233C, c, cast: Cast(43844, 5, c)),
                Actor(21, 0x233C, c, cast: Cast(43845, 7, c)),
                Actor(22, 0x233C, c, cast: Cast(43846, 9, c)),
                Actor(23, 0x233C, c, cast: Cast(43847, 11, c))
            ], party: party),
            Icon(.2), EffectEvent(1, 15, 43875, 1),
            EffectEvent(3, 11, 43861, 2, new Vector3(110, 0, 105)),
            Frame(1300, 5.5, [Actor(1, 0x48E2, c, combat: true), Actor(10, 0x233C, c),
                Actor(11, 0x233C, c), Actor(14, 0x233C, c), Actor(20, 0x233C, c)], party: party),
            EffectEvent(6, 11, 43861, 3, new Vector3(115, 0, 105)),
            EffectEvent(7, 1, 43883, 4, target: 102),
            Frame(1300, 7.5, [Actor(21, 0x233C, c)], party: party),
            Frame(1300, 8.5, [Actor(12, 0x233C, c), Actor(13, 0x233C, c)], party: party),
            Frame(1300, 9.5, [Actor(22, 0x233C, c)], party: party),
            Frame(1300, 10, [Actor(100, 0, c, classJob: 19, dead: true)], party: party),
            Frame(1300, 11.5, [Actor(23, 0x233C, c)], party: party),
            Frame(1300, 12, [Actor(1, 0x48E2, c, dead: true)], party: party), Frame(1300, 13, [], party: party)
        ]);
        controller.Seek(1.3);
        Check(Group("rush").Length == 1 && Group("vengeance").Length == 1 && Group("pulse").Length == 1
            && Group("lane").Length == 1 && Group("stack").Any() && Group("spread").Any() && Group("tower").Any()
            && previews().Length > 0 && dangers() > 0, "Arkveld actual DLL initial outputs.");
        controller.Seek(2); Check(!Group("pulse").Any(), "Arkveld pulse expiry.");
        controller.Seek(3.3);
        var expected = new Vector3(110, 0, 105);
        var actual = (Vector3)Value(Group("vengeance").Single(), "Position");
        Check(Vector3.DistanceSquared(expected, actual) < .002, "Arkveld actual hit did not move pending circle.");
        controller.Seek(5.7);
        Check(!Group("lane").Any() && !Group("rush").Any() && !Group("spread").Any()
            && Group("vengeance").Any() && Group("stack").Any(), "Arkveld cast finish consumed wrong mechanics.");
        controller.Seek(8.7);
        Check(!Group("vengeance").Any() && !Group("stack").Any() && !Group("tower").Any(), "Arkveld actual hits/tower finish.");
        controller.Seek(10.5); Check(previews().Length > 0, "Arkveld personal death erased remaining ring.");
        controller.Seek(12.5); Check(previews().Length == 0 && dangers() == 0, "Arkveld primary death.");
        controller.Seek(1.3); Check(Group("lane").Any() && Group("pulse").Any(), "Arkveld reverse Seek.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Arkveld disabled script leaked outputs.");
        stop();
        Console.WriteLine("PASS Arkveld actual DLL replay charge, independent rings, caster matched exaflares, timed pulses, asymmetric lane, icon stack, spread, role towers, death, Seek and isolation");
    }
}

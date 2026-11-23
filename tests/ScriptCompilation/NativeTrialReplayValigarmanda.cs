using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunValigarmanda(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("valig/" + prefix, StringComparison.Ordinal)).ToArray();
        bool Moving() => (bool)Value(Encounter(), "RequireValigMovement");
        string Map(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 2, Param1 = 0x10, Param2 = 0x20, Timestamp = Start.AddSeconds(time) }
        });
        string Icon(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = 101UL, TargetId = 102UL, MarkerId = 344, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.valigarmanda";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x4115, c, combat: true) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: i < 2 ? 19u : 20u)));
        actors.AddRange(Enumerable.Range(10, 10).Select(i => Actor((ulong)i, 0x233C, c)));
        load(id,
        [
            Header(1195), Frame(1195, 0, actors, full: true, party: party),
            Frame(1195, .1, [
                Actor(1, 0x4115, c, combat: true, cast: Cast(36177, 5, c)),
                Actor(10, 0x233C, c, cast: Cast(36160, 8, c)),
                Actor(11, 0x233C, c, cast: Cast(36187, 8, c)),
                Actor(12, 0x233C, c, cast: Cast(36189, 10, c)),
                Actor(30, 0x4181, c), Actor(31, 0x4493, c)
            ], party: party),
            Map(.2), Icon(.3), EffectEvent(.4, 13, 26708, 1, target: 103), EffectEvent(.5, 14, 34722, 2, target: 104),
            Frame(1195, 2, [Actor(15, 0x233C, c, cast: Cast(39001, 1, c, startedAt: 2)),
                Actor(16, 0x233C, c, cast: Cast(39265, 1, c, startedAt: 2))], party: party),
            Frame(1195, 3.5, [Actor(15, 0x233C, c), Actor(16, 0x233C, c)], party: party),
            Frame(1195, 5.5, [Actor(1, 0x4115, c, combat: true)], party: party),
            EffectEvent(6, 17, 36194, 3, target: 105),
            Frame(1195, 7, [Actor(100, 0, c, classJob: 19, stackedStatus: 4150)], party: party),
            Frame(1195, 8.5, [Actor(10, 0x233C, c), Actor(11, 0x233C, c),
                Actor(1, 0x4115, c, combat: true, cast: Cast(36197, 5, c, startedAt: 8.5))], party: party),
            EffectEvent(9, 1, 36185, 4),
            Frame(1195, 10.5, [Actor(12, 0x233C, c)], party: party),
            EffectEvent(11, 17, 36168, 5),
            Frame(1195, 14, [Actor(1, 0x4115, c, combat: true)], party: party),
            EffectEvent(15, 18, 36199, 6), EffectEvent(16, 18, 36200, 7), EffectEvent(17, 18, 36198, 8),
            Frame(1195, 18, [Actor(100, 0, c, classJob: 19, dead: true)], party: party),
            Frame(1195, 19, [Actor(1, 0x4115, c, dead: true)], party: party), Frame(1195, 20, [], party: party)
        ]);
        controller.Seek(1);
        Check(Group("lightning").Length == 1 && Group("chilling").Length == 2 && !Group("coil").Any()
            && Group("tower").Any() && Group("ruinfall").Any() && Group("northern").Length == 1
            && Group("line").Count(a => (bool)Value(a, "Display")) == 2 && Group("talon").Any() && !Moving(),
            "Valigarmanda initial prediction/event channels.");
        controller.Seek(3.7);
        Check(!Group("lightning").Any() && !Group("chilling").Any() && Group("coil").Length == 1, "Valigarmanda sphere finish/suppression.");
        controller.Seek(6.5);
        Check(Moving() && Group("freezing/move").Length == 1 && !Group("line").Any(), "Valigarmanda indefinite freeze or independent line correction.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!
            && !(bool)Value((object)runtime.Setting, "RuntimeMovementHeld"), "Valigarmanda replay executed PR movement/control.");
        controller.Seek(7.5); Check(!Moving() && !Group("freezing").Any(), "Valigarmanda raw DeepFreeze failed clear.");
        controller.Seek(14.5);
        Check(!Group("talon").Any() && !Group("northern").Any() && !Group("coil").Any() && !Group("tower").Any()
            && !Group("ruinfall").Any()
            && ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Count() == 24, "Valigarmanda endings or delayed three hits.");
        controller.Seek(17.5); Check(!((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Any(), "Valigarmanda delayed hit cleanup.");
        controller.Seek(19.5); Check(previews().Length == 0 && dangers() == 0, "Valigarmanda primary death cleanup.");
        controller.Seek(6.5); Check(Moving() && Group("coil").Any(), "Valigarmanda reverse Seek.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Valigarmanda disabled script leaked outputs.");
        stop();
        Console.WriteLine("PASS Valigarmanda actual DLL replay sphere timing, suppressed coil, tower knockback, freeze movement and raw status, line correction, three delayed hits, death, Seek and isolation");
    }
}

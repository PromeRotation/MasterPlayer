using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunQueenEternal(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<object>().Single().GetType()
            .GetProperty("Script")!.GetValue(((IEnumerable)manager.Running.Values).Cast<object>().Single())!;
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!
            .Invoke(Encounter(), null)!).Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("queen/" + prefix)).ToArray();
        string Map(double time, uint index, uint state) => JsonSerializer.Serialize(new
        {
            t = time, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = (ushort)state, Param2 = (ushort)(state >> 16), Timestamp = Start.AddSeconds(time) }
        });
        string Icon(double time, ulong actor, uint icon, ulong target = 0) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(time) }
        });
        string Timeline(double time, ulong actor, uint timeline) => JsonSerializer.Serialize(new
        {
            t = time, kind = "ActionTimeline", type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
            data = new { SourceId = actor, TimelineId = timeline, Timestamp = Start.AddSeconds(time) }
        });
        string Status(double time, ulong actor, uint status, float duration) => JsonSerializer.Serialize(new
        {
            t = time, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
            data = new { TargetId = actor, StatusId = status, Duration = duration, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.queen-eternal";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x41C0, c, combat: true),
            Actor(20, 0x41C4, new(110, 0, 100)), Actor(21, 0x41C2, c) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: i < 2 ? 19u : 20u)));
        actors.AddRange(Enumerable.Range(10, 10).Select(i => Actor((ulong)i, 0x233C, c)));
        load(id,
        [
            Header(1202), Frame(1202, 0, actors, full: true, party: party),
            Frame(1202, .1, [Actor(1, 0x41C0, c, combat: true, cast: Cast(36639, 5, c, .6f)),
                Actor(10, 0x233C, c, cast: Cast(36647, 5, c)),
                Actor(11, 0x233C, c, cast: Cast(36647, 6.5, c)),
                Actor(12, 0x233C, new(115, 0, 100), cast: Cast(36622, 5, new(115, 0, 100)))], party: party),
            Map(.2, 0, 0x00020001), Map(.3, 0, 0x04000100), Map(.4, 3, 0x00200010),
            Status(.5, 100, 4130, 5), Status(.6, 101, 3815, 5),
            Icon(.7, 1, 521), Icon(.8, 1, 327, 100), Icon(.9, 1, 327, 101), Icon(.95, 102, 55),
            Timeline(1, 20, 0x11D7), Timeline(1.1, 21, 0x11D2),
            EffectEvent(2, 13, 36614, 1),
            EffectEvent(3, 13, 36639, 2), EffectEvent(4, 13, 36643, 3),
            EffectEvent(4.1, 13, 36658, 4), EffectEvent(4.2, 20, 36631, 5),
            Map(5, 2, 0x00020001), Status(5.1, 102, 3814, 1),
            Frame(1202, 5.5, [Actor(1, 0x41C0, c, combat: true), Actor(10, 0x233C, c),
                Actor(12, 0x233C, new(115, 0, 100)), Actor(102, 0, c + Vector3.UnitX * 2, classJob: 20, stackedStatus: 3814, statusExpiresAt: Start.AddSeconds(6.1))], party: party),
            StatusEvent(6.2, 102, 3814, 0, false), StatusEvent(6.3, 100, 4130, 0, false), StatusEvent(6.4, 101, 3815, 0, false),
            Frame(1202, 6.6, [Actor(11, 0x233C, c)], party: party),
            EffectEvent(7, 13, 39518, 6), EffectEvent(7.1, 13, 39519, 7),
            .. Enumerable.Range(0, 8).Select(i => EffectEvent(7.2 + i * .05, 13, 36637, (uint)(10 + i))),
            Timeline(8, 13, 0x2BFE), EffectEvent(8.1, 13, 39518, 30), EffectEvent(8.2, 13, 36634, 31),
            Frame(1202, 8.5, [Actor(100, 0, c, classJob: 19, dead: true)], party: party),
            Frame(1202, 9, [Actor(1, 0x41C0, c, dead: true)], party: party), Frame(1202, 10, [], party: party)
        ]);
        controller.Seek(1.5);
        Check(Group("besiege").Length == 4 && !Group("force").Any(a => (bool)Value(a, "Display"))
            && Group("tithe").Any() && Group("waltz").Any() && Group("ruthless").Any()
            && Group("line").Count(a => (bool)Value(a, "Display")) == 8 && Group("flare").Count(a => (bool)Value(a, "Display")) == 2
            && Group("dorito").Any() && Group("banishment").Length == 1 && dangers() > 0 && previews().Length > 0,
            "Queen initial routes/outputs.");
        controller.Seek(2.5); Check(!Group("besiege").Any() && Group("force").Count(a => (bool)Value(a, "Display")) == 2, "Besiege hit did not reveal Force.");
        controller.Seek(3.5); Check(Group("force").Count(a => (bool)Value(a, "Display")) == 1, "Force first effect.");
        controller.Seek(4.7);
        Check(!Group("force").Any() && !Group("tithe").Any() && !Group("waltz").Any()
            && (bool)Value(Encounter(), "RequireAllStop"), "Independent effects/Hold.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var control = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)control.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!
            && !(bool)Value(runtime.Setting, "RuntimeMovementHeld"), "Queen offline replay executed PR or movement control.");
        controller.Seek(5.7);
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Contains(new Vector3(100, 0, 94)), "Global gravity did not bridge split.");
        controller.Seek(6.7);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!boundary.Contains(new Vector3(100, 0, 94)) && !Group("banishment").Any()
            && !(bool)Value(Encounter(), "RequireAllStop"), "Gravity/status/ordinary cast cleanup.");
        controller.Seek(7.6); Check(!Group("line").Any() && !Group("dorito").Any()
            && Group("flare").Count(a => (bool)Value(a, "Display")) == 1, "Line eighth hit/Flare FIFO/Dorito endings.");
        controller.Seek(8.6);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(boundary.Position.Z == 105 && previews().Length > 0, "Final timeline or personal death cleared fight.");
        controller.Seek(9.5); Check(previews().Length == 0 && dangers() == 0, "Queen primary cleanup.");
        controller.Seek(1.5); Check(Group("besiege").Length == 4 && Group("waltz").Any(), "Queen reverse Seek reconstruction.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Queen disabled owner cleanup.");
        stop();
        Console.WriteLine("PASS QueenEternal actual DLL map and timeline events, Force and Besiege, time-window casts, dynamic baits, Flare target/FIFO, Dorito, Hold and gaze replay isolation, gravity arenas, personal death, Seek and cleanup");
    }
}

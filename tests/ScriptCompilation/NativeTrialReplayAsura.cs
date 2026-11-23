using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAsura(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("asura/" + prefix, StringComparison.Ordinal)).ToArray();
        string Timeline(double time, ulong source) => JsonSerializer.Serialize(new
        {
            t = time, kind = "PlayActionTimeline", type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
            data = new { SourceId = source, TimelineId = 0x11D6, Timestamp = Start.AddSeconds(time) }
        });
        string Map(double time) => JsonSerializer.Serialize(new
        {
            t = time, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = 1, Param1 = 1, Param2 = 2, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.asura";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x409B, c, combat: true) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: 19)));
        actors.AddRange(Enumerable.Range(10, 35).Select(i => Actor((ulong)i, 0x233C, c)));
        var casting = new List<object>
        {
            Actor(1, 0x409B, c, combat: true, cast: Cast(36001, 5, c)),
            Actor(10, 0x233C, c, cast: Cast(36000, 5, c, target: 101)),
            Actor(11, 0x233C, c, cast: Cast(36003, 5, c, target: 102)),
            Actor(12, 0x233C, c, cast: Cast(35997, 6, c)),
            Actor(13, 0x233C, c, cast: Cast(35989, 8, c, rotation: 1.1f)),
        };
        casting.AddRange(Enumerable.Range(20, 6).Select(i => Actor((ulong)i, 0x233C, c,
            cast: Cast((uint)(36011 + (i - 20) % 3), 2, c, (i % 2) * MathF.PI))));
        casting.AddRange(Enumerable.Range(30, 12).Select(i => Actor((ulong)i, 0x233C, c,
            cast: Cast(i % 2 == 0 ? 36020u : 36021u, i % 2 == 0 ? 4 : 6, c, i * .3f))));
        load(id,
        [
            Header(1136), Frame(1136, 0, actors, full: true, party: party), Frame(1136, .1, casting, party: party),
            Timeline(.2, 42), Timeline(.3, 43), Timeline(.4, 44),
            EffectEvent(4, 10, 36006, 1),
            Frame(1136, 5.5, [Actor(1, 0x409B, c, combat: true), Actor(10, 0x233C, c), Actor(11, 0x233C, c),
                Actor(30, 0x233C, c), Actor(32, 0x233C, c), Actor(34, 0x233C, c), Actor(36, 0x233C, c),
                Actor(38, 0x233C, c), Actor(40, 0x233C, c)], party: party),
            Map(6),
            Frame(1136, 7, [Actor(12, 0x233C, c), Actor(31, 0x233C, c), Actor(33, 0x233C, c), Actor(35, 0x233C, c),
                Actor(37, 0x233C, c), Actor(39, 0x233C, c), Actor(41, 0x233C, c),
                Actor(42, 0x233C, c, cast: Cast(35991, 1, c, startedAt: 7))], party: party),
            Frame(1136, 8.5, [Actor(42, 0x233C, c)], party: party),
            Frame(1136, 10, [Actor(100, 0, c, classJob: 19, dead: true)], party: party),
            Frame(1136, 11, [Actor(100, 0, c, classJob: 19)], party: party),
            EffectEvent(14, 1, 35977, 2), EffectEvent(15, 1, 35978, 3),
            EffectEvent(16, 1, 35979, 4), EffectEvent(17, 1, 35980, 5),
            EffectEvent(18, 1, 35981, 6), EffectEvent(19, 1, 35982, 7),
            Frame(1136, 20, [Actor(1, 0x409B, c, dead: true)], party: party), Frame(1136, 21, [], party: party)
        ]);
        controller.Seek(.8);
        var blades = Group("khadga").Where(a => (bool)Value(a, "Display")).ToArray();
        Check(blades.Length == 2 && (bool)Value(blades[0], "Navigate") && !(bool)Value(blades[1], "Navigate")
            && Group("khadga/center").Length == 1 && Group("laceration").Length == 3
            && Group("myriad").Length == 6 && Group("shrink").Length == 1 && Group("face").Length == 1
            && Group("chakra5").Length == 1 && Group("bait/36000").Any() && Group("bait/36003").Any(),
            "Asura native timelines, opposing previews, sequence groups or independent casts missing.");
        controller.Seek(5.7);
        Check(!Group("face").Any() && !Group("bait").Any() && Group("laceration").Length == 3
            && Group("shrink").Length == 1 && Group("myriad").Length == 6, "Asura cast/effect endings conflated.");
        controller.Seek(6.2);
        Check(!Group("shrink").Any(), "Asura map failed clear shrink warning.");
        dynamic arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!arena.Contains(new Vector3(100, 0, 119.2f)), "Asura map did not change native polygon.");
        controller.Seek(8.6);
        Check(!Group("laceration").Any() && !Group("myriad").Any() && !Group("chakra5").Any()
            && Group("khadga").Count(a => (bool)Value(a, "Display")) == 2, "Asura independent finish lost FIFO predictions.");
        controller.Seek(10.5);
        Check(Group("khadga").Count(a => (bool)Value(a, "Display")) == 2, "Asura personal death erased sequence.");
        controller.Seek(18.5);
        Check(Group("khadga").Count(a => (bool)Value(a, "Display")) == 1 && !Group("khadga/center").Any(), "Asura fifth hit failed FIFO.");
        controller.Seek(19.5); Check(!Group("khadga").Any(), "Asura sixth hit failed consume final preview.");
        controller.Seek(20.5); Check(previews().Length == 0 && dangers() == 0, "Asura primary death leaked outputs.");
        controller.Seek(.8); Check(Group("laceration").Length == 3 && Group("shrink").Length == 1, "Asura reverse Seek failed.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Asura disabled script retained boundary or outputs.");
        stop();
        Console.WriteLine("PASS Asura actual DLL replay six-blade FIFO, opposing preview, timeline snapshots, many faces, twelve-caster sorting, duplicate chakra mapping, shrink, death, Seek and isolation");
    }
}

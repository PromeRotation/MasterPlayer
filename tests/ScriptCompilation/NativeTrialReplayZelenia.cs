using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunZelenia(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("zelenia/" + prefix, StringComparison.Ordinal)).ToArray();
        string Map(double time, byte index, ushort p1, ushort p2) => JsonSerializer.Serialize(new
        {
            t = time, kind = "MapEffect", type = "PromeRotation.LogSystem.LogSystemMapEffectEvent",
            data = new { Position = index, Param1 = p1, Param2 = p2, Timestamp = Start.AddSeconds(time) }
        });
        string Timeline(double time, ulong source) => JsonSerializer.Serialize(new
        {
            t = time, kind = "PlayActionTimeline", type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
            data = new { SourceId = source, TimelineId = 0x11DB, Timestamp = Start.AddSeconds(time) }
        });
        string Icon(double time, ulong source, uint icon) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = source, TargetId = 0UL, MarkerId = icon, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.zelenia";
        var c = new Vector3(100, 0, 100);
        object[] party = Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        var actors = new List<object> { Actor(1, 0x47C3, c, combat: true) };
        actors.AddRange(Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, c + Vector3.UnitX * i, classJob: 19)));
        actors.AddRange(Enumerable.Range(10, 10).Select(i => Actor((ulong)i, 0x233C, c)));
        actors.AddRange(Enumerable.Range(20, 3).Select(i => Actor((ulong)i, 0x47C4, c)));
        load(id,
        [
            Header(1270), Frame(1270, 0, actors, full: true, party: party),
            Map(.1, 4, 0x100, 0x40), Map(.11, 9, 0x100, 0x40),
            Frame(1270, .2, [
                Actor(10, 0x233C, c, cast: Cast(43102, 4, new(100, 0, 87))),
                Actor(11, 0x233C, c, cast: Cast(43084, 5, c)),
                Actor(12, 0x233C, c, cast: Cast(43085, 6, c)),
                Actor(13, 0x233C, c, cast: Cast(43092, 5, c)),
                Actor(14, 0x233C, c, cast: Cast(43129, 5, c, target: 101)),
                Actor(15, 0x233C, c, cast: Cast(43479, 5, c))
            ], party: party),
            Icon(.3, 102, 581), Icon(.4, 103, 590),
            Timeline(.5, 20), Timeline(.6, 21), Timeline(.7, 22),
            EffectEvent(2, 10, 43057, 1), EffectEvent(2.5, 11, 43057, 2),
            EffectEvent(3, 10, 43063, 3), EffectEvent(3.1, 10, 43063, 4),
            EffectEvent(3.2, 10, 43063, 5), EffectEvent(3.3, 10, 43063, 6),
            Frame(1270, 4.5, [Actor(10, 0x233C, c)], party: party),
            Frame(1270, 5.5, [Actor(11, 0x233C, c), Actor(13, 0x233C, c),
                Actor(14, 0x233C, c), Actor(15, 0x233C, c)], party: party),
            Map(5.6, 1, 1, 2),
            Frame(1270, 6.5, [Actor(12, 0x233C, c),
                Actor(16, 0x233C, c, cast: Cast(43074, 1, c, startedAt: 6.5))], party: party),
            EffectEvent(7, 1, 43090, 7),
            Frame(1270, 8, [Actor(16, 0x233C, c)], party: party),
            Frame(1270, 9, [Actor(100, 0, c, classJob: 19, dead: true)], party: party),
            Frame(1270, 10, [Actor(1, 0x47C3, c, dead: true)], party: party), Frame(1270, 11, [], party: party)
        ]);
        controller.Seek(1);
        Check(Group("thunder3").Length == 3 && Group("thunder4").Length == 1
            && Group("ascension").Length == 2 && Group("spread").Any() && Group("stack").Any()
            && Group("specter").Any() && Group("knockback/join").Length == 1 && Group("shrink").Length == 1
            && dangers() > 0 && previews().Length > 0, "Zelenia actual DLL initial outputs missing.");
        controller.Seek(2.7);
        Check(!Group("spread").Any() && Group("shock").Length == 2, "Zelenia spread effect/circles.");
        controller.Seek(3.5); Check(!Group("shock").Any(), "Zelenia repeated-hit circle cleanup.");
        controller.Seek(4.7); Check(!Group("thunder3").Any(), "Zelenia source end retained conduction.");
        controller.Seek(5.8);
        Check(Group("thunder4").Length == 1 && !Group("specter").Any() && !Group("knockback").Any()
            && !Group("shrink").Any(), "Zelenia independent finishes/map cleanup.");
        dynamic arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!arena.Contains(c) && arena.Contains(c + Vector3.UnitX * 8), "Zelenia native donut boundary.");
        controller.Seek(8.5);
        Check(!Group("thunder4").Any() && !Group("stack").Any() && Group("ascension").Length == 2, "Zelenia final stack/FIFO consumption.");
        controller.Seek(9.5); Check(Group("ascension").Length == 2, "Zelenia personal death cleared sequence.");
        controller.Seek(10.5); Check(previews().Length == 0 && dangers() == 0, "Zelenia boss death cleanup.");
        controller.Seek(1); Check(Group("thunder3").Length == 3 && Group("ascension").Length == 2, "Zelenia reverse Seek.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Zelenia disabled script leaked outputs.");
        stop();
        Console.WriteLine("PASS Zelenia actual DLL replay connected sectors, independent FIFO, shock repeated hits, stack, timeline, donut, knockback, death, Seek and isolation");
    }
}

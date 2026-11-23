using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunUnderkeep(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.underkeep"; const uint territory = 1266;
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object Value(object a, string name) => a.GetType().GetProperty(name)!.GetValue(a)!;
        object[] Group(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("under/" + key)).ToArray();
        string Timeline(double t, ulong source, uint timeline) => JsonSerializer.Serialize(new { t, kind = "ActionTimeline",
            type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent", data = new { SourceId = source, TimelineId = timeline, Timestamp = Start.AddSeconds(t) } });
        string Tether(double t, ulong source, ulong target, uint tether, bool present = true) => JsonSerializer.Serialize(new { t,
            kind = present ? "Tether" : "TetherCancel", type = present ? "PromeRotation.LogSystem.LogSystemTetherEvent" : "PromeRotation.LogSystem.LogSystemTetherCancelEvent",
            data = new { SourceId = source, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(t) } });
        string Icon(double t, ulong target, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent", data = new { SourceId = 1UL, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        var c = new Vector3(-248, 0, 122);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x4791, c, combat: true), Actor(100, 0, c)], full: true),
            Frame(territory, .1, [Actor(10, 0x4792, c)]),
            CastEvent(6, 10, 42545, 2, c),
            Frame(territory, 6.1, [Actor(10, 0x4792, c, cast: Cast(42545, 2, c, startedAt: 6))]),
            Frame(territory, 8.5, [Actor(10, 0x4792, c)]),
        ]);
        controller.Seek(.5); Check(Group("sphere").Length == 1 && dangers() == 1 && previews().Length == 1, "Underkeep sphere created.");
        Check(Math.Abs(((DateTime)Value(Group("sphere")[0], "Activation") - Start).TotalSeconds - 8) < .00001, "Underkeep sphere creation clock.");
        controller.Seek(7); Check(Group("sphere").Length == 1, "Underkeep sphere cast duplicate.");
        controller.Seek(9); Check(previews().Length == 0 && dangers() == 0, "Underkeep sphere finish.");
        controller.Seek(.5); Check(Group("sphere").Length == 1, "Underkeep sphere Seek."); stop();

        c = new(0, 0, -182);
        var lines = new List<string>
        {
            Header(territory), Frame(territory, 0, new[] { Actor(1, 0x47AD, c, combat: true), Actor(100, 0, c + Vector3.UnitZ * 5) }
                .Concat(Enumerable.Range(20, 6).Select(i => Actor((ulong)i, 0x47AE, c + Vector3.UnitX * (i - 20), modelState: 6))).ToArray(), full: true),
        };
        for (var n = 5; n >= 0; --n)
        {
            var source = (ulong)(20 + n);
            lines.Add(Tether(.1, source, 21, 313)); lines.Add(Tether(.1, source, source == 20 ? 21u : 20u, 327));
        }
        lines.Add(EffectEvent(1, 20, 43163, 1));
        lines.Add(Tether(1.1, 25, 20, 327, false));
        lines.Add(Icon(2, 100, 591));
        lines.Add(EffectEvent(7.2, 10, 42575, 2));
        lines.Add(CastEvent(8, 10, 42567, .5, c));
        lines.Add(Frame(territory, 8.1, [Actor(10, 0x233C, c, cast: Cast(42567, .5, c, startedAt: 8))]));
        lines.Add(Frame(territory, 9, [Actor(10, 0x233C, c)]));
        load(id, lines);
        controller.Seek(.5); Check(Group("sector").Length == 0, "Underkeep graph premature.");
        controller.Seek(1.5); Check(Group("sector").Length == 1 && previews().Length == 1 && dangers() == 1, "Underkeep sector graph.");
        controller.Seek(3); Check(Group("bait").Length > 0, "Underkeep icon bait.");
        controller.Seek(9.5); Check(previews().Length == 0 && dangers() == 0, "Underkeep sector and bait resolve.");
        controller.Seek(1.5); Check(Group("sector").Length == 1, "Underkeep sector Seek."); stop();

        c = new(0, 0, -331);
        load(id,
        [
            Header(territory), Frame(territory, 0, [Actor(1, 0x478E, c, combat: true), Actor(100, 0, c),
                Actor(101, 0, c + Vector3.UnitX * 5), Actor(20, 0x4789, c), Actor(21, 0x4789, c + Vector3.UnitX * 3),
                Actor(22, 0x4789, c + Vector3.UnitX * 6), Actor(24, 0x47B5, c + Vector3.UnitX * 12)], full: true,
                party: [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }]),
            Timeline(.1, 22, 0x11DA), Timeline(.1, 20, 0x11DA), Timeline(.1, 21, 0x11DA),
            Timeline(.2, 20, 0x11D5), Timeline(.2, 21, 0x11D5),
            Tether(1, 24, 20, 282), Icon(2, 101, 525), Icon(2.1, 100, 586),
            EffectEvent(8, 1, 42540, 1), EffectEvent(10, 10, 43261, 2),
        ]);
        controller.Seek(.5); Check(Group("cross").Length == 1 && dangers() == 1, "Underkeep cross first-only.");
        controller.Seek(1.5);
        Check(Math.Abs(((DateTime)Value(Group("cross")[0], "Activation") - Start).TotalSeconds - 1.6) < .00001, "Underkeep teleport clock.");
        controller.Seek(3); Check(Group("line").Length > 0 && Group("bait").Length > 0 && previews().Length == 3, "Underkeep lines bait dual views.");
        controller.Seek(10.5); Check(Group("line").Length == 0 && Group("bait").Length == 0 && previews().Length == 1, "Underkeep effect consumption.");
        controller.Seek(3); Check(Group("line").Length > 0 && Group("bait").Length > 0, "Underkeep final boss Seek."); stop();
        Console.WriteLine("PASS: native Underkeep three encounters synthetic replay sphere/clone graph/sector/icon baits/line stack/cross teleport/Seek");
    }
}

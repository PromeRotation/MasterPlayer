using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunUltimaExtreme(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.trial.ultima-extreme";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("ultima/" + prefix)).ToArray();
        void CheckPreviews(int firstCast)
        {
            var ids = previews().Select(p => (string)Value(p, "Id")).ToArray();
            var expected = new[] { "enemy/marker/1" }.Concat(Enumerable.Range(firstCast, 17 - firstCast).Select(i => $"cast/{i}")).ToArray();
            Check(ids.Length == expected.Length && expected.All(suffix => ids.Count(id => id.EndsWith(suffix, StringComparison.Ordinal)) == 1),
                "Ultima exact mechanism previews and boss marker.");
        }
        string Timeline(double t, ulong source, uint timeline) => JsonSerializer.Serialize(new
        {
            t, kind = "PlayActionTimeline", type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
            data = new { SourceId = source, TimelineId = timeline, Timestamp = Start.AddSeconds(t) },
        });
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        uint[] actions = [1521, 1522, 1524, 1507, 1508, 1509, 1526];
        var records = new List<string>
        {
            Header(348), Frame(348, 0, [Actor(1, 0x90F, Vector3.Zero, combat: true), Actor(100, 0, Vector3.UnitX * 10),
                .. actions.Select((a, i) => Actor((ulong)(10 + i), 0x1B2, Vector3.Zero, cast: Cast(a, 3 + i, Vector3.Zero, .7f, startedAt: 0)))],
                full: true, party: party),
            Frame(348, 1, [Actor(100, 0, Vector3.UnitX * 10, dead: true)], party: party),
        };
        for (var i = 0; i < actions.Length; ++i)
            records.Add(Frame(348, 3.4 + i, [Actor((ulong)(10 + i), 0x1B2, Vector3.Zero)], party: party));
        records.Add(Frame(348, 12, [Actor(1, 0x90F, Vector3.Zero, combat: true, dead: true)], party: party));
        records.Add(Frame(348, 13, [], party: party));
        load(id, records);
        controller.Seek(.1); Check(dangers() == 7, "Ultima native source output."); CheckPreviews(10);
        controller.Seek(1.1); Check(dangers() == 7, "Ultima personal death reset.");
        for (var i = 0; i < actions.Length; ++i)
        {
            controller.Seek(3.5 + i); Check(dangers() == 6 - i, "Ultima independent helper finishes."); CheckPreviews(11 + i);
        }
        controller.Seek(12.1); Check(dangers() == 0 && Value(Encounter(), "Active") == null, "Ultima HP0.");
        controller.Seek(.1); Check(dangers() == 7, "Ultima reverse Seek.");
        stop();

        load(id,
        [
            Header(348), Frame(348, 0, [Actor(1, 0x90F, Vector3.Zero, combat: true),
                Actor(100, 0, Vector3.UnitZ * 10), Actor(20, 0x912, new(0, 0, -15)),
                Actor(21, 0x910, new(0, 0, -18)), Actor(22, 0x1B2, new(-10, 0, 0))], full: true, party: party),
            Timeline(.1, 20, 0x588), Timeline(.2, 21, 0x8D), Timeline(.3, 22, 0x449),
            Frame(348, 1, [Actor(20, 0x912, new(3, 0, -14), rotation: .7f), Actor(21, 0x910, new(0, 0, -10), rotation: 1.2f)], party: party),
            EffectEvent(2, 1, 1518, 1),
            Frame(348, 3, [Actor(21, 0x910, new(0, 0, -10), cast: Cast(1525, 3, new(0, 0, -10), 1.2f, startedAt: 3))], party: party),
            Frame(348, 6.4, [Actor(21, 0x910, new(0, 0, -10))], party: party),
            EffectEvent(6.5, 22, 1515, 2),
            Frame(348, 7, [Actor(20, 0x912, new(3, 0, -14), cast: Cast(1517, 3, new(3, 0, -14), .7f, startedAt: 7))], party: party),
            Frame(348, 10.4, [Actor(20, 0x912, new(3, 0, -14))], party: party), Frame(348, 11, [], party: party),
        ]);
        controller.Seek(.5); Check(Group("mistral").Length == 0 && Group("cyclone").Length == 1 && Group("freefire").Length == 1, "Ultima timeline stagger.");
        controller.Seek(1.1);
        Check((float)Value(Group("mistral/display").Single(), "Rotation") == .7f
            && (float)Value(Group("cyclone").Single(), "Rotation") == 1.2f && Group("mistral/knockback").Length > 0, "Ultima live turn and burst preview.");
        controller.Seek(2.1); Check(Group("mistral/knockback").Length == 0 && Group("mistral/avoid").Length == 1, "Ultima burst phase.");
        controller.Seek(6.6); Check(Group("cyclone").Length == 0 && Group("freefire").Length == 0 && Group("mistral").Length > 0, "Ultima timeline actor effects independent.");
        controller.Seek(10.5); Check(Group("mistral").Length == 0, "Ultima actual Mistral finish.");
        controller.Seek(1.1); Check(Group("cyclone").Length == 1, "Ultima timeline reverse Seek.");
        stop();

        load(id,
        [
            Header(348), Frame(348, 0, [Actor(1, 0x90F, Vector3.Zero, combat: true), Actor(100, 0, Vector3.UnitZ * 10, classJob: 20)], full: true, party: party),
            Frame(348, .1, [Actor(1, 0x90F, Vector3.Zero, combat: true, cast: Cast(1511, 4, Vector3.Zero))], party: party),
            EffectEvent(4.4, 1, 1511, 1), Frame(348, 4.5, [Actor(1, 0x90F, Vector3.Zero, combat: true)], party: party),
            Frame(348, 5, [Actor(20, 0x914, new(0, 0, 18)), Actor(21, 0x914, new(18, 0, 0)),
                Actor(22, 0x914, new(0, 0, -18)), Actor(23, 0x914, new(-18, 0, 0))], party: party),
            EffectEvent(6, 20, 1512, 2), Frame(348, 6.1, [], removed: [20], party: party),
            EffectEvent(7, 21, 1512, 3), Frame(348, 7.1, [], removed: [21], party: party),
            Frame(348, 8, [Actor(1, 0x90F, Vector3.Zero, combat: true, targetId: 100),
                Actor(100, 0, new(0, 0, -2), classJob: 21, stackedStatus: 369, statusParam: 4)], party: party),
            Frame(348, 9, [], party: party),
        ]);
        controller.Seek(1); Check(Group("boom/south").Length == 1, "Ultima boom preposition.");
        controller.Seek(5.1); Check(Group("boom/orb").Length == 4 && Group("boom/pop").Length == 1, "Ultima exact orb count.");
        controller.Seek(6.2); Check((Vector3)Value(Group("boom/pop").Single(), "Position") == new Vector3(18, 0, 1), "Ultima pop queue order.");
        controller.Seek(7.2); Check(Group("boom/pop").Length == 0, "Ultima completed pop queue.");
        controller.Seek(8.1);
        Check(((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [new Vector3(0, 0, -2)])!).Cast<string>().Any(h => h.Contains("交接仇恨")),
            "Ultima four-stack replay state.");
        var actionControl = Script().GetType().BaseType!.GetField("twintaniaActions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        Check((DateTime)actionControl.GetType().GetField("nextAttempt", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(actionControl)! == default,
            "Ultima offline replay executed PR actions.");
        controller.Seek(5.1); Check(Group("boom/orb").Length == 4, "Ultima orb Seek.");
        manager.SetScriptEnabled(id, false); Check(dangers() == 0 && previews().Length == 0, "Ultima owner cleanup.");
        stop();
        Console.WriteLine("PASS Ultima Extreme actual DLL helpers, personal death, live primal timeline turns, independent finish/effect clocks, boom exact counts/pop queue, tank stacks, replay action isolation and Seek cleanup");
    }
}

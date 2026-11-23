using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunDarkness(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.alliance.darkness";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("darkness/" + prefix)).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        foreach (var (boss, c, actions) in new (uint, Vector3, uint[])[]
        {
            (0xE00, new(-145, 0, 300), [3275, 3277]),
            (0xDFC, new(200, 0, 180), [3295, 3285, 3284, 3288, 3286]),
            (0xDF6, new(0, 0, -197), [3246, 3241, 3249, 3250, 3251, 3253, 3247]),
            (0xCFA, new(-300, 0, -400), [3297]),
        })
        {
            var records = new List<string>
            {
                Header(151),
                Frame(151, 0, [Actor(1, boss, c, combat: true), Actor(100, 0, c),
                    .. actions.Select((a, i) => Actor((ulong)(10 + i), 0x1B2, c,
                        cast: Cast(a, 3 + i, c + Vector3.UnitZ * 10, .7f, startedAt: 0)))], full: true, party: party),
                Frame(151, 1, [Actor(100, 0, c, dead: true)], party: party),
            };
            for (var i = 0; i < actions.Length; ++i)
                records.Add(Frame(151, 3.4 + i, [Actor((ulong)(10 + i), 0x1B2, c)], party: party));
            records.Add(Frame(151, 12, [Actor(1, boss, c, dead: true)], party: party));
            records.Add(Frame(151, 13, [], party: party));
            load(id, records);
            controller.Seek(.1); Check(dangers() == actions.Length && previews().Length == actions.Length, $"Darkness {boss:X} helper output.");
            controller.Seek(1.1); Check(dangers() == actions.Length, "Darkness party death.");
            for (var i = 0; i < actions.Length; ++i)
            {
                controller.Seek(3.5 + i);
                Check(dangers() == actions.Length - i - 1 && previews().Length == actions.Length - i - 1, "Darkness independent finish.");
            }
            controller.Seek(12.1); Check(Value(Encounter(), "Active") == null && dangers() == 0, "Darkness primary completion.");
            controller.Seek(.1); Check(dangers() == actions.Length, "Darkness reverse Seek.");
            stop(); Check(dangers() == 0 && previews().Length == 0, "Darkness owner cleanup.");
        }
        var center = new Vector3(-145, 0, 300);
        load(id,
        [
            Header(151), Frame(151, 0, [Actor(1, 0xE00, center, combat: true), Actor(100, 0, center + Vector3.UnitZ * 10, rotation: MathF.PI)], full: true, party: party),
            Frame(151, .1, [Actor(1, 0xE00, center, combat: true, cast: Cast(3281, 3, center))], party: party),
            Frame(151, 1, [Actor(100, 0, center + Vector3.UnitZ * 10, rotation: 0)], party: party),
            Frame(151, 3.5, [Actor(1, 0xE00, center, combat: true)], party: party), Frame(151, 4, [], party: party),
        ]);
        controller.Seek(.5);
        Check(Group("gaze").Length == 3 && !(bool)Value(Group("gaze").Single(a => ((string)Value(a, "Id")).EndsWith("/eye")), "Safe"),
            "Mortal gaze eye unsafe color.");
        controller.Seek(2.9);
        var engine = Encounter();
        Check((bool)Value(Group("gaze").Single(a => ((string)Value(a, "Id")).EndsWith("/eye")), "Safe")
            && engine.GetType().GetMethod("GazeFacing")!.Invoke(engine, [center + Vector3.UnitZ * 10, MathF.PI]) != null, "Mortal gaze facing.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(facing)!
            && !(bool)Value(runtime.Setting, "RuntimeMovementHeld"), "Darkness offline replay executed PR control.");
        controller.Seek(3.6); Check(Group("gaze").Length == 0 && previews().Length == 0, "Mortal gaze finish.");
        stop();

        center = new(-300, 0, -400);
        string Icon(double t, ulong target) => JsonSerializer.Serialize(new
        {
            t, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = target, TargetId = target, MarkerId = 197u, Timestamp = Start.AddSeconds(t) },
        });
        load(id,
        [
            Header(151), Frame(151, 0, [Actor(1, 0xCFA, center, combat: true), Actor(100, 0, center)], full: true, party: party),
            Icon(.1, 100),
            Frame(151, .2, [Actor(1, 0xCFA, center, combat: true, cast: Cast(3298, 3.5, center, startedAt: .2))], party: party),
            Frame(151, 1, [Actor(100, 0, center + Vector3.UnitX * 20)], party: party),
            EffectEvent(4, 1, 3298, 1, center),
            Frame(151, 4.1, [Actor(1, 0xCFA, center, combat: true)], party: party),
            EffectEvent(5.5, 10, 3299, 2, center + Vector3.UnitX * 4),
            EffectEvent(7, 11, 3299, 3, center + Vector3.UnitX * 8),
            EffectEvent(8.5, 12, 3299, 4, center + Vector3.UnitX * 12),
            EffectEvent(10, 13, 3299, 5, center + Vector3.UnitX * 16),
            Frame(151, 11, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Group("chase").Count(a => (bool)Value(a, "Display")) == 2 && dangers() == 1, "Chase display and direction line.");
        controller.Seek(4.2);
        Check(((string)Value(Group("chase").Single(a => ((string)Value(a, "Id")).EndsWith("/display")), "Name")).Contains("4")
            && dangers() == 1, "Chase first effect or actual cast finish cleared sequence.");
        controller.Seek(8.6);
        Check(((string)Value(Group("chase").Single(a => ((string)Value(a, "Id")).EndsWith("/display")), "Name")).Contains("1"), "Chase fourth effect.");
        controller.Seek(10.1); Check(Group("chase").Length == 0 && dangers() == 0 && previews().Length == 0, "Chase fifth effect.");
        controller.Seek(4.2); Check(dangers() == 1, "Chase reverse Seek.");
        manager.SetScriptEnabled(id, false); Check(dangers() == 0 && previews().Length == 0, "Chase disable owner.");
        stop();
        Console.WriteLine("PASS Darkness actual DLL four bosses, independent helpers and charges, Mortal gaze drawing/facing and replay isolation, exact five-effect chase, Seek and owner cleanup");
    }
}

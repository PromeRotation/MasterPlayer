using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTitania(object managerObject, object runtimeObject, object controllerObject,
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
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("titania/" + prefix, StringComparison.Ordinal)).ToArray();
        string Icon(double time, ulong actor, uint icon, ulong target) => JsonSerializer.Serialize(new
        {
            t = time, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = target, MarkerId = icon, Timestamp = Start.AddSeconds(time) }
        });
        const string id = "masterplayer.trial.titania";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        load(id,
        [
            Header(845), Frame(845, 0, [Actor(1, 0x27ED, c, combat: true),
                Actor(100, 0, c + Vector3.UnitX * 7), Actor(101, 0, c + Vector3.UnitZ * 10),
                Actor(10, 0x233C, c), Actor(11, 0x233C, c), Actor(12, 0x233C, c),
                Actor(20, 0x1EABE5, c - Vector3.UnitX * 10),
                Actor(30, 0x1EABE4, c, eventAnimation: 0x00010002)], full: true, party: party),
            Frame(845, .1, [Actor(1, 0x27ED, c, combat: true, cast: Cast(15687, 8, c)),
                Actor(10, 0x233C, c, cast: Cast(16927, 8, c, target: 101)),
                Actor(11, 0x233C, c, cast: Cast(15789, 8, c, target: 101))], party: party),
            Icon(.2, 101, 230, 100), Icon(.3, 101, 62, 101),
            Frame(845, 1, [Actor(30, 0x1EABE4, c, eventAnimation: 0x00100020),
                Actor(21, 0x1E9E3C, c + Vector3.UnitX * 10)], party: party),
            Frame(845, 2, [Actor(1, 0x27ED, c, targetable: false),
                Actor(12, 0x233C, c, cast: Cast(15705, 8, c, startedAt: 2))], party: party),
            Frame(845, 3, [Actor(100, 0, c + Vector3.UnitX * 7, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(30)),
                Actor(10, 0x233C, c)], party: party),
            EffectEvent(3.1, 1, 15707, 1, target: 101), EffectEvent(3.2, 11, 17267, 2, target: 101),
            Frame(845, 4, [Actor(11, 0x233C, c), Actor(12, 0x233C, c), Actor(30, 0x1EABE4, c, dead: true)], party: party),
            Frame(845, 5, [Actor(1, 0x27ED, c, dead: true)], party: party), Frame(845, 6, [], party: party)
        ]);
        controller.Seek(.8);
        Check(Group("tower").Any() && Group("water").Any() && Group("roots").Length == 2
            && Group("mark/16927").Any() && Group("mark/17267").Any()
            && Group("bait/15707").Any() && Group("bait/15789").Any() && dangers() > 0,
            "Titania initial native mechanics or input routing missing.");
        controller.Seek(1.5);
        Check(!Group("tower").Any() && Group("roots").Length == 4
            && Group("water").Single(a => (bool)Value(a, "Display")) is { } burst
            && ((Vector3)Value(burst, "Position")).X == 110, "Titania burst precedence or root growth append differs.");
        controller.Seek(2.5);
        Check(!Group("water").Any() && Group("knockback/join").Any() && Group("roots").Length == 4,
            "Titania actual fire end or immediate safety donut missing.");
        controller.Seek(3.5);
        Check(!Group("knockback").Any() && !Group("mark/16927").Any() && !Group("mark/17267").Any()
            && !Group("bait/15707").Any() && Group("bait/15789").Any(),
            "Titania immunity or independent mark/bait resolution differs.");
        controller.Seek(4.5);
        Check(!Group("roots").Any() && !Group("bait").Any() && dangers() == 0, "Titania actual endings leaked.");
        controller.Seek(5.5); Check(previews().Length == 0 && dangers() == 0, "Titania primary death leaked.");
        controller.Seek(.8); Check(Group("tower").Any() && Group("roots").Length == 2, "Titania reverse Seek retained later state.");
        manager.SetScriptEnabled(id, false);
        Check(previews().Length == 0 && dangers() == 0 && runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null,
            "Titania disable leaked boundary, drawing or navigation.");
        stop();
        Console.WriteLine("PASS Titania actual DLL replay water towers/burst precedence, callback fire clock, root growth append, dynamic icon/cast baits, separate marks, immune KB safety ring, Seek/death/isolation");
    }
}

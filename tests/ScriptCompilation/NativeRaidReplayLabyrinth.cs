using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunLabyrinth(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.alliance.labyrinth";
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Group(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        bool Active() => Value(Encounter(), "Active") != null;
        string BattleEnd(double t) => JsonSerializer.Serialize(new
        {
            t, kind = "BattleEnded", type = "PromeRotation.LogSystem.LogSystemBattleEndedEvent",
            data = new { Timestamp = Start.AddSeconds(t) },
        });
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var c = new Vector3(-450, 0, 30);
        load(id,
        [
            Header(174), Frame(174, 0, [Actor(1, 0x92B, c, combat: true), Actor(100, 0, c),
                Actor(10, 0x92C, c, cast: Cast(749, 3, c, startedAt: 0))], full: true, party: party),
            Frame(174, 1, [Actor(1, 0x92B, c, dead: true, targetable: false)], party: party),
            BattleEnd(1.1),
            Frame(174, 2, [Actor(1, 0x92B, c, combat: true)], party: party),
            Frame(174, 4, [Actor(10, 0x92C, c)], party: party),
            Frame(174, 5, [], removed: [1], party: party), Frame(174, 6, [], party: party),
        ]);
        controller.Seek(.1); Check(Active() && dangers() == 1 && previews().Length == 1, "Labyrinth Bone start.");
        controller.Seek(1.2); Check(Active() && dangers() == 1 && previews().Length == 1, "Bone resurrection/BattleEnded reset.");
        controller.Seek(2.1); Check(Active() && dangers() == 1, "Bone revive reset.");
        controller.Seek(4.1); Check(Active() && dangers() == 0, "Bone helper finish.");
        controller.Seek(5.1); Check(!Active() && previews().Length == 0, "Bone destroyed completion.");
        controller.Seek(1.2); Check(Active() && dangers() == 1, "Bone reverse Seek.");
        stop();

        c = new(440, 0, 280);
        load(id,
        [
            Header(174), Frame(174, 0, [Actor(1, 0x92E, c, combat: true), Actor(100, 0, c),
                Actor(10, 0x92F, c, cast: Cast(760, 3, c, startedAt: 0)),
                Actor(11, 0x930, c, cast: Cast(1829, 4, c, startedAt: 0)),
                Actor(12, 0x931, c, cast: Cast(1759, 5, c, startedAt: 0))], full: true, party: party),
            Frame(174, 1, [Actor(100, 0, c, dead: true)], party: party),
            Frame(174, 3.4, [Actor(10, 0x92F, c)], party: party),
            Frame(174, 4.4, [Actor(11, 0x930, c)], party: party),
            Frame(174, 5.4, [Actor(12, 0x931, c)], party: party),
            Frame(174, 6, [Actor(1, 0x92E, c, dead: true)], party: party), Frame(174, 7, [], party: party),
        ]);
        controller.Seek(.1); Check(dangers() == 3 && previews().Length == 3, "Thanatos three independent sources.");
        controller.Seek(1.1); Check(Active() && dangers() == 3, "Thanatos personal death.");
        controller.Seek(3.5); Check(dangers() == 2 && previews().Length == 2, "Thanatos finish1.");
        controller.Seek(4.5); Check(dangers() == 1, "Thanatos finish2.");
        controller.Seek(6.1); Check(!Active() && dangers() == 0, "Thanatos death.");
        stop();

        c = new(-110, 0, -380);
        load(id,
        [
            Header(174), Frame(174, 0, [Actor(1, 0x932, c, combat: true), Actor(100, 0, c + new Vector3(10, 0, 4)),
                Actor(20, 0x936, c + new Vector3(10, 0, 0), hitboxRadius: 2.4f)], full: true, party: party),
            Frame(174, .1, [Actor(1, 0x932, c, combat: true, cast: Cast(1756, 10, c))], party: party),
            EffectEvent(.2, 1, 1752, 1, target: 100),
            Frame(174, .3, [Actor(21, 0x98C, c + new Vector3(10, 0, 4), eventState: 7)], party: party),
            Frame(174, .4, [Actor(21, 0x98C, c + new Vector3(10, 0, 4))], party: party),
            Frame(174, .5, [Actor(20, 0x936, c + new Vector3(0, 0, 10), hitboxRadius: 2.4f, dead: true)], party: party),
            Frame(174, 11, [Actor(1, 0x932, c, combat: true)], party: party),
            Frame(174, 12, [Actor(1, 0x932, c, dead: true)], party: party), Frame(174, 13, [], party: party),
        ]);
        controller.Seek(.35); Check(Group("labyrinth/meteor").Length == 2 && Group("trial/voidzone/event").Length == 1
            && dangers() == 2 && previews().Length == 2, "Behemoth meteor and retired voidzone.");
        controller.Seek(.6); Check(Group("trial/voidzone/event").Length == 0 && Group("trial/voidzone/live").Length == 1, "Behemoth voidzone transition.");
        var safe = Group("labyrinth/meteor").Single(a => (bool)Value(a, "Safe"));
        dynamic geometry = Value(safe, "Shape").GetType().GetMethod("Resolve")!.Invoke(Value(safe, "Shape"), [c, 0f, null])!;
        Check((bool)geometry.Contains(c + new Vector3(15, 0, 0), 0f)
            && !(bool)geometry.Contains(c + new Vector3(0, 0, 15), 0f), "Behemoth frozen shelter.");
        controller.Seek(11.1); Check(Group("labyrinth/meteor").Length == 0 && dangers() == 1, "Meteor finish consumed persistent voidzone.");
        controller.Seek(12.1); Check(!Active() && dangers() == 0, "Behemoth death owner cleanup.");
        controller.Seek(.35); Check(Group("trial/voidzone/event").Length == 1, "Behemoth reverse Seek.");
        stop();

        c = new(-110, 0, 180);
        load(id,
        [
            Header(174), Frame(174, 0, [Actor(1, 0x938, c, combat: true), Actor(100, 0, c),
                .. Enumerable.Range(0, 4).Select(i => Actor((ulong)(10 + i), 0x939, c,
                    cast: Cast((uint)(1741 + i), 3 + i, c, startedAt: 0)))], full: true, party: party),
            Frame(174, 2, [Actor(11, 0x939, c)], party: party),
            Frame(174, 3.4, [Actor(10, 0x939, c)], party: party),
            Frame(174, 5.4, [Actor(12, 0x939, c)], party: party),
            Frame(174, 6.4, [Actor(13, 0x939, c)], party: party), Frame(174, 7, [], party: party),
        ]);
        controller.Seek(.1); Check(dangers() == 4 && previews().Length == 4, "Phlegethon four helper queue.");
        controller.Seek(2.1); Check(dangers() == 3 && previews().Length == 3, "Phlegethon independent cancellation.");
        controller.Seek(5.5); Check(dangers() == 1, "Phlegethon ordered continuation.");
        controller.Seek(6.5); Check(dangers() == 0 && previews().Length == 0, "Phlegethon final helper cleanup.");
        controller.Seek(.1); Check(dangers() == 4, "Phlegethon reverse Seek.");
        manager.SetScriptEnabled(id, false);
        Check(dangers() == 0 && previews().Length == 0, "Labyrinth disable owner leak.");
        stop();
        Console.WriteLine("PASS Labyrinth actual DLL four bosses, Bone resurrection and BattleEnded, independent helper finishes, party death, frozen Meteor shelters, Charybdis effect clock and state7, cancellation, Seek and owner cleanup");
    }
}

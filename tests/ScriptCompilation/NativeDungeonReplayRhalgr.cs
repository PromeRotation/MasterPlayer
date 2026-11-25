using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunRhalgr(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var center = new Vector3(-195.25f, 0, 147.5f);
        var moved = center + Vector3.UnitZ * 4;
        load("masterplayer.quest.rhalgrs-beacon",
        [
            Header(671), Frame(671, 0, [Actor(1, 0x1A88, center, combat: true), Actor(100, 0, center),
                Actor(2, 0x1BCA, center, targetable: false), Actor(3, 0x1BCA, center, dead: true)], full: true, party: party),
            Frame(671, 1, [Actor(2, 0x1BCA, moved, rotation: .8f, targetable: false)], party: party),
            Frame(671, 2, [Actor(1, 0x1A88, center, combat: true, cast: Cast(8370, 3, center, startedAt: 2))], party: party),
            Frame(671, 3, [Actor(1, 0x1A88, center, combat: true)], party: party),
            EffectEvent(4, 3, 8337, 1), Frame(671, 5, [], removed: [2], party: party),
            Frame(671, 6, [Actor(2, 0x1BCA, center - Vector3.UnitX * 6, rotation: 2)], party: party),
            EffectEvent(7, 2, 8337, 2), Frame(671, 8, [Actor(1, 0x1A88, center, dead: true)], party: party),
            Frame(671, 9, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(dangers() == 2 && Aoes("rhalgr/marker/").Length == 1
            && MathF.Abs((float)Value(Aoes("rhalgr/terminus/2/").Single(), "Rotation") - .8f) < .001f,
            "Rhalgr dynamic Termini, dead-vs-untargetable source sets or navigation.");
        controller.Seek(3.5);
        Check(Aoes("rhalgr/terminus/").All(a => (DateTime?)Value(a, "Activation") == Start.AddSeconds(5.3)),
            "Rhalgr The Order cancellation reset shared clock.");
        controller.Seek(5.5);
        var frozen = (Vector3)Value(Aoes("rhalgr/terminus/2/").Single(), "Position");
        Check(dangers() == 1 && Aoes("rhalgr/marker/").Length == 0 && Vector3.Distance(frozen, moved) < .04f,
            "Rhalgr effect matching or frozen destroyed Terminus.");
        controller.Seek(6.5);
        Check(Aoes("rhalgr/terminus/2/").Length == 2 && dangers() == 2, "Rhalgr recreated ID lost old/new generations.");
        controller.Seek(7.5);
        Check(Aoes("rhalgr/terminus/2/").Length == 1
            && (Vector3)Value(Aoes("rhalgr/terminus/2/").Single(), "Position") == frozen, "Rhalgr new generation effect removed frozen old actor.");
        controller.Seek(8.5);
        Check(dangers() == 0 && previews().Length == 0, "Rhalgr primary death cleanup.");
        controller.Seek(6.5);
        Check(dangers() == 2, "Rhalgr reverse Seek lost generations.");
        stop();
        Console.WriteLine("PASS production Rhalgr Termini: dynamic position/facing, fallback and shared clocks, dead/targetable marker separation, effect matching, destroyed frozen references, recreated IDs, primary death and reverse Seek");

        var player = center + Vector3.UnitX * 5;
        var poison = center + Vector3.UnitX * 15;
        load("masterplayer.quest.rhalgrs-beacon",
        [
            Header(671), Frame(671, 0, [Actor(1, 0x1A88, center, combat: true), Actor(100, 0, player),
                Actor(4, 0x1EA4D9, poison, eventState: 7, dead: true, targetable: false)], full: true, party: party),
            Frame(671, 1, [Actor(1, 0x1A88, center, combat: true, cast: Cast(8310, 5, center, target: 999, startedAt: 1)),
                Actor(2, 0x1A57, center, cast: Cast(8346, 3, poison, startedAt: 1))], party: party),
            CastEvent(1.1, 2, 8346, 3, center - Vector3.UnitX * 15),
            Frame(671, 1.3, [Actor(2, 0x1A57, center, cast: Cast(8346, 3, poison, startedAt: 1))], party: party),
            Frame(671, 1.4, [Actor(2, 0x1A57, center, cast: Cast(8346, 3, poison + Vector3.UnitZ * 2, startedAt: 1))], party: party),
            EffectEvent(2, 2, 8346, 1, poison), Frame(671, 2.1, [Actor(2, 0x1A57, center)], party: party),
            Frame(671, 3, [Actor(4, 0x1EA4D9, poison, eventState: 0, dead: true, targetable: false)], party: party),
            Frame(671, 4, [Actor(100, 0, player, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(10))], party: party),
            Frame(671, 5, [Actor(1, 0x1A88, center, combat: true)], party: party), Frame(671, 6, [], party: party),
        ]);
        controller.Seek(1.35);
        Check(Vector3.Distance((Vector3)Value(Aoes("trial/voidzone/cast/2").Single(), "Position"),
            center - Vector3.UnitX * 15) < .04f, "Rhalgr unchanged stale scan undid accepted packet poison correction.");
        controller.Seek(1.5);
        Check(dangers() == 2 && Aoes("rhalgr/kb/avoid/").Length == 1 && Aoes("trial/voidzone/live/").Length == 0
            && Aoes("rhalgr/kb/player").Length == 1
            && previews().Count(p => ((string)Value(p, "Id")).Contains("rhalgr/kb/")) == 2,
            "Rhalgr cast prediction and state7 poison source navigation or actor/path dual views.");
        Check(Vector3.Distance((Vector3)Value(Aoes("trial/voidzone/cast/2").Single(), "Position"),
            poison + Vector3.UnitZ * 2) < .04f, "Rhalgr real CastInfo update failed after packet correction.");
        controller.Seek(2.5);
        Check(Aoes("trial/voidzone/cast/").Length == 0 && Aoes("trial/voidzone/event/").Length == 1
            && (DateTime?)Value(Aoes("trial/voidzone/event/").Single(), "Activation") == Start.AddSeconds(2.5),
            "Rhalgr cast/effect poison handoff and half-second prediction.");
        controller.Seek(3.5);
        Check(Aoes("trial/voidzone/event/").Length == 0 && Aoes("trial/voidzone/live/").Length == 1,
            "Rhalgr poison entity did not consume event prediction.");
        controller.Seek(4.5);
        Check(Aoes("rhalgr/kb/").Length == 0 && Aoes("trial/voidzone/live/").Length == 1 && dangers() == 1,
            "Rhalgr immunity retained KB or hid unrelated poison.");
        controller.Seek(5.5);
        Check(Aoes("rhalgr/kb/").Length == 0, "Rhalgr observed Gunblade end leaked KB.");
        controller.Seek(1.5);
        Check(dangers() == 2 && Aoes("rhalgr/kb/player").Length == 1, "Rhalgr reverse Seek lost poison/KB.");
        stop();
        Console.WriteLine("PASS production Rhalgr Gunblade: dynamic poison cast/effect/entity handoff, state7-independent cone navigation, locked 10y wall projection, strict immunity, dual views, actual endings and reverse Seek");
    }
}

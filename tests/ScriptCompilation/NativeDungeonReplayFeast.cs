using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunFeast(object managerObject, object runtimeObject, object controllerObject,
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
        ulong Target() => (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [1ul, new Vector3(0, 0, 18)])!;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var center = new Vector3(0, 0, 18);
        var actors = new List<object> { Actor(1, 0x295A, center, combat: true, targetId: 100, objectIndex: 2, isEnemy: true),
            Actor(100, 0, center), Actor(501, 0x295B, center + Vector3.UnitX * 4, objectIndex: 4, isEnemy: true),
            Actor(502, 0x295B, center + Vector3.UnitX * 7, hitboxRadius: 5, objectIndex: 6, isEnemy: true) };
        var lightning = Enumerable.Range(0, 11).Select(i => Actor((ulong)(10 + i), 0x295B, center,
            cast: Cast(16286, 10, center, startedAt: 1))).ToArray();
        load("masterplayer.quest.a-feast-of-lies",
        [
            Header(863), Frame(863, 0, actors, full: true, party: party),
            Frame(863, 1, lightning, party: party),
            Frame(863, 2, [Actor(21, 0x295B, center, cast: Cast(16286, 10, center, startedAt: 2))], party: party),
            Frame(863, 3, [Actor(22, 0x295B, center, cast: Cast(16286, 10, center, startedAt: 3))], party: party),
            Frame(863, 4, [Actor(22, 0x295B, center)], party: party),
            Frame(863, 5, [Actor(501, 0x295B, center, dead: true), Actor(502, 0x295B, center, dead: true)], party: party),
            Frame(863, 6, [Actor(600, 0x29E8, center + Vector3.UnitX, objectIndex: 8, isEnemy: true)], party: party),
            Frame(863, 7, [Actor(1, 0x295A, center, dead: true)], party: party), Frame(863, 8, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(dangers() == 11 && Aoes("feast/electrocution/").Length == 11 && Target() == 1, "Feast eleven cast baseline.");
        controller.Seek(2.5);
        Check(dangers() == 0 && Aoes("feast/electrocution/").Length == 12 && Target() == 502
            && previews().Count(p => ((string)Value(p, "Id")).Contains("feast/electrocution/")) == 12,
            "Feast twelve lost display or failed hitbox-based target replacement.");
        controller.Seek(3.5);
        Check(dangers() == 13 && Target() == 1, "Feast thirteen incorrectly retained exactly-twelve navigation/priority.");
        controller.Seek(4.5);
        Check(dangers() == 0 && Target() == 502, "Feast individual observed ending did not return to twelve.");
        controller.Seek(5.5);
        Check(dangers() == 0 && Target() == 1, "Feast twelve without potential ball should retain priority-zero enemies.");
        controller.Seek(6.5);
        Check(Target() == 600 && Aoes("job-quest/enemy/600").Length == 1, "Feast serpent Adds priority/marker.");
        controller.Seek(7.5);
        Check(dangers() == 0 && previews().Length == 0, "Feast death cleanup.");
        controller.Seek(2.5);
        Check(dangers() == 0 && Target() == 502 && Aoes("feast/electrocution/").Length == 12, "Feast reverse Seek.");
        stop();
        Console.WriteLine("PASS production Feast lightning: eleven/twelve/thirteen navigation transitions, persistent dual-view display, closest hitbox target, empty-ball fallback, serpent Adds, individual Finish, death and reverse Seek");

        var burn = Enumerable.Range(0, 6).Select(i => Actor((ulong)(10 + i), 0x29C2, center,
            cast: Cast(16290, 9 - i * .1, center, startedAt: 1))).ToArray();
        load("masterplayer.quest.a-feast-of-lies",
        [
            Header(863), Frame(863, 0, actors, full: true, party: party),
            Frame(863, 1, burn, party: party), Frame(863, 2, [Actor(10, 0x29C2, center)], party: party),
            Frame(863, 3, [Actor(30, 0x29C2, center, cast: Cast(16290, 3, center, startedAt: 3))], party: party),
            Frame(863, 4, [Actor(1, 0x295A, center, combat: true, cast: Cast(16292, 3, center - Vector3.UnitZ * 4, startedAt: 4)),
                Actor(100, 0, center + Vector3.UnitX * 5, rotation: 1.2f)], party: party),
            Frame(863, 5, [Actor(1, 0x295A, center, combat: true)], party: party), Frame(863, 6, [], party: party),
        ]);
        controller.Seek(3.5);
        var outputs = ((IEnumerable)Value(Encounter(), "OutputCasts")).Cast<object>()
            .Select(c => (ulong)Value(Value(c, "Value"), "Source")).ToArray();
        Check(dangers() == 5 && outputs.SequenceEqual(new ulong[] { 11, 12, 13, 14, 15 }), "Feast first-five FIFO vacant-slot promotion.");
        controller.Seek(4.5);
        var projected = Aoes("feast/kb/1/player").Single();
        var position = (Vector3)Value(projected, "Position");
        var offset = position - (center + Vector3.UnitX * 5);
        Check(MathF.Abs(offset.X / offset.Z - 1.25f) < .02f && dangers() == 5
            && MathF.Abs((float)Value(projected, "Rotation") - 1.2f) < .001f
            && previews().Count(p => ((string)Value(p, "Id")).Contains("feast/kb/")) == 2,
            "Feast radial cast-origin stopwall KB/actor heading/dual views or added navigation.");
        controller.Seek(5.5);
        Check(Aoes("feast/kb/").Length == 0 && dangers() == 5, "Feast observed KB cancellation altered independent Burn.");
        controller.Seek(4.5);
        Check(Aoes("feast/kb/").Length == 2 && dangers() == 5, "Feast KB reverse Seek.");
        stop();
        Console.WriteLine("PASS production Feast Burn and Spiritcall: first-five insertion FIFO, original clocks and independent radial 20y wall projection, heading, dual views, actual cancellation, no extra navigation and reverse Seek");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunViperSamurai(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        bool Active() => (bool)Value(Script(), "EncounterActive");
        object[] Arashi() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith("musosai/arashi/")).ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var center = new Vector3(264, 0, 480);
        object Primary(ulong actorId, bool dead = false) => new
        {
            Id = actorId, Oid = 0x429Fu, Kind = 2, Hp = 0u, MaxHp = 1000u, Targetable = false, Dead = dead,
            InCombat = false, Position = Pose(center),
        };
        load("masterplayer.quest.fangs-of-the-viper",
        [
            Header(1235), Frame(1235, 0, [Primary(1), Actor(100, 0, center, combat: true, objectIndex: 2),
                Actor(101, 0, center, objectIndex: 0), Actor(2, 0x42A0, center)], full: true, party: party),
            Frame(1235, 1, [Actor(100, 0, center, objectIndex: 2), Actor(101, 0, center, combat: true, objectIndex: 0)], party: party),
            Frame(1235, 3, [Actor(2, 0x42A0, center, dead: true)], party: party),
            Frame(1235, 4, [], party: party),
            Frame(1235, 5, [Primary(10), Actor(20, 0x42A0, center)], removed: [1, 2], party: party),
            Frame(1235, 6, [], removed: [20], party: party),
            Frame(1235, 7, [Actor(30, 0x42A0, center, dead: true)], party: party),
            Frame(1235, 8, [Primary(10, dead: true)], party: party),
            Frame(1235, 9, [], party: party),
        ]);
        controller.Seek(.5);
        Check(!Active(), "Viper pulled before player combat.");
        controller.Seek(1.5);
        Check(Active() && previews().Length == 1, "Viper player pull incorrectly required primary targetability/HP/combat or drew hidden primary.");
        controller.Seek(3.5);
        Check(!Active() && previews().Length == 0, "Viper Gowrow death did not finish phase or immediately repulled.");
        controller.Seek(5.5);
        Check(Active(), "Viper new primary failed to reset cached Gowrow.");
        controller.Seek(7.5);
        Check(Active(), "Viper replaced a destroyed-live cached Gowrow with another dead actor.");
        controller.Seek(8.5);
        Check(!Active() && previews().Length == 0, "Viper primary death cleanup.");
        controller.Seek(1.5);
        Check(Active(), "Viper reverse Seek failed to rebuild player pull.");
        stop();
        Console.WriteLine("PASS production Viper lifecycle: player combat pull with untargetable HP-zero primary, hidden primary drawing, cached Gowrow death versus destruction, new-primary reset, Seek and cleanup");

        load("masterplayer.quest.fangs-of-the-viper",
        [
            Header(1235), Frame(1235, 0, [Primary(1), Actor(100, 0, center, combat: true, objectIndex: 0),
                Actor(101, 0, center, objectIndex: 2)], full: true, party: party, sourcePlayerId: 101),
            Frame(1235, 1, [Actor(100, 0, center, objectIndex: 0), Actor(101, 0, center, combat: true, objectIndex: 2)],
                party: party, sourcePlayerId: 101),
            Frame(1235, 2, [], party: party, sourcePlayerId: 101),
        ]);
        controller.Seek(.5);
        Check(!Active(), "Normal PlayerState pull was incorrectly replaced by object zero.");
        controller.Seek(1.5);
        Check(Active(), "Recorded source PlayerState identity did not drive pull independently of perspective/object zero.");
        controller.Seek(.5);
        Check(!Active(), "Reverse Seek lost source PlayerState metadata.");
        stop();
        Console.WriteLine("PASS production Viper source player: recorded PlayerState identity overrides disagreeing object-zero and current perspective, with reverse Seek");

        center = new(-217.27f, 0, -158.31f);
        load("masterplayer.quest.the-face-of-true-evil",
        [
            Header(709), Frame(709, 0, [Actor(1, 0x1BEE, center, combat: true), Actor(100, 0, center),
                Actor(2, 0x1BF0, center), Actor(3, 0x1BF0, center + Vector3.UnitX * 8), Actor(4, 0x1BF1, center)], full: true, party: party),
            CastEvent(1, 1, 8418, 4, center),
            Frame(709, 2, [Actor(1, 0x1BEE, center, combat: true, cast: Cast(8643, 3, center, startedAt: 2))], party: party),
            CastEvent(2, 1, 8643, 3, center),
            Frame(709, 3, [Actor(1, 0x1BEE, center, combat: true), Actor(2, 0x1BF0, center + Vector3.UnitZ * 5),
                Actor(3, 0x1BF0, center + Vector3.UnitX * 8, dead: true, targetable: false)], party: party),
            Frame(709, 4, [Actor(1, 0x1BEE, center, combat: true, cast: Cast(8642, 3, center, startedAt: 4))], party: party),
            CastEvent(4, 1, 8642, 3, center),
            Frame(709, 5, [Actor(1, 0x1BEE, center, combat: true)], party: party),
            EffectEvent(6, 1, 8642, 1),
            Frame(709, 7, [], removed: [2, 3], party: party),
            Frame(709, 8, [Actor(5, 0x1BF0, center)], party: party),
            EffectEvent(9, 5, 8419, 2),
            Frame(709, 10, [Actor(5, 0x1BF0, center + Vector3.UnitX)], party: party),
            Frame(709, 11, [Actor(1, 0x1BEE, center, combat: true, cast: Cast(8422, 25, center, startedAt: 11))], party: party),
            Frame(709, 12, [Actor(1, 0x1BEE, center, dead: true)], party: party),
            Frame(709, 13, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Arashi().Length == 0 && dangers() == 0, "Arashi visual invented circle.");
        controller.Seek(2.5);
        Check(Arashi().Length == 2 && dangers() == 2 && previews().Length == 4, "Arashi circles or boss/ViolentWind markers.");
        controller.Seek(3.5);
        Check(Arashi().Length == 2 && Arashi().Any(a => (Vector3)Value(a, "Position") == center + Vector3.UnitZ * 5),
            "Arashi did not follow clones or dropped dead untargetable clone.");
        controller.Seek(4.5);
        Check(Arashi().All(a => (DateTime)Value(a, "Activation") == Start.AddSeconds(7.3)), "Arashi shared activation overwrite.");
        controller.Seek(6.5);
        Check(dangers() == 2, "Cancellation or non-Arashi effect erased circles.");
        controller.Seek(7.5);
        Check(dangers() == 0, "Destroyed clones retained circles.");
        controller.Seek(8.5);
        Check(dangers() == 1, "Empty clone list discarded persistent Arashi activation.");
        controller.Seek(10.5);
        Check(dangers() == 0 && Arashi().Length == 0, "Arashi effect did not clear or stale observation reactivated it.");
        controller.Seek(11.5);
        Check(dangers() == 0 && ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().Count() == 1, "Musojin raidwide-only damage prediction.");
        controller.Seek(12.5);
        Check(previews().Length == 0 && dangers() == 0, "Musosai primary death waited for living clones.");
        controller.Seek(3.5);
        Check(dangers() == 2, "Arashi reverse Seek reconstruction.");
        stop();
        Console.WriteLine("PASS production Musosai: dynamic/dead/untargetable clones, shared activation overwrite, cancellation and delayed effect, empty-list retention, ViolentWind markers, raidwide, primary-only death, Seek and cleanup");
    }
}

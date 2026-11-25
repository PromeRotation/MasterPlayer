using System.Collections;
using System.Numerics;
using System.Reflection;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunMainScenario(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        ulong PlannedTarget()
        {
            return (ulong)Encounter().GetType().GetMethod("PriorityTarget")!.Invoke(Encounter(), [1ul, Vector3.Zero])!;
        }
        object[] Aoes(string prefix) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(prefix)).ToArray();
        ulong[] Sources() => ((IEnumerable)Value(Encounter(), "OutputCasts")).Cast<object>()
            .Select(c => (ulong)Value(Value(c, "Value"), "Source")).Order().ToArray();
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 8, EntityId = 101ul }];
        var center = new Vector3(504, 0, -133);
        load("masterplayer.quest.the-measure-of-his-reach",
        [
            Header(687), Frame(687, 0, [Actor(1, 0x1C48, center, combat: true), Actor(100, 0, center), Actor(101, 0, center),
                Actor(102, 0x1C49, center, subKind: 5, isEnemy: false)], full: true, party: party),
            Frame(687, 1, [Actor(2, 0x1C59, center, cast: Cast(8398, 7, center, startedAt: 1))], party: party),
            Frame(687, 9, [Actor(2, 0x1C59, center)], party: party), Frame(687, 10, [], party: party),
        ]);
        controller.Seek(2d);
        Check(dangers() == 1 && Damage().Select(d => (ulong)Value(d, "TargetId")).Order().SequenceEqual(new ulong[] { 100, 102 }),
            "Hakuro Moonlight R10 plus regular-party/NPC prediction mask.");
        controller.Seek(9.5);
        Check(dangers() == 0 && Damage().Length == 0, "Hakuro observed end retained geometry/damage.");
        stop();
        Console.WriteLine("PASS production Hakuro: Moonlight circle, regular party and NPC damage mask, alliance exclusion and independent observed end");

        center = Vector3.Zero;
        load("masterplayer.quest.death-unto-dawn",
        [
            Header(977), Frame(977, 0, [Actor(1, 0x3202, center, combat: true, targetId: 100, objectIndex: 2, isEnemy: true),
                Actor(100, 0, center, targetId: 1), Actor(101, 0, center),
                Actor(102, 0x3206, center, subKind: 5, isEnemy: false),
                Actor(2, 0x3205, center + Vector3.UnitX, objectIndex: 4, isEnemy: true)], full: true, party: party),
            Frame(977, 1, [
                Actor(10, 0x233C, center, cast: Cast(24058, 36, center, startedAt: 1)),
                Actor(11, 0x233C, center, cast: Cast(24059, 28, center, startedAt: 1)),
                Actor(12, 0x233C, center, cast: Cast(24062, .7, center, startedAt: 1))], party: party),
            Frame(977, 3, [Actor(11, 0x233C, center), Actor(2, 0x3205, center, dead: true)], party: party),
            Frame(977, 4, [Actor(10, 0x233C, center), Actor(12, 0x233C, center)], party: party), Frame(977, 5, [], party: party),
        ]);
        controller.Seek(2d);
        Check(dangers() == 0 && Damage().Length == 9
            && PlannedTarget() == 2
            && ((IEnumerable)Value(Encounter(), "ForbiddenTargets")).Cast<ulong>().All(id => id != 2),
            $"Lunar raidwide dangers={dangers()} damage={Damage().Length} target={PlannedTarget()}: helper/NPC/alliance mask or nail priority/attack permission.");
        controller.Seek(3.5);
        Check(Damage().Length == 6 && PlannedTarget() == 1,
            "Lunar independent raidwide end or nail death target handoff.");
        controller.Seek(4.5);
        Check(Damage().Length == 0, "Lunar raidwide observed ending retained prediction.");
        controller.Seek(2d);
        Check(Damage().Length == 9 && PlannedTarget() == 2, "Lunar reverse Seek lost predictions/priority.");
        stop();
        Console.WriteLine("PASS production Lunar Ifrit: three independent raidwides with NPC/alliance masks, no invented geometry, nail priority five and incidental attack permission, target handoff and reverse Seek");

        foreach (var (territory, oid, script, action, max) in new (uint, uint, string, uint, int)[]
        {
            (977, 0x3202, "death-unto-dawn", 24054, 3),
            (977, 0x3202, "death-unto-dawn", 24046, 2),
            (861, 0x299D, "the-oracle-of-light", 18035, 8),
        })
        {
            center = territory == 861 ? new(126.75f, 0, -311.25f) : Vector3.Zero;
            var initial = new List<object> { Actor(1, oid, center, combat: true), Actor(100, 0, center) };
            var casts = Enumerable.Range(0, max + 1).Select(i => Actor((ulong)(10 + i), 0x233C, center,
                cast: Cast(action, 9 - i * .1f, center + Vector3.UnitX * i, i * .1f, startedAt: 1))).ToArray();
            var records = new List<string>
            {
                Header(territory), Frame(territory, 0, initial, full: true, party: party),
                Frame(territory, 1, casts, party: party),
                EffectEvent(2, 10, action, 1),
                Frame(territory, 3, [Actor(10, 0x233C, center)], party: party),
                Frame(territory, 4, [Actor(30, 0x233C, center, cast: Cast(action, 3, center, startedAt: 4))], party: party),
                Frame(territory, 15, [], party: party),
                Frame(territory, 16, [Actor(1, oid, center, dead: true)], party: party), Frame(territory, 17, [], party: party),
            };
            load("masterplayer.quest." + script, records);
            controller.Seek(2.5);
            Check(dangers() == max && Sources().SequenceEqual(Enumerable.Range(10, max).Select(i => (ulong)i)),
                $"{action} initial FIFO or effect prematurely dequeued.");
            controller.Seek(4.5);
            Check(dangers() == max && Sources().SequenceEqual(Enumerable.Range(11, max).Select(i => (ulong)i)),
                $"{action} FIFO promotion or dictionary vacant slot.");
            controller.Seek(15.5);
            Check(dangers() == max, $"{action} expired before observed Finish.");
            controller.Seek(16.5);
            Check(dangers() == 0 && previews().Length == 0, $"{action} primary death leaked outputs.");
            controller.Seek(4.5);
            Check(dangers() == max && Sources().SequenceEqual(Enumerable.Range(11, max).Select(i => (ulong)i)),
                $"{action} reverse Seek FIFO.");
            stop();
            Console.WriteLine($"PASS production MSQ {action}: first {max} FIFO, effect retention, individual observed ending, promotion, vacant-slot insertion, dual-view navigation, primary death and reverse Seek");
        }

        center = new(126.75f, 0, -311.25f);
        var player = center + Vector3.UnitX * 10;
        load("masterplayer.quest.the-oracle-of-light",
        [
            Header(861), Frame(861, 0, [Actor(1, 0x299D, center, combat: true), Actor(100, 0, player, rotation: .7f)], full: true, party: party),
            Frame(861, 1, [Actor(2, 0x299E, center, cast: Cast(18036, 5.5f, center, 0, startedAt: 1))], party: party),
            Frame(861, 2, [Actor(2, 0x299E, center + Vector3.UnitX * 5,
                cast: Cast(18036, 5.5f, center, 0, startedAt: 1))], party: party),
            Frame(861, 3, [Actor(100, 0, player, rotation: .7f, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(10))], party: party),
            Frame(861, 4, [Actor(100, 0, player, rotation: .7f)], party: party),
            Frame(861, 7, [Actor(2, 0x299E, center)], party: party), Frame(861, 8, [], party: party),
        ]);
        controller.Seek(2.5);
        var projected = Aoes("oracle/kb/2/player").Single();
        var landing = (Vector3)Value(projected, "Position");
        Check(dangers() == 0 && MathF.Abs(landing.X - player.X) < .04f
            && MathF.Abs(landing.Z - (center.Z + MathF.Sqrt(300) - .001f)) < .04f
            && MathF.Abs((float)Value(projected, "Rotation") - .7f) < .001f
            && previews().Count(p => ((string)Value(p, "Id")).Contains("oracle/kb/")) == 2,
            "Oracle locked directional stopwall projected player/path or non-navigation contract.");
        controller.Seek(3.5);
        Check(Aoes("oracle/").Length == 0, "Oracle strict immunity did not hide KB.");
        controller.Seek(4.5);
        Check(Aoes("oracle/").Length == 2, "Oracle actual status removal failed to restore KB.");
        controller.Seek(7.5);
        Check(Aoes("oracle/").Length == 0, "Oracle observed finish leaked KB.");
        controller.Seek(2.5);
        Check(Aoes("oracle/").Length == 2, "Oracle reverse Seek failed to restore KB.");
        stop();
        Console.WriteLine("PASS production Oracle: locked 20y heading, ray wall truncation, actor-facing triangular marker and path dual views, strict immunity, no extra navigation, observed finish, reverse Seek and owner cleanup");
        object RawCast(float duration, float heading) => new { ActionId = 18036, Location = Pose(center), Rotation = heading,
            StartedAt = Start.AddSeconds(1), FinishAt = Start.AddSeconds(1 + duration + .3), TotalTime = duration };
        load("masterplayer.quest.the-oracle-of-light",
        [
            Header(861), Frame(861, 0, [Actor(1, 0x299D, center, combat: true), Actor(100, 0, center)], full: true, party: party),
            Frame(861, 1, [Actor(2, 0x299E, center, cast: RawCast(5.5f, 0))], party: party),
            Frame(861, 2, [Actor(2, 0x299E, center, cast: RawCast(7.5f, 0))], party: party),
            Frame(861, 3, [Actor(2, 0x299E, center, cast: RawCast(7.5f, MathF.PI / 2))], party: party),
            Frame(861, 4, [], party: party),
        ]);
        controller.Seek(3.5);
        var revised = (Vector3)Value(Aoes("oracle/kb/2/player").Single(), "Position");
        Check(revised.X > center.X + 19 && MathF.Abs(revised.Z - center.Z) < .04f,
            "Oracle large raw duration replacement blocked subsequent facing correction.");
        stop();
        Console.WriteLine("PASS production Oracle raw CastInfo: large TotalTime replacement accepts subsequent real rotation and clears replacement tombstones");
    }
}

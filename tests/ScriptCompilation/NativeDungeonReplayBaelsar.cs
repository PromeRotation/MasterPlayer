using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBaelsar(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.baelsars-wall";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object? Value(string name) => Encounter().GetType().GetProperty(name)!.GetValue(Encounter());
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
                "Production Baelsar replay published PR control.");
        }
        var center = new Vector3(-174, 0, 73);
        load(id,
        [
            Header(1114), Frame(1114, 0, [Actor(1, 0x1938, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1939, center)], full: true, party: [new { Slot = 0, EntityId = 100UL }]),
            StatusEvent(.05, 100, 562, 0, true),
            Frame(1114, .1, [Actor(1, 0x1938, center, combat: true, cast: Cast(7347, 4, center)),
                Actor(100, 0, center, stackedStatus: 562)], party: [new { Slot = 0, EntityId = 100UL }]),
            EffectEvent(5.05, 1, 7348, 1, target: 100),
            Frame(1114, 5.1, [Actor(1, 0x1938, center, combat: true), Actor(100, 0, center, stackedStatus: 562)]),
        ]);
        controller.Seek(.2); Check(dangers() == 1 && previews().Length == 2, "Production Baelsar ray and prey missing.");
        var damage = ((IEnumerable)Value("PredictedDamage")!).Cast<object>().Single();
        Check(Math.Abs(((DateTime)damage.GetType().GetProperty("Activation")!.GetValue(damage)! - Start.AddSeconds(5.05)).TotalMilliseconds) < .001,
            $"Production Baelsar prey lost event clock: {damage.GetType().GetProperty("Activation")!.GetValue(damage):O}.");
        controller.Seek(5.2); Check(dangers() == 0 && previews().Length == 0, "Production Baelsar prey resurrected after hit.");
        controller.Seek(.2); Check(previews().Length == 2, "Production Baelsar prey seek failed."); Isolated(); stop();

        center = new(116, 0, 0);
        load(id,
        [
            Header(1114), Frame(1114, 0, [Actor(1, 0x193A, center, combat: true), Actor(100, 0, center)], full: true),
            JsonSerializer.Serialize(new { t = .1, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
                data = new { TargetId = 100, StatusId = 1132, DurationMilliseconds = 10000, Timestamp = Start.AddSeconds(.1) } }),
            Frame(1114, .2, [Actor(1, 0x193A, center, combat: true, cast: Cast(31469, 4, center, startedAt: .2)),
                Actor(100, 0, center, stackedStatus: 1132, statusExpiresAt: Start.AddSeconds(10.1))]),
            StatusEvent(10.1, 100, 1132, 0, false),
            Frame(1114, 11, [Actor(1, 0x193A, center, combat: true)]),
        ]);
        controller.Seek(8); Check(!(bool)Value("RequireAllStop")!, "Production Baelsar caution stopped early.");
        controller.Seek(9.2); Check((bool)Value("RequireAllStop")!, "Production Baelsar caution failed to stop.");
        Isolated();
        controller.Seek(10.2); Check(!(bool)Value("RequireAllStop")!, "Production Baelsar caution removal retained hold.");
        controller.Seek(9.2); Check((bool)Value("RequireAllStop")!, "Production Baelsar caution seek failed."); stop();

        center = new(351.235f, 0, 391.855f);
        load(id,
        [
            Header(1114), Frame(1114, 0, [Actor(1, 0x193D, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 5),
                .. Enumerable.Range(10, 9).Select(i => Actor((ulong)i, 0x193F, center + Vector3.UnitX * (i - 14)))], full: true),
            Frame(1114, .1, Enumerable.Range(10, 9).Select(i => Actor((ulong)i, 0x193F, center + Vector3.UnitX * (i - 14),
                cast: Cast(7372, 20, center + Vector3.UnitX * (i - 14))))),
            Frame(1114, 1, [Actor(14, 0x193F, center, dead: true)]),
            ResearchIconEvent(2, 100, 22),
            Frame(1114, 2.1, [Actor(1, 0x193D, center, combat: true, cast: Cast(7367, 3, center, target: 100, startedAt: 2.1))]),
            EffectEvent(5.5, 1, 7367, 1, target: 100),
            Frame(1114, 6, [Actor(1, 0x193D, center, combat: true)], removed: Enumerable.Range(10, 9).Select(i => (ulong)i).ToArray()),
            ResearchIconEvent(6.1, 100, 1), StatusEvent(6.2, 100, 3421, 0, true),
            Frame(1114, 6.3, [Actor(20, 0x193E, center + Vector3.UnitZ * 5, cast: Cast(7369, 15, center, startedAt: 6.3), hitboxRadius: 1)]),
            Frame(1114, 7, [Actor(1, 0x193D, center, combat: true, cast: Cast(7364, 3, center, startedAt: 7))]),
            StatusEvent(10.2, 100, 3421, 0, false),
            Frame(1114, 11, [Actor(1, 0x193D, center, combat: true)], removed: [20]),
        ]);
        controller.Seek(.2); Check(dangers() == 0 && previews().Length == 0, "Production Baelsar intact nine swords visible.");
        controller.Seek(1.1); Check(dangers() == 8 && previews().Length == 8, "Production Baelsar sword death did not reveal casts.");
        controller.Seek(2.2); Check(dangers() == 9 && previews().Length == 10, "Production Baelsar boot trajectory missing.");
        controller.Seek(5.6); Check(dangers() == 8 && previews().Length == 8, "Production Baelsar boot effect leaked.");
        controller.Seek(6.4); Check(dangers() == 1 && previews().Length == 1, "Production Baelsar collar rescue missing.");
        controller.Seek(7.1); Check(dangers() == 1 && previews().Length == 2, "Production Baelsar gaze/collar combination missing.");
        Isolated();
        controller.Seek(11.1); Check(dangers() == 0 && previews().Length == 0, "Production Baelsar final cleanup failed.");
        controller.Seek(2.2); Check(dangers() == 9 && previews().Length == 10, "Production Baelsar combination seek failed."); stop();
        Console.WriteLine("PASS native Baelsar three bosses, prey clock, caution hold, nine swords, boot, collar, gaze and seek isolation");
    }
}

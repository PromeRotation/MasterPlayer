using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAlaMhigo(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.ala-mhigo";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        DateTime Activation(object aoe) => (DateTime)aoe.GetType().GetProperty("Activation")!.GetValue(aoe)!;
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var center = new Vector3(-191, 0, 72);
        List<string> records =
        [
            Header(1146), Frame(1146, 0, [Actor(1, 0x1BA4, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x18D6, center), Actor(11, 0x18D6, center)], full: true, party: party),
            Frame(1146, .1, [Actor(10, 0x18D6, center, cast: Cast(8265, 3, center, MathF.PI / 2)),
                Actor(11, 0x18D6, center, cast: Cast(8266, 3, center, -MathF.PI / 2))], party: party),
            EffectEvent(3.1, 10, 8265, 1), EffectEvent(3.1, 11, 8266, 2),
            Frame(1146, 3.1, [Actor(10, 0x18D6, center), Actor(11, 0x18D6, center)], party: party),
        ];
        for (uint hit = 1; hit <= 12; ++hit) records.Add(EffectEvent(4 + hit, 10, 8267, hit + 10));
        load(id, records);
        controller.Seek(.2); Check(previews().Length == 2 && dangers() == 2, "Production Ala lasers missing.");
        Check(Aoes().All(a => Math.Abs((Activation(a) - Start.AddSeconds(4.4)).TotalMilliseconds) < 1), "Production Ala laser clock shifted.");
        controller.Seek(3.2); Check(previews().Length == 2, "Production Ala natural finish removed lasers.");
        controller.Seek(15.1); Check(previews().Length == 2, "Production Ala laser cleared before twelfth rest.");
        controller.Seek(16.1); Check(previews().Length == 0 && dangers() == 0, "Production Ala twelfth rest retained lasers.");
        controller.Seek(.2); Check(previews().Length == 2, "Production Ala laser seek failed."); stop();

        center = new(250, 0, -70);
        load(id,
        [
            Header(1146), Frame(1146, 0, [Actor(1, 0x1BA6, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 5),
                Actor(10, 0x1BAF, center + Vector3.UnitX * 10), Actor(11, 0x1BAF, center - Vector3.UnitX * 10)],
                full: true, party: party),
            AlaTether(.1, 10, 100, 45, true), AlaTether(.1, 11, 101, 45, true),
            Frame(1146, .2, [Actor(10, 0x1BAF, center + Vector3.UnitX * 10, tethers: [new { Id = 45, TargetId = 100UL }]),
                Actor(11, 0x1BAF, center - Vector3.UnitX * 10, tethers: [new { Id = 45, TargetId = 101UL }])], party: party),
            AlaTether(1, 11, 101, 45, false), Frame(1146, 1, [], party: party),
            AlaTether(2, 10, 100, 45, false), Frame(1146, 3, [], party: party),
        ]);
        controller.Seek(.3); Check(previews().Length == 2 && dangers() == 1, "Production Ala body entry/line missing.");
        controller.Seek(1.2); Check(previews().Length == 2 && dangers() == 1, "Production Ala second untether removed first body.");
        controller.Seek(3.1); Check(previews().Length == 0 && dangers() == 0, "Production Ala stale body resurrected.");
        controller.Seek(.3); Check(previews().Length == 2, "Production Ala body seek failed."); stop();

        center = new(250, 0, -353);
        load(id,
        [
            Header(1146), Frame(1146, 0, [Actor(1, 0x1BAA, center + Vector3.UnitX * 10, combat: true, targetId: 100),
                Actor(100, 0, center + Vector3.UnitX * 15), Actor(101, 0, center + Vector3.UnitZ * 8),
                Actor(50, 0x1EA1A1, center)], full: true, party: party),
            Frame(1146, .1, [Actor(50, 0x1EA1A1, center, eventAnimation: 0x00040008)], party: party),
            AlaTether(.2, 1, 100, 41, true),
            Frame(1146, .3, [Actor(1, 0x1BAA, center + Vector3.UnitX * 10, combat: true, targetId: 100,
                tethers: [new { Id = 41, TargetId = 100UL }], cast: Cast(8293, 6, center + Vector3.UnitX * 10, startedAt: .3))], party: party),
            Frame(1146, 6.3, [Actor(1, 0x1BAA, center + Vector3.UnitX * 10, combat: true, targetId: 100,
                tethers: [new { Id = 41, TargetId = 100UL }])], party: party),
            EffectEvent(7, 1, 8299, 1), Frame(1146, 7.1, [], party: party),
            JsonSerializer.Serialize(new { t = 8, kind = "NpcYell", type = "PromeRotation.LogSystem.LogSystemNpcYellEvent",
                data = new { SourceId = 1, NpcYellId = 5372, Timestamp = Start.AddSeconds(8) } }),
            EffectEvent(9, 1, 8288, 2),
            Frame(1146, 10, [Actor(1, 0x1BAA, center, combat: true, cast: Cast(8296, 5.5, center, startedAt: 10))], party: party),
            Frame(1146, 15.5, [Actor(1, 0x1BAA, center, combat: true)], party: party),
            EffectEvent(16, 20, 8297, 3),
        ]);
        controller.Seek(.01); Check(Aoes().Any(a => AoeId(a) == "ala/shrink"), "Production Ala shrink prediction missing.");
        controller.Seek(.4); Check(!Aoes().Any(a => AoeId(a) == "ala/shrink" || AoeId(a).EndsWith("/outward"))
            && Aoes().Any(a => AoeId(a) == "ala/spark/1/display") && dangers() == 1, "Production Ala shrink/KB priorities differ.");
        controller.Seek(6.4); Check(Aoes().Any(a => AoeId(a).EndsWith("/outward")), "Production Ala KB failed to restore spark navigation.");
        Check(Math.Abs((Activation(Aoes().Single(a => AoeId(a) == "ala/spark/1/display")) - Start.AddSeconds(8.2)).TotalMilliseconds) < 1,
            "Production Ala tether activation shifted.");
        controller.Seek(7.2); Check(previews().Length == 0 && dangers() == 0, "Production Ala resolved spark resurrected.");
        controller.Seek(8.2); Check(Aoes().Any(a => AoeId(a) == "ala/troika/display"), "Production Ala yell not routed.");
        controller.Seek(9.2); Check(previews().Length == 0, "Production Ala first troika retained.");
        controller.Seek(15.6); Check(previews().Length == 2, "Production Ala sword natural finish lost baits.");
        controller.Seek(16.1); Check(previews().Length == 0 && dangers() == 0, "Production Ala sword hit leaked.");
        controller.Seek(.4); Check(dangers() == 1, "Production Ala Zenos seek failed."); stop();
        Console.WriteLine("PASS native AlaMhigo three bosses, twelve rest hits, body tethers, stale snapshots, shrink, knockback priorities, spark timing, yell cleave, sword natural finish, seek and stop");
    }

    private static string AlaTether(double t, ulong source, ulong target, uint tether, bool present)
        => JsonSerializer.Serialize(new { t, kind = present ? "Tether" : "TetherCancel",
            type = "PromeRotation.LogSystem.LogSystemTether" + (present ? "" : "Cancel") + "Event",
            data = new { SourceId = source, TargetId = target, TetherId = tether, Timestamp = Start.AddSeconds(t) } });
}

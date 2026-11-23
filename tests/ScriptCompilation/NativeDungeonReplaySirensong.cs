using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSirensong(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.sirensong-sea";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        bool Flag(string name) => (bool)Encounter().GetType().GetProperty(name)!.GetValue(Encounter())!;
        void Isolated()
        {
            var hints = Script().GetType().GetProperty("Hints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(Script())!;
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
                "Production Sirensong replay published PR control.");
            object setting = runtime.Setting;
            Check(!(bool)setting.GetType().GetProperty("RuntimeMovementHeld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(setting)!,
                "Production Sirensong replay held live movement.");
        }
        var center = new Vector3(-2.7f, 0, -217);
        load(id,
        [
            Header(1142), Frame(1142, 0, [Actor(1, 0x1AFB, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x18D6, center)], full: true),
            ResearchIconEvent(.05, 100, 62),
            Frame(1142, .1, [Actor(1, 0x1AFB, center, combat: true, cast: Cast(8022, 5, center)),
                Actor(10, 0x18D6, center, cast: Cast(8027, 4, center))]),
            EffectEvent(5.1, 1, 8023, 1, target: 100),
            Frame(1142, 5.2, [Actor(1, 0x1AFB, center, combat: true), Actor(10, 0x18D6, center)]),
        ]);
        controller.Seek(.2); Check(dangers() == 2 && previews().Length == 3, "Production Sirensong Lugat combination missing.");
        controller.Seek(5.3); Check(dangers() == 0 && previews().Length == 0, "Production Sirensong Lugat finish leaked.");
        controller.Seek(.2); Check(previews().Length == 3, "Production Sirensong Lugat seek failed."); stop();

        center = new(-8, 0, 79);
        List<string> records =
        [
            Header(1142), Frame(1142, 0, [Actor(1, 0x1AFC, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 10),
                .. Enumerable.Range(10, 6).Select(i => Actor((ulong)i, 0x1AFD, center + Vector3.UnitX * (i - 9)))], full: true),
            .. Enumerable.Range(0, 6).Select(i => EffectEvent(.1 + i * .1, (ulong)(10 + i), 8031, (uint)(i + 1), rotation: i * MathF.PI / 3)),
            ResearchIconEvent(1, 100, 22),
            JsonSerializer.Serialize(new { t = 1.1, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = 1, TargetId = 100, TetherId = 61, Timestamp = Start.AddSeconds(1.1) } }),
            Frame(1142, 1.2, [Actor(1, 0x1AFC, center, combat: true, tethers: [new { Id = 61, TargetId = 100UL }])]),
            Frame(1142, 2, [Actor(100, 0, center + Vector3.UnitX * 10, stackedStatus: 1209)]),
            JsonSerializer.Serialize(new { t = 3, kind = "TetherCancel", type = "PromeRotation.LogSystem.LogSystemTetherCancelEvent",
                data = new { SourceId = 1, TargetId = 100, TetherId = 61, Timestamp = Start.AddSeconds(3) } }),
            Frame(1142, 3.1, [Actor(1, 0x1AFC, center, combat: true)]),
            Frame(1142, 9, []),
        ];
        load(id, records);
        controller.Seek(.5); Check(dangers() == 0 && previews().Length == 0, "Production Sirensong partial shadow published.");
        controller.Seek(.7); Check(dangers() == 13 && previews().Length == 13, "Production Sirensong six-event shadow missing.");
        controller.Seek(1.3); Check(dangers() == 15 && previews().Length == 17 && Flag("WantsSirenKnockbackImmunity"),
            "Production Sirensong pull/tether combination missing.");
        controller.Seek(2.1); Check(dangers() == 14 && previews().Length == 15 && !Flag("WantsSirenKnockbackImmunity"),
            "Production Sirensong immunity incorrectly cleared stretch or retained pull.");
        Isolated();
        controller.Seek(3.2); Check(dangers() == 13 && previews().Length == 13, "Production Sirensong untether leaked.");
        controller.Seek(8.2); Check(dangers() == 0 && previews().Length == 0, "Production Sirensong shadow expiry leaked.");
        controller.Seek(.7); Check(dangers() == 13, "Production Sirensong shadow seek failed."); stop();

        foreach (var action in new uint[] { 8037, 8038 })
        {
            center = new(-44.5f, 0, 465);
            var rotation = action == 8037 ? 0 : MathF.PI;
            load(id,
            [
                Header(1142), Frame(1142, 0, [Actor(1, 0x1AFE, center, combat: true),
                    Actor(100, 0, center + Vector3.UnitZ * 4, rotation: rotation), Actor(10, 0x1EA300, center + Vector3.UnitX * 10)], full: true),
                Frame(1142, .1, [Actor(1, 0x1AFE, center, combat: true, cast: Cast(action, 5, center))]),
                Frame(1142, 5.1, [Actor(1, 0x1AFE, center, combat: true)]),
                StatusEvent(6.1, 100, 5174, 0, true),
                Frame(1142, 6.1, [Actor(100, 0, center + Vector3.UnitZ * 4, rotation: MathF.PI, stackedStatus: 5174,
                    statusExpiresAt: Start.AddSeconds(9.1))]),
                StatusEvent(9.1, 100, 5174, 0, false),
                Frame(1142, 9.1, [Actor(100, 0, center + Vector3.UnitZ * 4, rotation: MathF.PI)]),
                Frame(1142, 10, [Actor(10, 0x1EA300, center + Vector3.UnitX * 10, eventState: 7)]),
            ]);
            controller.Seek(.2); Check(dangers() == 3 && previews().Length == 4, $"Production Sirensong {action} staging missing.");
            controller.Seek(5.5); Check(dangers() == 3 && previews().Length == 4, "Production Sirensong natural cast finish cleared pending march.");
            controller.Seek(6.2); Check(Flag("RequireSirenMovementHold") && dangers() == 1 && previews().Length == 3,
                "Production Sirensong active forced march failed.");
            Isolated();
            controller.Seek(9.2); Check(!Flag("RequireSirenMovementHold") && dangers() == 1 && previews().Length == 1,
                "Production Sirensong forced status removal failed.");
            controller.Seek(10.1); Check(dangers() == 0 && previews().Length == 0, "Production Sirensong state-seven mine leaked.");
            controller.Seek(6.2); Check(Flag("RequireSirenMovementHold"), "Production Sirensong forced march seek failed."); stop();
        }
        center = new(-44.5f, 0, 465);
        records = [Header(1142), Frame(1142, 0, [Actor(1, 0x1AFE, center, combat: true), Actor(100, 0, center)], full: true)];
        for (var i = 0; i < 4; ++i)
        {
            records.Add(Frame(1142, .1 + i * 5, [Actor(1, 0x1AFE, center, combat: true, cast: Cast(8041, 3, center, startedAt: .1 + i * 5))]));
            records.Add(Frame(1142, 3.1 + i * 5, [Actor(1, 0x1AFE, center, combat: true)]));
        }
        records.Add(Frame(1142, 19, [Actor(20, 0x1EA2FF, center, eventState: 2)]));
        records.Add(Frame(1142, 20, [Actor(20, 0x1EA2FF, center, eventState: 1)]));
        load(id, records);
        controller.Seek(16); Check(dangers() == 1 && previews().Length == 1, "Production Sirensong fourth tears ring missing.");
        controller.Seek(19.1);
        dynamic arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(dangers() == 0 && !(bool)arena.Contains(center + Vector3.UnitX * 17, 0f), "Production Sirensong small arena restore failed.");
        controller.Seek(20.1);
        arena = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((bool)arena.Contains(center + Vector3.UnitX * 17, 0f), "Production Sirensong arena reset failed.");
        controller.Seek(16); Check(dangers() == 1, "Production Sirensong ring count seek failed."); stop();
        Console.WriteLine("PASS native Sirensong three bosses, six-event shadow, pull immunity, tether, both forced marches, PR replay isolation, tears, shrink and seek");
    }
}

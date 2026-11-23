using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunBardam(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.bardam";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        string AoeId(object aoe) => (string)aoe.GetType().GetProperty("Id")!.GetValue(aoe)!;
        var center = new Vector3(4, 0, 249);
        load(id,
        [
            Header(1143), Frame(1143, 0, [Actor(1, 0x1A9E, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 10)], full: true),
            Frame(1143, .1, [Actor(1, 0x1A9E, center, combat: true, cast: Cast(7929, 10, center, target: 100),
                tethers: [new { Id = 57, TargetId = 100UL }])]),
            JsonSerializer.Serialize(new { t = .1, kind = "Tether", type = "PromeRotation.LogSystem.LogSystemTetherEvent",
                data = new { SourceId = 1, TargetId = 100, TetherId = 57, Timestamp = Start.AddSeconds(.1) } }),
            Frame(1143, 10.1, [Actor(1, 0x1A9E, center, combat: true)]),
        ]);
        controller.Seek(1); Check(dangers() == 1 && previews().Length == 2, "Production Bardam Rush stretch missing.");
        controller.Seek(10.2); Check(dangers() == 0 && previews().Length == 0, "Production Bardam Rush finish leaked.");
        controller.Seek(1); Check(dangers() == 1, "Production Bardam Rush seek failed."); stop();

        center = new(-28, 0, -14);
        List<string> records =
        [
            Header(1143), Frame(1143, 0, [Actor(1, 0x1AA5, center, combat: true, targetable: false),
                Actor(100, 0, center), Actor(10, 0x1AA4, center), Actor(11, 0x18D6, center)], full: true),
            Frame(1143, .1, [Actor(10, 0x1AA4, center, cast: Cast(9591, 4, center))]),
            Frame(1143, 4.1, [Actor(10, 0x1AA4, center), Actor(11, 0x18D6, center, cast: Cast(9592, 4, center, startedAt: 4.1))]),
            Frame(1143, 8.1, [Actor(11, 0x18D6, center, cast: Cast(9593, 4, center, startedAt: 8.1))]),
            Frame(1143, 12.1, [Actor(11, 0x18D6, center, cast: Cast(9594, 4, center, startedAt: 12.1))]),
            Frame(1143, 16.1, [Actor(11, 0x18D6, center)]),
        ];
        load(id, records);
        foreach (var at in new[] { .2, 4.2, 8.2, 12.2 })
        {
            controller.Seek(at); Check(dangers() == 1 && previews().Length == 1, $"Production Bardam Heavy stage at {at} differs: dangers={dangers()}, previews={previews().Length}, aoes={Aoes().Length}.");
            var ban = (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!;
            Check(ban.Contains(10UL) && ban.Contains(11UL), "Production Bardam trial failed to ban hostile adds.");
        }
        controller.Seek(16.2); Check(dangers() == 0 && previews().Length == 0, "Production Bardam Heavy final leaked.");
        controller.Seek(8.2); Check(dangers() == 1, "Production Bardam Heavy seek failed."); stop();
        load(id,
        [
            Header(1143), Frame(1143, 0, [Actor(1, 0x1AA5, center, combat: true, targetable: false),
                Actor(100, 0, center), Actor(10, 0x18D6, center)], full: true),
            Frame(1143, .1, [Actor(10, 0x18D6, center, cast: Cast(9596, 4, center))]),
            Frame(1143, 4.1, [Actor(10, 0x18D6, center, cast: Cast(9595, 4, center, startedAt: 4.1))]),
            Frame(1143, 8.1, [Actor(10, 0x18D6, center)]),
        ]);
        controller.Seek(.2); Check(dangers() == 1 && previews().Length == 1, "Production Bardam Tremblor inner missing.");
        controller.Seek(4.2); Check(dangers() == 1 && previews().Length == 1, "Production Bardam Tremblor outer duplicate.");
        controller.Seek(8.2); Check(dangers() == 0 && previews().Length == 0, "Production Bardam Tremblor leaked."); stop();
        records =
        [
            Header(1143), Frame(1143, 0, [Actor(1, 0x1AA5, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 12),
                Actor(10, 0x18D6, center)], full: true), ResearchIconEvent(.01, 100, 197),
        ];
        for (uint i = 0; i < 9; ++i)
        {
            var at = .1 + i * 5;
            records.Add(Frame(1143, at, [Actor(10, 0x18D6, center, cast: Cast(i == 0 ? 9597u : 9598u, i == 0 ? 4 : 1.5, center, startedAt: at))]));
            records.Add(Frame(1143, at + (i == 0 ? 4 : 1.5), [Actor(10, 0x18D6, center)]));
        }
        load(id, records);
        for (var i = 0; i < 9; ++i)
        {
            controller.Seek(.2 + i * 5);
            Check(Aoes().Count(a => AoeId(a).EndsWith("/display")) == (i == 8 ? 0 : 1), $"Production Bardam Comet start {i + 1} differs.");
        }
        controller.Seek(.2); Check(Aoes().Any(a => AoeId(a).EndsWith("/display")), "Production Bardam Comet seek failed."); stop();
        load(id,
        [
            Header(1143), Frame(1143, 0, [Actor(1, 0x1AA5, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1F4D, center - Vector3.UnitX * 12), Actor(11, 0x1F4A, center, hitboxRadius: 2.4f),
                Actor(12, 0x1F4A, center + Vector3.UnitZ * 8)], full: true),
            Frame(1143, .1, [Actor(10, 0x1F4D, center - Vector3.UnitX * 12, cast: Cast(9602, 30, center - Vector3.UnitX * 12))]),
            Frame(1143, 2, [Actor(12, 0x1F4A, center + Vector3.UnitZ * 8, dead: true)]),
            Frame(1143, 30.1, [Actor(10, 0x1F4D, center - Vector3.UnitX * 12)]),
        ]);
        controller.Seek(1); Check(previews().Length == 0, "Production Bardam shelter appeared with two stars.");
        controller.Seek(2.1); Check(previews().Length == 1 && dangers() == 1, "Production Bardam sole-star shelter missing.");
        controller.Seek(30.2); Check(previews().Length == 0 && dangers() == 0, "Production Bardam meteor finish leaked.");
        controller.Seek(2.1); Check(previews().Length == 1, "Production Bardam sole-star seek failed."); stop();

        center = new(24, 0, -475.5f);
        load(id,
        [
            Header(1143), Frame(1143, 0, [Actor(1, 0x1AA6, center + Vector3.UnitX * 12, combat: true, rotation: -MathF.PI / 2),
                Actor(100, 0, center + Vector3.UnitX * 4)], full: true),
            ResearchIconEvent(.1, 100, 23), ResearchIconEvent(.1, 100, 16),
            JsonSerializer.Serialize(new { t = .2, kind = "PlayActionTimeline", type = "PromeRotation.LogSystem.LogSystemPlayActionTimelineEvent",
                data = new { SourceId = 1, TimelineId = 0x1E43, Timestamp = Start.AddSeconds(.2) } }),
            Frame(1143, 1, [Actor(1, 0x1AA6, center, combat: true, cast: Cast(7949, 4.5, center, startedAt: 1))]),
            EffectEvent(3, 1, 7948, 1, target: 100), EffectEvent(3, 1, 7951, 2, target: 100),
            EffectEvent(6.9, 1, 7950, 3), Frame(1143, 7, [Actor(1, 0x1AA6, center, combat: true)]),
        ]);
        controller.Seek(.3); Check(previews().Length == 3, "Production Bardam Yol baits/timeline missing.");
        controller.Seek(5.1); Check(previews().Length == 2, "Production Bardam Yol gaze/squall missing.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
            "Production Bardam replay published PR facing.");
        controller.Seek(7.1); Check(previews().Length == 0 && dangers() == 0, "Production Bardam Yol final leaked.");
        controller.Seek(.3); Check(previews().Length == 3, "Production Bardam Yol seek failed."); stop();
        Console.WriteLine("PASS native Bardam three bosses, Rush, trial attack ban, three HeavyStrike stages, Tremblor, nine Comet starts, sole-star shelter, Yol timeline, gaze, seek and replay isolation");
    }
}

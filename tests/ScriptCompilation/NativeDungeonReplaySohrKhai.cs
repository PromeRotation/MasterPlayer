using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSohrKhai(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.sohr-khai";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Script())!;
        ulong[] Forbidden() => (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!;
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        Vector3 center = new(-400, 0, -158.04f);
        load(id,
        [
            Header(1112), Frame(1112, 0, [Actor(1, 0x15FB, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1112, 1, [Actor(10, 0x1603, center)]),
            Frame(1112, 2, [Actor(11, 0x1603, center + Vector3.UnitX * 5)]),
            StatusEvent(3, 1, 325, 0, true),
            Frame(1112, 3.1, [Actor(1, 0x15FB, center, combat: true, stackedStatus: 325)]),
            EffectEvent(8.3, 1, 6212, 1),
            Frame(1112, 9, [Actor(11, 0x1603, center + Vector3.UnitX * 5)]),
        ]);
        controller.Seek(2.1); Check(previews().Length == 1 && dangers() == 1, "Production SohrKhai latest cross missing.");
        controller.Seek(3.2); Check(Forbidden().SequenceEqual([1UL]), "Production SohrKhai invincibility ban missing.");
        controller.Seek(9.1); Check(previews().Length == 0 && dangers() == 0, "Production SohrKhai cross resurrected.");
        controller.Seek(2.1); Check(dangers() == 1 && Forbidden().Length == 0, "Production SohrKhai cross seek failed."); stop();

        center = new(400, 0, 104.166f);
        var point = new Vector3(400, 0, 89.161f);
        load(id,
        [
            Header(1112), Frame(1112, 0, [Actor(1, 0x155C, center, combat: true), Actor(100, 0, point),
                Actor(10, 0x1EA076, point), Actor(11, 0x1B2, new(398, 0, 89.161f))], full: true),
            Frame(1112, .1, [Actor(10, 0x1EA076, point, eventAnimation: 0x00100020),
                Actor(11, 0x1B2, new(398, 0, 89.161f), cast: Cast(5823, 4.9, new(398, 0, 89.161f), MathF.PI / 2))]),
            Frame(1112, 5, [Actor(11, 0x1B2, new(398, 0, 89.161f))]),
        ]);
        controller.Seek(.2); Check(dangers() == 2 && previews().Length == 9 && Goals() == 0, "Production SohrKhai wall safe-complement or knockback missing.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((bool)boundary.Contains(new Vector3(404.9f, 0, 89.161f), 0f)
            && !(bool)boundary.Contains(new Vector3(404.9f, 0, 99.161f), 0f), "Production SohrKhai wall segment expansion differs.");
        controller.Seek(5.1); Check(dangers() == 0 && previews().Length == 0, "Production SohrKhai knockback finish leaked.");
        controller.Seek(.2); Check(dangers() == 2, "Production SohrKhai wall seek failed."); stop();
        load(id,
        [
            Header(1112), Frame(1112, 0, [Actor(1, 0x155C, center, combat: true), Actor(100, 0, center)], full: true),
            ResearchIconEvent(.5, 100, 24),
            Frame(1112, 1, [Actor(10, 0x155D, center + Vector3.UnitX * 4)]),
            Frame(1112, 5, [Actor(10, 0x155D, center + Vector3.UnitX * 4,
                cast: new { ActionId = 6010, Location = Pose(center + Vector3.UnitX * 4), StartedAt = Start.AddSeconds(5), FinishAt = Start.AddSeconds(8.3) })]),
            Frame(1112, 8.1, [Actor(10, 0x155D, center + Vector3.UnitX * 4)]),
        ]);
        controller.Seek(.6); Check(previews().Length == 1 && dangers() == 1, "Production SohrKhai cloud marker missing.");
        controller.Seek(1.1); Check(previews().Length == 1 && dangers() == 1, "Production SohrKhai cloud birth missing.");
        controller.Seek(8.2); Check(previews().Length == 0 && dangers() == 0, "Production SohrKhai lightning finish leaked.");
        controller.Seek(1.1); Check(dangers() == 1, "Production SohrKhai cloud seek failed."); stop();

        center = new(400, 0, -400);
        var records = new List<string>
        {
            Header(1112), Frame(1112, 0, [Actor(1, 0x3D17, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 5)], full: true),
            Frame(1112, .1, [Actor(1, 0x3D17, center, combat: true, cast: Cast(32132, 5, center, target: 101))]),
            Frame(1112, 5.1, [Actor(1, 0x3D17, center, combat: true)]),
        };
        for (var i = 0; i < 5; ++i) records.Add(EffectEvent(5.5 + i, 1, i == 0 ? 32132u : 32133u, (uint)i));
        records.Add(Frame(1112, 15, [Actor(1, 0x3D17, center, combat: true, cast: new
        {
            ActionId = 32132, TargetId = 101UL, Location = Pose(center), StartedAt = Start.AddSeconds(15), FinishAt = Start.AddSeconds(20.3),
        })]));
        records.Add(Frame(1112, 20, [Actor(1, 0x3D17, center, combat: true)]));
        for (var i = 0; i < 6; ++i) records.Add(EffectEvent(20.5 + i, 1, i == 0 ? 32132u : 32133u, (uint)(10 + i)));
        load(id, records);
        controller.Seek(9); Check(Goals() == 1 && previews().Length == 1, "Production SohrKhai AkhMorn first round ended early.");
        controller.Seek(9.6); Check(Goals() == 0 && previews().Length == 0, "Production SohrKhai fifth hit leaked.");
        controller.Seek(24.6); Check(Goals() == 1, "Production SohrKhai second round ended at fifth hit.");
        controller.Seek(25.6); Check(Goals() == 0, "Production SohrKhai sixth hit leaked.");
        controller.Seek(6); Check(Goals() == 1, "Production SohrKhai AkhMorn seek failed."); stop();
        records =
        [
            Header(1112), Frame(1112, 0, [Actor(1, 0x3D17, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x233C, center - Vector3.UnitZ * 15)], full: true),
            Frame(1112, .1, [Actor(10, 0x233C, center - Vector3.UnitZ * 15, cast: Cast(32129, 5, center - Vector3.UnitZ * 15))]),
            Frame(1112, 5.1, [Actor(10, 0x233C, center - Vector3.UnitZ * 15)]),
        ];
        for (var i = 0; i < 5; ++i)
        {
            records.Add(Frame(1112, 5.5 + i, [Actor(10, 0x233C, center + Vector3.UnitZ * (-15 + 6.8f * i))]));
            records.Add(EffectEvent(5.5 + i, 10, i == 0 ? 32129u : 32130u, (uint)i));
        }
        records.Add(Frame(1112, 11, [Actor(100, 0, center, stackedStatus: 911, rotation: MathF.PI / 2)]));
        records.Add(Frame(1112, 12, [Actor(100, 0, center, stackedStatus: 911)], legacyMovement: true, cameraAzimuth: 0));
        records.Add(ResearchIconEvent(13, 100, 311)); records.Add(EffectEvent(19, 11, 32139, 1, target: 100));
        records.Add(Frame(1112, 20, [Actor(100, 0, center)]));
        load(id, records);
        controller.Seek(.2); Check(dangers() == 3 && previews().Length == 3, "Production SohrKhai exaflare preview cap missing.");
        controller.Seek(9.6); Check(dangers() == 0 && previews().Length == 0, "Production SohrKhai fifth orb leaked.");
        controller.Seek(11.1); Check(dangers() == 1 && previews().Length == 3, "Production SohrKhai thin ice planning missing.");
        controller.Seek(12.1);
        Check(((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().Count() == 1, "Production SohrKhai legacy ice constraint missing.");
        controller.Seek(19.1); Check(previews().Length == 3, "Production SohrKhai spread did not clear while retaining ice.");
        controller.Seek(20.1); Check(dangers() == 0 && previews().Length == 0, "Production SohrKhai ice loss leaked.");
        controller.Seek(.2); Check(dangers() == 3, "Production SohrKhai orb backward seek failed."); stop();

        foreach (var (boss, caster, action, position) in new[]
        {
            (0x1604u, 0x1606u, 4700u, new Vector3(-400.01f, 0, 90.185f)), (0x1604u, 0x1605u, 6237u, new Vector3(-400.01f, 0, 90.185f)),
            (0x1604u, 0x1604u, 6236u, new Vector3(-400.01f, 0, 90.185f)), (0x160Eu, 0x160Fu, 2478u, new Vector3(405, 0, 210)),
            (0x1612u, 0x1613u, 4724u, new Vector3(364, 0, -225)), (0x1612u, 0x1611u, 4794u, new Vector3(364, 0, -225)),
        })
        {
            var source = boss == caster ? 1UL : 10UL;
            var actors = new List<object> { Actor(1, boss, position, combat: true), Actor(100, 0, position) };
            if (source != 1) actors.Add(Actor(source, caster, position, combat: true));
            load(id,
            [
                Header(1112), Frame(1112, 0, actors, full: true),
                Frame(1112, .1, [Actor(source, caster, position, combat: true, cast: Cast(action, 3, position))]),
                Frame(1112, 3.1, [Actor(source, caster, position, combat: true)]),
            ]);
            controller.Seek(.2); Check(dangers() == 1 && previews().Length == 1, "Production SohrKhai trash cast missing: " + action);
            controller.Seek(3.2); Check(dangers() == 0 && previews().Length == 0, "Production SohrKhai trash cast leaked: " + action); stop();
        }
        center = new(364, 0, -225);
        load(id,
        [
            Header(1112), Frame(1112, 0, [Actor(1, 0x1612, center, targetable: false), Actor(100, 0, center),
                Actor(10, 0x1611, center, combat: true), Actor(11, 0x1636, center), Actor(12, 0x1EA032, center)], full: true),
            Frame(1112, .1, [Actor(12, 0x1EA032, center, eventAnimation: 0x00040008),
                Actor(11, 0x1636, center, cast: Cast(6241, 6, center))]),
            Frame(1112, 6.1, [Actor(11, 0x1636, center)]),
            EffectEvent(13.1, 10, 564, 1),
            Frame(1112, 14, [Actor(1, 0x1612, center, dead: true), Actor(10, 0x1611, center, dead: true)]),
            Frame(1112, 15, [], removed: [1]),
        ]);
        controller.Seek(.2); Check(dangers() == 2 && previews().Length == 2, "Production SohrKhai Cauterize/Touchdown missing.");
        controller.Seek(6.2); Check(dangers() == 1 && previews().Length == 1, "Production SohrKhai Touchdown did not survive cast.");
        controller.Seek(13.2); Check(dangers() == 0, "Production SohrKhai Touchdown effect leaked.");
        controller.Seek(14.1); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary != null, "Production SohrKhai dead hidden primary ended stage.");
        controller.Seek(15.1); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Production SohrKhai destroyed primary leaked stage.");
        controller.Seek(.2); Check(dangers() == 2, "Production SohrKhai Blizzard backward seek failed."); stop();
        Console.WriteLine("PASS production native dungeons: SohrKhai six modules, Moglin cross and ban, wall segments and safe complement, knockback, cloud birth, five/six AkhMorn, five-step exaflare, thin ice constraints and camera, trash casts, bridge, delayed Touchdown, hidden-primary lifecycle and backward seek");
    }
}

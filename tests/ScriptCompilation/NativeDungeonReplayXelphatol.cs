using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunXelphatol(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.xelphatol";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        ulong[] Forbidden() => (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!;
        bool Active() => Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null;
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        Vector3 center = new(-74.525f, 0, -70.125f);
        load(id,
        [
            Header(1113), Frame(1113, 0, [Actor(1, 0x179B, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x179E, center + Vector3.UnitX * 10), Actor(11, 0x179D, center)], full: true),
            Frame(1113, .1, [Actor(10, 0x179E, center + Vector3.UnitX * 10, cast: Cast(6604, 6, center + Vector3.UnitX * 10))]),
            StatusEvent(1, 1, 775, 0, true), StatusEvent(1, 10, 775, 0, true),
            Frame(1113, 1.1, [Actor(1, 0x179B, center, combat: true, stackedStatus: 775), Actor(10, 0x179E, center + Vector3.UnitX * 10,
                cast: Cast(6604, 6, center + Vector3.UnitX * 10), stackedStatus: 775)]),
            Frame(1113, 6.1, [Actor(10, 0x179E, center + Vector3.UnitX * 10, stackedStatus: 775)]),
            Frame(1113, 8, []),
        ]);
        controller.Seek(.2); Check(dangers() == 1 && previews().Length == 1 && Goals() == 0, "Production Xelphatol safe circle complement missing.");
        controller.Seek(1.2); Check(Forbidden().Order().SequenceEqual([1UL, 10UL]), "Production Xelphatol turret shield missing.");
        controller.Seek(6.5); Check(dangers() == 1, "Production Xelphatol HotBlast cleared at natural finish.");
        controller.Seek(7.5); Check(dangers() == 0 && previews().Length == 0, "Production Xelphatol HotBlast expiry leaked.");
        controller.Seek(.2); Check(dangers() == 1 && Forbidden().Length == 0, "Production Xelphatol HotBlast seek failed."); stop();

        center = new(245.28799f, 0, 13.62114f);
        load(id,
        [
            Header(1113), Frame(1113, 0, [Actor(1, 0x179F, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 5),
                Actor(20, 0x1EA187, center)], full: true),
            Frame(1113, .1, [Actor(20, 0x1EA187, center, eventAnimation: 0x00010002),
                Actor(1, 0x179F, center, combat: true, cast: Cast(6607, 5, center))]),
            Frame(1113, 2, [Actor(10, 0x17A0, center + Vector3.UnitX * 18)]),
            Frame(1113, 5.1, [Actor(1, 0x179F, center, combat: true)]),
            ResearchIconEvent(6, 100, 139), EffectEvent(11.1, 1, 32556, 1),
            Frame(1113, 12, [], removed: [10]),
        ]);
        controller.Seek(.05); Check(dangers() == 1 && previews().Length == 1, "Production Xelphatol initial shrink warning missing.");
        controller.Seek(.2); Check(dangers() == 2 && Goals() == 0 && previews().Length == 6, "Production Xelphatol wall knockback missing.");
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)boundary.Contains(center + Vector3.UnitX * 25, 0f), "Production Xelphatol shrink replay ignored.");
        controller.Seek(2.1); Check(dangers() == 3 && previews().Length == 6, "Production Xelphatol delayed whirlwind failed.");
        controller.Seek(5.5); Check(dangers() == 1 && previews().Length == 1, "Production Xelphatol knockback finish leaked.");
        controller.Seek(6.1); Check(previews().Length == 2, "Production Xelphatol icon spread missing.");
        controller.Seek(12.1); Check(previews().Length == 0 && dangers() == 0, "Production Xelphatol spread/wind cleanup failed.");
        controller.Seek(2.1); Check(dangers() == 3, "Production Xelphatol shrink and whirlwind seek failed."); stop();

        center = new(317.8f, 0, -416.19f);
        load(id,
        [
            Header(1113), Frame(1113, 0, [Actor(1, 0x17A2, center, combat: true), Actor(100, 0, center),
                Actor(101, 0, center + Vector3.UnitX * 5), Actor(10, 0xD25, center), Actor(11, 0x17A4, center)], full: true),
            ResearchIconEvent(.05, 101, 62),
            Frame(1113, .1, [Actor(10, 0xD25, center, cast: Cast(6619, 6, center)),
                Actor(11, 0x17A4, center, cast: Cast(6620, 5, center, MathF.PI / 2))]),
            EffectEvent(5.6, 10, 6617, 1),
            Frame(1113, 6.1, [Actor(10, 0xD25, center), Actor(11, 0x17A4, center)]),
        ]);
        controller.Seek(.2); Check(previews().Length == 3 && dangers() == 2 && Goals() == 1, "Production Xelphatol Garuda stack combination missing.");
        controller.Seek(6.2); Check(previews().Length == 0 && dangers() == 0 && Goals() == 0, "Production Xelphatol Garuda combination cleanup failed.");
        controller.Seek(.2); Check(Goals() == 1, "Production Xelphatol stack seek failed."); stop();

        foreach (var (boss, caster, action, position) in new[]
        {
            (0x17AFu, 0x17AFu, 2478u, new Vector3(-154, 0, 211)),
            (0x17AFu, 0x17AFu, 2478u, new Vector3(-115, 0, 40)),
            (0x17B5u, 0x17B5u, 6625u, new Vector3(70.5f, 0, -56)),
            (0x17B5u, 0x17B5u, 6625u, new Vector3(178, 0, -4)),
            (0x17BAu, 0x17B7u, 6627u, new Vector3(125, 0, -37)),
            (0x17BAu, 0x17B7u, 6627u, new Vector3(420, 0, -254)),
            (0x17BAu, 0x17B7u, 6627u, new Vector3(316, 0, -133)),
            (0x17B8u, 0x17B8u, 6628u, new Vector3(362.745f, 0, -400.815f)),
        })
        {
            var source = boss == caster ? 1UL : 10UL;
            var actors = new List<object> { Actor(1, boss, position, combat: true), Actor(100, 0, position) };
            if (source != 1) actors.Add(Actor(source, caster, position, combat: true));
            load(id,
            [
                Header(1113), Frame(1113, 0, actors, full: true),
                Frame(1113, .1, [Actor(source, caster, position, combat: true, cast: Cast(action, 3, position, MathF.PI / 2))]),
                Frame(1113, 4, [], removed: [1, 10]),
            ]);
            controller.Seek(.2); Check(dangers() > 0 && previews().Length > 0, $"Production Xelphatol trash {boss:X} at {position} did not start.");
            controller.Seek(4.1); Check(dangers() == 0 && previews().Length == 0, "Production Xelphatol trash finish retained output."); stop();
        }
        center = new(125, 0, -37);
        load(id,
        [
            Header(1113), Frame(1113, 0, [Actor(1, 0x17BA, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1113, 1, [Actor(20, 0x1EA165, new(-1000, 0, -1000))]),
            Frame(1113, 2, [Actor(1, 0x17BA, center, combat: true)]),
        ]);
        controller.Seek(2.1); Check(!Active(), "Production Xelphatol targetable key restarted phase.");
        controller.Seek(.1); Check(Active(), "Production Xelphatol pre-key seek did not rebuild."); stop();
        Console.WriteLine("PASS native Xelphatol seven modules, exact arenas, shields, wall knockback, delayed whirlwinds, stacks and seek cleanup");
    }
}

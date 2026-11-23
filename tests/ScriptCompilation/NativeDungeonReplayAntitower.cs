using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunAntitower(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.antitower";
        object Script()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            return running.GetType().GetProperty("Script")!.GetValue(running)!;
        }
        object Encounter()
        {
            var script = Script();
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        ulong[] Forbidden() => (ulong[])Encounter().GetType().GetProperty("ForbiddenTargets")!.GetValue(Encounter())!;
        int Goals() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas).Cast<object>().Count();
        Vector3 center = new(-365, 0, -250);
        var records = new List<string>
        {
            Header(1111),
            Frame(1111, 0, [Actor(1, 0x14FC, center, combat: true), Actor(100, 0, center), Actor(10, 0x233C, center)], full: true),
        };
        for (var i = 1; i <= 12; ++i) records.Add(EffectEvent(i, 10, 5540, (uint)i, rotation: i == 1 ? MathF.PI / 2 : 0));
        load(id, records);
        controller.Seek(1.1); Check(previews().Length == 1 && dangers() == 1, "Production Antitower first croak missing.");
        controller.Seek(11.1); Check(previews().Length == 1 && dangers() == 1, "Production Antitower croak ended before twelfth.");
        controller.Seek(12.1); Check(previews().Length == 0 && dangers() == 0, "Production Antitower twelfth croak leaked.");
        controller.Seek(5.1); Check(dangers() == 1, "Production Antitower croak seek failed."); stop();
        load(id,
        [
            Header(1111),
            Frame(1111, 0, [Actor(1, 0x14FC, center, combat: true), Actor(100, 0, center)], full: true),
            Frame(1111, 1, [Actor(10, 0x14FE, center), Actor(11, 0x14FE, center + Vector3.UnitX * 5)]),
            EffectEvent(9.7, 10, 5543, 1),
            Frame(1111, 10, [Actor(10, 0x14FE, center)]),
        ]);
        controller.Seek(1.1); Check(previews().Length == 2 && dangers() == 2, "Production Antitower chirp births missing.");
        controller.Seek(9.8); Check(previews().Length == 0 && dangers() == 0, "Production Antitower harmony did not clear all.");
        controller.Seek(10.1); Check(dangers() == 0, "Production Antitower chirp snapshot resurrected.");
        controller.Seek(1.1); Check(dangers() == 2, "Production Antitower chirp seek failed."); stop();

        center = new(185.8f, 0, 137.5f);
        load(id,
        [
            Header(1111),
            Frame(1111, 0, [Actor(1, 0x3D82, center, combat: true, tethers: [new { Id = 2, TargetId = 100UL }]),
                Actor(100, 0, center - Vector3.UnitX * 5), Actor(10, 0x3D83, center + Vector3.UnitZ * 10)], full: true),
            EffectEvent(2, 1, 31840, 1, target: 100),
            Frame(1111, 3, [Actor(1, 0x3D82, center, combat: true, tethers: [new { Id = 2, TargetId = 100UL }])]),
        ]);
        controller.Seek(.2); Check(Goals() == 1 && previews().Length == 2, "Production Antitower charge and shelter missing.");
        dynamic goal = runtime.Manager.BuildSnapshot(runtime.CurrentTime).GoalAreas[0].Geometry;
        Check((bool)goal.Contains(center + Vector3.UnitZ * 15, 0f) && !(bool)goal.Contains(center + Vector3.UnitX * 15, 0f),
            "Production Antitower shelter geometry differs.");
        controller.Seek(3.1); Check(Goals() == 0 && dangers() == 0 && previews().Length == 0, "Production Antitower tether resurrected.");
        controller.Seek(.2); Check(Goals() == 1, "Production Antitower meteor seek failed."); stop();

        center = new(232, 0, -182);
        load(id,
        [
            Header(1111),
            Frame(1111, 0, [Actor(1, 0x1503, center, combat: true), Actor(10, 0x1502, center, combat: true),
                Actor(100, 0, center - Vector3.UnitZ * 5, targetId: 10)], full: true),
            Frame(1111, .1, [Actor(10, 0x1502, center, combat: true, cast: Cast(5557, 3, center))]),
            Frame(1111, 3.1, [Actor(10, 0x1502, center, combat: true, stackedStatus: 680, statusParam: 14)], removed: [1]),
            StatusEvent(3.1, 10, 680, 14, true),
            Frame(1111, 4, [Actor(100, 0, center + Vector3.UnitZ * 5, targetId: 10)]),
            StatusEvent(5, 10, 680, 0, false),
            Frame(1111, 5.1, [Actor(10, 0x1502, center, combat: true)]),
            ResearchIconEvent(6, 100, 73),
            EffectEvent(9.1, 10, 5559, 1),
            Frame(1111, 10, [], removed: [10]),
        ]);
        controller.Seek(.2); Check(previews().Length == 3 && dangers() == 3 && Forbidden().Length == 0, "Production Antitower imminent parry missing.");
        controller.Seek(3.2); Check(previews().Length == 3 && Forbidden().SequenceEqual([10UL]), "Production Antitower phase transition or parry ban missing.");
        controller.Seek(4.1); Check(Forbidden().Length == 0 && dangers() == 0, "Production Antitower front attack stayed forbidden.");
        controller.Seek(5.2); Check(previews().Length == 0 && Forbidden().Length == 0, "Production Antitower parry loss leaked.");
        controller.Seek(8.5); Check(previews().Length == 2, "Production Antitower icon bait or gaze eye missing.");
        var hints = Script().GetType().GetProperty("Hints", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(Script())!;
        var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
            "Production Antitower replay published PR facing.");
        controller.Seek(9.2); Check(previews().Length == 0 && dangers() == 0, "Production Antitower gaze effect leaked.");
        controller.Seek(10.1); Check(runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary == null, "Production Antitower NPC dolls removal leaked stage.");
        controller.Seek(3.2); Check(Forbidden().SequenceEqual([10UL]), "Production Antitower parry backward seek failed."); stop();

        foreach (var (boss, caster, action, position) in new[]
        {
            (0x14FCu, 0x233Cu, 5538u, new Vector3(-365, 0, -250)),
            (0x14FCu, 0x233Cu, 5979u, new Vector3(-365, 0, -250)),
            (0x14FCu, 0x233Cu, 5977u, new Vector3(-365, 0, -250)),
            (0x3D82u, 0x3D83u, 31838u, new Vector3(185.8f, 0, 137.5f)),
            (0x3D82u, 0x3D83u, 31839u, new Vector3(185.8f, 0, 137.5f)),
            (0x3D82u, 0x3D82u, 31832u, new Vector3(185.8f, 0, 137.5f)),
            (0x1503u, 0x1504u, 5552u, new Vector3(232, 0, -182)),
            (0x1503u, 0x15E3u, 5552u, new Vector3(232, 0, -182)),
            (0x1503u, 0x15E1u, 5552u, new Vector3(232, 0, -182)),
            (0x1503u, 0x1503u, 5551u, new Vector3(232, 0, -182)),
            (0x1503u, 0x15E2u, 5551u, new Vector3(232, 0, -182)),
            (0x1503u, 0x15E0u, 5551u, new Vector3(232, 0, -182)),
        })
        {
            var source = boss == caster ? 1UL : 10UL;
            var actors = new List<object> { Actor(1, boss, position, combat: true), Actor(100, 0, position) };
            if (source != 1) actors.Add(Actor(source, caster, position));
            load(id,
            [
                Header(1111), Frame(1111, 0, actors, full: true),
                Frame(1111, .1, [Actor(source, caster, position, combat: true, cast: Cast(action, 3, position))]),
                Frame(1111, 3.1, [Actor(source, caster, position, combat: true)]),
            ]);
            controller.Seek(.2); Check(previews().Length == 1 && dangers() == 1, "Production Antitower cast missing: " + action);
            controller.Seek(3.2); Check(previews().Length == 0 && dangers() == 0, "Production Antitower cast finish leaked: " + action); stop();
        }
        Console.WriteLine("PASS production native dungeons: Antitower three bosses, twelve croaks, chirp births, meteor shelter, charge resolve, doll transition, directional parry ban, icon gaze replay isolation, all cast sources and backward seek");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunFractal(Assembly assembly, object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers, Func<int> goals)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.fractal-continuum";
        Vector3 Interior(string method)
        {
            dynamic arena = assembly.GetType("MasterPlayer.Mechanics.Dungeons.FractalArenas")!
                .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [0f])!;
            var center = (Vector3)arena.Position;
            if ((bool)arena.Contains(center, 0f)) return center;
            dynamic b = arena.GetBounds();
            for (var x = (float)b.MinX + .5f; x < (float)b.MaxX; x += 1)
                for (var z = (float)b.MinZ + .5f; z < (float)b.MaxZ; z += 1)
                    if ((bool)arena.Contains(new Vector3(x, 0, z), 0f)) return new(x, 0, z);
            throw new InvalidOperationException("No interior in " + method);
        }
        object Interaction()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("fruitInteraction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        var center = Interior("PhantomRay");
        load(id,
        [
            Header(430),
            Frame(430, 0, [Actor(1, 0x1012, center, combat: true), Actor(100, 0, center + Vector3.UnitX * 10),
                Actor(10, 0xD25, center), Actor(11, 0xD25, center)], full: true),
            Frame(430, .1, [Actor(10, 0xD25, center, cast: Cast(3965, 6, center)),
                Actor(11, 0xD25, center, cast: Cast(3965, 9, center, MathF.PI))]),
            Frame(430, 6.1, [Actor(10, 0xD25, center)]),
            Frame(430, 9.1, [Actor(11, 0xD25, center)]),
        ]);
        controller.Seek(.2); Check(previews().Length == 2 && dangers() == 2 && goals() == 1, "Production Fractal DoubleSever lost concurrency or close-origin goal.");
        controller.Seek(6.2); Check(previews().Length == 1 && dangers() == 1 && goals() == 1, "Production Fractal DoubleSever first resolution cleared both.");
        controller.Seek(9.2); Check(previews().Length == 0 && dangers() == 0 && goals() == 0, "Production Fractal DoubleSever final resolution leaked.");
        controller.Seek(.2); Check(dangers() == 2, "Production Fractal DoubleSever seek failed."); stop();

        center = Interior("Minotaur");
        load(id,
        [
            Header(430),
            Frame(430, 0, [Actor(1, 0x1013, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1E99EA, center + Vector3.UnitX * 2, hitboxRadius: 2),
                Actor(11, 0x1E99E8, center + Vector3.UnitX * 10, hitboxRadius: 2)], full: true),
            Frame(430, .1, [Actor(1, 0x1013, center, combat: true, cast: Cast(3975, 11, center))]),
            Frame(430, 11.1, [Actor(1, 0x1013, center, combat: true)]),
        ]);
        controller.Seek(.2); Check(previews().Length == 2 && dangers() == 0 && goals() == 1, "Production Fractal incubator guidance missing.");
        var interaction = Interaction();
        Check(!(bool)interaction.GetType().GetProperty("Holding", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(interaction)!
            && !(bool)interaction.GetType().GetProperty("Attempted", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(interaction)!,
            "Production Fractal replay entered native interaction.");
        controller.Seek(11.2); Check(goals() == 0 && previews().Length == 0, "Production Fractal incubator stop leaked."); stop();

        center = new(0, 0, -350);
        var tile = new Vector3(-15, 0, -365);
        load(id,
        [
            Header(430),
            Frame(430, 0, [Actor(1, 0x1018, center, combat: true), Actor(100, 0, center),
                Actor(10, 0x1019, tile), Actor(11, 0xD25, center), Actor(12, 0x101A, center)], full: true),
            CastEvent(.1, 1, 3981, 6, new(-5, 0, -355)),
            EffectEvent(.2, 11, 3983, 1),
            Frame(430, 1, [], removed: [12]),
            Frame(430, 2, [Actor(10, 0x1019, tile, cast: new { ActionId = 3986u, TargetId = 0UL, Location = Pose(tile),
                Rotation = 0f, StartedAt = Start.AddSeconds(2), FinishAt = Start.AddSeconds(5.3) })]),
            Frame(430, 5, [Actor(10, 0x1019, tile)]),
            EffectEvent(5.1, 10, 3986, 2),
            JsonSerializer.Serialize(new { t = 6, kind = "DirectorUpdate", type = "PromeRotation.LogSystem.LogSystemDirectorUpdateEvent",
                data = new { Command = 0x80000005u, Timestamp = Start.AddSeconds(6) } }),
        ]);
        controller.Seek(.3);
        dynamic boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)boundary.Contains(new Vector3(-5, 0, -350), 0f) && previews().Length == 1, "Production Fractal row/column floor or mine missing.");
        controller.Seek(1.1); Check(previews().Length == 0 && dangers() == 0, "Production Fractal snapshot removal retained mine.");
        controller.Seek(2.1); Check(previews().Length == 1 && dangers() == 1, "Production Fractal alarm preview missing.");
        controller.Seek(5.05);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((bool)boundary.Contains(tile, 0f) && previews().Length == 0, "Production Fractal alarm finish destroyed floor early.");
        controller.Seek(5.2);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)boundary.Contains(tile, 0f), "Production Fractal alarm effect did not destroy floor.");
        controller.Seek(6.1);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check((bool)boundary.Contains(center, 0f) && (bool)boundary.Contains(tile, 0f), "Production Fractal DirectorUpdate failed reset.");
        controller.Seek(5.2);
        boundary = runtime.Manager.BuildSnapshot(runtime.CurrentTime).ArenaBoundary;
        Check(!(bool)boundary.Contains(tile, 0f), "Production Fractal backward seek retained future floor repair."); stop();

        foreach (var (boss, caster, action, method) in new[]
        {
            (0x1020u, 0x1020u, 4556u, "Clockwork"), (0x101Du, 0x101Bu, 1135u, "Manikin"),
            (0x1023u, 0x1024u, 2145u, "Naga"), (0x1E990Eu, 0x1028u, 1427u, "Emerald"),
            (0x1E9910u, 0x102Au, 2706u, "Crimson"),
        })
        {
            center = Interior(method);
            var source = boss == caster ? 1UL : 10UL;
            var actors = new List<object> { Actor(1, boss, center, combat: true), Actor(100, 0, center) };
            if (source != 1) actors.Add(Actor(source, caster, center, combat: true));
            load(id,
            [
                Header(430), Frame(430, 0, actors, full: true),
                Frame(430, .1, [Actor(source, caster, center, combat: true, cast: Cast(action, 3, center))]),
                Frame(430, 3.1, [Actor(source, caster, center, combat: true)]),
            ]);
            controller.Seek(.2); Check(dangers() == 1 && previews().Length >= 1, "Production Fractal trash cast missing: " + method);
            controller.Seek(3.2); Check(dangers() == 0, "Production Fractal trash cast cancel leaked: " + method);
            controller.Seek(.2); Check(dangers() == 1, "Production Fractal trash seek failed: " + method); stop();
        }
        center = Interior("Violet");
        load(id,
        [
            Header(430),
            Frame(430, 0, [Actor(1, 0x1E990F, center, targetable: false), Actor(100, 0, center),
                Actor(10, 0x102B, center, combat: true)], full: true),
            Frame(430, 1, [Actor(1, 0x1E990F, center)]),
            Frame(430, 2, [Actor(1, 0x1E990F, center, eventState: 7)]),
        ]);
        controller.Seek(.2); Check(goals() == 0 && previews().Length == 0, "Production Fractal untargetable controller leaked interaction.");
        controller.Seek(1.1); Check(goals() == 1 && previews().Length == 1, "Production Fractal controller failed interaction goal.");
        controller.Seek(2.1); Check(goals() == 0 && previews().Length == 0, "Production Fractal completed controller leaked."); stop();
        Console.WriteLine("PASS production native dungeons: Fractal three bosses and six trash modules, concurrent sever, incubator replay isolation, floor, mines, director reset, control states and backward seek");
    }
}

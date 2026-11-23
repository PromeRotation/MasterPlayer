using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunSmileton(object managerObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject; dynamic controller = controllerObject;
        const string id = "masterplayer.dungeon.smileton";
        object Encounter()
        {
            var running = ((IEnumerable)manager.Running.Values).Cast<object>().Single();
            var script = running.GetType().GetProperty("Script")!.GetValue(running)!;
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
        }
        object[] Aoes() => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!).Cast<object>().ToArray();
        object Landing() => Aoes().Single(a => (string)a.GetType().GetProperty("Id")!.GetValue(a)! == "smileton/knockback/10/landing");
        int Damage() => ((IEnumerable)Encounter().GetType().GetProperty("PredictedDamage")!.GetValue(Encounter())!).Cast<object>().Count();
        object[] party = [new { Slot = 0, EntityId = 100UL }, new { Slot = 1, EntityId = 101UL }];
        var c = new Vector3(-45, 0, -20);
        load(id,
        [
            Header(976), Frame(976, 0, [Actor(1, 0x34CF, c, combat: true), Actor(100, 0, c + Vector3.UnitX * 18),
                Actor(101, 0, c), Actor(10, 0x233C, c)], full: true, party: party),
            Frame(976, .1, [Actor(10, 0x233C, c, cast: Cast(27742, 5, c, MathF.PI / 2))], party: party),
            Frame(976, 1, [Actor(100, 0, c + Vector3.UnitX * 18, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(20))], party: party),
            Frame(976, 2, [Actor(100, 0, c)], party: party),
            Frame(976, 5.5, [Actor(10, 0x233C, c)], party: party),
        ]);
        controller.Seek(.5);
        var landing = (Vector3)Landing().GetType().GetProperty("Position")!.GetValue(Landing())!;
        Check(previews().Length == 2 && dangers() == 0 && Vector3.Distance(landing, c + Vector3.UnitX * 19.999f) < .002f,
            "Production directional wall stop or locked rotation missing.");
        controller.Seek(1.1); Check(previews().Length == 0, "Production immune projection leaked.");
        controller.Seek(2.1); Check(previews().Length == 2, "Production immunity removal lost projection.");
        controller.Seek(5.6); Check(previews().Length == 0, "Production knockback finish leaked.");
        controller.Seek(.5); Check(previews().Length == 2, "Face Seek failed."); stop();
        c = new(64, 0, -112);
        load(id,
        [
            Header(976), Frame(976, 0, [Actor(1, 0x34D1, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(976, .1, [Actor(1, 0x34D1, c, combat: true, cast: Cast(26438, 7, c)),
                Actor(10, 0x34E9, c, cast: Cast(26439, 10, c + Vector3.UnitX * 5)),
                Actor(11, 0x233C, c, cast: Cast(26441, 5, c, .9f))], party: party),
            Frame(976, 5.5, [Actor(11, 0x233C, c)], party: party),
            Frame(976, 7.5, [Actor(1, 0x34D1, c, combat: true)], party: party),
            Frame(976, 10.5, [Actor(10, 0x34E9, c)], party: party),
        ]);
        controller.Seek(1); Check(previews().Length == 3 && dangers() == 3, "Production worker jumps/cone missing.");
        controller.Seek(5.6); Check(previews().Length == 2 && dangers() == 2, "Production cone end erased jumps.");
        controller.Seek(7.6); Check(previews().Length == 1, "Production boss jump end erased worker.");
        controller.Seek(10.6); Check(previews().Length == 0, "Production worker jump leaked.");
        controller.Seek(1); Check(previews().Length == 3, "Frameworker Seek failed."); stop();
        c = new(-22, 0, -44);
        load(id,
        [
            Header(976), Frame(976, 0, [Actor(1, 0x34D3, c, combat: true), Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 10)], full: true, party: party),
            Frame(976, .1, [Actor(1, 0x34D3, c, combat: true, cast: Cast(26447, 8, c, MathF.PI / 2)),
                Actor(10, 0x3585, c, cast: Cast(26445, 1.5, c + Vector3.UnitX * 3))], party: party),
            Frame(976, 2, [Actor(10, 0x3585, c)], party: party),
            Frame(976, 8.5, [Actor(1, 0x34D3, c, combat: true)], party: party),
            Frame(976, 9, [Actor(1, 0x34D3, c, combat: true, cast: Cast(26451, 5, c, target: 100, startedAt: 9))], party: party),
            Frame(976, 14.4, [Actor(1, 0x34D3, c, combat: true)], party: party),
            Frame(976, 15, [Actor(1, 0x34D3, c, combat: true, cast: Cast(26449, 5, c, target: 100, startedAt: 15))], party: party),
            Frame(976, 20.4, [Actor(1, 0x34D3, c, combat: true)], party: party),
        ]);
        controller.Seek(1); Check(previews().Length == 2 && dangers() == 2, "Production bomb/cleave missing.");
        controller.Seek(2.1); Check(previews().Length == 1, "Production bomb finish erased cleave.");
        controller.Seek(8.6); Check(previews().Length == 0, "Production cleave leaked.");
        controller.Seek(9.1); Check(previews().Length == 1 && dangers() == 1, "Production stack/hard constraint missing.");
        controller.Seek(14.5); Check(previews().Length == 0 && dangers() == 0, "Production stack leaked.");
        controller.Seek(15.1); Check(Damage() == 1, "Production tankbuster prediction missing.");
        controller.Seek(20.5); Check(Damage() == 0, "Production cast tankbuster leaked.");
        controller.Seek(1); Check(previews().Length == 2, "BigCheese Seek failed."); stop();
        Console.WriteLine("PASS production Smileton: three WIP source modules, directional wall-clamped knockback, immunity, independent jumps/cones/bombs/cleaves, R6 stack, cast buster, seek/stop; source Face color lanes remain unimplemented");
    }
}

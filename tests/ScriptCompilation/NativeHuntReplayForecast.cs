using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntForecast(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        dynamic manager = managerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object[] Areas(string group) => previews().Where(p => ((string)Value(p, "Id")).Contains("/" + group + "/"))
            .OrderBy(p => int.Parse(((string)Value(p, "Id")).Split('/')[^1])).ToArray();
        object[] Navigation() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        DateTime[] Activations() => Navigation().Select(a => (DateTime)Value(((IEnumerable)Value(a, "DangerWindows")).Cast<object>().Single(), "DangerFrom")).Order().ToArray();
        const uint territory = 1187;
        var origin = new Vector3(10, 0, 20);
        var moved = new Vector3(80, 0, 90);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x4580, origin, combat: true), Actor(100, 0, origin), Actor(101, 0, moved)], full: true, party: party),
            Frame(territory, 1, [Actor(1, 0x4580, origin, combat: true, cast: Cast(39797, 5, origin, startedAt: 1))]),
            CastEvent(1, 1, 39797, 5, origin), CastEvent(1, 1, 39797, 5, origin),
            Frame(territory, 7, [Actor(1, 0x4580, moved, combat: true, rotation: 2)]),
            EffectEvent(8, 1, 39755, 1),
            Frame(territory, 12, [Actor(1, 0x4580, moved, combat: true, cast: Cast(39756, 1, moved, startedAt: 12))]),
            Frame(territory, 14, [Actor(1, 0x4580, moved, combat: true)]),
            Frame(territory, 15, [Actor(1, 0x4580, moved, combat: true, cast: Cast(39755, 1, moved, startedAt: 15))]),
            Frame(territory, 17, [Actor(1, 0x4580, moved, combat: true)]),
            Frame(territory, 18, [Actor(1, 0x4580, moved, combat: true, cast: Cast(39760, 5, moved, startedAt: 18))], party: party),
            Frame(territory, 24, [Actor(1, 0x4580, moved, combat: true)]),
            Frame(territory, 25, [Actor(1, 0x4580, moved, combat: false)]),
            Frame(territory, 26, []),
        ]);
        controller.Seek(.5);
        Check(dangers() == 1 && Areas("marching-samba").Length == 0, "Pkuucha idle active component lost permanent R14 navigation.");
        dynamic constraint = Value(Navigation().Single(), "Geometry");
        Check(!(bool)constraint.Contains(origin, 0f) && (bool)constraint.Contains(origin + Vector3.UnitX * 14.1f, 0f), "Pkuucha R14 complement differs.");
        controller.Seek(2d);
        Check(dangers() == 3 && Areas("marching-samba").Length == 2, "Pkuucha did not navigate both future stages.");
        var clocks = Activations().Where(t => t > Start).ToArray();
        Check(clocks.Length == 2 && Math.Abs((clocks[0] - Start.AddSeconds(8)).TotalSeconds) < .00001
            && Math.Abs((clocks[1] - Start.AddSeconds(12)).TotalSeconds) < .00001, "Pkuucha float stage clocks differ.");
        dynamic cone = Value(Areas("marching-samba")[1], "Geometry");
        Check((bool)cone.Contains(origin + new Vector3(39, 0, .1f), 0f)
            && !(bool)cone.Contains(origin + new Vector3(39, 0, -.1f), 0f), "Pkuucha actual cone total angle differs.");
        controller.Seek(11d);
        Check(dangers() == 3 && Areas("marching-samba").Length == 2, "Pkuucha effect, visual finish or overdue time consumed FIFO.");
        constraint = Value(Navigation().Single(n => ((string)Value(n, "Geometry").GetType().Name).Contains("HuntStackJoin")), "Geometry");
        Check(!(bool)constraint.Contains(moved, 0f) && (bool)constraint.Contains(origin, 0f), "Pkuucha permanent navigation failed to follow the boss.");
        controller.Seek(14.1);
        Check(dangers() == 2 && Areas("marching-samba").Length == 1, "Pkuucha first actual finish failed FIFO.");
        controller.Seek(17.1);
        Check(dangers() == 1 && Areas("marching-samba").Length == 0, "Pkuucha final finish removed permanent range or retained a stage.");
        controller.Seek(19d);
        var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        Check(dangers() == 1 && ((string)Value(script, "PredictedDamageText")).Contains("Raidwide")
            && ((string)Value(script, "MechanicHintsText")).Contains("Raidwide (3x)"), "Pkuucha raidwide prediction differs.");
        controller.Seek(25.1);
        Check(dangers() == 0 && previews().Length == 0, "Pkuucha reset retained range or drawing.");
        controller.Seek(2d);
        Check(dangers() == 3 && Areas("marching-samba").Length == 2, "Pkuucha backward Seek failed to reconstruct stages.");
        stop();

        foreach (var forecast in new uint[] { 38520, 38521, 38522, 38523 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4397, origin, combat: true), Actor(100, 0, origin)], full: true),
                Frame(territory, 1, [Actor(1, 0x4397, origin, combat: true, cast: Cast(forecast, 7, origin, startedAt: 1))]),
                Frame(territory, 9, [Actor(1, 0x4397, origin, combat: true, cast: Cast(38530, 5, origin, startedAt: 9))]),
                CastEvent(9, 1, 38530, 5, origin), CastEvent(9, 1, 38530, 5, origin),
                Frame(territory, 15, [Actor(1, 0x4397, moved, combat: true, rotation: 2)]),
                EffectEvent(20, 1, 38527, 1), EffectEvent(20, 1, 38527, 1),
                EffectEvent(21, 1, 38525, 2), EffectEvent(22, 1, 38529, 3),
                Frame(territory, 23, [Actor(1, 0x4397, moved, combat: true, dead: true)]),
                Frame(territory, 24, []),
            ]);
            controller.Seek(2d);
            Check(dangers() == 0 && Areas("forecast").Length == 0, "Forecaster drew unexecuted recorded weather.");
            controller.Seek(10d);
            Check(Areas("forecast").Length == 2 && dangers() == (forecast == 38520 ? 1 : 2), "Forecaster two previews or pair-specific Risky differ.");
            var expected = forecast switch
            {
                38520 => new[] { "Donut", "Circle", "LocalPolygon" },
                38521 => new[] { "Circle", "RayRectangle", "Donut" },
                38522 => new[] { "LocalPolygon", "Circle", "RayRectangle" },
                _ => new[] { "RayRectangle", "Donut", "Circle" }
            };
            Check(Value(Areas("forecast")[0], "Geometry").GetType().Name.Contains(expected[0])
                && Value(Areas("forecast")[1], "Geometry").GetType().Name.Contains(expected[1]), "Forecaster replaced history with the first attack label.");
            controller.Seek(19d);
            Check(Areas("forecast").Length == 2, "Forecaster cast finish or overdue clock consumed forecast.");
            controller.Seek(20.1);
            Check(Areas("forecast").Length == 2 && Value(Areas("forecast")[0], "Geometry").GetType().Name.Contains(expected[1]),
                "Forecaster duplicate effect consumed two stages.");
            controller.Seek(21.1);
            Check(dangers() == 1 && Areas("forecast").Length == 1
                && Value(Areas("forecast")[0], "Geometry").GetType().Name.Contains(expected[2]), "Forecaster hidden third item did not promote.");
            Check(Math.Abs((Activations().Single() - Start.AddSeconds(20.4)).TotalSeconds) < .00001, "Forecaster rebased remaining prediction to the actual effect.");
            controller.Seek(22.1);
            Check(dangers() == 0 && Areas("forecast").Length == 0, "Forecaster last effect leaked.");
            controller.Seek(23.1);
            Check(previews().Length == 0, "Forecaster death retained output.");
            controller.Seek(10d);
            Check(Areas("forecast").Length == 2, "Forecaster reverse Seek lost history.");
            stop();
        }
        Console.WriteLine("PASS HuntForecast actual DLL: Pkuucha full two-stage FIFO, hidden dynamic R14, float clocks and pure raidwide; Forecaster four records, two risky previews, effect dedup, third promotion, locked poses, reverse Seek and cleanup");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntQueenHawk(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        string? Text(string name)
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return (string?)Value(script, name);
        }
        object[] Bee() => previews().Where(p => ((string)Value(p, "Id")).EndsWith("/bee")).ToArray();
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        foreach (var action in new uint[] { 39482, 39483 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x452B, default, combat: true),
                    Actor(100, 0, default, stackedStatus: 2163, statusExpiresAt: Start.AddSeconds(10))], full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x452B, default, combat: true, cast: Cast(action, 4, default))], party: party),
                CastEvent(.1, 1, action, 4, default),
                Frame(territory, 2, [Actor(100, 0, new(-20, 0, 0), stackedStatus: 2163, statusExpiresAt: Start.AddSeconds(10))], party: party),
                Frame(territory, 5, [Actor(1, 0x452B, default, combat: true)], party: party),
                StatusEvent(6, 999, action == 39482 ? 4148u : 4147u, 0, false),
                Frame(territory, 7, [], removed: [1], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == 1 && Bee().Length == 1, "QueenHawk bee AOE missing.");
            dynamic geometry = Value(Bee().Single(), "Geometry");
            Check((bool)geometry.Contains(new Vector3(0, 0, 11), 0f)
                && (bool)geometry.Contains(Vector3.Zero, 0f) == (action == 39482), "QueenHawk exact circle/donut differs.");
            var landing = previews().Single(p => ((string)Value(p, "Id")).Contains("march/100/0/landing"));
            dynamic marker = Value(landing, "Geometry");
            Check((bool)marker.Contains(new Vector3(6.6f * 3, 0, 0), 0f), "QueenHawk left march did not use three seconds.");
            Check((Text("MechanicHintsText")?.Contains("Aim outside of the AOE!") == true) == (action == 39483),
                "QueenHawk final landing hint checked another hazard or ignored bee.");
            controller.Seek(2.1);
            Check((Text("MechanicHintsText")?.Contains("Aim outside of the AOE!") == true) == (action == 39482),
                "QueenHawk moving player did not recompute last landing.");
            controller.Seek(5.1);
            Check(dangers() == 1 && Bee().Length == 1, "QueenHawk cast finish cleared persistent bee.");
            controller.Seek(6.1);
            Check(dangers() == 0 && Bee().Length == 0, "Opposite bee status lost on unrelated actor failed to clear.");
            controller.Seek(1d);
            Check(dangers() == 1 && Bee().Length == 1, "QueenHawk reverse Seek retained future loss.");
            stop();
        }
        foreach (var action in new uint[] { 39486, 39489, 39490 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x452B, default, combat: true), Actor(100, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x452B, default, combat: true, cast: Cast(action, 5, default, target: 100))], party: party),
                Frame(territory, 6, [Actor(1, 0x452B, default, combat: true)], party: party),
                Frame(territory, 7, [], removed: [1], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == (action == 39490 ? 1 : 0), "QueenHawk ordinary damage classification differs.");
            if (action != 39490) Check(Text("PredictedDamageText") != null, "QueenHawk damage prediction missing.");
            controller.Seek(6.1);
            Check(dangers() == 0 && Text("PredictedDamageText") == null, "QueenHawk ordinary actual finish retained output.");
            stop();
        }
        Console.WriteLine("PASS HuntQueenHawk actual DLL five casts, persistent circle/donut and opposite status loss, three-second left march, final landing only warning, pure damage, actual finish, reverse Seek and cleanup");
    }
}

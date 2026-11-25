using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntCatsEye(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        float? Facing()
        {
            var script = Script();
            var encounter = script.GetType().BaseType!.GetField("encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
            return (float?)encounter.GetType().GetMethod("Facing", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(encounter, [Value(script, "CurrentPlayer"), (object)runtime.CurrentTime]);
        }
        object[] Eyes() => previews().Where(p => ((string)Value(p, "Id")).EndsWith("/eye")).ToArray();
        object[] Outlines() => previews().Where(p => ((string)Value(p, "Id")).Contains("/stack/preview")).ToArray();
        const uint territory = 1187;
        var party = Enumerable.Range(0, 10).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
        foreach (var action in new uint[] { 38510, 38511, 38515, 39668, 39887 })
        {
            var isStack = action is 38515 or 39668;
            var inverted = action is 38511 or 39668;
            var center = new Vector3(0, 0, 50);
            object[] Allies(bool dead = false, Vector3? moved = null) => [
                Actor(100, 0, Vector3.Zero),
                Actor(101, 0, moved ?? center, dead: dead),
                .. Enumerable.Range(2, 8).Select(i => Actor((ulong)(100 + i), 0, center))];
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4396, default, combat: true), .. Allies()], full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x4396, default, combat: true, cast: Cast(action, 7, center, target: 101))], party: party),
                CastEvent(.1, 1, action, 7, center, target: 101),
                Frame(territory, 2, Allies(moved: new(10.1234f, 0, 50.5678f)), party: party),
                Frame(territory, 3, Allies(dead: true, moved: new(10.1234f, 0, 50.5678f)), party: party),
                Frame(territory, 4, Allies(moved: new(10.1234f, 0, 50.5678f)), party: party),
                Frame(territory, 8, [Actor(1, 0x4396, default, combat: true)], party: party),
                Frame(territory, 9, [], removed: [1], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == (isStack ? 1 : 0) && Outlines().Length == (isStack ? 1 : 0)
                && Eyes().Length == (action == 39887 ? 0 : 1), "CatsEye cast, eye or stack output misclassified.");
            if (action == 39887)
                Check(((string?)Value(Script(), "PredictedDamageText"))?.Contains("Raidwide") == true, "CatsEye raidwide missing.");
            else
            {
                Check(Facing() == null && ((string?)Value(Script(), "PredictedDamageText")) == null,
                    "CatsEye issued early facing or fabricated stack Shared damage.");
                Check((((string?)Value(Script(), "MechanicHintsText"))?.Contains("Turn away from gaze!") == true) == !inverted,
                    "CatsEye inverted warning used normal gaze semantics.");
                controller.Seek(2.1);
                dynamic eye = Value(Eyes().Single(), "Geometry");
                Check((bool)eye.Contains(isStack ? new Vector3(10.1234f, 0, 50.5678f) : center, 0f),
                    "CatsEye cast lock or moving Bloodshot eye changed.");
                controller.Seek(3.1);
                Check(Eyes().Length == 1 && Outlines().Length == 0 && dangers() == 0,
                    "CatsEye dead stack target either lost its raw eye or retained navigation.");
                controller.Seek(4.1);
                Check(Outlines().Length == (isStack ? 1 : 0), "CatsEye revived target failed to restore.");
                controller.Seek(6.8);
                Check(Facing() != null, "CatsEye imminent gaze did not plan PR facing beyond range 40.");
                var hints = Value(Script(), "Hints");
                var facingBridge = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
                Check(!(bool)facingBridge.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facingBridge)!,
                    "CatsEye replay leaked live PR facing control.");
            }
            controller.Seek(8.1);
            Check(Eyes().Length == 0 && Outlines().Length == 0 && dangers() == 0 && Facing() == null,
                "CatsEye actual finish leaked output.");
            controller.Seek(9.1);
            Check(previews().Length == 0, "CatsEye removal retained boss marker.");
            controller.Seek(1d);
            Check(Eyes().Length == (action == 39887 ? 0 : 1), "CatsEye reverse Seek retained a future finish.");
            stop();
        }
        Console.WriteLine("PASS HuntCatsEye actual DLL five casts, unlimited R8 stack, independent pure raidwide, normal/inverted eyes beyond 40, moving/dead/revived targets, PR facing lead and replay isolation, reverse Seek and cleanup");
    }
}

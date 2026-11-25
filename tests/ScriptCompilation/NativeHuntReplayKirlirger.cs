using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntKirlirger(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object[] Outlines() => previews().Where(p => ((string)Value(p, "Id")).Contains("/stack/preview")).ToArray();
        object[] Navigation() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        string? Damage()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return (string?)Value(script, "PredictedDamageText");
        }
        const uint territory = 1187;
        foreach (var count in new[] { 7, 8, 9 })
        {
            var party = Enumerable.Range(0, count + 1).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) }).ToArray();
            object[] Allies(bool dead = false, Vector3? center = null) => [
                Actor(100, 0, new(20, 0, 0)),
                .. Enumerable.Range(1, count).Select(i => Actor((ulong)(100 + i), 0, i == 1 ? center ?? Vector3.Zero : Vector3.Zero,
                    dead: dead && i == 1))];
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x452A, default, combat: true), .. Allies()], full: true, party: party),
                StatusEvent(.05, 100, 1435, 0, true),
                Frame(territory, .1, [Actor(1, 0x452A, default, combat: true, cast: Cast(39480, 5, default, target: 101))], party: party),
                CastEvent(.1, 1, 39480, 5, default, target: 101),
                Frame(territory, 2, Allies(center: new(30, 0, 30)), party: party),
                Frame(territory, 3, Allies(dead: true), party: party),
                Frame(territory, 4, Allies(), party: party),
                Frame(territory, 6, [Actor(1, 0x452A, default, combat: true)], party: party),
                Frame(territory, 7, [Actor(1, 0x452A, default, combat: false)], party: party),
                Frame(territory, 8, [], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == 1 && Outlines().Length == 1 && (bool)Value(Outlines()[0], "Safe")
                && (bool)Value(Outlines()[0], "OutlineOnly") && Damage() == null,
                "Kirlirger maximum eight, SID1435 or Shared prediction leaked from Nariphon.");
            dynamic navigation = Value(Navigation().Single(), "Geometry");
            Check(!(bool)navigation.Contains(Vector3.Zero, 0f) && (bool)navigation.Contains(new Vector3(20, 0, 0), 0f),
                "Kirlirger did not admit another participant after reaching eight or nine.");
            controller.Seek(2.1);
            dynamic outline = Value(Outlines().Single(), "Geometry");
            Check((bool)outline.Contains(new Vector3(30, 0, 30), 0f) && !(bool)outline.Contains(Vector3.Zero, 0f),
                "Kirlirger stack did not follow its target.");
            controller.Seek(3.1);
            Check(dangers() == 0 && Outlines().Length == 0, "Kirlirger dead target retained navigation.");
            controller.Seek(4.1);
            Check(dangers() == 1 && Outlines().Length == 1, "Kirlirger revived target failed to restore.");
            controller.Seek(6.1);
            Check(dangers() == 0 && Outlines().Length == 0, "Kirlirger observed finish retained stack.");
            controller.Seek(7.1);
            Check(previews().Length == 0, "Kirlirger disengagement retained marker.");
            controller.Seek(1d);
            Check(dangers() == 1 && Outlines().Length == 1, "Kirlirger reverse Seek failed to reconstruct stack.");
            stop();
        }

        foreach (var action in new uint[] { 39466, 39477, 39473, 39467, 39538, 39877, 39464, 39459, 39484, 39463, 39465, 39875, 39524, 39481 })
        {
            object[] party = [new { Slot = 0, EntityId = 100ul }];
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x452A, default, combat: true), Actor(100, 0, default)], full: true, party: party),
                Frame(territory, .1, [Actor(1, 0x452A, default, combat: true, cast: Cast(action, 5, default))], party: party),
                Frame(territory, 6, [Actor(1, 0x452A, default, combat: true)], party: party),
                Frame(territory, 7, [], full: true),
            ]);
            controller.Seek(1d);
            Check(dangers() == (action == 39481 ? 0 : 1), "Kirlirger source AOE or pure raidwide was misclassified.");
            if (action == 39481) Check(Damage()?.Contains("Raidwide") == true, "Kirlirger Raidwide prediction missing.");
            else
            {
                var preview = previews().Single(p => ((string)Value(p, "Id")).EndsWith("hunt/1"));
                dynamic geometry = Value(preview, "Geometry");
                if (action is 39466 or 39477 or 39473 or 39467 or 39538 or 39877)
                    Check((bool)geometry.Contains(new Vector3(20, 0, -19), 0f)
                        && !(bool)geometry.Contains(new Vector3(19, 0, -20), 0f), "Kirlirger 270-degree Flourish rear sector differs.");
            }
            controller.Seek(6.1);
            Check(dangers() == 0 && Damage() == null, "Kirlirger ordinary observed finish leaked output.");
            stop();
        }
        Console.WriteLine("PASS HuntKirlirger actual DLL: all 15 source casts, 270-degree grouped cones, pure raidwide, unlimited seven/eight/nine stack, no forbidden or Shared, dynamic dead/revived target, real finish, reverse Seek and cleanup");
    }
}

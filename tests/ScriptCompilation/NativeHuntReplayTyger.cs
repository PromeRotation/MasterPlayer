using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntTyger(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        dynamic manager = managerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object[] Tails() => previews().Where(p => ((string)Value(p, "Id")).EndsWith("/scorpions-sting")).ToArray();
        object[] Navigation() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>().ToArray();
        const uint territory = 813;
        var origin = new Vector3(10, 0, 20);
        var moved = new Vector3(40, 0, 50);
        foreach (var action in new uint[] { 16962, 16963 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x288E, origin, combat: true), Actor(100, 0, origin)], full: true),
                Frame(territory, .1, [Actor(1, 0x288E, origin, combat: true, cast: Cast(action, 4, origin))]),
                CastEvent(.1, 1, action, 4, origin), CastEvent(.1, 1, action, 4, origin),
                Frame(territory, 5, [Actor(1, 0x288E, origin, combat: true)]),
                Frame(territory, 10, [Actor(1, 0x288E, moved, combat: true, rotation: MathF.PI / 2), Actor(100, 0, moved - Vector3.UnitX * 10)]),
                EffectEvent(11, 1, 16961, 1), EffectEvent(11, 1, 16961, 1),
                Frame(territory, 12, [Actor(1, 0x288E, moved, combat: true)]),
                Frame(territory, 13, [Actor(1, 0x288E, moved, combat: true, dead: true)]),
                Frame(territory, 14, []),
            ]);
            controller.Seek(1d);
            Check(Tails().Length == 1 && dangers() == 2, "Tyger Voice did not create one ordinary AOE and one tail.");
            dynamic outline = Value(Tails().Single(), "Geometry");
            Check((bool)outline.Contains(origin - Vector3.UnitZ * 17.9f, 0f)
                && !(bool)outline.Contains(origin + Vector3.UnitZ * 10, 0f), "Tyger initial tail is not the exact rear cone.");
            controller.Seek(8d);
            Check(Tails().Length == 1 && dangers() == 1, "Voice finish or predicted deadline consumed Tyger tail.");
            dynamic navigation = Value(Navigation().Single(), "Geometry");
            Check((bool)navigation.Contains(origin - Vector3.UnitZ * 10, 0f)
                && !(bool)navigation.Contains(origin + Vector3.UnitZ * 10, 0f), "Unmoved Tyger distance field failed to remain rearward.");
            var window = ((IEnumerable)Value(Navigation().Single(), "DangerWindows")).Cast<object>().Single();
            Check(Math.Abs(((DateTime)Value(window, "DangerFrom") - Start.AddSeconds(6.7)).TotalSeconds) < .00001,
                "Tyger tail prediction did not preserve source 2.3d delay.");
            controller.Seek(10.1);
            outline = Value(Tails().Single(), "Geometry");
            navigation = Value(Navigation().Single(), "Geometry");
            Check((bool)outline.Contains(moved - Vector3.UnitX * 10, 0f) && !(bool)outline.Contains(moved + Vector3.UnitX * 10, 0f)
                && (bool)navigation.Contains(moved + Vector3.UnitX * 10, 0f) && !(bool)navigation.Contains(moved - Vector3.UnitX * 10, 0f),
                "Tyger moved rear drawing and forward AI distance were merged.");
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            Check(((string)Value(script, "MechanicHintsText")).Contains("GTFO from aoe!"), "Tyger text stopped using the rear drawn cone.");
            controller.Seek(11.1);
            Check(Tails().Length == 0 && dangers() == 0, "Tyger actual effect or duplicate handling leaked its tail.");
            controller.Seek(13.1);
            Check(previews().Length == 0, "Tyger death retained drawing.");
            controller.Seek(1d);
            Check(Tails().Length == 1 && dangers() == 2, "Tyger reverse Seek lost prediction.");
            stop();
        }
        foreach (var action in new uint[] { 16957, 16958, 16959, 16960 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x288E, origin, combat: true), Actor(100, 0, origin)], full: true),
                Frame(territory, .1, [Actor(1, 0x288E, origin, combat: true, cast: Cast(action, 4, origin))]),
                Frame(territory, 5, [Actor(1, 0x288E, origin, combat: true)]),
                Frame(territory, 6, []),
            ]);
            controller.Seek(1d);
            Check(dangers() == 1 && Tails().Length == 0, "Tyger ordinary breath or embrace invented another tail.");
            controller.Seek(5.1);
            Check(dangers() == 0, "Tyger ordinary AOE failed observed finish.");
            stop();
        }
        Console.WriteLine("PASS HuntTyger actual DLL: all six source casts, double 2.3 tail delay, finish persistence, live quantized motion, rear outline/text and forward updated navigation, effect dedup, reverse Seek and cleanup");
    }
}

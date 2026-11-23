using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntMissiles(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 956;
        dynamic Geometry() => previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/"))
            .GetType().GetProperty("Geometry")!.GetValue(previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/")))!;
        foreach (var (warning, actual) in new[] { (27517u, 27299u), (27518u, 27300u) })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x3609, Vector3.Zero, combat: true), Actor(100, 0, new(10, 0, 20))], full: true),
                Frame(territory, .1, [Actor(1, 0x3609, Vector3.Zero, combat: true, cast: Cast(warning, 6.5, Vector3.Zero, target: 100))]),
                Frame(territory, 3, [Actor(100, 0, new(30, 0, 40))]),
                Frame(territory, 7, [Actor(1, 0x3609, Vector3.Zero, combat: true), Actor(100, 0, new(40, 0, 50))]),
                Frame(territory, 8, [Actor(100, 0, new(60, 0, 70))]),
                Frame(territory, 10, [Actor(1, 0x3609, Vector3.Zero, combat: true,
                    cast: Cast(actual, 3.5, new(80, 0, 90), startedAt: 10))]),
                Frame(territory, 14, [Actor(1, 0x3609, Vector3.Zero, combat: true)]),
                Frame(territory, 15, [Actor(1, 0x3609, Vector3.Zero)]),
                Frame(territory, 16, [])
            ]);
            controller.Seek(3.5d);
            Check(dangers() == 1 && previews().Length == 2 && (bool)Geometry().Contains(new Vector3(30, 0, 49), 0f) == (warning == 27518),
                "Minerva warning lost its selected shape at the live target.");
            CheckHuntPersistentWindows(runtimeObject);
            controller.Seek(8.5d);
            Check((bool)Geometry().Contains(new Vector3(40, 0, 59), 0f) == (warning == 27518)
                && !(bool)Geometry().Contains(new Vector3(60, 0, 70), 0f),
                "Minerva warning finish failed to lock the target's final position.");
            controller.Seek(10.5d);
            Check(dangers() == 1 && (bool)Geometry().Contains(new Vector3(80, 0, 99), 0f) == (warning == 27518),
                "Minerva actual missile failed to replace the fixed landing.");
            controller.Seek(14.5d);
            Check(dangers() == 0 && previews().Length == 1, "Minerva actual missile finish retained its shape.");
            controller.Seek(15.5d);
            Check(previews().Length == 0, "Minerva disengagement retained its enemy drawing.");
            controller.Seek(3.5d);
            Check(dangers() == 1, "Minerva reverse Seek lost target selection.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory), Frame(territory, 0, [Actor(100, 0, Vector3.Zero)], full: true),
            Frame(territory, .1, [
                Actor(1, 0x3609, Vector3.Zero, combat: true, cast: Cast(27299, 4, Vector3.Zero)),
                Actor(2, 0x3609, Vector3.Zero, combat: true, cast: Cast(27301, 4, Vector3.Zero)),
                Actor(3, 0x3609, Vector3.Zero, combat: true, cast: Cast(27302, 4, Vector3.Zero)),
                Actor(4, 0x3609, Vector3.Zero, combat: true, cast: Cast(27304, 4, Vector3.Zero, target: 100)),
                Actor(5, 0x3609, Vector3.Zero, combat: true, cast: Cast(27297, 4, Vector3.Zero)),
                Actor(6, 0x3609, Vector3.Zero, combat: true, cast: Cast(27298, 4, Vector3.Zero))]),
            Frame(territory, 5, Enumerable.Range(1, 6).Select(i => Actor((ulong)i, 0x3609, Vector3.Zero, combat: true))),
            Frame(territory, 6, [])
        ]);
        controller.Seek(1d);
        Check(dangers() == 1 && previews().Length == 7, "Minerva guessed a missing warning or lost pure hints/damage/Hyperflame.");
        controller.Seek(5.5d);
        Check(dangers() == 0 && previews().Length == 6, "Minerva ordinary finishes leaked output.");
        stop();
        Console.WriteLine("PASS HuntMissiles actual DLL Minerva both live warning targets, finish lock, actual landing replacement, missing-history isolation, pure damage, reverse Seek and cleanup");
    }

    private static void RunHuntCompressor(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 956;
        foreach (var (mode, step) in new[] { (27470u, -MathF.PI / 6), (27471u, MathF.PI / 6) })
        {
            var lines = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x35BE, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
                EffectEvent(.05, 1, mode, 1),
                Frame(territory, .1, [Actor(1, 0x35BE, Vector3.Zero, combat: true, cast: Cast(27472, 6, Vector3.Zero))]),
                Frame(territory, 7, [Actor(1, 0x35BE, new(100, 0, 100), combat: true)])
            };
            for (var i = 0; i < 10; ++i)
            {
                var t = 10 + 3 * i;
                lines.Add(EffectEvent(t, 1, i == 0 ? 27472u : i == 5 ? 27473u : 27474u, (uint)(10 + i)));
                lines.Add(EffectEvent(t, 1, i == 0 ? 27472u : i == 5 ? 27473u : 27474u, (uint)(10 + i)));
            }
            lines.Add(Frame(territory, 40, []));
            load("masterplayer.hunts.simple", lines);
            controller.Seek(7.5d);
            Check(dangers() == 2 && previews().Length == 3, "Compressor cast finish consumed an effect-driven cross.");
            for (var i = 0; i < 10; ++i)
            {
                controller.Seek(10.5d + 3 * i);
                var remaining = 9 - i;
                Check(dangers() == Math.Min(2, remaining) && previews().Length == Math.Min(2, remaining) + 1,
                    $"Compressor repeated effect did not advance exactly once at{i}.");
                if (remaining == 0) continue;
                var rotation = step * (i < 5 ? i + 1 : 9 - i);
                var current = previews().Single(p => p.GetType().GetProperty("Color")!.GetValue(p) is Vector4 color
                    && color == new Vector4(1, 1, 0, 1) || remaining == 1
                        && ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/"));
                dynamic geometry = current.GetType().GetProperty("Geometry")!.GetValue(current)!;
                var point = new Vector3(MathF.Sin(rotation) * 49, 0, MathF.Cos(rotation) * 49);
                Check((bool)geometry.Contains(point, 0f) && !(bool)geometry.Contains(new Vector3(100, 0, 100), 0f),
                    "Compressor fifth-hit reversal or locked cross origin changed.");
            }
            controller.Seek(7.5d);
            Check(dangers() == 2, "Compressor reverse Seek lost its rotation sequence.");
            stop();
        }
        Console.WriteLine("PASS HuntCompressor actual DLL Armstrong CW/CCW ten actual effects, fifth-hit reversal, two risky crosses, duplicate targets, fixed origin, reverse Seek and cleanup");
    }
}

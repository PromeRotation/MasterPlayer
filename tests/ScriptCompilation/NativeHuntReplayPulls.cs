using System.Numerics;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntPulls(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 813;
        bool Projected() => previews().Any(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("knockback/1/landing"));
        foreach (var (oid, action) in new[] { (0x2853u, 17366u), (0x2875u, 18100u), (0x2875u, 18101u) })
        {
            var lines = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Actor(100, 0, new(20, 0, 0)), Actor(1, oid, Vector3.Zero, combat: true)], full: true),
                Frame(territory, .1, [Actor(1, oid, Vector3.Zero, combat: true, cast: Cast(action, 5, Vector3.Zero))]),
                Frame(territory, 3, [Actor(1, oid, new(10, 0, 0), combat: true, cast: Cast(action, 5, Vector3.Zero))]),
                Frame(territory, 6, [Actor(1, oid, new(10, 0, 0), combat: true)])
            };
            if (oid == 0x2853)
            {
                lines.Add(EffectEvent(10, 1, 17369, 1));
                lines.Add(Frame(territory, 11, [Actor(1, oid, new(10, 0, 0))]));
                lines.Add(Frame(territory, 12, []));
            }
            else
            {
                for (var i = 0; i < 3; ++i)
                {
                    var at = 7d + i * 4;
                    lines.Add(Frame(territory, at, [Actor(1, oid, new(10, 0, 0), combat: true,
                        cast: Cast(i % 2 == 0 ? 18098u : 18099u, .5, new(10, 0, 0), startedAt: at))]));
                    lines.Add(Frame(territory, at + 1, [Actor(1, oid, new(10, 0, 0), combat: true)]));
                }
                lines.Add(Frame(territory, 18, [Actor(1, oid, new(10, 0, 0))]));
                lines.Add(Frame(territory, 19, []));
            }
            load("masterplayer.hunts.simple", lines);
            controller.Seek(1d);
            Check(Projected() && dangers() == (oid == 0x2853 ? 1 : 2),
                "Pull did not publish its landing marker and exact future hazards, or invented a R30 danger.");
            controller.Seek(3.5d);
            Check(Projected(), "A moved pull boss lost its projection.");
            var area = previews().First(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1/"));
            dynamic geometry = area.GetType().GetProperty("Geometry")!.GetValue(area)!;
            Check((bool)geometry.Contains(Vector3.Zero, 0f), "A fixed delayed/rotating area moved with the pulling boss.");
            controller.Seek(6.5d);
            Check(!Projected() && dangers() == (oid == 0x2853 ? 1 : 2),
                "Starter finish retained its pull or consumed a predicted hazard.");
            if (oid == 0x2853)
            {
                controller.Seek(9.5d);
                Check(dangers() == 1, "Flood disappeared at its estimated activation without the real effect.");
                controller.Seek(10.5d);
                Check(dangers() == 0, "Flood actual effect retained its navigation.");
                controller.Seek(11.5d);
            }
            else
            {
                controller.Seek(8.5d);
                Check(dangers() == 2, "Sugaar first short-cast finish removed both current/next previews.");
                controller.Seek(12.5d);
                Check(dangers() == 1, "Sugaar second actual finish failed to leave its last cone.");
                controller.Seek(16.5d);
                Check(dangers() == 0, "Sugaar third actual finish retained a cone.");
                controller.Seek(18.5d);
            }
            Check(previews().Length == 0, "Disengaged pull boss retained drawings.");
            controller.Seek(1d);
            Check(Projected() && dangers() == (oid == 0x2853 ? 1 : 2), "Reverse Seek failed to rebuild pull and sequence outputs.");
            stop();
        }
        Console.WriteLine("PASS HuntPulls actual DLL Rusalka effect-ended Flood, Sugaar both three-cone sequences, pull projection, locked origins, actual finish advancement, reverse Seek and cleanup");
    }
}

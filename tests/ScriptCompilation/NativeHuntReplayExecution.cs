using System.Numerics;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntExecution(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 1187;
        foreach (var reverse in new[] { false, true })
        foreach (var middleCross in new[] { false, true })
        {
            var records = new uint[] { 38454, middleCross ? 38456u : 38455u, middleCross ? 38455u : 38456u };
            var execute = reverse ? 38459u : 38457u;
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Actor(1, 0x4395, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
                Frame(territory, .1, [Actor(1, 0x4395, Vector3.Zero, combat: true, cast: Cast(records[0], 2, Vector3.Zero))]),
                Frame(territory, 3, [Actor(1, 0x4395, Vector3.Zero, combat: true, cast: Cast(records[1], 2, Vector3.Zero, startedAt: 3))]),
                Frame(territory, 6, [Actor(1, 0x4395, Vector3.Zero, combat: true, cast: Cast(records[2], 2, Vector3.Zero, startedAt: 6))]),
                Frame(territory, 10, [Actor(1, 0x4395, new(10, 0, 20), combat: true, cast: Cast(execute, 5, new(10, 0, 20), startedAt: 10))]),
                Frame(territory, 16, [Actor(1, 0x4395, new(80, 0, 90), combat: true, rotation: .8f)]),
                EffectEvent(17, 1, execute, 1), EffectEvent(17, 1, execute, 1),
                EffectEvent(19, 1, 38462, 2), EffectEvent(21, 1, 38463, 3),
                Frame(territory, 23, [Actor(1, 0x4395, new(80, 0, 90), combat: true,
                    cast: Cast(38466, 7, new(30, 0, 40), startedAt: 23))]),
                Frame(territory, 31, [Actor(1, 0x4395, new(80, 0, 90), combat: true)]),
                Frame(territory, 32, [], full: true)
            ]);
            controller.Seek(7d);
            Check(dangers() == 0 && previews().Length == 1, "Execution telegraphs became damage zones before code execution.");
            controller.Seek(11d);
            Check(dangers() == (middleCross ? 2 : 1) && previews().Length == 3,
                "Sally lost global two-item previews or the future cross risk.");
            var areas = previews().Where(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("/execution/")).ToArray();
            if (reverse && !middleCross)
            {
                var crossPreview = areas.Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).EndsWith("/0"));
                dynamic cross = crossPreview.GetType().GetProperty("Geometry")!.GetValue(crossPreview)!;
                Check((bool)cross.Contains(new Vector3(30, 0, 40), 0f)
                    && !(bool)cross.Contains(new Vector3(30, 0, 20), 0f), "Sally reverse cross lost the execution origin or 45-degree rotation.");
            }
            controller.Seek(16.5d);
            Check(dangers() == (middleCross ? 2 : 1) && previews().Length == 3,
                "Execution finish consumed the first stage or boss movement changed its queue.");
            controller.Seek(17.5d);
            Check(previews().Length == 3, "Duplicate effect popped two queue heads.");
            controller.Seek(21.5d);
            Check(dangers() == 0 && previews().Length == 1, "Three real effects failed to resolve the recorded history.");
            controller.Seek(23.5d);
            var targeted = previews().Single(p => ((string)p.GetType().GetProperty("Id")!.GetValue(p)!).Contains("hunt/1"));
            dynamic circle = targeted.GetType().GetProperty("Geometry")!.GetValue(targeted)!;
            Check(dangers() == 1 && (bool)circle.Contains(new Vector3(47, 0, 40), 0f)
                && !(bool)circle.Contains(new Vector3(49, 0, 40), 0f), "TargetedAdvance did not preserve its exact R18.");
            controller.Seek(31.5d);
            Check(dangers() == 0, "TargetedAdvance finish retained output.");
            controller.Seek(11d);
            Check(previews().Length == 3, "Sally reverse Seek failed to reconstruct recorded shapes.");
            stop();
        }
        Console.WriteLine("PASS HuntExecution actual DLL Sally forward/reverse recorded shapes, risky future cross, exact rotated geometry, locked execution pose, effect-only queue, duplicate effects, targeted advance, reverse Seek and cleanup");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntHeshuala(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        string? Text(string name)
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return (string?)Value(script, name);
        }
        object[] Spins() => previews().Where(p => ((string)Value(p, "Id")).Contains("/heshuala/")
            && !((string)Value(p, "Id")).EndsWith("/cross")).ToArray();
        object? Cross() => previews().FirstOrDefault(p => ((string)Value(p, "Id")).EndsWith("/heshuala/cross"));
        const uint territory = 1187;
        var origin = new Vector3(10, 0, 20);
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        object Boss(object? cast = null) => Actor(1, 0x416E, origin, combat: true, cast: cast, stackedStatus: 3977);
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Boss(), Actor(100, 0, new(45, 0, 20))], full: true, party: party),
            Frame(territory, .1, [Boss(Cast(39095, 2, origin))], party: party),
            Frame(territory, 3, [Boss(Cast(39100, 5, origin, startedAt: 3))], party: party),
            StatusEvent(4, 100, 3978, 0, true),
            EffectEvent(5, 1, 39100, 10),
            Frame(territory, 8, [Boss()], party: party),
            Frame(territory, 9, [Boss(Cast(39101, .7, origin, MathF.PI / 2, startedAt: 9))], party: party),
            Frame(territory, 10, [Boss()], party: party),
            Frame(territory, 11, [Boss(Cast(39101, .7, origin, MathF.PI, startedAt: 11))], party: party),
            Frame(territory, 12, [Boss()], party: party),
            Frame(territory, 13, [Boss(Cast(39102, 1.7, origin, startedAt: 13))], party: party),
            Frame(territory, 15, [Boss()], party: party),
            Frame(territory, 16, [], removed: [1], party: party),
        ]);
        controller.Seek(3.5);
        Check(Spins().Length == 2 && Cross() == null && dangers() == 2, "Heshuala first two risky spins or hidden cross differ.");
        controller.Seek(5.5);
        Check(Spins().Length == 2 && Cross() == null && dangers() == 2, "Heshuala effect advanced its Finish-only rotation.");
        controller.Seek(8.5);
        Check(Spins().Length == 2 && Cross() != null && dangers() == 3, "Heshuala first real Finish failed to reveal its frozen cross.");
        dynamic geometry = Value(Cross()!, "Geometry");
        Check((bool)geometry.Contains(origin + new Vector3(49, 0, 4), 0f)
            && !(bool)geometry.Contains(origin + new Vector3(20, 0, 20), 0f),
            "Heshuala later status changed its frozen cardinal cross.");
        dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
        var deadlines = ((IEnumerable)snapshot.DangerAreas).Cast<object>()
            .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
            .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        Check(deadlines.SequenceEqual(new[] { Start.AddSeconds(8).AddSeconds(2.7d),
            Start.AddSeconds(8).AddSeconds(2.7d).AddSeconds(2.7d), Start.AddSeconds(3).AddSeconds(17.5d) }.Order()),
            "Heshuala real Finish clocks or first Update cross clock differ.");
        controller.Seek(10.5);
        Check(Spins().Length == 1 && Cross() != null && dangers() == 2, "Heshuala final spin failed to reduce preview count.");
        controller.Seek(12.5);
        Check(Spins().Length == 0 && Cross() != null && dangers() == 1, "Heshuala final Finish removed persistent cross early.");
        controller.Seek(15.5);
        Check(Spins().Length == 0 && Cross() == null && dangers() == 0, "Heshuala cross Finish retained output.");
        controller.Seek(8.5);
        Check(Spins().Length == 2 && Cross() != null && dangers() == 3, "Heshuala reverse Seek retained future consumption.");
        controller.Seek(16.1);
        Check(previews().Length == 0 && dangers() == 0, "Heshuala owner destruction retained output.");
        stop();

        foreach (var action in new uint[] { 39104, 39105 })
        {
            load("masterplayer.hunts.simple",
            [
                Header(territory),
                Frame(territory, 0, [Boss(), Actor(100, 0, origin)], full: true, party: party),
                Frame(territory, .1, [Boss(Cast(action, 3, origin, target: 100))], party: party),
                Frame(territory, 5, [Boss()], party: party), Frame(territory, 6, [], party: party),
            ]);
            controller.Seek(1d);
            Check(dangers() == (action == 39104 ? 1 : 0), "Heshuala ordinary geometry differs.");
            if (action == 39105) Check(Text("PredictedDamageText")?.Contains("Raidwide") == true,
                "Heshuala Raidwide prediction is missing.");
            controller.Seek(5.5);
            Check(dangers() == 0 && Text("PredictedDamageText") == null, "Heshuala ordinary Finish retained output.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Boss(), Actor(999, 0x233C, origin), Actor(100, 0, origin)], full: true, party: party),
            CastEvent(.1, 999, 39095, 2, origin),
            CastEvent(1, 999, 39100, 5, origin),
            Frame(territory, 2, [], removed: [999], party: party),
            Frame(territory, 3, [], party: party),
        ]);
        controller.Seek(1.5);
        Check(Spins().Length == 2 && dangers() == 2, "Heshuala packet-only helper did not publish early prediction.");
        controller.Seek(2.5);
        Check(Spins().Length == 0 && Cross() == null && dangers() == 0,
            "Heshuala packet-only helper removal retained an unconfirmed prediction.");
        stop();
        Console.WriteLine("PASS HuntHeshuala actual DLL two risky rotations, actual Finish clocks, hidden then frozen cardinal cross, unrelated status isolation, pure raidwide, unobserved helper retraction, reverse Seek and owner cleanup");
    }
}

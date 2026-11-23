using System.Collections;
using System.Numerics;
using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntCombos(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        const uint territory = 813;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        object? Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value);
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Healer", EntityId = 101UL } };
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, -Vector3.UnitZ * 20)], full: true),
            Frame(territory, .1, [Actor(1, 0x2854, Vector3.Zero, combat: true, cast: Cast(17956, 3, Vector3.Zero)),
                Actor(2, 0x2854, Vector3.Zero, combat: true, cast: Cast(17957, 3, Vector3.Zero)),
                Actor(3, 0x2854, Vector3.Zero, combat: true, cast: Cast(17423, 5, Vector3.Zero)),
                Actor(4, 0x2854, Vector3.Zero, combat: true, cast: Cast(17424, 5, Vector3.Zero))]),
            CastEvent(.1, 1, 17956, 3, Vector3.Zero), CastEvent(.1, 2, 17957, 3, Vector3.Zero),
            CastEvent(.1, 3, 17423, 5, Vector3.Zero, .1f), CastEvent(.1, 4, 17424, 5, Vector3.Zero, -.1f),
            EffectEvent(1, 3, 17423, 1),
            Frame(territory, 6, Enumerable.Range(1, 4).Select(i => Actor((ulong)i, 0x2854, Vector3.Zero, combat: true))),
            EffectEvent(7, 3, 17422, 2), EffectEvent(7, 3, 17422, 2), EffectEvent(7, 4, 17421, 3),
            Frame(territory, 8, []),
        ]);
        controller.Seek(1.5d);
        Check(dangers() == 4 && previews().Length == 10 && Value(Script(), "MechanicHintsText") == null,
            "Baal second preview became a danger/warning or first action effect consumed a stage.");
        controller.Seek(6.5d);
        Check(dangers() == 2 && previews().Length == 6 && ((string)Value(Script(), "MechanicHintsText")!).Contains("GTFO"),
            "Baal cast finish did not promote both second stages.");
        controller.Seek(7.5d);
        Check(dangers() == 0 && previews().Length == 4, "Baal second effects leaked or cross-deleted geometry.");
        controller.Seek(1.5d);
        Check(dangers() == 4 && previews().Length == 10, "Baal reverse Seek lost preview/current distinction.");
        stop();

        var rules = new (uint Action, double Seconds)[] { (17496, 3.5), (17499, 4), (17495, 3.5), (17497, 4), (17498, 4) };
        var lines = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, new(0, 0, 30))], full: true, party: party),
            Frame(territory, .1, rules.Select((r, i) => Actor((ulong)i + 1, 0x28B5, Vector3.Zero, combat: true,
                cast: Cast(r.Action, r.Seconds, Vector3.Zero))), party: party),
        };
        for (var i = 0; i < rules.Length; ++i) lines.Add(CastEvent(.1, (ulong)i + 1, rules[i].Action, rules[i].Seconds, Vector3.Zero));
        lines.Add(Frame(territory, .2, [Actor(3, 0x28B5, Vector3.Zero, combat: true, cast: new
        {
            ActionId = 17495, TargetId = 0, Location = Pose(Vector3.Zero), Rotation = 0,
            StartedAt = Start.AddSeconds(.1), FinishAt = Start.AddSeconds(.1 + 3.5 + .3).AddTicks(1), TotalTime = 3.5f
        })], party: party));
        lines.Add(Frame(territory, 1, [Actor(4, 0x28B5, new(20, 0, 30), combat: true,
            cast: Cast(17497, 4, Vector3.Zero), rotation: MathF.PI / 2)], party: party));
        lines.Add(Frame(territory, 6, Enumerable.Range(1, 5).Select(i => Actor((ulong)i, 0x28B5, Vector3.Zero, combat: true)), party: party));
        lines.Add(EffectEvent(7, 3, 17495, 1)); lines.Add(EffectEvent(7, 4, 17497, 2)); lines.Add(EffectEvent(7, 5, 17498, 3));
        lines.Add(EffectEvent(7, 4, 17497, 2));
        lines.Add(EffectEvent(8, 3, 18042, 4)); lines.Add(EffectEvent(8, 4, 18042, 5)); lines.Add(EffectEvent(8, 5, 18042, 6));
        lines.Add(Frame(territory, 9, [], party: party));
        load("masterplayer.hunts.simple", lines);
        controller.Seek(.5d);
        Check(dangers() == 7 && previews().Length == 12 && ((string)Value(Script(), "MechanicHintsText")!).Contains("GTFO"),
            $"Huracan future rectangles did not participate in navigation and current hints: dangers={dangers()}, previews={previews().Length}, hints={Value(Script(), "MechanicHintsText")}.");
        var world = Value(Script(), "ReplayWorld")!;
        var sampled = ((IDictionary)Value(world, "Actors")!)[3UL]!;
        Check(Value(Value(sampled, "Cast")!, "TotalTime") is float total && total == 3.5f,
            "Replay JSON discarded the original float cast duration.");
        controller.Seek(1.5d);
        var winter = previews().Where(p => ((string)Value(p, "Id")!).Contains("hunt/4/combo/")).ToArray();
        Check(winter.Length == 2, "WinterRain lost a stage.");
        Check(winter.Select(p => Value(p, "Geometry")).Any(g => (bool)g!.GetType().GetMethod("Contains")!
            .Invoke(g, [new Vector3(30, 0, 30), 0f])!), "WinterRain second rectangle did not track actor position/rotation.");
        controller.Seek(6.5d);
        Check(dangers() == 6 && previews().Length == 11, "Huracan cast finish incorrectly consumed effect-driven combos.");
        controller.Seek(7.5d);
        Check(dangers() == 3 && previews().Length == 8, "Huracan multi-target effects consumed too many or too few stages.");
        controller.Seek(8.5d);
        Check(dangers() == 0 && previews().Length == 5, "Windburst effects did not finish all three independent combos.");
        controller.Seek(.5d);
        Check(dangers() == 7 && previews().Length == 12, "Huracan reverse Seek did not reconstruct both risky stages.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x283A, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero), Actor(101, 0, Vector3.Zero)],
                full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x283A, Vector3.Zero, combat: true, cast: Cast(17859, 4, Vector3.Zero)),
                Actor(2, 0x283A, Vector3.Zero, combat: true, cast: Cast(17861, 3, new(20, 0, 10))),
                Actor(3, 0x283A, Vector3.Zero, combat: true, cast: Cast(17860, 4, Vector3.Zero, target: 100)),
                Actor(4, 0x283A, Vector3.Zero, combat: true, cast: Cast(17862, 3, Vector3.UnitZ * 20))], party: party),
            CastEvent(.1, 1, 17859, 4, Vector3.Zero, .1f),
            Frame(territory, 5, Enumerable.Range(1, 4).Select(i => Actor((ulong)i, 0x283A, Vector3.Zero, combat: true)), party: party),
            Frame(territory, 10, [Actor(1, 0x283A, Vector3.Zero, combat: true, cast: Cast(17859, 4, Vector3.Zero, startedAt: 10))], party: party),
            CastEvent(10, 1, 17859, 4, Vector3.Zero),
            Frame(territory, 14.5, [Actor(1, 0x283A, Vector3.Zero, combat: true)], party: party),
            EffectEvent(15, 1, 17863, 1), EffectEvent(15, 1, 17863, 1),
            EffectEvent(16, 1, 17863, 2), EffectEvent(17, 1, 17863, 3), EffectEvent(18, 1, 17863, 4),
            Frame(territory, 19, [], party: party)
        ]);
        controller.Seek(1d);
        Check(dangers() == 2 && ((string)Value(Script(), "MechanicHintsText")!).Contains("Raidwide")
            && !((string)Value(Script(), "MechanicHintsText")!).Contains("x5"), "First ChestThump became R30/x5 or lost the independent circle/charge.");
        controller.Seek(5.5d);
        Check(Value(Script(), "MechanicHintsText") == null && ((string)Value(Script(), "PredictedDamageText")!).Contains("2人/4次"),
            "First ChestThump did not clear only its hint while retaining source diagnostic prediction.");
        controller.Seek(17.5d);
        Check((string)Value(Script(), "MechanicHintsText")! == "Raidwide x5", "Grassman x5 ended before four unique extra effects.");
        controller.Seek(18.5d);
        Check(Value(Script(), "MechanicHintsText") == null && dangers() == 0, "Fourth extra hit failed to end the x5 hint.");
        controller.Seek(17.5d);
        Check((string)Value(Script(), "MechanicHintsText")! == "Raidwide x5", "Grassman reverse Seek lost cast history.");
        stop();

        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x2874, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero)], full: true),
            Frame(territory, .1, [Actor(1, 0x2874, Vector3.Zero, combat: true, cast: Cast(18020, 3, Vector3.Zero)),
                Actor(2, 0x2874, Vector3.Zero, combat: true, cast: Cast(18019, 3.5, Vector3.Zero)),
                Actor(3, 0x2874, Vector3.Zero, combat: true, cast: Cast(18022, 3.5, Vector3.Zero)),
                Actor(4, 0x2874, Vector3.Zero, combat: true, cast: Cast(18024, 3, Vector3.Zero))]),
            CastEvent(.1, 1, 18020, 3, Vector3.Zero), CastEvent(.1, 2, 18019, 3.5, Vector3.Zero),
            CastEvent(.1, 3, 18022, 3.5, Vector3.Zero), CastEvent(.1, 4, 18024, 3, Vector3.Zero),
            Frame(territory, 5, Enumerable.Range(1, 4).Select(i => Actor((ulong)i, 0x2874, Vector3.Zero, combat: true))),
            Frame(territory, 6, Enumerable.Range(1, 4).Select(i => Actor((ulong)i, 0x2874, Vector3.Zero))),
            Frame(territory, 7, [])
        ]);
        controller.Seek(1d);
        Check(dangers() == 3 && previews().Length == 7, "Maliktender lost one of its three exact source geometries.");
        controller.Seek(5.5d);
        Check(dangers() == 0 && ((string)Value(Script(), "MechanicHintsText")!).Contains("instantly kill"), "Haste latch ended with cast/status absence.");
        controller.Seek(6.5d);
        Check(Value(Script(), "MechanicHintsText") == null && previews().Length == 0, "Disengagement retained Haste or enemy markers.");
        stop();
        Console.WriteLine("PASS HuntCombos actual DLL Baal risk/preview promotion, Huracan concurrent stages/WinterRain tracking, Grassman first/x5 actual effects and persistent diagnostics, Maliktender Haste latch, reverse Seek and cleanup");
    }

    private static void RunHuntFanAil(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        const uint territory = 956;
        var origin = new Vector3(10, 0, 20);
        var rules = new uint[] { 27373, 27376, 27378, 27379, 27380 };
        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL } };
        var lines = new List<string>
        {
            Header(territory), Frame(territory, 0, [Actor(100, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, rules.Select((action, i) => Actor((ulong)i + 1, 0x35C1, origin, combat: true,
                cast: Cast(action, 5, origin, target: 100))), party: party),
            Frame(territory, 1, [Actor(1, 0x35C1, new(100, 0, 100), combat: true,
                cast: Cast(27373, 5, origin), rotation: MathF.PI / 2)], party: party),
            EffectEvent(2, 1, 27374, 1), EffectEvent(2.1, 1, 27375, 2),
            Frame(territory, 6, rules.Select((_, i) => Actor((ulong)i + 1, 0x35C1, origin, combat: true)), party: party),
            Frame(territory, 7, [], party: party)
        };
        load("masterplayer.hunts.simple", lines);
        controller.Seek(.5d);
        Check(dangers() == 3 && previews().Length == 8, "FanAil five source rules produced incorrect damage/preview count.");
        controller.Seek(2.5d);
        var dive = previews().Single(p => p.GetType().GetProperty("Id")!.GetValue(p)!.ToString()!.EndsWith("hunt/1", StringComparison.Ordinal));
        dynamic geometry = dive.GetType().GetProperty("Geometry")!.GetValue(dive)!;
        Check((bool)geometry.Contains(origin + Vector3.UnitX * 29.99f, 0f)
            && !(bool)geometry.Contains(origin + Vector3.UnitZ * 10, 0f)
            && dangers() == 3 && previews().Length == 8, "FanAil Divebomb did not rotate or Disappear/Reappear changed source state.");
        controller.Seek(6.5d);
        Check(dangers() == 0 && previews().Length == 5, "FanAil observed finish leaked a Divebomb stage.");
        controller.Seek(.5d);
        Check(dangers() == 3 && previews().Length == 8, "FanAil reverse Seek retained a future rotation.");
        stop();
        Console.WriteLine("PASS HuntFanAil actual DLL five source rules, live caster rotation with locked cast origin, exact rectangle, no extra Disappear/Reappear stage, observed finish, reverse Seek and cleanup");
    }
}

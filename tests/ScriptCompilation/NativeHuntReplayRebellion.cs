using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntRebellion(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        string? Text(string name) => (string?)Value(Script(), name);
        object[] Group(string name) => previews().Where(p => ((string)Value(p, "Id")).Contains("/rebellion/" + name)).ToArray();
        DateTime[] Deadlines() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>()
            .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
            .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"),
                "Rebellion replay retained a PR facing/action owner request or movement hold.");
        }
        string Icon(double at, ulong actor, uint icon) => JsonSerializer.Serialize(new
        {
            t = at, kind = "TargetIcon", type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = 100ul, MarkerId = icon, Timestamp = Start.AddSeconds(at) }
        });
        const uint territory = 817;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        var lines = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x28B6, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, new(-50, 0, 0))], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Cast(17628, 5, default))], party: party),
            Frame(territory, 2, [Actor(999, 0x233C, default)], party: party),
            Frame(territory, 3, [Actor(999, 0x233C, default, cast: Cast(17629, .5, default,
                MathF.PI / 4, startedAt: 3))], party: party),
            Frame(territory, 4, [Actor(999, 0x233C, default)], party: party),
            EffectEvent(5, 999, 17629, 100)
        };
        for (var i = 0; i < 6; ++i)
        {
            var at = 10 + 2 * i;
            lines.Add(Frame(territory, at, [Actor(999, 0x233C, default,
                cast: Cast(18080, .5, default, .4f, startedAt: at))], party: party));
            lines.Add(Frame(territory, at + 1, [Actor(999, 0x233C, default)], party: party));
        }
        lines.Add(Frame(territory, 23, [], removed: [1], party: party));
        load("masterplayer.hunts.simple", lines);
        controller.Seek(1d);
        Check(Group("candidate").Length == 3 && dangers() == 3
            && Text("PredictedDamageText")?.Contains("Raidwide") == true
            && Text("MechanicHintsText")?.Contains("Rotation direction undeterminable") == true,
            "Rebellion first Blizzard lost three risky candidates, raidwide prediction or cast hint.");
        controller.Seek(2.5);
        Check(Group("candidate").Length == 2 && dangers() == 2 && Text("PredictedDamageText") == null,
            "Blizzard actual Finish failed FIFO or retained prediction.");
        controller.Seek(5.5);
        Check(Group("candidate").Length == 0 && Group("blizzard").Length == 2 && dangers() == 2,
            "Blizzard rotation lost both risky projections or advanced on Effect.");
        var blizzardNext = Start.AddSeconds(4).AddSeconds(1.3d);
        Check(Deadlines().SequenceEqual(new[] { blizzardNext, blizzardNext.AddSeconds(1.3d) }),
            "Blizzard current/future clocks were not delivered to the production navigation snapshot.");
        controller.Seek(8d);
        Check(Deadlines().SequenceEqual(new[] { blizzardNext, blizzardNext.AddSeconds(1.3d) }),
            "A clock-only replay tick slid Blizzard navigation deadlines.");
        Isolated();
        controller.Seek(20.5);
        Check(Group("blizzard").Length == 1 && dangers() == 1, "Blizzard failed seven-stage actual Finish sequence.");
        controller.Seek(21.5);
        Check(Group("blizzard").Length == 0 && dangers() == 0, "Blizzard retained final stage.");
        controller.Seek(1d);
        Check(Group("candidate").Length == 3 && dangers() == 3, "Blizzard reverse Seek retained future sequence state.");
        stop();

        lines =
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x28B6, default, combat: true), Actor(999, 0x233C, new(50, 0, 50)),
                Actor(998, 0x233C, new(10, 0, 20)), Actor(100, 0, default, stackedStatus: 1958, statusParam: 0,
                    statusExpiresAt: Start.AddSeconds(100))], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, new(50, 0, 50), cast: Cast(18078, .5, default, .4f))], party: party),
            Frame(territory, 1, [Actor(999, 0x233C, new(60, 0, 60))], party: party),
            Icon(2, 998, 168),
            Frame(territory, 3, [Actor(999, 0x233C, default, cast: Cast(18126, 5, default)),
                Actor(998, 0x233C, new(80, 0, 90))], party: party),
            Frame(territory, 4, [Actor(999, 0x233C, default)], party: party),
            EffectEvent(10, 999, 18126, 1), EffectEvent(10.01, 999, 18126, 1),
            EffectEvent(12, 998, 18127, 2), EffectEvent(14, 999, 18128, 3),
            EffectEvent(16, 998, 18126, 4), EffectEvent(18, 999, 18127, 5), EffectEvent(20, 998, 18128, 6),
            Frame(territory, 21, [], removed: [1], party: party)
        ];
        load("masterplayer.hunts.simple", lines);
        controller.Seek(2.5);
        Check(Group("cyclone").Length == 2 && dangers() == 2
            && previews().Count(p => ((string)Value(p, "Id")).Contains("march/")) == 2
            && !(bool)Value(Script(), "PyreticActive"),
            "Rebellion icon routing, owner march or no-hold contract differs.");
        Check(Group("cyclone").All(p =>
        {
            dynamic geometry = Value(p, "Geometry");
            return Vector3.Distance((Vector3)geometry.Position, new(10, 0, 20)) < .04f;
        }), "Cyclone did not lock completing icon actor position.");
        var cycloneNext = Start.AddSeconds(.1 + .5 + .3).AddSeconds(5.2d);
        Check(Deadlines().SequenceEqual(new[] { cycloneNext, cycloneNext.AddSeconds(1.7d) }),
            "Cyclone F+5.2 and exact future interval were lost by the production navigation consumer.");
        Isolated();
        controller.Seek(4.5);
        Check(Group("cyclone").Length == 2 && dangers() == 2 && Group("cyclone").All(p =>
        {
            dynamic geometry = Value(p, "Geometry");
            return Vector3.Distance((Vector3)geometry.Position, new(10, 0, 20)) < .04f;
        }), "Cyclone Finish advanced its effect queue or actor motion changed its locked origin.");
        controller.Seek(18.5);
        Check(Group("cyclone").Length == 1 && dangers() == 1, "Cyclone duplicates consumed an extra stage.");
        controller.Seek(20.5);
        Check(Group("cyclone").Length == 0 && dangers() == 0, "Cyclone did not consume six real effects.");
        controller.Seek(21.5);
        Check(previews().Length == 0 && dangers() == 0, "Rebellion owner removal retained output.");
        stop();

        object Raw(double total, Vector3 location) => new
        {
            ActionId = 17629u, TargetId = 999ul, Location = Pose(location), Rotation = .3f,
            StartedAt = Start.AddSeconds(.1), FinishAt = Start.AddSeconds(.1 + total + .3), TotalTime = (float)total
        };
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x28B6, default, combat: true), Actor(2, 0x28B6, default, combat: true),
                Actor(999, 0x233C, default), Actor(100, 0, default)], full: true, party: party),
            Frame(territory, .1, [Actor(999, 0x233C, default, cast: Raw(5, default))], party: party),
            CastEvent(.2, 999, 17629, 5, new(10, 0, 20), .4f),
            Frame(territory, 1, [Actor(999, 0x233C, default, cast: Raw(MathF.BitIncrement(5f), default))], party: party),
            Frame(territory, 2, [], removed: [1], party: party),
            Frame(territory, 3, [], removed: [999], party: party),
            Frame(territory, 4, [], removed: [2], party: party)
        ]);
        controller.Seek(.5);
        Check(Group("blizzard").Length == 4 && dangers() == 4 && Group("blizzard").All(p =>
        {
            dynamic geometry = Value(p, "Geometry");
            return (Vector3)geometry.Position == new Vector3(10, 0, 20);
        }), "Packet correction duplicated a raw Start or failed per-owner pose correction.");
        controller.Seek(1.5);
        var afterRestart = Start.AddSeconds(1).AddSeconds(1.3d);
        Check(Deadlines().SequenceEqual(new[] { afterRestart, afterRestart,
            afterRestart.AddSeconds(1.3d), afterRestart.AddSeconds(1.3d) }.Order()),
            "Raw TotalTime change did not finish the old generation before the replacement Start.");
        controller.Seek(2.5);
        Check(Group("blizzard").Length == 2 && dangers() == 2
            && Deadlines().SequenceEqual(new[] { afterRestart, afterRestart.AddSeconds(1.3d) })
            && Group("blizzard").All(p =>
            {
                dynamic geometry = Value(p, "Geometry");
                return (Vector3)geometry.Position == new Vector3(10, 0, 20);
            }), "Removing a live rotation owner disturbed the surviving owner's queue.");
        controller.Seek(3.5);
        var current = Group("blizzard").Single(p => ((string)Value(p, "Id")).EndsWith("/current"));
        dynamic currentGeometry = Value(current, "Geometry");
        Check(MathF.Abs((float)currentGeometry.Rotation - (.4f + MathF.PI / 2)) < .00001f,
            "Replacement generation destruction did not independently Finish its surviving owner's sequence.");
        Isolated();
        controller.Seek(4.5);
        Check(previews().Length == 0 && dangers() == 0, "Removing the last owner with a live sequence retained output.");
        stop();
        Console.WriteLine("PASS Rebellion actual DLL unknown-direction Blizzard and seven Finish stages, icon/source Cyclone and six effects, both risky previews/navigation, owner forced-march projection, prediction lifetime, reverse Seek and cleanup");
    }
}

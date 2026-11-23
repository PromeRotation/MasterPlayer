using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntSequences(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const uint territory = 612;
        object Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;
        void Isolated()
        {
            var hints = Value(Script(), "Hints");
            var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
            Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!
                && !(bool)Value(Value(runtimeObject, "Setting"), "RuntimeMovementHeld"), "Offline Pyretic published a PR action or MP movement hold.");
        }
        var location = new Vector3(10, 0, 20);
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x1AC0, location, combat: true), Actor(2, 0x1AC0, location, combat: true),
                Actor(100, 0, location)], full: true),
            Frame(territory, .1, [Actor(1, 0x1AC0, location, combat: true, cast: Cast(8191, 3, location)),
                Actor(2, 0x1AC0, location, combat: true, cast: Cast(8191, 3, location))]),
            CastEvent(.1, 1, 8191, 3, location), CastEvent(.1, 2, 8191, 3, location),
            Frame(territory, 1, [Actor(1, 0x1AC0, new(500, 0, 500), combat: true, rotation: 2)]),
            Frame(territory, 3.1, [Actor(2, 0x1AC0, location, combat: true)]),
            EffectEvent(4, 1, 8192, 1), EffectEvent(4, 1, 8192, 1),
            Frame(territory, 5, [], removed: [2]),
            Frame(territory, 6, []),
        ]);
        controller.Seek(.5d);
        Check(dangers() == 4 && previews().Length == 6, "Angada did not draw simultaneous independent Butcher and Rip.");
        controller.Seek(1.5d);
        Check(dangers() == 3 && previews().Length == 5, "Butcher observed finish removed Rip.");
        var rip = previews().Single(p => ((string)Value(p, "Id")).EndsWith("hunt/1/followup/8192", StringComparison.Ordinal));
        dynamic geometry = Value(rip, "Geometry");
        Check((bool)geometry.Contains(location + Vector3.UnitZ * 11.39f, 0f)
            && !(bool)geometry.Contains(location + Vector3.UnitZ * 11.41f, 0f), "Rip moved with the caster or changed radius.");
        controller.Seek(3.5d);
        Check(dangers() == 2 && previews().Length == 4, "Rip expired at its predicted activation.");
        controller.Seek(4.5d);
        Check(dangers() == 1 && previews().Length == 3, "Rip effects deleted another instance or left the resolved instance.");
        controller.Seek(5.5d);
        Check(dangers() == 0 && previews().Length == 1, "Angada removal retained its Rip.");
        controller.Seek(.5d);
        Check(dangers() == 4 && previews().Length == 6, "Angada reverse Seek failed to reconstruct both shapes.");
        stop();

        var party = new object[] { new { Slot = 0, Role = "Tank", EntityId = 100UL }, new { Slot = 1, Role = "Healer", EntityId = 101UL } };
        string Gain(double t, ulong target, float seconds, ulong source = 0) => JsonSerializer.Serialize(new
        {
            t, kind = "StatusAdd", type = "PromeRotation.LogSystem.LogSystemStatusAddEvent",
            data = new { SourceId = source, TargetId = target, StatusId = 960, Duration = seconds, Timestamp = Start.AddSeconds(t) }
        });
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x1AB2, Vector3.Zero, combat: true), Actor(100, 0, Vector3.Zero),
                Actor(101, 0, Vector3.Zero)], full: true, party: party),
            Frame(territory, .1, [Actor(1, 0x1AB2, Vector3.Zero, combat: true, cast: Cast(7913, 1.5, Vector3.Zero))], party: party),
            CastEvent(.1, 1, 7913, 1.5, Vector3.Zero),
            Gain(1, 101, 10),
            Gain(2, 100, 4),
            Frame(territory, 2.1, [Actor(1, 0x1AB2, Vector3.Zero, combat: true)], party: party),
            Frame(territory, 3, [Actor(100, 0, Vector3.Zero, stackedStatus: 960, statusExpiresAt: Start.AddSeconds(6))], party: party),
            StatusEvent(4, 100, 960, 0, false),
            Frame(territory, 4.1, [Actor(100, 0, Vector3.Zero)], party: party),
            Frame(territory, 5, [Actor(100, 0, Vector3.Zero, stackedStatus: 960, statusExpiresAt: Start.AddSeconds(6))], party: party),
            Frame(territory, 7, [], party: party),
            Frame(territory, 8, [Actor(1, 0x1AB2, Vector3.Zero)], party: party),
            Frame(territory, 9, [], party: party),
        ]);
        controller.Seek(1.5d);
        Check(!(bool)Value(Script(), "PyreticActive") && dangers() == 0
            && ((string)Value(Script(), "MechanicHintsText")).Contains("Applies Pyretic")
            && ((string)Value(Script(), "PredictedDamageText")).Contains("2人/1次"),
            "Gamma raidwide did not retain pure prediction/warning or teammate status locked the player.");
        controller.Seek(2.5d);
        Check((bool)Value(Script(), "PyreticActive")
            && ((string)Value(Script(), "MechanicHintsText")).Contains("Pyretic on you!")
            && Value(Script(), "PredictedDamageText") == null, "Gamma lost SourceId0 status-before-snapshot or cleared it at cast end.");
        Isolated();
        controller.Seek(4.05d);
        Check(!(bool)Value(Script(), "PyreticActive"), "StatusLose failed to override the previous affected snapshot.");
        controller.Seek(5.5d);
        Check((bool)Value(Script(), "PyreticActive"), "Gamma did not acquire an already present Pyretic status.");
        controller.Seek(6.5d);
        Check(!(bool)Value(Script(), "PyreticActive"), "Gamma Pyretic ignored actual expiry.");
        controller.Seek(8.5d);
        Check(!(bool)Value(Script(), "PyreticActive") && previews().Length == 0, "Disengaged Gamma retained personal or enemy output.");
        controller.Seek(2.5d);
        Check((bool)Value(Script(), "PyreticActive"), "Gamma reverse Seek failed to reconstruct personal state.");
        Isolated();
        stop();
        object Affected(ulong source, double expiry) => new
        {
            Id = 100, Kind = 1, Position = Pose(Vector3.Zero), Hp = 1000, MaxHp = 1000, Targetable = true,
            Statuses = new[] { new { Id = 960, SourceId = source, ExpiresAt = Start.AddSeconds(expiry) } }
        };
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x1AB2, Vector3.Zero, combat: true), Affected(0, 3)], full: true, party: party),
            Gain(1, 100, 5, 1), StatusEvent(1, 100, 960, 0, false),
            Frame(territory, 1.1, [Affected(1, 6)], party: party),
            Gain(5.95, 100, 6, 1),
            Frame(territory, 6, [Affected(1, 6)], party: party),
            Frame(territory, 7, [Actor(100, 0, Vector3.Zero, dead: true)], party: party),
            Frame(territory, 8, [Actor(100, 0, Vector3.Zero)], party: party),
            Frame(territory, 9, [], party: party)
        ]);
        controller.Seek(1.5d);
        Check((bool)Value(Script(), "PyreticActive"), "PR same-frame new-source Add/old-source Remove incorrectly released Pyretic.");
        controller.Seek(6.5d);
        Check((bool)Value(Script(), "PyreticActive"), "Old status snapshot discarded an event refresh with a later expiry.");
        controller.Seek(8.5d);
        Check(!(bool)Value(Script(), "PyreticActive"), "Death followed by revival resurrected an unobserved old gain.");
        Isolated();
        stop();
        Console.WriteLine("PASS HuntSequences actual DLL Angada simultaneous Butcher/Rip, actual effect and instance isolation, Gamma real status/SourceId0/party/expiry, pure predictions, reverse Seek and zero replay control");
    }

    private static void RunHuntInvertedGaze(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        const uint territory = 956;
        var rules = new (uint Action, double Seconds)[]
        {
            (27363, 7), (27364, 7), (27365, 7), (27366, 3), (27367, 4), (27369, 7), (27370, 7), (27371, 7)
        };
        Vector3 Location(int i) => i == 0 ? -Vector3.UnitZ * 20 : i == 5 ? Vector3.UnitZ * 20 : new(100 + i * 10, 0, 0);
        var lines = new List<string>
        {
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, Vector3.Zero, rotation: MathF.PI)], full: true),
            Frame(territory, .1, rules.Select((r, i) => Actor((ulong)i + 1, 0x35DF, Location(i), combat: true,
                cast: Cast(r.Action, r.Seconds, Location(i))))),
        };
        for (var i = 0; i < rules.Length; ++i) lines.Add(CastEvent(.1, (ulong)i + 1, rules[i].Action, rules[i].Seconds, Location(i)));
        lines.Add(Frame(territory, 8, rules.Select((_, i) => Actor((ulong)i + 1, 0x35DF, Location(i), combat: true))));
        lines.Add(Frame(territory, 9, []));
        load("masterplayer.hunts.simple", lines);
        object Script() => ((IEnumerable)manager.Running.Values).Cast<dynamic>().Single().Script;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;
        float? Heading()
        {
            var script = Script();
            var encounter = script.GetType().BaseType!.GetField("encounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(script)!;
            return (float?)encounter.GetType().GetMethod("Facing", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(encounter, [Value(script, "CurrentPlayer"), (object)runtime.CurrentTime]);
        }
        controller.Seek(1d);
        Check(dangers() == 5 && previews().Length == 19 && Heading() == null
            && ((string)Value(Script(), "MechanicHintsText")).Contains("Face the eye!")
            && ((string)Value(Script(), "MechanicHintsText")).Contains("Turn away from gaze!")
            && ((string)Value(Script(), "MechanicHintsText")).Contains("Invert next cast"),
            "LunatenderQueen lost one of its eight source rules, mixed gaze hints or both arc shapes.");
        var invertedArc = previews().Single(p => ((string)Value(p, "Id")).EndsWith("gaze/view/inverted", StringComparison.Ordinal));
        dynamic arc = Value(invertedArc, "Geometry");
        Check((bool)arc.Contains(Vector3.UnitZ, 0f) && !(bool)arc.Contains(-Vector3.UnitZ, 0f),
            "Actual DLL inverted gaze arc did not cover the rear 270 degrees.");
        controller.Seek(6.7d);
        Check(Heading() is { } heading && MathF.Cos(heading) > .707107f, "Mixed inverted/ordinary gaze did not share a safe PR heading.");
        var hints = Value(Script(), "Hints");
        var facing = hints.GetType().GetField("_facing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hints)!;
        Check(!(bool)facing.GetType().GetField("_hasRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(facing)!,
            "Offline inverted gaze published PR control.");
        controller.Seek(8.5d);
        Check(dangers() == 0 && previews().Length == 8 && Heading() == null, "Gaze finish retained inverse/normal arcs or heading.");
        controller.Seek(1d);
        Check(dangers() == 5 && previews().Length == 19, "LunatenderQueen reverse Seek did not rebuild all rules.");
        stop();
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(100, 0, Vector3.Zero)], full: true),
            Frame(territory, .1, [
                Actor(1, 0x35DF, Vector3.UnitZ * 20, combat: true, cast: Cast(27369, 3, Vector3.UnitZ * 20)),
                Actor(2, 0x35DF, new(.19999667f, 0, 19.999f), combat: true, cast: Cast(27363, 3, new(.19999667f, 0, 19.999f)))]),
            Frame(territory, 4, [])
        ]);
        controller.Seek(2.8d);
        Check(Heading() is { } narrow && MathF.Cos(narrow) >= .707107f && MathF.Cos(narrow - .01f) < .707107f,
            "Actual DLL lost a narrow shared gaze direction that satisfies both source hit tests.");
        stop();
        Console.WriteLine("PASS HuntInvertedGaze actual DLL LunatenderQueen eight rules, inverse/ordinary hints, shared PR heading, rear270/front90 arcs, pure Hint, observed finish, reverse Seek and replay isolation");
    }
}

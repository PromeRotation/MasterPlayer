using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using PromeRotation.LogSystem;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureMolter(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Encounter()
        {
            var script = Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
            return script.GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(script)!;
        }
        object[] Models(string name) => ((IEnumerable)Value(Encounter(), name)).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        string EState(double time, ulong id, ushort state) => JsonSerializer.Serialize(new
        {
            t = time, kind = "EObjSetState", type = "PromeRotation.LogSystem.LogSystemEObjSetStateEvent",
            data = new { SourceId = id, State = state, Timestamp = Start.AddSeconds(time) },
        });
        string Status(double time, bool present, ulong target = 1, double? eventTime = null, ulong source = 1) => JsonSerializer.Serialize(new
        {
            t = time, kind = present ? "StatusAdd" : "StatusRemove",
            type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
            data = new { TargetId = target, SourceId = source, StatusId = 3997, Duration = 1f, Timestamp = Start.AddSeconds(eventTime ?? time) },
        });
        const string guid = "masterplayer.treasure.cenote-ja-ja-gural";
        var c = new Vector3(0, 5, -372);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)];
        uint[] actions = [38274, 38275, 38265, 38266, 38267, 38273, 38580, 38271, 38335, 38336, 38337,
            38279, 38278, 38277, 32301, 32302, 32303, 32304, 32305];
        var casts = actions.Select((action, i) => Actor((ulong)i + 2, 0x233C, c, cast: Cast(action, 5 + i, c, target: 100))).ToArray();
        load(guid,
        [
            Header(1209), Frame(1209, 0, [Actor(1, 0x4306, c, combat: true), .. players], full: true, party: party),
            Frame(1209, .1, casts, party: party), Frame(1209, 50, [], party: party),
            Frame(1209, 51, casts.Select((_, i) => Actor((ulong)i + 2, 0x233C, c)).ToArray(), party: party),
            Status(52, false, 101), Frame(1209, 53, [], party: party),
        ]);
        controller.Seek(.2);
        Check(dangers() == 16 && Models("PredictedDamage").Length == 3 && Find("treasure/crypsis/").Length == 1,
            "Molter ordinary geometry, source forecasts or Crypsis9 output missing.");
        Check(actions.All(a => (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)a }))
            && new ushort[] { 38268, 38280, 38272, 38276, 38264 }.All(a => !(bool)manager.IsGenericDodgeSuppressed(
                new LogSystemCastStartEvent { SourceId = 2, ActionId = a })), "Molter active/inactive suppression boundary differs.");
        var reveal = Aoes("treasure/crypsis/").Single(a => (bool)Value(a, "Display"));
        Check((float)Value(Value(reveal, "Shape"), "Radius") == 9 && (Vector4)Value(reveal, "Color") == new Vector4(0, 1, 0, 1)
            && (bool)Value(reveal, "OutlineOnly") && !(bool)Value(reveal, "Navigate"), "Crypsis safe circle9 display differs.");
        var spread = Aoes("quest/spread/");
        Check((float)Value(Value(spread.Single(a => (bool)Value(a, "Display")), "Shape"), "Radius") == 6
            && (float)Value(Value(spread.Single(a => (bool)Value(a, "Navigate")), "Shape"), "Radius") == 7,
            "Molter spread6/7 differs.");
        controller.Seek(50.1);
        Check(dangers() == 16, "Forecast timers removed source active mechanisms.");
        controller.Seek(51.1);
        Check(dangers() == 1 && !Models("PredictedDamage").Any(), "Actual Finish did not clear casts or incorrectly cleared Crypsis.");
        controller.Seek(52.1);
        Check(dangers() == 0 && !Find("treasure/crypsis/").Any(), "Other actor first Lose3997 failed global reveal.");
        controller.Seek(.2);
        Check(dangers() == 16, "Molter reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Molter all19 input rules ordinary views navigation spread6 and7 forecasts Crypsis Finish retention any Lose3997 reverse Seek owner cleanup");

        load(guid,
        [
            Header(1209), Frame(1209, 0, [Actor(1, 0x4306, c, combat: true), .. players], full: true, party: party),
            Frame(1209, .1, [Actor(2, 0x233C, c, cast: Cast(38265, 3, c)),
                Actor(3, 0x233C, c, stackedStatus: 3997, statusExpiresAt: Start.AddSeconds(1))], party: party),
            Frame(1209, 2, [Actor(2, 0x233C, c), Actor(1, 0x4306, c + Vector3.UnitX * 3, combat: true)], party: party),
            Frame(1209, 3, [Actor(3, 0x233C, c)], party: party),
            Status(4, true), Frame(1209, 5, [], party: party),
        ]);
        controller.Seek(2.1);
        var moved = Aoes("treasure/crypsis/").Single(a => (bool)Value(a, "Display"));
        Check((Vector3)Value(moved, "Position") == c + Vector3.UnitX * 3 && dangers() == 1
            && Value(Encounter(), "TreasureSprintIntent") != null, "Crypsis failed dynamic primary position or expired occupied slot.");
        controller.Seek(3.1);
        Check(dangers() == 0, "True raw status Lose did not reveal boss.");
        controller.Seek(4.1);
        Check(dangers() == 0 && Value(Encounter(), "TreasureSprintIntent") == null, "Gain3997 alone concealed boss.");
        stop();
        Console.WriteLine("PASS native treasure production Molter Crypsis dynamic primary9 raw occupied expired slot true Lose Gain no conceal Sprint intent only");

        load(guid,
        [
            Header(1209), Frame(1209, 0, [Actor(1, 0x4306, c, combat: true), .. players], full: true, party: party),
            Status(.1, false, source: 0),
            Frame(1209, .2, [Actor(1, 0x4306, c, combat: true, stackedStatus: 3997,
                statusExpiresAt: Start.AddSeconds(5))], party: party),
            Frame(1209, 1, [Actor(1, 0x4306, c, combat: true)], party: party),
            Frame(1209, 2, [Actor(2, 0x233C, c, cast: Cast(38266, 5, c))], party: party),
            Status(3, false, eventTime: .9, source: 0),
            Frame(1209, 4, [Actor(1, 0x4306, c, combat: true, stackedStatus: 3997)], party: party),
            Status(5, false, source: 0), Frame(1209, 6, [], party: party),
        ]);
        controller.Seek(3.1);
        Check(dangers() == 1 && Value(Encounter(), "TreasureSprintIntent") != null,
            "Historical empty event value prevented acknowledging raw Lose and cleared new Crypsis.");
        controller.Seek(5.1);
        Check(dangers() == 0, "Current Lose consumed a prior raw cycle's old removal receipt.");
        stop();
        Console.WriteLine("PASS native treasure production Molter prior Lose history raw cycle queued old Lose preserves new Crypsis");

        load(guid,
        [
            Header(1209), Frame(1209, 0, [Actor(1, 0x4306, c, combat: true), Actor(50, 0x42FE, c), .. players],
                full: true, party: party),
            Frame(1209, .1, [Actor(2, 0x233C, c, cast: Cast(38265, 3, c))], party: party),
            Frame(1209, 1, [], removed: [1], party: party),
            Frame(1209, 2, [Actor(1, 0x4306, c + Vector3.UnitX * 20)], party: party),
            Frame(1209, 3, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(dangers() == 1 && Find("treasure/crypsis/").Length == 1,
            "Destroyed primary lost Crypsis output during reward phase.");
        controller.Seek(2.1);
        Check((Vector3)Value(Aoes("treasure/crypsis/").Single(a => (bool)Value(a, "Display")), "Position") == c,
            "New same-ID primary rebound the destroyed Crypsis position.");
        stop();
        Console.WriteLine("PASS native treasure production Molter destroyed primary retains Crypsis original position reward phase same ID cannot rebind");

        load(guid,
        [
            Header(1209), Frame(1209, 0, [Actor(1, 0x4306, c, combat: true), .. players,
                Actor(10, 0x1EBCB8, c + Vector3.UnitX * .13f), Actor(11, 0x1EBCB8, c + Vector3.UnitX * 5),
                Actor(3, 0x233C, c)], full: true, party: party),
            EState(1, 3, 0x0104), EState(2, 999, 4),
            Frame(1209, 3, [Actor(10, 0x1EBCB8, c + Vector3.UnitX * 10, dead: true, eventState: 7)], party: party),
            Frame(1209, 4, [], removed: [10], party: party), EState(5, 3, 4), EState(6, 3, 4),
            Frame(1209, 7, [], party: party),
        ]);
        controller.Seek(4.1);
        Check(dangers() == 2 && Find("treasure/created/").Length == 2 && Models("TreasureCreatedCircles").Length == 2,
            "Pool movement/death/state7/Destroy/high-byte or unknown actor EState removed a circle.");
        var circles = Models("TreasureCreatedCircles");
        Check(circles.All(v => (float)Value(v, "Radius") == 17 && (DateTime)Value(v, "Activation") == default)
            && MathF.Abs(((Vector3)Value(circles[0], "Position")).X - 3.5f * (2000f / 65535f)) < .0001f,
            "Pool creation quantization17/default clock differs.");
        controller.Seek(5.1);
        Check(dangers() == 1 && (ulong)Value(Models("TreasureCreatedCircles").Single(), "Source") == 11,
            "Known non-pool actor ushort4 did not consume global first.");
        controller.Seek(6.1);
        Check(dangers() == 0, "Independent second ushort4 did not consume next pool.");
        controller.Seek(4.1);
        Check(dangers() == 2, "Pool reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Molter quantized pool17 creation ushort exact4 nonpool global FIFO Destroy retention reverse Seek dual views navigation");

        var lights = Enumerable.Range(2, 5).Select(i => Actor((ulong)i, 0x233C, c, cast: Cast(38271, 12 - i, c))).ToArray();
        load(guid,
        [
            Header(1209), Frame(1209, 0, [Actor(1, 0x4306, c, combat: true), .. players], full: true, party: party),
            Frame(1209, .1, lights, party: party), Frame(1209, 1, [Actor(2, 0x233C, c)], party: party),
            Frame(1209, 2, [], party: party),
        ]);
        controller.Seek(.2);
        var cones = Aoes("treasure/light/");
        Check(dangers() == 4 && cones.Length == 4 && cones.Take(2).All(a => (Vector4)Value(a, "Color") == new Vector4(1, 1, 0, 1))
            && !cones.Any(a => (string)Value(a, "Id") == "treasure/light/6"), "Aetherial first4/first2Danger differs.");
        controller.Seek(1.1);
        Check(dangers() == 4 && Find("treasure/light/6").Length == 1, "Hidden fifth cone failed Finish promotion.");
        stop();
        Console.WriteLine("PASS native treasure production Molter AetherialLight insertion first4 first2 yellow risk all reverse clocks hidden promotion");

        var room = new Vector3(0, 5, 377);
        load(guid,
        [
            Header(1209), Frame(1209, 0, [Actor(1, 0x1EBAA4, room), Actor(2, 0x4307, room),
                Actor(100, 0, room + Vector3.UnitZ * 8), Actor(101, 0, room + Vector3.UnitX * 10)], full: true, party: party),
            Frame(1209, .1, [Actor(2, 0x4307, room, cast: Cast(38281, 4, room, target: 100))], party: party),
            EffectEvent(.5, 2, 38281, 5, room),
            Frame(1209, 1, [Actor(100, 0, room + Vector3.UnitX * 6, dead: true)], party: party),
            EState(1.5, 1, 7), Frame(1209, 2, [Actor(2, 0x4307, room)], party: party),
            Frame(1209, 3, [Actor(1, 0x1EBAA4, room, eventState: 7)], party: party),
            Frame(1209, 4, [], party: party),
        ]);
        controller.Seek(.2);
        var bait = Aoes("treasure/cast-bait/").Single(a => (bool)Value(a, "Display"));
        Check((float)Value(Value(bait, "Shape"), "Length") == 8 && (float)Value(Value(bait, "Shape"), "MaxLocalX") == 1.5f
            && dangers() == 1 && Models("PredictedDamage").Length == 1
            && (bool)manager.IsGenericDodgeSuppressed(new LogSystemCastStartEvent { SourceId = 2, ActionId = 38281 }),
            "Room1 source charge3 geometry, prediction, navigation or suppression differs.");
        controller.Seek(1.6);
        bait = Aoes("treasure/cast-bait/").Single(a => (bool)Value(a, "Display"));
        Check((float)Value(Value(bait, "Shape"), "Length") == 6 && MathF.Abs((float)Value(bait, "Rotation") - MathF.PI / 2) < .00001f
            && Value(Encounter(), "Active") != null, "Charge dead target dynamics, Effect retention or EState/native separation differs.");
        controller.Seek(2.1);
        Check(dangers() == 0 && !Find("treasure/cast-bait/").Any() && Value(Encounter(), "Active") != null,
            "Finish-only charge clear or room lifetime differs.");
        controller.Seek(3.1);
        Check(Value(Encounter(), "Active") == null, "Native state7 did not end Room1.");
        controller.Seek(.2);
        Check(dangers() == 1, "Room1 charge reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Cenote Room1 dynamic charge3 target death effect retention tankbuster prediction PR source ownership EState separation true Finish native state7 Seek");
    }
}

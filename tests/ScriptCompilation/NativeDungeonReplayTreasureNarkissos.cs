using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureNarkissos(object managerObject, object runtimeObject, object controllerObject,
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
        object[] Models(string property) => ((IEnumerable)Value(Encounter(), property)).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        string[] Hints() => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [new Vector3(100, 0, 100)])!)
            .Cast<string>().ToArray();
        string Status(double t, uint status, bool present, double duration = 100, ulong source = 0) => JsonSerializer.Serialize(new
        {
            t, kind = present ? "StatusAdd" : "StatusRemove",
            type = "PromeRotation.LogSystem.LogSystemStatus" + (present ? "Add" : "Remove") + "Event",
            data = new { TargetId = 100ul, SourceId = source, StatusId = status, Param = 0, Duration = duration, Timestamp = Start.AddSeconds(t) },
        });
        Vector3 Landing()
        {
            var last = Models("TreasureMarchMovements").Last();
            return (Vector3)last.GetType().GetField("Item2")!.GetValue(last)!;
        }
        const string guid = "masterplayer.treasure.shifting-gymnasion";
        var c = new Vector3(100, 0, 100);
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        object[] players = [Actor(100, 0, c), Actor(101, 0, c + Vector3.UnitX * 8)];
        uint[] actions = [32336, 32333, 32339, 32338, 32317];
        var casting = actions.Select((action, i) => Actor((ulong)i + 2, 0x233C, c, cast: Cast(action, 5, c))).ToArray();
        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D48, c, combat: true), .. players], full: true, party: party),
            Frame(1123, .1, [.. casting, Actor(30, 0x233C, c, cast: Cast(32330, 5, c, target: 101)),
                Actor(31, 0x233C, c, cast: Cast(32331, 5, c)),
                Actor(32, 0x233C, c, cast: Cast(32332, 5, c + Vector3.UnitZ * 100)),
                Actor(33, 0x233C, c, cast: Cast(32340, 5, c, target: 100))], party: party),
            Frame(1123, 30, [], party: party),
            Frame(1123, 31, [.. casting.Select((_, i) => Actor((ulong)i + 2, 0x233C, c)),
                .. new ulong[] { 30, 31, 32, 33 }.Select(id => Actor(id, 0x233C, c))], party: party),
            Frame(1123, 32, [], party: party),
        ]);
        controller.Seek(.2);
        Check(dangers() == 6 && Models("PredictedDamage").Length == 3 && Find("treasure/gaze/").Length == 3
            && Aoes("treasure/sap/").Single() is { } sap && (Vector4)Value(sap, "Color") == new Vector4(1, 0, 0, 1),
            "Narkissos active output, damage, red Sap or default10000 gaze differs.");
        Check(new uint[] { 32336, 32330, 32332, 32333, 32339, 32338, 32340, 32331, 32317 }
            .All(action => (bool)manager.IsGenericDodgeSuppressed(
                new PromeRotation.LogSystem.LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })),
            "Narkissos source action suppression missing.");
        var spread = Aoes("quest/spread/");
        Check((float)Value(Value(spread.Single(a => (bool)Value(a, "Display")), "Shape"), "Radius") == 6
            && (float)Value(Value(spread.Single(a => (bool)Value(a, "Navigate")), "Shape"), "Radius") == 7,
            "Narkissos spread6/7 geometry differs.");
        controller.Seek(5);
        Check(Encounter().GetType().GetMethod("GazeFacing")!.Invoke(Encounter(), [c, 0f]) is float,
            "Narkissos near-finish PR gaze facing intent missing.");
        controller.Seek(30.1);
        Check(dangers() == 6, "Narkissos forecast clock expired source output.");
        controller.Seek(31.1);
        Check(dangers() == 0 && !Find("treasure/gaze/").Any() && !Models("PredictedDamage").Any(),
            "Narkissos true Finish retained output.");
        controller.Seek(.2);
        Check(dangers() == 6, "Narkissos reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Narkissos all9 rules red Sap spread6 and7 gaze10000 PR facing forecast clocks true Finish views navigation Seek");

        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D48, c, combat: true), .. players], full: true, party: party),
            .. Enumerable.Range(0, 5).SelectMany(i => new[]
            {
                Frame(1123, i * 10 + .1, [Actor(2, 0x233C, c, cast: Cast(32336, 5, c, startedAt: i * 10 + .1))], party: party),
                Frame(1123, i * 10 + 1, [Actor(2, 0x233C, c)], party: party),
            }),
            Frame(1123, 50, [], party: party),
        ]);
        controller.Seek(30.2);
        Check(Hints().Contains("Circles resolve before cross"), "Sap fourth source start count differs.");
        controller.Seek(40.2);
        Check(Hints().Contains("Circles resolve before cross, aim forced march into cross"),
            "Sap fifth source start failed cumulative transition.");
        controller.Seek(41.1);
        Check(!Hints().Any(h => h.StartsWith("Circles resolve")), "Sap source Finish did not revoke global hint.");
        controller.Seek(30.2);
        Check(Hints().Contains("Circles resolve before cross"), "Sap reverse Seek failed count reconstruction.");
        stop();
        Console.WriteLine("PASS native treasure production Narkissos Sap cumulative fifth transition true Finish and reverse Seek");

        foreach (var (status, direction) in new[] { (1958u, 0f), (1960u, MathF.PI / 2), (1959u, MathF.PI), (1961u, 3 * MathF.PI / 2) })
        {
            var expected = c + new Vector3(MathF.Sin(direction), 0, MathF.Cos(direction)) * 13.2f;
            load(guid,
            [
                Header(1123), Frame(1123, 0, [Actor(1, 0x3D48, c, combat: true), .. players], full: true, party: party),
                Status(.1, status, true),
                Frame(1123, .8, [], party: party),
                Frame(1123, 1, [Actor(100, 0, c, stackedStatus: status, statusParam: 0, statusExpiresAt: Start.AddSeconds(100.3))], party: party),
                Frame(1123, 120, [], party: party),
                Frame(1123, 121, [Actor(100, 0, c)], party: party), Frame(1123, 122, [], party: party),
            ]);
            controller.Seek(.9);
            Check(Vector3.Distance(Landing(), expected) < .00001f && dangers() == 0
                && Find("treasure/march/").Length == 2
                && Aoes("treasure/march/").All(a => !(bool)Value(a, "Navigate")),
                $"March{status} event overlay/13.2/direction/dual-view-only contract differs.");
            controller.Seek(120.1);
            Check(Models("TreasureMarchMovements").Length == 1 && Vector3.Distance(Landing(), expected) < .00001f,
                "Raw expiry correction or script status timer duplicated/removed march.");
            controller.Seek(121.1);
            Check(!Models("TreasureMarchMovements").Any() && !Find("treasure/march/").Any(),
                "Raw real Lose retained march.");
            controller.Seek(.9);
            Check(Models("TreasureMarchMovements").Length == 1, "March reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production Narkissos march{status} ordered13.2 event raw overlay expiry correction occupied expired slot true Lose projections only Seek");
        }

        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D48, c, combat: true), .. players], full: true, party: party),
            Status(.1, 1958, true),
            Frame(1123, .2, [Actor(2, 0x233C, c, cast: Cast(32336, 5, c + Vector3.UnitZ * 13, startedAt: .2))], party: party),
            Frame(1123, 1, [Actor(2, 0x233C, c), Actor(3, 0x233C, c, cast: Cast(32339, 5, c, startedAt: 1))], party: party),
            Status(2, 1958, false), Status(2, 1958, true, 200),
            Status(3, 1422, true, 1), Status(3.1, 2936, true, 1), Status(5, 1422, false),
            Frame(1123, 10, [], party: party),
        ]);
        controller.Seek(.3);
        Check(Hints().Contains("Aim for safe spot!"), "Current Sap did not mark forced landing unsafe.");
        controller.Seek(1.1);
        Check(!Hints().Contains("Aim for safe spot!"), "Cross added to Sap-only destination check.");
        controller.Seek(2.1);
        Check(Models("TreasureMarchMovements").Length == 1, "Same-time real Lose/Gain lost march replacement.");
        controller.Seek(4.5);
        Check((bool)Value(Encounter(), "TreasureMisdirectionActive"), "Misdirection expired by expected timer.");
        controller.Seek(5.1);
        Check(!(bool)Value(Encounter(), "TreasureMisdirectionActive"), "Any misdirection Lose must clear the mask.");
        stop();
        Check(dangers() == 0 && !previews().Any(), "Narkissos stop did not revoke owner outputs.");
        Console.WriteLine("PASS native treasure production Narkissos Sap-only destination same-time status replacement misdirection diagnostic timer retention any Lose owner cleanup");

        object RawMarch(ulong source, double expiration)
        {
            var actor = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(Actor(100, 0, c)))!;
            actor["Statuses"] = JsonSerializer.SerializeToElement(new[]
            {
                new { Id = 1958, Param = 0, SourceId = source, ExpiresAt = Start.AddSeconds(expiration) },
            });
            return actor;
        }
        var removal = JsonSerializer.Serialize(new
        {
            t = 2, kind = "ObjectRemove", type = "PromeRotation.LogSystem.LogSystemObjectRemoveEvent",
            data = new { SourceId = 100ul, Timestamp = Start.AddSeconds(2) },
        });
        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D48, c, combat: true), RawMarch(1, 10), players[1]], full: true, party: party),
            Frame(1123, 1, [RawMarch(2, 20)], party: party), Status(1, 1958, false), Status(1, 1958, true, 19, 2),
            Frame(1123, 1.1, [], party: party), removal, Frame(1123, 2.1, [], party: party),
            Frame(1123, 3, [RawMarch(2, 20)], party: party),
            Frame(1123, 4, [Actor(100, 0, c)], party: party),
            Status(5, 1958, true, 25, 2), Frame(1123, 6, [RawMarch(2, 30)], party: party),
            Frame(1123, 7, [Actor(100, 0, c)], party: party),
            Status(8, 1958, true, 10, 2), Status(9, 1958, false),
            Frame(1123, 10, [RawMarch(2, 18)], party: party),
            Frame(1123, 11, [RawMarch(2, 40)], party: party), Frame(1123, 12, [], party: party),
        ]);
        controller.Seek(1.2);
        Check(Models("TreasureMarchMovements").Length == 1, "Raw-first source replacement swallowed its network Gain.");
        controller.Seek(3.1);
        Check(!Models("TreasureMarchMovements").Any() && !Find("treasure/march/").Any(),
            "Explicit destruction followed by old raw frame revived march.");
        controller.Seek(6.1);
        Check(Models("TreasureMarchMovements").Length == 1, "Fresh post-destroy network/raw generation did not recover.");
        controller.Seek(10.1);
        Check(!Models("TreasureMarchMovements").Any(), "Skipped old Gain raw revoked true Lose.");
        controller.Seek(11.1);
        Check(Models("TreasureMarchMovements").Length == 1, "Skipped old event cycle swallowed new raw generation.");
        controller.Seek(3.1);
        Check(!Models("TreasureMarchMovements").Any(), "Reverse Seek lost destroyed-state barrier.");
        stop();
        Console.WriteLine("PASS native treasure production Narkissos raw-first source swap equal timestamp Lose Gain destroyed stale frame barrier new generation skipped cycle and reverse Seek");

        load(guid,
        [
            Header(1123), Frame(1123, 0, [Actor(1, 0x3D48, c, combat: true), RawMarch(1, 10)], full: true, party: party),
            Frame(1123, 1, [Actor(100, 0, c)], party: party),
            JsonSerializer.Serialize(new
            {
                t = 1.1, kind = "StatusRemove", type = "PromeRotation.LogSystem.LogSystemStatusRemoveEvent",
                data = new { TargetId = 100ul, SourceId = 1ul, StatusId = 1958, Timestamp = Start.AddSeconds(.8) },
            }),
            Frame(1123, 2, [RawMarch(1, 10)], party: party),
            Status(3, 1958, true, 7, 1), Frame(1123, 4, [], party: party),
        ]);
        controller.Seek(2.1);
        Check(!Models("TreasureMarchMovements").Any(), "Historical raw-first Lose receipt allowed stale occupied frame to revive march.");
        controller.Seek(3.1);
        Check(Models("TreasureMarchMovements").Length == 1, "Real same-fingerprint Gain failed to release historical Lose barrier.");
        stop();
        Console.WriteLine("PASS native treasure production Narkissos raw-first Lose historical receipt stale occupied frame barrier real same-fingerprint Gain recovery");
    }
}

using System.Reflection;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunHuntOwnerInitialization(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic controller = controllerObject;
        dynamic runtime = runtimeObject;
        object Value(object obj, string name) => obj.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;
        const uint territory = 1187;
        object[] party = [new { Slot = 0, EntityId = 100ul }];
        (uint Oid, uint Action, string Marker, int Count)[] cases =
        [
            (0x416D, 39013, "/stack/preview/", 1),
            (0x416E, 39099, "/heshuala/", 2),
            (0x4582, 39773, "/ihnuxokiy/", 1),
            (0x4233, 37377, "/neyoozoteel/sap/", 3),
            (0x452C, 39493, "/nechuciho/", 2)
        ];
        foreach (var (oid, action, marker, count) in cases)
        foreach (var delayRaw in new[] { false, true })
        {
            object Boss(ulong id) => Actor(id, oid, default, combat: true, stackedStatus: oid == 0x416E ? 3979u : 0);
            var raw = Cast(action, 5, default, target: 100, startedAt: .05);
            var frames = new List<string>
            {
                Header(territory),
                Frame(territory, 0, [Boss(1), Actor(999, 0x233C, default), Actor(100, 0, default)], full: true, party: party),
                CastEvent(.05, 999, action, 5, default, target: 100),
                Frame(territory, .2, [Boss(2), Actor(999, 0x233C, default, cast: delayRaw ? null : raw)], party: party),
                Frame(territory, .4, [Actor(999, 0x233C, default, cast: raw)], party: party),
                Frame(territory, .6, [Actor(999, 0x233C, default, cast: raw)], party: party),
                Frame(territory, 1, [], removed: [1], party: party),
                Frame(territory, 2, [], removed: [2], party: party),
                Frame(territory, 3, [], party: party)
            };
            load("masterplayer.hunts.simple", frames);
            int Outputs() => previews().Count(p => ((string)Value(p, "Id")).Contains(marker));
            controller.Seek(.3);
            Check(Outputs() == count * (delayRaw ? 1 : 2),
                $"Component activation inherited old packet or missed same-frame first raw {oid:X}/{action}.");
            controller.Seek(.5);
            Check(Outputs() == count * 2, $"Delayed first raw did not fill both owners {oid:X}/{action}.");
            controller.Seek(.8);
            Check(Outputs() == count * 2, $"Stable raw duplicated a received component {oid:X}/{action}.");
            controller.Seek(1.1);
            Check(Outputs() == count, $"Removing one component discarded another owner {oid:X}/{action}.");
            controller.Seek(2.1);
            Check(previews().Length == 0, $"Final component owner removal retained previews {oid:X}/{action}.");
            controller.Seek(.5);
            Check(Outputs() == count * 2, $"Reverse Seek retained component receipts {oid:X}/{action}.");
            stop();
        }
        load("masterplayer.hunts.simple",
        [
            Header(territory),
            Frame(territory, 0, [Actor(1, 0x452C, default, combat: true), Actor(999, 0x233C, default),
                Actor(100, 0, default)], full: true, party: party),
            CastEvent(.05, 999, 39493, 5, new(10, 0, 20), .2f),
            Frame(territory, .2, [Actor(2, 0x452C, default, combat: true),
                Actor(999, 0x233C, default, cast: Cast(39493, 6, new(30, 0, 40), 1, startedAt: .05))], party: party),
            CastEvent(.4, 999, 39493, 7, new(50, 0, 60), 1.6f),
            Frame(territory, .6, [], party: party),
            Frame(territory, 1, [], removed: [1, 2], party: party),
            Frame(territory, 2, [], party: party),
        ]);
        DateTime[] Deadlines()
        {
            dynamic snapshot = runtime.Manager.BuildSnapshot(runtime.CurrentTime);
            return ((System.Collections.IEnumerable)snapshot.DangerAreas).Cast<object>()
                .SelectMany(a => ((System.Collections.IEnumerable)Value(a, "DangerWindows")).Cast<object>())
                .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        }
        controller.Seek(.3);
        var packetFinish = Start.AddSeconds(.05).AddSeconds(5d + .3);
        var rawFinish = Start.AddSeconds(.05 + 6 + .3);
        Check(Deadlines().SequenceEqual(new[]
        {
            packetFinish.AddSeconds(9.3f), packetFinish.AddSeconds(9.3f + 2f),
            rawFinish.AddSeconds(9.3f), rawFinish.AddSeconds(9.3f + 2f)
        }.Order()), "New component did not use its current raw clock alongside the old owner's packet baseline.");
        controller.Seek(.5);
        var correctedFinish = Start.AddSeconds(.4).AddSeconds(7d + .3);
        Check(Deadlines().SequenceEqual(new[]
        {
            correctedFinish.AddSeconds(9.3f), correctedFinish.AddSeconds(9.3f),
            correctedFinish.AddSeconds(9.3f + 2f), correctedFinish.AddSeconds(9.3f + 2f)
        }.Order()), "Later packet did not correct each owner's delivered baseline.");
        controller.Seek(1.1);
        Check(previews().Length == 0 && dangers() == 0, "Per-owner baseline survived final revoke.");
        stop();
        Console.WriteLine("PASS Hunt actual DLL five component owners, same-frame and delayed first raw confirmation, packet isolation, exactly-once receipts, reverse Seek and immediate revoke");
    }
}

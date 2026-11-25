using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureCladoTyphon(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Models(string property) => ((IEnumerable)Value(Encounter(), property)).Cast<object>().ToArray();
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Aoes(string key) => ((IEnumerable)Encounter().GetType().GetMethod("AmbientAoes")!.Invoke(Encounter(), null)!)
            .Cast<object>().Where(a => ((string)Value(a, "Id")).StartsWith(key)).ToArray();
        string Icon(double t, ulong actor, uint icon) => JsonSerializer.Serialize(new { t, kind = "TargetIcon",
            type = "PromeRotation.LogSystem.LogSystemTargetIconEvent",
            data = new { SourceId = actor, TargetId = 101ul, MarkerId = icon, Timestamp = Start.AddSeconds(t) } });
        const string guid = "masterplayer.treasure.shifting-oubliettes";
        var c = new Vector3(100, 0, 100);
        var p = c + Vector3.UnitZ * 10;
        object[] party = [.. Enumerable.Range(0, 8).Select(i => (object)new { Slot = i, EntityId = (ulong)(100 + i) })];
        object[] players = [.. Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0, p + Vector3.UnitX * i))];
        foreach (var (oid, actions) in new[]
        {
            (0x3027u, new uint[] { 21705, 21704, 21769, 21767, 21768 }),
            (0x301Au, new uint[] { 21689, 21771, 21772, 21695, 21697, 6448, 6449, 6450, 6451, 6452 }),
        })
        {
            var casting = actions.Select((action, i) => Actor((ulong)i + 2, 0x233C, c,
                cast: Cast(action, 5 + i, c))).ToArray();
            load(guid,
            [
                Header(924), Frame(924, 0, [Actor(1, oid, c, combat: true), .. players], full: true, party: party),
                Frame(924, .1, casting, party: party), Frame(924, 30, [], party: party),
                Frame(924, 31, [Actor(2, 0x233C, c)], party: party),
                Frame(924, 32, casting.Skip(1).Select((_, i) => Actor((ulong)i + 3, 0x233C, c)), party: party),
                Frame(924, 33, [], party: party),
            ]);
            controller.Seek(.2);
            Check(dangers() == actions.Length && actions.All(action => (bool)manager.IsGenericDodgeSuppressed(
                new PromeRotation.LogSystem.LogSystemCastStartEvent { SourceId = 2, ActionId = (ushort)action })),
                "Complete ordinary shapes/suppression differs.");
            controller.Seek(30.1);
            Check(dangers() == actions.Length, "Observed-end ordinary cast expired.");
            controller.Seek(31.1);
            Check(dangers() == actions.Length - 1, "One Finish removed another cast.");
            controller.Seek(32.1);
            Check(dangers() == 0, "Final Finish retained ordinary danger.");
            controller.Seek(.2);
            Check(dangers() == actions.Length, "Ordinary reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {oid:X} complete ordinary rules independent source clocks real Finish suppression dual views navigation Seek");
        }
        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3027, c, combat: true), Actor(3, 0x233C, c),
                Actor(4, 0x233C, c + Vector3.UnitX * 7), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21706, 5, c, rotation: .3f))], party: party),
            Icon(.2, 4, 168), TreasureNativeEffect(1, 3, 21707, 1, 100, []),
            TreasureNativeEffect(1.2, 3, 21707, 1, 100, []),
            Frame(924, 2, [Actor(2, 0x233C, c)], removed: [4], party: party),
            Frame(924, 20, [], party: party),
            .. Enumerable.Range(0, 5).Select(i => TreasureNativeEffect(21 + i, 3, i % 2 == 0 ? 21706u : 21707u, (uint)i + 2, 100, [])),
            Frame(924, 27, [], party: party),
        ]);
        controller.Seek(.3);
        Check(Models("TreasureRotations").Length == 1 && (ulong)Value(Models("TreasureRotations")[0], "Source") == 4
            && MathF.Abs(((Vector3)Value(Models("TreasureRotations")[0], "Origin")).X - 107) < .05f
            && Find("treasure/rotation/").Length == 2 && dangers() == 2, "Clado pairing actor or two risky previews differ.");
        controller.Seek(1.3);
        Check((int)Value(Models("TreasureRotations").Single(), "Remaining") == 5
            && (DateTime)Value(Models("TreasureRotations").Single(), "NextActivation") == Start.AddSeconds(1).AddSeconds(2.1d),
            "Clado arbitrary source Native effect clock/dedup differs.");
        var future = (DateTime)Value(Aoes("treasure/rotation/").Single(a => ((string)Value(a, "Id")).EndsWith("/1")), "Activation");
        controller.Seek(20.1);
        Check(dangers() == 2 && (DateTime)Value(Aoes("treasure/rotation/").Single(a => ((string)Value(a, "Id")).EndsWith("/1")), "Activation") == future,
            "Clado source destroyed/Finish/clock expired or future drifted.");
        controller.Seek(26);
        Check(!Models("TreasureRotations").Any() && dangers() == 0, "Clado sixth effect failed queue removal.");
        controller.Seek(.3);
        Check(dangers() == 2, "Clado rotation reverse Seek failed.");
        stop();
        Console.WriteLine("PASS native treasure production Clado pairing actor origin six native effects global FIFO dedup consumption2.1 clocks future cache Finish destroy retention Seek");

        foreach (var zero in new[] { true, false })
        {
            load(guid,
            [
                Header(924), Frame(924, 0, [Actor(1, 0x3027, c, combat: true), Actor(4, 0x233C, c), .. players], full: true, party: party),
                Icon(.1, 4, 167),
                .. (zero ? Array.Empty<string>() : new[] { Icon(.15, 4, 999) }),
                Frame(924, .2, [Actor(2, 0x233C, c, cast: Cast(21706, 5, c, rotation: zero ? 0 : .3f, startedAt: .2))], party: party),
                Frame(924, 2, [], party: party),
            ]);
            controller.Seek(.3);
            Check(!Models("TreasureRotations").Any() && dangers() == 0, "Zero facing/unknown icon unexpectedly initialized.");
            if (zero)
            {
                var encounter = Encounter();
                object Correction(float rotation) => Activator.CreateInstance(encounter.GetType().Assembly.GetType(
                    "MasterPlayer.Mechanics.Dungeons.DungeonCastObservation")!,
                    [21706u, c, rotation, Start.AddSeconds(.2), Start.AddSeconds(.2 + 5 + .3), 0ul, null, null, null])!;
                var startCast = encounter.GetType().GetMethod("StartCast")!;
                startCast.Invoke(encounter, [2ul, Correction(.7f), Start.AddSeconds(.31), true]);
                Check(Models("TreasureRotations").Length == 1
                    && (float)Value(Models("TreasureRotations").Single(), "Rotation") == .7f,
                    "Accepted zero-to-valid packet correction lost pending pairing.");
                startCast.Invoke(encounter, [2ul, Correction(.8f), Start.AddSeconds(.32), true]);
                Check(Models("TreasureRotations").Length == 1, "Consumed pairing revived on packet correction.");
            }
            stop();
        }
        Console.WriteLine("PASS native treasure production Clado zero-facing and unknown-icon pairing boundaries no fabricated cone");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x301A, c, combat: true), Actor(20, 0x3082, c, rotation: .7f),
                Actor(21, 0x3051, c), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21695, 5, c))], party: party),
            Frame(924, 1, [Actor(3, 0x233C, c + new Vector3(.9f, 0, .9f), rotation: .74f)], party: party),
            TreasureNativeEffect(1.1, 3, 21781, 1, 100, []),
            Frame(924, 2, [Actor(2, 0x233C, c)], party: party),
            TreasureNativeEffect(3, 3, 21782, 1, 100, []),
            TreasureNativeEffect(4, 3, 21691, 1, 100, []),
            Frame(924, 20, [], removed: [20], party: party),
            Frame(924, 30, [Actor(1, 0x301A, c, dead: true)], party: party), Frame(924, 31, [], party: party),
        ]);
        controller.Seek(.05);
        Check(Models("TreasureRotations").Length == 2 && Models("TreasureRotations").All(s => (int)Value(s, "Remaining") == 31)
            && Find("treasure/rotation/").Length == 18 && Find("treasure/snortsault/").Length == 1 && dangers() == 19
            && (DateTime)Value(Value(Encounter(), "TreasureSnortsaultCircle"), "Activation") == Start.AddSeconds(14.3d),
            "Typhon bootstrap circle/double31x9 differs.");
        controller.Seek(1.2);
        Check(!Find("treasure/rotation/").Any() && !Find("treasure/snortsault/").Any() && dangers() == 1
            && (int)Value(Models("TreasureRotations")[0], "Remaining") == 30
            && (int)Value(Models("TreasureRotations")[1], "Remaining") == 31, "Typhon hidden effect axis/angle matching did not progress exactly one.");
        controller.Seek(3.1);
        Check(Find("treasure/rotation/").Length == 18 && Find("treasure/snortsault/").Length == 1 && dangers() == 19
            && (DateTime)Value(Models("TreasureRotations")[0], "NextActivation") == Start.AddSeconds(1.1).AddSeconds(1.1d),
            "Typhon unhide lost clock or21782 consumed center.");
        controller.Seek(20.1);
        Check(Find("treasure/rotation/").Length == 18 && !Find("treasure/snortsault/").Any() && dangers() == 18,
            "21691 cleared rotation or created source removal cleared queue.");
        controller.Seek(30.1);
        Check(dangers() == 0 && !previews().Any(), "Typhon all-dead/owner cleanup retained outputs.");
        controller.Seek(1.2);
        Check(Models("TreasureRotations").Length == 2 && dangers() == 1, "Typhon reverse Seek failed hidden stage.");
        stop();
        Console.WriteLine("PASS native treasure production Typhon bootstrap two31x9 center14.3 hidden advancement axis-angle match restore clocks21691 circle-only clear owner Seek");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3027, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x3028, c, cast: Cast(21709, 5, c, target: 101))], party: party),
            Frame(924, 1, [Actor(101, 0, c + new Vector3(6, 20, 8), dead: true)], party: party),
            Frame(924, 2, [Actor(2, 0x3028, c, dead: true, cast: Cast(21709, 5, c, target: 101))], party: party),
            Frame(924, 3, [Actor(2, 0x3028, c, cast: Cast(21709, 5, c, target: 101))], party: party),
            TreasureNativeEffect(3.1, 2, 21709, 1, 101, []),
            Frame(924, 4, [Actor(2, 0x3028, c)], party: party), Frame(924, 5, [], party: party),
        ]);
        controller.Seek(1.1);
        Check((float)Value(Value(Aoes("treasure/cast-bait/").Single(a => (bool)Value(a, "Display")), "Shape"), "Length") == 10
            && Find("treasure/cast-bait/").Length == 1 && dangers() == 1 && Models("PredictedDamage").Length == 1,
            "Clado charge width/planar length/dead target differs.");
        controller.Seek(2.1);
        Check(dangers() == 0 && !Find("treasure/cast-bait/").Any(), "Dead Clado source not hidden.");
        controller.Seek(3.2);
        Check(dangers() == 1 && Find("treasure/cast-bait/").Length == 1, "Clado source revival/effect retention failed.");
        controller.Seek(4.1);
        Check(dangers() == 0 && !Models("PredictedDamage").Any(), "Clado true charge Finish retained.");
        stop();
        Console.WriteLine("PASS native treasure production Clado dynamic width5 charge planar current length dead target source revival prediction native-effect retention true Finish");

        foreach (var (oid, action, key) in new[] { (0x3027u, 21708u, "quest/spread/"), (0x301Au, 21688u, "treasure/stack/") })
        {
            load(guid,
            [
                Header(924), Frame(924, 0, [Actor(1, oid, c, combat: true), .. players], full: true, party: party),
                Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(action, 5, c, target: 101)),
                    Actor(3, 0x233C, c, cast: Cast(action, 6, c, target: 101))], party: party),
                Frame(924, 1, [Actor(101, 0, p, dead: true)], party: party),
                Frame(924, 2, [Actor(101, 0, p)], party: party),
                Frame(924, 3, [Actor(3, 0x233C, c)], party: party),
                Frame(924, 4, [Actor(2, 0x233C, c)], party: party), Frame(924, 5, [], party: party),
            ]);
            controller.Seek(.2);
            Check(Find(key).Length == 2 && !Models("PredictedDamage").Any(), "Party mark display/no forecast differs.");
            Check((float)Value(Value(Aoes(key).First(a => (bool)Value(a, "Display")), "Shape"), "Radius")
                == (action == 21708 ? 8 : 6), "Party mark display radius differs.");
            if (action == 21708)
                Check(Aoes(key).Where(a => (bool)Value(a, "Navigate")).All(a =>
                    (float)Value(Value(a, "Shape"), "Radius") == 9), "Spread navigation radius differs.");
            controller.Seek(1.1);
            Check(!Find(key).Any(), "Dead party mark not hidden.");
            controller.Seek(2.1);
            Check(Find(key).Length == 2, "Party target revival failed.");
            controller.Seek(3.1);
            Check(Find(key).Length == 1, "Party target FIFO did not consume exactly first.");
            Check(Aoes(key).Where(a => (bool)Value(a, "Navigate")).All(a =>
                (DateTime)Value(a, "Activation") == Start.AddSeconds(.1 + 6 + .3)),
                "Party FIFO removed later source item instead of first original target.");
            controller.Seek(4.1);
            Check(!Find(key).Any(), "Party mark Finish retained.");
            controller.Seek(.2);
            Check(Find(key).Length == 2, "Party mark reverse Seek failed.");
            stop();
            Console.WriteLine($"PASS native treasure production {action} spread8or exact8stack original targets dead revival FIFO real Finish no forecast Seek");
        }
        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x301A, c, combat: true), .. players], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21690, 5, c))], party: party),
            Frame(924, 1, [Actor(100, 0, p, stackedStatus: 160, statusExpiresAt: Start.AddSeconds(8))], party: party),
            Frame(924, 2, [Actor(2, 0x233C, c)], party: party), Frame(924, 3, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Models("TreasureWallKnockbackSources").Length == 1 && Find("treasure/kb/").Length == 2 && dangers() == 0
            && !Models("PredictedDamage").Any(), "Typhon KB model/display-only/no raidwide differs.");
        Vector3 Landing()
        {
            var encounter = Encounter();
            var actors = (IDictionary)encounter.GetType().GetField("_actors", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(encounter)!;
            var moves = ((IEnumerable)encounter.GetType().GetMethod("TreasureWallMovements",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(encounter, [actors[100ul]])!).Cast<object>().ToArray();
            return (Vector3)moves.Single().GetType().GetField("Item3")!.GetValue(moves.Single())!;
        }
        Check(MathF.Abs(Landing().Z - 119) < .06f && MathF.Abs(Landing().X - 100) < .01f,
            "Typhon KB did not stop at real R19 polygon wall.");
        controller.Seek(1.1);
        Check(!Find("treasure/kb/").Any(), "Typhon strict immunity failed.");
        controller.Seek(2.1);
        Check(!Models("TreasureWallKnockbackSources").Any(), "Typhon KB true Finish retained.");
        stop();
        Console.WriteLine("PASS native treasure production Typhon radial20 wall model strict immunity display-only no extra raidwide true Finish");

        load(guid,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x301A, c, combat: true),
                Actor(100, 0, c - Vector3.UnitZ * 10)], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c - Vector3.UnitZ * 15, cast: Cast(21690, 5, c - Vector3.UnitZ * 15))], party: party),
            Frame(924, 1, [Actor(100, 0, c - Vector3.UnitZ * 10, stackedStatus: 160,
                statusExpiresAt: Start.AddSeconds(.1 + 5 + .3))], party: party),
            Frame(924, 2, [Actor(100, 0, c - Vector3.UnitZ * 10, stackedStatus: 160,
                statusExpiresAt: Start.AddSeconds(.1 + 5 + .3).AddTicks(1))], party: party),
            Frame(924, 3, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Vector3.Distance(Landing(), c + Vector3.UnitZ * 10) < .01f,
            "Unclipped Typhon radial displacement is not exactly20.");
        controller.Seek(1.1);
        Check(Find("treasure/kb/").Length == 2, "Immunity equal to Finish incorrectly suppressed knockback.");
        controller.Seek(2.1);
        Check(!Find("treasure/kb/").Any(), "Immunity one tick after Finish did not suppress knockback.");
        stop();
        Console.WriteLine("PASS native treasure production Typhon full20 displacement and exact equal versus one-tick-late immunity");

        foreach (var (oid, action, key) in new[] { (0x3027u, 21708u, "quest/spread/"), (0x301Au, 21688u, "treasure/stack/") })
        {
            var near = Enumerable.Range(0, 8).Select(i => Actor((ulong)(100 + i), 0,
                c + Vector3.UnitX * (i == 7 ? 10 : i * .3f))).ToArray();
            load(guid,
            [
                Header(924), Frame(924, 0, [Actor(1, oid, c, combat: true), .. near], full: true, party: party),
                Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(action, 5, c, target: 100))], party: party),
                Frame(924, 1, [Actor(107, 0, c + Vector3.UnitX)], party: party),
                Frame(924, 2, [], party: party),
            ]);
            controller.Seek(.2);
            if (action == 21708)
                Check(Aoes(key).Count(a => ((string)Value(a, "Id")).Contains("/separate/")) == 7
                    && Aoes(key).Where(a => (bool)Value(a, "Navigate")).All(a =>
                        (float)Value(Value(a, "Shape"), "Radius") == 9),
                    "Clado self spread did not avoid all seven unmarked buddies at R9.");
            else
            {
                var hints = Encounter().GetType().GetMethod("Hints")!;
                string[] Hints() => ((IEnumerable)hints.Invoke(Encounter(), [c])!).Cast<string>().ToArray();
                Check(Hints().Contains("Stack! (7/8)") && Aoes(key).Any(a => ((string)Value(a, "Id")).EndsWith("/buddy")),
                    "Typhon seven-person self stack did not require exact8/buddy navigation.");
                controller.Seek(1.1);
                Check(Hints().Contains("Stack (8/8)") && !Hints().Contains("Stack! (7/8)"),
                    "Typhon exact eighth member failed satisfied-stack transition.");
            }
            stop();
        }
        Console.WriteLine("PASS native treasure production Clado self spread9 seven buddy navigation and Typhon exact7 to8 self stack transition");
    }
}

using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Text.Json;

internal static partial class NativeDungeonReplayRegression
{
    private static void RunTreasureSequences(object managerObject, object runtimeObject, object controllerObject,
        Action<string, IEnumerable<string>> load, Action stop, Func<object[]> previews, Func<int> dangers)
    {
        dynamic manager = managerObject;
        dynamic runtime = runtimeObject;
        dynamic controller = controllerObject;
        object Value(object value, string name) => value.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(value)!;
        object Script() => Value(((IEnumerable)manager.Running.Values).Cast<object>().Single(), "Script");
        object Encounter() => Script().GetType().BaseType!.GetField("_encounter", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Script())!;
        object[] Find(string key) => previews().Where(p => ((string)Value(p, "Id")).Contains(key)).ToArray();
        object[] Damage() => ((IEnumerable)Value(Encounter(), "PredictedDamage")).Cast<object>().ToArray();
        string[] Hints(Vector3 point) => ((IEnumerable)Encounter().GetType().GetMethod("Hints")!.Invoke(Encounter(), [point])!).Cast<string>().ToArray();
        object Geometry(object preview) => Value(preview, "Geometry");
        bool Contains(object preview, Vector3 point) => (bool)Geometry(preview).GetType().GetMethod("Contains")!.Invoke(Geometry(preview), [point, 0f])!;
        DateTime[] DangerTimes() => ((IEnumerable)runtime.Manager.BuildSnapshot(runtime.CurrentTime).DangerAreas).Cast<object>()
            .SelectMany(a => ((IEnumerable)Value(a, "DangerWindows")).Cast<object>())
            .Select(w => (DateTime)Value(w, "DangerFrom")).Order().ToArray();
        const string script = "masterplayer.treasure.shifting-oubliettes";
        var c = new Vector3(100, 0, 100);
        string Packet(double time, ulong source, uint action, double original, Vector3 location, float rotation = 0, Vector3? origin = null) =>
            JsonSerializer.Serialize(new { t = time, kind = "CastStart", type = "PromeRotation.LogSystem.LogSystemCastStartEvent",
                packetFacing = rotation, data = new { SourceId = source, ActionId = action, CastTime = 4f,
                    EffectPosition = Pose(location), SourcePosition = origin is { } o ? Pose(o) : null, Timestamp = Start.AddSeconds(original) } });
        object[] party = [new { Slot = 0, EntityId = 100ul }, new { Slot = 1, EntityId = 101ul }];
        load(script,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x302D, c, combat: true), Actor(2, 0x233C, c),
                Actor(3, 0x233C, c), Actor(4, 0x233C, c), Actor(100, 0, c), Actor(101, 0, c)], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21724, 4, c)),
                Actor(1, 0x302D, c, combat: true, cast: Cast(21729, 4, c))], party: party),
            Frame(924, 1, [Actor(1, 0x302D, c, combat: true)], removed: [2], party: party),
            Frame(924, 2, [Actor(3, 0x233C, c, cast: Cast(21725, 4, c + new Vector3(.9f, 0, .9f), .049f, startedAt: 2))], party: party),
            Frame(924, 3, [Actor(3, 0x233C, c)], party: party),
            Frame(924, 4, [Actor(4, 0x233C, c, cast: Cast(21726, 4, c, startedAt: 4))], party: party),
            Frame(924, 5, [Actor(4, 0x233C, c)], party: party),
            EffectEvent(7, 1, 21730, 1),
            TreasureNativeEffect(8, 99, 21730, 1, 0, []),
            Frame(924, 9, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/strike/").Length == 1 && dangers() == 1 && Contains(Find("treasure/strike/").Single(), c + Vector3.UnitZ * 4)
            && !Contains(Find("treasure/strike/").Single(), c + Vector3.UnitZ * 9)
            && Damage().Length == 2 && Damage().All(p => Math.Abs(((DateTime)Value(p, "Activation") - Start).TotalSeconds - 5.6) < .00001),
            "Basket initial cone, delayed raidwide or displayed/navigation count differs.");
        controller.Seek(1.1);
        Check(Find("treasure/strike/").Length == 1 && !Contains(Find("treasure/strike/").Single(), c + Vector3.UnitZ * 4)
            && Contains(Find("treasure/strike/").Single(), c + Vector3.UnitZ * 9)
            && DangerTimes().Single() == Start.AddSeconds(1).AddSeconds(1.1d)
            && Damage().Length == 2, "Basket destroy Finish failed second-stage prediction or cancelled earthquake.");
        controller.Seek(3.1);
        Check(Contains(Find("treasure/strike/").Single(), c + Vector3.UnitZ * 15)
            && !Contains(Find("treasure/strike/").Single(), c + Vector3.UnitZ * 9)
            && DangerTimes().Single() == Start.AddSeconds(3).AddSeconds(1.1d),
            "Basket cross-source square/angle matching or third-stage time differs.");
        controller.Seek(7.1);
        Check(Find("treasure/strike/").Length == 0 && dangers() == 0 && Damage().Length == 2 && Hints(c).Contains("Raidwide"),
            "Basket final Finish, persistent delayed raidwide or expanded-event isolation differs.");
        controller.Seek(8.1);
        Check(Damage().Length == 0 && !Hints(c).Contains("Raidwide"), "Basket cross-source native raidwide effect failed.");
        controller.Seek(1.1);
        Check(Find("treasure/strike/").Length == 1 && Damage().Length == 2, "Basket reverse Seek failed state rebuild.");
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Basket stop leaked owner outputs.");
        Console.WriteLine("PASS native treasure production Basket three concentric stages preserve locks and real finish timing");
        Console.WriteLine("PASS native treasure production Basket delayed raidwide persists until native effect and rebuilds on Seek");

        var first = c + new Vector3(.123f, 0, .231f);
        var advance = Vector3.UnitZ * 12;
        load(script,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3014, c, combat: true), Actor(2, 0x30F3, first),
                Actor(3, 0x233C, first), Actor(100, 0, c)], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x30F3, first, cast: Cast(22937, 4, c + Vector3.UnitX * 30))], party: party),
            Frame(924, 1, [], removed: [2], party: party),
            EffectEvent(2, 3, 21672, 1, c + Vector3.UnitX * 30),
            Frame(924, 6, [Actor(3, 0x233C, first + new Vector3(.9f, 0, .9f))], party: party),
            TreasureNativeEffect(6.1, 3, 21672, 1, 0, [], c + Vector3.UnitX * 30),
            TreasureNativeEffect(6.2, 3, 21672, 1, 0, []),
            Frame(924, 10, [Actor(3, 0x233C, first + new Vector3(.9f, 0, .9f) + advance)], party: party),
            TreasureNativeEffect(10.1, 3, 21672, 2, 0, []),
            Frame(924, 14, [Actor(3, 0x233C, first + new Vector3(.9f, 0, .9f) + advance * 2)], party: party),
            TreasureNativeEffect(14.1, 3, 21672, 3, 0, []),
            Frame(924, 18, [Actor(3, 0x233C, first + new Vector3(.9f, 0, .9f) + advance * 3)], party: party),
            TreasureNativeEffect(18.1, 3, 21672, 4, 0, []),
            Frame(924, 20, [], party: party),
        ]);
        controller.Seek(.2);
        Check(Find("treasure/sweep/").Length == 3 && dangers() == 3
            && Find("treasure/sweep/").All(p => ((Vector3)Value(Geometry(p), "Position")).X < 101)
            && Math.Abs((DangerTimes()[0] - Start).TotalSeconds - 5.3) < .00001
            && Hints(c).Contains("GTFO from exaflare!"), "Sweep source lock/three risk circles/.9 extra differs.");
        var initialTimes = DangerTimes();
        controller.Seek(6);
        Check(Find("treasure/sweep/").Length == 3 && DangerTimes().SequenceEqual(initialTimes),
            "Sweep destroy/expanded effect/time changed retained line or future cache.");
        controller.Seek(6.3);
        Check(Find("treasure/sweep/").Length == 3 && DangerTimes()[0] == Start.AddSeconds(6.1).AddSeconds(4.5d)
            && Find("treasure/sweep/").All(p => ((Vector3)Value(Geometry(p), "Position")).Z > 112),
            "Sweep native current caster correction, square tolerance, duplicate packet or next time differs.");
        controller.Seek(10.2);
        Check(Find("treasure/sweep/").Length == 2 && dangers() == 2, "Sweep third window count differs.");
        controller.Seek(14.2);
        Check(Find("treasure/sweep/").Length == 1 && dangers() == 1, "Sweep last window count differs.");
        controller.Seek(18.2);
        Check(Find("treasure/sweep/").Length == 0 && dangers() == 0, "Fourth matching effect did not consume line.");
        controller.Seek(6.3);
        Check(Find("treasure/sweep/").Length == 3 && dangers() == 3, "Sweep reverse Seek failed native state rebuild.");
        stop();
        Check(dangers() == 0 && previews().Length == 0, "Sweep owner stop retained projections.");
        Console.WriteLine("PASS native treasure production Porxie four native explosions three shown square matching and processing clock");
        Console.WriteLine("PASS native treasure production Porxie cancelled destroyed source retained line and cached future timestamps");

        load(script,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3014, c, combat: true), Actor(2, 0x30F3, c),
                Actor(3, 0x30F2, c), Actor(100, 0, c)], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x30F3, c, cast: Cast(22937, 4, c)), Actor(3, 0x30F2, c, cast: Cast(22937, 4, c))], party: party),
            TreasureNativeEffect(1, 1, 21672, 1, 0, []),
            Frame(924, 2, [], party: party),
        ]);
        controller.Seek(1.1);
        Check(Find("treasure/sweep/").Length == 6 && dangers() == 6
            && Find("treasure/sweep/").Count(p => ((Vector3)Value(Geometry(p), "Position")).Z < 101) == 1,
            "Overlapping Sweep lines were collapsed or one native event advanced both.");
        stop();
        Console.WriteLine("PASS native treasure production Porxie overlapping lines preserve insertion order and independent windows");

        load(script,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x3014, c, combat: true), Actor(2, 0x30F3, c), Actor(100, 0, c)], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x30F3, c, cast: Cast(22937, 4, c))], party: party),
            Packet(.2, 2, 22937, .1, c, origin: c + Vector3.UnitX * 5),
            Frame(924, .3, [Actor(2, 0x30F3, c + Vector3.UnitX * 15, cast: Cast(22937, 4, c))], party: party),
            Packet(.4, 2, 22937, .1, c, .1f),
            Frame(924, 1, [], party: party),
        ]);
        controller.Seek(.25);
        Check(Find("treasure/sweep/").Length == 3 && Find("treasure/sweep/").All(p =>
            ((Vector3)Value(Geometry(p), "Position")).X is > 104 and < 106), "SourcePosition-only packet correction was lost.");
        controller.Seek(.5);
        Check(Find("treasure/sweep/").Length == 3 && Find("treasure/sweep/").Any(p =>
            ((Vector3)Value(Geometry(p), "Position")).X is > 104 and < 106)
            && Find("treasure/sweep/").All(p => ((Vector3)Value(Geometry(p), "Position")).X < 110),
            "Missing packet source position replaced locked origin with moving actor.");
        stop();
        Console.WriteLine("PASS native treasure production Porxie packet origin correction and missing-origin lock retention");

        load(script,
        [
            Header(924), Frame(924, 0, [Actor(1, 0x302D, c, combat: true), Actor(2, 0x233C, c),
                Actor(3, 0x233C, c), Actor(100, 0, c)], full: true, party: party),
            Frame(924, .1, [Actor(2, 0x233C, c, cast: Cast(21729, 4, c)),
                Actor(1, 0x302D, c, combat: true, cast: Cast(21724, 4, c))], party: party),
            Frame(924, 1, [Actor(3, 0x233C, c, cast: Cast(21729, 4, c, startedAt: 1))], party: party),
            Packet(2, 2, 21729, .1, c + Vector3.UnitX),
            TreasureNativeEffect(3, 99, 21730, 1, 0, []),
            Packet(4, 3, 21729, 1, c + Vector3.UnitX),
            JsonSerializer.Serialize(new { t = 10d, kind = "CancelCast", type = "PromeRotation.LogSystem.LogSystemCancelCastEvent",
                data = new { SourceId = 1ul, ActionId = 21724u, Timestamp = Start.AddSeconds(4) } }),
            Frame(924, 11, [], party: party),
        ]);
        controller.Seek(2.1);
        Check(Damage().Length == 1 && Math.Abs(((DateTime)Value(Damage().Single(), "Activation") - Start).TotalSeconds - 6.5) < .00001,
            "Old source packet correction reclaimed earthquake activation.");
        controller.Seek(4.1);
        Check(Damage().Length == 0, "Old same-generation correction resurrected consumed earthquake.");
        controller.Seek(10.1);
        Check(DangerTimes().Single() == Start.AddSeconds(10).AddSeconds(1.1d),
            "Delayed cancel used old packet timestamp for next concentric activation.");
        stop();
        Console.WriteLine("PASS native treasure production Basket latest earthquake generation and queued cancel processing clock");

        foreach (var oid in new uint[] { 0x302D, 0x3014 })
        {
            load(script,
            [
                Header(924), Frame(924, 0, [Actor(1, oid, c, combat: true), Actor(2, 0x3034, c), Actor(100, 0, c)], full: true, party: party),
                Frame(924, 1, [Actor(1, oid, c, dead: true)], party: party),
                Frame(924, 2, [Actor(2, 0x3034, c, dead: true), Actor(3, 0x3033, c), Actor(4, 0x30F3, c)], party: party),
                Frame(924, 3, [], party: party),
            ]);
            controller.Seek(1.1);
            Check(Value(Encounter(), "Active") != null, "Reward did not retain phase.");
            controller.Seek(2.1);
            Check((Encounter().GetType().GetProperty("Active")!.GetValue(Encounter()) != null) == (oid == 0x302D),
                "Basket/Porxie excluded helper/reward lifecycle differs.");
            stop();
        }
        Console.WriteLine("PASS native treasure production Basket and Porxie reward All groups preserve distinct completion boundaries");
    }
}

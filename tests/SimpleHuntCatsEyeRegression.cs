using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntCatsEyeRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayCast Cast(uint action, ulong target = 101, double at = 0)
        => new(action, target, new(0, 0, 50), 0, Start.AddSeconds(at), Start.AddSeconds(at + 7), false, false);
    private static ReplayActorState Boss(ReplayCast? cast = null) => new()
    {
        Id = 1, Oid = 0x4396, Hp = 1000, Targetable = true, InCombat = true, Cast = cast
    };
    private static ReplayActorState Player(ulong id, Vector3 position) => new() { Id = id, Position = position };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts CatsEye cast eyes use source range lock and independent damage contract", CastEyes);
        run("SimpleHunts CatsEye independent unlimited R8 components and raw first gaze priority", Components);
        run("SimpleHunts CatsEye dead revived detached target gaze and exact unquantized moving origin", Targets);
        run("SimpleHunts CatsEye missing corrected target cannot block fallback and raw first dead identity remains distinct", RawLists);
    }

    private static void CastEyes()
    {
        var definition = SimpleHuntCatalog.Find(0x4396)!;
        Check(definition.Casts.Length == 5, "CatsEye activated cast set differs.");
        var player = Player(100, Vector3.Zero);
        foreach (var action in new uint[] { 38510, 38511 })
        {
            var e = new SimpleHuntEncounter();
            var cast = Cast(action);
            e.Synchronize([Boss(cast), player], _ => true, Start);
            var eye = e.Gazes(player).Single();
            Check(eye.Input.Location == cast.Location && eye.Rule.GazeRange == 10000
                && eye.Rule.InvertedGaze == (action == 38511) && !e.Areas.Any(a => a.Shape != null),
                "CatsEye invented a range-40 AOE or lost its locked eye.");
            Check(e.Facing(player, Start.AddSeconds(6.24)) == null
                && e.Facing(player, Start.AddSeconds(6.25)) is { } facing
                && SimpleHuntEncounter.HitByGaze(eye, player with { Rotation = facing }) == eye.Rule.InvertedGaze,
                "CatsEye forbidden facing did not reach the shared PR timing contract.");
            Check(HuntDrawing.Collect(e, player).Count(d => d.Id.EndsWith("/eye")) == 1,
                "CatsEye eye primitive missing.");
            e.Finish(1, 38514, Start.AddSeconds(1));
            Check(e.Gazes(player).Count() == 1, "Unwatched finish consumed cast gaze.");
            e.Synchronize([Boss(cast) with { Position = new(40, 0, 40) }, player], _ => true, Start.AddSeconds(30));
            Check(e.Gazes(player).Single().Input.Location == cast.Location, "Time or boss movement relocated a locked eye.");
            e.Finish(1, action, Start.AddSeconds(31));
            Check(!e.Gazes(player).Any(), "Actual cast finish retained eye.");
        }
        var damage = new SimpleHuntEncounter();
        damage.Synchronize([Boss(Cast(39887)), player], _ => true, Start);
        Check(damage.PredictedDamage([new(0, "P", 100)]).Single().DamageType == "Raidwide", "CatsEye raidwide prediction missing.");
    }

    private static void Components()
    {
        var e = new SimpleHuntEncounter();
        var player = Player(100, new(100, 0, 100));
        var normal = Player(101, new(0, 0, 10));
        var inverted = Player(102, new(40, 0, 10));
        var party = Enumerable.Range(0, 10).Select(i => new ReplayPartyMember(i, $"P{i}", (ulong)(100 + i))).ToArray();
        var allies = Enumerable.Range(3, 7).Select(i => Player((ulong)(100 + i), normal.Position)).ToArray();
        e.Synchronize([Boss(Cast(38515)), player, normal, inverted, .. allies], _ => true, Start, party);
        e.Finish(1, 38515, Start.AddSeconds(1), 999);
        e.Start(1, Cast(39668, 102, 2), true);
        var previews = e.TargetPreviews(player).ToArray();
        Check(previews.Length == 2 && previews.All(a => a.Shape is CircleTemplate { Radius: 8 } && a.Safe && a.OutlineOnly)
            && !e.PredictedDamage(party).Any(), "CatsEye stack inherited a capacity limit, damage or filled circle.");
        var navigation = e.TargetDangers(player, party).ToArray();
        Check(navigation.Length == 2 && navigation.All(a => a.Shape is HuntStackJoinTemplate { Radius: 8 }),
            "Independent normal and inverted components merged their navigation.");
        Check(e.TargetDangers(normal, party).Any(a => a.Shape is HuntStackJoinTemplate { Radius: 4 }),
            "CatsEye marked target did not receive R4 buddy navigation.");
        Check(e.Gazes(player).Single().Rule.Action == 38515
            && e.Gazes(normal).Single().Rule.Action == 39668
            && e.Gazes(inverted).Single().Rule.Action == 38515, "Bloodshot raw-first component priority or self exemption differs.");
        e.Finish(1, 39668, Start.AddSeconds(3), 999);
        e.Start(1, Cast(38510, at: 4), true);
        Check(e.Gazes(player).Count() == 2
            && HuntDrawing.Collect(e, player).Where(d => d.Id.EndsWith("/eye")).Select(d => d.Id).Distinct().Count() == 2,
            "A cast eye collided with the retained Bloodshot eye.");
        e.Finish(1, 38515, Start.AddSeconds(5), 101);
        Check(e.Gazes(player).Any(a => a.Rule.Action == 39668), "Normal finish did not promote inverted component.");
        e.Finish(1, 39668, Start.AddSeconds(6), 102);
        Check(e.Gazes(player).Count() == 1, "Target matching finish retained a stack gaze.");
    }

    private static void Targets()
    {
        var e = new SimpleHuntEncounter();
        var cast = Cast(38515);
        var player = Player(100, Vector3.Zero);
        var target = Player(101, new(3.1234f, 0, 8.5678f));
        var party = new ReplayPartyMember[] { new(0, "P", 100), new(1, "T", 101) };
        e.Synchronize([Boss(cast), player], _ => true, Start, party);
        e.Synchronize([Boss(cast), player, target], _ => true, Start.AddSeconds(1), party);
        Check(!e.Gazes(player).Any() && !e.TargetPreviews(player).Any(), "Missing initial target rebound later.");
        e.Reset();
        e.Synchronize([Boss(cast), player, target], _ => true, Start, party);
        var activation = e.Gazes(player).Single().Activation;
        var moved = target with { Position = new(4.4321f, 0, 9.8765f), Dead = true };
        e.Synchronize([Boss(cast), player, moved], _ => true, Start.AddSeconds(1), party);
        Check(!e.TargetPreviews(player).Any() && e.Gazes(player).Single().Input.Location == moved.Position
            && e.Gazes(player).Single().Activation == activation && !e.Gazes(moved).Any(),
            "Dead target incorrectly disappeared from the raw gaze list or moving eye quantized/delayed.");
        e.Synchronize([Boss(cast), player, moved with { Dead = false }], _ => true, Start.AddSeconds(2), party);
        Check(e.TargetPreviews(player).Count() == 1, "Revived stack did not restore.");
        e.Remove(101);
        e.Synchronize([Boss(cast), player, target with { Position = new(90, 0, 90) }], _ => true, Start.AddSeconds(3), party);
        Check(e.Gazes(player).Single().Input.Location == moved.Position && e.Gazes(target).Count() == 1,
            "Same-ID recreation hijacked the old reference or inherited its eye exemption.");
        e.Finish(1, 38515, Start.AddSeconds(4), 101);
        Check(!e.Gazes(player).Any() && !e.TargetPreviews(player).Any(), "Frozen target ID finish failed.");
    }

    private static void RawLists()
    {
        var e = new SimpleHuntEncounter();
        var player = Player(100, default);
        var first = Player(101, new(0, 0, 10));
        var second = Player(102, new(20, 0, 10));
        var inverse = Player(103, new(40, 0, 10));
        var party = new ReplayPartyMember[] { new(0, "P", 100), new(1, "A", 101), new(2, "B", 102), new(3, "I", 103) };
        e.Synchronize([Boss(Cast(39668, 103)), player, first, second, inverse], _ => true, Start, party);
        e.Finish(1, 39668, Start.AddSeconds(1), 999);
        var normal = Cast(38515, 101, 2);
        e.Start(1, normal, true);
        e.Synchronize([Boss(normal), player, first, second, inverse], _ => true, Start.AddSeconds(2.1), party);
        var missing = normal with { TargetId = 999 };
        e.Start(1, missing, true);
        e.Synchronize([Boss(missing), player, first, second, inverse], _ => true, Start.AddSeconds(3), party);
        Check(e.Gazes(player).Single().Rule.Action == 39668, "Null corrected normal entry blocked inverted fallback.");
        var replacement = missing with { TargetId = 102 };
        e.Synchronize([Boss(replacement), player, first, second, inverse], _ => true, Start.AddSeconds(4), party);
        Check(e.Gazes(player).Single().TargetId == 102, "Null entry blocked a real subsequent start.");
        e.Finish(1, 38515, Start.AddSeconds(5), 999);
        e.Start(1, Cast(38515, 101, 6), true);
        Check(e.Gazes(second).Single().Rule.Action == 39668,
            "Self first entry incorrectly skipped to the second normal stack instead of inverted fallback.");
        e.Synchronize([Boss(Cast(38515, 101, 6)), player, first, second with { Dead = true }, inverse],
            _ => true, Start.AddSeconds(6.1), party);
        Check(e.Gazes(player).Single().TargetId == 102
            && !e.TargetPreviews(second with { Dead = true }).Single(a => a.TargetId == 101).Safe,
            "Dead raw first entry lost gaze priority or another same-component circle became safe.");
        e.Finish(1, 38515, Start.AddSeconds(7), 102);
        Check(e.Gazes(player).Single().TargetId == 101, "Finishing raw first entry failed to expose the second.");
        Check(HuntDrawing.Collect(e, player).Where(p => p.Id.EndsWith("/pupil")).All(p => p.Color == Vector4.One),
            "Gaze pupils differ from source default border color.");
    }
}

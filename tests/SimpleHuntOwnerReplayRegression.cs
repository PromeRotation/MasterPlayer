using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;

internal static class SimpleHuntOwnerReplayRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100)];
    private static ReplayActorState Boss(uint oid, ulong id = 1) => new()
    {
        Id = id, Oid = oid, Kind = 2, Hp = 1000, InCombat = true, Targetable = true,
        Statuses = oid == 0x416E ? [new(3979, 3, id, Start.AddSeconds(60))] : []
    };
    private static ReplayActorState Player() => new() { Id = 100, Hp = 1000 };
    private static ReplayCast Cast(uint action, double at = 0, Vector3 position = default)
        => new(action, 100, position, .2f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static ReplayActorState Helper(ReplayCast? cast = null, ulong id = 999)
        => new() { Id = id, Oid = 0x233C, Kind = 2, Cast = cast };
    private static int Outputs(SimpleHuntEncounter e, ulong owner) => e.Areas.Count(a => a.Shape != null && a.Source == owner)
        + e.TargetPreviews(Player()).Count(a => a.Source == owner);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts component replay first raw confirmation same frame and delayed new owner no duplicate", FirstConfirmation);
        run("SimpleHunts component replay current actor order differs from historical cast insertion order", ActorOrder);
        run("SimpleHunts component replay no finished storage spin or packet stack inherited by new owner", EndedInputs);
        run("SimpleHunts component replay changed raw generation delivers only current input to new owner", ChangedGeneration);
        run("SimpleHunts component replay raw pose target and clock baseline remains correct through later packet and finish", RawBaseline);
    }

    private static void FirstConfirmation()
    {
        (uint Oid, uint Action, int Count)[] cases =
            [(0x416D, 39013, 1), (0x416E, 39099, 2), (0x4582, 39773, 1), (0x4233, 37377, 3), (0x452C, 39493, 2)];
        foreach (var (oid, action, count) in cases)
        foreach (var delayedRaw in new[] { false, true })
        {
            var e = new SimpleHuntEncounter();
            e.Synchronize([Boss(oid), Helper(), Player()], _ => true, Start, Party);
            var cast = Cast(action);
            e.Start(999, cast, true);
            e.UpdateComponents(Start);
            Check(Outputs(e, 1) == count, $"Initial packet failed {oid:X}/{action}.");
            if (delayedRaw)
            {
                e.Synchronize([Boss(oid), Boss(oid, 2), Helper(), Player()], _ => true, Start.AddSeconds(.1), Party);
                Check(Outputs(e, 2) == 0, "New owner inherited unobserved packet without a current raw cast.");
            }
            var observations = new[] { Boss(oid), Boss(oid, 2), Helper(cast), Player() };
            e.Synchronize(observations, _ => true, Start.AddSeconds(.2), Party);
            Check(Outputs(e, 1) == count && Outputs(e, 2) == count,
                $"First raw did not fill only the missing owner {oid:X}/{action}.");
            e.Synchronize(observations, _ => true, Start.AddSeconds(.3), Party);
            Check(Outputs(e, 1) == count && Outputs(e, 2) == count, "Stable raw duplicated a received component Start.");
            if (oid is 0x4233 or 0x452C)
            {
                var effect = oid == 0x4233 ? 37370u : 39785u;
                var hits = oid == 0x4233 ? 3 : 4;
                for (var i = 0; i < hits; ++i) e.Effect(999, effect, Start.AddSeconds(1 + i), (uint)(i + 1));
                e.Synchronize(observations, _ => true, Start.AddSeconds(5), Party);
                Check(Outputs(e, 1) == 0 && Outputs(e, 2) == 0, "Hidden duplicate queue survived exact effect count or stable raw rebuilt consumed history.");
            }
        }
    }

    private static void ActorOrder()
    {
        foreach (var (oid, action, marker) in new[] { (0x4233u, 37377u, "/sap/"), (0x452Cu, 39493u, "/nechuciho/") })
        {
            var e = new SimpleHuntEncounter();
            var first = Cast(action, 1, new(10, 0, 0));
            var second = Cast(action, 0, new(20, 0, 0));
            e.Synchronize([Boss(oid), Helper(id: 998), Helper(), Player()], _ => true, Start, Party);
            e.Start(999, second, false);
            e.Start(998, first, false);
            e.Synchronize([Boss(oid), Helper(first, 998), Helper(second), Boss(oid, 2), Player()], _ => true, Start.AddSeconds(2), Party);
            var oldOwner = e.Areas.First(a => a.Source == 1 && a.SequenceId?.Contains(marker) == true);
            var newOwner = e.Areas.First(a => a.Source == 2 && a.SequenceId?.Contains(marker) == true);
            Check(oldOwner.Input.StartedAt == second.StartedAt && newOwner.Input.StartedAt == first.StartedAt,
                "New owner replay used historical cast dictionary order instead of current actor order.");
        }
    }

    private static void EndedInputs()
    {
        foreach (var (oid, action) in new[] { (0x4582u, 39778u), (0x416Eu, 39099u) })
        {
            var e = new SimpleHuntEncounter();
            e.Synchronize([Boss(oid), Helper(Cast(action)), Player()], _ => true, Start, Party);
            e.Synchronize([Boss(oid), Boss(oid, 2), Helper(), Player()], _ => true, Start.AddSeconds(1), Party);
            Check(Outputs(e, 2) == 0, "New owner inherited a finished cast's sequence.");
            var next = oid == 0x4582 ? 39773u : 39099u;
            e.Start(999, Cast(next, 2), false);
            e.UpdateComponents(Start.AddSeconds(2));
            Check(Outputs(e, 2) == (oid == 0x4582 ? 1 : 2),
                "Finished storage/spin input polluted a new owner's following Smog/Shock.");
        }
        var atticus = new SimpleHuntEncounter();
        atticus.Synchronize([Boss(0x416D), Helper(), Player()], _ => true, Start, Party);
        atticus.Start(999, Cast(39013), true);
        atticus.Synchronize([Boss(0x416D), Boss(0x416D, 2), Helper(), Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Outputs(atticus, 1) == 1 && Outputs(atticus, 2) == 0, "New Atticus owner inherited an old packet-only stack.");
    }

    private static void ChangedGeneration()
    {
        foreach (var (oid, action, count) in new[] { (0x4233u, 37377u, 3), (0x452Cu, 39493u, 2) })
        {
            var e = new SimpleHuntEncounter();
            var original = Cast(action);
            var changed = original with { Location = new(30, 0, 40), Rotation = 1 };
            e.Synchronize([Boss(oid), Helper(original), Player()], _ => true, Start, Party);
            e.Synchronize([Boss(oid), Boss(oid, 2), Helper(changed), Player()], _ => true, Start.AddSeconds(1), Party);
            var owner = e.Areas.Where(a => a.Source == 2 && a.Shape != null).ToArray();
            Check(owner.Length == count && owner.All(a => a.Input == changed),
                "New owner received an obsolete raw generation before current pose change.");
        }
    }

    private static void RawBaseline()
    {
        foreach (var (oid, action, offset) in new[] { (0x4233u, 37377u, MathF.PI), (0x452Cu, 39493u, 0f) })
        {
            var e = new SimpleHuntEncounter();
            e.Synchronize([Boss(oid), Helper(), Player()], _ => true, Start, Party);
            var packet = Cast(action, position: new(10, 0, 20));
            var raw = packet with { Location = new(30, 0, 40), Rotation = 1, FinishAt = Start.AddSeconds(7) };
            e.Start(999, packet, true);
            e.Synchronize([Boss(oid), Boss(oid, 2), Helper(raw), Player()], _ => true, Start.AddSeconds(1), Party);
            var oldHead = e.Areas.First(a => a.Shape != null && a.Source == 1);
            var newHead = e.Areas.First(a => a.Shape != null && a.Source == 2);
            Check(oldHead.Input == packet && newHead.Input == raw && newHead.Rotation == 1 + offset,
                "New owner inherited packet pose/clock instead of current raw, or old owner lost its accepted input.");
            var corrected = packet with { Rotation = 1.6f, Location = new(50, 0, 60), FinishAt = Start.AddSeconds(9) };
            e.Start(999, corrected, true);
            var heads = e.Areas.Where(a => a.Shape != null && a.Stage == 0).ToArray();
            Check(heads.Length == 2 && heads.All(a => MathF.Abs(a.Rotation - 1.6f - offset) < .000001f && a.Input == corrected),
                "Later packet correction used another owner's baseline.");
        }
        foreach (var (oid, action) in new[] { (0x416Du, 39013u), (0x4233u, 37307u) })
        {
            var e = new SimpleHuntEncounter();
            var p1 = new ReplayActorState { Id = 101, Hp = 1000, Position = new(20, 0, 0) };
            ReplayPartyMember[] party = [.. Party, new(1, "P1", 101)];
            e.Synchronize([Boss(oid), Helper(), Player(), p1], _ => true, Start, party);
            var packet = Cast(action);
            var raw = packet with { TargetId = 101 };
            e.Start(999, packet, true);
            e.Synchronize([Boss(oid), Boss(oid, 2), Helper(raw), Player(), p1], _ => true, Start.AddSeconds(1), party);
            Check(e.TargetPreviews(Player()).Count() == 2, "Different raw stack target was not delivered to new owner.");
            e.Finish(999, action, Start.AddSeconds(2));
            Check(!e.TargetPreviews(Player()).Any(), "Observed Finish failed to clear each owner's delivered target.");
        }
        var shock = new SimpleHuntEncounter();
        shock.Synchronize([Boss(0x416E), Helper(), Player()], _ => true, Start, Party);
        var initial = Cast(39099);
        shock.Start(999, initial, true);
        shock.Synchronize([Boss(0x416E), Boss(0x416E, 2), Helper(initial with { Rotation = 1 }), Player()],
            _ => true, Start.AddSeconds(1), Party);
        shock.Finish(999, 39099, Start.AddSeconds(2));
        shock.UpdateComponents(Start.AddSeconds(2));
        var currentShocks = shock.Areas.Where(a => a.SequenceId?.EndsWith("/current") == true).ToArray();
        Check(currentShocks.Length == 2 && currentShocks.All(a => a.Stage == 1),
            "Heshuala Finish matched the shared packet rotation instead of each owner's received pose.");
    }
}

using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntAglaopeRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "T", 101)];
    private static ReplayActorState Boss(ReplayCast? cast = null, ulong id = 1) =>
        new() { Id = id, Oid = 0x281E, Kind = 2, Hp = 1000, Targetable = true, InCombat = true, Cast = cast };
    private static ReplayActorState Player(ulong id = 100, float x = 10, params ReplayStatus[] statuses) =>
        new() { Id = id, Kind = 1, Hp = 1000, Position = new(x, 0, 0), Statuses = statuses };
    private static ReplayActorState Helper(ReplayCast? cast = null) =>
        new() { Id = 999, Oid = 0x233C, Kind = 2, Hp = 1000, Cast = cast };
    private static ReplayCast Cast(uint action, double start = 0, Vector3 location = default) =>
        new(action, 100, location, .7f, Start.AddSeconds(start), Start.AddSeconds(start + 3.3), false, false) { TotalTime = 3 };
    private static ulong Status(uint id, byte flags = 0, byte extra = 0x11, byte type = 14) =>
        type | ((ulong)extra << 24) | ((ulong)flags << 40) | ((ulong)id << 48);
    private static ulong[][] Raw(params ulong[] effects) => [.. effects.Select(e => new ulong[] { 0, 0, e, 0, 0, 0, 0, 0 })];
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Player(), Player(101, 20)], _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast Sonata(SimpleHuntEncounter e) => e.Areas.Single(a => a.SequenceId?.Contains("/aglaope/sonata") == true);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Aglaope seven exact rules eight components no invented gaze interrupt action or dangerous raidwide", Rules);
        run("SimpleHunts Aglaope Sonata finish cancellation and timeout retain effect-ended single slot", SonataLifecycle);
        run("SimpleHunts Aglaope complete native payload pending before done delayed ACK and strict expiry", Pending);
        run("SimpleHunts Aglaope pending flags original target indexes all effect slots and first status ACK", Payload);
        run("SimpleHunts Aglaope actual status confirms matching source first copy only and no pending synthesis", Confirmation);
        run("SimpleHunts Aglaope source slot zero independent of perspective done survives new Start", PlayerSlot);
        run("SimpleHunts Aglaope ten radial knockback range immunity true finish and no navigation", Knockback);
        run("SimpleHunts Aglaope raw bootstrap correction helper destruction and mechanism owner cleanup", Owners);
        run("SimpleHunts Aglaope first raw authority saturated NPC clock and unchanged raw never renews", FirstRaw);
        run("SimpleHunts Aglaope explicit helper removal unclaimed cast replacement and pending actor generations", Removal);
    }

    private static void Rules()
    {
        var d = SimpleHuntCatalog.Find(0x281E)!;
        Check(d.Casts.Length == 7 && d.Casts.All(c => c.Action != 870 && c.GazeRange == 0), "Unused autoattack or invented gaze.");
        Check(d.Casts.Single(c => c.Action == 16819).Shape == new DonutTemplate(5, 50)
            && d.Casts.Single(c => c.Action == 16823).Shape == RayRectangleTemplate.Symmetric(6, 42.4f), "Source dimensions differ.");
        foreach (var action in new uint[] { 16825, 18056 })
        {
            var e = Create();
            e.Start(1, Cast(action), true);
            Check(!e.Areas.Any(c => c.Shape != null), "Hint/raidwide became a dangerous circle.");
            Check(e.PredictedDamage(Party).Count() == (action == 18056 ? 2 : 0), "Interrupt hint invented prediction.");
            Check(e.Hints(Player()).Contains(action == 16825 ? "Interrupt! (Raidwide + Bleed)" : "Raidwide"), "Source text missing.");
            Check(e.Facing(Player(), Start.AddSeconds(3)) == null && !e.StopEverything(Player(), Party, Start), "Invented control.");
        }
    }

    private static void SonataLifecycle()
    {
        var e = Create();
        e.Start(999, Cast(16824, location: new(12, 0, 17)), true);
        var first = Sonata(e);
        Check(first.Shape == new CircleTemplate(16.2f) && first.Location == new Vector3(12, 0, 17)
            && first.Rotation == 0, "Sonata was replaced by actual R40 or dynamic caster.");
        e.Finish(999, 16824, Start.AddSeconds(1));
        e.UpdateComponents(Start.AddSeconds(100));
        Check(Sonata(e) == first, "Cancel or timeout cleared source event-ended circle.");
        e.AglaopeEffect(999, 17074, 1, [100], Raw(Status(991)), Start.AddSeconds(100));
        e.UpdateComponents(Start.AddSeconds(100));
        Check(Sonata(e) == first, "Other action prematurely marked Sonata done.");
        e.AglaopeEffect(999, 16824, 2, [], [], Start.AddSeconds(100));
        e.UpdateComponents(Start.AddSeconds(100));
        Check(Sonata(e) == first, "Existing pending from a different action was ignored.");
        e.UpdateComponents(Start.AddSeconds(103).AddTicks(1));
        Check(!e.Areas.Any(c => c.Shape != null), "Expired pending did not release done circle.");
    }

    private static void Pending()
    {
        var e = Create();
        e.Start(1, Cast(16824), true);
        e.AglaopeEffect(1, 16824, 7, [101, 100], Raw(0, Status(991)), Start);
        e.UpdateComponents(Start);
        Check(Sonata(e).Shape != null, "done consumed before pending creation.");
        e.Synchronize([Boss(), Helper(), Player(), Player(101, 20)], _ => true, Start.AddSeconds(2.2), Party);
        e.UpdateComponents(Start.AddSeconds(2.2));
        Check(Sonata(e).Shape != null, "Invented 2.1-second delay cutoff.");
        var ack = Player() with { IncomingEffects = [new(3, 7, 1, true, false)] };
        e.Synchronize([Boss(), Helper(), ack, Player(101, 20)], _ => true, Start.AddSeconds(2.3), Party);
        e.UpdateComponents(Start.AddSeconds(2.3));
        Check(!e.Areas.Any(c => c.Shape != null), "Matching real ACK failed release.");
        e = Create();
        e.Start(1, Cast(16824), true);
        e.AglaopeEffect(1, 16824, 0, [100], Raw(Status(991)), Start);
        e.UpdateComponents(Start.AddSeconds(3));
        Check(Sonata(e).Shape != null, "Expiration equality was removed.");
        e.UpdateComponents(Start.AddSeconds(3).AddTicks(1));
        Check(!e.Areas.Any(c => c.Shape != null), "Strictly expired pending retained circle.");
    }

    private static void Payload()
    {
        var input = new HuntAglaopePendingStatuses();
        var actors = new[] { Boss(), Player(), Player(101, 20) };
        var map = actors.ToDictionary(a => a.Id);
        input.Observe(actors, Start);
        ulong[][] raw = [new ulong[8], new ulong[8]];
        raw[1][0] = Status(123);
        raw[1][7] = Status(991);
        input.Effect(1, 8, [101, 100], raw, map, Start);
        Check(input.Pending.Count == 2 && input.Pending.All(p => p.TargetIndex == 1), "Effects after empty slot or original index lost.");
        var ack = Player() with { IncomingEffects = [new(0, 8, 1, true, false)] };
        input.Observe([Boss(), ack, Player(101)], Start);
        Check(input.Pending.Count == 1 && input.HasSeduced(100), "ACK removed all or only991 instead of first any status.");
        input.Observe([Boss(), ack, Player(101)], Start);
        Check(input.Pending.Count == 1, "Repeated confirmed snapshot acknowledged twice.");
        input.Observe([Boss(), Player(), Player(101)], Start);
        input.Observe([Boss(), ack, Player(101)], Start);
        Check(input.Pending.Count == 0, "Rearmed ACK did not remove next copy.");
        input.Effect(1, 9, [100], Raw(Status(991, 0xA0, type: 15)), map, Start);
        Check(input.Pending.Single() is { Actor: 1, Source: 100, TargetIndex: 0, ExtraLo: 0x11 }, "AtSource/FromTarget routing differs.");
        input.Effect(1, 9, [404], Raw(Status(991, 0x80)), map, Start);
        Check(input.Pending.Count == 1, "Unresolved target incorrectly wrote AtSource pending.");
    }

    private static void Confirmation()
    {
        var input = new HuntAglaopePendingStatuses();
        var actors = new[] { Boss(), Player(), Player(101) };
        input.Observe(actors, Start);
        var map = actors.ToDictionary(a => a.Id);
        input.Effect(1, 1, [100], Raw(Status(991)), map, Start);
        input.Effect(1, 1, [100], Raw(Status(991)), map, Start);
        input.Observe([Boss(), Player(statuses: [new(991, 0x11, 101, Start.AddSeconds(6))]), Player(101)], Start);
        Check(input.Pending.Count == 2, "Wrong-source actual status confirmed pending.");
        var actual = Player(statuses: [new(991, 0x11, 1, Start.AddSeconds(6))]);
        input.Observe([Boss(), actual, Player(101)], Start);
        Check(input.Pending.Count == 1, "Actual status did not remove exactly one source match.");
        input.Observe([Boss(), actual, Player(101)], Start);
        Check(input.Pending.Count == 1, "Unchanged actual status repeatedly acknowledged.");
        input.Clear();
        input.Observe([Boss(), actual, Player(101)], Start);
        Check(!input.HasSeduced(100), "Actual status synthesized a pending991.");
    }

    private static void PlayerSlot()
    {
        var e = Create();
        e.Start(1, Cast(16824), true);
        e.AglaopeEffect(1, 16824, 8, [101], Raw(Status(991)), Start);
        e.UpdateComponents(Start);
        Check(!e.Areas.Any(c => c.Shape != null), "Sonata queried arbitrary viewed player rather than Raid0.");
        e = Create();
        e.Start(1, Cast(16824), true);
        e.AglaopeEffect(1, 16824, 9, [100], Raw(Status(991)), Start);
        e.UpdateComponents(Start);
        e.Start(999, Cast(16824, 1, new(25, 0, 0)), true);
        e.Synchronize([Boss(), Helper(Cast(16824, 1, new(25, 0, 0))), Player(), Player(101)], _ => true,
            Start.AddSeconds(1), [new(0, "P", 101), new(1, "T", 100)]);
        e.UpdateComponents(Start.AddSeconds(1));
        Check(!e.Areas.Any(c => c.Shape != null), "New Start reset done or pending query followed old player identity.");
    }

    private static void Knockback()
    {
        var e = Create();
        e.Start(1, Cast(18056), true);
        var kb = e.AglaopeKnockbackSources.Single();
        Check(kb.Origin == Vector3.Zero && HuntAglaopeKnockback.DISTANCE == 10
            && !HuntAglaopeKnockback.STOP_AT_WALL && !HuntAglaopeKnockback.IGNORE_IMMUNES, "Source movement model missing.");
        Check(e.Knockbacks(Player()).Single().To == new Vector3(20, 0, 0)
            && !e.Knockbacks(Player(x: 30.001f)).Any() && !e.Knockbacks(Player(x: 0)).Any(), "Radial/range/zero-direction rule differs.");
        Check(!e.NavigationAreas(Player()).Any(c => c.Shape != null)
            && HuntDrawing.Collect(e, Player()).Count(p => p.Id.Contains("knockback")) == 2, "KB became navigation or lost dual-view primitives.");
        foreach (var sid in new uint[] { 160, 1209, 3054, 1722, 2663, 2345 })
        {
            var immune = Player(statuses: [new(sid, 0, 1, kb.Activation.AddTicks(1))]);
            e.Synchronize([Boss(Cast(18056)), Helper(), immune, Player(101)], _ => true, Start, Party);
            Check(!e.Knockbacks(immune).Any(), "Source immunity SID lost.");
            e.Synchronize([Boss(Cast(18056)), Helper(), Player(), Player(101)], _ => true, Start, Party);
        }
        var equality = Player(statuses: [new(160, 0, 1, kb.Activation)]);
        e.Synchronize([Boss(Cast(18056)), Helper(), equality, Player(101)], _ => true, Start, Party);
        Check(e.Knockbacks(equality).Any(), "Immunity equality treated as greater.");
        e.Finish(1, 18056, Start.AddSeconds(1));
        Check(!e.Knockbacks(Player()).Any(), "Real Finish retained KB.");
    }

    private static void Owners()
    {
        var e = Create();
        e.Synchronize([Boss(), Helper(Cast(16824)), Player(), Player(101)], _ => true, Start, Party);
        Check(Sonata(e).Shape != null, "Cold raw helper bootstrap missing.");
        e.Finish(999, 16824, Start.AddSeconds(1));
        e.Synchronize([Boss(), Helper(Cast(16824)), Player(), Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(e.Areas.Count(c => c.Shape != null) == 1, "Cancel rebuilt or cleared persistent Sonata.");
        e.Start(999, Cast(18056, 2), true);
        e.Synchronize([Boss(), Player(), Player(101)], _ => true, Start.AddSeconds(2), Party);
        Check(!e.Knockbacks(Player()).Any(), "Missing packet-only helper retained KB.");
        e.Synchronize([Boss(), Player(), Player(101)], _ => false, Start.AddSeconds(3), Party);
        Check(!e.Areas.Any() && !e.Active, "Mechanism disable retained owner output.");
        e.Reset();
        Check(!e.AglaopePayloadMissing && !e.AglaopeKnockbackSources.Any(), "Reset retained model.");
    }

    private static void FirstRaw()
    {
        var e = Create();
        e.Start(999, Cast(18056), true);
        var raw = Cast(18056, location: new(3, 0, 5));
        e.Synchronize([Boss(), Helper(raw), Player(), Player(101)], _ => true, Start.AddSeconds(.2), Party);
        var kb = e.AglaopeKnockbackSources.Single();
        Check(kb.Origin == raw.Location && kb.Cast.Input == raw
            && Math.Abs((kb.Activation - Start).TotalSeconds - 3.3) < .000001,
            "First raw did not correct an already delivered packet exactly once.");
        e = Create();
        e.Synchronize([Boss(), Helper(raw), Player(), Player(101)], _ => true, Start.AddSeconds(3.4), Party);
        kb = e.AglaopeKnockbackSources.Single();
        var expected = Start.AddSeconds(3.4).AddSeconds((3f + .3f) - 3f);
        Check(kb.Activation == expected && kb.Origin == raw.Location, "New saturated CastInfo lost NPC remainder.");
        e.Synchronize([Boss(), Helper(raw), Player(), Player(101)], _ => true, Start.AddSeconds(4), Party);
        Check(e.AglaopeKnockbackSources.Single().Activation == expected, "Unchanged raw renewed source clock.");
        e = Create();
        e.Start(999, raw, true);
        e.Synchronize([Boss(), Helper(raw), Player(), Player(101)], _ => true, Start.AddSeconds(3.4), Party);
        Check(e.AglaopeKnockbackSources.Single().Activation == expected,
            "First raw with identical payload did not correct saturated packet clock.");
    }

    private static void Removal()
    {
        foreach (var packetOnly in new[] { false, true })
        {
            var e = Create();
            e.Start(999, Cast(18056), true);
            if (!packetOnly)
                e.Synchronize([Boss(), Helper(Cast(18056)), Player(), Player(101)], _ => true, Start, Party);
            e.Remove(999, Start.AddSeconds(1));
            Check(!e.AglaopeKnockbackSources.Any(), "Explicit helper destroy retained knockback.");
        }
        var encounter = Create();
        encounter.Synchronize([Boss(), Helper(Cast(18056)), Player(), Player(101)], _ => true, Start, Party);
        encounter.Synchronize([Boss(), Helper(Cast(999999)), Player(), Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(!encounter.AglaopeKnockbackSources.Any(), "Unclaimed new raw action retained previous knockback.");
        encounter = Create();
        encounter.Start(999, Cast(16824), true);
        encounter.AglaopeEffect(999, 16824, 7, [100], Raw(Status(991)), Start);
        encounter.Remove(100, Start);
        encounter.Synchronize([Boss(), Helper(), Player(), Player(101)], _ => true, Start, Party);
        Check(!encounter.Areas.Any(c => c.Shape != null), "Reused recipient ID inherited destroyed Actor's pending991.");
        var pending = new HuntAglaopePendingStatuses();
        var map = new[] { Boss(), Player(), Player(101) }.ToDictionary(a => a.Id);
        pending.Effect(1, 1, [100, 101], Raw(Status(991), Status(991)), map, Start);
        pending.Remove(1);
        Check(pending.Pending.Count == 2, "Destroyed source incorrectly removed other recipients' pending statuses.");
    }
}

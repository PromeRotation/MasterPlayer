using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntIxtabRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "P", 101), new(2, "P", 102)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x2838, Kind = 2, Hp = 1000, InCombat = true, Targetable = true };
    private static ReplayActorState Helper(ReplayCast? cast = null) => new()
        { Id = 999, Oid = 0x233C, Kind = 2, Cast = cast };
    private static ReplayActorState Player(ulong id = 100, uint job = 0, ReplayStatus? status = null) => new()
        { Id = id, Name = $"P{id}", Hp = 1000, Position = new(id == 100 ? 10 : -10, 0, 20),
            ClassJob = job, Statuses = status == null ? [] : [status] };
    private static ReplayCast Cast(uint action, double at = 0, ulong target = 100)
        => new(action, target, new(10, 0, 20), .3f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Player(), Player(101), Player(102)], _ => true, Start, Party);
        return e;
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static HuntPendingCast[] Queue(SimpleHuntEncounter e) => e.Areas.Where(a => a.SequenceId?.Contains("/ixtab/dual/") == true).ToArray();
    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Ixtab dualcast retained through cancel exact float delay effect FIFO and all previews", Dualcast);
        run("SimpleHunts Ixtab Abyss finish-by-source Crypt effect-all persistent dead target and frozen identity", Baits);
        run("SimpleHunts Ixtab Meteor component target FIFO unlimited stack and absent Shared prediction", Meteor);
        run("SimpleHunts Ixtab Doom duplicate gains healer branches raw-event acknowledgement and destroy identity", Doom);
        run("SimpleHunts Ixtab helper owner replay ordinary geometry raidwide hint lifetime and cleanup", Lifecycle);
        run("SimpleHunts Ixtab missing packet facing and same-frame raw replacement destruction finish each order", Generation);
    }

    private static void Dualcast()
    {
        foreach (var action in new uint[] { 17843, 17999 })
        {
            var e = Create();
            e.Start(1, Cast(18077), false);
            e.Finish(1, 18077, Start.AddSeconds(1));
            Check(!e.Hints(Player()).Any(), "Dualcast hint survived cancellation.");
            e.Start(999, Cast(action, 2), false);
            var queue = Queue(e);
            Check(queue.Length == 2 && queue[0].Risky && !queue[1].Risky
                && queue[1].Activation == Cast(action, 2).FinishAt.AddSeconds(5.1f)
                && queue.All(a => a.Location == Cast(action).Location && a.Rotation == 0)
                && (action == 17843 ? queue[0].Shape is CircleTemplate { Radius: 20 } && queue[1].Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 40 }
                    : queue[0].Shape is DonutTemplate && queue[1].Shape is CircleTemplate),
                "Ixtab dual queue lost order, geometry, locked location, float delay or head-only risk.");
            e.Finish(999, action, Start.AddSeconds(3));
            e.Start(1, Cast(action, 4), false);
            Check(Queue(e).Length == 3 && Queue(e).Count(c => c.Risky) == 1, "Dual flag failed consumption or overlapping risk.");
            foreach (var (hit, sequence) in new[] { (18074u, 1u), (17843u, 2u), (18075u, 3u) })
            {
                e.Effect(999, hit, Start.AddSeconds(20 + sequence), sequence);
                e.Effect(999, hit, Start.AddSeconds(20.01 + sequence), sequence);
            }
            Check(Queue(e).Length == 0, "Ixtab effect queue was reset by Finish, expired or duplicated.");
        }
    }

    private static void Baits()
    {
        var e = Create();
        e.Start(1, Cast(17848), false);
        e.Effect(999, 17848, Start.AddSeconds(1), 1);
        Check(e.TargetPreviews(Player()).Single().Shape is CircleTemplate { Radius: 6 }
            && e.TargetPreviews(Player()).Single().Location == Player().Position
            && e.TargetDangers(Player(), Party).All(c => c.Shape is CircleTemplate { Radius: 6 }),
            "Abyss did not use dynamic target circle or was consumed by effect.");
        e.Finish(1, 17848, Start.AddSeconds(2));
        Check(!e.TargetPreviews(Player()).Any(), "Abyss cancel failed source-first removal.");
        e.Start(1, Cast(17847, 3), false);
        e.Finish(1, 17847, Start.AddSeconds(4));
        e.Start(999, Cast(17847, 5, 101), false);
        e.Finish(999, 17847, Start.AddSeconds(6));
        e.Synchronize([Boss(), Helper(), Player() with { Dead = true }, Player(101), Player(102)], _ => true, Start.AddSeconds(30), Party);
        var baits = e.TargetPreviews(Player()).ToArray();
        Check(baits.Length == 2 && baits.All(c => c.Shape is FanTemplate { Radius: 38.24f })
            && e.PredictedDamage(Party).Count() == 2
            && e.PredictedDamage(Party).All(p => p.DamageType == "Raidwide" && p.Activation == Cast(17847, 3).FinishAt)
            && !e.Hints(Player()).Any(),
            "Crypt Finish/timeout/dead target incorrectly removed bait, retained cast hint or predicted the entire raid.");
        e.Remove(100);
        e.Synchronize([Boss(), Helper(), Player() with { Position = new(50, 0, 0) }, Player(101), Player(102)],
            _ => true, Start.AddSeconds(31), Party);
        Check(e.TargetPreviews(Player()).First().Rotation != MathF.PI / 2, "Crypt rebound a destroyed target.");
        e.Effect(999, 17847, Start.AddSeconds(32), 2);
        Check(!e.TargetPreviews(Player()).Any() && !e.PredictedDamage(Party).Any(), "Crypt real effect did not clear every source.");
    }

    private static void Meteor()
    {
        var e = Create();
        e.Start(1, Cast(17844), false);
        e.Start(999, Cast(17844, 1), false);
        Check(e.TargetPreviews(Player()).Count() == 2 && e.TargetPreviews(Player()).All(c => c.Shape is CircleTemplate { Radius: 10 })
            && !e.PredictedDamage(Party).Any(), "Meteor lost duplicate targets/R10 or invented Shared damage.");
        e.Finish(999, 17844, Start.AddSeconds(2), 100);
        Check(e.TargetPreviews(Player()).Single().Order == e.Casts.Single(c => c.Source == 1).Order + 1,
            "Meteor Finish deleted by caster rather than component target FIFO.");
        e.Effect(999, 17844, Start.AddSeconds(3), 1);
        Check(e.TargetPreviews(Player()).Count() == 1, "Meteor effect consumed a cast-finish stack.");
        Check(e.TargetDangers(Player(), Party).Single().Shape is HuntStackJoinTemplate { Radius: 5 },
            "Meteor marked player lost half-radius gather navigation.");
        e.Finish(1, 17844, Start.AddSeconds(4), 100);
        Check(!e.TargetPreviews(Player()).Any(), "Meteor last target Finish retained output.");
    }

    private static void Doom()
    {
        var e = Create();
        var status = new ReplayStatus(1769, 0, 1, Start.AddSeconds(25));
        e.IxtabStatus(100, status, 1, Start.AddSeconds(.1));
        e.IxtabStatus(100, status, 1, Start.AddSeconds(.2));
        e.IxtabStatus(100, null, 1, Start.AddSeconds(.3));
        e.UpdateComponents(Start.AddSeconds(100));
        Check(e.TargetHints(Player(), Party).Contains("You were doomed! Get healed to full fast.")
            && e.TargetHints(Player(job: 24), Party).Contains("Heal yourself to full! (Doom).")
            && e.TargetHints(Player(101, 40), Party).Contains("Heal to full P100! (Doom)"),
            "Doom lost duplicate Gain/one Lose, healer branches or used HP/timeout to clear.");
        e.IxtabStatus(100, null, 1, Start.AddSeconds(101));
        Check(!e.TargetHints(Player(), Party).Any(), "Second Doom Lose failed to remove remaining reference.");
        e.Reset();
        e.Synchronize([Boss(), Helper(), Player(status: status), Player(101), Player(102)], _ => true, Start, Party);
        e.IxtabStatus(100, status, 1, Start.AddSeconds(.1));
        e.IxtabStatus(100, null, 1, Start.AddSeconds(.2));
        Check(!e.TargetHints(Player(), Party).Any(), "Initial raw status plus matching Gain double-counted Doom.");
        e.Synchronize([Boss(), Helper(), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(1), Party);
        e.IxtabStatus(100, status, 1, Start.AddSeconds(2));
        e.Synchronize([Boss(), Helper(), Player(status: status), Player(101), Player(102)], _ => true, Start.AddSeconds(2.1), Party);
        e.Remove(100);
        e.Synchronize([Boss(), Helper(), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(3), Party);
        Check(!e.TargetHints(Player(), Party).Any(), "Doom destroy did not apply status Lose or rebound a same-ID object.");
        e.Reset();
        e.Synchronize([Boss(), Helper(), Player(status: status), Player(101), Player(102)], _ => true, Start.AddSeconds(2), Party);
        e.IxtabStatus(100, status, 1, Start.AddSeconds(.1));
        e.IxtabStatus(100, status, 1, Start.AddSeconds(2.1));
        e.Synchronize([Boss(), Helper(), Player(status: status), Player(101), Player(102)], _ => true, Start.AddSeconds(2.2), Party);
        e.Synchronize([Boss(), Helper(), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(4), Party);
        e.IxtabStatus(100, null, 1, Start.AddSeconds(3));
        Check(e.TargetHints(Player(), Party).Contains("You were doomed! Get healed to full fast."),
            "Delayed raw/event confirmation doubled Gain or consumed a second real Doom entry.");
        e.IxtabStatus(100, null, 1, Start.AddSeconds(5));
        Check(!e.TargetHints(Player(), Party).Any(), "Delayed acknowledgement left a fabricated duplicate Doom.");
        e.Reset();
        var later = status with { ExpiresAt = Start.AddSeconds(35) };
        e.Synchronize([Boss(), Helper(), Player(status: later), Player(101), Player(102)], _ => true, Start.AddSeconds(12), Party);
        e.IxtabStatus(100, status, 1, Start);
        e.IxtabStatus(100, null, 1, Start.AddSeconds(1));
        e.IxtabStatus(100, later, 1, Start.AddSeconds(10));
        e.Synchronize([Boss(), Helper(), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(20), Party);
        Check(!e.TargetHints(Player(), Party).Any(),
            "Unobserved old Gain/Lose overrode the newer sampled Doom baseline and swallowed its raw Lose.");
        e.Reset();
        var newest = status with { ExpiresAt = Start.AddSeconds(45) };
        e.Synchronize([Boss(), Helper(), Player(status: later), Player(101), Player(102)], _ => true, Start.AddSeconds(12), Party);
        e.IxtabStatus(100, status, 1, Start);
        e.IxtabStatus(100, null, 1, Start.AddSeconds(1));
        e.Synchronize([Boss(), Helper(), Player(status: newest), Player(101), Player(102)], _ => true, Start.AddSeconds(13), Party);
        e.IxtabStatus(100, later, 1, Start.AddSeconds(10));
        e.Synchronize([Boss(), Helper(), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(20), Party);
        Check(e.TargetHints(Player(), Party).Contains("You were doomed! Get healed to full fast."),
            "Pending history swallowed a newer raw refresh Gain or matched its Lose against an older status.");
        e.IxtabStatus(100, newest, 1, Start.AddSeconds(13));
        e.IxtabStatus(100, null, 1, Start.AddSeconds(20));
        Check(e.TargetHints(Player(), Party).Contains("You were doomed! Get healed to full fast."),
            "Confirmation of already applied C transitions changed the duplicate-refresh count.");
        var state = new HuntIxtabDoomState();
        var actors = new Dictionary<ulong, ReplayActorState> { [100] = Player(status: status) };
        state.Observe(actors, Start);
        state.Status(Player(), status, 1, Start.AddSeconds(.1));
        actors[100] = Player(status: later);
        state.Observe(actors, Start.AddSeconds(1));
        Check(state.Hints(Player(101, 40)).Count() == 2, "Doom raw refresh failed to append its second Actor reference.");
        actors[100] = Player();
        state.Observe(actors, Start.AddSeconds(2));
        Check(state.Hints(Player(101, 40)).Count() == 1, "Doom raw Lose did not remove precisely one reference.");
        state.Remove(100);
        Check(state.Hints(Player(101, 40)).Single() == "Heal to full P100! (Doom)",
            "Doom destruction emitted a second Lose after raw had already emptied the status slot.");
        state = new();
        actors[100] = Player(status: status);
        state.Observe(actors, Start);
        state.Status(Player(), status, 1, Start.AddSeconds(.1));
        actors[100] = Player(status: later);
        state.Observe(actors, Start.AddSeconds(.1));
        actors[100] = Player();
        state.Observe(actors, Start.AddSeconds(.1));
        state.Remove(100);
        Check(state.Hints(Player(101, 40)).Count() == 1,
            "Equal-timestamp raw refresh/loss/destruction reused an earlier confirmed Gain as occupied status.");
        state = new();
        actors[100] = Player(status: status);
        state.Observe(actors, Start);
        state.Status(Player(), status, 1, Start.AddSeconds(.1));
        state.Status(Player(), later, 1, Start.AddSeconds(1));
        state.Status(Player(), null, 1, Start.AddSeconds(2));
        state.Remove(100);
        Check(state.Hints(Player(101, 40)).Count() == 1,
            "Doom destruction ignored a newer event Lose while the last raw snapshot still contained the older status.");
        state = new();
        actors[100] = Player(status: status);
        state.Observe(actors, Start);
        state.Status(Player(), status, 1, Start.AddSeconds(.1));
        state.Status(Player(), later, 1, Start.AddSeconds(1));
        state.Status(Player(), null, 1, Start.AddSeconds(2));
        actors[100] = Player();
        state.Observe(actors, Start.AddSeconds(3));
        state.Remove(100);
        Check(state.Hints(Player(101, 40)).Count() == 1,
            "A raw sample that skipped the refreshed status consumed its already handled Lose twice.");
        state = new();
        actors[100] = Player(status: status);
        state.Observe(actors, Start);
        state.Status(Player(), status, 1, Start);
        actors[100] = Player();
        state.Observe(actors, Start.AddSeconds(3));
        state.Status(Player(), later, 1, Start.AddSeconds(1));
        state.Status(Player(), null, 1, Start.AddSeconds(2));
        Check(state.Hints(Player(101, 40)).Count() == 1,
            "Late intermediate Gain/Lose failed to reconnect the sampled removal receipt.");
        state = new();
        actors[100] = Player(status: status);
        state.Observe(actors, Start);
        state.Status(Player(), status, 1, Start);
        actors[100] = Player();
        state.Observe(actors, Start.AddSeconds(1));
        state.Status(Player(), later, 1, Start.AddSeconds(2));
        state.Status(Player(), null, 1, Start.AddSeconds(3));
        Check(!state.Hints(Player(101, 40)).Any(),
            "A newer Doom lifecycle incorrectly reused an older removal receipt.");
        state = new();
        actors[100] = Player(status: later);
        state.Observe(actors, Start.AddSeconds(12));
        state.Status(Player(), status, 1, Start);
        state.Status(Player(), null, 1, Start.AddSeconds(1));
        actors[100] = Player(status: newest);
        state.Observe(actors, Start.AddSeconds(13));
        state.Status(Player(), later, 1, Start.AddSeconds(10));
        actors[100] = Player();
        state.Observe(actors, Start.AddSeconds(20));
        Check(state.Hints(Player(101, 40)).Count() == 1, "Doom A/B/C count before confirmations differs.");
        state.Status(Player(), newest, 1, Start.AddSeconds(13));
        Check(state.Hints(Player(101, 40)).Count() == 1, "Delayed C Gain confirmation added a duplicate.");
        state.Status(Player(), null, 1, Start.AddSeconds(20));
        Check(state.Hints(Player(101, 40)).Count() == 1, "Delayed C Lose confirmation removed another reference.");
    }

    private static void Generation()
    {
        var e = Create();
        e.Start(999, Cast(18077) with { Rotation = null }, true);
        e.Finish(999, 18077, Start.AddSeconds(1));
        e.Start(999, Cast(17843, 2) with { Rotation = null }, true);
        Check(Queue(e).Length == 2, "Rotationless dual/circle packet was rejected.");
        foreach (var action in new uint[] { 17848, 17844, 17847 })
        {
            e = Create();
            e.Start(999, Cast(action) with { Rotation = null }, true);
            Check(e.TargetPreviews(Player()).Count() == 1, "Rotationless target bait/stack packet was rejected.");
        }
        foreach (var action in new uint[] { 17848, 17844 })
        {
            e = Create();
            var raw = Cast(action);
            e.Synchronize([Boss(), Helper(raw), Player(), Player(101), Player(102)], _ => true, Start, Party);
            e.Synchronize([Boss(), Helper(raw with { TargetId = 101 }), Player(), Player(101), Player(102)],
                _ => true, Start.AddSeconds(1), Party);
            e.Remove(999, Start.AddSeconds(1));
            Check(!e.TargetPreviews(Player()).Any() && !e.PredictedDamage(Party).Any(),
                "Same-frame replacement and source destruction failed to finish each cast order.");
        }
    }

    private static void Lifecycle()
    {
        var e = Create();
        e.Start(999, Cast(17846), false);
        Check(e.Areas.Single(c => c.Shape != null).Shape is CircleTemplate { Radius: 18 }, "Flare lost R18.");
        e.Start(999, Cast(17845, 1), false);
        Check(e.Areas.Single(c => c.Shape != null).Shape is FanTemplate { Radius: 40, AngleRadians: MathF.PI / 4 }, "Blizzard lost 45 degree cone.");
        e.Start(999, Cast(18072, 2), false);
        Check(e.Hints(Player()).Contains("Interrupt!") && !e.PredictedDamage(Party).Any()
            && !e.Areas.Any(c => c.Shape != null), "Twister fabricated raidwide geometry or lost interrupt hint.");
        e.Start(999, Cast(17849, 3), false);
        Check(e.PredictedDamage(Party).Count() == 3, "Quake lost raidwide prediction.");
        e.Reset();
        var raw = Cast(17843);
        e.Synchronize([Boss(), Helper(raw), Player()], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), Helper(raw), Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Queue(e).Length == 2, "New Ixtab owner failed to replay current helper cast.");
        e.Remove(1);
        Check(Queue(e).Single().Source == 2, "Ixtab owner cleanup erased a different owner.");
        e.Remove(2);
        Check(!e.Areas.Any() && !e.Casts.Any() && !e.TargetPreviews(Player()).Any(), "Last Ixtab owner retained helper output.");
    }
}

using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleRebekkahRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private const string Ink = "Cast Ink Jet on boss to get blinded!";
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Rebekkah both default primary lifecycle exact component scope and small arena", Lifecycle);
        run("Carnivale Rebekkah reflect component latch any caster cancellation repeated starts no restrictions", Reflect);
        run("Carnivale Rebekkah blind prompt trigger finish only empty inherited eyes no expiry or effect", Blind);
        run("Carnivale Rebekkah blind native status slots gains losses roster changes missing slots", BlindSlots);
        run("Carnivale Rebekkah both voidzone native main target float versus double consumer delays", Voidzones);
        run("Carnivale Rebekkah voidzone current cast target interrupt cancel and persistent live handoff", CastAndLive);
        run("Carnivale Rebekkah background component state retained and exact actor ordered bootstrap", Background);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x2728, bool combat = true)
        {
            Actors =
            [
                new(1, oid, Center, InCombat: combat, IsEnemy: true, ObjectIndex: 2),
                new(2, 0x233C, Center),
                new(100, 0, Center, Player: true, PartySlot: 0),
                new(101, 0, Center + Vector3.UnitX * 3, Player: true, PartySlot: 1),
            ];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync(ulong player = 100) => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == player)?.Position,
            Now, playerId: player);
        internal void Cast(uint action, ulong caster = 2, ulong target = 101)
        {
            Now = Now.AddSeconds(.01);
            Set(caster, a => a with { Cast = new(action, Center + Vector3.UnitZ * 12, .3f, Now, Now.AddSeconds(3.3), target) });
            Sync();
        }
        internal string[] Hints() => E.Hints(Center).ToArray();
        internal void End(ulong caster = 2)
        {
            Set(caster, a => a with { Cast = null });
            Now = Now.AddSeconds(.1);
            Sync();
        }
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x2727, 0x2728 })
        {
            var f = new Fixture(oid, false);
            f.Actors.Add(new(3, 0x2779, Center, InCombat: true));
            f.Sync();
            Check(f.E.Active == null && f.E.CarnivalePrecombatHint != null, "Auxiliary combat pulled primary.");
            f.Set(1, a => a with { InCombat = true, Targetable = false });
            f.Sync();
            Check(f.E.Active == null, "Untargetable primary pulled.");
            f.Set(1, a => a with { Targetable = true });
            f.Sync();
            Check(f.E.Active?.Casts.Length == (oid == 0x2727 ? 4 : 6) && f.E.CarnivalePrecombatHint == null
                && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1
                && !f.E.Arena!.Contains(Center + Vector3.UnitX * 17), "Scope, small arena or default drawing.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Default HP0 completion.");
        }
    }

    private static void Reflect()
    {
        var f = new Fixture();
        f.Cast(15073);
        Check(f.Hints().Contains("Boss will reflect all magic damage!") && f.E.CarnivaleReflectCast?.Source == 2,
            "Reflect complete cast model/start.");
        f.E.Effect(2, 15073, 1, Start.AddSeconds(10));
        Check(f.Hints().Contains("Boss will reflect all magic damage!"), "Effect finished reflection.");
        f.End();
        Check(f.Hints().Contains("Boss reflects all magic damage!") && !f.Hints().Contains("Boss will reflect all magic damage!"),
            "Cancel did not latch reflection.");
        f.Now = Start.AddSeconds(100);
        f.Sync();
        Check(f.Hints().Contains("Boss reflects all magic damage!") && f.E.ForbiddenTargets.Length == 0
            && !f.E.PredictedDamage.Any() && !f.E.Gazes.Any(), "Latch expiry or automatic restrictions.");
        f.Cast(15073, 1);
        f.Cast(15073, 2);
        f.E.Finish(1, 15073, f.Now.AddSeconds(.1));
        Check(f.Hints().Contains("Boss reflects all magic damage!"), "Reflect incorrectly counted active casters.");
        f.E.StatusChanged(1, 15, false, f.Now);
        Check(f.Hints().Contains("Boss reflects all magic damage!"), "Unrelated status removed reflect.");
    }

    private static void Blind()
    {
        var f = new Fixture();
        f.Cast(15078);
        Check(!f.Hints().Contains(Ink), "Mid-gaze observation invented Schizocarps history.");
        f.End();
        f.Cast(15077);
        Check(f.Hints().Contains(Ink) && f.E.CarnivaleBlindTrigger?.Rule.Action == 15077, "Trigger missing.");
        f.End();
        f.Now = Start.AddSeconds(100);
        f.Sync();
        Check(f.Hints().Contains(Ink), "Schizocarps cancel/deadline ended prompt.");
        f.E.StatusChanged(100, 15, true, f.Now, expiresAt: Start);
        Check(!f.Hints().Contains(Ink), "Blind expiry wrongly checked.");
        f.E.StatusChanged(100, 15, false, f.Now.AddSeconds(.1));
        Check(f.Hints().Contains(Ink), "Blind lose did not resume prompt.");
        f.Cast(15078);
        f.E.Effect(2, 15078, 1, f.Now.AddSeconds(20));
        Check(f.Hints().Contains(Ink) && !f.E.Gazes.Any() && f.E.GazeFacing(Center, 0) == null
            && !f.E.AmbientAoes().Any(a => a.Id.Contains("gaze")), "Inherited gaze callbacks or effect ended prompt.");
        f.End();
        Check(!f.Hints().Contains(Ink), "ExplosiveDehiscence Finish failed to close.");
    }

    private static void BlindSlots()
    {
        var f = new Fixture();
        f.Set(100, a => a with { StatusSlots = [new ReplayStatus(15, 0, 1, Start) { SlotIndex = 7 }] });
        f.Cast(15077);
        Check(!f.Hints().Contains(Ink), "Activation status bootstrap.");
        f.Set(100, a => a with { PartySlot = 2 });
        f.Sync();
        Check(f.Hints().Contains(Ink), "Roster change moved mask.");
        f.Set(100, a => a with { StatusSlots = [] });
        f.Sync();
        f.Set(100, a => a with { PartySlot = 0 });
        f.Sync();
        Check(!f.Hints().Contains(Ink), "Lose used old party slot instead of current.");
        f.E.StatusChanged(100, 15, false, Start.AddSeconds(1));
        Check(f.Hints().Contains(Ink), "Current slot lose failed.");
        f.Set(100, a => a with { StatusSlots =
        [
            new ReplayStatus(15, 0, 1, Start) { SlotIndex = 0 },
            new ReplayStatus(15, 0, 2, Start) { SlotIndex = 1 },
        ] });
        f.Sync();
        f.Set(100, a => a with { StatusSlots = [new ReplayStatus(15, 0, 2, Start) { SlotIndex = 1 }] });
        f.Sync();
        Check(f.Hints().Contains(Ink), "Blind mask became reference counter.");
        f.Sync(101);
        Check(f.E.Hints(Center).Contains(Ink), "Switching viewer copied previous blind state.");
        f.E.StatusChanged(999, 15, true, Start.AddSeconds(2));
        Check(f.E.Hints(Center).Contains(Ink), "Unknown actor blind wrote a valid bit.");
        var mask = new CarnivaleBlindness();
        var a = new DungeonActor(200, 0, Center, PartySlot: 0,
            StatusSlots: [new ReplayStatus(15, 0, 1, Start) { SlotIndex = 0 }]);
        Dictionary<ulong, int> roster = new() { [200] = 0 };
        mask.Observe([], new Dictionary<ulong, int>(), true);
        mask.Observe([a], roster, false);
        Check(!mask.Blinded(0), "New member Gain used uncommitted roster.");
        mask.Observe([a with { StatusSlots = [] }], roster, false);
        mask.Observe([a], roster, false);
        Check(mask.Blinded(0), "Existing roster Gain ignored.");
        mask.Observe([a with { PartySlot = -1 }], new Dictionary<ulong, int>(), false);
        mask.Removed(200);
        Check(mask.Blinded(0), "Departed actor destroy cleared old bit via historical slot.");
        mask.Clear();
        mask.Observe([a], roster, true);
        mask.Removed(200);
        Check(!mask.Blinded(0), "Destroy before roster change failed to clear old roster bit.");
        mask.Observe([a], roster, false);
        Check(mask.Blinded(0), "Destroy erased a still-enrolled member's roster identity.");
        mask.Clear();
        mask.Observe([], roster, true);
        mask.Observe([a], roster, false);
        Check(mask.Blinded(0), "Missing actor already in roster lost first Gain.");
    }

    private static void Voidzones()
    {
        foreach (var oid in new uint[] { 0x2727, 0x2728 })
        {
            var f = new Fixture(oid);
            f.E.Effect(2, 15076, 0, Start.AddSeconds(10), 101, Center, Start.AddSeconds(2));
            f.E.Effect(2, 15076, 0, Start.AddSeconds(11), 999, Center + Vector3.UnitZ);
            var p = f.E.CarnivaleRayPredictions.First();
            var delay = oid == 0x2727 ? (double)1.6f : 1.6d;
            Check(p.Action == 15076 && p.Source == 2 && p.MainTarget == 101 && p.Sequence == 0
                && p.OccurredAt == Start.AddSeconds(2) && p.ObservedAt == Start.AddSeconds(10)
                && p.Activation == Start.AddSeconds(10).AddSeconds(delay)
                && p.Position == Center + Vector3.UnitX * 3 && f.E.CarnivaleRayPredictions.Count() == 2,
                "Full effect model, distinct events or exact delay.");
            f.Set(101, a => a with { Position = Center + Vector3.UnitX * 15 });
            f.Now = Start.AddSeconds(50);
            f.Sync();
            Check(f.E.CarnivaleRayPredictions.First().Position == p.Position
                && f.E.AmbientAoes().Count(a => a.Id.Contains("/ray/event/") && a.Navigate && a.Shape is CircleTemplate { Radius: 6 }) == 2,
                "Event prediction moved or expired.");
        }
    }

    private static void CastAndLive()
    {
        var f = new Fixture();
        f.Cast(15076);
        Check(f.E.CarnivaleInterruptCasts.Single().Rule.Action == 15076 && f.Hints().Contains("Interrupt!")
            && f.E.AmbientAoes().Single(a => a.Id.Contains("/ray/cast/")).Position == Center + Vector3.UnitX * 3,
            "Interrupt/cast target.");
        f.Set(101, a => a with { Position = Center + Vector3.UnitX * 7 });
        f.Sync();
        Check(f.E.AmbientAoes().Single(a => a.Id.Contains("/ray/cast/")).Position == Center + Vector3.UnitX * 7, "Dynamic cast target.");
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        Check(f.E.AmbientAoes().Single(a => a.Id.Contains("/ray/cast/")).Position == Center + Vector3.UnitZ * 12, "Cast fallback.");
        f.E.Effect(2, 15076, 1, Start, 100);
        f.E.Effect(2, 15076, 2, Start, 100);
        f.End();
        Check(!f.E.CarnivaleRayCasts.Any() && !f.E.CarnivaleInterruptCasts.Any() && f.E.CarnivaleRayPredictions.Count() == 2,
            "Finish cleared wrong outputs.");
        f.Actors.Add(new(5, 0x1EA9F9, Center + Vector3.UnitX * .123456f, Dead: true));
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 1 && f.Hints().Contains("GTFO from voidzone!")
            && f.E.AmbientAoes().Single(a => a.Id.Contains("/ray/live/")).Position.X != f.Actors.Last().Position.X, "Live handoff/dead/quantization.");
        f.Set(5, a => a with { EventState = 7 });
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 1 && !f.E.AmbientAoes().Any(a => a.Id.Contains("/ray/live/")), "State7.");
        f.Cast(15074);
        Check(!f.E.CarnivaleInterruptCasts.Any(), "BadBreath comment expanded interrupt scope.");
    }

    private static void Background()
    {
        var f = new Fixture();
        var e = new DungeonEncounter(796);
        var old = new DungeonActor(9, 0x26F2, Center, InCombat: true);
        e.Synchronize([old, f.Actors[2]], Center, Start, playerId: 100);
        f.Set(1, a => a with { Cast = new(15077, Center, 0, Start, Start.AddSeconds(5), 0) });
        f.Set(2, a => a with { Cast = new(15073, Center, 0, Start, Start.AddSeconds(3), 0) });
        e.Synchronize([old, .. f.Actors], Center, Start.AddSeconds(.1), playerId: 100);
        e.Finish(2, 15073, Start.AddSeconds(.2));
        e.Effect(2, 15076, 1, Start.AddSeconds(.2), 100);
        e.Synchronize([old with { Dead = true }, .. f.Actors], Center, Start.AddSeconds(.3), playerId: 100);
        Check(e.Active?.BossOid == 0x2728 && e.Hints(Center).Contains(Ink)
            && e.Hints(Center).Contains("Boss reflects all magic damage!") && e.CarnivaleRayPredictions.Count() == 1,
            "Background latch/trigger/effect lost.");
        var g = new Fixture(0x2727);
        g.Actors.Add(new(4, 0x2728, Center));
        g.Cast(15073);
        g.Set(4, a => a with { InCombat = true });
        g.End();
        g.Set(1, a => a with { Dead = true });
        g.Sync();
        Check(g.E.Active?.BossOid == 0x2728 && !g.E.Hints(Center).Contains("Boss reflects all magic damage!"),
            "Newly activated module received a Finish before its components existed.");
        var h = new Fixture(0x2727);
        h.Actors.Add(new(4, 0x2728, Center));
        h.Cast(15076);
        var firstClock = h.E.CarnivaleRayCasts.Single().Cast.FinishAt;
        h.Now = Start.AddSeconds(20);
        h.Set(4, a => a with { InCombat = true });
        h.Sync();
        Check(h.E.CarnivaleRayCasts.Single().Cast.FinishAt == firstClock, "Existing component clock refreshed.");
        h.Set(1, a => a with { Dead = true });
        h.Sync();
        Check(h.E.Active?.BossOid == 0x2728
            && h.E.CarnivaleRayCasts.Single().Cast.FinishAt == h.Now.AddSeconds((double).3f),
            "New component did not lock activation-time NPC remaining delay.");
        h.End();
        Check(!h.E.CarnivaleRayCasts.Any() && !h.E.CarnivaleInterruptCasts.Any()
            && !h.Hints().Contains("Interrupt!"), "Late bootstrap cloned the interrupt receipt identity.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

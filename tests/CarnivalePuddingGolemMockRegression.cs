using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivalePuddingGolemMockRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Pudding two acts group pull all dead primary HP0 not premature preparation first only", Pudding);
        run("Carnivale Pudding GoldenTongue all actors interrupt intent and ordinary elemental actions unconsumed", Interrupts);
        run("Carnivale Golem default primary lifecycle exact cone14 total120 real Finish no raidwide", Golem);
        run("Carnivale Golem EarthenHeart three paths actual float1point2 delay state7 raw target and source", GolemPools);
        run("Carnivale Mock Dark independent R5 simple and R4 three paths double1 delay", MockPools);
        run("Carnivale Mock primary lifecycle all six orange adds unconditional priority overwrite including pointless", Mock);
        run("Carnivale Golem Mock known source actual main target effects no guessed duplicate replay", NativeEffects);
        run("Carnivale Golem Mock late activation current raw clock source barrier cancellations and bootstrap", Bootstrap);
        run("Carnivale Pudding shared action background owner independent hints and Finish", Background);
        run("Carnivale Pudding later owner activation replays unchanged raw current interrupt", LateOwner);
        run("Carnivale Pudding Golem Mock saturated raw clocks retain cast generation and original deadlines", Saturated);
        run("Carnivale Pudding Golem Mock unclaimed real Finish blocks stale raw on activation", UnclaimedFinish);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat, IsEnemy: true, ObjectIndex: 2, Name: "Primary"),
                new(2, 0x233C, Center), new(100, 0, Center + Vector3.UnitZ * 3, Player: true, PartySlot: 0)];
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Cast(uint action, ulong source = 2, ulong target = 100, double seconds = 4)
        {
            Now = Now.AddSeconds(.01);
            if (!Actors.Any(a => a.Id == source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, Center, .7f, Now, Now.AddSeconds(seconds), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
        internal string[] Hints() => E.Hints(Center).ToArray();
    }

    private static void Pudding()
    {
        foreach (var (oid, adds) in new[] { (0x25C0u, new uint[] { 0x25C2, 0x25C4 }), (0x25C1u, new uint[] { 0x25C5, 0x25C3 }) })
        {
            var f = new Fixture(oid, false);
            Check((f.E.CarnivalePrecombatHint != null) == (oid == 0x25C0), "Constructor hint first only.");
            f.Actors.AddRange(adds.Select((add, i) => new DungeonActor((ulong)(10 + i), add, Center, InCombat: i == 0)));
            f.Sync();
            Check(f.E.Active?.BossOid == oid && f.Aoes("carnivale/enemy/").Length == 3, "Any trash pull and all markers.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active != null, "HP0 not death.");
            f.Set(1, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active != null, "Adds preserve phase.");
            foreach (var add in adds)
                f.Set(f.Actors.Single(a => a.Oid == add).Id, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active == null, "All Dead.");
        }
    }

    private static void Interrupts()
    {
        foreach (var oid in new uint[] { 0x25C0, 0x25C1, 0x242D })
        {
            var f = new Fixture(oid);
            f.Cast(14265);
            Check(f.E.CarnivaleInterrupts.Count() == 1 && f.Hints().Contains("Interrupt!") && !f.E.PredictedDamage.Any()
                && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Pure interrupt.");
            f.Now = Start.AddSeconds(30);
            f.Sync();
            f.E.Effect(2, 14265, 1, f.Now);
            Check(f.E.CarnivaleInterrupts.Any(), "Effect and timeout do not clear.");
            f.E.Finish(2, 14265, f.Now);
            Check(!f.E.CarnivaleInterrupts.Any(), "Real Finish.");
            f.Cast(14266);
            Check(!f.E.OutputCasts.Any(), "Unactivated elemental attacks not fabricated.");
        }
    }

    private static void Golem()
    {
        var f = new Fixture(0x25D4);
        f.Cast(14363);
        Check(f.E.OutputCasts.Single().Value.Rule.Shape is FanTemplate { Radius: 14 }
            && f.E.OutputCasts.Single().Value.Rotation == .7f && !f.E.PredictedDamage.Any(), "Cone.");
        Check(f.Hints().Any(h => h.Contains("Earth based spells are useless")), "Combat hint.");
        f.E.Finish(2, 14363, f.Now);
        f.Cast(14365);
        Check(f.E.CarnivaleInterrupts.Single().Cast.Rule.Action == 14365 && !f.E.PredictedDamage.Any(), "No invented raidwide.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null, "Primary completion.");
    }

    private static void Pool(uint oid, uint action, uint poolOid, float radius, double delay)
    {
        var f = new Fixture(oid);
        f.Cast(action);
        var cast = f.E.CarnivaleRayCasts.Single();
        Check(f.Aoes("carnivale/ray/cast/").Single().Shape is CircleTemplate c && c.Radius == radius, "Voidzone radius.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 7 });
        f.Now = Start.AddSeconds(1);
        f.Sync();
        Check(f.Aoes("carnivale/ray/cast/").Single().Position == Center + Vector3.UnitX * 7, "Dynamic target.");
        f.E.Effect(2, action, 1, f.Now, target: 100);
        var prediction = f.E.CarnivaleRayPredictions.Single();
        Check(prediction.Activation == f.Now.AddSeconds(delay) && prediction.Position == Center + Vector3.UnitX * 7, "Consumer clock.");
        f.E.Finish(2, action, f.Now);
        Check(!f.E.CarnivaleRayCasts.Any() && f.E.CarnivaleRayPredictions.Count() == 1, "Independent Finish.");
        f.Set(2, a => a with { Cast = null });
        f.Actors.Add(new(10, poolOid, prediction.Position, Dead: true, Targetable: false));
        f.Now = Start.AddSeconds(2);
        f.Sync();
        Check(!f.E.CarnivaleRayPredictions.Any() && f.Aoes("carnivale/ray/live/").Length == 1, "Live only filters state7.");
        f.Set(10, a => a with { EventState = 7 });
        f.Sync();
        Check(!f.Aoes("carnivale/ray/").Any(), "State7 hides.");
    }

    private static void GolemPools() => Pool(0x25D4, 14364, 0x1E8FEA, 6, (double)1.2f);
    private static void MockPools()
    {
        Pool(0x242D, 15057, 0x1E9C9D, 4, 1d);
        var f = new Fixture(0x242D);
        f.Cast(15057);
        Check(f.E.OutputCasts.Single().Value.Rule.Shape is CircleTemplate { Radius: 5 }
            && f.Aoes("carnivale/ray/cast/").Single().Shape is CircleTemplate { Radius: 4 }, "Two independent shapes.");
    }

    private static void Mock()
    {
        var f = new Fixture(0x242D);
        var adds = new uint[] { 0x2714, 0x2716, 0x2711, 0x2713, 0x2715, 0x2712 };
        f.Actors.AddRange(adds.Select((oid, i) => new DungeonActor((ulong)(10 + i), oid, Center,
            IsEnemy: true, ObjectIndex: (ushort)(4 + i * 2), Hp: i == 0 ? 1u : 100u)));
        f.Sync();
        Check(f.Aoes("carnivale/enemy/").Length == 7 && f.Aoes("carnivale/enemy/").Count(a =>
            a.Color == new Vector4(1, 128f / 255, 0, 1)) == 6, "All six orange adds.");
        Check(f.E.PriorityTarget(20, Center) == 10, "Source setter overrides pending-dead priority too.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null, "Default Primary ends despite adds.");
    }

    private static void NativeEffects()
    {
        var f = new Fixture(0x25D4);
        f.E.Effect(999, 14364, 1, f.Now, target: 100);
        Check(!f.E.CarnivaleRayPredictions.Any(), "Missing source ignored.");
        f.E.Effect(2, 14364, 2, f.Now, target: 100);
        Check(f.E.CarnivaleRayPredictions.Single().MainTarget == 100 && f.E.UsesCarnivaleNativeEffect(14364)
            && f.E.UsesCarnivaleNativeEffect(15057), "Exact native paths without cast.");
    }

    private static void Bootstrap()
    {
        foreach (var (oid, action, interrupt) in new[] { (0x25D4u, 14364u, 14365u), (0x242Du, 15057u, 14265u) })
        {
            var f = new Fixture(oid, false);
            f.Cast(action);
            f.Cast(interrupt, 3);
            f.Now = Start.AddSeconds(30);
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.CarnivaleRayCasts.Single().Cast.FinishAt == f.Now.AddSeconds((double).3f)
                && f.E.CarnivaleInterrupts.Single().Cast.Rule.Action == interrupt, "Late current state bootstrap.");
        }
    }

    private static void Background()
    {
        var f = new Fixture(0x25C0);
        f.Actors.Add(new(3, 0x25C1, Center, InCombat: true));
        f.Sync();
        f.Cast(14265);
        Check(f.E.CarnivaleInterrupts.Count() == 1, "One per selected owner.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x25C1 && f.E.CarnivaleInterrupts.Count() == 1, "Background module receives same action.");
        f.E.Finish(2, 14265, f.Now);
        Check(!f.E.CarnivaleInterrupts.Any(), "Finish clears both owners independently.");
    }

    private static void LateOwner()
    {
        var f = new Fixture(0x25C0);
        f.Actors.Add(new(3, 0x25C1, Center));
        f.Sync();
        f.Cast(14265);
        f.Now = f.Now.AddSeconds(1);
        f.Set(3, a => a with { InCombat = true });
        f.Sync();
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x25C1 && f.E.CarnivaleInterrupts.Count() == 1
            && f.Hints().Contains("Interrupt!"), "Late component must receive existing unchanged cast.");
        f.E.Finish(2, 14265, f.Now);
        Check(!f.E.CarnivaleInterrupts.Any(), "Bootstrapped interrupt real Finish.");
    }

    private static void Saturated()
    {
        foreach (var (oid, action) in new[] { (0x25C0u, 14265u), (0x25D4u, 14363u),
            (0x25D4u, 14364u), (0x25D4u, 14365u), (0x242Du, 15057u) })
        {
            var f = new Fixture(oid);
            f.Now = Start.AddSeconds(3);
            var raw = new DungeonCastObservation(action, Center, .7f, Start, Start.AddSeconds(3.3), 100, TotalTime: 3);
            f.Set(2, a => a with { Cast = raw });
            f.Sync();
            var accepted = f.E.OutputCasts.Single().Value;
            var pool = f.E.CarnivaleRayCasts.SingleOrDefault();
            var interrupt = f.E.CarnivaleInterrupts.SingleOrDefault();
            f.Now = Start.AddSeconds(3.6);
            f.Set(2, a => a with { Cast = raw with { StartedAt = Start.AddSeconds(.6), FinishAt = Start.AddSeconds(3.9) } });
            f.Sync();
            Check(ReferenceEquals(accepted, f.E.OutputCasts.Single().Value), "Raw clock drift restarted ordinary cast.");
            Check(pool == null || f.E.CarnivaleRayCasts.Single().Generation == pool.Generation
                && f.E.CarnivaleRayCasts.Single().Cast.FinishAt == pool.Cast.FinishAt, "Raw clock drift restarted pool.");
            Check(interrupt == null || f.E.CarnivaleInterrupts.Single().Generation == interrupt.Generation,
                "Raw clock drift restarted interrupt.");
        }
    }

    private static void UnclaimedFinish()
    {
        foreach (var (oid, action) in new[] { (0x25C0u, 14265u), (0x25D4u, 14363u),
            (0x25D4u, 14364u), (0x25D4u, 14365u), (0x242Du, 15057u) })
        {
            var f = new Fixture(oid, false);
            f.Cast(action);
            f.E.Finish(2, action, f.Now.AddSeconds(.1));
            f.Now = f.Now.AddSeconds(.2);
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(!f.E.OutputCasts.Any() && !f.E.CarnivaleRayCasts.Any() && !f.E.CarnivaleInterrupts.Any(),
                "Real preactivation Finish must block stale raw.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleClawsRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale claws first act group lifecycle first claw counter text only", FirstAct);
        run("Carnivale claws second act primary lifecycle living first claw priority and exact component scope", SecondAct);
        run("Carnivale claws voidzone cast follows current target fallback location observed finish", CastTargets);
        run("Carnivale claws event full identity main target lock consumer float delay no timeout", EventPrediction);
        run("Carnivale claws live dead zone state seven quantized one event match per entity update", LiveHandoff);
        run("Carnivale claws background module input retained immediate handoff and own cancellation", Background);
        run("Carnivale claws zero sequence events and persistent creation order despite snapshot reordering", OrderAndZero);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x2721, Center, InCombat: true, Name: "克利俄斯", IsEnemy: true, ObjectIndex: 2),
            new(2, 0x233C, Center),
            new(100, 0, Center, Player: true, PartySlot: 0),
            new(101, 0, Center + Vector3.UnitX * 3, Player: true, PartySlot: 1),
        ];
        internal DateTime Now = Start;
        internal Fixture() => Sync();
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: 100);
        internal void Cast(uint action = 15048, ulong target = 101)
        {
            Set(2, a => a with { Cast = new(action, Center + Vector3.UnitZ * 12, .3f, Now, Now.AddSeconds(3.3), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Rays() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/ray/")).ToArray();
    }

    private static void FirstAct()
    {
        var f = new Fixture();
        f.Set(1, a => a with { Oid = 0x2720, InCombat = false, Name = "主爪" });
        f.Actors.Add(new(3, 0x271F, Center, Dead: true, InCombat: true, Name: "首右爪"));
        f.Actors.Add(new(4, 0x271F, Center, Name: "次右爪"));
        var e = new DungeonEncounter(796);
        e.Synchronize(f.Actors, Center, Start, playerId: 100);
        Check(e.Active?.BossOid == 0x2720 && e.Hints(Center).Contains("主爪 counters magical damage!")
            && !e.Hints(Center).Contains("次右爪 counters physical damage!"), "Group pull or first-claw contract.");
        Check(e.ForbiddenTargets.Length == 0 && e.Active.Casts.Length == 2, "Counter text became action restrictions.");
        f.Set(1, a => a with { Hp = 0 });
        e.Synchronize(f.Actors, Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null, "HP0 replaced allDead.");
        f.Set(1, a => a with { Dead = true });
        e.Synchronize(f.Actors, Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active != null, "Primary death ignored right claw.");
        f.Actors.RemoveAll(a => a.Id == 4);
        e.Synchronize(f.Actors, Center, Start.AddSeconds(3), playerId: 100);
        Check(e.Active == null, "Destroyed last claw did not complete.");
    }

    private static void SecondAct()
    {
        var f = new Fixture();
        f.Actors.Add(new(3, 0x2722, Center, Dead: true, Name: "死左爪"));
        f.Actors.Add(new(4, 0x2722, Center, Name: "活左爪", IsEnemy: true, ObjectIndex: 4));
        f.Actors.Add(new(5, 0x2723, Center, Name: "右爪", IsEnemy: true, ObjectIndex: 6));
        f.Sync();
        Check(f.E.Active?.Casts.Length == 5 && f.E.Hints(Center).Contains("活左爪 counters magical damage!")
            && f.E.Hints(Center).Contains("右爪 counters physical damage!") && f.E.PriorityTarget(1, Center) is 4 or 5,
            "Second-act components/hints/priorities.");
        f.Cast(15049);
        Check(f.E.CarnivaleInterruptCasts.Single().Rule.Action == 15049 && f.E.Hints(Center).Contains("Interrupt!")
            && f.E.ForbiddenTargets.Length == 0 && !f.E.PredictedDamage.Any(), "Interrupt intent.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null && !f.E.CarnivaleInterruptCasts.Any(), "Primary completion waited for claws.");
    }

    private static void CastTargets()
    {
        var f = new Fixture();
        f.Cast();
        Check(f.Rays().Single().Position == f.Actors[3].Position
            && f.Rays().Single().Activation == Start.AddSeconds(3.3), "Cast did not follow target.");
        f.Set(101, a => a with { Position = Center + Vector3.UnitX * 7 });
        f.Now = Start.AddSeconds(1);
        f.Sync();
        Check(f.Rays().Single().Position == Center + Vector3.UnitX * 7, "Target movement lost.");
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        Check(f.Rays().Single().Position == Center + Vector3.UnitZ * 12, "Missing target fallback.");
        f.E.Tick(Start.AddSeconds(30));
        Check(f.Rays().Length == 1, "Cast deadline cleanup.");
        f.E.Finish(2, 15048, Start.AddSeconds(31));
        Check(!f.Rays().Any() && !f.E.CarnivaleRayCasts.Any(), "Finish did not clear cast prediction.");
    }

    private static void EventPrediction()
    {
        var f = new Fixture();
        f.E.Effect(2, 15048, 71, Start.AddSeconds(10), 101, Center, Start.AddSeconds(2));
        var p = f.E.CarnivaleRayPredictions.Single();
        Check(p.Source == 2 && p.MainTarget == 101 && p.Sequence == 71 && p.Action == 15048
            && p.OccurredAt == Start.AddSeconds(2) && p.ObservedAt == Start.AddSeconds(10)
            && p.Activation == Start.AddSeconds(10).AddSeconds((double)1.1f)
            && p.Position == Center + Vector3.UnitX * 3, "Full effect model/consumer clock/float delay/main target.");
        f.Set(101, a => a with { Position = Center + Vector3.UnitX * 15 });
        f.Now = Start.AddSeconds(30);
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Single().Position == p.Position && f.Rays().Single().Activation == p.Activation,
            "Prediction moved or expired.");
        f.E.Effect(2, 15048, 72, Start.AddSeconds(32), 999, Center + Vector3.UnitZ);
        Check(f.E.CarnivaleRayPredictions.Last().Position == Center + Vector3.UnitZ, "Missing MainTarget fallback.");
    }

    private static void LiveHandoff()
    {
        var f = new Fixture();
        f.E.Effect(2, 15048, 1, Start, 100);
        f.E.Effect(2, 15048, 2, Start, 100);
        f.E.Effect(2, 15048, 3, Start, 100);
        f.Actors.Add(new(5, 0x1E8D9B, Center + Vector3.UnitX * .123456f, Dead: true));
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 2 && f.Rays().Count(a => a.Id.Contains("/live/")) == 1
            && f.Rays().All(a => a.Navigate && a.Shape is CircleTemplate { Radius: 6 }),
            "Dead live zone or first matching event count.");
        Check(f.Rays().Single(a => a.Id.Contains("/live/")).Position.X != f.Actors.Last().Position.X, "Live position not quantized.");
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 1, "One event per entity update.");
        f.Set(5, a => a with { EventState = 7 });
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 1 && !f.Rays().Any(a => a.Id.Contains("/live/")),
            "State7 consumed or displayed.");
        Check(f.E.Hints(Center).Contains("GTFO from voidzone!"), "Voidzone text.");
    }

    private static void Background()
    {
        var f = new Fixture();
        var selected = new DungeonActor(9, 0x26F2, Center, InCombat: true);
        var e = new DungeonEncounter(796);
        e.Synchronize([selected, f.Actors[2]], Center, Start, playerId: 100);
        Check(e.Active?.BossOid == 0x26F2, "Background fixture selection.");
        List<DungeonActor> actors = [selected, .. f.Actors];
        var index = actors.FindIndex(a => a.Id == 2);
        actors[index] = actors[index] with { Cast = new(15048, Center, .3f, Start.AddSeconds(.1), Start.AddSeconds(4), 101) };
        e.Synchronize(actors, Center, Start.AddSeconds(.1), playerId: 100);
        e.Effect(2, 15048, 1, Start.AddSeconds(.2), 101);
        actors[0] = selected with { Dead = true };
        e.Synchronize(actors, Center, Start.AddSeconds(.3), playerId: 100);
        Check(e.Active?.BossOid == 0x2721 && e.CarnivaleRayCasts.Count() == 1 && e.CarnivaleRayPredictions.Count() == 1,
            "Background model lost before handoff.");
        e.Finish(2, 15048, Start.AddSeconds(.4));
        Check(!e.CarnivaleRayCasts.Any() && e.CarnivaleRayPredictions.Count() == 1, "Cancel deleted event prediction.");
    }

    private static void OrderAndZero()
    {
        var f = new Fixture();
        f.Actors.Add(new(5, 0x1E8D9B, Center + Vector3.UnitX * 1.5f, EventState: 7));
        f.Actors.Add(new(6, 0x1E8D9B, Center, EventState: 7));
        f.Sync();
        f.E.Effect(2, 15048, 0, Start.AddSeconds(1), 999, Center);
        f.E.Effect(2, 15048, 0, Start.AddSeconds(2), 999, Center + Vector3.UnitX * 3);
        Check(f.E.CarnivaleRayPredictions.Count() == 2, "Separate zero-sequence native events merged.");
        f.Set(5, a => a with { EventState = 0 });
        f.Set(6, a => a with { EventState = 0 });
        f.Actors.Reverse();
        f.Now = Start.AddSeconds(3);
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Single().Position == Center + Vector3.UnitX * 3,
            "Snapshot order replaced persistent live-zone order.");
        var actors = new List<DungeonActor>
        {
            new(1, 0x2720, Center, InCombat: true), new(2, 0x271F, Center, Dead: true, Name: "首右爪"),
            new(3, 0x271F, Center, Name: "次右爪"), new(100, 0, Center, PartySlot: 0, Player: true),
        };
        var e = new DungeonEncounter(796);
        e.Synchronize(actors, Center, Start, playerId: 100);
        actors.Reverse();
        e.Synchronize(actors, Center, Start.AddSeconds(1), playerId: 100);
        Check(!e.Hints(Center).Any(s => s.Contains("counters physical")), "Snapshot reorder changed first right claw.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

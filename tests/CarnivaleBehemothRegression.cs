using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleBehemothRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Behemoth default lifecycle preparation primary only markers small source arena", Lifecycle);
        run("Carnivale Behemoth maelstrom unquantized moving R8 hard obstacles include dead untargetable state7", Maelstrom);
        run("Carnivale Behemoth exact R6 R10 total60 cone locks real Finish no effect or timeout", Ordinary);
        run("Carnivale Behemoth meteor dynamic raw clock raid64 no R50 navigation no pull or auto skills", Meteor);
        run("Carnivale Behemoth late current raw bootstrap accepted once cancellation watermark retained", Bootstrap);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors = [new(1, 0x2732, Center), new(2, 0x233C, Center),
            new(100, 0, Center, PartySlot: 0, Player: true)];
        internal DateTime Now = Start;
        internal Fixture(bool combat = true)
        {
            Set(1, a => a with { InCombat = combat });
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: 100);
        internal void Cast(uint action)
        {
            Now = Now.AddSeconds(.01);
            Set(2, a => a with { Cast = new(action, Center + Vector3.UnitX, .3f, Now, Now.AddSeconds(4), 100) });
            Sync();
        }
        internal DungeonAmbientAoe[] Pools() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/maelstrom/")).ToArray();
    }
    private static void Lifecycle()
    {
        var f = new Fixture(false);
        Check(f.E.Active == null && f.E.CarnivalePrecombatHint!.Contains("40%"), "Preparation.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Active?.BossOid == 0x2732 && !f.E.Arena!.Contains(Center + Vector3.UnitX * 20)
            && f.E.CarnivalePrecombatHint == null, "Default pull or small arena.");
        f.Set(1, a => a with { InCombat = false });
        f.Sync();
        Check(f.E.Active != null, "Disengage reset.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null, "Primary HP0 did not complete.");
    }
    private static void Maelstrom()
    {
        var f = new Fixture();
        var pos = Center + new Vector3(1.12345f, 2.6f, 3.87654f);
        f.Actors.Add(new(3, 0x2733, pos, Rotation: .7f, Dead: true, Targetable: false, EventState: 7));
        f.Sync();
        var pool = f.Pools().Single();
        Check(pool.Obstacle && pool.Navigate && pool.Shape is CircleTemplate { Radius: 8 }
            && pool.Position == pos && pool.Rotation == .7f && f.E.Hints(Center).Contains("GTFO from voidzone!")
            && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1, "Pool source filters/geometry/markers.");
        f.E.Finish(3, 15259, f.Now);
        f.E.Effect(3, 15259, 1, f.Now);
        f.Set(3, a => a with { Position = pos + Vector3.UnitX * 2 });
        f.Sync();
        Check(f.Pools().Single().Position.X == pos.X + 2, "Pool froze or Finish removed entity.");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        Check(!f.Pools().Any(), "Destroyed pool retained.");
        f.Actors.Add(new(3, 0x2733, pos + Vector3.UnitZ));
        f.Sync();
        Check(f.Pools().Single().Position == pos + Vector3.UnitZ, "Same ID new pool wrong position.");
    }
    private static void Ordinary()
    {
        var rules = CarnivaleCatalog.Create().Single(d => d.BossOid == 0x2732).Casts;
        Check(rules.Single(r => r.Action == 15258).Shape is CircleTemplate { Radius: 6 }
            && rules.Single(r => r.Action == 15261).Shape is CircleTemplate { Radius: 10 }
            && rules.Single(r => r.Action == 15256).Shape is FanTemplate { Radius: 55.8f }
            && rules.All(r => r.Action is not (15259 or 15260 or 6499))
            && CarnivaleCatalog.IgnoredCast(0x2732, 15259), "Exact shapes/source scope.");
        foreach (var action in new uint[] { 15258, 15256, 15261 })
        {
            var f = new Fixture();
            f.Cast(action);
            var pending = f.E.OutputCasts.Single().Value;
            f.Set(2, a => a with { Position = Center + Vector3.UnitZ * 10, Rotation = 2 });
            f.Now = Start.AddSeconds(12);
            f.Sync();
            f.E.Effect(2, action, 1, f.Now);
            Check(f.E.OutputCasts.Single().Value == pending, "Ordinary lock/deadline/effect.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.OutputCasts.Any(), "Ordinary Finish.");
        }
    }
    private static void Meteor()
    {
        var f = new Fixture();
        f.Actors.Add(new(101, 0, Center, PartySlot: 63));
        f.Actors.Add(new(102, 0, Center, PartySlot: 1, Dead: true));
        f.Cast(15257);
        Check(f.E.PredictedDamage.Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 101 })
            && f.E.Hints(Center).Contains("Use Diamondback!") && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null)
            && !f.E.CarnivaleCastKnockbacks.Any() && !f.E.CarnivaleInterrupts.Any(), "Meteor invented AOEs/actions/pull.");
        var later = Start.AddSeconds(40);
        f.Set(2, a => a with { Cast = a.Cast! with { FinishAt = later } });
        f.Sync();
        Check(f.E.PredictedDamage.All(d => d.Activation == later), "Meteor current raw clock.");
        f.Now = Start.AddSeconds(60);
        f.Sync();
        Check(f.E.PredictedDamage.All(d => d.Activation == f.Now.AddSeconds((double).3f)), "Meteor NPC remaining floor.");
        f.E.Finish(2, 15257, f.Now);
        Check(!f.E.PredictedDamage.Any(), "Meteor Finish.");
    }
    private static void Bootstrap()
    {
        var f = new Fixture(false);
        f.Cast(15258);
        f.Now = Start.AddSeconds(10);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        var pending = f.E.OutputCasts.Single().Value;
        Check(pending.FinishAt == f.Now.AddSeconds((double).3f), "Late previously unclaimed raw clock.");
        f.Now = f.Now.AddSeconds(1);
        f.Sync();
        Check(ReferenceEquals(f.E.OutputCasts.Single().Value, pending), "Repeated raw refreshed component clock.");
        f.E.Finish(2, 15258, f.Now);
        f.Sync();
        Check(!f.E.OutputCasts.Any(), "Consumed raw revived through bootstrap admission.");
        var canceled = new Fixture(false);
        canceled.Cast(15258);
        canceled.E.Finish(2, 15258, canceled.Now.AddSeconds(.1));
        canceled.Now = Start.AddSeconds(10);
        canceled.Set(1, a => a with { InCombat = true });
        canceled.Sync();
        Check(!canceled.E.OutputCasts.Any(), "Pre-activation cancellation revived unchanged raw.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

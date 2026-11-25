using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleGogoRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Gogo two default lifecycles small arena act1 preparation only primary marker", Lifecycle);
        run("Carnivale Gogo Sap actual23101 and23105 Protean50 total30 FireBlast70point5 ordinary Finish", Ordinary);
        run("Carnivale Gogo two independent Meteor groups R8 and16 any Finish no helper20056 consumption", Groups);
        run("Carnivale Gogo Fire creates global delayed Blizzard 5point1 overwrite any Blizzard Finish only", Blizzard);
        run("Carnivale Gogo Icestorm 0point9 delayed raidwide no cancel timeout clear known native effect", Icestorm);
        run("Carnivale Gogo Pyretic64 same frame raw loss cast actor order death perspective", Pyretic);
        run("Carnivale Gogo Thunder cast native effect live three paths R6 state7 source gate", Thunder);
        run("Carnivale Gogo Maelstrom raw R8 hard obstacles dead untargetable state7 included", Maelstrom);
        run("Carnivale Gogo Mimic raw2450 1797 cleanse1107 268 pure hints no new restriction", Hints);
        run("Carnivale Gogo FlareHoly MeteorBig alive64 current raw clocks only no defense prediction act1", Damage);
        run("Carnivale Gogo late bootstrap current cast retained NPC delay canceled raw barrier", Bootstrap);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Viewer = 100;
        internal Fixture(uint oid = 0x30F7, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat), new(2, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 5, Player: true, PartySlot: 0),
                new(101, 0, Center + Vector3.UnitX * 3, Player: true, PartySlot: 1)];
            Sync();
        }
        internal CarnivaleGogoState State => E.CarnivaleGogo!;
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.FirstOrDefault(a => a.Id == Viewer)?.Position, Now, playerId: Viewer);
        internal void Cast(uint action, ulong source = 2, double seconds = 4, float rotation = 0)
        {
            Now = Now.AddSeconds(.01);
            if (!Actors.Any(a => a.Id == source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, Center, rotation, Now, Now.AddSeconds(seconds), 100) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
        internal string[] Hints() => E.Hints(Actors.Single(a => a.Id == Viewer).Position).ToArray();
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x30F5, 0x30F7 })
        {
            var f = new Fixture(oid, false);
            Check((f.E.CarnivalePrecombatHint != null) == (oid == 0x30F5)
                && f.Aoes("carnivale/precombat/arena").Length == 1, "Preparation.");
            f.Actors.Add(new(3, 0x30F6, Center));
            f.Actors.Add(new(4, 0x30F9, Center));
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.Aoes("carnivale/enemy/").Length == 1 && !f.E.Arena!.Contains(Center + Vector3.UnitX * 17), "Arena/markers.");
            f.Set(1, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active == null, "Default primary complete.");
        }
    }

    private static void Ordinary()
    {
        foreach (var (oid, action) in new[] { (0x30F5u, 23112u), (0x30F5u, 23110u), (0x30F7u, 23121u) })
        {
            var f = new Fixture(oid);
            f.Cast(action, rotation: MathF.PI / 2);
            var cast = f.E.OutputCasts.Single().Value;
            Check(action switch
            {
                23112 => cast.Rule.Shape is FanTemplate { Radius: 50 } cone && MathF.Abs(cone.AngleRadians - MathF.PI / 6) < .0001f,
                23110 => cast.Rule.Shape is RayRectangleTemplate { Length: 70.5f, MinLocalX: -2, MaxLocalX: 2 },
                _ => cast.Rule.Shape is CircleTemplate { Radius: 5 },
            }, "Actual ordinary geometry.");
            if (action != 23121) Check(cast.Rotation == MathF.PI / 2, "Locked facing.");
            f.Now = Start.AddSeconds(30);
            f.Sync();
            f.E.Effect(2, action, 1, f.Now);
            Check(f.E.OutputCasts.Any(), "No effect/timeout removal.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.OutputCasts.Any(), "Real Finish.");
        }
        var sap = new Fixture(0x30F5);
        sap.Cast(23101);
        sap.Cast(23105, 3);
        Check(sap.Aoes("carnivale/sap/").Length == 2 && sap.Aoes("carnivale/sap/").All(a => a.Shape is CircleTemplate { Radius: 8 }), "Sap actual actions.");
        sap.E.Finish(2, 60000, sap.Now);
        Check(sap.Aoes("carnivale/sap/").Length == 1, "Any Finish.");
    }

    private static void Groups()
    {
        var f = new Fixture();
        f.Cast(23131);
        f.Cast(23122, 3);
        f.Cast(20055, 4);
        f.Cast(23123, 5);
        f.Cast(23129, 6);
        Check(f.Aoes("carnivale/sap/").Length == 3 && f.Aoes("carnivale/gogo/meteor/").Length == 2
            && f.Aoes("carnivale/gogo/meteor/").All(a => a.Shape is CircleTemplate { Radius: 16 }), "Independent meteor groups.");
        f.E.Finish(5, 60000, f.Now);
        Check(f.State.LargeMeteors.Count == 1 && f.Aoes("carnivale/sap/").Length == 3, "Large arbitrary Finish scope.");
        f.Cast(20056, 7);
        Check(f.Aoes("carnivale/sap/").Length == 3 && f.State.LargeMeteors.Count == 1, "Unconsumed Helper action.");
    }

    private static void Blizzard()
    {
        var f = new Fixture();
        f.Cast(23117);
        var first = f.State.Blizzard!;
        Check(f.Aoes("carnivale/gogo/blizzard").Single().Activation == first.FinishAt.AddSeconds(5.1d), "Fire-based Blizzard time.");
        f.E.Finish(2, 23117, f.Now);
        Check(f.State.Blizzard == first && f.State.Pyretic.Requirements.All(c => c != null), "Fire cancellation must retain both.");
        f.Cast(23117, 3);
        Check(f.State.Blizzard!.Source == 3, "Global overwrite.");
        f.Cast(23118, 4);
        f.E.Effect(4, 23118, 1, f.Now);
        f.Now = Start.AddSeconds(60);
        f.Sync();
        Check(f.State.Blizzard != null, "Effect/timeout clear.");
        f.E.Finish(4, 23118, f.Now);
        Check(f.State.Blizzard == null && f.State.Pyretic.Requirements.All(c => c != null), "Blizzard Finish only clears delayed AOE.");
    }

    private static void Icestorm()
    {
        var f = new Fixture();
        f.Cast(23126);
        var ice = f.State.Icestorm!;
        Check(f.E.PredictedDamage.Count() == 2 && f.E.PredictedDamage.All(d => d.Activation == ice.FinishAt.AddSeconds(.9d)), "Delayed damage.");
        f.E.Finish(2, 23126, f.Now);
        f.Now = Start.AddSeconds(60);
        f.Sync();
        Check(f.State.Icestorm == ice && f.Hints().Contains("Raidwide + Frostbite + Heavy"), "Cancel/timeout retained.");
        f.E.Effect(999, 23127, 1, f.Now);
        Check(f.State.Icestorm != null, "Unknown source cannot clear.");
        f.E.Effect(2, 23127, 2, f.Now);
        Check(f.State.Icestorm == null && !f.E.PredictedDamage.Any(), "Known native effect clears.");
    }

    private static void Pyretic()
    {
        foreach (var lossFirst in new[] { false, true })
        {
            var f = new Fixture();
            f.Set(101, a => a with { StatusSlots = [new(960, 0, 1, Start)] });
            f.Sync();
            if (lossFirst)
            {
                var loss = f.Actors.Single(a => a.Id == 101);
                f.Actors.Remove(loss);
                f.Actors.Insert(1, loss);
            }
            f.Set(101, a => a with { StatusSlots = [] });
            f.Cast(23117);
            Check(f.State.Pyretic.Requirements.All(c => (c != null) == lossFirst), "Per-actor Start/Lose.");
        }
    }

    private static void Thunder()
    {
        var f = new Fixture();
        f.Cast(23119);
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 3 });
        f.Sync();
        f.E.Effect(999, 23119, 1, f.Now, target: 100);
        Check(!f.E.CarnivaleRayPredictions.Any(), "Source gate.");
        f.E.Effect(2, 23119, 2, f.Now, target: 100);
        Check(f.E.CarnivaleRayCasts.Count() == 1 && f.E.CarnivaleRayPredictions.Single().Activation == f.Now.AddSeconds(.8d), "Three path prediction.");
        f.E.Finish(2, 23119, f.Now);
        f.Set(2, a => a with { Cast = null });
        f.Actors.Add(new(3, 0x1E9684, Center + Vector3.UnitX * 3, Dead: true, Targetable: false));
        f.Sync();
        Check(!f.E.CarnivaleRayPredictions.Any() && f.Aoes("carnivale/ray/live/").Length == 1, "Live confirmation.");
        f.Set(3, a => a with { EventState = 7 });
        f.Sync();
        Check(!f.Aoes("carnivale/ray/").Any(), "State7.");
    }

    private static void Maelstrom()
    {
        var f = new Fixture();
        var raw = Center + new Vector3(1.12345f, 0, 2.37654f);
        f.Actors.Add(new(3, 0x30F9, raw, Dead: true, Targetable: false, EventState: 7));
        f.Sync();
        var pool = f.Aoes("carnivale/gogo/maelstrom/").Single();
        Check(pool.Obstacle && pool.Shape is CircleTemplate { Radius: 8 } && pool.Position == raw, "Raw obstacle all states.");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        Check(!f.Aoes("carnivale/gogo/maelstrom/").Any(), "Destroy removes obstacle.");
    }

    private static void Hints()
    {
        var f = new Fixture(0x30F5);
        f.Set(1, a => a with { StatusSlots = [new(2450, 0, 1, Start), new(1797, 0, 1, Start)] });
        f.Sync();
        Check(f.Hints().Contains("Do no damage!") && f.Hints().Contains("Dispel buff!"), "Raw primary statuses.");
        foreach (var action in new uint[] { 23097, 23113, 23115, 23098, 23100, 23102, 23103, 23114 })
        {
            f.Cast(action);
            Check(!f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null) && !f.E.PredictedDamage.Any(), "Pure hints no fabricated damage.");
        }
        Check(f.E.CarnivaleInterrupts.Single().Cast.Rule.Action == 23114, "Interrupt intent.");
        var second = new Fixture();
        second.Set(100, a => a with { StatusSlots = [new(1107, 0, 1, Start)] });
        second.Sync();
        Check(second.Hints().Contains("Cleanse debuffs!"), "Raw cleanse.");
    }

    private static void Damage()
    {
        foreach (var action in new uint[] { 23128, 23130, 23124 })
        {
            var f = new Fixture();
            f.Set(101, a => a with { Dead = true });
            f.Cast(action);
            Check(f.E.PredictedDamage.Single().TargetId == 100, "Alive mask.");
            f.Set(2, a => a with { Cast = a.Cast! with { FinishAt = f.Now.AddSeconds(5) } });
            f.Sync();
            Check(f.E.PredictedDamage.Single().Activation == f.Now.AddSeconds(5), "Current raw clock.");
        }
    }

    private static void Bootstrap()
    {
        var f = new Fixture(combat: false);
        f.Cast(23117);
        f.Now = Start.AddSeconds(30);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.State.Blizzard!.FinishAt == f.Now.AddSeconds((double).3f)
            && f.State.Pyretic.Requirements.All(c => c != null), "Late activation NPC delay.");
        var canceled = new Fixture(combat: false);
        canceled.Cast(23117);
        canceled.E.Finish(2, 23117, canceled.Now);
        canceled.Set(1, a => a with { InCombat = true });
        canceled.Sync();
        Check(canceled.State.Blizzard == null && canceled.State.Pyretic.Requirements.All(c => c == null), "Canceled raw must not bootstrap.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

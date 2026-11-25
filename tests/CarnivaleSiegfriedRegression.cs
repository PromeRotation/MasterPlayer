using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleSiegfriedRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Siegfried three default lifecycles small polygon preparation only act1 clone orange no priority", Lifecycle);
        run("Carnivale Siegfried Bomb Create quantized birth clock8point4 Rubber20 locks raw primary6point3", Bombs);
        run("Carnivale Siegfried created predictions freeze dead removed sameID latest KB real Finish clears globally", Identity);
        run("Carnivale Siegfried same frame Finish and Bomb Create preserve input order and primary position", CreateOrder);
        run("Carnivale Siegfried Boulder CreateR10 clock effect clear only native source no timeout", Boulders);
        run("Carnivale Siegfried three shared groups independent any Finish threshold99 and8 one second strict prefix", Groups);
        run("Carnivale Siegfried ordinary Swift R4 ring8to20 Shatter5 observed Finish no effect", Ordinary);
        run("Carnivale Siegfried Sparksteel three paths dynamic target native processing point8 repeated live match state7", Voidzones);
        run("Carnivale Siegfried Swift10 chained source records Rubber20 independent current hazard subsets immunity", Knockbacks);
        run("Carnivale Siegfried reflect2166 bind564 occupied expired slots pure cast hints no raidwide", Hints);
        run("Carnivale Siegfried late activation replays Create current processing clock no historical model", Bootstrap);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x2C6C, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat, Name: "Siegfried"), new(2, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 5, Player: true, PartySlot: 0)];
            Sync();
        }
        internal CarnivaleSiegfriedState State => E.CarnivaleSiegfried!;
        internal DungeonActor Player => Actors.Single(a => a.Id == 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Player.Position, Now, playerId: 100);
        internal void Born(ulong id, uint oid, Vector3 position) { Actors.Add(new(id, oid, position)); Sync(); }
        internal void Cast(uint action, ulong source = 2, double seconds = 4, float rotation = 0)
        {
            Now = Now.AddSeconds(.01);
            if (!Actors.Any(a => a.Id == source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, Center, rotation, Now, Now.AddSeconds(seconds), 100) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix = "carnivale/siegfried/") => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x2C67, 0x2C6A, 0x2C6C })
        {
            var f = new Fixture(oid, false);
            Check((f.E.CarnivalePrecombatHint != null) == (oid == 0x2C67)
                && f.E.AmbientAoes().Any(a => a.Id == "carnivale/precombat/arena"), "Only act1 constructor Hints; all arenas visible.");
            f.Actors.AddRange(new uint[] { 0x2C6E, 0x2C6D, 0x2C6F }.Select((add, i) => new DungeonActor((ulong)(10 + i), add, Center)));
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == (oid == 0x2C6C ? 4 : 1)
                && !f.E.Arena!.Contains(Center + Vector3.UnitX * 17), "Markers/small polygon.");
            if (oid == 0x2C6C)
                Check(f.E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/enemy/") && !a.Id.EndsWith("/1"))
                    .All(a => a.Color == new Vector4(1, 128f / 255, 0, 1)), "Clone orange.");
            f.Set(1, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active == null, "Clones kept phase alive.");
        }
    }

    private static void Bombs()
    {
        var f = new Fixture(0x2C67);
        var raw = Center + new Vector3(1.12345f, 0, 2.37654f);
        f.Now = Start.AddSeconds(2);
        f.Born(3, 0x2C68, raw);
        var bomb = f.State.Bombs.Single();
        var kb = f.State.RubberBullet!;
        Check(bomb.Radius == 8 && bomb.Position != raw && bomb.CreatedAt == f.Now
            && bomb.Activation == f.Now.AddSeconds(8.4d) && kb is { Distance: 20 } && kb.Origin == Center
            && kb.Activation == f.Now.AddSeconds(6.3d) && ReferenceEquals(bomb.Trigger, kb.Trigger), "Birth model.");
        f.Set(1, a => a with { Position = Center + Vector3.UnitX * 2 });
        f.Set(3, a => a with { Position = Center + Vector3.UnitX * 10, Dead = true });
        f.Sync();
        Check(f.State.RubberBullet!.Origin == Center && f.State.Bombs.Single() == bomb, "Birth lock followed movement.");
        f.Cast(18847);
        Check(f.State.RubberBullet == kb, "Rubber Start must not create new KB.");
        f.E.Effect(2, 18847, 1, f.Now);
        Check(f.State.RubberBullet == kb, "Rubber Effect clear.");
        f.E.Finish(2, 18847, f.Now);
        Check(f.State.RubberBullet == null && f.State.Bombs.Count == 1, "Rubber Finish scope.");
    }

    private static void Identity()
    {
        var f = new Fixture();
        f.Born(3, 0x2C68, Center);
        var old = f.State.Bombs.Single().Trigger;
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        f.Born(3, 0x2C68, Center + Vector3.UnitX * 5);
        Check(f.State.Bombs.Count == 2 && old.Removed
            && !ReferenceEquals(f.State.Bombs[0].Trigger, f.State.Bombs[1].Trigger), "Destroyed prediction/new generation.");
        f.Cast(18888, 3);
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        Check(f.State.Bombs.Count == 0, "Destroy active18888 must synthesize Finish and clear all.");
    }

    private static void CreateOrder()
    {
        foreach (var birthFirst in new[] { false, true })
        {
            var f = new Fixture(0x2C67);
            f.Born(3, 0x2C68, Center);
            f.Cast(18847, 1);
            f.Set(1, a => a with { Cast = null, Position = Center + Vector3.UnitX * 2 });
            f.Actors.Insert(birthFirst ? 0 : f.Actors.Count, new(4, 0x2C68, Center));
            f.Sync();
            Check((f.State.RubberBullet != null) == !birthFirst, "18847 Finish/Create order.");
            if (!birthFirst) Check(f.State.RubberBullet!.Origin == Center + Vector3.UnitX * 2, "Primary update before birth.");

            var g = new Fixture(0x2C67);
            g.Born(3, 0x2C68, Center);
            g.Cast(18888, 3);
            g.Set(3, a => a with { Cast = null });
            g.Actors.Insert(birthFirst ? 0 : g.Actors.Count, new(4, 0x2C68, Center));
            g.Sync();
            Check(g.State.Bombs.Count == (birthFirst ? 0 : 1), "18888 Finish/Create order.");
        }
        var beforePrimary = new Fixture(0x2C67);
        beforePrimary.Set(1, a => a with { Position = Center + Vector3.UnitX * 2 });
        beforePrimary.Actors.Insert(0, new(4, 0x2C68, Center));
        beforePrimary.Sync();
        Check(beforePrimary.State.RubberBullet!.Origin == Center, "Birth before primary update must lock previous primary position.");
    }

    private static void Boulders()
    {
        var f = new Fixture(0x2C6A);
        f.Born(3, 0x2CC6, Center);
        f.Born(4, 0x2CC6, Center + Vector3.UnitX * 2);
        Check(f.State.Boulders.Count == 2 && f.State.Boulders.All(b => b.Radius == 10 && b.Activation == Start.AddSeconds(8.4d)), "Boulder creation.");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Now = Start.AddSeconds(30);
        f.Sync();
        f.E.Effect(999, 18986, 1, f.Now);
        Check(f.State.Boulders.Count == 2, "Unknown source/timeout/destroy cleared.");
        f.E.Effect(2, 18986, 2, f.Now);
        Check(f.State.Boulders.Count == 0, "Known arbitrary source effect did not clear all.");
    }

    private static void Groups()
    {
        var f = new Fixture();
        f.Cast(18836, 2, 20);
        f.Cast(18837, 3, 5);
        f.Cast(18838, 4, 20, MathF.PI / 2);
        f.Cast(18839, 5, 5);
        Check(f.State.Hyperdrives.Count == 2 && f.State.Torches.Count == 2
            && f.Aoes("carnivale/siegfried/torch/").All(a => a.Shape is FanTemplate { Radius: 34 }),
            "Independent Hyper/Torch groups.");
        f.E.Finish(4, 60000, f.Now);
        Check(f.State.Torches.Count == 1 && f.State.Hyperdrives.Count == 2, "Arbitrary Finish only source first item per group.");
        f.Cast(18841, 6, 10);
        var first = f.State.Sparksteels[0].Cast.FinishAt;
        f.Cast(18897, 7, 20);
        f.Cast(18841, 8, 10);
        Check(f.Aoes("carnivale/siegfried/sparksteel/").Length == 1, "Window must stop at first late inserted item.");
        f.E.Finish(7, 60000, f.Now);
        Check(f.Aoes("carnivale/siegfried/sparksteel/").Length == 2, "Prefix after late removal.");
        f.Born(9, 0x233C, Center);
        f.E.StartCast(9, new(18841, Center, 0, f.Now, first.AddSeconds(1)), f.Now);
        Check(f.Aoes("carnivale/siegfried/sparksteel/").Length == 2, "Activation at exactly one second must be excluded.");
        for (ulong id = 10; id < 15; ++id) f.Cast(18897, id, 30 - id);
        Check(f.State.Sparksteels.Count == 8 && f.State.Sparksteels.Select(a => a.Cast.FinishAt)
            .SequenceEqual(f.State.Sparksteels.Select(a => a.Cast.FinishAt).Order()), "Eight threshold sort.");
    }

    private static void Ordinary()
    {
        foreach (var action in new uint[] { 18843, 18844, 19027 })
        {
            var f = new Fixture(0x2C6A);
            f.Cast(action);
            var cast = f.E.OutputCasts.Single().Value;
            Check(action switch
            {
                18843 => cast.Rule.Shape is CircleTemplate { Radius: 4 },
                18844 => cast.Rule.Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 20 },
                _ => cast.Rule.Shape is CircleTemplate { Radius: 5 },
            }, "Source ordinary geometry.");
            f.Now = Start.AddSeconds(30);
            f.Sync();
            f.E.Effect(2, action, 1, f.Now);
            Check(f.E.OutputCasts.Any(), "Effect/timeout removed ordinary.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.OutputCasts.Any(), "Finish ordinary.");
        }
    }

    private static void Voidzones()
    {
        var f = new Fixture(0x2C6A);
        f.Cast(18840);
        Check(f.E.CarnivaleRayCasts.Count() == 1, "Sparksteel cast path.");
        f.E.Effect(999, 18840, 9, f.Now, target: 100);
        Check(!f.E.CarnivaleRayPredictions.Any(), "Missing source must not generate permanent prediction.");
        f.Set(100, a => a with { Position = Center + Vector3.UnitX * 3 });
        f.Sync();
        Check(f.Aoes("carnivale/ray/cast/").Single().Position == f.Player.Position, "Dynamic target path.");
        f.E.Effect(2, 18840, 1, f.Now, target: 100, location: Center - Vector3.UnitX * 8);
        f.E.Effect(2, 18840, 2, f.Now, target: 100);
        Check(f.E.CarnivaleRayPredictions.Count() == 2 && f.E.CarnivaleRayPredictions.All(p =>
            p.Position == f.Player.Position && p.Activation == f.Now.AddSeconds(.8d)), "Native MainTarget current processing clock.");
        f.E.Finish(2, 18840, f.Now);
        f.Set(2, a => a with { Cast = null });
        f.Born(3, 0x1E8D9B, f.Player.Position);
        Check(f.E.CarnivaleRayPredictions.Count() == 1, "One live entity consumes first prediction.");
        f.Sync();
        Check(!f.E.CarnivaleRayPredictions.Any(), "Existing live entity must match next update.");
        f.Set(3, a => a with { Dead = true, Targetable = false });
        f.Sync();
        Check(f.Aoes("carnivale/ray/live/").Length == 1 && !f.Aoes("carnivale/ray/live/").Single().Obstacle, "Live pool is danger not hard obstacle.");
        f.Set(3, a => a with { EventState = 7 });
        f.Sync();
        Check(f.Aoes("carnivale/ray/live/").Length == 0, "State7 pool.");
    }

    private static void Knockbacks()
    {
        var f = new Fixture();
        f.Cast(18842);
        var move = f.E.CarnivaleCastKnockbackMovements(f.Player, 18842).Single();
        Check(move.Source is { Distance: 10, StopAtWall: false, Forward: false } && Near(move.To, Center + Vector3.UnitZ * 15), "Swift10.");
        f.Cast(18843, 3);
        f.Set(3, a => a with { Cast = a.Cast! with { Location = Center + Vector3.UnitZ * 15 } });
        f.Sync();
        Check(f.E.Hints(f.Player.Position).Contains("About to be knocked into danger!"), "Swift checks complete R4 caster list.");
        f.Born(4, 0x2C68, Center);
        Check(Near(f.E.CarnivaleRubberBulletLanding(f.Player)!.Value, Center + Vector3.UnitZ * 25), "Rubber independently starts from player.");
        var at = f.State.RubberBullet!.Activation;
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: at);
        Check(f.E.CarnivaleRubberBulletLanding(f.Player) != null, "Equal immunity.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: at.AddTicks(1));
        Check(f.E.CarnivaleRubberBulletLanding(f.Player) == null && f.State.Bombs.Count == 1, "Immune KB must retain bomb hazard.");
    }

    private static void Hints()
    {
        var f = new Fixture(0x2C67);
        f.Set(1, a => a with { StatusSlots = [new(2166, 0, 1, Start)] });
        f.Set(100, a => a with { StatusSlots = [new(564, 0, 1, Start)] });
        f.Sync();
        Check(f.E.Hints(f.Player.Position).Contains("Siegfried will reflect all magic damage!")
            && f.E.Hints(f.Player.Position).Contains("You were bound! Cleanse it with Exuviation."), "Occupied expired slots.");
        foreach (var action in new uint[] { 18890, 18846 })
        {
            f.Cast(action);
            Check(!f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null) && !f.E.PredictedDamage.Any(), "Pure CastHints.");
        }
    }

    private static void Bootstrap()
    {
        var f = new Fixture(0x2C6C, false);
        f.Born(3, 0x2C68, Center);
        f.Born(4, 0x2CC6, Center);
        f.Now = Start.AddSeconds(30);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.State.Bombs.Single().Activation == f.Now.AddSeconds(8.4d)
            && f.State.Boulders.Single().Activation == f.Now.AddSeconds(8.4d)
            && f.State.RubberBullet!.Activation == f.Now.AddSeconds(6.3d), "Activation Create replay must use processing clock.");
    }

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleGoldorRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Goldor two default lifecycles preparation first only orange adds first only", Lifecycle);
        run("Carnivale Goldor ordinary60rect Burn10 Thunder6 Cross100 half3point5 Swing12 Fire8", Ordinary);
        run("Carnivale Goldor first Fire shared group any Finish second Fire ordinary scoped Finish", Fire);
        run("Carnivale Goldor Quake only current stage cancel advances exact processing1point5 no timeout", Quake);
        run("Carnivale Goldor Quake first match same position independent source axis1 wrong order ignored", QuakeMatching);
        run("Carnivale Goldor Beam single analytic40 total120 count dead actors included", BeamSingle);
        run("Carnivale Goldor Beam four edge2 fold parity two coverage safe four danger fifth no rebuild any Finish", BeamCombined);
        run("Carnivale Goldor Beam raw count at actor update before later Marionette created", BeamOrder);
        run("Carnivale Goldor two global delayed raidwides exact float double cancel survives targetless known effect", Delayed);
        run("Carnivale Goldor Away10 independent Pull30 clamped strict immunity no radius gate", Knockbacks);
        run("Carnivale Goldor Aero risk only Burn first Cross second Rush Pull only arena", LandingRisk);
        run("Carnivale Goldor raw immunity3621 and3779 240 hints no new action restriction", Hints);
        run("Carnivale Goldor late activation Quake Beam delayed clocks and interrupt bootstrap", Bootstrap);
        run("Carnivale Goldor Rupture first caster live countdown Finish promotion and ordinary danger hint", Rupture);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x3FA7, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat, Name: "Goldor"), new(2, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 5, Player: true, PartySlot: 0)];
            Sync();
        }
        internal CarnivaleGoldorState State => E.CarnivaleGoldor!;
        internal DungeonActor Player => Actors.Single(a => a.Id == 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Player.Position, Now, playerId: 100);
        internal void Cast(uint action, ulong source = 2, double seconds = 4, float rotation = 0, Vector3? position = null)
        {
            Now = Now.AddSeconds(.01);
            if (!Actors.Any(a => a.Id == source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, position ?? Center, rotation, Now, Now.AddSeconds(seconds), 100) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
        internal string[] Hints() => E.Hints(Player.Position).ToArray();
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x3FA5, 0x3FA7 })
        {
            var f = new Fixture(oid, false);
            Check((f.E.CarnivalePrecombatHint != null) == (oid == 0x3FA5) && f.Aoes("carnivale/precombat/arena").Length == 1, "Preparation.");
            f.Actors.AddRange(new uint[] { 0x3FA6, 0x3FAB, 0x3FAC, 0x3FA8, 0x3FA9, 0x3FAA }.Select((add, i) => new DungeonActor((ulong)(10 + i), add, Center)));
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            Check(f.Aoes("carnivale/enemy/").Length == (oid == 0x3FA5 ? 3 : 1) && !f.E.Arena!.Contains(Center + Vector3.UnitX * 17), "Markers/small arena.");
            if (oid == 0x3FA5) Check(f.Aoes("carnivale/enemy/").Where(a => !a.Id.EndsWith("/1")).All(a => a.Color == new Vector4(1, 128f / 255, 0, 1)), "Orange objects.");
            f.Set(1, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active == null, "Default primary completion ignores adds.");
        }
    }

    private static void Ordinary()
    {
        foreach (var (oid, action) in new[] { (0x3FA5u, 34450u), (0x3FA5u, 34446u), (0x3FA5u, 34455u),
            (0x3FA7u, 34465u), (0x3FA7u, 34467u), (0x3FA7u, 34449u) })
        {
            var f = new Fixture(oid);
            f.Cast(action, rotation: .7f);
            var cast = f.E.OutputCasts.Single().Value;
            Check(action switch
            {
                34450 => cast.Rule.Shape is RayRectangleTemplate { Length: 60, MinLocalX: -5, MaxLocalX: 5 } && cast.Rotation == .7f,
                34446 => cast.Rule.Shape is CircleTemplate { Radius: 10 },
                34455 => cast.Rule.Shape is CircleTemplate { Radius: 6 },
                34465 => cast.Rule.Shape is DungeonCrossTemplate { Length: 100, HalfWidth: 3.5f } && cast.Rotation == .7f,
                34467 => cast.Rule.Shape is CircleTemplate { Radius: 12 },
                _ => cast.Rule.Shape is CircleTemplate { Radius: 8 },
            }, "Actual geometry.");
            f.Now = Start.AddSeconds(30);
            f.Sync();
            f.E.Effect(2, action, 1, f.Now);
            Check(f.E.OutputCasts.Any(), "Effect/timeout.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.OutputCasts.Any(), "Real Finish.");
        }
    }

    private static void Fire()
    {
        var first = new Fixture(0x3FA5);
        first.Cast(34448);
        first.Cast(34449, 3);
        Check(first.Aoes("carnivale/sap/").Length == 2, "First shared list.");
        first.E.Finish(3, 60000, first.Now);
        Check(first.Aoes("carnivale/sap/").Length == 1, "First34449 any Finish.");
        var second = new Fixture();
        second.Cast(34449);
        second.E.Finish(2, 60000, second.Now);
        Check(second.E.OutputCasts.Count() == 1, "Second SimpleAOEs not group Finish.");
    }

    private static void Quake()
    {
        var f = new Fixture();
        f.Cast(34457);
        Check(f.State.Quakes.Count == 1 && f.Aoes("carnivale/goldor/quake/").Single().Shape is CircleTemplate { Radius: 10 }, "Current first circle.");
        f.E.Finish(2, 34457, f.Now);
        Check(f.State.Quakes.Single().Order == 1 && f.State.Quakes.Single().Activation == f.Now.AddSeconds(1.5d)
            && f.Aoes("carnivale/goldor/quake/").Single().Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 20 }, "Cancel advances.");
        f.Set(2, a => a with { Cast = null });
        f.Now = Start.AddSeconds(30);
        f.Sync();
        Check(f.State.Quakes.Single().Order == 1, "No timeout.");
        f.Cast(34458, 3);
        f.E.Finish(3, 34458, f.Now);
        Check(f.State.Quakes.Single().Order == 2 && f.State.Quakes.Single().Activation == f.Now.AddSeconds(1.5d), "Independent source finish.");
        f.Cast(34459, 4);
        f.E.Finish(4, 34459, f.Now);
        Check(f.State.Quakes.Count == 0, "Third Finish removes.");
    }

    private static void QuakeMatching()
    {
        var f = new Fixture();
        f.Cast(34457);
        f.Cast(34457, 3);
        f.E.Finish(2, 34457, f.Now);
        f.Cast(34458, 4, position: Center + new Vector3(1, 0, 1));
        f.E.Finish(4, 34458, f.Now);
        Check(f.State.Quakes[0].Order == 2 && f.State.Quakes[1].Order == 0, "Axis1 matching and first eligible.");
        f.Cast(34459, 5, position: Center + Vector3.UnitX * 1.01f);
        f.E.Finish(5, 34459, f.Now);
        Check(f.State.Quakes.Count == 2, "Wrong location cannot consume.");
    }

    private static void BeamSingle()
    {
        var f = new Fixture();
        f.Actors.Add(new(3, 0x3FA8, Center, Dead: true, Targetable: false));
        f.Sync();
        f.Cast(34464, rotation: .7f);
        Check(f.State.BeamOutput.Length == 1 && f.Aoes("carnivale/goldor/beam").Single().Shape is FanTemplate { Radius: 40 }
            && f.State.BeamOutput[0].Rotation == .7f, "Exactly one raw reference includes dead.");
        f.E.Finish(2, 34464, f.Now);
        Check(f.State.BeamInputs.Count == 0 && f.State.BeamOutput.Length == 0, "Any Beam Finish.");
    }

    private static void BeamCombined()
    {
        var f = new Fixture();
        for (ulong id = 3; id < 7; ++id) f.Actors.Add(new(id, 0x3FA8, Center));
        f.Sync();
        f.Cast(34464, 3);
        f.Cast(34464, 4);
        f.Cast(34464, 5, position: Center + Vector3.UnitX * 100);
        Check(f.State.BeamOutput.Length == 0, "First three no output.");
        f.Cast(34464, 6, position: Center + Vector3.UnitX * 200);
        var aoe = f.Aoes("carnivale/goldor/beam").Single();
        var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
        Check(!shape.Contains(Center + Vector3.UnitZ * 10, 0) && shape.Contains(Center + Vector3.UnitX * 100 + Vector3.UnitZ * 10, 0), "Two coverage safe one dangerous.");
        var previous = f.State.BeamOutput;
        f.Cast(34464, 7);
        Check(ReferenceEquals(previous, f.State.BeamOutput), "Fifth must not rebuild.");
        f.E.Finish(7, 34464, f.Now);
        Check(!f.Aoes("carnivale/goldor/beam").Any(), "Any source Finish clears whole group.");
        var all = new Fixture();
        for (ulong id = 3; id < 7; ++id) all.Actors.Add(new(id, 0x3FA8, Center));
        all.Sync();
        for (ulong id = 3; id < 7; ++id) all.Cast(34464, id);
        var four = all.Aoes("carnivale/goldor/beam").Single();
        Check(four.Shape.Resolve(four.Position, four.Rotation, null).Contains(Center + Vector3.UnitZ * 10, 0), "Four intersection dangerous.");
    }

    private static void BeamOrder()
    {
        foreach (var late in new[] { false, true })
        {
            var f = new Fixture();
            f.Actors.Add(new(3, 0x3FA8, Center));
            f.Sync();
            f.Actors.Insert(late ? f.Actors.Count : 0, new(4, 0x3FA8, Center));
            f.Cast(34464);
            Check(f.State.BeamOutput.Length == (late ? 1 : 0), "Enemy count at source actor update.");
        }
    }

    private static void Delayed()
    {
        foreach (var oid in new uint[] { 0x3FA5, 0x3FA7 })
        {
            var f = new Fixture(oid);
            f.Cast(34451);
            f.Cast(34453, 3);
            var delay = oid == 0x3FA5 ? (double).8f : .8d;
            Check(f.E.PredictedDamage.Select(d => d.Activation).Order().SequenceEqual(
                new[] { f.State.Gravity!.FinishAt.AddSeconds(delay), f.State.Thunder!.FinishAt.AddSeconds(delay) }.Order()), "Float/double exact.");
            f.E.Finish(2, 34451, f.Now);
            f.Now = Start.AddSeconds(30);
            f.Sync();
            f.E.Effect(999, 34452, 1, f.Now);
            Check(f.State.Gravity != null && f.State.Thunder != null, "Cancel timeout missing source.");
            f.E.Effect(3, 34452, 2, f.Now);
            Check(f.State.Gravity == null && f.State.Thunder != null, "Zero target arbitrary known effect clear one.");
            f.E.Effect(2, 34454, 3, f.Now);
            Check(!f.E.PredictedDamage.Any(), "Thunder effect.");
        }
    }

    private static void Knockbacks()
    {
        var f = new Fixture();
        f.Cast(34460);
        f.Cast(34468, 3);
        f.Cast(34466, 4);
        var aero = f.E.CarnivaleCastKnockbackMovements(f.Player, 34460).Single();
        var rush = f.E.CarnivaleCastKnockbackMovements(f.Player, 34468).Single();
        var pull = f.E.CarnivaleCastKnockbackMovements(f.Player, 34466).Single();
        Check(aero.To == rush.To && Near(pull.To, Center) && pull.Source.Pull && !pull.Source.StopAtWall
            && Near(aero.To, Center + Vector3.UnitZ * 15), "Separate Away10 and clampPull30.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: aero.Source.Cast.FinishAt);
        Check(f.E.CarnivaleCastKnockbackMovements(f.Player, 34460).Any(), "Equal not immune.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: aero.Source.Cast.FinishAt.AddTicks(1));
        Check(!f.E.CarnivaleCastKnockbackMovements(f.Player, 34460).Any(), "Strict immunity.");
    }

    private static void LandingRisk()
    {
        foreach (var oid in new uint[] { 0x3FA5, 0x3FA7 })
        {
            var f = new Fixture(oid);
            f.Cast(34460);
            var at = Center + Vector3.UnitZ * 15;
            f.Cast(oid == 0x3FA5 ? 34446u : 34465u, 3, position: at);
            Check(f.Hints().Contains("About to be knocked into danger!"), "Aero hazard subset.");
        }
    }

    private static void Hints()
    {
        var f = new Fixture();
        f.Set(1, a => a with { StatusSlots = [new(3621, 0, 1, Start)] });
        f.Set(100, a => a with { StatusSlots = [new(3779, 0, 1, Start), new(240, 0, 1, Start)] });
        f.Sync();
        Check(f.Hints().Any(h => h.Contains("immune to magic damage")) && f.Hints().Contains("Cleanse Electrocution!")
            && f.Hints().Contains("Use Loom to dodge AOEs!"), "Raw slots only hints.");
        var first = new Fixture(0x3FA5);
        first.Cast(34462);
        first.Cast(34463, 3);
        Check(!first.E.PredictedDamage.Any() && !first.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Summon/Rupture only hint.");
    }

    private static void Bootstrap()
    {
        var f = new Fixture(combat: false);
        f.Cast(34457);
        f.Cast(34589, 3);
        f.Now = Start.AddSeconds(30);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.State.Quakes.Single().Activation == f.Now.AddSeconds((double).3f)
            && f.E.CarnivaleInterrupts.Single().Cast.Rule.Action == 34589, "Late raw bootstrap and intent.");
    }

    private static void Rupture()
    {
        var f = new Fixture(0x3FA5);
        f.Cast(34463, seconds: 10);
        f.Cast(34463, 3, seconds: 2);
        f.Now = Start.AddSeconds(1);
        f.Sync();
        var first = f.Hints().Single(h => h.StartsWith("Kill slime ASAP!"));
        Check(first.EndsWith("9.0s left"), $"First insertion current clock: {first}");
        f.E.Finish(2, 34463, f.Now);
        var second = f.Hints().Single(h => h.StartsWith("Kill slime ASAP!"));
        Check(second.EndsWith("1.0s left"), $"Second promotes only after first Finish: {second}");
        f.E.Finish(3, 34463, f.Now);
        Check(!f.Hints().Any(h => h.StartsWith("Kill slime ASAP!")), "Last Finish.");
        f.Cast(34446);
        Check(f.Hints().Contains("GTFO from aoe!"), "Ordinary AOE danger text.");
    }

    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

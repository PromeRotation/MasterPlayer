using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleEyesVoltageRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Eyes constructor gaze stack display no precombat navigation or facing", Constructor);
        run("Carnivale Eyes solo TearyTwirl radius6point3 no automatic approach no generic Stack hint", Solo);
        run("Carnivale Eyes Blind571 hides true gaze and separate cones preserves stack expired status", Blind);
        run("Carnivale Eyes constructor and combat independently bootstrap raid slot Blind masks", SlotBootstrap);
        run("Carnivale Eyes true gaze source Finish effect timeout retained default range10000", Gaze);
        run("Carnivale Eyes Cold Dread ordinary90 cones exact radii no gaze or raidwide", Cones);
        run("Carnivale Eyes stack old target Destroy freeze same ID generation target FIFO Finish", Stacks);
        run("Carnivale Eyes group lifecycle HP0 not Dead all markers orange adds unconditional priorities", Groups);
        run("Carnivale Voltage source position first lock not spell location no range35 clipping real Finish", Voltage);
        run("Carnivale Voltage preactivation cancel stale raw does not revive on pull", Canceled);
        run("Carnivale Voltage slime outline7point6 creation death destroyed freeze no explosion navigation", Slimes);
        run("Carnivale Eyes Voltage raw saturated clocks and first pull overdue deadline", Saturation);
        run("Carnivale Eyes source solo raid0 independent of PR priority reorder", SourceSlot);
        run("Carnivale Eyes Pending constructor retains cast and Blind slot history before promotion", Pending);
        run("Carnivale Eyes Teary packet target correction raw confirmation real Finish atomic identity", StackCorrection);
        run("Carnivale Eyes cold start Blind constructor previous roster combat activation new roster", ColdStart);
        run("Carnivale Eyes Teary target FIFO consumed source correction cannot replay Start", StackConsumed);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x25CD, bool combat = false)
        {
            Actors = [new(1, oid, Center, InCombat: combat, IsEnemy: true, ObjectIndex: 2),
                new(2, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 3, Rotation: MathF.PI, Player: true, PartySlot: 0)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now, playerId: 100);
        internal void Cast(uint action, ulong source = 2, ulong target = 100, double seconds = 4, Vector3? location = null, float rotation = .7f)
        {
            Now = Now.AddSeconds(.01);
            if (!Actors.Any(a => a.Id == source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, location ?? Center, rotation, Now, Now.AddSeconds(seconds), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
        internal string[] Hints() => E.Hints(Actors.Single(a => a.Id == 100).Position).ToArray();
        internal void Pull()
        {
            Set(1, a => a with { InCombat = true });
            Now = Now.AddSeconds(.01);
            Sync();
        }
    }
    private static void Constructor()
    {
        foreach (var oid in new uint[] { 0x25CD, 0x26FF })
        {
            var f = new Fixture(oid);
            f.Cast(14691, seconds: .5);
            f.Cast(14693, 3);
            Check(f.E.Active == null && f.E.Gazes.Count() == 1 && f.E.CarnivaleEyeStacks.Count() == 1
                && f.Aoes("carnivale/gaze/").Length == 3 && f.Aoes("carnivale/eyes/stack/").Length == 1,
                "Constructor components not active before pull.");
            Check(f.E.AmbientAoes().All(a => !a.Navigate) && f.E.GazeFacing(Center + Vector3.UnitZ * 3, MathF.PI) == null,
                "Precombat control scope.");
            f.Pull();
            Check(f.E.Gazes.Count() == 1 && f.E.CarnivaleEyeStacks.Count() == 1, "Pull duplicated constructor cast.");
        }
    }
    private static void Solo()
    {
        var f = new Fixture();
        f.Actors.Add(new(3, 0x2700, Center));
        f.Cast(14693, 3, 3);
        f.Pull();
        var circle = f.Aoes("carnivale/eyes/stack/").Single();
        Check(circle.Shape is CircleTemplate { Radius: 6.3f } && !circle.Navigate && circle.Safe
            && !f.E.AmbientAoes().Any(a => a.Navigate) && f.Hints().Contains("Stack to get blinded!")
            && !f.Hints().Contains("Stack!") && !f.E.PredictedDamage.Any(), "Solo source semantics.");
    }
    private static void Blind()
    {
        var f = new Fixture(0x26FF, true);
        f.Cast(14691);
        f.Cast(14693, 3);
        f.Cast(14692, 4);
        f.Cast(14694, 5);
        f.Set(100, a => a with { Statuses = [571], StatusExpirations = new Dictionary<uint, DateTime> { [571] = Start.AddSeconds(-1) } });
        f.Sync();
        Check(!f.E.Gazes.Any() && !f.Aoes("carnivale/gaze/").Any() && !f.Aoes("carnivale/eyes/cone/").Any()
            && f.E.CarnivaleEyeStacks.Count() == 1 && f.Hints().Contains("Kill mandragoras last incase you need to get blinded again."),
            "Blind expiry must not synthesize Lose or hide stack.");
        f.Set(100, a => a with { Statuses = [], StatusExpirations = null });
        f.Sync();
        Check(f.E.Gazes.Count() == 1 && f.Aoes("carnivale/eyes/cone/").Length == 2, "True status Lose.");
    }
    private static void SlotBootstrap()
    {
        var f = new Fixture(0x26FF);
        f.Set(100, a => a with { Statuses = [571] });
        f.Sync();
        f.Set(100, a => a with { PartySlot = 1 });
        f.Sync();
        f.Cast(14691);
        f.Cast(14692, 4);
        f.Cast(14694, 5);
        f.Pull();
        Check(f.E.Gazes.Count() == 1 && !f.Aoes("carnivale/eyes/cone/").Any()
            && f.Hints().Contains("Stack to get blinded!"), "Constructor bit0 persists, new combat components bootstrap bit1.");
    }
    private static void Gaze()
    {
        var f = new Fixture(combat: true);
        f.Cast(14691, seconds: .5);
        var cast = f.E.Gazes.Single();
        Check(f.E.GazeFacing(Center + Vector3.UnitZ * 3, MathF.PI) is { } facing && float.IsFinite(facing), "PR facing absent.");
        f.Now = Start.AddSeconds(30);
        f.Sync();
        f.E.Effect(2, 14691, 1, f.Now);
        Check(f.E.Gazes.Single().FinishAt == cast.FinishAt, "Effect or timeout cleared true gaze.");
        f.E.Finish(2, 14691, f.Now);
        Check(!f.E.Gazes.Any() && f.E.GazeFacing(Center, 0) == null, "True Finish retained gaze.");
    }
    private static void Cones()
    {
        var f = new Fixture(0x26FF, true);
        f.Cast(14692, rotation: MathF.PI / 2);
        f.Cast(14694, 3, rotation: MathF.PI / 2);
        var cones = f.Aoes("carnivale/eyes/cone/");
        Check(cones.Length == 2 && cones.All(a => a.Shape is FanTemplate { AngleRadians: var angle } && angle == MathF.PI / 2)
            && cones.Select(a => ((FanTemplate)a.Shape).Radius).Order().SequenceEqual(new[] { 7.35f, 42.53f })
            && !f.E.Gazes.Any() && !f.E.PredictedDamage.Any(), "Cone parameter/type mismatch.");
        foreach (var cone in cones)
        {
            var geometry = cone.Shape.Resolve(cone.Position, cone.Rotation, null);
            Check(cone.Rotation == MathF.PI / 2 && geometry.Contains(Center + Vector3.UnitX * 5, 0)
                && !geometry.Contains(Center + Vector3.UnitZ * 5, 0), "Cone lost spell rotation.");
        }
    }
    private static void Stacks()
    {
        var f = new Fixture();
        f.Actors.Add(new(3, 0x2700, Center + Vector3.UnitX * 5));
        f.Cast(14693, 2, 3);
        f.Cast(14693, 4, 3);
        var old = f.E.CarnivaleEyeStacks.First().Target;
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        f.Actors.Add(new(3, 0x2700, Center + Vector3.UnitX * 10));
        f.Sync();
        Check(old.Removed && f.E.CarnivaleEyeStacks.All(s => ReferenceEquals(s.Target, old))
            && f.Aoes("carnivale/eyes/stack/").All(a => MathF.Abs(a.Position.X - 105) < .04f), "Destroy did not freeze original target.");
        f.E.Finish(4, 14693, f.Now);
        Check(f.E.CarnivaleEyeStacks.Count() == 1 && f.E.CarnivaleEyeStacks.Single().Cast.Source == 4,
            "Finish must consume first target match, not source record.");
    }
    private static void Groups()
    {
        foreach (var (oid, add) in new[] { (0x25CDu, 0x2700u), (0x26FFu, 0x2701u), (0x2706u, 0x2707u) })
        {
            var f = new Fixture(oid);
            f.Actors.Add(new(3, add, Center, InCombat: true, IsEnemy: true, ObjectIndex: 4));
            f.Sync();
            Check(f.E.Active != null && f.Aoes("carnivale/enemy/").Length == 2, "Any trash pull.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active != null, "HP0 not group death.");
            f.Set(1, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active != null, "Primary Dead but add alive.");
            f.Set(3, a => a with { Dead = true });
            f.Sync();
            Check(f.E.Active == null, "All Dead.");
        }
    }
    private static void Voltage()
    {
        var f = new Fixture(0x2706, true);
        f.Set(2, a => a with { Position = Center + Vector3.UnitX * 8 });
        f.Cast(14710, location: Center - Vector3.UnitX * 10, seconds: 12);
        var first = f.E.CarnivaleVoltages.Single();
        Check(first.Origin == Center + Vector3.UnitX * 8, "Must use caster position.");
        f.Set(2, a => a with { Position = Center + Vector3.UnitZ * 8 });
        f.Now = Start.AddSeconds(30);
        f.Sync();
        f.E.Effect(2, 14710, 1, f.Now);
        Check(ReferenceEquals(first.Visibility, f.E.CarnivaleVoltages.Single().Visibility)
            && f.Aoes("carnivale/voltage/").Single().Navigate, "Visibility cache or timeout.");
        f.E.Finish(2, 14710, f.Now);
        Check(!f.E.CarnivaleVoltages.Any(), "Real Finish.");
    }
    private static void Canceled()
    {
        var f = new Fixture(0x2706);
        f.Cast(14710);
        f.E.Finish(2, 14710, f.Now.AddSeconds(.1));
        f.Now = f.Now.AddSeconds(.2);
        f.Pull();
        Check(!f.E.CarnivaleVoltages.Any(), "Canceled preactivation raw revived.");
    }
    private static void Slimes()
    {
        var f = new Fixture(0x2706);
        f.Actors.Add(new(3, 0x2707, Center));
        f.Sync();
        var circle = f.Aoes("carnivale/explosion/").Single();
        Check(circle.Shape is CircleTemplate { Radius: 7.6f } && !circle.Navigate
            && f.Hints().Contains("In slime explosion radius!"), "Slime outline only.");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        Check(f.Aoes("carnivale/explosion/").Length == 1, "Destroy freezes.");
        f.Actors.Add(new(3, 0x2707, Center + Vector3.UnitX * 10));
        f.Sync();
        Check(f.Aoes("carnivale/explosion/").Length == 2, "Same ID creates new reference.");
        f.Set(3, a => a with { Dead = true });
        f.Sync();
        f.Set(3, a => a with { Dead = false });
        f.Sync();
        Check(f.Aoes("carnivale/explosion/").Length == 1, "Death deletes, revive not re-add.");
    }
    private static void Saturation()
    {
        var f = new Fixture(0x26FF, true);
        f.Cast(14692);
        var first = f.Aoes("carnivale/eyes/cone/").Single().Activation;
        f.Now = Start.AddSeconds(6);
        f.Set(2, a => a with { Cast = a.Cast! with { StartedAt = a.Cast.StartedAt.AddSeconds(1), FinishAt = a.Cast.FinishAt.AddSeconds(1) } });
        f.Sync();
        Check(f.Aoes("carnivale/eyes/cone/").Single().Activation == first, "Elapsed drift reset cone.");
    }
    private static void SourceSlot()
    {
        var f = new Fixture(0x26FF, true);
        f.E.CarnivaleSourcePlayerId = 100;
        f.Set(100, a => a with { PartySlot = 1, Statuses = [571] });
        f.Sync();
        f.Cast(14691);
        f.Cast(14692, 4);
        Check(!f.E.Gazes.Any() && !f.Aoes("carnivale/eyes/cone/").Any(), "Source player must be raid0.");
        f.Set(100, a => a with { PartySlot = 0 });
        f.Sync();
        Check(!f.E.Gazes.Any() && !f.Aoes("carnivale/eyes/cone/").Any()
            && f.Hints().Contains("Kill mandragoras last incase you need to get blinded again."), "PR reorder moved Blind mask.");
    }
    private static void Pending()
    {
        var e = new DungeonEncounter(796);
        var far = Center + Vector3.UnitX * 600;
        var actors = new List<DungeonActor> { new(1, 0x26FF, far), new(2, 0x233C, far),
            new(100, 0, Center, Player: true, PartySlot: 0), new(101, 0, Center, Player: true, PartySlot: 1) };
        void Sync() => e.Synchronize(actors, actors.Single(a => a.Id == 100).Position, Start, playerId: 100);
        Sync();
        actors[2] = actors[2] with { Statuses = [571] };
        actors[1] = actors[1] with { Cast = new(14691, far, 0, Start, Start.AddSeconds(5), 2) };
        Sync();
        actors[2] = actors[2] with { PartySlot = 1 };
        actors[3] = actors[3] with { PartySlot = 0 };
        Sync();
        Check(!e.Gazes.Any() && !e.AmbientAoes().Any(), "Pending displayed output.");
        actors[0] = actors[0] with { InCombat = true };
        actors[2] = actors[2] with { Position = far };
        actors[3] = actors[3] with { Position = far };
        Sync();
        Check(e.Gazes.Count() == 1 && e.Hints(far).Contains("Stack to get blinded!"),
            "Promotion bootstrapped over constructor Blind history or lost pending cast.");
    }
    private static void StackCorrection()
    {
        var f = new Fixture(combat: true);
        f.Actors.Add(new(3, 0x2700, Center + Vector3.UnitX * 5));
        f.Actors.Add(new(4, 0x2700, Center + Vector3.UnitX * 10));
        f.Cast(14693, target: 3);
        var generation = f.E.CarnivaleEyeStacks.Single().Generation;
        var original = f.Actors.Single(a => a.Id == 2).Cast!;
        f.E.StartCast(2, original with { TargetId = 4 }, f.Now);
        Check(f.E.CarnivaleEyeStacks.Single() is { Target.Actor.Id: 4, Cast.TargetId: 4 }
            && f.E.CarnivaleEyeStacks.Single().Generation == generation, "Packet split Cast and Target identity.");
        f.Sync();
        f.E.Finish(2, 14693, f.Now.AddSeconds(.2));
        Check(!f.E.CarnivaleEyeStacks.Any(), "Corrected real Finish leaked stack.");
    }
    private static void ColdStart()
    {
        var e = new DungeonEncounter(796) { CarnivaleSourcePlayerId = 100 };
        DungeonActor[] actors = [new(1, 0x26FF, Center, InCombat: true),
            new(2, 0x233C, Center, Cast: new(14691, Center, 0, Start, Start.AddSeconds(5), 2)),
            new(3, 0x233C, Center, Cast: new(14692, Center, 0, Start, Start.AddSeconds(5), 3)),
            new(100, 0, Center, PartySlot: 1, Statuses: [571], Player: true)];
        e.Synchronize(actors, Center, Start, playerId: 100);
        Check(e.Gazes.Count() == 1 && !e.AmbientAoes().Any(a => a.Id.StartsWith("carnivale/eyes/cone/"))
            && e.Hints(Center).Contains("Stack to get blinded!"), "Constructor replay used uncommitted first roster.");
        e.Synchronize(actors, Center, Start.AddSeconds(.1), playerId: 100);
        Check(e.Gazes.Count() == 1, "Unchanged status synthesized a new Gain.");
        var late = new DungeonEncounter(796) { CarnivaleSourcePlayerId = 100 };
        late.Synchronize([actors[3]], Center, Start, playerId: 100);
        late.Synchronize(actors, Center, Start.AddSeconds(.1), playerId: 100);
        Check(!late.Gazes.Any() && late.Hints(Center).Contains("Kill mandragoras last incase you need to get blinded again."),
            "Later constructor lost already committed roster.");
    }
    private static void StackConsumed()
    {
        var f = new Fixture(combat: true);
        f.Actors.Add(new(3, 0x2700, Center));
        f.Cast(14693, 2, 3);
        f.Cast(14693, 4, 3);
        var first = f.Actors.Single(a => a.Id == 2).Cast!;
        f.E.Finish(4, 14693, f.Now.AddSeconds(.1));
        f.E.StartCast(2, first with { Location = Center + Vector3.UnitX }, f.Now.AddSeconds(.2));
        Check(f.E.CarnivaleEyeStacks.Count() == 1 && f.E.CarnivaleEyeStacks.Single().Cast.Source == 4,
            "Correction replayed already consumed source Start.");
        f.E.Finish(2, 14693, f.Now.AddSeconds(.3));
        Check(!f.E.CarnivaleEyeStacks.Any(), "Cross-source FIFO left a corrected orphan.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

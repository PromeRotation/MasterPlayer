using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleChimeraRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private const string Imps = "The imps are weak to fire spells and strong against ice.\nInterrupt Void Blizzard with Flying Sardine.";
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Chimera first act group pull hp zero not death all imp markers and combat text", FirstAct);
        run("Carnivale Chimera second act primary completion living imp hints not targetability or health", SecondAct);
        run("Carnivale Chimera all ordinary exact source circles donut and two independent interrupt components", Ordinary);
        run("Carnivale Chimera keeper current target dynamic fallback cast complete identity finish no timeout", Cast);
        run("Carnivale Chimera keeper actual event identity main target point nine delay live dead state7 handoff", Prediction);
        run("Carnivale Chimera shared background raw casts interrupts keeper event retained immediate handoff", Background);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x2730, Center, InCombat: true),
            new(2, 0x233C, Center),
            new(100, 0, Center, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 3, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal Fixture() => Sync();
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = fn(Actors[index]);
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: 100);
        internal void Begin(uint action = 15081)
        {
            Now = Now.AddSeconds(.01);
            Set(2, a => a with { Cast = new(action, Center + Vector3.UnitZ * 12, .5f, Now, Now.AddSeconds(6), 101) });
            Sync();
        }
        internal DungeonAmbientAoe[] Rays() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/ray/")).ToArray();
    }
    private static void FirstAct()
    {
        var actors = new List<DungeonActor>
        {
            new(1, 0x272F, Center), new(2, 0x272F, Center, InCombat: true, Targetable: false),
            new(100, 0, Center, PartySlot: 0, Player: true),
        };
        var e = new DungeonEncounter(796);
        e.Synchronize(actors, Center, Start, playerId: 100);
        Check(e.Active?.BossOid == 0x272F && e.Hints(Center).Contains(Imps) && e.CarnivalePrecombatHint == null
            && e.Arena!.Contains(Center + Vector3.UnitX * 20), "Group pull or hints/arena.");
        actors[0] = actors[0] with { Dead = true };
        actors[1] = actors[1] with { Hp = 0, Targetable = true };
        e.Synchronize(actors, Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && e.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1,
            "HP0 ended group/markers.");
        actors.RemoveAt(1);
        e.Synchronize(actors, Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null, "AllDeadOrDestroyed completion.");
    }
    private static void SecondAct()
    {
        var f = new Fixture();
        Check(!f.E.Hints(Center).Contains(Imps), "Missing imp generated hint.");
        f.Actors.Add(new(3, 0x2731, Center, Targetable: false, Hp: 0));
        f.Sync();
        Check(f.E.Hints(Center).Contains(Imps) && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1,
            "Imp hint incorrectly filters HP/targetability.");
        f.Set(3, a => a with { Targetable = true });
        f.Sync();
        Check(f.E.AmbientAoes().Single(a => a.Id == "carnivale/enemy/3").Color == new Vector4(1, 128f / 255, 0, 1),
            "Imp object colour.");
        f.Set(3, a => a with { Dead = true });
        f.Sync();
        Check(!f.E.Hints(Center).Contains(Imps), "Dead imp hint.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null, "Primary completion.");
    }
    private static void Ordinary()
    {
        var rules = CarnivaleCatalog.Create().Where(d => d.BossOid is 0x272F or 0x2730).SelectMany(d => d.Casts).ToArray();
        Check(rules.Count(r => r.Action == 15064 && r.Shape is CircleTemplate { Radius: 5 }) == 2
            && rules.Single(r => r.Action == 15079).Shape is CircleTemplate { Radius: 9 }
            && rules.Single(r => r.Action == 15080).Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 30 }
            && rules.All(r => r.Action is not (14267 or 6497)), "Dimensions or enumeration expanded scope.");
        var f = new Fixture();
        f.Actors.Add(new(3, 0x233C, Center, Cast: new(15063, Center, 0, Start, Start.AddSeconds(6))));
        f.Begin();
        Check(f.E.CarnivaleInterruptCasts.Select(c => c.Rule.Action).Order().SequenceEqual(new uint[] { 15063, 15081 })
            && f.E.Hints(Center).Count(h => h == "Interrupt!") == 2 && !f.E.Gazes.Any() && !f.E.PredictedDamage.Any(),
            "Two interrupt components collapsed or added gaze/damage.");
        f.E.Finish(2, 15081);
        Check(f.E.CarnivaleInterruptCasts.Single().Rule.Action == 15063, "Keeper Finish cleared VoidBlizzard.");
    }
    private static void Cast()
    {
        var f = new Fixture();
        f.Begin();
        var cast = f.E.CarnivaleRayCasts.Single();
        Check(cast.Cast.Source == 2 && cast.Cast.TargetId == 101 && cast.Cast.Rule.Action == 15081
            && f.Rays().Single().Shape is CircleTemplate { Radius: 9 } && f.Rays().Single().Position.X == 103, "Keeper model/target.");
        f.Set(101, a => a with { Position = Center + Vector3.UnitX * 7 });
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.Rays().Single().Position.X == 107 && f.Rays().Single().Activation == cast.Cast.FinishAt, "Moving target or timeout.");
        f.Actors.RemoveAll(a => a.Id == 101);
        f.Sync();
        Check(f.Rays().Single().Position == Center + Vector3.UnitZ * 12, "Keeper missing target Loc.");
        f.E.Finish(2, 15081, f.Now);
        Check(!f.Rays().Any() && !f.E.CarnivaleInterrupts.Any(), "Keeper Finish.");
    }
    private static void Prediction()
    {
        var f = new Fixture();
        f.E.Effect(2, 15081, 0, Start.AddSeconds(10), 101, Center, Start.AddSeconds(2));
        f.E.Effect(2, 15081, 0, Start.AddSeconds(11), 999, Center + Vector3.UnitX * 3);
        var p = f.E.CarnivaleRayPredictions.First();
        Check(p.Source == 2 && p.MainTarget == 101 && p.Action == 15081 && p.OccurredAt == Start.AddSeconds(2)
            && p.ObservedAt == Start.AddSeconds(10) && p.Activation == Start.AddSeconds(10.9)
            && p.Position == Center + Vector3.UnitX * 3, "Keeper full event/clock.");
        f.Set(101, a => a with { Position = Center + Vector3.UnitX * 15 });
        f.Now = Start.AddSeconds(30);
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 2 && f.Rays().All(a => a.Position == p.Position && a.Navigate),
            "Keeper event lock or timeout.");
        f.Actors.Add(new(5, 0x1E972A, p.Position + Vector3.UnitX * .123456f, Dead: true));
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 1 && f.Rays().Single(a => a.Id.Contains("/live/")).Shape is CircleTemplate { Radius: 9 }
            && f.E.Hints(Center).Contains("GTFO from voidzone!"), "Keeper dead live or one match.");
        f.Set(5, a => a with { EventState = 7 });
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 1 && !f.Rays().Any(a => a.Id.Contains("/live/")), "Keeper state7.");
    }
    private static void Background()
    {
        var f = new Fixture();
        var e = new DungeonEncounter(796);
        var old = new DungeonActor(9, 0x272F, Center, InCombat: true);
        e.Synchronize([old, f.Actors[2]], Center, Start, playerId: 100);
        f.Set(2, a => a with { Cast = new(15063, Center, 0, Start, Start.AddSeconds(6)) });
        e.Synchronize([old, .. f.Actors], Center, Start.AddSeconds(.1), playerId: 100);
        e.Effect(2, 15081, 1, Start.AddSeconds(.2), 100);
        e.Synchronize([old with { Dead = true }, .. f.Actors], Center, Start.AddSeconds(.3), playerId: 100);
        Check(e.Active?.BossOid == 0x2730 && e.CarnivaleInterruptCasts.Single().Rule.Action == 15063
            && e.CarnivaleRayPredictions.Single().Action == 15081 && e.Casts[2].Rule.Action == 15063,
            "Shared background unload discarded input.");
        e.Finish(2, 15063, Start.AddSeconds(.4));
        Check(!e.CarnivaleInterruptCasts.Any() && e.CarnivaleRayPredictions.Any(), "Independent event persisted.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

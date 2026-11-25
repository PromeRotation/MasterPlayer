using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class PalacePlantsRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -236);
    internal static void Run(Action<string, Action> run)
    {
        run("Palace plants exact territories source rules no invented cleave casts", Rules);
        run("Palace plants dynamic all source cleaves target party geometry arbitrary raw casts", Cleave);
        run("Palace plants boss hornet priorities no aggro condition DoT intention no whole attack ban", Priorities);
        run("Palace plants observed end native fields clock identity late cancel helper death and owner", Timing);
        run("Palace plants unavoidable damage true slots self fallback default clock and no shapes", Damage);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E;
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(bool later, bool combat = true)
        {
            E = new(later ? 599u : 562u);
            Actors = [new(1, later ? 0x1819u : 0x169Fu, Center, InCombat: combat, TargetId: 100),
                new(2, later ? 0x1905u : 0x1763u, Center + Vector3.UnitZ * 4),
                new(3, 0x233C, Center), new(100, 0, Center + Vector3.UnitX * 10, PartySlot: 0, Player: true),
                new(101, 0, Center + Vector3.UnitZ * 15, PartySlot: 1, Player: true)];
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == 100).Position, Now, null, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change) => Actors[Actors.FindIndex(a => a.Id == id)] = change(Actors.First(a => a.Id == id));
        internal void Cast(uint action, ulong source = 3, ulong target = 100)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(action, Center + Vector3.UnitX * 2, .7f, Now, Now.AddSeconds(3), target, TotalTime: 2.7f) });
            Sync();
        }
        internal DungeonAmbientAoe[] Cleaves() => E.AmbientAoes().Where(a => a.Id.StartsWith("palace-plant/cleave/")).ToArray();
    }
    private static void Rules()
    {
        foreach (var later in new[] { false, true })
        {
            var f = new Fixture(later, false);
            Check(f.E.Active == null && f.E.AmbientAoes().All(a => !a.Navigate)
                && f.E.AmbientAoes().Count(a => a.Id.StartsWith("palace-plant/precombat/")) == 2, "Precombat scope.");
            f.Set(1, a => a with { Position = new(-240, 0, -236) });
            f.Sync();
            var marker = f.E.AmbientAoes().Single(a => a.Id == "palace-plant/precombat/enemy");
            Check(marker.OutlineOnly && marker.Position.X < -274, "Outside precombat marker did not clamp to arena.");
            f.Set(1, a => a with { Position = Center });
            var rules = f.E.Definitions.Single().Casts;
            Check(rules.Length == (later ? 5 : 4) && !rules.Any(r => r.Action is 6421 or 7133)
                && rules.Count(r => r.Shape != null) == 3, "Cleave became a cast or extra inactive skill.");
            Check(rules.Single(r => r.Name == "Acid Mist").Shape == new CircleTemplate(9.6f)
                && rules.Single(r => r.Name == "Rotten Stench").Shape == RayRectangleTemplate.Symmetric(12, 48.6f), "Precise source shapes.");
            f.Set(1, a => a with { InCombat = true });
            f.Sync();
            f.Set(1, a => a with { InCombat = false });
            f.Now = f.Now.AddSeconds(10);
            f.Sync();
            Check(f.E.Active != null, "Disengage ended source default phase.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null, "Living hornets held completed primary.");
        }
    }
    private static void Cleave()
    {
        var f = new Fixture(false);
        var own = f.Cleaves();
        Check(own.Single(a => a.Display).Shape == new FanTemplate(11.6f, 2 * MathF.PI / 3)
            && own.Single(a => a.Display).OutlineOnly
            && own.Single(a => a.Navigate).Shape == new FanTemplate(100, 2 * MathF.PI / 3), "Tank cleave geometry.");
        f.Set(1, a => a with { TargetId = 101, Rotation = MathF.PI });
        f.Sync();
        Check(f.Cleaves().Single(a => a.Navigate).Shape == new FanTemplate(11.6f, 2 * MathF.PI / 3)
            && MathF.Abs(f.Cleaves().Single(a => a.Display).Rotation) < .001, "Cleave used boss rotation.");
        f.Actors.Add(new(4, 0x169F, Center + Vector3.UnitZ, TargetId: 100));
        f.Sync();
        Check(f.Cleaves().Count(a => a.Display) == 2, "Only primary source was included.");
        f.Set(1, a => a with { Cast = new(99999, Center, 0, Start, Start.AddSeconds(2)) });
        f.Sync();
        Check(f.Cleaves().Count(a => a.Display) == 1, "Unknown raw cast failed to hide cleave.");
        f.Set(4, a => a with { TargetId = 999 });
        f.Sync();
        Check(f.Cleaves().Length == 0, "Unresolved target still drew cleave.");
    }
    private static void Priorities()
    {
        foreach (var later in new[] { false, true })
        {
            var f = new Fixture(later);
            Check(f.E.PriorityTarget(1, Center) == 2 && f.E.ForbiddenDotTargets.SequenceEqual([2ul]), "Unaggroed hornet lost priority2/DoT intention.");
            Check(!f.E.ForbiddenTargets.Contains(2ul), "DoT intention expanded to forbidden target.");
            f.Set(2, a => a with { Dead = true });
            f.Sync();
            Check(f.E.PriorityTarget(2, Center) == 1 && f.E.ForbiddenDotTargets.Length == 0, "Boss priority1 fallback.");
        }
    }
    private static void Timing()
    {
        var f = new Fixture(true);
        f.Cast(7137);
        var first = f.E.Casts[3];
        f.Now = f.Now.AddSeconds(10);
        f.Sync();
        Check(ReferenceEquals(first, f.E.Casts[3]), "Stable raw timed out or changed identity.");
        f.Set(3, a => a with { Cast = a.Cast! with { Rotation = 1.7f } });
        f.Sync();
        var next = f.E.Casts[3];
        Check(next.Rotation == 1.7f && next.FinishAt > f.Now,
            $"Raw shape replacement or NPC cap lost: rotation={next.Rotation:R}, F={next.FinishAt:O}, Now={f.Now:O}.");
        f.E.Finish(3, 7137, f.Now.AddSeconds(-1));
        Check(f.E.Casts.ContainsKey(3), "Old cancel ended new raw generation.");
        f.Set(3, a => a with { Cast = null });
        f.Sync();
        Check(f.E.Casts.Count == 0, "Real raw Finish retained.");
        f.Set(3, a => a with { Dead = true });
        f.Cast(7135);
        Check(f.E.Casts.Count == 1, "Dead helper cast incorrectly ignored.");
        f.E.ResetFight();
        Check(f.E.Casts.Count == 0 && f.Cleaves().Length == 0, "Owner clear retained output.");
    }
    private static void Damage()
    {
        var f = new Fixture(true);
        f.Cast(7136);
        Check(f.E.PredictedDamage.Count() == 2 && f.E.PredictedDamage.All(p => p.DamageType == "Raidwide")
            && f.E.Casts[3].Rule.Shape == null, "Raidwide shape/targets.");
        f.Now = f.Now.AddSeconds(10);
        f.Sync();
        Check(f.E.PredictedDamage.All(p => p.Activation > f.Now), "Current NPC saturated clock lost.");
        f.Set(3, a => a with { Cast = null });
        f.Sync();
        f.Set(2, a => a with { TargetId = 101 });
        f.Cast(919, 2, 2);
        Check(f.E.PredictedDamage.Single() is { TargetId: 101, PartySlot: 1, DamageType: "Tankbuster" }
            && f.E.Casts[2].Rule.Shape == null && f.E.Hints(Center).Any(s => s.Contains("98%")), "Final Sting target self fallback/custom hint.");
        var early = new Fixture(false);
        early.Cast(919, 2);
        Check(early.E.Casts.Count == 0, "Spurge invented Final Sting component.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

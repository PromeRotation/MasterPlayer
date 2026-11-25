using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivaleAzulmagiaRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Azulmagia three default lifecycles preparation primary and orange Angon markers", Lifecycle);
        run("Carnivale Azulmagia twenty ordinary instances source anomalous width90 independent rings real Finish", Ordinary);
        run("Carnivale Azulmagia Meteor independent R15 lock and dynamic R10 target cast prediction", MeteorCast);
        run("Carnivale Azulmagia Meteor actual main target effect processing clock no Start no timeout independent Finish", MeteorEffect);
        run("Carnivale Azulmagia lava EventState filter quantized navigation first per entity raw matching", Lava);
        run("Carnivale Azulmagia maelstrom full references raw moving R8 hard obstacles", Maelstrom);
        run("Carnivale Azulmagia Web global boolean any source Finish no movement or damage requests", Web);
        run("Carnivale Azulmagia reflection precedence Doom current perspective Angon priority Pointless", Hints);
        run("Carnivale Azulmagia late component bootstrap independent clocks and preactivation cancel", Bootstrap);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Player = 100;
        internal Fixture(uint oid = 0x2680, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat, Name: "Azulmagia"),
                new(2, 0x233C, Center), new(3, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitZ * 2, PartySlot: 0, Player: true),
                new(101, 0, Center - Vector3.UnitZ * 4, PartySlot: 1, Player: true)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == Player).Position, Now, playerId: Player);
        internal void Cast(uint action, ulong source = 2, ulong target = 100)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(action, Center + Vector3.UnitX, .4f, Now, Now.AddSeconds(4), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Rays() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/ray/")).ToArray();
    }

    private static void Lifecycle()
    {
        foreach (var oid in new uint[] { 0x2678, 0x267F, 0x2680 })
        {
            var f = new Fixture(oid, false);
            Check(f.E.Active == null && f.E.CarnivalePrecombatHint != null, "Preparation.");
            f.Actors.Add(new(4, 0x2682, Center, Name: "Angon"));
            f.Set(1, a => a with { InCombat = true, Targetable = false });
            f.Sync();
            Check(f.E.Active == null, "Untargetable Primary pulled.");
            f.Set(1, a => a with { Targetable = true });
            f.Sync();
            Check(f.E.Active?.BossOid == oid && f.E.Arena!.Contains(Center + Vector3.UnitX * 20) == (oid != 0x2680)
                && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == (oid == 0x267F ? 2 : 1), "Arena/markers.");
            f.Set(1, a => a with { InCombat = false });
            f.Sync();
            Check(f.E.Active != null, "Disengage reset.");
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null, "Live Angon kept module alive.");
        }
    }

    private static void Ordinary()
    {
        foreach (var oid in new uint[] { 0x2678, 0x267F, 0x2680 })
        {
            var rules = CarnivaleCatalog.Create().Single(d => d.BossOid == oid).Casts;
            Check(rules.Single(r => r.Action == 14766).Shape is RayRectangleTemplate bolt
                && bolt.MinLocalX == (oid == 0x2678 ? -45 : -4) && bolt.MaxLocalX == -bolt.MinLocalX
                && bolt.Length == 51.2f, "Source Bolt half width.");
            foreach (var rule in rules.Where(r => r.Shape != null))
            {
                var f = new Fixture(oid);
                f.Cast(rule.Action);
                var pending = f.E.OutputCasts.Single().Value;
                f.Set(2, a => a with { Position = Center + Vector3.UnitZ * 12, Rotation = 2 });
                f.Now = Start.AddSeconds(15);
                f.Sync();
                f.E.Effect(2, rule.Action, 1, f.Now);
                Check(f.E.OutputCasts.Single().Value == pending, "Ordinary lock/effect/timeout.");
                f.E.Finish(2, rule.Action, f.Now);
                Check(!f.E.OutputCasts.Any(), "Ordinary Finish.");
            }
        }
        var rings = new Fixture();
        rings.Cast(14774);
        rings.Cast(14775, 3);
        rings.E.Finish(2, 14775, rings.Now);
        Check(rings.E.OutputCasts.Count() == 2, "Different ring action consumed old ring.");
        rings.E.Finish(2, 14774, rings.Now);
        Check(rings.E.OutputCasts.Single().Value.Rule.Action == 14775, "Ring independence.");
    }

    private static void MeteorCast()
    {
        var f = new Fixture();
        f.Cast(14771);
        var cast = f.E.OutputCasts.Single().Value;
        var ray = f.E.CarnivaleRayCasts.Single();
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 8 });
        f.Set(2, a => a with { Position = Center - Vector3.UnitX * 5 });
        f.Now = Start.AddSeconds(8);
        f.Sync();
        Check(f.E.OutputCasts.Single().Value == cast && cast.Rule.Shape is CircleTemplate { Radius: 15 }
            && f.Rays().Single().Position == Center + Vector3.UnitZ * 8
            && f.Rays().Single().Shape is CircleTemplate { Radius: 10 }
            && f.E.CarnivaleRayCasts.Single().Cast.FinishAt == ray.Cast.FinishAt, "Meteor merged/frozen dynamic target.");
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Player = 101;
        f.Sync();
        Check(f.Rays().Single().Position == cast.Location, "Missing target raw location fallback.");
        f.E.Finish(2, 14771, f.Now);
        Check(!f.Rays().Any() && !f.E.OutputCasts.Any(), "Meteor Finish paths.");
    }

    private static void MeteorEffect()
    {
        var f = new Fixture();
        f.Now = Start.AddSeconds(10);
        f.E.Effect(2, 14771, 1, f.Now, 100, Center + Vector3.UnitX * 9, occurredAt: Start);
        var p = f.E.CarnivaleRayPredictions.Single();
        Check(p.Position == f.Actors.Single(a => a.Id == 100).Position
            && p.Activation == f.Now.AddSeconds(1.2d) && p.OccurredAt == Start, "Main target/processing clock.");
        f.E.Finish(2, 14771, f.Now);
        f.Now = Start.AddSeconds(100);
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Single() == p, "Effect prediction expired or Finish consumed.");
        f.E.Effect(2, 14771, 2, f.Now, 999, Center);
        Check(f.E.CarnivaleRayPredictions.Last().Position == Center, "Missing target packet location fallback.");
    }

    private static void Lava()
    {
        var f = new Fixture();
        var pos = Center + new Vector3(1.12345f, 0, 2.37654f);
        f.E.Effect(2, 14771, 1, f.Now, 999, pos + Vector3.UnitX);
        f.E.Effect(2, 14771, 2, f.Now, 999, pos + Vector3.UnitX * 2);
        f.Actors.Add(new(4, 0x1EA8BB, pos, Dead: true, Targetable: false, EventState: 7));
        f.Sync();
        Check(f.E.CarnivaleRayPredictions.Count() == 2, "State7 matched prediction.");
        f.Set(4, a => a with { EventState = 0 });
        f.Sync();
        var live = f.Rays().Single(a => a.Id.Contains("/live/"));
        Check(f.E.CarnivaleRayPredictions.Single().Sequence == 2 && !live.Obstacle && live.Navigate
            && live.Position != pos && live.Shape is CircleTemplate { Radius: 10 }, "Raw matching/quantized navigation.");
        f.Sync();
        Check(!f.E.CarnivaleRayPredictions.Any(), "Live entity failed next-frame first match.");
        f.Set(4, a => a with { EventState = 7 });
        f.Sync();
        Check(!f.Rays().Any(), "State7 live output retained.");
    }

    private static void Maelstrom()
    {
        var f = new Fixture();
        var pos = Center + new Vector3(1.12345f, 0, 2.37654f);
        f.Actors.Add(new(4, 0x2681, pos, Rotation: .7f, Dead: true, Targetable: false, EventState: 7));
        f.Sync();
        var a = f.E.AmbientAoes().Single(a => a.Id.StartsWith("carnivale/maelstrom/"));
        Check(a.Position == pos && a.Rotation == .7f && a.Obstacle && a.Shape is CircleTemplate { Radius: 8 }
            && CarnivaleCatalog.IgnoredCast(0x2680, 14780), "Maelstrom source semantics.");
    }

    private static void Web()
    {
        var f = new Fixture();
        f.Cast(14770);
        f.Cast(14770, 3);
        Check(f.E.Hints(Center).Any(h => h.Contains("Bait the Meteor")) && !f.E.PredictedDamage.Any()
            && !f.E.OutputCasts.Any(c => c.Value.Rule.Shape != null), "Web missing/invented outputs.");
        f.E.Finish(2, 14770, f.Now);
        Check(!f.E.Hints(Center).Any(h => h.Contains("Bait the Meteor")), "Web changed to overlapping count.");
        f.Sync();
        Check(!f.E.Hints(Center).Any(h => h.Contains("Bait the Meteor")), "Repeated raw restarted Web.");
        var raw = f.Actors.Single(a => a.Id == 3).Cast!;
        f.E.StartCast(3, raw with { Location = Center + Vector3.UnitX * 3 }, f.Now, fromPacket: true);
        Check(!f.E.Hints(Center).Any(h => h.Contains("Bait the Meteor")), "Packet spatial correction restarted Web.");
    }

    private static void Hints()
    {
        var f = new Fixture();
        f.Set(1, a => a with { Statuses = [556, 1307] });
        f.Set(100, a => a with { Statuses = [910] });
        f.Sync();
        Check(f.E.Hints(Center).Contains("Azulmagia will reflect all magic damage!")
            && !f.E.Hints(Center).Contains("Azulmagia will reflect all physical damage!")
            && f.E.Hints(Center).Any(h => h.StartsWith("You were doomed!")), "Status precedence.");
        f.Player = 101;
        f.Sync();
        Check(!f.E.Hints(Center).Any(h => h.StartsWith("You were doomed!")), "Doom leaked perspective.");
        f.Set(1, a => a with { Statuses = [], StatusSlots = [new(556, 0, 0, Start)] });
        f.Set(101, a => a with { Statuses = [], StatusSlots = [new(910, 0, 0, Start)] });
        f.Now = Start.AddSeconds(10);
        f.Sync();
        Check(f.E.Hints(Center).Contains("Azulmagia will reflect all magic damage!")
            && f.E.Hints(Center).Any(h => h.StartsWith("You were doomed!")), "Expired occupied slots were filtered.");
        var p = new Fixture(0x267F);
        p.Actors.Add(new(4, 0x2682, Center, Hp: 0, Dead: true, Name: "Dead first", IsEnemy: true, ObjectIndex: 4));
        p.Actors.Add(new(5, 0x2682, Center, Hp: 100, Name: "Live next", Targetable: false, IsEnemy: true, ObjectIndex: 6));
        p.Actors.Add(new(6, 0x9999, Center, Hp: 100, IsEnemy: true, ObjectIndex: 8));
        p.Sync();
        Check(p.E.Hints(Center).Any(h => h.StartsWith("Kill Live next!")) && p.E.PriorityTarget(0, Center) == 6,
            "Angon text or fallback priority.");
        p.Set(5, a => a with { Targetable = true });
        p.Sync();
        Check(p.E.PriorityTarget(6, Center) == 5, "Angon priority.");
        p.Set(5, a => a with { Hp = 1 });
        p.Sync();
        Check(p.E.PriorityTarget(0, Center) == 6, "Pointless Angon resurrected.");
    }

    private static void Bootstrap()
    {
        var f = new Fixture(combat: false);
        f.Cast(14771);
        f.Now = Start.AddSeconds(10);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.CarnivaleRayCasts.Single().Cast.FinishAt == f.Now.AddSeconds((double).3f)
            && f.E.OutputCasts.Single().Value.FinishAt == f.Now.AddSeconds((double).3f), "Late component clock.");
        var canceled = new Fixture(combat: false);
        canceled.Cast(14771);
        canceled.E.Finish(2, 14771, canceled.Now.AddSeconds(.1));
        canceled.Now = Start.AddSeconds(10);
        canceled.Set(1, a => a with { InCombat = true });
        canceled.Sync();
        Check(!canceled.Rays().Any() && !canceled.E.OutputCasts.Any(), "Pre-activation cancel revived.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

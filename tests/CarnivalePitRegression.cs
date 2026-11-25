using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class CarnivalePitRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("Carnivale Pit first act group combat completion hp zero and fixed primary first viking hints", FirstAct);
        run("Carnivale Pit second act source double Magus text bug no damage restrictions full priority overwrite", SecondAct);
        run("Carnivale Pit third act current live vacuum raw R3 obstacles death resurrection no cast dependency", Vacuum);
        run("Carnivale Pit all seven ordinary instances source R8 PageTear and exact lifetime", Ordinary);
        run("Carnivale Pit two interrupt components retain intent source identity deadline and real Finish", Interrupts);
        run("Carnivale Pit shared two-action single target dynamic self fallback raw clock missing and dead roster", SingleTarget);
        run("Carnivale Pit dynamic charge old actor identity navigation prediction and independent locked forward KB", Charge);
        run("Carnivale Pit forward KB chain no target eligibility immune strict expiry live pool landing warnings", Knockback);
        run("Carnivale Pit background first component activation current raw bootstrap source clocks", Background);
    }
    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(796);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(uint oid = 0x2739, bool combat = true)
        {
            Actors = [new(1, oid, Center, InCombat: combat, Name: "Primary", IsEnemy: true, ObjectIndex: 2),
                new(2, 0x233C, Center), new(100, 0, Center + Vector3.UnitZ * 5, Player: true, PartySlot: 0),
                new(101, 0, Center + Vector3.UnitX * 5, Player: true, PartySlot: 1)];
            Sync();
        }
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> fn)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = fn(Actors[i]);
        }
        internal void Sync() => E.Synchronize(Actors, Center, Now, playerId: 100);
        internal void Cast(uint action, ulong source = 2, ulong target = 100, float rotation = 0)
        {
            Now = Now.AddSeconds(.01);
            if (Actors.All(a => a.Id != source)) Actors.Add(new(source, 0x233C, Center));
            Set(source, a => a with { Cast = new(action, Center, rotation, Now, Now.AddSeconds(4), target) });
            Sync();
        }
        internal DungeonAmbientAoe[] Pools() => E.AmbientAoes().Where(a => a.Id.StartsWith("carnivale/vacuum/")).ToArray();
        internal (CarnivaleKnockbackCast Source, Vector3 From, Vector3 To)[] Moves() =>
            E.CarnivaleCastKnockbackMovements(Actors.Single(a => a.Id == 100), 15325).ToArray();
    }
    private static void FirstAct()
    {
        var f = new Fixture(0x2735, false);
        f.Actors.Add(new(3, 0x2734, Center, Name: "Viking", Targetable: false, InCombat: true));
        f.Actors.Add(new(4, 0x2734, Center, Name: "Second"));
        f.Sync();
        Check(f.E.Active?.BossOid == 0x2735 && f.E.Hints(Center).Contains("Primary is immune to magical damage!")
            && f.E.Hints(Center).Contains("Viking is immune to physical damage!"), "Group pull/immunity hints.");
        f.Set(3, a => a with { Dead = true });
        f.Set(1, a => a with { Dead = true });
        f.Set(4, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active != null && !f.E.Hints(Center).Any(h => h.Contains("immune to"))
            && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1,
            "First Viking hint skipped dead or HP0 ended group.");
        f.Actors.RemoveAll(a => a.Id == 4);
        f.Sync();
        Check(f.E.Active == null, "AllDeadOrDestroyed.");
    }
    private static void SecondAct()
    {
        var f = new Fixture(0x2736);
        f.Actors.Add(new(3, 0x2737, Center, Name: "Viking", IsEnemy: true, ObjectIndex: 4));
        f.Sync();
        Check(!f.E.Hints(Center).Any(h => h.Contains("immune to")), "Source text bug silently corrected.");
        f.Actors.Add(new(4, 0x2738, Center, Name: "Magus", Hp: 0, IsEnemy: true, ObjectIndex: 6));
        f.Sync();
        Check(f.E.Hints(Center).Count(h => h.StartsWith("Magus is immune to")) == 2, "Two branches did not use Magus.");
        Check(f.E.PriorityTarget(20, Center) is 3 or 4, "Add priority.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null, "Default Primary completion waited for adds.");
    }
    private static void Vacuum()
    {
        var f = new Fixture();
        var pos = Center + new Vector3(1.12345f, 3, 2.37654f);
        f.Actors.Add(new(3, 0x273B, pos, Targetable: false, EventState: 7));
        f.Actors.Add(new(4, 0x273A, Center, Name: "Magus", Targetable: false));
        f.Sync();
        Check(f.Pools().Single() is { Obstacle: true, Navigate: true, Shape: CircleTemplate { Radius: 3 } }
            && f.Pools()[0].Position == pos && f.E.Hints(Center).Contains("GTFO from voidzone!")
            && f.E.Hints(Center).Any(h => h.StartsWith("Kill Magus fast"))
            && f.E.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 1, "Vacuum filters/geometry/markers.");
        f.E.Finish(3, 15328, f.Now);
        f.Set(3, a => a with { Dead = true });
        f.Sync();
        Check(!f.Pools().Any(), "Dead vacuum remained.");
        f.Set(3, a => a with { Dead = false, Position = pos + Vector3.UnitX });
        f.Sync();
        Check(f.Pools().Single().Position == pos + Vector3.UnitX, "Revived vacuum missing/did not move.");
        f.Actors.RemoveAll(a => a.Id == 3);
        f.Sync();
        Check(!f.Pools().Any(), "Destroyed vacuum remained.");
    }
    private static void Ordinary()
    {
        foreach (var (oid, action) in new[]
        {
            (0x2735u, 15317u), (0x2735u, 15316u), (0x2736u, 15317u), (0x2736u, 15316u),
            (0x2736u, 15322u), (0x2739u, 15327u), (0x2739u, 15324u),
        })
        {
            var f = new Fixture(oid);
            f.Cast(action);
            var pending = f.E.OutputCasts.Single().Value;
            if (action == 15324) Check(pending.Rule.Shape is FanTemplate { Radius: 8 }, "PageTear inferred R9.");
            f.Now = Start.AddSeconds(20);
            f.Sync();
            f.E.Effect(2, action, 1, f.Now);
            Check(ReferenceEquals(f.E.OutputCasts.Single().Value, pending), "Effect/timeout re-created/cleared ordinary.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.OutputCasts.Any(), "Ordinary Finish.");
        }
    }
    private static void Interrupts()
    {
        foreach (var (oid, action) in new[] { (0x2735u, 15318u), (0x2736u, 15321u) })
        {
            var f = new Fixture(oid);
            f.Cast(action);
            Check(f.E.CarnivaleInterrupts.Single().Cast.Rule.Action == action
                && f.E.Hints(Center).Contains("Interrupt!") && !f.E.PredictedDamage.Any(), "Interrupt intent scope.");
            f.E.Finish(2, action, f.Now);
            Check(!f.E.CarnivaleInterrupts.Any(), "Interrupt Finish.");
        }
    }
    private static void SingleTarget()
    {
        var f = new Fixture(0x2736);
        f.E.CurrentPartyMembers = [new(0, "DPS", 100), new(1, "DPS", 101), new(63, "DPS", 999)];
        f.Cast(15319);
        f.Cast(15320, 3, 3);
        f.Set(3, a => a with { TargetId = 999 });
        f.Sync();
        Check(f.E.PredictedDamage.Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 999 })
            && f.E.PredictedDamage.All(d => d.DamageType == "Tankbuster"), "Shared single targets/self fallback/missing actor.");
        f.Set(2, a => a with { Cast = a.Cast! with { TargetId = 101, FinishAt = Start.AddSeconds(30) } });
        f.Set(101, a => a with { Dead = true });
        f.Sync();
        Check(f.E.PredictedDamage.Any(d => d.TargetId == 101 && d.Activation == Start.AddSeconds(30)),
            "Current raw target/dead roster/clock lost.");
        f.E.Finish(2, 15319, f.Now);
        Check(f.E.PredictedDamage.Single().TargetId == 999, "One action Finish cleared both.");
    }
    private static void Charge()
    {
        var f = new Fixture();
        f.Cast(15325, target: 100, rotation: MathF.PI / 2);
        Check(f.E.CarnivaleCharges.Single().Target.Actor.Id == 100
            && f.Moves().Single().Source is { Forward: true, Distance: 10, StopAtWall: false, EffectShape: null },
            "Charge or independent KB model.");
        var kb = f.Moves().Single();
        f.Set(100, a => a with { Position = Center + Vector3.UnitZ * 10 });
        f.Set(2, a => a with { Position = Center + Vector3.UnitX * 3, Rotation = -1 });
        f.Sync();
        Check(MathF.Abs(f.Moves().Single().To.X - 110) < .001f
            && f.E.CarnivaleCastKnockbacks.Single().Cast.Rotation == kb.Source.Cast.Rotation,
            $"Forward KB followed dynamic bait/caster facing: {f.Moves().Single()}, old={kb.Source.Cast.Rotation}.");
        var oldTarget = f.E.CarnivaleCharges.Single().Target;
        f.Actors.RemoveAll(a => a.Id == 100);
        f.Sync();
        f.Actors.Add(new(100, 0, Center - Vector3.UnitZ * 8, PartySlot: 0, Player: true));
        f.Sync();
        Check(ReferenceEquals(f.E.CarnivaleCharges.Single().Target, oldTarget) && oldTarget.Removed
            && f.E.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")), "Recreated target rebound/no avoid.");
        f.E.Finish(2, 15325, f.Now);
        Check(!f.E.CarnivaleCharges.Any() && !f.Moves().Any(), "Charge/KB Finish isolation.");
    }
    private static void Knockback()
    {
        var f = new Fixture();
        f.Cast(15325, target: 999, rotation: MathF.PI / 2);
        f.Cast(15325, 3, target: 999, rotation: MathF.PI);
        var moves = f.Moves();
        Check(!f.E.CarnivaleCharges.Any() && moves.Length == 2 && moves[1].From == moves[0].To
            && MathF.Abs(moves[1].To.X - 110) < .001f && MathF.Abs(moves[1].To.Z - 95) < .001f,
            "KB incorrectly requires bait target/rectangle or failed sequential forward chain.");
        f.Actors.Add(new(4, 0x273B, moves[0].To, Targetable: false, EventState: 7));
        f.Sync();
        Check(f.E.Hints(Center).Contains("About to be knocked into danger!"), "Live pool landing not warned.");
        f.Set(4, a => a with { Dead = true });
        f.Sync();
        Check(!f.E.Hints(Center).Contains("About to be knocked into danger!"), "Dead pool still warned.");
        var activation = moves[0].Source.Cast.FinishAt;
        f.E.StatusChanged(100, 1722, true, f.Now, expiresAt: activation);
        Check(f.Moves().Length == 2, "Equal expiry immune.");
        f.E.StatusChanged(100, 1722, true, f.Now, expiresAt: Start.AddSeconds(10));
        Check(!f.Moves().Any(), "KB immunity not applied.");
    }
    private static void Background()
    {
        var f = new Fixture(0x2736, false);
        f.Cast(15321);
        f.Cast(15319, 3);
        f.Now = Start.AddSeconds(10);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.CarnivaleInterrupts.Single().Cast.Rule.Action == 15321
            && f.E.PredictedDamage.Single().TargetId == 100, "Pre-pull cast activation.");
        f.E.Finish(2, 15321, f.Now);
        f.Sync();
        Check(!f.E.CarnivaleInterrupts.Any(), "Consumed interrupt raw resurrected.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

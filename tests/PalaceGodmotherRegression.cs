using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class PalaceGodmotherRegression
{
    private static readonly DateTime Start = new(2026, 11, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-300, 0, -236);

    internal static void Run(Action<string, Action> run)
    {
        run("Palace Godmother exact native rules and no floor automation", Rules);
        run("Palace Godmother creation lock deadline death removal identity and real Finish whole wave", Explosions);
        run("Palace Godmother first Giddy native effect index repeated destination and soft pull", Giddy);
        run("Palace Godmother dynamic all cleaves exact suppression and party direction", Cleave);
        run("Palace Godmother Raidwide raw clock no circle and observed cancellation", Damage);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(596);
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal Fixture(bool combat = true)
        {
            Actors = [new(1, 0x1817, Center, InCombat: combat, TargetId: 100, HitboxRadius: 3.75f),
                new(2, 0x18E8, Center + Vector3.UnitX * 4),
                new(3, 0x18EA, Center + Vector3.UnitZ * 5),
                new(4, 0x18EA, Center + Vector3.UnitZ * 6),
                new(100, 0, Center + Vector3.UnitX * 12, PartySlot: 0, Player: true),
                new(101, 0, Center + Vector3.UnitZ * 15, PartySlot: 1, Player: true)];
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.First(a => a.Id == 100).Position, Now, null, 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change) => Actors[Actors.FindIndex(a => a.Id == id)] = change(Actors.First(a => a.Id == id));
        internal void Cast(ulong source, uint action, double duration = 3)
        {
            Now = Now.AddSeconds(.01);
            Set(source, a => a with { Cast = new(action, Center + Vector3.UnitX * 2, .7f, Now, Now.AddSeconds(duration), 100, TotalTime: (float)duration) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith("godmother/" + prefix)).ToArray();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void Rules()
    {
        var f = new Fixture(false);
        Check(f.E.Active == null && f.E.Definitions.Single().BossOid == 0x1817
            && ArrDungeonCatalog.Owns(0x1817, 596), "Boss ownership or precombat gate.");
        f.E.Effect(999, 7104, 1, f.Now);
        Check(f.E.GodmotherBombIndex == 0, "Inactive component consumed effect.");
        var rules = f.E.Definitions.Single().Casts;
        Check(rules.Length == 5 && rules.Count(r => r.Shape != null) == 2 && rules.All(r => r.Action != 7100)
            && rules.Single(r => r.Action == 7104).Shape == new CircleTemplate(7.2f)
            && rules.Single(r => r.Action == 7101).Shape == new CircleTemplate(8), "Source active rules or exact geometry.");
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.PriorityTarget(1, Center) == 2 && f.E.ForbiddenDotTargets.SequenceEqual([2ul])
            && !f.E.ForbiddenTargets.Contains(2ul), "Grey priority5 DoT diagnostic became whole attack ban.");
        Check(f.Aoes("").All(a => !a.Id.Contains("floor", StringComparison.OrdinalIgnoreCase))
            && f.Aoes("").All(a => !a.Safe), "Invented floor or pull without dead Giddy.");
        f.Set(1, a => a with { InCombat = false });
        f.Sync();
        Check(f.E.Active != null, "Disengage completed default phase.");
        f.Set(1, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active == null && f.E.GodmotherExplosions.Count == 0, "Primary HP0 did not end.");
    }

    private static void Explosions()
    {
        var cold = new Fixture();
        cold.Actors.Add(new(10, 0x18E9, Center));
        cold.Actors.Add(new(11, 0x18E9, Center + Vector3.UnitX));
        var firstInput = new DungeonEncounter(596);
        firstInput.Synchronize(cold.Actors, Center, cold.Now, null, 100);
        Check(firstInput.GodmotherExplosions.Count == 2
            && firstInput.GodmotherExplosions.All(e => e.Activation == cold.Now.AddSeconds(10)), "First active input delayed bootstrap.");
        var f = new Fixture(false);
        var origin = Center + Vector3.UnitX * 8;
        f.Actors.Add(new(10, 0x18E9, origin));
        f.Sync();
        Check(f.E.GodmotherExplosions.Count == 0, "Precombat created an active prediction.");
        f.Now = Start.AddSeconds(2);
        f.Set(1, a => a with { InCombat = true });
        f.Sync();
        var first = f.E.GodmotherExplosions.Single();
        Check(first.Activation == f.Now.AddSeconds(10) && Vector3.Distance(first.Origin, origin) < .04f, "Bootstrap lock origin/activation.");
        f.Set(10, a => a with { Position = Center, Dead = true });
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(ReferenceEquals(first, f.E.GodmotherExplosions.Single())
            && f.Aoes("explosion/").Single().Shape == new CircleTemplate(6.6f), "Death/deadline moved or removed prediction.");
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Sync();
        Check(first.Source.Removed && f.E.GodmotherExplosions.Count == 1, "Destroy incorrectly cleared uncased prediction.");
        f.Actors.Add(new(10, 0x18E9, Center));
        f.Sync();
        Check(f.E.GodmotherExplosions.Count == 2
            && !ReferenceEquals(first.Source, f.E.GodmotherExplosions[1].Source), "Same ID rebound old prediction.");
        f.E.Effect(10, 7106, 11, f.Now);
        Check(f.E.GodmotherExplosions.Count == 2, "Effect stood in for real Finish.");
        f.Actors.Add(new(11, 0x233C, Center));
        f.Cast(11, 7106);
        f.Set(11, a => a with { Cast = null });
        f.Sync();
        Check(f.E.GodmotherExplosions.Count == 0, "Any-source real Finish did not clear whole wave.");
        f.Actors.Add(new(12, 0x18E9, Center));
        f.Sync();
        f.Cast(12, 7106);
        f.Actors.RemoveAll(a => a.Id == 12);
        f.Sync();
        Check(f.E.GodmotherExplosions.Count == 0, "Destroy must Finish before Remove.");
    }

    private static void Giddy()
    {
        var f = new Fixture();
        Check(f.E.ForbiddenTargets.SequenceEqual([3ul]), "No-cast first Giddy range ban.");
        f.Actors.Reverse();
        f.Sync();
        Check(f.E.ForbiddenTargets.SequenceEqual([3ul]), "Snapshot reorder changed first Giddy.");
        f.Set(3, a => a with { Dead = true, Targetable = false });
        f.Sync();
        Check(f.E.ForbiddenTargets.SequenceEqual([3ul]) && f.E.GodmotherPull?.Destination == new Vector3(-305, 0, -240),
            "Dead/untargetable Giddy ban or tank pull.");
        var goal = f.Aoes("pull").Single();
        var destination = f.E.GodmotherPull!.Destination;
        var expected = destination - Vector3.Normalize(Center - destination) * 6.25f;
        Check(goal.Safe && !goal.Navigate && !goal.Display && goal.GoalPriority == 10 && goal.Shape == new CircleTemplate(.5f)
            && Vector3.Distance(goal.Position, expected) < .001f, "Pull source soft goal formula.");
        Check(MasterPlayer.MechanicHost.MechanicHintsConvert.ConvertGoalZones([_ => 10],
            new BossMod.WPos(Center.X, Center.Z), 25, 0).Count == 0, "Existing bridge weight10 consumption boundary changed.");
        f.Cast(3, 7104);
        f.Set(3, a => a with { Cast = null });
        f.Sync();
        Check(f.E.GodmotherBombIndex == 0, "Start/Finish advanced native index.");
        f.E.Effect(999, 7104, 5, f.Now);
        Check(f.E.GodmotherBombIndex == 1 && f.E.GodmotherPull!.Destination.X == -295, "First complete native packet did not advance.");
        var repeated = f.E.GodmotherPull.Destination;
        f.E.Effect(999, 7104, 5, f.Now);
        Check(f.E.GodmotherBombIndex == 2 && f.E.GodmotherPull!.Destination == repeated, "Duplicate source waypoint removed.");
        f.E.Effect(999, 7104, 0, f.Now);
        Check(f.E.GodmotherBombIndex == 3 && f.E.GodmotherPull!.Destination == new Vector3(-300, 0, -235),
            "Independent complete native effects did not advance.");
        f.E.Effect(999, 7104, 0, f.Now);
        Check(f.E.GodmotherBombIndex == 4 && f.E.GodmotherPull!.Destination == new Vector3(-305, 0, -240),
            "Sequence-zero separate effects did not wrap.");
        f.Set(1, a => a with { TargetId = 101 });
        f.Sync();
        Check(f.E.GodmotherPull == null, "Non-tank got pull goal.");
        f.Set(1, a => a with { TargetId = 100 });
        f.Actors.RemoveAll(a => a.Oid == 0x18EA);
        f.Sync();
        Check(f.E.GodmotherPull == null && f.E.ForbiddenTargets.Length == 0, "Absent Giddy induced pull or stale ban.");
        f.E.PalaceGuideDirector(0x40000005);
        f.E.Effect(999, 7104, 7, f.Now);
        f.Sync();
        Check(f.E.Active == null && f.E.GodmotherBombIndex == 0 && !f.E.AmbientAoes().Any(a => a.Navigate),
            "Wipe retained outputs or relaunched stale InCombat owner.");
        f.E.PalaceGuideDirector(0x40000011);
        f.Sync();
        Check(f.E.Active == null, "Resume reconstructed old owner.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x1817, Center, InCombat: true, TargetId: 100));
        f.Sync();
        Check(f.E.Active != null, "Real new owner failed after wipe.");
        f.Set(1, a => a with { Dead = true });
        f.Sync();
        f.Set(1, a => a with { Dead = false });
        f.Sync();
        Check(f.E.Active == null, "Finished old identity automatically restarted.");
    }

    private static void Cleave()
    {
        var f = new Fixture();
        Check(f.Aoes("cleave/").Count(a => a.Display) == 1
            && f.Aoes("cleave/").Single(a => a.Navigate).Shape == new FanTemplate(100, 2 * MathF.PI / 3),
            "Own-target party direction.");
        f.Set(1, a => a with { TargetId = 101, Rotation = MathF.PI });
        f.Sync();
        var shape = f.Aoes("cleave/").Single(a => a.Display);
        Check(shape.Shape == new FanTemplate(11.75f, 2 * MathF.PI / 3) && shape.OutlineOnly
            && MathF.Abs(shape.Rotation) < .001f, "Cleave exact geometry/target direction.");
        f.Cast(2, 7105);
        Check(f.Aoes("cleave/").Length == 2, "Grey Raidwide wrongly suppressed cleave.");
        f.Cast(1, 99999);
        Check(f.Aoes("cleave/").Length == 2, "Unknown cast wrongly suppressed activeWhileCasting cleave.");
        f.Cast(1, 7101);
        Check(f.Aoes("cleave/").Length == 0, "Sap did not suppress cleave.");
        f.Cast(1, 7102);
        Check(f.Aoes("cleave/").Length == 0, "Massive Burst did not suppress cleave.");
        f.Set(1, a => a with { Cast = null });
        f.Sync();
        Check(f.Aoes("cleave/").Length == 2, "Finish did not restore cleave.");
        f.Set(1, a => a with { Position = new(-300, 0, -235.981f), TargetId = 101 });
        f.Set(100, a => a with { Position = new(-300, 0, -224.231f) });
        f.Set(101, a => a with { Position = new(-300, 0, -210) });
        f.Sync();
        Check(f.E.Hints(f.Actors.Single(a => a.Id == 100).Position).Contains("GTFO from cleave!"),
            "Cleave text must use raw origin and quantized player.");
    }

    private static void Damage()
    {
        var f = new Fixture();
        f.Cast(1, 7102);
        f.Cast(2, 7105);
        Check(f.E.PredictedDamage.Count() == 4 && f.E.Casts.Values.All(c => c.Rule.Shape == null), "Unavoidable damage became spatial AOE.");
        f.Now = Start.AddSeconds(40);
        f.Sync();
        Check(f.E.PredictedDamage.All(p => p.Activation > f.Now), "NPC saturated raw clock became expired constant.");
        f.Set(1, a => a with { Cast = null });
        f.Set(2, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.PredictedDamage.Any(), "Observed end retained damage.");
    }
}

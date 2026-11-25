using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class MainScenarioTimeRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(-247, 0, 540);

    public static void Run(Action<string, Action> run)
    {
        run("MSQ Time exact five action component table and eight vertex world arena", Catalog);
        run("MSQ Time primary death HP0 and destroy do not substitute P1 untargetable transition", Primary);
        run("MSQ Time P2 retains dead actor until actual removal completion latch and new raw generation", Phases);
        run("MSQ Time absent P2 actor completes immediately on first untargetable update", MissingP2);
        run("MSQ Time created flames retain source timing frozen quantized origin and source ID0 semantics", Flames);
        run("MSQ Time raw lifecycle ignores late duplicate Create and preserves true absence generations", CreationReceipts);
        run("MSQ Time phase transition resets components replays existing flames and enables sword", ReplayPhase);
        run("MSQ Time Raidwide only predicts party damage without fake geometry", Raidwide);
        run("MSQ Time phase scoped default primary and extra P2 actor markers", Markers);
        run("MSQ Time Director wipe and disabled mechanism clear all native outputs", Wipe);
        run("MSQ Time public production solver escapes vein splitter and owner clear", NativeOutput);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E = new(685);
        internal readonly List<DungeonActor> Actors =
        [
            new(1, 0x1A36, Center, InCombat: false, Hp: 1000, IsEnemy: true, ObjectIndex: 2),
            new(100, 0, Center + Vector3.UnitZ, PartySlot: 0, Player: true),
            new(101, 0, Center + Vector3.UnitX * 12, PartySlot: 1, Player: true),
        ];
        internal DateTime Now = Start;
        internal bool Enabled = true;
        internal Fixture() => Sync();
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == 100).Position, Now,
            enabled: _ => Enabled, playerId: 100);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var i = Actors.FindIndex(a => a.Id == id);
            Actors[i] = change(Actors[i]);
        }
        internal void Pull()
        {
            Set(1, a => a with { InCombat = true });
            Sync();
        }
        internal void Cast(ulong source, uint action, double duration = 3)
        {
            var observation = new DungeonCastObservation(action, Center, .4f, Now, Now.AddSeconds(duration + .3));
            Set(source, a => a with { Cast = observation });
            E.StartCast(source, observation, Now);
            Sync();
        }
        internal void Phase2()
        {
            Actors.Add(new(11, 0x1EBC, Center));
            Set(1, a => a with { Targetable = false });
            Sync();
        }
    }

    private static void Catalog()
    {
        var f = new Fixture();
        Check(f.E.Definitions.Single().Casts.Select(c => c.Action).SequenceEqual(new uint[] { 8987, 8986, 8988, 8985, 8993 })
            && MainScenarioQuestCatalog.Owns(685, 0x1A36) && !MainScenarioQuestCatalog.Owns(614, 0x1A36),
            "Wrong activated action table or territory.");
        f.Pull();
        Check(f.E.Arena!.GetContours().Single().Count == 8
            && Vector3.Distance(f.E.Arena.Position, new(-248.45f, 0, 544.81f)) < .0001f,
            "Source concave arena lost vertices or AABB center.");
    }

    private static void Primary()
    {
        var f = new Fixture();
        f.Set(1, a => a with { InCombat = true, Dead = true, Hp = 0 });
        f.Sync();
        Check(f.E.Active != null && f.E.TimeBetweenPhase == 0 && f.E.PreserveStageAcrossBattleEnd,
            "Default dead HP0 pull was replaced by default death-phase handling.");
        f.E.RemoveActor(1);
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active != null && f.E.TimeBetweenPhase == 0,
            "Destroyed but formerly targetable original primary changed the source P1 predicate.");
        f.Actors.Add(new(1, 0x1A36, Center, Targetable: false));
        f.Actors.Add(new(11, 0x1EBC, Center));
        f.Sync();
        Check(f.E.Active != null && f.E.TimeBetweenPhase == 1,
            "Still-loaded source module failed same ID/OID primary rebind after raw absence.");
        f.Actors.Add(new(9, 0x1CED, Center));
        f.Sync();
        var flame = f.E.TimeBetweenFlames.Single();
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(new(1, 0x1A36, Center, Targetable: true));
        f.Sync();
        Check(f.E.TimeBetweenPhase == 1 && f.E.TimeBetweenFlames.Single() == flame,
            "P2 primary rebind restarted the stage or replaced active flame components.");
    }

    private static void Phases()
    {
        var f = new Fixture();
        f.Pull();
        f.Phase2();
        Check(f.E.Active != null && f.E.TimeBetweenPhase == 1, "Untargetable primary did not advance P2.");
        f.Set(11, a => a with { Dead = true, Hp = 0, Targetable = false });
        f.Sync();
        Check(f.E.Active != null, "P2 dead object was mistaken for empty Enemies list.");
        f.Actors.RemoveAll(a => a.Id == 11);
        f.Sync();
        Check(f.E.Active == null, "Actual last P2 actor removal failed completion.");
        f.Set(1, a => a with { Targetable = true, Dead = false, Hp = 1000 });
        f.Sync();
        Check(f.E.Active == null, "Completed original primary repulled from healing.");
        f.Now = Start.AddSeconds(1);
        f.E.Tick(f.Now);
        f.E.ObjectCreated(1, f.Now, 0x1A36);
        f.Sync();
        Check(f.E.Active == null, "PR Create bypassed raw completion latch without an observed new generation.");
        var original = f.Actors.Single(a => a.Id == 1);
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        f.Actors.Add(original);
        f.Sync();
        Check(f.E.Active != null && f.E.TimeBetweenPhase == 0, "New raw primary generation failed to pull.");
    }

    private static void MissingP2()
    {
        var f = new Fixture();
        f.Pull();
        f.Set(1, a => a with { Targetable = false });
        f.Sync();
        Check(f.E.Active == null, "State machine did not immediately evaluate empty P2 after P1 transition.");
    }

    private static void Flames()
    {
        var f = new Fixture();
        var original = Center + new Vector3(1.23456f, 0, 2.34567f);
        f.Actors.Add(new(9, 0x1CED, original, Targetable: false));
        f.Pull();
        var initial = f.E.TimeBetweenFlames.Single();
        Check(initial.Source == 9 && initial.Activation == Start.AddSeconds(7)
            && initial.Origin != original && Vector3.Distance(initial.Origin, original) < .05f,
            "Created flame lost original identity/time/quantization.");
        f.Now = Start.AddSeconds(1);
        f.Set(9, a => a with { Position = Center + Vector3.UnitX * 20 });
        f.Cast(9, 8988, .5);
        f.E.Finish(9, 8988, f.Now.AddSeconds(.5));
        f.E.Effect(9, 8988, 1, f.Now.AddSeconds(.5));
        f.E.RemoveActor(9);
        f.Actors.RemoveAll(a => a.Id == 9);
        f.Now = Start.AddSeconds(20);
        f.Sync();
        Check(f.E.TimeBetweenFlames.Single() == initial,
            "Normal nonzero source changed/consumed source AOEInstance whose ActorID defaults to zero.");
        f.E.ObjectCreated(9, Start, 0x1CED, Center + Vector3.UnitZ * 3);
        f.Actors.Add(new(9, 0x1CED, Center + Vector3.UnitZ * 3));
        f.Sync();
        Check(f.E.TimeBetweenFlames.Count == 2 && f.E.TimeBetweenFlames[1].Generation != initial.Generation
            && f.E.TimeBetweenFlames[1].Activation == f.Now.AddSeconds(7),
            "Same ID new generation relocked old flame or event/raw duplicated new creation.");
    }

    private static void ReplayPhase()
    {
        var f = new Fixture();
        f.Actors.Add(new(9, 0x1CED, Center + Vector3.UnitZ * 3));
        f.Actors.Add(new(10, 0x1CEE, Center));
        f.Pull();
        f.Cast(10, 8993);
        f.Cast(1, 8987);
        var cancelled = f.Actors.Single(a => a.Id == 1).Cast!;
        f.Set(1, a => a with { Cast = null });
        f.Sync();
        Check(!f.E.OutputCasts.Any(c => c.Value.Rule.Action == 8993), "P1 activated P2-only sword component.");
        f.Now = Start.AddSeconds(2);
        f.Phase2();
        Check(f.E.OutputCasts.Any(c => c.Value.Rule.Action == 8993)
            && f.E.TimeBetweenFlames.Single().Activation == f.Now.AddSeconds(7),
            "Phase entry failed component reset and current actor/cast replay.");
        f.E.StartCast(1, cancelled, f.Now);
        Check(!f.E.OutputCasts.Any(c => c.Value.Rule.Action == 8987),
            "A late cancelled P1 packet revived after component replacement.");
    }

    private static void CreationReceipts()
    {
        var f = new Fixture();
        f.Pull();
        f.E.ObjectCreated(1, Start, 0x1A36, Center);
        f.Actors.Add(new(9, 0x1CED, Center + Vector3.UnitZ * 3));
        f.Sync();
        f.E.ObjectCreated(9, Start, 0x1CED, Center + Vector3.UnitZ * 3);
        Check(f.E.TimeBetweenFlames.Count == 1, "Raw-first creation acknowledgement duplicated a flame.");
        f.Now = Start.AddMilliseconds(10);
        f.E.Tick(f.Now);
        f.E.ObjectCreated(1, f.Now, 0x1A36, Center);
        f.E.ObjectCreated(9, f.Now, 0x1CED, Center + Vector3.UnitZ * 3);
        f.Sync();
        Check(f.E.TimeBetweenFlames.Count == 1, "Later PR scan clock duplicated the existing raw flame.");
        f.Set(9, a => a with { Oid = 0x233C });
        f.Now = Start.AddSeconds(1);
        f.Phase2();
        Check(f.E.TimeBetweenPhase == 1 && f.E.TimeBetweenFlames.Count == 1,
            "PR confirmation froze the original primary or continuous ID OID changes rebuilt source identity.");
        f.Now = Start.AddSeconds(2);
        f.E.Tick(f.Now);
        f.E.ObjectCreated(20, f.Now, 0x1CED, Center + Vector3.UnitX * 3);
        f.Sync();
        f.Now = Start.AddSeconds(3);
        f.Sync();
        f.Now = Start.AddSeconds(4);
        f.Actors.Add(new(20, 0x1CED, Center + Vector3.UnitX * 3));
        f.Sync();
        Check(f.E.TimeBetweenFlames.Count == 2 && f.E.TimeBetweenFlames[1].Activation == Start.AddSeconds(11),
            "PR event created a flame before its first source-compatible raw frame.");
        f.Now = Start.AddSeconds(5);
        f.E.Tick(f.Now);
        f.E.ObjectCreated(20, f.Now, 0x1CED, Center - Vector3.UnitX * 3);
        f.Sync();
        Check(f.E.TimeBetweenFlames.Count == 2, "Duplicate Create invented a generation while raw remained present.");
        f.Actors.RemoveAll(a => a.Id == 20);
        f.Now = Start.AddSeconds(6);
        f.Sync();
        f.Actors.Add(new(20, 0x1CED, Center - Vector3.UnitX * 3));
        f.Now = Start.AddSeconds(7);
        f.Sync();
        Check(f.E.TimeBetweenFlames.Count == 3
            && f.E.TimeBetweenFlames[2].Activation == Start.AddSeconds(14),
            "Raw absence and return failed to create the next flame generation.");
        f.Now = Start.AddSeconds(8);
        f.E.ObjectCreated(21, f.Now, 0x1CED, Center);
        f.Actors.Add(new(21, 0x1CED, Center));
        f.Sync();
        f.Now = Start.AddSeconds(8.1);
        f.Actors.RemoveAll(a => a.Id == 21);
        f.Sync();
        f.Now = Start.AddSeconds(8.2);
        f.Actors.Add(new(21, 0x1CED, Center + Vector3.UnitX));
        f.Sync();
        Check(f.E.TimeBetweenFlames.Count == 5
            && f.E.TimeBetweenFlames[^1].Activation == f.Now.AddSeconds(7),
            "Same-clock raw confirmation then brief raw absence lost a real generation.");
    }

    private static void Raidwide()
    {
        var f = new Fixture();
        f.Pull();
        f.Cast(1, 8986);
        Check(f.E.OutputCasts.All(c => c.Value.Rule.Shape == null)
            && f.E.PredictedDamage.Count() == 2
            && f.E.PredictedDamage.All(p => p.DamageType == "Raidwide"),
            "Raidwide created geometry or lost ordinary party damage prediction.");
    }

    private static void Markers()
    {
        var f = new Fixture();
        f.Actors.Add(new(11, 0x1EBC, Center));
        f.Actors.Add(new(12, 0x1A37, Center));
        f.Actors.Add(new(13, 0x1CEE, Center));
        f.Pull();
        var before = f.E.AmbientAoes().Where(a => a.Id.StartsWith("job-quest/enemy/")).ToArray();
        Check(before.Length == 1 && before[0].Id.EndsWith("/1"), "P1 default primary marker expanded to all enemies.");
        f.Set(1, a => a with { Targetable = false });
        f.Sync();
        var after = f.E.AmbientAoes().Where(a => a.Id.StartsWith("job-quest/enemy/")).ToArray();
        Check(after.Length == 2 && after.Any(a => a.Id.EndsWith("/11")) && after.Any(a => a.Id.EndsWith("/12")),
            "P2 foreground lost exact two OID groups or bypassed targetable filter.");
    }

    private static void Wipe()
    {
        var f = new Fixture();
        f.Actors.Add(new(9, 0x1CED, Center + Vector3.UnitZ * 3));
        f.Pull();
        f.E.DirectorUpdate(0x40000005);
        f.Sync();
        Check(f.E.Active == null && f.E.TimeBetweenFlames.Count == 0, "Wipe retained phase or repulled stale actor.");
        f.E.DirectorUpdate(0x40000011);
        f.Sync();
        Check(f.E.Active != null && f.E.TimeBetweenFlames.Count == 1, "Wipe unlock failed current actor replay.");
        f.Enabled = false;
        f.Sync();
        Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Disabled mechanism retained phase output.");
    }

    private static void NativeOutput()
    {
        var f = new Fixture();
        f.Pull();
        f.Cast(1, 8987);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        var player = f.Actors[1].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0
            && Vector3.Distance(path[^1].Position, Center) >= 10, "Real solver failed native Vein Splitter escape.");
        output.Clear();
        Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Stopped output retained danger.");
    }
}

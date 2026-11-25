using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class QuestIseultalonDikaiosyneRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Quest Iseultalon exact eight components two tower families and primary scope", CatalogIseultalon);
        run("Quest Iseultalon electrocution tutorial counter positional finish and locked forbidden identity", Electrocution);
        run("Quest Iseultalon moved destroyed caster and simultaneous target historical Finish positions", ElectrocutionDestroyed);
        run("Quest Iseultalon tower occupancy display navigation prediction and forbidden inTower exception", TowerOccupancy);
        run("Quest Iseultalon second tower first FIFO immediate navigation without damage", SecondTower);
        run("Quest Iseultalon repeated icon actor identity default clock death revival frozen generation", Spreads);
        run("Quest Iseultalon dynamic wide ray consume clock arbitrary effect and independent narrow ray", Rays);
        run("Quest Iseultalon Needle Shot R5 actual target finish and independent Stomp damage", Stack);
        run("Quest Dikaiosyne exact eleven components sixteen actions and seven ordinary shapes", CatalogDikaiosyne);
        run("Quest Dikaiosyne six tower actions shared exact location first removal and first clock", Fracture);
        run("Quest Dikaiosyne knockback locks LocXZ sequential ten no wall stop immunity strict expiry", Knockback);
        run("Quest Dikaiosyne min one stack and R6 R7 spread independent references no invented damage", DikaiosyneParty);
        run("Quest native pair completion raw new generations wipe reopen disable and owner cleanup", Lifecycle);
        run("Quest native pair actual common output solver enters tower and clears owner", NativeOutput);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E;
        internal readonly Vector3 Center;
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Player = 100;
        internal bool Enabled = true;
        internal Fixture(bool dikaiosyne = false)
        {
            E = new(dikaiosyne ? 869u : 839u);
            Center = dikaiosyne ? new(-798.6f, 0, -40.49f) : new(-289, 0, -30);
            Actors =
            [
                new(1, dikaiosyne ? 0x2927u : 0x252Cu, Center, InCombat: true, Hp: 1000, IsEnemy: true),
                new(10, 0x233C, Center), new(11, 0x233C, Center), new(12, 0x233C, Center),
                new(100, 0, Center + Vector3.UnitX * 10, PartySlot: 0, Player: true),
                new(101, 0x2650, Center + Vector3.UnitX * 14, PartySlot: 24, Buddy: true, FriendlyNpc: true),
                new(102, 0, Center + Vector3.UnitZ * 10, PartySlot: 1, Player: true),
            ];
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == Player).Position, Now,
            enabled: _ => Enabled, playerId: Player);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void Cast(ulong source, uint action, Vector3? location = null, ulong target = 0, double duration = 10, float rotation = 0)
        {
            var observation = new DungeonCastObservation(action, location ?? Center, rotation, Now, Now.AddSeconds(duration + .3),
                target, Actors.Single(a => a.Id == source).Position);
            Set(source, a => a with { Cast = observation });
            E.StartCast(source, observation, Now);
            Sync();
        }
        internal void Finish(ulong source)
        {
            Set(source, a => a with { Cast = null });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix)
            => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
    }

    private static void CatalogIseultalon()
    {
        var f = new Fixture();
        Check(f.E.Active?.BossOid == 0x252C && f.E.Arena!.Position == f.Center
            && f.E.Arena.Contains(f.Center + Vector3.UnitX * 24.99f)
            && !f.E.Arena.Contains(f.Center + Vector3.UnitX * 25.01f), "Iseultalon source R25 or primary scope changed.");
        Check(f.E.Definitions.Single().Casts.Select(c => c.Action).SequenceEqual(new uint[]
            { 14605, 14602, 14603, 14606, 14607, 14609, 14610, 14981 }), "Missing activated cast or Finish-only observation.");
        Check(f.Aoes("job-quest/enemy/").Length == 2
            && f.Aoes("job-quest/enemy/").Single(a => a.Id.EndsWith("/101")).Color == new Vector4(1, 0, 1, 1),
            "Noctis vulnerability marker missing.");
        f.Cast(10, 15082);
        Check(!f.E.OutputCasts.Any() && !f.E.PredictedDamage.Any(), "Thunderbolt visual invented an AOE or damage.");
    }

    private static void Electrocution()
    {
        var f = new Fixture();
        f.Cast(10, 14609, f.Center + Vector3.UnitX * 20, target: 101);
        var tower = f.E.ElectrocutionTowers.Single();
        Check(tower.Position == f.Actors.Single(a => a.Id == 101).Position && tower.Activation == default
            && tower.ForbiddenSlot == -1, "Tutorial tower did not lock target raw position/default activation.");
        f.Set(101, a => a with { Position = f.Center + new Vector3(14.8f, 0, .8f) });
        f.Finish(10);
        Check(f.E.ElectrocutionTowers.Count == 0 && f.E.ElectrocutionNumCasts == 1, "Axial current target finish did not remove tutorial.");
        f.Now = Start.AddSeconds(1);
        f.Cast(10, 14609, f.Center);
        Check(f.E.ElectrocutionTowers.Single().ForbiddenSlot == 0, "Later tower did not capture current player slot.");
        f.Player = 102;
        f.Sync();
        Check(f.Aoes("iseultalon/tower/join").Length == 1, "Perspective change reassigned captured forbidden mask.");
        f.Player = 100;
        f.Sync();
        Check(f.Aoes("iseultalon/tower/join").Length == 0 && f.Aoes("iseultalon/tower/").Any(a => a.Id.EndsWith("/avoid")),
            "Original forbidden player gained soak navigation.");
        f.Now = Start.AddSeconds(2);
        f.Cast(11, 14610, f.Center + new Vector3(.8f, 0, .8f));
        f.Finish(11);
        Check(f.E.ElectrocutionTowers.Count == 0 && f.E.ElectrocutionNumCasts == 2, "FatalCurrent did not increment and clear axial matches.");
    }

    private static void TowerOccupancy()
    {
        var f = new Fixture();
        f.Cast(10, 14609);
        f.Set(101, a => a with { Position = f.Center });
        f.Sync();
        Check(f.Aoes("iseultalon/tower/").Any(a => a.Id.EndsWith("/avoid"))
            && f.Aoes("iseultalon/tower/").Single(a => a.Display).OutlineOnly == false
            && f.E.PredictedDamage.Single().TargetId == 101, "Occupied tower display/avoid/soaker prediction differs.");
        f.Finish(10);
        f.Now = Start.AddSeconds(1);
        f.Cast(10, 14609);
        f.Set(100, a => a with { Position = f.Center });
        f.Sync();
        Check(f.E.Hints(f.Center).Contains("GTFO from tower!")
            && !f.Aoes("iseultalon/tower/").Any(a => a.Navigate)
            && f.E.PredictedDamage.All(p => p.TargetId != 100),
            "Source forbidden inTower exception or forbidden damage mask was normalized away.");
    }

    private static void ElectrocutionDestroyed()
    {
        var f = new Fixture();
        f.Cast(10, 14609, target: 10);
        f.Set(10, a => a with { Position = f.Center + Vector3.UnitZ * 5 });
        f.Sync();
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Sync();
        Check(f.E.ElectrocutionNumCasts == 1 && f.E.ElectrocutionTowers.Count == 1,
            "Destroyed self caster used initial rather than last accepted position.");
        f = new Fixture();
        f.Cast(10, 14609, target: 101);
        f.Set(101, a => a with { Position = f.Center + Vector3.UnitZ * 3 });
        f.Sync();
        f.Actors.RemoveAll(a => a.Id is 10 or 101);
        f.Sync();
        Check(f.E.ElectrocutionTowers.Count == 1, "Simultaneous source-first destroy lost still-resolvable target last position.");
        f = new Fixture();
        f.Cast(10, 14609, target: 10);
        f.Now = f.Now.AddSeconds(1);
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Set(11, a => a with { Cast = new(14609, f.Center, 0, f.Now, f.Now.AddSeconds(10.3), 11, f.Center) });
        f.Sync();
        Check(f.E.ElectrocutionNumCasts == 1 && f.E.ElectrocutionTowers.Count == 1
            && f.E.ElectrocutionTowers[0].Cast.Source == 11 && f.E.ElectrocutionTowers[0].ForbiddenSlot == 0,
            "New same-frame tutorial tower started before old raw destruction Finish.");
    }

    private static void SecondTower()
    {
        var f = new Fixture();
        f.Set(101, a => a with { Position = f.Center });
        f.Cast(10, 14981);
        f.Cast(11, 14981, f.Center + Vector3.UnitZ * 10);
        var join = f.Aoes("iseultalon/tower2/join").Single();
        Check(join.Activation == default(DateTime) && !f.E.PredictedDamage.Any()
            && !join.Shape.Resolve(join.Position, join.Rotation, null).Contains(f.Center)
            && join.Shape.Resolve(join.Position, join.Rotation, null).Contains(f.Center + Vector3.UnitZ * 10),
            "Second tower inherited capacity/damage or joined a later tower.");
        f.Finish(10);
        Check(f.E.Electrocution2Towers.Count == 1 && f.E.Electrocution2Towers[0].Cast.Source == 11
            && f.E.ElectrocutionNumCasts == 0, "Cast tower FIFO removal polluted tutorial counter.");
    }

    private static void Spreads()
    {
        var f = new Fixture();
        f.E.Icon(101, 129, f.Now, 100);
        f.E.Icon(101, 129, f.Now, 100);
        Check(f.Aoes("iseultalon/spread/").Count(a => a.Display) == 2
            && f.Aoes("iseultalon/spread/").Where(a => a.Navigate).All(a => a.Activation == default(DateTime)),
            "Icon callback actor/default time/duplicate append changed.");
        Check(f.E.PredictedDamage.Single().TargetId == 100 && f.E.PredictedDamage.Single().Activation == default,
            "Spread mask did not use marked actor only.");
        f.Set(100, a => a with { Dead = true });
        f.Sync();
        Check(!f.Aoes("iseultalon/spread/").Any(), "Dead marked target still active.");
        f.Set(100, a => a with { Dead = false });
        f.Sync();
        Check(f.Aoes("iseultalon/spread/").Count(a => a.Display) == 2, "Revived original target lost marks.");
        f.Player = 102;
        var original = f.Actors.Single(a => a.Id == 100);
        f.Actors.Remove(original);
        f.Sync();
        f.Actors.Add(original with { Position = f.Center + Vector3.UnitX * 20 });
        f.Sync();
        Check(f.Aoes("iseultalon/spread/").Where(a => a.Display).All(a => Vector3.Distance(a.Position, original.Position) < .05f),
            "Same ID replacement stole old spread references.");
        f.E.Effect(12, 15083, 1, f.Now);
        Check(!f.Aoes("iseultalon/spread/").Any(), "Arbitrary source Thunderbolt effect failed global clear.");
    }

    private static void Rays()
    {
        var f = new Fixture();
        f.Now = Start.AddSeconds(2);
        f.Cast(1, 14602, duration: 4);
        var ray = f.Aoes("iseultalon/death-ray").Single();
        Check(ray.Activation == f.Now.AddSeconds(6.1d), "Wide ray used CastFinishAt instead of consumption+6.1.");
        f.Finish(1);
        var rawPosition = f.Center + new Vector3(1.12345f, 0, .23456f);
        f.Set(1, a => a with { Position = rawPosition, Rotation = MathF.PI / 2 });
        f.Now = Start.AddSeconds(20);
        f.Sync();
        ray = f.Aoes("iseultalon/death-ray").Single();
        Check(ray.Position == rawPosition && ray.Rotation == MathF.PI / 2 && ray.Activation == Start.AddSeconds(8.1d)
            && f.E.Hints(rawPosition + Vector3.UnitX * 5).Contains("GTFO from aoe!"),
            "Wide ray froze position, expired, or slid clock.");
        f.Cast(10, 14603, f.Center, rotation: MathF.PI / 2);
        Check(!f.E.OutputCasts.Any() && f.Aoes("iseultalon/narrow-ray/").Single().Color == new Vector4(1, 1, 0, 1),
            "Narrow ray lost permanent danger color or gained duplicate output.");
        f.E.Effect(12, 14604, 2, f.Now);
        Check(!f.Aoes("iseultalon/death-ray").Any() && f.Aoes("iseultalon/narrow-ray/").Length == 1,
            "Wide ray effect failed source-independent clear or cleared narrow cast.");
    }

    private static void Stack()
    {
        var f = new Fixture();
        f.Cast(10, 14606, target: 101);
        Check(f.Aoes("quest/stack/").Single(a => a.Display).Shape is CircleTemplate { Radius: 5 }
            && !f.E.PredictedDamage.Any(), "Needle Shot wrong R5 or invented damage.");
        f.Now = f.Now.AddSeconds(1);
        f.Cast(12, 14606, target: 100);
        Check(f.Aoes("quest/stack/join").Length == 1
            && !f.Aoes("quest/stack/").Any(a => a.Id.EndsWith("/avoid")),
            "Unlimited Needle Shot stacks gained R10 avoidance instead of allowing another R5 stack.");
        f.Cast(11, 14605);
        Check(f.E.PredictedDamage.Count() == 3 && f.E.OutputCasts.All(c => c.Value.Rule.Shape == null),
            "Stomp did not remain independent raidwide prediction.");
        f.E.Effect(10, 14606, 1, f.Now);
        Check(f.Aoes("quest/stack/").Any(), "Needle Shot cleared on effect.");
        f.Finish(10);
        Check(f.Aoes("quest/stack/").Count(a => a.Display) == 1, "Needle Shot failed target-specific Finish.");
        f.Finish(12);
        Check(!f.Aoes("quest/stack/").Any(), "Last Needle Shot did not clear on real Finish.");
    }

    private static void CatalogDikaiosyne()
    {
        var f = new Fixture(true);
        var arena = f.E.Arena ?? throw new InvalidOperationException("Dikaiosyne arena missing.");
        Check(f.E.Active?.BossOid == 0x2927 && f.E.Definitions.Single().Casts.Length == 16
            && arena.Position == f.Center && arena.Contains(f.Center + Vector3.UnitX * 19.99f)
            && !arena.Contains(f.Center + Vector3.UnitX * 20.01f), "Dikaiosyne exact source catalog/arena mismatch.");
        foreach (var action in new uint[] { 4747, 17427, 17430, 17552, 17470, 4746, 16131 })
        {
            f.Now = f.Now.AddSeconds(1);
            f.Cast(10, action);
            Check(f.E.OutputCasts.Count() == 1, $"Missing ordinary action {action}.");
            f.E.Effect(10, action, action, f.Now);
            Check(f.E.OutputCasts.Count() == 1, "Ordinary cast consumed on damage effect.");
            f.Finish(10);
            Check(!f.E.OutputCasts.Any(), "Observed ordinary finish not consumed.");
        }
    }

    private static void Fracture()
    {
        var f = new Fixture(true);
        f.Cast(10, 15576, duration: 5);
        f.Cast(11, 13207, duration: 10);
        f.Cast(12, 13208, f.Center + Vector3.UnitX * .001f);
        f.Set(101, a => a with { Position = f.Center });
        f.Sync();
        Check(f.E.FractureTowers.Count == 3 && f.E.PredictedDamage.Single().Activation == f.E.FractureTowers[0].Activation,
            "Six actions did not share source tower list/first clock.");
        f.Finish(11);
        Check(f.E.FractureTowers.Count == 2 && f.E.FractureTowers[0].Cast.Source == 11,
            "Fracture Finish removed own source instead of first exact location.");
        f.Finish(12);
        Check(f.E.FractureTowers.Count == 1 && f.E.FractureTowers[0].Cast.Source == 11,
            "Fracture used proximity instead of exact position or removed all matches.");
        var old = f.Actors.Single(a => a.Id == 10).Cast!;
        f.E.StartCast(10, old with { Location = f.Center + Vector3.UnitZ * 5 }, f.Now);
        Check(f.E.FractureTowers.Count == 1, "Consumed tower resurrected on same generation packet correction.");
        f.E.StartCast(10, old with { Location = f.Center + Vector3.UnitZ * 7 }, f.Now);
        Check(f.E.FractureTowers.Count == 1, "Second same generation correction resurrected a consumed tower.");
        f.Now = f.Now.AddSeconds(1);
        f.Cast(10, 15576, f.Center + Vector3.UnitZ * 7);
        Check(f.E.FractureTowers.Count == 2, "Consumed cast barrier rejected the next true generation.");
    }

    private static void Knockback()
    {
        var f = new Fixture(true);
        f.Set(100, a => a with { Position = f.Center + Vector3.UnitX * 15 });
        f.Cast(10, 16132, f.Center, duration: 3);
        f.Set(10, a => a with { Position = f.Center + Vector3.UnitZ * 15 });
        f.Sync();
        var player = f.Actors.Single(a => a.Id == 100);
        Check(Vector3.Distance(f.E.DikaiosyneLanding(player), f.Center + Vector3.UnitX * 25) < .001f
            && f.Aoes("dikaiosyne/knockback/").All(a => !a.Navigate)
            && f.E.Hints(player.Position).Contains("Knockback into danger!"),
            "Brand Of Sin moved origin, stopped at wall, added AI, or lost out of bounds hint.");
        var activation = f.E.BrandOfSinKnockbacks[0].Cast.FinishAt;
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: activation);
        Check(f.E.DikaiosyneLanding(player) != player.Position, "Exact expiry incorrectly grants immunity.");
        f.E.StatusChanged(100, 160, true, f.Now, expiresAt: activation.AddTicks(1));
        Check(f.E.DikaiosyneLanding(player) == player.Position && !f.Aoes("dikaiosyne/knockback/").Any(),
            "Strict later expiry did not suppress projection.");
        f.E.StatusChanged(100, 160, false, f.Now);
        f.Cast(11, 16132, f.Center, duration: 3);
        Check(Vector3.Distance(f.E.DikaiosyneLanding(player), f.Center + Vector3.UnitX * 35) < .001f,
            "Multiple knockbacks were not sequential.");
        f.Finish(10);
        Check(f.E.BrandOfSinKnockbacks.Count == 1, "Simple knockback Finish did not remove first matching source.");
    }

    private static void DikaiosyneParty()
    {
        var f = new Fixture(true);
        f.Set(101, a => a with { Position = f.Center });
        f.Cast(10, 16134, target: 101);
        Check(!f.E.Hints(f.Actors.Single(a => a.Id == 100).Position).Contains("Stack!")
            && f.Aoes("quest/stack/join").Length == 1 && !f.E.PredictedDamage.Any(),
            "Min1 incorrectly warns for solitary target or disabled source navigation.");
        f.Cast(11, 17431, target: 100);
        Check(f.Aoes("quest/spread/").Single(a => a.Display).Shape is CircleTemplate { Radius: 6 }
            && f.Aoes("quest/spread/").Where(a => a.Navigate).All(a => a.Shape is CircleTemplate { Radius: 7 })
            && !f.E.PredictedDamage.Any(), "R6 spread R7 navigation or no-damage contract lost.");
        f.Finish(11);
        f.Now = f.Now.AddSeconds(1);
        f.Cast(11, 16134, target: 100);
        Check(f.Aoes("quest/stack/join").Length == 1
            && !f.Aoes("quest/stack/").Any(a => a.Id.EndsWith("/avoid")),
            "Unlimited stacks prevented marked viewer from entering another stack.");
    }

    private static void Lifecycle()
    {
        foreach (var dikaiosyne in new[] { false, true })
        {
            var f = new Fixture(dikaiosyne);
            f.Cast(10, dikaiosyne ? 15576u : 14609u);
            f.Set(1, a => a with { Hp = 0 });
            f.Sync();
            Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Primary HP0 retained active component.");
            f.Set(1, a => a with { Hp = 1000 });
            f.Sync();
            Check(f.E.Active == null, "Completed primary healing repulled.");
            var primary = f.Actors.Single(a => a.Id == 1);
            f.Actors.Remove(primary);
            f.Sync();
            f.Actors.Add(primary);
            f.Sync();
            Check(f.E.Active != null, "Raw primary new generation failed pull.");
            f.E.DirectorUpdate(0x40000005);
            f.Sync();
            Check(f.E.Active == null, "Wipe repulled stale frame.");
            f.E.DirectorUpdate(0x40000011);
            f.Sync();
            Check(f.E.Active != null, "Director unlock failed valid pull.");
            f.Enabled = false;
            f.Sync();
            Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Disabled native owner retained output.");
        }
    }

    private static void NativeOutput()
    {
        foreach (var dikaiosyne in new[] { false, true })
        {
            var f = new Fixture(dikaiosyne);
            f.Now = Start;
            f.Cast(10, dikaiosyne ? 15576u : 14609u, duration: 20);
            using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
            var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
            output.Apply(f.E, Start, false);
            var snapshot = safe.Manager.BuildSnapshot(Start);
            var player = f.Actors.Single(a => a.Id == 100).Position;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && path.Count > 0
                && Vector3.Distance(path[^1].Position, f.Center) <= 3.1f, "Production solver failed tower join.");
            f.Finish(10);
            output.Apply(f.E, Start, false);
            Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Finished tower retained persistent navigation.");
            if (!dikaiosyne)
            {
                f.Now = f.Now.AddSeconds(1);
                f.E.Icon(100, 129, f.Now, 100);
                output.Apply(f.E, f.Now, false);
                Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count > 0,
                    "Default spread clock failed public snapshot/Buffered consumption.");
                f.E.Effect(12, 15083, 100, f.Now);
                output.Apply(f.E, f.Now, false);
                Check(safe.Manager.BuildSnapshot(f.Now).DangerAreas.Count == 0, "Spread effect retained persistent navigation.");
            }
            output.Clear();
            Check(safe.Manager.BuildSnapshot(Start).DangerAreas.Count == 0, "Owner retained native tower constraint.");
        }
    }
}

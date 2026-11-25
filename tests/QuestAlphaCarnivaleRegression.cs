using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class QuestAlphaCarnivaleRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(410, 0, -110);

    internal static void Run(Action<string, Action> run)
    {
        run("Quest Alpha exact primary ownership two components and sixty vertex arena", AlphaCatalog);
        run("Quest Alpha default clock middle live target raw rectangle already tethered player", Intercept);
        run("Quest Alpha shared Toxic Spit clock raw restart cancellation visual and effect independence", Clocks);
        run("Quest Alpha tether source live raid classification safe colour excluded ally slot direct clear", TetherSides);
        run("Quest Alpha ordered simultaneous destruction stale references and same ID new generation", Destruction);
        run("Quest Alpha middle source cached list appends new generations after survivors", MiddleSourceOrder);
        run("Quest Alpha intercept reversed endpoints and undefined zero length retain model without invalid navigation", InterceptEndpoints);
        run("Quest Alpha Carnivale real raw changed after cancel rebuilds unchanged raw does not", RawAfterCancel);
        run("Quest Alpha primary death HP0 destruction and cached final boss delayed completion", AlphaLifecycle);
        run("Quest Alpha actual output solver intercepts narrow rectangle and owner clears", InterceptOutput);
        run("Carnivale Stage01 exact ownership precombat display only sixty four polygon", CarnivaleCatalogCheck);
        run("Carnivale Stage01 group pull untargetable primary fixed identity and group completion", CarnivaleLifecycle);
        run("Carnivale Stage01 true cast end late packet boundary and independent navigation", CarnivaleCast);
        run("Quest Alpha Carnivale wipe unlock mechanism disable and owner cleanup", Controls);
        run("Carnivale Stage04 Act1 preparation combat hints fixed primary and bat group completion", CarnivaleFour);
        run("Carnivale Stage05 polygon32 turtle group no invented cast dispel or action", CarnivaleFive);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal readonly DungeonEncounter E;
        internal readonly List<DungeonActor> Actors;
        internal DateTime Now = Start;
        internal ulong Player = 100;
        internal bool Enabled = true;
        internal Fixture(bool carnivale = false, bool pull = true, bool targetFirst = false)
        {
            E = new(carnivale ? 796u : 1244u);
            var center = carnivale ? new Vector3(100, 0, 100) : Center;
            Actors =
            [
                new(1, carnivale ? 0x25BEu : 0x4632u, center, InCombat: pull, Hp: 1000),
                new(10, carnivale ? 0x25BDu : 0x4635u, carnivale ? center : new(403, 0, -105)),
                new(20, carnivale ? 0x25BDu : 0x4637u, center),
                new(100, 0, center + Vector3.UnitZ * 2, PartySlot: 0, Player: true),
                new(101, 0, center + Vector3.UnitZ * 5, PartySlot: 1, Player: true),
            ];
            if (targetFirst)
            {
                var target = Actors.Single(a => a.Id == 101);
                Actors.Remove(target);
                Actors.Insert(0, target);
            }
            Sync();
        }
        internal void Sync() => E.Synchronize(Actors, Actors.Single(a => a.Id == Player).Position, Now, _ => Enabled, Player);
        internal void Set(ulong id, Func<DungeonActor, DungeonActor> change)
        {
            var index = Actors.FindIndex(a => a.Id == id);
            Actors[index] = change(Actors[index]);
        }
        internal void RawTether(ulong source, ulong target, uint type = 84)
        {
            Set(source, a => a with { Tethers = target == 0 ? [] : new Dictionary<uint, ulong> { [type] = target } });
            Sync();
        }
        internal void Cast(ulong source, uint action, float duration = 10, Vector3? location = null, float rotation = 0)
        {
            Set(source, a => a with { Cast = new(action, location ?? a.Position, rotation, Now, Now.AddSeconds(duration + .3), source) });
            Sync();
        }
        internal DungeonAmbientAoe[] Aoes(string prefix) => E.AmbientAoes().Where(a => a.Id.StartsWith(prefix)).ToArray();
        internal void Finish(ulong source)
        {
            Set(source, a => a with { Cast = null });
            Sync();
        }
    }

    private static void AlphaCatalog()
    {
        var f = new Fixture();
        Check(ArrDungeonCatalog.Owns(0x4632, 1244) && !ArrDungeonCatalog.Owns(0x4637, 1244)
            && f.E.Active?.BossOid == 0x4632 && f.E.Active.Casts.Length == 2
            && f.E.Arena!.GetContours().Single().Count == 60, "Alpha primary scope/components/polygon differ from source.");
        f.Cast(10, 40563, location: Center + Vector3.UnitX, rotation: .31f);
        var cast = f.E.Casts[10];
        var fan = (FanTemplate)cast.Rule.Shape!;
        Check(fan.Radius == 10 && MathF.Abs(fan.AngleRadians - MathF.PI * 2 / 3) < .00001
            && cast.Location == Center + Vector3.UnitX && cast.Rotation == .31f
            && !f.E.PredictedDamage.Any(), "Critical Bite locked geometry or invented damage.");
        f.Now = Start.AddSeconds(40);
        f.Sync();
        Check(f.E.Casts.ContainsKey(10), "Critical Bite expired without real Finish.");
        f.Finish(10);
        Check(f.E.Casts.Count == 0, "Real cancel failed.");
    }

    private static void Intercept()
    {
        var f = new Fixture();
        f.Set(101, a => a with { Position = new(403.12345f, 0, -96.12345f), HitboxRadius = 2 });
        f.RawTether(10, 101);
        var zone = f.Aoes("alpha/intercept").Single();
        Check(!zone.Display && zone.Navigate && zone.Safe && zone.GoalPriority == 1 && zone.Activation == default(DateTime)
            && f.E.Hints(Center).Contains("Grab the tether!"), "Initial tether clock/visibility/hint wrong.");
        var geometry = zone.Shape.Resolve(zone.Position, zone.Rotation, null);
        var source = f.Actors.Single(a => a.Id == 10).Position;
        var target = f.Actors.Single(a => a.Id == 101);
        var direction = Vector3.Normalize(source - target.Position);
        var from = target.Position + direction * 2.1f;
        var mid = (from + source) * .5f;
        var right = new Vector3(direction.Z, 0, -direction.X);
        Check(geometry.Contains(mid) && geometry.Contains(from) && geometry.Contains(source)
            && geometry.Contains(mid + right * .499f) && !geometry.Contains(mid + right * .501f),
            "Interception rectangle raw endpoints/halfwidth/complement differ.");
        f.Player = 101;
        f.Sync();
        Check(!f.E.Hints(target.Position).Contains("Grab the tether!") && f.Aoes("alpha/intercept").Length == 1,
            "Already-tethered player incorrectly lost navigation or gained hint.");
        // The first middle lizard is retained even when dead/untargetable and without a resolvable target.
        f.Actors.Add(new(11, 0x4635, source, Tethers: new Dictionary<uint, ulong> { [17] = 100 }));
        f.Set(10, a => a with { Dead = true, Targetable = false });
        f.Sync();
        Check(f.Aoes("alpha/intercept").Length == 1, "Dead middle source incorrectly filtered.");
        f.RawTether(10, 999);
        Check(f.Aoes("alpha/intercept").Length == 0 && f.E.AlphaTetherCount > 0,
            "Missing first target incorrectly fell through to second middle source.");
    }

    private static void Clocks()
    {
        var f = new Fixture();
        f.RawTether(10, 101);
        f.Cast(10, 40561);
        Check(f.E.AlphaActivation == default, "Visual 40561 set clock.");
        f.Cast(10, 40562, 8);
        Check(Math.Abs((f.E.AlphaActivation - Start.AddSeconds(9.5)).TotalSeconds) < .00001,
            $"40562 omitted 1.2 after buffered finish: {f.E.AlphaActivation:O}.");
        f.Now = Start.AddSeconds(1);
        f.Set(10, a => a with { Cast = a.Cast! with { Location = a.Position + Vector3.UnitX, TotalTime = 8 } });
        f.Sync();
        Check(Math.Abs((f.E.AlphaActivation - Start.AddSeconds(9.5)).TotalSeconds) < .00001, "Raw restart changed unchanged finish clock.");
        f.Cast(20, 40562, 4);
        Check(Math.Abs((f.E.AlphaActivation - Start.AddSeconds(6.5)).TotalSeconds) < .00001, "Second source failed shared last-write clock.");
        f.Finish(20);
        f.Finish(10);
        f.E.Effect(10, 40562, 1, f.Now);
        f.RawTether(10, 0);
        Check(Math.Abs((f.E.AlphaActivation - Start.AddSeconds(6.5)).TotalSeconds) < .00001, "Finish/effect/untether cleared persistent shared clock.");
        f.RawTether(10, 101, 17);
        Check(Math.Abs((f.Aoes("alpha/intercept").Single().Activation!.Value - Start.AddSeconds(6.5)).TotalSeconds) < .00001,
            "Later tether failed retained clock.");
    }

    private static void MiddleSourceOrder()
    {
        var f = new Fixture();
        f.RawTether(10, 101);
        f.Actors.Add(new(11, 0x4635, new(403, 0, -105), Tethers: new Dictionary<uint, ulong> { [84] = 101 }));
        f.Sync();
        _ = f.Aoes("alpha/intercept");
        f.Actors.RemoveAll(a => a.Id == 10);
        f.Actors.Add(new(12, 0x4635, new(403, 0, -105)));
        f.Sync();
        Check(f.Aoes("alpha/intercept").Length == 1, "New untethered source displaced surviving cached middle lizard.");
    }

    private static void InterceptEndpoints()
    {
        var f = new Fixture();
        f.Set(101, a => a with { Position = new(402.5f, 0, -105), HitboxRadius = .9f });
        f.RawTether(10, 101);
        var aoe = f.Aoes("alpha/intercept").Single();
        var geometry = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
        Check(geometry.Contains(new(403.25f, 0, -105)) && !geometry.Contains(new(402.75f, 0, -105)),
            "Target hitbox crossing source failed reversed final endpoint geometry.");
        foreach (var target in new[] { new Vector3(403, 0, -105), new Vector3(402, 0, -105) })
        {
            f.Set(101, a => a with { Position = target });
            f.Sync();
            Check(f.E.AlphaTetherCount == 1 && f.E.AlphaLines().Count() == 1 && f.Aoes("alpha/intercept").Length == 0,
                "Undefined zero-length source rectangle lost model/display or threw invalid geometry.");
        }
    }

    private static void RawAfterCancel()
    {
        foreach (var carnivale in new[] { false, true })
        {
            var f = new Fixture(carnivale);
            var source = carnivale ? 1ul : 10ul;
            f.Cast(source, carnivale ? 14199u : 40563u);
            f.Now = Start.AddSeconds(1);
            f.E.Finish(source, endedAt: f.Now);
            f.Sync();
            Check(!f.E.Casts.ContainsKey(source), "Unchanged cancelled raw resurrected.");
            var old = f.Actors.Single(a => a.Id == source).Cast!;
            f.Set(source, a => a with { Cast = old with { Action = 99999 } });
            f.Sync();
            f.E.StartCast(source, old, f.Now);
            Check(!f.E.Casts.ContainsKey(source), "Unclaimed raw erased tombstone and unlocked a late old packet.");
            f.Set(source, a => a with { Cast = old });
            f.Sync();
            Check(f.E.Casts.ContainsKey(source), "Real raw transition from unclaimed back to known action failed.");
            f.E.Finish(source, endedAt: f.Now);
            f.Set(source, a => a with { Cast = a.Cast! with { Rotation = .71f } });
            f.Sync();
            Check(f.E.Casts.TryGetValue(source, out var rebuilt) && rebuilt.Rotation == .71f,
                "True raw change after cancelled cast failed Finish/Start replacement.");
        }
    }

    private static void TetherSides()
    {
        var f = new Fixture();
        f.RawTether(10, 101);
        Check(f.E.AlphaLines().Single().Safe, "Party target line should be safe.");
        f.RawTether(20, 101, 17);
        f.Player = 101;
        f.Sync();
        Check(!f.E.Hints(Center).Contains("Grab the tether!"), "Set slot failed.");
        f.RawTether(20, 0);
        Check(f.E.AlphaTetherCount == 1 && f.E.Hints(Center).Contains("Grab the tether!"), "Untether must Clear slot even with another line.");
        f.Set(1, a => a with { PartySlot = 24, FriendlyNpc = true });
        f.RawTether(10, 1);
        Check(!f.E.AlphaLines().Single().Safe, "Excluded ally4632 coloured safe.");
        f.RawTether(100, 10);
        Check(f.E.AlphaLines().Last().Safe && f.E.AlphaLines().Last().End == f.Actors.Single(a => a.Id == 100).Position,
            "Live raid source should be Player side.");
        f.Set(100, a => a with { Dead = true });
        f.RawTether(100, 10, 17);
        Check(!f.E.AlphaLines().Last().Safe && f.E.AlphaLines().Last().End == f.Actors.Single(a => a.Id == 10).Position,
            "Dead raid source should cease being Player side.");
    }

    private static void Destruction()
    {
        foreach (var targetFirst in new[] { false, true })
        {
            var f = new Fixture(targetFirst: targetFirst);
            f.RawTether(10, 101);
            var oldPosition = f.Actors.Single(a => a.Id == 101).Position;
            f.Actors.RemoveAll(a => a.Id is 10 or 101);
            f.Sync();
            Check(f.E.AlphaTetherCount == (targetFirst ? 1 : 0), "Historical target/source destruction order changed.");
            f.Actors.Add(new(101, 0, Center + Vector3.UnitX * 40, PartySlot: 1, Player: true));
            f.Sync();
            if (targetFirst)
                Check(f.E.AlphaLines().Single().End == oldPosition && !f.E.AlphaLines().Single().Safe,
                    "Same ID replacement stole old target reference.");
        }
    }

    private static void AlphaLifecycle()
    {
        var f = new Fixture();
        f.Set(1, a => a with { Dead = true, Hp = 0, InCombat = false });
        f.Sync();
        Check(f.E.Active != null && f.E.PreserveStageAcrossBattleEnd, "Primary death ended independent phase.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active != null, "Primary destruction ended phase.");
        f.Set(20, a => a with { Hp = 0 });
        f.Sync();
        Check(f.E.Active != null, "Final boss HP0 must not substitute IsDead.");
        f.Set(20, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null, "Cached4637 IsDead did not complete.");
        f.Actors.Add(new(1, 0x4632, Center, InCombat: true));
        f.Set(20, a => a with { Dead = false, Hp = 1000 });
        f.Sync();
        Check(f.E.Active != null, "Absent primary completion blocked real same ID birth.");
        var delayed = new Fixture(pull: false);
        delayed.Set(20, a => a with { Dead = true });
        delayed.Set(1, a => a with { InCombat = true });
        delayed.Sync();
        Check(delayed.E.Active != null, "First cached dead4637 ended one Update too early.");
        delayed.Sync();
        Check(delayed.E.Active == null, "Cached dead4637 did not end next Update.");
        var destroyed = new Fixture();
        destroyed.Actors.RemoveAll(a => a.Oid is >= 0x4633 and <= 0x4637);
        destroyed.Sync();
        Check(destroyed.E.Active == null, "AllDestroyed failed while Primary alive.");
        destroyed.Sync();
        Check(destroyed.E.Active == null, "Completed same Primary repulled.");
    }

    private static void InterceptOutput()
    {
        var f = new Fixture();
        f.RawTether(10, 101);
        using var safe = new ScriptSafeZoneApi { DangerMarginForTest = 0, UtcNowForTest = Start };
        var output = new DungeonOutput(safe, new ScriptDrawApi(), safe.Manager);
        output.Apply(f.E, Start, false);
        var snapshot = safe.Manager.BuildSnapshot(Start);
        Check(snapshot.GoalAreas.Count == 1 && snapshot.DangerAreas.Count == 0, "Intercept must preserve existing bridge goal semantics.");
        var origin = f.Actors.Single(a => a.Id == 100).Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(origin, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .2f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, Start);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && path.Count > 0
            && f.Aoes("alpha/intercept").Single().Shape.Resolve(f.Aoes("alpha/intercept").Single().Position,
                f.Aoes("alpha/intercept").Single().Rotation, null).Contains(path[^1].Position),
            "Actual solver failed narrow middle intercept.");
        f.RawTether(10, 0);
        output.Apply(f.E, Start, false);
        Check(safe.Manager.BuildSnapshot(Start).GoalAreas.Count == 0, "Untether leaked persistent intercept goal.");
        output.Clear();
    }

    private static void CarnivaleCatalogCheck()
    {
        var f = new Fixture(true, false);
        Check(ArrDungeonCatalog.Owns(0x25BE, 796) && ArrDungeonCatalog.Owns(0x25C0, 796)
            && !ArrDungeonCatalog.Owns(0x25BD, 796) && f.E.Active == null && f.E.Arena == null
            && f.E.CarnivalePrecombatHint != null, "Shared territory scope or precombat navigation expanded.");
        Check(f.Aoes("carnivale/enemy/").Length == 3 && f.Aoes("carnivale/precombat/arena").Length == 1
            && f.E.AmbientAoes().All(a => !a.Navigate), "Precombat source display failed.");
        var arena = CarnivaleCatalog.Arena(0);
        Check(arena.GetContours().Single().Count == 64 && arena.Contains(new(100, 0, 124.499f))
            && !arena.Contains(new(100, 0, 124.501f), 0), "Masked arena64 vertices/radius changed.");
        var angle = MathF.PI / 64;
        Check(!arena.Contains(new(100 + 24.49f * MathF.Sin(angle), 0, 100 + 24.49f * MathF.Cos(angle)), 0),
            "64-edge polygon became mathematical circle.");
    }

    private static void CarnivaleLifecycle()
    {
        var f = new Fixture(true, false);
        f.Set(1, a => a with { Targetable = false });
        f.Set(10, a => a with { InCombat = true });
        f.Sync();
        Check(f.E.Active != null && f.E.PrimaryId == 1 && f.E.CarnivalePrecombatHint == null, "Slime group pull gated on primary.");
        f.Actors.Insert(0, new(2, 0x25BE, new(100, 0, 102), InCombat: true));
        f.Set(1, a => a with { Dead = true, Hp = 0 });
        f.Set(10, a => a with { InCombat = false });
        f.Sync();
        Check(f.E.Active != null && f.E.PrimaryId == 1, "Primary changed or its death ended group.");
        f.Actors.RemoveAll(a => a.Id == 1);
        f.Sync();
        Check(f.E.Active != null && f.E.PrimaryId == 1, "Missing primary ended living group.");
        f.Set(10, a => a with { Dead = true });
        f.Set(20, a => a with { Dead = true });
        f.Set(2, a => a with { Dead = true });
        f.Sync();
        Check(f.E.Active == null && f.E.CarnivalePrecombatHint == null, "AllDeadOrDestroyed did not end source group.");
        f.Actors.Add(new(1, 0x25BE, new(100, 0, 100), InCombat: true));
        f.Set(10, a => a with { Dead = false });
        f.Sync();
        Check(f.E.Active != null && f.E.PrimaryId == 1, "Absent old primary completion blocked a real same ID birth.");
    }

    private static void CarnivaleCast()
    {
        var f = new Fixture(true);
        f.Cast(1, 14199, rotation: .37f);
        Check(f.E.OutputCasts.Count() == 1 && !f.E.PredictedDamage.Any(), "Iron Justice output missing/invented damage.");
        f.Now = Start.AddSeconds(40);
        f.Sync();
        Check(f.E.Casts.ContainsKey(1), "Iron Justice timeout replaced true end.");
        var old = f.Actors.Single(a => a.Id == 1).Cast!;
        f.Finish(1);
        f.E.StartCast(1, old, f.Now);
        Check(!f.E.Casts.ContainsKey(1), "Late ended cast resurrected.");
        f.Now = Start.AddSeconds(41);
        f.Cast(1, 14199, location: new(101, 0, 100));
        Check(f.E.Casts[1].StartedAt == f.Now, "New true cast generation blocked.");
    }

    private static void Controls()
    {
        foreach (var carnivale in new[] { false, true })
        {
            var f = new Fixture(carnivale);
            f.E.DirectorUpdate(0x40000005);
            f.Sync();
            Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Wipe failed output cleanup.");
            f.E.DirectorUpdate(0x40000011);
            f.Sync();
            if (carnivale)
            {
                Check(f.E.Active == null, "Director recommence recreated unchanged unloaded Carnivale owner.");
                var primary = f.Actors.Single(a => a.Id == 1);
                f.Actors.Remove(primary);
                f.Sync();
                f.Actors.Add(primary);
                f.Sync();
            }
            Check(f.E.Active != null, "Director unlock/new generation failed.");
            f.Enabled = false;
            f.Sync();
            Check(f.E.Active == null && !f.E.AmbientAoes().Any(), "Disabled owner retained previews.");
        }
    }
    private static void CarnivaleFour()
    {
        var e = new DungeonEncounter(796);
        var center = new Vector3(100, 0, 100);
        var actors = new List<DungeonActor>
        {
            new(1, 0x25C8, center),
            new(10, 0x25D2, center + Vector3.UnitX * 5),
            new(100, 0, center, Player: true, PartySlot: 0),
        };
        void Sync(bool enabled = true) => e.Synchronize(actors, center, Start, _ => enabled, 100);
        Sync();
        Check(CarnivaleCatalog.Owns(796, 0x25C8) && CarnivaleCatalog.Owns(796, 0x25D5)
            && e.Active == null && e.CarnivalePrecombatHint!.Contains("Flying Sardine")
            && e.AmbientAoes().All(a => !a.Navigate), "Act1 preparation or Act2 ownership expanded.");
        actors[0] = actors[0] with { Targetable = false, Hp = 0 };
        actors[1] = actors[1] with { InCombat = true };
        Sync();
        Check(e.Active?.BossOid == 0x25C8 && e.Active.Casts.Length == 0 && e.PrimaryId == 1
            && e.Arena!.GetContours().Single().Count == 64 && e.CarnivalePrecombatHint == null
            && e.Hints(center).Contains("Bats are weak to lightning.\nThe wolf is weak to fire."),
            "Bat pull gated on primary or failed combat hint switch.");
        e.StartCast(1, new(14361, center, 0, Start, Start.AddSeconds(1), 100), Start);
        Check(e.Casts.Count == 0 && !e.PredictedDamage.Any(), "Unactivated Sanguine Bite became AOE or prediction.");
        actors.RemoveAt(0);
        Sync();
        Check(e.Active != null && e.PrimaryId == 1, "Primary destruction ended alive bat group.");
        actors[0] = actors[0] with { Hp = 0, InCombat = false };
        Sync();
        Check(e.Active != null, "Bat HP0 replaced DeadOrDestroyed.");
        actors[0] = actors[0] with { Dead = true };
        Sync();
        Check(e.Active == null && e.CarnivalePrecombatHint == null && !e.AmbientAoes().Any(),
            "Completed Act1 resurrected preparation on dead remainder.");
        actors.Add(new(20, 0x25CC, center, InCombat: true));
        Sync();
        Check(e.Active?.BossOid == 0x25CC, "Completed Act1 corpse blocked next turtle stage.");
        var corpse = new DungeonEncounter(796);
        corpse.Synchronize([new(1, 0x25C8, center), new(10, 0x25D2, center, InCombat: true),
            new(100, 0, center, Player: true, PartySlot: 0)], center, Start, _ => true, 100);
        corpse.Synchronize([new(1, 0x25C8, center, Dead: true), new(10, 0x25D2, center, Dead: true),
            new(20, 0x25CC, center, InCombat: true), new(100, 0, center, Player: true, PartySlot: 0)],
            center, Start, _ => true, 100);
        Check(corpse.Active?.BossOid == 0x25CC && corpse.PrimaryId == 20 && corpse.Actors[1].Dead,
            "Retained completed primary corpse blocked same-frame stage handoff.");
        var firstDead = new DungeonEncounter(796);
        firstDead.Synchronize([new(100, 0, center, Player: true, PartySlot: 0)], center, Start, _ => true, 100);
        firstDead.Synchronize([new(1, 0x25C8, center, Dead: true),
            new(10, 0x25D2, center, InCombat: true), new(100, 0, center, Player: true, PartySlot: 0)],
            center, Start, _ => true, 100);
        Check(firstDead.Active?.BossOid == 0x25C8, "First observed dead primary blocked alive combat group.");

    }

    private static void CarnivaleFive()
    {
        var e = new DungeonEncounter(796);
        var center = new Vector3(100, 0, 100);
        var actors = new List<DungeonActor>
        {
            new(1, 0x25CC, center),
            new(2, 0x25CC, center + Vector3.UnitX * 5),
            new(3, 0x25CC, center - Vector3.UnitX * 5),
            new(100, 0, center, Player: true, PartySlot: 0),
        };
        void Sync(bool enabled = true) => e.Synchronize(actors, center, Start, _ => enabled, 100);
        // A known party before creation loads the first turtle directly; cold order is tested separately.
        e.Synchronize([actors[^1]], center, Start, _ => true, 100);
        Sync();
        Check(e.CarnivalePrecombatHint!.Contains("Eerie Soundwave")
            && e.AmbientAoes().Count(a => a.Id.StartsWith("carnivale/enemy/")) == 3,
            "Turtle hints or all-instance markers missing.");
        actors[1] = actors[1] with { InCombat = true, Targetable = false };
        Sync();
        Check(e.Active?.BossOid == 0x25CC && e.PrimaryId == 1 && e.Active.Casts.Length == 0
            && e.Arena!.GetContours().Single().Count == 32 && e.Arena.Contains(center + Vector3.UnitZ * 16.0137f)
            && !e.Arena.Contains(center + Vector3.UnitZ * 16.014f, 0)
            && !e.Hints(center).Any() && !e.PredictedDamage.Any(), "Turtle pull, small polygon, no-combat-component contract.");
        actors[0] = actors[0] with { Dead = true };
        Sync();
        Check(e.Active != null && e.PrimaryId == 1, "Primary death ended other turtles.");
        actors.RemoveAt(0);
        Sync();
        Check(e.Active != null && e.PrimaryId == 1, "Primary destruction rebound pending turtle.");
        actors[0] = actors[0] with { Hp = 0, InCombat = false };
        Sync();
        Check(e.Active != null, "Turtle HP0 replaced source dead flag.");
        actors[0] = actors[0] with { Dead = true };
        actors[1] = actors[1] with { Dead = true };
        Sync();
        Sync();
        Check(e.Active == null && e.CarnivalePrecombatHint == null && !e.AmbientAoes().Any(),
            "Dead pending turtle restored completed preparation.");
        actors[0] = actors[0] with { Dead = false, Hp = 100, InCombat = true };
        Sync();
        Check(e.Active == null && e.CarnivalePrecombatHint == null,
            "Revived discarded pending turtle recreated a module without new birth.");

        actors.Add(new(1, 0x25CC, center, InCombat: true));
        Sync();
        Check(e.Active != null, "New turtle generation could not start after group cleanup.");
        Sync(false);
        Check(e.Active == null && !e.AmbientAoes().Any(), "Disabled turtle owner retained output.");
    }

}

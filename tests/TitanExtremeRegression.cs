using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class TitanExtremeRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("TitanExtreme identity personal death wipe HP0 and true phase predicates", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.titan-extreme"), "Discovery.");
            Check(((CircleGeometry)f.Engine.Arena!).Radius == 25, "Initial arena.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.Active != null, "Personal death.");
            f.Engine.DirectorUpdate(0x40000005); f.Scan(); Check(f.Engine.Active == null, "Wipe.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(f.Engine.Active != null, "Recommence.");
            f.Actors[1] = f.Actors[1] with { Hp = 0 }; f.Scan(); Check(f.Engine.Active == null, "HP0.");
        });
        run("TitanExtreme independent source shapes hold through nominal timeout and remove on actual end", () =>
        {
            using var f = Scene();
            foreach (var (action, source) in new (uint, BossMod.AOEShape)[]
            {
                (1467, new BossMod.AOEShapeRect(40.25f, 3)),
                (1468, new BossMod.AOEShapeRect(40.25f, 3)),
                (1475, new BossMod.AOEShapeRect(40.25f, 3)),
                (1470, new BossMod.AOEShapeCircle(6)),
                (1471, new BossMod.AOEShapeCircle(6.3f)),
            })
            {
                Start(f, 20, action, 2, .7f);
                var aoe = Group(f, action == 1471 ? "burst/20" : "cast/20").Single();
                if (action is 1467 or 1468 or 1475) Check(aoe.Rotation == .7f, "Packet rotation discarded.");
                var geo = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
                for (var x = -15f; x < 45; x += .49f)
                    for (var z = -15f; z < 45; z += .53f)
                        Check(geo.Contains(new(x, 0, z), .00001f) == source.Check(new(x, z),
                            new(aoe.Position.X, aoe.Position.Z), new(aoe.Rotation)), "Source shape.");
                f.Now = f.Now.AddSeconds(10); f.Engine.Tick(f.Now); f.Render();
                Check(f.Engine.Casts.ContainsKey(20), "Predicted timeout removed cast.");
                End(f, 20, action);
                Check(!f.Engine.Casts.ContainsKey(20) && Group(f, "burst/20").Length == 0 && Group(f, "cast/20").Length == 0, "Actual ending.");
            }
        });
        run("TitanExtreme shrink rings15 then12 actual finishes and early heart transitions", () =>
        {
            using var f = Scene();
            Hide(f); Check(Group(f, "crush").Length == 3, "First disappearance.");
            var safe = Group(f, "crush/safe").Single().Shape.Resolve(Vector3.Zero, 0, null);
            Check(safe.Contains(new(14, 0, 0)) && !safe.Contains(new(12.9f, 0, 0)) && !safe.Contains(new(15.1f, 0, 0)), "First ring.");
            Start(f, 1, 1472, 3); End(f, 1, 1472);
            Check(((CircleGeometry)f.Engine.Arena!).Radius == 15 && Group(f, "crush").Length == 0, "Actual first shrink.");
            Show(f); Hide(f); Start(f, 1, 1472, 3); End(f, 1, 1472);
            Check(((CircleGeometry)f.Engine.Arena!).Radius == 12, "Actual second shrink.");
            f.Actors[30] = new(30, 0x5E5, Vector3.Zero, Targetable: true); f.Scan();
            Start(f, 20, 1470, 3); Check(Group(f, "cast/20").Length == 1, "Heart phase.");
            f.Actors[30] = f.Actors[30] with { Targetable = false }; f.Scan();
            Check(Group(f, "cast").Length == 0, "Heart disappearance retained phase components.");
            Show(f);
            f.Actors[31] = new(31, 0x1E8F8F, new(4, 0, 0)); f.Scan();
            Check(Group(f, "voidzone").Single().Obstacle && f.Snapshot.HardObstacles.Count == 1, "Final phase voidzone is impassable.");
            f.Actors[31] = f.Actors[31] with { EventState = 7 }; f.Scan();
            Check(Group(f, "voidzone").Length == 0, "State7 voidzone.");
        });
        run("TitanExtreme real solver reaches geocrush ring with independent display dimensions", () =>
        {
            using var f = Scene(); Hide(f);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ExecutablePath.Concat(solution.ContinuationPath).ToArray();
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Any(p =>
                Vector2.Distance(new(p.Position.X, p.Position.Z), Vector2.Zero) is >= 13 and <= 15), "No ring route.");
            Check(f.Draw.Previews.Keys.Count(k => !k.StartsWith("enemy/marker/", StringComparison.Ordinal)) == 2
                && f.Safe.NavigationOnly.Count == 2, "Display/navigation split.");
        });
        run("TitanExtreme fetters tether duplicate markers status clear and healer separation", () =>
        {
            using var f = Scene(); Phase2(f);
            f.Actors[100] = f.Actors[100] with { PartyRole = "Healer", ClassJob = 24 }; f.Scan();
            f.Engine.Tether(101, 7, true, f.Now, 100); f.Render();
            var aoe = Group(f, "gaol/stand").Single();
            Check(Vector3.Distance(aoe.Position, new(-7.5f, 0, 15 * MathF.Sqrt(3) / 2)) < .001f
                && aoe.Activation == f.Now.AddSeconds(2.9), "Healer gaol.");
            f.Now = f.Now.AddSeconds(.5);
            f.Engine.Effect(1, 1652, 2, f.Now, 100); f.Render();
            Check(Group(f, "gaol/stand").Single().Activation == aoe.Activation, "Duplicate marker reset tether clock.");
            f.Engine.StatusChanged(100, 292, true, f.Now); f.Render();
            Check(Group(f, "gaol").Length == 0, "Fetters acquired.");
            f.Actors[102] = new(102, 0, new(5, 0, 5), PartySlot: 2, PartyRole: "Melee", Hostile: false); f.Scan();
            f.Engine.Effect(1, 645, 3, f.Now, 102); f.Render();
            f.PlayerId = 102; f.Scan();
            Check(Group(f, "gaol/stand").Single().Position == f.Actors[1].Position + Vector3.UnitZ, "DPS gaol perspective.");
        });
        run("TitanExtreme activation restores existing tethers clears existing292 and keeps zero priority bombs selectable", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { Tethers = new Dictionary<uint, ulong> { [7] = 101 } };
            f.Actors[101] = f.Actors[101] with { Statuses = [292] };
            Phase2(f);
            Check(Group(f, "gaol/stand").Length == 1, "Existing tether missing.");
            f.PlayerId = 101; f.Scan(); Check(Group(f, "gaol/stand").Length == 0, "Existing292 not cleared.");
            using var g = Scene(); Heart(g);
            g.Actors[30] = g.Actors[30] with { Targetable = false }; g.Scan(); Show(g); Hide(g);
            g.Actors[20] = new(20, 0x5E0, Vector3.Zero); g.Scan();
            Check(g.Engine.PriorityTarget(0, Vector3.Zero) == 20, "Priority0 bomb must remain selectable without a positive-priority target.");
        });
        run("TitanExtreme upheaval live origin observed ending plus1 and immunity only hides arrow", () =>
        {
            using var f = Scene(); Phase2(f);
            f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -9) }; f.Scan();
            Start(f, 1, 1466, 5);
            Check(Group(f, "upheaval/stand").Single().Position == new Vector3(0, 0, -7)
                && Group(f, "upheaval/stand").Single().Activation == f.Engine.Casts[1].FinishAt.AddSeconds(1), "Hold clock.");
            End(f, 1, 1466);
            var ended = f.Now;
            f.Now = ended.AddSeconds(.99); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "upheaval/stand").Length == 1, "Postcast hold.");
            f.Now = ended.AddSeconds(1); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "upheaval").Length == 0, "Strict hold expiry.");
        });
        run("TitanExtreme four Tumult effects prediction1.2 duplicate effects and no invented cast advance", () =>
        {
            using var f = Scene();
            BossCast(f, 1467); BossCast(f, 1469);
            f.Engine.Effect(1, 1464, 10, f.Now); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.PredictedDamage.Count() == 2, "Tumult prediction activation.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Engine.PredictedDamage.Count() == 1, "Dead party member predicted.");
            f.Actors[100] = f.Actors[100] with { Dead = false }; f.Scan();
            for (uint i = 1; i <= 4; ++i)
            {
                f.Now = f.Now.AddSeconds(1.2);
                f.Engine.Effect(1, 1465, 10 + i, f.Now);
                f.Engine.Effect(1, 1465, 10 + i, f.Now);
                f.Engine.Tick(f.Now); f.Render();
                Check(f.Engine.PredictedDamage.Count() == (i < 4 ? 2 : 0), "Four actual effects.");
                if (i < 4) Check(f.Engine.PredictedDamage.All(p => p.Activation == f.Now.AddSeconds(1.2)), "Prediction cadence.");
            }
            f.Now = f.Now.AddSeconds(100); f.Engine.Tick(f.Now);
            Check(f.Engine.Hints(null).Any(h => h.Contains("地裂起读")), "Cast state advanced on timer.");
        });
        run("TitanExtreme second phase alternating8 and5 bombs then all9 on observed landslide end", () =>
        {
            using var f = Scene(); Phase2(f); BossCast(f, 1467);
            for (uint cycle = 0; cycle < 2; ++cycle)
            {
                f.Engine.Effect(1, 645, 100 + cycle, f.Now, 101); f.Engine.Tick(f.Now);
                Buster(f); BossCast(f, 1466); BossCast(f, 1467);
                Tumult(f, 200 + cycle * 10); BossCast(f, 1469); Buster(f);
                BossCast(f, 1467); BossCast(f, 1469);
                for (ulong bomb = 20; bomb < 29; ++bomb) Start(f, bomb, 1471, 5);
                Check(Group(f, "burst").Length == 0, "Bombs shown before buster.");
                Buster(f);
                Check(Group(f, "burst").Length == (cycle == 0 ? 8 : 5), "Alternating cap.");
                Start(f, 1, 1467, 2);
                Check(Group(f, "burst").Length == (cycle == 0 ? 8 : 5), "Cap changed before actual end.");
                End(f, 1, 1467); Check(Group(f, "burst").Length == 9, "Reset9.");
                for (ulong bomb = 20; bomb < 29; ++bomb) End(f, bomb, 1471);
                f.Engine.StatusChanged(101, 292, true, f.Now);
            }
        });
        run("TitanExtreme tank swap two stacks disabled during active MountainBuster and gaolers", () =>
        {
            using var f = Scene();
            f.Actors[100] = f.Actors[100] with { ClassJob = 21, PartyRole = "Tank" }; f.Scan();
            f.Engine.StatusChanged(101, 126, true, f.Now, 2);
            Check(f.Engine.EnemyProvokeActions().Single().Target == 1, "Swap.");
            BossCast(f, 1467); BossCast(f, 1469);
            Check(f.Engine.EnemyProvokeActions().Count == 0, "Swap during MountainBuster.");
            Buster(f); Check(f.Engine.EnemyProvokeActions().Count == 1, "Postbuster swap.");
            f.Actors[30] = new(30, 0x8F2, Vector3.Zero); f.Scan();
            Check(f.Engine.EnemyProvokeActions().Count == 0, "Swap while gaolers alive.");
        });
        run("TitanExtreme heart bomb gate5 live buster target and early heart departure clears all", () =>
        {
            using var f = Scene(); Heart(f); Show(f);
            BossCast(f, 1469);
            f.Engine.Effect(1, 645, sequence++, f.Now, 100); f.Engine.Tick(f.Now);
            f.Engine.Effect(1, 1463, sequence++, f.Now); f.Engine.Tick(f.Now);
            BossCast(f, 1466); BossCast(f, 1467); Tumult(f, sequence += 10); BossCast(f, 1469);
            var buster = Group(f, "buster/display").Single();
            var source = new BossMod.AOEShapeCone(11.25f, new(MathF.PI / 3));
            var geometry = buster.Shape.Resolve(buster.Position, buster.Rotation, null);
            for (var x = -13f; x < 13; x += .61f)
                for (var z = -13f; z < 13; z += .59f)
                    Check(geometry.Contains(new(x, 0, z), .00001f) == source.Check(new(x, z),
                        new(buster.Position.X, buster.Position.Z), new(buster.Rotation)), "RockBuster source shape.");
            f.Actors[101] = f.Actors[101] with { Position = new(10, 0, 0) }; f.Scan();
            Check(Math.Abs(Group(f, "buster/display").Single().Rotation - MathF.PI / 2) < .001f, "Live target angle.");
            f.Engine.Effect(1, 1463, sequence++, f.Now); f.Engine.Tick(f.Now);
            for (ulong bomb = 20; bomb < 27; ++bomb) Start(f, bomb, 1471, 5);
            Check(Group(f, "burst").Length == 0, "Heart bombs before landslide.");
            Start(f, 1, 1467, 2); Check(Group(f, "burst").Length == 5, "Heart5.");
            End(f, 1, 1467); Check(Group(f, "burst").Length == 5, "Heart cap changed on end.");
            Hide(f); f.Actors[30] = f.Actors[30] with { Targetable = false }; f.Scan();
            Check(Group(f, "burst").Length == 0 && Group(f, "buster").Length == 0, "Heart early exit.");
        });
        run("TitanExtreme five complete final cycles4to9 6to9 double Weight checkDelay and terminal", () =>
        {
            using var f = Scene(); Heart(f);
            f.Actors[30] = f.Actors[30] with { Targetable = false }; f.Scan(); Show(f);
            f.Actors[31] = new(31, 0x8F2, new(5, 0, 5)); f.Scan();
            for (var cycle = 0; cycle < 5; ++cycle)
            {
                Buster(f);
                Bombs(f); Check(Group(f, "burst").Length == 0, "Pattern1 before landslide.");
                Start(f, 1, 1467, 2); Check(Group(f, "burst").Length == 4, "First4.");
                End(f, 1, 1467);
                for (ulong bomb = 20; bomb < 24; ++bomb) End(f, bomb, 1471);
                Check(Group(f, "burst").Length == 5, "Remaining5 not shown.");
                ClearBombs(f); Buster(f); BossCast(f, 1469);
                f.Engine.Effect(1, 645, sequence++, f.Now, 100); f.Engine.Tick(f.Now);
                Buster(f); BossCast(f, 1466); BossCast(f, 1467); Buster(f); Tumult(f, sequence += 10);
                BossCast(f, 1469);
                Check(f.Engine.Hints(null).Any(h => h.Contains("大地之重落点结束")), "Double Weight checkDelay missing.");
                f.Now = f.Now.AddSeconds(1); f.Engine.Tick(f.Now); Buster(f);
                Bombs(f); Check(Group(f, "burst").Length == 0, "Pattern2 before landslide end.");
                Start(f, 1, 1467, 2); Check(Group(f, "burst").Length == 0, "Pattern2 shown on start.");
                End(f, 1, 1467); Check(Group(f, "burst").Length == 6, "Two lines6.");
                for (ulong bomb = 20; bomb < 23; ++bomb) End(f, bomb, 1471);
                Check(Group(f, "burst").Length == 6, "Remaining lines cap.");
                ClearBombs(f); Buster(f);
                f.Actors[40] = new(40, 0x5E0, Vector3.Zero);
                f.Actors[41] = new(41, 0x5E0, new(6, 0, 0)); f.Scan();
                Check(f.Engine.PriorityTarget(0, Vector3.Zero) == 40, "Center bomb priority.");
                Tumult(f, sequence += 10); BossCast(f, 1469); Hide(f);
                Check(Group(f, "crush").Length == 3, "Final geocrush.");
                BossCast(f, 1472); Show(f); BossCast(f, 1467);
                f.Actors.Remove(40); f.Actors.Remove(41); f.Scan();
                f.Engine.StatusChanged(100, 292, true, f.Now);
            }
            Check(f.Engine.Hints(null).Any(h => h.Contains("阶段结束等待")), "Source terminal.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0xF8, 296);
        f.Actors[100] = new(100, 0, new(0, 0, 4), PartySlot: 0, PartyRole: "Melee", Hostile: false);
        f.Actors[101] = new(101, 0, new(0, 0, -12), PartySlot: 1, PartyRole: "Tank", Hostile: false);
        f.Actors[1] = f.Actors[1] with { TargetId = 101 };
        f.Scan(); return f;
    }

    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("titan-ex/" + prefix)).ToArray();
    private static void Hide(Fight f) { f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan(); }
    private static void Show(Fight f) { f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan(); }
    private static void Phase2(Fight f) { Hide(f); BossCast(f, 1472); Show(f); }
    private static void Heart(Fight f)
    {
        Phase2(f); Hide(f); BossCast(f, 1472);
        f.Actors[30] = new(30, 0x5E5, Vector3.Zero, Targetable: true); f.Scan();
    }
    private static void Bombs(Fight f) { for (ulong bomb = 20; bomb < 29; ++bomb) Start(f, bomb, 1471, 5); }
    private static void ClearBombs(Fight f)
    {
        for (ulong bomb = 20; bomb < 29; ++bomb)
            if (f.Engine.Casts.ContainsKey(bomb)) End(f, bomb, 1471);
    }
    private static void BossCast(Fight f, uint action) { Start(f, 1, action, 1); End(f, 1, action); }
    private static void Start(Fight f, ulong source, uint action, double seconds, float rotation = 0)
    {
        if (!f.Actors.ContainsKey(source)) { f.Actors[source] = new(source, 0x1B2, new((float)source - 24, 0, 0)); f.Scan(); }
        f.Now = f.Now.AddSeconds(.01);
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds));
        f.Actors[source] = f.Actors[source] with { Cast = cast };
        f.Engine.StartCast(source, cast, f.Now); f.Engine.Tick(f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Now = f.Now.AddSeconds(.01); f.Actors[source] = f.Actors[source] with { Cast = null };
        f.Engine.Finish(source, action, f.Now); f.Engine.Tick(f.Now); f.Render();
    }
    private static uint sequence = 1000;
    private static void Buster(Fight f) { f.Engine.Effect(1, 1464, sequence++, f.Now); f.Engine.Tick(f.Now); f.Render(); }
    private static void Tumult(Fight f, uint first)
    {
        for (uint hit = 0; hit < 4; ++hit)
        { f.Engine.Effect(1, 1465, first + hit, f.Now); f.Engine.Tick(f.Now); }
        f.Render();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

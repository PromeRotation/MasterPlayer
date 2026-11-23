using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class UnderkeepRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Underkeep native discovery territory ownership and no invented controls", () =>
        {
            Check(ArrDungeonCatalog.Create(1266).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.underkeep"), "Discovery.");
            Check(UnderkeepCatalog.Owns(0x4791) && !UnderkeepCatalog.Owns(0x4792), "Ownership.");
            using var f = Prepare(0x4791);
            Check(!f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Invented controls.");
        });
        run("Underkeep Gargant source 72 polygon radar and world", () =>
        {
            var type = typeof(BossMod.WorldState).Assembly.GetType("BossMod.Dawntrail.Dungeon.D10Underkeep.D101Gargant.D101Gargant")!;
            var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            Compare(UnderkeepArenas.Gargant(0), p => source.Contains(new(p.X - source.Center.X, p.Z - source.Center.Z)), 17, .27f);
        });
        foreach (var (boss, center, half) in new (uint, Vector3, float)[] { (0x47AD, new(0, 0, -182), 15.5f), (0x478E, new(0, 0, -331), 17.5f) })
            run($"Underkeep {boss:X} exact square arena", () =>
            {
                using var f = Prepare(boss);
                Compare(f.Engine.Arena!, p => MathF.Abs(p.X - center.X) <= half && MathF.Abs(p.Z - center.Z) <= half, 20, .37f);
            });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x4791, 42546, new BossMod.AOEShapeRect(30, 15)), (0x4791, 42543, new BossMod.AOEShapeRect(30, 7.5f)),
            (0x4791, 43161, new BossMod.AOEShapeCircle(10)), (0x47AD, 42573, new BossMod.AOEShapeRect(55, 4)),
            (0x478E, 43130, new BossMod.AOEShapeRect(40, 4.5f)), (0x478E, 42521, new BossMod.AOEShapeCone(26, new(25 * MathF.PI / 180))),
            (0x478E, 42738, new BossMod.AOEShapeCone(30, new(MathF.PI / 2))),
        })
            run($"Underkeep {action} source geometry locked packet correction finish dual output", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var c = f.Actors[10].Cast! with { Location = f.Actors[10].Position + Vector3.UnitX * 2, Rotation = 1.3f };
                f.Engine.StartCast(10, c, f.Now); f.Render();
                var a = Group(f, "simple").Single();
                Check(a.Position == c.Location && a.Rotation == (action == 43161 ? 0 : c.Rotation), "Packet correction.");
                Compare(a.Shape.Resolve(a.Position, a.Rotation, null), p => shape.Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)));
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Dual output.");
                f.Now = f.Now.AddSeconds(60); f.Engine.Tick(f.Now); Check(Group(f, "simple").Length == 1, "Time consumed.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Finish retained.");
            });
        run("Underkeep sand sphere creation packet clock strict prefix unsorted queue and FIFO", () =>
        {
            using var f = Prepare(0x4791); var start = f.Now;
            f.Engine.ObjectCreated(10, start);
            f.Engine.ObjectCreated(11, start.AddSeconds(1));
            f.Engine.ObjectCreated(12, start.AddSeconds(-1));
            foreach (var id in new ulong[] { 10, 11, 12 }) f.Actors[id] = new(id, 0x4792, f.Actors[1].Position + Vector3.UnitX * id);
            f.Now = start.AddSeconds(.4); Scan(f);
            var a = Group(f, "sphere"); Check(a.Length == 1 && Near(a[0].Activation, start, 7.9), "Prefix strict deadline / event clock.");
            Check(a[0].Position == Q(f.Actors[10].Position), "Quantization.");
            Scan(f); Check(Group(f, "sphere").Length == 1, "Scan duplicated.");
            f.Engine.Effect(10, 42545, 1, f.Now); Check(Group(f, "sphere").Length == 1, "Effect consumed.");
            Cast(f, 11, 43135); Finish(f, 11);
            a = Group(f, "sphere"); Check(a.Length == 2 && Near(a[0].Activation, start, 8.9) && Near(a[1].Activation, start, 6.9), "FIFO / sorted incorrectly.");
            f.Engine.RemoveActor(12); f.Actors.Remove(12); f.Now = start.AddSeconds(60); Scan(f);
            Check(Group(f, "sphere").Length == 2, "Destroy or timeout consumed sphere.");
        });
        run("Underkeep delayed Trap Jaws keeps original self fallback through finish until matching main target effect", () =>
        {
            using var f = Prepare(0x4791); f.Actors[1] = f.Actors[1] with { TargetId = 101 };
            Cast(f, 1, 42548, target: 1); Check(f.Engine.PredictedDamage.Count(d => d.DamageType == "Tankbuster" && d.TargetId == 101) == 1, "Fallback / duplicate.");
            f.Actors[1] = f.Actors[1] with { TargetId = 102 }; Finish(f, 1);
            f.Now = f.Now.AddSeconds(90); f.Engine.Tick(f.Now);
            Check(f.Engine.PredictedDamage.Single(d => d.DamageType == "Tankbuster").TargetId == 101, "Delay lost target.");
            f.Engine.Effect(1, 42548, 1, f.Now, 102); Check(f.Engine.PredictedDamage.Any(d => d.TargetId == 101), "Wrong effect cleared.");
            f.Engine.Effect(1, 42548, 2, f.Now, 101); Check(!f.Engine.PredictedDamage.Any(d => d.DamageType == "Tankbuster"), "Effect retained.");
        });
        foreach (var count in new[] { 4, 6 })
            run($"Underkeep sector graph unordered edges clone count {count} update clock and model tuple", () =>
            {
                using var f = Prepare(0x47AD); Sector(f, count);
                var now = f.Now; f.Engine.Tick(now);
                var a = Group(f, "sector").Single();
                Check(Near(a.Activation, now, count == 6 ? 4.2 : 5.9), "Update clock.");
                Check(a.Position == Q(f.Actors[21].Position) && MathF.Abs(a.Rotation - (f.Actors[21].Rotation - MathF.PI / 2)) < .00001, "Graph / direction.");
                Compare(a.Shape.Resolve(a.Position, a.Rotation, null), p => new BossMod.AOEShapeCone(45, new(MathF.PI / 2))
                    .Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)));
                Cast(f, 30, 42567); Finish(f, 30); Check(Group(f, "sector").Length == 0, "Finish did not reset.");
            });
        run("Underkeep sector snapshots acknowledge event tethers without clone inflation", () =>
        {
            using var f = Prepare(0x47AD);
            for (ulong id = 20; id < 26; ++id) f.Actors[id] = new(id, 0x47AE, f.Actors[1].Position + Vector3.UnitX * (id - 20));
            Scan(f);
            for (ulong id = 20; id < 26; ++id)
            {
                f.Engine.Tether(21, 313, true, f.Now, id);
                f.Engine.Tether(id == 20 ? 21u : 20u, 327, true, f.Now, id);
            }
            Scan(f); Scan(f);
            foreach (var id in f.Actors.Keys.Where(id => id is >= 20 and < 26).ToArray())
                f.Actors[id] = f.Actors[id] with { Tethers = new Dictionary<uint, ulong> { [313] = 21, [327] = id == 20 ? 21u : 20u } };
            Scan(f); f.Engine.Effect(20, 43163, 1, f.Now);
            f.Engine.Tether(20, 327, false, f.Now, 25); Scan(f);
            Check(Near(Group(f, "sector").Single().Activation, f.Now, 4.2), "Snapshot inflated clones or resurrected cancellation.");
        });
        run("Underkeep sector waits for active untether graph and uses latest target transform", () =>
        {
            using var f = Prepare(0x47AD);
            for (ulong id = 20; id < 24; ++id) f.Actors[id] = new(id, 0x47AE, f.Actors[1].Position);
            Scan(f);
            for (ulong id = 20; id < 24; ++id) { f.Engine.Tether(21, 313, true, f.Now, id); f.Engine.Tether(21, 327, true, f.Now, id); }
            f.Engine.Tether(21, 327, false, f.Now, 23); f.Engine.Tick(f.Now); Check(Group(f, "sector").Length == 0, "Pre-active untether affected edges.");
            f.Engine.Effect(20, 43163, 1, f.Now); f.Engine.Tick(f.Now); Check(Group(f, "sector").Length == 0, "No active missing edge.");
            f.Actors[21] = f.Actors[21] with { Position = f.Actors[21].Position + Vector3.UnitZ * 7, Rotation = .9f, ModelState = 5 };
            // Explicit cancellation is valid even when the raw snapshot already shows no tether.
            f.Engine.Tether(21, 327, false, f.Now, 22); Scan(f);
            var a = Group(f, "sector").Single(); Check(a.Position == Q(f.Actors[21].Position) && MathF.Abs(a.Rotation - (.9f + MathF.PI / 2)) < .00001, "Stale transform.");
        });
        foreach (var (boss, icon, action, delay, radius, half) in new (uint, uint, uint, double, float, float)[]
            { (0x47AD, 591, 42575, 5.1, 60, 15), (0x478E, 586, 43261, 7.4, 26, 25) })
            run($"Underkeep {icon} dynamic icon bait own outline away team other cone FIFO dead target", () =>
            {
                using var f = Prepare(boss); var now = f.Now;
                f.Engine.Icon(100, icon, now, 1); f.Engine.Icon(101, icon, now.AddSeconds(1), 1); f.Render();
                var a = Group(f, "bait").Single(a => a.Id.EndsWith("/display") && a.OutlineOnly);
                Check(Near(a.Activation, now, delay) && ((FanTemplate)a.Shape).Radius == radius, "Icon clock shape.");
                Check(Group(f, "bait").Where(a => a.Id.Contains("/away/")).All(a => ((FanTemplate)a.Shape).Radius == 100), "Own bait range.");
                f.Actors[100] = f.Actors[100] with { Position = f.Actors[100].Position + Vector3.UnitZ * 5 }; Scan(f);
                a = Group(f, "bait").Single(a => a.Id.EndsWith("/display") && a.OutlineOnly);
                Compare(a.Shape.Resolve(a.Position, a.Rotation, null), p => new BossMod.AOEShapeCone(radius, new(half * MathF.PI / 180))
                    .Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)));
                f.Engine.Effect(99, action, 1, f.Now); Check(Group(f, "bait").Count(a => a.Display) == 1, "FIFO.");
                f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); Check(Group(f, "bait").Length == 0, "Dead target not cleared.");
            });
        run("Underkeep line icon uses event source keeps dead target resolves only effect and enforces exact one", () =>
        {
            using var f = Prepare(0x478E); f.Engine.Icon(101, 525, f.Now, 1); f.Engine.Icon(102, 525, f.Now, 1);
            Check(Group(f, "line").Count(a => a.Display) == 2 && Group(f, "line").Any(a => a.Shape is DungeonExclusiveJoinTemplate), "Line joins.");
            Check(f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 4, "Shared predictions.");
            f.PlayerId = 101; Scan(f); Check(Group(f, "line").Any(a => a.Id.EndsWith("/gather")) && Group(f, "line").Any(a => a.Id.EndsWith("/avoid")), "Marked stack steering.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); Check(Group(f, "line").Count(a => a.Display) == 2, "Dead target cleared.");
            f.Engine.Effect(1, 42540, 1, f.Now, 999); Check(Group(f, "line").Count(a => a.Display) == 1, "Any effect FIFO.");
            f.Engine.Icon(999, 525, f.Now, 1); Check(Group(f, "line").Count(a => a.Display) == 1, "Unknown target fallback.");
        });
        run("Underkeep enforcement ray first preview arbitrary actor timeline source geometry center clock teleport all timings", () =>
        {
            using var f = Prepare(0x478E); Crosses(f);
            var a = Group(f, "cross").Single(); Check(Near(a.Activation, f.Now, 8.3), "Initial time.");
            Compare(a.Shape.Resolve(a.Position, 0, null), p => new BossMod.AOEShapeCross(36, 4.5f).Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), default));
            f.Engine.Tether(20, 282, true, f.Now, 24);
            var clock = f.Now;
            a = Group(f, "cross").Single(); Check(Near(a.Activation, f.Now, .6), "Teleport first.");
            var before = a.Id; f.Engine.Timeline(25, 0x11DA, f.Now); Check(Group(f, "cross").Single().Id == before, "Timeline after teleport.");
            ConsumeCross(f); Check(Near(Group(f, "cross").Single().Activation, clock, 7.9), "Second time.");
            ConsumeCross(f); Check(Near(Group(f, "cross").Single().Activation, clock, 14.4), "Third time.");
            ConsumeCross(f); Check(Group(f, "cross").Length == 0, "Batch clear.");
        });
        run("Underkeep cross second batch uses 11.4 then teleport 2.8 and 7.9", () =>
        {
            using var f = Prepare(0x478E); Crosses(f); ConsumeCross(f); ConsumeCross(f); ConsumeCross(f);
            f.Now = f.Now.AddSeconds(1); Crosses(f);
            Check(Near(Group(f, "cross").Single().Activation, f.Now, 11.4), "Second batch start.");
            f.Engine.Tether(20, 282, true, f.Now, 24);
            var clock = f.Now;
            Check(Near(Group(f, "cross").Single().Activation, f.Now, 2.8), "Second batch teleport.");
            ConsumeCross(f); Check(Near(Group(f, "cross").Single().Activation, clock, 7.9), "Second batch next.");
        });
        foreach (var (boss, action, radius) in new (uint, uint, float)[] { (0x4791, 43160, 5), (0x47AD, 43139, 6), (0x478E, 42524, 5) })
            run($"Underkeep {action} spread correction dynamic cached death and finish", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action, target: 101);
                f.Engine.StartCast(10, f.Actors[10].Cast! with { TargetId = 102 }, f.Now);
                f.Actors[102] = f.Actors[102] with { Position = f.Actors[102].Position + Vector3.UnitZ * 3 }; Scan(f);
                var a = Group(f, "spread").Single(a => a.Display);
                Check(a.Position == Q(f.Actors[102].Position) && ((CircleTemplate)a.Shape).Radius == radius, "Correction.");
                f.Actors[102] = f.Actors[102] with { Dead = true }; Scan(f); Check(Group(f, "spread").Length == 0, "Dead spread.");
                Finish(f, 10); Check(Group(f, "spread").Length == 0, "Finish.");
            });
        run("Underkeep temporal solver moves away from actual predicted sand sphere inside source arena", () =>
        {
            using var f = Prepare(0x4791); var c = f.Actors[1].Position; f.Actors[10] = new(10, 0x4792, c); Scan(f);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(c, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No safe route.");
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(path.Count >= 2 && path.All(p => f.Engine.Arena!.Contains(p.Position)) && Vector3.Distance(path[^1].Position, c) >= 6, "Route not outside danger.");
        });
        run("Underkeep alive detached primary preserves predictions death reset clears both views", () =>
        {
            using var f = Prepare(0x4791); f.Actors[10] = new(10, 0x4792, f.Actors[1].Position); Scan(f);
            f.Actors[1] = f.Actors[1] with { InCombat = false }; Scan(f); Check(Group(f, "sphere").Length == 1, "Detached reset.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Death retained.");
        });
        run("Underkeep destroy cast source forwards finish without clearing delayable buster", () =>
        {
            using var f = Prepare(0x4791); Cast(f, 10, 42546);
            f.Engine.RemoveActor(10); Check(Group(f, "simple").Length == 0, "Simple retained.");
            f.Actors[11] = new(11, 0x4792, f.Actors[1].Position); Scan(f); Cast(f, 11, 42545);
            f.Engine.RemoveActor(11); Check(Group(f, "sphere").Length == 0, "Sphere finish not forwarded.");
            Cast(f, 12, 42548, target: 101); f.Engine.RemoveActor(12);
            Check(f.Engine.PredictedDamage.Any(p => p.TargetId == 101 && p.DamageType == "Tankbuster"), "Buster cleared by destruction.");
        });
        run("Underkeep late sphere Create corrects clock and callback FIFO without restoring consumed object", () =>
        {
            using var f = Prepare(0x4791); var start = f.Now;
            foreach (var id in new ulong[] { 10, 11, 12 }) f.Actors[id] = new(id, 0x4792, f.Actors[1].Position + Vector3.UnitX * id);
            Scan(f); f.Engine.ObjectCreated(11, start.AddSeconds(-.5)); f.Engine.ObjectCreated(10, start.AddSeconds(.5));
            Check(Group(f, "sphere").Single().Id == "under/sphere/11" && Near(Group(f, "sphere").Single().Activation, start, 7.4), "Late callback order/clock.");
            Cast(f, 11, 42545); Finish(f, 11); f.Engine.ObjectCreated(11, start.AddSeconds(1));
            Check(Group(f, "sphere").All(a => a.Id != "under/sphere/11"), "Consumed sphere restored.");
        });
        run("Underkeep active tether target replacement implicitly removes old edge before appending new edge", () =>
        {
            using var f = Prepare(0x47AD);
            for (ulong id = 20; id < 24; ++id) f.Actors[id] = new(id, 0x47AE, f.Actors[1].Position + Vector3.UnitX * (id - 20));
            Scan(f); f.Engine.Tether(21, 313, true, f.Now, 20); f.Engine.Tether(21, 313, true, f.Now, 21);
            f.Engine.Tether(21, 327, true, f.Now, 20);
            f.Engine.Effect(20, 43163, 1, f.Now); f.Engine.Tether(22, 327, true, f.Now, 20); f.Engine.Tick(f.Now);
            Check(Group(f, "sector").Single().Position == Q(f.Actors[22].Position), "Old edge retained.");
        });
        run("Underkeep snapshot teleport followed by event corrects original clock without reapplying movement", () =>
        {
            using var f = Prepare(0x478E); Crosses(f); var start = f.Now;
            f.Actors[24] = f.Actors[24] with { Tethers = new Dictionary<uint, ulong> { [282] = 20 } }; Scan(f);
            var before = Group(f, "cross").Single();
            f.Engine.Tether(20, 282, true, start.AddSeconds(-.5), 24);
            var a = Group(f, "cross").Single();
            Check(Near(a.Activation, start, .1) && a.Position == before.Position && a.Id == before.Id, "Late teleport event clock.");
            ConsumeCross(f); Check(Near(Group(f, "cross").Single().Activation, start, 7.4), "Remaining times not corrected.");
        });
        run("Underkeep late teleport after consumed head and raw untether keeps remaining time offsets", () =>
        {
            using var f = Prepare(0x478E); Crosses(f); var start = f.Now;
            f.Actors[24] = f.Actors[24] with { Tethers = new Dictionary<uint, ulong> { [282] = 20 } }; Scan(f);
            ConsumeCross(f); f.Actors[24] = f.Actors[24] with { Tethers = null }; Scan(f);
            f.Engine.Tether(20, 282, true, start.AddSeconds(-.5), 24);
            Check(Near(Group(f, "cross").Single().Activation, start, 7.4), "Remaining head was reindexed.");
        });
        run("Underkeep late teleport correction respects times overwritten by a subsequent timeline", () =>
        {
            using var f = Prepare(0x478E); Crosses(f); var start = f.Now;
            // A fresh fight whose starting center has not been established yet.
            f.Engine.ResetFight(); Scan(f);
            foreach (var id in new ulong[] { 20, 21, 22 }) f.Engine.Timeline(id, 0x11DA, start);
            f.Engine.Timeline(20, 0x11D5, start);
            f.Actors[24] = f.Actors[24] with { Tethers = new Dictionary<uint, ulong> { [282] = 20 } }; Scan(f);
            f.Engine.Timeline(21, 0x11D5, start.AddSeconds(.2));
            var before = Group(f, "cross").Single().Activation;
            f.Engine.Tether(20, 282, true, start.AddSeconds(-.5), 24);
            Check(Group(f, "cross").Single().Activation == before, "Timeline time shifted.");
        });
    }

    private static void Sector(Fight f, int count)
    {
        for (ulong id = 20; id < (ulong)(20 + count); ++id)
            f.Actors[id] = new(id, 0x47AE, f.Actors[1].Position + Vector3.UnitX * (id - 20), ModelState: 6, Rotation: .4f);
        Scan(f);
        for (var n = count - 1; n >= 0; --n)
        {
            var id = (ulong)(20 + n);
            f.Engine.Tether(21, 313, true, f.Now, id);
            f.Engine.Tether(id == 20 ? 21u : 20u, 327, true, f.Now, id);
        }
        f.Engine.Effect(20, 43164, 1, f.Now);
        f.Engine.Tether(20, 327, false, f.Now, (ulong)(19 + count));
    }
    private static void Crosses(Fight f)
    {
        var c = f.Actors[1].Position;
        for (ulong id = 20; id < 26; ++id) f.Actors[id] = new(id, id == 24 ? 0x47B5u : 0x4789u, c + Vector3.UnitX * (id - 20) * 3);
        Scan(f);
        foreach (var id in new ulong[] { 22, 20, 21 }) f.Engine.Timeline(id, 0x11DA, f.Now);
        f.Engine.Timeline(20, 0x11D5, f.Now); f.Engine.Timeline(21, 0x11D5, f.Now);
    }
    private static void ConsumeCross(Fight f)
    {
        var pos = Group(f, "cross").Single().Position;
        f.Now = f.Now.AddMilliseconds(10);
        f.Actors[30] = new(30, 0x233C, pos);
        Cast(f, 30, 42737); Finish(f, 30);
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1266); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * (i * 3 + 2), PartySlot: i, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double duration = 5, float rotation = .7f, ulong target = 0)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        var cast = new DungeonCastObservation(action, f.Actors[id].Position, rotation, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[id] = f.Actors[id] with { Cast = cast }; Scan(f); f.Engine.StartCast(id, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id) { f.Actors[id] = f.Actors[id] with { Cast = null }; f.Engine.Finish(id, endedAt: f.Now); f.Render(); }
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("under/" + key)).ToArray();
    private static bool Near(DateTime? at, DateTime start, double seconds) => at is { } value && Math.Abs((value - start).TotalSeconds - seconds) < .00001;
    private static Vector3 Q(Vector3 p) { var q = new BossMod.WPos(p.X, p.Z).Quantized(); return new(q.X, p.Y, q.Z); }
    private static void Compare(IGeometryShape native, Func<Vector3, bool> source, float range = 58, float step = .97f)
    {
        var world = HostDrawingGeometry.Convert(native);
        for (var x = -range + .137f; x < range; x += step)
            for (var z = -range + .231f; z < range; z += step)
            {
                var p = native.Position + new Vector3(x, 0, z);
                Check(native.Contains(p, 0) == source(p) && world.Contains(p, 0) == source(p), $"Dual geometry at {p}.");
            }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

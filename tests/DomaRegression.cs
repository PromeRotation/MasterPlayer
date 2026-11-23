using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class DomaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var boss in new uint[] { 0x1BCC, 0x1BD0, 0x1BD4 })
            run($"Doma {boss:X} exact square source geometry and both views", () =>
            {
                using var f = Prepare(boss);
                var angle = boss == 0x1BCC ? 76.5f * MathF.PI / 180 : 0;
                var source = new BossMod.ArenaBoundsSquare(19.5f, new(angle));
                var host = HostDrawingGeometry.Convert(f.Engine.Arena!); var center = Center(boss);
                for (var x = -30f; x < 30; x += .43f)
                    for (var z = -30f; z < 30; z += .43f)
                    {
                        var p = center + new Vector3(x, 0, z); var expected = source.Contains(new(x, z));
                        Check(f.Engine.Arena!.Contains(p) == expected && host.Contains(p) == expected, $"Square differs at {p}.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 1144) && !ArrDungeonCatalog.Owns(boss, 1143), "Territory ownership differs.");
            });
        foreach (var (oid, action, p) in new (uint, uint, Vector3)[]
        {
            (0x1BCD, 8351, new(5.9f, 0, 0)), (0x1BCF, 8353, new(0, 0, 45.8f)),
            (0x1BCE, 8352, new(5.9f, 0, 0)),
        })
            run($"Doma Rearguard {action} cast shape and cancellation", () =>
            {
                using var f = Prepare(0x1BCC); var center = Center(0x1BCC);
                f.Actors[10] = new(10, oid, center); Scan(f); Cast(f, 10, action, 3);
                Check(f.Draw.Previews.Values.Any(a => a.Geometry.Contains(center + p)), "AOE dimensions differ.");
                f.Engine.Finish(10); f.Render(); Check(f.Engine.OutputCasts.All(c => c.Value.Rule.Action != action), "Cancelled cast retained.");
            });
        run("Doma Rearguard cleave follows tank avoids party and hides throughout casting", () =>
        {
            using var f = Prepare(0x1BCC); var center = Center(0x1BCC);
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 8 };
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, Hostile: false, PartySlot: 1);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/party/101")), "Tank cleave direction constraint missing.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id == "doma/cleave/avoid"), "Other viewer cleave missing.");
            Cast(f, 1, 8350, 3); Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("doma/cleave")), "Cleave active while casting.");
            f.Actors[1] = f.Actors[1] with { Cast = null }; f.Engine.Finish(1); Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "doma/cleave/avoid"), "Cleave failed to resume.");
        });
        run("Doma moving mine capsule display and four future windows preserve world contours", () =>
        {
            using var f = Prepare(0x1BCC); var center = Center(0x1BCC);
            f.Actors[10] = new(10, 0x1BCE, center, Rotation: MathF.PI / 2); Scan(f);
            var display = f.Draw.Previews.Single().Value.Geometry;
            Check(display.Contains(center + Vector3.UnitX * 10.8f) && !display.Contains(center + Vector3.UnitX * 11)
                && !display.Contains(center + Vector3.UnitZ), "Mine is not forward capsule R.9 L10.");
            var host = HostDrawingGeometry.Convert(display);
            Check(host.Contains(center + Vector3.UnitX * 8) && !host.Contains(center - Vector3.UnitX * 2), "World capsule changed.");
            var future = f.Engine.AmbientAoes().Where(a => a.Id.Contains("/future/")).ToArray();
            Check(future.Length == 4 && future.Select(a => a.Activation).SequenceEqual(new DateTime?[]
                { f.Now.AddSeconds(1.1), f.Now.AddSeconds(3), f.Now.AddSeconds(10), DateTime.MaxValue })
                && f.Snapshot.HardObstacles.Count == 1 && f.Snapshot.DangerAreas.Count == 4, "Mine future windows or obstacle differ.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.HardObstacles.Count == 0 && f.Draw.Previews.Count == 0, "Dead mine retained.");
        });
        foreach (var boss in new uint[] { 0x1BD0, 0x1BD4 })
            run($"Doma {boss:X} chain tether locks quantized origin and clock then clears", () =>
            {
                using var f = Prepare(boss); var center = Center(boss);
                var oid = boss == 0x1BD0 ? 0x1BD2u : 0x1BD6u;
                f.Actors[10] = new(10, oid, center, Rotation: MathF.PI / 2); Scan(f);
                f.Engine.Tether(11, 60, true, f.Start, 10); f.Render();
                var chain = f.Engine.AmbientAoes().Single();
                var shape = chain.Shape.Resolve(chain.Position, chain.Rotation, null);
                Check(chain.Activation == f.Start.AddSeconds(5.6) && shape.Contains(center - Vector3.UnitX * 9.9f)
                    && shape.Contains(center + Vector3.UnitX * 39.9f) && !shape.Contains(center + Vector3.UnitZ * 2.1f), "Chain back/front/width or time differs.");
                f.Engine.Tether(11, 60, true, f.Start.AddSeconds(1), 10); f.Render();
                Check(f.Engine.AmbientAoes().Single().Activation == chain.Activation, "Repeated tether reset clock.");
                f.Engine.Tether(11, 60, false, f.Start.AddSeconds(2), 10); f.Render(); Check(f.Draw.Previews.Count == 0, "Untether retained chain.");
            });
        run("Doma chain navigation suppressed for two thermobaric casters while both views retain chain", () =>
        {
            using var f = Prepare(0x1BD4); var center = Center(0x1BD4);
            f.Actors[10] = new(10, 0x1BD6, center); f.Actors[11] = new(11, 0x1BD5, center - Vector3.UnitX * 15);
            f.Actors[12] = new(12, 0x1BD5, center + Vector3.UnitX * 15); Scan(f);
            f.Engine.Tether(20, 60, true, f.Start, 10); Cast(f, 11, 8367, 10); Cast(f, 12, 8367, 10);
            Check(f.Snapshot.DangerAreas.Count == 2 && f.Draw.Previews.Count == 3
                && !f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("doma/chain")).Navigate, "Two bombs did not suppress chain planning only.");
            f.Engine.Finish(12); f.Render(); Check(f.Snapshot.DangerAreas.Count == 2 && f.Draw.Previews.Count == 2, "One bomb did not restore chain planning.");
        });
        run("Doma Hexadrone icon stack clock and target viewer semantics", () =>
        {
            using var f = Prepare(0x1BD0); var center = Center(0x1BD0);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(101, 62, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/enter")).Activation == f.Start.AddSeconds(5.1)
                && f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 1, "Stack timing/strong demand missing.");
            f.PlayerId = 101; Scan(f); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 1, "Stack target moved away from self.");
            f.Engine.Effect(1, 8355, 1, f.Now, 101); f.Render(); Check(f.Draw.Previews.Count == 0, "Stack effect retained.");
        });
        run("Doma Hexadrone R6 towers true solver selects empty tower", () =>
        {
            using var f = Prepare(0x1BD0); var center = Center(0x1BD0);
            f.Actors[10] = new(10, 0x1BD1, center + Vector3.UnitX * 10);
            f.Actors[11] = new(11, 0x1BD1, center - Vector3.UnitX * 10);
            f.Actors[101] = new(101, 0, f.Actors[10].Position, PartySlot: 1, Hostile: false); Scan(f);
            Cast(f, 10, 8357, 8); Cast(f, 11, 8357, 8);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(center, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && Vector3.Distance(path[^1].Position, f.Actors[11].Position) <= 6, "Tower solver failed.");
        });
        run("Doma five chainsaw effects survive natural first finish and deduplicate", () =>
        {
            using var f = Prepare(0x1BD4); var center = Center(0x1BD4);
            Cast(f, 1, 8360, 2, rotation: MathF.PI / 2);
            f.Engine.Effect(1, 8360, 1, f.Now); f.Engine.Finish(1, completed: true); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.Geometry.Contains(center + Vector3.UnitX * 4.5f), "First saw finish removed subsequent four hits.");
            f.Engine.Effect(1, 8360, 1, f.Now);
            for (uint i = 2; i <= 5; ++i)
            {
                f.Engine.Effect(10, 8361, i, f.Now); f.Render();
                Check(f.Draw.Previews.Count == (i == 5 ? 0 : 1), $"Saw hit {i} differs.");
            }
            f.Now = f.Start.AddSeconds(5); Cast(f, 1, 8360, 2); f.Engine.Finish(1); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled saw retained.");
        });
        run("Doma gunsaw chooses actual heading target and clears on fourth rest effect", () =>
        {
            using var f = Prepare(0x1BD4); var center = Center(0x1BD4);
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 8 };
            f.Actors[101] = new(101, 0, center + new Vector3(.5f, 0, 8), PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Effect(1, 8362, 1, f.Now, 101); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/party/101")), "Gunsaw used packet target instead of heading.");
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 8 }; Scan(f);
            var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(display.Shape.Resolve(display.Position, display.Rotation, null).Contains(center + Vector3.UnitX * 20), "Gunsaw did not follow target.");
            for (uint i = 1; i <= 4; ++i)
            {
                f.Engine.Effect(1, 8363, i + 1, f.Now); f.Render();
                Check(f.Draw.Previews.Count == (i == 4 ? 0 : 1), "Gunsaw rest count differs.");
            }
        });
        run("Doma CleanCut locks real endpoints and cancellation clears", () =>
        {
            using var f = Prepare(0x1BD4); var center = Center(0x1BD4);
            f.Actors[10] = new(10, 0x1BD8, center - Vector3.UnitX * 15); Scan(f);
            Cast(f, 10, 8369, 6, location: center + Vector3.UnitX * 15);
            var g = f.Draw.Previews.Single().Value.Geometry;
            Check(g.Contains(center + Vector3.UnitZ * 3.9f) && !g.Contains(center + Vector3.UnitZ * 4.1f), "CleanCut width differs.");
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "CleanCut cancellation retained.");
        });
        run("Doma prey raw status clock edge bait removal and stale-snapshot protection", () =>
        {
            using var f = Prepare(0x1BD4); var center = Center(0x1BD4);
            f.Actors[100] = f.Actors[100] with { Statuses = [1253], StatusExpirations = new Dictionary<uint, DateTime> { [1253] = f.Start.AddSeconds(9) } }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "doma/prey/100").Activation == f.Start.AddSeconds(9)
                && f.Engine.AmbientAoes().Any(a => a.Id == "doma/prey/edge"), "Restored prey clock or corner bait missing.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 1253, Timestamp = f.Start }, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Prey removal retained.");
            Scan(f); Check(f.Draw.Previews.Count == 0, "Stale status resurrected prey.");
            f.Now = f.Start.AddSeconds(2); Scan(f);
            Check(f.Draw.Previews.Count == 0, "Stale status resurrected prey after event override expired.");
            f.Actors[100] = f.Actors[100] with { Statuses = [] }; Scan(f);
            f.Actors[100] = f.Actors[100] with { Statuses = [1253] }; Scan(f);
            Check(f.Draw.Previews.Count == 1, "Fresh prey did not restore after confirmed status loss.");
        });
        run("Doma prey event duration and spread icon use original timestamps", () =>
        {
            using var f = Prepare(0x1BD4);
            f.Now = f.Start.AddSeconds(.2);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 1253, Duration = 8, Timestamp = f.Start }, f.Now);
            f.Engine.Icon(100, 99, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "doma/prey/100").Activation == f.Start.AddSeconds(8)
                && f.Engine.AmbientAoes().Single(a => a.Id == "doma/spread/100").Activation == f.Start.AddSeconds(4), "Dispatch latency shifted clocks.");
            f.Engine.Effect(10, 8365, 1, f.Now, 100); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("doma/spread")), "Spread effect retained.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset leaked.");
        });
    }

    private static Vector3 Center(uint boss) => boss switch { 0x1BCC => new(124.64f, 0, 17.54f), 0x1BD0 => new(-240, 0, 130.5f), _ => new(-240, 0, -198) };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1144); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, Vector3? location = null, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

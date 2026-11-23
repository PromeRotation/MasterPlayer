using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class SohmAlRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (geometry, name) in new[] { (SohmAlArenas.Raskovnik(0), "D021Raskovnik"),
            (SohmAlArenas.Myath(0), "D022Myath"), (SohmAlArenas.Tioman(0), "D023Tioman") })
            run($"Sohm Al {name} exact arena and host drawing match runtime", () =>
            {
                var type = typeof(BossMod.Heavensward.Dungeon.D02SohmAl.D021Raskovnik.D021Raskovnik).Assembly
                    .GetType($"BossMod.Heavensward.Dungeon.D02SohmAl.{name}.{name}")!;
                var bounds = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(geometry);
                for (var x = -34.13f; x < 34; x += .43f)
                    for (var z = -34.27f; z < 34; z += .43f)
                    {
                        var p = geometry.Position + new Vector3(x, 0, z);
                        var expected = bounds.Contains(new(p.X - bounds.Center.X, p.Z - bounds.Center.Z));
                        Check(geometry.Contains(p) == expected && host.Contains(p) == expected, "Arena topology changed.");
                    }
            });
        foreach (var (boss, oid, action, distance, outside) in new[]
        {
            (0xE8Fu, 0x1B2u, 3799u, 5.99f, 6.01f),
            (0xE8Fu, 0xE8Fu, 3797u, 48.67f, 48.69f),
            (0xE8Fu, 0xE8Fu, 5010u, 7.99f, 8.01f),
            (0xE91u, 0xE91u, 3804u, 64.89f, 64.91f),
            (0xE96u, 0xE96u, 3813u, 1.99f, 2.01f),
            (0xE96u, 0x1B2u, 3819u, 50.49f, 50.51f),
            (0xE96u, 0x1B2u, 3816u, 3.99f, 4.01f),
            (0xE96u, 0x1B2u, 3818u, 4.99f, 5.01f),
        })
            run($"Sohm Al AID {action} exact shape margin locked pose and timestamp", () =>
            {
                using var f = new Fight(boss, 1064);
                var source = oid == boss ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, oid, f.Center); f.Scan(); }
                f.Cast(source, action, 4, MathF.PI / 2);
                var shape = f.Draw.Previews.Single().Value.Geometry;
                var host = HostDrawingGeometry.Convert(shape);
                Check(shape.Contains(Vector3.UnitX * distance) && host.Contains(Vector3.UnitX * distance)
                    && !shape.Contains(Vector3.UnitX * outside), "Display size or forward axis changed.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3),
                    "Native cast lost packet delay.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * (outside + .2f)), "Navigation margin missing.");
                f.Engine.Finish(source, action); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancellation leaked.");
            });
        run("Sohm Al Floral Trap follows marked player, uses original icon clock and separates perspectives", () =>
        {
            using var f = new Fight(0xE8F, 1064);
            f.Actors[100] = new(100, 0, Vector3.UnitX * 10, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitZ * 10, PartySlot: 1, Hostile: false); f.Scan();
            f.Events.Handle(new LogSystemTargetIconEvent { SourceId = 100, MarkerId = 46, Timestamp = f.Start.AddSeconds(-.2) }, f.Now); f.Render();
            Check(f.Draw.Previews.Values.Single().Geometry.Contains(Vector3.UnitX * 20), "Bait cone not directed at marked player.");
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitZ * 20)
                && !f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * 20), "Marked player avoided own cone.");
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(8.3), "Icon dispatch delay changed activation.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * 20), "Other perspective ignored bait.");
            f.Actors[100] = f.Actors[100] with { Position = -Vector3.UnitZ * 10 }; f.Scan();
            Check(f.Draw.Previews.Values.Single().Geometry.Contains(-Vector3.UnitZ * 20), "Bait did not follow target.");
            f.Engine.Effect(1, 5009, 1, f.Now, 100); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Floral Trap effect failed to clear.");
        });
        run("Sohm Al Myath stack disables cleave in both outputs and spread follows actual target", () =>
        {
            using var f = new Fight(0xE91, 1064);
            f.Actors[100] = new(100, 0, Vector3.UnitX * 10, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitZ * 10, PartySlot: 1, Hostile: false);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
            Check(f.Draw.Previews.Count == 1, "Default cleave missing.");
            var cast = new DungeonCastObservation(3809, f.Center, 0, f.Now, f.Now.AddSeconds(5.3), 101);
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.DangerAreas.Count == 0,
                "Stack conflicted with cleave.");
            f.Actors[101] = f.Actors[101] with { Position = Vector3.UnitZ * 15 }; f.Scan();
            Check(f.Draw.Previews.Values.Single().Geometry.Position.Z == 15, "Stack position did not follow target.");
            f.Complete(1, 5);
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 1, "Cleave failed to return after stack.");
            cast = new(3808, f.Center, 0, f.Now, f.Now.AddSeconds(5.3), 101);
            f.Engine.StartCast(1, cast, f.Now); f.Render();
            Check(f.Snapshot.DangerAreas.Any(a => a.Geometry.Position == f.Actors[101].Position), "Spread ignored cast target.");
        });
        run("Sohm Al Tioman meteor transitions from edge bait to locked delayed impact and survives natural cast finish", () =>
        {
            using var f = new Fight(0xE96, 1064);
            f.Actors[100] = new(100, 0, new(-104, 0, -395), PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, new(-100, 0, -395), PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x1B2, new(-85, 0, -395)); f.Scan();
            f.Engine.Icon(100, 7, f.Now); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(8.1),
                "Meteor edge bait or activation missing.");
            var cast = new DungeonCastObservation(4999, f.Actors[10].Position, null, f.Now, f.Now.AddSeconds(3.8));
            f.Actors[10] = f.Actors[10] with { Cast = cast }; f.Engine.StartCast(10, cast, f.Now); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 1
                && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5.3),
                "Meteor did not lock at cast start or lost extra delay.");
            f.Complete(10, 3.5);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Natural cast finish removed delayed impact.");
            f.Actors[10] = f.Actors[10] with { Position = Vector3.Zero }; f.Scan();
            Check(f.Draw.Previews.Values.Single().Geometry.Position.X == -85, "Locked impact followed helper.");
            f.Engine.Effect(20, 4997, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Meteor hit failed to clear all impacts.");
        });
        run("Sohm Al Tioman heavensfall edge goal is reachable by actual solver with navigation margin", () =>
        {
            using var f = new Fight(0xE96, 1064);
            var player = new Vector3(-104, 0, -395);
            f.Actors[100] = new(100, 0, player, PartySlot: 0, Hostile: false); f.Scan();
            f.Engine.Icon(100, 10); f.Render();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && Vector3.Distance(path[^1].Position, player) >= 26 && f.Engine.Arena!.Contains(path[^1].Position),
                $"No reachable edge bait: {solution.Status}, fallback={solution.IsLeastDangerFallback}, points={path.Count}, end={(path.Count > 0 ? path[^1].Position : default)}, distance={(path.Count > 0 ? Vector3.Distance(path[^1].Position, player) : 0)}.");
            f.Engine.Effect(1, 3817, 1, f.Now, 100); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "Heavensfall hit retained edge goal.");
        });
        run("Sohm Al icon target death and reset release personal outputs and priorities select actual adds", () =>
        {
            foreach (var (boss, add) in new[] { (0xE8Fu, 0x13C2u), (0xE91u, 0xE92u), (0xE96u, 0x10B4u) })
            {
                using var f = new Fight(boss, 1064);
                f.Actors[10] = new(10, add, Vector3.UnitX); f.Scan();
                Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Add priority lost.");
                f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
                Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 1, "Dead add remained selected.");
            }
            using var bait = new Fight(0xE96, 1064);
            bait.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0); bait.Scan();
            bait.Engine.Icon(100, 10); bait.Render();
            bait.Actors[100] = bait.Actors[100] with { Dead = true }; bait.Scan();
            Check(bait.Snapshot.GoalAreas.Count == 0 && bait.Draw.Previews.Count == 0, "Dead bait target leaked.");
            bait.Engine.ResetFight(); bait.Render(); Check(bait.Snapshot.ArenaBoundary == null, "Reset retained arena.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

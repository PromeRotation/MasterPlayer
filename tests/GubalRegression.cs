using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class GubalRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Gubal native ownership covers three bosses in territory 1109 only", () =>
        {
            foreach (var oid in new uint[] { 0xE82, 0xE83, 0xE84 })
                Check(ArrDungeonCatalog.Owns(oid, 1109) && !ArrDungeonCatalog.Owns(oid, 0), "Wrong native ownership.");
        });
        run("Gubal Disclosure preserves delayed spin through cast completion and switches both arena contours", () =>
        {
            using var f = new Fight(0xE82, 1109);
            Check(f.Engine.Arena!.GetContours().Count == 2, "Default separated arena missing.");
            f.Actors[10] = new(10, 0xED6, Vector3.Zero); f.Scan(); f.Cast(10, 4818, 8);
            Check(f.Engine.Arena!.GetContours().Count == 2 && f.Engine.Arena.Contains(new(0, 0, 7)), "Spin corridor not opened.");
            Check(f.Engine.OutputCasts.Single(c => c.Key == "gubal/spin").Value.FinishAt == f.Start.AddSeconds(10.7), "Spin delay lost.");
            f.Complete(10, 8);
            Check(f.Engine.OutputCasts.Count() == 1, "Natural cast completion lost spin.");
            f.Engine.Effect(10, 3989, 1, f.Now); f.Render();
            Check(!f.Engine.OutputCasts.Any() && !f.Engine.Arena.Contains(new(0, 0, 7)), "Spin effect failed to restore separated arena.");
        });
        run("Gubal interrupted Disclosure releases spin and restores arena", () =>
        {
            using var f = new Fight(0xE82, 1109);
            f.Actors[10] = new(10, 0xED6, Vector3.Zero); f.Scan(); f.Cast(10, 4818, 8);
            f.Engine.Finish(10); f.Render();
            Check(!f.Engine.OutputCasts.Any() && !f.Engine.Arena!.Contains(new(0, 0, 7)), "Cancellation leaked.");
        });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0xE82u, 0xED6u, 3520u, 57.84f), (0xE82u, 0xED6u, 3521u, 57.84f),
            (0xE83u, 0x10B7u, 5058u, 24.2f), (0xE83u, 0xE83u, 4165u, 12f),
            (0xE84u, 0xEA5u, 3531u, 41f), (0xE84u, 0xE86u, 3535u, 5f),
        })
            run($"Gubal AID {action} preserves dimensions cast deadline and cancellation", () =>
            {
                using var f = new Fight(boss, 1109); var source = boss == caster ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Vector3.Zero); f.Scan(); }
                f.Cast(source, action, 4, MathF.PI / 2);
                var preview = f.Draw.Previews.Values.Single().Geometry;
                Check(preview.Contains(Vector3.UnitX * (range - .02f)) && !preview.Contains(Vector3.UnitX * (range + .02f)),
                    "True display extent differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3), "Deadline differs.");
                f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled area leaked.");
            });
        run("Gubal Repel ignores immunity and stops at the first hole boundary along packet facing", () =>
        {
            using var f = new Fight(0xE82, 1109);
            f.Actors[100] = new(100, 0, new(-10, 0, 0), PartySlot: 0, Statuses: [160], Hostile: false); f.Scan();
            f.Cast(1, 3519, 3, MathF.PI / 2);
            var landing = f.Engine.AmbientAoes().Single(a => a.Id == "gubal/repel/landing").Position;
            Check(landing.X > -4 && landing.X < -3 && MathF.Abs(landing.Z) < .001f, "Knockback crossed hole or used zero facing.");
        });
        run("Gubal thin ice follows actor facing in standard and recorded camera in legacy movement", () =>
        {
            using var f = new Fight(0xE82, 1109);
            f.Actors[100] = new(100, 0, new(-10, 0, 0), PartySlot: 0, Statuses: [911], Hostile: false); f.Scan();
            var standard = f.Engine.AmbientAoes().Single(a => a.Id == "gubal/ice/landing").Position;
            f.Engine.LegacyMovement = true; f.Engine.CameraAzimuth = -MathF.PI / 2;
            var legacy = f.Engine.AmbientAoes().Single(a => a.Id == "gubal/ice/landing").Position;
            Check(standard.Z > 9 && legacy.X > -4 && legacy.X < -3, "Movement mode direction missing.");
            f.Engine.StatusChanged(100, 911, false, f.Now);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("gubal/ice")), "Ice removal leaked.");
        });
        run("Gubal invincibility bans every afflicted target and releases cleave on removal", () =>
        {
            using var f = new Fight(0xE83, 1109);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[100] = new(100, 0, Vector3.UnitZ * 4, PartySlot: 0, Hostile: false); f.Scan();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "cleave/display"), "Initial cleave missing.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 325, Timestamp = f.Start }, f.Now);
            Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]) && f.Engine.PriorityTarget(1, Vector3.Zero) == 0
                && !f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("cleave/")), "Invincibility did not suppress attack and cleave.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 1, StatusId = 325, Timestamp = f.Start }, f.Now);
            Check(f.Engine.ForbiddenTargets.Length == 0 && f.Engine.PriorityTarget(0, Vector3.Zero) == 1, "Invincibility release failed.");
        });
        run("Gubal wind is a true obstacle and whale oil goals are personal with cancellation isolation", () =>
        {
            using var f = new Fight(0xE83, 1109);
            f.Actors[100] = new(100, 0, Vector3.UnitX * 8, PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0x10C4, Vector3.UnitX * 15, Tethers: new Dictionary<uint, ulong> { [3] = 100 });
            f.Actors[11] = new(11, 0x10C5, Vector3.UnitZ * 8); f.Scan();
            Check(f.Snapshot.HardObstacles.Count == 1 && f.Snapshot.GoalAreas.Count == 1
                && f.Draw.Previews.Keys.Any(k => k.Contains("tether")), "Wind, oil goal or tether drawing missing.");
            f.Engine.Tether(100, 3, false, f.Now, 10); f.Scan();
            f.Now = f.Start.AddSeconds(1); f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0, "Stale snapshot resurrected cancelled oil.");
            f.Engine.Tether(101, 3, true, f.Now, 10); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "Other player's oil goal leaked.");
        });
        run("Gubal sphere birth deadlines correct delayed create and FIFO completion never resurrects objects", () =>
        {
            using var f = new Fight(0xE84, 1109);
            f.Now = f.Start.AddSeconds(1);
            f.Actors[10] = new(10, 0xEC5, new(380, 0, -60)); f.Scan();
            f.Engine.ObjectCreated(10, f.Start); f.Render();
            var sphere = f.Engine.OutputCasts.Single().Value;
            Check(sphere.FinishAt == f.Start.AddSeconds(9.8), "Dispatch delay contaminated birth deadline.");
            f.Actors[10] = f.Actors[10] with { Position = Vector3.Zero }; f.Scan();
            Check(f.Engine.OutputCasts.Single().Value.Location == sphere.Location, "Sphere did not lock birth position.");
            f.Cast(10, 3780, 4); f.Engine.Finish(10); f.Scan();
            Check(!f.Engine.OutputCasts.Any(), "Finished sphere resurrected from old snapshot.");
        });
        run("Gubal icon bait keeps original clock edge assignment and per-target effect removal", () =>
        {
            using var f = new Fight(0xE84, 1109);
            f.Actors[100] = new(100, 0, new(377.8f, 0, -59.7f), PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, new(380, 0, -59.7f), PartySlot: 1, Hostile: false); f.Scan();
            f.Engine.Icon(100, 23, f.Start); f.Engine.Icon(101, 23, f.Start.AddSeconds(1)); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.DangerAreas.Count == 2
                && f.Snapshot.DangerAreas.Any(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5.1)), "Bait clock or first edge goal missing.");
            f.Engine.Effect(1, 3526, 1, f.Now, 100); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 1, "Target effect removed wrong bait.");
        });
        foreach (var (number, needed) in new[] { (2, 3), (3, 2), (6, 1) })
            run($"Gubal {number} towers require {needed} soakers and preserve birth clock", () =>
            {
                using var f = new Fight(0xE84, 1109);
                f.Actors[100] = new(100, 0, new(378, 0, -60), PartySlot: 0, Hostile: false);
                for (var i = 0; i < number; ++i) f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), 0x1E99F3, new(380 + i * 7, 0, -60));
                f.Scan(); f.Engine.ObjectCreated(10, f.Start.AddSeconds(-.5)); f.Render();
                Check(f.Engine.Hints(null).Contains($"召唤塔：每塔需要 {needed} 人")
                    && f.Engine.AmbientAoes().Any(a => a.Id == "gubal/tower/10/display" && a.Name.EndsWith($"/{needed}")), "Tower requirements differ.");
                f.Engine.Animation(10, 0x00100020); f.Scan();
                Check(f.Engine.AmbientAoes().Count(a => a.Id.EndsWith("/display")) == number - 1, "Resolved tower reappeared.");
            });
        run("Gubal add creation clears every tower independent of object enumeration order", () =>
        {
            using var f = new Fight(0xE84, 1109);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[11] = new(11, 0xE86, Vector3.Zero);
            f.Actors[10] = new(10, 0x1E99F3, Vector3.Zero); f.Scan();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("gubal/tower/")), "Tower survived summon.");
        });
        run("Gubal full towers forbid excess entry and reset clears native output", () =>
        {
            using var f = new Fight(0xE84, 1109);
            for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, Vector3.Zero, PartySlot: i, Hostile: false);
            f.Actors[10] = new(10, 0x1E99F3, Vector3.Zero); f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == 1, "Overfull tower not avoided.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.ArenaBoundary == null, "Reset leaked.");
        });
        run("Gubal tower publishes a reachable real solver route within the authored arena", () =>
        {
            using var f = new Fight(0xE84, 1109);
            var player = new Vector3(377.8f, 0, -59.7f);
            var tower = player + Vector3.UnitX * 12;
            f.Actors[100] = new(100, 0, player, PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0x1E99F3, tower); f.Scan();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && path.Count > 1 && Vector3.Distance(path[^1].Position, tower) <= 3,
                "No real movement route into tower.");
        });
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

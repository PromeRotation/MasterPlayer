using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Teratotaur = BossMod.RealmReborn.Dungeon.D08Qarn.D081Teratotaur.D081Teratotaur;
using Guardian = BossMod.RealmReborn.Dungeon.D08Qarn.D082TempleGuardian.D082TempleGuardian;

internal static class QarnRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Qarn polygon arenas and world drawing preserve original concave contours", () =>
        {
            foreach (var (geometry, type) in new[]
            {
                (QarnArenas.Teratotaur(3), typeof(Teratotaur)),
                (QarnArenas.TempleGuardian(3), typeof(Guardian)),
            })
            {
                var original = (BossMod.ArenaBoundsCustom)type.GetField("arena",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(geometry);
                var bounds = geometry.GetBounds();
                for (var x = bounds.MinX + .127f; x < bounds.MaxX; x += .7f)
                    for (var z = bounds.MinZ + .213f; z < bounds.MaxZ; z += .7f)
                    {
                        var expected = original.Contains(new(x - original.Center.X, z - original.Center.Z));
                        Check(geometry.Contains(new(x, 3, z)) == expected, "Native arena changed containment.");
                        Check(host.Contains(new(x, 3, z)) == expected, "World conversion changed containment.");
                    }
            }
        });
        foreach (var (boss, action, caster, radius) in new[]
        {
            (0x477Au, 42232u, 0x477Au, 8.25f), (0x477Au, 42233u, 0x477Au, 6f),
            (0x477Cu, 42234u, 0x477Cu, 14.2f), (0x477Cu, 42235u, 0x477Cu, 14.2f),
            (0x477Eu, 42248u, 0x477Eu, 40f), (0x477Eu, 42246u, 0x477Eu, 5f),
        })
            run($"Qarn AID {action} exact dimensions, independent navigation margin and timestamp", () =>
            {
                using var f = new Fight(boss, 1267);
                f.Cast(1, action, 3, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !geometry.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Display radius differs.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Center + Vector3.UnitX * (radius + .4f)), "Missing navigation margin.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "NPC delay differs.");
                if (action != 42233 && action != 42246)
                {
                    var half = MathF.PI / 3;
                    Check(geometry.Contains(f.Center + Direction(MathF.PI / 2 + half - .01f) * 3)
                        && !geometry.Contains(f.Center + Direction(MathF.PI / 2 + half + .01f) * 3), "Cone full angle differs.");
                }
            });
        run("Qarn Rockslide and both Verge rays are forward rectangles with original lengths", () =>
        {
            foreach (var (boss, action, oid, length, halfWidth) in new[]
            {
                (0x477Cu, 42236u, 0x477Cu, 16.2f, 4f),
                (0x477Eu, 42244u, 0x477Fu, 60.6f, 2f),
                (0x477Eu, 42241u, 0x4780u, 60.6f, 2f),
            })
            {
                using var f = new Fight(boss, 1267);
                f.Actors[10] = new(10, oid, f.Center);
                f.Scan();
                f.Cast(10, action, 20, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + new Vector3(length - .01f, 0, halfWidth - .01f))
                    && !geometry.Contains(f.Center - Vector3.UnitX)
                    && !geometry.Contains(f.Center + new Vector3(1, 0, halfWidth + .01f)), "Forward ray dimensions differ.");
            }
        });
        run("Qarn independent long and short Verge rays finish, cancel and repeat without premature removal", () =>
        {
            using var f = new Fight(0x477E, 1267);
            f.Actors[10] = new(10, 0x477F, f.Center);
            f.Actors[11] = new(11, 0x4780, f.Center);
            f.Scan(); f.Cast(10, 42244, 2.2); f.Cast(11, 42241, 20);
            f.Complete(10, 2.2);
            Check(f.Draw.Previews.Count == 1 && f.Engine.Casts.ContainsKey(11), "Short ray removed the 20 second ray.");
            f.Engine.Finish(11, 42241, f.Now);
            f.Actors[11] = f.Actors[11] with { Cast = null };
            f.Render();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Ray cancellation leaked.");
            f.Now = f.Start.AddSeconds(5); f.Cast(10, 42244, 2.2);
            Check(f.Draw.Previews.Count == 1, "Repeated ray was suppressed by old completion.");
        });
        run("Qarn Doom selects visible platform only for current afflicted player and follows platform transitions", () =>
        {
            using var f = new Fight(0x477A, 1267);
            f.Actors[100] = new(100, 0, f.Center, Hostile: false, Statuses: [5187]);
            f.Actors[101] = new(101, 0, f.Center, Hostile: false);
            f.Actors[10] = new(10, 0x1E87E2, f.Center + Vector3.UnitX * 5, Hostile: false, RenderFlags: 0);
            f.Actors[11] = new(11, 0x1E87E3, f.Center - Vector3.UnitX * 5, Hostile: false, RenderFlags: 1);
            f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.DangerAreas.Count == 0, "Platform became damage or did not navigate.");
            var preview = f.Draw.Previews.Single().Value;
            Check(preview.Safe && preview.Geometry.Contains(f.Actors[10].Position + new Vector3(1.74f, 0, 1.74f))
                && !preview.Geometry.Contains(f.Actors[10].Position + Vector3.UnitX * 1.76f), "Platform is not a true 3.5 yard square.");
            f.PlayerId = 101; f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Healthy perspective retained Doom goal.");
            f.PlayerId = 100;
            f.Actors[10] = f.Actors[10] with { RenderFlags = 1 };
            f.Actors[11] = f.Actors[11] with { RenderFlags = 0 }; f.Scan();
            Check(f.Draw.Previews.Single().Value.Geometry.Contains(f.Actors[11].Position), "Newly glowing platform failed to replace the old goal.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 5187, Timestamp = f.Now }, f.Now);
            f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0, "Status loss did not immediately remove stale-snapshot Doom.");
        });
        run("Qarn missing platform flags never invent a safe platform and expired Doom releases the goal", () =>
        {
            using var f = new Fight(0x477A, 1267);
            f.Actors[100] = new(100, 0, f.Center, Hostile: false, Statuses: [5187]);
            f.Actors[10] = new(10, 0x1E87E2, f.Center);
            f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Engine.Hints(f.Center).Any(h => h.Contains("缺少平台状态")), "Old replay without render flags invented a glowing platform.");
            f.Actors[10] = f.Actors[10] with { RenderFlags = 0 }; f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 1, "Platform state recovery failed.");
            f.Actors[100] = f.Actors[100] with { Statuses = [] }; f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0, "Expired Doom retained movement.");
        });
        run("Qarn bee, soulstone and Verge priorities skip dead and untargetable actors", () =>
        {
            foreach (var (boss, oid) in new[] { (0x477Au, 0x477Bu), (0x477Cu, 0x477Du), (0x477Eu, 0x4780u) })
            {
                using var f = new Fight(boss, 1267);
                f.Actors[10] = new(10, oid, f.Center); f.Scan();
                Check(f.Engine.PriorityTarget(1, f.Center) == 10, "Mechanic add priority lost.");
                f.Actors[10] = f.Actors[10] with { Targetable = false }; f.Scan();
                Check(f.Engine.PriorityTarget(10, f.Center) == 1, "Untargetable add retained control.");
            }
        });
        run("Qarn render flags round trip, produce world deltas and preserve unknown legacy data", () =>
        {
            var old = new ReplayWorldFrame { Actors = [new() { Id = 10, RenderFlags = 1 }] };
            var next = old with { Actors = [old.Actors[0] with { RenderFlags = 0 }] };
            var delta = ReplayWorldFrame.Delta(old, next);
            Check(delta.Actors.Single().RenderFlags == 0, "Glow transition was omitted from delta.");
            var decoded = JsonSerializer.Deserialize<ReplayActorState>(JsonSerializer.Serialize(next.Actors[0]))!;
            Check(decoded.RenderFlags == 0, "RenderFlags zero failed to round trip.");
            Check(JsonSerializer.Deserialize<ReplayActorState>("{}")!.RenderFlags == null, "Legacy data was treated as visible.");
        });
        run("Qarn actual solver routes afflicted player into glowing platform without crossing arena wall", () =>
        {
            using var f = new Fight(0x477A, 1267);
            var player = new Vector3(-75, 0, -59);
            var platform = new Vector3(-65, 0, -59);
            f.Actors[100] = new(100, 0, player, Hostile: false, Statuses: [5187]);
            f.Actors[10] = new(10, 0x1E87E2, platform, Hostile: false, RenderFlags: 0);
            f.Scan();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count >= 2,
                $"No native route to cleansing platform: {solution.Status}, points={path.Count}.");
            Check(path.All(p => f.Engine.Arena!.Contains(p.Position)), "Cleansing path crossed arena wall.");
            Check(snapshot.GoalAreas.Single().Geometry.Contains(path[^1].Position), "Route did not reach platform.");
        });
    }

    private static Vector3 Direction(float radians) => new(MathF.Sin(radians), 0, MathF.Cos(radians));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

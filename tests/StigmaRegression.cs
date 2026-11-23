using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class StigmaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Stigma all arenas both views territory ownership WIP absent mechanics not claimed", () =>
        {
            foreach (var boss in new uint[] { 0x3417, 0x3416, 0x3428 })
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position; var world = HostDrawingGeometry.Convert(f.Engine.Arena);
                for (var x = -22.137f; x < 22; x += .31f)
                    for (var z = -22.231f; z < 22; z += .31f)
                    {
                        var expected = boss == 0x3416 ? x * x + z * z <= 400 : Math.Abs(x) <= 20 && Math.Abs(z) <= 20;
                        var p = c + new Vector3(x, 0, z);
                        Check(f.Engine.Arena.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Arena differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 986) && !ArrDungeonCatalog.Owns(boss, 976), "Ownership leaked.");
                var claims = ArrDungeonCatalog.ClaimedActions(f.Engine.Definitions).ToArray();
                Check(!claims.Contains(25385u) && !claims.Contains(25382u) && !claims.Contains(25648u), "Source absent mechanics claimed.");
            }
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x3417, 0x3418, 25383, new BossMod.AOEShapeCircle(3)),
            (0x3417, 0x3417, 25376, new BossMod.AOEShapeCone(60, new(MathF.PI / 2))),
            (0x3417, 0x3417, 25377, new BossMod.AOEShapeCone(60, new(MathF.PI / 2))),
            (0x3416, 0x233C, 25522, new BossMod.AOEShapeRect(40, 3)),
            (0x3416, 0x233C, 25521, new BossMod.AOEShapeRect(41, 4)),
            (0x3416, 0x3416, 25519, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x3428, 0x233C, 25650, new BossMod.AOEShapeDonut(7, 60)),
            (0x3428, 0x233C, 25652, new BossMod.AOEShapeCircle(16)),
            (0x3428, 0x342B, 25646, new BossMod.AOEShapeCone(40, new(MathF.PI / 3))),
            (0x3428, 0x342B, 26873, new BossMod.AOEShapeCircle(7)),
            (0x3428, 0x342A, 25642, new BossMod.AOEShapeCone(60, new(MathF.PI / 2))),
            (0x3428, 0x342A, 25643, new BossMod.AOEShapeCone(60, new(MathF.PI / 2))),
        })
            run($"Stigma {action} source shape double view finish", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position; f.Actors[10] = new(10, oid, c); Scan(f);
                Cast(f, 10, action); var cast = f.Engine.Casts[10];
                Compare(cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null), shape, cast.Location, cast.Rotation);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Cast missing.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cast end leaked.");
            });
        foreach (var (boss, oid, action, halfWidth) in new (uint, uint, uint, float)[]
        { (0x3416, 0x3416, 25516, 4), (0x3416, 0x3416, 25517, 4), (0x3416, 0x233C, 25518, 4), (0x3428, 0x3429, 25645, 2.5f) })
            run($"Stigma charge {action} source quantized origin dynamic length packet correction no duplicate and finish", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position + new Vector3(.123f, 0, .231f);
                f.Actors[10] = new(10, oid, c); Scan(f); var to = c + new Vector3(12, 0, 9);
                Cast(f, 10, action, location: to); var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "stigma/charge/10");
                Compare(aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null), new BossMod.AOEShapeRect(15, halfWidth), aoe.Position, MathF.Atan2(12, 9));
                f.Engine.StartCast(10, f.Actors[10].Cast! with { Location = c + Vector3.UnitX * 20, TargetId = 100 }, f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Corrected charge duplicated.");
                aoe = f.Engine.AmbientAoes().Single(a => a.Id == "stigma/charge/10");
                Compare(aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null), new BossMod.AOEShapeRect(20, halfWidth), aoe.Position, MathF.PI / 2);
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Charge end leaked.");
            });
        foreach (var (status, angle) in new (uint, float)[] { (1958, 0), (1959, MathF.PI), (1960, MathF.PI / 2), (1961, -MathF.PI / 2) })
            run($"Stigma march {status} two seconds speed six point six eight second visibility facing and event removal", () =>
            {
                using var f = Prepare(0x3428); var c = f.Engine.Arena!.Position;
                f.Engine.StatusChanged(100, status, true, f.Now, expiresAt: f.Now.AddSeconds(9)); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Engine.GazeFacing(c, 0) == null, "March visible outside eight seconds.");
                f.Now = f.Now.AddSeconds(1.1); f.Engine.Tick(f.Now); f.Render();
                var offsets = f.Engine.StigmaMarchOffsets(f.Actors[100], 0);
                Check(offsets.Length == 1 && Vector3.Distance(offsets[0], new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 13.2f) < .001,
                    "Source march distance/direction differs.");
                Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 1, "March path or navigation missing.");
                f.Now = f.Start.AddSeconds(8.5); f.Engine.Tick(f.Now);
                var p = c + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 18;
                var heading = f.Engine.GazeFacing(p, 0);
                Check(heading != null && f.Engine.Arena!.Contains(p + new Vector3(MathF.Sin(heading.Value + angle), 0, MathF.Cos(heading.Value + angle)) * 13.2f, 0),
                    "No safe PR facing for impending march.");
                f.Engine.StatusChanged(100, status, false, f.Now); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Engine.GazeFacing(p, 0) == null, "Removed march leaked.");
            });
        run("Stigma march snapshot recovery cumulative steps active duration perspective death reset real solver", () =>
        {
            using var f = Prepare(0x3428); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitZ * 18, Statuses = [1958, 1960],
                StatusExpirations = new Dictionary<uint, DateTime> { [1958] = f.Now.AddSeconds(6), [1960] = f.Now.AddSeconds(7) } }; Scan(f);
            var steps = f.Engine.StigmaMarchOffsets(f.Actors[100], 0);
            Check(steps.Length == 2 && Vector3.Distance(steps[1], new(13.2f, 0, 13.2f)) < .001, "Cumulative offsets differ.");
            var snapshot = f.Snapshot; var march = (StigmaMarchTemplate)f.Engine.AmbientAoes().Single(a => a.Id == "stigma/march/avoid").Shape;
            var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && !march.Unsafe(path[^1].Position),
                "Real solver failed cumulative forced path.");
            f.Engine.StatusChanged(100, 1958, false, f.Now); f.Engine.StatusChanged(100, 1960, false, f.Now);
            f.Engine.StatusChanged(100, 1257, true, f.Now, expiresAt: f.Now.AddSeconds(1.5));
            f.Render(); Check(f.Engine.RequireStigmaMovementHold && f.Engine.StigmaMarchOffsets(f.Actors[100], 1).Length == 1
                && Math.Abs(f.Engine.StigmaMarchOffsets(f.Actors[100], 1)[0].Length() - 9.9f) < .001 && f.Snapshot.DangerAreas.Count == 0,
                "Active march did not use remaining duration/hold or release pending navigation.");
            f.PlayerId = 101; Scan(f); Check(!f.Engine.RequireStigmaMovementHold && f.Draw.Previews.Count == 0, "Perspective leaked march.");
            f.PlayerId = 100; f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(!f.Engine.RequireStigmaMovementHold && f.Draw.Previews.Count == 0, "Death leaked march.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0, "Reset leaked.");
        });
        run("Stigma status events survive stale snapshots until acknowledged and loss cannot resurrect", () =>
        {
            using var f = Prepare(0x3428);
            f.Engine.StatusChanged(100, 1961, true, f.Now, expiresAt: f.Now.AddSeconds(6));
            f.Now = f.Now.AddSeconds(2); Scan(f);
            Check(f.Engine.StigmaMarchOffsets(f.Actors[100], 0).Length == 1, "Old snapshot deleted event march.");
            f.Engine.StatusChanged(100, 1961, false, f.Now);
            f.Actors[100] = f.Actors[100] with { Statuses = [1961],
                StatusExpirations = new Dictionary<uint, DateTime> { [1961] = f.Start.AddSeconds(6) } };
            f.Now = f.Now.AddSeconds(1); Scan(f);
            Check(f.Engine.StigmaMarchOffsets(f.Actors[100], 0).Length == 0, "Old snapshot resurrected removed march.");
            f.Engine.StatusChanged(100, 1257, true, f.Now, expiresAt: f.Now.AddSeconds(2));
            f.Now = f.Now.AddSeconds(1); Scan(f);
            Check(f.Engine.RequireStigmaMovementHold, "Old snapshot released active movement hold.");
        });
        run("Stigma snapshot expiration corrections update active control and remaining distance", () =>
        {
            using var f = Prepare(0x3428);
            f.Actors[100] = f.Actors[100] with { Statuses = [1257],
                StatusExpirations = new Dictionary<uint, DateTime> { [1257] = f.Start.AddSeconds(2) } }; Scan(f);
            f.Now = f.Start.AddSeconds(1);
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1257] = f.Start.AddSeconds(5) } }; Scan(f);
            f.Now = f.Start.AddSeconds(3); Scan(f);
            Check(f.Engine.RequireStigmaMovementHold && Math.Abs(f.Engine.StigmaMarchOffsets(f.Actors[100], 0)[0].Length() - 13.2f) < .001,
                "Snapshot extension ignored.");
            f.Actors[100] = f.Actors[100] with { Statuses = [], StatusExpirations = null }; Scan(f);
            Check(!f.Engine.RequireStigmaMovementHold, "Acknowledged snapshot loss ignored.");
        });
        run("Stigma radial fifteen stopwall immunity stack and source cast damage", () =>
        {
            using var f = Prepare(0x3417); var c = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 10 }; Scan(f);
            Cast(f, 1, 25386, 101);
            var landing = f.Engine.AmbientAoes().Single(a => a.Id == "stigma/knockback/1/landing").Position;
            Check(Vector3.Distance(landing, c + Vector3.UnitX * 19.999f) < .002, "Radial wall clamp differs.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.Contains("/knockback/")), "Immune projection leaked.");
            f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stack/knockback leaked.");
            Cast(f, 1, 25387, 100); Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Buster missing.");
            f.Engine.Finish(1); Check(!f.Engine.PredictedDamage.Any(), "Buster leaked.");
        });
    }
    private static void Compare(IGeometryShape geometry, BossMod.AOEShape source, Vector3 origin, float rotation)
    {
        var world = HostDrawingGeometry.Convert(geometry);
        for (var x = -62.137f; x < 62; x += .81f)
            for (var z = -62.231f; z < 62; z += .81f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(geometry.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source geometry differs.");
            }
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 986); f.Engine.ResetFight(); var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true); f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, float rotation = .7f, Vector3? location = null)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

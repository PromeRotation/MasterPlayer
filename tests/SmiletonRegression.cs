using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class SmiletonRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Smileton all source arenas native ownership exact two views", () =>
        {
            foreach (var boss in new uint[] { 0x34CF, 0x34D1, 0x34D3 })
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                var world = HostDrawingGeometry.Convert(f.Engine.Arena!);
                for (var x = -22.137f; x < 22; x += .31f)
                    for (var z = -22.231f; z < 22; z += .31f)
                    {
                        var expected = boss switch { 0x34CF => Math.Abs(x) <= 20 && Math.Abs(z) <= 20,
                            0x34D1 => x * x + z * z <= 400, _ => Math.Abs(x) <= 14.5 && Math.Abs(z) <= 10 };
                        var p = c + new Vector3(x, 0, z);
                        Check(f.Engine.Arena.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Arena differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 976) && !ArrDungeonCatalog.Owns(boss, 973), "Ownership leaked.");
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.smileton"), "Entry missing.");
        });
        foreach (var (boss, oid, action, source) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x34D1, 0x34D1, 26438, new BossMod.AOEShapeCircle(15)),
            (0x34D1, 0x34E9, 26439, new BossMod.AOEShapeCircle(15)),
            (0x34D1, 0x233C, 26441, new BossMod.AOEShapeCone(40, new(MathF.PI / 8))),
            (0x34D3, 0x38CF, 27698, new BossMod.AOEShapeCircle(8)),
            (0x34D3, 0x3585, 26445, new BossMod.AOEShapeCircle(16)),
            (0x34D3, 0x34D3, 26447, new BossMod.AOEShapeRect(30, 5)),
            (0x34D3, 0x34D3, 26448, new BossMod.AOEShapeRect(30, 5)),
        })
            run($"Smileton {action} source shape double view finish and duplicate packet", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position; f.Actors[10] = new(10, oid, c); Scan(f);
                Cast(f, 10, action); f.Engine.StartCast(10, f.Actors[10].Cast!, f.Now); f.Render();
                var cast = f.Engine.Casts[10]; var g = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null); var world = HostDrawingGeometry.Convert(g);
                for (var x = -42.137f; x < 42; x += .71f)
                    for (var z = -42.231f; z < 42; z += .71f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = source.Check(new(p.X, p.Z), new(c.X, c.Z), new(cast.Rotation));
                        Check(g.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Shape differs.");
                    }
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Duplicate packet duplicated output.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "End leaked.");
            });
        run("Smileton Face directional eight yards all rotations wall clamp sequential immunity and release", () =>
        {
            using var f = Prepare(0x34CF); var c = f.Engine.Arena!.Position;
            for (var i = 0; i < 16; ++i)
            {
                var rotation = i * MathF.PI / 8;
                var p = DungeonEncounter.DungeonSquareKnockbackLanding(f.Engine.Arena, c, rotation, .5f);
                Check(Math.Abs(Vector3.Distance(c, p) - 8) < .001, "Center distance differs.");
                var edge = c + new Vector3(19, 0, 19);
                var actual = DungeonEncounter.DungeonSquareKnockbackLanding(f.Engine.Arena, edge, rotation, .5f);
                var dir = new BossMod.WDir(MathF.Sin(rotation), MathF.Cos(rotation));
                var ray = new BossMod.ArenaBoundsSquare(20).IntersectRay(new(19, 19), dir);
                var expected = edge + new Vector3(dir.X, 0, dir.Z) * Math.Min(8, ray - .001f);
                Check(Vector3.Distance(actual, expected) < .001f, "Source wall ray differs.");
            }
            f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x233C, c);
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitZ * 18 }; Scan(f);
            Cast(f, 10, 27742, rotation: 0); Cast(f, 11, 27742, rotation: MathF.PI / 2);
            var landing = f.Engine.AmbientAoes().Single(a => a.Id == "smileton/knockback/11/landing");
            Check(Vector3.Distance(landing.Position, c + new Vector3(8, 0, 19.999f)) < .002, "Sequential landings differ.");
            Check(f.Snapshot.DangerAreas.Count == 0, "Solid wall knockback invented a forbidden zone.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; Scan(f);
            Check(f.Draw.Previews.Count == 0, "Immune still projected.");
            f.Engine.Finish(10); f.Engine.Finish(11); f.Render(); Check(f.Draw.Previews.Count == 0, "Knockback leaked.");
        });
        run("Smileton source stack R6 moving target half radius goal and real solver", () =>
        {
            using var f = Prepare(0x34D3); Cast(f, 1, 26451, 100);
            Check(f.Engine.Casts[1].Rule.PartyAoe == "分摊", "Stack missing.");
            var p = f.Actors[101].Position;
            var solution = new SafeZoneSolver().Solve(f.Snapshot, new(f.Actors[100].Position, 0, 6, false, false, 0, true, f.Snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0
                && Vector3.Distance(path[^1].Position, p) <= 3.01f, "Real solver failed target half-radius stack.");
            f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stack leaked.");
        });
        run("Smileton source two printed leaps independent finish and repeated workers", () =>
        {
            using var f = Prepare(0x34D1); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x34E9, c); f.Actors[11] = new(11, 0x34E9, c); Scan(f);
            Cast(f, 10, 26439); Cast(f, 11, 26439); Check(f.Draw.Previews.Count == 2, "Worker wave missing.");
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 1, "One worker finish erased another.");
            f.Actors[10] = f.Actors[10] with { Cast = null }; f.Now = f.Now.AddSeconds(8); Scan(f);
            Cast(f, 10, 26439); Check(f.Draw.Previews.Count >= 1 && f.Engine.Casts.ContainsKey(10), "Repeated worker missing.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0, "Reset leaked.");
        });
        run("Smileton source raidwide buster predictions and WIP Face absent mechanics stay unsuppressed", () =>
        {
            foreach (var (boss, action) in new (uint, uint)[] { (0x34CF, 26435), (0x34D1, 26437), (0x34D3, 26449) })
            {
                using var f = Prepare(boss); Cast(f, 1, action, 100);
                Check(f.Engine.PredictedDamage.Any(), "Source damage prediction missing.");
                f.Engine.Finish(1); Check(!f.Engine.PredictedDamage.Any(), "Source cast prediction leaked.");
            }
            using var face = Prepare(0x34CF);
            var claimed = ArrDungeonCatalog.ClaimedActions(face.Engine.Definitions).ToArray();
            Check(!claimed.Contains(26422u) && !claimed.Contains(26423u)
                && !claimed.Contains(26424u), "Missing source WIP face lanes were suppressed.");
        });
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 976); f.Engine.ResetFight(); var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true); f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, float rotation = .7f)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

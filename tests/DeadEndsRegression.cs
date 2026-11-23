using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class DeadEndsRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("DeadEnds four exact source arenas and two views", () =>
        {
            foreach (var (boss, type, field, small) in new (uint, string, string, bool)[]
            {
                (0x34C4, "D061CausticGrebuloff.D061CausticGrebuloff", "defaultBounds", false),
                (0x34C6, "D062Peacekeeper.D062Peacekeeper", "StartingBounds", false),
                (0x34C6, "D062Peacekeeper.D062Peacekeeper", "SmallerBounds", true),
                (0x34C7, "D063Rala.D063Rala", "defaultBounds", false),
            })
            {
                using var f = Prepare(boss); if (small) f.Engine.MapEffect(0x17, 0x00020001);
                var source = (BossMod.ArenaBoundsCustom)Source(type).GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var arena = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(arena);
                for (var x = -22.137f; x < 22; x += .31f)
                    for (var z = -22.231f; z < 22; z += .31f)
                    {
                        var p = arena.Position + new Vector3(x, 0, z);
                        Check(arena.Contains(p, 0) == source.Contains(new(x, z)) && world.Contains(p, 0) == arena.Contains(p, 0), "Arena differs.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.dead-ends") && new uint[] { 0x34C4, 0x34C6, 0x34C7 }
                .All(o => ArrDungeonCatalog.Owns(o, 973) && !ArrDungeonCatalog.Owns(o, 978)), "Ownership leaked.");
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x34C4, 0x233C, 25918, new BossMod.AOEShapeCircle(6)),
            (0x34C6, 0x233C, 25927, new BossMod.AOEShapeCircle(6)),
            (0x34C6, 0x233C, 25934, new BossMod.AOEShapeCircle(10)),
            (0x34C6, 0x384B, 28352, new BossMod.AOEShapeRect(20, 2)),
            (0x34C7, 0x233C, 25939, new BossMod.AOEShapeCircle(15)),
            (0x34C7, 0x233C, 25951, new BossMod.AOEShapeRect(40, 2)),
            (0x34C7, 0x34C7, 25940, new BossMod.AOEShapeRect(50, 5)),
            (0x34C7, 0x34C7, 25943, new BossMod.AOEShapeCone(45, new(MathF.PI / 2))),
            (0x34C7, 0x34C7, 25944, new BossMod.AOEShapeCone(45, new(MathF.PI / 2))),
        })
            run($"DeadEnds {action} source shape both views finish", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position; f.Actors[10] = new(10, oid, c); Scan(f); Cast(f, 10, action);
                var cast = f.Engine.Casts[10]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Cast missing.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Finish leaked.");
            });
        run("DeadEnds both wind explosion count all special lanes and fallback equal source", () =>
        {
            var type = Source("D061CausticGrebuloff.NecroticFluidMist"); var c = new Vector3(266.5f, 0, -178);
            foreach (var wind in new[] { 1, 2 })
                foreach (var z in new[] { -184.5f, -187.5f, -191.4f, -194.5f, -196.9f, -171.5f, -168.5f, -164.6f, -161.5f, -159.1f, -178, -180, -176 })
                    foreach (var x in new[] { 266.5f, 280f })
                    {
                        var p = new Vector3(x, 0, z);
                        var method = type.GetMethod(wind == 1 ? "GetSouthwardExplosions" : "GetNorthwardExplosions", BindingFlags.NonPublic | BindingFlags.Static)!;
                        Check(DeadEndsCatalog.TERRITORY == 973 && DungeonEncounter.DeadEndsExplosionCount(p, c, wind)
                            == (int)method.Invoke(null, [new BossMod.WPos(x, z), new BossMod.WPos(c.X, c.Z)])!, "Explosion count differs.");
                    }
        });
        foreach (var wind in new[] { 1, 2 })
            run($"DeadEnds wind {wind} exaflare five per line true finish location correction clock and all steps", () =>
            {
                using var f = Prepare(0x34C4);
                f.Engine.MapEffect(0x2B, wind == 1 ? 0x00020080u : 0x00200040u);
                var p = new Vector3(266.5f, 0, wind == 1 ? -196.9f : -159.1f);
                f.Actors[10] = new(10, 0x34C5, p); Scan(f); Cast(f, 10, 25919);
                var aoes = Exa(f); Check(aoes.Length == 5 && aoes[0].Activation == f.Engine.Casts[10].FinishAt
                    && aoes[4].Position.Z == p.Z + (wind == 1 ? 24 : -24), "Initial exa extent differs.");
                f.Engine.StartCast(10, f.Actors[10].Cast! with { Location = p + Vector3.UnitX * .1f, Rotation = 1 }, f.Now);
                var originalFuture = aoes[1].Activation; f.Now = f.Start.AddSeconds(9); f.Engine.Tick(f.Now); f.Render();
                Check(Exa(f)[1].Activation == originalFuture, "Timer autonomously advanced exa.");
                f.Now = f.Start.AddSeconds(9.2);
                for (var step = 0; step < 7; ++step)
                {
                    var next = Exa(f)[0].Position + Vector3.UnitX * .1f;
                    if (step == 0)
                    {
                        f.Engine.Finish(10, endedAt: f.Now);
                        f.Actors[10] = f.Actors[10] with { Cast = null };
                    }
                    else
                    {
                        f.Actors[20] = new(20, 0x233C, next); Scan(f); Cast(f, 20, 28348);
                        f.Engine.Finish(20, endedAt: f.Now); f.Actors[20] = f.Actors[20] with { Cast = null };
                    }
                    f.Render();
                    if (step < 6)
                    {
                        var current = Exa(f);
                        Check(current.Length == Math.Min(6 - step, 5) && Math.Abs(current[0].Position.X - next.X) < .001
                            && current[0].Activation == f.Now.AddSeconds(2), "True finish did not correct exa location/time.");
                    }
                    f.Now = f.Now.AddSeconds(2);
                }
                Check(Exa(f).Length == 0 && f.Draw.Previews.Count == 0, "Final exa leaked.");
            });
        foreach (var wind in new[] { 1, 2 })
            run($"DeadEnds wind {wind} nausea source safe difference two views exa navigation suppression and real solver", () =>
            {
                using var f = Prepare(0x34C4); var c = new Vector3(266.5f, 0, -178);
                f.Actors[1] = f.Actors[1] with { Position = c }; f.Actors[10] = new(10, 0x34C5, c + Vector3.UnitZ * (wind == 1 ? -18.9f : 18.9f)); Scan(f);
                f.Engine.MapEffect(0x2B, wind == 1 ? 0x00020080u : 0x00200040u);
                Cast(f, 10, 25919); Cast(f, 1, 28347);
                Check(Exa(f).All(a => !a.Navigate) && f.Draw.Previews.Count == 7, "Wave hid exa display or failed navigation suppression.");
                var safe = f.Engine.AmbientAoes().Single(a => a.Id == "deadends/wave/safe");
                var source = new BossMod.AOEShapeCustom([new BossMod.ConeHA(new(c.X, c.Z), 6,
                    new(wind == 1 ? MathF.PI : 0), new(MathF.PI / 2))],
                    [new BossMod.Circle(new(271.473f, -178.027f), 6), new BossMod.Circle(new(261.494f, -178.027f), 6)]);
                var geometry = safe.Shape.Resolve(c, 0, null); var world = HostDrawingGeometry.Convert(geometry);
                for (var x = -7.137f; x < 7; x += .13f)
                    for (var z = -7.231f; z < 7; z += .13f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = source.Check(new(p.X, p.Z), new(0, 0), default);
                        Check(geometry.Contains(p, 0) == expected && world.Contains(p, 0) == expected,
                            $"Wave safe difference differs at {p}, source={expected}, native={geometry.Contains(p, 0)}, world={world.Contains(p, 0)}.");
                    }
                f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 10 }; Scan(f); Solve(f, p => geometry.Contains(p, .01f));
                f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null }; Scan(f);
                Check(Exa(f).All(a => a.Navigate) && f.Draw.Previews.Count == 5, "Wave finish failed to restore exa navigation.");
            });
        run("DeadEnds companionship first status clock persists until all statuses gone and cleanse jobs death range", () =>
        {
            using var f = Prepare(0x34C4); var c = f.Engine.Arena!.Position;
            f.Engine.StatusChanged(101, 2966, true, f.Start.AddSeconds(-1)); f.Engine.StatusChanged(100, 2966, true, f.Now);
            f.Render(); var enter = f.Engine.AmbientAoes().Single(a => a.Id == "deadends/solitude/enter");
            Check(enter.Activation == f.Start.AddSeconds(4), "First companionship clock changed.");
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= 1.51f);
            f.Engine.StatusChanged(101, 2966, false, f.Now); Scan(f); Check(f.Draw.Previews.Count == 1, "First loss erased active stack.");
            f.Engine.StatusChanged(100, 2966, false, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Last loss failed to clear stack.");
            f.Engine.StatusChanged(100, 2966, true, f.Now); f.Render();
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= .76f);
            f.Engine.StatusChanged(100, 2966, false, f.Now);
            f.Engine.StatusChanged(101, 2965, true, f.Now);
            Check(f.Engine.DeadEndsCleanseTarget(24)?.Id == 101 && f.Engine.DeadEndsCleanseTarget(23)?.Id == 101
                && f.Engine.DeadEndsCleanseTarget(19) == null, "Cleansable jobs differ.");
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 40 }; Scan(f);
            Check(f.Engine.DeadEndsCleanseTarget(24) == null, "Out of range cleanse requested.");
        });
        run("DeadEnds Peacekeeper shrink ring original clock persists until map", () =>
        {
            using var f = Prepare(0x34C6); Cast(f, 1, 25936);
            var ring = f.Engine.AmbientAoes().Single(); Check(ring.Activation == f.Engine.Casts[1].FinishAt.AddSeconds(.4), "Shrink clock differs.");
            f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 1, "Ring ended before map.");
            f.Engine.MapEffect(0x17, 0x00020001); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Engine.Arena!.GetBounds().MaxX < -88, "Shrink not applied.");
        });
        run("DeadEnds Peacekeeper void cast target follows event snapshot point seven real objects consume first state seven gates all", () =>
        {
            using var f = Prepare(0x34C6); var c = f.Engine.Arena!.Position;
            Cast(f, 1, 28360, 100); f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 5 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single().Position == f.Actors[100].Position, "Cast target did not follow.");
            f.Engine.Effect(1, 28360, 1, f.Now, 100, occurredAt: f.Start.AddSeconds(-.2)); f.Render();
            Check(f.Engine.AmbientAoes().Count() == 2 && Math.Abs((f.Engine.AmbientAoes().Single(a => a.Id.Contains("/predict/")).Activation!.Value
                - f.Start.AddSeconds(.5)).TotalMilliseconds) < .001, "Event prediction replaced active cast or wrong delay.");
            f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null };
            f.Actors[20] = new(20, 0x1EB5F7, c + Vector3.UnitX * 5, EventState: 7);
            f.Actors[21] = new(21, 0x1EB5F7, c + Vector3.UnitX * 12); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().Any(a => a.Id.Contains("/predict/")), "First state seven did not gate full list.");
            f.Actors[20] = f.Actors[20] with { EventState = 0 }; f.Actors[21] = f.Actors[21] with { EventState = 7 }; Scan(f);
            Check(f.Draw.Previews.Count == 2 && !f.Engine.AmbientAoes().Any(a => a.Id.Contains("/predict/")), "Real zone failed prediction consumption or filtered later state seven.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f); Check(f.Draw.Previews.Count == 0, "First state seven failed to hide all zones.");
        });
        run("DeadEnds Peacekeeper elimination dynamic source target effect only and native width ten", () =>
        {
            using var f = Prepare(0x34C6); var c = f.Engine.Arena!.Position;
            Cast(f, 1, 25935, 100); f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null };
            f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 8 }; Scan(f);
            var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "deadends/elimination");
            Check(aoe.OutlineOnly && !aoe.Navigate && Math.Abs(aoe.Rotation + MathF.PI / 2) < .001 && f.Engine.PredictedDamage.Count() == 1, "Elimination follow/finish differs.");
            Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeRect(46, 5));
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Single(a => a.Id == "deadends/elimination").Navigate, "Other perspective lacks elimination danger.");
            f.Engine.Effect(1, 25935, 1, f.Now, 100); f.Render(); Check(f.Draw.Previews.Count == 0 && !f.Engine.PredictedDamage.Any(), "Elimination real effect leaked.");
        });
        run("DeadEnds Peacekeeper knockback center four fire direction sectors eleven landing and immunity display", () =>
        {
            using var f = Prepare(0x34C6); var c = f.Engine.Arena!.Position; f.Engine.MapEffect(0x17, 0x00020001);
            f.Actors[10] = new(10, 0x233C, c + Vector3.UnitZ * 12); f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 10 }; Scan(f);
            Cast(f, 10, 25934); Cast(f, 1, 25931);
            Solve(f, p => Vector3.Distance(p, c) <= 4.01f && !new FanGeometry(c, 0, 16, MathF.PI * .4f).Contains(p, 0));
            var preview = f.Engine.AmbientAoes().Single(a => a.Id == "deadends/knockback/landing");
            Check(Vector3.Distance(preview.Position, c + Vector3.UnitX * 21) < .001, "Knockback was not eleven.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(20) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "deadends/knockback/landing")
                && f.Engine.AmbientAoes().Any(a => a.Id == "deadends/knockback/center"), "Immunity display or source center constraint differs.");
        });
        run("DeadEnds Rala first three circles promote delayed buster waits true event and doom role hints", () =>
        {
            using var f = Prepare(0x34C7); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 15; ++i) f.Actors[i] = new(i, 0x233C, c); Scan(f);
            for (ulong i = 10; i < 15; ++i) Cast(f, i, 25939);
            Check(f.Draw.Previews.Count == 3 && f.Snapshot.DangerAreas.Count == 3, "Rala cap differs.");
            f.Engine.Finish(10); f.Render(); Check(f.Engine.AmbientAoes().Any(a => a.Id == "deadends/light/13"), "Rala circle did not promote.");
            Cast(f, 1, 25949, 100); f.Now = f.Now.AddSeconds(9); f.Engine.Tick(f.Now);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Timeout removed delayed buster.");
            f.Engine.Finish(1, completed: true); Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Rala delayed buster lost.");
            f.Engine.Effect(1, 25949, 1, f.Now, 100); Check(!f.Engine.PredictedDamage.Any(), "Rala buster true effect leaked.");
            f.Engine.StatusChanged(101, 1769, true, f.Now); f.Actors[100] = f.Actors[100] with { ClassJob = 24 }; Scan(f);
            Check(f.Engine.Hints(c).Any(h => h.Contains("恢复至满血")), "Healer doom hint missing.");
            f.Engine.StatusChanged(101, 1769, false, f.Now); Scan(f); Check(!f.Engine.Hints(c).Any(h => h.Contains("恢复至满血")), "Removed doom hint leaked.");
        });
    }

    private static DungeonAmbientAoe[] Exa(Fight f) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("deadends/exa/")).ToArray();
    private static Type Source(string type) => typeof(BossMod.WorldState).Assembly.GetType("BossMod.Endwalker.Dungeon.D06DeadEnds." + type)!;
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 973); f.Engine.ResetFight(); var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true); f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, float rotation = .7f)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -52.137f; x < 52; x += .91f)
            for (var z = -52.231f; z < 52; z += .91f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source shape differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot; var player = f.Actors[f.PlayerId].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && accept(path.Count > 0 ? path[^1].Position : player),
            $"Real solver failed: {solution.Status}, fallback={solution.IsLeastDangerFallback}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

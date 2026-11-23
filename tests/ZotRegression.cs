using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class ZotRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Zot exact source arenas radar world and ownership", () =>
        {
            foreach (var (oid, path) in new[]
            {
                (0x33EEu, "D01TowerOfZot.D011Minduruva.D011Minduruva"),
                (0x33EFu, "D01TheTowerOifZot.D012Sanduruva.D012Sanduruva"),
                (0x33F1u, "D01TheTowerOfZot.D013MagusSisters.D013MagusSisters"),
            })
            {
                using var f = Prepare(oid);
                var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Endwalker.Dungeon." + path)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var arena = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(arena);
                for (var x = -22.137f; x < 22; x += .23f)
                    for (var z = -22.231f; z < 22; z += .23f)
                    {
                        var p = arena.Position + new Vector3(x, 0, z);
                        Check(arena.Contains(p, 0) == source.Contains(new(x, z)) && world.Contains(p, 0) == arena.Contains(p, 0), "Arena differs.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.zot")
                && new uint[] { 0x33EE, 0x33EF, 0x33F1, 0x33F2, 0x33F3 }.All(o => ArrDungeonCatalog.Owns(o, 952) && !ArrDungeonCatalog.Owns(o, 938)), "Ownership differs.");
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x33EE, 0x233C, 25239, new BossMod.AOEShapeCircle(3)),
            (0x33EE, 0x233C, 25240, new BossMod.AOEShapeCone(40.5f, new(MathF.PI / 2))),
            (0x33EE, 0x233C, 25238, new BossMod.AOEShapeCone(40.5f, new(MathF.PI / 18))),
            (0x33EE, 0x233C, 25237, new BossMod.AOEShapeDonut(5, 60)),
            (0x33EF, 0x33EF, 25256, new BossMod.AOEShapeRect(40, 2)),
            (0x33EF, 0x33EF, 25251, new BossMod.AOEShapeCircle(5)),
            (0x33F1, 0x233C, 25269, new BossMod.AOEShapeCircle(3)),
            (0x33F1, 0x233C, 25270, new BossMod.AOEShapeCircle(5)),
            (0x33F1, 0x233C, 25271, new BossMod.AOEShapeRect(40, 5)),
            (0x33F1, 0x233C, 25266, new BossMod.AOEShapeCone(40.5f, new(MathF.PI / 18))),
            (0x33F1, 0x233C, 25267, new BossMod.AOEShapeRect(44, 2)),
            (0x33F1, 0x233C, 25268, new BossMod.AOEShapeCircle(15)),
            (0x33F1, 0x233C, 25263, new BossMod.AOEShapeDonut(5, 40)),
            (0x33F1, 0x233C, 25264, new BossMod.AOEShapeRect(44, 5)),
            (0x33F1, 0x33F2, 25275, new BossMod.AOEShapeRect(40, 2)),
            (0x33F1, 0x33F2, 25278, new BossMod.AOEShapeCircle(5)),
            (0x33F1, 0x3610, 25286, new BossMod.AOEShapeCone(40.5f, new(MathF.PI / 18))),
        })
            run($"Zot {action} exact source shape both views original activation margin and cancel", () =>
            {
                using var f = Prepare(boss);
                var id = oid == boss ? 1UL : 10UL;
                f.Actors[id] = new(id, oid, f.Actors[1].Position, InCombat: true); Scan(f);
                Cast(f, id, action, target: 100); var cast = f.Engine.Casts[id];
                var geometry = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                var world = HostDrawingGeometry.Convert(geometry);
                for (var x = -62.137f; x < 62; x += .71f)
                    for (var z = -62.231f; z < 62; z += .71f)
                    {
                        var p = cast.Location + new Vector3(x, 0, z);
                        var expected = shape.Check(new(p.X, p.Z), new(cast.Location.X, cast.Location.Z), new(cast.Rotation));
                        Check(geometry.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "AOE differs.");
                    }
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1 && f.Safe.NavigationOnly.Count == 1, "Missing or duplicate cast output.");
                f.Engine.Finish(id); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled AOE leaked.");
            });
        run("Zot spheres first and later waves strict seven second promotion original birth no resurrection", () =>
        {
            using var f = Prepare(0x33EF); var center = f.Actors[1].Position;
            for (var wave = 0; wave < 2; ++wave)
            {
                var id = (ulong)(20 + wave);
                f.Engine.ObjectCreated(id, f.Now);
                f.Actors[id] = new(id, 0x33F0, center + new Vector3(.123f, 0, .234f)); Scan(f);
                var aoe = f.Engine.AmbientAoes().Single();
                var expected = f.Now.AddSeconds(wave == 0 ? 10.8 : 20);
                Check(Math.Abs((aoe.Activation!.Value - expected).TotalMilliseconds) < .001 && !aoe.Navigate
                    && f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 0, "Initial sphere should display only.");
                f.Now = aoe.Activation.Value.AddSeconds(-7); f.Engine.Tick(f.Now); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 0, "Strict threshold promoted at equality.");
                f.Now = f.Now.AddMilliseconds(1); f.Engine.Tick(f.Now); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 1 && f.Engine.AmbientAoes().Single().Activation == aoe.Activation, "Promotion changed hit time.");
                Solve(f, p => Vector3.Distance(p, aoe.Position) >= 15);
                Cast(f, id, 25252); f.Engine.Finish(id); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Sphere cancellation did not clear wave.");
                f.Actors[id] = f.Actors[id] with { Cast = null }; Scan(f);
                Check(!f.Engine.AmbientAoes().Any(), "Old sphere snapshot resurrected cleared wave.");
                f.Now = f.Now.AddSeconds(25);
            }
        });
        run("Zot sisters spheres immediate risk source destruction finishes entire wave", () =>
        {
            using var f = Prepare(0x33F1); var c = f.Actors[1].Position;
            f.Engine.ObjectCreated(20, f.Start.AddSeconds(-1));
            f.Actors[20] = new(20, 0x33F0, c); f.Actors[21] = new(21, 0x33F0, c + Vector3.UnitX); Scan(f);
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2
                && Math.Abs((f.Engine.AmbientAoes().First().Activation!.Value - f.Start.AddSeconds(6.3)).TotalMilliseconds) < .001, "Sisters sphere clock/risk differs.");
            Cast(f, 20, 25279); f.Engine.RemoveActor(20, f.Now); f.Actors.Remove(20); f.Render();
            Check(!f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0, "Destroyed caster did not finish wave.");
            Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Remaining object resurrected wave.");
        });
        foreach (var boss in new uint[] { 0x33EE, 0x33F1 })
            run($"Zot Dhrupad {boss} three real hits duplicates and reset", () =>
            {
                using var f = Prepare(boss); var caster = boss == 0x33EE ? 1UL : 3UL;
                Cast(f, caster, boss == 0x33EE ? 25244u : 25281u); f.Engine.Finish(caster, completed: true);
                var hits = boss == 0x33EE ? new uint[] { 25245, 25246, 25247 } : [25282, 25283, 25284];
                for (var i = 0; i < 3; ++i)
                {
                    Check(f.Engine.Hints(f.Actors[100].Position).Contains("三次单体伤害及持续伤害"), "Hint cleared before third hit.");
                    f.Engine.Effect(caster, hits[i], (uint)i, f.Now); f.Engine.Effect(caster, hits[i], (uint)i, f.Now);
                }
                Check(!f.Engine.Hints(f.Actors[100].Position).Contains("三次单体伤害及持续伤害"), "Third hit leaked hint.");
            });
        run("Zot sisters primary death destruction handoff target order and all dead cleanup", () =>
        {
            using var f = Prepare(0x33F1);
            Check(f.Engine.PriorityTarget(2, Vector3.Zero) == 1, "Cinduruva must be first.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x33F1 && f.Engine.PrimaryId != 1
                && f.Engine.PriorityTarget(2, Vector3.Zero) == 3, "Primary death lost sisters or priority.");
            f.Engine.RemoveActor(3); f.Actors.Remove(3); Scan(f);
            Check(f.Engine.Active != null && f.Engine.PrimaryId == 2, "Secondary removal ended fight.");
            f.Actors[2] = f.Actors[2] with { Dead = true }; Scan(f);
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "All-dead cleanup failed.");
        });
        run("Zot poison status event healer bard range death removal and no false stop", () =>
        {
            using var f = Prepare(0x33EE);
            f.Engine.StatusChanged(101, 18, true, f.Now);
            Check(f.Engine.ZotCleanseTarget(24)?.Id == 101 && f.Engine.ZotCleanseTarget(23)?.Id == 101
                && f.Engine.ZotCleanseTarget(19) == null, "Cleanse job/target differs.");
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position + Vector3.UnitX * 40 }; Scan(f);
            Check(f.Engine.ZotCleanseTarget(24) == null, "Out-of-range cleanse.");
            f.Engine.StatusChanged(101, 18, false, f.Now);
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position }; Scan(f);
            Check(f.Engine.ZotCleanseTarget(24) == null, "Removed poison retained cleanse.");
            Check(f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.Gazes.Any(), "Nonexistent shield/gaze added.");
        });
        foreach (var boss in new uint[] { 0x33EE, 0x33F1 })
            run($"Zot Bio {boss} tankbuster prediction and poison cast hint", () =>
            {
                using var f = Prepare(boss);
                Cast(f, boss == 0x33EE ? 1UL : 3UL, boss == 0x33EE ? 25248u : 25290u, 100);
                Check(f.Engine.PredictedDamage.Single().TargetId == 100
                    && f.Engine.Hints(f.Actors[100].Position).Contains("死刑后中毒：可驱散"), "Bio damage prediction missing.");
            });
        foreach (var own in new[] { false, true })
            run($"Zot stack perspective {own} real solver gathering spread separation and cleanup", () =>
            {
                using var f = Prepare(0x33F1); var c = f.Actors[1].Position;
                f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 10 };
                f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 5 };
                f.Actors[10] = new(10, 0x3610, c); Scan(f);
                Cast(f, 10, 25272, own ? 100UL : 101UL);
                Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= (own ? 2.5f : 5));
                Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.Safe, "Stack display differs.");
                f.Engine.Finish(10); f.Render();
                Cast(f, 10, 25265, 100);
                Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("party/separate/")) && f.Draw.Previews.Count == 1, "Own spread does not avoid others.");
                f.Engine.Finish(10); f.Render();
                Check(f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Party AOE leaked.");
            });
        run("Zot pointless sphere is excluded from targeting without global action prohibition", () =>
        {
            using var f = Prepare(0x33EF); f.Actors[20] = new(20, 0x33F0, f.Actors[100].Position); Scan(f);
            Check(f.Engine.PriorityTarget(20, f.Actors[100].Position) == 1 && f.Engine.ForbiddenTargets.Length == 0, "Pointless sphere became target or global stop.");
            Cast(f, 1, 25255); Check(f.Engine.Hints(f.Actors[100].Position).Any(), "Stop cast lost hint.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 952); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true);
        if (boss == 0x33F1)
        {
            f.Actors[2] = new(2, 0x33F2, c, InCombat: true);
            f.Actors[3] = new(3, 0x33F3, c, InCombat: true);
        }
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }

    private static void Scan(Fight f)
    {
        f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render();
    }

    private static void Cast(Fight f, ulong source, uint action, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, .7f, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast };
        f.Engine.StartCast(source, cast, f.Now); f.Render();
    }

    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot; var player = f.Actors[f.PlayerId].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && accept(path.Count > 0 ? path[^1].Position : player),
            $"Real solver failed: {solution.Status}, from {player} to {(path.Count > 0 ? path[^1].Position : player)}.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

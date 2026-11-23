using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;
using Berserker = BossMod.Shadowbringers.Dungeon.D11HeroesGauntlet.D113SpectralBerserker.D113SpectralBerserker;

internal static class HeroesRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Heroes source necromancer cross inward landing and collapse polygons both views ownership", () =>
        {
            var necro = (BossMod.ArenaBoundsCustom)typeof(BossMod.BossModule).Assembly.GetType("BossMod.Shadowbringers.Dungeon.D11HeroesGauntlet.D112SpectralNecromancer.D112SpectralNecromancer")!
                .GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            foreach (var (shape, center, contains) in new (IGeometryShape, Vector2, Func<BossMod.WDir, bool>)[]
            {
                (HeroesArenas.Necromancer(0), new(necro.Center.X, necro.Center.Z), p => necro.Contains(p)),
                (HeroesArenas.Berserker(0), new(750, 482), p => Berserker.DefaultBounds.Contains(p)),
                (HeroesArenas.BerserkerLanding(0), new(750, 482), p => Berserker.DefaultBounds.Polygon.Offset(-1).Contains(p)),
                (HeroesArenas.BerserkerCollapse(0), new(750, 482), p => Math.Abs(p.X) <= 23 && Math.Abs(p.Z) <= 23 && !Berserker.DefaultBounds.Contains(p)),
            })
            {
                var world = HostDrawingGeometry.Convert(shape);
                for (var x = -24.137f; x < 24; x += .31f)
                    for (var z = -24.231f; z < 24; z += .31f)
                    {
                        var p = new Vector3(center.X + x, 0, center.Y + z); var expected = contains(new(x, z));
                        Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source polygon mismatch.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.heroes") && new uint[] { 0x2DEC, 0x2DF1, 0x2EFD }.All(o => ArrDungeonCatalog.Owns(o, 916) && !ArrDungeonCatalog.Owns(o, 898)), "Ownership mismatch.");
        });
        foreach (var (boss, oid, action, source) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x2DEC, 0x2E71, 20439, new BossMod.AOEShapeRect(60, 1)),
            (0x2DF1, 0x2DF1, 20321, new BossMod.AOEShapeCone(40, new(MathF.PI / 3))),
            (0x2DF1, 0x233C, 20317, new BossMod.AOEShapeCircle(5)),
            (0x2EFD, 0x233C, 20995, new BossMod.AOEShapeCircle(8)),
            (0x2EFD, 0x2EFD, 21002, new BossMod.AOEShapeRect(50, 3)),
            (0x2EFD, 0x2EFD, 21003, new BossMod.AOEShapeRect(50, 3)),
        })
            run($"Heroes cast {action} exact shape both views cancel", () =>
            {
                using var f = Prepare(boss); var id = oid == boss ? 1UL : 10UL;
                if (id != 1) { f.Actors[id] = new(id, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 5, .7f); var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, source);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Ordinary shape missing.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        foreach (var param in new ushort[] { 0xB0, 0xB1, 0xB2 })
            run($"Heroes dash {param:X} original status clock marker shape knives only suppress navigation effect next same state", () =>
            {
                using var f = Prepare(0x2DEC); var c = f.Actors[1].Position;
                f.Actors[20] = new(20, 0x1EAED9, c + Vector3.UnitX * 4); Scan(f);
                f.Engine.StatusChanged(1, 2193, true, f.Start.AddSeconds(-1), param); f.Render();
                var aoe = f.Engine.AmbientAoes().Single();
                Check(aoe.Activation == f.Start.AddSeconds(7.1), "Dash clock lost.");
                BossMod.AOEShape source = param == 0xB0 ? new BossMod.AOEShapeCircle(15) : new BossMod.AOEShapeRect(40, 7, 40);
                var native = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null); var world = HostDrawingGeometry.Convert(native); var marker = f.Actors[20].Position;
                for (var x = -45.137f; x < 45; x += .71f)
                    for (var z = -45.231f; z < 45; z += .71f)
                    {
                        var p = marker + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(marker.X, marker.Z), new(aoe.Rotation));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Centered dash shape changed.");
                    }
                f.Actors[10] = new(10, 0x2E71, c); Scan(f); Cast(f, 10, 20439, 3);
                Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 1 && !f.Engine.AmbientAoes().Single().Navigate, "Knives suppressed drawing or kept dash navigation.");
                f.Engine.Finish(10); f.Render(); Check(f.Snapshot.DangerAreas.Count == 1, "Dash did not resume after knives.");
                f.Engine.Effect(10, 20577, 1, f.Now); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Dash hit did not clear.");
                f.Engine.StatusChanged(1, 2193, true, f.Start.AddSeconds(20), param); f.Render(); Check(f.Draw.Previews.Count == 1, "Same-param next status event lost.");
            });
        foreach (var action in HeroesCatalog.BURSTS)
            run($"Heroes burst {action} model fifty four conservative delay snapshot no repeat natural finish clears all", () =>
            {
                using var f = Prepare(0x2DF1); var c = f.Actors[1].Position; var oid = (uint)(0x2DF2 + Array.IndexOf(HeroesCatalog.BURSTS, action));
                f.Actors[10] = new(10, oid, c + Vector3.UnitX * 6, ModelState: 54);
                f.Actors[11] = new(11, 0x2DF2, c - Vector3.UnitX * 6, ModelState: 54); Scan(f);
                Check(f.Draw.Previews.Count == 2 && f.Engine.AmbientAoes().All(a => a.Activation == f.Start.AddSeconds(6)), "Burst model timing differs.");
                f.Now = f.Start.AddSeconds(1); Scan(f); Check(f.Draw.Previews.Count == 2, "Model snapshot duplicated burst.");
                Cast(f, 10, action, 4); f.Engine.Finish(10, completed: true); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Burst finish did not clear wave.");
                Scan(f); Check(f.Draw.Previews.Count == 0, "Old model resurrected wave.");
            });
        run("Heroes pain mire existing pool suppresses cast navigation only state seven restores it", () =>
        {
            using var f = Prepare(0x2DF1); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, 20388, 5.5);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Mire initial cast missing.");
            f.Actors[20] = new(20, 0x1EB02C, c + Vector3.UnitX * 8); Scan(f);
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 1, "Existing pool should suppress only mire navigation.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "State seven pool did not restore mire.");
        });
        foreach (var removed in new[] { false, true })
            run($"Heroes burst {(removed ? "source disappearance" : "cancelled cast")} clears model wave", () =>
            {
                using var f = Prepare(0x2DF1); var c = f.Actors[1].Position;
                f.Actors[10] = new(10, 0x2DF2, c, ModelState: 54); Scan(f); Cast(f, 10, 20322, 4);
                if (removed) f.Engine.RemoveActor(10);
                else f.Engine.Finish(10);
                f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Interrupted burst wave persisted.");
            });
        run("Heroes crawling zombies only before icon baits icon clock own party avoidance both perspectives effect and cleanup", () =>
        {
            using var f = Prepare(0x2DF1); var c = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x2DF6, c, Tethers: new Dictionary<uint, ulong> { [79] = 100 }); Scan(f);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 1, "Crawling hint should only navigate.");
            f.Engine.Icon(100, 23, f.Start); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("heroes/crawling/")) && f.Draw.Previews.Count == 1, "Icon did not supersede crawling hint.");
            var bait = f.Engine.AmbientAoes().Single(a => a.Id == "heroes/bait/0"); Check(bait.Activation == f.Start.AddSeconds(5.1), "Bait clock wrong.");
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) >= 9.25f);
            f.PlayerId = 101; Scan(f); Solve(f, p => Vector3.Distance(p, f.Actors[100].Position) >= 9.25f);
            f.Engine.Effect(10, 20323, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 1, "Effect did not restore crawling hint.");
            f.Engine.ResetFight(); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0, "Reset leaked crawling hint.");
        });
        run("Heroes doom supported jobs own first range and status release", () =>
        {
            using var f = Prepare(0x2DF1); f.Actors[100] = f.Actors[100] with { Statuses = [910] };
            f.Actors[101] = f.Actors[101] with { Statuses = [910] }; Scan(f);
            foreach (var job in new uint[] { 6, 24, 28, 33, 40, 23 }) Check(f.Engine.HeroesCleanseTarget(job)?.Id == 100, "Doom cleanse job/own target missing.");
            Check(f.Engine.HeroesCleanseTarget(19) == null, "Non-cleanse job requested Esuna.");
            f.Engine.StatusChanged(100, 910, false, f.Now); Check(f.Engine.HeroesCleanseTarget(24)?.Id == 101, "Doom removal not released.");
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position + Vector3.UnitX * 40 }; Scan(f);
            Check(f.Engine.HeroesCleanseTarget(24) == null, "Out of range doom targeted.");
        });
        run("Heroes collapse original finish plus one point one exact danger then map changes arena", () =>
        {
            using var f = Prepare(0x2EFD); Cast(f, 1, 21004, 4);
            Check(Math.Abs((f.Engine.AmbientAoes().Single().Activation!.Value - f.Start.AddSeconds(5.4)).TotalMilliseconds) < .001, "Collapse added wrong delay.");
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Collapse display missing.");
            f.Engine.MapEffect(0x0B, 0x00020001); f.Render();
            Check(!f.Engine.Arena!.Contains(new(768, 0, 500)) && !f.Engine.AmbientAoes().Any(), "Cross arena/map handoff failed.");
        });
        run("Heroes rubble icon true solo towers spread source activation real solver consume exact hitboxes no snapshot resurrection", () =>
        {
            using var f = Prepare(0x2EFD); var c = f.Actors[1].Position;
            f.Actors[101] = f.Actors[101] with { Position = c - Vector3.UnitX * 16 }; Scan(f);
            f.Engine.Icon(100, 229, f.Start);
            f.Actors[20] = new(20, 0x2EFE, c + Vector3.UnitX * 10, HitboxRadius: 2.5f);
            f.Actors[21] = new(21, 0x2EFE, c - Vector3.UnitX * 10, HitboxRadius: 2.5f); Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "heroes/rubble/enter").Activation == f.Start.AddSeconds(6.1), "Rubble birth deadline wrong.");
            Solve(f, p => Vector3.Distance(p, f.Actors[20].Position) <= 2.5f && Vector3.Distance(p, f.Actors[101].Position) >= 8.5f);
            f.Actors[100] = f.Actors[100] with { Position = f.Actors[20].Position }; Scan(f);
            f.Engine.Effect(1, 21000, 1, f.Now, target: 100); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Last spread/rubble effect leaked.");
            Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Old rubble snapshot resurrected towers.");
        });
        run("Heroes normal stack both roles true solver rubble suppresses stack", () =>
        {
            using var f = Prepare(0x2EFD); var c = f.Actors[1].Position; f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 12 }; Scan(f);
            Cast(f, 1, 21000, 5, target: 101); Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= 6);
            f.Engine.Finish(1); f.Now = f.Now.AddSeconds(1); Cast(f, 1, 21000, 5, target: 100);
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= 3);
            f.Actors[20] = new(20, 0x2EFE, c); Scan(f); Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("heroes/stack/")), "Stack not suppressed during rubble phase.");
        });
        run("Heroes four rubble targets consume one each mixed hits unknown target FIFO and duplicate effects", () =>
        {
            using var f = Prepare(0x2EFD); var c = f.Actors[1].Position;
            Vector3[] positions = [c + new Vector3(-12, 0, -12), c + new Vector3(12, 0, -12), c + new Vector3(-12, 0, 12), c + new Vector3(12, 0, 12)];
            for (var i = 0; i < 4; ++i)
            {
                var target = (ulong)(100 + i);
                f.Actors[target] = new(target, 0, positions[i], PartySlot: i, Hostile: false);
                f.Engine.Icon(target, 229, f.Start);
                f.Actors[(ulong)(20 + i)] = new((ulong)(20 + i), 0x2EFE, positions[i], HitboxRadius: 2.5f);
            }
            Scan(f);
            for (var i = 0; i < 4; ++i)
            {
                var action = i == 0 ? 21000u : 21001u;
                f.Engine.Effect(1, action, (uint)i, f.Now, target: (ulong)(100 + i));
                f.Engine.Effect(1, action, (uint)i, f.Now, target: (ulong)(100 + i)); f.Render();
                Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("heroes/spread/") && a.Id.Count(c => c == '/') == 2) == 3 - i, "One hit cleared other marked players.");
                Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("heroes/rubble/") && a.Id != "heroes/rubble/enter") == 3 - i, "One hit cleared distant rubble.");
            }
            f.Engine.Icon(100, 229, f.Now); f.Engine.Icon(101, 229, f.Now);
            f.Engine.Effect(1, 21001, 10, f.Now, target: 999); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "heroes/spread/101") && !f.Engine.AmbientAoes().Any(a => a.Id == "heroes/spread/100"), "Unknown target did not consume only FIFO head.");
        });
        run("Heroes crater exact animations danger safe inversion forced solver cancel returns danger", () =>
        {
            using var f = Prepare(0x2EFD); var c = f.Actors[1].Position;
            f.Actors[20] = new(20, 0x1EA1A1, new(738, 0, 482), EventAnimation: 0x00010002);
            f.Actors[21] = new(21, 0x1EA1A1, new(762, 0, 482), EventAnimation: 0x00010002); Scan(f);
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2, "Persistent craters missing.");
            f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, 20999, 5.5);
            Check(f.Engine.AmbientAoes().Count(a => a.Safe) == 2 && f.Snapshot.DangerAreas.Count == 1, "Rampage did not invert crater union.");
            Solve(f, p => Math.Min(Vector3.Distance(p, f.Actors[20].Position), Vector3.Distance(p, f.Actors[21].Position)) <= 7);
            f.Engine.Finish(10); f.Render(); Check(f.Snapshot.DangerAreas.Count == 2 && f.Engine.AmbientAoes().All(a => !a.Safe), "Rampage cancellation left safe craters.");
        });
        run("Heroes radial knockback source inset cross and crater clearance dense parity actual solver immunity", () =>
        {
            using var f = Prepare(0x2EFD); var c = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = c + new Vector3(1, 0, 2) };
            f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, 20996, 5.7);
            var kb = (HeroesKnockbackTemplate)f.Engine.AmbientAoes().Single(a => a.Id == "heroes/knockback/10").Shape;
            var source = new BossMod.SDKnockbackInComplexPolygonAwayFromOriginPlusAOECircles(new(750, 482), new(750, 482), 15,
                Berserker.DefaultBounds.Polygon.Offset(-1), [new(738, 482), new(762, 482)], 7.5f, 2);
            for (var x = -19.137f; x < 19; x += .31f)
                for (var z = -19.231f; z < 19; z += .31f)
                {
                    var p = c + new Vector3(x, 0, z); Check(kb.Unsafe(p, 0) == source.Contains(new(p.X, p.Z)), "Knockback source parity differs.");
                }
            Solve(f, p => !kb.Unsafe(p, 0));
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(10) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("heroes/knockback/")), "Knockback immunity ignored.");
        });
        run("Heroes thief delayed buster natural finish effect cancel", () =>
        {
            using var f = Prepare(0x2DEC); Cast(f, 1, 20427, 4, target: 100); f.Engine.Finish(1, completed: true);
            Check(f.Engine.PredictedDamage.Count() == 1, "Delayed buster lost or duplicate.");
            f.Engine.Effect(1, 20427, 1, f.Now); Check(!f.Engine.PredictedDamage.Any(), "Buster effect leaked.");
            f.Now = f.Now.AddSeconds(10); Cast(f, 1, 20427, 4, target: 100); f.Engine.Finish(1); Check(!f.Engine.PredictedDamage.Any(), "Cancelled buster leaked.");
        });
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 916); f.Engine.ResetFight(); var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true); f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double seconds, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -62.137f; x < 62; x += .73f)
            for (var z = -62.231f; z < 62; z += .73f)
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
            $"Real solver failed: {solution.Status}, from {player} to {(path.Count > 0 ? path[^1].Position : player)}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

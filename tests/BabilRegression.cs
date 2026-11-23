using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class BabilRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Babil exact initial and small source arenas world radar ownership", () =>
        {
            using var f = Prepare(0x33F7);
            foreach (var small in new[] { false, true })
            {
                if (small) f.Engine.MapEffect(0, 0x00020001);
                var source = small ? BossMod.Endwalker.Dungeon.D02TowerOfBabil.D021Barnabas.D021Barnabas.SmallerBounds
                    : BossMod.Endwalker.Dungeon.D02TowerOfBabil.D021Barnabas.D021Barnabas.StartingBounds;
                var arena = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(arena);
                for (var x = -21.137f; x < 21; x += .23f)
                    for (var z = -21.231f; z < 21; z += .23f)
                    {
                        var p = arena.Position + new Vector3(x, 0, z);
                        Check(arena.Contains(p, 0) == source.Contains(new(x, z)) && world.Contains(p, 0) == arena.Contains(p, 0), "Source arena differs.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.babil")
                && new uint[] { 0x33F7, 0x33FA, 0x33FD }.All(o => ArrDungeonCatalog.Owns(o, 969) && !ArrDungeonCatalog.Owns(o, 952)), "Ownership leaked.");
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x33F7, 0x33F7, 25157, new BossMod.AOEShapeRect(40, 3)),
            (0x33F7, 0x33F7, 25326, new BossMod.AOEShapeRect(40, 3)),
            (0x33F7, 0x33F7, 25158, new BossMod.AOEShapeCircle(8)),
            (0x33F7, 0x33F7, 25328, new BossMod.AOEShapeCircle(8)),
            (0x33F7, 0x233C, 25327, new BossMod.AOEShapeRect(40, 3)),
            (0x33F7, 0x233C, 25329, new BossMod.AOEShapeCircle(8)),
            (0x33F7, 0x33F7, 25159, new BossMod.AOEShapeRect(40, 3)),
            (0x33F7, 0x33F7, 25322, new BossMod.AOEShapeRect(40, 3)),
            (0x33F7, 0x33F7, 25323, new BossMod.AOEShapeCircle(8)),
            (0x33F7, 0x33F8, 25330, new BossMod.AOEShapeCircle(8)),
            (0x33FA, 0x33FA, 25340, new BossMod.AOEShapeRect(50, 3)),
            (0x33FA, 0x33FC, 25337, new BossMod.AOEShapeCross(40, 4)),
            (0x33FA, 0x233C, 25335, new BossMod.AOEShapeCircle(6)),
            (0x33FD, 0x233C, 25343, new BossMod.AOEShapeRect(20, 10)),
            (0x33FD, 0x233C, 24168, new BossMod.AOEShapeRect(60, 4)),
            (0x33FD, 0x3401, 25356, new BossMod.AOEShapeCircle(3)),
        })
            run($"Babil {action} exact shape both views independent navigation margin cancellation", () =>
            {
                using var f = Prepare(boss);
                var id = oid == boss ? 1UL : 10UL; f.Actors[id] = new(id, oid, f.Actors[1].Position, InCombat: true); Scan(f);
                Cast(f, id, action); var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Safe.NavigationOnly.Count == 1, "Cast output missing or duplicated.");
                f.Engine.Finish(id); f.Render();
                if (action is 25159 or 25322) Check(f.Engine.AmbientAoes().Single().Id == "babil/ring", "Ground pound must leave shrinking preview even after cancel.");
                else Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        foreach (var rectangle in new[] { false, true })
            foreach (var pull in new[] { false, true })
                run($"Babil magnetism rectangle {rectangle} pull {pull} icon clock constraints landing immunity and every resolve", () =>
                {
                    using var f = Prepare(0x33F7); var c = f.Engine.Arena!.Position;
                    f.Actors[100] = f.Actors[100] with { Position = c + new Vector3(8, 0, -1) };
                    f.Actors[10] = new(10, 0x233C, c); Scan(f);
                    Cast(f, 1, rectangle ? 25326u : 25328u);
                    Cast(f, 10, rectangle ? 25327u : 25329u);
                    f.Engine.Icon(100, pull ? 163u : 162u, f.Start.AddSeconds(-1)); f.Render();
                    var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "babil/magnet/avoid");
                    Check(Math.Abs((aoe.Activation!.Value - f.Start.AddSeconds(7.1)).TotalMilliseconds) < .001, "Magnetic activation did not use icon time.");
                    var forbidden = aoe.Shape.Resolve(aoe.Position, 0, null);
                    Check(forbidden.Contains(c, 0) == pull, "Magnetism zone inversion differs.");
                    var landing = BabilCatalog.MagneticLanding(c, c + Vector3.UnitX * 2, rectangle, pull);
                    Check(Math.Abs(landing.X - (pull ? c.X : c.X + (rectangle ? 11 : 7))) < .001f, "Pull crossed origin or push distance differs.");
                    f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(20) } }; Scan(f);
                    Check(f.Engine.AmbientAoes().Any(a => a.Id == "babil/magnet/avoid") == !rectangle, "Circular magnetism must ignore immunity, rectangular must honor it.");
                    foreach (var resolve in new uint[] { 24693, 24694, 25053, 25054 })
                    {
                        f.Engine.Icon(100, 162, f.Now); f.Engine.Effect(10, resolve, resolve, f.Now); f.Render();
                        Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("babil/magnet/")), "Resolve retained magnetism.");
                    }
                });
        run("Babil shrinking ring retains estimated finish plus six point one and map owns boundary", () =>
        {
            using var f = Prepare(0x33F7); Cast(f, 1, 25159); var cast = f.Engine.Casts[1];
            f.Engine.Finish(1, endedAt: f.Start.AddSeconds(4), completed: true); f.Render();
            var ring = f.Engine.AmbientAoes().Single();
            Check(ring.Activation == cast.FinishAt.AddSeconds(6.1) && f.Engine.Arena!.Contains(f.Engine.Arena.Position + Vector3.UnitX * 19, 0), "Shrinking timing/boundary changed early.");
            f.Engine.MapEffect(0, 0x00020001); f.Render();
            Check(!f.Engine.AmbientAoes().Any() && !f.Engine.Arena!.Contains(f.Engine.Arena.Position + Vector3.UnitX * 16, 0), "Map did not shrink/clear ring.");
        });
        foreach (var mode in new[] { 1, 2 })
            run($"Babil floor mode {mode} specific status both perspectives forced entry thermal map cleanup", () =>
            {
                using var f = Prepare(0x33FA); var c = f.Engine.Arena!.Position;
                Cast(f, 1, mode == 1 ? 25333u : 25331u); f.Engine.Finish(1, completed: true); f.Render();
                if (mode == 1) f.Engine.StatusChanged(100, 2672, true, f.Now);
                f.Render(); var enter = f.Engine.AmbientAoes().Single(a => a.Id == "babil/floor/enter");
                var destination = new Vector3(mode == 1 ? 213 : 229, 0, 306);
                Check(!enter.Shape.Resolve(enter.Position, 0, null).Contains(destination, 0), "Floor inverted navigation wrong.");
                Solve(f, p => new RectangleGeometry(destination, 0, 8, 8).Contains(p, 0));
                if (mode == 1)
                {
                    f.Engine.StatusChanged(100, 2504, true, f.Now); f.Engine.StatusChanged(100, 2504, false, f.Now); f.Render();
                    Check(f.Engine.AmbientAoes().Any(a => a.Id == "babil/floor/enter"), "Losing unrelated Minimum erased Breathless.");
                    f.Engine.StatusChanged(100, 2672, false, f.Now);
                }
                else f.Engine.StatusChanged(100, 2504, true, f.Now);
                f.Render(); Check(!f.Engine.AmbientAoes().Any(a => a.Id == "babil/floor/enter"), "Floor status did not turn entry into danger.");
                f.PlayerId = 101; Scan(f);
                Check(f.Engine.AmbientAoes().Any(a => a.Id == "babil/floor/enter") == (mode == 2), "Player perspective reused previous status.");
                Cast(f, 1, 25338); f.Render();
                Check(!f.Engine.AmbientAoes().Any(a => a.Id == "babil/floor/enter"), "Thermal did not release purple entry.");
                f.Engine.MapEffect(1, 0x00080004); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Map did not clear transformation floors.");
            });
        run("Babil Anima upper lower map noncombat primary preservation and graviton living filter", () =>
        {
            using var f = Prepare(0x33FD); f.Engine.MapEffect(3, 0x00020001); f.Render();
            Check(f.Engine.Arena!.Position.Z == -400, "Lower arena missing.");
            f.Actors[1] = f.Actors[1] with { Targetable = false, InCombat = false }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x33FD && f.Engine.Arena!.Position.Z == -400, "Upper primary intermission reset arena.");
            f.Actors[20] = new(20, 0x33FF, f.Engine.Arena!.Position); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Graviton not dangerous.");
            f.Actors[20] = f.Actors[20] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 0, "Dead graviton remained dangerous.");
            f.Engine.MapEffect(3, 0x00080004); Check(f.Engine.Arena!.Position.Z == -180, "Return arena missing.");
        });
        run("Babil boundless pain starts on pull persists twenty real hits duplicates and north navigation", () =>
        {
            using var f = Prepare(0x33FD); var c = f.Engine.Arena!.Position;
            f.Engine.Effect(10, 26229, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 2, "Pain needs circle and invisible half arena.");
            Solve(f, p => p.Z < c.Z && Vector3.Distance(p, c) >= 18);
            for (var i = 0; i < 20; ++i)
            {
                f.Engine.Effect(10, i == 0 ? 25348u : 25349u, (uint)(i + 2), f.Now);
                f.Engine.Effect(10, i == 0 ? 25348u : 25349u, (uint)(i + 2), f.Now);
                if (i < 19) Check(f.Engine.AmbientAoes().Any(a => a.Id == "babil/pain"), "Pain cleared before hit twenty.");
            }
            f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Pain final hit leaked.");
        });
        run("Babil graviton tether reverse sides shared color switch clock immunity range auto action and removal", () =>
        {
            using var f = Prepare(0x33FD); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x33FF, c + Vector3.UnitX * 18); Scan(f);
            f.Engine.Tether(100, 57, true, f.Start, 20); f.Render();
            var first = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/avoid"));
            Check(first.Activation == f.Start.AddSeconds(7.9) && !f.Engine.WantsBabilKnockbackImmunity, "Tether initial clock/action window differs.");
            f.Engine.Tether(100, 57, false, f.Start.AddSeconds(1), 20);
            f.Engine.Tether(100, 17, true, f.Start.AddSeconds(1), 20); f.Engine.Tick(f.Start.AddSeconds(2)); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/avoid")).Activation == first.Activation
                && f.Engine.WantsBabilKnockbackImmunity, "Color switch restarted activation or missed immunity request.");
            Solve(f, p => Vector3.Distance(p, f.Actors[20].Position) >= 33);
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(8) } };
            f.Actors[20] = f.Actors[20] with { Tethers = new Dictionary<uint, ulong> { [17] = 100 } }; f.Now = f.Start.AddSeconds(2); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("babil/tether/")) && !f.Engine.WantsBabilKnockbackImmunity, "Effective immunity failed to suppress tether.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = first.Activation!.Value } }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")), "Expiry equality incorrectly immune.");
            f.Engine.Tether(100, 17, false, f.Now, 20); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("babil/tether/")), "Untether leaked.");
        });
        run("Babil coffin five hits initial fixed then six yard tracking actual clock margin directions and phase clear", () =>
        {
            using var f = Prepare(0x33FD); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c); Scan(f);
            f.Engine.Icon(100, 197, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(5.1), "Coffin icon clock differs.");
            Cast(f, 10, 25358); f.Engine.Finish(10, completed: true); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "babil/chase/edge"), "First cast did not consume icon target.");
            var firstPosition = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display")).Position;
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 15, Rotation = MathF.PI / 2 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display")).Position == firstPosition, "First chase moved before first hit.");
            for (var i = 0; i < 5; ++i)
            {
                var time = f.Start.AddSeconds(6 + i * 1.2);
                var aoe = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
                f.Engine.Effect(10, i == 0 ? 25358u : 21239u, (uint)i, time, location: aoe.Position);
                if (i < 4)
                {
                    var next = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
                    Check(next.Activation == time.AddSeconds(1) && Vector3.Distance(next.Position, aoe.Position) <= 6.01f, "Chase did not advance from actual effect at six yards.");
                }
            }
            f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Fifth chase hit leaked.");
            f.Engine.Icon(100, 197, f.Now); f.Actors[30] = new(30, 0x3400, c); Scan(f);
            Cast(f, 30, 25359); f.Render();
            Check(!f.Engine.AmbientAoes().Any() && f.Engine.Hints(c).Contains("连续16次全屏伤害"), "Oblivion failed to clear unassigned icons or hint.");
        });
        run("Babil charnel claws only first five promoted after cast end", () =>
        {
            using var f = Prepare(0x33FD); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 17; ++i)
            {
                f.Actors[i] = new(i, 0x3401, c); Scan(f); Cast(f, i, 25357);
            }
            Check(f.Draw.Previews.Count == 5 && f.Snapshot.DangerAreas.Count == 5, "Charnel exceeded five displayed/planned casts.");
            f.Engine.Finish(10); f.Render();
            Check(f.Draw.Previews.Count == 5 && f.Engine.AmbientAoes().Any(a => a.Id == "babil/claw/15"), "Next claw not promoted.");
            f.Actors[17] = new(17, 0x3401, c); Scan(f); Cast(f, 17, 25357);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "babil/claw/15")
                && !f.Engine.AmbientAoes().Any(a => a.Id == "babil/claw/17"), "New caster reused dictionary slot and jumped queue.");
        });
        foreach (var own in new[] { false, true })
            run($"Babil stack perspective {own} real solver gathering and cleanup", () =>
            {
                using var f = Prepare(0x33F7); var c = f.Engine.Arena!.Position;
                f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 10 };
                f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 5 }; Scan(f);
                Cast(f, 1, 25324, own ? 100UL : 101UL);
                Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= (own ? 3 : 6));
                f.Engine.Finish(1); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stack cleanup failed.");
            });
        run("Babil coffin initial dead target retained then progressed target removed and icon edge real solve", () =>
        {
            using var f = Prepare(0x33FD); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c); Scan(f); f.Engine.Icon(100, 197, f.Now); f.Render();
            Solve(f, p => Math.Abs(p.X - c.X) >= 18.5f);
            Cast(f, 10, 25358); f.Engine.Finish(10, completed: true);
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/display")), "Initial chase removed before first event.");
            f.Engine.Effect(10, 25358, 1, f.Now, location: c); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("babil/chase/")), "Progressed chase retained dead target.");
        });
        run("Babil tether immunity request expires and source removal clears prediction immediately", () =>
        {
            using var f = Prepare(0x33FD); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x33FF, c); Scan(f);
            f.Engine.Tether(100, 57, true, f.Start, 20);
            f.Engine.Tick(f.Start.AddSeconds(9));
            Check(!f.Engine.WantsBabilKnockbackImmunity, "Expired tether still requests actions.");
            Check(f.Engine.PredictedDamage.Any(), "Tether damage prediction missing.");
            f.Engine.RemoveActor(20); f.Render();
            Check(!f.Engine.PredictedDamage.Any() && !f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("babil/tether/")), "Removed source leaked tether prediction.");
        });
        run("Babil icon absent from cached actors forces fresh scan and preserves original clock", () =>
        {
            using var f = Prepare(0x33FD); var c = f.Engine.Arena!.Position;
            f.Actors[102] = new(102, 0, c + Vector3.UnitX * 10, PartySlot: 2, Hostile: false);
            var scans = 0;
            var adapter = new DungeonEventAdapter(f.Engine, () => { ++scans; Scan(f); }, _ => 0, m => throw new Exception(m));
            adapter.Handle(new PromeRotation.LogSystem.LogSystemTargetIconEvent
            {
                SourceId = 102, TargetId = 102, MarkerId = 197, Timestamp = f.Start.AddSeconds(-1),
            }, f.Now);
            f.PlayerId = 102; Scan(f);
            Check(scans == 1 && f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(4.1),
                "Fresh icon target lost or original clock restarted.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 969); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true);
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
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }

    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -62.137f; x < 62; x += .71f)
            for (var z = -62.231f; z < 62; z += .71f)
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

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

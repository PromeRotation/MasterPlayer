using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class DohnMhegRegression
{
    internal static void Run(Action<string, Action> run)
    {
        FollowRegression.RunDohnControls(run);
        DohnMhegMovementRegression.Run(run);
        foreach (var (boss, typeName, field, native) in new (uint, string, string, Func<float, IGeometryShape>)[]
        {
            (0x3F2, "D021AencThon.D021AencThon", "arena", DohnMhegArenas.First),
            (0x98E, "D022Griaule.D022Griaule", "arena", DohnMhegArenas.Griaule),
            (0xF14, "D031AencThon.D033AencThon", "ArenaBounds", DohnMhegArenas.Last),
            (0xF14, "D031AencThon.D033AencThon", "ChasmArena", DohnMhegArenas.Chasm),
        })
            run($"DohnMheg {boss:X} {field} source exact arena both views and territory isolation", () =>
            {
                var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Shadowbringers.Dungeon.D02DohnMheg." + typeName)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var shape = native(0); var world = HostDrawingGeometry.Convert(shape);
                for (var x = -26f; x < 26; x += .19f)
                    for (var z = -26f; z < 26; z += .19f)
                    {
                        var point = new Vector3(source.Center.X + x, 0, source.Center.Z + z);
                        Check(shape.Contains(point) == source.Contains(new(x, z)) && world.Contains(point) == shape.Contains(point), "Source arena differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 821) && !ArrDungeonCatalog.Owns(boss, 837), "Territory ownership differs.");
            });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x3F2, 8893, new BossMod.AOEShapeCircle(6)),
            (0x3F2, 8852, new BossMod.AOEShapeCircle(4)),
            (0xF14, 13551, new BossMod.AOEShapeCone(19.5f, new(MathF.PI * 5 / 12))),
            (0xF14, 13550, new BossMod.AOEShapeCircle(8)),
        })
            run($"DohnMheg action {action} source shape packet heading both views and cancel", () =>
            {
                using var f = Prepare(boss); var center = f.Actors[1].Position;
                var source = action is 8893 or 13550 ? 10UL : 1UL;
                if (source == 10) { f.Actors[10] = new(10, 0x233C, center); Scan(f); }
                Cast(f, source, action, 4, .62f);
                var cast = f.Engine.Casts[source]; var native = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                var world = HostDrawingGeometry.Convert(native);
                for (var x = -21f; x <= 21; x += .47f)
                    for (var z = -21f; z <= 21; z += .47f)
                    {
                        var p = center + new Vector3(x, 0, z); var expected = shape.Check(new(p.X, p.Z), new(center.X, center.Z), new(.62f));
                        Check(native.Contains(p) == expected && world.Contains(p) == expected, "Shape/heading differs.");
                    }
                f.Engine.Finish(source); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled cast remained.");
            });
        foreach (var oid in new uint[] { 0x1EAAA1, 0x1EAAA2 })
            run($"DohnMheg geyser {oid:X} source rotated offsets original animation clock and stale frame", () =>
            {
                using var f = Prepare(0x3F2); var center = f.Actors[1].Position; const float angle = .7f;
                f.Actors[10] = new(10, oid, center, Rotation: angle); Scan(f);
                f.Engine.Animation(10, 0x00100020, f.Start.AddSeconds(-.3)); f.Render();
                BossMod.WDir[] offsets = oid == 0x1EAAA1 ? [new(-9, 15), new(0, -16)] : [new(-9, -15), new(0, 5), new(7, -7)];
                var aoes = f.Engine.AmbientAoes().ToArray();
                Check(aoes.Length == offsets.Length, "Geyser count differs.");
                for (var i = 0; i < offsets.Length; ++i)
                {
                    var source = (new BossMod.WPos(center.X, center.Z) + offsets[i].Rotate(new BossMod.Angle(angle))).Quantized();
                    Check(Vector3.Distance(aoes[i].Position, new(source.X, 0, source.Z)) < .001f
                        && Math.Abs((aoes[i].Activation!.Value - f.Start.AddSeconds(4.8)).TotalMilliseconds) < 1, "Geyser offset/clock differs.");
                }
                for (var i = 0; i < offsets.Length; ++i) f.Engine.Effect(20, 8800, (uint)i, f.Now);
                f.Actors[10] = f.Actors[10] with { EventAnimation = 0x00100020 }; Scan(f);
                Check(f.Draw.Previews.Count == 0, "Old animation rearmed consumed geysers.");
                f.Engine.Animation(10, 0x00100020, f.Start.AddSeconds(8)); f.Render();
                Check(f.Draw.Previews.Count == offsets.Length, "Next explicit same-state wave was lost.");
            });
        run("DohnMheg geyser wave promotion future display-only duplicate effects and restored packet clock", () =>
        {
            using var f = Prepare(0x3F2); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x1EAAA1, center); f.Actors[11] = new(11, 0x1EAAA2, center); Scan(f);
            f.Engine.Animation(10, 0x00100020); f.Engine.Animation(10, 0x00100020, f.Start.AddSeconds(-.2));
            f.Engine.Animation(11, 0x00100020, f.Start.AddSeconds(2)); f.Render();
            var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 5 && aoes.Count(a => a.Navigate) == 2 && aoes.Count(a => a.OutlineOnly) == 3,
                "Future geyser wave incorrectly navigated or duplicated.");
            Check(Math.Abs((aoes[0].Activation!.Value - f.Start.AddSeconds(4.9)).TotalMilliseconds) < 1, "Packet did not correct restored forecast.");
            f.Engine.Effect(20, 8800, 1, f.Now); f.Engine.Effect(20, 8800, 1, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Count(a => a.Navigate) == 1, "Duplicate effect advanced twice.");
            f.Engine.Effect(20, 8800, 2, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Count() == 3 && f.Engine.AmbientAoes().All(a => a.Navigate && !a.OutlineOnly), "Next wave did not become risky.");
        });
        run("DohnMheg LaughingLeap stack actual solver and source icon plus five point one", () =>
        {
            using var f = Prepare(0x3F2); f.Engine.Icon(101, 62, f.Start.AddSeconds(-.3)); f.Render();
            var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(Math.Abs((display.Activation!.Value - f.Start.AddSeconds(4.8)).TotalMilliseconds) < 1, "Stack clock differs.");
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) < 4);
            f.Engine.Effect(1, 8840, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Stack effect retained.");
        });
        foreach (var radius in new[] { 3.18f, 12.72f })
            run($"DohnMheg Griaule Swinge hitbox radius {radius} source range heading and finish", () =>
            {
                using var f = Prepare(0x98E); var center = f.Actors[1].Position;
                f.Actors[1] = f.Actors[1] with { HitboxRadius = radius, Rotation = -1 }; Scan(f);
                Cast(f, 1, 8906, 4, .7f); var aoe = f.Engine.AmbientAoes().Single();
                var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null); var world = HostDrawingGeometry.Convert(shape);
                var source = new BossMod.AOEShapeCone(50 + radius, new(MathF.PI / 6));
                for (var x = -65f; x < 65; x += .83f)
                    for (var z = -65f; z < 65; z += .83f)
                    {
                        var p = center + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(center.X, center.Z), new(.7f));
                        Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Scaled Swinge differs.");
                    }
                f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0, "Swinge finish retained.");
            });
        run("DohnMheg FeedingTime per slot creation order original birth clock true solver and root priority", () =>
        {
            using var f = Prepare(0x98E); var center = f.Actors[1].Position;
            for (var i = 0; i < 4; ++i)
            {
                var id = (ulong)(20 - i);
                f.Engine.ObjectCreated(id, f.Start.AddSeconds(-.2));
                f.Actors[id] = new(id, 0xEFB, center + new Vector3(i % 2 == 0 ? 15 : -15, 0, i < 2 ? 8 : -8),
                    Tethers: new Dictionary<uint, ulong> { [84] = 1 });
            }
            f.Actors[10] = new(10, 0xF08, center + Vector3.UnitX * 3); Scan(f);
            for (var i = 0; i < 2; ++i)
            {
                f.PlayerId = (ulong)(100 + i); Scan(f);
                var display = f.Engine.AmbientAoes().Single(a => a.Id == "dohn/feeding/display");
                var source = f.Actors[(ulong)(20 - i)];
                var shape = display.Shape.Resolve(display.Position, display.Rotation, null);
                Check(shape.Contains(source.Position) && Math.Abs((display.Activation!.Value - f.Start.AddSeconds(10.7)).TotalMilliseconds) < 1,
                    "Assignment sorted IDs or shifted original creation clock.");
                Solve(f, p => shape.Contains(p));
            }
            Check(f.Engine.PriorityTarget(1, center) == 10, "Root target priority missing.");
            foreach (var id in new ulong[] { 20, 19, 18, 17 }) f.Engine.Tether(1, 84, false, f.Now, id);
            Scan(f); Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("dohn/feeding") || a.Id.StartsWith("dohn/tether")),
                "Cancelled tether was restored from old snapshot.");
        });
        run("DohnMheg FeedingTime missing assignment safe and personal perspective refresh", () =>
        {
            using var f = Prepare(0x98E); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0xEFB, center + Vector3.UnitX * 15); Scan(f); f.Engine.Tether(1, 84, true, f.Now, 10);
            f.PlayerId = 101; Scan(f); Check(!f.Engine.AmbientAoes().Any(a => a.Id == "dohn/feeding/enter"), "Missing slot crashed or chose another player's sapling.");
            f.PlayerId = 100; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id == "dohn/feeding/enter"), "Perspective did not refresh assignment.");
            f.Engine.Effect(10, 8899, 1, f.Now); Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Feeding resolve retained line/goal.");
        });
        run("DohnMheg six CorrosiveBile effects retain natural cast finish duplicates and cancellation", () =>
        {
            using var f = Prepare(0xF14); Cast(f, 1, 13547, 4, .6f);
            var initial = f.Engine.AmbientAoes().Single();
            Check(((FanTemplate)initial.Shape).Radius == 24.875f && ((FanTemplate)initial.Shape).AngleRadians == MathF.PI / 2
                && initial.Rotation == .6f, "Bile is not source ninety-degree cone/packet heading.");
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 1, "Natural finish erased bile.");
            for (var i = 0; i < 6; ++i)
            {
                f.Engine.Effect(i == 0 ? 1UL : 20UL, i == 0 ? 13547u : 13548u, (uint)i, f.Now);
                f.Engine.Effect(i == 0 ? 1UL : 20UL, i == 0 ? 13547u : 13548u, (uint)i, f.Now);
                f.Render(); Check(f.Draw.Previews.Count == (i < 5 ? 1 : 0), "Bile counter differs.");
            }
            f.Now = f.Start.AddSeconds(20); Cast(f, 1, 13547, 4); f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled bile remained.");
        });
        run("DohnMheg five tentacle effects use primary actor heading plus forty five and cast plus one", () =>
        {
            using var f = Prepare(0xF14); f.Actors[1] = f.Actors[1] with { Rotation = .4f }; Scan(f);
            Cast(f, 1, 13952, 5, -.8f); var aoe = f.Engine.AmbientAoes().Single();
            Check(Math.Abs(aoe.Rotation - (.4f + MathF.PI / 4)) < .00001f
                && Math.Abs((aoe.Activation!.Value - f.Start.AddSeconds(6.3)).TotalMilliseconds) < 1, "Tentacle heading/clock differs.");
            var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
            var source = new BossMod.AOEShapeCross(38.875f, 3.5f); var world = HostDrawingGeometry.Convert(shape);
            for (var x = -40f; x < 40; x += .63f)
                for (var z = -40f; z < 40; z += .63f)
                {
                    var p = aoe.Position + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(aoe.Position.X, aoe.Position.Z), new(aoe.Rotation));
                    Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Tentacle dimensions differ.");
                }
            f.Engine.Finish(1, completed: true);
            for (var i = 0; i < 5; ++i)
            {
                f.Engine.Effect(i == 0 ? 1UL : 20UL, i == 0 ? 13952u : 13953u, (uint)i, f.Now); f.Render();
                Check(f.Draw.Previews.Count == (i < 4 ? 1 : 0), "Tentacles did not count visual and four helper effects.");
            }
        });
        run("DohnMheg Chasm natural finish exact bridge path swept containment pull hold and finale restore", () =>
        {
            using var f = Prepare(0xF14); var start = new Vector3(-128.5f, 0, -229);
            f.Actors[100] = f.Actors[100] with { Position = start };
            f.Actors[10] = new(10, 0xF63, new(-128.5f, 0, -260), HitboxRadius: 2.5f); Scan(f);
            Cast(f, 1, 13498, 4); f.Engine.Finish(1, completed: true); Scan(f);
            Check(f.Engine.DohnChasm, "Chasm did not switch on natural finish.");
            var route = f.Engine.DohnBridgeRoute();
            Check(route.Length >= 5, "Bridge route missing.");
            var previous = start;
            foreach (var point in route)
            {
                Check(DohnMhegBridgePath.SafeSegment(f.Engine.Arena!, previous, point), "Bridge route cuts the chasm.");
                for (var t = 0f; t < 1; t += .01f)
                {
                    var sample = Vector3.Lerp(previous, point, t);
                    f.Actors[100] = f.Actors[100] with { Position = sample }; Scan(f);
                    var continuation = f.Engine.DohnBridgeRoute();
                    Check(continuation.Length > 0 || Vector3.Distance(sample, f.Actors[10].Position) <= 3.01f, "Bridge continuation lost midway.");
                    if (continuation.Length > 0) Check(DohnMhegBridgePath.SafeSegment(f.Engine.Arena!, sample, continuation[0]), "Bridge continuation unsafe.");
                }
                previous = point;
            }
            f.Actors[100] = f.Actors[100] with { Position = start }; f.Actors[20] = new(20, 0x233C, start); Scan(f);
            Cast(f, 20, 13519, 4); Check(f.Engine.DohnPulling && f.Engine.DohnBridgeRoute().Length == 0, "Pull kept walking across bridge.");
            f.Engine.Finish(20, completed: true); f.Engine.RemoveActor(10); f.Render();
            Check(!f.Engine.DohnChasm && f.Engine.DohnBridgeRoute().Length == 0
                && !f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("dohn/bridge")), "Lyre removal retained chasm movement.");
            f.Now = f.Start.AddSeconds(30); Cast(f, 1, 13498, 4); f.Engine.Finish(1); Check(!f.Engine.DohnChasm, "Cancelled cast changed arena.");
        });
        run("DohnMheg Finale natural finish and dead lyre restore arena and distance exact target ban", () =>
        {
            using var f = Prepare(0xF14); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0xF63, center + Vector3.UnitX * 8, HitboxRadius: 2.5f); Scan(f);
            Check(f.Engine.ForbiddenTargets.SequenceEqual([10UL]) && f.Engine.PriorityTarget(10, center) != 10, "Remote lyre attack ban missing.");
            f.Actors[100] = f.Actors[100] with { Position = f.Actors[10].Position + Vector3.UnitX * 3.8f }; Scan(f);
            Check(f.Engine.ForbiddenTargets.Length == 0 && f.Engine.PriorityTarget(1, f.Actors[100].Position) == 10, "Nearby lyre remained banned.");
            Cast(f, 1, 13498, 4); f.Engine.Finish(1, completed: true); Cast(f, 10, 15723, 60); f.Engine.Finish(10, completed: true);
            Check(!f.Engine.DohnChasm, "Finale natural finish retained chasm.");
            f.Now = f.Start.AddSeconds(70); Cast(f, 1, 13498, 4); f.Engine.Finish(1, completed: true);
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f); Check(!f.Engine.DohnChasm, "Dead lyre retained chasm.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 821); f.Engine.ResetFight();
        var center = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, center, InCombat: true, HitboxRadius: boss == 0x98E ? 3.18f : 2.5f);
        f.Actors[100] = new(100, 0, center, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position), "Real solver failed native goal.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

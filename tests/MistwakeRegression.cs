using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class MistwakeRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Mistwake native discovery owns all bosses and no invented gaze shield stop", () =>
        {
            Check(ArrDungeonCatalog.Create(1314).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.mistwake"), "Discovery.");
            foreach (var oid in new uint[] { 0x4841, 0x4A77, 0x4A65 })
            {
                using var f = Prepare(oid);
                Check(MistwakeCatalog.Owns(oid) && !f.Engine.Gazes.Any() && f.Engine.ForbiddenTargets.Length == 0, "Ownership/control.");
            }
        });
        foreach (var removed in new int[][] { [], [0], [1], [2], [5, 6], [7, 8], [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] })
            run($"Mistwake source rock difference radar world subset {string.Join(",", removed)}", () =>
            {
                var native = MistwakeRocks.Build(0, removed.ToHashSet());
                var source = new BossMod.ArenaBoundsCustom([new BossMod.Square(new(84, 370), 19.5f)],
                    MistwakeRocks.Rocks.Select((r, i) => (r, i)).Where(v => !removed.Contains(v.i))
                        .Select(v => (BossMod.Shape)new BossMod.Polygon(new(v.r.X, v.r.Z), v.r.Radius, 16, new(v.r.Degrees * MathF.PI / 180))).ToArray());
                CompareArena(native, source);
            });
        foreach (var (oid, name) in new[] { (0x4A77u, "D122Amdusias.D122Amdusias"), (0x4A65u, "D123ThundergustGriffin.D123ThundergustGriffin") })
            run($"Mistwake source exported arena {oid:X} radar world", () =>
            {
                using var f = Prepare(oid);
                var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Dawntrail.Dungeon.D12Mistwake." + name)!;
                var tuple = type.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                var source = (BossMod.ArenaBoundsCustom)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
                CompareArena(f.Engine.Arena!, source);
            });
        foreach (var (boss, action, source) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x4841, 43332, new BossMod.AOEShapeCircle(5)), (0x4841, 43335, new BossMod.AOEShapeCone(30, new(MathF.PI / 3))),
            (0x4A77, 45337, new BossMod.AOEShapeCone(40, new(MathF.PI * 5 / 6))),
            (0x4A77, 45342, new BossMod.AOEShapeCone(40, new(MathF.PI * 5 / 6))),
            (0x4A65, 45293, new BossMod.AOEShapeCircle(5)),
            (0x4A65, 45296, new BossMod.AOEShapeRect(92, 3)), (0x4A65, 45297, new BossMod.AOEShapeRect(92, 3)),
            (0x4A65, 45298, new BossMod.AOEShapeRect(92, 3)), (0x4A65, 45302, new BossMod.AOEShapeRect(40, 5)),
            (0x4A65, 45304, new BossMod.AOEShapeRect(40, 9)),
        })
            run($"Mistwake {action} locked source geometry dual output until observed finish", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var c = f.Actors[10].Cast! with { Location = f.Engine.Arena!.Position + Vector3.UnitX * 2, Rotation = 1.2f };
                f.Engine.StartCast(10, c, f.Now); f.Render();
                var a = Group(f, "simple").Single(); var shape = a.Shape.Resolve(a.Position, a.Rotation, null);
                Check(a.Position == c.Location && (a.Shape is CircleTemplate || a.Rotation == 1.2f), "Packet correction.");
                for (var x = -45.13f; x < 46; x += .91f)
                    for (var z = -45.27f; z < 46; z += .97f)
                    {
                        var p = a.Position + new Vector3(x, 0, z);
                        Check(shape.Contains(p, 0) == source.Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)), "Shape.");
                    }
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Dual output.");
                f.Now = f.Now.AddMinutes(1); f.Engine.Tick(f.Now); Check(Group(f, "simple").Length == 1, "Timed consumption.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Finish.");
            });
        run("Mistwake rocks map ignores state removes static geometry independently of actor destruction", () =>
        {
            using var f = Prepare(0x4841); var rock = f.Actors[30].Position;
            Check(!f.Engine.Arena!.Contains(rock, 0), "Initial hole.");
            f.Engine.MapEffect(7, 0); Check(f.Engine.Arena!.Contains(rock, 0), "State incorrectly filtered.");
            Cast(f, 10, 43329, target: 100);
            Check(Group(f, "buster").Any(a => a.Id.Contains("rock/30")), "Map deleted actor blocker.");
            f.Engine.RemoveActor(30); Check(!Group(f, "buster").Any(a => a.Id.Contains("rock/30")), "Destroyed actor retained.");
            f.Actors.Remove(30); Scan(f);
            var other = new Vector3(84.0001f, 0, 359);
            Check(!f.Engine.Arena!.Contains(other, 0), "Unrelated hole removed.");
        });
        run("Mistwake LOS snapshots source origin actor radius shadows both views and finish FIFO", () =>
        {
            using var f = Prepare(0x4841); Cast(f, 10, 43330);
            var los = Group(f, "los/avoid").Single();
            var g = los.Shape.Resolve(los.Position, 0, null);
            var display = Group(f, "los/display");
            Check(display.Length == 2 && display.All(a => a.Safe && !a.Navigate), "Safe display.");
            var origin = f.Actors[10].Cast!.Location;
            var shadows = f.Actors.Values.Where(a => a.Oid is 0x4842 or 0x4843 or 0x4851).Select(a =>
            {
                var d = a.Position - origin; var distance = new Vector2(d.X, d.Z).Length();
                return (BossMod.Shape)new BossMod.DonutSegmentHA(new(origin.X, origin.Z), distance + .2f, 30,
                    new(MathF.Atan2(d.X, d.Z)), new(MathF.Asin(a.HitboxRadius / distance)));
            }).ToArray();
            var source = new BossMod.AOEShapeCustom(shadows, invertForbiddenZone: true);
            for (var x = 65.17f; x < 103; x += .41f)
                for (var z = 351.13f; z < 389; z += .43f)
                {
                    var p = new Vector3(x, 0, z);
                    var expected = source.Check(new(x, z), new BossMod.WPos(84, 370), default);
                    Check(g.Contains(p, 0) != expected, "LOS complement differs from source safe union.");
                    Check(display.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(p, 0)) == expected, "LOS display.");
                }
            f.Engine.RemoveActor(30); f.Actors.Remove(30); Scan(f);
            Check(Group(f, "los/display").Length == 2, "LOS refreshed after destroy.");
            f.Render(); Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 1, "LOS dual output.");
            Finish(f, 10); Check(Group(f, "los").Length == 0, "LOS finish.");
        });
        run("Mistwake spread protects only rock centers outside ground circles and follows current perspective", () =>
        {
            using var f = Prepare(0x4841); Cast(f, 10, 43332, position: f.Actors[30].Position);
            Cast(f, 11, 43333, target: 100);
            Check(!Group(f, "spread").Any(a => a.Id.Contains("rock/30")) && Group(f, "spread").Any(a => a.Id.Contains("rock/31")), "Safe rock filtering.");
            var avoid = Group(f, "spread").Single(a => a.Id.EndsWith("rock/31/avoid"));
            Check(((CircleTemplate)avoid.Shape).Radius == f.Actors[31].HitboxRadius + 5, "Protect radius.");
            f.PlayerId = 101; Scan(f);
            Check(!Group(f, "spread").Any(a => a.Id.Contains("/rock/")), "Personal output leaked.");
        });
        run("Mistwake target fallback buster protects all rocks damage and line icon protects source rays", () =>
        {
            using var f = Prepare(0x4841); f.Actors[1] = f.Actors[1] with { TargetId = 100 }; Scan(f);
            Cast(f, 1, 43329, target: 1);
            Check(Group(f, "buster").Count(a => a.Id.Contains("/rock/") && a.Navigate) == 2
                && f.Engine.PredictedDamage.Single(a => a.DamageType == "Tankbuster").TargetId == 100, "Buster.");
            Finish(f, 1);
            f.Engine.Icon(100, 524, f.Now, 1);
            var ray = Group(f, "line").Single(a => a.Id.EndsWith("rock/30/avoid"));
            Check(ray.Shape is FanTemplate fan && fan.Radius == 100 && Near(ray.Activation, f.Now, 6.2), "Line rock ray.");
            Check(Group(f, "line").Single(a => a.Id.EndsWith("/display")).Position == f.Actors[1].Position, "Icon source.");
            f.Engine.Effect(1, 43334, 1, f.Now, target: 101); Check(Group(f, "line").Length == 0, "Single actual target correction.");
        });
        run("Mistwake Amdusias charge dynamic length FIFO activation offset colors persist without telegraph finish consumption", () =>
        {
            using var f = Prepare(0x4A77); var p = f.Engine.Arena!.Position;
            Cast(f, 10, 45348, 1.5, position: p + Vector3.UnitX * 12);
            Cast(f, 11, 45348, 2, position: p - Vector3.UnitZ * 8);
            Cast(f, 12, 45348, 1, position: p + Vector3.UnitZ * 6);
            var q = Group(f, "charge");
            Check(q.Length == 3 && Near(q[0].Activation, f.Now, 10.2) && Near(q[1].Activation, f.Now, 11.2)
                && Near(q[2].Activation, f.Now, 10.7), "Queue offsets.");
            Check(q[0].Shape.Resolve(q[0].Position, q[0].Rotation, null).Contains(p + Vector3.UnitX * 11.9f, 0), "Charge path.");
            Finish(f, 10); Check(Group(f, "charge").Length == 3, "Telegraph consumed charge.");
            f.Engine.Effect(99, 45347, 1, f.Now); Check(Group(f, "charge")[0].Color != null, "New head color.");
            f.Engine.Effect(99, 45347, 2, f.Now); Check(Group(f, "charge").Single().Color == null, "Single tail recolored.");
            f.Now = f.Now.AddMinutes(1); f.Engine.Tick(f.Now); Check(Group(f, "charge").Length == 1, "Time popped tail.");
            f.Engine.Effect(99, 45347, 3, f.Now); Check(Group(f, "charge").Length == 0, "Third actual.");
        });
        run("Mistwake Burst status arbitrary actor gain clock quantized position snapshot acknowledgement FIFO finish", () =>
        {
            using var f = Prepare(0x4A77); var p = f.Engine.Arena!.Position;
            f.Actors[30] = new(30, 0x9999, p + new Vector3(.123f, 0, .177f)); Scan(f);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 30, StatusId = 2536, Param = 0, Timestamp = f.Now }, f.Now);
            var first = Group(f, "burst").Single(); Check(Near(first.Activation, f.Now, 4.5), "Event clock.");
            f.Actors[30] = f.Actors[30] with { Statuses = [2536], Position = p + Vector3.UnitX * 5 }; Scan(f);
            Check(Group(f, "burst").Length == 1 && Group(f, "burst")[0].Position == first.Position, "Snapshot duplicate/moved.");
            f.Now = f.Now.AddSeconds(2); f.Engine.StatusChanged(30, 2536, true, f.Now);
            f.Engine.Tick(f.Now);
            Check(Group(f, "burst").Length == 2 && Group(f, "burst")[0].Color != null, "Repeated gain / all visible color.");
            f.Engine.StatusChanged(30, 2536, false, f.Now); Check(Group(f, "burst").Length == 2, "Lose consumed.");
            Cast(f, 10, 45349); Check(Group(f, "burst").Length == 2, "Cast created/consumed.");
            Finish(f, 10); Check(Group(f, "burst").Length == 1, "Finish FIFO.");
        });
        run("Mistwake three hit stack retains finish and only second repeat clears including dead target hit counter", () =>
        {
            using var f = Prepare(0x4A77); Cast(f, 10, 45353, target: 100); Finish(f, 10);
            Check(Group(f, "stack").Any() && !f.Engine.PredictedDamage.Any(a => a.DamageType == "Shared"), "Stack state/allmask prediction.");
            f.Engine.Effect(10, 45353, 1, f.Now); f.Engine.Effect(10, 45354, 2, f.Now);
            Check(Group(f, "stack").Any(), "Early repeat clear.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f); Check(!Group(f, "stack").Any(), "Dead stack.");
            Cast(f, 11, 45353, target: 101); f.Engine.Effect(11, 45354, 3, f.Now);
            Check(!Group(f, "stack").Any(), "Death reset repeat count.");
        });
        run("Mistwake BioII remains raidwide without invented radius twenty danger", () =>
        {
            using var f = Prepare(0x4A77); Cast(f, 10, 45345);
            Check(!Group(f, "simple").Any() && f.Engine.PredictedDamage.Count() == 2 && f.Draw.Previews.Count == 0, "BioII.");
        });
        run("Mistwake Griffin map shrink actual center not finish and force hidden from both outputs until all Rush end", () =>
        {
            using var f = Prepare(0x4A65); var center = f.Engine.Arena!.Position;
            Cast(f, 1, 45291); Check(Near(Group(f, "shrink").Single().Activation, f.Now, 5.7), "Shrink clock.");
            Finish(f, 1); Check(f.Engine.Arena!.Contains(center + Vector3.UnitX * 25, 0), "Finish shrank.");
            f.Engine.MapEffect(0x17, 1); Check(Group(f, "shrink").Length == 1, "Wrong state.");
            f.Engine.MapEffect(0x17, 0x00020001); Check(Group(f, "shrink").Length == 0 && !f.Engine.Arena!.Contains(center + Vector3.UnitX * 21, 0), "Map.");
            Cast(f, 10, 45304, 9.6); Cast(f, 11, 45302, 6); Cast(f, 12, 45302, 6);
            Check(Group(f, "simple").Length == 2 && f.Draw.Previews.Count == 2, "Hidden force.");
            Finish(f, 11); Check(Group(f, "simple").Length == 1, "Remaining rush.");
            Finish(f, 12); Check(Group(f, "simple").Single().Name == "ElectrogeneticForce"
                && Near(Group(f, "simple").Single().Activation, f.Now, 9.6), "Force original time.");
        });
        run("Mistwake ShockStorm create clock overwrite freeze survives destroy clears only StormSurge finish", () =>
        {
            using var f = Prepare(0x4A65); var p = f.Engine.Arena!.Position;
            f.Engine.ObjectCreated(30, f.Now.AddSeconds(-1));
            f.Actors[30] = new(30, 0x4A67, p, Rotation: .7f); Scan(f);
            Check(Near(Group(f, "storm").Single().Activation, f.Now, 5), "Create clock.");
            var locked = Group(f, "storm").Single().Position;
            f.Actors[30] = f.Actors[30] with { Position = p + Vector3.UnitX * 10 }; Scan(f);
            Check(Group(f, "storm").Single().Position == locked, "Moved prediction.");
            f.Actors[31] = new(31, 0x4A67, p + Vector3.UnitZ * 5); Scan(f);
            Check(MathF.Abs(Group(f, "storm").Single().Position.Z - (p.Z + 5)) < .011f, "Quantized overwrite.");
            f.Engine.RemoveActor(31); f.Actors.Remove(31); Scan(f); Check(Group(f, "storm").Length == 1, "Destroy cleared.");
            Cast(f, 1, 45299); Check(Group(f, "storm").Length == 1, "Start cleared.");
            Finish(f, 1); Check(Group(f, "storm").Length == 0, "Finish retained.");
        });
        foreach (var rotation in new[] { 0f, MathF.PI / 2, .7f })
            run($"Mistwake left right KB source axis navigation and sequential displayed landing {rotation}", () =>
            {
                using var f = Prepare(0x4A65); f.Engine.MapEffect(0x17, 0x00020001);
                var p = f.Engine.Arena!.Position;
                f.Actors[100] = f.Actors[100] with { Position = p + new Vector3(4, 0, 3) }; Scan(f);
                Cast(f, 10, 47022, rotation: rotation, position: p + Vector3.UnitX * 100);
                var kb = (MistwakeKnockbackTemplate)Group(f, "kb/avoid").Single().Shape;
                var alongZ = rotation == MathF.PI / 2;
                for (var x = -18.17f; x < 19; x += .71f)
                    for (var z = -18.23f; z < 19; z += .73f)
                    {
                        var point = p + new Vector3(x, 0, z);
                        var dest = point + (alongZ ? Vector3.UnitZ * (z > 0 ? 12 : -12) : Vector3.UnitX * (x > 0 ? 12 : -12));
                        var expected = Vector2.Distance(new(dest.X, dest.Z), new(p.X, p.Z)) > 19;
                        Check(kb.Resolve(p, 0, null).Contains(point, 0) == expected, "Axis navigation.");
                    }
                Check(Group(f, "kb").Count(a => a.Display) >= 2, "Missing landing preview.");
                f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddMinutes(1) } };
                Scan(f); Check(Group(f, "kb").Length == 0, "Immune navigation/preview.");
                Finish(f, 10); Check(Group(f, "kb").Length == 0, "Finish clear.");
            });
        run("Mistwake LOS out of order finish retains separate active caster and frozen safezone queue", () =>
        {
            using var f = Prepare(0x4841); var p = new Vector3(84, 0, 370);
            Cast(f, 10, 43330, 10, position: p);
            Cast(f, 11, 43330, 5, position: p + Vector3.UnitX * 10);
            Finish(f, 11);
            f.Now = f.Now.AddSeconds(6);
            Cast(f, 12, 43330, 14, position: p + Vector3.UnitZ * 10);
            Finish(f, 10);
            var a = Group(f, "los/avoid").Single();
            Check(!a.Shape.Resolve(a.Position, 0, null).Contains(new(97, 0, 370), 0), "Ended caster supplied new shadow origin.");
        });
        run("Mistwake Burst snapshot inferred gain acknowledgement expires across status loss", () =>
        {
            using var f = Prepare(0x4A77); var p = f.Engine.Arena!.Position;
            f.Actors[30] = new(30, 0x9999, p, Statuses: [2536]); Scan(f);
            Cast(f, 10, 45349); Finish(f, 10); Check(Group(f, "burst").Length == 0, "First burst retained.");
            f.Engine.StatusChanged(30, 2536, false, f.Now);
            f.Actors[30] = f.Actors[30] with { Statuses = [] }; Scan(f);
            f.Now = f.Now.AddSeconds(2); f.Engine.StatusChanged(30, 2536, true, f.Now);
            Check(Group(f, "burst").Length == 1 && Near(Group(f, "burst")[0].Activation, f.Now, 4.5), "Next gain swallowed.");
        });
        run("Mistwake overlapping spreads consume one target and recover after resurrection", () =>
        {
            using var f = Prepare(0x4A77); Cast(f, 10, 45343, target: 101); Cast(f, 11, 45343, target: 101);
            Finish(f, 10); Check(Group(f, "spread").Count(a => a.Display) == 1, "Consumed both.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); Check(!Group(f, "spread").Any(), "Dead spread shown.");
            f.Actors[101] = f.Actors[101] with { Dead = false }; Scan(f);
            Check(Group(f, "spread").Count(a => a.Display) == 1 && !f.Engine.PredictedDamage.Any(), "Resurrected spread/source prediction.");
        });
        run("Mistwake line target source buddy radius is two yards", () =>
        {
            using var f = Prepare(0x4841); f.Engine.Icon(100, 524, f.Now, 1);
            var a = Group(f, "line").Single(a => a.Id.EndsWith("/gather"));
            Check(!a.Shape.Resolve(a.Position, 0, null).Contains(f.Actors[101].Position + Vector3.UnitX * 1.5f, 0), "Wrong buddy radius.");
        });
        run("Mistwake overlapping knockback preview checks immunity at each activation", () =>
        {
            using var f = Prepare(0x4A65); var p = f.Engine.Arena!.Position;
            f.Actors[100] = f.Actors[100] with { Position = p + new Vector3(4, 0, 3), Statuses = [160],
                StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(7) } }; Scan(f);
            Cast(f, 10, 47022, 5); Cast(f, 11, 47022, 10);
            Check(Group(f, "kb/avoid").Length == 0 && Group(f, "kb").Any(a => a.Display), "Later nonimmune preview lost.");
        });
        run("Mistwake temporal solver actually moves to source knockback safe start", () =>
        {
            using var f = Prepare(0x4A65); f.Engine.MapEffect(0x17, 0x00020001);
            var p = f.Engine.Arena!.Position + Vector3.UnitX * 15;
            f.Actors[100] = f.Actors[100] with { Position = p }; Scan(f); Cast(f, 10, 47022, 8); f.Render();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(p, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No safe route.");
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(path.Count >= 2 && !Group(f, "kb/avoid").Single().Shape.Resolve(p, 0, null).Contains(path[^1].Position, 0), "Unsafe destination.");
        });
    }

    private static void CompareArena(IGeometryShape native, BossMod.ArenaBoundsCustom source)
    {
        var world = HostDrawingGeometry.Convert(native);
        var center = native.Position;
        Vector2 Project(Vector3 p) => new Vector2(p.X - center.X, p.Z - center.Z) * 10;
        var outlines = native.GetContours().Select(c => (IReadOnlyList<Vector2>)c.Select(Project).ToArray()).ToArray();
        var fill = native is PolygonRegionGeometry r ? r.FillPolygons.Select(c => (IReadOnlyList<Vector2>)c.Select(Project).ToArray()).ToArray() : outlines;
        var radar = new RadarClipRegion(outlines[0], Vector2.Zero, 500, outlines, fill);
        for (var x = center.X - 31.137f; x < center.X + 31; x += .317f)
            for (var z = center.Z - 31.173f; z < center.Z + 31; z += .331f)
            {
                var p = new Vector3(x, 0, z);
                var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, $"Arena {p} native={native.Contains(p, 0)} source={expected}.");
                if (radar.Contains(Project(p)) != expected)
                    Check(native.GetContours().Any(c => Enumerable.Range(0, c.Count).Any(i =>
                    {
                        var a = c[i]; var b = c[(i + 1) % c.Count]; var d = b - a;
                        var t = d.LengthSquared() == 0 ? 0 : Math.Clamp(Vector3.Dot(p - a, d) / d.LengthSquared(), 0, 1);
                        return Vector3.Distance(p, a + t * d) < .0002f;
                    })), $"Radar {p}.");
            }
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1314);
        var p = boss switch { 0x4841 => new Vector3(84, 0, 370), 0x4A77 => new Vector3(281, 0, -285), _ => new Vector3(281, 0, -620) };
        f.Actors[1] = f.Actors[1] with { Position = p };
        f.Actors[100] = new(100, 0, p, PartySlot: 0, PartyRole: "Tank", Hostile: false);
        f.Actors[101] = new(101, 0, p + Vector3.UnitX * 4, PartySlot: 1, PartyRole: "Healer", Hostile: false);
        if (boss == 0x4841)
        {
            // Rocks exist when the module activates; later creations must not join its captured list.
            f.Engine.ResetFight();
            f.Actors[30] = new(30, 0x4851, new(72, 0, 357.5f), HitboxRadius: 2.5f);
            f.Actors[31] = new(31, 0x4842, new(90.5f, 0, 370), HitboxRadius: 1.5f);
        }
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double seconds = 5, ulong target = 0, float rotation = .3f, Vector3? position = null)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        Scan(f);
        var cast = new DungeonCastObservation(action, position ?? f.Engine.Arena!.Position, rotation, f.Now, f.Now.AddSeconds(seconds), target);
        f.Actors[id] = f.Actors[id] with { Cast = cast }; f.Engine.StartCast(id, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id)
    {
        var action = f.Actors[id].Cast!.Action; f.Actors[id] = f.Actors[id] with { Cast = null };
        f.Engine.Finish(id, action, f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string group) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("mist/" + group)).ToArray();
    private static bool Near(DateTime? value, DateTime now, double seconds) => value.HasValue && Math.Abs((value.Value - now).TotalSeconds - seconds) < .001;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

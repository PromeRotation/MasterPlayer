using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class MalikahRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (boss, typeName, native) in new (uint, string, Func<float, IGeometryShape>)[]
        {
            (0x2679, "D041GreaterArmadillo", MalikahArenas.Armadillo),
            (0x267A, "D042AmphibiousTalos", MalikahArenas.Talos),
            (0x267B, "D043Storge", MalikahArenas.Storge),
        })
            run($"Malikah {typeName} source exact arena both views concave corners and ownership", () =>
            {
                var type = typeof(BossMod.BossModule).Assembly.GetType($"BossMod.Shadowbringers.Dungeon.D04MalikahsWell.{typeName}.{typeName}")!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var shape = native(0); var world = HostDrawingGeometry.Convert(shape);
                for (var x = -23f; x < 23; x += .17f)
                    for (var z = -23f; z < 23; z += .17f)
                    {
                        var p = new Vector3(source.Center.X + x, 0, source.Center.Z + z); var expected = source.Contains(new(x, z));
                        Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Arena source or world shape differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 836) && !ArrDungeonCatalog.Owns(boss, 823), "Ownership escaped territory.");
                if (boss == 0x267B) Check(!shape.Contains(shape.Position + new Vector3(18, 0, 18)), "Storge corner filled.");
            });
        foreach (var (boss, action, oid, source) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x2679, 15594, 0x233C, new BossMod.AOEShapeCircle(4)),
            (0x2679, 15593, 0x2679, new BossMod.AOEShapeCircle(10)),
            (0x2679, 15929, 0x233C, new BossMod.AOEShapeDonut(10, 20)),
            (0x267A, 15597, 0x267A, new BossMod.AOEShapeCircle(15)),
            (0x267A, 15598, 0x233C, new BossMod.AOEShapeCircle(8)),
            (0x267B, 15605, 0x267B, new BossMod.AOEShapeDonut(5, 60)),
            (0x267B, 15602, 0x267B, new BossMod.AOEShapeCross(60, 5)),
            (0x267B, 15607, 0x267C, new BossMod.AOEShapeCircle(5)),
        })
            run($"Malikah action {action} source shape packet heading both views and cancel", () =>
            {
                using var f = Prepare(boss); var center = f.Actors[1].Position; var id = oid == boss ? 1UL : 10UL;
                if (id == 10) { f.Actors[id] = new(id, oid, center); Scan(f); }
                Cast(f, id, action, 5, .7f); var cast = f.Engine.Casts[id];
                Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, source, center, .7f);
                Check(f.Draw.Previews.Count == 1 && f.Safe.NavigationOnly.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Output duplication/missing preview.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancellation leaked.");
            });
        foreach (var length in new[] { 0f, 7f, 25f })
            run($"Malikah RightRound capsule length {length} source cap original endpoint delay and effect", () =>
            {
                using var f = Prepare(0x2679); var center = f.Actors[1].Position; var endpoint = center + Vector3.UnitZ * length;
                var cast = new DungeonCastObservation(15591, endpoint, .7f, f.Now, f.Now.AddSeconds(2.8));
                f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
                var aoe = f.Engine.AmbientAoes().Single();
                Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeCapsule(9, MathF.Min(10, length)), endpoint, .7f + MathF.PI);
                Check(Math.Abs((aoe.Activation!.Value - f.Start.AddSeconds(3.7)).TotalMilliseconds) < .001, "RightRound cast plus .9 differs.");
                f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 1, "Natural finish erased jump forecast.");
                f.Engine.Effect(20, 15592, 1, f.Now); f.Engine.Effect(20, 15592, 1, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Resolved jump revived.");
            });
        run("Malikah HeadToss forced four stack real solver and add interrupt priority", () =>
        {
            using var f = Prepare(0x2679); var center = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = center - Vector3.UnitZ * 9 };
            f.Actors[101] = f.Actors[101] with { Position = center + Vector3.UnitZ * 9 };
            f.Actors[10] = new(10, 0x2683, center + Vector3.UnitX * 5); Scan(f); Cast(f, 10, 16776, 5);
            Check(f.Engine.PriorityTarget(1, center) == 10 && f.Engine.Hints(center).Contains("打断或眩晕"), "Add interrupt/priority missing.");
            Cast(f, 1, 15590, 5, target: 101); Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) < 6);
            f.Engine.Finish(1); f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Stack cancel leaked.");
        });
        foreach (var icon in new uint[] { 156, 157 })
            foreach (var iconFirst in new[] { false, true })
                run($"Malikah SwiftSpill icon {icon} first {iconFirst} zero heading six actual effects and clock", () =>
                {
                    using var f = Prepare(0x267A); var center = f.Actors[1].Position;
                    if (iconFirst) f.Engine.Icon(1, icon, f.Now);
                    Cast(f, 1, 15599, 7);
                    if (!iconFirst) f.Engine.Icon(1, icon, f.Now);
                    f.Render(); var increment = (icon == 156 ? -1 : 1) * MathF.PI / 3;
                    Check(f.Draw.Previews.Count == 2, "Zero heading not initialized or next segment omitted.");
                    f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 2, "Natural finish cleared rotation.");
                    for (var i = 0; i < 6; ++i)
                    {
                        var current = f.Engine.AmbientAoes().Single(a => a.Id == "malikah/swift/0");
                        Compare(current.Shape, current.Position, current.Rotation, new BossMod.AOEShapeCone(50, new(MathF.PI / 6)), center, increment * i);
                        var timestamp = f.Start.AddSeconds(7.3 + i * 1.1); f.Now = timestamp.AddSeconds(.2);
                        var action = i == 0 ? 15599u : 15600u;
                        f.Engine.Effect(1, action, (uint)i, f.Now, occurredAt: timestamp);
                        f.Engine.Effect(1, action, (uint)i, f.Now, occurredAt: timestamp); f.Render();
                        Check(f.Draw.Previews.Count == Math.Min(2, 5 - i), "Duplicate effect advanced twice or six-count differs.");
                        if (i < 5) Check(f.Engine.AmbientAoes().Single(a => a.Id == "malikah/swift/0").Activation == timestamp.AddSeconds(1.1),
                            "Next clock used dispatch instead of original effect time.");
                    }
                    Scan(f); Check(f.Draw.Previews.Count == 0, "Consumed wave revived from old cast.");
                });
        run("Malikah SwiftSpill cancellation missing icon unrelated icon and repeat wave", () =>
        {
            using var f = Prepare(0x267A); Cast(f, 1, 15599, 7);
            Check(!f.Engine.AmbientAoes().Any(), "Missing rotation icon was guessed.");
            f.Engine.Icon(100, 156, f.Now); Check(!f.Engine.AmbientAoes().Any(), "Other actor icon controlled Boss.");
            f.Engine.Icon(1, 157, f.Now); f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled rotation leaked.");
            f.Now = f.Start.AddSeconds(10); Cast(f, 1, 15599, 7, .4f); f.Engine.Icon(1, 156, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 2, "Next wave could not initialize.");
            f.Engine.RemoveActor(1); f.Render(); Check(f.Draw.Previews.Count == 0, "Removed boss retained rotation.");
        });
        run("Malikah HighPressure wall landing persistent geysers true solver immunity and expiry", () =>
        {
            using var f = Prepare(0x267A); var center = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 5 };
            f.Actors[10] = new(10, 0x1EAAC9, center + Vector3.UnitZ * 17); Scan(f); Cast(f, 1, 15596, 4);
            var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "malikah/pressure/1"); var template = (CastrumKnockbackTemplate)aoe.Shape;
            Check(f.Engine.Arena!.Contains(template.Landing(f.Actors[100].Position), 0)
                && aoe.Shape.Resolve(aoe.Position, 0, null).Contains(f.Actors[100].Position), "Wall stop or landing hazard differs.");
            Solve(f, p => !template.LandingDangers.Any(d => d.Contains(template.Landing(p))));
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Now.AddSeconds(10) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("malikah/pressure")), "Immune player displacement remained.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Now.AddSeconds(1) } }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("malikah/pressure")), "Expired immunity suppressed knockback.");
            f.Engine.EventState(10, 7); f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0, "State seven/cancel leaked.");
        });
        run("Malikah polygon knockback clipping matches original containment bisection", () =>
        {
            var arena = MalikahArenas.Talos(0);
            foreach (var origin in new[] { arena.Position, arena.Position + new Vector3(6, 0, -4) })
            {
                var template = new CastrumKnockbackTemplate(arena, origin, 20, []);
                for (var x = -18f; x <= 18; x += .7f)
                    for (var z = -18f; z <= 18; z += .7f)
                    {
                        var point = arena.Position + new Vector3(x, 0, z);
                        if (!arena.Contains(point, 0)) continue;
                        var delta = point - origin;
                        if (delta.LengthSquared() < .0001f) continue;
                        var destination = point + Vector3.Normalize(delta) * 20;
                        var expected = destination;
                        if (!arena.Contains(destination))
                        {
                            var near = point; var far = destination;
                            for (var i = 0; i < 16; ++i)
                            {
                                var middle = (near + far) * .5f;
                                if (arena.Contains(middle, 0)) near = middle;
                                else far = middle;
                            }
                            expected = near;
                        }
                        Check(Vector3.Distance(template.Landing(point), expected) < .002f && arena.Contains(template.Landing(point), 0),
                            "Edge clip changed knockback wall landing.");
                    }
            }
        });
        foreach (var longDonut in new[] { true, false })
            run($"Malikah Storge long {(longDonut ? "donut" : "cross")} four nails sorted promotion late cancellation and finish", () =>
            {
                using var f = Prepare(0x267B); var center = f.Actors[1].Position;
                Cast(f, 1, longDonut ? 15887u : 15886u, 29, .7f);
                for (var i = 0; i < 4; ++i)
                {
                    var id = (ulong)(10 + i); f.Actors[id] = new(id, 0x267C, center + new Vector3(i % 2 == 0 ? -10 : 10, 0, i < 2 ? -10 : 10));
                }
                Scan(f);
                for (var i = 3; i >= 0; --i) Cast(f, (ulong)(10 + i), longDonut ? 15609u : 15610u, 8 + i * 3, .3f);
                for (var i = 0; i < 4; ++i)
                {
                    var aoe = f.Engine.AmbientAoes().Single();
                    Check(aoe.Position == f.Actors[(ulong)(10 + i)].Position && f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1,
                        "Streak not sorted or emitted future segments simultaneously.");
                    f.Engine.Finish((ulong)(10 + i), completed: true); f.Render();
                }
                Check(f.Engine.AmbientAoes().Single().Position == center, "Long Boss segment was lost while nails completed.");
                f.Engine.Finish(1, completed: true); Scan(f); Check(f.Draw.Previews.Count == 0, "Final streak revived.");
            });
        run("Malikah Storge cancelled future source preserves first and cross donut source geometry", () =>
        {
            using var f = Prepare(0x267B); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x267C, center + Vector3.UnitX * 10); Scan(f);
            Cast(f, 1, 15887, 29, .4f); Cast(f, 10, 15609, 8, .7f);
            var first = f.Engine.AmbientAoes().Single(); Compare(first.Shape, first.Position, first.Rotation, new BossMod.AOEShapeCross(60, 5), first.Position, .7f);
            f.Engine.Finish(1); f.Render(); Check(f.Engine.AmbientAoes().Single().Position == f.Actors[10].Position, "Future cancel consumed first.");
            f.Engine.Finish(10); f.Now = f.Start.AddSeconds(40); Cast(f, 1, 15886, 23, .5f); Cast(f, 10, 15610, 9);
            var donut = f.Engine.AmbientAoes().Single(); Compare(donut.Shape, donut.Position, donut.Rotation, new BossMod.AOEShapeDonut(5, 60), donut.Position, 0);
            f.Engine.RemoveActor(10); f.Render(); Check(f.Engine.AmbientAoes().Single().Position == center, "Removed nail erased Boss followup.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset leaked.");
        });
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 836); f.Engine.ResetFight(); var center = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, center, InCombat: true);
        f.Actors[100] = new(100, 0, center, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source, Vector3 sourceOrigin, float sourceRotation)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -65f; x <= 65; x += .73f)
            for (var z = -65f; z <= 65; z += .73f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(sourceOrigin.X, sourceOrigin.Z), new(sourceRotation));
                Check(shape.Contains(p) == expected, "Source analytic shape differs.");
                // Capsule world contours approximate curved caps; sample away from their .02y tessellation edge.
                if (template is DungeonCapsuleTemplate && shape.Contains(p, .025f) != shape.Contains(p, -.025f)) continue;
                Check(world.Contains(p) == expected, "World shape or packet heading differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position),
            $"Real solver failed: {solution.Status}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

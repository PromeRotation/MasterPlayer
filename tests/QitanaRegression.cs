using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class QitanaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (boss, name, field, native) in new (uint, string, string, Func<float, IGeometryShape>)[]
        {
            (0x2A40, "D030RonkanDreamer", "arena1", QitanaArenas.DreamerFirst),
            (0x2A40, "D030RonkanDreamer", "arena2", QitanaArenas.DreamerSecond),
            (0x27AF, "D031Lozatl", "arena", QitanaArenas.Lozatl),
            (0x27B0, "D032Batsquatch", "arena", QitanaArenas.Batsquatch),
        })
            run($"Qitana {name} {field} resolved source arena holes both views and ownership", () =>
            {
                var type = typeof(BossMod.BossModule).Assembly.GetType($"BossMod.Shadowbringers.Dungeon.D03QitanaRavel.{name}.{name}")!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var shape = native(0); var world = HostDrawingGeometry.Convert(shape);
                for (var x = -25f; x < 25; x += .17f)
                    for (var z = -25f; z < 25; z += .17f)
                    {
                        var point = new Vector3(source.Center.X + x, 0, source.Center.Z + z);
                        var expected = source.Contains(new(x, z));
                        Check(shape.Contains(point) == expected && world.Contains(point) == expected, "Resolved arena differs.");
                    }
                if (boss == 0x2A40)
                    Check(shape is PolygonRegionGeometry && shape.GetContours().Count == 3, "Two pillar holes were filled.");
                Check(ArrDungeonCatalog.Owns(boss, 823) && !ArrDungeonCatalog.Owns(boss, 821), "Ownership escaped territory.");
            });
        foreach (var (boss, action, oid, sourceShape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x2A40, 17387, 0x2A40, new BossMod.AOEShapeCircle(6)),
            (0x2A40, 15923, 0x28E3, new BossMod.AOEShapeRect(15, 2)),
            (0x27AF, 15498, 0x27AF, new BossMod.AOEShapeCircle(5)),
            (0x27AF, 15504, 0x27AF, new BossMod.AOEShapeRect(60, 10)),
            (0x27AF, 15503, 0x27AF, new BossMod.AOEShapeRect(60, 10)),
            (0x27B0, 15510, 0x233C, new BossMod.AOEShapeCircle(3)),
            (0x27B0, 15509, 0x233C, new BossMod.AOEShapeCircle(2)),
            (0x27B0, 15511, 0x233C, new BossMod.AOEShapeCircle(4)),
            (0x27B1, 15518, 0x233C, new BossMod.AOEShapeCircle(6)),
            (0x27B1, 15526, 0x233C, new BossMod.AOEShapeCone(60, new(MathF.PI / 6))),
            (0x27B1, 15527, 0x233C, new BossMod.AOEShapeCone(60, new(MathF.PI / 6))),
            (0x27B1, 15522, 0x233C, new BossMod.AOEShapeCone(60, new(MathF.PI / 6))),
        })
            run($"Qitana action {action} source geometry packet heading double view and cancellation", () =>
            {
                using var f = Prepare(boss); var center = f.Actors[1].Position;
                var id = oid == boss ? 1UL : 10UL;
                if (id == 10) { f.Actors[id] = new(id, oid, center); Scan(f); }
                Cast(f, id, action, 4, .7f); var cast = f.Engine.Casts[id];
                Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, sourceShape, center, .7f);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1 && f.Safe.NavigationOnly.Count == 1,
                    "Native output duplicated or omitted either view/navigation.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        foreach (var (z, x, length) in new (float, float, float)[] { (627, -17, 22), (642, -17, 12), (630, 0, 35), (436, -17, 22), (421, -17, 12) })
            run($"Qitana Ronka source map {x} {z} length {length} packet clock lock and effect cleanup", () =>
            {
                using var f = Prepare(0x2A40, z > 550); var position = new Vector3(x, 0, z);
                f.Actors[10] = new(10, 0x28E8, position, Rotation: .8f, Tethers: new Dictionary<uint, ulong> { [37] = 1 }); Scan(f);
                var timestamp = f.Start.AddSeconds(-.4);
                f.Engine.Tether(1, 37, true, timestamp, 10); f.Engine.Tether(1, 37, true, timestamp, 10); f.Render();
                var aoe = f.Engine.AmbientAoes().Single();
                var sourcePosition = new BossMod.WPos(x, z).Quantized();
                var locked = new Vector3(sourcePosition.X, 0, sourcePosition.Z);
                Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeRect(length, 4), locked, .8f);
                Check(aoe.Activation == timestamp.AddSeconds(6), "Original tether clock was shifted.");
                f.Actors[10] = f.Actors[10] with { Position = position + Vector3.UnitX * 5, Rotation = -1 }; Scan(f);
                Check(Vector3.Distance(f.Engine.AmbientAoes().Single().Position, locked) < .001f, "Beam drifted after lock.");
                f.Engine.Effect(20, 15917, 1, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Consumed beam revived from stale frame.");
                f.Engine.Tether(1, 37, true, timestamp.AddSeconds(10), 10); f.Render(); Check(f.Draw.Previews.Count == 1, "Next wave was lost.");
                f.Engine.RemoveActor(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Removed statue leaked.");
            });
        foreach (var x in new[] { 10f, -10f })
            run($"Qitana RonkanLight x{x} forty width source animation clock and next wave", () =>
            {
                using var f = Prepare(0x27AF); f.Actors[10] = new(10, 0x1EAB00, new(x, 0, 315), EventAnimation: 0x00040008); Scan(f);
                var timestamp = f.Start.AddSeconds(-.3); f.Engine.Animation(10, 0x00040008, timestamp); f.Render();
                var light = f.Engine.AmbientAoes().Single();
                Compare(light.Shape, light.Position, light.Rotation, new BossMod.AOEShapeRect(60, 20), new(0, 0, 315),
                    x == 10 ? MathF.PI / 2 : -MathF.PI / 2);
                Check(light.Activation == timestamp.AddSeconds(8), "Light clock shifted.");
                f.Engine.Effect(20, 15500, 1, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Old animation revived light.");
                f.Engine.Animation(10, 0x00040008, timestamp.AddSeconds(10)); f.Render(); Check(f.Draw.Previews.Count == 1, "Next animation ignored.");
            });
        run("Qitana Towerfall birth clock restored cast handoff natural finish and next pillars", () =>
        {
            using var f = Prepare(0x27B0); var position = f.Actors[1].Position;
            f.Engine.ObjectCreated(10, f.Start.AddSeconds(-.5)); f.Actors[10] = new(10, 0x1EAACD, position, Rotation: .6f); Scan(f);
            var preliminary = f.Engine.AmbientAoes().Single();
            Check(preliminary.Activation == f.Start.AddSeconds(11.5), "Pillar birth clock shifted.");
            Compare(preliminary.Shape, preliminary.Position, preliminary.Rotation, new BossMod.AOEShapeCone(15, new(MathF.PI / 12)), position, .6f);
            var exact = position + Vector3.UnitX * .3f;
            f.Actors[20] = new(20, 0x233C, position, Cast: new(15512, exact, -.7f, f.Now, f.Now.AddSeconds(3.3))); Scan(f);
            var aoe = f.Engine.AmbientAoes().Single();
            Check(aoe.Position == exact && aoe.Rotation == -.7f, "Restored cast did not replace preliminary tower.");
            f.Engine.Finish(20, completed: true); Scan(f); Check(f.Draw.Previews.Count == 0, "Finished tower was restored.");
            f.Engine.ObjectCreated(11, f.Start.AddSeconds(5)); f.Actors[11] = new(11, 0x1EAACD, position); Scan(f);
            Check(f.Draw.Previews.Count == 1, "Next pillar generation lost.");
            f.Engine.RemoveActor(11); f.Render(); Check(f.Draw.Previews.Count == 0, "Removed pillar leaked.");
        });
        run("Qitana Towerfall cancellation and simultaneous group finish", () =>
        {
            using var f = Prepare(0x27B0); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x1EAACD, center); f.Actors[11] = new(11, 0x1EAACD, center + Vector3.UnitX * 5);
            f.Actors[20] = new(20, 0x233C, center); f.Actors[21] = new(21, 0x233C, center + Vector3.UnitX * 5); Scan(f);
            Cast(f, 20, 15512, 3, .5f); Cast(f, 21, 15512, 3, -.5f); Check(f.Engine.AmbientAoes().Count() == 2, "Handoff duplicated.");
            f.Engine.Finish(20); f.Render(); Check(f.Engine.AmbientAoes().Count() == 1, "Cancellation removed unrelated tower.");
            f.Engine.Finish(21, completed: true); f.Engine.Finish(21, completed: true); Scan(f); Check(f.Draw.Previews.Count == 0, "Group finish leaked.");
        });
        run("Qitana Eros dual tethers deadline good bad changes perspective and no immunity bypass", () =>
        {
            using var f = Prepare(0x27B1); var timestamp = f.Start.AddSeconds(-.2);
            f.Engine.Tether(100, 57, true, timestamp, 1); f.Engine.Tether(1, 57, true, timestamp, 101); f.Render();
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("qitana/hound/") && a.Display) == 2, "Dual endpoint tethers missing.");
            var avoid = f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("qitana/hound/avoid"));
            Check(avoid.Activation == timestamp.AddSeconds(5.2) && ((CircleTemplate)avoid.Shape).Radius == 19, "Distance/deadline differs.");
            f.Engine.Tether(100, 57, false, timestamp.AddSeconds(1), 1); f.Engine.Tether(100, 1, true, timestamp.AddSeconds(1), 1); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "qitana/hound/1/100").Activation == avoid.Activation, "Good/bad change restarted clock.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(30) } }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("qitana/hound/avoid")), "Tether incorrectly accepted immunity.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("qitana/hound/avoid")), "Personal tether missing after perspective.");
            f.Engine.Tether(1, 57, false, timestamp.AddSeconds(2), 101); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("qitana/hound/avoid")), "Cancelled personal tether remained.");
        });
        run("Qitana Eros stretch nineteen actual solver and effect cleanup", () =>
        {
            using var f = Prepare(0x27B1); var center = f.Actors[1].Position;
            f.Actors[1] = f.Actors[1] with { Position = center - Vector3.UnitZ * 12 };
            f.Actors[100] = f.Actors[100] with { Position = center - Vector3.UnitZ * 5 }; Scan(f);
            f.Engine.Tether(100, 57, true, f.Now, 1); f.Render(); Solve(f, p => Vector3.Distance(p, f.Actors[1].Position) >= 19);
            f.Engine.Effect(1, 17079, 1, f.Now, 100); Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Resolved tether leaked.");
        });
        run("Qitana Eros poison effect plus point eight object handoff and state seven", () =>
        {
            using var f = Prepare(0x27B1); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, center); Scan(f); Cast(f, 10, 15518, 6);
            f.Engine.Finish(10, completed: true); f.Engine.Effect(10, 15518, 1, f.Now, location: center, occurredAt: f.Start.AddSeconds(-.2)); f.Render();
            Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(.6), "Poison effect delay differs.");
            f.Actors[20] = new(20, 0x1E972C, center + Vector3.UnitX); f.Actors[10] = f.Actors[10] with { Cast = null }; Scan(f);
            Check(f.Engine.AmbientAoes().Count() == 1 && f.Engine.AmbientAoes().Single().Activation == null, "Object handoff duplicated.");
            f.Engine.EventState(20, 7); f.Render(); Check(f.Draw.Previews.Count == 0, "Retired poison remained.");
        });
        run("Qitana Eros personal bait edges dynamic targets and actual solver", () =>
        {
            using var f = Prepare(0x27B1); var center = f.Actors[1].Position;
            f.Actors[101] = f.Actors[101] with { Position = center - Vector3.UnitX * 8 }; f.Actors[10] = new(10, 0x233C, center); Scan(f);
            Cast(f, 10, 15517, 6, target: 100);
            Solve(f, p => MathF.Abs(p.X - 17) > 13 && Vector3.Distance(p, f.Actors[101].Position) > 6);
            f.PlayerId = 101; Scan(f); Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("qitana/bait/edge")), "Other bait forced personal edge.");
            var old = f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("party/display")).Position;
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 5 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("party/display")).Position != old, "Bait did not follow target.");
        });
        run("Qitana Eros forced four stack solver and spread separation", () =>
        {
            using var f = Prepare(0x27B1); var center = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = center - Vector3.UnitZ * 10 };
            f.Actors[101] = f.Actors[101] with { Position = center + Vector3.UnitZ * 8 }; f.Actors[10] = new(10, 0x233C, center); Scan(f);
            Cast(f, 10, 15525, 6, target: 101); Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) < 6);
            f.Engine.Finish(10); f.Actors[10] = f.Actors[10] with { Cast = null }; f.Now = f.Start.AddSeconds(7); Cast(f, 10, 15523, 6, target: 100);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("party/separate")), "Spread missed separation.");
        });
        foreach (var pull in new[] { false, true })
            run($"Qitana Eros {(pull ? "pull" : "forward wall knockback")} all poison lanes landing and immunity expiry", () =>
            {
                using var f = Prepare(0x27B1); var center = f.Actors[1].Position;
                f.Actors[1] = f.Actors[1] with { Position = new(17, 0, -552), Rotation = .4f };
                f.Actors[10] = new(10, 0x1E972C, new(7, 0, -535)); f.Actors[11] = new(11, 0x1E972C, new(27, 0, -535)); Scan(f);
                Cast(f, 1, pull ? 17168u : 15520u, 4, .4f); var aoes = f.Engine.AmbientAoes().ToArray();
                Check(aoes.Count(a => a.Id.StartsWith("qitana/displacement/lane")) == 2, "Only first poison contributed a lane.");
                foreach (var point in new[] { new Vector3(7, 0, -535), new Vector3(27, 0, -535) })
                    Check(aoes.Where(a => a.Id.StartsWith("qitana/displacement/lane")).Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(point)),
                        "A poison lane omitted.");
                var template = (QitanaDisplacementTemplate)aoes.Single(a => a.Id == "qitana/displacement/1").Shape; var landing = template.Landing(center);
                Check(pull ? Vector3.Distance(landing, f.Actors[1].Position) < .001f : f.Engine.Arena!.Contains(landing, 0), "Displacement/wall clamp differs.");
                f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(10) } }; Scan(f);
                Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("qitana/displacement")), "Immune player received displacement.");
                f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(1) } }; Scan(f);
                Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("qitana/displacement")), "Expiring immunity suppressed displacement.");
            });
        run("Qitana Eros forward safe landing actual temporal solver", () =>
        {
            using var f = Prepare(0x27B1); f.Actors[100] = f.Actors[100] with { Position = new(9, 0, -548) };
            f.Actors[10] = new(10, 0x1E972C, new(9, 0, -518.5f)); Scan(f); Cast(f, 1, 15520, 4);
            var template = (QitanaDisplacementTemplate)f.Engine.AmbientAoes().Single(a => a.Id == "qitana/displacement/1").Shape;
            Check(template.Resolve(Vector3.Zero, 0, null).Contains(f.Actors[100].Position), "Unsafe initial landing missed.");
            Solve(f, p => !template.Dangers.Any(d => d.Contains(template.Landing(p))));
        });
        run("Qitana delayed tankbuster effect cleanup and encounter reset", () =>
        {
            using var f = Prepare(0x27AF); Cast(f, 1, 15497, 4, target: 100); f.Engine.Finish(1, completed: true);
            Check(f.Engine.PredictedDamage.Count() == 1, "Delayable buster lost before actual damage.");
            f.Engine.Effect(1, 15497, 1, f.Now); Check(!f.Engine.PredictedDamage.Any(), "Actual buster retained.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Reset leaked.");
        });
    }
    private static Fight Prepare(uint boss, bool firstRoom = true)
    {
        var f = new Fight(boss, 823); f.Engine.ResetFight();
        var seed = boss == 0x2A40 && firstRoom ? new Vector3(0, 0, 640) : Vector3.Zero;
        var center = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(seed).Position;
        f.Actors[1] = new(1, boss, center, InCombat: true); f.Actors[100] = new(100, 0, center, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate shape, Vector3 origin, float rotation, BossMod.AOEShape source, Vector3 sourceOrigin, float sourceRotation)
    {
        var geometry = shape.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(geometry);
        for (var x = -65f; x <= 65; x += .73f)
            for (var z = -65f; z <= 65; z += .73f)
            {
                var point = origin + new Vector3(x, 0, z);
                var expected = source.Check(new(point.X, point.Z), new(sourceOrigin.X, sourceOrigin.Z), new(sourceRotation));
                Check(geometry.Contains(point) == expected && world.Contains(point) == expected, "Source shape/heading differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position),
            $"Real solver failed native constraint: {solution.Status}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

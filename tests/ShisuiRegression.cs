using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Stormblood.Dungeon.D02ShisuiOfTheVioletTides;

internal static class ShisuiRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (type, factory) in new (Type, Func<float, IGeometryShape>)[]
        {
            (typeof(Source.D021Amikiri.D021Amikiri), ShisuiArenas.Amikiri),
            (typeof(Source.D022RubyPrincess.D022RubyPrincess), ShisuiArenas.RubyPrincess),
            (typeof(Source.D023ShisuiYohi.D023ShisuiYohi), ShisuiArenas.Yohi),
        })
            run($"Shisui exact {type.Name} arena matches source in both views", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var native = factory(0); var host = HostDrawingGeometry.Convert(native); var bounds = native.GetBounds();
                for (var x = bounds.MinX + .123f; x < bounds.MaxX; x += .47f)
                    for (var z = bounds.MinZ + .241f; z < bounds.MaxZ; z += .47f)
                    {
                        var p = new Vector3(x, 0, z); var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Arena differs at {p}.");
                    }
            });
        run("Shisui current client territory owns three source bosses and keeps other territories separate", () =>
        {
            foreach (var oid in new uint[] { 0x1B0C, 0x1B0E, 0x1B0F })
                Check(ArrDungeonCatalog.Owns(oid, 1367) && !ArrDungeonCatalog.Owns(oid, 1142), "Wrong territory.");
        });
        run("Shisui Amikiri leg and add priority matches source", () =>
        {
            using var f = Prepare(0x1B0C);
            f.Actors[10] = new(10, 0x1B0D, Center(0x1B0C));
            f.Actors[11] = new(11, 0x1B2A, Center(0x1B0C)); Scan(f);
            Check(f.Engine.PriorityTarget(10, Center(0x1B0C)) == 11, "Leg priority missing.");
        });
        run("Shisui Amikiri bind bait separates party then new bind object clears bait", () =>
        {
            using var f = Prepare(0x1B0C);
            f.Actors[101] = new(101, 0, Center(0x1B0C) + Vector3.UnitX * 8, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(100, 1); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[101].Position), "Self bait failed to avoid party.");
            f.Actors[20] = new(20, 0x1EA0D7, f.Actors[100].Position); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().All(a => !a.Id.StartsWith("shisui/bind")), "New bind object did not replace bait.");
            f.Engine.EventState(20, 7); f.Render(); Check(f.Draw.Previews.Count == 0, "State-seven bind zone retained.");
        });
        run("Shisui Amikiri digestive icon event clock edge bait and add death cancellation", () =>
        {
            using var f = Prepare(0x1B0C);
            f.Actors[10] = new(10, 0x1B0D, Center(0x1B0C)); Scan(f);
            f.Engine.Icon(100, 14, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "shisui/digest/edge")
                && f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 5 }, "Digest edge bait missing.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "All adds dead did not cancel digest.");
        });
        run("Shisui Amikiri digest effect plus point-seven locks pool until actual object handoff", () =>
        {
            using var f = Prepare(0x1B0C);
            f.Actors[10] = new(10, 0x18D6, Center(0x1B0C));
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1B0C) + Vector3.UnitX * 10 }; Scan(f);
            f.Now = f.Start.AddSeconds(.2);
            f.Engine.Effect(10, 8057, 1, f.Now, 100, occurredAt: f.Start); f.Render();
            var pool = f.Engine.AmbientAoes().Single();
            Check(pool.Activation == f.Start.AddSeconds(.7) && pool.Position == f.Actors[100].Position, "Pool event time or target location missing.");
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1B0C) }; Scan(f);
            Check(f.Engine.AmbientAoes().Single().Position == pool.Position, "Resolved pool followed target.");
            f.Actors[20] = new(20, 0x1E950D, pool.Position + Vector3.UnitX); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().All(a => !a.Id.Contains("predicted")), "Pool was duplicated instead of handed off.");
        });
        run("Shisui Ruby chest NameId excludes ordinary helpers and open animation uses five-yard square", () =>
        {
            using var f = Prepare(0x1B0E);
            var p = Center(0x1B0E) + Vector3.UnitX * 15;
            f.Actors[10] = new(10, 0x18D6, p, NameId: 6274);
            f.Actors[11] = new(11, 0x18D6, p - Vector3.UnitX * 10, NameId: 999);
            f.Actors[20] = new(20, 0x1EA001, p + new Vector3(4.9f, 0, 4.9f)); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Chest NameId filter missing.");
            Cast(f, 1, 8058, 7);
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Engine.AmbientAoes().Any(a => a.Id == "shisui/chest/enter"), "Seduce chest is optional goal.");
            f.Engine.Animation(20, 0x00040008); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "shisui/chest/10" && !a.Safe), "Open chest square position matching failed.");
            f.Engine.Animation(20, 0x00100020); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "shisui/chest/10" && a.Safe), "Chest close state failed.");
        });
        run("Shisui Ruby Old status changes chest complement to avoidance and viewer switch rebuilds", () =>
        {
            using var f = Prepare(0x1B0E);
            f.Actors[10] = new(10, 0x18D6, Center(0x1B0E) + Vector3.UnitX * 15, NameId: 6274);
            f.Actors[101] = new(101, 0, Center(0x1B0E), PartySlot: 1, Hostile: false); Scan(f); Cast(f, 1, 8058, 7);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 1259, Timestamp = f.Now }, f.Now); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "shisui/chest/enter")
                && f.Engine.AmbientAoes().Any(a => a.Id == "shisui/chest/10" && a.Navigate && !a.Safe), "Old player did not avoid chest.");
            f.PlayerId = 101; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "shisui/chest/enter"), "Other viewer inherited Old.");
        });
        run("Shisui Ruby restored chest animation is applied before first Seduce output", () =>
        {
            using var f = Prepare(0x1B0E);
            f.Actors[10] = new(10, 0x18D6, Center(0x1B0E) + Vector3.UnitX * 15, NameId: 6274, EventAnimation: 0x00040008);
            f.Actors[1] = f.Actors[1] with { Cast = new(8058, Center(0x1B0E), 0, f.Now, f.Now.AddSeconds(7.3)) }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "shisui/chest/enter"), "Restored open chest was offered for transformation.");
        });
        run("Shisui Ruby delayed kick survives natural Seduce finish then real kick corrects clock and origin", () =>
        {
            using var f = Prepare(0x1B0E); Cast(f, 1, 8058, 7);
            var predicted = f.Engine.AmbientAoes().Single(a => a.Id == "shisui/kick");
            Check(predicted.Activation is { } at && Math.Abs((at - f.Start.AddSeconds(15)).TotalMilliseconds) < .001, "Seduce kick should finish plus eight seconds.");
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 1, "Natural Seduce finish removed delayed kick.");
            f.Now = f.Start.AddSeconds(9); f.Actors[1] = f.Actors[1] with { Position = Center(0x1B0E) + Vector3.UnitX }; Scan(f); Cast(f, 1, 8059, 5);
            var kick = f.Engine.AmbientAoes().Single(a => a.Id == "shisui/kick");
            Check(kick.Position == f.Actors[1].Position && kick.Activation == f.Now.AddSeconds(5.3), "Real kick did not correct prediction.");
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0, "Real kick finish retained danger.");
        });
        run("Shisui Ruby cancelled Seduce clears chest demand and delayed kick", () =>
        {
            using var f = Prepare(0x1B0E);
            f.Actors[10] = new(10, 0x18D6, Center(0x1B0E) + Vector3.UnitX * 15, NameId: 6274); Scan(f); Cast(f, 1, 8058, 7);
            f.Engine.Finish(1); f.Render();
            Check(f.Engine.AmbientAoes().All(a => a.Id is not ("shisui/kick" or "shisui/chest/enter")), "Cancelled Seduce leaked stage.");
        });
        run("Shisui real solver enters closed chest before delayed kick", () =>
        {
            using var f = Prepare(0x1B0E);
            f.Actors[10] = new(10, 0x18D6, Center(0x1B0E) + Vector3.UnitX * 16, NameId: 6274); Scan(f); Cast(f, 1, 8058, 7);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(Center(0x1B0E), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && Vector3.Distance(path[^1].Position, f.Actors[10].Position) <= 2.5f, "Chest solver failed strong transformation constraint.");
        });
        run("Shisui Ruby ten chasing hits advance only from effects with point-eight deadlines", () =>
        {
            using var f = Prepare(0x1B0E);
            f.Actors[10] = new(10, 0x18D6, Center(0x1B0E));
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1B0E) + Vector3.UnitX * 10 }; Scan(f);
            f.Engine.Icon(100, 1, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(5.1), "Precast chase icon time missing.");
            Cast(f, 10, 9431, 3.8);
            var first = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(Vector3.Distance(first.Position, Center(0x1B0E)) < .1f, "Initial chase moved before first hit.");
            for (uint i = 0; i < 10; ++i)
            {
                f.Now = f.Start.AddSeconds(4 + i * .8);
                f.Engine.Effect(10, i == 0 ? 9431u : 8061u, i + 1, f.Now, 10, f.Actors[10].Position, f.Now);
                f.Render();
                var chase = f.Engine.AmbientAoes().FirstOrDefault(a => a.Id.EndsWith("/display"));
                if (i == 9) Check(chase == null && f.Snapshot.DangerAreas.Count == 0, "Tenth chase hit did not finish.");
                else
                {
                    Check(chase != null && chase.Name.Contains($"剩余{9 - i}"), "Chase count skipped.");
                    var expected = Center(0x1B0E) + Vector3.UnitX * 3;
                    Check(Vector3.Distance(chase!.Position, expected) < .1f && chase.Activation == f.Now.AddSeconds(.8), "Chase movement cap or clock differs.");
                    f.Engine.Effect(10, i == 0 ? 9431u : 8061u, i + 1, f.Now, 10, f.Actors[10].Position, f.Now); f.Render();
                    Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display")).Name == chase.Name, "Duplicate chase packet advanced twice.");
                }
            }
        });
        run("Shisui Ruby chase separates self avoidance radius from display and cancels initial cast", () =>
        {
            using var f = Prepare(0x1B0E); f.Actors[10] = new(10, 0x18D6, Center(0x1B0E)); Scan(f);
            f.Engine.Icon(100, 1); Cast(f, 10, 9431, 3.8);
            var avoid = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/avoid"));
            var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(display.Shape.Resolve(display.Position, 0, null) is CircleGeometry { Radius: 4 }
                && avoid.Shape.Resolve(avoid.Position, 0, null) is CircleGeometry { Radius: 7 }, "Chase self movement hack/display radius changed.");
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled initial chase persisted.");
        });
        run("Shisui Yohi tenth splash publishes moving helper black tide and actual effect resets", () =>
        {
            using var f = Prepare(0x1B0F); f.Actors[10] = new(10, 0x18D6, Center(0x1B0F)); Scan(f);
            for (uint i = 1; i <= 9; ++i) f.Engine.Effect(10, 8067, i, f.Now);
            f.Render(); Check(f.Draw.Previews.Count == 0, "BlackTide predicted before ten splashes.");
            f.Engine.Effect(10, 8067, 10, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(4), "Tenth splash prediction missing.");
            f.Actors[10] = f.Actors[10] with { Position = Center(0x1B0F) + Vector3.UnitX * 4 }; Scan(f);
            Check(Vector3.Distance(f.Engine.AmbientAoes().Single().Position, f.Actors[10].Position) < .1f, "BlackTide did not follow helper.");
            f.Engine.Effect(1, 8065, 11, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "BlackTide actual effect did not clear.");
        });
        foreach (var (caster, action) in new[] { (0x1B10u, 8069u), (0x1C87u, 8070u) })
            run($"Shisui Yohi {action} charge follows target and changes personal avoidance", () =>
            {
                using var f = Prepare(0x1B0F);
                f.Actors[10] = new(10, caster, Center(0x1B0F) - Vector3.UnitX * 10);
                f.Actors[101] = new(101, 0, Center(0x1B0F), PartySlot: 1, Hostile: false); Scan(f); Cast(f, 10, action, 15, 101);
                Check(f.Snapshot.DangerAreas.Count == 1 && f.Engine.PredictedDamage.Any(p => p.TargetId == 101), "Charge danger/buster prediction missing.");
                f.Actors[101] = f.Actors[101] with { Position = Center(0x1B0F) + Vector3.UnitZ * 6 }; Scan(f);
                var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
                Check(display.Shape.Resolve(display.Position, display.Rotation, null).Contains(f.Actors[101].Position), "Charge did not follow target.");
                f.PlayerId = 101; Scan(f);
                Check(f.Engine.AmbientAoes().Any(a => a.Id.Contains("/party/100")) && !f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")),
                    "Charge target did not avoid clipping teammates.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Charge cancellation leaked.");
            });
        run("Shisui Yohi gaze uses PR facing reaction window and stop resets output", () =>
        {
            using var f = Prepare(0x1B0F); f.Actors[100] = f.Actors[100] with { Position = Center(0x1B0F) + Vector3.UnitX * 5 }; Scan(f);
            Cast(f, 1, 8066, 5);
            Check(f.Engine.GazeFacing(f.Actors[100].Position, -MathF.PI / 2) == null, "Gaze reacted too early.");
            f.Now = f.Start.AddSeconds(4.6); f.Engine.Tick(f.Now);
            Check(f.Engine.GazeFacing(f.Actors[100].Position, -MathF.PI / 2) is { } face && MathF.Sin(face) > 0, "Gaze PR face-away missing.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stop leaked.");
        });
    }

    private static Vector3 Center(uint boss) => boss switch { 0x1B0C => new(0, 0, 69.221f), 0x1B0E => new(-.046f, 0, -208.362f), _ => new(0, 0, -433) };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1367); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, 0, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

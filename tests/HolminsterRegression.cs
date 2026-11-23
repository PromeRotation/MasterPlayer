using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class HolminsterRegression
{
    internal static void Run(Action<string, Action> run)
    {
        FollowRegression.RunHolminsterControls(run);
        foreach (var (boss, typeName) in new[]
        {
            (0x278Au, "D01Holminster.D011ForgivenDissonance.D011ForgivenDissonance"),
            (0x278Bu, "D01Holminster.D012TesleentheForgiven.D012TesleentheForgiven"),
            (0x278Cu, "D01Holmintser.D013Philia.D013Philia"),
        })
            run($"Holminster {boss:X} source polygon both views and exact territory ownership", () =>
            {
                using var f = Prepare(boss); var native = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(native);
                var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Shadowbringers.Dungeon." + typeName)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                for (var x = -21f; x < 21; x += .37f)
                    for (var z = -21f; z < 21; z += .37f)
                    {
                        var p = new Vector3(source.Center.X + x, 0, source.Center.Z + z);
                        var expected = source.Contains(new(x, z));
                        Check(native.Contains(p) == expected && world.Contains(p) == expected, "Source arena differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 837) && !ArrDungeonCatalog.Owns(boss, 1174), "Ownership differs.");
            });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x278A, 0x278A, 15816, new BossMod.AOEShapeCircle(8)),
            (0x278A, 0x2896, 15822, new BossMod.AOEShapeCross(40, 3)),
            (0x278A, 0x2896, 15819, new BossMod.AOEShapeRect(40, 2)),
            (0x278A, 0x278A, 15815, new BossMod.AOEShapeCone(40, new(MathF.PI / 4))),
            (0x278C, 0x233C, 16778, new BossMod.AOEShapeCircle(15)),
            (0x278C, 0x278C, 15846, new BossMod.AOEShapeCone(24, new(MathF.PI * 7 / 12))),
            (0x278C, 0x278C, 15847, new BossMod.AOEShapeCone(24, new(MathF.PI * 7 / 12))),
        })
            run($"Holminster source action {action} packet heading both views cancellation", () =>
            {
                using var f = Prepare(boss); var center = f.Actors[1].Position; const float angle = .619f;
                f.Actors[10] = new(10, oid, center, Rotation: -1); Scan(f); Cast(f, 10, action, 5, angle);
                var c = f.Engine.Casts[10]; var native = c.Rule.Shape!.Resolve(c.Location, c.Rotation, null); var world = HostDrawingGeometry.Convert(native);
                for (var x = -41f; x <= 41; x += .79f)
                    for (var z = -41f; z <= 41; z += .79f)
                    {
                        var p = center + new Vector3(x, 0, z);
                        var expected = shape.Check(new(p.X, p.Z), new(center.X, center.Z), new(angle));
                        Check(native.Contains(p) == expected && world.Contains(p) == expected, "Shape dimensions/heading differ.");
                    }
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled cast remained.");
            });
        run("Holminster Thumbscrew locks true segment start and endpoint during charge", () =>
        {
            using var f = Prepare(0x278A); var center = f.Actors[1].Position;
            Cast(f, 1, 15814, 4.8, location: center + Vector3.UnitX * 15);
            var initial = f.Engine.AmbientAoes().Single();
            f.Actors[1] = f.Actors[1] with { Position = center + Vector3.UnitX * 10 }; Scan(f);
            var current = f.Engine.AmbientAoes().Single();
            var native = current.Shape.Resolve(current.Position, current.Rotation, null);
            Check(current.Position == initial.Position && native.Contains(center + new Vector3(10, 0, 3.9f))
                && !native.Contains(center + new Vector3(15.1f, 0, 0))
                && HostDrawingGeometry.Convert(native).Contains(center + new Vector3(2, 0, 3.9f)), "Charge geometry followed caster.");
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0, "Charge finish retained.");
        });
        run("Holminster four charge targets use original icon order dynamic lengths FIFO and dead target removal", () =>
        {
            using var f = Prepare(0x278B); var center = f.Actors[1].Position;
            for (var i = 0; i < 4; ++i)
                f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, center + new Vector3(10, 0, i * 4), PartySlot: i, Hostile: false);
            Scan(f);
            for (var i = 0; i < 4; ++i) f.Engine.Icon((ulong)(100 + i), (uint)(82 - i), f.Start);
            f.Engine.Icon(100, 82, f.Start); f.Render();
            var displays = f.Engine.AmbientAoes().Where(a => a.Id.EndsWith("/display")).ToArray();
            Check(displays.Length == 4 && displays.Select((a, i) =>
                Math.Abs((a.Activation!.Value - f.Start.AddSeconds(8.3 + .3 * i)).TotalMilliseconds) < 1).All(v => v), "Icon order/clock/duplicate differs.");
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 16 }; Scan(f);
            Check(((RayRectangleTemplate)f.Engine.AmbientAoes().Single(a => a.Id == "holminster/flagellation/100/display").Shape).Length == 16,
                "Bait length did not track target.");
            f.Engine.Effect(1, 15830, 1, f.Now); f.Engine.Effect(1, 15830, 1, f.Now); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("holminster/flagellation/100/"))
                && f.Engine.AmbientAoes().Count(a => a.Id.EndsWith("/display")) == 3, "Actual FIFO duplicate advanced twice.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
            Check(f.Engine.AmbientAoes().Count(a => a.Id.EndsWith("/display")) == 2, "Dead charge target remained.");
            f.Engine.Effect(1, 15830, 2, f.Now); f.Engine.Effect(1, 15830, 3, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0, "Charge sequence retained.");
        });
        run("Holminster Exorcise stack source clock true solver and actual effect clear", () =>
        {
            using var f = Prepare(0x278B); f.Engine.Icon(101, 62, f.Start.AddSeconds(-.2)); f.Render();
            var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(Math.Abs((display.Activation!.Value - f.Start.AddSeconds(4.9)).TotalMilliseconds) < 1, "Stack clock shifted.");
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) < 6);
            f.Engine.Effect(1, 15827, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Exorcise effect retained.");
        });
        run("Holminster HolyWater cast effect spawn handoff uses original point eight activation and inactive cleanup", () =>
        {
            using var f = Prepare(0x278B); var location = f.Actors[1].Position + Vector3.UnitX * 8;
            f.Actors[10] = new(10, 0x233C, location); Scan(f); Cast(f, 10, 15828, 7);
            f.Engine.Effect(10, 15828, 1, f.Now, location: location, occurredAt: f.Start.AddSeconds(7));
            f.Engine.Finish(10, completed: true); f.Render();
            var prediction = f.Engine.AmbientAoes().Single();
            Check(Math.Abs((prediction.Activation!.Value - f.Start.AddSeconds(7.8)).TotalMilliseconds) < 1, "Water postcast clock shifted.");
            f.Actors[11] = new(11, 0x1EABF9, location); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().Single().Id == "holminster/pool/11", "Water spawn duplicated forecast.");
            f.Engine.EventState(11, 7); f.Render(); Check(f.Draw.Previews.Count == 0, "Inactive water remained.");
        });
        run("Holminster Fetters victim perspective ban rescue true solver aethersup four actual effects and stale status", () =>
        {
            using var f = Prepare(0x278C); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x2895, center + Vector3.UnitZ * 8); Scan(f);
            f.Engine.Icon(101, 92, f.Start); Cast(f, 1, 17052, 5);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 101, StatusId = 1849, Timestamp = f.Start }, f.Now);
            Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]) && f.Engine.PriorityTarget(1, center) == 10, "Rescuer ban/priority missing.");
            f.Actors[101] = f.Actors[101] with { Statuses = [1849] }; f.Engine.Finish(1, completed: true);
            f.Now = f.Start.AddSeconds(6); Cast(f, 1, 15848, 15);
            Check(!f.Engine.AmbientAoes().Single(a => a.Id == "holminster/aethersup").Navigate, "Aethersup prevented rescue.");
            Solve(f, p => Vector3.Distance(p, f.Actors[10].Position) < 3.6f);
            f.PlayerId = 101; Scan(f); Check(f.Engine.ForbiddenTargets.Length == 0, "Victim inherited rescuer attack ban.");
            f.PlayerId = 100; f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "holminster/aethersup").Navigate, "Dead chain suppressed aethersup navigation.");
            f.Engine.Finish(1, completed: true); f.Render();
            for (var i = 0; i < 4; ++i)
            {
                f.Engine.Effect(i == 0 ? 1UL : 20UL, i == 0 ? 15848u : 15849u, (uint)i, f.Now);
                f.Render(); Check(f.Draw.Previews.Count == (i < 3 ? 1 : 0), "Aethersup not exactly four hits.");
            }
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 101, StatusId = 1849, Timestamp = f.Now }, f.Now);
            Scan(f); Check(f.Engine.ForbiddenTargets.Length == 0, "Stale fetter status rearmed ban.");
        });
        run("Holminster Pendulum R20 flare target edge requirement other viewer and resolve", () =>
        {
            using var f = Prepare(0x278C); var center = new Vector3(134, 0, -465);
            f.Engine.Icon(100, 87, f.Start); f.Render();
            var edge = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/edge"));
            Check(((CircleTemplate)edge.Shape).Radius == 18.5f
                && Math.Abs((edge.Activation!.Value - f.Start.AddSeconds(5.1)).TotalMilliseconds) < 1, "Flare edge/time differs.");
            var marker = f.Engine.AmbientAoes().Single(a => a.Id == "holminster/flare/100");
            Check(((CircleTemplate)marker.Shape).Radius == 20 && !marker.Navigate, "Marked flare must not flee itself.");
            f.PlayerId = 101; Scan(f); Check(!f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/edge"))
                && f.Engine.AmbientAoes().Single(a => a.Id == "holminster/flare/100").Navigate, "Other perspective flare differs.");
            f.Engine.Effect(1, 16790, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Flare hit retained.");
        });
        run("Holminster IntoTheLight marker original five point three true line stack solver and resolve", () =>
        {
            using var f = Prepare(0x278C); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, center - Vector3.UnitX * 8);
            f.Actors[100] = f.Actors[100] with { Position = center - Vector3.UnitZ * 10 }; Scan(f);
            f.Engine.Effect(10, 15844, 1, f.Now, target: 101, occurredAt: f.Start.AddSeconds(-.2)); f.Render();
            var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(Math.Abs((display.Activation!.Value - f.Start.AddSeconds(5.1)).TotalMilliseconds) < 1, "Line stack clock shifted.");
            var shape = display.Shape.Resolve(display.Position, display.Rotation, null);
            Solve(f, p => shape.Contains(p));
            f.Engine.Effect(1, 15845, 2, f.Now, target: 101); f.Render(); Check(f.Draw.Previews.Count == 0, "Line stack resolve retained.");
        });
        run("Holminster eight tail sweeps advance only natural helper finish preserve first visual and cancel", () =>
        {
            using var f = Prepare(0x278C); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, center); Scan(f); Cast(f, 1, 15834, 5, .4f);
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 2, "Visual finish erased rotation.");
            f.Engine.Effect(10, 15841, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 2, "Effect unexpectedly advanced finish-based sequence.");
            for (var i = 0; i < 8; ++i)
            {
                f.Now = f.Start.AddSeconds(6 + 2 * i); Cast(f, 10, 15841, 2); f.Engine.Finish(10, completed: true); f.Render();
                var aoes = f.Engine.AmbientAoes().ToArray();
                Check(aoes.Length == Math.Min(2, 7 - i), "Tail sweep count differs.");
                if (i < 7) Check(Math.Abs(aoes[0].Rotation - (.4f + MathF.PI - (i + 1) * MathF.PI / 4)) < .00001f
                    && Math.Abs((aoes[0].Activation!.Value - f.Now.AddSeconds(2)).TotalMilliseconds) < 1, "Tail heading/clock differs.");
            }
            f.Now = f.Start.AddSeconds(30); Cast(f, 1, 15834, 5); f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled rotation retained.");
        });
        run("Holminster seven hit exaflare positions three shown original effects and next group prediction", () =>
        {
            using var f = Prepare(0x278C); var center = new Vector3(134, 0, -465); var origin = center + Vector3.UnitX * 10;
            f.Actors[10] = new(10, 0x233C, origin, Rotation: MathF.PI); Scan(f); Cast(f, 10, 15837, 5, MathF.PI);
            var initial = f.Engine.AmbientAoes().ToArray();
            Check(initial.Length == 4 && initial.Count(a => a.Id.Contains("/prediction/")) == 1, "Exaflare initial cap/prediction differs.");
            var next = initial.Single(a => a.Id.Contains("/prediction/"));
            var sourcePoint = BossMod.WPos.RotateAroundOrigin(45, new(134, -465),
                new(Quantized(origin).X, Quantized(origin).Z));
            Check(Vector3.Distance(next.Position, new(sourcePoint.X, 0, sourcePoint.Z)) < .001f
                && Math.Abs((next.Activation!.Value - f.Start.AddSeconds(3.7)).TotalMilliseconds) < 1, "Next group prediction position/time differs.");
            f.Engine.Finish(10, completed: true); f.Render(); Check(f.Draw.Previews.Count == 4, "Natural first cast erased exaflare.");
            var position = origin;
            for (var i = 0; i < 7; ++i)
            {
                f.Now = f.Start.AddSeconds(5 + i);
                f.Engine.Effect(10, i == 0 ? 15837u : 15839u, (uint)i, f.Now, location: position, occurredAt: f.Now.AddSeconds(-.1));
                f.Render();
                var aoes = f.Engine.AmbientAoes().Where(a => !a.Id.Contains("/prediction/")).ToArray();
                Check(aoes.Length == Math.Min(3, 6 - i), "Exaflare remaining count differs.");
                if (i < 6) Check(Vector3.Distance(aoes[0].Position, Quantized(position - Vector3.UnitZ * 2.5f)) < .001f
                    && Math.Abs((aoes[0].Activation!.Value - f.Now.AddSeconds(.9)).TotalMilliseconds) < 1, "Exaflare effect origin/clock differs.");
                position -= Vector3.UnitZ * 2.5f;
            }
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0, "Reset kept next group prediction.");
        });
        run("Holminster sixteen exaflare starts retire predictions two at a time reset cycle and cancel source", () =>
        {
            using var f = Prepare(0x278C); var center = new Vector3(134, 0, -465);
            for (var i = 1; i <= 16; ++i)
            {
                var id = (ulong)(10 + i); var angle = i * MathF.PI / 8;
                var origin = center + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * 15;
                f.Actors[id] = new(id, 0x233C, origin, Rotation: angle);
                Scan(f); f.Engine.Effect(id, 15838, (uint)i, f.Now, sourceRotation: angle); f.Render();
                var predictions = f.Engine.AmbientAoes().Count(a => a.Id.Contains("/prediction/"));
                Check(predictions == (i < 2 ? i : i <= 14 ? 2 : 16 - i), "Prediction queue did not close at start16.");
            }
            f.Actors[50] = new(50, 0x233C, center); Scan(f); Cast(f, 50, 15837, 5);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.Contains("/prediction/")), "Next sixteen-start cycle did not reset.");
            f.Engine.Finish(50); f.Render(); Check(!f.Engine.AmbientAoes().Any(a => a.Id.Contains("/prediction/")), "Cancelled source retained prediction.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 837); f.Engine.ResetFight();
        var center = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, center, InCombat: true); f.Actors[100] = new(100, 0, center, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static Vector3 Quantized(Vector3 p)
    {
        var source = new BossMod.WPos(p.X, p.Z).Quantized();
        return new(source.X, p.Y, source.Z);
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0, Vector3? location = null)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position), "Real solver failed native mechanic goal.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

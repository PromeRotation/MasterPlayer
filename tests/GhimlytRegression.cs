using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class GhimlytRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (boss, typeName, fieldName, native) in new (uint, string, string, Func<float, IGeometryShape>)[]
        {
            (0x2526, "D150ScholaColossus.D0150ScholaColossus", "arena", GhimlytArenas.Schola),
            (0x2528, "D150ScholaMarkIIColossus.D0150ScholaMarkIIColossus", "arena", GhimlytArenas.ScholaII),
            (0x25DA, "D151MarkIIIBMagitekColossus.D151MarkIIIBMagitekColossus", "arena", GhimlytArenas.Colossus),
            (0x2515, "D152Prometheus.D152Prometheus", "StartingArena", GhimlytArenas.PrometheusStarting),
            (0x2515, "D152Prometheus.D152Prometheus", "DefaultArena", GhimlytArenas.Prometheus),
            (0x25B0, "D153SoranusDuo.D153SoranusDuo", "arena", GhimlytArenas.Duo),
        })
            run($"Ghimlyt {typeName} {fieldName} source arena radar world and ownership", () =>
            {
                var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Stormblood.Dungeon.D15TheGhimlytDark." + typeName)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var geometry = native(0); var world = HostDrawingGeometry.Convert(geometry);
                for (var x = -40f; x <= 40; x += .47f)
                    for (var z = -40f; z <= 40; z += .47f)
                    {
                        var p = new Vector3(source.Center.X + x, 0, source.Center.Z + z);
                        var expected = source.Contains(new(x, z));
                        Check(geometry.Contains(p) == expected && world.Contains(p) == expected, "Source arena differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 1174) && !ArrDungeonCatalog.Owns(boss, 1173), "Territory ownership differs.");
            });
        run("Ghimlyt Schola spread and true 48 point two beam cancellation both views", () =>
        {
            using var f = Prepare(0x2526); var center = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, center); Scan(f);
            Cast(f, 10, 14468, 5, target: 100);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("party/separate")) && f.Draw.Previews.Count == 1, "Homing spread missing.");
            Cast(f, 1, 14965, 2.5, MathF.PI / 2);
            var cast = f.Engine.Casts[1]; var shape = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
            Check(shape.Contains(center + new Vector3(48.1f, 0, 1.9f)) && !shape.Contains(center + new Vector3(48.3f, 0, 0))
                && HostDrawingGeometry.Convert(shape).Contains(center + new Vector3(40, 0, 1.9f)), "GrandStrike packet heading/dimensions lost.");
            f.Engine.Finish(1); f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled Schola casts retained.");
        });
        foreach (var (boss, oid, action, expected) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x2528, 0x233C, 15042, new BossMod.AOEShapeCircle(15)),
            (0x2528, 0x2528, 14966, new BossMod.AOEShapeRect(43.2f, 5)),
            (0x2528, 0x2527, 14967, new BossMod.AOEShapeCone(18.4f, new(MathF.PI / 3))),
            (0x25DA, 0x25DA, 14192, new BossMod.AOEShapeRect(43.5f, 5)),
            (0x2515, 0x2515, 13402, new BossMod.AOEShapeCone(47.8f, new(MathF.PI / 4))),
            (0x2515, 0x2515, 13398, new BossMod.AOEShapeCone(47.8f, new(MathF.PI * .75f))),
            (0x2515, 0x233C, 13404, new BossMod.AOEShapeCircle(8)),
            (0x25B0, 0x25B0, 14124, new BossMod.AOEShapeRect(45.6f, 3)),
            (0x25B0, 0x25B5, 14131, new BossMod.AOEShapeRect(41, 2)),
            (0x25B0, 0x25B5, 14729, new BossMod.AOEShapeRect(26, 2)),
            (0x25B0, 0x25B0, 14098, new BossMod.AOEShapeCircle(6.6f)),
            (0x25B0, 0x233C, 14097, new BossMod.AOEShapeCircle(10)),
            (0x25B0, 0x233C, 14126, new BossMod.AOEShapeCircle(6)),
        })
            run($"Ghimlyt action {action} source shape packet heading both views and cancellation", () =>
            {
                using var f = Prepare(boss); var center = f.Engine.Arena!.Position; const float rotation = .713f;
                f.Actors[10] = new(10, oid, center, Rotation: -1.3f); Scan(f); Cast(f, 10, action, 5, rotation);
                var cast = f.Engine.Casts[10]; var native = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                var world = HostDrawingGeometry.Convert(native);
                for (var x = -48f; x <= 48; x += .91f)
                    for (var z = -48f; z <= 48; z += .91f)
                    {
                        var point = center + new Vector3(x, 0, z);
                        var inside = expected.Check(new(point.X, point.Z), new(center.X, center.Z), new(rotation));
                        Check(native.Contains(point) == inside && world.Contains(point) == inside, "Source dimensions or packet heading differs.");
                    }
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled cast retained.");
            });
        run("Ghimlyt elemental shield truly forces solver inside original four point five shelter then effect clears", () =>
        {
            using var f = Prepare(0x2528); var center = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x2632, center + Vector3.UnitZ * 8, Hostile: false); Scan(f);
            f.Engine.Effect(10, 14472, 1, f.Now); f.Render();
            Solve(f, p => Vector3.Distance(p, f.Actors[10].Position) < 4.5f);
            var display = f.Draw.Previews.Values.Single().Geometry;
            Check(HostDrawingGeometry.Convert(display).Contains(f.Actors[10].Position), "Shield world missing.");
            f.Engine.Effect(1, 14470, 2, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Shield finish retained.");
        });
        foreach (var (boss, alias) in new[] { (0x2528u, 0x2527u), (0x25B0u, 0x25AFu) })
            run($"Ghimlyt {boss:X} companion combat death and primary removal preserve encounter", () =>
            {
                using var f = Prepare(boss); var center = f.Engine.Arena!.Position;
                f.Actors[2] = new(2, alias, center + Vector3.UnitX * 3, InCombat: true); Scan(f);
                if (boss == 0x25B0) f.Engine.Icon(1, 0);
                f.Engine.RemoveActor(1); f.Actors.Remove(1); f.Render(); Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.PrimaryId == 2, "Primary removal reset companion encounter.");
                f.Actors[2] = f.Actors[2] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Both dead retained encounter.");
                f.Actors[2] = f.Actors[2] with { Dead = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Alias alone cannot start encounter.");
            });
        foreach (var (icon, rotation, before) in new[] { (167u, 0f, true), (168u, .7f, false) })
            run($"Ghimlyt rotating slash {icon} packet zero heading six actual hits current next and clock", () =>
            {
                using var f = Prepare(0x25DA);
                if (before) f.Engine.Icon(1, icon);
                Cast(f, 1, 14196, 5, rotation);
                if (!before) f.Engine.Icon(1, icon);
                f.Render();
                var aoes = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("ghimlyt/slash")).ToArray();
                Check(aoes.Length == 2 && Math.Abs((aoes[0].Activation!.Value - f.Start.AddSeconds(7.5)).TotalMilliseconds) < 1
                    && aoes[0].Rotation == rotation && aoes[1].OutlineOnly, "Initial rotation/readiness/forecast differs.");
                f.Engine.Finish(1, completed: true); f.Render();
                Check(f.Engine.AmbientAoes().Count() == 2, "Natural finish removed sequence.");
                for (var i = 0; i < 6; ++i)
                {
                    f.Now = f.Start.AddSeconds(5.1 + i * 1.1);
                    f.Engine.Effect(i == 0 ? 1UL : 20UL, i == 0 ? 14196u : 14671u, (uint)i, f.Now, occurredAt: f.Now.AddSeconds(-.1));
                    f.Render();
                    var current = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("ghimlyt/slash")).ToArray();
                    Check(current.Length == Math.Min(2, 5 - i), "Not exactly six hits.");
                    if (i < 5)
                        Check(Math.Abs((current[0].Activation!.Value - f.Now.AddSeconds(1)).TotalMilliseconds) < 1,
                            "Sequence used dispatch clock.");
                }
            });
        run("Ghimlyt slash cancellation and event state four cannot resurrect from stale snapshot", () =>
        {
            using var f = Prepare(0x25DA); var center = f.Engine.Arena!.Position;
            f.Engine.Icon(1, 168); Cast(f, 1, 14196, 5, 0); f.Engine.Finish(1); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled rotation retained.");
            f.Actors[10] = new(10, 0x1EA1A1, center, Rotation: .7f, EventState: 1); Scan(f);
            Check(f.Engine.AmbientAoes().Single().Rotation == .7f, "Fire sector heading differs.");
            f.Engine.EventState(10, 4); f.Render(); Scan(f); Check(f.Draw.Previews.Count == 0, "Stale state revived cleared sector.");
            f.Actors[10] = f.Actors[10] with { EventState = 4 }; Scan(f);
            f.Engine.EventState(10, 1); f.Render(); Check(f.Draw.Previews.Count == 1, "Next real state did not reactivate.");
        });
        run("Ghimlyt Nitrospin exact polygon hole danger persists natural finish then shrink restores once", () =>
        {
            using var f = Prepare(0x2515); var center = f.Engine.Arena!.Position; var initial = f.Engine.Arena;
            Cast(f, 1, 13397, 4.5);
            var aoe = f.Engine.AmbientAoes().Single(); var native = aoe.Shape.Resolve(aoe.Position, 0, null);
            var world = HostDrawingGeometry.Convert(native);
            Check(!native.Contains(center) && !world.Contains(center) && native.Contains(center + Vector3.UnitX * 30)
                && world.Contains(center + Vector3.UnitX * 30), "Nitrospin lost polygon hole.");
            Check(Math.Abs((aoe.Activation!.Value - f.Start.AddSeconds(5.6)).TotalMilliseconds) < 1, "Nitrospin clock differs.");
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 1, "Natural raidwide finish removed rim.");
            f.Actors[10] = new(10, 0x1EA1A1, center, EventState: 1); Scan(f); var shrunk = f.Engine.Arena;
            Check(!ReferenceEquals(initial, shrunk) && !shrunk!.Contains(center + Vector3.UnitX * 25) && f.Draw.Previews.Count == 0, "Arena shrink failed.");
            Scan(f); Check(ReferenceEquals(shrunk, f.Engine.Arena), "Repeated restored state replaced arena.");
        });
        foreach (var heading in new[] { 68, -113, 113, 23, 157, -157, -23, -68 })
            run($"Ghimlyt Heat heading {heading} original animation clock five actual effects and stale animation", () =>
            {
                using var f = Prepare(0x2515);
                f.Actors[10] = new(10, 0x1EA1A1, f.Engine.Arena!.Position, Rotation: heading * MathF.PI / 180); Scan(f);
                f.Events.Handle(new LogSystemEObjAnimationEvent { SourceId = 10, Param1 = 4, Param2 = 8, Timestamp = f.Start.AddSeconds(-.2) }, f.Now);
                f.Render();
                var heat = f.Engine.AmbientAoes().Single();
                Check(Math.Abs((heat.Activation!.Value - f.Start.AddSeconds(6.5)).TotalMilliseconds) < 1
                    && ((RayRectangleTemplate)heat.Shape).Length == 107.8f, "Heat source/clock differs.");
                for (var i = 1; i <= 5; ++i)
                {
                    f.Engine.Effect(20, 13400, (uint)i, f.Now); f.Engine.Effect(20, 13400, (uint)i, f.Now); f.Render();
                    Check(f.Draw.Previews.Count == (i < 5 ? 1 : 0), "Heat duplicate counted or cleared before fifth.");
                }
                f.Actors[10] = f.Actors[10] with { EventAnimation = 0x00040008 }; Scan(f);
                Check(f.Draw.Previews.Count == 0, "Old animation resurrected heat.");
            });
        run("Ghimlyt Crosshatch eight telegraphs survive natural finish queue actual effects and cancel single source", () =>
        {
            using var f = Prepare(0x25B0); var center = f.Engine.Arena!.Position;
            uint[] actions = [14411, 14412, 14413, 14115, 14116, 14697, 14698, 14699];
            for (var i = 0; i < 8; ++i)
            {
                var id = (ulong)(10 + i); f.Actors[id] = new(id, 0x233C, center + Vector3.UnitX * i); Scan(f);
                Cast(f, id, actions[i], 3.5, .2f * i);
            }
            var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 8 && aoes.Select((a, i) =>
                Math.Abs((a.Activation!.Value - f.Start.AddSeconds(5.9 + .5 * i)).TotalMilliseconds) < 1).All(v => v), "Crosshatch FIFO timing differs.");
            foreach (var id in Enumerable.Range(10, 8)) f.Engine.Finish((ulong)id, completed: true);
            f.Render(); Check(f.Draw.Previews.Count == 8, "Natural telegraph finish erased pending impacts.");
            for (var i = 0; i < 8; ++i)
            {
                f.Engine.Effect(20, 14114, (uint)i, f.Now); f.Engine.Effect(20, 14114, (uint)i, f.Now); f.Render();
                Check(f.Draw.Previews.Count == 7 - i, "Crosshatch effect duplicate/FIFO differs.");
            }
            f.Now = f.Start.AddSeconds(10); Cast(f, 10, 14411, 3.5); Cast(f, 11, 14412, 3.5);
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 1, "Cancel cleared unrelated lane.");
        });
        run("Ghimlyt Ceruleum birth forecasts nine hits displays eight then FIFO promotes final and no raw rearm", () =>
        {
            using var f = Prepare(0x25B0); var center = f.Engine.Arena!.Position;
            f.Engine.ObjectCreated(10, f.Start.AddSeconds(-.2));
            f.Actors[10] = new(10, 0x25B2, center); Scan(f);
            var first = f.Engine.AmbientAoes().ToArray();
            Check(first.Length == 8 && Math.Abs((first[0].Activation!.Value - f.Start.AddSeconds(2.9)).TotalMilliseconds) < 1
                && first.All(a => a.Id != "ghimlyt/tank/7"), "Tank cap/birth clock differs.");
            f.Engine.Effect(1, 14104, 1, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Count() == 8 && f.Engine.AmbientAoes().Any(a => a.Id == "ghimlyt/tank/7"), "Final tank did not promote.");
            for (var i = 0; i < 8; ++i)
            {
                f.Engine.Effect(10, 14106, (uint)(10 + i), f.Now); f.Render();
                Check(f.Draw.Previews.Count == 7 - i, "Tank actual hit did not advance.");
            }
            Scan(f); Check(f.Draw.Previews.Count == 0, "Raw tanks restarted completed sequence.");
        });
        run("Ghimlyt Crossbones stop at wall avoids bombardment real solver landing line and immunity", () =>
        {
            using var f = Prepare(0x25B0); var center = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x25B5, center - Vector3.UnitX * 10);
            f.Actors[11] = new(11, 0x233C, center + Vector3.UnitX * 14); Scan(f);
            Cast(f, 11, 14097, 7); Cast(f, 10, 15488, 5, target: 100);
            var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "ghimlyt/knockback/10/avoid");
            var kb = (CastrumKnockbackTemplate)aoe.Shape;
            Check(kb.Resolve(center, 0, null).Contains(center), "Unsafe landing not forbidden.");
            Solve(f, p => Vector3.Distance(kb.Landing(p), f.Actors[11].Position) > 10);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/line/point/0") || a.Id.Contains("/line")),
                "Landing path missing.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(20) } }; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("ghimlyt/knockback")), "Immunity retained KB constraint.");
        });
        run("Ghimlyt MagitekRay four person stack forces real solver to target and reset cleans", () =>
        {
            using var f = Prepare(0x25DA); var center = f.Engine.Arena!.Position;
            f.Actors[101] = f.Actors[101] with { Position = center + Vector3.UnitX * 10 }; Scan(f);
            Cast(f, 1, 14191, 5, target: 101);
            Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) < 6);
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Reset leaked stack.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1174);
        var center = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Engine.ResetFight(); f.Actors[1] = new(1, boss, center, InCombat: true);
        f.Actors[100] = new(100, 0, center, Hostile: false, PartySlot: 0);
        f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 10, Hostile: false, PartySlot: 1);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position),
            $"Real solver failed: {solution.Status} / {string.Join(',', path.Select(p => p.Position))}");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

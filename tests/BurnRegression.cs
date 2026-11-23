using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class BurnRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Burn Hedetet exported source arena matches radar and world", () =>
        {
            var type = typeof(BossMod.Stormblood.Dungeon.D13TheBurn.D131Hedetet.D131Hedetet);
            var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            using var f = Prepare(0x2419); var center = Center(0x2419); var native = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(native);
            for (var x = -22f; x < 22; x += .41f)
                for (var z = -22f; z < 22; z += .41f)
                {
                    var point = center + new Vector3(x, 0, z);
                    var expected = source.Contains(new(point.X - source.Center.X, point.Z - source.Center.Z));
                    Check(native.Contains(point) == expected && world.Contains(point) == expected, "Arena differs.");
                }
        });
        foreach (var boss in new uint[] { 0x23AA, 0x2431 })
            run($"Burn {boss:X} source arena and territory ownership", () =>
            {
                using var f = Prepare(boss); var center = Center(boss);
                var source = boss == 0x23AA ? (BossMod.ArenaBounds)new BossMod.ArenaBoundsRect(10, 9.5f) : new BossMod.ArenaBoundsCircle(19.5f);
                var world = HostDrawingGeometry.Convert(f.Engine.Arena!);
                for (var x = -21f; x < 21; x += .53f)
                    for (var z = -21f; z < 21; z += .53f)
                    {
                        var p = center + new Vector3(x, 0, z); var expected = source.Contains(new(x, z));
                        Check(f.Engine.Arena!.Contains(p) == expected && world.Contains(p) == expected, "Arena differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 1173) && !ArrDungeonCatalog.Owns(boss, 1172), "Ownership differs.");
            });
        run("Burn Hailfire crystal shadows lock origin target enters true shelter solver and clear", () =>
        {
            using var f = Prepare(0x2419); var center = Center(0x2419);
            f.Actors[10] = new(10, 0x241A, center + Vector3.UnitZ * 7, HitboxRadius: 1.6f); Scan(f);
            Cast(f, 1, 12692, 6, target: 100);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "burn/hail/enter"), "Target shelter constraint missing.");
            Solve(f, p => p.Z > center.Z + 7 && MathF.Abs(p.X - center.X) <= 1.6f);
            f.Actors[10] = f.Actors[10] with { Position = center + Vector3.UnitX * 7 }; Scan(f);
            var locked = f.Engine.AmbientAoes().Single(a => a.Id == "burn/hail/safe/0");
            Check(locked.Shape.Resolve(locked.Position, 0, null).Contains(center + Vector3.UnitZ * 12), "Hailfire shadow moved after cast start.");
            f.Engine.Finish(1); f.Render(); Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("burn/hail")), "Hailfire cancel retained.");
        });
        run("Burn Hailfire other viewer beam minus crystal shadow has exact world cutout", () =>
        {
            using var f = Prepare(0x2419); var center = Center(0x2419);
            f.Actors[10] = new(10, 0x241A, center + Vector3.UnitZ * 7, HitboxRadius: 1.6f);
            f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 15, PartySlot: 1, Hostile: false); Scan(f);
            Cast(f, 1, 12692, 6, target: 101);
            var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "burn/hail/avoid");
            var native = aoe.Shape.Resolve(aoe.Position, 0, null); var world = HostDrawingGeometry.Convert(native);
            foreach (var (p, expected) in new (Vector3, bool)[] { (center + Vector3.UnitZ * 3, true),
                (center + Vector3.UnitZ * 12, false), (center + new Vector3(1.9f, 0, 12), true), (center + Vector3.UnitX * 3, false) })
                Check(native.Contains(p) == expected && world.Contains(p) == expected, $"Beam cutout differs at {p}.");
        });
        run("Burn Shardfall live crystal LOS true solver and destroyed blocker update", () =>
        {
            using var f = Prepare(0x2419); var center = Center(0x2419);
            f.Actors[10] = new(10, 0x241A, center + Vector3.UnitZ * 7, HitboxRadius: 1.6f); Scan(f);
            Cast(f, 1, 12689, 5);
            Solve(f, p => p.Z > center.Z + 7);
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            var danger = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/avoid"));
            Check(danger.Shape.Resolve(danger.Position, 0, null).Contains(center + Vector3.UnitZ * 12), "Destroyed crystal still sheltered.");
        });
        run("Burn spread original clock protects crystals and batch effect clears every player", () =>
        {
            using var f = Prepare(0x2419); var center = Center(0x2419);
            f.Actors[10] = new(10, 0x241A, center + Vector3.UnitZ * 7, HitboxRadius: 1.6f);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(100, 96, f.Start); f.Engine.Icon(101, 96, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "burn/spread/100").Activation == f.Start.AddSeconds(5.8)
                && f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/spread") && a.Navigate), "Spread clock or crystal protection missing.");
            f.Engine.Effect(20, 12697, 1, f.Now, 100); f.Engine.Effect(20, 12697, 1, f.Now, 101); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("burn/spread") || a.Id.EndsWith("/spread")), "Batch spread not fully cleared.");
        });
        foreach (var (boss, oid, action, inside, outside) in new (uint, uint, uint, Vector3, Vector3)[]
        {
            (0x2419, 0x241A, 12696, new(5.9f, 0, 0), new(6.1f, 0, 0)),
            (0x2419, 0x241A, 12695, new(2.9f, 0, 0), new(3.1f, 0, 0)),
            (0x2419, 0x2419, 12690, new(5.1f, 0, 0), new(4.9f, 0, 0)),
            (0x23AA, 0x23AC, 11640, new(2.9f, 0, 32.9f), new(3.1f, 0, 20)),
            (0x2431, 0x2431, 12618, new(14.9f, 0, 0), new(15.1f, 0, 0)),
        })
            run($"Burn {action} source cast geometry both views and cancellation", () =>
            {
                using var f = Prepare(boss); var center = Center(boss);
                f.Actors[10] = new(10, oid, center); Scan(f); Cast(f, 10, action, 5);
                var geometry = f.Engine.OutputCasts.Single().Value.Rule.Shape!.Resolve(center, 0, null);
                var world = HostDrawingGeometry.Convert(geometry);
                Check(geometry.Contains(center + inside) && !geometry.Contains(center + outside)
                    && world.Contains(center + inside) && !world.Contains(center + outside), "Source dimensions differ.");
                f.Engine.Finish(10); f.Render(); Check(f.Engine.OutputCasts.All(a => a.Value.Rule.Action != action), "Cancelled cast retained.");
            });
        run("Burn throttle locks active drone formation original eight point two clock and full finish clears", () =>
        {
            using var f = Prepare(0x23AA); var center = Center(0x23AA);
            f.Actors[10] = new(10, 0x23AB, new(-18, 0, -75), Rotation: MathF.PI / 2);
            f.Actors[11] = new(11, 0x23AB, new(-18, 0, -70), Rotation: MathF.PI / 2, AnimationState1: 1); Scan(f);
            f.Engine.Effect(1, 13526, 1, f.Now, occurredAt: f.Start.AddSeconds(-.2)); f.Render();
            var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 2 && aoes.All(a => Math.Abs((a.Activation!.Value - f.Start.AddSeconds(8)).TotalMilliseconds) < 1)
                && aoes.Single(a => a.Id.EndsWith("/boss")).Rotation == MathF.PI / 2, "Formation filter/side/clock differs.");
            f.Engine.Effect(1, 13526, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 2, "Duplicate visual added formation.");
            f.Actors[10] = f.Actors[10] with { Position = center }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/10")).Position.X < -17, "Formation followed drones.");
            Cast(f, 1, 11637, 3); f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0, "FullThrottle finish retained formation.");
        });
        run("Burn residue icon and sludge state priority match active source components", () =>
        {
            using var f = Prepare(0x23AA); var center = Center(0x23AA);
            f.Actors[10] = new(10, 0x1EA9EF, center + Vector3.UnitX * 7); f.Actors[11] = new(11, 0x23AD, center); Scan(f);
            f.Engine.Icon(100, 2, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "burn/residue/100").Activation == f.Start.AddSeconds(4.1)
                && f.Engine.PriorityTarget(1, center) == 11, "Residue or dreadnaught priority differs.");
            f.Engine.Effect(1, 11636, 1, f.Now); f.Actors[10] = f.Actors[10] with { EventState = 7 }; Scan(f);
            Check(f.Draw.Previews.Count == 0, "Residue/sludge cleanup failed.");
        });
        run("Burn fog circle forecasts two fixed crosses persists through first finish and clears at cross finish", () =>
        {
            using var f = Prepare(0x2431); var center = Center(0x2431);
            f.Actors[10] = new(10, 0x233C, center); Scan(f); Cast(f, 10, 12613, 3);
            Check(f.Draw.Previews.Count == 3 && f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("burn/cross")) == 2
                && f.Engine.AmbientAoes().All(a => Math.Abs((a.Activation!.Value - f.Start.AddSeconds(6.9)).TotalMilliseconds) < 1), "Cross forecast dimensions/time missing.");
            f.Engine.Finish(10, completed: true); f.Render(); Check(f.Draw.Previews.Count == 2, "Circle finish erased future crosses.");
            f.Now = f.Start.AddSeconds(4); Cast(f, 10, 12614, 1.5);
            f.Engine.Finish(10, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0, "Cross finish retained future crosses.");
            f.Now = f.Start.AddSeconds(8); Cast(f, 10, 12613, 3); f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled circle kept future crosses.");
        });
        run("Burn cold fog radius status activation last add death retimes once and stale effect cannot rearm", () =>
        {
            using var f = Prepare(0x2431); var center = Center(0x2431);
            f.Actors[10] = new(10, 0x2432, center + Vector3.UnitX * 8); Scan(f); Cast(f, 1, 12609, 24);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 618, Param = 5, Timestamp = f.Start }, f.Now); f.Render();
            var fog = f.Engine.AmbientAoes().Single();
            Check(fog.Activation == f.Start.AddSeconds(24.3) && ((CircleTemplate)fog.Shape).Radius == 9, "Cold fog radius/clock differs.");
            f.Actors[1] = f.Actors[1] with { Statuses = [618], StatusParams = new Dictionary<uint, ushort> { [618] = 5 } };
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Now = f.Start.AddSeconds(3); Scan(f);
            Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(8.6), "Last add did not retime fog.");
            f.Now = f.Start.AddSeconds(4); Scan(f); Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(8.6), "Fog retimed every frame.");
            f.Engine.Effect(20, 12610, 1, f.Now); f.Now = f.Start.AddSeconds(7); Scan(f); Check(f.Draw.Previews.Count == 0, "Stale fog status resurrected.");
        });
        foreach (var icon in new uint[] { 14, 26 })
            run($"Burn mist dragon icon {icon} actual bait clock and future ice circles in both views", () =>
            {
                using var f = Prepare(0x2431); var center = Center(0x2431);
                f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 10 };
                f.Actors[101] = new(101, 0, center + Vector3.UnitX * 10, PartySlot: 1, Hostile: false); Scan(f);
                f.Engine.Icon(100, icon, f.Start); f.Render();
                var aoes = f.Engine.AmbientAoes().ToArray();
                Check(aoes.Any(a => a.Id.Contains("/pool/")) && aoes.All(a => a.Activation == f.Start.AddSeconds(icon == 14 ? 6.1 : 4.1))
                    && aoes.Any(a => a.Id.Contains("/party/")), "Bait forecast/time/party avoidance missing.");
                var pool = f.Draw.Previews.Values.Select(a => a.Geometry).First(g => g is CircleGeometry);
                Check(HostDrawingGeometry.Convert(pool).Contains(pool.Position + Vector3.UnitX * 5.9f), "Future ice world circle differs.");
                f.Engine.Effect(1, icon == 14 ? 12621u : 12620u, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Bait hit retained.");
            });
        run("Burn cauterize object birth original time locks quantized origin then actual cast finish clears", () =>
        {
            using var f = Prepare(0x2431); var center = Center(0x2431);
            f.Engine.ObjectCreated(10, f.Start.AddSeconds(-.2));
            f.Actors[10] = new(10, 0x2434, center, Rotation: MathF.PI / 2); Scan(f);
            var aoe = f.Engine.AmbientAoes().Single();
            Check(Math.Abs((aoe.Activation!.Value - f.Start.AddSeconds(7.1)).TotalMilliseconds) < 1
                && aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null).Contains(center + new Vector3(30, 0, 7.9f)), "Cauterize birth/shape differs.");
            f.Actors[11] = new(11, 0x233C, center); Scan(f); Cast(f, 11, 12617, .7);
            f.Engine.Finish(11, completed: true); Scan(f); Check(f.Draw.Previews.Count == 0, "Cauterize finish resurrected old helper.");
        });
        run("Burn ice state add priorities and reset clear both views", () =>
        {
            using var f = Prepare(0x2431); var center = Center(0x2431);
            f.Actors[10] = new(10, 0x2432, center); f.Actors[11] = new(11, 0x2433, center);
            f.Actors[12] = new(12, 0x1E8713, center); Scan(f);
            Check(f.Engine.PriorityTarget(10, center) == 11 && f.Draw.Previews.Count == 1, "Mist/add priority or ice pool missing.");
            f.Actors[12] = f.Actors[12] with { EventState = 7 }; Scan(f); Check(f.Draw.Previews.Count == 0, "Inactive ice retained.");
            f.Engine.ResetFight(); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Reset leaked.");
        });
    }

    private static Vector3 Center(uint boss) => boss switch { 0x2419 => new(174, 0, 178), 0x23AA => new(0, 0, -70), _ => new(-300, 0, -400) };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1173); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position, f.Now, playerId: f.PlayerId); f.Render(); }
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
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position), "True solver did not enter shelter.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

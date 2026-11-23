using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class HardTrialRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (boss, territory, radius) in new[] { (0xD1u, 292u, 19.5f), (0xF2u, 294u, 22f), (0xF7u, 293u, 25f) })
            run($"HardTrial {boss:X} territory fixed arena ownership and phase lifetime", () =>
            {
                using var f = new Fight(boss, territory);
                Check(ArrDungeonCatalog.Owns(boss, territory) && !ArrDungeonCatalog.Owns(boss, 0), "Wrong ownership.");
                Check(f.Engine.Arena!.Contains(Vector3.UnitZ * (radius - .01f))
                    && !f.Engine.Arena.Contains(Vector3.UnitZ * (radius + .01f)), "Arena radius differs.");
                f.Actors[1] = f.Actors[1] with { InCombat = false }; f.Scan();
                Check(f.Engine.Active?.BossOid == boss, "Temporary combat gap reset trial.");
                Check(!f.Engine.RequireAllStop && f.Engine.ForbiddenTargets.Length == 0, "Unspecified stop/forbidden request added.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
                Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null, "Dead primary retained trial.");
            });
        foreach (var (boss, territory, action, radius, width) in new[]
        {
            (0xD1u, 292u, 1358u, 8f, 0f), (0xD1u, 292u, 1359u, 8f, 0f),
            (0xF2u, 294u, 1386u, 8f, 0f), (0xF7u, 293u, 1363u, 6f, 0f),
            (0xF7u, 293u, 1364u, 40.25f, 6f),
        })
            run($"HardTrial {action} radar world exact shape margin clock and observed end", () =>
            {
                using var f = new Fight(boss, territory);
                f.Actors[10] = new(10, 0xFFFF, Vector3.Zero); f.Scan(); f.Cast(10, action, 4, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                var inside = Vector3.UnitX * (radius - .01f);
                Check(geometry.Contains(inside) && HostDrawingGeometry.Convert(geometry).Contains(inside)
                    && !geometry.Contains(Vector3.UnitX * (radius + .01f)), "Display size or world conversion differs.");
                if (width > 0) Check(geometry.Contains(new(1, 0, width / 2 - .01f))
                    && !geometry.Contains(new(1, 0, width / 2 + .01f)), "Rectangle width differs.");
                var danger = f.Snapshot.DangerAreas.Single();
                Check(danger.Geometry.Contains(Vector3.UnitX * (radius + .4f))
                    && danger.DangerWindows.Single().DangerFrom == f.Now.AddSeconds(4.3)
                    && danger.DangerWindows.Single().DangerUntil == null, "Margin/NPC clock differs.");
                f.Now = f.Start.AddSeconds(30); f.Scan();
                Check(f.Draw.Previews.Count == 1, "Prediction timeout removed active cast.");
                f.Actors[10] = f.Actors[10] with { Cast = null }; f.Scan();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Observed finish leaked.");
            });
        foreach (var (boss, territory, radius) in new[] { (0xD1u, 292u, 15f), (0xF2u, 294u, 11.7f), (0xF7u, 293u, 21.25f) })
            run($"HardTrial {boss:X} cleave follows target and tank avoids party cones", () =>
            {
                using var f = new Fight(boss, territory);
                f.Actors[1] = f.Actors[1] with { TargetId = 101 };
                f.Actors[100] = new(100, 0, Vector3.UnitZ * 5, PartySlot: 0, Hostile: false);
                f.Actors[101] = new(101, 0, Vector3.UnitZ * 10, PartySlot: 1, Hostile: false); f.Scan();
                var display = Group(f, "cleave").Single(a => a.Display);
                Check(display.Shape is FanTemplate { AngleRadians: var angle } && MathF.Abs(angle - MathF.Tau / 3) < .001f
                    && display.Shape.Resolve(display.Position, display.Rotation, null).Contains(Vector3.UnitZ * (radius - .01f)),
                    "Cleave size differs.");
                Check(Group(f, "cleave").Single(a => a.Navigate).Id.EndsWith("/avoid"), "Non-target not protected.");
                f.PlayerId = 101; f.Scan();
                Check(Group(f, "cleave").Single(a => a.Navigate).Shape is FanTemplate { Radius: 100 }, "Tank lost party-facing exclusion.");
                f.Actors[101] = f.Actors[101] with { Position = Vector3.UnitX * 10 }; f.Scan();
                Check(Group(f, "cleave").Single(a => a.Display).Rotation == MathF.PI / 2, "Target movement did not rotate cleave.");
            });
        run("HardTrial Ifrit timeline gate dynamic position rolling clock real cast duplicate FIFO", () =>
        {
            using var f = new Fight(0xD1, 292);
            f.Actors[10] = new(10, 0xD1, Vector3.UnitZ * 5, Rotation: MathF.PI / 2);
            f.Actors[11] = new(11, 0x1B2, Vector3.Zero); f.Scan();
            f.Engine.Timeline(1, 0x008D, f.Now); f.Engine.Timeline(11, 0x008D, f.Now);
            f.Engine.Timeline(10, 0x008C, f.Now); f.Render();
            Check(Group(f, "cyclone").Length == 0, "Wrong actor/timeline entered charge list.");
            f.Engine.Timeline(10, 0x008D, f.Now); f.Render();
            var charge = Group(f, "cyclone").Single();
            Check(charge.Position == Vector3.UnitZ * 5 && charge.Rotation == MathF.PI / 2 && charge.Activation == f.Now.AddSeconds(4),
                "Timeline pre-cast geometry or activation differs.");
            var shape = charge.Shape.Resolve(charge.Position, charge.Rotation, null);
            Check(shape.Contains(charge.Position + new Vector3(42.99f, 0, 5.99f))
                && !shape.Contains(charge.Position + new Vector3(43.01f, 0, 0))
                && !shape.Contains(charge.Position + new Vector3(1, 0, 6.01f)), "43 by 12 charge differs.");
            f.Now = f.Start.AddSeconds(2);
            f.Actors[10] = f.Actors[10] with { Position = Vector3.UnitX * 7, Rotation = MathF.PI }; f.Scan();
            charge = Group(f, "cyclone").Single();
            Check(charge.Position.X == 7 && charge.Rotation == MathF.PI && charge.Activation == f.Now.AddSeconds(4), "Uncast charge became fixed prediction.");
            f.Engine.Timeline(10, 0x008D, f.Now); f.Cast(10, 457, 3, MathF.PI / 2);
            Check(Group(f, "cyclone").Length == 2 && Group(f, "cyclone").All(a => a.Position == Vector3.Zero
                && a.Rotation == MathF.PI / 2 && a.Activation == f.Now.AddSeconds(3.3)), "Real cast did not lock timeline entries.");
            f.Engine.Finish(10, 457); f.Actors[10] = f.Actors[10] with { Cast = null }; f.Scan();
            Check(Group(f, "cyclone").Length == 1, "One finish consumed repeated timeline entries.");
            f.Cast(1, 457, 3);
            Check(Group(f, "cyclone").Length == 1, "Primary cast alone added a charge.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset retained charge.");
        });
        foreach (var (boss, territory, addOid, seconds) in new[] { (0xD1u, 292u, 0xD2u, 55), (0xF7u, 293u, 0x5E4u, 62) })
            run($"HardTrial {boss:X} active add first-observation countdown persists and clamps", () =>
            {
                using var f = new Fight(boss, territory);
                f.Actors[10] = new(10, addOid, Vector3.Zero, Targetable: false); f.Scan();
                Check(f.Engine.HardTrialCountdown == null, "Untargetable add started timer.");
                f.Now = f.Start.AddSeconds(1); f.Actors[10] = f.Actors[10] with { Targetable = true }; f.Scan();
                Check(f.Engine.HardTrialCountdown!.Contains($"{seconds:F1}") && f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Spawn timer or priority missing.");
                f.Now = f.Start.AddSeconds(6); f.Scan();
                Check(f.Engine.HardTrialCountdown!.Contains($"{seconds - 5:F1}"), "Timer restarted on scan.");
                f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan(); Check(f.Engine.HardTrialCountdown == null, "Dead add retained timer.");
                f.Now = f.Start.AddSeconds(100); f.Actors[10] = f.Actors[10] with { Dead = false }; f.Scan();
                Check(f.Engine.HardTrialCountdown!.Contains("0.0"), "Reappearing add reset/clamped timer incorrectly.");
            });
        run("HardTrial Garuda independent LOS queues retain true pillar snapshots and FIFO", () =>
        {
            using var f = new Fight(0xF2, 294);
            var origin = new Vector3(0, 0, -15);
            f.Actors[1] = f.Actors[1] with { Position = origin };
            f.Actors[10] = new(10, 0xF0, Vector3.Zero, Dead: true, Targetable: false, HitboxRadius: 2);
            f.Actors[11] = new(11, 0x61E, origin); f.Scan();
            Check(Group(f, "pillar").Single().Color == new Vector4(1, 128f / 255, 0, 1), "Object foreground color missing.");
            Start(f, 1, 1384, origin, 3); Start(f, 11, 1390, origin, 4);
            Check(Group(f, "los").Count(a => a.Display) == 2 && !Group(f, "friction").Any(), "LOS components conflated or casting did not release Friction.");
            var safe = Group(f, "los").Single(a => a.Id == "hard-trial/los/1390/display").Shape.Resolve(origin, 0, null);
            Check(safe.Contains(new(0, 0, 5)) && !safe.Contains(Vector3.Zero) && !safe.Contains(new(5, 0, 5)),
                "Pillar shadow ignored hitbox or occupancy exclusion.");
            f.Actors[10] = f.Actors[10] with { Position = Vector3.UnitX * 15, HitboxRadius = 10 }; f.Scan();
            Check(Group(f, "los").Single(a => a.Id == "hard-trial/los/1390/display").Shape.Resolve(origin, 0, null).Contains(new(0, 0, 5)), "LOS moved after start.");
            f.Engine.Finish(1, 1384); f.Render();
            Check(Group(f, "los").Count(a => a.Display) == 1 && Group(f, "los").All(a => a.Id.Contains("1390")), "One LOS finish consumed another component.");
            f.Engine.Finish(11, 1390); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Scan();
            Check(Group(f, "los").Length == 0 && Group(f, "friction").Length == 1, "Finish failed to restore Friction.");
            f.Actors[1] = f.Actors[1] with { Cast = new(999, origin, 0, f.Now, f.Now.AddSeconds(4)) }; f.Scan();
            Check(!Group(f, "friction").Any(), "Unrecognized boss cast retained Friction.");
        });
        run("HardTrial Garuda green tether events and snapshots drive exact add priority", () =>
        {
            using var f = new Fight(0xF2, 294);
            f.Actors[10] = new(10, 0x61E, Vector3.Zero);
            f.Actors[11] = new(11, 0x61F, Vector3.Zero, Tethers: new Dictionary<uint, ulong> { [4] = 1 });
            f.Actors[12] = new(12, 0x5FF, Vector3.Zero); f.Scan();
            Check(f.Engine.PriorityTarget(11, Vector3.Zero) == 11, "Snapshot green tether priority missing.");
            f.Engine.Tether(1, 4, true, f.Now, 10); f.Render();
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 10, "Green tether event not applied to source.");
            f.Engine.Tether(1, 4, false, f.Now, 10); f.Engine.Tether(1, 11, true, f.Now, 11);
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 12, "Red/cancelled tether retained green priority.");
            f.Actors[12] = f.Actors[12] with { Dead = true }; f.Scan();
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 10, "Normal add priority missing.");
        });
        run("HardTrial Garuda same action LOS retains event FIFO when later caster ends first", () =>
        {
            using var f = new Fight(0xF2, 294);
            var firstOrigin = -Vector3.UnitZ * 15;
            var secondOrigin = -Vector3.UnitZ * 10;
            f.Actors[1] = f.Actors[1] with { Position = firstOrigin };
            f.Actors[10] = new(10, 0xF0, Vector3.Zero, HitboxRadius: 2);
            f.Actors[11] = new(11, 0x61E, secondOrigin); f.Scan();
            Start(f, 1, 1390, firstOrigin, 4);
            Start(f, 11, 1390, secondOrigin, 3);
            var p = new Vector3(4, 0, 10);
            Check(!Group(f, "los").Single(a => a.Display).Shape.Resolve(firstOrigin, 0, null).Contains(p), "First LOS snapshot not at FIFO head.");
            f.Engine.Finish(11, 1390); f.Render();
            Check(Group(f, "los").Single(a => a.Display).Shape.Resolve(secondOrigin, 0, null).Contains(p), "Later caster finish did not remove FIFO head.");
            f.Engine.Finish(1, 1390); f.Render(); Check(Group(f, "los").Length == 0, "Last LOS finish leaked.");
        });
        run("HardTrial Garuda later snapshots correct green and cancelled tether events", () =>
        {
            using var f = new Fight(0xF2, 294);
            f.Actors[10] = new(10, 0x61E, Vector3.Zero);
            f.Actors[11] = new(11, 0x5FF, Vector3.Zero); f.Scan();
            f.Engine.Tether(1, 4, true, f.Now, 10);
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 10, "Incoming tether did not override stale snapshot.");
            f.Now = f.Start.AddSeconds(1);
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong> { [11] = 1 } }; f.Scan();
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 11, "Expired green event masked red snapshot.");
            f.Engine.Tether(1, 4, false, f.Now, 10);
            f.Now = f.Start.AddSeconds(2);
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong> { [4] = 1 } }; f.Scan();
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 10, "Expired cancellation masked restored green snapshot.");
        });
        run("HardTrial Ifrit same OID cleaves remain independent from different OID adds", () =>
        {
            using var f = new Fight(0xD1, 292);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[100] = new(100, 0, Vector3.UnitZ * 5, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0xD1, Vector3.Zero, TargetId: 101);
            f.Actors[11] = new(11, 0xD2, Vector3.Zero, TargetId: 101); f.Scan();
            Check(Group(f, "cleave").Count(a => a.Display) == 2, "Same OID clone cleave omitted or nail added.");
            f.Actors.Remove(10); f.Scan();
            Check(Group(f, "cleave").Count(a => a.Display) == 1, "Removing clone consumed primary cleave.");
        });
        run("HardTrial Garuda slipstream ninety degrees eye cast lifecycle raidwide prediction", () =>
        {
            using var f = new Fight(0xF2, 294);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0x624, Vector3.Zero); f.Scan(); f.Cast(1, 1382, 3);
            Check(f.Draw.Previews.Single().Value.Geometry.Contains(new(0, 0, 11.6f))
                && !f.Draw.Previews.Single().Value.Geometry.Contains(new(10, 0, 0)), "Slipstream cone differs.");
            f.Engine.Finish(1, 1382); f.Cast(10, 1387, 3);
            var donut = (DonutGeometry)f.Draw.Previews.Single().Value.Geometry;
            Check(donut.InnerRadius == 12 && donut.OuterRadius == 25, "Eye donut differs.");
            f.Engine.Finish(10, 1387); f.Render(); Check(f.Draw.Previews.Count == 0, "Eye became persistent normal-mode state.");
            f.Cast(1, 1385, 4); Check(f.Engine.PredictedDamage.Single().DamageType == "Raidwide", "AerialBlast prediction missing.");
        });
        run("HardTrial Titan bomb strict activation prefix slides on first finish not current time", () =>
        {
            using var f = new Fight(0xF7, 293);
            for (ulong i = 10; i < 15; ++i) f.Actors[i] = new(i, 0x5A3, Vector3.UnitX * (i - 10));
            f.Scan();
            var durations = new[] { 5d, 7.249, 7.25, 7.251, 5.5 };
            for (var i = 0; i < durations.Length; ++i) f.Cast((ulong)(10 + i), 1052, durations[i]);
            Check(Group(f, "bomb").Select(a => a.Id).SequenceEqual(["hard-trial/bomb/10", "hard-trial/bomb/11"]), "Strict 2.25 prefix differs.");
            Check(Group(f, "bomb").All(a => a.Shape is CircleTemplate { Radius: 6.3f }), "Bomb radius differs.");
            f.Engine.StartCast(10, f.Actors[10].Cast! with { Location = Vector3.UnitZ * 5 }, f.Now); f.Render();
            Check(Group(f, "bomb").Length == 2 && Group(f, "bomb")[0].Position.Z == 5, "Packet correction duplicated bomb or lost locked location.");
            f.Now = f.Start.AddSeconds(50); f.Scan();
            Check(Group(f, "bomb").Length == 2, "Window incorrectly anchored to current time.");
            f.Engine.Finish(10, 1052); f.Render();
            Check(Group(f, "bomb").Length == 4, "First finish did not advance prefix window.");
            f.Engine.Finish(12, 1052); f.Render(); Check(Group(f, "bomb").Length == 3, "Non-first finish consumed other bombs.");
        });
        run("HardTrial Titan Geocrush restores historical inner outer pair and preserves outer on finish", () =>
        {
            using var f = new Fight(0xF7, 293);
            Check(!Group(f, "geocrush").Any(), "Geocrush active before first cast.");
            for (var i = 0; i < 4; ++i)
            {
                f.Now = f.Start.AddSeconds(i * 10); f.Cast(1, 1365, 6);
                var radius = new[] { 23f, 20f, 15f, 25f }[i];
                Check(Group(f, "geocrush").Single(a => a.Id.EndsWith("/inner")).Shape.Resolve(Vector3.Zero, 0, null) is CircleGeometry circle
                    && circle.Radius == radius - 2, "Inner falloff radius differs.");
                Check(Group(f, "geocrush").Count(a => a.Id.EndsWith("/outer")) == (i < 3 ? 1 : 0), "Outer ring/zero-area case differs.");
                var inner = Group(f, "geocrush").Single(a => a.Id.EndsWith("/inner"));
                Check(inner.Activation == f.Now.AddSeconds(6.3) && HostDrawingGeometry.Convert(inner.Shape.Resolve(inner.Position, 0, null)).Contains(Vector3.Zero),
                    "Inner activation or world output missing.");
                f.Engine.Finish(1, 1365); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Scan();
                Check(Group(f, "geocrush").All(a => a.Id.EndsWith("/outer"))
                    && f.Engine.Arena!.Contains(Vector3.UnitZ * 24.9f), "Finish cleared outer or shrank formal boundary.");
            }
        });
        run("HardTrial Titan Geocrush actual default solver reaches annulus before inner activation", () =>
        {
            using var f = new Fight(0xF7, 293);
            f.Cast(1, 1365, 6);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, $"No safe annulus route: {solution}.");
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            var end = path[^1].Position.Length();
            Check(path.Count > 1 && end > 21 && end < 23 && path.All(p => f.Engine.Arena!.Contains(p.Position)), "Route did not reach annulus.");
        });
    }

    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("hard-trial/" + prefix)).ToArray();

    private static void Start(Fight f, ulong source, uint action, Vector3 location, double duration)
    {
        var cast = new DungeonCastObservation(action, location, 0, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast };
        f.Engine.StartCast(source, cast, f.Now); f.Render();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

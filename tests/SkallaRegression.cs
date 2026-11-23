using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class SkallaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        FollowRegression.RunSkallaControls(run);
        foreach (var boss in new uint[] { 0x1FAB, 0x1FAC, 0x1FAE })
            run($"Skalla {boss:X} arena geometry and isolated ownership", () =>
            {
                using var f = Prepare(boss); var center = Center(boss);
                var source = boss == 0x1FAB ? (BossMod.ArenaBounds)new BossMod.ArenaBoundsSquare(14.5f) : new BossMod.ArenaBoundsCircle(20);
                var native = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(native);
                for (var x = -23f; x < 23; x += .47f)
                    for (var z = -23f; z < 23; z += .47f)
                    {
                        var p = center + new Vector3(x, 0, z); var expected = source.Contains(new(x, z));
                        Check(native.Contains(p) == expected && world.Contains(p) == expected, "Arena shape differs.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 1172) && !ArrDungeonCatalog.Owns(boss, 1146), "Ownership differs.");
            });
        run("Skalla water puddles preserve source R7 drawing R8 navigation and dead cleanup", () =>
        {
            using var f = Prepare(0x1FAB); var center = Center(0x1FAB);
            f.Actors[10] = new(10, 0x1EA7D5, center); Scan(f);
            Check(f.Draw.Previews.Single().Value.Geometry.Contains(center + Vector3.UnitX * 6.9f)
                && !f.Draw.Previews.Single().Value.Geometry.Contains(center + Vector3.UnitX * 7.1f), "Puddle display differs.");
            var avoid = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/avoid"));
            Check(avoid.Shape.Resolve(avoid.Position, 0, null).Contains(center + Vector3.UnitX * 7.9f), "Puddle nav lost R8.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f); Check(f.Draw.Previews.Count == 0, "Dead puddle retained.");
        });
        run("Skalla icon puddles use original event time corner union and FIFO real effects", () =>
        {
            using var f = Prepare(0x1FAB); var center = Center(0x1FAB);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 7, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(100, 43, f.Start); f.Engine.Icon(101, 43, f.Start.AddSeconds(.1)); f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Engine.AmbientAoes().Single(a => a.Id == "skalla/puddle/100/display").Activation == f.Start,
                "Puddle icon timing/geometry missing.");
            Solve(f, p => MathF.Abs(p.X + 220) >= 11.5f && MathF.Abs(p.Z - 4) >= 11.5f);
            f.Engine.Effect(10, 9813, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 1, "Puddle FIFO not advanced.");
            f.Engine.Effect(10, 9813, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 1, "Puddle duplicate advanced.");
            f.Engine.Effect(10, 9813, 2, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Final puddle retained.");
        });
        foreach (var action in new uint[] { 9809, 9810 })
            run($"Skalla {action} directional twenty yalm movement real solver and immunity", () =>
            {
                using var f = Prepare(0x1FAB); var center = Center(0x1FAB);
                f.Actors[1] = f.Actors[1] with { Position = action == 9809 ? center + Vector3.UnitZ * 14 : center - Vector3.UnitX * 14 };
                Scan(f); Cast(f, 1, action, 7, MathF.PI / 2);
                var delta = action == 9809 ? -20 : 20;
                var aoes = f.Engine.AmbientAoes().ToArray();
                Check(Vector3.Distance(aoes.Single(a => a.Id.EndsWith("/landing")).Position, center + Vector3.UnitX * delta) < .001f, "Knockback direction lost.");
                Solve(f, p => f.Engine.Arena!.Contains(p + Vector3.UnitX * delta));
                f.Actors[100] = f.Actors[100] with { Statuses = [1209], StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(10) } }; Scan(f);
                Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("skalla/kb")), "Immune knockback retained.");
                f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled KB retained.");
            });
        run("Skalla sphere source centered bait line raw restore cancellation and fresh tether", () =>
        {
            using var f = Prepare(0x1FAB); var center = Center(0x1FAB);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 10, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x2051, center + Vector3.UnitX * 5, Tethers: new Dictionary<uint, ulong> { [3] = 101 }); Scan(f);
            Check(f.Draw.Previews.Count == 2 && f.Engine.AmbientAoes().Any(a => a.Id == "skalla/sphere/10/avoid"), "Sphere circle/line missing.");
            var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
            Check(display.Position == f.Actors[10].Position, "Sphere incorrectly centered on tether target.");
            f.Engine.Tether(101, 3, false, f.Now, 10); f.Now = f.Start.AddSeconds(2); Scan(f);
            Check(f.Draw.Previews.Count == 0, "Stale sphere resurrected.");
            f.Actors[10] = f.Actors[10] with { Tethers = null }; Scan(f);
            f.Actors[10] = f.Actors[10] with { Tethers = new Dictionary<uint, ulong> { [3] = 101 } }; Scan(f);
            Check(f.Draw.Previews.Count == 2, "Fresh sphere failed to restore.");
            f.Engine.Effect(10, 9755, 1, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Burst sphere resurrected.");
        });
        foreach (var (boss, oid, action, point, outside) in new (uint, uint, uint, Vector3, Vector3)[]
        {
            (0x1FAC, 0x1FAC, 9815, new(0, 0, 39.9f), new(30, 0, 0)),
            (0x1FAC, 0x18D6, 9817, new(7.9f, 0, 0), new(8.1f, 0, 0)),
            (0x1FAE, 0x1FAE, 9825, new(0, 0, 16.9f), new(0, 0, 17.1f)),
            (0x1FAE, 0x1FAE, 9827, new(0, 0, 49.9f), new(40, 0, 0)),
            (0x1FAE, 0x1FAE, 9828, new(16.9f, 0, 0), new(17.1f, 0, 0)),
            (0x1FAE, 0x1FAE, 9826, new(2.9f, 0, 64.9f), new(3.1f, 0, 30)),
        })
            run($"Skalla {action} actual shape both views and cancellation", () =>
            {
                using var f = Prepare(boss); var center = Center(boss);
                f.Actors[10] = new(10, oid, center); Scan(f); Cast(f, 10, action, 5);
                var native = f.Draw.Previews.Single().Value.Geometry; var world = HostDrawingGeometry.Convert(native);
                Check(native.Contains(center + point) && !native.Contains(center + outside)
                    && world.Contains(center + point) && !world.Contains(center + outside), "AOE shape differs.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled shape retained.");
            });
        run("Skalla invincible boss target ban add priority status removal and transformed duty selection", () =>
        {
            using var f = Prepare(0x1FAC); var center = Center(0x1FAC);
            f.Actors[1] = f.Actors[1] with { Statuses = [325] };
            f.Actors[10] = new(10, 0x1FAD, center + Vector3.UnitZ);
            f.Actors[11] = new(11, 0x1FAD, center - Vector3.UnitZ * 8); Scan(f);
            Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]) && f.Engine.PriorityTarget(1, center) == 10
                && f.Engine.SkallaSubservient?.Id == 10 && f.Engine.SkallaShatterstoneTarget == null, "Invincibility or add ordering differs.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 1448, Timestamp = f.Now }, f.Now);
            Check(f.Engine.SkallaShatterstoneTarget?.Id == 10, "Transform event not consumed.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 1, StatusId = 325, Timestamp = f.Now }, f.Now);
            Check(f.Engine.ForbiddenTargets.Length == 0, "Invincibility removal did not clear attack ban.");
        });
        run("Skalla duty execution gates range casting transformed status and rate", () =>
        {
            using var f = Prepare(0x1FAC); var center = Center(0x1FAC);
            f.Actors[100] = f.Actors[100] with { Statuses = [1448] };
            f.Actors[10] = new(10, 0x1FAD, center + Vector3.UnitZ); Scan(f);
            var control = new SkallaDutyControl(); var fired = 0;
            ReplayActorState player = new() { Id = 100, Position = center };
            bool CastDuty(Vector3 position) { Check(position == f.Actors[10].Position, "Wrong duty target location."); ++fired; return true; }
            control.Update(f.Engine, player, true, f.Now, CastDuty);
            control.Update(f.Engine, player, true, f.Now.AddSeconds(.1), CastDuty); Check(fired == 1, "Duty rate missing.");
            control.Update(f.Engine, player, false, f.Now.AddSeconds(1), CastDuty); Check(fired == 1 && control.Attempts == 0, "Disabled duty fired.");
            control.Update(f.Engine, player with { Dead = true }, true, f.Now.AddSeconds(2), CastDuty); Check(fired == 1, "Dead player duty fired.");
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 10 }; Scan(f);
            control.Update(f.Engine, player, true, f.Now.AddSeconds(3), CastDuty); Check(fired == 1, "Out of range duty fired.");
            f.Actors[100] = f.Actors[100] with { Position = center, Statuses = [] }; Scan(f);
            control.Update(f.Engine, player, true, f.Now.AddSeconds(4), CastDuty); Check(fired == 1, "Untransformed duty fired.");
        });
        run("Skalla gaze facing begins in final window then releases on finish", () =>
        {
            using var f = Prepare(0x1FAE); var center = Center(0x1FAE);
            f.Actors[100] = f.Actors[100] with { Position = center - Vector3.UnitZ * 8 }; Scan(f); Cast(f, 1, 9829, 3);
            Check(f.Engine.GazeFacing(f.Actors[100].Position, 0) == null && f.Draw.Previews.Count > 0, "Gaze activated too early or not drawn.");
            f.Now = f.Start.AddSeconds(3); f.Engine.Tick(f.Now);
            var angle = f.Engine.GazeFacing(f.Actors[100].Position, 0);
            Check(angle != null && MathF.Abs(MathF.Atan2(MathF.Sin(angle.Value), MathF.Cos(angle.Value))) > MathF.PI / 4, "Gaze facing unsafe.");
            f.Engine.Finish(1); Check(f.Engine.GazeFacing(f.Actors[100].Position, 0) == null, "Gaze finish retained.");
        });
        foreach (var icon in new uint[] { 121, 122, 28 })
            run($"Skalla chaos icon {icon} original time fixed rotation and viewer navigation", () =>
            {
                using var f = Prepare(0x1FAE); var center = Center(0x1FAE);
                f.Actors[100] = f.Actors[100] with { Rotation = .7f };
                f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false); Scan(f);
                f.Engine.Icon(100, icon, f.Start.AddSeconds(-.2)); f.Render();
                var display = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display"));
                Check(Math.Abs((display.Activation!.Value - f.Start.AddSeconds(4.5)).TotalMilliseconds) < 1 && display.Rotation == MathF.PI,
                    "Chaos clock/rotation differs.");
                Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith(icon == 121 ? "/center" : "/edge")), "Personal bait constraint absent.");
                if (icon != 121) Solve(f, p => Vector3.Distance(center, p) >= 16);
                f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")), "Other viewer bait avoidance missing.");
                f.Engine.Effect(10, icon == 121 ? 9831u : 9832u, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Chaos effect retained.");
            });
        run("Skalla chaos mixed effect clears circle and cross together but preserves donut", () =>
        {
            using var f = Prepare(0x1FAE); var center = Center(0x1FAE);
            f.Engine.Icon(100, 121, f.Start); f.Engine.Icon(100, 122, f.Start); f.Engine.Icon(100, 28, f.Start); f.Render();
            f.Engine.Effect(10, 9833, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().Any(a => a.Id.Contains("/121/")), "Mixed chaos clearing differs.");
            f.Engine.ResetFight(); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Reset leaked.");
        });
    }

    private static Vector3 Center(uint boss) => boss switch { 0x1FAB => new(-220, 0, 4), 0x1FAC => new(115, 0, 4), _ => new(479, 0, 4) };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1172); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3));
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position), "Real solver did not find required position.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

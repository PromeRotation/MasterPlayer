using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Heavensward.Dungeon.D17BaelsarsWall;

internal static class BaelsarRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Baelsar Griffin exact arena matches source in radar and world", () =>
        {
            var source = (BossMod.ArenaBoundsCustom)typeof(Source.D173TheGriffin.D173TheGriffin)
                .GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var native = BaelsarArenas.Griffin(0);
            var host = HostDrawingGeometry.Convert(native);
            var bounds = native.GetBounds();
            for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .43f)
                for (var z = bounds.MinZ + .273f; z < bounds.MaxZ; z += .43f)
                {
                    var p = new Vector3(x, 0, z);
                    var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                    Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Arena differs at {p}.");
                }
        });
        run("Baelsar remade territory owns three bosses and exact square bounds", () =>
        {
            foreach (var oid in new uint[] { 0x1938, 0x193A, 0x193D })
                Check(ArrDungeonCatalog.Owns(oid, 1114) && !ArrDungeonCatalog.Owns(oid, 615), "Wrong ownership.");
            foreach (var oid in new uint[] { 0x1938, 0x193A })
            {
                using var f = Prepare(oid);
                Check(f.Engine.Arena!.Contains(Center(oid) + new Vector3(19.49f, 0, 19.49f))
                    && !f.Engine.Arena.Contains(Center(oid) + Vector3.UnitX * 19.51f), "Square bounds differ.");
            }
        });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0x1938u, 0x1938u, 7347u, 42.94f), (0x193Au, 0x193Au, 31469u, 5f),
            (0x193Au, 0x193Au, 7355u, 5f), (0x193Au, 0x193Bu, 7360u, 40.94f),
            (0x193Du, 0x193Du, 7365u, 41.5f), (0x193Du, 0x193Fu, 7372u, 9f),
        })
            run($"Baelsar cast {action} extent clock both views single margin and cancel", () =>
            {
                using var f = Prepare(boss);
                var source = boss == caster ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Center(boss)); Scan(f); }
                Cast(f, source, action, 3, MathF.PI / 2);
                var shape = f.Draw.Previews.Single().Value.Geometry;
                var location = f.Engine.Casts[source].Location;
                Check(shape.Contains(location + Vector3.UnitX * (range - .01f))
                    && HostDrawingGeometry.Convert(shape).Contains(location + Vector3.UnitX * (range - .01f))
                    && !shape.Contains(location + Vector3.UnitX * (range + .01f)), "Wrong extent.");
                var area = f.Snapshot.DangerAreas.Single();
                Check(area.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3)
                    && area.Geometry.Contains(location + Vector3.UnitX * (range + .4f)), "Cast clock or margin differs.");
                f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast retained output.");
            });
        run("Baelsar Predator prey uses event timestamp five-second damage and effect latch", () =>
        {
            using var f = Prepare(0x1938);
            f.Now = f.Start.AddSeconds(.4);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 562, Timestamp = f.Start }, f.Now); f.Render();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(5)
                && f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 0, "Prey timing/display missing.");
            f.Actors[100] = f.Actors[100] with { Statuses = [562] }; Scan(f);
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(5), "Repeated snapshot moved prey activation.");
            f.Engine.Effect(1, 7348, 1, f.Now, 100); Scan(f);
            Check(!f.Engine.PredictedDamage.Any() && f.Draw.Previews.Count == 0, "Resolved prey resurrected.");
            f.Engine.StatusChanged(100, 562, false, f.Now);
            f.Now = f.Start.AddSeconds(10); f.Engine.StatusChanged(100, 562, true, f.Now); f.Render();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(15), "Next prey did not restart.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(!f.Engine.PredictedDamage.Any(), "Dead prey target retained prediction.");
        });
        run("Baelsar Predator reinforces add priority while single target busters predict party target", () =>
        {
            using var f = Prepare(0x1938);
            f.Actors[10] = new(10, 0x1939, Center(0x1938)); Scan(f); Cast(f, 1, 7346, 4, target: 100);
            Check(f.Engine.PriorityTarget(1, Center(0x1938)) == 10 && f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(4.3),
                "Predator add or buster missing.");
        });
        run("Baelsar caution three-second warning one-second all-stop and removal release", () =>
        {
            using var f = Prepare(0x193A);
            f.Actors[100] = f.Actors[100] with { Statuses = [1132],
                StatusExpirations = new Dictionary<uint, DateTime> { [1132] = f.Start.AddSeconds(10) } };
            Scan(f); Check(!f.Engine.RequireAllStop && !f.Engine.Hints(null).Any(h => h.Contains("极度警戒")), "Caution acted too early.");
            f.Now = f.Start.AddSeconds(7); Scan(f);
            Check(!f.Engine.RequireAllStop && f.Engine.Hints(null).Any(h => h.Contains("准备停止")), "Three-second warning missing.");
            f.Now = f.Start.AddSeconds(8.999); Scan(f); Check(!f.Engine.RequireAllStop, "Stop window began early.");
            f.Now = f.Start.AddSeconds(9); Scan(f); Check(f.Engine.RequireAllStop, "One-second all-stop missing.");
            f.Now = f.Start.AddSeconds(10.1); Scan(f); Check(f.Engine.RequireAllStop, "All-stop expired before status removal.");
            f.Engine.StatusChanged(100, 1132, false, f.Now); f.Render();
            Check(!f.Engine.RequireAllStop, "Status removal did not release stop.");
        });
        run("Baelsar caution status packet milliseconds refresh viewer switch and missing expiry", () =>
        {
            using var f = Prepare(0x193A);
            f.Now = f.Start.AddSeconds(.2);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 1132, Timestamp = f.Start, DurationMilliseconds = 1000 }, f.Now);
            Check(f.Engine.AllStopActivation == f.Start.AddSeconds(1) && f.Engine.RequireAllStop, "Packet duration or timestamp ignored.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 1132, Timestamp = f.Now, Duration = 10 }, f.Now);
            Check(!f.Engine.RequireAllStop, "Refreshed caution did not release early hold.");
            f.Actors[101] = new(101, 0, Center(0x193A), PartySlot: 1, Hostile: false); f.PlayerId = 101; Scan(f);
            Check(!f.Engine.RequireAllStop && f.Engine.AllStopActivation == null, "Old perspective stop leaked.");
            f.Engine.StatusChanged(101, 1132, true, f.Now); Check(f.Engine.RequireAllStop, "Unknown expiry should stop while affected.");
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f); Check(!f.Engine.RequireAllStop, "Dead viewer retains stop.");
        });
        run("Baelsar FlashPowder uses PR gaze timing and joins simultaneous directions", () =>
        {
            using var f = Prepare(0x193D);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x193D) + Vector3.UnitX * 5 }; Scan(f);
            Cast(f, 1, 7364, 3);
            var player = f.Actors[100].Position;
            Check(f.Engine.GazeFacing(player, -MathF.PI / 2) == null, "Gaze turned before reaction window.");
            f.Now = f.Start.AddSeconds(2.55); f.Engine.Tick(f.Now);
            Check(f.Engine.GazeFacing(player, -MathF.PI / 2) is { } face && MathF.Sin(face) > 0, "Gaze did not face away.");
            f.Actors[10] = new(10, 0x193D, Center(0x193D) + Vector3.UnitX * 10); Scan(f);
            Cast(f, 10, 7364, 0);
            var joint = f.Engine.GazeFacing(player, 0);
            Check(joint != null && MathF.Abs(MathF.Cos(joint.Value)) > .7f, "Joint gaze directions conflict.");
            f.Engine.Finish(1); f.Engine.Finish(10); f.Render();
            Check(f.Engine.GazeFacing(player, 0) == null && f.Draw.Previews.Count == 0, "Cancelled gaze persisted.");
        });
        run("Baelsar nine live blades suppress Corrosion then dead blade reveals remaining casts", () =>
        {
            using var f = Prepare(0x193D);
            for (var i = 0; i < 9; ++i) f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), 0x193F, Center(0x193D) + Vector3.UnitX * (i - 4));
            Scan(f);
            for (ulong i = 10; i < 19; ++i) Cast(f, i, 7372, 20);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Nine intact blades drew unsafe all-arena coverage.");
            f.Actors[14] = f.Actors[14] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 8 && f.Snapshot.DangerAreas.Count == 8, "Dead center blade did not reveal remaining AOEs.");
            f.Actors[19] = new(19, 0x193F, Center(0x193D)); Scan(f); Cast(f, 19, 7372, 20);
            Check(f.Draw.Previews.Count == 9, "Ten blades including one dead incorrectly suppressed.");
            foreach (var i in f.Engine.Casts.Keys.ToArray()) f.Engine.Finish(i);
            f.Render(); Check(f.Draw.Previews.Count == 0, "Blade cancellation leaked.");
        });
        run("Baelsar blade priority uses five-yard square and collar priority", () =>
        {
            using var f = Prepare(0x193D);
            var center = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x193F, center + new Vector3(4.9f, 0, 4.9f));
            f.Actors[11] = new(11, 0x193F, center + Vector3.UnitX * 6); Scan(f);
            Check(f.Engine.PriorityTarget(11, center) == 10, "Central blade check used circular five-yard distance.");
            f.Actors.Remove(10); Scan(f); Check(f.Engine.PriorityTarget(11, center) == 1, "Outer blade remained selectable.");
            f.Actors[12] = new(12, 0x193E, center); Scan(f); Check(f.Engine.PriorityTarget(1, center) == 12, "Collar priority missing.");
        });
        run("Baelsar collar impending fetters status finish and strict rescue distance", () =>
        {
            using var f = Prepare(0x193D);
            f.Actors[101] = new(101, 0, Center(0x193D), PartySlot: 1, Hostile: false, Name: "队友"); Scan(f);
            f.Engine.Icon(101, 1); Check(f.Engine.Hints(null).Any(h => h.Contains("即将被拘束")), "Collar icon missing.");
            f.Engine.StatusChanged(101, 3421, true, f.Now); Scan(f);
            Check(f.Engine.Hints(null).Any(h => h.Contains("打破 队友")), "Fetters status not active.");
            f.Actors[10] = new(10, 0x193E, Center(0x193D) + Vector3.UnitZ * 5, HitboxRadius: 1); Scan(f);
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == 1
                && !f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[10].Position + Vector3.UnitX * 3.59f)
                && f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[10].Position + Vector3.UnitX * 3.61f), "Collar distance became optional goal.");
            Cast(f, 1, 7368, 3); f.Engine.Finish(1, 7368, completed: true);
            Check(f.Engine.Hints(null).Any(h => h.Contains("队友")), "Visual finish prematurely cleared collar target.");
            Cast(f, 10, 7369, 15); f.Engine.Finish(10, 7369, completed: true);
            f.Engine.StatusChanged(101, 3421, false, f.Now); Scan(f);
            Check(!f.Engine.Hints(null).Any(h => h.Contains("队友")), "Unbound target retained hint.");
            f.Engine.RemoveActor(10); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0, "Removed collar distance persists.");
        });
        run("Baelsar collar rescue uses real solver to enter attack range", () =>
        {
            using var f = Prepare(0x193D);
            f.Actors[10] = new(10, 0x193E, Center(0x193D) + Vector3.UnitZ * 8, HitboxRadius: 1); Scan(f);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(Center(0x193D), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && !snapshot.DangerAreas.Single().Geometry.Contains(path[^1].Position), "Rescue solver failed strict attack-range constraint.");
        });
        run("Baelsar BigBoot targets only viewer ignores immunity and persists to actual effect", () =>
        {
            using var f = Prepare(0x193D);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x193D) + Vector3.UnitX * 5, Statuses = [1209],
                StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(20) } };
            f.Actors[101] = new(101, 0, Center(0x193D), PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(100, 22); f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Engine.AmbientAoes().Any(a => a.Id == "bael/boot/avoid"), "Boot ignored icon or incorrectly accepted immunity.");
            var landing = f.Engine.AmbientAoes().Single(a => a.Id == "bael/boot/landing").Position;
            Check(f.Engine.Arena!.Contains(landing, 0) && Vector3.Distance(landing, f.Actors[100].Position) is > 12 and < 15, "Boot did not stop at arena wall.");
            Cast(f, 1, 7367, 3, target: 100); f.Engine.Finish(1, 7367, completed: true); f.Render();
            Check(f.Draw.Previews.Count == 2, "Natural boot finish cleared before actual effect.");
            f.PlayerId = 101; Scan(f); Check(f.Draw.Previews.Count == 0, "Boot leaked into another viewer.");
            f.PlayerId = 100; Scan(f); f.Engine.Effect(1, 7367, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Boot effect did not clear.");
        });
        run("Baelsar cancelled boot target death and fight disable release all output", () =>
        {
            using var f = Prepare(0x193D);
            f.Engine.Icon(100, 22); Cast(f, 1, 7367, 3, target: 100); f.Engine.Finish(1); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled boot retained marker.");
            f.Now = f.Start.AddSeconds(10); f.Engine.Icon(100, 22); f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 0, "Dead boot target retained marker.");
            f.Engine.Synchronize(f.Actors.Values, Center(0x193D), f.Now, _ => false, f.PlayerId); f.Render();
            Check(f.Engine.Active == null && f.Engine.Arena == null && !f.Engine.RequireAllStop, "Disable retained controls.");
        });
        FollowRegression.RunBaelsarControls(run);
    }
    private static Vector3 Center(uint boss) => boss switch
    {
        0x1938 => new(-174, 0, 73), 0x193A => new(116, 0, 0), _ => new(351.235f, 0, 391.855f),
    };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1114); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f)
    {
        f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position ?? Center(f.Actors[1].Oid), f.Now, playerId: f.PlayerId); f.Render();
    }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

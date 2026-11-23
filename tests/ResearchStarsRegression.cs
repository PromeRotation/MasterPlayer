using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class ResearchStarsRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (oid, count, ice) in new[] { (0x3DA5u, 4, true), (0x3DA6u, 5, false) })
            run($"ResearchFacility tutorial {(ice ? "ice" : "fire")} waits for {count} births and resolves once", () =>
            {
                using var f = new Fight(0x3DA4, 1110);
                for (var i = 0; i < count - 1; ++i) f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), oid, Vector3.UnitX * i);
                f.Scan(); Check(!f.Engine.OutputCasts.Any(), "Tutorial forecast started before final birth.");
                var id = (ulong)(10 + count - 1);
                f.Engine.ObjectCreated(id, f.Start.AddSeconds(-.2));
                f.Actors[id] = new(id, oid, Vector3.UnitX * count); f.Scan();
                Check(f.Engine.OutputCasts.Count() == count && f.Engine.OutputCasts.All(c => c.Value.FinishAt == f.Start.AddSeconds(7.6)),
                    "Tutorial birth clock or complete set differs.");
                Check(f.Draw.Previews.Values.All(p => ice ? p.Geometry is DonutGeometry { InnerRadius: 5, OuterRadius: 15 }
                    : p.Geometry is CircleGeometry { Radius: 8 }), "Tutorial true size differs.");
                f.Engine.Effect(10, ice ? 31879u : 31887u, 1, f.Now); f.Scan();
                Check(!f.Engine.OutputCasts.Any() && f.Draw.Previews.Count == 0, "Resolved stars resurrected.");
            });
        run("ResearchFacility fused ice uses quantized midpoint original tether clock and remaining small star", () =>
        {
            using var f = new Fight(0x3DA4, 1110);
            f.Actors[10] = new(10, 0x3DA5, new(220, 0, -181));
            f.Actors[11] = new(11, 0x3DA5, new(240, 0, -181));
            f.Actors[12] = new(12, 0x3DA5, new(230, 0, -170)); f.Scan();
            f.Now = f.Start.AddSeconds(.5);
            f.Engine.Tether(11, 110, true, f.Start, 10); f.Render();
            var casts = f.Engine.OutputCasts.Select(c => c.Value).ToArray();
            Check(casts.Length == 2 && casts.All(c => c.FinishAt == f.Start.AddSeconds(10.6))
                && casts.Any(c => c.Rule.Shape is DonutTemplate { OuterRadius: 40 }) && casts.Any(c => c.Rule.Shape is DonutTemplate { OuterRadius: 15 }),
                "Fused and remaining star prediction differs.");
            Check(Math.Abs(casts[0].Location.X - 230) < .04f && casts[1].Location == f.Actors[12].Position, "Midpoint or small origin differs.");
            f.Engine.Tether(10, 110, true, f.Start, 11); f.Render();
            Check(f.Engine.OutputCasts.Count() == 2, "Symmetric tether duplicated pair.");
            f.Engine.Effect(10, 33019, 1, f.Now); f.Scan();
            Check(!f.Engine.OutputCasts.Any(), "Fused resolution leaked.");
        });
        foreach (var iceFirst in new[] { true, false })
            run($"ResearchFacility Dualstar {(iceFirst ? "ice then fire" : "fire then ice")} preserves 11.8 and 14.8 waves", () =>
            {
                using var f = new Fight(0x3DA7, 1110);
                f.Actors[10] = new(10, 0x3DA8, new(230, 0, iceFirst ? 86 : 92));
                f.Actors[11] = new(11, 0x233C, Vector3.Zero);
                f.Actors[12] = new(12, 0x233C, Vector3.Zero); f.Scan();
                var first = f.Engine.OutputCasts.Where(c => c.Value.Rule.Shape != null).ToArray();
                Check(first.Length == (iceFirst ? 1 : 2) && first.All(c => c.Value.FinishAt == f.Start.AddSeconds(11.8)),
                    "First wave count, order or clock differs.");
                var action = iceFirst ? 33021u : 33022u;
                f.Cast(11, action, 2); f.Engine.Finish(11, action, completed: true);
                if (!iceFirst) { f.Cast(12, action, 2); f.Engine.Finish(12, action, completed: true); }
                f.Render();
                var next = f.Engine.OutputCasts.Where(c => c.Value.Rule.Shape != null).ToArray();
                Check(next.Length == (iceFirst ? 2 : 1) && next.All(c => c.Value.FinishAt == f.Start.AddSeconds(14.8)),
                    "Second wave was flattened, lost or advanced early.");
                Check(next.All(c => iceFirst ? c.Value.Rule.Shape is CircleTemplate { Radius: 16 } : c.Value.Rule.Shape is DonutTemplate { InnerRadius: 5, OuterRadius: 40 }),
                    "Second wave true shape differs.");
            });
        run("ResearchFacility Dualstar birth correction changes both future waves without revealing the second", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            f.Now = f.Start.AddSeconds(.5); f.Actors[10] = new(10, 0x3DA8, new(230, 0, 86)); f.Scan();
            f.Engine.ObjectCreated(10, f.Start); f.Render();
            Check(f.Engine.OutputCasts.Single().Value.FinishAt == f.Start.AddSeconds(11.8), "Late object event did not correct original clock.");
        });
        run("ResearchFacility Dualstar cancellation preserves wave and effect without cast advances once", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            f.Actors[10] = new(10, 0x3DA8, new(230, 0, 86));
            f.Actors[11] = new(11, 0x233C, new(230, 0, 79)); f.Scan();
            f.Cast(11, 33021, 3); f.Engine.Finish(11); f.Render();
            Check(f.Engine.OutputCasts.Single().Value.Rule.Shape is DonutTemplate, "Cancelled helper consumed the ice wave.");
            f.Engine.Effect(11, 33021, 1, f.Start.AddSeconds(11.8)); f.Render();
            Check(f.Engine.OutputCasts.Count() == 2 && f.Engine.OutputCasts.All(c => c.Value.Rule.Action == 33022),
                "Effect without a cast did not advance to fire.");
            f.Engine.Effect(11, 33021, 1, f.Start.AddSeconds(11.8)); f.Render();
            Check(f.Engine.OutputCasts.Count() == 2, "Repeated effect advanced another star.");
        });
        run("ResearchFacility Dualstar effect plus finish never consumes a second same-action star", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            f.Actors[10] = new(10, 0x3DA8, new(230, 0, 92)); f.Actors[11] = new(11, 0x233C, Vector3.Zero); f.Scan();
            f.Cast(11, 33022, 3);
            f.Engine.Effect(11, 33022, 1, f.Now);
            f.Engine.Finish(11, 33022, completed: true); f.Render();
            Check(f.Engine.OutputCasts.Single().Value.Rule.Shape is CircleTemplate, "Effect and finish double-consumed fire.");
        });
        run("ResearchFacility Dualstar missing helper resolve expires only the first wave after grace", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            f.Actors[10] = new(10, 0x3DA8, new(230, 0, 86)); f.Scan();
            f.Now = f.Start.AddSeconds(13.81); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.OutputCasts.Count() == 2 && f.Draw.Previews.Count == 2
                && f.Engine.OutputCasts.All(c => c.Value.FinishAt == f.Start.AddSeconds(14.8)), "Missing ice resolve blocked the next wave.");
            f.Now = f.Start.AddSeconds(16.81); f.Engine.Tick(f.Now); f.Render();
            Check(!f.Engine.OutputCasts.Any() && f.Draw.Previews.Count == 0, "Lost final resolve leaked stars.");
        });
        run("ResearchFacility missing icon and line resolves release movement goals and stale icons cannot revive", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitZ * 10, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x233C, Vector3.Zero); f.Scan();
            f.Engine.Icon(101, 384, f.Start);
            f.Engine.Icon(101, 139, f.Start);
            f.Engine.Icon(101, 161, f.Start);
            f.Engine.Effect(10, 31907, 1, f.Now, 101); f.Render();
            Check(f.Draw.Previews.Count == 4 && f.Snapshot.GoalAreas.Count > 0, "Initial markers missing.");
            f.Now = f.Start.AddSeconds(10.01); f.Engine.Tick(f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == 0,
                "Lost resolves left permanent movement output.");
            f.Engine.Effect(10, 31901, 2, f.Now, 101);
            f.Engine.Effect(10, 31903, 3, f.Now, 101);
            f.Engine.Effect(10, 31904, 4, f.Now, 101);
            f.Engine.Effect(10, 32555, 5, f.Now, 101);
            f.Engine.Icon(101, 384, f.Start);
            f.Engine.Icon(101, 139, f.Start);
            f.Engine.Icon(101, 161, f.Start);
            f.Engine.Effect(10, 31907, 6, f.Now, 101, occurredAt: f.Start); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Late old markers revived resolved output.");
        });
        run("ResearchFacility AncientCircle draws true donut while navigating into the shared 2.5 radius", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false); f.Scan();
            f.Engine.Icon(100, 384, f.Start); f.Engine.Icon(101, 384, f.Start); f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Draw.Previews.Values.All(p => p.Geometry is DonutGeometry { InnerRadius: 10, OuterRadius: 20 })
                && f.Engine.AmbientAoes().Single(a => a.Navigate).Shape is CircleTemplate { Radius: 2.5f },
                "Donut display or close stack goal differs.");
            f.Engine.Effect(10, 31901, 1, f.Now, 0); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Self-targeted fallback failed to clear donuts.");
        });
        run("ResearchFacility DarkWhispers only separates marked players from unmarked allies", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            for (var i = 0; i < 3; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, Vector3.UnitX * i * 8, PartySlot: i, Hostile: false);
            f.Scan(); f.Engine.Icon(100, 139, f.Start); f.Engine.Icon(101, 139, f.Start); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 2
                && f.Snapshot.DangerAreas.All(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5)), "Spread count or original clock differs.");
            f.Engine.Effect(10, 31903, 1, f.Now); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Spread effect leaked.");
        });
        foreach (var (boss, marker, resolve, delay) in new[] { (0x3DA4u, 31892u, 33029u, 5.1), (0x3DA7u, 31907u, 32555u, 5.2) })
            run($"ResearchFacility line stack {marker} tracks target and uses original {delay} clock", () =>
            {
                using var f = new Fight(boss, 1110);
                f.Actors[10] = new(10, 0x233C, Vector3.Zero);
                f.Actors[100] = new(100, 0, Vector3.UnitX * 5, PartySlot: 0, Hostile: false);
                f.Actors[101] = new(101, 0, Vector3.UnitZ * 20, PartySlot: 1, Hostile: false); f.Scan();
                f.Engine.Effect(10, marker, 1, f.Now, 101, occurredAt: f.Start); f.Render();
                Check(f.Snapshot.GoalAreas.Count == 1 && f.Draw.Previews.Values.Single().Geometry.Contains(Vector3.UnitZ * 49),
                    "Line stack goal or full true length differs.");
                Check(f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(delay)), "Line stack timing differs.");
                f.Engine.Effect(10, resolve, 2, f.Now, 101); f.Render();
                Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0, "Line stack resolution leaked.");
            });
        run("ResearchFacility BurningChains reuse native symmetric chain control and cancellation", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false); f.Scan();
            f.Engine.Tether(101, 9, true, f.Now, 100); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 1, "Chain geometry missing.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 1, "Reverse partner lost personal output.");
            f.Engine.Tether(101, 9, false, f.Now, 100); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Chain cancellation leaked.");
        });
        run("ResearchFacility Dualstar second wave produces a real escape route in its three second interval", () =>
        {
            using var f = new Fight(0x3DA7, 1110);
            var player = new Vector3(230, 0, 79);
            f.Actors[100] = new(100, 0, player, PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0x3DA8, new(230, 0, 86));
            f.Actors[11] = new(11, 0x233C, player); f.Scan();
            f.Now = f.Start.AddSeconds(11.8);
            f.Cast(11, 33021, 2); f.Engine.Finish(11, 33021, completed: true); f.Render();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && snapshot.DangerAreas.All(a => !a.Geometry.Contains(path[^1].Position)) && f.Engine.Arena!.Contains(path[^1].Position),
                $"No second-wave escape: {solution.Status}, fallback={solution.IsLeastDangerFallback}.");
        });
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}

using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;
using Claviger = BossMod.RealmReborn.Dungeon.D06Haukke.D061ManorClaviger.D061ManorClaviger;
using Steward = BossMod.RealmReborn.Dungeon.D06Haukke.D062ManorSteward.D062ManorSteward;
using Amandine = BossMod.RealmReborn.Dungeon.D06Haukke.D063LadyAmandine.D063LadyAmandine;

internal static class ArrDungeonAdvancedRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("native ARR Haukke all three arena polygons match BMR", () =>
        {
            foreach (var (geometry, original) in new (IGeometryShape, BossMod.ArenaBoundsCustom)[]
            {
                (HaukkeArenas.Claviger(5), Claviger.arena), (HaukkeArenas.Steward(5), Steward.arena),
                (HaukkeArenas.Amandine(5), Amandine.arena),
            })
            {
                var bounds = geometry.GetBounds();
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .7f)
                    for (var z = bounds.MinZ + .231f; z < bounds.MaxZ; z += .7f)
                        Check(geometry.Contains(new(x, 5, z), 0) == original.Contains(new(x - original.Center.X, z - original.Center.Z)),
                            $"Haukke arena mismatch {x}, {z}.");
            }
        });
        run("native ARR Totorak forward rectangle uses locked cast rotation and exact dimensions", () =>
        {
            using var f = new Fight(0x103, 1039);
            f.Cast(1, 701, 2.5, MathF.PI / 2);
            var geometry = f.Draw.Previews.Single().Value.Geometry;
            Check(geometry.Contains(f.Center + new Vector3(17.9f, 0, 1.9f)), "Silkscreen range/half width lost.");
            Check(!geometry.Contains(f.Center + new Vector3(-1, 0, 0))
                && !geometry.Contains(f.Center + new Vector3(10, 0, 2.1f)), "Silkscreen became centered rectangle or doubled width.");
            f.Engine.Finish(1);
            f.Actors[1] = f.Actors[1] with { Cast = null };
            f.Now = f.Now.AddSeconds(1);
            f.Engine.StartCast(1, new(701, f.Center, null, f.Now, f.Now.AddSeconds(2.8)), f.Now);
            Check(f.Engine.Casts.Count == 0, "Missing locked rectangle rotation produced fabricated facing.");
        });
        run("native ARR Totorak pollen is persistent, follows objects and clears independently from pods", () =>
        {
            using var f = new Fight(0x103, 1039);
            f.Actors[10] = new(10, 0x1E8614, f.Center + Vector3.UnitX * 6, Targetable: false);
            f.Actors[11] = new(11, 0x23C, f.Center);
            f.Scan();
            f.Cast(11, 730, 3);
            Check(f.Snapshot.DangerAreas.Count == 2 && f.Draw.Previews.Count == 2, "Pollen/PodBurst outputs missing.");
            var pollen = f.Snapshot.DangerAreas.Single(a => a.Geometry.Position.X == f.Center.X + 6);
            f.Scan();
            Check(f.Snapshot.DangerAreas.Any(a => a.Handle == pollen.Handle), "Stable persistent hazard churned handles.");
            f.Actors[10] = f.Actors[10] with { Position = f.Center - Vector3.UnitX * 6 };
            f.Scan();
            Check(f.Snapshot.DangerAreas.Any(a => a.Geometry.Position.X == f.Center.X - 6), "Pollen failed to follow its object.");
            f.Actors.Remove(10);
            f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 1, "Removing pollen removed an unrelated pod.");
            f.Complete(11, 3);
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Pod finish leaked.");
        });
        run("native ARR Totorak and Haukke target priorities preserve equal-priority current targets", () =>
        {
            using var f = new Fight(0x103, 1039);
            f.Actors[10] = new(10, 0x104, f.Center);
            f.Scan();
            Check(f.Engine.PriorityTarget(1, f.Center) == 1, "Boss/add tie unnecessarily changed current target.");
            f.Actors[11] = new(11, 0x10A, f.Center + Vector3.UnitX * 2);
            f.Scan();
            Check(f.Engine.PriorityTarget(1, f.Center) is 10 or 11, "Tail phase failed to prioritize adds.");
            Check(f.Engine.PriorityTarget(10, f.Center) == 10, "Equal-priority add target was replaced.");
            using var steward = new Fight(0x112, 1040);
            steward.Actors[10] = new(10, 0x111, Vector3.UnitX);
            steward.Scan();
            Check(steward.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Manor Jester priority was lost.");
            using var lady = new Fight(0x38A8, 1040);
            lady.Actors[10] = new(10, 0x38AA, Vector3.UnitX);
            lady.Scan();
            Check(lady.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Handmaiden priority was lost.");
            lady.Actors[10] = lady.Actors[10] with { Dead = true };
            lady.Scan();
            Check(lady.Engine.PriorityTarget(10, Vector3.Zero) == 1, "Dead add remained preferred.");
        });
        run("native ARR Haukke cleave distinguishes target tank from party and draws only true outline", () =>
        {
            using var f = new Fight(0x110, 1040);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[100] = new(100, 0, new(0, 0, 3), PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, new(0, 0, -5), PartySlot: 1, Hostile: false);
            f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.OutlineOnly, "Cleave must be outlined, not filled.");
            Check(f.Draw.Previews.Single().Value.Geometry is FanGeometry { Radius: 7.4f }, "Cleave preview radius changed.");
            Check(f.Engine.AmbientAoes().Single(a => a.Navigate).Shape is MasterPlayer.Spatial.SafeZone.FanTemplate { Radius: 100 }, "Tank failed to avoid pointing towards party.");
            f.PlayerId = 101;
            f.Scan();
            Check(f.Engine.AmbientAoes().Single(a => a.Navigate).Shape is MasterPlayer.Spatial.SafeZone.FanTemplate { Radius: 7.4f }, "Non-target player did not dodge actual cleave.");
            Check(f.Snapshot.DangerAreas.Count == 1, "Cleave created duplicate planning areas.");
            f.Actors[1] = f.Actors[1] with { Targetable = false };
            f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Untargetable cleaver retained hazard.");
        });
        run("native ARR Haukke gaze honors 0.75s lead, multiple eyes, cancel and non-spatial display", () =>
        {
            using var f = new Fight(0x38A8, 1040);
            f.Actors[10] = new(10, 0x38A9, new(0, 0, 5));
            f.Actors[11] = new(11, 0x38A9, new(0, 0, -5));
            f.Scan();
            f.Engine.StartCast(10, new(28648, new(0, 0, 5), 0, f.Start, f.Start.AddSeconds(5.3)), f.Now);
            f.Engine.StartCast(11, new(28648, new(0, 0, -5), 0, f.Start, f.Start.AddSeconds(5.3)), f.Now);
            f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 0, "Gaze became a spatial hazard or lost its eye marker.");
            Check(f.Engine.GazeFacing(Vector3.Zero, 0) == null, "Gaze faced away before lead window.");
            f.Now = f.Start.AddSeconds(4.6);
            f.Engine.Tick(f.Now);
            var heading = f.Engine.GazeFacing(Vector3.Zero, 0);
            Check(heading != null && Math.Abs(MathF.Cos(heading.Value)) < .7071f, "Chosen heading faces one of two opposing eyes.");
            f.Engine.Finish(10);
            f.Engine.Finish(11);
            f.Render();
            Check(f.Engine.GazeFacing(Vector3.Zero, 0) == null && f.Draw.Previews.Count == 0, "Cancelled gaze retained facing/display.");
        });
        foreach (var (boss, action, radius) in new[] { (0x110u, 855u, 5f), (0x110u, 705u, 9.4f), (0x112u, 860u, 9f), (0x38A8u, 28646u, 9f) })
            run($"native ARR Haukke AID {action} true radius and cast timing", () =>
            {
                using var f = new Fight(boss, 1040);
                f.Cast(1, action, 4);
                Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry circle && circle.Radius == radius, "Wrong display radius.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3), "Cast timing shifted.");
            });
        run("Halatali actual solver dodges each concentric stage while preserving its inner safe ring", () =>
        {
            using var f = new Fight(0x4645);
            f.Actors[10] = new(10, 0x233C, f.Center);
            f.Actors[11] = new(11, 0x233C, f.Center);
            f.Scan();
            f.Cast(1, 40599, 5);
            f.Cast(10, 40600, 8);
            f.Cast(11, 40601, 11);
            foreach (var (stage, distance) in new[] { (0, 0f), (1, 15f), (2, 25f) })
            {
                if (stage == 1) f.Complete(1, 5);
                if (stage == 2) f.Complete(10, 8);
                var snapshot = f.Snapshot;
                var player = f.Center + Vector3.UnitX * distance;
                var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                    new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
                Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No route for stage " + stage);
                var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
                Check(path.Count > 1 && !snapshot.DangerAreas.Single().Geometry.Contains(path[^1].Position), "Stage route ends in damage.");
                Check(path.All(p => f.Engine.Arena!.Contains(p.Position)), "Stage route crossed arena wall.");
            }
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

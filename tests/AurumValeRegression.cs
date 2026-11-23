using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class AurumValeRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Aurum Vale three remade arenas preserve original concave boundaries in both views", () =>
        {
            foreach (var (geometry, name) in new[]
            {
                (AurumValeArenas.Locksmith(3), "D121Locksmith"), (AurumValeArenas.Coincounter(3), "D122Coincounter"),
                (AurumValeArenas.Mistress(3), "D123MisersMistress"),
            })
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D12AurumVale.D121Locksmith.D121Locksmith).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D12AurumVale.{name}.{name}")!;
                var tuple = type.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                var original = (BossMod.ArenaBoundsCustom)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
                var host = HostDrawingGeometry.Convert(geometry);
                var bounds = geometry.GetBounds();
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .7f)
                    for (var z = bounds.MinZ + .231f; z < bounds.MaxZ; z += .7f)
                    {
                        var expected = original.Contains(new(x - original.Center.X, z - original.Center.Z));
                        Check(geometry.Contains(new(x, 3, z)) == expected && host.Contains(new(x, 3, z)) == expected,
                            $"Arena {name} changed at {x}, {z}.");
                    }
            }
        });
        foreach (var (boss, action, radius) in new[]
        {
            (0x4B0Cu, 45741u, 8f), (0x4B0Du, 45745u, 10f), (0x4B0Du, 45744u, 10f),
            (0x4B0Du, 45746u, 12f), (0x4B0Eu, 45996u, 15.85f),
        })
            run($"Aurum Vale AID {action} native radius and independent display/navigation timing", () =>
            {
                using var f = new Fight(boss, 1331);
                f.Cast(1, action, 4, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !geometry.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Actual radius differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3), "NPC delay differs.");
            });
        run("Aurum Vale Glower is a forward twenty-one by seven rectangle", () =>
        {
            using var f = new Fight(0x4B0D, 1331);
            f.Cast(1, 45743, 3, MathF.PI / 2);
            var geometry = f.Draw.Previews.Single().Value.Geometry;
            Check(geometry.Contains(f.Center + new Vector3(20.99f, 0, 3.49f))
                && !geometry.Contains(f.Center - Vector3.UnitX)
                && !geometry.Contains(f.Center + new Vector3(10, 0, 3.51f)), "Glower dimensions differ.");
        });
        run("Aurum Vale Beholder preserves locked rotation, 270 degree sector and eight-to-nineteen ring", () =>
        {
            using var f = new Fight(0x4B0D, 1331);
            f.Cast(1, 45747, 2.5, MathF.PI / 2);
            var geometry = f.Draw.Previews.Single().Value.Geometry;
            var host = HostDrawingGeometry.Convert(geometry);
            foreach (var p in new[] { Vector3.UnitX * 12, Vector3.UnitZ * 12 })
                Check(geometry.Contains(f.Center + p) && host.Contains(f.Center + p), "Sector lost a forward/side point.");
            Check(!geometry.Contains(f.Center - Vector3.UnitX * 12)
                && !geometry.Contains(f.Center + Vector3.UnitX * 7.99f)
                && !geometry.Contains(f.Center + Vector3.UnitX * 19.01f), "Sector safe hole/gap changed.");
            var navigation = f.Snapshot.DangerAreas.Single().Geometry;
            Check(navigation.Contains(f.Center + Vector3.UnitX * 7.6f)
                && navigation.Contains(f.Center + Vector3.UnitX * 19.4f), "Donut sector navigation margin missing.");
        });
        foreach (var (boss, status, first, second) in new[]
        {
            (0x4B0Cu, 302u, 0x1E8ED9u, 0x1E8ED7u), (0x4B0Eu, 303u, 0x1E8EE3u, 0x1E8EE1u),
        })
            run($"Aurum Vale boss {boss:X} fruit cleansing respects three stacks, perspective, targetability and status loss", () =>
            {
                using var f = new Fight(boss, 1331);
                f.Actors[100] = new(100, 0, f.Center, Hostile: false, Statuses: [status],
                    StatusParams: new Dictionary<uint, ushort> { [status] = 2 });
                f.Actors[101] = new(101, 0, f.Center, Hostile: false);
                f.Actors[10] = new(10, first, f.Center + Vector3.UnitX * 5, Hostile: false);
                f.Actors[11] = new(11, second, f.Center + Vector3.UnitX * 10, Hostile: false); f.Scan();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Two stacks triggered fruit consumption.");
                f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = status, Param = 3, Timestamp = f.Now }, f.Now);
                f.Render();
                Check(f.Draw.Previews.Count == 2 && f.Draw.Previews.Values.All(p => p.Safe && p.OutlineOnly)
                    && f.Snapshot.GoalAreas.Count == 1 && f.Engine.FruitTarget?.Id == 10, "Three stacks did not choose nearest usable fruit.");
                Check(f.Snapshot.GoalAreas.Single().Geometry is CircleGeometry { Radius: 2.5f }, "Fruit interaction range lost hitbox.");
                f.PlayerId = 101; f.Scan();
                Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0, "Perspective switch retained another player's cleansing.");
                f.PlayerId = 100; f.Actors[10] = f.Actors[10] with { Targetable = false }; f.Scan();
                Check(f.Engine.FruitTarget?.Id == 11 && f.Draw.Previews.Count == 1, "Consumed fruit retained movement target.");
                f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = status, Timestamp = f.Now }, f.Now); f.Render();
                Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0, "Cleansed status retained fruit output.");
            });
        run("Aurum Vale scanned stack parameters recover fruit guidance without a status packet", () =>
        {
            using var f = new Fight(0x4B0C, 1331);
            f.Actors[100] = new(100, 0, f.Center, Hostile: false, Statuses: [302],
                StatusParams: new Dictionary<uint, ushort> { [302] = 3 });
            f.Actors[10] = new(10, 0x1E8ED8, f.Center + Vector3.UnitX * 5, Hostile: false);
            f.Actors[11] = new(11, 0x1E8EE3, f.Center, Hostile: false); f.Scan();
            Check(f.Engine.FruitTarget?.Id == 10 && f.Draw.Previews.Count == 1, "Wrong boss fruit selected or stack snapshot ignored.");
        });
        run("Aurum Vale fruit interaction gates replay, movement, casting, range and repeat attempts", () =>
        {
            var interaction = new DungeonObjectInteraction();
            var now = DateTime.UtcNow;
            var fruit = new DungeonActor(10, 0x1E8ED9, Vector3.Zero, Hostile: false);
            var calls = 0;
            bool Interact(ulong id) { Check(id == 10, "Wrong fruit sent to native layer."); ++calls; return true; }
            interaction.Prepare(fruit, Vector3.Zero, false);
            Check(!interaction.Update(now, false, Interact) && !interaction.Holding && calls == 0, "Replay/disabled movement executed interaction.");
            interaction.Prepare(fruit, Vector3.UnitX * 3, true);
            Check(!interaction.Update(now, false, Interact) && !interaction.Holding, "Out-of-range fruit interrupted ACR.");
            interaction.Prepare(fruit, Vector3.Zero, true);
            Check(interaction.Holding && !interaction.Update(now, true, Interact), "Interaction was sent during an active cast.");
            Check(interaction.Update(now, false, Interact) && interaction.Attempted && calls == 1, "Usable fruit did not interact.");
            Check(!interaction.Update(now.AddSeconds(.5), false, Interact) && calls == 1, "Fruit interaction spammed before cooldown.");
            Check(interaction.Update(now.AddSeconds(1.1), false, Interact) && calls == 2, "Allowed retry did not occur.");
            interaction.Prepare(null, Vector3.Zero, true);
            Check(!interaction.Holding && !interaction.Attempted && !interaction.Update(now.AddSeconds(3), false, Interact), "Cleansed fruit retained control.");
            interaction.Prepare(fruit with { Oid = 0x4B10 }, Vector3.Zero, true);
            Check(!interaction.Holding, "Attackable seed was treated as a cleansing event object.");
        });
        foreach (var (boss, action) in new[] { (0x4B0Cu, 45742u), (0x4B0Eu, 45997u) })
            run($"Aurum Vale delayed tankbuster {action} survives natural cast end and clears on effect or cancel", () =>
            {
                using var f = new Fight(boss, 1331);
                f.Actors[100] = new(100, 0, f.Center, Hostile: false, PartySlot: 0);
                f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
                f.Cast(1, action, 5);
                var cast = f.Actors[1].Cast! with { TargetId = 100 };
                f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now);
                f.Complete(1, 5);
                Check(f.Engine.Casts.Count == 0 && f.Engine.PredictedDamage.Single().TargetId == 100, "Natural cast end dropped delayed damage.");
                f.Engine.Effect(1, action, 1, f.Now, 100);
                Check(!f.Engine.PredictedDamage.Any(), "Actual hit retained delayed damage.");
                f.Now = f.Start.AddSeconds(10); f.Cast(1, action, 5);
                f.Engine.Finish(1, action, f.Start.AddSeconds(11));
                Check(!f.Engine.PredictedDamage.Any(), "Cancelled cast retained delayed damage.");
            });
        run("Aurum Vale actual solver approaches the usable cleansing fruit inside the real arena", () =>
        {
            using var f = new Fight(0x4B0C, 1331);
            var player = new Vector3(34, 0, 8);
            var target = new Vector3(44, 0, 8);
            f.Actors[100] = new(100, 0, player, Hostile: false, Statuses: [302],
                StatusParams: new Dictionary<uint, ushort> { [302] = 3 });
            f.Actors[10] = new(10, 0x1E8ED9, target, Hostile: false); f.Scan();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count >= 2, "No fruit approach route.");
            Check(path.All(p => f.Engine.Arena!.Contains(p.Position)) && snapshot.GoalAreas.Single().Geometry.Contains(path[^1].Position),
                "Fruit approach crossed a wall or missed interaction range.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

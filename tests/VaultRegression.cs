using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class VaultRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (geometry, name) in new[] { (VaultArenas.Adelphel(0), "D041SerAdelphel"), (VaultArenas.Grinnaux(0), "D042SerGrinnaux") })
            run($"Vault {name} exact arena notches and world contours match runtime", () =>
            {
                var type = typeof(BossMod.Heavensward.Dungeon.D04TheVault.D041SerAdelphel.D041SerAdelphel).Assembly
                    .GetType($"BossMod.Heavensward.Dungeon.D04TheVault.{name}.{name}")!;
                var arena = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(geometry);
                for (var x = -22.13f; x < 22; x += .37f)
                    for (var z = -22.27f; z < 22; z += .37f)
                    {
                        var p = geometry.Position + new Vector3(x, 0, z);
                        var expected = arena.Contains(new(p.X - arena.Center.X, p.Z - arena.Center.Z));
                        Check(geometry.Contains(p) == expected && host.Contains(p) == expected, "Arena notch changed.");
                    }
            });
        run("Vault bridge ownership includes both phases only in territory 1066", () =>
        {
            foreach (var oid in new uint[] { 0x1051, 0x104E, 0x1054, 0x1053, 0x1056 })
                Check(ArrDungeonCatalog.Owns(oid, 1066) && !ArrDungeonCatalog.Owns(oid, 0), "Ownership differs.");
            Check(!ArrDungeonCatalog.Owns(0x1057, 1066), "Trash helper claimed as boss.");
        });
        run("Vault Adelphel four charges preserve original yell clock, two-point-two intervals and duplicate packet isolation", () =>
        {
            using var f = new Fight(0x1051, 1066);
            f.Actors[1] = f.Actors[1] with { Position = new(17, 0, -100) }; f.Scan();
            f.Now = f.Start.AddSeconds(.5);
            f.Events.Handle(new LogSystemNpcYellEvent { SourceId = 1, NpcYellId = 2523, Timestamp = f.Start }, f.Now); f.Render();
            var casts = f.Engine.OutputCasts.Select(c => c.Value).ToArray();
            Check(casts.Length == 4, "Yell did not produce four charges.");
            for (var i = 0; i < 4; ++i)
            {
                Check(Math.Abs((casts[i].FinishAt - f.Start).TotalSeconds - (.08 + 2.2 * i)) < .0001, "Charge time drift.");
                var geometry = casts[i].Rule.Shape!.Resolve(casts[i].Location, casts[i].Rotation, null);
                Check(HostDrawingGeometry.Convert(geometry).Contains(geometry.GetContours()[0][0]), "Charge host geometry missing.");
            }
            Check(casts[0].Location.X == 17 && casts[1].Location.X == -18.509f, "Wrong directional order.");
            f.Engine.Effect(1, 4130, 1, f.Now); f.Engine.Effect(1, 4130, 1, f.Now, 101); f.Render();
            Check(f.Draw.Previews.Count == 3, "AOE target rows consumed multiple charges.");
            foreach (var sequence in new uint[] { 2, 3, 4 }) f.Engine.Effect(1, 4130, sequence, f.Now);
            f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final charge leaked.");
        });
        run("Vault Adelphel bright spheres lock quantized birth position and never resurrect after FIFO effects", () =>
        {
            using var f = new Fight(0x1051, 1066);
            f.Actors[10] = new(10, 0x1052, new(3, 0, -101)); f.Scan();
            var sphere = f.Engine.OutputCasts.Single().Value;
            Check(sphere.FinishAt == f.Start.AddSeconds(4.6) && ((CircleGeometry)f.Draw.Previews.Values.Single().Geometry).Radius == 6,
                "Birth activation or display radius differs.");
            f.Actors[10] = f.Actors[10] with { Position = new(13, 0, -101) }; f.Now = f.Start.AddSeconds(1); f.Scan();
            Check(f.Engine.OutputCasts.Single().Value.Location == sphere.Location, "Moving actor moved locked sphere.");
            f.Actors[11] = new(11, 0x1052, new(6, 0, -101)); f.Scan();
            f.Engine.Effect(11, 4132, 1, f.Now); f.Scan();
            Check(f.Engine.OutputCasts.Single().Value.Source == 11, "Effect did not remove FIFO sphere.");
            f.Engine.Effect(10, 4132, 2, f.Now); f.Scan();
            Check(!f.Engine.OutputCasts.Any(), "Stale living spheres resurrected.");
        });
        run("Vault Adelphel execution uses four-point-eight icon time and personal separation", () =>
        {
            using var f = new Fight(0x1051, 1066);
            f.Actors[100] = new(100, 0, new(0, 0, -100), PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, new(4, 0, -100), PartySlot: 1, Hostile: false); f.Scan();
            f.Engine.Icon(100, 32, f.Start); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.8),
                "Execution icon delay or separation missing.");
            f.Engine.Effect(1, 4131, 1, f.Now, 100); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Execution effect leaked.");
        });
        foreach (var (p1, p2) in new[] { (0x104Eu, 0x1051u), (0x1053u, 0x1054u) })
            run($"Vault phase alias {p1:X} switches to transformed {p2:X} without losing session", () =>
            {
                using var f = new Fight(p1, 1066);
                var arena = f.Engine.Arena;
                f.Actors[2] = new(2, p2, f.Actors[1].Position, InCombat: true);
                f.Actors[1] = f.Actors[1] with { Dead = true, InCombat = false }; f.Scan();
                Check(f.Engine.PrimaryId == 2 && ReferenceEquals(f.Engine.Arena, arena), "Phase switch rebuilt encounter.");
            });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0x104Eu, 0x104Eu, 1099u, 5f), (0x1053u, 0x1053u, 1135u, 30.5f),
            (0x1054u, 0x1054u, 4134u, 47.2f), (0x1056u, 0xD25u, 4146u, 5f),
            (0x1056u, 0x1057u, 4152u, 40f), (0x1056u, 0x1058u, 4153u, 40f),
        })
            run($"Vault AID {action} preserves dimensions timing cancellation and both draw geometries", () =>
            {
                using var f = new Fight(boss, 1066); var source = boss == caster ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Vector3.Zero); f.Scan(); }
                f.Cast(source, action, 4, MathF.PI / 2);
                var preview = f.Draw.Previews.Values.Single().Geometry;
                Check(preview.Contains(Vector3.UnitX * (range - .02f)) && !preview.Contains(Vector3.UnitX * (range + .02f))
                    && HostDrawingGeometry.Convert(preview).Contains(Vector3.UnitX * (range - .02f)), "Display dimensions differ.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3), "Cast delay differs.");
                f.Engine.Finish(source, action); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled cast leaked.");
            });
        run("Vault Grinnaux concentric half rings keep simultaneous independent cast deadlines", () =>
        {
            using var f = new Fight(0x1054, 1066);
            for (ulong id = 10; id < 13; ++id) f.Actors[id] = new(id, 0xD25, Vector3.Zero);
            f.Scan();
            f.Cast(10, 4137, 3, MathF.PI / 2); f.Cast(11, 4138, 3, MathF.PI / 2); f.Cast(12, 4139, 3, MathF.PI / 2);
            Check(f.Draw.Previews.Count == 3 && f.Snapshot.DangerAreas.All(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3)),
                "Independent rings converted into invented sequence.");
            foreach (var id in new ulong[] { 10, 11, 12 }) f.Engine.Finish(id);
            f.Render(); Check(f.Draw.Previews.Count == 0, "Ring cancellation leaked.");
        });
        run("Vault Grinnaux rip predicts from effect plus one-point-one then hands off to active object", () =>
        {
            using var f = new Fight(0x1054, 1066); f.Cast(1, 4140, 3); f.Complete(1, 3);
            Check(f.Draw.Previews.Count == 0, "Finished cast did not remove telegraph.");
            f.Engine.Effect(1, 4140, 1, f.Now, location: new(6, 0, 72), occurredAt: f.Start.AddSeconds(2.8)); f.Render();
            Check(Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 3.9) < .0001,
                "Spawn delay used dispatch clock.");
            f.Actors[10] = new(10, 0x1E91C1, new(6.5f, 0, 72)); f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Object handoff duplicated predicted zone.");
            f.Actors[10] = f.Actors[10] with { EventState = 7 }; f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Inactive event object remained dangerous.");
        });
        run("Vault Grinnaux stop-at-wall displacement avoids tears with a real temporal solver route", () =>
        {
            using var f = new Fight(0x1054, 1066);
            var center = new Vector3(0, 0, 72);
            f.Actors[1] = f.Actors[1] with { Position = center };
            f.Actors[100] = new(100, 0, center + Vector3.UnitX * 8, PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0x1055, center + Vector3.UnitX * 16); f.Scan();
            var cast = new DungeonCastObservation(4135, center, 0, f.Now, f.Now.AddSeconds(8.3));
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
            var template = (VaultKnockbackTemplate)f.Engine.AmbientAoes().Single(a => a.Id == "vault/faith/constraint").Shape;
            var hit = template.Landing(f.Actors[100].Position);
            Check(f.Engine.Arena!.Contains(hit) && hit.X < 18 && hit.X > 17,
                "Knockback crossed the right-hand arena notch.");
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot, new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && path.Count > 1
                && !template.Resolve(default, 0, null).Contains(path[^1].Position), "No safe wall landing route.");
        });
        run("Vault Charibert chain is symmetric follows perspective and cancellation resists stale snapshot", () =>
        {
            using var f = new Fight(0x1056, 1066);
            f.Actors[100] = new(100, 0, new(0, 0, 4.1f), PartySlot: 0, Hostile: false,
                Tethers: new Dictionary<uint, ulong> { [9] = 101 });
            f.Actors[101] = new(101, 0, new(5, 0, 4.1f), PartySlot: 1, Hostile: false); f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(10),
                "Chain deadline or line missing.");
            f.PlayerId = 101; f.Scan();
            Check(((CircleGeometry)f.Snapshot.DangerAreas.Single().Geometry).Position == f.Actors[100].Position, "Reverse partner not recorded.");
            f.Events.Handle(new LogSystemTetherCancelEvent { SourceId = 100, TargetId = 101, TetherId = 9, Timestamp = f.Now }, f.Now);
            f.Scan(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stale snapshot restored cancelled chain.");
        });
        run("Vault Charibert knight model state controls exact capsule display and four timed navigation zones", () =>
        {
            using var f = new Fight(0x1056, 1066);
            f.Actors[10] = new(10, 0x1057, new(0, 0, 18), ModelState: 8); f.Scan();
            Check(f.Draw.Previews.Values.Single().Geometry.Contains(new(0, 0, 29.99f))
                && !f.Draw.Previews.Values.Single().Geometry.Contains(new(0, 0, 30.01f)), "Knight capsule differs.");
            Check(f.Snapshot.HardObstacles.Count == 1 && f.Snapshot.DangerAreas.Count == 4, "Knight current obstacle or timed forecasts absent.");
            var deadlines = f.Snapshot.DangerAreas.Select(a => a.DangerWindows.Single().DangerFrom).Order().ToArray();
            Check(deadlines[0] == f.Start.AddSeconds(1.1) && deadlines[1] == f.Start.AddSeconds(3)
                && deadlines[2] == f.Start.AddSeconds(10), "Knight forecast time changed.");
            f.Actors[10] = f.Actors[10] with { ModelState = 0 }; f.Scan();
            Check(f.Draw.Previews.Values.Single().Geometry.Contains(new(0, 0, 22.99f))
                && !f.Draw.Previews.Values.Single().Geometry.Contains(new(0, 0, 23.01f)), "Staging hint still used moving length.");
            f.Actors[10] = f.Actors[10] with { Position = new(0, 0, 4.1f) }; f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.HardObstacles.Count == 0, "Central stationary knight still blocked.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

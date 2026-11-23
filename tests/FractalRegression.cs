using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Heavensward.Dungeon.D08FractalContinuum;

internal static class FractalRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (native, type) in new (IGeometryShape, Type)[]
        {
            (FractalArenas.Clockwork(0), typeof(Source.D080ClockworkDreadnaught.D080ClockworkDreadnaught)),
            (FractalArenas.Manikin(0), typeof(Source.D080ManikinConjurer.D080ManikinConjurer)),
            (FractalArenas.Naga(0), typeof(Source.D080FlawlessNaga.D080FlawlessNaga)),
            (FractalArenas.Violet(0), typeof(Source.D080VioletControlUnit.D080VioletControlUnit)),
            (FractalArenas.Emerald(0), typeof(Source.D080EmeraldControlUnit.D080EmeraldControlUnit)),
            (FractalArenas.Crimson(0), typeof(Source.D080CrimsonControlUnit.D080CrimsonControlUnit)),
            (FractalArenas.PhantomRay(0), typeof(Source.D081PhantomRay.D081PhantomRay)),
            (FractalArenas.Minotaur(0), typeof(Source.D082Minotaur.D082Minotaur)),
        })
            run($"Fractal {type.Name} exact arena preserves source geometry and world contours", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                    .First(f => f.FieldType == typeof(BossMod.ArenaBoundsCustom)).GetValue(null)!;
                var host = HostDrawingGeometry.Convert(native);
                var b = native.GetBounds();
                for (var x = b.MinX + .137f; x <= b.MaxX; x += .57f)
                    for (var z = b.MinZ + .231f; z <= b.MaxZ; z += .57f)
                    {
                        var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(new(x, 0, z)) == expected && host.Contains(new(x, 0, z)) == expected,
                            $"Arena mismatch at {x}, {z}.");
                    }
            });
        run("Fractal all nine modules own only territory 430 and suppress every migrated action", () =>
        {
            Check(FractalCatalog.Create().Length == 9, "Missing module.");
            foreach (var module in FractalCatalog.Create())
                Check(ArrDungeonCatalog.Owns(module.BossOid, 430) && !ArrDungeonCatalog.Owns(module.BossOid, 0), "Wrong bridge ownership.");
            Check(!ArrDungeonCatalog.Owns(0x1019, 430), "Helper claimed as primary.");
        });
        foreach (var definition in FractalCatalog.Create())
            foreach (var rule in definition.Casts.Where(c => c.Shape != null && c.Action != 3986))
                run($"Fractal {definition.Name} AID {rule.Action} preserves geometry clock cancellation and both views", () =>
                {
                    using var f = new Fight(definition.BossOid, 430);
                    var arena = definition.Arena(Vector3.Zero);
                    var center = Inside(arena);
                    f.Actors[1] = f.Actors[1] with { Position = center };
                    var source = rule.CasterOid == definition.BossOid ? 1UL : 10UL;
                    if (source != 1) f.Actors[source] = new(source, rule.CasterOid, center, InCombat: true);
                    if (FractalCatalog.IsControl(definition.BossOid)) f.Actors[20] = new(20, 0x102B, center, InCombat: true);
                    Sync(f, center);
                    var cast = new DungeonCastObservation(rule.Action, center, MathF.PI / 2, f.Now, f.Now.AddSeconds(3.3));
                    f.Engine.StartCast(source, cast, f.Now); f.Render();
                    var geometry = f.Draw.Previews["cast/" + source].Geometry;
                    var expected = rule.Shape!.Resolve(center, MathF.PI / 2, null);
                    var host = HostDrawingGeometry.Convert(geometry);
                    for (var x = -45.13f; x < 45; x += 1.13f)
                        for (var z = -45.27f; z < 45; z += 1.13f)
                        {
                            var p = center + new Vector3(x, 0, z);
                            Check(geometry.Contains(p) == expected.Contains(p) && host.Contains(p) == expected.Contains(p), "Cast projection differs.");
                        }
                    Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "Activation drift.");
                    f.Engine.Finish(source); f.Render();
                    Check(!f.Draw.Previews.ContainsKey("cast/" + source) && f.Snapshot.DangerAreas.Count == 0, "Cancel leaked.");
                });
        run("Fractal Curator floor masks preserve holes islands fill and perimeter without internal tile seams", () =>
        {
            var masks = new HashSet<ushort> { 0, ushort.MaxValue, 0x5A5A, 0xA5A5, 0x0660, 0xF99F, 0xFFFE };
            for (var i = 0; i < 16; ++i)
            {
                masks.Add((ushort)(1 << i));
                masks.Add((ushort)~(1 << i));
                for (var j = i + 1; j < 16; ++j) masks.Add((ushort)((1 << i) | (1 << j)));
            }
            var random = new Random(35);
            for (var i = 0; i < 128; ++i) masks.Add((ushort)random.Next(65536));
            foreach (var mask in masks)
            {
                var shape = FractalFloorBoundary.Create(7, mask);
                var host = HostDrawingGeometry.Convert(shape);
                var effective = mask == ushort.MaxValue ? 0 : mask;
                var layout = RadarArenaLayout.Create(shape, shape.Position, new(1950, 1080), 150, .37f);
                var clip = new RadarClipRegion(layout.Outline, new(1950, 1080), 150, layout.Outlines, layout.FillPolygons);
                float[] coordinates = [-19.5f, -10, 0, 10, 19.5f];
                var perimeter = 0f;
                bool Safe(int x, int z) => x is >= 0 and < 4 && z is >= 0 and < 4 && (effective & (1 << (x | z << 2))) == 0;
                for (var z = 0; z < 4; ++z)
                    for (var x = 0; x < 4; ++x)
                    {
                        var index = x | z << 2;
                        var p = FractalFloorBoundary.CellCenter(index, 7);
                        var safe = Safe(x, z);
                        Check(shape.Contains(p) == safe && host.Contains(p) == safe && clip.Contains(layout.Projection.Project(p)) == safe,
                            $"Broken floor became walkable: {mask:X4}/{index}.");
                        if (!safe) continue;
                        if (!Safe(x - 1, z)) perimeter += coordinates[z + 1] - coordinates[z];
                        if (!Safe(x + 1, z)) perimeter += coordinates[z + 1] - coordinates[z];
                        if (!Safe(x, z - 1)) perimeter += coordinates[x + 1] - coordinates[x];
                        if (!Safe(x, z + 1)) perimeter += coordinates[x + 1] - coordinates[x];
                    }
                var actual = shape.GetContours().Sum(c => Enumerable.Range(0, c.Count).Sum(i => Vector3.Distance(c[i], c[(i + 1) % c.Count])));
                Check(Math.Abs(actual - perimeter) < .001f, $"Rim contains a seam or lost a hole: {mask:X4}.");
            }
        });
        run("Fractal Curator row column alarm finish effect and DirectorUpdate retain distinct transitions", () =>
        {
            using var f = new Fight(0x1018, 430);
            var center = new Vector3(0, 0, -350);
            f.Actors[1] = f.Actors[1] with { Position = center };
            f.Actors[10] = new(10, 0x1019, FractalFloorBoundary.CellCenter(0, 0)); Sync(f, center);
            f.Engine.StartCast(1, new(3981, FractalFloorBoundary.CellCenter(5, 0), 0, f.Now, f.Now.AddSeconds(6.3)), f.Now);
            for (var i = 0; i < 16; ++i)
                Check(f.Engine.Arena!.Contains(FractalFloorBoundary.CellCenter(i, 0)) == ((i & 3) != 1 && i >> 2 != 1), "Row/column union differs.");
            f.Engine.StartCast(10, new(3986, new(-16, 0, -364), 0, f.Now, f.Now.AddSeconds(3.3)), f.Now); f.Render();
            Check(f.Draw.Previews["cast/10"].Geometry is RectangleGeometry { Width: 10, Length: 10 } square
                && square.Position == FractalFloorBoundary.CellCenter(0, 0), "Alarm preview did not snap to tile center.");
            f.Engine.Finish(10, completed: true); f.Render();
            Check(f.Engine.Arena!.Contains(FractalFloorBoundary.CellCenter(0, 0)) && f.Draw.Previews.Count == 0, "Cast finish destroyed floor before actual effect.");
            f.Engine.Effect(10, 3986, 1, f.Now); f.Render();
            Check(!f.Engine.Arena!.Contains(FractalFloorBoundary.CellCenter(0, 0)), "Alarm effect did not destroy tile.");
            f.Events.Handle(new LogSystemDirectorUpdateEvent { Command = 0x80000004, Timestamp = f.Now }, f.Now);
            Check(!f.Engine.Arena.Contains(FractalFloorBoundary.CellCenter(0, 0)), "Ignored director update reset floor.");
            f.Events.Handle(new LogSystemDirectorUpdateEvent { Command = 0x80000005, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.Arena.Contains(FractalFloorBoundary.CellCenter(0, 0)) && f.Snapshot.ArenaBoundary!.Contains(center), "Director reset did not publish restored floor.");
        });
        run("Fractal Curator mines clear on effect object removal and missing snapshot", () =>
        {
            using var f = new Fight(0x1018, 430);
            f.Actors[10] = new(10, 0xD25, Vector3.Zero);
            f.Actors[11] = new(11, 0x101A, Vector3.Zero); f.Scan();
            f.Engine.Effect(10, 3983, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Mine missing.");
            f.Engine.Effect(11, 3984, 2, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0, "Explosion retained mine.");
            f.Engine.Effect(10, 3983, 3, f.Now); f.Engine.RemoveActor(11); f.Render();
            Check(f.Draw.Previews.Count == 0, "Object remove retained mine.");
            f.Actors[11] = new(11, 0x101A, Vector3.Zero); f.Scan();
            f.Engine.Effect(10, 3983, 4, f.Now); f.Actors.Remove(11); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Missing snapshot retained mine.");
        });
        run("Fractal Curator bomb uses original status clock cleanses and resists stale snapshots", () =>
        {
            using var f = new Fight(0x1018, 430);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false, ClassJob: 24);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
            f.Scan();
            f.Now = f.Start.AddSeconds(.4);
            f.Events.Handle(new LogSystemStatusAddEvent { StatusId = 723, TargetId = 101, Timestamp = f.Start }, f.Now); f.Render();
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6)
                && f.Engine.FractalCleanseTarget(24)?.Id == 101 && f.Engine.FractalCleanseTarget(19) == null, "Bomb clock or dispel job differs.");
            f.Actors[101] = f.Actors[101] with { Statuses = [723] };
            f.Events.Handle(new LogSystemStatusRemoveEvent { StatusId = 723, TargetId = 101, Timestamp = f.Now }, f.Now);
            f.Scan(); f.Now = f.Start.AddSeconds(2); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Stale snapshot resurrected cleansed spread.");
            f.Actors[101] = f.Actors[101] with { Statuses = [] }; f.Scan();
            f.Actors[101] = f.Actors[101] with { Statuses = [723] }; f.Scan();
            Check(f.Draw.Previews.Count == 1, "New bomb did not recover from snapshot.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.Zero), "Viewer did not avoid a teammate.");
        });
        foreach (var oid in new uint[] { 0x1E990F, 0x1E990E, 0x1E9910 })
            run($"Fractal control {oid:X} survives untargetable state and ends at EventState seven", () =>
            {
                using var f = new Fight(oid, 430);
                var center = FractalCatalog.Create().Single(d => d.BossOid == oid).Arena(Vector3.Zero).Position;
                f.Actors[1] = f.Actors[1] with { Position = center, Targetable = false, InCombat = false };
                f.Actors[10] = new(10, 0x102B, center, InCombat: true); Sync(f, center);
                Check(f.Engine.Active?.BossOid == oid && f.Engine.FractalInteractionTarget == null, "Untargetable controller failed pull.");
                f.Actors[1] = f.Actors[1] with { Targetable = true }; Sync(f, center);
                Check(f.Engine.FractalInteractionTarget?.Id == 1 && f.Snapshot.GoalAreas.Count == 1
                    && f.Draw.Previews.Values.Single().Safe, "Control did not publish interaction.");
                if (oid != 0x1E990F)
                {
                    Sync(f, center + Vector3.UnitX * 71);
                    Check(f.Engine.Active == null && f.Snapshot.GoalAreas.Count == 0, "Out-of-bounds control immediately restarted.");
                    Sync(f, center);
                }
                f.Actors[1] = f.Actors[1] with { EventState = 7 }; Sync(f, center);
                Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Completed controller retained outputs.");
            });
        run("Fractal Minotaur incubators show every usable object select nearest and hold only in interaction range", () =>
        {
            using var f = new Fight(0x1013, 430);
            f.Actors[100] = new(100, 0, Vector3.Zero, Hostile: false);
            f.Actors[10] = new(10, 0x1E99EA, Vector3.UnitX * 10, HitboxRadius: 2);
            f.Actors[11] = new(11, 0x1E99E8, Vector3.UnitX * 5, HitboxRadius: 2); f.Scan();
            f.Cast(1, 3975, 11);
            Check(f.Engine.FractalInteractionTarget?.Id == 11 && f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 0
                && f.Snapshot.GoalAreas.Single().Geometry is CircleGeometry { Radius: 4 }, "Incubators became a dangerous circle or wrong target.");
            var interaction = new DungeonObjectInteraction();
            interaction.Prepare(f.Engine.FractalInteractionTarget, Vector3.Zero, true);
            Check(!interaction.Holding, "Interaction held before arrival.");
            interaction.Prepare(f.Engine.FractalInteractionTarget, Vector3.UnitX * 2, true);
            Check(interaction.Holding && !interaction.Update(f.Now, true, _ => throw new Exception("Interaction during cast")), "Failed cast gate.");
            Check(interaction.Update(f.Now, false, id => id == 11) && interaction.Attempted, "Interaction not dispatched.");
            interaction.Prepare(f.Engine.FractalInteractionTarget, Vector3.UnitX * 2, false);
            Check(!interaction.Holding && !interaction.Attempted, "Replay/disabled gate retained execution.");
            f.Engine.Finish(1); f.Render();
            Check(f.Engine.FractalInteractionTarget == null && f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Cancelled swing retained interaction.");
        });
        run("Fractal Minotaur knockback stops at exact arena wall and respects immunity expiration", () =>
        {
            using var f = new Fight(0x1013, 430);
            var center = f.Engine.Arena!.Position;
            f.Actors[1] = f.Actors[1] with { Position = center };
            f.Actors[100] = new(100, 0, center + Vector3.UnitX * 10, Hostile: false); Sync(f, center);
            f.Engine.StartCast(1, new(3972, center, 0, f.Now, f.Now.AddSeconds(3.3)), f.Now); f.Render();
            var knockback = (VaultKnockbackTemplate)f.Engine.AmbientAoes().Single(a => a.Id == "fractal/groan/constraint").Shape;
            var landing = knockback.Landing(f.Actors[100].Position);
            Check(f.Engine.Arena.Contains(landing) && Vector3.Distance(landing, f.Actors[100].Position) < 20, "Knockback crossed wall.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(4) } };
            Sync(f, center); f.Engine.StartCast(1, new(3972, center, 0, f.Now, f.Now.AddSeconds(3.3)), f.Now); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("fractal/groan")), "Immune viewer retained knockback.");
        });
        run("Fractal Phantom double sever preserves concurrent helpers and solver approaches first origin", () =>
        {
            using var f = new Fight(0x1012, 430);
            var center = f.Engine.Arena!.Position;
            f.Actors[1] = f.Actors[1] with { Position = center };
            f.Actors[10] = new(10, 0xD25, center);
            f.Actors[11] = new(11, 0xD25, center); Sync(f, center);
            f.Engine.StartCast(10, new(3965, center, 0, f.Now, f.Now.AddSeconds(6.3)), f.Now);
            f.Engine.StartCast(11, new(3965, center, MathF.PI, f.Now, f.Now.AddSeconds(9.3)), f.Now); f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.GoalAreas.Count == 1, "Concurrent sever or close-origin goal missing.");
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(center + Vector3.UnitX * 10, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 12, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && path.Count >= 2 && snapshot.GoalAreas.Single().Geometry.Contains(path[^1].Position),
                "Solver failed to approach the sever origin.");
            f.Engine.Finish(10); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.GoalAreas.Count == 1, "First resolution removed remaining sever.");
            f.Engine.Finish(11); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Final sever leaked.");
        });
        run("Fractal trash retains group lifetime and Naga destroyed semantics", () =>
        {
            using var f = new Fight(0x1020, 430);
            var center = FractalArenas.Clockwork(0).Position;
            f.Actors[1] = f.Actors[1] with { Position = center, InCombat = false };
            f.Actors[10] = new(10, 0x101F, center, InCombat: true); Sync(f, center);
            f.Engine.RemoveActor(1); f.Actors.Remove(1); Sync(f, center);
            Check(f.Engine.Active?.BossOid == 0x1020, "Primary removal dropped living group.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; Sync(f, center);
            Check(f.Engine.Active == null, "Dead group leaked.");
            using var naga = new Fight(0x1023, 430);
            center = FractalArenas.Naga(0).Position;
            naga.Actors[1] = naga.Actors[1] with { Position = center, Dead = true };
            naga.Actors[10] = new(10, 0x1021, center, Dead: true);
            naga.Engine.ResetFight();
            naga.Actors[1] = naga.Actors[1] with { Dead = false }; Sync(naga, center);
            naga.Actors[1] = naga.Actors[1] with { Dead = true }; Sync(naga, center);
            Check(naga.Engine.Active != null, "Dead naga actors were treated as destroyed.");
            naga.Actors.Clear(); Sync(naga, center);
            Check(naga.Engine.Active == null, "Destroyed naga group leaked.");
        });
    }

    private static void Sync(Fight f, Vector3 player)
    {
        f.Engine.Synchronize(f.Actors.Values, player, f.Now, playerId: f.PlayerId);
        f.Render();
    }

    private static Vector3 Inside(IGeometryShape arena)
    {
        if (arena.Contains(arena.Position)) return arena.Position;
        var b = arena.GetBounds();
        for (var x = b.MinX + .5f; x < b.MaxX; x += 1)
            for (var z = b.MinZ + .5f; z < b.MaxZ; z += 1)
                if (arena.Contains(new(x, 0, z))) return new(x, 0, z);
        throw new InvalidOperationException("Arena has no floor.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

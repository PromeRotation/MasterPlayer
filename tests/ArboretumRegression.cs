using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Heavensward.Dungeon.D09SaintMociannesArboretum;

internal static class ArboretumRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (shape, type, field) in new (IGeometryShape, Type, string)[]
        {
            (ArboretumArenas.RoseGarden(0), typeof(Source.D091RoseGarden.D091RoseGarden), "arena"),
            (ArboretumArenas.QueenHawk(0), typeof(Source.D092QueenHawk.D092QueenHawk), "ArenaBounds"),
            (ArboretumArenas.Belladonna(0), typeof(Source.D093Belladonna.D093Belladonna), "arena"),
            (ArboretumArenas.AntFirst(0), typeof(Source.D090VelveteenAnt.D090VelveteenAnt), "Arena1"),
            (ArboretumArenas.AntFirstOpen(0), typeof(Source.D090VelveteenAnt.D090VelveteenAnt), "Arena1B"),
            (ArboretumArenas.AntSecond(0), typeof(Source.D090VelveteenAnt.D090VelveteenAnt), "Arena2"),
            (ArboretumArenas.AntSecondOpen(0), typeof(Source.D090VelveteenAnt.D090VelveteenAnt), "Arena2B"),
            (ArboretumArenas.NarbrooiFirst(0), typeof(Source.D090TangledNarbrooi.D090TangledNarbrooi), "Arena1"),
            (ArboretumArenas.NarbrooiFirstOpen(0), typeof(Source.D090TangledNarbrooi.D090TangledNarbrooi), "Arena1B"),
            (ArboretumArenas.NarbrooiSecond(0), typeof(Source.D090TangledNarbrooi.D090TangledNarbrooi), "Arena2"),
        })
            run($"Arboretum {type.Name} exact arena and world geometry preserve source notches", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(shape);
                var bounds = shape.GetBounds();
                for (var x = bounds.MinX + .13f; x < bounds.MaxX; x += .57f)
                    for (var z = bounds.MinZ + .27f; z < bounds.MaxZ; z += .57f)
                    {
                        var p = new Vector3(x, 0, z);
                        var expected = source.Contains(new(p.X - source.Center.X, p.Z - source.Center.Z));
                        Check(shape.Contains(p) == host.Contains(p), $"World projection differs at {p}.");
                        if (shape.Contains(p) == expected) continue;
                        var distanceSquared = shape.GetContours().Min(contour => Enumerable.Range(0, contour.Count).Min(i =>
                            GeometryHelper.DistanceSquaredPointToSegment2D(new(p.X, p.Z),
                                new(contour[i].X, contour[i].Z), new(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z))));
                        // Both libraries include edges, but use different tolerances and float transforms.
                        Check(distanceSquared < .001f * .001f,
                            $"Arena notch differs at {p}: source={expected}, local={shape.Contains(p)}, edge distance={MathF.Sqrt(distanceSquared)}.");
                    }
            });
        run("Arboretum native ownership includes every boss and trash stage", () =>
        {
            foreach (var oid in new uint[] { 0x142F, 0x1432, 0x1434, 0x1423, 0x142A })
                Check(ArrDungeonCatalog.Owns(oid, 511) && !ArrDungeonCatalog.Owns(oid, 0), "Wrong boss ownership.");
            foreach (var oid in new uint[] { 0xFFFF })
                Check(!ArrDungeonCatalog.Owns(oid, 511), "Unmigrated trash was suppressed.");
        });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0x142Fu, 0x142Fu, 5333u, 24.775f), (0x1432u, 0x1432u, 5237u, 6f),
            (0x1432u, 0x1433u, 5240u, 51.08f), (0x1432u, 0x1B2u, 5243u, 50.5f),
            (0x1423u, 0x1422u, 417u, 11f), (0x1423u, 0x146Eu, 4526u, 9.08f),
            (0x142Au, 0x142Cu, 5353u, 7.85f), (0x142Au, 0x142Bu, 5350u, 9f),
            (0x142Au, 0x142Du, 4701u, 7.7f), (0x142Au, 0x142Cu, 5352u, 6f),
        })
            run($"Arboretum cast {action} preserves shape time cancellation and navigation margin", () =>
            {
                using var f = new Fight(boss, 511);
                var source = boss == caster ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Vector3.Zero); f.Scan(); }
                f.Cast(source, action, 3, MathF.PI / 2);
                var shape = f.Draw.Previews["cast/" + source].Geometry;
                Check(shape.Contains(Vector3.UnitX * (range - .01f)) && !shape.Contains(Vector3.UnitX * (range + .01f)), "Wrong extent.");
                Check(HostDrawingGeometry.Convert(shape).Contains(Vector3.UnitX * (range - .01f)), "World projection changed.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "Wrong activation.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * (range + .4f)), "Navigation margin missing.");
                f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        foreach (var sign in new[] { -1, 1 })
            run($"Arboretum rotating breath direction {sign} preserves ambiguity first and later sequences original effect time and deduplication", () =>
            {
                using var f = new Fight(0x142F, 511);
                f.Actors[10] = new(10, 0x14C5, Vector3.Zero); f.Scan();
                for (var rotationIndex = 0; rotationIndex < 2; ++rotationIndex)
                {
                    f.Now = f.Start.AddSeconds(rotationIndex * 40);
                    f.Engine.Tick(f.Now);
                    f.Cast(10, 5230, 5);
                    Check(f.Draw.Previews.Count == 3 && f.Engine.OutputCasts.Count() == 3, "Unknown direction did not predict three cones.");
                    var output = f.Engine.OutputCasts.Select(p => p.Value).ToArray();
                    Check(output[0].FinishAt == f.Now.AddSeconds(5.3) && output[1].FinishAt == f.Now.AddSeconds(6.3)
                        && Math.Abs(output[1].Rotation + MathF.PI / 4) < .0001, "Ambiguous heading or clock differs.");
                    f.Engine.Effect(10, 5230, 1, f.Now.AddSeconds(5));
                    f.Engine.Finish(10, 5230, f.Now.AddSeconds(5), completed: true);
                    f.Engine.Effect(10, 5225, 2, f.Now.AddSeconds(6), sourceRotation: .00001f);
                    var occurred = f.Now.AddSeconds(7);
                    f.Events.Handle(new LogSystemActionEffectEvent
                    {
                        SourceId = 10, ActionId = 5225, GlobalSequence = 3, Timestamp = occurred,
                        SourceRotation = sign * 11.6f * MathF.PI / 180,
                    }, occurred.AddSeconds(.4)); f.Render();
                    var expectedRemaining = (rotationIndex == 0 ? 13 : 25) - 1;
                    var sequence = f.Engine.OutputCasts.Select(p => p.Value).OrderBy(p => p.FinishAt).ToArray();
                    Check(sequence.Length == 8 && sequence[0].FinishAt == occurred.AddSeconds(1)
                        && Math.Abs(sequence[1].Rotation - sequence[0].Rotation - sign * 11.6f * MathF.PI / 180) < .0001,
                        "Resolved rotation lost count direction interval or original timestamp.");
                    f.Engine.Effect(10, 5225, 3, occurred.AddSeconds(.5), target: 101); f.Render();
                    Check(f.Engine.OutputCasts.Min(p => p.Value.FinishAt) == occurred.AddSeconds(1), "Duplicate target row advanced sequence.");
                    for (var i = 0; i < expectedRemaining; ++i)
                        f.Engine.Effect(10, 5225, (uint)(4 + i), occurred.AddSeconds(i + 1));
                    f.Engine.Finish(10, completed: true); f.Render();
                    Check(!f.Engine.OutputCasts.Any() && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final rotation leaked.");
                }
            });
        run("Arboretum cancelled first breath and EarthyBreath model transition clean predictions", () =>
        {
            using var f = new Fight(0x142F, 511);
            f.Actors[10] = new(10, 0x14C5, Vector3.Zero);
            f.Actors[11] = new(11, 0x14C6, Vector3.Zero, ModelState: 1); f.Scan();
            f.Cast(10, 5230, 5); f.Engine.Finish(10); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled breath retained ambiguous predictions.");
            f.Cast(11, 5316, 5);
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2, "Earthy cone and line did not coexist.");
            f.Complete(11, 5);
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Values.Single().Geometry is RayRectangleGeometry, "Natural cone finish removed persistent line.");
            f.Actors[11] = f.Actors[11] with { ModelState = 0 }; f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Model reset did not remove Earthy line.");
        });
        run("Arboretum first effect survives a missing cast end packet until rotation direction arrives", () =>
        {
            using var f = new Fight(0x142F, 511);
            f.Actors[10] = new(10, 0x14C5, Vector3.Zero); f.Scan(); f.Cast(10, 5230, 5);
            f.Engine.Effect(10, 5230, 1, f.Start.AddSeconds(5));
            f.Engine.Tick(f.Start.AddSeconds(7.4));
            f.Engine.Effect(10, 5225, 2, f.Start.AddSeconds(7.4), sourceRotation: .2f); f.Render();
            Check(f.Engine.OutputCasts.Count() == 8, "Effect-confirmed cast timeout erased rotating follow-ups.");
        });
        run("Arboretum Queen shield forbids protected target switches to knight and gates rectangular cleave", () =>
        {
            using var f = new Fight(0x1432, 511);
            f.Actors[1] = f.Actors[1] with { TargetId = 101 };
            f.Actors[10] = new(10, 0x1433, Vector3.UnitX * 10);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitZ * 5, PartySlot: 1, Hostile: false); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Engine.ForbiddenTargets.Length == 0, "Cleave appeared without shield.");
            f.Events.Handle(new LogSystemStatusAddEvent { StatusId = 152, TargetId = 1, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]) && f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Shield did not release protected boss.");
            Check(f.Draw.Previews.Values.Single().Geometry is RayRectangleGeometry && f.Snapshot.DangerAreas.Count == 1, "Shield cleave missing.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { StatusId = 152, TargetId = 1, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.ForbiddenTargets.Length == 0 && f.Draw.Previews.Count == 0, "Shield removal retained ban or cleave.");
        });
        run("Arboretum Queen poison bridges effect to spawn at original effect plus point seven", () =>
        {
            using var f = new Fight(0x1432, 511);
            f.Actors[100] = new(100, 0, Vector3.UnitX * 5, Hostile: false); f.Scan();
            f.Engine.Effect(1, 5237, 1, f.Start.AddSeconds(.4), 100, occurredAt: f.Start); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(.7), "Spawn prediction delay drift.");
            f.Actors[10] = new(10, 0x1E8FCA, Vector3.UnitX * 5); f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Spawn duplicated prediction.");
            f.Actors[10] = f.Actors[10] with { EventState = 7 }; f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Removed poison leaked.");
        });
        run("Arboretum Belladonna gaze uses facing window and distinct buster timings", () =>
        {
            using var f = new Fight(0x1434, 511);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[100] = new(100, 0, Vector3.UnitX * 5, PartySlot: 0, Hostile: false);
            f.Engine.ResetFight(); f.Scan();
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(3.1), "Initial instant buster time differs.");
            f.Engine.Effect(1, 5219, 1, f.Now);
            f.Cast(1, 5216, 4);
            Check(f.Engine.GazeFacing(Vector3.UnitX * 5, -MathF.PI / 2) == null && f.Draw.Previews.Count == 1, "Gaze forced facing early or missed visual.");
            f.Now = f.Start.AddSeconds(3.7); f.Engine.Tick(f.Now);
            Check(f.Engine.GazeFacing(Vector3.UnitX * 5, -MathF.PI / 2) != null, "Gaze failed imminent facing constraint.");
            f.Engine.Finish(1, 5216, f.Start.AddSeconds(4), completed: true); f.Render();
            Check(!f.Engine.Gazes.Any() && f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(8.1), "Gaze end buster delay differs.");
            f.Engine.Effect(1, 5219, 2, f.Now);
            for (ulong i = 10; i < 15; ++i) f.Actors[i] = new(i, 0x1435, Vector3.UnitX * (i - 10), AnimationState1: 1);
            f.Scan();
            Check(f.Draw.Previews.Count == 5 && f.Engine.PredictedDamage.Single().Activation == f.Now.AddSeconds(8.1), "Five bulb transition missed buster.");
            Check(f.Snapshot.DangerAreas.All(a => a.DangerWindows.Single().DangerFrom == f.Now.AddSeconds(10)), "Mildew time differs.");
            f.Actors[10] = f.Actors[10] with { AnimationState1 = 0 }; f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Bulb reset did not clear all spores.");
        });
        run("Arboretum Belladonna icon spreads keep event clock viewer separation resolve and timeout", () =>
        {
            using var f = new Fight(0x1434, 511);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false); f.Scan();
            f.Now = f.Start.AddSeconds(.4);
            f.Engine.Icon(101, 43, f.Start); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.1), "Icon time drift.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.Zero), "Pointed viewer did not avoid teammates.");
            f.Engine.Effect(1, 5218, 1, f.Now, 101); f.Engine.Icon(101, 43, f.Start); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Late icon resurrected resolved spread.");
            f.Now = f.Start.AddSeconds(10); f.Engine.Icon(101, 43, f.Now); f.Render();
            f.Now = f.Start.AddSeconds(15.2); f.Scan();
            Check(f.Draw.Previews.Count == 0, "Missing petal effect leaked indefinitely.");
        });
        run("Arboretum Ant wall openings preserve scene state corridor switching and Queen room completion", () =>
        {
            using var f = new Fight(0x1423, 511);
            f.Actors[100] = new(100, 0, new(-115, 0, -100), Hostile: false);
            f.Actors[10] = new(10, 0x1E9DFA, Vector3.Zero);
            f.Actors[11] = new(11, 0x1E9DFB, Vector3.Zero); f.Scan();
            f.Engine.Animation(10, 0x00040008);
            Check(f.Engine.Arena!.GetBounds() == ArboretumArenas.AntFirstOpen(0).GetBounds(), "First wall failed opening.");
            f.Engine.Synchronize(f.Actors.Values, new(-120, 0, -190), f.Now, playerId: 100);
            Check(f.Engine.Arena.GetBounds() == ArboretumArenas.AntSecond(0).GetBounds(), "Crossing corridor failed second area.");
            f.Engine.Synchronize(f.Actors.Values, new(-120, 0, -180), f.Now, playerId: 100);
            Check(f.Engine.Arena.GetBounds() == ArboretumArenas.AntFirstOpen(0).GetBounds(), "Returning corridor failed previous area.");
            f.Engine.Animation(11, 0x00040008);
            Check(f.Engine.Arena.GetBounds() == ArboretumArenas.AntSecondOpen(0).GetBounds(), "Second wall failed opening.");
            f.Engine.ResetFight(); f.Scan();
            Check(f.Engine.Arena!.GetBounds() == ArboretumArenas.AntSecondOpen(0).GetBounds(), "Restart forgot scene wall state.");
            f.Engine.Synchronize(f.Actors.Values, new(-268, 0, -134), f.Now, playerId: 100); f.Render();
            Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null, "Queen room did not finish Ant stage.");
        });
        run("Arboretum Narbrooi wall preserves hole and survives primary removal until whole group dies", () =>
        {
            using var f = new Fight(0x142A, 511);
            f.Actors[10] = new(10, 0x1E9DFC, Vector3.Zero);
            f.Actors[11] = new(11, 0x142B, new(-110, 0, 30), InCombat: true);
            f.Actors[100] = new(100, 0, new(-110, 0, 30), Hostile: false); f.Scan();
            f.Engine.Animation(10, 0x00040008);
            Check(!f.Engine.Arena!.Contains(new(-112.773f, 0, 8.201f)), "Opening filled original hexagonal hole.");
            f.Engine.Synchronize(f.Actors.Values, new(-90, 0, 30), f.Now, playerId: 100);
            Check(f.Engine.Arena.GetBounds() == ArboretumArenas.NarbrooiSecond(0).GetBounds(), "Second area transition differs.");
            f.Engine.RemoveActor(1); f.Actors.Remove(1);
            f.Engine.Synchronize(f.Actors.Values, new(-110, 0, 30), f.Now, playerId: 100);
            Check(f.Engine.Active?.BossOid == 0x142A && f.Engine.Arena!.GetBounds() == ArboretumArenas.NarbrooiFirstOpen(0).GetBounds(),
                "Primary removal or backward transition lost encounter.");
            f.Actors[11] = f.Actors[11] with { Dead = true }; f.Scan();
            Check(f.Engine.Active == null, "All-dead Narbrooi group leaked.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

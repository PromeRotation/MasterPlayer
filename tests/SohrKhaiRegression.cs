using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Heavensward.Dungeon.D13SohrKhai;

internal static class SohrKhaiRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (shape, type, field) in new (IGeometryShape, Type, string)[]
        {
            (SohrKhaiArenas.Moglin(0), typeof(Source.D131ChieftainMoglin.D131ChieftainMoglin), "arena"),
            (SohrKhaiArenas.Hraesvelgr(0), typeof(Source.D133Hraesvelgr.D133Hraesvelgr), "arena"),
            (SohrKhaiArenas.GardenFirst(0), typeof(Source.D130CloudGardener.D130CloudGardener), "arena1"),
            (SohrKhaiArenas.GardenSecond(0), typeof(Source.D130CloudGardener.D130CloudGardener), "arena2"),
            (SohrKhaiArenas.Kargas(0), typeof(Source.D130Kargas.D130Kargas), "arena"),
            (SohrKhaiArenas.BlizzardFirst(0), typeof(Source.D130BlizzardDragon.D130BlizzardDragon), "arena1"),
            (SohrKhaiArenas.BlizzardBridge(0), typeof(Source.D130BlizzardDragon.D130BlizzardDragon), "Arena2"),
        })
            run($"SohrKhai {type.Name} {field} precise arena and both projections match", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(shape);
                var bounds = shape.GetBounds();
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .71f)
                    for (var z = bounds.MinZ + .273f; z < bounds.MaxZ; z += .71f)
                    {
                        var p = new Vector3(x, 0, z);
                        var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(shape.Contains(p) == expected && host.Contains(p) == expected, $"Arena differs at {p}.");
                    }
            });
        run("SohrKhai all 256 Poqhiraj wall masks match source union without internal edges", () =>
        {
            for (var mask = 0; mask < 256; ++mask)
            {
                var rectangles = new List<BossMod.Rectangle> { new(new(400, 104.166f), 4.5f, 19.75f) };
                for (var i = 0; i < 8; ++i)
                    if ((mask & 1 << i) != 0)
                    {
                        var j = i % 4;
                        rectangles.Add(new(new(i < 4 ? 395.25f : 404.75f,
                            89.161f + 10 * j + (j == 0 ? .125f : j == 3 ? -.125f : 0)), .25f, j is 0 or 3 ? 4.875f : 5));
                    }
                var source = new BossMod.ArenaBoundsCustom(rectangles.ToArray());
                var native = SohrKhaiPoqhirajArena.Create(mask, 0);
                var host = HostDrawingGeometry.Convert(native);
                foreach (var x in new[] { 394.99f, 395.1f, 395.4f, 395.6f, 400f, 404.4f, 404.6f, 404.9f, 405.01f })
                    for (var z = 84.417f; z < 124; z += .31f)
                    {
                        var p = new Vector3(x, 0, z);
                        var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Mask {mask} differs at {p}.");
                    }
                Check(native.GetContours().Count == 1, "Union retained internal tile edges.");
            }
        });
        run("SohrKhai remade territory owns six primary modules only", () =>
        {
            foreach (var oid in new uint[] { 0x1604, 0x15FB, 0x160E, 0x155C, 0x1612, 0x3D17 })
                Check(ArrDungeonCatalog.Owns(oid, 1112) && !ArrDungeonCatalog.Owns(oid, 555), "Wrong module ownership.");
            Check(!ArrDungeonCatalog.Owns(0x155D, 1112), "Helper claimed as module.");
        });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0x15FBu, 0x15FDu, 4429u, 20.9f), (0x15FBu, 0x1600u, 4428u, 20.9f), (0x15FBu, 0x15FCu, 4425u, 6.9f),
            (0x155Cu, 0x1B2u, 5778u, 40.5f), (0x155Cu, 0x1B2u, 6240u, 25f),
            (0x3D17u, 0x3D17u, 32136u, 50f), (0x3D17u, 0x3D17u, 32137u, 50f),
            (0x3D17u, 0x3D17u, 32131u, 40f), (0x3D17u, 0x3D17u, 32134u, 40f), (0x3D17u, 0x233Cu, 32135u, 6f),
        })
            run($"SohrKhai cast {action} preserves clock extent both views margin and cancel", () =>
            {
                using var f = Prepare(boss);
                var source = caster == boss ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Center(boss)); Scan(f); }
                Cast(f, source, action, 3, MathF.PI / 2);
                var cast = f.Engine.Casts[source];
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                var p = cast.Location + Vector3.UnitX * (range - .01f);
                Check(geometry.Contains(p) && HostDrawingGeometry.Convert(geometry).Contains(p)
                    && !geometry.Contains(cast.Location + Vector3.UnitX * (range + .01f)), "Wrong shape.");
                var area = f.Snapshot.DangerAreas.Single();
                Check(area.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3)
                    && area.Geometry.Contains(cast.Location + Vector3.UnitX * (range + .4f)), "Clock or margin differs.");
                f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancel retained output.");
            });
        run("SohrKhai Moglin latest cross uses birth clock effect latch and fresh spawn", () =>
        {
            using var f = Prepare(0x15FB);
            f.Engine.ObjectCreated(10, f.Start);
            f.Now = f.Start.AddSeconds(.5); f.Actors[10] = new(10, 0x1603, Center(0x15FB)); Scan(f);
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6.3), "Cross birth drift.");
            f.Engine.ObjectCreated(11, f.Start.AddSeconds(1));
            f.Actors[11] = new(11, 0x1603, Center(0x15FB) + Vector3.UnitX * 5); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(7.3), "Latest cross did not replace old one.");
            f.Engine.Effect(1, 6212, 1, f.Now); Scan(f);
            Check(f.Draw.Previews.Count == 0, "Resolved cross resurrected.");
        });
        run("SohrKhai Moglin cleave cycles praise relevance status packet and invincibility ban", () =>
        {
            using var f = Prepare(0x15FB);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 };
            f.Actors[101] = new(101, 0, Center(0x15FB) + Vector3.UnitX * 10, PartySlot: 1, Hostile: false); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Initial cleave missing.");
            f.Engine.Effect(1, 6019, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cleave not resolved.");
            Cast(f, 1, 6017, 2); Check(f.Draw.Previews.Count == 1, "Mogcane did not rearm cleave.");
            f.Engine.Effect(1, 6019, 2, f.Now); f.Engine.Finish(1);
            f.Actors[10] = new(10, 0x1B2, Center(0x15FB));
            f.Actors[11] = new(11, 0x15FC, Center(0x15FB));
            f.Actors[12] = new(12, 0x1600, Center(0x15FB) + Vector3.UnitX * 10); Scan(f);
            Cast(f, 10, 6016, 13);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 11, StatusId = 1064, Timestamp = f.Now }, f.Now); f.Render();
            f.Engine.StatusChanged(1, 325, true, f.Now); f.Render();
            Check(f.Engine.PriorityTarget(1, Center(0x15FB)) == 11 && f.Engine.ForbiddenTargets.Contains(1UL)
                && f.Draw.Previews.Count == 1 && f.Engine.Hints(null).Any(h => h.Contains("推出")), "Praise/ban missing.");
            f.Actors[11] = f.Actors[11] with { Position = Center(0x15FB) + Vector3.UnitX * 5 }; Scan(f);
            Check(f.Draw.Previews.Count == 0, "Moogle outside 4.9 still relevant.");
            Cast(f, 1, 6015, 13); f.Engine.Finish(1, 6015, completed: true); f.Render();
            Check(f.Engine.Hints(null).All(h => !h.Contains("推出")), "Praise positions leaked.");
        });
        run("SohrKhai Poqhiraj intact walls stop thirty-yard knockback and broken walls permit falling", () =>
        {
            var point = new Vector3(400, 0, 89.161f);
            Check(Math.Abs(SohrKhaiPoqhirajArena.Landing(point, MathF.PI / 2, 0).X - 404.5f) < .01, "Intact right wall ignored.");
            Check(SohrKhaiPoqhirajArena.Landing(point, MathF.PI / 2, 1 << 4).X > 429.9, "Broken wall still stops knockback.");
            Check(SohrKhaiPoqhirajArena.Landing(point, 0, 0).Z > 119, "Non-wall end incorrectly stopped knockback.");
            using var f = Prepare(0x155C);
            f.Actors[100] = f.Actors[100] with { Position = point };
            f.Actors[10] = new(10, 0x1EA076, point); f.Actors[11] = new(11, 0x1B2, new(398, 0, 89.161f)); Scan(f);
            f.Engine.Animation(10, 0x00100020);
            Cast(f, 11, 5823, 4.9, MathF.PI / 2);
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == 2
                && f.Engine.AmbientAoes().Single(a => a.Id == "sohr/knockback/11").Shape.Resolve(point, 0, null).Contains(point), "Broken-wall constraint missing.");
            Check(f.Draw.Previews.Values.Any(p => p.Geometry.Contains(new(430, 0, 89.161f))), "Knockback landing not drawn.");
            var arena = f.Engine.Arena; f.Engine.Animation(10, 0x00100020);
            Check(ReferenceEquals(arena, f.Engine.Arena), "Duplicate animation rebuilt arena.");
            f.Actors[12] = new(12, 0x1B2, new(398, 0, 99.161f)); Scan(f); Cast(f, 12, 5823, 4.9, MathF.PI / 2);
            f.Engine.Finish(11, 5823, completed: true); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "One finish did not clear all knockbacks.");
        });
        run("SohrKhai Poqhiraj cloud marker actual birth delayed lightning and buster predictions", () =>
        {
            using var f = Prepare(0x155C, target: true);
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(6.1), "Initial RearHoof missing.");
            f.Engine.Icon(100, 24, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "sohr/cloudbait/center"), "Cloud target did not avoid center.");
            f.Engine.ObjectCreated(10, f.Start.AddSeconds(1)); f.Now = f.Start.AddSeconds(1.4);
            f.Actors[10] = new(10, 0x155D, Center(0x155C) + Vector3.UnitX * 4); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.Contains("cloudbait")) && f.Engine.PredictedDamage.Count() == 2, "Cloud did not clear bait and add buster.");
            Check(Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start.AddSeconds(8.8)).TotalSeconds) < .000001, "Cloud activation drift.");
            Cast(f, 10, 6010, 3); f.Engine.Finish(10, 6010, completed: true); Scan(f);
            Check(f.Draw.Previews.Count == 0, "Lightning finish did not clear or old cloud resurrected.");
            f.Engine.Effect(1, 6013, 1, f.Now); Check(!f.Engine.PredictedDamage.Any(), "RearHoof did not clear predictions.");
        });
        run("SohrKhai Poqhiraj BurningBright survives natural cast finish and avoids last walls", () =>
        {
            using var f = Prepare(0x155C);
            f.Actors[10] = new(10, 0x155E, Center(0x155C) + Vector3.UnitZ * 15, HitboxRadius: 5); Scan(f);
            Cast(f, 1, 6011, 3, target: 100);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("sohr/bright/wall/")) && f.Draw.Previews.Count == 2, "Wall avoidance missing.");
            f.Engine.Finish(1, 6011, completed: true); f.Render();
            Check(f.Draw.Previews.Count == 2, "Natural finish cleared bait before effect.");
            f.Engine.Effect(1, 6011, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "BurningBright effect did not clear.");
        });
        run("SohrKhai Hraesvelgr first five then six AkhMorn hits deduplicate and preserve stack", () =>
        {
            using var f = Prepare(0x3D17);
            f.Actors[101] = new(101, 0, Center(0x3D17) + Vector3.UnitX * 4, PartySlot: 1, Hostile: false); Scan(f);
            for (var cycle = 0; cycle < 2; ++cycle)
            {
                f.Now = f.Start.AddSeconds(cycle * 20);
                Cast(f, 1, 32132, 5, target: 101); f.Engine.Finish(1, 32132, completed: true); f.Render();
                Check(f.Snapshot.GoalAreas.Count == 1, "Natural finish cleared AkhMorn.");
                var count = cycle == 0 ? 5 : 6;
                for (var hit = 1; hit <= count; ++hit)
                {
                    var action = hit == 1 ? 32132u : 32133u;
                    var sequence = (uint)(cycle * 20 + hit);
                    f.Engine.Effect(1, action, sequence, f.Now);
                    f.Engine.Effect(1, action, sequence, f.Now, 100);
                    f.Render();
                    Check(f.Snapshot.GoalAreas.Count == (hit == count ? 0 : 1), "Wrong AkhMorn count.");
                }
            }
        });
        run("SohrKhai HolyOrb advances by actual source position five hits and three previews", () =>
        {
            using var f = Prepare(0x3D17);
            var origin = Center(0x3D17) - Vector3.UnitZ * 15;
            f.Actors[10] = new(10, 0x233C, origin); Scan(f); Cast(f, 10, 32129, 5);
            Check(f.Draw.Previews.Count == 3, "Initial orb preview cap differs.");
            f.Engine.Finish(10, 32129, completed: true);
            for (var hit = 0; hit < 5; ++hit)
            {
                f.Now = f.Start.AddSeconds(5 + hit);
                f.Actors[10] = f.Actors[10] with { Position = origin + Vector3.UnitZ * (6.8f * hit), Cast = null }; Scan(f);
                var action = hit == 0 ? 32129u : 32130u;
                f.Engine.Effect(10, action, (uint)hit, f.Now); f.Engine.Effect(10, action, (uint)hit, f.Now); f.Render();
                Check(f.Draw.Previews.Count == Math.Min(3, 4 - hit), "Orb count differs.");
                if (hit < 4) Check(f.Snapshot.DangerAreas.Min(a => a.DangerWindows.Single().DangerFrom) == f.Now.AddSeconds(1), "Orb step clock differs.");
            }
            Check(f.Snapshot.DangerAreas.Count == 0, "Final orb leaked.");
        });
        run("SohrKhai HolyBreath icon clock perspective and effect clearing", () =>
        {
            using var f = Prepare(0x3D17);
            f.Actors[101] = new(101, 0, Center(0x3D17) + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(101, 311, f.Start); f.Render();
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6), "Icon delay changed.");
            f.PlayerId = 101; Scan(f);
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position), "Viewer switch did not separate from buddy.");
            f.Engine.Effect(10, 32139, 1, f.Now, 101); f.Render();
            Check(f.Draw.Previews.Count == 0, "Spread effect did not clear.");
        });
        run("SohrKhai thin ice constraints match source inverted union standard camera wall and danger", () =>
        {
            using var f = Prepare(0x3D17);
            var center = Center(0x3D17);
            f.Actors[100] = f.Actors[100] with { Statuses = [911], Rotation = MathF.PI / 2 }; Scan(f);
            var constraint = f.Snapshot.DangerAreas.Single().Geometry;
            foreach (var distance in new[] { 0f, .49f, .6f, 10.9f, 11.1f, 12.1f, 12.3f, 21.9f, 22.1f, 23.1f, 23.3f })
                Check(constraint.Contains(center + Vector3.UnitX * distance) == (distance > .5f && !(distance >= 11 && distance <= 12.2f || distance >= 22 && distance <= 23.2f)), "ThinIce feasible set differs.");
            Check(f.Draw.Previews.Values.Any(p => p.Geometry.Contains(center + Vector3.UnitX * 11)), "Standard 11-yard preview missing.");
            f.Engine.LegacyMovement = true; f.Engine.CameraAzimuth = 0; f.Render();
            Check(f.Draw.Previews.Values.Any(p => p.Geometry.Contains(center - Vector3.UnitZ * 11)), "Legacy camera direction missing.");
            f.Engine.LegacyMovement = false;
            f.Actors[10] = new(10, 0x233C, center + Vector3.UnitX * 11); Scan(f); Cast(f, 10, 32135, 5);
            Check(f.Engine.Hints(null).Any(h => h.Contains("落点处于危险")), "Dangerous ice landing not detected.");
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 17 }; Scan(f);
            var landing = f.Engine.AmbientAoes().Single(a => a.Id == "sohr/ice/landing").Position;
            Check(f.Engine.Arena!.Contains(landing) && landing.X < 420, "Ice did not stop at wall.");
            f.Engine.StatusChanged(100, 911, false, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().All(a => !a.Id.StartsWith("sohr/ice")), "Status lose did not clear ice.");
        });
        run("SohrKhai trash pulls and group lifecycle retain dead hidden Blizzard primary", () =>
        {
            using var f = new Fight(0x1604, 1112);
            f.Engine.ResetFight();
            var garden = SohrKhaiArenas.GardenFirst(0).Position;
            f.Actors[1] = new(1, 0x1604, garden);
            f.Engine.Synchronize(f.Actors.Values, garden, f.Now);
            Check(f.Engine.Active?.BossOid == 0x1604, "Garden must start on entry without combat.");
            f.Actors[10] = new(10, 0x1605, garden, InCombat: true);
            f.Actors[1] = f.Actors[1] with { Dead = true };
            f.Engine.Synchronize(f.Actors.Values, garden, f.Now);
            Check(f.Engine.Active?.BossOid == 0x1604 && f.Engine.PrimaryId == 10, "Garden group ended with first primary.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Engine.Synchronize(f.Actors.Values, garden, f.Now);
            Check(f.Engine.Active == null, "Garden not ended after group death.");
            f.Actors.Clear();
            f.Actors[1] = new(1, 0x1612, new(364, 0, -225), Targetable: false);
            f.Actors[10] = new(10, 0x1611, new(364, 0, -225), InCombat: true);
            f.Engine.Synchronize(f.Actors.Values, new(364, 0, -225), f.Now);
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Actors[10] = f.Actors[10] with { Dead = true };
            f.Engine.Synchronize(f.Actors.Values, new(364, 0, -225), f.Now);
            Check(f.Engine.Active?.BossOid == 0x1612, "Dead hidden primary treated as destroyed.");
            f.Actors.Remove(1); f.Engine.RemoveActor(1);
            Check(f.Engine.Active == null, "Destroyed primary did not end phase.");
        });
        run("SohrKhai Blizzard bridge changes both bounds and Cauterize predicts delayed touchdown", () =>
        {
            using var f = new Fight(0x1612, 1112);
            f.Actors[10] = new(10, 0x1611, new(364, 0, -225), InCombat: true);
            f.Actors[11] = new(11, 0x1636, new(364, 0, -225));
            f.Actors[12] = new(12, 0x1EA032, new(364, 0, -225));
            f.Engine.Synchronize(f.Actors.Values, new(364, 0, -225), f.Now); f.Render();
            var old = f.Engine.Arena;
            f.Engine.Animation(12, 0x00040008); f.Render();
            Check(!ReferenceEquals(old, f.Engine.Arena), "Bridge animation ignored.");
            Cast(f, 11, 6241, 6);
            Check(f.Snapshot.DangerAreas.Count == 2 && Math.Abs((f.Snapshot.DangerAreas.Max(a => a.DangerWindows.Single().DangerFrom) - f.Start.AddSeconds(13)).TotalSeconds) < .000001, "Touchdown 6.7 after cast missing.");
            f.Engine.Finish(11, 6241, completed: true); f.Render();
            Check(f.Draw.Previews.Count == 1, "Touchdown cleared with dive.");
            f.Engine.Effect(10, 564, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0, "Touchdown effect not cleared.");
        });
        run("SohrKhai actual solver escapes broken-wall landing constraint toward intact segment", () =>
        {
            using var f = Prepare(0x155C);
            var point = new Vector3(400, 0, 89.161f);
            f.Actors[100] = f.Actors[100] with { Position = point };
            f.Actors[10] = new(10, 0x1EA076, point); f.Actors[11] = new(11, 0x1B2, new(398, 0, 89.161f)); Scan(f);
            f.Engine.Animation(10, 0x00100020); Cast(f, 11, 5823, 4.9, MathF.PI / 2);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(point, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No safe knockback route.");
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(path.Count > 1 && path.All(p => f.Engine.Arena!.Contains(p.Position)), "Knockback route invalid.");
            Check(snapshot.DangerAreas.All(a => !a.Geometry.Contains(path[^1].Position)), $"Route ended in falling zone: {path[^1].Position}.");
        });
        foreach (var (boss, caster, action, position, range) in new[]
        {
            (0x1604u, 0x1606u, 4700u, new Vector3(-400.01f, 0, 90.185f), 9f),
            (0x1604u, 0x1605u, 6237u, new Vector3(-400.01f, 0, 90.185f), 18f),
            (0x1604u, 0x1604u, 6236u, new Vector3(-400.01f, 0, 90.185f), 5f),
            (0x160Eu, 0x160Fu, 2478u, new Vector3(405, 0, 210), 6f),
            (0x1612u, 0x1613u, 4724u, new Vector3(436, 0, -205), 4f),
            (0x1612u, 0x1611u, 4794u, new Vector3(436, 0, -205), 5f),
        })
            run($"SohrKhai trash cast {action} owns shape clock both views and cancellation", () =>
            {
                using var f = new Fight(boss, 1112);
                f.Actors[1] = new(1, boss, position, InCombat: true);
                var source = boss == caster ? 1UL : 10UL;
                if (source != 1) f.Actors[source] = new(source, caster, position, InCombat: true);
                f.Engine.Synchronize(f.Actors.Values, position, f.Now);
                Cast(f, source, action, 3, MathF.PI / 2);
                var shape = f.Draw.Previews.Single().Value.Geometry;
                var p = position + Vector3.UnitX * (range - .01f);
                Check(shape.Contains(p) && HostDrawingGeometry.Convert(shape).Contains(p)
                    && !shape.Contains(position + Vector3.UnitX * (range + .01f)), "Trash extent differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "Trash clock drift.");
                f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Trash cancellation leaked.");
            });
        run("SohrKhai Kargas requires combat in bounds and keeps whole group until destroyed", () =>
        {
            using var f = new Fight(0x160E, 1112);
            Vector3 p = new(405, 0, 210);
            f.Actors[1] = new(1, 0x160E, p);
            f.Engine.Synchronize(f.Actors.Values, p, f.Now);
            Check(f.Engine.Active == null, "Kargas started without combat.");
            f.Actors[10] = new(10, 0x160C, Vector3.Zero, InCombat: true);
            f.Engine.Synchronize(f.Actors.Values, p, f.Now);
            Check(f.Engine.Active == null, "Combat outside bounds started Kargas.");
            f.Actors[10] = f.Actors[10] with { Position = p }; f.Engine.Synchronize(f.Actors.Values, p, f.Now);
            Check(f.Engine.Active?.BossOid == 0x160E, "Ally pull did not start Kargas.");
            f.Actors.Remove(1); f.Engine.RemoveActor(1);
            f.Actors[10] = f.Actors[10] with { Position = Vector3.Zero }; f.Engine.Synchronize(f.Actors.Values, p, f.Now);
            Check(f.Engine.Active?.BossOid == 0x160E, "Living group outside bounds ended Kargas.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Engine.Synchronize(f.Actors.Values, p, f.Now);
            Check(f.Engine.Active == null, "Dead group did not end Kargas.");
        });
        run("SohrKhai first Gallop finish adds one buster and cancel does not add one", () =>
        {
            using var f = Prepare(0x155C, target: true);
            f.Actors[10] = new(10, 0x1B2, Center(0x155C)); Scan(f);
            Cast(f, 10, 5823, 4.9, MathF.PI / 2);
            f.Engine.Finish(10, 5823, f.Start.AddSeconds(1)); f.Render();
            Check(f.Engine.PredictedDamage.Count() == 1, "Cancelled Gallop predicted buster.");
            f.Now = f.Start.AddSeconds(2); Cast(f, 10, 5823, 4.9, MathF.PI / 2);
            f.Engine.Finish(10, 5823, f.Start.AddSeconds(6.9), completed: true);
            Check(f.Engine.PredictedDamage.Count() == 2
                && Math.Abs((f.Engine.PredictedDamage.Max(b => b.Activation) - f.Start.AddSeconds(13.9)).TotalSeconds) < .000001, "First Gallop buster missing.");
            f.Now = f.Start.AddSeconds(15); Cast(f, 10, 5823, 4.9, MathF.PI / 2);
            f.Engine.Finish(10, 5823, f.Start.AddSeconds(19.9), completed: true);
            Check(f.Engine.PredictedDamage.Count() == 2, "Later Gallop incorrectly added buster.");
        });
        run("SohrKhai delayed birth corrects cloud and buster clocks without repeating spawn", () =>
        {
            using var f = Prepare(0x155C, target: true);
            f.Now = f.Start.AddSeconds(1);
            f.Actors[10] = new(10, 0x155D, Center(0x155C)); Scan(f);
            f.Engine.ObjectCreated(10, f.Start); f.Render();
            Check(Math.Abs((f.Engine.PredictedDamage.Last().Activation - f.Start.AddSeconds(4)).TotalSeconds) < .000001
                && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(7.8), "Delayed birth did not correct clocks.");
            Scan(f); Check(f.Engine.PredictedDamage.Count() == 2, "Repeated snapshot added buster.");
        });
        run("SohrKhai restored wall state precedes cast and repeated bridge snapshot preserves arena reference", () =>
        {
            using var f = Prepare(0x155C);
            f.Actors[10] = new(10, 0x1EA076, Center(0x155C), EventAnimation: 0x00100020);
            f.Actors[11] = new(11, 0x1B2, new(398, 0, 89.161f), Cast: new(5823, new(398, 0, 89.161f), MathF.PI / 2,
                f.Start, f.Start.AddSeconds(5.2))); Scan(f);
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("sohr/knockback/goal/")) == 7, "Restored cast preceded broken-wall state.");
            f.Engine.ResetFight(); f.Actors.Clear();
            f.Actors[1] = new(1, 0x1612, new(436, 0, -205));
            f.Actors[10] = new(10, 0x1611, new(436, 0, -205), InCombat: true);
            f.Actors[11] = new(11, 0x1EA032, new(436, 0, -205), EventAnimation: 0x00040008);
            f.Engine.Synchronize(f.Actors.Values, new(436, 0, -205), f.Now);
            var arena = f.Engine.Arena;
            f.Engine.Synchronize(f.Actors.Values, new(436, 0, -205), f.Now);
            Check(ReferenceEquals(arena, f.Engine.Arena), "Bridge snapshot rebuilt boundary every scan.");
        });
        run("SohrKhai cancelled sequences perspective death and mechanism disable clear owned output", () =>
        {
            using var f = Prepare(0x3D17);
            f.Actors[101] = new(101, 0, Center(0x3D17) + Vector3.UnitX, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x233C, Center(0x3D17)); Scan(f);
            Cast(f, 1, 32132, 5, target: 101); Cast(f, 10, 32129, 5);
            f.Engine.Finish(1); f.Engine.Finish(10); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Cancel retained sequences.");
            f.Now = f.Start.AddSeconds(10); Cast(f, 1, 32132, 5, target: 101);
            f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
            Check(f.Snapshot.GoalAreas.Count == 0, "Dead stack target retained goal.");
            f.Engine.Synchronize(f.Actors.Values, Center(0x3D17), f.Now, _ => false, f.PlayerId); f.Render();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Engine.Arena == null, "Disabled encounter retained output.");
        });
    }

    private static Vector3 Center(uint boss) => boss switch
    {
        0x15FB => new(-400, 0, -158.04f), 0x155C => new(400, 0, 104.166f), 0x3D17 => new(400, 0, -400), _ => Vector3.Zero,
    };
    private static Fight Prepare(uint boss, bool target = false)
    {
        var f = new Fight(boss, 1112);
        f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true, TargetId: target ? 100UL : 0);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, PartyRole: "Tank", Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f)
    {
        f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position ?? f.Actors[1].Position, f.Now, playerId: f.PlayerId);
        f.Render();
    }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0)
    {
        var observation = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = observation };
        f.Engine.StartCast(source, observation, f.Now); f.Render();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

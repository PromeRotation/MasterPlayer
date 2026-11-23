using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Heavensward.Dungeon.D15Xelphatol;
using Dotoli = BossMod.Heavensward.Dungeon.D11Antitower.D152DotoliCiloc.D152DotoliCiloc;

internal static class XelphatolRegression
{
    private const BindingFlags STATIC = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (name, native, source) in Arenas())
            run($"Xelphatol {name} precise polygon holes and both views match source", () => Compare(native, source));
        run("Xelphatol ownership covers seven modules and remade territory only", () =>
        {
            foreach (var oid in new uint[] { 0x17AF, 0x179B, 0x17B5, 0x17BA, 0x17B8, 0x179F, 0x17A2 })
                Check(ArrDungeonCatalog.Owns(oid, 1113) && !ArrDungeonCatalog.Owns(oid, 572), "Wrong primary ownership.");
            Check(!ArrDungeonCatalog.Owns(0x17A0, 1113), "Whirlwind claimed as module.");
        });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0x179Bu, 0x179Bu, 6599u, 61.5f), (0x179Fu, 0x179Fu, 6606u, 10.98f),
            (0x179Fu, 0x179Fu, 32557u, 6f), (0x17A2u, 0x17A2u, 6612u, 43f),
            (0x17A2u, 0xD25u, 6619u, 20f), (0x17A2u, 0x17A4u, 6620u, 32.89f),
            (0x17A2u, 0x17A4u, 6621u, 7f), (0x17AFu, 0x17AEu, 1656u, 8.16f),
            (0x17AFu, 0x17ADu, 1006u, 10.32f), (0x17AFu, 0xD25u, 6852u, 4f),
            (0x17AFu, 0x17AFu, 2478u, 6f), (0x17B5u, 0x17B2u, 720u, 7.08f),
            (0x17B5u, 0x17B5u, 6625u, 20.85f), (0x17BAu, 0x17B7u, 6627u, 5f),
            (0x17B8u, 0x17B8u, 6628u, 41.8f),
        })
            run($"Xelphatol cast {action} clock extent both views margin and cancellation", () =>
            {
                using var f = Prepare(boss);
                if (boss == 0x179F) Shrink(f);
                var id = caster == boss ? 1UL : 10UL;
                if (id != 1) { f.Actors[id] = new(id, caster, Center(boss)); Scan(f); }
                Cast(f, id, action, 3, MathF.PI / 2);
                var c = f.Engine.Casts[id];
                var shape = f.Draw.Previews.Single().Value.Geometry;
                var p = c.Location + Vector3.UnitX * (range - .01f);
                Check(shape.Contains(p) && HostDrawingGeometry.Convert(shape).Contains(p)
                    && !shape.Contains(c.Location + Vector3.UnitX * (range + .01f)), "Wrong display extent.");
                var area = f.Snapshot.DangerAreas.Single();
                Check(area.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3)
                    && area.Geometry.Contains(c.Location + Vector3.UnitX * (range + .4f)), "Timing or single margin differs.");
                f.Engine.Finish(id); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Cancel retained output.");
            });
        run("Xelphatol Nuzal invincibility forbids every protected target and prioritizes airstones", () =>
        {
            using var f = Prepare(0x179B);
            f.Actors[10] = new(10, 0x179E, Center(0x179B));
            f.Actors[11] = new(11, 0x179D, Center(0x179B));
            f.Actors[12] = new(12, 0x179C, Center(0x179B)); Scan(f);
            foreach (var id in new ulong[] { 1, 10, 12 })
                f.Events.Handle(new LogSystemStatusAddEvent { TargetId = id, StatusId = 775, Timestamp = f.Now }, f.Now);
            f.Render();
            Check(f.Engine.ForbiddenTargets.Order().SequenceEqual([1UL, 10UL, 12UL])
                && f.Engine.PriorityTarget(10, Center(0x179B)) == 11
                && f.Engine.Hints(null).Any(h => h.Contains("气石")), "Shield control or priority missing.");
            f.Actors.Remove(11); Scan(f);
            Check(f.Engine.PriorityTarget(1, Center(0x179B)) == 0, "Protected turret selected.");
            f.Engine.StatusChanged(10, 775, false, f.Now); f.Render();
            Check(f.Engine.PriorityTarget(1, Center(0x179B)) == 10 && !f.Engine.ForbiddenTargets.Contains(10UL), "Shield removal did not restore turret.");
        });
        run("Xelphatol HotBlast locks boss circle and survives natural finish until activation plus one", () =>
        {
            using var f = Prepare(0x179B);
            f.Actors[10] = new(10, 0x179E, Center(0x179B) + Vector3.UnitX * 10); Scan(f);
            Cast(f, 10, 6604, 6);
            f.Actors[1] = f.Actors[1] with { Position = Center(0x179B) + Vector3.UnitZ * 10 }; Scan(f);
            var area = f.Snapshot.DangerAreas.Single();
            Check(f.Snapshot.GoalAreas.Count == 0 && !area.Geometry.Contains(Center(0x179B))
                && area.Geometry.Contains(Center(0x179B) + Vector3.UnitX * 4.01f), "Safe circle became a preference or moved with boss.");
            f.Now = f.Start.AddSeconds(6); f.Actors[10] = f.Actors[10] with { Cast = null }; Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Natural finish cleared HotBlast.");
            f.Now = f.Start.AddSeconds(7.299); Scan(f); Check(f.Draw.Previews.Count == 1, "HotBlast ended early.");
            f.Now = f.Start.AddSeconds(7.3); Scan(f); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "HotBlast expired late.");
            f.Now = f.Start.AddSeconds(20); Cast(f, 10, 6604, 6); f.Engine.Finish(10); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled HotBlast persisted.");
        });
        run("Xelphatol HotBlast real solver enters safe circle", () =>
        {
            using var f = Prepare(0x179B);
            f.Actors[10] = new(10, 0x179E, Center(0x179B)); Scan(f); Cast(f, 10, 6604, 6);
            Solve(f, Center(0x179B) + Vector3.UnitX * 12);
        });
        run("Xelphatol Dotoli initial ring clock animation shrinks with inward hitbox and restores before cast", () =>
        {
            using var f = Prepare(0x179F);
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4), "Initial shrink clock wrong.");
            Check(f.Engine.Arena!.Contains(Center(0x179F) + Vector3.UnitX * 25), "Initial arena already small.");
            Shrink(f);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0
                && !f.Engine.Arena!.Contains(Center(0x179F) + Vector3.UnitX * 25), "Shrink event ignored.");
            var arena = f.Engine.Arena; Scan(f); Check(ReferenceEquals(arena, f.Engine.Arena), "Duplicate animation rebuilds boundary.");
            Cast(f, 1, 6607, 3); Check(f.Snapshot.DangerAreas.Count == 2, "Knockback constraints missing after restore.");
        });
        run("Xelphatol Dotoli safe walls match source rotations and radial landing", () =>
        {
            var type = typeof(Dotoli).Assembly.GetType("BossMod.Heavensward.Dungeon.D11Antitower.D152DotoliCiloc.OnHigh")!;
            var walls = (BossMod.Components.GenericKnockback.SafeWall[])type.GetField("allSafeWalls", STATIC)!.GetValue(null)!;
            var arena = XelphatolArenas.DotoliSmall(0);
            for (var i = 0; i < walls.Length; ++i)
            {
                var native = XelphatolKnockback.WALLS[i];
                Check(Vector2.Distance(native.A, new(walls[i].Vertex1.X, walls[i].Vertex1.Z)) < .0001f
                    && Vector2.Distance(native.B, new(walls[i].Vertex2.X, walls[i].Vertex2.Z)) < .0001f, "Wall rotation differs.");
            }
            var center = Center(0x179F);
            for (var i = 0; i < 4; ++i)
            {
                var dir = new Vector3(MathF.Sin(i * MathF.PI / 2), 0, MathF.Cos(i * MathF.PI / 2));
                var landing = XelphatolKnockback.Landing(arena, center, center + dir * 5, .5f);
                Check(arena.Contains(landing, 0) && Vector3.Distance(center, landing) is > 17 and < 20, "Cardinal wall did not stop.");
            }
            var diagonal = center + new Vector3(5, 0, 5);
            Check(!arena.Contains(XelphatolKnockback.Landing(arena, center, diagonal, .5f), 0), "Ordinary boundary became safe wall.");
        });
        run("Xelphatol Dotoli delayed whirlwind rebuilds safe complement and landing dangers", () =>
        {
            using var f = Prepare(0x179F); Shrink(f);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x179F) + Vector3.UnitX * 5 }; Scan(f);
            Cast(f, 1, 6607, 3);
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("xel/shelter/") && a.Display) == 4
                && f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 6, "Shelter union/preview missing.");
            var safe = Center(0x179F) + Vector3.UnitX * 5;
            var unsafePosition = Center(0x179F) + new Vector3(5, 0, 5);
            var before = f.Engine.AmbientAoes().Single(a => a.Id == "xel/shelter/avoid").Shape.Resolve(default, 0, null);
            Check(!before.Contains(safe) && before.Contains(unsafePosition), "Safe cones not complemented.");
            f.Actors[10] = new(10, 0x17A0, Center(0x179F) + Vector3.UnitX * 18); Scan(f);
            Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("xel/shelter/") && a.Display) == 3, "Late whirlwind did not remove east shelter.");
            var after = f.Engine.AmbientAoes().Single(a => a.Id == "xel/shelter/avoid").Shape.Resolve(default, 0, null);
            Check(after.Contains(safe) && f.Snapshot.DangerAreas.Count == 3, "Late whirlwind missing from navigation.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "xel/wind/10"), "Present dead whirlwind prematurely removed.");
            f.Engine.Finish(1, 6607, completed: true); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 1, "Knockback finish leaked.");
            f.Engine.RemoveActor(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Destroyed whirlwind remains.");
        });
        run("Xelphatol Dotoli real solver reaches safe wall sector without whirlwind", () =>
        {
            using var f = Prepare(0x179F); Shrink(f);
            f.Actors[10] = new(10, 0x17A0, Center(0x179F) + Vector3.UnitX * 18); Scan(f);
            Cast(f, 1, 6607, 5); Solve(f, Center(0x179F) + new Vector3(5, 0, 5));
        });
        run("Xelphatol Dotoli immunity must cover activation and never hides shelter hint", () =>
        {
            using var f = Prepare(0x179F); Shrink(f);
            f.Actors[100] = f.Actors[100] with { Statuses = [1209], StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(10) } };
            Scan(f); Cast(f, 1, 6607, 3);
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 4, "Immune player still has knockback movement.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = f.Start.AddSeconds(3) } }; Scan(f);
            Check(f.Snapshot.DangerAreas.Count == 2, "Expired immunity suppresses knockback.");
        });
        run("Xelphatol Swiftfeather arms cleave until OnLow starts", () =>
        {
            using var f = Prepare(0x179F); Shrink(f);
            f.Actors[1] = f.Actors[1] with { TargetId = 101 };
            f.Actors[101] = new(101, 0, Center(0x179F) + Vector3.UnitX * 5, PartySlot: 1, Hostile: false); Scan(f);
            Cast(f, 1, 6609, 3); f.Engine.Effect(1, 6609, 1, f.Now); f.Engine.Finish(1, 6609, completed: true); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Haste cleave ended on effect.");
            f.Now = f.Start.AddSeconds(10); Cast(f, 1, 6606, 4);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("xel/haste")) && f.Draw.Previews.Count == 1, "OnLow did not disarm cleave.");
        });
        run("Xelphatol icon spreads use event clock perspective death and self-target fallback", () =>
        {
            using var f = Prepare(0x179F); Shrink(f);
            f.Actors[101] = new(101, 0, Center(0x179F) + Vector3.UnitX * 10, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(101, 139, f.Start.AddSeconds(-.5)); f.Render();
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.6), "DarkWings icon clock drift.");
            f.PlayerId = 101; Scan(f); Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position), "Viewer switch retained prior separation.");
            f.Engine.Effect(1, 32556, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Self-target effect did not remove marker.");
            f.Engine.Icon(101, 139, f.Now); f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 0, "Dead spread target retained marker.");
        });
        run("Xelphatol Tozol stack survives visual cast finish and clears on real hit", () =>
        {
            using var f = Prepare(0x17A2);
            f.Actors[101] = new(101, 0, Center(0x17A2) + Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x17A3, Center(0x17A2)); Scan(f);
            f.Engine.Icon(101, 62, f.Start); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Draw.Previews.Count == 1 && f.Engine.PredictedDamage.All(p => p.Activation == f.Start.AddSeconds(5.5)), "Four-person stack missing.");
            Cast(f, 10, 6618, 5, target: 100); f.Engine.Finish(10, 6618, completed: true); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1, "Bill finish cleared icon stack.");
            f.Engine.Effect(11, 6617, 1, f.Now); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0, "Actual stack hit leaked.");
        });
        foreach (var (boss, position) in new[]
        {
            (0x17AFu, new Vector3(-154, 0, 211)), (0x17AFu, new Vector3(-115, 0, 40)),
            (0x17B5u, new Vector3(70.5f, 0, -56)), (0x17B5u, new Vector3(178, 0, -4)),
            (0x17BAu, new Vector3(125, 0, -37)), (0x17BAu, new Vector3(420, 0, -254)),
            (0x17BAu, new Vector3(316, 0, -133)),
        })
            run($"Xelphatol trash {boss:X} at {position} group pull replacement and end", () =>
            {
                using var f = Prepare(boss, position);
                var buddyOid = boss == 0x17AF ? 0x17ACu : 0x17B1u;
                f.Actors[1] = f.Actors[1] with { InCombat = false };
                f.Actors[10] = new(10, buddyOid, position, InCombat: true); Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Group combat did not pull.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.PrimaryId == 10, "Primary death ended live group.");
                f.Actors.Remove(10); Scan(f); Check(f.Engine.Active == null, "Empty group remains active.");
            });
        run("Xelphatol Skycaller default pull requires primary combat then retains live group", () =>
        {
            using var f = Prepare(0x17B8);
            f.Engine.ResetFight(); f.Actors[1] = f.Actors[1] with { InCombat = false };
            f.Actors[10] = new(10, 0x17B7, Center(0x17B8), InCombat: true); Scan(f);
            Check(f.Engine.Active == null, "Skycaller started from hornbill combat.");
            f.Actors[1] = f.Actors[1] with { InCombat = true }; Scan(f); Cast(f, 1, 6629, 3);
            Check(f.Engine.Hints(null).Any(h => h.Contains("IxaliAeroIII") && h.Contains("打断")), "Interrupt name missing.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x17B8, "Live hornbill did not retain group.");
        });
        run("Xelphatol Swiftbeak any targetable key ends phase and prevents repeated restart", () =>
        {
            using var f = Prepare(0x17BA);
            f.Actors[20] = new(20, 0x1EA165, new(-1000, 0, -1000), Targetable: true, Hostile: false); Scan(f);
            Check(f.Engine.Active == null && f.Engine.Arena == null, "Key outside arena did not end phase.");
            Scan(f); Check(f.Engine.Active == null && f.Draw.Previews.Count == 0, "Phase restarted while key remains targetable.");
            f.Actors[20] = f.Actors[20] with { Targetable = false }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x17BA, "Fresh closed-key fight did not start.");
        });
        run("Xelphatol Siege first yell and automatic second ring keep four-second activation", () =>
        {
            using var f = Prepare(0x17B5);
            Check(f.Draw.Previews.Count == 0, "First ring preceded yell.");
            f.Engine.NpcYell(1, 999, f.Start.AddSeconds(-.5)); f.Render();
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.5), "Yell clock drift.");
            f.Engine.NpcYell(1, 1, f.Start.AddSeconds(2)); f.Render();
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.5), "Repeat yell shifted ring.");
            using var second = Prepare(0x17B5, new(178, 0, -4));
            Check(second.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == second.Start.AddSeconds(4), "Automatic second ring missing.");
        });
    }

    private static IEnumerable<(string Name, IGeometryShape Native, BossMod.ArenaBoundsCustom Source)> Arenas()
    {
        foreach (var (name, native, type, method) in new (string, IGeometryShape, Type, string)[]
        {
            ("HarpeiaFirst", XelphatolArenas.HarpeiaFirst(0), typeof(Source.D150ChasmHarpeia.D150ChasmHarpeia), "BuildArena1"),
            ("HarpeiaSecond", XelphatolArenas.HarpeiaSecond(0), typeof(Source.D150ChasmHarpeia.D150ChasmHarpeia), "BuildArena2"),
            ("SiegeFirst", XelphatolArenas.SiegeFirst(0), typeof(Source.D150SiegeGobbue.D150SiegeGobbue), "BuildArena1"),
            ("SiegeSecond", XelphatolArenas.SiegeSecond(0), typeof(Source.D150SiegeGobbue.D150SiegeGobbue), "BuildArena2"),
            ("SwiftFirst", XelphatolArenas.SwiftFirst(0), typeof(Source.D150XelphatolSwiftbeak.D150XelphatolSwiftbeak), "BuildArena1"),
            ("SwiftSecond", XelphatolArenas.SwiftSecond(0), typeof(Source.D150XelphatolSwiftbeak.D150XelphatolSwiftbeak), "BuildArena2"),
            ("SwiftThird", XelphatolArenas.SwiftThird(0), typeof(Source.D150XelphatolSwiftbeak.D150XelphatolSwiftbeak), "BuildArena3"),
            ("Skycaller", XelphatolArenas.Skycaller(0), typeof(Source.D150XelphatolSkycaller.D150XelphatolSkycaller), "BuildArena"),
            ("Nuzal", XelphatolArenas.Nuzal(0), typeof(Source.D151NuzalHueloc.D151NuzalHueloc), "BuildArena"),
            ("Tozol", XelphatolArenas.Tozol(0), typeof(Source.D153TozolHuatotl.D153TozolHuatotl), "BuildArena"),
        })
        {
            var tuple = type.GetMethod(method, STATIC)!.Invoke(null, null)!;
            yield return (name, native, (BossMod.ArenaBoundsCustom)tuple.GetType().GetField("Item2")!.GetValue(tuple)!);
        }
        yield return ("DotoliStarting", XelphatolArenas.DotoliStarting(0), new(Dotoli.StartingBoundsP, Dotoli.Difference, AdjustForHitboxInwards: true));
        yield return ("DotoliSmall", XelphatolArenas.DotoliSmall(0), new(Dotoli.DefaultBoundsP, Dotoli.Difference, AdjustForHitboxInwards: true));
        yield return ("DotoliRing", XelphatolArenas.DotoliRing(0), new(Dotoli.StartingBoundsP, Dotoli.DefaultBoundsP));
        var hazardType = typeof(Source.D150SiegeGobbue.D150SiegeGobbue).Assembly.GetType("BossMod.Heavensward.Dungeon.D15Xelphatol.D150SiegeGobbue.ArenaChange")!;
        foreach (var first in new[] { true, false })
        {
            var shape = hazardType.GetField(first ? "poly1" : "poly2", STATIC)!.GetValue(null)!;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var union = ((IEnumerable<BossMod.Shape>)shape.GetType().GetField("Shapes1", flags)!.GetValue(shape)!).ToArray();
            var diff = ((IEnumerable<BossMod.Shape>)shape.GetType().GetField("DifferenceShapes", flags)!.GetValue(shape)!).ToArray();
            yield return (first ? "SiegeHazardFirst" : "SiegeHazardSecond",
                first ? XelphatolArenas.SiegeHazardFirst(0) : XelphatolArenas.SiegeHazardSecond(0), new(union, diff));
        }
    }
    private static void Compare(IGeometryShape native, BossMod.ArenaBoundsCustom source)
    {
        var host = HostDrawingGeometry.Convert(native);
        var bounds = native.GetBounds();
        for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .71f)
            for (var z = bounds.MinZ + .273f; z < bounds.MaxZ; z += .71f)
            {
                var p = new Vector3(x, 0, z);
                var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Polygon mismatch at {p}.");
            }
    }
    private static void Solve(Fight f, Vector3 start)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(start, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No safe route.");
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(path.Count > 1 && path.All(p => f.Engine.Arena!.Contains(p.Position))
            && snapshot.DangerAreas.All(a => !a.Geometry.Contains(path[^1].Position)), "Solver endpoint remains unsafe.");
    }
    private static Vector3 Center(uint boss) => boss switch
    {
        0x179B => new(-74.525f, 0, -70.125f), 0x179F => XelphatolKnockback.CENTER,
        0x17A2 => new(317.8f, 0, -416.19f), 0x17AF => new(-154, 0, 211),
        0x17B5 => new(70.5f, 0, -56), 0x17BA => new(125, 0, -37), 0x17B8 => new(362.745f, 0, -400.815f), _ => Vector3.Zero,
    };
    private static Fight Prepare(uint boss, Vector3? position = null)
    {
        var f = new Fight(boss, 1113); f.Engine.ResetFight();
        var center = position ?? Center(boss);
        f.Actors[1] = new(1, boss, center, InCombat: true);
        f.Actors[100] = new(100, 0, center, PartySlot: 0, Hostile: false);
        Scan(f); return f;
    }
    private static void Shrink(Fight f)
    {
        f.Actors[20] = new(20, 0x1EA187, Center(0x179F), Hostile: false, EventAnimation: 0x00010002); Scan(f);
    }
    private static void Scan(Fight f)
    {
        f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position ?? f.Actors[1].Position, f.Now, playerId: f.PlayerId); f.Render();
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

using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Stormblood.Dungeon.D03BardamsMettle;

internal static class BardamRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (type, factory) in new (Type, Func<float, IGeometryShape>)[]
        {
            (typeof(Source.D031Garula.D031Garula), BardamArenas.Garula),
            (typeof(Source.D032HunterOfBardam.D032HunterOfBardam), BardamArenas.Hunter),
            (typeof(Source.D033Yol.D033Yol), BardamArenas.Yol),
        })
            run($"Bardam exact {type.Name} arena matches source in both views", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var native = factory(0); var host = HostDrawingGeometry.Convert(native); var bounds = native.GetBounds();
                for (var x = bounds.MinX + .123f; x < bounds.MaxX; x += .47f)
                    for (var z = bounds.MinZ + .241f; z < bounds.MaxZ; z += .47f)
                    {
                        var p = new Vector3(x, 0, z); var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(native.Contains(p) == expected && host.Contains(p) == expected, $"Arena differs at {p}.");
                    }
            });
        run("Bardam current client territory claims three bosses without cross-territory ownership", () =>
        {
            foreach (var oid in new uint[] { 0x1A9E, 0x1AA5, 0x1AA6 })
                Check(ArrDungeonCatalog.Owns(oid, 1143) && !ArrDungeonCatalog.Owns(oid, 1367), "Ownership differs.");
        });
        foreach (var (oid, action, point) in new (uint, uint, Vector3)[]
        {
            (0x1A9E, 7927, new(0, 0, 12)), (0x19A, 7955, new(2.9f, 0, 0)),
            (0x1A9F, 9394, new(3.6f, 0, 0)), (0x1AA2, 9395, new(0, 0, 29)),
        })
            run($"Bardam Garula {action} real geometry and cancellation", () =>
            {
                using var f = Prepare(0x1A9E);
                f.Actors[10] = new(10, oid, Center(0x1A9E)); Scan(f); Cast(f, 10, action, 5);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1
                    && f.Draw.Previews.Single().Value.Geometry.Contains(Center(0x1A9E) + point), "AOE size missing.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Cancelled AOE retained.");
            });
        foreach (var oid in new uint[] { 0x1AA1, 0x1AA0 })
            run($"Bardam Garula Yamaa {oid:X} charge locks start and endpoint", () =>
            {
                using var f = Prepare(0x1A9E); var center = Center(0x1A9E);
                f.Actors[10] = new(10, oid, center - Vector3.UnitX * 10); Scan(f);
                Cast(f, 10, 7932, 4.5, location: center + Vector3.UnitX * 10);
                var aoe = f.Draw.Previews.Single().Value.Geometry;
                Check(aoe.Contains(center + Vector3.UnitZ * 3.9f) && !aoe.Contains(center + Vector3.UnitZ * 4.1f)
                    && !aoe.Contains(center + Vector3.UnitX * 10.1f), "Charge dimensions differ.");
                f.Actors[10] = f.Actors[10] with { Position = center }; Scan(f);
                Check(f.Draw.Previews.Single().Value.Geometry.Contains(center - Vector3.UnitX * 9), "Charge snapshot followed caster.");
            });
        run("Bardam Garula tether original clock survives good-bad change and untether clears", () =>
        {
            using var f = Prepare(0x1A9E);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1A9E) + Vector3.UnitX * 10 }; Scan(f);
            Cast(f, 1, 7929, 10, 100);
            f.Engine.Tether(100, 57, true, f.Start, 1); f.Render();
            var distance = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/distance"));
            Check(distance.Activation == f.Start.AddSeconds(10.1) && distance.Shape.Resolve(distance.Position, 0, null).Contains(Center(0x1A9E) + Vector3.UnitX * 22.9f),
                "Tether clock or minimum distance differs.");
            f.Now = f.Start.AddSeconds(1);
            f.Engine.Tether(100, 57, false, f.Now, 1); f.Engine.Tether(100, 1, true, f.Now, 1); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/distance")).Activation == distance.Activation, "Good tether reset clock.");
            f.Engine.Tether(100, 1, false, f.Now, 1); f.Render(); Check(f.Draw.Previews.Count == 0, "Untether retained bait.");
        });
        run("Bardam Garula tether target avoids teammate cone and other viewer avoids actual rectangle", () =>
        {
            using var f = Prepare(0x1A9E);
            f.Actors[101] = new(101, 0, Center(0x1A9E) + Vector3.UnitZ * 10, PartySlot: 1, Hostile: false);
            f.Actors[100] = f.Actors[100] with { Position = Center(0x1A9E) + Vector3.UnitX * 20 }; Scan(f); Cast(f, 1, 7929, 10, 100);
            f.Engine.Tether(100, 57, true, f.Start, 1); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/party/101")), "Self charge did not protect teammates.");
            f.PlayerId = 101; Scan(f);
            Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")) && f.Engine.AmbientAoes().All(a => !a.Id.EndsWith("/distance")),
                "Viewer switch kept personal stretch.");
            f.Engine.Finish(1); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0, "Cancelled charge leaked.");
        });
        run("Bardam trial starts on untargetable primary and bans all hostile targets while retaining navigation", () =>
        {
            using var f = Prepare(0x1AA5);
            f.Actors[1] = f.Actors[1] with { Targetable = false };
            f.Actors[10] = new(10, 0x1F4A, Center(0x1AA5));
            f.Actors[11] = new(11, 0x1AA4, Center(0x1AA5)); Scan(f); Cast(f, 11, 9591, 4);
            Check(f.Engine.Active != null && f.Engine.ForbiddenTargets.Order().SequenceEqual([10UL, 11UL])
                && f.Engine.PriorityTarget(11, Center(0x1AA5)) == 0 && f.Snapshot.DangerAreas.Count == 1, "Trial attack ban blocked movement or omitted adds.");
        });
        run("Bardam HeavyStrike three rings advance only from matching natural helper finish", () =>
        {
            using var f = Prepare(0x1AA5); var center = Center(0x1AA5);
            f.Actors[10] = new(10, 0x18D6, center); Scan(f); Cast(f, 1, 9591, 4);
            var first = f.Engine.AmbientAoes().Single();
            Check(Math.Abs((first.Activation!.Value - f.Start.AddSeconds(5)).TotalMilliseconds) < .001, "Heavy first prediction differs.");
            f.Engine.Finish(1, completed: true); f.Render();
            Check(f.Engine.AmbientAoes().Single().Shape.Resolve(center, 0, null).Contains(center + Vector3.UnitZ * 6), "Visual finish advanced heavy.");
            foreach (var (action, inside, outside) in new (uint, float, float)[] { (9592, 10, 5), (9593, 15, 10) })
            {
                Cast(f, 10, action, 4); f.Now = f.Now.AddSeconds(4); f.Engine.Finish(10, endedAt: f.Now, completed: true); f.Render();
                var aoe = f.Engine.AmbientAoes().Single();
                var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null);
                Check(shape.Contains(center + Vector3.UnitZ * inside) && !shape.Contains(center + Vector3.UnitZ * outside)
                    && aoe.Activation == f.Now.AddSeconds(1.3) && f.Draw.Previews.Count == 1, "Heavy stage or deadline differs.");
            }
            Cast(f, 10, 9594, 4); f.Now = f.Now.AddSeconds(4); f.Engine.Finish(10, completed: true); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Heavy third stage retained.");
        });
        run("Bardam HeavyStrike concurrent origin-rotation matches and cancellation", () =>
        {
            using var f = Prepare(0x1AA5); var center = Center(0x1AA5);
            f.Actors[11] = new(11, 0x1AA4, center + Vector3.UnitX * 8);
            f.Actors[10] = new(10, 0x18D6, center + Vector3.UnitX * 8); Scan(f);
            Cast(f, 1, 9591, 4); Cast(f, 11, 9591, 4, rotation: MathF.PI / 2);
            Cast(f, 10, 9592, 4, rotation: MathF.PI / 2); f.Engine.Finish(10, completed: true); f.Render();
            Check(f.Engine.AmbientAoes().Count(a => a.Shape is FanTemplate) == 1, "Heavy advanced wrong origin.");
            f.Engine.Finish(1); f.Render(); Check(f.Draw.Previews.Count == 1, "Cancelling visual cleared other sequence.");
        });
        run("Bardam HeavyStrike same-frame next cast tolerates timestamp tick rounding", () =>
        {
            using var f = Prepare(0x1AA5); var center = Center(0x1AA5);
            f.Actors[10] = new(10, 0x18D6, center); Scan(f);
            f.Now = f.Start.AddSeconds(.1); Cast(f, 1, 9591, 4);
            f.Now = f.Start.AddSeconds(4.1); f.Actors[1] = f.Actors[1] with { Cast = null }; Scan(f); Cast(f, 10, 9592, 4);
            f.Now = f.Start.AddSeconds(8.1); Cast(f, 10, 9593, 4);
            Check(f.Engine.AmbientAoes().Single().Shape is PtDonutSectorTemplate,
                "Replacing cast at natural finish cancelled the sequence.");
            f.Now = f.Start.AddSeconds(12.1); Cast(f, 10, 9594, 4);
            Check(f.Draw.Previews.Single().Value.Geometry.Contains(center + Vector3.UnitZ * 15), "Same-frame replacement lost outer stage.");
        });
        run("Bardam Tremblor inner outer and standalone final donut", () =>
        {
            using var f = Prepare(0x1AA5); f.Actors[10] = new(10, 0x18D6, Center(0x1AA5)); Scan(f);
            Cast(f, 10, 9596, 4); Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry, "Inner missing.");
            f.Now = f.Start.AddSeconds(4); f.Engine.Finish(10, endedAt: f.Now, completed: true); f.Render();
            var outer = f.Engine.AmbientAoes().Single();
            Check(outer.Shape is DonutTemplate && outer.Activation == f.Now.AddSeconds(1.5), "Outer prediction differs.");
            Cast(f, 10, 9595, 4); Check(f.Draw.Previews.Count == 1, "Final donut duplicated predicted donut.");
            f.Engine.Finish(10, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0, "Outer retained.");
            f.Now = f.Now.AddSeconds(5); Cast(f, 10, 9595, 4); Check(f.Draw.Previews.Single().Value.Geometry is DonutGeometry, "Standalone final hidden.");
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Final cancellation leaked.");
        });
        run("Bardam Comet nine starts advance chase and effects never advance duplicate packets", () =>
        {
            using var f = Prepare(0x1AA5); var center = Center(0x1AA5);
            f.Actors[10] = new(10, 0x18D6, center);
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 15 }; Scan(f); f.Engine.Icon(100, 197);
            for (uint i = 0; i < 9; ++i)
            {
                f.Now = f.Start.AddSeconds(i * 5); var action = i == 0 ? 9597u : 9598u;
                Cast(f, 10, action, i == 0 ? 4 : 1.5);
                var aoe = f.Engine.AmbientAoes().FirstOrDefault(a => a.Id.EndsWith("/display"));
                if (i == 8) Check(aoe == null, "Ninth cast start retained chase.");
                else
                {
                    Check(aoe != null && aoe.Name.Contains($"剩余{8 - i}") && aoe.Activation == f.Now.AddSeconds(1.5)
                        && Vector3.Distance(aoe.Position, center + Vector3.UnitX * 10) < .1, "Chase start clock or distance differs.");
                    var cast = f.Actors[10].Cast!; f.Engine.StartCast(10, cast, f.Now); f.Render();
                    f.Engine.Effect(10, action, i + 1, f.Now); f.Render();
                    Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/display")).Name == aoe!.Name, "Packet/effect advanced chase twice.");
                }
                f.Engine.Finish(10, completed: true);
                f.Actors[10] = f.Actors[10] with { Cast = null };
            }
        });
        run("Bardam towers avoid full towers and real solver enters empty one", () =>
        {
            using var f = Prepare(0x1AA5); var center = Center(0x1AA5);
            f.Actors[10] = new(10, 0x18D6, center + Vector3.UnitX * 8);
            f.Actors[11] = new(11, 0x18D6, center - Vector3.UnitX * 8);
            f.Actors[101] = new(101, 0, f.Actors[10].Position, PartySlot: 1, Hostile: false); Scan(f);
            Cast(f, 10, 7937, 7); Cast(f, 11, 7937, 7);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(center, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && Vector3.Distance(path[^1].Position, f.Actors[11].Position) <= 3, "Tower solver entered occupied tower or ignored soak.");
        });
        run("Bardam ring icons use original clock and collect in another ring center", () =>
        {
            using var f = Prepare(0x1AA5); var center = Center(0x1AA5);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(100, 58, f.Start); f.Engine.Icon(101, 58, f.Start); f.Render();
            var demand = f.Engine.AmbientAoes().Single(a => a.Id == "bardam/ring/stack");
            Check(demand.Activation == f.Start.AddSeconds(3.5)
                && !demand.Shape.Resolve(demand.Position, 0, null).Contains(f.Actors[101].Position) && f.Draw.Previews.Count == 2, "Ring timing/collection missing.");
            f.Engine.Effect(1, 9601, 1, f.Now, 1); f.Render(); Check(f.Draw.Previews.Count == 0, "Self-target ring effect failed to clear.");
        });
        run("Bardam meteor waits for exactly one living star then locks real shadow excluding hitbox", () =>
        {
            using var f = Prepare(0x1AA5); var center = Center(0x1AA5);
            f.Actors[10] = new(10, 0x1F4D, center - Vector3.UnitX * 12);
            f.Actors[11] = new(11, 0x1F4A, center, HitboxRadius: 2.4f);
            f.Actors[12] = new(12, 0x1F4A, center + Vector3.UnitZ * 8); Scan(f); Cast(f, 10, 9602, 30);
            Check(f.Draw.Previews.Count == 0, "Meteor created shelter with two stars.");
            f.Actors[12] = f.Actors[12] with { Dead = true }; Scan(f);
            var safe = f.Draw.Previews.Single().Value.Geometry;
            var danger = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/danger"));
            Check(safe.Contains(center + Vector3.UnitX * 8) && !safe.Contains(center + Vector3.UnitX * 1)
                && safe.Contains(center + Vector3.UnitX * 2.45f), "Meteor shadow includes star or applies extra half-yard exclusion.");
            var host = HostDrawingGeometry.Convert(safe);
            Check(host.Contains(center + Vector3.UnitX * 8) && !host.Contains(center + Vector3.UnitX), "World shadow differs.");
            Check(!danger.Shape.Resolve(danger.Position, 0, null).Contains(center + Vector3.UnitX * 8), "Navigation filled shelter.");
            f.Engine.Finish(10); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Meteor cancel leaked.");
        });
        run("Bardam Yol timeline locks origin rotation clock excludes center and effect clears", () =>
        {
            using var f = Prepare(0x1AA6); var center = new Vector3(24, 0, -475.5f);
            f.Actors[1] = f.Actors[1] with { Position = center + new Vector3(.9f, 0, .9f) }; Scan(f);
            f.Engine.Timeline(1, 0x1E43, f.Start); f.Render(); Check(f.Draw.Previews.Count == 0, "Center exclusion used circle instead of square.");
            f.Actors[1] = f.Actors[1] with { Position = center + Vector3.UnitX * 12, Rotation = -MathF.PI / 2 }; Scan(f);
            f.Engine.Timeline(1, 0x1E43, f.Start); f.Render();
            var aoe = f.Engine.AmbientAoes().Single();
            Check(aoe.Activation == f.Start.AddSeconds(6.7) && aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null).Contains(center), "Squall timeline missing.");
            f.Actors[1] = f.Actors[1] with { Position = center }; Scan(f);
            Check(f.Engine.AmbientAoes().Single().Position == aoe.Position, "Squall followed boss.");
            f.Engine.Effect(1, 7950, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Squall effect did not clear.");
        });
        run("Bardam Yol spread and wingbeat personal avoidance switch and effect cleanup", () =>
        {
            using var f = Prepare(0x1AA6); var center = Center(0x1AA6);
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 8 };
            f.Actors[101] = new(101, 0, center + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f);
            f.Engine.Icon(100, 23, f.Start); f.Engine.Icon(100, 16); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id.Contains("/party/101")) && f.Draw.Previews.Count == 2, "Personal Yol baits missing.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")), "Other-viewer wingbeat missing.");
            f.Engine.Effect(1, 7948, 1, f.Now, 100); f.Engine.Effect(1, 7951, 2, f.Now, 100); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Yol baits retained.");
        });
        foreach (var (boss, action) in new[] { (0x1AA5u, 7940u), (0x1AA6u, 7949u) })
            run($"Bardam {action} PR gaze reaction and reset", () =>
            {
                using var f = Prepare(boss);
                f.Actors[100] = f.Actors[100] with { Position = Center(boss) + Vector3.UnitX * 5 }; Scan(f); Cast(f, 1, action, 5);
                Check(f.Engine.GazeFacing(f.Actors[100].Position, -MathF.PI / 2) == null, "Gaze reacted early.");
                f.Now = f.Start.AddSeconds(4.6); f.Engine.Tick(f.Now);
                Check(f.Engine.GazeFacing(f.Actors[100].Position, -MathF.PI / 2) is { } face && MathF.Sin(face) > 0, "Gaze facing missing.");
                f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Engine.ForbiddenTargets.Length == 0, "Reset leaked controls.");
            });
    }

    private static Vector3 Center(uint boss) => boss switch { 0x1A9E => new(4, 0, 249), 0x1AA5 => new(-28, 0, -14), _ => new(24, 0, -475.5f) };
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1143); f.Engine.ResetFight();
        f.Actors[1] = new(1, boss, Center(boss), InCombat: true);
        f.Actors[100] = new(100, 0, Center(boss), PartySlot: 0, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors.GetValueOrDefault(f.PlayerId)?.Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, ulong target = 0, Vector3? location = null, float rotation = 0)
    {
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

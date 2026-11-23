using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class LateArrTrialRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("LateArrTrial Thornmarch dead primary retains phases until destroyed", () =>
        {
            using var f = new Fight(0x391C, 1067);
            Check(ArrDungeonCatalog.Owns(0x391C, 1067) && !ArrDungeonCatalog.Owns(0x3923, 1067), "Primary ownership differs.");
            f.Engine.MapEffect(8, 0x00020001, f.Now);
            f.Actors[1] = f.Actors[1] with { Dead = true, Targetable = false, InCombat = false };
            f.Actors[2] = new(2, 0x3923, Vector3.Zero, InCombat: true); f.Scan();
            Check(f.Engine.Active != null && Group(f, "thorn/tower").Any(), "First moogle death lost phase/tower.");
            Check(f.Engine.PreserveStageAcrossBattleEnd, "BattleEnded would reset dead-primary phase.");
            f.Cast(2, 29210, 5);
            Check(f.Engine.Casts.Count == 1 && f.Engine.Hints(Vector3.Zero).Contains("全屏伤害"), "Later king casts not handled.");
            f.Engine.RemoveActor(1); f.Render();
            Check(f.Engine.Active == null && f.Snapshot.ArenaBoundary == null && f.Draw.Previews.Count == 0, "Primary destruction leaked phase.");
            Check(!f.Engine.PreserveStageAcrossBattleEnd, "Destroyed primary still preserves phase.");
        });
        foreach (var (action, radius, width) in new[] { (29216u, 6f, 0f), (29215u, 20f, -1f),
            (29213u, 30f, -1f), (29199u, 6f, 0f), (29208u, 50f, 10f), (1873u, 8f, 0f) })
            run($"LateArrTrial {action} exact radar world size clock margin and real finish", () =>
            {
                using var f = new Fight(action == 1873 ? 0xA67u : 0x391Cu, action == 1873 ? 281u : 1067u);
                f.Actors[10] = new(10, 0x233C, Vector3.Zero); f.Scan(); f.Cast(10, action, 4, MathF.PI / 2);
                var castPreview = f.Engine.OutputCasts.Single(c => c.Value.Rule.Action == action);
                var geometry = castPreview.Value.Rule.Shape!.Resolve(Vector3.Zero, castPreview.Value.Rotation, null);
                var point = (width == 0 ? Vector3.UnitZ : Vector3.UnitX) * (radius - .01f);
                Check(geometry.Contains(point) && HostDrawingGeometry.Convert(geometry).Contains(point), "Radar/world geometry differs.");
                if (width > 0) Check(!geometry.Contains(new(1, 0, width / 2 + .01f)), "Rectangle width differs.");
                var danger = f.Snapshot.DangerAreas.Single();
                Check(danger.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3)
                    && danger.DangerWindows.Single().DangerUntil == null, "Cast activation/end differs.");
                f.Now = f.Start.AddSeconds(40); f.Scan();
                Check(f.Engine.Casts.Count == 1, "Timeout removed real active cast.");
                f.Actors[10] = f.Actors[10] with { Cast = null }; f.Scan();
                Check(f.Snapshot.DangerAreas.Count == 0, "Observed finish leaked.");
            });
        run("LateArrTrial Thornmarch GoRound FIFO first two dynamic position third preposition", () =>
        {
            using var f = new Fight(0x391C, 1067);
            f.Actors[10] = new(10, 0x391D, new(-15, 0, 0));
            f.Actors[11] = new(11, 0x3923, new(15, 0, 0));
            f.Actors[12] = new(12, 0x391C, new(0, 0, 15)); f.Scan();
            Start(f, 10, 29197, Vector3.Zero, 9);
            Start(f, 11, 29196, Vector3.Zero, 10);
            Start(f, 12, 29197, Vector3.Zero, 11);
            var rounds = Group(f, "thorn/round");
            Check(rounds.Count(a => a.Display) == 2 && rounds.Count(a => !a.Display) == 2
                && rounds.Where(a => !a.Display).All(a => a.Activation == f.Start.AddSeconds(10.3)), "First two/third timing differs.");
            f.Actors[10] = f.Actors[10] with { Position = new(-12, 0, 3) }; f.Scan();
            Check(Group(f, "thorn/round").First().Position == new Vector3(-12, 0, 3), "Round froze caster position.");
            f.Now = f.Start.AddSeconds(30); f.Scan();
            Check(Group(f, "thorn/round").Length == 4, "Tick consumed round queue.");
            End(f, 10, 29197);
            Check(Group(f, "thorn/round").Count(a => a.Display) == 2
                && Group(f, "thorn/round").All(a => a.Display), "Round FIFO did not slide.");
        });
        run("LateArrTrial Thornmarch tower map states shared activation comet phase and reset", () =>
        {
            using var f = new Fight(0x391C, 1067);
            f.Engine.MapEffect(8, 0x00020001, f.Start);
            f.Now = f.Start.AddSeconds(1); f.Engine.MapEffect(9, 0x00020001, f.Now); f.Render();
            Check(Group(f, "thorn/tower").Where(a => a.Navigate).All(a => a.Activation == f.Start.AddSeconds(9)),
                "Tower activation not refreshed for all active towers.");
            f.Engine.MapEffect(8, 0x00200010, f.Now); f.Render();
            Check(Group(f, "thorn/tower/0/display").Single().Safe, "Soaked color missing.");
            f.Engine.MapEffect(8, 0x00400001, f.Now); f.Render();
            Check(!Group(f, "thorn/tower/0/display").Single().Safe, "Leaving tower did not clear soak.");
            f.Actors[10] = new(10, 0x3923, Vector3.Zero);
            f.Actors[11] = new(11, 0x233C, Vector3.Zero); f.Scan();
            f.Cast(10, 29190, 4); f.Engine.MapEffect(10, 0x00020001, f.Now); f.Render();
            Check(Group(f, "thorn/tower").Where(a => a.Navigate).All(a => a.Shape is CircleTemplate { Radius: 6 }
                && a.Activation == null), "Comet gate should immediately avoid towers.");
            for (var i = 0; i < 3; ++i)
            {
                f.Now = f.Start.AddSeconds(2 + i);
                f.Cast(11, 29199, 3);
                f.Engine.StartCast(11, f.Actors[11].Cast! with { Location = Vector3.UnitX }, f.Now);
                End(f, 11, 29199);
            }
            Check(Group(f, "thorn/tower").Where(a => a.Navigate).All(a => a.Activation == f.Start.AddSeconds(17)),
                "Three distinct starts or packet corrections changed comet/tower clock.");
            f.Engine.MapEffect(8, 0x00080004, f.Now); f.Render();
            Check(!Group(f, "thorn/tower/0").Any(), "Tower deactivation leaked.");
            f.Actors[10] = f.Actors[10] with { Cast = null };
            f.Engine.ResetFight(); f.Scan(); f.Engine.MapEffect(8, 0x00020001, f.Now); f.Render();
            Check(Group(f, "thorn/tower").Where(a => a.Navigate).All(a => a.Activation == f.Now.AddSeconds(8)), "Reset kept delay16.");
        });
        foreach (var (role, index) in new[] { ("H1", 0), ("H2", 1), ("R1", 2), ("R2", 3) })
            run($"LateArrTrial Thornmarch {role} tower assignment opposite fallback and actual default solver", () =>
            {
                using var f = new Fight(0x391C, 1067);
                f.Engine.CurrentAssignment = role;
                f.Engine.MapEffect((uint)(8 + index), 0x00020001, f.Now); f.Render();
                var join = Group(f, "thorn/tower/join").Single();
                var position = new Vector3(MathF.Sin(index * MathF.PI / 4), 0, MathF.Cos(index * MathF.PI / 4)) * 10;
                var geometry = join.Shape.Resolve(join.Position, 0, null);
                Check(!geometry.Contains(position) && geometry.Contains(-position), "Explicit role assigned wrong tower.");
                var snap = f.Snapshot;
                var solution = new SafeZoneSolver().Solve(snap, new(Vector3.Zero, 0, 6, false, false, 0, true, snap.Version),
                    new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
                var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
                Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                    && Vector3.Distance(path[^1].Position, position) < 5, "Default solver did not enter assigned tower.");
                f.Engine.MapEffect((uint)(8 + index), 0x00080004, f.Now);
                f.Engine.MapEffect((uint)(12 + index), 0x00020001, f.Now); f.Render();
                join = Group(f, "thorn/tower/join").Single();
                Check(!join.Shape.Resolve(join.Position, 0, null).Contains(-position), "Inactive first-half tower did not choose opposite.");
                f.Engine.CurrentAssignment = "Unassigned"; f.Render();
                Check(!Group(f, "thorn/tower/join").Any() && Group(f, "thorn/tower").Single(a => a.Navigate).Shape is CircleTemplate { Radius: 5 },
                    "Unassigned used a guessed tower role.");
            });
        run("LateArrTrial Thornmarch concentric rings advance only matching real finish and actual plus2", () =>
        {
            using var f = new Fight(0x391C, 1067);
            for (ulong id = 10; id < 14; ++id) f.Actors[id] = new(id, 0x233C, Vector3.Zero);
            f.Scan(); Start(f, 10, 29201, Vector3.Zero, 5);
            Start(f, 11, 29202, new(5, 0, 0), 5); End(f, 11, 29202);
            Check(Group(f, "thorn/stone").Single().Shape is CircleTemplate { Radius: 10 }, "Wrong order/location advanced sequence.");
            f.Now = f.Start.AddSeconds(20); f.Scan();
            Check(Group(f, "thorn/stone").Single().Shape is CircleTemplate, "Time advanced ring.");
            End(f, 10, 29201);
            Check(Group(f, "thorn/stone").Single() is { Shape: DonutTemplate { InnerRadius: 10, OuterRadius: 20 }, Activation: var at }
                && at == f.Now.AddSeconds(2), "First real finish timing differs.");
            Start(f, 12, 29202, new(.9f, 20, .9f), 5); End(f, 12, 29202);
            Check(Group(f, "thorn/stone").Single().Shape is DonutTemplate { InnerRadius: 20, OuterRadius: 30 }, "Planar tolerance blocked matching helper.");
            Start(f, 13, 29619, Vector3.Zero, 5); End(f, 13, 29619);
            Check(!Group(f, "thorn/stone").Any(), "Third finish leaked sequence.");
        });
        run("LateArrTrial Thornmarch snapshot packet correction moves initial stone without duplicating", () =>
        {
            using var f = new Fight(0x391C, 1067);
            f.Actors[10] = new(10, 0x233C, Vector3.Zero); f.Scan();
            var cast = new DungeonCastObservation(29201, Vector3.Zero, 0, f.Now, f.Now.AddSeconds(5.3));
            f.Actors[10] = f.Actors[10] with { Cast = cast };
            f.Engine.StartCast(10, cast, f.Now, false);
            var corrected = cast with { Location = Vector3.UnitX * 5 };
            f.Actors[10] = f.Actors[10] with { Cast = corrected };
            f.Engine.StartCast(10, corrected, f.Now); f.Render();
            Check(Group(f, "thorn/stone").Single().Position.X == 5, "Packet left snapshot stone at old origin.");
            End(f, 10, 29201);
            Check(Group(f, "thorn/stone").Single().Shape is DonutTemplate { InnerRadius: 10 }, "Corrected cast finish did not advance sequence.");
        });
        run("LateArrTrial Thornmarch independent spread strict eight stack shared tankbuster and perspective", () =>
        {
            using var f = new Fight(0x391C, 1067);
            for (var i = 0; i < 8; ++i)
                f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, new(i == 7 ? 10 : 0, 0, 0), PartySlot: i,
                    PartyRole: i < 2 ? "Tank" : "Healer", Hostile: false);
            f.Actors[10] = new(10, 0x233C, Vector3.Zero);
            f.Actors[11] = new(11, 0x233C, Vector3.Zero); f.Actors[12] = new(12, 0x233C, Vector3.Zero); f.Scan();
            Start(f, 10, 29204, Vector3.Zero, 6, 100);
            Start(f, 11, 29192, Vector3.Zero, 6, 102);
            Check(!f.Engine.PredictedDamage.Any(), "Ordinary stack/spread added unsupported raidwide predictions.");
            Check(f.Engine.Hints(Vector3.Zero).Contains("八人分摊") && Group(f, "thorn/spread").Any(a => a.Navigate)
                && Group(f, "thorn/stack").Any(a => a.Navigate), "Independent party outputs missing.");
            f.Actors[107] = f.Actors[107] with { Position = Vector3.Zero }; f.Scan();
            Check(!f.Engine.Hints(Vector3.Zero).Contains("八人分摊"), "Eight players did not satisfy strict stack.");
            End(f, 11, 29192);
            Check(!Group(f, "thorn/spread").Any() && Group(f, "thorn/stack").Any(), "Spread finish consumed stack.");
            Start(f, 12, 29206, Vector3.Zero, 5, 100);
            Check(Group(f, "thorn/tank").Count(a => a.Navigate) == 6
                && f.Engine.PredictedDamage.Count(p => p.DamageType == "Tankbuster") == 2, "Marked tank did not avoid non-tanks/predict both tanks.");
            f.PlayerId = 101; f.Scan();
            Check(Group(f, "thorn/tank/join").Any() && Group(f, "thorn/tank/display").Single() is { Safe: true, OutlineOnly: true },
                "Other tank join/display missing.");
            f.PlayerId = 102; f.Scan();
            Check(Group(f, "thorn/tank/avoid").Any() && !Group(f, "thorn/tank/display").Single().OutlineOnly, "Non-tank did not avoid TB.");
            End(f, 12, 29206); End(f, 10, 29204);
            Check(!Group(f, "thorn/tank").Any() && !Group(f, "thorn/stack").Any(), "Party finish leaked.");
        });
        foreach (var (boss, territory, action, oid, radius, delay) in new[]
        {
            (0x391Cu, 1067u, 29207u, 0x1E8F67u, 8f, .8),
            (0xA67u, 281u, 1864u, 0x1E9230u, 5f, 0d),
            (0xA67u, 281u, 1865u, 0x1E9231u, 5f, 0d),
        })
            run($"LateArrTrial {action} voidzone dynamic target event live merge state7 no timeout", () =>
            {
                using var f = new Fight(boss, territory);
                f.Actors[10] = new(10, 0x233C, Vector3.Zero);
                f.Actors[100] = new(100, 0, Vector3.UnitX * 10, PartySlot: 0, Hostile: false); f.Scan();
                Start(f, 10, action, Vector3.Zero, 5, 100);
                Check(Group(f, "trial/voidzone").Single().Position.X == 10, "Voidzone did not follow target.");
                f.Actors[100] = f.Actors[100] with { Position = Vector3.UnitZ * 10 }; f.Scan();
                Check(Group(f, "trial/voidzone").Single().Position.Z == 10, "Moving target not followed.");
                f.Engine.Effect(10, action, 1, f.Now, 100, Vector3.Zero); End(f, 10, action);
                Check(Group(f, "trial/voidzone").Single().Activation == f.Now.AddSeconds(delay), "Event spawn clock differs.");
                f.Now = f.Now.AddSeconds(100); f.Scan();
                Check(Group(f, "trial/voidzone").Length == 1, "Event prediction timed out.");
                f.Actors[11] = new(11, oid, new(1, 0, 10), Dead: true, EventState: 0); f.Scan();
                Check(Group(f, "trial/voidzone").Single() is { Shape: CircleTemplate { Radius: var r }, Activation: null } && r == radius,
                    "Live source failed to consume prediction or incorrectly filtered death.");
                f.Actors[11] = f.Actors[11] with { EventState = 7 }; f.Scan();
                Check(!Group(f, "trial/voidzone").Any(), "State7 poison persisted.");
            });
        run("LateArrTrial Whorleater exact arena body independent damage and directional knockback clocks", () =>
        {
            using var f = new Fight(0xA67, 281);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false); f.Scan();
            Check(f.Engine.Arena!.Contains(new(14.49f, 0, 19.49f))
                && !f.Engine.Arena.Contains(new(14.51f, 0, 0)) && !f.Engine.Arena.Contains(new(0, 0, 19.51f)), "Arena dimensions differ.");
            f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -25) }; f.Scan();
            Check(!Group(f, "whorl/body").Any(), "First nonzero baseline triggered body.");
            f.Now = f.Start.AddSeconds(1);
            f.Actors[1] = f.Actors[1] with { Position = new(0, 0, 25), Rotation = MathF.PI }; f.Scan();
            var body = Group(f, "whorl/body/aoe").Single();
            Check(body.Activation == f.Now.AddSeconds(2.6) && body.Position.Z == 25 && body.Rotation == MathF.PI,
                "Body snapshot timing/rotation differs.");
            var landing = Group(f, "whorl/body/knockback/landing").Single();
            Check(landing.Position.Z > 19.4f && landing.Position.Z < 19.51f && !landing.Navigate, "Directional KB did not stop at wall or added hard navigation.");
            f.Engine.Effect(1, 1860, 1, f.Now); f.Render();
            Check(!Group(f, "whorl/body/aoe").Any() && Group(f, "whorl/body/knockback").Any(), "Damage effect cleared KB.");
            f.Engine.Effect(1, 1938, 2, f.Now); f.Render();
            Check(!Group(f, "whorl/body").Any(), "KB effect leaked preview.");
            f.Actors[1] = f.Actors[1] with { Position = Vector3.Zero }; f.Scan();
            f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -25) }; f.Scan();
            Check(Group(f, "whorl/body/knockback/landing").Single().Position.Z < -19.4f, "Zero crossing reset baseline or reversed direction.");
        });
        run("LateArrTrial Whorleater dive created radial knockback shape gate poison unsafe immunity and separate ends", () =>
        {
            using var f = new Fight(0xA67, 281);
            f.Actors[100] = new(100, 0, new(4, 0, 5), PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0xA87, Vector3.Zero); f.Scan();
            var aoe = Group(f, "whorl/dive/aoe").Single();
            var landing = Group(f, "whorl/dive/knockback/landing").Single();
            Check(aoe.Activation == f.Start.AddSeconds(.6) && landing.Position.X > 10 && landing.Position.Z > 12
                && !landing.Navigate, "Dive KB became directional or added hard navigation.");
            f.Actors[11] = new(11, 0x1E9231, landing.Position, Dead: true); f.Scan();
            Check(!Group(f, "whorl/dive/knockback/landing").Single().Safe
                && f.Engine.Hints(Vector3.Zero).Contains("避开击退后的毒区落点"), "Poison landing did not warn.");
            f.Engine.Effect(10, 1869, 1, f.Now); f.Render();
            Check(!Group(f, "whorl/dive/aoe").Any() && Group(f, "whorl/dive/knockback").Any(), "Snapshot consumed KB.");
            f.Actors[100] = f.Actors[100] with { Position = new(9, 0, 5) }; f.Scan();
            Check(!Group(f, "whorl/dive/knockback").Any(), "Outside dive shape got KB.");
            f.Actors[100] = f.Actors[100] with { Position = new(4, 0, 5), Statuses = [160],
                StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(1.39) } }; f.Scan();
            Check(Group(f, "whorl/dive/knockback").Any(), "Expiring immunity blocked activation after expiry.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(1.41) } }; f.Scan();
            Check(!Group(f, "whorl/dive/knockback").Any(), "Activation-valid immunity failed.");
            f.Engine.Effect(10, 1861, 2, f.Now); f.Actors[100] = f.Actors[100] with { Statuses = [] }; f.Scan();
            Check(!Group(f, "whorl/dive").Any(), "Dive effect leaked KB.");
        });
        run("LateArrTrial Whorleater creation clock late snapshot resolved-before-observed and reused source", () =>
        {
            using var f = new Fight(0xA67, 281);
            f.Engine.ObjectCreated(10, f.Start);
            f.Now = f.Start.AddSeconds(.2);
            f.Actors[100] = new(100, 0, new(4, 0, 5), PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0xA87, new(.045777064f, 0, .045777064f)); f.Scan();
            var dive = Group(f, "whorl/dive/aoe").Single();
            const float grid = 2000f / 65535f;
            Check(dive.Position.X == 1.5f * grid && dive.Position.Z == 1.5f * grid, "Half-grid quantization differs.");
            Check(dive.Activation == f.Start.AddSeconds(.6), "Late object snapshot shifted creation clock.");
            f.Engine.Effect(10, 1869, 1, f.Now); f.Render();
            f.Engine.ObjectCreated(11, f.Now);
            f.Now = f.Start.AddSeconds(.3);
            f.Engine.Effect(11, 1869, 2, f.Now);
            f.Now = f.Start.AddSeconds(.4);
            f.Actors[11] = new(11, 0xA87, Vector3.Zero); f.Scan();
            Check(!Group(f, "whorl/dive/aoe").Any() && Group(f, "whorl/dive/knockback").Any(),
                "Late snapshot rearmed resolved damage or lost independent knockback.");
            f.Engine.Effect(11, 1861, 3, f.Now);
            f.Engine.RemoveActor(11); f.Actors.Remove(11);
            f.Now = f.Start.AddSeconds(2); f.Engine.ObjectCreated(11, f.Now);
            f.Actors[11] = new(11, 0xA87, Vector3.Zero); f.Scan();
            Check(Group(f, "whorl/dive/aoe").Single().Activation == f.Now.AddSeconds(.6), "Reused ID kept old resolved state.");
        });
        run("LateArrTrial Whorleater snapshot-first birth correction and immunity snapshot handoff", () =>
        {
            using var f = new Fight(0xA67, 281);
            f.Now = f.Start.AddSeconds(.2);
            f.Actors[100] = new(100, 0, new(4, 0, 5), PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0xA87, Vector3.Zero); f.Scan();
            f.Engine.ObjectCreated(10, f.Start); f.Render();
            Check(Group(f, "whorl/dive/aoe").Single().Activation == f.Start.AddSeconds(.6), "Snapshot-first clock not corrected.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 160, Duration = 5, Timestamp = f.Now }, f.Now);
            f.Render(); Check(!Group(f, "whorl/dive/knockback").Any(), "Event immunity missing.");
            f.Now = f.Start.AddSeconds(.71); f.Scan();
            Check(Group(f, "whorl/dive/knockback").Any(), "Expired event override did not yield to absent snapshot.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(5) } }; f.Scan();
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 160, Timestamp = f.Now }, f.Now);
            f.Render(); Check(Group(f, "whorl/dive/knockback").Any(), "Removal did not override stale snapshot.");
            f.Now = f.Start.AddSeconds(1.22); f.Scan();
            Check(!Group(f, "whorl/dive/knockback").Any(), "Override failed to yield to current snapshot.");
        });
        foreach (var hitbox in new[] { .5f, 1.5f })
            run($"LateArrTrial Whorleater hitbox {hitbox} exact wall inset independent projections and immunity event", () =>
            {
                using var f = new Fight(0xA67, 281);
                f.Actors[100] = new(100, 0, new(2, 0, 5), PartySlot: 0, Hostile: false, HitboxRadius: hitbox);
                f.Actors[1] = f.Actors[1] with { Position = new(0, 0, -25) }; f.Scan();
                f.Actors[1] = f.Actors[1] with { Position = new(0, 0, 25) };
                f.Actors[10] = new(10, 0xA87, Vector3.Zero); f.Scan();
                var body = Group(f, "whorl/body/knockback/landing").Single();
                Check(MathF.Abs(body.Position.Z - (19.5f - (hitbox - .499f))) < .0001f, "Wall inset differs from hitbox contract.");
                var dive = Group(f, "whorl/dive/knockback/landing").Single();
                Check(dive.Position.Z < body.Position.Z && dive.Position.X > 2, "Independent dive chained from body landing.");
                f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 160, Duration = 2, Timestamp = f.Now }, f.Now);
                f.Render();
                Check(Group(f, "whorl/body/knockback").Any() && !Group(f, "whorl/dive/knockback").Any()
                    && Group(f, "whorl/dive/aoe").Any(), "Event immunity did not independently use activation time.");
                f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 160, Timestamp = f.Now }, f.Now);
                f.Render();
                Check(Group(f, "whorl/dive/knockback").Any(), "Immediate immunity removal left stale preview.");
                f.Now = f.Start.AddSeconds(10); f.Scan();
                Check(Group(f, "whorl/body/knockback").Any() && Group(f, "whorl/dive/knockback").Any(), "Time consumed KB state.");
                f.Engine.Effect(1, 1937, 10, f.Now); f.Render();
                Check(!Group(f, "whorl/body/knockback").Any() && Group(f, "whorl/dive/knockback").Any()
                    && Group(f, "whorl/body/aoe").Any(), "Body KB end consumed damage/dive.");
            });
        foreach (var (job, status, expected) in new[]
        {
            (24u, false, 1UL), (42u, false, 1UL), (23u, false, 2UL), (19u, false, 2UL),
            (30u, false, 2UL), (36u, false, 1UL), (36u, true, 2UL),
        })
            run($"LateArrTrial Whorleater class {job} tankmimic {status} target priorities and reflection hints", () =>
            {
                using var f = new Fight(0xA67, 281);
                f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false, ClassJob: job,
                    Statuses: status ? [2124u] : [], TargetId: job == 23 ? 1UL : 2UL);
                f.Actors[2] = new(2, 0xA86, Vector3.Zero, Statuses: [477]); f.Scan();
                Check(f.Engine.PriorityTarget(2, Vector3.Zero) == expected && !f.Engine.RequireAllStop
                    && f.Engine.ForbiddenTargets.Length == 0, "Class target/stop differs.");
                if (expected == 1) Check(f.Engine.Hints(Vector3.Zero).Any(h => h.Contains("攻击头部")), "Reflection target hint missing.");
                if (job == 23) Check(f.Engine.Hints(Vector3.Zero).Any(h => h.Contains("攻击尾部")), "Physical reflection hint missing.");
                f.Actors[3] = new(3, 0xA85, Vector3.Zero); f.Actors[4] = new(4, 0xA84, Vector3.Zero);
                f.Actors[5] = new(5, 0xA83, Vector3.Zero); f.Scan();
                Check(f.Engine.PriorityTarget(2, Vector3.Zero) == 5, "Fish priority4 missing.");
                f.Actors[5] = f.Actors[5] with { Dead = true }; f.Scan();
                Check(f.Engine.PriorityTarget(2, Vector3.Zero) == 3, "Spume priority3 missing.");
                Check(Group(f, "whorl/enemy/3").Single().Color == new Vector4(1, 0, 1, 1), "Spume vulnerable color differs.");
                f.Engine.StatusChanged(2, 775, true, f.Now);
                Check(!f.Engine.Hints(Vector3.Zero).Any(h => h.Contains("反射")), "Invincible tail gave reflection hint.");
            });
    }

    private static DungeonAmbientAoe[] Group(Fight f, string prefix)
        => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    private static void Start(Fight f, ulong source, uint action, Vector3 location, double seconds, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, location, 0, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast };
        f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source, uint action)
    {
        f.Engine.Finish(source, action, f.Now, true);
        f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class DoomtrainRegression
{
    private static uint sequence;
    internal static void Run(Action<string, Action> run)
    {
        run("Doomtrain identity ownership and cast-start waits for actual cast without 1.9 second timeout", () =>
        {
            using var f = Scene();
            Check(ArrDungeonCatalog.IsScript("masterplayer.trial.doomtrain") && ArrDungeonCatalog.Owns(0x4A30, 1307)
                && !ArrDungeonCatalog.Owns(0x4A34, 1307) && !ArrDungeonCatalog.Owns(0x4A30, 1200), "Ownership.");
            At(f, 100); Check(Stage(f) == 0 && f.Engine.Arena!.Position.Z == 100, "CastStart incorrectly timed out.");
            Start(f, 1, 45660); Check(Stage(f) == 1, "Source CastStart must accept unexpected current cast.");
        });
        run("Doomtrain state counters consume effects while finish waits for tolerance timeout", () =>
        {
            using var f = Scene(); Start(f, 1, 45623); Start(f, 10, 45624);
            End(f, 10); End(f, 1);
            At(f, 6.899); Check(Stage(f) == 1, "Finish advanced effect counter.");
            At(f, 6.9); Check(Stage(f) == 2, "Component condition expected +1 timeout.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(Stage(f) == 4 && f.Engine.Arena!.Position.Z == 150, "Timeout0/targetability chaining.");
        });
        run("Doomtrain three car transitions use actual effects and reset component instances", () =>
        {
            using var f = Scene(); NextCar(f); Check(Stage(f) == 4, "Car2.");
            Check(!f.Engine.Arena!.Contains(new(102.5f, 0, 147.5f)) && f.Engine.Arena.Contains(new(92, 0, 150)), "Car2 crate subtraction.");
            NextCar(f); Check(Stage(f) == 8 && f.Engine.Arena!.Position.Z == 200, "Car3.");
            NextCar(f); Check(Stage(f) == 12 && f.Engine.Arena!.Position.Z == 250, "Car4.");
            Check(f.Engine.Arena.Contains(new(100, 0, 264.5f)) && !f.Engine.Arena.Contains(new(100, 0, 264.7f)), "Car4 14.6 half-length.");
        });
        run("Doomtrain levin birth uses locked height length and offset but moving actor position", () =>
        {
            using var f = Scene();
            f.Actors[20] = new(20, 0x4A31, new(94, 0, 85)); f.Actors[21] = new(21, 0x4A31, new(106, 3, 85)); f.Scan();
            Check(Group(f, "levin").Single().Shape is RayRectangleTemplate { Length: 30 }, "Initial ground/culled air.");
            NextCar(f); NextCar(f);
            var before = Group(f, "levin").ToArray();
            Check(before.Any(a => a.Shape is RayRectangleTemplate { Length: 10 }) && before.Any(a => a.Shape is RayRectangleTemplate { Length: 20 })
                && before.Any(a => a.Position.Z == 95), "Car3 replay length/air offset.");
            f.Actors[21] = f.Actors[21] with { Position = new(106, 0, 190) }; f.Scan();
            Check(Group(f, "levin").Any(a => a.Position.Z == 200 && a.Shape is RayRectangleTemplate { Length: 20 }), "Height reclassified after birth or position froze.");
            Start(f, 20, 45619); End(f, 20);
            Check(Group(f, "levin").Count() == 2, "Finish cleared effect-owned prediction.");
            Hit(f, 20, 45621); Check(Group(f, "levin").Count() == 1, "Actual beam did not clear source.");
        });
        run("Doomtrain levin reactivation refreshes prediction clock and car2 strict X intervals", () =>
        {
            using var f = Scene(); f.Actors[20] = new(20, 0x4A31, new(97, 0, 85)); f.Actors[21] = new(21, 0x4A31, new(100, 0, 85)); f.Scan();
            At(f, 2); NextCar(f);
            Check(Group(f, "levin").All(a => Time(a.Activation, f.Now.AddSeconds(7)))
                && Group(f, "levin").Any(a => a.Shape is RayRectangleTemplate { Length: 20 })
                && Group(f, "levin").Any(a => a.Shape is RayRectangleTemplate { Length: 30 }), "Reactivation clock/strict interval.");
        });
        run("Doomtrain car3 platform obstacles switch at Y4 and directed portals publish exact coordinates", () =>
        {
            using var f = Scene(); NextCar(f); NextCar(f);
            Check(f.Engine.NativeTeleporters.Count == 2 && f.Engine.NativeTeleporters.All(p => !p.Bidirectional && p.Radius == 1)
                && f.Engine.NativeTeleporters[0].Entrance == new Vector3(96.1f, 0, 204.9f)
                && f.Engine.NativeTeleporters[0].Exit == new Vector3(92.4f, 0, 204.9f), "Portal contracts.");
            Check(Group(f, "obstacle").Count() == 4 && Group(f, "obstacle").All(a => a.Obstacle && !a.Display), "Ground obstacles.");
            Check(Group(f, "platforms").Single().OutlineOnly && Group(f, "portal").Count() == 2, "Dual-view platform/entrance.");
            f.Actors[100] = f.Actors[100] with { Position = new(92.4f, 4, 204.9f) }; f.Scan();
            Check(Group(f, "obstacle").Count() == 2 && Group(f, "obstacle").All(a => a.Shape is CircleTemplate { Radius: 1.5f }), "Upper obstacles threshold.");
        });
        run("Doomtrain head-on is immediate ungated by height and clears all on either finish", () =>
        {
            using var f = Scene(); NextCar(f); NextCar(f); Start(f, 10, 45635); Start(f, 11, 45637);
            var head = Group(f, "HeadOn"); Check(head.Length == 2 && head.All(a => a.Activation == null && a.Navigate), "HeadOn delayed or clipped by height.");
            Check(head.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(new(100, 0, 200)))
                && head.Any(a => a.Shape.Resolve(a.Position, 0, null).Contains(new(92.5f, 0, 205))), "Ground/air topology.");
            Hit(f, 10, 45635); Check(Group(f, "HeadOn").Length == 2, "Effect cleared cast-owned head-on.");
            End(f, 11); Check(Group(f, "HeadOn").Length == 0, "Either finish must clear all.");
        });
        run("Doomtrain car4 retained car3 topology has identical AI and drawing origins", () =>
        {
            using var f = Scene(); NextCar(f); NextCar(f); NextCar(f); Start(f, 10, 45637);
            var aoe = Group(f, "HeadOn").Single(); var geometry = aoe.Shape.Resolve(aoe.Position, 0, null);
            Check(geometry.Contains(new(92.5f, 0, 255)) && !geometry.Contains(new(92.5f, 0, 205))
                && f.Draw.Previews.Any(p => p.Key.StartsWith("doom/HeadOn") && p.Value.Geometry.Contains(new(92.5f, 0, 255))), "History-dependent origin cache retained.");
        });
        run("Doomtrain intermission arena retains disconnected rail ring with inward hitbox offset", () =>
        {
            using var f = Scene(); Intermission(f);
            var arena = f.Engine.Arena!;
            Check(Stage(f) == 15 && arena.Position.X == -400 && arena.Contains(new(-400, 0, -400))
                && !arena.Contains(new(-385.8f, 0, -400)) && !arena.Contains(new(-380, 0, -400))
                && arena.Contains(new(-375, 0, -400)) && !arena.Contains(new(-372.8f, 0, -400)), "Union/offset.");
            Check(arena.GetContours().Count >= 3 && arena is PolygonRegionGeometry, "Ring hole was lost.");
            At(f, 1000); Check(Stage(f) == 15, "70000-second state replaced with inferred delay.");
        });
        run("Doomtrain phase transition precedes intermission timeout and checks first aether death only", () =>
        {
            using var f = Scene(); Intermission(f);
            f.Actors[30] = new(30, 0x4A34, new(-400, 0, -400)); f.Actors[31] = new(31, 0x4A34, new(-400, 0, -400), Dead: true); f.Scan();
            Check(Stage(f) == 15, "Any dead aether incorrectly advanced phase.");
            f.Actors.Remove(30); f.Actors.Remove(31); f.Scan(); Check(Stage(f) == 15, "Missing aether counted as death.");
            f.Actors[30] = new(30, 0x4A34, new(-400, 0, -400), Dead: true); f.Scan();
            Check(Stage(f) == 20, "Phase completion blocked by timeout.");
            Hit(f, 10, 45645); Check(Stage(f) == 20, "RunawayTrainRaidwide substituted for watched45638.");
            Hit(f, 1, 45638); Check(Stage(f) == 23 && f.Engine.Arena!.Position.Z == 250, "New P2 counter/targetability chaining.");
        });
        run("Doomtrain phase2 runaways do not inherit previous hit and timeout at16.2", () =>
        {
            using var f = Scene(); Intermission(f); Phase2(f);
            Check(Stage(f) == 20 && !Group(f, "levin").Any(), "Phase cleared geometry component or reused counters.");
            var entered = f.Now; f.Now = entered.AddSeconds(16.199); f.Engine.Tick(f.Now); Check(Stage(f) == 20, "Early P2 timeout.");
            f.Now = entered.AddSeconds(16.2); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.Arena!.Position.Z == 250, "P2 tolerance timeout.");
        });
        run("Doomtrain P2 returns car4 before derail finish and car5 terminal preserves active components", () =>
        {
            using var f = Scene(); ReturnCar4(f); Start(f, 10, 45654, duration: 10);
            Check(f.Engine.Arena!.Position.Z == 250 && Group(f, "Derail").Single().Shape is DonutTemplate { InnerRadius: 2, OuterRadius: 32 }
                && Time(Group(f, "Derail").Single().Activation, f.Now.AddSeconds(10.9)), "Derail early transition/timing.");
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(Stage(f) == 23 && f.Engine.Arena.Position.Z == 250, "Car5 state must not change geometry itself.");
            End(f, 10); f.Engine.Tick(f.Now); f.Render();
            Check(f.Engine.Arena.Position.Z == 300 && !Group(f, "Derail").Any() && f.Engine.NativeTeleporters[0].Entrance.Z == 310
                && f.Engine.NativeTeleporters[1].Entrance.Z == 300, "Derail finish/car5 portals.");
            f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan();
            At(f, 200); Check(Stage(f) == 23 && Group(f, "platforms").Any(), "Terminal state exited.");
        });
        run("Doomtrain car5 deck shapes and levin asymmetric offsets match both platforms", () =>
        {
            using var f = Scene(); Car5(f);
            f.Actors[20] = new(20, 0x4A31, new(94, 3, 285)); f.Actors[21] = new(21, 0x4A31, new(106, 3, 285)); f.Scan();
            Check(Group(f, "levin").Any(a => a.Position.Z == 305 && a.Shape is RayRectangleTemplate { Length: 10 })
                && Group(f, "levin").Any(a => a.Position.Z == 295 && a.Shape is RayRectangleTemplate { Length: 10 }), "Asymmetric offsets.");
            Start(f, 10, 45637); var shape = Group(f, "HeadOn").Single().Shape.Resolve(default, 0, null);
            Check(shape.Contains(new(92.4f, 0, 310)) && shape.Contains(new(107.6f, 0, 300))
                && !shape.Contains(new(107.6f, 0, 310)), "Car5 platforms.");
        });
        run("Doomtrain windpipe shared draw-in is slot independent and safewalls extrude plusZ with strict far edge", () =>
        {
            using var f = Scene(); NextCar(f); Start(f, 10, 45667);
            f.PlayerId = 107; f.Scan();
            var aoe = Group(f, "windpipe").Single(); var shape = aoe.Shape.Resolve(default, 0, null);
            Check(Time(aoe.Activation, f.Now) && !shape.Contains(new(102, 0, 155)) && !shape.Contains(new(97, 0, 165))
                && !shape.Contains(new(100.1f, 0, 150.1f)) && shape.Contains(new(102, 0, 170.1f))
                && shape.Contains(new(102, 0, 149)) && shape.Contains(new(99, 0, 155)), "Safe wall union/direction/strict20.");
            Check(!Group(f, "windpipe").Any(a => a.Display), "Source has no draw-in arrow.");
            End(f, 10); Check(!Group(f, "windpipe").Any(), "Windpipe finish.");
        });
        run("Doomtrain windpipe uses current immunity rather than activation and gain loss packets precede raw", () =>
        {
            using var f = Scene(); NextCar(f); Start(f, 10, 45667, duration: 7);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 160, Duration = 1, Timestamp = f.Now }, f.Now);
            Check(!Group(f, "windpipe").Any(), "Current immunity insufficient for future cast was ignored.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 160, Timestamp = f.Now }, f.Now);
            Check(Group(f, "windpipe").Any(), "Loss did not restore current windpipe.");
        });
        run("Doomtrain car5 windpipe wall and inactive car1 drawin do not invent geometry", () =>
        {
            using var f = Scene(); Start(f, 10, 45667); Check(!Group(f, "windpipe").Any(), "Car1 draw-in should have no safewalls.");
            using var p = Scene(); Car5(p); Start(p, 10, 45667);
            var shape = Group(p, "windpipe").Single().Shape.Resolve(default, 0, null);
            Check(!shape.Contains(new(107, 0, 310)) && shape.Contains(new(102, 0, 310)), "Car5 single wall.");
        });
        run("Doomtrain ordinary rays remain displayed before risky threshold and upper finish clears batch", () =>
        {
            using var f = Scene(); Car5(f); Start(f, 10, 45627, duration: 7);
            Check(Group(f, "cast").Single().Display && !Group(f, "cast").Single().Navigate, "Blastpipe threshold.");
            f.Now = f.Now.AddSeconds(4.3); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "cast").Single().Navigate, "Risky at3.");
            Start(f, 11, 45630); Start(f, 12, 45630);
            Check(Group(f, "Upper").Length == 2 && Group(f, "Upper").All(a => a.Shape is RayRectangleTemplate { Length: 10 }), "Upper shape/batch.");
            End(f, 12); Check(Group(f, "Upper").Length == 0, "Upper any finish clears all.");
        });
        run("Doomtrain phase activation gates simple casts and surge actual R15 cone", () =>
        {
            using var f = Scene(); Start(f, 10, 45659); Check(Group(f, "cast").Length == 0, "Inactive Hail leaked.");
            using var p = Scene(); Intermission(p); Start(p, 10, 45643, duration: 7);
            var cone = Group(p, "cast").Single(); Check(cone.Shape is FanTemplate { Radius: 15 } fan && MathF.Abs(fan.AngleRadians - MathF.PI / 4) < .0001f
                && !cone.Navigate, "Surge range/angle/threshold.");
            p.Now = p.Now.AddSeconds(1.3); p.Engine.Tick(p.Now); p.Render(); Check(Group(p, "cast").Single().Navigate, "Surge six seconds.");
        });
        run("Doomtrain lightning express uses fixed forward16 landing and strict immunity expiry", () =>
        {
            using var f = Scene(); f.Actors[100] = f.Actors[100] with { Position = new(100, 0, 100) }; f.Scan();
            Start(f, 1, 45618, rotation: MathF.PI / 2);
            var ai = Group(f, "lightning").Single(a => a.Navigate);
            Check(ai.Shape.Resolve(default, 0, null).Contains(new(100, 0, 100))
                && !ai.Shape.Resolve(default, 0, null).Contains(new(90, 0, 100))
                && Group(f, "lightning").Any(a => a.Id.EndsWith("/landing") && a.Unclipped), "Forward landing/trajectory.");
            f.Engine.StatusChanged(100, 160, true, f.Now, expiresAt: f.Now.AddSeconds(5.3));
            Check(Group(f, "lightning").Any(), "Equal expiry incorrectly immune.");
            f.Engine.StatusChanged(100, 160, true, f.Now, expiresAt: f.Now.AddSeconds(6));
            Check(!Group(f, "lightning").Any(), "Immunity packet not applied.");
        });
        run("Doomtrain tank icons prefer target and consume FIFO on effect only", () =>
        {
            using var f = Scene(); f.Engine.Icon(101, 343, f.Now, 100); f.Engine.Icon(102, 343, f.Now, 100); f.Render();
            Check(Group(f, "tank").Count(a => a.Id.EndsWith("/display")) == 2
                && f.Engine.PredictedDamage.Count(p => p.DamageType == "Tankbuster") == 2, "Tank target/prediction.");
            Start(f, 10, 45661); End(f, 10); Check(Group(f, "tank").Count(a => a.Id.EndsWith("/display")) == 2, "Finish consumed icon.");
            Hit(f, 10, 45661); Check(Group(f, "tank").Count(a => a.Id.EndsWith("/display")) == 1, "Effect FIFO.");
            f.Actors[102] = f.Actors[102] with { Dead = true }; f.Scan(); Check(!Group(f, "tank").Any(), "Dead target permanent removal.");
        });
        run("Doomtrain ray icon follows target source status replacement allows dead targets and only actual ray clears", () =>
        {
            using var f = Scene(); Intermission(f);
            f.Actors[20] = new(20, 0x4B80, new(-400, 0, -425)); f.Actors[21] = new(21, 0x4B80, new(-425, 0, -400)); f.Scan();
            f.Engine.Icon(100, 642, f.Now, 20); f.Render();
            Check(Group(f, "ray").Single(a => a.Id.EndsWith("/display")).OutlineOnly
                && Group(f, "ray").Where(a => a.Id.Contains("/buddy/")).All(a => a.Shape is FanTemplate { Radius: 100 }), "Own cone/buddy directions.");
            f.Engine.StatusChanged(21, 4541, true, f.Now, 0x578); f.Render();
            Check(Group(f, "ray").First().Position.X == -400, "Unused Distance status inferred rotation.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 21, StatusId = 4176, Timestamp = f.Now }, f.Now); f.Render();
            Check(Group(f, "ray").First().Position.X == -425, "Stop did not replace source.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan(); Check(Group(f, "ray").Any(), "Dead target not supported.");
            f.Actors[21] = f.Actors[21] with { Dead = true }; f.Scan(); Check(!Group(f, "ray").Any(), "Dead source not hidden.");
            f.Actors[21] = f.Actors[21] with { Dead = false }; f.Scan(); Check(Group(f, "ray").Any(), "Dead source incorrectly consumed bait.");
            Hit(f, 20, 45640); Check(!Group(f, "ray").Any(), "Ray effect.");
        });
        run("Doomtrain arcane indicators track moving dead targets until all disappear", () =>
        {
            using var f = Scene(); ReturnCar4(f);
            f.Actors[20] = new(20, 0x4A36, new(100, 0, 250)); f.Actors[21] = new(21, 0x4A36, new(100, 0, 255)); f.Scan();
            f.Actors[20] = f.Actors[20] with { Position = new(105, 0, 250), Dead = true }; f.Scan();
            Check(Group(f, "arcane").Count(a => a.Display) == 2 && Group(f, "arcane").Any(a => a.Position.X == 105), "Individual dead target removed or froze.");
            f.Actors.Remove(21); f.Scan(); Check(!Group(f, "arcane").Any(), "All dead/removed did not clear.");
        });
        run("Doomtrain circle stacks min8 unlimited max independent real finish and source noShared quirk", () =>
        {
            using var f = Scene(); ReturnCar4(f);
            f.Actors[101] = f.Actors[101] with { Position = new(120, 0, 100) }; f.Scan();
            Start(f, 10, 45649, target: 100); Start(f, 11, 45649, target: 101);
            Check(Group(f, "stack").Count(a => a.Display) == 2 && Group(f, "stack").Any(a => a.Id.EndsWith("/buddy"))
                && !f.Engine.PredictedDamage.Any(p => p.DamageType == "Shared")
                && f.Engine.Hints(f.Actors[100].Position).Contains("八人分摊：人数不足"), "Stack AI/minimum/source prediction.");
            Hit(f, 10, 45649); Check(Group(f, "stack").Count(a => a.Display) == 2, "Effect consumed cast stack.");
            End(f, 11); Check(Group(f, "stack").Count(a => a.Display) == 1, "Independent stack finish.");
        });
        run("Doomtrain raidwide predictions require casts and do not synthesize nocast effects", () =>
        {
            using var f = Scene(); Intermission(f);
            Hit(f, 10, 45645); Hit(f, 11, 45647); Check(!f.Engine.PredictedDamage.Any(), "No-cast effects fabricated predictions.");
            Start(f, 10, 45645); Start(f, 11, 45647); Check(f.Engine.PredictedDamage.Count() == 16, "Raidwide cast lifecycle.");
            End(f, 10); Check(f.Engine.PredictedDamage.Count() == 8, "Independent raidwide finish.");
        });
        run("Doomtrain death wipe owner cleanup and missing primary P1 semantics", () =>
        {
            using var f = Scene(); NextCar(f); NextCar(f); f.Actors.Remove(1); f.Scan();
            Check(f.Engine.Active != null && f.Engine.NativeTeleporters.Count == 2, "P1 missing primary incorrectly ended phase.");
            f.Engine.DirectorUpdate(0x40000005); f.Scan();
            Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && f.Engine.NativeTeleporters.Count == 0, "Wipe cleanup.");
            f.Actors[1] = new(1, 0x4A30, new(100, 0, 100), InCombat: true); f.Scan(); Check(f.Engine.Active == null, "Wipe resurrected.");
            f.Engine.DirectorUpdate(0x40000011); f.Scan(); Check(Stage(f) == 0 && f.Engine.Arena!.Position.Z == 100, "Recommence.");
            using var p = Scene(); Intermission(p); Phase2(p); p.Actors[1] = p.Actors[1] with { Hp = 0 }; p.Scan();
            Check(p.Engine.Active == null && p.Draw.Previews.Count == 0, "P2 HP0 cleanup.");
        });
        run("Doomtrain actual solver dodges delayed short ray within car2 crate arena", () =>
        {
            using var f = Scene(); NextCar(f);
            f.Actors[100] = f.Actors[100] with { Position = new(92, 0, 150) }; f.Actors[10] = f.Actors[10] with { Position = new(92, 0, 140) }; f.Scan();
            Start(f, 10, 45633, duration: 6);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot,
                new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && path.All(p => f.Engine.Arena!.Contains(p.Position))
                && !Group(f, "cast").Single().Shape.Resolve(f.Actors[10].Position, 0, null).Contains(path[^1].Position), "Solver ray/obstacle path.");
        });
        run("Doomtrain actor destruction ends head-on upper drawin and derail without timeout", () =>
        {
            using var f = Scene(); NextCar(f); Start(f, 10, 45667); f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Render();
            Check(!Group(f, "windpipe").Any(), "Destroy draw-in caster.");
            f.Actors[10] = new(10, 0x233C, new(100, 0, 150)); f.Scan();
            NextCar(f); Start(f, 11, 45635); f.Engine.RemoveActor(11); f.Actors.Remove(11); f.Render();
            Check(!Group(f, "HeadOn").Any(), "Destroy HeadOn caster.");
            using var p = Scene(); ReturnCar4(p); Start(p, 10, 45654); p.Engine.RemoveActor(10); p.Actors.Remove(10); p.Render();
            Check(p.Engine.Arena!.Position.Z == 300 && !Group(p, "Derail").Any(), "Destroy Derail caster must finish Car5 transition.");
            p.Actors[1] = p.Actors[1] with { Targetable = false }; p.Scan(); Start(p, 11, 45630);
            p.Actors.Remove(11); p.Scan(); Check(!Group(p, "Upper").Any(), "Raw missing caster Upper finish.");
        });
        run("Doomtrain same-cast target correction restarts whole batches while rotation-only correction preserves consumption", () =>
        {
            using var f = Scene(); NextCar(f); NextCar(f); Start(f, 10, 45635); Start(f, 11, 45637);
            var cast10 = f.Actors[10].Cast!;
            f.Engine.StartCast(10, cast10 with { TargetId = 100 }, f.Now); f.Render();
            Check(Group(f, "HeadOn").Length == 1, "Target correction did not finish old whole batch.");
            f.Engine.StartCast(11, f.Actors[11].Cast! with { TargetId = 101 }, f.Now); f.Render();
            Check(Group(f, "HeadOn").Length == 1, "Consumed peer key blocked replacement target cast.");
            f.Engine.StartCast(10, cast10 with { TargetId = 100, Location = new(105, 0, 200) }, f.Now); f.Render();
            Check(Group(f, "HeadOn").Length == 1, "Position-only packet revived consumed peer.");
        });
        run("Doomtrain stacked marks use dangerous peer outline and overlap warning even with eight inside", () =>
        {
            using var f = Scene(); ReturnCar4(f);
            for (ulong i = 100; i < 108; ++i) f.Actors[i] = f.Actors[i] with { Position = new(100, 0, 250) };
            f.Scan(); Start(f, 10, 45649, target: 100); Start(f, 11, 45649, target: 101);
            Check(Group(f, "stack").Count(a => a.Display && a.Safe) == 1
                && Group(f, "stack").Count(a => a.Display && !a.Safe) == 1
                && f.Engine.Hints(f.Actors[100].Position).Contains("八人分摊：避免重叠其他点名")
                && !f.Engine.Hints(f.Actors[100].Position).Contains("八人分摊：保持集合"), "Peer color/overlap.");
        });
    }

    private static Fight Scene()
    {
        var f = new Fight(0x4A30, 1307); var c = new Vector3(100, 0, 100);
        f.Actors[1] = f.Actors[1] with { Position = c, Hp = 100 };
        for (var i = 0; i < 8; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * i,
            PartySlot: i, Hostile: false);
        for (ulong i = 10; i < 20; ++i) f.Actors[i] = new(i, 0x233C, c);
        f.Scan(); return f;
    }
    private static void At(Fight f, double seconds) { f.Now = f.Start.AddSeconds(seconds); f.Scan(); }
    private static void Start(Fight f, ulong source, uint action, double duration = 5, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void End(Fight f, ulong source)
    { f.Engine.Finish(source); f.Actors[source] = f.Actors[source] with { Cast = null }; f.Render(); }
    private static void Hit(Fight f, ulong source, uint action)
    { f.Engine.Effect(source, action, ++sequence, f.Now); f.Render(); }
    private static void NextCar(Fight f)
    {
        f.Now = f.Now.AddSeconds(1);
        f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan();
        Start(f, 1, 45623); Start(f, 10, 45624); Hit(f, 10, 45624); End(f, 1); End(f, 10);
        f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
    }
    private static void Intermission(Fight f)
    {
        NextCar(f); NextCar(f); NextCar(f); f.Now = f.Now.AddSeconds(1);
        f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan();
        Start(f, 1, 45638); Hit(f, 1, 45638); End(f, 1);
        f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
    }
    private static void Phase2(Fight f)
    { f.Actors[30] = new(30, 0x4A34, new(-400, 0, -400), Dead: true); f.Scan(); }
    private static void ReturnCar4(Fight f)
    {
        Intermission(f); Phase2(f); f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan();
        Hit(f, 1, 45638);
    }
    private static void Car5(Fight f)
    {
        ReturnCar4(f); f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
        Start(f, 10, 45654); End(f, 10); f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan();
    }
    private static int Stage(Fight f) => (int)typeof(DungeonEncounter).GetField("doomStage", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.Engine)!;
    private static DungeonAmbientAoe[] Group(Fight f, string prefix) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("doom/" + prefix)).ToArray();
    private static bool Time(DateTime? a, DateTime b) => a != null && Math.Abs((a.Value - b).Ticks) < 10;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

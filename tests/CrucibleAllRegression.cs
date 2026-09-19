using System.Numerics;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Mechanics.Crucible.Components;
using MasterPlayer.Dodge;
using MasterPlayer.Scripting;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class CrucibleAllRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("Crucible radar display follows the owning encounter and clears with its navigation boundary", () =>
        {
            using var f = new Fight(14693, new(520,-420));
            f.Render();
            var navigation = f.Snapshot.ArenaBoundary!;
            var displayed = f.Output.GetRadarArena(navigation);
            Check(displayed is { } arena && arena.Center == new Vector3(520,0,-420)
                && arena.Boundary.Contains(new Vector3(534.9f,0,-427.5f))
                && !navigation.Contains(new Vector3(534.9f,0,-427.5f)), "radar still used the navigation-inset floor");
            var foreign = new PromeRotation.Spatial.Geometry.CircleGeometry(Vector3.Zero,12);
            Check(f.Output.GetRadarArena(foreign) == null, "stale encounter display replaced another owner's map");
            f.Output.Apply(f.Engine,f.Frame,0,true,true,false);
            Check(f.Output.GetRadarArena(navigation) == null, "disabling boundaries left the display active");
            f.Render();
            Check(f.Output.GetRadarArena(f.Snapshot.ArenaBoundary) != null, "reenabling did not restore display");
            f.Output.Clear();
            Check(f.Output.GetRadarArena(navigation) == null && f.Output.GetRadarArena(null) == null, "stop retained the old arena shape");
        });
        run("sampled safe regions draw only exposed edges, including hole boundaries", () =>
        {
            var grid = FrozenConstraintGeometry.Create(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Z)) <= 1 ? 1 : -1,
                new(0,0), 0, radius: 3, step: 1, visible: true);
            foreach (var (a, b) in grid.GetBoundarySegments())
            {
                var middle = (a + b) * 0.5f;
                var delta = b - a;
                var normal = Vector3.Normalize(new Vector3(-delta.Z, 0, delta.X)) * 0.01f;
                Check(grid.Contains(middle + normal) != grid.Contains(middle - normal), "internal grid seam was outlined");
            }
            Check(grid.GetBoundarySegments().Any(s => Math.Abs(s.Start.X) <= 1 && Math.Abs(s.Start.Z) <= 1), "inner hole rim lost");
        });
        run("B33 PR escapes fungal poison while an attack-position goal is active", () =>
        {
            using var f = new Fight(14633, new(120, 0));
            f.Boss.HitboxRadius = 3;
            f.Player.Position = new(120, 4);
            f.Spawn(19679, new(120, 6));
            f.Render();
            var solution = Solve(f);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2 && solution.TargetPosition is { } target
                && !f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(target)), $"fungal escape failed: {solution.Status}, path={solution.ExecutablePath.Count}, target={solution.TargetPosition}");
        });
        run("B33 PR escapes glyph lines and crosses with the boss goal active", () =>
        {
            foreach (var action in new uint[] { 49203, 49204, 49207, 50939 })
            {
                using var f = new Fight(14633, new(120, 0));
                f.Boss.HitboxRadius = 3;
                f.Player.Position = new(120, 2);
                var origin = action is 49203 or 49204 ? new WPos(120, -50) : new WPos(120, 0);
                f.Cast(action, 5, origin, caster: f.Spawn(9020, new(120, 0)));
                f.Render();
                var solution = Solve(f);
                Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2 && solution.TargetPosition is { } target
                    && !f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(target)), $"glyph {action} escape failed: {solution.Status}, path={solution.ExecutablePath.Count}, target={solution.TargetPosition}");
            }
        });
        run("B33 PR separates from a water-marked companion despite the attack goal", () =>
        {
            using var f = new Fight(14633, new(120, 0));
            f.Boss.HitboxRadius = 3;
            f.Player.Position = new(120, 3);
            var pet = f.Spawn(18916, new(122, 3)); pet.OwnerID = f.Player.InstanceID;
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player, 135, f.Player.InstanceID));
            f.Render();
            var solution = Solve(f);
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2 && solution.TargetPosition is { } target
                && !f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(target)), $"water separation failed: {solution.Status}, path={solution.ExecutablePath.Count}, target={solution.TargetPosition}");
        });
        run("Crucible explicit last-moment cancellation clears predicted followups", () =>
        {
            using var f = new Fight(14555);
            f.Engine.Dispatch(c => c.OnEventIcon(f.Boss, 167, f.Boss.InstanceID));
            f.Cast(48210, 4, f.Boss.Position);
            f.World.CurrentTime = f.World.FutureTime(4.1);
            var adapter = new CrucibleEventAdapter(f.Engine, f.World.Actors.Find, _ => 0, () => { }, _ => { });
            adapter.Handle(new LogSystemCancelCastEvent { SourceId = 1, ActionId = 48210 });
            Check(!f.Aoes.Any(a => a.Shape is AOEShapeCone), "last-moment cancel retained spin predictions");
        });
        run("PR solver reaches the imported dragon bait point with its frozen constraints", () =>
        {
            using var f = new Fight(14628, new(920, -430));
            f.Player.Position = new(920, -420);
            f.Engine.Dispatch(c => c.OnTethered(f.Boss, new(17, f.Player.InstanceID))); f.Render();
            var solution = Solve(f);
            Check(solution.IsSuccess && solution.TargetPosition is { } target && f.Snapshot.GoalAreas.Single().Geometry.Contains(target), $"PR did not reach the ported precise goal: {solution.Status}, target={solution.TargetPosition}, goal={f.Snapshot.GoalAreas.Single().Geometry.Position}, dangers={f.Snapshot.DangerAreas.Count}");
            Check(solution.ExecutablePath.All(p => f.Snapshot.ArenaBoundary!.Contains(p.Position)), "bait path left the field");
        });
        run("PR solver traverses Lauda's joined platform without stepping on fire", () =>
        {
            using var f = new Fight(14693, new(520, -420));
            f.Player.Position = new(530, -427.5f);
            var frame = f.Frame;
            frame.Hints.GoalPoints.Add((new(532.5f, -427.5f), 0.2f, 100));
            f.Output.Apply(f.Engine, frame, 0, true, true, true);
            var solution = Solve(f);
            Check(solution.IsSuccess && solution.TargetPosition is { } target && Vector3.Distance(target, new(532.5f,0,-427.5f)) < 0.21f, "Lauda ledge unreachable");
            Check(solution.ExecutablePath.All(p => CrucibleLaudaFloor.Contains(WPos.FromVec3(p.Position))), "ledge route crosses fire");
        });
        run("Crucible adapter subtracts event delay, locks location, and reports missing rotation", () =>
        {
            using var f = new Fight(14631);
            var errors = new List<string>();
            float? rotation = 0.75f;
            var adapter = new CrucibleEventAdapter(f.Engine, f.World.Actors.Find, _ => rotation, () => { }, errors.Add);
            var cast = new LogSystemCastStartEvent { ActionId = 49193, SourceId = 1, CastTime = 6, Timestamp = f.World.CurrentTime.AddSeconds(-2), EffectPosition = new(130, 0, -425), SourcePosition = f.Boss.Position.ToVec3(), SourceRotation = -1 };
            adapter.Handle(cast);
            var aoe = f.Aoes.Single(a => a.Shape is AOEShapeRect { LengthFront: 50 });
            Check(Math.Abs((aoe.Activation - f.World.CurrentTime).TotalSeconds - 4.3) < 0.001, "dispatch delay/NPC correction wrong");
            Check(aoe.Rotation.Rad == rotation && aoe.Origin == new WPos(130, -425), "cast snapshot changed");
            adapter.Handle(new LogSystemInterruptCastEvent { SourceId = 1, ActionId = 49193 });
            rotation = null; adapter.Handle(cast);
            Check(errors.Count == 1 && !f.Aoes.Any(a => a.Shape is AOEShapeRect { LengthFront: 50 }), "unlocked cast accepted or interrupt leaked");
        });
        run("Crucible multi-target effects preserve all targets and advance only once", () =>
        {
            using var f = new Fight(14693, new(520, -420));
            var recorder = new EventRecorder(f.Engine.Module!);
            f.Engine.Module!.Components = [..f.Engine.Module.Components, recorder];
            var adapter = new CrucibleEventAdapter(f.Engine, f.World.Actors.Find, _ => 0, () => { }, _ => { });
            var effect = new LogSystemActionEffectEvent { SourceId = 1, ActionId = 49489, GlobalSequence = 42, TargetId = 2, TargetIndex = 1 };
            adapter.Handle(effect); adapter.Handle(effect with { TargetIndex = 0, TargetId = 3 }); adapter.Flush();
            Check(recorder.Casts.Count == 1 && recorder.Casts[0].Targets.Length == 2 && recorder.Casts[0].MainTargetID == 3, "target expansion lost or advanced repeatedly");
            adapter.Handle(effect); adapter.Flush(); Check(recorder.Casts.Count == 1, "repeated packet advanced again");
            adapter.Handle(effect with { GlobalSequence = 43 }); adapter.Clear(); adapter.Flush();
            Check(recorder.Casts.Count == 1, "reset retained queued effects");
        });
        run("Crucible adapter keeps map word order and cancels only the matching tether", () =>
        {
            using var f = new Fight(14564, new(120, 0));
            var adapter = new CrucibleEventAdapter(f.Engine, f.World.Actors.Find, _ => 0, () => { }, _ => { });
            adapter.Handle(new LogSystemMapEffectEvent { Position = 0, Param1 = 4, Param2 = 8 });
            Check(f.Aoes.Count == 0, "map words reversed");
            adapter.Handle(new LogSystemTetherEvent { SourceId = 1, TargetId = 2, TetherId = 17 });
            Check(f.Boss.Tether.Target == 2 && f.Boss.Tether.ID == 17, "tether not tracked");
            adapter.Handle(new LogSystemTetherCancelEvent { SourceId = 1 });
            Check(f.Boss.Tether == default, "tether not cleared");
        });
        run("Crucible adapter uses the packet animation word order and status deadlines", () =>
        {
            using var f = new Fight(14663);
            var ice = f.Spawn(2015490, f.Boss.Position + new WDir(12, 0), type: ActorType.EventObj);
            var adapter = new CrucibleEventAdapter(f.Engine, f.World.Actors.Find, _ => 0, () => { }, _ => { });
            adapter.Handle(new LogSystemEObjAnimationEvent { SourceId = ice.InstanceID, Param1 = 0x10, Param2 = 0x20 });
            f.Tick(6);
            Check(f.Aoes.Any(a => a.ActorID == ice.InstanceID && a.Shape is AOEShapeCircle { Radius: >=12 and <13 }), "animation words reversed");
            adapter.Handle(new LogSystemStatusAddEvent { TargetId = 2, StatusId = 5341, Param = 2, Duration = 10, Timestamp = f.World.CurrentTime.AddSeconds(-2) });
            Check(f.Player.FindStatus(5341)?.ExpireAt == f.World.FutureTime(8), "status delayed twice");
            adapter.Handle(new LogSystemStatusRemoveEvent { TargetId = 2, StatusId = 5341 });
            Check(f.Player.FindStatus(5341) == null, "status removal lost");
        });
        run("Crucible duplicate cast packets do not restart component state", () =>
        {
            using var f = new Fight(14631);
            var recorder = new EventRecorder(f.Engine.Module!);
            f.Engine.Module!.Components = [..f.Engine.Module.Components, recorder];
            f.Cast(49193, 4, f.Boss.Position); f.Cast(49193, 4.1f, f.Boss.Position);
            Check(recorder.Starts == 1, "duplicate cast restarted a component");
        });
        run("Crucible moving bombs remain preview-only and clear on detonation", () =>
        {
            using var f = new Fight(14624);
            var bomb = f.Spawn(19668, f.Player.Position + new WDir(9, 0)); f.Tick();
            Check(!f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(bomb.Position.ToVec3())), "moving bomb still forces kiting");
            Check(f.Engine.Module!.FindComponent<CrucibleMovingBombs>()!.ActiveAOEs(0, f.Player).ToArray().Any(a => a.ActorID == bomb.InstanceID && !a.Risky), "bomb preview missing");
            f.Engine.Effect(bomb, new() { Action = new(48792), GlobalSequence = 55 }); f.Tick();
            Check(!f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(bomb.Position.ToVec3())), "detonated bomb persisted");
        });
        run("Crucible geometry snapshots keep configured margins and freeze mutable constraints", () =>
        {
            var template = new ResolvedGeometryTemplate(new PromeRotation.Spatial.Geometry.CircleGeometry(Vector3.Zero, 6));
            var inflated = DangerMarginInflator.Inflate(template, 0.5f).Resolve(Vector3.Zero, 0, null);
            Check(inflated.Contains(new(6.4f, 0, 0)) && !inflated.Contains(new(6.6f, 0, 0)), "configured margin was lost");
            var origin = new WPos(0,0);
            var frozen = FrozenConstraintGeometry.Create(p => (p - origin).Length() - 3, origin, 0, 5);
            origin = new(20,20);
            Check(frozen.Contains(Vector3.Zero) && !frozen.Contains(origin.ToVec3()), "solver captured live state");
        });
        run("Lauda native polygon preserves platform seams and rejects a path through fire", () =>
        {
            var shape = CrucibleLaudaFloor.Safe.Geometry(CrucibleLaudaFloor.Center, 0);
            Check(shape.GetContours().Count == 1 && shape.GetBounds().Width < 31, "floor became disconnected strips or an oversized square");
            Check(shape.Contains(new(530, 0, -427.5f)), "platform seam blocked");
            Check(!shape.Contains(new(532.5f, 0, -437.5f)), "fire counted as floor");
        });
        uint[] names = [14531,14533,14535,14536,14538,14541,14545,14546,14549,14552,14555,14557,14561,14564,14569,14572,14577,14581,14583,14587,14592,14596,14599,14603,14606,14608,14612,14617,14618,14624,14628,14631,14633,14641,14642,14651,14657,14660,14663,14665,14670,14675,14684,14688,14693];
        for (var index = 0; index < names.Length; ++index)
        {
            var group = index + 1; var name = names[index];
            run($"all Crucible B{group:00} activates, publishes and clears its arena", () =>
            {
                using var f = new Fight(name);
                Check(f.Engine.Module?.Group == group, "wrong group selection");
                f.Render();
                Check(f.Snapshot.ArenaBoundary != null, "missing arena");
                f.Engine.Destroy(f.Boss, true); f.Tick();
                Check(f.Engine.Module == null && f.Snapshot.ArenaBoundary == null && f.Snapshot.DangerAreas.Count == 0, "group teardown leaked areas");
            });
        }
        run("ported shapes agree with PR at rotated and rear rectangle points", () =>
        {
            AOEShape[] shapes = [new AOEShapeCircle(6), new AOEShapeDonut(5, 18), new AOEShapeCone(25, 120f.Degrees()), new AOEShapeRect(12, 4, 3), new AOEShapeCross(15, 3)];
            var origin = new WPos(120, -420); var rotation = 73f.Degrees();
            foreach (var shape in shapes)
            {
                var native = shape.Geometry(origin, rotation, 0);
                for (float x = -26.13f; x < 26; x += 1.13f)
                    for (float z = -26.23f; z < 26; z += 1.17f)
                    {
                        var p = origin + new WDir(x, z);
                        Check(shape.Check(p, origin, rotation) == native.Contains(p.ToVec3()), $"shape conversion mismatch: {shape}, {p}");
                    }
            }
        });
        run("B03 cross is a cross and raidwide flare never forbids the arena", () =>
        {
            using var f = new Fight(14535);
            f.Cast(46892, 3, f.Boss.Position);
            var cross = f.Aoes.Single(a => a.Shape is AOEShapeCross);
            Check(cross.Check(cross.Origin + new WDir(20, 0)) && !cross.Check(cross.Origin + new WDir(10, 10)), "incorrect cross");
            f.Cast(46895, 6, f.Boss.Position);
            Check(!f.Frame.Hints.ForbiddenZones.Any(z => z.Shape.Distance(f.Boss.Position + new WDir(60, 0)) < 0), "flare is not an avoidable 100y disk");
        });
        run("all imported fallback shapes include locked direction and NPC correction", () =>
        {
            Check(AoeShapeDatabase.TryGetShape(49207, out var cross) && cross!.ExactTemplate != null, "missing high2 cross fallback");
            Check(AoeShapeDatabase.TryGetShape(49451, out var line) && line is { Param1: 20, Param2: 80, FollowCaster: false, UseCastRotation: true, ExtraDelaySeconds: 0.3f }, "Lauda dimensions wrong");
            Check(!AoeShapeDatabase.TryGetShape(49332, out _), "unavoidable poison raidwide must not become a whole-room AOE");
        });
        run("location casts stay fixed when the source actor moves and cancel immediately", () =>
        {
            using var f = new Fight(14631);
            var location = f.Boss.Position + new WDir(8, 3);
            f.Cast(49193, 5, location, 90f.Degrees());
            f.Boss.Position += new WDir(12, 0); f.Tick();
            Check(f.Aoes.Single(a => a.Shape is AOEShapeRect).Origin == location, "cast followed caster");
            f.Engine.FinishCast(f.Boss, 49193); f.Render();
            Check(!f.Aoes.Any(a => a.Shape is AOEShapeRect { LengthFront: 50 }), "cancel leaked cast");
        });
        run("B05 lava persists after the hit and disappears at event state 7", () =>
        {
            using var f = new Fight(14538);
            var pool = f.Spawn(0x1EC025, f.Boss.Position + new WDir(7, 0), type: ActorType.EventObj);
            f.Tick(20);
            Check(f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(pool.Position.ToVec3()) && a.DangerWindows.Any(w => w.DangerUntil == null)), "pool was turned into an expired hit");
            Check(f.Snapshot.HardObstacles.Count == 0, "damage pools must not be obstacles");
            pool.EventState = 7; f.Tick();
            Check(!f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(pool.Position.ToVec3())), "inactive pool leaked");
        });
        run("B04 and B29 sequence windows reach PR together without becoming persistent", () =>
        {
            foreach (var (name, start, count) in new[] { (14536u, 46902u, 4), (14618u, 48771u, 5) })
            {
                using var f = new Fight(name);
                for (var i = 0; i < count; ++i) f.Cast(start + (uint)i, 3 + i * 2, f.Boss.Position, caster: f.Spawn(9020, f.Boss.Position));
                f.Render();
                var windows = f.Snapshot.DangerAreas.SelectMany(a => a.DangerWindows).Where(w => w.DangerUntil != null).Select(w => w.DangerFrom).Distinct().ToArray();
                Check(windows.Length >= count, $"future sequence waves missing for {name}");
            }
        });
        run("B11 rotation icon predicts next swings, duplicate effects advance once, cancel clears", () =>
        {
            using var f = new Fight(14555);
            f.Engine.Dispatch(c => c.OnEventIcon(f.Boss, 167, f.Boss.InstanceID));
            f.Cast(48210, 4, f.Boss.Position);
            var component = f.Engine.Module!.FindComponent<CrucibleMinotaurRotation>()!;
            Check(component.ActiveAOEs(0, f.Player).Length == 3, "missing rotating previews");
            f.Effect(48210, 17);
            var rotation = component.ActiveAOEs(0, f.Player)[0].Rotation;
            f.Effect(48210, 17);
            Check(component.ActiveAOEs(0, f.Player)[0].Rotation == rotation, "duplicated effect advanced twice");
            f.Cast(48212, 4, f.Boss.Position); f.Engine.FinishCast(f.Boss, 48212);
            Check(component.ActiveAOEs(0, f.Player).Length == 0, "canceled rotating cast leaked predictions");
        });
        run("third-board map effects use reversed indices and switch off only their own fence", () =>
        {
            using var f = new Fight(14564, new(120, 0));
            Check(f.Aoes.Count(a => a.Shape is AOEShapeRect) == 4, "missing square fence");
            f.Engine.Dispatch(c => c.OnMapEffect(1, 0x00080004));
            Check(f.Aoes.Count(a => a.Shape is AOEShapeRect) == 4, "wrong index removed square fence");
            f.Engine.Dispatch(c => c.OnMapEffect(0, 0x00080004));
            Check(f.Aoes.Count == 0, "correct index did not remove square fence");
        });
        run("B31 rectangle bait uses real matched pools and replaces old precise goals", () =>
        {
            using var f = new Fight(14628, new(920, -430));
            f.Engine.Dispatch(c => c.OnTethered(f.Boss, new(17, f.Player.InstanceID)));
            f.Render();
            var p1 = f.Snapshot.GoalAreas.Single().Geometry.Position;
            var offset = MathF.Sqrt(18.3f * 18.3f - 49);
            Check(Vector3.Distance(p1, new(913, 0, -420 - offset + 7)) < 0.01f, "route is not the latest BMR rectangle");
            f.Player.Position = WPos.FromVec3(p1);
            f.Spawn(0x1EB704, new(925, -419), type: ActorType.EventObj); f.Tick();
            Check(f.Snapshot.GoalAreas.Single().Geometry.Position == p1, "unrelated pool advanced route");
            var oldHandle = f.Snapshot.GoalAreas.Single().Handle;
            f.Spawn(0x1EB704, f.Player.Position, type: ActorType.EventObj); f.Tick();
            var p2 = f.Snapshot.GoalAreas.Single().Geometry.Position;
            Check(Math.Abs(Vector3.Distance(p1, p2) - 7) < 0.01f, "wrong rectangular route depth");
            Check(f.Snapshot.GoalAreas.Count == 1 && !f.SafeZone.Manager.ContainsArea(oldHandle), "old bait goal retained");
            f.Tick(); Check(f.Snapshot.GoalAreas.Single().Geometry.Position == p2, "same pool counted again");
        });
        run("B31 final pool releases routing and is immediately a persistent danger", () =>
        {
            using var f = new Fight(14628, new(920, -430));
            f.Engine.Dispatch(c => c.OnTethered(f.Boss, new(17, f.Player.InstanceID)));
            for (var i = 0; i < 4; ++i)
            {
                f.Render(); f.Player.Position = WPos.FromVec3(f.Snapshot.GoalAreas.Single().Geometry.Position);
                f.Spawn(0x1EB704, f.Player.Position, type: ActorType.EventObj); f.Tick();
            }
            Check(f.Snapshot.GoalAreas.Count == 0, "fourth pool did not release bait");
            Check(f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(f.Player.Position.ToVec3()) && a.DangerWindows.Any(w => w.DangerUntil == null)), "last pool was not activated immediately");
        });
        run("B31 outer landing holds center preview out of navigation until the outer hit", () =>
        {
            using var f = new Fight(14628, new(920, -420));
            f.Cast(48816, 4, f.Boss.Position); f.Cast(48828, 5.5f, f.Boss.Position, caster: f.Spawn(9020, f.Boss.Position));
            Check(!f.Frame.Hints.ForbiddenZones.Any(z => z.Shape is SDCircle { Radius: 6 }), "center activated too soon");
            f.Effect(48816, 18);
            Check(f.Frame.Hints.ForbiddenZones.Any(z => z.Shape is SDCircle { Radius: 6 }), "center not activated after outer hit");
        });
        run("B33 player water bait never chases its own circle and separates the companion", () =>
        {
            using var f = new Fight(14633);
            var pet = f.Spawn(18916, f.Player.Position + new WDir(2, 0)); pet.OwnerID = f.Player.InstanceID;
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player, 135, f.Player.InstanceID));
            var zones = f.Frame.Hints.ForbiddenZones.Select(z => z.Shape).OfType<SDCircle>().ToArray();
            Check(zones.Any(z => z.Origin == pet.Position && z.Radius == 8.5f), "no companion separation");
            Check(!zones.Any(z => z.Origin == f.Player.Position), "self-chasing water zone");
        });
        run("B39 expanding ice preserves its age and sleep state removes it", () =>
        {
            using var f = new Fight(14663);
            var ice = f.Spawn(2015490, f.Boss.Position + new WDir(12, 0), type: ActorType.EventObj);
            f.Engine.Dispatch(c => c.OnActorEAnim(ice, 0x00100020));
            f.Tick(6);
            Check(f.Aoes.Any(a => a.ActorID == ice.InstanceID && a.Shape is AOEShapeCircle { Radius: >= 12 and < 13 }), "ice failed to grow");
            f.Engine.Dispatch(c => c.OnActorEAnim(ice, 0x00100020)); f.Tick(6);
            Check(f.Aoes.Any(a => a.ActorID == ice.InstanceID && a.Shape is AOEShapeCircle { Radius: 18 }), "duplicate animation restarted growth");
            ice.EventState = 4; f.Engine.Dispatch(c => c.OnActorEState(ice, 4)); f.Tick();
            Check(!f.Aoes.Any(a => a.ActorID == ice.InstanceID), "sleeping ice leaked");
        });
        run("B40 number rules use shuffled object values, then clear on status loss", () =>
        {
            using var f = new Fight(14665, new(120, 0));
            for (uint n = 1; n <= 9; ++n) f.Spawn(2015492 + n, f.Boss.Position + new WDir(((n - 1) % 3 - 1f) * 12, ((n - 1) / 3 - 1f) * 12), type: ActorType.EventObj);
            f.Player.Statuses = [new() { ID = 5150, ExpireAt = f.World.FutureTime(10) }];
            Check(f.Aoes.Count(a => a.Color == Colors.SafeFromAOE) == 4, "prime numbers should be 2,3,5,7");
            f.Player.Statuses = []; f.Tick();
            Check(!f.Aoes.Any(a => a.Color == Colors.SafeFromAOE), "status loss retained number answers");
        });
        run("B45 floor includes six ledges and excludes the surrounding fire", () =>
        {
            Check(CrucibleLaudaFloor.Contains(new(520, -442.5f)), "north tip absent");
            Check(CrucibleLaudaFloor.Contains(new(532.5f, -427.5f)), "east ledge absent");
            Check(!CrucibleLaudaFloor.Contains(new(532.5f, -437.5f)), "fire accepted as floor");
            Check(CrucibleLaudaFloor.Contains(new(530, -427.5f)), "union seam became an impassable wall");
        });
        run("B45 blade timeline reconciles earlier cue and cancel removes warning", () =>
        {
            using var f = new Fight(14693, new(520, -420));
            var blade = f.Spawn(19745, new(535, -425));
            f.Engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(blade, 4562));
            Check(f.Aoes.Any(a => a.ActorID == blade.InstanceID && Math.Abs((a.Activation - f.World.CurrentTime).TotalSeconds - 15) < 0.01), "missing 15 second cue");
            f.Tick(7); f.Engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(blade, 4576));
            var deadline = f.Aoes.Single(a => a.ActorID == blade.InstanceID).Activation;
            f.Engine.Dispatch(c => c.OnActorPlayActionTimelineEvent(blade, 4562));
            Check(f.Aoes.Single(a => a.ActorID == blade.InstanceID).Activation == deadline, "old cue postponed blade");
            f.Cast(49483, 3, blade.Position, caster: blade); f.Engine.FinishCast(blade, 49483);
            Check(!f.Aoes.Any(a => a.ActorID == blade.InstanceID), "canceled blade retained");
        });
        run("output toggles and reset remove owned areas and leave another owner's boundary", () =>
        {
            using var f = new Fight(14631);
            f.Cast(49193, 5, f.Boss.Position); f.Render();
            Check(f.Snapshot.DangerAreas.Count > 0, "no danger");
            f.Output.Apply(f.Engine, f.Frame, 0, false, false, false);
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Snapshot.ArenaBoundary == null, "disabled mechanisms leaked");
            var foreign = new PromeRotation.Spatial.Geometry.CircleGeometry(Vector3.Zero, 12);
            f.SafeZone.Manager.SetArenaBoundary(foreign); f.Output.Clear(); f.Output.Clear();
            Check(ReferenceEquals(f.Snapshot.ArenaBoundary, foreign), "clear removed another owner's boundary");
        });
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static SafeZoneSolution Solve(Fight f) => new SafeZoneSolver().Solve(f.Snapshot,
        new PlayerState(f.Player.Position.ToVec3(), 0, 6, false, false, 0, true, f.Snapshot.Version),
        new MovementCapability(6,6,0,false,6,6), new SolveOptions(), f.World.CurrentTime > DateTime.UtcNow ? f.World.CurrentTime : DateTime.UtcNow);
    private sealed class EventRecorder(BossModule module) : BossComponent(module)
    {
        public readonly List<ActorCastEvent> Casts = [];
        public int Starts;
        public override void OnEventCast(Actor actor, ActorCastEvent spell) => Casts.Add(spell);
        public override void OnCastStarted(Actor actor, ActorCastInfo spell) => ++Starts;
    }
    private sealed class Fight : IDisposable
    {
        public readonly WorldState World = new() { CurrentTime = DateTime.UtcNow };
        public readonly CrucibleEngine Engine;
        public readonly ScriptSafeZoneApi SafeZone = new();
        public readonly CrucibleOutput Output;
        public Actor Boss, Player;
        private ulong _nextId = 100;
        public Fight(uint name, WPos? position = null)
        {
            var p = position ?? new WPos(120, -420);
            Boss = new() { InstanceID = 1, OID = name == 14628 ? 19672u : 19000, NameID = name, Position = p, HPMP = (100, 100), Type = ActorType.Enemy, IsTargetable = true, InCombat = true };
            Player = new() { InstanceID = 2, Position = p, Type = ActorType.Player, IsTargetable = true, HPMP = (100, 100) };
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player = Player;
            Engine = new(World, 1, 45); Engine.SelectGroup();
            Output = new(SafeZone, new ScriptDrawApi(), SafeZone.Manager, _ => { });
        }
        public Actor Spawn(uint oid, WPos p, uint name = 0, ActorType type = ActorType.Enemy)
        {
            var a = new Actor { InstanceID = _nextId++, OID = oid, NameID = name, Position = p, Type = type, IsTargetable = true, HPMP = (100, 100) };
            World.Actors.Add(a); Engine.Dispatch(c => c.OnActorCreated(a)); return a;
        }
        public void Cast(uint action, float time, WPos p, Angle rotation = default, Actor? caster = null) => Engine.Cast(caster ?? Boss, new() { Action = new(action), FinishAt = World.FutureTime(time + 0.3), NPCRemainingTime = time + 0.3f, RemainingTime = time, Rotation = rotation, LocXZ = p, TargetID = Player.InstanceID });
        public void Effect(uint action, uint sequence) => Engine.Effect(Boss, new() { Action = new(action), MainTargetID = Player.InstanceID, GlobalSequence = sequence, Rotation = Boss.Rotation, TargetXZ = Player.Position });
        public CrucibleFrame Frame => Engine.Frame();
        public List<GenericAOEs.AOEInstance> Aoes => Frame.Aoes.Select(a => a.Aoe).ToList();
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);
        public void Render() => Output.Apply(Engine, Frame, 0, true, true, true);
        public void Tick(double seconds = 0.1) { World.CurrentTime = World.FutureTime(seconds); Engine.Update(); Render(); }
        public void Dispose() { Output.Clear(); SafeZone.Dispose(); }
    }
}

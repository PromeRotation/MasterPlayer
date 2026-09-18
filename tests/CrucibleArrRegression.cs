using System.Numerics;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;

internal static class CrucibleArrRegression
{
    // ARR: 斗兽奇弈高段 第二盘 2026.09.17 22.48.46.dat. Entity IDs are test-local.
    public static void Run(Action<string, Action> run)
    {
        run("Crucible optional goals discard mostly covered areas without suppressing mechanic points", () =>
        {
            using var f = new Fight();
            var center = f.Engine.Module!.Center;
            f.Player.Position = center + new WDir(-13,0);
            var frame = new CrucibleFrame();
            frame.Hints.GoalZones.Add(AIHints.GoalSingleTarget(center,6));
            frame.Hints.AddForbiddenZone(new SDCircle(center,5.5f), f.World.FutureTime(3));
            frame.Hints.GoalPoints.Add((center,0.2f,100));
            f.Output.Apply(f.Engine,frame,0,true,true,true);
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.GoalAreas[0].Geometry.Contains(center.ToVec3()),
                "nearly covered output preference competed with the precise mechanic point");
        });
        foreach (var casting in new[] { false, true })
            run($"Crucible optional goals yield to {(casting ? "casting" : "urgent escape")}", () =>
            {
                using var f = new Fight();
                var center = f.Engine.Module!.Center;
                f.Player.Position = center;
                f.Player.CastInfo = casting ? new ActorCastInfo { FinishAt = f.World.FutureTime(4) } : null;
                var frame = new CrucibleFrame();
                frame.Hints.GoalZones.Add(AIHints.GoalSingleTarget(center + new WDir(10,0),3));
                frame.Hints.AddForbiddenZone(new SDCircle(center,2),f.World.FutureTime(casting ? 4 : 0.8));
                f.Output.Apply(f.Engine,frame,0,true,true,true);
                Check(f.Snapshot.GoalAreas.Count == 0, "optional goal retained during safety or cast priority");
                Check(f.Snapshot.DangerAreas.Count == 1, "priority gate disabled actual avoidance");
            });
        run("Crucible preferences keep a lower safe peak and preserve explicit mechanic points", () =>
        {
            using var f = new Fight();
            var module = f.Engine.Module!;
            var blockedPeak = module.Center + new WDir(-5, 0);
            var safePeak = module.Center + new WDir(5, 0);
            var frame = new CrucibleFrame();
            frame.Hints.PathfindMapCenter = module.Center;
            frame.Hints.PathfindMapBounds = module.Arena.Bounds;
            frame.Hints.GoalZones.Add(p => p.InCircle(blockedPeak, 2) ? 100 : p.InCircle(safePeak, 2) ? 50 : 0);
            frame.Hints.AddForbiddenZone(new SDCircle(blockedPeak, 3), f.World.FutureTime(4));
            frame.Hints.AddForbiddenZone(new SDCircle(safePeak, 3), DateTime.MaxValue);
            frame.Hints.AddForbiddenZone(new SDCircle(safePeak, 3), f.World.FutureTime(-10), f.World.FutureTime(-1));
            frame.Hints.GoalPoints.Add((blockedPeak, 0.2f, 100));
            f.Output.Apply(f.Engine, frame, 0, true, true, true);
            var preferences = f.Snapshot.GoalAreas.Single(a => a.Geometry is FrozenConstraintGeometry);
            Check(preferences.Geometry.Contains(safePeak.ToVec3()) && !preferences.Geometry.Contains(blockedPeak.ToVec3()),
                "unsafe maximum suppressed the lower safe peak, or expired/unknown danger blocked it");
            Check(f.Snapshot.GoalAreas.Any(a => a.Geometry is PromeRotation.Spatial.Geometry.CircleGeometry { Radius: 0.2f }
                && a.Geometry.Contains(blockedPeak.ToVec3())), "explicit mechanic point was clipped as an attack preference");
            frame.Hints.GoalPoints.Clear();
            frame.Hints.AddForbiddenZone(new SDCircle(safePeak, 3), f.World.FutureTime(3));
            f.Output.Apply(f.Engine, frame, 0, true, true, true);
            Check(f.Snapshot.GoalAreas.Count == 0, "fully excluded preferences left a stale goal");
            f.World.CurrentTime = f.World.FutureTime(10);
            f.Output.Apply(f.Engine, frame, 0, true, true, true);
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.GoalAreas.Single().Geometry.Contains(blockedPeak.ToVec3()),
                "attack preference did not recover after all hits expired");
        });
        run("B33 ARR four self-targeted glyphs keep packet origins and escape the recorded hit position", () =>
        {
            using var f = new Fight();
            f.At(275.503);
            f.Player.Position = new(117.9065f, -4.1962f);
            var rotation = (-0.002746624f).Degrees();
            var lines = new[]
            {
                (Action: 49204u, Actor: new WPos(115, 15), Location: new WPos(114.9767f, -35.0195f)),
                (Action: 49203u, Actor: new WPos(135, 5), Location: new WPos(134.9966f, -45.0294f)),
                (Action: 49203u, Actor: new WPos(105, -5), Location: new WPos(104.9973f, -55.0088f)),
                (Action: 49204u, Actor: new WPos(125, -15), Location: new WPos(124.9866f, -65.0187f)),
            };
            var adapter = f.Adapter(rotation.Rad);
            foreach (var line in lines)
            {
                var glyph = f.Spawn(19681, line.Actor, 14637);
                // Seed the potential native-scan fallback first. The event must correct a self target origin.
                f.Engine.Cast(glyph, new()
                {
                    Action = new(line.Action), TargetID = glyph.InstanceID, LocXZ = line.Actor,
                    Rotation = rotation, FinishAt = f.Time(277.503), RemainingTime = 1.7f, NPCRemainingTime = 2,
                });
                adapter.Handle(new LogSystemCastStartEvent
                {
                    Timestamp = f.World.CurrentTime, SourceId = glyph.InstanceID, TargetId = glyph.InstanceID,
                    ActionId = (ushort)line.Action, CastTime = 1.7f, EffectPosition = line.Location.ToVec3(),
                    SourcePosition = line.Actor.ToVec3(), TargetPosition = line.Actor.ToVec3(), SourceRotation = 1.5f,
                });
                Check(glyph.CastInfo!.LocXZ == line.Location, "self-target location replaced the 50y-offset packet origin");
                var aoe = f.Engine.Frame().Aoes.Single(a => a.Aoe.ActorID == glyph.InstanceID).Aoe;
                Check(aoe.Origin == line.Location && aoe.Rotation == rotation, "the AOE did not preserve the event position/direction");
            }
            f.Render();
            Check(f.Engine.Frame().Aoes.Count(a => a.Aoe.Shape is AOEShapeRect { LengthFront: 100 }) == 4, "native + packet casts duplicated or lost a glyph");
            var snapshot = f.Snapshot;
            Check(snapshot.DangerAreas.Any(a => a.Geometry.Contains(f.Player.Position.ToVec3())), "recorded cast-start position was incorrectly considered safe");
            Check(snapshot.DangerAreas.Any(a => a.Geometry.Contains(new Vector3(117.9980f, 0, -3.9216f))), "recorded 49204 hit position was incorrectly considered safe");
            var solution = f.Solve();
            Check(solution.ExecutablePath.Count >= 2, "the four-glyph fixture did not request an escape");
            CheckReachable(f, solution, f.Time(277.416), "four glyphs");
        });

        run("B33 ARR water keeps replacement-pet danger until both real hits and has an escape route", () =>
        {
            using var f = new Fight();
            f.At(225.382);
            f.Player.Position = new(119.9817f, -3.2197f);
            var oldPet = f.Spawn(18931, new(119.9817f, -0.2289f), 14422);
            oldPet.OwnerID = f.Player.InstanceID;
            var adapter = f.Adapter(0);
            foreach (var target in new[] { f.Player, oldPet })
                adapter.Handle(new LogSystemTargetIconEvent { Timestamp = f.World.CurrentTime, SourceId = target.InstanceID, TargetId = target.InstanceID, MarkerId = 135 });
            f.Render();
            var baselineWater = f.Engine.Module!.FindComponent<CrucibleMindflayerWater>()!;
            Check(baselineWater.IsBaitTarget(f.Player) && baselineWater.IsBaitTarget(oldPet), "ARR's two initial marks were not captured");
            var plan = f.Solve();
            CheckReachable(f, plan, f.Time(231.318), "initial water");

            // Follow each computed route between packet times; these positions represent the repaired movement,
            // while spawn/removal times and both moving-pet positions come directly from the recording.
            f.AdvanceAlong(226.475, plan);
            var newPet = f.Spawn(18916, new(119.9796f, -6.0936f), 14407);
            newPet.OwnerID = f.Player.InstanceID;
            f.Render();
            CheckPetDanger(f, oldPet, f.Time(231.318));
            CheckPetDanger(f, newPet, f.Time(231.318));
            plan = f.Solve();
            CheckReachable(f, plan, f.Time(231.318), "both pets present");

            foreach (var move in new[] { (Time: 227.074, Pos: new WPos(119.1577f, -4.7150f)), (Time: 227.383, Pos: new WPos(118.1506f, -3.0060f)) })
            {
                f.AdvanceAlong(move.Time, plan);
                newPet.Position = move.Pos;
                f.Render();
                CheckPetDanger(f, newPet, f.Time(231.318));
                plan = f.Solve();
                CheckReachable(f, plan, f.Time(231.318), "replacement pet moved");
            }

            f.AdvanceAlong(229.668, plan);
            adapter.Handle(new LogSystemObjectRemoveEvent { Timestamp = f.World.CurrentTime, SourceId = oldPet.InstanceID });
            f.Render();
            Check(baselineWater.IsBaitTarget(f.Player) && !baselineWater.IsBaitTarget(oldPet), "old-pet removal also removed the player's water mark");
            CheckPetDanger(f, newPet, f.Time(231.318));
            Check(!f.Snapshot.DangerAreas.Any(a => a.Geometry.Position == oldPet.Position.ToVec3()), "old-pet splash remained after its removal");
            plan = f.Solve();
            CheckReachable(f, plan, f.Time(231.318), "old marked pet destroyed");

            f.AdvanceAlong(231.317, plan);
            f.Render();
            CheckPetDanger(f, newPet, f.Time(231.318));
            var actualHitPosition = new Vector3(121.2024f, 0, 1.8769f);
            Check(f.Snapshot.DangerAreas.Any(a => a.Geometry.Position == newPet.Position.ToVec3()
                && a.Geometry.Contains(actualHitPosition) && a.DangerWindows.Any(w => w.IsActiveAt(f.Time(231.318)))),
                "recorded player/new-pet separation failure stopped being dangerous before the hit");

            f.At(231.318);
            // Positions of damage-only helpers do not affect bait resolution.
            foreach (var sequence in new uint[] { 721, 722 })
            {
                var helper = f.Spawn(9020, f.Boss.Position);
                foreach (var target in new[] { f.Player, newPet }.Select((actor, index) => (actor, index)))
                    adapter.Handle(new LogSystemActionEffectEvent
                    {
                        Timestamp = f.World.CurrentTime, SourceId = helper.InstanceID, ActionId = 49200,
                        GlobalSequence = sequence, TargetId = target.actor.InstanceID, TargetIndex = target.index,
                    });
            }
            adapter.Flush();
            f.Render();
            Check(baselineWater.NumCasts == 2 && baselineWater.CurrentBaits.Count == 0, "two helpers with expanded target lists did not resolve exactly twice");
            Check(!f.Snapshot.DangerAreas.Any(a => a.Geometry.Position == newPet.Position.ToVec3()), "resolved water retained its companion constraint");
        });
    }

    private static void CheckPetDanger(Fight f, Actor pet, DateTime hitTime)
        => Check(f.Snapshot.DangerAreas.Any(a => a.Geometry.Position == pet.Position.ToVec3()
            && a.DangerWindows.Any(w => w.IsActiveAt(hitTime))), $"pet {pet.OID} lost its splash before actual damage");

    private static void CheckReachable(Fight f, SafeZoneSolution solution, DateTime deadline, string stage)
    {
        var snapshot = f.Snapshot;
        Check(solution.IsSuccess, $"{stage}: no route ({solution.Status})");
        var target = solution.ExecutablePath.Count > 0 ? solution.ExecutablePath[^1].Position : f.Player.Position.ToVec3();
        Check(!snapshot.DangerAreas.Any(a => a.Geometry.Contains(target)), $"{stage}: route returns to a still dangerous point");
        Check(snapshot.ArenaBoundary!.Contains(target), $"{stage}: route leaves the arena");
        if (solution.ExecutablePath.Count > 0)
            Check(solution.ExecutablePath[^1].PlannedArrivalTime <= deadline, $"{stage}: safe point is reached after damage");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fight : IDisposable
    {
        private readonly DateTime _start = DateTime.UtcNow.AddMinutes(1);
        private ulong _nextId = 100;
        public readonly WorldState World = new();
        public readonly CrucibleEngine Engine;
        public readonly ScriptSafeZoneApi SafeZone = new();
        public readonly CrucibleOutput Output;
        public readonly Actor Boss, Player;
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);

        public Fight()
        {
            World.CurrentTime = _start;
            Boss = new() { InstanceID = 1, OID = 19677, NameID = 14633, Position = new(119.9817f, -4.8066f), HitboxRadius = 3, HPMP = (100, 100), Type = ActorType.Enemy, IsTargetable = true, InCombat = true };
            Player = new() { InstanceID = 2, Type = ActorType.Player, HPMP = (100, 100), IsTargetable = true, Position = Boss.Position };
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player = Player;
            Engine = new(World, 32, 45); Engine.SelectGroup();
            Output = new(SafeZone, new ScriptDrawApi(), SafeZone.Manager, _ => { });
        }
        public DateTime Time(double seconds) => _start.AddSeconds(seconds);
        public void At(double seconds) => World.CurrentTime = Time(seconds);
        public void AdvanceAlong(double seconds, SafeZoneSolution plan)
        {
            At(seconds);
            var path = plan.ExecutablePath;
            if (path.Count == 0) return;
            for (var i = 1; i < path.Count; ++i)
            {
                if (World.CurrentTime >= path[i].PlannedArrivalTime) continue;
                var duration = (path[i].PlannedArrivalTime - path[i - 1].PlannedDepartureTime).TotalSeconds;
                var fraction = duration > 0 ? Math.Clamp((World.CurrentTime - path[i - 1].PlannedDepartureTime).TotalSeconds / duration, 0, 1) : 1;
                Player.Position = WPos.FromVec3(Vector3.Lerp(path[i - 1].Position, path[i].Position, (float)fraction));
                return;
            }
            Player.Position = WPos.FromVec3(path[^1].Position);
        }
        public Actor Spawn(uint oid, WPos position, uint name = 0)
        {
            var actor = new Actor { InstanceID = _nextId++, OID = oid, NameID = name, Position = position, Type = ActorType.Enemy, IsTargetable = true, HPMP = (100, 100) };
            World.Actors.Add(actor); Engine.Dispatch(c => c.OnActorCreated(actor)); return actor;
        }
        public CrucibleEventAdapter Adapter(float rotation) => new(Engine, World.Actors.Find, _ => rotation, () => { }, message => throw new InvalidOperationException(message));
        public void Render() { Engine.Update(); Output.Apply(Engine, Engine.Frame(), 0, true, true, true); }
        public SafeZoneSolution Solve()
        {
            var snapshot = Snapshot;
            return new SafeZoneSolver().Solve(snapshot,
                new PlayerState(Player.Position.ToVec3(), 0, 6, false, false, 0, true, snapshot.Version),
                new MovementCapability(6, 6, 0, false, 6, 6), new SolveOptions(), World.CurrentTime);
        }
        public void Dispose() { Output.Clear(); SafeZone.Dispose(); }
    }
}

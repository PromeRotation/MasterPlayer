using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class CrucibleTimingRegression
{
    public static void Run(Action<string, Action> run)
    {
        foreach (var (name, action, center) in new[] { (14599u, 50758u, new WPos(520,-420)), (14617u, 50649u, new WPos(120,-420)) })
        {
            run($"high-one knockback {action} holds its landing constraint after cast end until effect", () =>
            {
                using var f = new Fight(name, center);
                f.Render();
                var baseline = f.Snapshot.DangerAreas.Count;
                var impact = f.World.FutureTime(3);
                f.Engine.Cast(f.Boss, new() { Action = new(action), FinishAt = impact, LocXZ = f.Center });
                f.Render();
                Check(f.Snapshot.DangerAreas.Count > baseline, "no knockback constraint before cast end");
                f.World.CurrentTime = impact;
                f.Engine.FinishCast(f.Boss, action);
                f.World.CurrentTime = impact.AddSeconds(0.15);
                f.Render();
                Check(f.Snapshot.DangerAreas.Count > baseline, "normal cast completion released landing constraint before hit");
                Check(f.Snapshot.DangerAreas.Any(a => a.DangerWindows.Any(w => w.IsActiveAt(f.World.CurrentTime))), "pending hit lost its active window");
                f.Engine.Effect(f.Boss, new() { Action = new(action), GlobalSequence = 1 });
                f.Render();
                Check(f.Snapshot.DangerAreas.Count == baseline, "effect did not release landing constraint");
            });
            run($"high-one knockback {action} cancels, expires and clears on source death", () =>
            {
                foreach (var cleanup in new[] { "cancel", "timeout", "death" })
                {
                    using var f = new Fight(name, center);
                    var component = f.Engine.Module!.Components.OfType<HighCrucibleKnockback>().Single();
                    var impact = f.World.FutureTime(3);
                    f.Engine.Cast(f.Boss, new() { Action = new(action), FinishAt = impact, LocXZ = f.Center });
                    Check(component.ActiveKnockbacks(0, f.Player).Length == 1, "missing knockback source");
                    if (cleanup == "cancel") { f.World.CurrentTime = impact.AddSeconds(-0.05); f.Engine.FinishCast(f.Boss, action, true); }
                    else if (cleanup == "timeout") { f.World.CurrentTime = impact.AddSeconds(2.01); }
                    else f.Engine.Destroy(f.Boss, false);
                    Check(component.ActiveKnockbacks(0, f.Player).Length == 0, $"{cleanup} retained stale knockback source");
                }
            });
        }
        run("Crucible knockback immunity matches BMR status IDs and must outlast the impact", () =>
        {
            using var f = new Fight(14617, new(120,-420));
            var component = f.Engine.Module!.FindComponent<CrucibleGolemKnockback>()!;
            var impact = f.World.FutureTime(3);
            foreach (var status in new[] { 160u, 1209u, 3054u, 1722u, 2663u, 2345u })
            {
                f.Player.Statuses = [new() { ID = status, ExpireAt = impact.AddSeconds(1) }];
                Check(component.IsImmune(0, impact), $"immunity status {status} was ignored");
                f.Player.Statuses = [new() { ID = status, ExpireAt = impact }];
                Check(!component.IsImmune(0, impact), "immunity ending exactly at impact was trusted");
            }
        });
        foreach (var effectReceived in new[] { true, false })
        foreach (var angle in new[] { 0f, 45f, 180f, 270f })
        run($"high-one Gargoyle steel/ring solves with margin, direction {angle}, effect={effectReceived}", () =>
        {
            using var f = new Fight(14608, new(120,0), 0.5f);
            f.Player.Position = f.Center + 8 * angle.Degrees().ToDirection();
            var finish = f.World.FutureTime(4.5);
            f.Engine.Cast(f.Boss, new() { Action = new(48719), FinishAt = finish, RemainingTime = 4.2f, NPCRemainingTime = 4.5f, LocXZ = f.Center });
            f.Render();
            var solution = f.Solve();
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, $"steel/ring prediction prevents escape: {solution.Status}");
            f.World.CurrentTime = finish.AddSeconds(-2.5);
            f.Boss.CastInfo!.RemainingTime = 2.2f;
            f.Render();
            solution = f.Solve();
            Check(solution.IsSuccess && solution.TargetPosition != null, $"steel staging has no safe approach: {solution.Status}");
            var stage = WPos.FromVec3(solution.TargetPosition!.Value);
            Check(!stage.InCircle(f.Center, 13.25f) && stage.InCircle(f.Center, 13.75f), "staging point is inside steel or too far to return");
            var steel = f.Engine.Module!.FindComponent<CrucibleGargoyleInOut>()!.ActiveAOEs(0, f.Player)[0];
            var impact = steel.Activation;
            f.World.CurrentTime = impact.AddSeconds(-0.05);
            f.Player.Position = stage;
            f.Render();
            Check(f.Snapshot.GoalAreas.All(g => !g.Geometry.Contains(f.Center.ToVec3())), "ring goal drew player inward before steel hit");
            f.World.CurrentTime = effectReceived ? impact : impact.AddSeconds(0.26);
            if (effectReceived) f.Engine.Effect(f.Boss, new() { Action = new(48720), GlobalSequence = 1 });
            f.Render();
            solution = f.Solve();
            Check(solution.IsSuccess && solution.TargetPosition is { } destination && WPos.FromVec3(destination).InCircle(f.Center, 5.5f),
                $"ring safe center is unreachable after steel: {solution.Status}");
            Check(solution.ExecutablePath[^1].PlannedArrivalTime < impact.AddSeconds(1.95), "ring route enters safety too late");
            f.Engine.Effect(f.Boss, new() { Action = new(48721), GlobalSequence = 2 });
            f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "ring left its old center goal after effect");
        });
        run("high-one Gargoyle chooses a mine-free staging route and clears it on interruption", () =>
        {
            using var f = new Fight(14608, new(120,0), 0.5f);
            f.Player.Position = f.Center + new WDir(0, 12);
            var mine = f.Spawn(19651, f.Center + new WDir(0, 9), ActorType.Enemy);
            f.Engine.Dispatch(c => c.OnTethered(mine, new(426, f.Boss.InstanceID)));
            f.Engine.Cast(f.Boss, new() { Action = new(48719), FinishAt = f.World.FutureTime(2.8), RemainingTime = 2.5f, NPCRemainingTime = 2.8f, LocXZ = f.Center });
            f.Render();
            var solution = f.Solve();
            Check(solution.IsSuccess && solution.TargetPosition != null, $"mine blocks steel staging: {solution.Status}");
            var stage = WPos.FromVec3(solution.TargetPosition!.Value);
            var route = f.Center - stage;
            var projection = Math.Clamp(((mine.Position-stage).X * route.X + (mine.Position-stage).Z * route.Z) / route.LengthSq(), 0, 1);
            Check(!mine.Position.InCircle(stage + projection * route, 2.3f), "staging point sends ring approach through a mine");
            var impact = f.Engine.Module!.FindComponent<CrucibleGargoyleInOut>()!.ActiveAOEs(0,f.Player)[0].Activation;
            f.Player.Position = stage;
            f.World.CurrentTime = impact;
            f.Engine.Effect(f.Boss, new() { Action = new(48720), GlobalSequence = 1 });
            f.Render();
            solution = f.Solve();
            Check(solution.IsSuccess && solution.TargetPosition is { } safe && WPos.FromVec3(safe).InCircle(f.Center,5.5f),
                $"mine blocks ring approach: {solution.Status}");
            Check(solution.ExecutablePath.All(p => !mine.Position.InCircle(WPos.FromVec3(p.Position), 2.3f)), "ring route crosses armed mine");
            f.Engine.FinishCast(f.Boss, 48719, true);
            f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "interrupted steel/ring retained staging goal");
        });
        foreach (var delay in new[] { 0.08, 0.3 })
        foreach (var effectReceived in new[] { true, false })
        foreach (var angle in new[] { 0f, 45f })
        foreach (var phase in new[] { 0, 2 })
        run($"high-one Gargoyle continuous movement, worker={delay:F2}s, effect={effectReceived}, angle={angle}, tick={phase}", () =>
            GargoyleMovement(delay, effectReceived, angle, phase));
        run("high-one Gargoyle immediate worker does not reenter a slightly late steel hit", () =>
            GargoyleMovement(0, false, 45, 0, startup: 0, steelDelayTicks: 2));
        foreach (var effectReceived in new[] { true, false })
        run($"high-one Gargoyle continuous mine avoidance, effect={effectReceived}", () =>
            GargoyleMovement(0.3, effectReceived, 0, 2, mine: true));
        foreach (var optimize in new[] { false, true })
        foreach (var start in new[] { 6f, -6f })
        run($"high-one Gargoyle charge cleaves, optimize={optimize}, start={start}", () =>
            GargoyleChargeMovement(optimize, start, 0.3));
        foreach (var optimize in new[] { false, true })
        foreach (var effectReceived in new[] { true, false })
        foreach (var delay in new[] { 0, 0.3 })
        foreach (var angle in new[] { -0.6f, 45f })
        run($"high-one Gargoyle charge cleaves recorded landing, optimize={optimize}, effect={effectReceived}, worker={delay}, angle={angle}", () =>
            GargoyleChargeMovement(optimize, 0, delay, angle, effectReceived));
        foreach (var optimize in new[] { false, true })
        run($"high-one Gargoyle charge cleaves near boundary, optimize={optimize}", () =>
            GargoyleChargeMovement(optimize, 0, 0.3, 0, landingDistance: 16));
        run("high-one Gargoyle charge holds the first refuge and switches on a single damage event", () =>
        {
            using var f = new Fight(14608, new(120, 0), 0.3f);
            f.Boss.OID = 19650;
            var facing = (-0.6f).Degrees();
            f.Boss.Position = f.Center + new WDir(-0.1f, 0.9f);
            f.Engine.Effect(f.Boss, new() { Action = new(50932), GlobalSequence = 100, TargetXZ = f.Boss.Position, Rotation = facing });
            f.World.CurrentTime = f.World.FutureTime(1);
            f.Player.Position = f.Boss.Position - 3 * facing.ToDirection();
            f.Render();
            var component = f.Engine.Module!.FindComponent<CrucibleGargoyleCharge>()!;
            Check(component.ActiveAOEs(0, f.Player).Length == 2, "second cleave preview disappeared");
            var solution = f.Solve();
            Check(solution.IsSuccess && !solution.UsesTemporalPlanning && solution.ExecutablePath.Count == 0,
                "settling behind the first cleave started a full-field temporal search instead of holding its refuge");
            f.World.CurrentTime = f.World.FutureTime(1.8);
            var effect = new ActorCastEvent { Action = new(48718), GlobalSequence = 101, Rotation = facing };
            f.Engine.Effect(f.Boss, effect);
            f.Engine.Effect(f.Boss, effect);
            f.Render();
            Check(component.ActiveAOEs(0, f.Player).Length == 1, "duplicate first-cleave effect consumed the second cleave");
            Check(f.Snapshot.GoalAreas.Single().Geometry.Contains((f.Boss.Position + 3 * facing.ToDirection()).ToVec3()),
                "first hit did not immediately switch to the opposite refuge");
            solution = f.Solve();
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "second-cleave escape has no executable route");
            f.Engine.Destroy(f.Boss, false);
            f.Render();
            Check(component.ActiveAOEs(0, f.Player).Length == 0 && f.Snapshot.GoalAreas.Count == 0,
                "source death retained the pending cleave or its goal");
        });
        run("high-one Morbol breath predicts five windows and consumes only real damage effects", () =>
        {
            using var f = new Fight(14599, new(520,-420));
            var component = f.Engine.Module!.FindComponent<CrucibleMorbolBreath>()!;
            var finish = f.World.FutureTime(4.5);
            f.Engine.Cast(f.Boss, new() { Action = new(48671), FinishAt = finish, RemainingTime = 4.2f, NPCRemainingTime = 4.5f, LocXZ = f.Center });
            f.Render();
            Check(component.ActiveAOEs(0,f.Player).Length == 5, "missing predicted breaths");
            Check(f.Snapshot.DangerAreas.SelectMany(a => a.DangerWindows).Where(w => w.DangerUntil != null).Select(w => w.DangerFrom).Distinct().Count() >= 5, "future breaths did not reach PR");
            f.World.CurrentTime = finish.AddSeconds(0.5);
            f.Engine.Effect(f.Boss, new() { Action = new(48674), GlobalSequence = 1 });
            Check(component.ActiveAOEs(0,f.Player).Length == 5, "rotation visual consumed a damage step");
            f.Engine.Effect(f.Boss, new() { Action = new(48673), GlobalSequence = 2 });
            f.Engine.Effect(f.Boss, new() { Action = new(48673), GlobalSequence = 2 });
            Check(component.ActiveAOEs(0,f.Player).Length == 4, "duplicate effect consumed extra breath");
            f.Engine.Destroy(f.Boss, false);
            Check(component.ActiveAOEs(0,f.Player).Length == 0, "death retained breath predictions");
        });
        run("Crucible exact positioning holes preserve narrow safe points and bounded segment checks", () =>
        {
            var shape = new CircleComplementGeometry(Vector3.Zero, 0.6f, new(-5, -5, 5, 5));
            Check(!shape.Contains(new(0.59f, 0, 0)) && shape.Contains(new(0.61f, 0, 0)), "safe pocket was rounded away");
            Check(!shape.IntersectsSegment(new(-0.4f, 0, 0), new(0.4f, 0, 0)), "path inside a safe pocket became dangerous");
            Check(shape.IntersectsSegment(new(-0.4f, 0, 0), new(0.7f, 0, 0)), "path leaving the pocket was accepted");
            Check(!shape.IntersectsSegment(new(6, 0, -1), new(6, 0, 1)) && !shape.Contains(new(6, 0, 0)), "constraint escaped its arena extent");
            var covered = new CircleComplementGeometry(Vector3.Zero, 10, new(-1, -1, 1, 1));
            Check(!covered.IntersectsSegment(new(-20, 0, 0), new(20, 0, 0)), "unclipped endpoints poisoned an entirely safe bounded region");
        });
        run("Crucible unknown activation stays inert and explicit default-time expiry is preserved", () =>
        {
            using var f = new Fight(14631, new(120, 0));
            var frame = f.EmptyFrame();
            frame.Hints.AddForbiddenZone(new SDCircle(f.Center, 3), DateTime.MaxValue);
            f.Apply(frame);
            Check(f.Snapshot.DangerAreas.Count == 0, "unknown activation became an active hazard");
            var expires = f.World.FutureTime(2);
            frame.Hints.AddForbiddenZone(new SDCircle(f.Center, 3), expiresAt: expires);
            f.Apply(frame);
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerUntil == expires, "default activation discarded the explicit expiry");
        });
        run("Crucible positioning deadlines stay binding until the component releases them", () =>
        {
            using var f = new Fight(14631, new(120, 0));
            var frame = f.EmptyFrame();
            var deadline = f.World.FutureTime(1);
            frame.Hints.AddForbiddenConstraint(new SDInvertedCircle(f.Center, 3), deadline);
            f.Apply(frame);
            f.World.CurrentTime = deadline.AddSeconds(2);
            f.Apply(frame);
            var window = f.Snapshot.DangerAreas.Single().DangerWindows.Single();
            Check(window.IsActiveAt(f.World.CurrentTime) && window.DangerUntil == null, "positioning deadline became a 0.6-second hit");
            f.Apply(f.EmptyFrame());
            Check(f.Snapshot.DangerAreas.Count == 0, "component release retained a positioning constraint");
        });
        run("Crucible Sphinx correct tile is enforced through the last countdown fraction", () =>
        {
            using var f = new Fight(14665, new(120, 0));
            f.Render();
            var baselineDangers = f.Snapshot.DangerAreas.Count;
            f.Spawn(2015494, f.Center, ActorType.EventObj); // Two pips, accepted by even rule.
            var expires = f.World.FutureTime(10);
            f.Player.Statuses = [new() { ID = 5148, ExpireAt = expires }];
            f.Player.Position = f.Center + new WDir(8, 0);
            f.World.CurrentTime = expires.AddSeconds(-0.05);
            f.Render();
            Check(f.Snapshot.DangerAreas.Any(a => a.Geometry.Contains(f.Player.Position.ToVec3()) && a.DangerWindows.Any(w => w.IsActiveAt(f.World.CurrentTime))), "number restriction ended before its status");
            var solution = f.Solve();
            Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2 && solution.TargetPosition is { } target
                && Math.Abs(target.X - f.Center.X) < 5.5f && Math.Abs(target.Z - f.Center.Z) < 5.5f, $"correct-tile escape failed: {solution.Status}");
            f.World.CurrentTime = expires.AddSeconds(0.1);
            f.Player.Statuses = [];
            f.Render();
            Check(f.Snapshot.DangerAreas.Count == baselineDangers && f.Snapshot.GoalAreas.Count == 0, "expired number rule retained its constraint");
        });
        run("Crucible Lauda shockwave keeps its final position until the knockback effect", () =>
        {
            using var f = new Fight(14693, new(520, -420));
            f.Render();
            var baselineDangers = f.Snapshot.DangerAreas.Count;
            var bound = new ActorStatus { ID = 5555, ExpireAt = f.World.FutureTime(1) };
            var mark = new ActorStatus { ID = 5341, ExpireAt = f.World.FutureTime(10) };
            f.Engine.Dispatch(c => c.OnStatusGain(f.Player, ref bound));
            f.Engine.Dispatch(c => c.OnStatusGain(f.Player, ref mark));
            f.Engine.Effect(f.Boss, new() { Action = new(49485), GlobalSequence = 1, Targets = [new(f.Player.InstanceID)] });
            f.World.CurrentTime = mark.ExpireAt.AddSeconds(-7);
            f.Render();
            var approach = f.Solve();
            Check(approach.IsSuccess && approach.ExecutablePath.Count >= 2, $"shockwave approach failed: {approach.Status}");
            f.Player.Position = f.Engine.Frame().Hints.GoalPoints.Single().Position;
            f.World.CurrentTime = mark.ExpireAt.AddSeconds(-0.1);
            f.Render();
            Check(f.Snapshot.DangerAreas.Any(a => a.DangerWindows.Any(w => w.DangerUntil == null && w.DangerFrom == mark.ExpireAt)), "shockwave standing constraint disappeared before the impact");
            var solution = f.Solve();
            Check(solution.IsSuccess && solution.ExecutablePath.Count == 0, $"shockwave left the final safe point: {solution.Status}");
            f.Engine.Effect(f.Boss, new() { Action = new(49489), GlobalSequence = 2, Targets = [new(f.Player.InstanceID)] });
            f.Render();
            Check(f.Snapshot.DangerAreas.Count == baselineDangers && f.Snapshot.GoalAreas.Count == 0, "knockback resolution retained its old standing point");
        });
        run("Crucible Durga ARR staging survives visual completion and the later helper knockback", () =>
        {
            using var f = new Fight(14657, new(120, 0));
            f.Render();
            var baselineDangers = f.Snapshot.DangerAreas.Count;
            var started = f.World.CurrentTime;
            f.Player.Position = new(112, -8);
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player, 23, f.Player.InstanceID));
            f.Engine.Cast(f.Boss, new() { Action = new(49273), FinishAt = started.AddSeconds(4.2), RemainingTime = 3.9f, NPCRemainingTime = 4.2f });
            f.World.CurrentTime = started.AddSeconds(3.532); // ARR 1240.8: the old follower turned back here.
            f.Player.Position = new(102.56f, -17.41f);
            f.Render();
            Check(f.Snapshot.GoalAreas.Any(a => a.Geometry.Contains(f.Player.Position.ToVec3())), "Durga corner goal disappeared before the source was placed");
            f.World.CurrentTime = started.AddSeconds(4.161);
            f.Engine.Effect(f.Boss, new() { Action = new(49273), GlobalSequence = 10 });
            f.Render();
            Check(f.Snapshot.GoalAreas.Count > 0, "visual 49273 effect released corner bait too early");
            f.World.CurrentTime = started.AddSeconds(5.059);
            var helper = f.Spawn(9020, new(102.56f, -17.41f), ActorType.Enemy);
            f.Engine.Cast(helper, new() { Action = new(49274), FinishAt = started.AddSeconds(7.059), RemainingTime = 1.7f, NPCRemainingTime = 2 });
            f.Render();
            var approach = f.Solve();
            Check(approach.IsSuccess && approach.ExecutablePath.Count >= 2 && approach.TargetPosition is not null,
                $"Durga diagonal approach is not reachable when the helper locks: {approach.Status}, path={approach.ExecutablePath.Count}");
            Check(approach.ExecutablePath[^1].PlannedArrivalTime < started.AddSeconds(7.036), "Durga route cannot finish before the recorded knockback");
            f.Player.Position = WPos.FromVec3(approach.TargetPosition!.Value);
            var landing = f.Player.Position + (f.Player.Position - helper.Position).Normalized() * 40;
            Check(Math.Abs(landing.X - f.Center.X) < 19.3f && Math.Abs(landing.Z - f.Center.Z) < 19.3f,
                $"40y knockback from the solved position leaves the actual field: {landing}");
            f.World.CurrentTime = started.AddSeconds(7.02); // ARR real knockback was 7.036 seconds after the visual began.
            f.Render();
            Check(f.Snapshot.DangerAreas.Count > baselineDangers && f.Snapshot.GoalAreas.Any(a => a.Geometry.Contains(f.Player.Position.ToVec3())),
                "helper landing check or standing goal disappeared before real effect");
            var held = f.Solve();
            Check(held.IsSuccess && (held.TargetPosition is not { } target || WPos.FromVec3(target).AlmostEqual(f.Player.Position, 0.25f)),
                $"Durga left the safe knockback point before the hit: {held.Status}");
            f.World.CurrentTime = started.AddSeconds(7.709); // Keep the constraint if the effect packet is delayed.
            f.Render();
            Check(f.Snapshot.DangerAreas.Count > baselineDangers && f.Snapshot.GoalAreas.Any(a => a.Geometry.Contains(f.Player.Position.ToVec3())),
                "pending helper lost its landing constraint on a guessed hit duration");
            f.Engine.Effect(helper, new() { Action = new(49274), GlobalSequence = 11, Targets = [new(f.Player.InstanceID)] });
            f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Count == baselineDangers, "resolved helper retained its old position constraint");
        });
        run("Crucible early AOE departure margins do not advance the hit-window end", () =>
        {
            using var f = new Fight(14631, new(120, 0));
            var frame = f.EmptyFrame();
            var impact = f.World.FutureTime(5);
            CrucibleScorpionAI.Avoid(frame.Hints, new(new AOEShapeCircle(6), f.Center, activation: impact));
            f.Apply(frame);
            var window = f.Snapshot.DangerAreas.Single().DangerWindows.Single();
            Check(window.DangerFrom == impact.AddSeconds(-0.25) && window.DangerUntil == impact.AddSeconds(0.6), "start cushion also shortened the hit's tail");
        });
        run("Crucible finite hit windows allow PR to wait then enter the following donut", () =>
        {
            using var f = new Fight(14631, new(120, 0));
            f.Player.Position = f.Center + new WDir(8, 0);
            var frame = f.EmptyFrame();
            var first = f.World.FutureTime(2);
            var second = f.World.FutureTime(6);
            frame.Hints.AddForbiddenZone(new AOEShapeCircle(6), f.Center, activation: first);
            frame.Hints.AddForbiddenConstraint(new SDInvertedCircle(f.Center, 10), first.AddSeconds(-1), first.AddSeconds(0.6));
            frame.Hints.AddForbiddenZone(new AOEShapeDonut(6, 40), f.Center, activation: second);
            f.Apply(frame);
            var before = f.Solve();
            Check(before.IsSuccess && before.ExecutablePath.Any(p => p.IsWaitPoint && p.PlannedDepartureTime >= first.AddSeconds(0.6)),
                $"PR did not preserve a wait before re-entering steel: {before.Status}, path={before.ExecutablePath.Count}");
            f.World.CurrentTime = first.AddSeconds(1);
            f.SafeZone.Manager.PruneExpiredAreas(f.World.CurrentTime.AddSeconds(-0.3));
            f.Apply(frame);
            var after = f.Solve();
            Check(after.IsSuccess && after.ExecutablePath.Count >= 2 && after.TargetPosition is { } target
                && WPos.FromVec3(target).InCircle(f.Center, 6), $"PR could not transition into the later donut pocket: {after.Status}");
        });
    }

    private static void GargoyleMovement(double delay, bool effectReceived, float angle, int phase, double startup = 0.08, int steelDelayTicks = 0, bool mine = false)
    {
        using var f = new Fight(14608, new(120, 0), 0.5f);
        f.Boss.OID = 19650;
        f.Player.Position = f.Center + 8 * angle.Degrees().ToDirection();
        var epoch = f.World.CurrentTime;
        var steelHelper = f.Spawn(9020, f.Center, ActorType.Enemy);
        var ringHelper = f.Spawn(9020, f.Center, ActorType.Enemy);
        Actor? armedMine = null;
        if (mine)
        {
            armedMine = f.Spawn(19651, f.Center + new WDir(0, 10.5f), ActorType.Enemy);
            f.Engine.Dispatch(c => c.OnTethered(armedMine, new(426, f.Boss.InstanceID)));
        }
        f.Engine.Cast(f.Boss, new() { Action = new(48719), FinishAt = epoch.AddSeconds(4.5), LocXZ = f.Center });
        const BindingFlags binding = BindingFlags.Instance | BindingFlags.NonPublic;
        var (movement, move) = ((object, PromeRotation.GreenMoveSystem.IGreenMoveSystem))typeof(LateDodgeRegression)
            .GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var type = movement.GetType();
        type.GetField("_manager", binding)!.SetValue(movement, f.SafeZone.Manager);
        type.GetField("_setting", binding)!.SetValue(movement, new SafeZoneSetting
        {
            EnableSafeZone = true, EnableAutoMovement = true, AttackUptimeExpiresAt = epoch.AddMinutes(1)
        });
        var latest = type.GetField("_latestSolution", binding)!;
        var dispatch = type.GetMethod("Dispatch", binding)!;
        var valid = type.GetMethod("IsSolutionValid", binding)!;
        var advance = move.GetType().GetMethod("Advance")!;
        SafeZoneSolution? pending = null;
        var position = f.Player.Position.ToVec3();
        double ready = 0, nextSolve = 0, movementReady = 0;
        var wasMoving = false;
        for (var tick = 0; tick <= 360; ++tick)
        {
            var t = tick * 0.02;
            var now = epoch.AddSeconds(t);
            f.World.CurrentTime = now;
            f.Player.Position = WPos.FromVec3(position);
            foreach (var actor in f.World.Actors)
                if (actor.CastInfo is { } cast)
                {
                    cast.NPCRemainingTime = (float)(cast.FinishAt - now).TotalSeconds;
                    cast.RemainingTime = cast.NPCRemainingTime - ActorCastInfo.NPCFinishDelay;
                }
            if (tick == 220)
                f.Engine.Cast(steelHelper, new() { Action = new(48720), FinishAt = epoch.AddSeconds(5.1), LocXZ = f.Center });
            if (tick == 225) f.Engine.FinishCast(f.Boss, 48719);
            if (tick == 255 + steelDelayTicks)
            {
                Check(!f.Player.Position.InCircle(f.Center, 13.1f), $"steel hit or insufficient clearance at {position}");
                if (effectReceived) f.Engine.Effect(steelHelper, new() { Action = new(48720), GlobalSequence = 1 });
                f.Engine.FinishCast(steelHelper, 48720);
            }
            if (tick == 320)
                f.Engine.Cast(ringHelper, new() { Action = new(48721), FinishAt = epoch.AddSeconds(7.1), LocXZ = f.Center });
            if (tick == 355)
            {
                Check(f.Player.Position.InCircle(f.Center, 5.75f), $"ring hit or insufficient clearance at {position}; latest={((SafeZoneSolution?)latest.GetValue(movement))?.Status}");
                f.Engine.Effect(ringHelper, new() { Action = new(48721), GlobalSequence = 2 });
                f.Engine.FinishCast(ringHelper, 48721);
            }
            Check(armedMine == null || !f.Player.Position.InCircle(armedMine.Position, CrucibleMiasma.ContactRadius), "steel/ring route crossed an armed mine");
            Check(f.Engine.Module!.Arena.InBounds(f.Player.Position), "steel/ring route left the arena");
            if (tick == 0 || tick % 5 == phase) f.Render();
            var snapshot = f.Snapshot;
            var player = new PlayerState(position, 0, 6, false, false, 0, true, snapshot.Version);
            if (pending != null && t >= ready) { latest.SetValue(movement, pending); pending = null; }
            var accepted = (SafeZoneSolution?)latest.GetValue(movement);
            if (pending == null && t >= nextSolve && !(bool)valid.Invoke(movement, [accepted, snapshot, player, now])!)
            {
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), new(), now, accepted);
                ready = t + delay;
                nextSolve = t + 0.1;
                if (delay == 0)
                {
                    latest.SetValue(movement, accepted = pending);
                    pending = null;
                }
            }
            dispatch.Invoke(movement, [accepted, player, snapshot, now]);
            if (move.IsRunning && !wasMoving) movementReady = t + startup;
            if (t >= movementReady) position = (Vector3)advance.Invoke(move, [position, 0.02f])!;
            wasMoving = move.IsRunning;
        }
        Check(f.Snapshot.GoalAreas.Count == 0, "ring resolution retained its movement goal");
    }

    private static void GargoyleChargeMovement(bool optimize, float start, double delay, float angle = 0,
        bool effectReceived = true, float landingDistance = 0)
    {
        using var f = new Fight(14608, new(120, 0), 0.5f);
        f.Boss.OID = 19650;
        var facing = angle.Degrees();
        var landing = f.Center + new WDir(-0.1f, 0.9f) + landingDistance * facing.ToDirection();
        f.Boss.Position = landing;
        f.Player.Position = landing + start * facing.ToDirection();
        if (landingDistance > 0)
            Check(!f.Engine.Module!.Arena.InBounds(landing + 4 * facing.ToDirection()),
                "boundary fixture must clip the second refuge's outer edge");
        var epoch = f.World.CurrentTime;
        f.Engine.Effect(f.Boss, new() { Action = new(50932), GlobalSequence = 100, TargetXZ = landing, Rotation = facing });
        const BindingFlags binding = BindingFlags.Instance | BindingFlags.NonPublic;
        var (movement, move) = ((object, PromeRotation.GreenMoveSystem.IGreenMoveSystem))typeof(LateDodgeRegression)
            .GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var type = movement.GetType();
        type.GetField("_manager", binding)!.SetValue(movement, f.SafeZone.Manager);
        var setting = new SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true };
        type.GetField("_setting", binding)!.SetValue(movement, setting);
        var latest = type.GetField("_latestSolution", binding)!;
        var dispatch = type.GetMethod("Dispatch", binding)!;
        var valid = type.GetMethod("IsSolutionValid", binding)!;
        var advance = move.GetType().GetMethod("Advance")!;
        SafeZoneSolution? pending = null;
        var position = f.Player.Position.ToVec3();
        double ready = 0, nextSolve = 0, movementReady = 0, slowestSolve = 0;
        var wasMoving = false;
        var trace = new List<string>();
        var cleave = new AOEShapeCone(60, 90f.Degrees());
        for (var tick = 0; tick <= 550; ++tick)
        {
            var t = tick * 0.01;
            var now = epoch.AddSeconds(t);
            f.World.CurrentTime = now;
            f.Player.Position = WPos.FromVec3(position);
            setting.AttackUptimeExpiresAt = optimize ? now.AddSeconds(0.5) : DateTime.MinValue;
            if (tick is 280 or 485)
            {
                var rotation = tick == 280 ? facing : facing + 180f.Degrees();
                Check(!cleave.Check(f.Player.Position, landing, rotation),
                    $"cleave {tick} hit at {position}; {string.Join("; ", trace)}");
                if (effectReceived || tick == 485)
                    f.Engine.Effect(f.Boss, new() { Action = new(48718), GlobalSequence = tick == 280 ? 101u : 102u, Rotation = rotation });
            }
            Check(f.Engine.Module!.Arena.InBounds(f.Player.Position), "charge cleave route left the arena");
            if (tick % 10 == 0 || tick == 485) f.Render();
            var snapshot = f.Snapshot;
            var player = new PlayerState(position, 0, 6, false, false, 0, true, snapshot.Version);
            if (pending != null && t >= ready) { latest.SetValue(movement, pending); pending = null; }
            var accepted = (SafeZoneSolution?)latest.GetValue(movement);
            if (pending == null && t >= nextSolve && !(bool)valid.Invoke(movement, [accepted, snapshot, player, now])!)
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), new(), now, accepted);
                timer.Stop();
                slowestSolve = Math.Max(slowestSolve, timer.Elapsed.TotalSeconds);
                // Count actual solver cost too: a fixed artificial delay hid the
                // multi-second search that kept the live second cleave from moving.
                ready = t + Math.Max(delay, timer.Elapsed.TotalSeconds);
                nextSolve = t + 0.1;
            }
            dispatch.Invoke(movement, [accepted, player, snapshot, now]);
            if (tick % 20 == 0)
                trace.Add($"{t:F2}:pos={position},phase={type.GetProperty("PhaseText")!.GetValue(movement)},solution={accepted?.Status},temporal={accepted?.UsesTemporalPlanning},running={move.IsRunning}");
            if (move.IsRunning && !wasMoving) movementReady = t + 0.08;
            if (t >= movementReady) position = (Vector3)advance.Invoke(move, [position, 0.01f])!;
            wasMoving = move.IsRunning;
        }
        Check(f.Snapshot.GoalAreas.Count == 0, "charge cleave resolution retained its movement goal");
        Console.WriteLine($"INFO charge cleaves: slowest solve={slowestSolve * 1000:F1}ms");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Fight : IDisposable
    {
        public readonly WorldState World = new() { CurrentTime = new(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc) };
        public readonly ScriptSafeZoneApi SafeZone = new();
        public readonly CrucibleEngine Engine;
        public readonly CrucibleOutput Output;
        public readonly Actor Boss, Player;
        private ulong _nextID = 100;
        public WPos Center => Engine.Module!.Center;
        public Fight(uint name, WPos position, float dangerMargin = 0)
        {
            Boss = new() { InstanceID = 1, OID = 19000, NameID = name, Position = position, HPMP = (100,100), Type = ActorType.Enemy, IsTargetable = true, InCombat = true };
            Player = new() { InstanceID = 2, Position = position, HPMP = (100,100), Type = ActorType.Player, IsTargetable = true };
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player = Player;
            Engine = new(World, 1, 45); Engine.SelectGroup();
            SafeZone.DangerMarginForTest = dangerMargin;
            Output = new(SafeZone, new ScriptDrawApi(), SafeZone.Manager, _ => { }, () => dangerMargin);
        }
        public Actor Spawn(uint oid, WPos position, ActorType type)
        {
            var actor = new Actor { InstanceID = _nextID++, OID = oid, Position = position, Type = type, HPMP = (100,100) };
            World.Actors.Add(actor); Engine.Dispatch(c => c.OnActorCreated(actor)); return actor;
        }
        public CrucibleFrame EmptyFrame()
        {
            var frame = new CrucibleFrame();
            frame.Hints.PathfindMapBounds = Engine.Module!.Arena.Bounds;
            return frame;
        }
        public void Apply(CrucibleFrame frame)
        {
            // Goals and damage must share the simulated clock even when solving is slower than real time.
            SafeZone.UtcNowForTest = World.CurrentTime;
            Output.Apply(Engine, frame, 0, true, true, true);
        }
        public void Render() { Engine.Update(); Apply(Engine.Frame()); }
        public SafeZoneSnapshot Snapshot => SafeZone.Manager.BuildSnapshot(World.CurrentTime);
        public SafeZoneSolution Solve()
        {
            var snapshot = Snapshot;
            return new SafeZoneSolver().Solve(snapshot, new PlayerState(Player.Position.ToVec3(), 0, 6, false, false, 0, true, snapshot.Version),
                new MovementCapability(6,6,0,false,6,6), new SolveOptions(), World.CurrentTime);
        }
        public void Dispose() { Output.Clear(); SafeZone.Dispose(); }
    }
}

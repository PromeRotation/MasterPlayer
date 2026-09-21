using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Mechanics.Crucible.Components;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class BmrLocalSyncRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("optional host bridge tolerates missing and incompatible capabilities", () =>
        {
            foreach (var type in new[] { typeof(string), typeof(IncompatibleHost) })
            {
                var messages = new List<string>();
                var bridge = new CrucibleHostControl(messages.Add, type);
                var plan = new CrucibleControlPlan(3, 0, [4], null, false, false, false);
                bridge.Apply(plan); bridge.Apply(plan); bridge.Clear();
                Check(messages.Count == 1, "missing host did not degrade with one warning");
            }
        });
        run("optional host bridge isolates owners and clears only its own controls", () =>
        {
            CompatibleHost.Requests.Clear();
            var a = new CrucibleHostControl(_ => {}, typeof(CompatibleHost));
            var b = new CrucibleHostControl(_ => {}, typeof(CompatibleHost));
            a.Apply(new(3, 0, [4], 90.Degrees(), true, true, true));
            b.Apply(new(5, 0, [], null, false, false, false));
            Check(CompatibleHost.Requests.Count == 2 && CompatibleHost.Requests.Values.Any(p => p.Target == 0 && p.CancelCast && p.Heading != null), "facing control or immediate target release lost");
            a.Clear();
            Check(CompatibleHost.Requests.Count == 1 && CompatibleHost.Requests.Values.Single().Target == 0, "clear removed other script controls or left a target lock");
            b.Clear();
        });
        foreach (var fixture in new[] { "eye-fixture", "gaze-fixture" })
            run($"B17 recorded {fixture} retains and releases each eye independently", () => ReplayEyes(fixture));
        run("B30 narrow landing snapshots reuse nonzero heights and invalidate on pool changes", () =>
        {
            using var f = new Fight(14624, 19666, new(120, -420));
            var helper = f.Spawn(9020, new(106, -412));
            f.Cast(helper, 48805, helper.Position, 8);
            var first = f.Frame.Hints.ForbiddenZones.Last().Shape;
            var geometry = first.Geometry(7.25f);
            var next = f.Frame.Hints.ForbiddenZones.Last().Shape;
            Check(ReferenceEquals(first, next) && ReferenceEquals(geometry, next.Geometry(7.25f)), "unchanged frame rebuilt the fine landing grid");
            f.Spawn(2002331, new(130, -420));
            Check(!ReferenceEquals(first, f.Frame.Hints.ForbiddenZones.Last().Shape), "new pool retained a stale landing snapshot");
            f.Engine.FinishCast(helper, 48805, true);
            Check(f.Frame.Hints.GoalPoints.Count == 0, "canceled knockback retained its staging goal");
        });
        foreach (var fixture in new[] { "fury-613837", "fury-713412" })
            foreach (var delay in new[] { 0.15, 0.30 })
                run($"B30 recorded {fixture} three knockbacks with {delay * 1000:F0}ms delay", () => ReplayFury(fixture, delay));
        run("B03 moving ice matches impact positions across reused helpers", () =>
        {
            using var f = new Fight(14535, 19338, new(120, 0));
            var first = f.Spawn(9020, new(123.5f, -17.5f));
            f.Cast(first, 46889, first.Position, 6);
            var ice = f.Component<CrucibleVoidBlizzard>();
            Check(ice.ActiveAOEs(0, f.Player).Length == 2, "missing early next-step warning");
            Check(f.Component<XBMB03AOE>().ActiveAOEs(0, f.Player).Length == 0, "duplicate generic warning");
            f.Hit(first, 46889, 1);
            var next = f.Spawn(9020, new(123.5f, -10.5f));
            f.Hit(next, 46890, 2); f.Hit(next, 46890, 2);
            Check(ice.ActiveAOEs(0, f.Player)[0].Origin == new WPos(123.5f, -3.5f), "effect helper identity or duplicate advanced the wrong step");
            f.World.CurrentTime = f.World.FutureTime(4);
            Check(ice.ActiveAOEs(0, f.Player).Length == 0, "missing effect did not expire");
            next.Position = new(123.5f, 17.5f); f.Hit(next, 46890, 3);
            Check(ice.ActiveAOEs(0, f.Player).Length == 0, "ice continues outside the arena");
        });

        run("B08 dark ray keeps the edge goal after damage until an orb appears", () =>
        {
            using var f = Taurus();
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player, 234, f.Player.InstanceID));
            var goal = f.Frame.Hints.GoalPoints.Single().Position;
            f.Hit(f.Boss, 48145, 1);
            Check(f.Frame.Hints.GoalPoints.Single().Position == goal, "ray damage released bait too early");
            f.Spawn(19541, goal);
            Check(f.Frame.Hints.GoalPoints.Count == 0, "orb appearance retained edge bait");
            Check(f.Component<CrucibleTaurusExplosions>().ActiveAOEs(0, f.Player).Length == 1, "spawn missed early warning");
            Check(f.Engine.Module!.Field!.SafeBounds.Contains(new(19.5f, 14.5f)), "wrong Taurus rectangle");
        });

        run("B08 orb grows only after four pulses and finished casts stay resolved", () =>
        {
            using var f = Taurus();
            var orb = f.Spawn(19541, f.Center);
            var explosions = f.Component<CrucibleTaurusExplosions>();
            for (uint i = 0; i < 12; ++i)
            {
                var pending = explosions.ActiveAOEs(0, f.Player)[0];
                Check(pending.Shape is AOEShapeCircle circle && circle.Radius == 6 * (1 + i / 4), "wrong expansion stage");
                f.Hit(orb, 48146 + i / 4, 10 + i); f.Hit(orb, 48146 + i / 4, 10 + i);
            }
            f.Cast(orb, 48148, orb.Position, 1.5);
            Check(explosions.ActiveAOEs(0, f.Player).Length == 0, "stale final cast resurrected orb");
        });

        run("B08 staggered gazes turn away only for the imminent pair", () =>
        {
            using var f = Taurus();
            var a = f.Spawn(9020, f.Center + new WDir(-18, -13));
            var b = f.Spawn(9020, f.Center + new WDir(18, -13));
            var c = f.Spawn(9020, f.Center + new WDir(-18, 13));
            f.Cast(a, 48153, a.Position, 5); f.Cast(b, 48153, b.Position, 5); f.Cast(c, 48153, c.Position, 9);
            Check(f.Frame.Hints.ForbiddenDirections.Count == 2 && f.Plan.Heading == null, "gaze started too early or combined opposite waves");
            f.World.CurrentTime = f.World.FutureTime(4);
            var plan = f.Plan;
            Check(plan.Heading is { } heading && heading.ToDirection().Z > 0.9f && plan.HoldActions && plan.CancelCast, "no safe south-facing hold for the north pair");
            f.Hit(a, 48153, 1); f.Hit(b, 48153, 2);
            Check(f.Plan.Heading == null, "first pair facing persisted before second pair");
            f.World.CurrentTime = f.World.FutureTime(4);
            Check(f.Plan.Heading is { } second && second.ToDirection().Z < 0, "second pair did not turn north");
            f.Engine.FinishCast(c, 48153, true);
            Check(f.Plan.Heading == null && !f.Plan.HoldActions, "canceled gaze kept actions suppressed");
        });

        run("B15 fish shell body order includes protection before shell creation", () =>
        {
            using var f = new Fight(14569, 19603, new(120, 0));
            var fish = f.Spawn(19605, f.Center + new WDir(3, 0));
            var shell = f.Spawn(19604, f.Center);
            fish.Statuses = [new() { ID = 5434 }];
            f.Player.TargetID = f.Boss.InstanceID;
            f.Player.CastInfo = new() { TargetID = f.Boss.InstanceID };
            Check(f.Plan.Target == fish.InstanceID && f.Plan.CancelCast, "spikes or wrong current cast defeated fish priority");
            f.Engine.Destroy(fish, true); Check(f.Plan.Target == shell.InstanceID, "shell not selected after fish");
            f.Engine.Destroy(shell, true); f.Player.CastInfo = null;
            Check(f.Plan.Target == f.Boss.InstanceID, "unprotected body not selected");
            f.Boss.Statuses = [new() { ID = 2198 }];
            Check(f.Plan.Target == 0 && f.Plan.HoldActions, "protected body remains attackable");
            f.Player.TargetID = 0;
            Check(f.Plan.ForbiddenTargets.Contains(f.Boss.InstanceID) && f.Plan.HoldActions, "clearing target released protection policy");
            var regenerated = f.Spawn(19604, f.Center);
            Check(f.Plan.Target == regenerated.InstanceID && !f.Plan.HoldActions, "new shell did not resume attacks");
        });

        run("B17 helper fallback keeps the inner spot until effect and cancels short casts", () =>
        {
            using var f = new Fight(14577, 19611, new(120, -420));
            var helper = f.Spawn(9020, new(141, -420));
            f.Cast(helper, 48508, helper.Position, 0.5);
            var eye = f.Component<CrucibleCatoblepasEyes>();
            var safe = eye.ActiveAOEs(0, f.Player).ToArray().Single(a => !a.Risky);
            Check(safe.Origin == new WPos(138, -420), "wrong ring inner pocket");
            f.World.CurrentTime = f.World.FutureTime(0.25); f.Engine.FinishCast(helper, 48508);
            Check(eye.ActiveAOEs(0, f.Player).Length == 1, "normal cast end released inner spot");
            helper.Position = f.Center;
            f.Hit(helper, 48508, 1);
            Check(eye.ActiveAOEs(0, f.Player).Length == 0, "moved helper did not release its old warning");
            f.Cast(helper, 48508, new(120, -441), 0.5); f.Engine.FinishCast(helper, 48508, true);
            Check(eye.ActiveAOEs(0, f.Player).Length == 0, "short canceled cast survived");
        });

        run("B17 tether marks one gaze eye while explosion mechanics remain independent", () =>
        {
            using var f = new Fight(14577, 19611, new(120, -420));
            var a = f.Spawn(19612, new(99, -420)); var b = f.Spawn(19613, new(141, -420));
            f.Engine.Dispatch(c => c.OnTethered(b, new(195, f.Boss.InstanceID)));
            a.Position += new WDir(3, 0); b.Position += new WDir(-3, 0); f.Engine.Update();
            var gazes = f.Component<CrucibleCatoblepasGaze>().ActiveEyes(0, f.Player).ToArray();
            Check(gazes.Length == 1 && gazes[0].ActorID == b.InstanceID, "both eyes incorrectly require gaze avoidance");
            Check(f.Component<CrucibleCatoblepasEyes>().ActiveAOEs(0, f.Player).Length == 2, "gaze ownership removed other explosion");
        });

        run("B19 all staggered Siren explosions reach temporal navigation", () =>
        {
            using var f = new Fight(14583, 19617, new(120, -420));
            var a = f.Spawn(19618, f.Center + new WDir(-10, 0));
            f.World.CurrentTime = f.World.FutureTime(2);
            var b = f.Spawn(19618, f.Center + new WDir(10, 0));
            var hints = new AIHints(); f.Component<CrucibleSirenExplosions>().AddAIHints(0, f.Player, default, hints);
            Check(hints.ForbiddenZones.Count == 2 && hints.ForbiddenZones.All(z => z.ExpiresAt != null), "future wave missing or permanent");
            f.Hit(a, 48577, 1);
            Check(f.Component<CrucibleSirenExplosions>().ActiveAOEs(0, f.Player).ToArray().Single().ActorID == b.InstanceID, "first wave removed second");
        });

        run("B21 irregular floor and two-sided torrent reject burning notches", () =>
        {
            using var f = new Fight(14592, 19626, new(520, -420));
            Check(!f.Engine.Module!.Arena.InBounds(new(532.5f, -437.5f)), "old rectangular fire floor retained");
            f.Cast(f.Boss, 48607, f.Center, 5, 90);
            var kb = f.Component<CrucibleGatlerTorrent>();
            f.Player.Position = new(520, -421); var north = kb.ActiveKnockbacks(0, f.Player)[0].Direction.ToDirection();
            f.Player.Position = new(520, -419); var south = kb.ActiveKnockbacks(0, f.Player)[0].Direction.ToDirection();
            Check(north.Z < -0.99 && south.Z > 0.99, "torrent pushes only one direction");
            var hints = new AIHints(); kb.AddAIHints(0, f.Player, default, hints);
            Check(hints.ForbiddenZones.Single().ExpiresAt == null, "landing constraint ends before impact");
            f.Engine.FinishCast(f.Boss, 48607, true);
            Check(kb.ActiveKnockbacks(0, f.Player).Length == 0, "cancellation retained knockback");
        });

        run("B21 bait releases at helper lock and own thunder does not chase the player", () =>
        {
            using var f = new Fight(14592, 19626, new(520, -420));
            f.Engine.Dispatch(c => c.OnTethered(f.Boss, new(1, f.Player.InstanceID)));
            f.World.CurrentTime = f.World.FutureTime(2);
            Check(f.Frame.Hints.GoalPoints.Any(p => p.Position.X == 520), "missing long-axis bait point");
            var helper = f.Spawn(9020, f.Center); f.Cast(helper, 48614, f.Center, 2);
            Check(f.Frame.Hints.GoalPoints.Count == 0, "locked line retained bait");
            f.Hit(helper, 48614, 1); f.Cast(f.Boss, 48620, f.Center, 5, target: f.Player.InstanceID);
            var thunder = f.Component<CrucibleGatlerThunder>();
            Check(!thunder.ActiveAOEs(0, f.Player)[0].Risky, "own tankbuster treated as dodgable ground line");
            var hints = new AIHints(); thunder.AddAIHints(0, f.Player, default, hints);
            Check(hints.ForbiddenZones.Count == 0, "own thunder creates self-chasing danger");
        });

        run("B21 fire knife fixes drop then encourages distance without a full-arena danger", () =>
        {
            using var f = new Fight(14592, 19626, new(520, -420));
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player, 669, f.Player.InstanceID));
            var drop = f.Frame.Hints.GoalPoints.Single().Position;
            Check(CrucibleLaudaFloor.Contains(drop), "knife bait on burning floor");
            var helper = f.Spawn(9020, drop); f.Cast(helper, 48618, drop, 1);
            Check(f.Frame.Hints.GoalPoints.Count == 0 && f.Frame.Hints.GoalZones.Count > 0, "locked drop does not release placement goal");
            Check(!f.Frame.Aoes.Any(a => a.Aoe.Shape is AOEShapeCircle { Radius: 80 }), "falloff attack became unavoidable danger");
        });

        foreach (var circle in new[] { false, true })
            run($"B28 golem {(circle ? "circle" : "ring")} handoff belongs to receiving golem", () =>
            {
                using var f = new Fight(14617, 19660, new(520, 0));
                var receiver = f.Spawn(19660, f.Center + new WDir(10, 0));
                var proxy = f.Spawn(9020, f.Center);
                f.Cast(proxy, circle ? 48760u : 48759u, proxy.Position, 2);
                f.Hit(proxy, 48761, 1, receiver.InstanceID);
                var jump = f.Component<CrucibleGolemJump>();
                Check(jump.ActiveAOEs(0, f.Player)[0].Origin == receiver.Position, "prediction follows stale helper position");
                f.Engine.Destroy(proxy, true);
                Check(jump.ActiveAOEs(0, f.Player).Length == 1, "dead sender erased receiver warning");
                Check((jump.ActiveAOEs(0, f.Player)[0].Shape is AOEShapeCircle) == circle, "wrong handoff variant");
                f.Engine.Destroy(receiver, true);
                Check(jump.ActiveAOEs(0, f.Player).Length == 0, "dead receiver retained warning");
            });

        run("B30 meltdown prepares before bind and retains the landing through impact", () =>
        {
            using var f = new Fight(14624, 19666, new(120, -420));
            f.Player.Position = f.Center + new WDir(0, 3);
            f.Engine.Dispatch(c => c.OnEventIcon(f.Player, 412, f.Player.InstanceID));
            var meltdown = f.Component<CrucibleBombMeltdown>();
            Check(meltdown.ActiveAOEs(0, f.Player).Length == 1 && !meltdown.ActiveAOEs(0, f.Player)[0].Risky, "marked line absent or self-chasing");
            var hints = new AIHints(); f.Component<CrucibleBombMeltdownKnockback>().AddAIHints(0, f.Player, default, hints);
            Check(Math.Abs((hints.ForbiddenZones.Single().Activation - f.World.FutureTime(6.7)).TotalSeconds) < 0.001 && hints.ForbiddenZones[0].ExpiresAt == null, "setup deadline does not precede bind");
            f.Hit(f.Boss, 48800, 1, f.Player.InstanceID); var rotation = meltdown.ActiveAOEs(0, f.Player)[0].Rotation;
            f.Player.Position = f.Center + new WDir(3, 0);
            Check(meltdown.ActiveAOEs(0, f.Player)[0].Rotation == rotation, "bound line did not lock");
            f.Hit(f.Boss, 48801, 2);
            Check(meltdown.ActiveAOEs(0, f.Player).Length == 0, "meltdown impact retained constraint");
        });

        foreach (var aid in new uint[] { 48150, 48159, 48161, 48163, 48165, 48512, 48577, 48594, 48595, 48596, 48600, 48618, 48620, 48765, 48827 })
            run($"new action {aid} suppresses generic duplicate and retains exact fallback", () =>
            {
                Check(CrucibleActionCatalog.IsClaimed(aid), "unclaimed action");
                Check(CrucibleActionCatalog.TryGetShape(aid, out _), "missing generic shape fallback");
            });

        foreach (var delay in new[] { 0.15, 0.30 })
        run($"B08 ARR late donut and orb escape with {delay * 1000:F0}ms dispatch delay", () =>
        {
            using var f = Taurus();
            f.Player.Position = new(511, 3);
            var ring = f.Spawn(9020, f.Center);
            var orb = f.Spawn(19541, new(530.1442f, 3.9216f));
            f.Cast(ring, 48161, new(519.9817f, -0.0153f), 4);
            f.Cast(orb, 48148, orb.Position, 4.709);
            var epoch = f.World.CurrentTime;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var (movement, move) = ((object, PromeRotation.GreenMoveSystem.IGreenMoveSystem))typeof(LateDodgeRegression)
                .GetMethod("CreateMovement", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!;
            var latest = movement.GetType().GetField("_latestSolution", flags)!;
            var dispatch = movement.GetType().GetMethod("Dispatch", flags)!;
            var advance = move.GetType().GetMethod("Advance")!;
            SafeZoneSolution? pending = null;
            double ready = 0;
            var trace = new List<string>();
            for (var tick = 0; tick <= 471; ++tick)
            {
                var t = tick * 0.01;
                f.World.CurrentTime = epoch.AddSeconds(t);
                if (tick == 400)
                {
                    Check((f.Player.Position - new WPos(519.9817f, -0.0153f)).Length() < 7,
                        "donut hit: " + f.Player.Position);
                    f.Hit(ring, 48161, 51);
                }
                f.Render();
                var snapshot = f.SafeZone.Manager.BuildSnapshot(f.World.CurrentTime);
                if (pending != null && t + 1e-6 >= ready) { latest.SetValue(movement, pending); pending = null; }
                var solution = (SafeZoneSolution?)latest.GetValue(movement);
                if (pending == null) { pending = f.Solve(solution); ready = t + delay; }
                dispatch.Invoke(movement, [solution, new PlayerState(f.Player.Position.ToVec3(), 0, 6, false, false, 0, true, snapshot.Version), snapshot, f.World.CurrentTime]);
                if (tick % 10 == 0) trace.Add($"{t:F2}:{f.Player.Position}/{solution?.Status}/{move.IsRunning}");
                if (tick == 471)
                    Check((f.Player.Position - orb.Position).Length() > 18,
                        "orb hit: " + string.Join("; ", trace.TakeLast(15)));
                f.Player.Position = WPos.FromVec3((System.Numerics.Vector3)advance.Invoke(move, [f.Player.Position.ToVec3(), 0.01f])!);
                Check(snapshot.ArenaBoundary!.Contains(f.Player.Position.ToVec3()), "escape left arena");
            }
        });
    }

    private static Fight Taurus() => new(14546, 19540, new(520, 0));

    private static void ReplayEyes(string fixture)
    {
        using var f = new Fight(14577, 19611, new(120, -420));
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Crucible", fixture + ".json")));
        var epoch = f.World.CurrentTime;
        var actors = new Dictionary<string, Actor> { ["boss"] = f.Boss };
        uint sequence = 0;
        var explosions = 0; var gazes = 0;
        foreach (var e in json.RootElement.EnumerateArray())
        {
            f.World.CurrentTime = epoch.AddSeconds(e.GetProperty("t").GetDouble());
            var key = e.GetProperty("key").GetString()!;
            var pos = new WPos(e.GetProperty("x").GetSingle(), e.GetProperty("z").GetSingle());
            var kind = e.GetProperty("kind").GetString();
            if (kind == "spawn") actors[key] = f.Spawn(e.GetProperty("oid").GetUInt32(), pos);
            else
            {
                if (!actors.TryGetValue(key, out var actor)) actors[key] = actor = f.Spawn(9020, pos);
                actor.Position = pos;
                if (kind == "tether") f.Engine.Dispatch(c => c.OnTethered(actor, new(195, f.Boss.InstanceID)));
                else if (kind == "cast") f.Cast(actor, e.GetProperty("aid").GetUInt32(), pos, e.GetProperty("duration").GetDouble());
                else if (kind == "effect")
                {
                    var aid = e.GetProperty("aid").GetUInt32();
                    if (aid == 48508)
                    {
                        Check(f.Component<CrucibleCatoblepasEyes>().ActiveAOEs(0, f.Player).ToArray().Any(a => !a.Risky), "inner pocket disappeared before recorded explosion");
                        ++explosions;
                    }
                    if (aid == 48510)
                    {
                        Check(f.Component<CrucibleCatoblepasGaze>().ActiveEyes(0, f.Player).Length > 0, "recorded gaze has no active eye");
                        Check(f.Plan.Heading != null && f.Plan.HoldActions, "recorded gaze has no facing control");
                        ++gazes;
                    }
                    f.Hit(actor, aid, ++sequence);
                }
            }
            f.Engine.Update();
        }
        Check(explosions >= 3 && (fixture == "eye-fixture" ? gazes == 0 : gazes == 2), "fixture coverage changed");
        f.World.CurrentTime = f.World.FutureTime(60);
        Check(f.Component<CrucibleCatoblepasEyes>().ActiveAOEs(0, f.Player).Length == 0 && f.Plan.Heading == null, "recorded eyes never expire");
    }

    private static void ReplayFury(string fixture, double delay)
    {
        using var f = new Fight(14624, 19666, new(120, -420));
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Crucible", fixture + ".json")));
        var root = json.RootElement;
        f.Player.Position = new(root.GetProperty("player")[0].GetSingle(), root.GetProperty("player")[1].GetSingle());
        WPos Pos(System.Text.Json.JsonElement e) => new(e[0].GetSingle(), e[2].GetSingle());
        var pools = root.GetProperty("pools").EnumerateArray().Select(e => (Actor: f.Spawn(2002331, Pos(e.GetProperty("position"))),
            End: e.GetProperty("end").ValueKind == System.Text.Json.JsonValueKind.Null ? double.PositiveInfinity : e.GetProperty("end").GetDouble())).ToArray();
        var events = root.GetProperty("events").EnumerateArray().ToArray();
        var actors = new Dictionary<int, Actor>();
        var epoch = f.World.CurrentTime;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var (movement, move) = ((object, PromeRotation.GreenMoveSystem.IGreenMoveSystem))typeof(LateDodgeRegression)
            .GetMethod("CreateMovement", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, null)!;
        var latest = movement.GetType().GetField("_latestSolution", flags)!;
        var dispatch = movement.GetType().GetMethod("Dispatch", flags)!;
        var advance = move.GetType().GetMethod("Advance")!;
        SafeZoneSolution? pending = null;
        double ready = 0, flightUntil = 0;
        var index = 0; var hits = 0; uint sequence = 0;
        var trace = new List<string>();
        for (var tick = 0; tick <= 1450; ++tick)
        {
            var t = tick * 0.01;
            f.World.CurrentTime = epoch.AddSeconds(t);
            foreach (var pool in pools) if (t >= pool.End) pool.Actor.EventState = 7;
            while (index < events.Length && events[index].GetProperty("t").GetDouble() <= t)
            {
                var e = events[index++]; var id = e.GetProperty("id").GetInt32(); var aid = e.GetProperty("aid").GetUInt32();
                var pos = Pos(e.GetProperty("position"));
                if (!actors.TryGetValue(id, out var actor)) actors[id] = actor = f.Spawn(9020, pos);
                actor.Position = pos;
                if (e.GetProperty("kind").GetString() == "cast")
                    f.Cast(actor, aid, Pos(e.GetProperty("location")), e.GetProperty("duration").GetDouble(), e.GetProperty("rotation").GetSingle());
                else
                {
                    if (aid == 48827) Check(!f.Player.Position.InCircle(pos, 6), $"{fixture} core hit at {t:F2}: {f.Player.Position}; " + string.Join("; ", trace.Where((_, i) => i % 5 == 0)));
                    else
                    {
                        var kb = f.Component<CrucibleBombFuryKnockback>();
                        Check(kb.ActiveKnockbacks(0, f.Player).Length > 0, "next knockback released before effect");
                        var landing = f.Player.Position + (f.Player.Position - pos).Normalized() * 15;
                        Check(!kb.DestinationUnsafe(0, f.Player, landing), $"{fixture} unsafe landing at {t:F2}: {f.Player.Position} -> {landing}; " + string.Join("; ", trace.Where((_, i) => i % 5 == 0)));
                        f.Player.Position = landing; flightUntil = t + 0.75; ++hits;
                        pending = null; latest.SetValue(movement, null); move.Stop();
                    }
                    f.Hit(actor, aid, ++sequence);
                }
            }
            if (tick % 10 == 0) f.Render();
            var snapshot = f.SafeZone.Manager.BuildSnapshot(f.World.CurrentTime);
            Check(snapshot.ArenaBoundary!.Contains(f.Player.Position.ToVec3()), "knockback left arena");
            if (t < flightUntil) continue;
            if (pending != null && t + 1e-6 >= ready) { latest.SetValue(movement, pending); pending = null; }
            var solution = (SafeZoneSolution?)latest.GetValue(movement);
            if (tick % 10 == 0) trace.Add($"{t:F2}:{f.Player.Position}/{solution?.Status}/{move.IsRunning}");
            if (pending == null) { pending = f.Solve(solution); ready = t + delay; }
            dispatch.Invoke(movement, [solution, new PlayerState(f.Player.Position.ToVec3(), 0, 6, false, false, 0, true, snapshot.Version), snapshot, f.World.CurrentTime]);
            f.Player.Position = WPos.FromVec3((System.Numerics.Vector3)advance.Invoke(move, [f.Player.Position.ToVec3(), 0.01f])!);
        }
        Check(hits == 3 && f.Component<CrucibleBombFuryKnockback>().ActiveKnockbacks(0, f.Player).Length == 0, "three impacts did not clean up");
    }

    public static class IncompatibleHost
    {
        public static int Update(string owner, ulong target, ulong clearTarget, ulong[] forbidden, float? heading, bool hold, bool block, bool cancel) => 0;
        public static void Clear(string owner) => throw new Exception("incompatible capability must not be called");
    }
    public static class CompatibleHost
    {
        public static readonly Dictionary<string, CrucibleControlPlan> Requests = [];
        public static void Update(string owner, ulong target, ulong clearTarget, ulong[] forbidden, float? heading, bool hold, bool block, bool cancel)
            => Requests[owner] = new(target, clearTarget, forbidden, heading is { } a ? new Angle(a) : null, hold, block, cancel);
        public static void Clear(string owner) => Requests.Remove(owner);
    }
    private sealed class Fight : IDisposable
    {
        public readonly WorldState World = new() { CurrentTime = DateTime.UtcNow.AddMinutes(1) };
        public readonly ScriptSafeZoneApi SafeZone = new() { DangerMarginForTest = 0.5f };
        public readonly ScriptDrawApi Draw = new();
        public readonly CrucibleEngine Engine;
        public readonly CrucibleOutput Output;
        public readonly Actor Boss, Player;
        public readonly WPos Center;
        private ulong _id = 100;
        public CrucibleFrame Frame { get { Engine.Update(); return Engine.Frame(); } }
        public CrucibleControlPlan Plan => CrucibleControlPlan.Build(Frame.Hints, Player, World.CurrentTime);
        public Fight(uint name, uint oid, WPos center)
        {
            Center = center;
            Boss = new() { InstanceID = 1, OID = oid, NameID = name, Position = center, Type = ActorType.Enemy, HPMP = (100,100), IsTargetable = true, InCombat = true };
            Player = new() { InstanceID = 2, Position = center, Type = ActorType.Player, HPMP = (100,100), IsTargetable = true };
            World.Actors.Add(Boss); World.Actors.Add(Player); World.Player = Player;
            Engine = new(World, 1, 45); Engine.SelectGroup();
            Output = new(SafeZone, Draw, SafeZone.Manager, _ => {}, () => 0.5f);
        }
        public T Component<T>() where T : BossComponent => Engine.Module!.FindComponent<T>()!;
        public Actor Spawn(uint oid, WPos position)
        {
            var a = new Actor { InstanceID = _id++, OID = oid, Position = position, Type = ActorType.Enemy, HPMP = (100,100), IsTargetable = true };
            World.Actors.Add(a); Engine.Dispatch(c => c.OnActorCreated(a)); return a;
        }
        public void Cast(Actor a, uint aid, WPos position, double delay, float degrees = 0, ulong target = 0)
            => Engine.Cast(a, new() { Action = new(aid), LocXZ = position, Rotation = degrees.Degrees(), FinishAt = World.FutureTime(delay), TargetID = target });
        public void Hit(Actor a, uint aid, uint sequence, ulong target = 0) => Engine.Effect(a, new() { Action = new(aid), GlobalSequence = sequence, MainTargetID = target });
        public void Render() => Output.Apply(Engine, Frame, 0, true, true, true);
        public SafeZoneSolution Solve(SafeZoneSolution? previous = null)
        {
            var snapshot = SafeZone.Manager.BuildSnapshot(World.CurrentTime);
            return new SafeZoneSolver().Solve(snapshot, new(Player.Position.ToVec3(), 0, 6, false, false, 0, true, snapshot.Version), new(6,6,0,false,6,6), new(), World.CurrentTime, previous);
        }
        public void Dispose() { Output.Clear(); SafeZone.Dispose(); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

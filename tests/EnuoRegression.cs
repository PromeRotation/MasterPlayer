using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Enuo;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class EnuoRegression
{
    private static readonly DateTime Epoch = new(2026, 11, 12, 14, 5, 2, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Enuo debug exposes all eight assignments without changing the local plan", () =>
        {
            var f = new Fight(); var debug = new EnuoPartyDebug(_ => 0);
            var party = EnuoStrategy.Roles.Select((role, i) => new ReplayPartyMember(i, role, (ulong)i + 1)).ToArray();
            debug.Synchronize(f.Actors.Values, party, f.Now);
            var local = new EnuoEventAdapter(f.Engine, _ => 0);
            for (var role = 0; role < 8; role++)
            {
                var cast = new LogSystemCastStartEvent { SourceId = (ulong)(200 + role), TargetId = (ulong)role + 1,
                    ActionId = 50042, CastTime = 8, EffectPosition = EnuoStrategy.Center };
                // Collect with the display off; the first snapshot must still have all assignments.
                local.Handle(cast, f.Now); debug.Handle(cast, f.Now);
            }
            var before = f.Plan(); var frame = debug.Snapshot(f.Now);
            var first = debug.ForPlayer(1);
            foreach (var id in new ulong[] { 1, 5, 2, 8, 1 })
            {
                var view = debug.ForPlayer(id)!;
                Check(view.Role == (int)id - 1 && view.Plan(f.Now).Goal?.Position == EnuoStrategy.Spread(view.Role),
                    "Perspective switching lost the role's earlier cast or reused another role's goal.");
            }
            Check(ReferenceEquals(first, debug.ForPlayer(1)) && debug.ForPlayer(0) == null && debug.ForPlayer(99) == null,
                "Switching rebuilt the encounter or selected a fallback role.");
            Check(frame.Targets.Length == 8 && frame.Markers.Length == 8, "Debug lost a role or collapsed distinct spread targets.");
            foreach (var target in frame.Targets)
                Check(target.Actor == (ulong)target.Role + 1 && target.Goal?.Position == EnuoStrategy.Spread(target.Role), "Debug spread did not use that role's field assignment.");
            Check(f.Plan().Goal == before.Goal && f.Plan().Target == before.Target && !f.Plan().HoldMovement,
                "Querying all debug roles changed the local player's control plan.");
            debug.Handle(new LogSystemStatusAddEvent { TargetId = 3, StatusId = 4562, Duration = 4 }, f.Now);
            frame = debug.Snapshot(f.Now);
            Check(frame.Targets[2].Goal == null && frame.Targets[2].State.Contains("停步") && frame.Markers.Length == 7,
                "A heat-held role still had an invented destination.");
            Check(f.Plan().Goal == before.Goal, "Other roles' debug status changed local planning.");
            debug.Reset();
            Check(debug.Snapshot(f.Now).Markers.Length == 0, "Debug reset leaked old targets.");
        });
        run("Enuo debug merges shared destinations and rejects missing roles or dead actors", () =>
        {
            var f = new Fight(); var debug = new EnuoPartyDebug(_ => 0);
            var party = EnuoStrategy.Roles.Select((role, i) => new ReplayPartyMember(i, role, (ulong)i + 1)).ToArray();
            debug.Synchronize(f.Actors.Values, party, f.Now);
            debug.Handle(new LogSystemCastStartEvent { SourceId = 100, ActionId = 50032, CastTime = 8 }, f.Now);
            var frame = debug.Snapshot(f.Now);
            Check(frame.Markers.Length == 4 && frame.Markers.All(m => m.Label.Split('/').Length == 2), "Shared pair goals must keep both role labels at the exact same destination.");
            f.Actors[1] = f.Actors[1] with { Dead = true };
            debug.Synchronize(f.Actors.Values, party, f.Now);
            frame = debug.Snapshot(f.Now);
            Check(frame.Targets[0].Goal == null && frame.Targets[0].State == "已死亡" && frame.Targets[6].Goal != null, "Dead MT retained a target or hid their living partner.");
            debug.Synchronize(f.Actors.Values, party.Take(7), f.Now);
            frame = debug.Snapshot(f.Now);
            Check(frame.Markers.Length == 0 && frame.Targets.All(t => t.State == "分工未确认"), "Incomplete roles guessed debug destinations.");
        });
        run("Enuo video tower assignment: all 36 layouts, both role groups and reversed event order", () =>
        {
            var layouts = 0;
            foreach (var left in Pairs([4, 5, 6, 7]))
                foreach (var right in Pairs([0, 1, 2, 3]))
                    foreach (var marked in new[] { new HashSet<int>([0, 1, 2, 3]), new HashSet<int>([4, 5, 6, 7]) })
                    {
                        var towers = left.Concat(right).ToHashSet();
                        var actual = EnuoStrategy.AssignTowers(towers, marked);
                        Check(actual.Length == 8 && actual.Distinct().Count() == 8, "Every player needs a unique tower or empty spot.");
                        Check(actual.SequenceEqual(EnuoStrategy.AssignTowers(towers.Reverse().ToHashSet(), marked.Reverse().ToHashSet())), "Event ordering changed assignment.");
                        for (var role = 0; role < 8; role++)
                        {
                            Check(towers.Contains(actual[role]) != marked.Contains(role), "Marked player was sent into a tower.");
                            Check((EnuoStrategy.Towers[actual[role]].X > 100) == (role % 2 == 1), "Video west/east assignment reversed.");
                        }
                        for (var side = 0; side < 2; side++)
                        {
                            Check(EnuoStrategy.Towers[actual[side]].Z < EnuoStrategy.Towers[actual[side + 2]].Z, "Tank must take the higher same-side spot.");
                            Check(EnuoStrategy.Towers[actual[side + 4]].Z < EnuoStrategy.Towers[actual[side + 6]].Z, "D1/D2 must take the higher same-side spot.");
                        }
                        layouts++;
                    }
            Check(layouts == 72 && EnuoStrategy.AssignTowers(new HashSet<int>([1, 2]), new HashSet<int>([0, 1, 2, 3])).Length == 0, "Partial input must not guess.");
        });
        run("Enuo tower target icons and staggered effects preserve each role until its own resolution", () =>
        {
            var f = new Fight(1); f.Cast(50010, 0);
            var adapter = new EnuoEventAdapter(f.Engine, _ => 0);
            var layout = new[] { 0, 3, 4, 7 };
            void Towers()
            {
                for (var i = 0; i < 4; i++)
                {
                    var source = (ulong)(200 + i); f.Actors[source] = Actor(source, 0x233C, EnuoStrategy.Towers[layout[i]]);
                    f.Scan(); f.Cast(50013, 0, source, 6.7f);
                }
            }
            Towers();
            foreach (ulong id in new ulong[] { 1, 2, 3, 4 })
                adapter.Handle(new LogSystemTargetIconEvent { SourceId = id, TargetId = id, MarkerId = 721 }, f.Now);
            var bait = f.Plan().Goal;
            Check(bait?.Name == "小怪：空位引导扇形", "TargetIcon 721 must send its target to an empty position.");
            f.Now = Epoch.AddSeconds(7.01);
            foreach (ulong source in new ulong[] { 200, 201, 202, 203 }) f.Engine.Effect(source, 50013, (uint)source, f.Now);
            Check(f.Plan().Goal?.Position == bait!.Position, "Tower effects released the marked bait before its later cone hit.");
            f.Now = f.Now.AddSeconds(.3); f.Engine.Effect(300, 50038, 300, f.Now);
            Check(f.Plan().Goal?.Position == bait.Position, "First cone hit cleared all four target icons prematurely.");
            f.Now = f.Now.AddSeconds(.1);
            foreach (ulong source in new ulong[] { 301, 302, 303 }) f.Engine.Effect(source, 50038, (uint)source, f.Now);
            Check(f.Plan().Goal == null, "Resolved bait retained its assignment.");
            f.Now = f.Now.AddSeconds(.1);
            adapter.Handle(new LogSystemTargetIconEvent { SourceId = 1, TargetId = 1, MarkerId = 721 }, f.Now);
            Check(f.Plan().Goal == null, "Duplicate old icon reopened a resolved tower wave.");
            f.Now = Epoch.AddSeconds(23); Towers();
            foreach (ulong id in new ulong[] { 5, 6, 7, 8 })
                adapter.Handle(new LogSystemTargetIconEvent { SourceId = id, TargetId = id, MarkerId = 721 }, f.Now);
            Check(f.Plan().Goal?.Name == "小怪：单人踩塔", "Second wave reused the first wave's marked players.");
            var soak = f.Plan().Goal!.Position;
            var mine = (ulong)(200 + Array.FindIndex(layout, i => Vector3.Distance(EnuoStrategy.Towers[i], soak) < .1f));
            var other = mine == 200 ? 201UL : 200UL;
            f.Now = Epoch.AddSeconds(30);
            f.Engine.Effect(other, 50013, 500, f.Now);
            Check(f.Plan().Goal?.Position == soak, "Another tower's hit released my still-unresolved tower.");
            f.Engine.Effect(mine, 50013, 501, f.Now);
            Check(f.Plan().Goal == null, "A resolved tower sent the player back to soak again.");
        });
        run("Enuo tower icons use explicit targets, deduplicate and block old-add movement while incomplete", () =>
        {
            var f = new Fight(); f.Cast(50010, 0);
            f.Actors[500] = Actor(500, 0x4DC8, EnuoStrategy.Towers[7]); f.Scan();
            f.Engine.Tether(500, 1, 284, f.Now);
            var adapter = new EnuoEventAdapter(f.Engine, _ => 0);
            adapter.Handle(new LogSystemTargetIconEvent { SourceId = 100, TargetId = 1, MarkerId = 721 }, f.Now);
            Check(f.Plan().Goal == null, "Incomplete icons sent the player back to their old add/tower.");
            foreach (ulong id in new ulong[] { 2, 3, 4 })
                adapter.Handle(new LogSystemTargetIconEvent { SourceId = id, MarkerId = 721 }, f.Now);
            for (var i = 0; i < 4; i++)
            {
                var source = (ulong)(200 + i); f.Actors[source] = Actor(source, 0x233C, EnuoStrategy.Towers[new[] { 0, 3, 4, 7 }[i]]);
                f.Scan(); f.Cast(50013, 0, source, 6.7f);
            }
            var goal = f.Plan().Goal;
            Check(goal?.Name == "小怪：空位引导扇形", "Source-only icon fallback lost a marked party member.");
            adapter.Handle(new LogSystemTargetIconEvent { SourceId = 1, TargetId = 1, MarkerId = 721 }, f.Now);
            Check(f.Plan().Goal == goal, "Duplicate TargetIcon changed a locked assignment.");
            f.Now = Epoch.AddSeconds(11); f.Scan();
            Check(f.Plan().Goal?.Name == "击杀自己的连线小怪", "Missing resolution left a permanent tower hold.");
        });
        run("Enuo passage cast locations distinguish center strips from offset side strips", () =>
        {
            foreach (var rotation in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2, MathF.PI / 4 })
            {
                var forward = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
                var right = new Vector3(MathF.Cos(rotation), 0, -MathF.Sin(rotation));
                var origin = EnuoStrategy.Center - forward * 40;
                var f = new Fight();
                f.Actors[201] = Actor(201, 0x4DC3, origin); f.Actors[202] = Actor(202, 0x233C, origin); f.Scan();
                f.Engine.StartCast(new(201, 49986, 201, origin - right * 16, rotation, f.Now, f.Now.AddSeconds(6)), true);
                f.Engine.StartCast(new(202, 49987, 202, origin + right * 16, rotation, f.Now, f.Now.AddSeconds(6)), true);
                var wings = f.Plan().Hazards;
                Check(wings.Count == 2 && !wings.Any(h => h.Contains(EnuoStrategy.Center)), "Side-strip packet locations collapsed to the center lane.");
                Check(wings.Any(h => h.Contains(EnuoStrategy.Center + right * 16)) && wings.Any(h => h.Contains(EnuoStrategy.Center - right * 16)), "Side strips are missing.");
                f.Engine.Reset(); f.Scan();
                f.Engine.StartCast(new(201, 49985, 201, origin, rotation, f.Now, f.Now.AddSeconds(7)), true);
                var center = f.Plan().Hazards.Single();
                Check(center.Contains(EnuoStrategy.Center) && !center.Contains(EnuoStrategy.Center + right * 16), "Center strip has inverted safe/danger positions.");
            }
        });
        run("Enuo heat and freeze follow observed status expiry and never leave stale movement", () =>
        {
            var f = new Fight();
            f.Status(1, 4562, 2);
            Check(f.Plan().HoldMovement && f.Plan().Goal == null, "Heat must suppress movement and its destination.");
            f.Now = Epoch.AddSeconds(2.01); f.Scan();
            Check(!f.Plan().HoldMovement, "Heat persisted past recorded expiry.");
            f.Status(1, 3523, 2);
            var p = f.Plan();
            Check(p.KeepMoving && p.BlockCasts && p.Goal != null, "Freeze must request continuous movement and cancel/block casts.");
            f.Engine.Reset();
            Check(!f.Plan().KeepMoving && !f.Plan().HoldMovement && f.Plan().Goal == null, "Reset retained player status or movement.");
        });
        run("Enuo actual flare targets override assumed tank roles", () =>
        {
            var f = new Fight(local: 8);
            f.Cast(50043, 0, duration: 5);
            f.Cast(50044, 8, source: 101, duration: 6);
            f.Cast(50044, 1, source: 102, duration: 6);
            Check(f.Plan().Goal!.Position.Z < 100, "A non-tank with the flare cannot remain in the south group.");
        });
        run("Enuo typed status events stop immediately and release on status removal", () =>
        {
            var f = new Fight(); var adapter = new EnuoEventAdapter(f.Engine, _ => null);
            adapter.Handle(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 4562, Duration = 2 }, f.Now);
            Check(f.Plan().HoldMovement, "Must not wait for the next world poll to stop on heat.");
            adapter.Handle(new LogSystemStatusRemoveEvent { TargetId = 1, StatusId = 4562 }, f.Now);
            Check(!f.Plan().HoldMovement, "Status loss must release the stop.");
            f.Engine.Effect(100, 50044, 1, f.Now);
            Check(f.Plan().KeepMoving, "Freeze effect must cover the gap before status delivery.");
            adapter.Handle(new LogSystemStatusRemoveEvent { TargetId = 1, StatusId = 3523 }, f.Now);
            Check(!f.Plan().KeepMoving, "Actual status loss must end the fallback freeze interval.");
        });
        run("Enuo adds select only own tether and request interrupt cleanse gaze and knockback tools", () =>
        {
            var f = new Fight(); f.Cast(50010, 0);
            f.Actors[201] = Actor(201, 0x4DC8, new(123, 0, 90));
            f.Actors[202] = Actor(202, 0x4DC9, new(77, 0, 90)); f.Scan();
            f.Engine.Tether(201, 1, 284, f.Now); f.Cast(50018, 201, source: 201);
            Check(f.Plan().Target == 201 && f.Plan().ForbiddenTargets.SequenceEqual([202UL]), "Must not attack another player's add.");
            Check(f.Plan().Actions.Any(a => a.Id == 7538 && a.Target == 201), "Own Drain Touch needs Interject.");
            f.Engine.RemoveActor(201); Check(f.Plan().Target == 0, "Removed add is still targeted.");
            var healer = new Fight(3); healer.Cast(50010, 0); healer.Status(3, 3943, 30);
            Check(healer.Plan().Actions.Any(a => a.Id == 7568 && a.Target == 3), "Healer must cleanse their own Disease.");
            f.Cast(49982, 0, duration: 4);
            Check(f.Plan().Actions.Any(a => a.Id == 7548), "Knockback needs Arm's Length for tank.");
            f.Actors[203] = Actor(203, 0x4DC9, new(100, 0, 112)); f.Scan(); f.Cast(50022, 0, source: 203, duration: 1);
            Check(f.Plan().Facing != null && f.Plan().HoldActions && f.Plan().BlockCasts, "Gaze needs facing and action suppression together.");
        });
        run("Enuo packet facing, cancellation, effect deduplication and clock rewind", () =>
        {
            var f = new Fight();
            var adapter = new EnuoEventAdapter(f.Engine, _ => MathF.PI / 2);
            adapter.Handle(new LogSystemCastStartEvent { SourceId = 100, ActionId = 49985, CastTime = 7, EffectPosition = EnuoStrategy.Center }, f.Now);
            var hazard = f.Plan().Hazards.Single();
            Check(hazard.Contains(new(120, 0, 100)) && !hazard.Contains(new(100, 0, 120)), "Packet facing was lost.");
            adapter.Handle(new LogSystemCancelCastEvent { SourceId = 100, ActionId = 49985 }, f.Now);
            Check(f.Plan().Hazards.Count == 0, "Cancelled cast remains hazardous.");
            f.Engine.Icon(1, 719, f.Now);
            for (uint sequence = 1; sequence <= 3; sequence++)
            {
                for (var target = 0; target < 8; target++) f.Engine.Effect(100, 50048, sequence, f.Now);
                Check((f.Plan().Goal != null) == (sequence < 3), "One multi-target packet advanced multiple hits.");
            }
            f.Cast(49979, 0); f.Engine.Tick(f.Now.AddSeconds(-1));
            Check(f.Engine.Casts.Count == 0, "Replay rewind retained a future cast.");
        });
        run("Enuo rotated boss-relative wild charges preserve video left/right", () =>
        {
            foreach (var direction in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 })
                foreach (var edgeAction in new uint[] { 49977, 49978 })
                    foreach (var bossAction in new uint[] { 0, 49979, 49980 })
                    {
                        var results = new List<Vector3>();
                        for (ulong local = 3; local <= 4; local++)
                        {
                            var f = new Fight(local);
                            f.Actors[101] = Actor(101, 0x4DC2, EnuoStrategy.Polar(direction, 40)); f.Scan();
                            f.Cast(edgeAction, 0, source: 101);
                            if (bossAction != 0) f.Cast(bossAction, 0);
                            f.Engine.Icon(3, 701, f.Now); f.Engine.Icon(4, 701, f.Now);
                            var plan = f.Plan();
                            Check(plan.Goal != null, $"No charge assignment for {direction}/{edgeAction}/{bossAction}.");
                            Check(!plan.Hazards.Any(h => h.Contains(plan.Goal!.Position)), "Charge assignment is inside steel/donut.");
                            results.Add(plan.Goal!.Position);
                        }
                        Check(Vector3.Distance(results[0], results[1]) >= 6.5f, "Healer charge lanes overlap.");
                        var midpoint = (results[0] + results[1]) / 2;
                        var clockwise = new Vector3(-(midpoint.Z - 100), 0, midpoint.X - 100);
                        Check(Vector3.Dot(results[0] - midpoint, clockwise) > 0, "MT group must be left when facing the boss.");
                    }
        });
        run("Enuo video reference frames: waymark assignments stay fixed while charge groups follow the safe side", () =>
        {
            var spreadDegrees = new[] { 0, 180, 270, 90, 225, 135, 315, 45 };
            var pairDegrees = new[] { 315, 135, 225, 45, 225, 135, 315, 45 };
            var chargeDegrees = new[,] { { 45, 315 }, { 135, 45 }, { 225, 135 }, { 315, 225 } };
            foreach (var facing in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 })
                for (var role = 0; role < 8; role++)
                {
                    foreach (var action in new uint[] { 50042, 50032, 50033, 50045 })
                    {
                        var f = new Fight((ulong)role + 1);
                        f.Actors[100] = f.Actors[100] with { Rotation = facing }; f.Scan();
                        f.Cast(action, f.Local);
                        var angle = action == 50042 ? spreadDegrees[role] : action == 50032 ? pairDegrees[role] : role % 2 == 0 ? 270 : 90;
                        var expected = EnuoStrategy.Polar(angle * MathF.PI / 180, action == 50042 ? 13 : 10);
                        Check(f.Plan().Goal is { } goal && Vector3.Distance(goal.Position, expected) < 0.01f,
                            $"Video field assignment {action}/{EnuoStrategy.Roles[role]} rotated with boss facing {facing}.");
                    }
                    for (var safeSide = 0; safeSide < 4; safeSide++)
                        foreach (var edgeAction in new uint[] { 49977, 49978 })
                        {
                            var f = new Fight((ulong)role + 1);
                            f.Actors[100] = f.Actors[100] with { Rotation = facing };
                            var edgeBearing = safeSide * MathF.PI / 2 + (edgeAction == 49977 ? MathF.PI : 0);
                            f.Actors[101] = Actor(101, 0x4DC2, EnuoStrategy.Polar(edgeBearing, 40)); f.Scan();
                            f.Cast(edgeAction, 0, source: 101);
                            f.Engine.Icon(3, 701, f.Now); f.Engine.Icon(4, 701, f.Now);
                            var expected = EnuoStrategy.Polar(chargeDegrees[safeSide, role % 2] * MathF.PI / 180, role < 2 ? 8 : 6);
                            Check(f.Plan().Goal is { } goal && Vector3.Distance(goal.Position, expected) < 0.01f,
                                $"Charge {safeSide}/{edgeAction}/{EnuoStrategy.Roles[role]} used arena west/east or boss facing instead of facing the boss from the safe side.");
                        }
                }
        });
        run("Enuo wild charge tanks stay outside the healer even while orb transforms remain at center", () =>
        {
            foreach (var direction in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 })
                foreach (var edgeAction in new uint[] { 49977, 49978 })
                    foreach (var bossAction in new uint[] { 0, 49979, 49980 })
                        foreach (var single in new[] { false, true })
                        {
                            var plans = new List<EnuoPlan>();
                            for (ulong local = 1; local <= 4; local++)
                            {
                                var f = new Fight(local);
                                f.Actors[101] = Actor(101, 0x4DC2, EnuoStrategy.Polar(direction, 40));
                                // ARR 58.980–67.770: both 0x4BF7 actors report the boss center until the charge.
                                f.Actors[201] = Actor(201, single ? 0x4BFAu : 0x4BF7u, EnuoStrategy.Center);
                                if (!single) f.Actors[202] = Actor(202, 0x4BF7, EnuoStrategy.Center);
                                f.Scan(); f.Cast(edgeAction, 0, source: 101);
                                if (bossAction != 0) f.Cast(bossAction, 0);
                                f.Engine.Icon(4, single ? 702u : 701u, f.Now);
                                if (!single) f.Engine.Icon(3, 701, f.Now);
                                plans.Add(f.Plan());
                            }
                            for (var tank = 0; tank < 2; tank++)
                            {
                                var anchor = plans[single ? 3 : tank + 2].Goal!.Position;
                                var expected = anchor + Vector3.Normalize(anchor - EnuoStrategy.Center) * 2;
                                if (plans[tank].Hazards.Any(h => h.Contains(expected))) expected = anchor;
                                Check(Vector3.Distance(plans[tank].Goal!.Position, expected) < 0.01f,
                                    $"Tank {tank} is on the inward side of the healer: {direction}/{edgeAction}/{bossAction}/{single}.");
                            }
                        }
        });
        run("Enuo host controls are owner-scoped and execution is disabled in replay", () =>
        {
            for (var bits = 0; bits < 32; bits++)
                Check(EnuoExecutionGate.Allows((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0)
                    == (bits == 27), "Control gate bypassed a required condition.");
            FakeControl.Calls.Clear();
            var control = new EnuoHostControl(_ => throw new Exception("Unexpected missing capability"), typeof(FakeControl));
            control.Apply(new() { Target = 12, ForbiddenTargets = [13], Facing = 1, BlockCasts = true });
            control.Clear();
            Check(FakeControl.Calls.Count == 2 && FakeControl.Calls[0].Owner == FakeControl.Calls[1].Owner
                && FakeControl.Calls[0].Target == 12 && FakeControl.Calls[0].Forbidden.SequenceEqual([13UL]) && FakeControl.Calls[1].Cleared,
                "Target exclusion or owner cleanup lost.");
            Console.WriteLine("  Installed PR MechanicControl: " + (typeof(PromeRotation.Plugin).Assembly.GetType("PromeRotation.Hosting.MechanicControl") != null));
        });
        run("Enuo orb assignments rotate with the large pair and wait out vulnerability", () =>
        {
            foreach (var bearing in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 })
                for (var role = 0; role < 8; role++)
                {
                    var f = new Fight((ulong)role + 1); f.Cast(50002, 0);
                    var yellow = new ulong[] { 201, 203, 205, 207 };
                    var purple = new ulong[] { 202, 204, 206, 208 };
                    for (var group = 0; group < 4; group++)
                    {
                        var a = bearing + (-22.5f + 90 * group) * MathF.PI / 180;
                        f.Actors[yellow[group]] = Actor(yellow[group], group == 0 ? 0x4DC6u : 0x4DC5u, EnuoStrategy.Polar(a, 17));
                        f.Actors[purple[group]] = Actor(purple[group], group == 0 ? 0x4DC6u : 0x4DC5u, EnuoStrategy.Polar(a + MathF.PI / 4, 17));
                        f.Engine.Tether(purple[group], 100, 406, f.Now); f.Engine.Tether(yellow[group], 100, 407, f.Now);
                    }
                    f.Actors[f.Local] = f.Actors[f.Local] with { Position = EnuoStrategy.Center };
                    f.Actors[(ulong)(role ^ 1) + 1] = f.Actors[(ulong)(role ^ 1) + 1] with { Position = EnuoStrategy.Center };
                    f.Scan();
                    Check(f.Plan().Goal?.Position == f.Actors[yellow[role / 2]].Position, "Must select assigned accelerated yellow orb first.");
                    f.Engine.Effect(yellow[role / 2], role < 2 ? 50007u : 50006u, 1, f.Now);
                    f.Status(f.Local, 2941, 5);
                    Check(f.Plan().Goal?.Name.Contains("等搭档") == true && f.Plan().Hazards.Any(h => h.Key == $"orb/{purple[role / 2]}"), "Purple orb is not safe while vulnerable.");
                    f.Now = f.Now.AddSeconds(5.1); f.Scan();
                    Check(f.Plan().Goal?.Position == f.Actors[purple[role / 2]].Position, "Vulnerability expiry must release the assigned purple orb.");
                }
        });
        run("Enuo chase relay waits ahead, transfers ownership and protects bystanders", () =>
        {
            var f = new Fight(2); f.Cast(49992, 0, duration: 6);
            f.Actors[201] = Actor(201, 0x4EB5, new(117, 0, 100));
            f.Actors[1] = f.Actors[1] with { Position = new(100, 0, 117) }; f.Scan();
            f.Engine.Tether(201, 1, 404, f.Now); f.Now = Epoch.AddSeconds(8); f.Engine.Tether(1, 2, 405, f.Now);
            var plan = f.Plan();
            Check(plan.Goal?.Name.Contains("前一人") == true && !plan.KeepMoving, "Relay must stage ahead before receiving the chaser.");
            Check(plan.Hazards.Any(h => h.Key == "chase/201"), "Non-current targets still need the chaser danger.");
            f.Engine.Tether(201, 2, 404, f.Now.AddSeconds(1));
            Check(f.Plan().KeepMoving && f.Plan().Goal?.Name.Contains("顺时针沿场边") == true, "Changed tether target was not accepted.");
            f.Engine.RemoveActor(201); f.Engine.Untether(1, 405);
            Check(!f.Plan().KeepMoving && !f.Plan().Hazards.Any(h => h.Key == "chase/201"), "Despawned chaser retained control.");
        });
        run("Enuo output retains absolute deadlines, removes owned goals and preserves foreign areas", () =>
        {
            var f = new Fight(); f.Cast(49979, 0);
            using var safe = new ScriptSafeZoneApi { UtcNowForTest = Epoch };
            var draw = new ScriptDrawApi(); var output = new EnuoOutput(safe, draw, safe.Manager);
            var foreign = safe.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(Vector3.Zero, 0), [DangerWindow.Persistent(Epoch)]);
            var plan = f.Plan(); plan.Goal = new("test", new(116, 0, 100), Epoch.AddSeconds(5));
            output.Apply(plan, Epoch); var version = safe.Manager.Version;
            output.Apply(plan, Epoch.AddSeconds(1));
            Check(safe.Manager.Version == version, "Unchanged plan recreated its regions or shifted deadlines.");
            output.Clear(Epoch);
            Check(safe.Manager.ContainsArea(foreign) && safe.Manager.BuildSnapshot(Epoch).GoalAreas.Count == 0 && draw.Previews.Count == 0, "Cleanup touched foreign regions or leaked a goal.");
        });
        run("Enuo ARR five pulls: eight roles, real packet order, statuses and mechanic coverage",
            () => ArrReplay("enuo-20261112.json.gz", 263405, 5, 871, 0));
        run("Enuo ARR independent party clear: duplicate jobs, outward charges and all eight role routes",
            () => ArrReplay("enuo-20261115.json.gz", 74003, 0, 201, 1));
        run("Enuo ARR third recording clear: boss steel, edge donut and new tower layout",
            () =>
            {
                var checkedDeviation = false;
                ArrReplay("enuo-20261115-165045.json.gz", 75216, 0, 201, 1, (fights, time, action) =>
                {
                    if (checkedDeviation || action != 49983 || time > 70) return;
                    // At 66.569s the recording has D2 in H1's lane (5/3); the user confirmed a positioning mistake.
                    var actors = fights[0].Actors;
                    Check(Vector3.Distance(actors[6].Position, actors[3].Position) < 2
                        && Vector3.Distance(actors[6].Position, actors[4].Position) > 7,
                        "Fixture no longer reproduces D2 standing in the wrong charge group.");
                    var plans = fights.Select(f => { f.Scan(); return f.Plan(); }).ToArray();
                    Check(plans.All(p => p.Goal?.Name.StartsWith("回归波动") == true), "Missing charge assignment before recorded impact.");
                    var h1 = plans[2].Goal!.Position; var h2 = plans[3].Goal!.Position;
                    Check(h1.X > 100 && h2.X < 100 && Vector3.Distance(h1, h2) > 6.5f,
                        "North-side charge groups must preserve video boss-relative left/right.");
                    foreach (var role in new[] { 4, 6 })
                        Check(Vector3.Distance(plans[role].Goal!.Position, h1) < 0.01f, "D1/D3 lost the MT/H1 charge assignment.");
                    foreach (var role in new[] { 5, 7 })
                        Check(Vector3.Distance(plans[role].Goal!.Position, h2) < 0.01f, "Recorded positioning mistake changed D2/D4's ST/H2 assignment.");
                    checkedDeviation = true;
                });
                Check(checkedDeviation, "Did not check the recorded 5/3 charge deviation.");
            });
    }

    private static void ArrReplay(string fixture, int expectedPackets, int expectedWipes, int expectedCasts, int expectedClears,
        Action<Fight[], double, uint>? beforeEffect = null)
    {
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Enuo", fixture));
        using var zip = new GZipStream(file, CompressionMode.Decompress);
        using var document = JsonDocument.Parse(zip);
        var root = document.RootElement;
        Check(root.GetProperty("build").GetInt32() == 14251465 && root.GetProperty("packets").GetInt32() == expectedPackets, "Wrong fixture or incomplete packet scan.");
        var fights = Enumerable.Range(1, 8).Select(i => new Fight((ulong)i)).ToArray();
        var actors = fights[0].Actors;
        foreach (var member in root.GetProperty("roles").EnumerateArray())
        {
            var id = member.GetProperty("id").GetUInt64();
            Check(EnuoStrategy.Roles[(int)id - 1] == member.GetProperty("role").GetString(), "Fixture role IDs do not match the explicit role mapping.");
            actors[id] = actors[id] with { ClassJob = member.GetProperty("job").GetUInt32() };
        }
        foreach (var f in fights.Skip(1)) f.Actors = actors;
        var wipes = 0; var clears = 0; var casts = 0; var heat = 0; var freeze = 0; var tower = 0; var orb = 0; var chase = 0; var solves = 0; var charge = 0;
        var goals = new HashSet<string>();
        actors.Remove(100);
        var nextPlan = 0d;
        var towerRoutes = new HashSet<ulong>();
        var spreadRoutes = new HashSet<ulong>();
        var checkedSolves = new HashSet<uint>();
        var orbTypes = new Dictionary<ulong, uint>();
        var orbFirstType = new Dictionary<int, uint>();
        var towerWaveStart = double.NegativeInfinity;
        var waveTowers = new Dictionary<ulong, Vector3>();
        var waveMarks = new HashSet<ulong>();
        var towerHits = new HashSet<ulong>(); var coneHits = new HashSet<ulong>();
        Vector3[] waveGoals = [];
        var towerLifecycleChecks = 0; var passageLocations = 0;
        var debug = new EnuoPartyDebug(e => e.Raw is ReplayCastData raw ? raw.PacketFacing : null);
        var debugParty = EnuoStrategy.Roles.Select((role, i) => new ReplayPartyMember(i, role, (ulong)i + 1)).ToArray();
        var debugChecks = 0;
        foreach (var e in root.GetProperty("events").EnumerateArray())
        {
            var now = Epoch.AddSeconds(e.GetProperty("t").GetDouble());
            var id = e.GetProperty("id").GetUInt64();
            var kind = e.GetProperty("kind").GetString();
            foreach (var f in fights) f.Now = now;
            var actor = actors.GetValueOrDefault(id) ?? Actor(id, 0, EnuoStrategy.Center);
            switch (kind)
            {
                case "npc": actors[id] = Actor(id, e.GetProperty("oid").GetUInt32(), Vec(e.GetProperty("pos"))) with { Rotation = e.GetProperty("rot").GetSingle() }; break;
                case "move": case "setpos": actors[id] = actor with { Position = Vec(e.GetProperty("pos")), Rotation = e.GetProperty("rot").GetSingle() }; break;
                case "targetable": actors[id] = actor with { Targetable = e.GetProperty("value").GetBoolean() }; break;
                case "status":
                    var sid = e.GetProperty("status").GetUInt32(); var duration = e.GetProperty("duration").GetSingle();
                    actors[id] = actor with { Statuses = [.. actor.Statuses.Where(s => s.Id != sid), .. duration > 0 ? new ReplayStatus[] { new(sid, 0, 0, now.AddSeconds(duration)) } : []] };
                    break;
                case "despawn": actors.Remove(id); foreach (var f in fights) f.Engine.RemoveActor(id); break;
            }
            if (kind == "effect") beforeEffect?.Invoke(fights, e.GetProperty("t").GetDouble(), e.GetProperty("action").GetUInt32());
            if (kind is "cast" or "icon" or "tether" or "untether" or "effect" or "despawn")
            {
                debug.Synchronize(actors.Values, debugParty, now);
                LogSystemEvent ev = kind switch
                {
                    "cast" => new LogSystemCastStartEvent { SourceId = id, ActionId = e.GetProperty("action").GetUInt16(),
                        TargetId = e.GetProperty("target").GetUInt64(), EffectPosition = Vec(e.GetProperty("loc")),
                        CastTime = e.GetProperty("seconds").GetSingle(), Raw = new ReplayCastData(e.GetProperty("rot").GetSingle()) },
                    "icon" => new LogSystemTargetIconEvent { SourceId = id, TargetId = e.GetProperty("target").GetUInt64(), MarkerId = e.GetProperty("icon").GetUInt32() },
                    "tether" => new LogSystemTetherEvent { SourceId = id, TargetId = e.GetProperty("target").GetUInt64(), TetherId = e.GetProperty("tether").GetUInt32() },
                    "untether" => new LogSystemTetherCancelEvent { SourceId = id, TetherId = e.GetProperty("tether").GetUInt16() },
                    "effect" => new LogSystemActionEffectEvent { SourceId = id, ActionId = e.GetProperty("action").GetUInt32(), GlobalSequence = e.GetProperty("seq").GetUInt32() },
                    _ => new LogSystemObjectRemoveEvent { SourceId = id }
                };
                debug.Handle(ev, now);
            }
            if (kind is "wipe" or "clear") debug.Reset();
            if (kind is "cast" or "icon" or "tether" or "untether" or "effect" or "wipe" or "clear")
                foreach (var f in fights)
                {
                    f.Scan();
                    switch (kind)
                    {
                        case "cast": f.Engine.StartCast(new(id, e.GetProperty("action").GetUInt32(), e.GetProperty("target").GetUInt64(), Vec(e.GetProperty("loc")), e.GetProperty("rot").GetSingle(), now, now.AddSeconds(e.GetProperty("seconds").GetDouble() + 0.3))); break;
                        case "icon": new EnuoEventAdapter(f.Engine, _ => null).Handle(new LogSystemTargetIconEvent
                            { SourceId = id, TargetId = e.GetProperty("target").GetUInt64(), MarkerId = e.GetProperty("icon").GetUInt32() }, now); break;
                        case "tether": f.Engine.Tether(id, e.GetProperty("target").GetUInt64(), e.GetProperty("tether").GetUInt32(), now); break;
                        case "untether": f.Engine.Untether(id, e.GetProperty("tether").GetUInt32()); break;
                        case "effect": f.Engine.Effect(id, e.GetProperty("action").GetUInt32(), e.GetProperty("seq").GetUInt32(), now); break;
                        case "wipe": case "clear": f.Engine.Reset(); Check(f.Plan().Goal == null && f.Plan().Target == 0 && !f.Plan().HoldMovement, "Battle end retained control."); break;
                    }
                }
            var towerCast = kind == "cast" && e.GetProperty("action").GetUInt32() == 50013;
            var towerIcon = kind == "icon" && e.GetProperty("icon").GetUInt32() == 721;
            var towerEffect = kind == "effect" && e.GetProperty("action").GetUInt32() is 50013 or 50038;
            if (towerCast || towerIcon)
            {
                var t = e.GetProperty("t").GetDouble();
                if (t > towerWaveStart + 2)
                { towerWaveStart = t; waveTowers.Clear(); waveMarks.Clear(); towerHits.Clear(); coneHits.Clear(); waveGoals = []; }
                if (towerCast) waveTowers[id] = Vec(e.GetProperty("loc"));
                else waveMarks.Add(e.GetProperty("target").GetUInt64());
            }
            if (towerEffect)
                (e.GetProperty("action").GetUInt32() == 50013 ? towerHits : coneHits).Add(id);
            if ((towerCast || towerIcon || towerEffect) && waveTowers.Count == 4 && waveMarks.Count == 4)
            {
                var plans = fights.Select(f => { f.Scan(); return f.Plan(); }).ToArray();
                if (waveGoals.Length == 0)
                {
                    Check(plans.All(p => p.Goal?.Name.StartsWith("小怪：") == true), "ARR complete tower/icon input did not assign all eight roles.");
                    waveGoals = plans.Select(p => p.Goal!.Position).ToArray();
                }
                for (var i = 0; i < 8; i++)
                {
                    var marked = waveMarks.Contains((ulong)i + 1);
                    var source = waveTowers.MinBy(p => Vector3.DistanceSquared(p.Value, waveGoals[i]));
                    Check((Vector3.Distance(source.Value, waveGoals[i]) < 1) != marked, "ARR marked player was assigned a real tower.");
                    var needsGoal = marked ? coneHits.Count < 4 : !towerHits.Contains(source.Key);
                    Check(needsGoal ? plans[i].Goal?.Position == waveGoals[i] : plans[i].Goal?.Name.StartsWith("小怪：") != true,
                        $"ARR {fixture} {e.GetProperty("t").GetDouble():F3} role {i}: tower/bait released early or a resolved tower reactivated.");
                    towerLifecycleChecks++;
                }
            }
            if (kind == "cast" && e.GetProperty("action").GetUInt32() is 49985 or 49986 or 49987)
            {
                var action = e.GetProperty("action").GetUInt32(); var expected = Vec(e.GetProperty("loc"));
                var hazard = fights[0].Plan().Hazards.Single(h => h.Key == $"cast/{id}/{action}");
                Check(Vector3.Distance(hazard.Position, expected) < .01f, "ARR passage ignored its packet effect position.");
                var rotation = e.GetProperty("rot").GetSingle();
                var forward = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
                var centerline = actors[id].Position + forward * 40;
                Check(hazard.Contains(centerline, 0) == (action == 49985), "ARR passage center/side safety is inverted.");
                Check(hazard.Contains(expected + forward * 40, 0), "ARR passage misses the recorded damage lane.");
                passageLocations++;
            }
            if (kind == "cast") casts++;
            if (kind == "wipe") { wipes++; orbFirstType.Clear(); }
            if (kind == "clear") clears++;
            if (kind == "tether" && e.GetProperty("tether").GetUInt32() is 406 or 407) orbTypes[id] = e.GetProperty("tether").GetUInt32();
            if (kind == "effect" && e.GetProperty("action").GetUInt32() is 50006 or 50007 && !orbFirstType.ContainsKey(wipes))
            { orbFirstType[wipes] = orbTypes.GetValueOrDefault(id); Check(orbFirstType[wipes] == 407, "ARR accelerated yellow orb tether mapping changed."); }
            if (e.GetProperty("t").GetDouble() < nextPlan || kind is "wipe" or "clear") continue;
            nextPlan = e.GetProperty("t").GetDouble() + 0.25;
            debug.Synchronize(actors.Values, debugParty, now);
            var debugFrame = debug.Snapshot(now);
            foreach (var f in fights)
            {
                f.Scan(); var plan = f.Plan();
                var shown = debugFrame.Targets[(int)f.Local - 1];
                var expectedGoal = plan.Goal is { } active && active.Until >= now ? active : null;
                Check(expectedGoal == null ? shown.Goal == null : shown.Goal != null && shown.Goal.Name == expectedGoal.Name
                    && Vector3.Distance(shown.Goal.Position, expectedGoal.Position) < .001f,
                    $"ARR debug {fixture} {e.GetProperty("t").GetDouble():F3} {EnuoStrategy.Roles[(int)f.Local - 1]}: displayed target disagrees with its actual plan.");
                Check(!plan.HoldMovement || shown.State.Contains("停步"), "ARR debug did not label a held role.");
                debugChecks++;
                if (plan.HoldMovement) { heat++; Check(plan.Goal == null, "Heat released an assigned move."); }
                if (plan.KeepMoving && plan.Goal?.Name.Contains("冻结") == true) freeze++;
                if (plan.Goal?.Name.Contains("小怪：") == true) tower++;
                if (plan.Goal?.Name.Contains("撞球") == true) orb++;
                if (plan.Goal?.Name.Contains("追踪") == true) chase++;
                if (f.Local <= 2 && plan.Goal?.Name.StartsWith("回归波动") == true)
                {
                    var boss = actors.Values.First(a => a.Oid == 0x4DC1);
                    var radial = plan.Goal.Position - boss.Position;
                    var others = fights.Skip(2).Take(2).Select(h => { h.Scan(); return h.Plan().Goal; })
                        .Where(g => g?.Name.StartsWith("回归波动") == true).ToArray();
                    var group = others.MinBy(g => Vector3.DistanceSquared(g!.Position, plan.Goal.Position));
                    Check(group != null, "ARR charge lost the marked healer's group.");
                    Check(Vector3.Dot(plan.Goal.Position - group!.Position, radial) >= -0.01f,
                        $"ARR {e.GetProperty("t").GetDouble():F3}: tank {f.Local} sent inside the healer by stale orb coordinates.");
                    charge++;
                }
                if (plan.Goal is { } goal)
                {
                    goals.Add(goal.Name);
                    Check(float.IsFinite(goal.Position.X) && Vector3.Distance(goal.Position, EnuoStrategy.Center) < plan.ArenaRadius + 1, "Invalid/outside-arena goal.");
                }
                foreach (var h in plan.Hazards) Check(h.Until >= h.From && h.Telegraph <= h.From, "Invalid absolute hazard window.");
                var routed = plan.Goal?.Name.StartsWith("小怪：") == true ? towerRoutes : plan.Goal?.Name.Contains("八方散开") == true ? spreadRoutes : null;
                if (routed != null && !routed.Contains(f.Local) && plan.Goal!.Until > now.AddSeconds(3))
                {
                    using var safe = new ScriptSafeZoneApi { UtcNowForTest = now, DangerMarginForTest = 0.5f };
                    new EnuoOutput(safe, new(), safe.Manager).Apply(plan, now);
                    var snapshot = safe.Manager.BuildSnapshot(now); var pos = actors[f.Local].Position;
                    var route = new SafeZoneSolver().Solve(snapshot, new(pos, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), new(), now);
                    Check(route.IsSuccess, $"ARR assignment {plan.Goal.Name} role {f.Local}: {route.Status}.");
                    var destination = route.ExecutablePath.Count == 0 ? pos : route.ExecutablePath[^1].Position;
                    Check(Vector3.Distance(destination, plan.Goal.Position) <= 1.1f, $"ARR assignment {plan.Goal.Name} role {f.Local}: missed assigned position ({destination} vs {plan.Goal.Position}).");
                    Check(route.ExecutablePath.Count == 0 || route.ExecutablePath[^1].PlannedArrivalTime <= plan.Goal.Until.AddSeconds(-0.6),
                        $"ARR assignment {plan.Goal.Name}: route arrives after hit.");
                    routed.Add(f.Local);
                }
                var cast = f.Engine.Casts.Values.FirstOrDefault(c => c.Action is 49977 or 49978 or 49979 or 49980 or 49985 or 49986 or 49987 or 50001
                    && c.Finish > now.AddSeconds(3) && !checkedSolves.Contains(c.Action));
                if (cast != null && f.Local == 1)
                {
                    using var safe = new ScriptSafeZoneApi { UtcNowForTest = now, DangerMarginForTest = 0.5f };
                    var output = new EnuoOutput(safe, new(), safe.Manager); output.Apply(plan, now);
                    var snapshot = safe.Manager.BuildSnapshot(now);
                    var pos = actors[f.Local].Position;
                    var solution = new SafeZoneSolver().Solve(snapshot, new(pos, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), new(), now);
                    Check(solution.IsSuccess, $"ARR AID {cast.Action} at {(now - Epoch).TotalSeconds:F3}: solver {solution.Status}.");
                    var end = solution.ExecutablePath.Count > 0 ? solution.ExecutablePath[^1].Position : pos;
                    Check(!plan.Hazards.Any(h => h.Contains(end, 0)), $"ARR AID {cast.Action}: solver endpoint in hazard.");
                    Check(snapshot.ArenaBoundary!.Contains(end), "Solver left arena.");
                    checkedSolves.Add(cast.Action); solves++;
                }
            }
        }
        Check(wipes == expectedWipes && clears == expectedClears && casts == expectedCasts && heat > 0 && freeze > 0 && tower > 0 && orb > 0 && chase > 0 && charge > 0 && solves >= 5,
            $"Missing coverage: wipes={wipes}, clears={clears}, casts={casts}, heat={heat}, freeze={freeze}, tower={tower}, orb={orb}, chase={chase}, solves={solves}.");
        Check(towerRoutes.Count == 8 && spreadRoutes.Count == 8, "All eight roles need reachable tower and spread routes before their hit deadlines.");
        Check(towerLifecycleChecks > 0 && passageLocations > 0, "Missing tower lifecycle or passage location regression.");
        Console.WriteLine($"  ARR {fixture}: {wipes} wipes, {clears} completion events, {casts} casts; eight-role samples heat={heat}, freeze={freeze}, towers={tower}, orbs={orb}, chase={chase}; outward charge tank samples={charge}; solver shapes={solves}.");
        Console.WriteLine("  Goals: " + string.Join(" / ", goals));
        Console.WriteLine($"  Tower lifecycle role checks={towerLifecycleChecks}; packet passage locations={passageLocations}.");
        Console.WriteLine($"  Debug targets matched independent role plans: {debugChecks}.");
    }

    private static IEnumerable<int[]> Pairs(int[] a) { for (var i = 0; i < a.Length; i++) for (var j = i + 1; j < a.Length; j++) yield return [a[i], a[j]]; }
    private static Vector3 Vec(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());
    private static ReplayActorState Actor(ulong id, uint oid, Vector3 p) => new() { Id = id, EntityId = (uint)id, Oid = oid, Position = p, Targetable = true, InCombat = true, Hp = 100, MaxHp = 100 };
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private sealed class Fight
    {
        public DateTime Now = Epoch;
        public readonly ulong Local;
        public Dictionary<ulong, ReplayActorState> Actors = [];
        public readonly EnuoEncounter Engine = new();
        private readonly ReplayPartyMember[] _party = EnuoStrategy.Roles.Select((role, i) => new ReplayPartyMember(i, role, (ulong)i + 1)).ToArray();
        public Fight(ulong local = 1)
        {
            Local = local;
            foreach (var p in _party) Actors[p.EntityId] = Actor(p.EntityId, 0, EnuoStrategy.Spread(p.Slot, 4));
            Actors[100] = Actor(100, 0x4DC1, EnuoStrategy.Center); Scan();
        }
        public void Scan() => Engine.Synchronize(Actors.Values, _party, Local, Now);
        public EnuoPlan Plan() => Engine.Plan(Now);
        public void Status(ulong target, uint id, float seconds) { Actors[target] = Actors[target] with { Statuses = [new(id, 0, 100, Now.AddSeconds(seconds))] }; Scan(); }
        public void Cast(uint action, ulong target, ulong source = 100, float duration = 8)
            => Engine.StartCast(new(source, action, target, Actors.GetValueOrDefault(source)?.Position ?? EnuoStrategy.Center, 0, Now, Now.AddSeconds(duration + 0.3)));
    }
    public static class FakeControl
    {
        public sealed record Call(string Owner, ulong Target, ulong[] Forbidden, bool Cleared);
        public static readonly List<Call> Calls = [];
        public static void Update(string owner, ulong target, ulong clearTarget, ulong[] forbidden, float? heading, bool hold, bool block, bool cancel) => Calls.Add(new(owner, target, forbidden, false));
        public static void Clear(string owner) => Calls.Add(new(owner, 0, [], true));
    }
}

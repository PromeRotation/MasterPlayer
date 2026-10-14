using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

// 2026-10-13 route research: MP never delivered a crystal in 5 automated
// Delivery rounds. The hazards run on the course-start clock, so the carry is
// forecast from t0 (BlundervilleDeliverySchedule) instead of waiting at the
// hub to learn cycles. Replays all recorded rounds with at least 75s of
// hazards; a crystal is lost when the calibrated footprint holds the player
// 0.40s before an effect packet.
internal static class BlundervilleDeliveryScheduleRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 13, 3, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Orders = ["WEN", "NEW", "ENW", "WNE", "NWE", "EWN"];

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville delivery wind keeps barrier pockets open and resets its clock", () =>
        {
            var schedule = new BlundervilleDeliverySchedule(); schedule.SetStart(Epoch);
            schedule.Observe(34800, new(-200, 6, 212), Epoch.AddSeconds(BlundervilleDeliveryWind.First + .2));
            var wind = schedule.Hazards(Epoch.AddSeconds(4)).Where(h => h.Id.StartsWith("delivery/wind/0/")).ToArray();
            Check(wind.Length > 0, "Wind forecast missing.");
            Check(wind.All(h => Math.Abs((h.From - Epoch).TotalSeconds - (BlundervilleDeliveryWind.First + .2 + BlundervilleDeliveryWind.LaunchFrom)) < .001), "Observed wind did not correct the launch clock.");
            Check(wind.Any(h => h.Geometry.Contains(new(-200, 6, 230))), "Open floor must receive wind displacement.");
            foreach (var shelter in new[] { new Vector3(-203.2f, 6, 229.9f), new Vector3(-204.1f, 6, 224.1f) })
            {
                Check(!wind.Any(h => h.Geometry.Contains(shelter, .1f)), "The confirmed barrier pocket is blocked by wind.");
                var floor = BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, shelter);
                Check(ArenaBoundaryTraversal.ContainsTube(floor, shelter, shelter, .25f), "Shelter is inside a wall.");
            }
            schedule.SetStart(null);
            Check(!schedule.Hazards(Epoch).Any(), "Wind survives round reset.");
            schedule.SetStart(Epoch);
            Check(schedule.Hazards(Epoch).Where(h => h.Id.StartsWith("delivery/wind/0/")).All(h => Math.Abs((h.From - Epoch).TotalSeconds - (BlundervilleDeliveryWind.First + BlundervilleDeliveryWind.LaunchFrom)) < .001), "Previous round's wind correction leaked.");
        });
        run("Blunderville delivery charger body survives its effect packet window", () =>
        {
            var schedule = new BlundervilleDeliverySchedule(); schedule.SetStart(Epoch);
            var hazards = schedule.Hazards(Epoch.AddSeconds(.8)).ToArray();
            Check(hazards.Any(h => h.Id.StartsWith("delivery/body/17/0/0.104/") && h.Until > Epoch.AddSeconds(.8)), "Charger body expired with its instantaneous hit.");
            Check(!hazards.Any(h => h.Id.StartsWith("delivery/17/0/0.104")), "Instantaneous charge retained too long.");
        });
        run("Blunderville delivery completed carry retains its return colour and clears on reset", () =>
        {
            foreach (var (colour, profile) in new[] { ((ushort)35, BlundervilleFinalPlanner.ReturnWest), ((ushort)36, BlundervilleFinalPlanner.ReturnEast), ((ushort)37, BlundervilleFinalPlanner.ReturnNorth) })
            {
                var state = new BlundervilleRaceState();
                state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
                state.SynchronizeStatuses([new(3704, colour, Epoch.AddSeconds(30))], Epoch);
                state.Director(BlundervilleRaceState.DirectorInstance, 3, 1, 0, 0, 0, Epoch.AddSeconds(1));
                state.SynchronizeStatuses([], Epoch.AddSeconds(1));
                Check(state.NeedsPickup && !state.FailedDelivery && state.LastCrystalParam == colour && new BlundervilleFinalPlanner(profile).Active(state, profile.Spoke![0], Epoch.AddSeconds(1)), "A completed carry must retain its return corridor.");
                state.Reset();
                Check(state.LastCrystalParam == 0, "Colour leaked into next round.");
            }
        });
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "delivery-recorded.json")));
        var names = data.RootElement.GetProperty("recordings").EnumerateObject().Select(p => p.Name).ToArray();
        foreach (var (time, position) in new[]
        {
            (15.66, new Vector3(-203.204f, 5.997f, 230.365f)),
            (38.07, new Vector3(-203.082f, 5.997f, 224.659f)),
        })
            run($"Blunderville delivery yellow feedback resumes after wind at {time:F2}", () =>
                Simulate("2026.10.14 yellow-feedback", "NWE", (position, time), true));
        run("Blunderville delivery schedule forecasts every recorded carrying hit", () =>
        {
            int covered = 0, total = 0;
            foreach (var name in names)
            {
                using var replay = new Recording(name);
                foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() is 34716 or 34774 or 34799 && e[0].GetDouble() > 2))
                {
                    var t = e[0].GetDouble(); replay.Advance(t - 1.5);
                    // North hammers stun only after their packet (239 human crossings
                    // left the band up to the next packet unhurt): check the packet itself.
                    var northHammer = e[3].GetUInt32() == 34799 && Vec(e[4]).Z < 220;
                    var instant = Epoch.AddSeconds(northHammer ? t : t - BlundervilleDeliverySchedule.HitLead);
                    ++total;
                    if (replay.Encounter.DeliverySchedule.Hazards(Epoch.AddSeconds(t - 1.5))
                        .Any(h => h.From <= instant && h.Until >= instant && h.Geometry.Contains(Vec(e[4]) with { Y = 6 }, 1)))
                        ++covered;
                }
            }
            Console.WriteLine($"DELIVERY schedule coverage {covered}/{total}");
            Check(total > 5000 && covered >= total * .995, "A recorded Delivery hit fell outside its scheduled window.");
        });
        // Baseline of the timed carry corridors: 2026-10-13 9 of 14 replays
        // delivered all three, at most 2 lost one; 2026-10-14 (wait slip and
        // retreat) 15 of 15 with none lost. Raise as it improves.
        run("Blunderville delivery schedule rounds deliver with the carry corridors", () =>
        {
            int complete = 0, lost = 0, selected = 0, slow = 0;
            var rounds = Enumerable.Range(0, names.Length)
                .Select(i => (Name: names[i], Order: Environment.GetEnvironmentVariable("DELIVERY_ORDER") ?? Orders[i % Orders.Length]))
                // The yellow-feedback recording contains only an unfinished yellow carry.
                .Where(r => !r.Name.Contains("yellow-feedback")
                    && !(Environment.GetEnvironmentVariable("DELIVERY_ONLY") is { Length: > 0 } only && !r.Name.Contains(only))).ToArray();
            // Sequential: the carry search runs on background tasks whose completion
            // the simulation polls by wall clock; parallel rounds starve them.
            var errors = rounds.Select(r =>
            {
                try { Simulate(r.Name, r.Order); return null; }
                catch (InvalidOperationException e) { return e.Message; }
            }).ToArray();
            for (var i = 0; i < rounds.Length; ++i)
            {
                ++selected;
                if (errors[i] is not { } message) { ++complete; continue; }
                if (message.StartsWith("Crystal lost", StringComparison.Ordinal)) ++lost;
                else if (message.StartsWith("The Delivery round was slower", StringComparison.Ordinal)) { ++complete; ++slow; }
                Console.WriteLine($"DELIVERY {rounds[i].Name} {rounds[i].Order}: {message}");
            }
            Console.WriteLine($"DELIVERY rounds complete {complete}/{selected}, crystals lost {lost}, over 75s {slow}");
            Check(selected > 0 && complete == selected && lost == 0, "A recorded Delivery round failed to deliver all three crystals.");
        });
    }

    internal static void Simulate(string recording, string order, (Vector3 Position, double Time)? resume = null, bool firstOnly = false,
        string fixture = "delivery-recorded.json", double firstLimit = 15)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var replay = new Recording(recording, fixture); var now = Epoch;
        var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner(); var direct = false;
        using var terrain = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "delivery-surfaces.json")));
        var polygons = terrain.RootElement.EnumerateArray().Select(a => a.EnumerateArray().Select(Vec).ToArray()).ToArray();
        var grounded = true; var withoutFloor = 0.0;
        BlundervilleRaceDecision decision = new(null, true, false, null, "");
        BlundervilleFinalPlanner[] corridors = [new(BlundervilleFinalPlanner.DeliveryWest), new(BlundervilleFinalPlanner.DeliveryEast), new(BlundervilleFinalPlanner.DeliveryNorth)];
        BlundervilleFinalPlanner[] returns = [new(BlundervilleFinalPlanner.ReturnWest), new(BlundervilleFinalPlanner.ReturnEast), new(BlundervilleFinalPlanner.ReturnNorth)];
        // Humans pick up the first crystal 3.5–3.9s after the start.
        var p = resume?.Position ?? new Vector3(-200.3f, 6, 255.5f); var previous = p;
        // The triangulated hub surface is coarser than its sunken centre in
        // ARR (~5.4m). Start on the independently sampled upper surface;
        // otherwise the ceiling rule incorrectly snaps this standing player
        // onto the overlapping lower floor on the first simulation frame.
        if (p.Y >= 4.8f && BlundervilleDeliveryRecoveryRegression.Height(polygons, p, 6.5f) is { } upper && upper >= 4.8f)
            previous = p = p with { Y = upper };
        var history = new List<(double T, Vector3 P)>();
        var carrying = true; var score = 0; var colour = 0; var sprint = false; var stuns = 0;
        var picked = 3.8; var carries = new List<string>(); var plans = new List<double>();
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0; var nextScan = 0.0;
        var impacts = DamageTimeline(replay.Events); var hitIndex = 0;
        var hammers = impacts.Where(i => i.Event[3].GetUInt32() == 34799 && Vec(i.Event[4]).Z < 220).ToArray();
        var depositDwell = 0.0;
        // Charger body stuns after the recorded packets (24-round stun map).
        var charges = replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() == 34774 && e[6].ValueKind == JsonValueKind.Array)
            .Select(e => (T: e[0].GetDouble(), From: Vec(e[4]), To: Vec(e[6]))).ToArray();
        var winds = replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() == 34800)
            .Select(e => e[0].GetDouble() + .9).ToArray();
        var windIndex = 0;
        const double frame = 1.0 / 30;
        var begin = resume?.Time ?? 3.8;
        for (var tick = 0; begin + tick * frame < 100; ++tick)
        {
            var t = begin + tick * frame; now = Epoch.AddSeconds(t); move.Now = now;
            if (t + 1e-7 >= nextScan)
            {
                nextScan = t + .1; replay.Advance(t); api.UtcNowForTest = now;
                // Mirrors BlundervilleScript: the Director start drives the schedule.
                replay.Encounter.DeliverySchedule.SetStart(state.StartedByDirector ? state.StartedAt : null);
                state.SynchronizeStatuses(carrying ? [new(3704, Colour(order[colour]), now.AddSeconds(30))]
                    : sprint ? [new(1548, 0, now.AddSeconds(30))] : [], now);
                decision = planner.Update(state, now, p, grounded, false, false, null, false);
                decision = BlundervilleDeliveryPlanner.Apply(decision, state, replay.Encounter, p, now);
                // Mirrors BlundervilleScript: the carry corridor of the crystal's colour.
                var corridor = state.CarryCrystalParam switch
                {
                    35 => corridors[0], 36 => corridors[1], 37 => corridors[2],
                    0 => state.LastCrystalParam switch { 35 => returns[0], 36 => returns[1], 37 => returns[2], _ => null },
                    _ => null
                };
                if (corridor != null)
                {
                    var planning = System.Diagnostics.Stopwatch.StartNew();
                    decision = corridor.Apply(decision, state, replay.Encounter.TraversalHazards(now).ToArray(), p, now, nav.GetDirectTraversalExecution(now));
                    plans.Add(planning.Elapsed.TotalMilliseconds);
                    // The real framework keeps ticking during this work. Model
                    // its measured latency before consuming the task's result.
                    if (corridor.PendingSearch is { IsCompleted: false } search)
                    {
                        var worker = System.Diagnostics.Stopwatch.StartNew();
                        search.GetAwaiter().GetResult();
                        var delay = double.TryParse(Environment.GetEnvironmentVariable("DELIVERY_LATENCY"), out var configured) ? configured : .1;
                        nextScan = t + Math.Max(delay, worker.Elapsed.TotalSeconds);
                    }
                }
                foreach (var other in corridors) if (other != corridor) other.Reset();
                foreach (var other in returns) if (other != corridor) other.Reset();
                direct = decision.DirectTraversal;
                if (Environment.GetEnvironmentVariable("DELIVERY_DEBUG") == "1")
                    Console.WriteLine($"DLVDBG t={t:F2} p=({p.X:F2},{p.Z:F2}) {decision.Hint} path={string.Join(" ", decision.ContinuousPath?.Select(q => $"({q.X:F1},{q.Z:F1})") ?? [])}"
                        + (corridor?.Plan is { } plan ? $" plan={string.Join(" ", plan.Select(w => $"({w.Position.X:F1},{w.Position.Z:F1})@{(w.Arrival - now).TotalSeconds:F1}-{(w.Departure - now).TotalSeconds:F1}"))}" : ""));
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var speed = state.NavigationBaseSpeed(now); move.Speed = speed;
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, speed, Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, grounded, snapshot.Version);
            var capability = new MovementCapability(speed, speed, 0, false, speed, speed);
            if (!runtime.Setting.EffectiveEnableAutoMovement)
            {
                // Update gates Dispatch on this flag; the reflected fallback
                // below must honour the same gate during a planned wait.
                move.Stop(); pending = accepted = null;
            }
            else if (direct) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, capability, now);
            else
            {
                if (pending != null && t >= ready) { accepted = pending; pending = null; }
                if (pending == null && t >= nextSolve)
                {
                    var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    pending = new SafeZoneSolver().Solve(snapshot, player, capability, options, now);
                    ready = t + Math.Max(.3, SimClock.Solve(watch)); nextSolve = ready + .1;
                }
                runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            }
            previous = p; p = move.Advance(p, (float)frame);
            if (decision.RecoveryFloor != null || p.Y < 5.98f)
            {
                var height = BlundervilleDeliveryRecoveryRegression.Height(polygons, p, MathF.Max(3.65f, previous.Y + .4f));
                withoutFloor = height == null ? withoutFloor + frame : 0;
                Check(withoutFloor < 1 && (height != null || decision.DirectTraversal), $"No lower-floor landing at {p}.");
                p.Y = MathF.Max(height ?? 3.25f, previous.Y - .3f);
                grounded = height != null && MathF.Abs(p.Y - height.Value) < .05f;
                Check(p.Y - previous.Y < .41f, "Return walked through an upper bridge wall.");
            }
            while (windIndex < winds.Length && winds[windIndex] <= t + frame)
            {
                var launch = winds[windIndex++];
                if (launch < begin || p.Y < 4.8f || p.X is < -206 or > -194 || p.Z is < 212 or > 243) continue;
                // Actual 13:46 samples: open centre returns to the hub; west
                // barriers stop at z~230.4 or 224.6. Do not let a no-physics
                // replay count walking straight through the wind as success.
                var wall = p.X is >= -204.35f and <= -202.15f && p.Z < 230.45f ? 230.45f : p.Z + 20;
                // The new ARR records wall contact at z=224.659 (16-bit positions).
                if (p.X is >= -205.25f and <= -202.9f && p.Z <= 224.7f) wall = 224.7f;
                Check(wall - p.Z <= 1.5f, $"Wind exposed at t={launch:F2} p={p}; landing z={wall:F2} ({recording} {order}).");
                var pushed = p with { Z = MathF.Min(wall, p.Z + .35f) };
                var floor = BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, p);
                while (!ArenaBoundaryTraversal.ContainsSegment(floor, p, pushed) && pushed.Z - p.Z > .01f)
                    pushed.Z = (p.Z + pushed.Z) / 2;
                if (ArenaBoundaryTraversal.ContainsSegment(floor, p, pushed)) p = pushed;
            }
            history.Add((t + frame, p));
            if (carrying && p.Y >= 4.8f)
                foreach (var (hammer, packetAt) in hammers)
                {
                    var age = t - packetAt;
                    if (age < .15 || age > 1.6) continue;
                    var source = Vec(hammer[4]);
                    Check(MathF.Abs(p.X - source.X) > 2 || p.Z < source.Z - 2.5f || p.Z > source.Z + (age < .5 ? 3.5f : 2.5f),
                        $"North animation stun {age:F2}s after packet at {p}, t={t:F2} ({recording}).");
                }
            var pad = state.DeliveryTarget;
            depositDwell = carrying && pad is { } deposit && Vector2.Distance(new(p.X, p.Z), new(deposit.X, deposit.Z)) < 3
                && Vector2.Distance(new(p.X, p.Z), new(previous.X, previous.Z)) < .01f ? depositDwell + frame : 0;
            Check(depositDwell < .6, $"Stopped short of the deposit at {p} for {depositDwell:F2}s.");
            Check(ArenaBoundaryTraversal.ContainsSegment(decision.RecoveryFloor ?? BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, p), previous, p),
                $"Delivery left its raised floor between {previous} and {p} ({recording}).");
            // The effect packet trails the damage by ~0.40s.
            while (hitIndex < impacts.Length && impacts[hitIndex].At - BlundervilleDeliverySchedule.HitLead <= t)
            {
                var (e, packetAt) = impacts[hitIndex++]; var instant = packetAt - BlundervilleDeliverySchedule.HitLead;
                if (instant < begin || !carrying || p.Y < 4.8f) continue;
                var at = history.LastOrDefault(h => h.T <= instant + 1e-6).P;
                if (BlundervilleDeliverySchedule.Hits(e[3].GetUInt32(), Vec(e[4]), at))
                    Console.WriteLine($"DELIVERY hit forecast {string.Join("; ", replay.Encounter.DeliverySchedule.Hazards(now).Where(h => h.Geometry.Contains(at)).Select(h => $"{h.Id}@{(h.From - Epoch).TotalSeconds:F2}..{(h.Until - Epoch).TotalSeconds:F2}").Take(12))}");
                Check(!BlundervilleDeliverySchedule.Hits(e[3].GetUInt32(), Vec(e[4]), at),
                    $"Crystal lost to {e[3].GetUInt32()} from {Vec(e[4])} at t={instant:F2} p={at} carrying {order[colour]} ({recording} {order}).");
            }
            foreach (var (packet, from, to) in charges)
            {
                if (p.Y < 4.8f || t < packet || t > packet + 2 || t < begin + 1) continue;
                var length = Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z));
                foreach (var (near, far, start, end, _) in BlundervilleDeliverySchedule.TruthZones(length))
                    if (t >= packet + start && t <= packet + end && BlundervilleDeliverySchedule.Zone(from, to, near, far, 4f).Contains(p with { Y = from.Y }))
                        ++stuns;
            }
            if (carrying && Delivered(order[colour], p))
            {
                if (firstOnly)
                {
                    Console.WriteLine($"DELIVERY recovery from {begin:F2}: delivered after {t - begin:F2}s, dispatch {move.Count}");
                    Check(t - begin < firstLimit, $"Carry exceeded its {firstLimit:F0}s time budget.");
                    return;
                }
                carries.Add($"{order[colour]}{t - picked:F1}");
                carrying = false; sprint = true;
                state.Director(BlundervilleRaceState.DirectorInstance, 3, (uint)++score, 0, 0, 0, now);
                if (score == 3)
                {
                    Console.WriteLine($"DELIVERY {recording} {order}: three crystals at {t:F2}s, dispatch {move.Count}, body frames {stuns}, carries {string.Join(" ", carries)}, planning p95 {plans.Order().ElementAt((int)(plans.Count * .95)):F1}ms max {plans.Max():F1}ms");
                    Check(t < 75, "The Delivery round was slower than the human median (75s).");
                    return;
                }
                ++colour;
            }
            else if (!carrying && p.Y >= 4.8f && Vector2.Distance(new(p.X, p.Z), new(-200, 252)) <= 3.15f) { carrying = true; sprint = false; picked = t; }
        }
        throw new InvalidOperationException($"Delivered {score}/3 by 100s; stopped at {p} ({recording} {order}).");
    }

    private static ushort Colour(char c) => c switch { 'W' => 35, 'E' => 36, _ => 37 };

    // 00:14:40 stalls from 54.100 to 55.292 and records 23 events together.
    // That charge's neighbours are 46.214 and 62.248: its animation belongs
    // near 54.231, not 55.292. Reconstruct only independently corroborated
    // batched timestamps; live playback still receives the original packets.
    private static (JsonElement Event, double At)[] DamageTimeline(JsonElement[] events)
    {
        var times = events.Select(e => e[0].GetDouble()).Distinct().Order().ToArray();
        var resumes = times.Skip(1).Where((t, i) => t - times[i] > .5).ToArray();
        var batches = events.GroupBy(e => e[0].GetDouble()).Where(g => g.Count() >= 8
                || g.Count() >= 3 && resumes.Any(r => g.Key >= r && g.Key - r < .3))
            .Select(g => g.Key).ToHashSet();
        var result = new List<(JsonElement, double)>(); var restored = 0;
        foreach (var group in events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() is 34716 or 34774 or 34799)
            .GroupBy(e => (e[2].GetUInt64(), e[3].GetUInt32(), Rotation: Math.Round(e[5].GetDouble(), 1))))
        {
            var sequence = group.OrderBy(e => e[0].GetDouble()).ToArray();
            var intervals = sequence.Zip(sequence.Skip(1), (a, b) => b[0].GetDouble() - a[0].GetDouble()).Where(t => t > .5).Order().ToArray();
            var period = intervals.Length > 0 ? intervals[intervals.Length / 2] : 0;
            var fullCycles = Enumerable.Range(3, 6).SelectMany(span => Enumerable.Range(0, Math.Max(0, sequence.Length - span))
                .Select(i => (sequence[i + span][0].GetDouble() - sequence[i][0].GetDouble()) / span))
                .Where(p => Math.Abs(p - period) < .3).Order().ToArray();
            if (fullCycles.Length >= 8) period = fullCycles[fullCycles.Length / 2];
            for (var i = 0; i < sequence.Length; ++i)
            {
                var at = sequence[i][0].GetDouble();
                if (period > 1 && i > 0 && i + 1 < sequence.Length && batches.Contains(at))
                {
                    // 22:38 batches two successive cycles (6.91 and 13.80),
                    // so the immediate neighbour is also late. Require two
                    // bracketing packets agreeing on the measured full cycles;
                    // never shift an isolated genuine phase change.
                    var corrected = false;
                    for (var span = 2; span <= 8 && !corrected; ++span)
                        for (var left = Math.Max(0, i - span + 1); left < i && left + span < sequence.Length; ++left)
                        {
                            var right = left + span;
                            var before = sequence[left][0].GetDouble(); var after = sequence[right][0].GetDouble();
                            var expected = before + (after - before) * (i - left) / span;
                            if (at - expected <= .75 || at - expected > 3.5 || Math.Abs(after - before - span * period) >= .3) continue;
                            if (Enumerable.Range(left + 1, span - 1).Any(j => sequence[j][0].GetDouble() < before + (after - before) * (j - left) / span - .3)) continue;
                            at = expected; ++restored; corrected = true; break;
                        }
                    // The final two packets can both be delayed, leaving no
                    // clean right bracket before the recording ends. Extrapolate
                    // only one cycle after three independently stable intervals,
                    // and only for a confirmed multi-device packet batch.
                    if (!corrected && i >= 4 && Enumerable.Range(i - 3, 3).All(j =>
                        Math.Abs(sequence[j][0].GetDouble() - sequence[j - 1][0].GetDouble() - period) < .3))
                    {
                        var expected = sequence[i - 1][0].GetDouble() + period;
                        if (at - expected is > .75 and <= 3.5) { at = expected; ++restored; }
                    }
                }
                result.Add((sequence[i], at));
            }
        }
        if (restored > 0) Console.WriteLine($"DELIVERY animation timestamps reconstructed from adjacent cycles: {restored}");
        return result.OrderBy(e => e.Item2).ToArray();
    }

    // 22:38 stopped ~1.5m short for 3s, then scored 0.66m from centre.
    // Require walking into the pad instead of treating the route end as a score.
    private static bool Delivered(char c, Vector3 p) => c switch
    {
        'W' => Vector2.Distance(new(p.X, p.Z), new(-240.30f, 275.50f)) <= .7f,
        'E' => Vector2.Distance(new(p.X, p.Z), new(-159.43f, 275.25f)) <= .7f,
        _ => Vector2.Distance(new(p.X, p.Z), new(-200.1303f, 205.1951f)) <= .7f,
    };

    internal sealed class Recording : IDisposable
    {
        private readonly JsonDocument _data;
        public readonly JsonElement[] Events;
        private readonly Dictionary<ulong, BlundervilleActor> _actors;
        public readonly BlundervilleEncounter Encounter = new(_ => { });
        private int _index; private double _sample = -1;
        public Recording(string name, string fixture = "delivery-recorded.json")
        {
            _data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", fixture)));
            var recording = _data.RootElement.GetProperty("recordings").GetProperty(name);
            Events = recording.GetProperty("events").EnumerateArray().ToArray();
            _actors = recording.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(),
                d.GetProperty("oid").GetUInt32(), Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
            Encounter.DeliverySchedule.SetStart(Epoch);
        }
        public void Advance(double time)
        {
            while (_sample + .1 < time) { _sample += .1; Sample(_sample); }
            Sample(time); _sample = time;
        }
        private void Sample(double time)
        {
            while (_index < Events.Length && Events[_index][0].GetDouble() <= time)
            {
                var e = Events[_index++]; var id = e[2].GetUInt64(); var at = Epoch.AddSeconds(e[0].GetDouble());
                switch (e[1].GetString())
                {
                    case "spawn": _actors[id] = new(id, e[3].GetUInt32(), Vec(e[4]), Rotation: e[5].GetSingle()); break;
                    case "move": if (_actors.TryGetValue(id, out var moved)) _actors[id] = moved with { Position = Vec(e[3]), Rotation = e[4].GetSingle() }; break;
                    case "cast":
                        var cast = new BlundervilleCast(e[3].GetUInt32(), Vec(e[7]), e[6].GetSingle(), at, at.AddSeconds(e[4].GetDouble() + .3), Vec(e[5]));
                        if (_actors.TryGetValue(id, out var caster)) _actors[id] = caster with { Cast = cast };
                        Encounter.StartCast(id, cast, at);
                        break;
                    case "hit":
                        Encounter.Effect(id, e[3].GetUInt32(), e[7].GetUInt32(), Vec(e[4]), e[5].GetSingle(), at,
                            e[6].ValueKind == JsonValueKind.Array ? Vec(e[6]) : null);
                        if (_actors.TryGetValue(id, out var source)) _actors[id] = source with { Cast = null };
                        break;
                }
            }
            Encounter.Synchronize(_actors.Values, Epoch.AddSeconds(time));
        }
        public void Dispose() => _data.Dispose();
    }

    internal sealed class Moving : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now; public int Count; public float Speed = 6;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, IsRunning ? _points[^1] : null,
            IsRunning ? _index : 0, _points.Count, IsRunning ? Math.Max(0, _points.Count - _index - 1) : 0);
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { var running = IsRunning; _points = p; _index = 0; ++Count; if (!running) _ready = Now.AddSeconds(.08); }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => Append([p]);
        public void Append(IReadOnlyList<GreenMovePoint> p) => _points = _points.Concat(p).ToArray();
        public void ClearQueue() => _points = _points.Take(_index + 1).ToArray();
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt)
        {
            while (IsRunning && Flat(_points[_index].Position - p).Length() <= _points[_index].Tolerance) ++_index;
            if (!IsRunning || Now < _ready) return p;
            var d = Flat(_points[_index].Position - p); var distance = d.Length();
            return p + d / distance * MathF.Min(distance, Speed * dt);
        }
        private static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    }

    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

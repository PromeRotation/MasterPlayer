using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;

internal static class BlundervilleRingColumnsRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Origin = new(184.8388f, 10, -122.3267f);
    private static readonly Vector3 Destination = new(175.525f, 10, -120.714f);

    internal static void Run(Action<string, Action> run)
    {
        run("Blunderville ring columns cover idle bodies and only the two physical fan bases", () =>
        {
            var encounter = New();
            encounter.Synchronize([new(1, 16350, Origin), new(2, 16338, new(183.03f, 10, -108.03f))], Epoch);
            var bodies = Columns(encounter, Epoch);
            Check(bodies.Length == 3 && bodies.Count(h => h.Geometry is CircleGeometry { Radius: 1.1f }) == 2,
                "Missing idle column or invented a pillar at the circle helper.");
            Check(bodies.All(h => h.Until == DateTime.MaxValue), "An idle physical body expired before its next move.");
            encounter.Synchronize([new(1, 16350, new(-200, 6, 252))], Epoch.AddSeconds(.1));
            Check(Columns(encounter, Epoch.AddSeconds(.1)).Length == 0, "Delivery inherited Ring columns.");
        });

        run("Blunderville ring columns reserve cast transit and cancel back to the source", () =>
        {
            var encounter = New();
            encounter.Synchronize([new(1, 16350, Origin)], Epoch);
            var finish = Epoch.AddSeconds(1.5);
            encounter.StartCast(1, new(35409, Destination, 0, Epoch, finish), Epoch);
            var planned = Columns(encounter, Epoch);
            Check(planned.Single(h => h.Id == "ring/column/1/source").Until == finish.AddSeconds(.85), "Removed the source before transit completed.");
            Check(planned.Single(h => h.Id == "ring/column/1/body").From == finish, "Destination cap became dangerous before the planned charge.");
            Check(planned.All(h => !h.HasMotionEnvelope), "A cast reservation bypassed ordinary preparation margins.");
            encounter.Cancel(1, 35409, Epoch.AddSeconds(.1));
            var body = Columns(encounter, Epoch.AddSeconds(.1)).Single(h => h.Id == "ring/column/1/body");
            Check(body.Geometry.Position == Origin && !Columns(encounter, Epoch.AddSeconds(.1)).Any(h => h.Id.EndsWith("/sweep")), "Cancelled move retained its destination or sweep.");
        });

        run("Blunderville ring columns survive stale poses and persist at the actual destination", () =>
        {
            var encounter = New();
            encounter.Synchronize([new(1, 16350, Origin)], Epoch);
            encounter.Effect(1, 35409, 7, Origin, 0, Epoch, Destination);
            var sweep = Columns(encounter, Epoch).Single(h => h.Id.EndsWith("/sweep"));
            Check(sweep.Geometry.Contains((Origin + Destination) / 2), "The 9m charge was filtered out.");
            encounter.Synchronize([new(1, 16350, Origin)], Epoch.AddSeconds(.2));
            encounter.Effect(1, 35409, 7, Origin, 0, Epoch.AddSeconds(.3), Destination);
            Check(Columns(encounter, Epoch.AddSeconds(.3)).Single(h => h.Id.EndsWith("/sweep")) == sweep, "A stale pose or duplicate effect moved the transit clock.");
            encounter.Synchronize([new(1, 16350, Destination)], Epoch.AddSeconds(.75));
            var parked = Columns(encounter, Epoch.AddSeconds(2)).Single(h => h.Id == "ring/column/1/body");
            Check(parked.Geometry.Position == Destination && parked.Until == DateTime.MaxValue, "The parked column vanished after the charge.");
            Check(Columns(encounter, Epoch.AddSeconds(2)).All(h => !h.Id.EndsWith("/sweep") && !h.Id.EndsWith("/source")), "The entire old corridor stayed permanently blocked.");
            var previews = encounter.Previews(Epoch.AddSeconds(2)).ToDictionary(p => p.Id);
            Check(previews.ContainsKey(parked.Id) && previews[parked.Id].Group == BlundervilleCatalog.Devices, "The new navigation danger was not drawn.");
        });

        run("Blunderville ring columns clear on removal oid replacement reset and rewind", () =>
        {
            var encounter = New();
            encounter.Synchronize([new(1, 16350, Origin), new(2, 16350, Destination)], Epoch);
            encounter.Remove(1);
            Check(Columns(encounter, Epoch).Any(h => h.Id == "ring/column/2/body") && !Columns(encounter, Epoch).Any(h => h.Id.StartsWith("ring/column/1/")), "Removal crossed source ownership.");
            encounter.Synchronize([new(2, 16338, Destination)], Epoch.AddSeconds(.1));
            Check(!Columns(encounter, Epoch.AddSeconds(.1)).Any(h => h.Id == "ring/column/2/body"), "OID replacement kept a moving column.");
            encounter.Reset();
            Check(Columns(encounter, Epoch).Length == 0, "Reset retained columns.");
            encounter.Synchronize([new(1, 16350, Origin)], Epoch);
            Check(Columns(encounter, Epoch.AddSeconds(-1)).Length == 0, "Replay rewind retained future occupancy.");
        });

        run("Blunderville ring columns register stable persistent danger instead of hard walls", () =>
        {
            var now = Epoch;
            using var runtime = new SafeZoneRuntime(new BlundervilleRingRegression.Moving(), () => false, () => false) { ReplayClock = () => now };
            using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
            var nav = new BlundervilleNavigation(runtime, api, new());
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20015, 0, 0, 0, now);
            var encounter = New(); encounter.Synchronize([new(1, 16350, Origin)], now);
            var player = new Vector3(218, 10, -134);
            var decision = new BlundervilleRaceDecision(new(218, 10, -125), false, false, null, "test");
            nav.Apply(state, decision, encounter, now, player, true, false, false);
            var first = runtime.Manager.BuildSnapshot(now);
            Check(first.HardObstacles.Count == 0 && first.DangerAreas.Count == 3, "Columns became hard walls or were omitted from navigation.");
            Check(first.DangerAreas.SelectMany(a => a.DangerWindows).All(w => w.DangerUntil == null), "Persistent bodies retained MaxValue and would overflow buffering.");
            foreach (var window in first.DangerAreas.SelectMany(a => a.DangerWindows)) _ = window.Buffered(.2f, .1f);
            now = now.AddSeconds(.1);
            encounter.Synchronize([new(1, 16350, Origin)], now);
            nav.Apply(state, decision, encounter, now, player, true, false, false);
            Check(runtime.Manager.BuildSnapshot(now).Version == first.Version, "Unchanged column poses cancelled the solver every scan.");
            encounter.Effect(1, 35409, 1, Origin, 0, now, Destination);
            nav.Apply(state, decision, encounter, now, player, true, false, false);
            var sweepWindow = runtime.Manager.BuildSnapshot(now).DangerAreas.Single(a => a.Geometry is RectangleGeometry).DangerWindows.Single();
            Check(sweepWindow.HasMotionEnvelope && sweepWindow.DangerFrom == now && sweepWindow.DangerUntil == now.AddSeconds(.85)
                && sweepWindow.Buffered(.5f, .3f) == sweepWindow, "The confirmed physical sweep was buffered a second time as a future cast.");
            nav.Clear();
        });

        run("Blunderville ring columns keep jump targets exact and validate the full run", () =>
        {
            var now = Epoch;
            var move = new BlundervilleRingRegression.Moving();
            using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
            runtime.Setting.EnableSafeZone = true;
            using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
            var nav = new BlundervilleNavigation(runtime, api, new());
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20015, 0, 0, 0, now);
            var corridor = new BlundervilleJumpCorridor(new(210.8f, 10, -142.6f), new(213.2f, 10, -138.7f));
            var player = corridor.Takeoff - Vector3.Normalize(corridor.Landing - corridor.Takeoff);
            var decision = new BlundervilleRaceDecision(corridor.Landing, false, false, null, "test");
            nav.Apply(state, decision, New(), now, player, true, false, false, corridor);
            Tick();
            Check(move.GetStatus().IsRunning && move.GetStatus().FinalPoint?.Position == corridor.Landing,
                "The running jump chose another escape goal instead of its exact landing.");
            Check(!nav.CommitJump(corridor.Landing, player, 0, now), "A standing jump bypassed the speed gate.");
            runtime.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(corridor.Landing, 0), [new(now, null)]);
            now = now.AddSeconds(.03); Tick();
            Check(!move.GetStatus().IsRunning && !nav.GetDirectTraversalExecution(now).HasControl,
                "The exact jump target bypassed complete route danger validation.");
            nav.Clear();

            void Tick()
            {
                var snapshot = runtime.Manager.BuildSnapshot(now);
                ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(
                    new(player, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), now);
            }
        });

        run("Blunderville ring columns leave the east relay clear of device footprints", () =>
        {
            var nodes = BlundervilleRoutes.Nodes(BlundervilleCourse.Ring, new(215, 10, -138));
            var goal = nodes[0].Position;
            CircleGeometry[] devices = [new(new(180, 10, -125), 8), new(new(220, 10, -125), 8),
                new(new(183.03f, 10, -108.03f), 6), new(new(188.69f, 10, -113.69f), 6),
                new(new(217.03f, 10, -108.03f), 6), new(new(211.31f, 10, -113.69f), 6)];
            Check(BlundervilleRoutes.Boundary(BlundervilleCourse.Ring).Contains(goal, -.8f), "The east goal circle crosses the measured course edge.");
            Check(devices.All(d => !d.Contains(goal, 1.3f)), "The east goal ends inside a periodic device's full footprint.");
            Check(nodes.Count(n => n.Jump) == 3, "The relay changes removed a measured jump.");
            foreach (var position in new[] { BlundervilleRoutes.VerifiedStart(BlundervilleCourse.Ring), new Vector3(218, 10, -134) })
            {
                var state = new BlundervilleRaceState();
                state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20015, 0, 0, 0, Epoch);
                var decision = new BlundervilleRacePlanner().Update(state, Epoch, position, true, false, false, null);
                Check(decision.Target == goal, "Starting on the closing Ring segment returned toward the last jump landing.");
            }
        });

        run("Blunderville ring columns cover every recorded long charge through transit", () =>
        {
            using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ring-recorded.json")));
            var count = 0;
            foreach (var recording in data.RootElement.GetProperty("recordings").EnumerateObject())
            {
                var encounter = New();
                foreach (var e in recording.Value.GetProperty("events").EnumerateArray().Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() == 35409))
                {
                    var now = Epoch.AddSeconds(e[0].GetDouble());
                    var from = Vec(e[4]); var to = Vec(e[6]); var id = e[2].GetUInt64();
                    encounter.Effect(id, 35409, e[7].GetUInt32(), from, e[5].GetSingle(), now, to);
                    var hazards = Columns(encounter, now).Where(h => h.Id.StartsWith($"ring/column/{id}/")).ToArray();
                    var sweep = hazards.Single(h => h.Id.EndsWith("/sweep"));
                    Check(sweep.Until == now.AddSeconds(.85) && sweep.Geometry.Contains((from + to) / 2), "Recorded transit is missing its long swept corridor.");
                    Check(hazards.Count(h => h.Geometry is CircleGeometry { Radius: 1.5f }) == 2, "Recorded charge lost a rounded endpoint.");
                    Check(hazards.All(h => h.HasMotionEnvelope), "Confirmed moving bodies lost the physical-envelope timing contract.");
                    ++count;
                }
            }
            Check(count == 653, $"Expected 653 recorded charge effects, got {count}.");
        });
    }

    private static BlundervilleEncounter New() => new(_ => { });
    private static BlundervilleHazard[] Columns(BlundervilleEncounter encounter, DateTime now)
        => encounter.TraversalHazards(now).Where(h => h.Id.StartsWith(BlundervilleRingColumns.Prefix)).ToArray();
    private static Vector3 Vec(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

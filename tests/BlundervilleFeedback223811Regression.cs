using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;

internal static class BlundervilleFeedback223811Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 13, 3, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville 223811 Final hammer feet publish danger in both sections", () =>
        {
            using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "delivery-feedback-223811.json")));
            using var safe = new ScriptSafeZoneApi(); var draw = new ScriptDrawApi();
            var encounter = new BlundervilleEncounter(_ => { }); var output = new BlundervilleOutput(safe, draw);
            var actors = new Dictionary<ulong, BlundervilleActor>(); uint sequence = 0;
            foreach (var e in data.RootElement.GetProperty("finalHammers").EnumerateArray())
            {
                var now = Epoch.AddSeconds(e[0].GetDouble()); var id = e[1].GetUInt64(); var action = e[2].GetUInt32(); var p = Vec(e[3]);
                actors[id] = new(id, 16338, p); encounter.Synchronize(actors.Values, now);
                encounter.Effect(id, action, ++sequence, p, e[4].GetSingle(), now);
                output.Apply(encounter, now, p, _ => true);
                var radius = action switch { 34801 => 6, 29795 => 3, _ => 5 };
                Check(draw.Previews.Values.Any(v => !v.Safe && v.Geometry is CircleGeometry c && c.Radius == radius
                    && Vector3.Distance(c.Position, p) < .01f && c.Contains(p)), $"Hammer {action} foot at {p} was not drawn as danger.");
            }
            Check(sequence == 214, "Final hammer fixture is incomplete.");
            output.Apply(encounter, Epoch.AddMinutes(2), new(200, 6, 250), _ => true);
            Check(draw.Previews.Count == 0, "Distant or expired hammers remain on the local map.");
        });
        run("Blunderville 223811 north animation covers recorded late contacts", () =>
        {
            using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "delivery-feedback-223811.json")));
            foreach (var contact in data.RootElement.GetProperty("northStuns").EnumerateArray())
            {
                var source = Vec(contact.GetProperty("source")); var position = Vec(contact.GetProperty("position"));
                var delay = contact.GetProperty("at").GetDouble() - contact.GetProperty("packet").GetDouble();
                var first = source.X < -200 ? .2 : 1.994;
                var schedule = new BlundervilleDeliverySchedule(); schedule.SetStart(Epoch);
                schedule.Observe(34799, source, Epoch.AddSeconds(first));
                var at = Epoch.AddSeconds(first + delay);
                Check(schedule.Hazards(at).Any(h => h.From <= at && h.Until >= at && h.Geometry.Contains(position)),
                    $"North hammer body absent {delay:F3}s after packet at {position}.");
            }
        });
        run("Blunderville 223811 yellow hammer animation remains visible after packet", () =>
        {
            using var safe = new ScriptSafeZoneApi(); var draw = new ScriptDrawApi();
            var encounter = new BlundervilleEncounter(_ => { }); var output = new BlundervilleOutput(safe, draw);
            encounter.DeliverySchedule.SetStart(Epoch);
            var at = Epoch.AddSeconds(1); var p = new Vector3(-203.021f, 5.997f, 218.006f);
            output.Apply(encounter, at, p, _ => true);
            Check(draw.Previews.Values.Any(v => !v.Safe && !v.OutlineOnly && v.Geometry.Contains(p)), "The live animation lost its danger fill.");
            encounter.Reset(); output.Apply(encounter, at, p, _ => true);
            Check(draw.Previews.Count == 0, "The hammer survived a round reset.");
        });
        run("Blunderville 223811 yellow carry from recorded pickup", () =>
            BlundervilleDeliveryScheduleRegression.Simulate("2026.10.14 22.38.11", "NWE",
                (new(-200.641f, 5.325f, 254.994f), 44.621), true, "delivery-feedback-223811.json", 30));
        run("Blunderville 223811 complete delivery round uses lower returns", () =>
            BlundervilleDeliveryScheduleRegression.Simulate("2026.10.14 22.38.11", "WNE",
                (new(-199.786f, 5.447f, 254.108f), 4.546), fixture: "delivery-feedback-223811.json"));
        foreach (var colour in new ushort[] { 35, 36, 37 })
        {
            run($"Blunderville 223811 keeps walking into deposit colour {colour}", () =>
            {
                var state = new BlundervilleRaceState();
                state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
                state.SynchronizeStatuses([new(3704, colour, DateTime.MaxValue)], Epoch);
                var target = state.DeliveryTarget!.Value;
                var planner = new BlundervilleRacePlanner();
                var decision = planner.Update(state, Epoch, target + Vector3.UnitZ, true, false, false, null);
                Check(!decision.Hold, "Stopped outside the confirmed deposit instead of walking in.");
                var profile = colour switch { 35 => BlundervilleFinalPlanner.DeliveryWest, 36 => BlundervilleFinalPlanner.DeliveryEast, _ => BlundervilleFinalPlanner.DeliveryNorth };
                Check(Vector3.Distance(profile.Spoke![^1], target) < .01f, "The continuous route ends before the deposit.");
            });
            run($"Blunderville 223811 physical successful return colour {colour}", () =>
                BlundervilleDeliveryRecoveryRegression.Simulate(colour, colour switch
                { 35 => new(-239.796f, 5.997f, 275.074f), 36 => new(-160.449f, 5.997f, 272.053f), _ => new(-199.6f, 5.997f, 207) }, true));
            run($"Blunderville 223811 successful return colour {colour}", () =>
            {
                foreach (var scoreFirst in new[] { false, true })
                {
                    var state = new BlundervilleRaceState();
                    state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
                    state.SynchronizeStatuses([new(3704, colour, DateTime.MaxValue)], Epoch);
                    var position = state.DeliveryTarget!.Value;
                    var planner = new BlundervilleRacePlanner();
                    if (scoreFirst) state.Director(BlundervilleRaceState.DirectorInstance, 3, 1, 0, 0, 0, Epoch);
                    state.SynchronizeStatuses([new(1548, 30, Epoch.AddSeconds(30))], Epoch);
                    var beforeScore = planner.Update(state, Epoch, position, true, false, false, null);
                    Check(beforeScore.RecoveryFloor != null, "Successful carry still uses the upper return.");
                    if (!scoreFirst) state.Director(BlundervilleRaceState.DirectorInstance, 3, 1, 0, 0, 0, Epoch);
                    var afterScore = planner.Update(state, Epoch, position, true, false, false, null);
                    Check(afterScore.Target == beforeScore.Target, "Delayed score reset the return.");
                    Check(!afterScore.Hint.Contains("失败"), "Successful return is labelled a failure.");
                    state.Director(BlundervilleRaceState.DirectorInstance, 3, 3, 0, 0, 0, Epoch);
                    Check(planner.Update(state, Epoch, position, true, false, false, null).RecoveryFloor == null,
                        "Third delivery started another return.");
                }
            });
        }
    }
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

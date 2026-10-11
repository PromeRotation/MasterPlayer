using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleLatestForecastRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville latest forecast coalesced movement preserves confirmed animation", () =>
        {
            using var replay = new Recording();
            replay.Advance(27.4);
            var now = Epoch.AddSeconds(27.4);
            var body = (BlundervilleBumperTracker)typeof(BlundervilleEncounter).GetField("_bumpers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(replay.Encounter)!;
            var tracks = (System.Collections.IDictionary)typeof(BlundervilleBumperTracker).GetField("_tracks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(body)!;
            var actors = (Dictionary<ulong, BlundervilleActor>)typeof(BlundervilleEncounter).GetField("_actors", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(replay.Encounter)!;
            var hazards = replay.Encounter.TraversalHazards(now).ToArray();
            Check(!hazards.Any(h => h.Id.StartsWith("bumper-unknown/")), "The recording's confirmed entry clocks became unknown.");
            foreach (var actor in actors.Values.Where(a => a.Id is 43 or 45 or 47 or 49).OrderBy(a => a.Id))
            {
                var track = tracks[actor.Id]!;
                var until = (DateTime)track.GetType().GetField("CoarseUntil")!.GetValue(track)!;
                var coarse = (bool)track.GetType().GetField("Coarse")!.GetValue(track)!;
                var uncertainty = (double)track.GetType().GetField("BatchUncertainty")!.GetValue(track)!;
                Check(replay.BodyReady(actor.Id) && replay.PulseReady(actor.Id, now) && !coarse && until < now && uncertainty > 0,
                    "Coalesced movement replaced a confirmed clock with a persistent full sweep.");
                foreach (var lead in new[] { 0, 8, 15 })
                {
                    var impact = now.AddSeconds(lead);
                    var slices = hazards.Where(h => h.Id.StartsWith($"bumper/{actor.Id}/") && h.From <= impact && h.Until >= impact);
                    if (lead == 0) Check(slices.All(h => h.Geometry.GetBounds().MaxX - h.Geometry.GetBounds().MinX < 3),
                        "The stationary endpoint retained the entire movement sweep after a valid packet batch.");
                    Console.WriteLine($"BODY {actor.Id} lead={lead} {string.Join(';', slices.Select(h => $"x=[{h.Geometry.GetBounds().MinX:F3},{h.Geometry.GetBounds().MaxX:F3}] width={h.Geometry.GetBounds().MaxX-h.Geometry.GetBounds().MinX:F3} from={(h.From-Epoch).TotalSeconds:F3}"))}");
                }
            }
        });
        foreach (var lead in new[] { .3, 1, 3, 8, 17.5, 20.5 })
            run($"Blunderville latest forecast moving body at {lead:F1}s", () =>
            {
                using var replay = new Recording();
                var covered = 0; var untrusted = 0;
                foreach (var e in replay.Events.Where(e => e[1].GetString() == "move" && e[0].GetDouble() > 38 + lead))
                {
                    var time = e[0].GetDouble();
                    replay.Advance(time - lead);
                    if (lead > 1 && !replay.BodyReady(e[2].GetUInt64()))
                    {
                        Check(!replay.Encounter.BumpersReady(Vec(e[3])), "An unlearned body clock authorized entering the bridge.");
                        ++untrusted;
                        continue;
                    }
                    var impact = Epoch.AddSeconds(time);
                    var hazards = replay.Encounter.Hazards(Epoch.AddSeconds(time - lead))
                        .Where(h => h.Id.StartsWith($"bumper/{e[2].GetUInt64()}/") && h.From <= impact && h.Until >= impact).ToArray();
                    var position = Vec(e[3]);
                    Check(Enumerable.Range(0,16).Select(i => position+new Vector3(MathF.Cos(i*MathF.Tau/16),0,MathF.Sin(i*MathF.Tau/16))*1.2f)
                        .All(p => hazards.Any(h => h.Geometry.Contains(p))), $"Body escaped the {lead:F1}s forecast: {e}.");
                    ++covered;
                }
                Check(covered > 500, "Insufficient latest-recording body samples.");
                Console.WriteLine($"LATEST BODY lead={lead:F1}s covered={covered} gated={untrusted}");
            });

        run("Blunderville latest forecast unconfirmed entry clocks remain blocked across the full route", () =>
        {
            using var replay = new Recording();
            // Two beats per endpoint (two complete cycles) confirm a clock since
            // 1.12.0; the z=-193/-203 cylinders reach their fourth beat at 21.42s.
            replay.Advance(21.3);
            var now = Epoch.AddSeconds(21.3);
            Check(!replay.Encounter.BumpersReady(new(-210, 17.47f, -158)), "An incomplete endpoint pattern authorized early entry.");
            var blocked = replay.Encounter.TraversalHazards(now).Where(h => h.Id.StartsWith("bumper-unknown/")).ToArray();
            var covered = 0;
            foreach (var e in replay.Events.Where(e => e[1].GetString() == "move" && e[0].GetDouble() > 21.3 && e[0].GetDouble() < 43))
            {
                var hazard = blocked.SingleOrDefault(h => h.Id == $"bumper-unknown/{e[2].GetUInt64()}");
                if (hazard == null) continue;
                var impact = Epoch.AddSeconds(e[0].GetDouble());
                var position = Vec(e[3]);
                Check(hazard.From <= impact && hazard.Until >= impact && new[] { -1, 1 }
                    .All(sign => hazard.Geometry.Contains(position + Vector3.UnitX * (sign * 1.2f))),
                    "An unconfirmed entry clock became a safe gap after its short body forecast expired.");
                ++covered;
            }
            Check(covered > 50, "Insufficient unconfirmed-lane trajectory evidence.");
        });

        foreach (var risk in new[] { "opposite phase", "different axis" })
            run($"Blunderville latest forecast rejects plausible-speed batches with {risk}", () =>
            {
                var (tracker, actor) = WarmBatchClock(risk == "opposite phase" ? 22 : 21.5);
                Check(tracker.Ready(actor.Id), "The batch risk was tested before confirming its animation clock.");
                var at = Epoch.AddSeconds(risk == "opposite phase" ? 22.6 : 22.1);
                var position = actor.Position + Vector3.UnitX * (risk == "opposite phase" ? -1.25f : 1.25f);
                if (risk == "different axis") position += Vector3.UnitZ * 3;
                tracker.Observe(actor with { Position = position }, at);
                var future = at.AddSeconds(1);
                Check(tracker.Hazards.Any(h => h.From <= future && h.Until >= future
                    && h.Geometry.Contains(new(-211.2f, 17.47f, -166))
                    && h.Geometry.Contains(new(-204.8f, 17.47f, -166))
                    && h.Geometry.Contains(position - Vector3.UnitX * 1.2f)
                    && h.Geometry.Contains(position + Vector3.UnitX * 1.2f)),
                    "A plausible update speed preserved a phase prediction inconsistent with the observed pose.");
            });

        run("Blunderville latest forecast all verified contact stuns", () =>
        {
            using var replay = new Recording();
            var contacts = replay.Data.RootElement.GetProperty("contacts").EnumerateArray().ToArray();
            var statuses = replay.Data.RootElement.GetProperty("contactStatuses").EnumerateArray().ToArray();
            Check(contacts.Length == 5 && statuses.Length == contacts.Length, "Verified contact evidence is incomplete.");
            foreach (var contact in contacts)
            {
                var time = contact[0].GetDouble();
                var state = statuses.Single(s => Math.Abs(s[0].GetDouble() - time) < .001);
                Check(new uint[] { 149, 529 }.All(id => state[2].EnumerateArray().Any(s => s[0].GetUInt32() == id)),
                    "Contact lacks recorded stun and slowdown confirmation.");
                replay.Advance(time - 1.3);
                Check(replay.Encounter.Hazards(Epoch.AddSeconds(time - 1.3)).Any(h => h.Id.StartsWith($"rhythm/{contact[1].GetUInt64()}/34717/")
                    && h.Geometry.Contains(Vec(contact[2])) && h.From <= Epoch.AddSeconds(time) && h.Until >= Epoch.AddSeconds(time)),
                    $"Verified contact escaped the endpoint animation forecast: {contact}.");
            }
        });

        run("Blunderville latest forecast endpoint animation before cast", () =>
        {
            using var replay = new Recording();
            var covered = 0;
            var untrusted = 0;
            foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[0].GetDouble() > 38))
            {
                var time = e[0].GetDouble();
                replay.Advance(time - 1.3);
                var predicted = replay.Encounter.Hazards(Epoch.AddSeconds(time - 1.3)).Any(h => h.Id.StartsWith($"rhythm/{e[2].GetUInt64()}/34717/")
                    && h.From.AddSeconds(-.5) <= Epoch.AddSeconds(time) && h.Until.AddSeconds(.3) >= Epoch.AddSeconds(time)
                    && h.Geometry.Contains(Vec(e[4])));
                if (!predicted)
                {
                    Check(!replay.PulseReady(e[2].GetUInt64(), Epoch.AddSeconds(time - 1.3)) && !replay.Encounter.BumpersReady(Vec(e[4])),
                        $"A trusted clock omitted the latest-recording animation: {e}.");
                    ++untrusted;
                    continue;
                }
                ++covered;
            }
            Check(covered > 100, "Insufficient endpoint animation samples.");
            Console.WriteLine($"LATEST PULSE covered={covered} gated={untrusted}");
        });

        run("Blunderville latest forecast covers a full route with an aged endpoint clock", () =>
        {
            var tracker = new BlundervilleRhythmTracker();
            var actor = new BlundervilleActor(1, 16339, new(-210, 17.47f, -166));
            var other = actor.Position + Vector3.UnitX * 4;
            var times = new[] { 0, 6.4, 12.8, 19.2, 25.6 };
            for (var i = 0; i < times.Length; ++i)
                tracker.Effect(actor, 34717, i % 2 == 0 ? actor.Position : other, 0, Epoch.AddSeconds(times[i]));
            var last = Epoch.AddSeconds(times[^1]);
            var now = last.AddSeconds(6.4);
            Check(tracker.Ready(actor.Id, 34717, now), "Valid endpoint clock expired before its next animation.");
            var impact = last.AddSeconds(25.6);
            Check(impact < now.AddSeconds(20.5) && tracker.Hazards(now).Any(h => h.From <= impact && h.Until >= impact
                && h.Geometry.Contains(actor.Position)), "Route horizon omitted an endpoint animation near the end of traversal.");
        });

        run("Blunderville latest forecast rejects an observed interval jump", () =>
        {
            var tracker = new BlundervilleRhythmTracker();
            var actor = new BlundervilleActor(1, 16339, new(-210, 17.47f, -166));
            var times = new[] { 0, 5.695, 11.503, 17.198, 23.006, 29.327 };
            for (var i = 0; i < times.Length; ++i)
            {
                var at = Epoch.AddSeconds(times[i]);
                tracker.Effect(actor, 34717, actor.Position + Vector3.UnitX * (i % 2 == 0 ? 0 : 4), 0, at);
                if (i == 4) Check(tracker.Ready(actor.Id, 34717, at), "Matching cycles were never confirmed.");
            }
            // Since 1.12.0 one deviating cycle is a late packet (01:01 ARR had
            // .8s jumps around a stable period). The clock stays trusted and its
            // forecast must cover both the cycle-consistent and the shifted beat.
            var last = Epoch.AddSeconds(times[^1]);
            Check(tracker.Ready(actor.Id, 34717, last), "A single late endpoint packet discarded a consistent clock.");
            foreach (var impact in new[] { times[3] + 2 * 11.503, times[^1] + 11.503 })
                Check(tracker.Hazards(last).Any(h => h.From <= Epoch.AddSeconds(impact) && h.Until >= Epoch.AddSeconds(impact)
                    && h.Geometry.Contains(actor.Position + Vector3.UnitX * 4)), $"The forecast lost the possible beat at {impact:F3}s.");
            var shifted = new BlundervilleRhythmTracker();
            var drift = new[] { 0, 5.695, 11.503, 17.198, 23.9, 30.2, 36.9 };
            for (var i = 0; i < drift.Length; ++i)
                shifted.Effect(actor, 34717, actor.Position + Vector3.UnitX * (i % 2 == 0 ? 0 : 4), 0, Epoch.AddSeconds(drift[i]));
            Check(!shifted.Ready(actor.Id, 34717, Epoch.AddSeconds(drift[^1])), "A mostly inconsistent clock retained trusted timing.");
        });

        run("Blunderville latest forecast retires coarse cached slices after a fresh cycle", () =>
        {
            var tracker = new BlundervilleBumperTracker();
            var actor = new BlundervilleActor(1, 16339, new(-210, 17.47f, -166));
            for (var tick = 0; tick <= 253; ++tick)
            {
                var time = tick / 10.0;
                var phase = time % 10;
                var offset = (float)(phase <= 5 ? phase * .8 : (10 - phase) * .8);
                var position = actor.Position + Vector3.UnitX * offset;
                if (tick == 151) position = actor.Position + Vector3.UnitX * 2;
                tracker.Observe(actor with { Position = position }, Epoch.AddSeconds(time));
                if (tick % 50 == 0) tracker.Effect(actor.Id, position, Epoch.AddSeconds(time));
            }
            var future = Epoch.AddSeconds(26);
            var slices = tracker.Hazards.Where(h => h.From <= future && h.Until >= future).ToArray();
            Check(tracker.Ready(actor.Id) && slices.Length > 0, "Fresh-cycle body timing was not available.");
            Check(slices.All(h => h.Geometry.GetBounds().MaxX - h.Geometry.GetBounds().MinX < 5),
                "A coarse forecast kept the full sweep blocked after fresh sampling replaced it.");
        });

        run("Blunderville latest forecast gates both entry bands while blocking unknown opposite lanes", () =>
        {
            var encounter = new BlundervilleEncounter(_ => { });
            var initial = new[]
            {
                new BlundervilleActor(1, 16339, new(-210, 17.47f, -166)),
                new BlundervilleActor(2, 16339, new(-210, 17.47f, -188)),
                new BlundervilleActor(3, 16339, new(-188, 17.47f, -166)),
                new BlundervilleActor(4, 16339, new(-188, 17.47f, -188)),
            };
            var actors = initial;
            for (var beat = 0; beat <= 4; ++beat)
            {
                var at = Epoch.AddSeconds(beat * 5);
                actors = initial.Select(a => a.Id <= 2 ? a with { Position = a.Position + Vector3.UnitX * (beat % 2 == 0 ? 0 : 4) } : a).ToArray();
                encounter.Synchronize(actors, at);
                foreach (var actor in actors.Where(a => a.Id <= 2))
                    encounter.Effect(actor.Id, 34717, (uint)(beat * 10) + (uint)actor.Id, actor.Position, 0, at);
                encounter.Synchronize(actors, at);
            }
            var now = Epoch.AddSeconds(20);
            var player = new Vector3(-210, 17.47f, -158);
            Check(encounter.BumpersReady(player), "Unknown opposite-lane clocks blocked two confirmed entry bands.");
            var blocked = encounter.TraversalHazards(now).Where(h => h.Id.StartsWith("bumper-unknown/")).ToArray();
            Check(blocked.Length == 2 && blocked.All(h => h.From <= now && h.Until >= now.AddSeconds(20.5)),
                "Unknown lanes were not blocked through the full traversal horizon.");
            foreach (var actor in actors.Where(a => a.Id > 2))
            {
                var hazard = blocked.Single(h => h.Id == $"bumper-unknown/{actor.Id}");
                Check(new[] { -1, 1 }.All(sign => hazard.Geometry.Contains(actor.Position + Vector3.UnitX * (sign * (4.028f + 3)))),
                    "A partial unknown sweep did not cover both possible endpoints and their pulses.");
                Check(!hazard.Geometry.Contains(player), "The unknown opposite-lane strip expanded into the confirmed entry lane.");
            }
            encounter.Synchronize(actors.Append(new(5, 16339, new(-210, 17.47f, -198))), now);
            Check(!encounter.BumpersReady(player), "An unknown ice device in the entry lane bypassed the second-band gate.");
        });

        run("Blunderville latest forecast unknown complete sweeps retain observed endpoints", () =>
        {
            var tracker = new BlundervilleBumperTracker();
            var actor = new BlundervilleActor(1, 16339, new(-188, 17.47f, -188));
            for (var tick = 0; tick <= 135; ++tick)
            {
                var phase = tick % 100;
                var offset = (phase <= 50 ? phase : 100 - phase) * .08f;
                tracker.Observe(actor with { Position = actor.Position + Vector3.UnitX * offset }, Epoch.AddSeconds(tick / 10.0));
            }
            var hazard = tracker.UnknownTraversalHazard(actor, Epoch.AddSeconds(13.5));
            var bounds = hazard.Geometry.GetBounds();
            Check(!tracker.Ready(actor.Id) && bounds.MaxX - bounds.MinX <= 10.1f, "Complete observed sweep retained an invented second travel range.");
            Check(hazard.Geometry.Contains(actor.Position - Vector3.UnitX * 3)
                && hazard.Geometry.Contains(actor.Position + Vector3.UnitX * 7), "Unknown complete sweep omitted either endpoint pulse.");
        });
    }

    private sealed class Recording : IDisposable
    {
        public readonly JsonDocument Data;
        public readonly JsonElement[] Events;
        public readonly BlundervilleEncounter Encounter = new(_ => { });
        private readonly Dictionary<ulong, BlundervilleActor> _actors;
        private readonly BlundervilleBumperTracker _body;
        private readonly BlundervilleRhythmTracker _pulse;
        private int _index;
        private double _sample = -1;

        public Recording()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _body = (BlundervilleBumperTracker)typeof(BlundervilleEncounter).GetField("_bumpers", flags)!.GetValue(Encounter)!;
            _pulse = (BlundervilleRhythmTracker)typeof(BlundervilleEncounter).GetField("_rhythms", flags)!.GetValue(Encounter)!;
            Data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "obstacles-firsta-044552.json")));
            Events = Data.RootElement.GetProperty("events").EnumerateArray().ToArray();
            _actors = Data.RootElement.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(),
                d.GetProperty("oid").GetUInt32(), Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
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
                var e = Events[_index++];
                var id = e[2].GetUInt64();
                var at = Epoch.AddSeconds(e[0].GetDouble());
                if (e[1].GetString() == "move") _actors[id] = _actors[id] with { Position = Vec(e[3]), Rotation = e[4].GetSingle() };
                else if (e[1].GetString() == "cast")
                {
                    var cast = new BlundervilleCast(e[3].GetUInt32(), Vec(e[5]), e[6].GetSingle(), at, at.AddSeconds(e[4].GetDouble() + .3), Vec(e[5]));
                    _actors[id] = _actors[id] with { Cast = cast };
                    Encounter.StartCast(id, cast, at);
                }
                else
                {
                    Encounter.Effect(id, e[3].GetUInt32(), (uint)_index, Vec(e[4]), e[5].GetSingle(), at);
                    _actors[id] = _actors[id] with { Cast = null };
                }
            }
            Encounter.Synchronize(_actors.Values, Epoch.AddSeconds(time));
        }

        public void Dispose() => Data.Dispose();
        public bool BodyReady(ulong id) => _body.Ready(id);
        public bool PulseReady(ulong id, DateTime now) => _pulse.Ready(id, 34717, now);
    }

    private static (BlundervilleBumperTracker Tracker, BlundervilleActor Actor) WarmBatchClock(double until)
    {
        var tracker = new BlundervilleBumperTracker();
        var origin = new Vector3(-210, 17.47f, -166);
        var actor = new BlundervilleActor(1, 16339, origin);
        for (var tick = 0; tick <= (int)Math.Round(until * 10); ++tick)
        {
            var time = tick / 10.0;
            var phase = time % 10;
            var offset = phase switch
            {
                < 1 => 0,
                < 3 => (phase - 1) * 2,
                < 6 => 4,
                < 8 => 4 - (phase - 6) * 2,
                _ => 0,
            };
            actor = actor with { Position = origin + Vector3.UnitX * (float)offset };
            var at = Epoch.AddSeconds(time);
            tracker.Observe(actor, at);
            if (tick % 50 == 0) tracker.Effect(actor.Id, actor.Position, at);
        }
        return (tracker, actor);
    }

    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

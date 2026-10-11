using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial;

internal static class BlundervilleIceMotionRegression
{
    private static readonly BlundervilleIceTiming Timing = BlundervilleIceTiming.Instance;

    public static void Run(Action<string, Action> run)
    {
        foreach (var fps in new[] { 30, 60 })
            run($"Blunderville ice timing recorded acceleration at {fps}fps", () =>
            {
                foreach (var profile in Profiles())
                {
                    var slices = Timing.Sweep(profile.Origin, profile.Target, 3);
                    for (var frame = 0; frame <= Math.Ceiling(profile.Duration * fps); ++frame)
                    {
                        var time = Math.Min(profile.Duration, (double)frame / fps);
                        Contains(slices, profile.Position(time), time);
                    }
                }
            });

        run("Blunderville ice timing constant speed and length envelope", () =>
        {
            foreach (var speed in new[] { 2.88f, 2.94f, 3f })
                foreach (var length in new[] { 4.88f, 5f, 5.1f })
                {
                    var origin = new Vector3(-210, 17.47f, -183); var target = origin - Vector3.UnitZ * length;
                    var slices = Timing.Sweep(origin, target, 3);
                    for (var frame = 0; frame <= Math.Ceiling(length / speed * 60); ++frame)
                    {
                        var time = Math.Min(length / speed, (double)frame / 60);
                        Contains(slices, origin - Vector3.UnitZ * Math.Min(length, speed * (float)time), time);
                    }
                }
        });

        run("Blunderville ice timing oblique entry uses free running distance", () =>
        {
            var origin = new Vector3(-211, 17.47f, -180.4f); var target = origin + new Vector3(3, 0, -4);
            var slices = Timing.Sweep(origin, target, 3);
            Near(Timing.Duration(origin, target, 3), 1.5, "The diagonal entry did not use its physical free-run distance.");
            foreach (var entry in new[] { -181f, -181.4f })
            {
                var direction = Vector3.Normalize(target - origin); var free = (entry - origin.Z) / direction.Z;
                foreach (var speed in new[] { 2.88f, 3f })
                    for (var frame = 0; frame <= 120; ++frame)
                    {
                        var time = (double)frame / 60;
                        var distance = time < free / 6 ? time * 6 : free + (time - free / 6) * speed;
                        if (distance > 5 + .000001) break;
                        Contains(slices, origin + direction * (float)distance, time);
                    }
            }
        });

        foreach (var fps in new[] { 30, 60 })
            run($"Blunderville ice entry covers sampled dry-frame overrun at {fps}fps", () =>
            {
                var curves = new List<(float Length, double Duration, Func<double, float> Progress)>();
                foreach (var length in new[] { 4.88f, 5.1f })
                {
                    foreach (var profile in Profiles())
                    {
                        var scale = length / profile.Length;
                        curves.Add((length, profile.Duration * scale, time => profile.Progress(time / scale) * scale));
                    }
                    foreach (var speed in new[] { 2.88f, 3f })
                        curves.Add((length, length / speed, time => Math.Min(length, speed * (float)time)));
                }
                foreach (var direction in new[] { new Vector3(0, 0, -1), new Vector3(.6f, 0, -.8f), new Vector3(.96f, 0, -.28f) })
                    foreach (var entry in new[] { -181f, -181.2f, -181.4f })
                        foreach (var phase in new[] { 0f, .04f, .1f, .19f })
                        {
                            var origin = new Vector3(-210, 17.47f, -180.75f) + direction * phase;
                            var dryDistance = (entry - origin.Z) / direction.Z;
                            // The independent host oracle samples state first,
                            // then moves the entire dry frame across the line.
                            // Also cover both continuous delay endpoints.
                            var sampledDry = Math.Ceiling(dryDistance / 6 * fps) * 6 / fps;
                            foreach (var free in new[] { (double)dryDistance, sampledDry, dryDistance + .2 })
                                foreach (var curve in curves)
                                {
                                    var target = origin + direction * (float)(free + curve.Length);
                                    var slices = Timing.Sweep(origin, target, 3); var clock = Timing.Prepare(origin, target);
                                    var freeTime = free / 6; var end = freeTime + curve.Duration;
                                    for (var frame = 0; frame <= Math.Ceiling(end * fps); ++frame)
                                        Validate(Math.Min(end, (double)frame / fps));
                                    // These sub-frame samples exercise the last
                                    // dry portion and first accelerated portion
                                    // sharing one wall-clock sample interval.
                                    Validate(Math.Max(0, freeTime - .5 / fps));
                                    Validate(freeTime); Validate(freeTime + .5 / fps); Validate(end);

                                    void Validate(double time)
                                    {
                                        var distance = time <= freeTime ? time * 6 : free + curve.Progress(time - freeTime);
                                        var point = origin + direction * (float)distance;
                                        Contains(slices, point, time); Occupied(clock, point, time);
                                        // Quantized world coordinates put the
                                        // entry's stationary jump within one
                                        // spatial ULP of the ideal oracle ray.
                                        // Bound only that coordinate rounding;
                                        // the raw clock gains no frame slack.
                                        var rounding = CoordinateRounding(new(origin, target, 0, 0));
                                        var earliest = clock.TimeBoundsAt(Math.Max(0, distance - rounding)).Earliest;
                                        var latest = clock.TimeBoundsAt(distance + rounding).Latest;
                                        Check(time >= earliest - .00002 && time <= latest + .00002,
                                            $"The entry clock omitted a dry-frame overrun at {fps}fps: entry={entry}, free={free:F4}, distance={distance:F4}, time={time:F5}, bounds=({earliest},{latest}).");
                                    }
                                }
                        }
            });

        run("Blunderville ice timing exit switches to running in space and time", () =>
        {
            var origin = new Vector3(-210, 17.47f, -202.5f); var target = origin - Vector3.UnitZ * 5;
            var slices = Timing.Sweep(origin, target, 3);
            Near(Timing.Duration(origin, target, 3), 2.0 / 3 + 3.0 / 6, "The dry exit retained sliding speed.");
            foreach (var profile in Profiles())
            {
                var crossing = profile.TimeAt(2);
                for (var frame = 0; frame <= 150; ++frame)
                {
                    var time = (double)frame / 60;
                    var distance = time <= crossing ? profile.Progress(time) : 2 + (time - crossing) * 6;
                    if (distance > 5 + .000001) break;
                    Contains(slices, origin - Vector3.UnitZ * (float)distance, time);
                }
            }
        });

        run("Blunderville ice timing covers hazards missed by constant speed", () =>
        {
            var profile = Profiles()[0]; var origin = new Vector3(-210, 17.47f, -183);
            var target = origin - Vector3.UnitZ * profile.Length; var slices = Timing.Sweep(origin, target, 3);
            foreach (var distance in new[] { .5f, 4f })
            {
                var time = profile.TimeAt(distance); var point = origin - Vector3.UnitZ * distance;
                Check(Math.Abs(time - distance / 3) > .07, "The negative control lost the measured acceleration timing error.");
                Contains(slices, point, time);
            }
        });

        run("Blunderville ice timing ordinary and stationary segments", () =>
        {
            var origin = new Vector3(-210, 17.47f, -183); var target = origin - Vector3.UnitZ * 5;
            Near(Timing.Duration(origin, target, 6), 5.0 / 6, "A normal point inherited ice timing.");
            foreach (var slice in Timing.Sweep(origin, target, 6))
                Check(slice.EarliestStart <= Distance(origin, slice.From) / 6 + .00001
                    && slice.LatestEnd >= Distance(origin, slice.To) / 6 - .00001, "A normal slice excluded its physical arrival.");
            var stationary = Timing.Sweep(origin, origin, 3);
            Check(stationary.Count == 1 && stationary[0].EarliestStart == 0 && stationary[0].LatestEnd == 0,
                "A stationary endpoint acquired a fictitious slide duration.");
        });

        foreach (var fps in new[] { 30, 60 })
            run($"Blunderville ice inverse occupancy recorded acceleration at {fps}fps", () =>
            {
                foreach (var profile in Profiles())
                {
                    var clock = Timing.Prepare(profile.Origin, profile.Target);
                    for (var frame = 0; frame <= Math.Ceiling(profile.Duration * fps); ++frame)
                    {
                        var time = Math.Min(profile.Duration, (double)frame / fps);
                        Occupied(clock, profile.Position(time), time);
                    }
                }
            });

        run("Blunderville ice inverse occupancy entry and exit independent physics", () =>
        {
            foreach (var profile in Profiles())
            {
                foreach (var entry in new[] { -181f, -181.4f })
                {
                    var origin = new Vector3(-210, 17.47f, -180.4f); var direction = new Vector3(.6f, 0, -.8f);
                    var free = (entry - origin.Z) / direction.Z; var target = origin + direction * (free + profile.Length);
                    var clock = Timing.Prepare(origin, target);
                    for (var frame = 0; frame <= Math.Ceiling((profile.Duration + free / 6) * 60); ++frame)
                    {
                        var time = Math.Min(profile.Duration + free / 6, (double)frame / 60);
                        var distance = time < free / 6 ? (float)time * 6 : free + profile.Progress(time - free / 6);
                        Occupied(clock, origin + direction * distance, time);
                    }
                }
                {
                    var origin = new Vector3(-210, 17.47f, -202.5f); var target = origin - Vector3.UnitZ * 5;
                    var clock = Timing.Prepare(origin, target); var crossing = profile.TimeAt(2);
                    for (var frame = 0; frame <= 120; ++frame)
                    {
                        var time = (double)frame / 60;
                        var distance = time < crossing ? profile.Progress(time) : 2 + (time - crossing) * 6;
                        if (distance > 5 + .000001) break;
                        Occupied(clock, origin - Vector3.UnitZ * (float)distance, time);
                    }
                }
            }
        });

        run("Blunderville ice inverse occupancy excludes inactive times and fixed spatial slices", () =>
        {
            var origin = new Vector3(-210, 17.47f, -183); var target = origin - Vector3.UnitZ * 5;
            var clock = Timing.Prepare(origin, target);
            Check(clock.Occupancy(-2, -.04) == null && clock.Occupancy(clock.LatestDuration + .04, clock.LatestDuration + 1) == null,
                "A hazard outside the physical segment acquired occupancy.");
            var occupancy = clock.Occupancy(.85, .85);
            Check(occupancy != null && Distance(occupancy.Value.From, occupancy.Value.To) < .5f,
                "The inverse retained the avoidable spatial width of fixed slices.");
            var all = clock.Occupancy(-1, clock.LatestDuration + 1);
            Check(all != null && Distance(all.Value.From, origin) < .0001f && Distance(all.Value.To, target) < .0001f,
                "The full time interval lost part of the physical segment.");
            Check(clock.Occupancy(1, 0) == null && clock.Occupancy(double.NaN, 1) == null,
                "An invalid danger window acquired occupancy.");
        });

        run("Blunderville ice shared clock preserves arbitrary ray lengths and measured remaining time", () =>
        {
            var origin = new Vector3(-210, 17.47f, -183);
            foreach (var length in new[] { .1f, .35f, 1f, 2.5f, 4.88f, 5f, 5.1f, 5.2f })
            {
                var target = origin - Vector3.UnitZ * length; var clock = Timing.Prepare(origin, target);
                var all = clock.Occupancy(-1, clock.LatestDuration + 1);
                Check(all != null && Distance(all.Value.From, origin) < .0001f && Distance(all.Value.To, target) < .0001f,
                    "The shared time curve extended an actual ray to its precomputed length.");
                var end = clock.TimeBoundsAt(length);
                Near(end.Earliest, clock.EarliestDuration, "The shared curve used the wrong earliest endpoint time.");
                Near(end.Latest, clock.LatestDuration, "The shared curve used the wrong latest endpoint time.");
                foreach (var speed in new[] { 2.88f, 3f })
                    for (var frame = 0; frame <= Math.Ceiling(length / speed * 60); ++frame)
                    {
                        var time = Math.Min(length / speed, (double)frame / 60);
                        Occupied(clock, origin - Vector3.UnitZ * Math.Min(length, speed * (float)time), time);
                    }
            }
            foreach (var profile in Profiles())
            {
                var clock = Timing.Prepare(origin, origin - Vector3.UnitZ * profile.Length);
                for (var frame = 0; frame <= Math.Ceiling(profile.Duration * 60); ++frame)
                {
                    var time = Math.Min(profile.Duration, (double)frame / 60); var distance = profile.Progress(time);
                    Occupied(clock, origin - Vector3.UnitZ * distance, time);
                    var actualRemaining = profile.Duration - time;
                    foreach (var remaining in new[] { clock.Remaining(distance), clock.Remaining(distance, time) })
                        Check(actualRemaining >= remaining.Earliest - 1.0 / 30 && actualRemaining <= remaining.Latest + 1.0 / 30,
                            "The observed progress or elapsed time excluded the measured remaining slide duration.");
                }
            }
        });

        run("Blunderville ice shared clock avoids rebuilding curve arrays per ray", () =>
        {
            var origin = new Vector3(-210, 17.47f, -183);
            Timing.Prepare(origin, origin - Vector3.UnitZ * 5).Occupancy(.5, .7);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2000; ++i)
            {
                var distance = .1f + i % 50 * .1f;
                var clock = Timing.Prepare(origin, origin - Vector3.UnitZ * distance);
                clock.Occupancy(.1, .15); GC.KeepAlive(clock);
            }
            var allocation = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocation <= 2000 * 160, $"Preparing short ice rays allocated {allocation / 2000} bytes per ray by rebuilding curve arrays.");
        });
    }

    private sealed record Profile(Vector3 Origin, Vector3 Target, float Length, double Duration,
        IReadOnlyList<(double Time, float Distance)> Samples)
    {
        public float Progress(double time)
        {
            if (time >= Duration) return Length;
            for (var i = 1; i < Samples.Count; ++i)
                if (time <= Samples[i].Time)
                {
                    var a = Samples[i - 1]; var b = Samples[i];
                    return float.Lerp(a.Distance, b.Distance, (float)((time - a.Time) / (b.Time - a.Time)));
                }
            return Length;
        }

        public double TimeAt(float distance)
        {
            for (var i = 1; i < Samples.Count; ++i)
            {
                var a = Samples[i - 1]; var b = Samples[i];
                if (distance <= b.Distance && b.Distance > a.Distance)
                    return a.Time + (b.Time - a.Time) * (distance - a.Distance) / (b.Distance - a.Distance);
            }
            return Duration;
        }

        public Vector3 Position(double time) => Origin + Vector3.Normalize(Target - Origin) * Progress(time);
    }

    private static Profile[] Profiles()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Blunderville", "ice-firsta-044552.json")));
        var events = document.RootElement.GetProperty("events").EnumerateArray().ToArray();
        return document.RootElement.GetProperty("iceSlides").EnumerateArray().Take(2).Select(slide =>
        {
            var origin = Vec(slide.GetProperty("startPos")); var target = Vec(slide.GetProperty("endPos"));
            var start = slide.GetProperty("start").GetDouble(); var end = slide.GetProperty("end").GetDouble();
            var length = Distance(origin, target); var direction = Vector3.Normalize(target - origin);
            var samples = new List<(double Time, float Distance)> { (0, 0) };
            foreach (var batch in events.Where(e => e[1].GetString() == "move" && e[2].GetUInt64() == 1
                && e[0].GetDouble() > start && e[0].GetDouble() < end).GroupBy(e => e[0].GetDouble()).OrderBy(g => g.Key))
                samples.Add((batch.Key - start, Math.Clamp(Vector3.Dot(Vec(batch.Last()[3]) - origin, direction), samples[^1].Distance, length)));
            samples.Add((end - start, length));
            Check(samples.Count >= 6 && length is > 4.8f and < 5.1f, "The independent slide oracle lost its server samples.");
            return new Profile(origin, target, length, end - start, samples);
        }).ToArray();
    }

    private static void Contains(IReadOnlyList<DirectTraversalSlice> slices, Vector3 point, double time)
    {
        var found = slices.Any(slice => DistanceToSegment(point, slice.From, slice.To) <= .0001f
            && time >= slice.EarliestStart - .00001 && time <= slice.LatestEnd + .00001);
        Check(found, $"The ice timing envelope excluded physical position {point} at {time:F4}s.");
        Check(slices.All(slice => Distance(slice.From, slice.To) <= .25f + CoordinateRounding(slice)
            && slice.EarliestStart <= slice.LatestEnd),
            "The timing sweep lost its spatial bound.");
    }

    private static void Occupied(BlundervilleIceTiming.IceTraversalClock clock, Vector3 point, double time)
    {
        var occupied = clock.Occupancy(time, time);
        Check(occupied != null && DistanceToSegment(point, occupied.Value.From, occupied.Value.To) <= .0001f,
            $"The inverse ice timing excluded physical position {point} at {time:F4}s.");
    }

    private static float CoordinateRounding(DirectTraversalSlice slice)
    {
        var x = Math.Max(Math.Abs(slice.From.X), Math.Abs(slice.To.X));
        var z = Math.Max(Math.Abs(slice.From.Z), Math.Abs(slice.To.Z));
        var ulpX = MathF.BitIncrement(x) - x; var ulpZ = MathF.BitIncrement(z) - z;
        return 2 * MathF.Sqrt(ulpX * ulpX + ulpZ * ulpZ);
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var delta = to - from;
        var fraction = delta.LengthSquared() > .000001f ? Math.Clamp(Vector3.Dot(point - from, delta) / delta.LengthSquared(), 0, 1) : 0;
        return Distance(point, from + delta * fraction);
    }
    private static Vector3 Vec(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());
    private static float Distance(Vector3 from, Vector3 to) => Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z));
    private static void Near(double actual, double expected, string message) => Check(Math.Abs(actual - expected) < .00002, message);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}

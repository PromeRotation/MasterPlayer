using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleSlideEnvelopeRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville slide envelope contains mixed physical slide lengths and starting positions", () =>
            Verify(new(-210, 17, -190.1f), new(-210, 17, -189.9f), .04f, new(-205.1f, 17, -190), true));
        run("Blunderville slide envelope contains oblique entry positions and both trigger depths", () =>
            Verify(new(-210, 17, -180.55f), new(-209.9f, 17, -180.45f), .02f, new(-207, 17, -185.5f), false));
        run("Blunderville slide envelope contains entry kinks inside an uncertain starting segment", () =>
            Verify(new(-210, 17, -181.2f), new(-209.9f, 17, -180.8f), .02f, new(-207, 17, -185.5f), false));
        run("Blunderville slide envelope contains 30 and 60 FPS oblique entry overruns", () =>
            VerifyFrameEntry(new(-210, 17, -180.55f), new(-209.9f, 17, -180.45f), .02f, new(-207, 17, -185.5f)));
        run("Blunderville slide envelope contains frame overruns at entry kinks and capsule trigger crossings", () =>
        {
            VerifyFrameEntry(new(-210, 17, -181.2f), new(-209.9f, 17, -180.8f), .02f, new(-207, 17, -185.5f));
            VerifyFrameEntry(new(-210, 17, -181.005f), new(-209.98f, 17, -181.005f), .02f, new(-207, 17, -185.5f));
        });
        run("Blunderville slide envelope nominal entry clock retains ordinary speed through the frame overrun", () =>
        {
            var start = new Vector3(-210, 17, -180.7f); var target = new Vector3(-207, 17, -185.5f);
            var heading = Vector3.Normalize(target - start);
            var free = (-181.2f - start.Z) / heading.Z;
            var result = BlundervilleSlideEnvelope.Propagate(start, start, 0, start, target, false)!;
            Check(Vector3.Distance(result.Nominal, start + heading * (free + .1f + 5)) < .0001f,
                "The first slide endpoint did not include the ordinary overrun along the native ray.");
            Check(Math.Abs(result.NominalDuration - ((free + .1) / 6 + 5.0 / 3)) < .00002,
                "The entry overrun was timed at ice speed or added to the locked slide length.");
        });
        run("Blunderville slide envelope entry frame uncertainty applies only to the first slide", () =>
        {
            var start = new Vector3(-210, 17, -190); var target = new Vector3(-205.1f, 17, -190);
            var continuous = BlundervilleSlideEnvelope.Propagate(start, start, .02f, start, target, true, 0)!;
            var framed = BlundervilleSlideEnvelope.Propagate(start, start, .02f, start, target, true, .2f)!;
            Check(continuous == framed, "Every locked slide accumulated another ordinary entry frame.");
        });
        run("Blunderville swept slide supports contain the full transverse incoming capsule midway through movement", () =>
            VerifySwept(new(-210.5f, 17, -190), new(-209.5f, 17, -190), .02f, new(-210, 17, -195), true));
        run("Blunderville swept slide supports retain unit gain on a radial incoming axis", () =>
        {
            var a = new Vector3(-210, 17, -190); var b = new Vector3(-209.8f, 17, -190);
            var sweep = BlundervilleSlideEnvelope.PrepareSweep(a, b, .02f, (a + b) / 2, new(-205, 17, -190), true)!;
            Check(sweep.Growth == 1 && sweep.Supports.Count <= 4 && sweep.SweepRadius <= .0701f,
                "A radial starting capsule acquired unnecessary tangential gain or support points.");
            VerifySwept(a, b, .02f, new(-205, 17, -190), true);
        });
        run("Blunderville swept slide entry clocks contain independent acceleration with mixed triggers and frame overruns", () =>
        {
            VerifySwept(new(-210, 17, -180.55f), new(-209.9f, 17, -180.45f), .02f, new(-207, 17, -185.5f), false);
            VerifySwept(new(-210, 17, -181.2f), new(-209.9f, 17, -180.8f), .02f, new(-207, 17, -185.5f), false);
        });
        run("Blunderville swept slide clocks contain partial and whole dry exits from a grazing capsule", () =>
            VerifySwept(new(-210, 17, -204.48f), new(-210, 17, -204.52f), .02f, new(-205.1f, 17, -204.5f), true));
        run("Blunderville swept slide exit retains ice speed until the capsule reaches dry ground",()=>
        {
            var start=new Vector3(-207,17,-200);var target=new Vector3(-207,17,-204.5f);
            VerifySwept(start-Vector3.UnitX*.06f,start+Vector3.UnitX*.06f,.03f,target,true);
            var envelope=BlundervilleSlideEnvelope.Propagate(start,start,.03f,start,target,true)!;
            Check(envelope.EarliestDuration>1.3&&envelope.LatestDuration<1.8,
                "A longitudinal exit was assigned whole-slide dry speed or an unbounded exit clock.");
        });
        run("Blunderville slide envelope radial motion does not accumulate tangential curvature", () =>
        {
            var result = BlundervilleSlideEnvelope.Propagate(new(-210, 17, -190), new(-209.8f, 17, -190), .02f,
                new(-209.9f, 17, -190), new(-205, 17, -190), true);
            Check(result != null && result.Radius <= .0201f, "A radial ray accumulated curvature unsupported by physical motion.");
        });
        run("Blunderville slide envelope keeps independent constant-speed timing bounds", () =>
        {
            var a = new Vector3(-210, 17, -190); var q = new Vector3(-205.1f, 17, -190);
            var result = BlundervilleSlideEnvelope.Propagate(a, a, 0, a, q, true)!;
            foreach (var length in new[] { 4.88f, 5.1f })
            foreach (var speed in new[] { 2.88f, 2.93f, 3f })
                Check(result.EarliestDuration <= (double)length / speed && result.LatestDuration >= (double)length / speed,
                    "Spatial endpoint selection discarded an independent physical speed or slide length.");
        });
        run("Blunderville slide envelope rejects a nearly parallel ambiguous entry and an undefined native ray", () =>
        {
            var a = new Vector3(-210, 17, -181.5f);
            Check(BlundervilleSlideEnvelope.Propagate(a, a, .1f, a, new(-205, 17, -181.51f), false) == null,
                "A nearly parallel entry produced a finite precise envelope.");
            Check(BlundervilleSlideEnvelope.Propagate(a, a, .1f, a, a, true) == null,
                "A target inside the start capsule acquired a physical heading.");
            Check(BlundervilleSlideEnvelope.Propagate(a, a, .1f, a, new(-205, 17, -185), false, float.NaN) == null
                && BlundervilleSlideEnvelope.Propagate(a, a, .1f, a, new(-205, 17, -185), false, -.1f) == null,
                "An invalid frame overrun acquired a finite physical envelope.");
        });
    }

    private static void VerifySwept(Vector3 a, Vector3 b, float radius, Vector3 target, bool onIce)
    {
        var sweep = BlundervilleSlideEnvelope.PrepareSweep(a, b, radius, (a + b) / 2, target, onIce);
        Check(sweep != null, "The swept capsule fixture was rejected.");
        Check(sweep!.SweepRadius <= radius * sweep.Growth + .05001f,
            "The uniform support mesh lost its fixed .05m approximation bound.");
        foreach (var fps in new[] { 30, 60 })
        foreach (var along in new[] { .07f, .27f, .57f, .91f })
        for (var angle = -1; angle < 4; ++angle)
        foreach (var length in new[] { 4.88f, 4.99f, 5.1f })
        foreach (var entry in onIce ? new[] { -181.2f } : new[] { -181f, -181.2f, -181.4f })
        {
            var theta = angle * MathF.Tau / 4;
            var start = Vector3.Lerp(a, b, along)
                + (angle < 0 ? Vector3.Zero : new Vector3(MathF.Cos(theta), 0, MathF.Sin(theta)) * radius);
            var heading = target - start; heading.Y = 0; heading = Vector3.Normalize(heading);
            var free = onIce || start.Z <= entry ? 0 : (entry - start.Z) / heading.Z;
            var sampledFree = Math.Ceiling(free / 6 * fps) * 6 / fps;
            foreach (var walking in onIce ? new[] { 0.0 } : new[] { (double)free, sampledFree, free + .1, free + .2 })
            {
                for (var frame = 0; frame <= Math.Ceiling(walking / 6 * fps); ++frame)
                {
                    var time = Math.Min(walking / 6, (double)frame / fps);
                    Contains(start + heading * (float)(time * 6), time);
                }
                foreach (var curve in Curves.Value)
                {
                    var scale = length / curve.Length;
                    var slideStart = start + heading * (float)walking;
                    var exitDistance = slideStart.Z < -204.5f ? 0
                        : heading.Z < -.000001f ? Math.Clamp((-204.5f - slideStart.Z) / heading.Z, 0, length) : length;
                    var crossingTime = curve.TimeAt(exitDistance / scale) * scale;
                    var duration = exitDistance < length
                        ? crossingTime + (length - exitDistance) / 6 : curve.Duration * scale;
                    for (var frame = 0; frame <= Math.Ceiling(duration * fps); frame += 3)
                        CheckTime(Math.Min(duration, (double)frame / fps));
                    CheckTime(0); CheckTime(duration); CheckTime(crossingTime);

                    void CheckTime(double time)
                    {
                        var slideDistance = exitDistance >= length || time <= crossingTime ? curve.Progress(time / scale) * scale
                            : exitDistance + (float)((time - crossingTime) * 6);
                        Contains(slideStart + heading * slideDistance, walking / 6 + time);
                    }
                }
            }
        }

        void Contains(Vector3 point, double time)
        {
            var found = sweep.Supports.Any(support => support.Clock.Occupancy(time - sweep.TimeUncertainty, time + sweep.TimeUncertainty) is { } occupied
                && DistanceToSegment(point, occupied.From, occupied.To) <= sweep.SweepRadius + .003f);
            Check(found, $"A physical intermediate position escaped all swept supports: point={point}, time={time:F5}, radius={sweep.SweepRadius:F5}.");
        }
    }

    private sealed record Curve(float Length, double Duration, IReadOnlyList<(double Time, float Distance)> Samples)
    {
        public float Progress(double time)
        {
            if (time <= 0) return 0;
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
            if (distance <= 0) return 0;
            for (var i = 1; i < Samples.Count; ++i)
                if (distance <= Samples[i].Distance && Samples[i].Distance > Samples[i - 1].Distance)
                {
                    var a = Samples[i - 1]; var b = Samples[i];
                    return a.Time + (b.Time - a.Time) * (distance - a.Distance) / (b.Distance - a.Distance);
                }
            return Duration;
        }
    }
    private static readonly Lazy<Curve[]> Curves = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "Blunderville", "ice-firsta-044552.json")));
        var events = document.RootElement.GetProperty("events").EnumerateArray().ToArray();
        var curves = new List<Curve>();
        foreach (var slide in document.RootElement.GetProperty("iceSlides").EnumerateArray().Take(2))
        {
            var origin = Vector(slide.GetProperty("startPos")); var endPoint = Vector(slide.GetProperty("endPos"));
            var start = slide.GetProperty("start").GetDouble(); var end = slide.GetProperty("end").GetDouble();
            var length = Vector3.Distance(origin, endPoint); var heading = Vector3.Normalize(endPoint - origin);
            var samples = new List<(double Time, float Distance)> { (0, 0) };
            foreach (var batch in events.Where(e => e[1].GetString() == "move" && e[2].GetUInt64() == 1
                && e[0].GetDouble() > start && e[0].GetDouble() < end).GroupBy(e => e[0].GetDouble()).OrderBy(group => group.Key))
                samples.Add((batch.Key - start, Math.Clamp(Vector3.Dot(Vector(batch.Last()[3]) - origin, heading), samples[^1].Distance, length)));
            samples.Add((end - start, length));
            Check(samples.Count >= 6, "The independent swept-motion oracle lost its recorded acceleration samples.");
            curves.Add(new(length, end - start, samples));
        }
        foreach (var speed in new[] { 2.88f, 3f }) curves.Add(new(5, 5.0 / speed, [(0, 0), (5.0 / speed, 5)]));
        return curves.ToArray();

        static Vector3 Vector(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());
    });

    private static void VerifyFrameEntry(Vector3 a, Vector3 b, float radius, Vector3 target)
    {
        var result = BlundervilleSlideEnvelope.Propagate(a, b, radius, (a + b) / 2, target, false, .2f);
        Check(result != null, "The frame-entry envelope fixture was rejected.");
        foreach (var framesPerSecond in new[] { 30, 60 })
        for (var along = 0; along <= 10; ++along)
        for (var angle = -1; angle < 8; ++angle)
        foreach (var length in new[] { 4.88f, 5.1f })
        foreach (var entry in new[] { -181f, -181.2f, -181.4f })
        {
            var theta = angle * MathF.Tau / 8;
            var start = Vector3.Lerp(a, b, along / 10f)
                + (angle < 0 ? Vector3.Zero : new Vector3(MathF.Cos(theta), 0, MathF.Sin(theta)) * radius);
            var physical = TraverseFrames(start, target, entry, length, framesPerSecond);
            Check(DistanceToSegment(physical.Position, result!.AxisA, result.AxisB) <= result.Radius + .006f,
                $"Independent {framesPerSecond} FPS entry movement escaped the propagated endpoint capsule.");
            Check(physical.Time + .0001 >= result.EarliestDuration && physical.Time <= result.LatestDuration + .0001,
                $"Independent {framesPerSecond} FPS entry movement escaped the propagated time interval.");
        }
    }

    private static void Verify(Vector3 a, Vector3 b, float radius, Vector3 target, bool ice)
    {
        var result = BlundervilleSlideEnvelope.Propagate(a, b, radius, (a + b) / 2, target, ice);
        Check(result != null, "The physical envelope fixture was rejected.");
        for (var along = 0; along <= 10; ++along)
        for (var angle = 0; angle < 8; ++angle)
        foreach (var length in new[] { 4.88f, 4.99f, 5.1f })
        foreach (var entry in ice ? new[] { -181f } : new[] { -181f, -181.4f })
        {
            var theta = angle * MathF.Tau / 8;
            var start = Vector3.Lerp(a, b, along / 10f) + new Vector3(MathF.Cos(theta), 0, MathF.Sin(theta)) * radius;
            var physical = Traverse(start, target, ice, entry, length);
            Check(DistanceToSegment(physical.Position, result!.AxisA, result.AxisB) <= result.Radius + .006f,
                "Independent physical movement escaped the propagated endpoint capsule.");
            Check(physical.Time + .0001 >= result.EarliestDuration && physical.Time <= result.LatestDuration + .0001,
                "Independent physical movement escaped the propagated time interval.");
        }
    }

    // Advance ordinary input at 6m/s until the trigger, then retain its locked
    // direction for the selected slide length. The trigger can occur inside a
    // physics step; endpoint bounds here are continuous, not frame overshoot.
    private static (Vector3 Position, double Time) Traverse(Vector3 start, Vector3 target, bool ice, float entry, float length)
    {
        var position = start; var delta = target - start; delta.Y = 0; var heading = Vector3.Normalize(delta);
        var time = 0.0; var walking = 0.0;
        while (!ice && position.Z > entry)
        {
            var step = .006f;
            if (position.Z + heading.Z * step <= entry) step = (entry - position.Z) / heading.Z;
            if (step <= .000001f) break;
            walking += step; position = start + heading * (float)walking; time += step / 6;
        }
        var slideStart = position; var remaining = (double)length;
        while (remaining > .000001f)
        {
            var step = Math.Min(.003, remaining);
            remaining -= step; position = slideStart + heading * (float)(length - remaining); time += step / 3;
        }
        return (position, time);
    }

    // Detect the trigger after a whole ordinary frame has advanced. The
    // following frame begins the slide with its direction already locked.
    // Compute displacement from total travel to avoid incremental float drift.
    private static (Vector3 Position, double Time) TraverseFrames(Vector3 start, Vector3 target, float entry, float length, int framesPerSecond)
    {
        var heading = target - start; heading.Y = 0; heading = Vector3.Normalize(heading);
        var position = start; var walkingFrames = 0;
        while (position.Z > entry)
        {
            ++walkingFrames;
            Check(walkingFrames < 1000, "The frame oracle did not reach the entry trigger.");
            position = start + heading * (float)(6.0 * walkingFrames / framesPerSecond);
        }
        var slideStart = position; var travelled = 0.0; var slidingTime = 0.0;
        while (travelled < length - .000001)
        {
            var distance = Math.Min(3.0 / framesPerSecond, length - travelled);
            travelled += distance; slidingTime += distance / 3;
            position = slideStart + heading * (float)travelled;
        }
        return (position, (double)walkingFrames / framesPerSecond + slidingTime);
    }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var delta = to - from; var length = delta.LengthSquared();
        return Vector3.Distance(point, from + delta * (length > .000001f ? Math.Clamp(Vector3.Dot(point - from, delta) / length, 0, 1) : 0));
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

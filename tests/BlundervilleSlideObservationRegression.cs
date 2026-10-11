using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleSlideObservationRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville slide observation brackets entry across a consumed walking corner", () =>
        {
            foreach(var advance in new[]{false,true})
            {
                var tracker=new BlundervilleSlideObservation(); var generation=new object();
                var dry=new Vector3(-208,17,-180.95f); var wet=new Vector3(-208,17,-181.37f);
                var target=new Vector3(-208,17,-185.5f);
                tracker.Observe(dry,Epoch,generation,0,advance ? new(-208,17,-180.75f) : target,false);
                var observed=tracker.Observe(wet,Epoch.AddSeconds(.1),generation,advance ? 1 : 0,target,true);
                Check(observed is {BeganAtIceEntry:true},"A native corner discarded the observed ice-entry transition.");
                foreach(var entry in new[]{-181.0f,-181.2f,-181.37f})
                    Check(Vector3.Distance(observed!.Start,new(-208,17,entry))<=observed.StartRadius,
                        "The entry bound excluded a possible trigger within the two samples.");
                Check(observed!.SeamEarliest==Epoch && observed.SeamLatest==Epoch.AddSeconds(.1),
                    "The observed entry claimed a more precise physical start time.");
            }
        });
        run("Blunderville slide observation requires continuous entry evidence", () =>
        {
            foreach(var reason in new[]{"stationary","generation","stale","off surface","skipped targets"})
            {
                var tracker=new BlundervilleSlideObservation(); var generation=new object();
                var dry=new Vector3(-208,17,-180.95f); var wet=new Vector3(-208,17,-181.37f);
                var target=new Vector3(-208,17,-185.5f);
                if(reason=="stationary") dry=wet;
                if(reason=="off surface") { dry-=Vector3.UnitZ*5; wet-=Vector3.UnitZ*5; target-=Vector3.UnitZ*5; }
                tracker.Observe(dry,Epoch,generation,0,target,false);
                var observed=tracker.Observe(wet,Epoch.AddSeconds(reason=="stale" ? .5 : .1),
                    reason=="generation" ? new object() : generation,reason=="skipped targets" ? 2 : 0,target,true);
                Check(observed==null,$"Unproven entry manufactured a physical slide after {reason}.");
            }
        });
        foreach (var fps in new[] { 30, 60 })
            run($"Blunderville slide observation bounds a physical turn after early native consumption at {fps}fps", () =>
            {
                var tracker = new BlundervilleSlideObservation(); var generation = new object();
                var seam = new Vector3(0, 17, 5.097f); const double seamAt = 5.097 / 3;
                BlundervilleObservedSlide? observation = null;
                for (var frame = 0; frame <= fps * 2; ++frame)
                {
                    var time = (double)frame / fps;
                    var position = time <= seamAt ? new Vector3(0, 17, (float)(time * 3))
                        : seam + Vector3.UnitX * (float)((time - seamAt) * 3);
                    var index = time * 3 >= 4.65 ? 1 : 0;
                    var target = index == 0 ? seam : seam + Vector3.UnitX * 5;
                    observation = tracker.Observe(position, Epoch.AddSeconds(time), generation, index, target, true);
                    if (time <= seamAt) Check(observation == null, "Early native target consumption invented a physical turn.");
                }
                Check(observation is { NativeIndex: 1 }, "The sampled physical turn was never confirmed.");
                Check(Vector3.Distance(observation!.Start, seam) <= observation.StartRadius,
                    "The inferred start bounds excluded the true physical seam.");
                Check(observation.SeamEarliest <= Epoch.AddSeconds(seamAt) && observation.SeamLatest >= Epoch.AddSeconds(seamAt),
                    "The observed seam time interval excluded the physical turn.");
                foreach (var length in new[] { 4.88f, 5.1f })
                {
                    var landing = seam + Vector3.UnitX * length;
                    var endpoint = length == 4.88f ? observation.ShortLanding : observation.LongLanding;
                    Check(Vector3.Distance(landing, endpoint) <= observation.LandingRadius, "Landing uncertainty omitted a recorded slide length.");
                    foreach (var radial in new[] { 0f, .5f, 1f })
                        for (var angle = 0; angle < 64; ++angle)
                        {
                            var theta = angle * MathF.Tau / 64;
                            var actualStart = observation.Start + new Vector3(MathF.Cos(theta), 0, MathF.Sin(theta))
                                * (observation.StartRadius * radial);
                            var actualLanding = actualStart + Vector3.Normalize(observation.NativeTarget - actualStart) * length;
                            Check(DistanceToSegment(actualLanding, observation.ShortLanding, observation.LongLanding)
                                <= observation.LandingRadius + .0001f,
                                "The observed landing tube excluded a physical origin inside its measured start ball.");
                        }
                }
                Check(observation.LandingRadius <= observation.StartRadius + .0201f,
                    "A well-conditioned five-metre target amplified a radial start error beyond its physical Jacobian.");
                var travelled = (float)((2 - seamAt) * 3);
                Check(observation.TravelledMin <= travelled && observation.TravelledMax >= travelled
                    && observation.RemainingLengthMin <= 4.88f - travelled && observation.RemainingLengthMax >= 5.1f - travelled,
                    "Remaining bounds restarted the slide at the current player sample.");
            });
        run("Blunderville slide observation keeps a collinear native seam unknown", () =>
        {
            var tracker = new BlundervilleSlideObservation(); var generation = new object();
            for (var frame = 0; frame <= 120; ++frame)
            {
                var position = new Vector3(0, 17, frame / 10f);
                var index = position.Z >= 4.65f ? 1 : 0; var target = new Vector3(0, 17, index == 0 ? 5 : 10);
                Check(tracker.Observe(position, Epoch.AddSeconds(frame / 30.0), generation, index, target, true) == null,
                    "A collinear seam was assigned an exact physical origin without evidence.");
            }
        });
        run("Blunderville slide observation ignores a nearly collinear apparent turn", () =>
        {
            var tracker = new BlundervilleSlideObservation(); var generation = new object();
            var direction = new Vector3(MathF.Sin(.05f), 0, MathF.Cos(.05f)); var seam = new Vector3(0, 17, 5);
            for (var frame = 0; frame <= 90; ++frame)
            {
                var time = frame / 30.0;
                var position = time <= 5.0 / 3 ? new Vector3(0, 17, (float)(time * 3)) : seam + direction * (float)((time - 5.0 / 3) * 3);
                var index = time * 3 >= 4.65 ? 1 : 0; var target = index == 0 ? seam : seam + direction * 5;
                Check(tracker.Observe(position, Epoch.AddSeconds(time), generation, index, target, true) == null,
                    "A poorly conditioned line intersection produced a precise slide observation.");
            }
        });
        foreach(var frequency in new[]{10,30,60})
            run($"Blunderville slide observation recovers a clear turn after an unresolved shallow turn at {frequency}Hz",()=>
            {
                var tracker=new BlundervilleSlideObservation(); var generation=new object();
                var shallow=new Vector3(MathF.Sin(.05f),0,MathF.Cos(.05f));
                var first=new Vector3(0,17,5); var second=first+shallow*5;
                BlundervilleObservedSlide? result=null;
                for(var tick=0;tick<=frequency*4;++tick)
                {
                    var time=(double)tick/frequency; var distance=(float)(time*3);
                    var position=distance<=5 ? new Vector3(0,17,distance)
                        : distance<=10 ? first+shallow*(distance-5) : second+Vector3.UnitX*(distance-10);
                    var index=distance<4.15f ? 0 : distance<9.15f ? 1 : 2;
                    var target=index==0 ? first-Vector3.UnitZ*.5f : index==1 ? second-shallow*.5f : second+Vector3.UnitX*4.5f;
                    result=tracker.Observe(position,Epoch.AddSeconds(time),generation,index,target,true);
                    if(distance<10) Check(result==null,"An unresolved shallow seam was fabricated.");
                }
                Check(result is {NativeIndex:2} && Vector3.Distance(result.Start,second)<=result.StartRadius
                    && result.SeamEarliest<=Epoch.AddSeconds(10.0/3) && result.SeamLatest>=Epoch.AddSeconds(10.0/3),
                    "An unresolved shallow turn prevented the later clear turn from being measured.");
            });
        foreach (var frequency in new[] { 10, 30, 60 })
            run($"Blunderville slide observation carries physical bounds through shallow seams at {frequency}Hz", () =>
            {
                foreach (var degrees in new[] { 3f, 5f, 9f })
                foreach (var speed in new[] { 2.88f, 3f })
                foreach (var length in new[] { 4.88f, 5.1f })
                foreach (var error in new[] { Vector3.Zero, new Vector3(.014f, 0, .014f), new Vector3(-.014f, 0, .014f),
                    new Vector3(.014f, 0, -.014f), new Vector3(-.014f, 0, -.014f) })
                {
                    var tracker = new BlundervilleSlideObservation(); var generation = new object();
                    var origin = new Vector3(-206, 17, -200);
                    var first = origin + Vector3.UnitZ * length;
                    var second = first + Vector3.UnitX * length;
                    var direction = new Vector3(MathF.Cos(degrees * MathF.PI / 180), 0, -MathF.Sin(degrees * MathF.PI / 180));
                    var targets = new[] { origin + Vector3.UnitZ * 4.5f, first + Vector3.UnitX * 4.5f, second + direction * 4.5f };
                    BlundervilleObservedSlide? observed = null;
                    var now = Epoch;
                    var truePosition = origin;
                    for (var tick = 0; tick / (double)frequency <= (2 * length + 2) / speed; ++tick)
                    {
                        var distance = (float)(tick * speed / frequency);
                        truePosition = distance <= length ? origin + Vector3.UnitZ * distance
                            : distance <= 2 * length ? first + Vector3.UnitX * (distance - length)
                            : second + direction * (distance - 2 * length);
                        var index = distance < length - .8f ? 0 : distance < 2 * length - .8f ? 1 : 2;
                        now = Epoch.AddSeconds(tick / (double)frequency);
                        observed = tracker.Observe(truePosition + error, now, generation, index, targets[index], true);
                    }
                    Check(observed is { NativeIndex: 1 or 2 } && (degrees < 9 || observed.NativeIndex == 2),
                        $"The {degrees}-degree seam erased the previous observed landing (speed={speed}, length={length}).");
                    var seamAt = Epoch.AddSeconds(2 * (double)length / speed);
                    Check(observed!.NativeIndex != 2 || Vector3.Distance(observed.Start, second) <= observed.StartRadius + .0001f
                        && observed.SeamEarliest <= seamAt && observed.SeamLatest >= seamAt,
                        "The inherited spatial/time bounds excluded the true shallow seam.");
                    BlundervilleBumperPlanner.Step[] path = targets.Select(p => new BlundervilleBumperPlanner.Step(p, 0, 3, true)).ToArray();
                    var prediction = BlundervilleIceContinuation.Predict(truePosition + error, now, path, 2, 2, observed)!;
                    foreach (var nextLength in new[] { 4.88f, 5.1f })
                    {
                        var landing = second + direction * nextLength;
                        var eta = (seamAt - now).TotalSeconds + nextLength / speed;
                        Check(prediction.UsesObservation && DistanceToSegment(landing, prediction.Anchor.ShortLanding!.Value,
                                prediction.Anchor.LongLanding!.Value) <= prediction.Radius + .0001f
                            && prediction.Earliest <= eta + .0001 && prediction.Latest >= eta - .0001,
                            "A shallow seam observation excluded the physical landing or arrival time.");
                    }
                }
            });
        foreach (var reset in new[] { "clear", "unlock", "generation", "clock", "teleport" })
            run($"Blunderville slide observation discards physical evidence after {reset}", () =>
            {
                var tracker = new BlundervilleSlideObservation(); var generation = new object();
                var seam = new Vector3(0, 17, 5); var target = seam + Vector3.UnitX * 5;
                BlundervilleObservedSlide? observation = null;
                for (var frame = 0; frame <= 60; ++frame)
                {
                    var time = frame / 30.0;
                    var position = time <= 5.0 / 3 ? new Vector3(0, 17, (float)(time * 3)) : seam + Vector3.UnitX * (float)((time - 5.0 / 3) * 3);
                    observation = tracker.Observe(position, Epoch.AddSeconds(time), generation, time * 3 >= 4.65 ? 1 : 0,
                        time * 3 >= 4.65 ? target : seam, true);
                }
                Check(observation != null, "Reset fixture never confirmed its physical slide.");
                if (reset == "clear") tracker.Reset();
                var nextGeneration = reset == "generation" ? new object() : generation;
                var now = Epoch.AddSeconds(reset == "clock" ? 1.9 : 2.1);
                var next = seam + Vector3.UnitX * (reset == "teleport" ? 4 : 1.3f);
                Check(tracker.Observe(next, now, nextGeneration, 1, target, reset != "unlock") == null,
                    "Old physical evidence survived a discontinuity in the observation stream.");
            });
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var delta = to - from; var length = delta.LengthSquared();
        return Vector3.Distance(point, from + delta * (length > .000001f ? Math.Clamp(Vector3.Dot(point - from, delta) / length, 0, 1) : 0));
    }
}

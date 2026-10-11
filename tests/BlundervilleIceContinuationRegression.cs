using System.Numerics;
using MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleIceContinuationRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville ice continuation conditions lateral uncertainty on the live acquired ray", () =>
        {
            var start=new Vector3(-207,17,-186); const float radius=.14f;
            BlundervilleBumperPlanner.Step[] path=[new(start-Vector3.UnitZ*4.5f,0,3,true)];
            foreach(var progress in new[]{.8f,2.5f,4f})
            foreach(var speed in new[]{2.88f,3f})
            for(var angle=0;angle<32;++angle)
            for(var noise=0;noise<16;++noise)
            {
                var actualStart=start+new Vector3(MathF.Cos(angle*MathF.Tau/32),0,MathF.Sin(angle*MathF.Tau/32))*radius;
                var direction=Vector3.Normalize(path[0].Position-actualStart);
                var actualPlayer=actualStart+direction*progress;
                var sample=actualPlayer+new Vector3(MathF.Cos(noise*MathF.Tau/16),0,MathF.Sin(noise*MathF.Tau/16))*.02f;
                var prediction=BlundervilleIceContinuation.Predict(sample,Epoch.AddSeconds(progress/speed),path,0,0,
                    Observation(start,-Vector3.UnitZ,0,path[0].Position,radius))!;
                Check(prediction.UsesObservation,"A compatible acquired ray lost its measured evidence.");
                foreach(var length in new[]{4.88f,5.1f})
                {
                    var landing=actualStart+direction*length;
                    Check(DistanceToSegment(landing,prediction.Anchor.ShortLanding!.Value,prediction.Anchor.LongLanding!.Value)
                        <=prediction.Radius+.0002f,"The live ray omitted a compatible physical origin or quantized position.");
                    var remaining=(length-progress)/speed;
                    Check(prediction.Earliest<=remaining+.0001 && prediction.Latest>=remaining-.0001,
                        "Conditioning lateral geometry shortened the physical clock envelope.");
                }
            }
        });
        run("Blunderville ice continuation uses observed slide age for its remaining clock", () =>
        {
            var start = new Vector3(-210, 17, -185); var direction = -Vector3.UnitZ;
            var now = Epoch.AddSeconds(1.5); var player = start + direction * 4.8f;
            var path = new[] { new BlundervilleBumperPlanner.Step(start + direction * 5, 0, 3, true) };
            var prediction = BlundervilleIceContinuation.Predict(player, now, path, 0, 0, Observation(start, direction, 0, path[0].Position))!;
            Check(prediction.UsesObservation && prediction.Eta > .15 && prediction.Eta < .25,
                "The physical slide clock was restarted from distance to the current queue target.");
            Check(prediction.Anchor.Position == path[0].Position && prediction.Anchor.ShortLanding == start + direction * 4.88f,
                "The observed physical slide endpoint was replaced by the current player position.");
            Check(prediction.Anchor.LandingPosition == prediction.Anchor.Position && prediction.Anchor.LandingRadius == prediction.Radius,
                "The rebased anchor retained stale physical metadata.");
        });
        run("Blunderville ice continuation completes the previous physical tail before reaiming a native target", () =>
        {
            var start = new Vector3(-210, 17, -185); var direction = -Vector3.UnitZ;
            var now = Epoch.AddSeconds(4.7 / 3); var player = start + direction * 4.7f;
            BlundervilleBumperPlanner.Step[] path = [new(start + direction * 5, 0, 3, true), new(new(-205, 17, -190), 0, 3, true)];
            var prediction = BlundervilleIceContinuation.Predict(player, now, path, 1, 1, Observation(start, direction, 0, path[0].Position))!;
            Check(prediction.UsesObservation && prediction.Eta > 1.73 && prediction.Eta < 1.9,
                "A native early advancement omitted the remaining previous physical tail.");
            Check(Vector3.Distance(prediction.Anchor.Position, path[1].Position) < .001f,
                "The next ray started from the early native-consumption player sample.");
            foreach (var oldLength in new[] { 4.88f, 5.1f })
            foreach (var newLength in new[] { 4.88f, 5.1f })
            {
                var physicalStart = start + direction * oldLength;
                var actual = physicalStart + Vector3.Normalize(path[1].Position - physicalStart) * newLength;
                Check(DistanceToSegment(actual, prediction.Anchor.ShortLanding!.Value, prediction.Anchor.LongLanding!.Value)
                    <= prediction.Radius + .0001f, "The anchor tube excluded mixed short/long slide lengths.");
            }
        });
        run("Blunderville ice continuation replaces completed history while preserving current observation uncertainty", () =>
        {
            var start = new Vector3(-210, 17, -190); var now = Epoch.AddSeconds(1);
            BlundervilleBumperPlanner.Step[] path = [new(new(-210, 17, -185), 0, 3, true),
                new(new(-205, 17, -190), 0, 3, true, new(-207, 17, -188), new(-203, 17, -192), -1, 1)];
            var prediction = BlundervilleIceContinuation.Predict(start + Vector3.UnitX * 3, now, path, 1, 1,
                Observation(start, Vector3.UnitX, 1, path[1].Position, .08f))!;
            Check(prediction.UsesObservation && prediction.Radius < .2f
                && Vector3.Distance(prediction.Anchor.ShortLanding!.Value, path[1].Position) < .5f
                && new[]{4.88f-.08f,5.1f+.08f}.All(length => DistanceToSegment(start+Vector3.UnitX*length,
                    prediction.Anchor.ShortLanding!.Value,prediction.Anchor.LongLanding!.Value)<=prediction.Radius+.0001f),
                "Rebasing kept impossible completed-route history or discarded measured uncertainty.");
        });
        run("Blunderville ice continuation retains measured elapsed time across collinear slides", () =>
        {
            var start = new Vector3(-207,17,-184); var direction=-Vector3.UnitZ;
            BlundervilleBumperPlanner.Step[] path = Enumerable.Range(0,3).Select(i =>
                new BlundervilleBumperPlanner.Step(start+direction*(i*5+4.5f),0,3,true,
                    LandingPosition:start+direction*((i+1)*5))).ToArray();
            foreach(var distance in new[]{6f,9f,11f})
            {
                var cursor=distance<9.15f ? 1 : 2;
                var prediction=BlundervilleIceContinuation.Predict(start+direction*distance,Epoch.AddSeconds(distance/3),
                    path,cursor,2,Observation(start,direction,0,path[0].Position))!;
                Check(prediction.UsesObservation && Math.Abs(prediction.Eta-(15-distance)/3)<.08,
                    "The previous slide's measured age was discarded at a straight native advance.");
                Check(prediction.Earliest <= (14.64-distance)/3 && prediction.Latest >= (15.3-distance)/2.88,
                    "A straight continuation lost independent length or speed uncertainty.");
            }
        });
        run("Blunderville ice continuation preserves old bounds without a compatible physical observation", () =>
        {
            BlundervilleBumperPlanner.Step[] path = [new(new(-210, 17, -190), 0, 3, true,
                new(-210.5f, 17, -189.5f), new(-209.5f, 17, -190.5f), -.3, .4)];
            var prediction = BlundervilleIceContinuation.Predict(new(-210, 17, -187), Epoch.AddSeconds(1), path, 0, 0, null)!;
            Check(!prediction.UsesObservation && prediction.Anchor.Position == path[0].Position
                && prediction.Anchor.ShortLanding == path[0].ShortLanding && prediction.Anchor.LongLanding == path[0].LongLanding
                && prediction.Earliest <= prediction.Eta - .3 && prediction.Latest >= prediction.Eta + .4
                && Math.Abs(prediction.Eta + prediction.Anchor.ShortTimeOffset - prediction.Earliest) < .000001
                && Math.Abs(prediction.Eta + prediction.Anchor.LongTimeOffset - prediction.Latest) < .000001,
                "An unknown physical seam silently tightened inherited anchor bounds.");
        });
        run("Blunderville ice continuation rejects a previous observation after the player has visibly turned", () =>
        {
            var start = new Vector3(-210, 17, -185);
            BlundervilleBumperPlanner.Step[] path = [new(start - Vector3.UnitZ * 5, 0, 3, true), new(new(-205, 17, -190), 0, 3, true)];
            var prediction = BlundervilleIceContinuation.Predict(new(-209, 17, -190), Epoch.AddSeconds(2), path, 1, 1,
                Observation(start, -Vector3.UnitZ, 0, path[0].Position))!;
            Check(!prediction.UsesObservation, "A stale previous-ray observation rewound a player already on the next slide.");
        });
        run("Blunderville ice continuation unknown prefix uses physical landing coordinates and preserves radius", () =>
        {
            var start = new Vector3(-210, 17, -185);
            var physical = start - Vector3.UnitZ * 5;
            BlundervilleBumperPlanner.Step[] path = [new(start - Vector3.UnitZ * 4.9f, 0, 3, true,
                start - Vector3.UnitZ * 4.88f, start - Vector3.UnitZ * 5.1f, -.3, .4, LandingRadius: .12f, LandingPosition: physical)];
            var prediction = BlundervilleIceContinuation.Predict(start, Epoch, path, 0, 0, null)!;
            Check(!prediction.UsesObservation && Math.Abs(prediction.Eta - 5.0 / 3) < .000001
                && prediction.Anchor.Position == physical && prediction.Radius == .12f
                && prediction.Anchor.LandingPosition == physical && prediction.Anchor.LandingRadius == .12f
                && prediction.Earliest <= prediction.Eta - .3 && prediction.Latest >= prediction.Eta + .4,
                "Unknown prediction substituted the early queue target for its physical seam or discarded radius.");
        });
        run("Blunderville ice continuation does not enlarge native tolerance for an unreachable next target", () =>
        {
            var start = new Vector3(-210, 17, -185); var now = Epoch.AddSeconds(1.5);
            BlundervilleBumperPlanner.Step[] path = [new(start - Vector3.UnitZ * 5, 0, 3, true),
                new(new(-204.5f, 17, -190), 0, 3, true)];
            var prediction = BlundervilleIceContinuation.Predict(start - Vector3.UnitZ * 4.5f, now, path, 1, 1,
                Observation(start, -Vector3.UnitZ, 0, path[0].Position))!;
            Check(!prediction.UsesObservation, "A short physical slide was silently extended to consume an unreachable queue target.");
        });
        run("Blunderville ice continuation propagates fallback clock uncertainty into anchor offsets", () =>
        {
            var start = new Vector3(-210, 17, -185);
            BlundervilleBumperPlanner.Step[] path = [new(start - Vector3.UnitZ * 4.9f, 0, 3, true,
                LandingRadius: .1f, LandingPosition: start - Vector3.UnitZ * 5)];
            var prediction = BlundervilleIceContinuation.Predict(start, Epoch, path, 0, 0, null)!;
            Check(prediction.Latest > prediction.Eta + .05 && prediction.Anchor.LongTimeOffset > .05
                && Math.Abs(prediction.Eta + prediction.Anchor.ShortTimeOffset - prediction.Earliest) < .000001
                && Math.Abs(prediction.Eta + prediction.Anchor.LongTimeOffset - prediction.Latest) < .000001
                && prediction.Anchor.LandingRadius == .1f && prediction.Radius == .1f,
                "Fallback search offsets ignored the nonlinear clock envelope returned by the same prediction.");
        });
        run("Blunderville ice continuation bounds entry clock changes from a nearly horizontal uncertain ray", () =>
        {
            var start = new Vector3(-210, 17, -186.3f); var now = Epoch.AddSeconds(1.5);
            BlundervilleBumperPlanner.Step[] path = [new(start + Vector3.UnitZ * 5, 0, 3, true),
                new(new(-205.1f, 17, -181.3f), 0, 3, true)];
            var observation = Observation(start, Vector3.UnitZ, 0, path[0].Position, .08f);
            var oldTail = BlundervilleIceContinuation.Predict(start + Vector3.UnitZ * 4.5f, now, path, 1, 1, observation)!;
            var justCurrent = BlundervilleIceContinuation.Predict(start + Vector3.UnitZ * 4.5f, now, path, 0, 0, observation)!;
            Check(oldTail.UsesObservation && oldTail.Earliest - justCurrent.Earliest <= 4.88 / 6 + .0001
                && oldTail.Latest - justCurrent.Latest >= 5.1 / 2.88 - .0001,
                "A small landing radius omitted a full dry/ice clock discontinuity at the entry boundary.");
        });
        run("Blunderville ice continuation ordinary prefix retains ordinary speed and anchor geometry", () =>
        {
            BlundervilleBumperPlanner.Step[] path = [new(new(0, 0, 6), 0), new(new(0, 0, 12), 0)];
            var prediction = BlundervilleIceContinuation.Predict(Vector3.Zero, Epoch, path, 0, 1, null)!;
            Check(prediction.Eta == 2 && prediction.Earliest == 2 && prediction.Latest == 2 && prediction.Anchor.Position == path[1].Position,
                "A physical ice clock changed ordinary prefix traversal.");
        });
        run("Blunderville ice continuation bounds mixed physical lengths after radial and turning seams", () =>
        {
            var start = new Vector3(-210, 17, -188); var now = Epoch.AddSeconds(1.5); const float radius = .08f;
            BlundervilleBumperPlanner.Step[] path = [new(new(-210, 17, -192.9f), 0, 3, true),
                new(new(-210, 17, -197.9f), 0, 3, true), new(new(-205.2f, 17, -198), 0, 3, true)];
            var observation = Observation(start, -Vector3.UnitZ, 0, path[0].Position, radius);
            var player = start - Vector3.UnitZ * 4.5f;
            var radialPrediction = BlundervilleIceContinuation.Predict(player, now, path, 0, 1, observation)!;
            Check(radialPrediction.UsesObservation && radialPrediction.Radius <= radius + .0001f,
                "A radial continuation accumulated curvature that its physical path does not have.");
            var prediction = BlundervilleIceContinuation.Predict(player, now, path, 0, 2, observation)!;
            Check(prediction.UsesObservation, "The independent mixed-physics continuation was not predicted.");
            foreach (var radial in new[] { 0f, .5f, 1f })
                for (var angle = 0; angle < 32; ++angle)
                    for (var pattern = 0; pattern < 8; ++pattern)
                        foreach (var speed in new[] { 2.88f, 3f })
                            foreach (var seamOffset in new[] { -.03, .03 })
                            {
                                var theta = angle * MathF.Tau / 32;
                                var actual = start + new Vector3(MathF.Cos(theta), 0, MathF.Sin(theta)) * (radius * radial);
                                // Supply the player position generated by this
                                // physical origin. The acquired ray is new
                                // evidence, so arbitrary off-ray origins cannot
                                // share one fixed nominal player sample.
                                var sampledPlayer=actual+Vector3.Normalize(path[0].Position-actual)*4.5f;
                                var conditioned=BlundervilleIceContinuation.Predict(sampledPlayer,now,path,0,2,observation)!;
                                var remaining = seamOffset - (now - Epoch).TotalSeconds;
                                for (var i = 0; i < path.Length; ++i)
                                {
                                    var length = (pattern & (1 << i)) == 0 ? 4.88f : 5.1f;
                                    actual += Vector3.Normalize(path[i].Position - actual) * length;
                                    remaining += length / speed;
                                }
                                Check(DistanceToSegment(actual, conditioned.Anchor.ShortLanding!.Value, conditioned.Anchor.LongLanding!.Value)
                                    <= conditioned.Radius + .0001f,
                                    "The continuation tube excluded independent short/long lengths and a sampled physical origin.");
                                Check(remaining >= conditioned.Earliest - .0001 && remaining <= conditioned.Latest + .0001,
                                    "The continuation clock excluded an independently propagated physical arrival.");
                            }
        });
    }
    private static BlundervilleObservedSlide Observation(Vector3 start, Vector3 direction, int index, Vector3 target, float radius = .04f)
        => new(index, target, start, radius, direction, start + direction * 4.88f, start + direction * 5.1f,
            radius, 0, 0, 0, 0, Epoch.AddSeconds(-.03), Epoch.AddSeconds(.03), 0, 0);
    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var delta = to - from; var length = delta.LengthSquared();
        return Vector3.Distance(point, from + delta * (length > .000001f ? Math.Clamp(Vector3.Dot(point - from, delta) / length, 0, 1) : 0));
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleIceContinuation
{
    internal sealed record Prediction(BlundervilleBumperPlanner.Step Anchor, double Eta, float Radius,
        double Earliest, double Latest, bool UsesObservation);
    private const float Short = 4.88f, Long = 5.1f;

    public static Prediction? Predict(Vector3 player, DateTime now, IReadOnlyList<BlundervilleBumperPlanner.Step> path,
        int cursor, int anchor, BlundervilleObservedSlide? observation)
    {
        if (cursor < 0 || anchor < cursor || anchor >= path.Count) return null;
        if (observation == null || observation.NativeIndex < 0 || observation.NativeIndex > cursor
            || observation.NativeTarget != path[observation.NativeIndex].Position || !path[observation.NativeIndex].Sliding
            || now < observation.SeamLatest)
            return Fallback(player, path, cursor, anchor);
        var onObservedRay = DistanceToSegment(player, observation.Start, observation.LongLanding) <= observation.StartRadius+.04f;
        var playerFits = observation.NativeIndex >= cursor-1 && onObservedRay;
        // Consecutive straight slides have no visible turning seam. Keep the
        // last measured clock through that chain, subtracting its entire age.
        // A visibly changed direction still needs a fresh observation.
        var chainFrom = observation.Start;
        for (var i=observation.NativeIndex;i<=cursor;++i)
        {
            var heading = path[i].Position-chainFrom; heading.Y=0;
            if (!path[i].Sliding || heading.LengthSquared()<.01f
                || !onObservedRay && Vector3.Dot(Vector3.Normalize(heading),observation.Heading)<.9902681f)
                return Fallback(player,path,cursor,anchor);
            chainFrom = path[i].LandingPosition ?? chainFrom+Vector3.Normalize(heading)*5;
        }
        var nominal = observation.Start + observation.Heading * 5;
        var shortFrom = observation.ShortLanding; var longFrom = observation.LongLanding;
        var radius = observation.LandingRadius;
        var ageMin = Math.Max(0, (now - observation.SeamLatest).TotalSeconds);
        var ageMax = Math.Max(0, (now - observation.SeamEarliest).TotalSeconds);
        var whollyOnIce = new[] { observation.Start, shortFrom, longFrom }
            .All(p => (p.Z + radius <= -181.4f || observation.BeganAtIceEntry && observation.Heading.Z < 0) && p.Z - radius >= -204.5f);
        var shortClock = whollyOnIce ? BlundervilleIceTiming.Instance.PrepareFullSlide(observation.Start,shortFrom,Short)
            : BlundervilleIceTiming.Instance.PrepareSlideExit(observation.Start, shortFrom,Short,radius);
        var longClock = whollyOnIce ? BlundervilleIceTiming.Instance.PrepareFullSlide(observation.Start,longFrom,Long)
            : BlundervilleIceTiming.Instance.PrepareSlideExit(observation.Start, longFrom,Long,radius);
        var ownTolerance = path.Take(observation.NativeIndex).Any(s => s.Sliding) ? .35f : .55f;
        if (Distance(observation.Start, observation.NativeTarget) + observation.StartRadius > Short + ownTolerance)
            return Fallback(player, path, cursor, anchor);
        var totalEarly = Math.Min(shortClock.EarliestDuration, longClock.EarliestDuration);
        var totalLate = Math.Max(shortClock.LatestDuration, longClock.LatestDuration);
        if (whollyOnIce && onObservedRay)
        {
            var travelled = Vector3.Dot(player-observation.Start,observation.Heading);
            var error = observation.StartRadius + .02f;
            var progressEarly = Math.Min(shortClock.TimeBoundsAt(Math.Max(0,travelled-error)).Earliest,
                longClock.TimeBoundsAt(Math.Max(0,travelled-error)).Earliest);
            var progressLate = Math.Max(shortClock.TimeBoundsAt(travelled+error).Latest,
                longClock.TimeBoundsAt(travelled+error).Latest);
            var constrainedMin = Math.Max(ageMin,progressEarly);
            var constrainedMax = Math.Min(ageMax,progressLate);
            if (constrainedMin <= constrainedMax)
            { ageMin = constrainedMin; ageMax = constrainedMax; }
        }
        else if (!whollyOnIce && observation.Start.Z+radius > -181.4f)
        {
            totalEarly = Math.Min(totalEarly, Short / 6.0);
            totalLate = Math.Max(totalLate, BlundervilleIceTiming.Instance.Prepare(new(0, 0, -190), new(Long, 0, -190)).LatestDuration);
        }
        var earliest = totalEarly - ageMax;
        var latest = totalLate - ageMin;
        var eta = Math.Clamp(5.0 / 3 - (ageMin + ageMax) / 2, earliest, latest);
        if (onObservedRay && observation.NativeIndex == cursor)
        {
            // The live sample lies on the acquired ray toward this native
            // target. Uncertainty in the old seam is mainly along that ray;
            // it is not an equally uncertain lateral landing after we have
            // observed the player moving on it.
            const float sampleError = .02f;
            var targetDistance = Distance(player,observation.NativeTarget);
            if (targetDistance > .3f)
            {
                var travelled = Distance(player,observation.Start);
                var error = observation.StartRadius+sampleError;
                var remainingMin = MathF.Max(0,Short-travelled-error);
                var remainingMax = MathF.Max(0,Long-travelled+error);
                var heading = Vector3.Normalize(observation.NativeTarget-player);
                nominal = player+heading*MathF.Max(0,5-travelled);
                shortFrom = player+heading*remainingMin; longFrom = player+heading*remainingMax;
                radius = sampleError*MathF.Max(1,remainingMax/(targetDistance-sampleError)-1);
            }
        }
        for (var i = observation.NativeIndex + 1; i <= anchor; ++i)
        {
            var step = path[i]; var target = step.Position;
            var previousNominal = nominal;
            var previousExtent = MathF.Max(Distance(nominal,shortFrom),Distance(nominal,longFrom))+radius;
            if (!step.Sliding)
            {
                var durations = new[] { Distance(nominal, target), Distance(shortFrom, target), Distance(longFrom, target) };
                eta += durations[0] / Math.Max(.1f, step.Speed);
                earliest += Math.Max(0, durations.Min() - radius) / Math.Max(.1f, step.Speed);
                latest += (durations.Max() + radius) / Math.Max(.1f, step.Speed);
                nominal = shortFrom = longFrom = target; radius = 0;
            }
            else
            {
                var minimumDistance = DistanceToSegment(target, shortFrom, longFrom) - radius;
                var tolerance = path.Take(i).Any(s => s.Sliding) ? .35f : .55f;
                if (minimumDistance <= .3f || MathF.Max(Distance(shortFrom, target), Distance(longFrom, target)) + radius > Short + tolerance)
                    return Fallback(player, path, cursor, anchor);
                var envelope = BlundervilleSlideEnvelope.Propagate(shortFrom, longFrom, radius, nominal, target, true);
                if (envelope == null) return Fallback(player, path, cursor, anchor);
                earliest += envelope.EarliestDuration; latest += envelope.LatestDuration;
                eta += envelope.NominalDuration;
                nominal = envelope.Nominal; shortFrom = envelope.AxisA; longFrom = envelope.AxisB; radius = envelope.Radius;
            }
            if (i>=cursor-1 && i<=cursor)
                playerFits |= DistanceToSegment(player,previousNominal,nominal)
                    <= MathF.Max(previousExtent,MathF.Max(Distance(nominal,shortFrom),Distance(nominal,longFrom))+radius)+.04f;
            earliest += step.Wait; latest += step.Wait; eta += step.Wait;
        }
        if (!playerFits || latest < 0) return Fallback(player,path,cursor,anchor);
        earliest=Math.Max(0,earliest); latest=Math.Max(0,latest);
        eta = Math.Clamp(eta, earliest, latest);
        var certified = path[anchor];
        if (certified.LandingPosition is { } certifiedNominal && certified.ShortLanding is { } certifiedA
            && certified.LongLanding is { } certifiedB && certified.Sliding)
        {
            // A sampled intersection adds evidence; it must not erase a
            // tighter landing bound already certified for this native ray.
            // Shallow turns can have a wider measurement ball than the
            // propagated physical envelope. Keep the smaller enclosing tube
            // when both bounds agree, retaining the newly observed clock.
            var priorRadius = certified.LandingRadius;
            var priorArea = MathF.PI*priorRadius*priorRadius+2*priorRadius*Distance(certifiedA,certifiedB);
            var observedArea = MathF.PI*radius*radius+2*radius*Distance(shortFrom,longFrom);
            var priorExtent = MathF.Max(Distance(certifiedNominal,certifiedA),Distance(certifiedNominal,certifiedB))+priorRadius;
            var observedExtent = MathF.Max(Distance(nominal,shortFrom),Distance(nominal,longFrom))+radius;
            if (priorArea < observedArea && Distance(certifiedNominal,nominal) <= priorExtent+observedExtent)
            {
                nominal=certifiedNominal; shortFrom=certifiedA; longFrom=certifiedB; radius=priorRadius;
            }
        }
        return new(path[anchor] with { Position = nominal, ShortLanding = shortFrom, LongLanding = longFrom,
            ShortTimeOffset = earliest - eta, LongTimeOffset = latest - eta, LandingRadius = radius,
            LandingPosition = nominal }, eta, radius, earliest, latest, true);
    }

    private static Prediction Fallback(Vector3 player, IReadOnlyList<BlundervilleBumperPlanner.Step> path, int cursor, int anchor)
    {
        var eta = 0.0; var earliest = 0.0; var latest = 0.0; var from = player;
        for (var i = cursor; i <= anchor; ++i)
        {
            var step = path[i];
            var to = step.LandingPosition ?? step.Position;
            if (step.Sliding)
            {
                eta += BlundervilleIceTiming.Instance.Duration(from, to, step.Speed);
                var clock = BlundervilleIceTiming.Instance.Prepare(from, to);
                earliest += clock.EarliestDuration; latest += clock.LatestDuration;
            }
            else
            {
                var duration = Distance(from, to) / Math.Max(.1f, step.Speed);
                eta += duration; earliest += duration; latest += duration;
            }
            eta += step.Wait; earliest += step.Wait; latest += step.Wait; from = to;
        }
        earliest = Math.Min(earliest, eta + Math.Min(0, path[anchor].ShortTimeOffset));
        latest = Math.Max(latest, eta + Math.Max(0, path[anchor].LongTimeOffset));
        var anchorStep = path[anchor];
        anchorStep = anchorStep with { Position = anchorStep.LandingPosition ?? anchorStep.Position,
            ShortTimeOffset = earliest - eta, LongTimeOffset = latest - eta };
        return new(anchorStep, eta, path[anchor].LandingRadius, earliest, latest, false);
    }

    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var a = new Vector2(from.X, from.Z); var b = new Vector2(to.X, to.Z); var p = new Vector2(point.X, point.Z);
        var delta = b - a; var length = delta.LengthSquared();
        return Vector2.Distance(p, a + delta * (length > .000001f ? Math.Clamp(Vector2.Dot(p - a, delta) / length, 0, 1) : 0));
    }
}

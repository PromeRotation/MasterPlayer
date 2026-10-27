using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed record BlundervilleObservedSlide(int NativeIndex, Vector3 NativeTarget, Vector3 Start, float StartRadius,
    Vector3 Heading, Vector3 ShortLanding, Vector3 LongLanding, float LandingRadius,
    float TravelledMin, float TravelledMax, float RemainingLengthMin, float RemainingLengthMax,
    DateTime SeamEarliest, DateTime SeamLatest, double AgeMin, double AgeMax, bool BeganAtIceEntry = false);

// Native arrival consumes a waypoint before the physical slide necessarily
// ends. A changed target is not evidence of a new physical direction.
internal sealed class BlundervilleSlideObservation(float positionError = .02f)
{
    private const float MinimumSlide = 4.88f, MaximumSlide = 5.1f, MaximumSpeed = 4.8f;
    private const float MinimumTurnSin = .1391731f; // eight degrees
    private const float MaximumStartRadius = .3f;
    private readonly float _error = float.IsFinite(positionError) && positionError is > 0 and <= .1f ? positionError : .02f;
    private object? _generation;
    private Vector3? _previous;
    private DateTime _previousAt;
    private int _nativeIndex = -1, _physicalIndex = -1;
    private Vector3 _nativeTarget, _physicalTarget, _rayOrigin, _lastOldPosition;
    private DateTime _lastOldAt;
    private Vector2? _heading, _bootstrapHeading;
    private Vector2? _aimHeading;
    private float _aimBaseline;
    private Vector3 _bootstrapOrigin;
    private int _bootstrapSamples, _turnSamples;
    private int _bootstrapIndex = -1;
    private Vector3 _bootstrapTarget;
    private Vector3 _turnStart;
    private float _turnRadius;
    private DateTime _turnEarliest, _turnLatest;
    private BlundervilleObservedSlide? _confirmed;
    private Vector3? _dryPosition;
    private DateTime _dryAt;
    private object? _dryGeneration;
    private int _dryIndex;
    private Vector3 _dryTarget;

    public void Reset()
    {
        _generation = null; _previous = null; _previousAt = default;
        _nativeIndex = _physicalIndex = -1; _heading = _bootstrapHeading = null;
        _aimHeading = null; _aimBaseline = 0;
        _bootstrapSamples = _turnSamples = 0; _bootstrapIndex = -1; _confirmed = null;
        _dryPosition = null; _dryGeneration = null;
    }

    public BlundervilleObservedSlide? Observe(Vector3 position, DateTime now, object pathGeneration,
        int nativeIndex, Vector3 nativeTarget, bool inputLocked)
    {
        if (nativeIndex < 0 || !Finite(position) || !Finite(nativeTarget))
        { Reset(); return null; }
        if (!inputLocked)
        {
            Reset(); _dryPosition=position; _dryAt=now; _dryGeneration=pathGeneration;
            _dryIndex=nativeIndex; _dryTarget=nativeTarget;
            return null;
        }
        if (!ReferenceEquals(_generation, pathGeneration) || _previous == null)
        {
            var dry = _dryPosition; var dryAt = _dryAt;
            var entry = dry != null && ReferenceEquals(_dryGeneration,pathGeneration)
                && (nativeIndex==_dryIndex && _dryTarget==nativeTarget || nativeIndex==_dryIndex+1)
                && now>dryAt && (now-dryAt).TotalSeconds<=.2
                && dry.Value.Z>=-181.6f && position.Z<=-181 && position.Z>=-182
                && MathF.Abs(position.Y-dry.Value.Y)<.2f
                && nativeTarget.Z<position.Z-.5f && XZ(position-dry.Value).Length() is > .04f and < 1.25f;
            Seed(position, now, pathGeneration, nativeIndex, nativeTarget);
            if (!entry) return null;
            var entryStart=(dry!.Value+position)/2; var entryHeading=Vector3.Normalize(nativeTarget-entryStart);
            // A walking corner can be consumed between these samples. Every
            // possible seam on that at-most-6m/s path lies inside this ball,
            // including a bent approach rather than just their straight chord.
            var entryRadius=3*(float)(now-dryAt).TotalSeconds+_error;
            var minimumTargetDistance=XZ(nativeTarget-entryStart).Length()-entryRadius;
            if (minimumTargetDistance<=.5f) return null;
            var entryGrowth=MathF.Max(1,MaximumSlide/minimumTargetDistance-1);
            _confirmed=new(nativeIndex,nativeTarget,entryStart,entryRadius,entryHeading,entryStart+entryHeading*MinimumSlide,entryStart+entryHeading*MaximumSlide,
                entryRadius*entryGrowth+_error,0,0,0,0,dryAt,now,0,0,true);
            _heading=XZ(entryHeading); _rayOrigin=entryStart; _physicalIndex=nativeIndex; _physicalTarget=nativeTarget;
            RememberAim(position);
            _lastOldPosition=position; _lastOldAt=dryAt;
            return Current(position,now);
        }
        var previous = _previous.Value; var previousAt = _previousAt; var elapsed = (now - previousAt).TotalSeconds;
        if (elapsed == 0 && position == previous) return Current(position, now);
        var displacement = XZ(position - previous); var distance = displacement.Length();
        if (elapsed <= 0 || elapsed > .3 || distance > 2 || distance > MaximumSpeed * elapsed + 2 * _error
            || nativeIndex < _nativeIndex || nativeIndex == _nativeIndex && nativeTarget != _nativeTarget)
        { Seed(position, now, pathGeneration, nativeIndex, nativeTarget); return null; }
        _previous = position; _previousAt = now; _nativeIndex = nativeIndex; _nativeTarget = nativeTarget;
        if (distance < .02f) return Current(position, now);
        var movement = displacement / distance;
        if (_heading == null)
        {
            if (_bootstrapHeading is not { } bootstrap || Vector2.Dot(bootstrap, movement) < .995f)
            {
                _bootstrapHeading = movement; _bootstrapOrigin = previous; _bootstrapSamples = 1;
                _physicalIndex = nativeIndex; _physicalTarget = nativeTarget;
            }
            else if (_physicalIndex == nativeIndex && _physicalTarget == nativeTarget && ++_bootstrapSamples >= 2)
            {
                _heading = Vector2.Normalize(XZ(position - _bootstrapOrigin)); _rayOrigin = _bootstrapOrigin;
                _lastOldPosition = position; _lastOldAt = previousAt;
                RememberAim(_bootstrapOrigin);
            }
            return null;
        }

        var oldHeading = _heading.Value;
        // The last sample that demonstrably follows the old ray bounds the
        // possible seam interval, even if several native indices advance early.
        if (Vector2.Dot(oldHeading, movement) >= .9961947f
            && MathF.Abs(Cross(XZ(position - _rayOrigin), oldHeading)) <= 2 * _error)
        {
            // Once the native target advances, samples may straddle the next
            // shallow turn. Keep the established incoming direction fixed;
            // refitting it across that seam gradually absorbs the new ray and
            // can hide the physical turn for the rest of the bridge.
            if (nativeIndex == _physicalIndex && XZ(position - _rayOrigin).Length() >= .3f)
                _heading = Vector2.Normalize(XZ(position - _rayOrigin));
            if (nativeIndex == _physicalIndex) RememberAim(position);
            // A sample that appears aligned can straddle a turn by a few
            // quantized millimetres. Include its preceding sample in time.
            _lastOldPosition = position; _lastOldAt = previousAt; _turnSamples = 0;
            return Current(position, now);
        }
        if (nativeIndex == _physicalIndex && nativeTarget == _physicalTarget)
        { Seed(position, now, pathGeneration, nativeIndex, nativeTarget); return null; }
        var targetRay = XZ(nativeTarget - position); var targetDistance = targetRay.Length();
        if (targetDistance < .5f || Vector2.Dot(movement, targetRay / targetDistance) < .995f)
        { _turnSamples = 0; return Current(position, now); }
        var newHeading = targetRay / targetDistance; var sine = Cross(oldHeading, newHeading);
        var gap = XZ(position - _lastOldPosition);
        var possibleTail = MaximumSpeed * (float)(now - _lastOldAt).TotalSeconds;
        var oldSpan = XZ(_lastOldPosition - _rayOrigin).Length();
        var directionError = MathF.Min(1, 2 * _error / MathF.Max(.2f, oldSpan));
        Vector3 start; float radius;
        if (!IntersectAimedRays(newHeading, targetDistance, nativeTarget, out start, out radius))
        {
            if (MathF.Abs(sine) < MinimumTurnSin)
            { _turnSamples = 0; TrackUnconfirmedRay(); return Current(position, now); }
            start = _lastOldPosition + V3(oldHeading * (Cross(gap, newHeading) / sine));
            radius = (2 * _error + possibleTail * directionError) / MathF.Abs(sine) + _error;
        }
        var alongOld = Vector2.Dot(XZ(start - _lastOldPosition), oldHeading);
        var alongNew = Vector2.Dot(XZ(start - position), newHeading);
        if (alongOld < -radius || alongOld > possibleTail + radius || alongNew > radius
            || radius > MaximumStartRadius || XZ(nativeTarget - start).Length() <= radius + .5f)
        { _turnSamples = 0; TrackUnconfirmedRay(); return Current(position, now); }
        if (_turnSamples == 0 || XZ(start - _turnStart).Length() > radius + _turnRadius)
        {
            _turnStart = start; _turnRadius = radius; _turnSamples = 1;
            _turnEarliest = _lastOldAt; _turnLatest = now;
            return Current(position, now);
        }
        // Keep the union of both sample bounds instead of pretending the
        // intersection can be known exactly from quantized observations.
        radius = MathF.Max(radius, _turnRadius + XZ(start - _turnStart).Length());
        if (radius > MaximumStartRadius) { _turnSamples = 0; TrackUnconfirmedRay(); return Current(position, now); }
        var heading = Vector2.Normalize(XZ(nativeTarget - start));
        var slideTargetDistance = XZ(nativeTarget - start).Length();
        // The radial Jacobian is 1; its tangential magnitude is |1-L/d|.
        // Bound the whole start ball using its minimum target distance.
        var growth = MathF.Max(1, MaximumSlide / (slideTargetDistance - radius) - 1);
        var landingRadius = radius * growth + _error;
        _confirmed = new(nativeIndex, nativeTarget, start, radius, V3(heading), start + V3(heading * MinimumSlide),
            start + V3(heading * MaximumSlide), landingRadius, 0, 0, 0, 0,
            _turnEarliest, _turnLatest, 0, 0);
        _physicalIndex = nativeIndex; _physicalTarget = nativeTarget; _heading = heading; _rayOrigin = start;
        _aimHeading = null; _aimBaseline = 0; RememberAim(position);
        _lastOldPosition = position; _lastOldAt = now; _turnSamples = 0; _bootstrapIndex = -1;
        return Current(position, now);

        void TrackUnconfirmedRay()
        {
            // A shallow turn may be too ill-conditioned to locate its seam.
            // Its measured direction is still useful as the incoming ray for
            // the next turn. Otherwise one missed seam poisons every later
            // intersection by joining samples from multiple distinct slides.
            if (_bootstrapIndex != nativeIndex || _bootstrapTarget != nativeTarget
                || _bootstrapHeading is not { } direction || Vector2.Dot(direction,movement)<.995f)
            {
                _bootstrapIndex=nativeIndex; _bootstrapTarget=nativeTarget;
                _bootstrapHeading=movement; _bootstrapOrigin=previous; _bootstrapSamples=1;
                return;
            }
            if (++_bootstrapSamples < 2) return;
            var continued = ContinueObservedLanding();
            _heading=Vector2.Normalize(XZ(position-_bootstrapOrigin)); _rayOrigin=_bootstrapOrigin;
            _physicalIndex=nativeIndex; _physicalTarget=nativeTarget;
            _aimHeading=null; _aimBaseline=0; RememberAim(_bootstrapOrigin);
            _lastOldPosition=position; _lastOldAt=previousAt; _confirmed=continued; _bootstrapIndex=-1;
        }

        BlundervilleObservedSlide? ContinueObservedLanding()
        {
            // A shallow turn cannot locate a seam by intersecting two nearly
            // parallel lines. Two samples along the new target still prove
            // acquisition, and the previous observed slide already bounds
            // its landing. Carry that whole bound and clock forward instead
            // of throwing away all physical evidence at the shallow seam.
            if (_confirmed is not { } prior || prior.NativeIndex + 1 != nativeIndex) return null;
            var start = (prior.ShortLanding + prior.LongLanding) / 2;
            var radius = XZ(prior.LongLanding - prior.ShortLanding).Length() / 2 + prior.LandingRadius;
            var minimumDistance = XZ(nativeTarget - start).Length() - radius;
            if (minimumDistance <= .5f) return null;
            var shortClock = BlundervilleIceTiming.Instance.PrepareSlideExit(prior.Start, prior.ShortLanding, MinimumSlide, prior.LandingRadius);
            var longClock = BlundervilleIceTiming.Instance.PrepareSlideExit(prior.Start, prior.LongLanding, MaximumSlide, prior.LandingRadius);
            var earliest = prior.SeamEarliest.AddSeconds(Math.Min(shortClock.EarliestDuration, longClock.EarliestDuration));
            var latest = prior.SeamLatest.AddSeconds(Math.Max(shortClock.LatestDuration, longClock.LatestDuration));
            if (latest > now) latest = now;
            if (earliest > latest) return null;
            var heading = Vector3.Normalize(nativeTarget - start);
            var growth = MathF.Max(1, MaximumSlide / minimumDistance - 1);
            return new(nativeIndex, nativeTarget, start, radius, heading, start + heading * MinimumSlide,
                start + heading * MaximumSlide, radius * growth + _error, 0, 0, 0, 0, earliest, latest, 0, 0);
        }
    }

    private void RememberAim(Vector3 sample)
    {
        var ray = XZ(_physicalTarget - sample); var distance = ray.Length();
        if (distance <= MathF.Max(.5f, _aimBaseline) || _heading is not { } heading
            || Vector2.Dot(ray / distance, heading) < .995f) return;
        _aimBaseline = distance; _aimHeading = ray / distance;
    }

    private bool IntersectAimedRays(Vector2 outgoing, float outgoingBaseline, Vector3 target,
        out Vector3 start, out float radius)
    {
        start = default; radius = float.PositiveInfinity;
        if (_aimHeading is not { } incoming) return false;
        var sine = Cross(incoming, outgoing);
        var incomingError = _error / (_aimBaseline - _error);
        var outgoingError = _error / (outgoingBaseline - _error);
        // Both physical rays pass through their exact native target. Player
        // samples bound their directions; near-parallel lines are accepted
        // only when the resulting intersection ball remains well conditioned.
        // A unit-vector perturbation is at most twice its angular sine, so
        // this is a lower bound on the true pair's determinant.
        var determinant = MathF.Abs(sine) - 2 * (incomingError + outgoingError);
        if (determinant <= 0) return false;
        start = _physicalTarget + V3(incoming * (Cross(XZ(target - _physicalTarget), outgoing) / sine));
        radius = (XZ(start - _physicalTarget).Length() * incomingError
            + XZ(start - target).Length() * outgoingError) / determinant + _error;
        return radius <= MaximumStartRadius;
    }

    private BlundervilleObservedSlide? Current(Vector3 position, DateTime now)
    {
        if (_confirmed is not { } observation) return null;
        var travelled = Vector2.Dot(XZ(position - observation.Start), XZ(observation.Heading));
        var uncertainty = observation.StartRadius + _error;
        var minimum = MathF.Max(0, travelled - uncertainty); var maximum = MathF.Max(0, travelled + uncertainty);
        return observation with { TravelledMin = minimum, TravelledMax = maximum,
            RemainingLengthMin = MathF.Max(0, MinimumSlide - maximum), RemainingLengthMax = MathF.Max(0, MaximumSlide - minimum),
            AgeMin = Math.Max(0, (now - observation.SeamLatest).TotalSeconds), AgeMax = Math.Max(0, (now - observation.SeamEarliest).TotalSeconds) };
    }

    private void Seed(Vector3 position, DateTime now, object generation, int index, Vector3 target)
    {
        Reset(); _generation = generation; _previous = position; _previousAt = now;
        _nativeIndex = index; _nativeTarget = target;
    }

    private static Vector2 XZ(Vector3 v) => new(v.X, v.Z);
    private static Vector3 V3(Vector2 v) => new(v.X, 0, v.Y);
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}

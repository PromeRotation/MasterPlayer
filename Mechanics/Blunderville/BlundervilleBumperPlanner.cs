using System.Collections.Concurrent;
using System.Numerics;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Preserve native input while extending a validated route at a physical slide seam.
internal sealed class BlundervilleBumperPlanner(Func<Func<BlundervilleBumperPlanner.Step[]>, Task<BlundervilleBumperPlanner.Step[]>>? schedule = null, double publicationDelay = .2)
{
    internal const double PublicationWindow = 1;
    // Recorded 3693 gains while walking south across the platform: z=-180.2..-180.45.
    internal const float IceStatusEdge = -180.3f;
    internal const double EscapeTrigger = 2.5, EscapeMinimumHold = 1.5, EscapeMaximumHold = 4;
    // ARR 01:01 replay: a committed ice endpoint clear for only 1.5s became a
    // dead end when its preview continuation failed. Falling back to such an
    // endpoint when no longer refuge exists reproduced the hit; wait instead.
    internal const double RefugeHold = 2.5;
    internal sealed record Step(Vector3 Position, double Wait, float Speed = 6, bool Sliding = false,
        Vector3? ShortLanding = null, Vector3? LongLanding = null, double ShortTimeOffset = 0, double LongTimeOffset = 0,
        double EndpointSafety = 0, float LandingRadius = 0, Vector3? LandingPosition = null, double? Duration = null,
        double RemainingDuration = 0, Vector3? IncomingDirection = null);
    private sealed record Node(Vector3 Position, double Time, double WaitBefore, Node? Parent, float Speed = 6, bool Sliding = false,
        Vector3? ShortLanding = null, Vector3? LongLanding = null, double? ShortTime = null, double? LongTime = null, float Radius = 0, Vector3? NativeTarget = null,
        bool Preview = false);
    private readonly List<Step> _path = [];
    private int _cursor;
    private Vector3 _origin;
    private IReadOnlyList<Vector3>? _run;
    private Spatial.DirectTraversalMotion? _motion;
    private int _suffixAnchor = -1;
    private bool _suffixRequired;
    private bool _continuationBlocked;
    private IReadOnlyList<Vector3>? _requestedRun;
    private DateTime _retry, _requestedAt, _waitUntil, _plannedDeparture;
    private Task<Step[]>? _pending;
    // Clearing a generation discards its result, but does not spawn another
    // expensive worker while that computation is still running.
    private Task<Step[]>? _searchInFlight;
    private Vector3 _requestedPosition;
    private Step[]? _proposal;
    private IReadOnlyList<Vector3>? _proposalRun;
    private DirectTraversalMotion? _proposalMotion;
    private DateTime _proposedAt, _publishedAt;
    private bool _acknowledged;
    private readonly BlundervilleSlideObservation _slides = new();
    private object _slideGeneration = new();

    public void Reset() { ClearPath(); _retry = default; }    private void ClearPath()
    {
        _path.Clear(); _cursor = 0; _run = null; _motion = null; _waitUntil = default;
        _pending = null; _suffixAnchor = -1; _requestedRun = null; _requestedAt = default; _requestedPosition = default; _plannedDeparture = default;
        _continuationBlocked = false;
        _proposal = null; _proposalRun = null; _proposalMotion = null; _acknowledged = false; _publishedAt = default; _slides.Reset(); _slideGeneration = new();
    }

    public BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        BlundervilleEncounter encounter, Vector3 player, DateTime now, DirectTraversalExecution? execution = null)
    {
        if (!state.IsRacing || state.Qualified || state.Course != BlundervilleCourse.FirstA || player.Y < 16
            || player.Z > -158 || player.Z < -208 && (_run == null || execution?.HasControl != true) || !state.CanMove(now))
        { Reset(); return normal; }
        var hazards = encounter.TraversalHazards(now).Where(h => Radar.RadarLocalGeometry.Intersects(h.Geometry.GetBounds(), player, 65)).ToArray();
        ObstacleIndex? validation = null;
        var floor = BlundervilleRoutes.Boundary(state.Course);
        var speed = state.NavigationBaseSpeed(now);
        if (_proposalRun != null && now > _proposedAt)
        {
            if (execution is { HasControl: true } && execution.Path.SequenceEqual(_proposalRun)
                && (execution.Motion == null || execution.Motion.Matches(_proposalMotion, _proposalRun)))
            {
                _path.Clear(); _path.AddRange(_proposal!); _run = _proposalRun; _motion = _proposalMotion;
                _acknowledged = true; _continuationBlocked = false;
            }
            // An unapplied or rejected amendment leaves the accepted route active.
            _proposal = null; _proposalRun = null; _proposalMotion = null;
        }
        if (_run != null && execution != null && now > _publishedAt)
        {
            if (execution.HasControl && execution.Path.SequenceEqual(_run)
                && (execution.Motion == null || execution.Motion.Matches(_motion, _run)))
            { _acknowledged = true; _cursor = Math.Clamp(execution.CurrentIndex, 0, _path.Count); }
            else if (execution.Rejected || _acknowledged || now - _publishedAt > TimeSpan.FromSeconds(.25)) ClearPath();
        }
        if (_run != null && !OnPublishedRoute(player)) ClearPath();
        AdvanceCursor();
        var observation = _run != null && execution is { HasControl: true } && _cursor < _path.Count
            ? _slides.Observe(player, now, _slideGeneration, _cursor, _path[_cursor].Position, state.IsSlippery)
            : null;
        var predictions = new Dictionary<int, BlundervilleIceContinuation.Prediction>();
        BlundervilleIceContinuation.Prediction Prefix(int anchor)
        {
            if (!predictions.TryGetValue(anchor, out var value))
            {
                var physicalCursor = _cursor;
                if (observation == null && state.IsSlippery && _cursor > 0 && _path[_cursor-1].Sliding)
                {
                    var a = _cursor > 1 ? _path[_cursor-2].LandingPosition ?? _path[_cursor-2].Position : _origin;
                    var b = _path[_cursor-1].LandingPosition ?? _path[_cursor-1].Position;
                    if (Vector3.Dot(player-b,b-a) < 0 && GeometryHelper.DistanceSquaredPointToSegmentXZ(player,a,b) < .15f*.15f)
                        physicalCursor--;
                }
                predictions[anchor] = value = BlundervilleIceContinuation.Predict(player, now, _path, physicalCursor, anchor, observation)!;
            }
            return value;
        }
        if (_pending is { IsCompleted: true } completed && (now - _requestedAt).TotalSeconds + .00001 >= publicationDelay)
        {
            _pending = null;
            if (_suffixAnchor >= 0)
            {
                if (completed.IsCompletedSuccessfully && ReferenceEquals(_requestedRun, _run)
                    && _cursor <= _suffixAnchor && now >= _requestedAt && now - _requestedAt < TimeSpan.FromSeconds(PublicationWindow)
                    && _suffixAnchor == _path.Count - 1 && completed.Result.Length == 0)
                    _continuationBlocked = true;
                if (ReferenceEquals(_requestedRun, _run) && _cursor <= _suffixAnchor && _suffixAnchor < _path.Count)
                    _suffixRequired |= _path[^1].Position.Z > -208 && _suffixAnchor == _path.Count-1
                        || !CanPublish(_path.Skip(_suffixAnchor + 1).ToArray(),
                            Prefix(_suffixAnchor).Anchor, TimeTo(_suffixAnchor), Prefix(_suffixAnchor).Radius);
                if (completed.IsCompletedSuccessfully && ReferenceEquals(_requestedRun, _run) && _cursor <= _suffixAnchor
                    // Pending and completed work share the same deadline;
                    // the latest prefix and suffix are revalidated below.
                    && now >= _requestedAt && now - _requestedAt < TimeSpan.FromSeconds(PublicationWindow)
                    && completed.Result.Length > 0 && completed.Result.All(s => s.Wait == 0)
                    && (_suffixAnchor == _cursor || CanPublish(_path.Skip(_cursor + 1).Take(_suffixAnchor - _cursor).ToArray(),
                        Prefix(_cursor).Anchor, TimeTo(_cursor), Prefix(_cursor).Radius))
                    && CanPublish(completed.Result, Prefix(_suffixAnchor).Anchor, TimeTo(_suffixAnchor), Prefix(_suffixAnchor).Radius)
                    && (_suffixRequired || _path[^1].Position.Z > -208 || CandidateTime(completed.Result, _path[_suffixAnchor].Position) + TimeTo(_suffixAnchor) < TimeTo(_path.Count - 1) - .5)
                    && (_suffixRequired || Improves(completed.Result)))
                {
                    _proposal = _path.Take(_suffixAnchor + 1).Concat(completed.Result).ToArray();
                    _proposalRun = _proposal.Select(s => s.Position).ToArray();
                    _proposalMotion = Motion(_proposal);
                    _proposedAt = now;
                }
                _suffixAnchor = -1; _requestedRun = null;
            }
            else if (completed.IsCompletedSuccessfully && now >= _requestedAt && now - _requestedAt < TimeSpan.FromSeconds(PublicationWindow)
                && completed.Result.Length > 0 && Distance(player, _requestedPosition) < .25f
                && execution?.InputLocked != true)
            {
                var candidate=completed.Result;
                var lead=Math.Max(0,(_plannedDeparture-now).TotalSeconds);
                // A planned initial hold belongs to the current standing point.
                // Sub-.25m drift since the request must not leave a second hold
                // step that the cursor can never reach and then silently skips.
                if(candidate[0].Wait>0 && Distance(candidate[0].Position,player)<.25f)
                    candidate=[candidate[0] with {Position=player,Wait=candidate[0].Wait+lead},..candidate.Skip(1)];
                else if(lead>0)
                    candidate=[new(player,lead),..candidate];
                if(CanPublish(candidate))
                {
                    _origin = player; _path.Clear(); _cursor = 0; _path.AddRange(candidate); _waitUntil = default;
                    if (_path.Count > 0 && _path[0].Wait > 0)
                        _waitUntil = now.AddSeconds(_path[0].Wait);
                }
            }
        }
        if (_pending != null && (now < _requestedAt || now - _requestedAt >= TimeSpan.FromSeconds(PublicationWindow)))
        { _pending = null; _suffixAnchor = -1; _requestedRun = null; }
        AdvanceCursor();
        if (_cursor < _path.Count)
        {
            var from = player; var time = _run == null && _path[_cursor].Wait == 0 ? .1 : 0.0; var valid = true;
            var shortFrom = player; var longFrom = player; var shortTime = time; var longTime = time; var fromRadius = 0f;
            var segmentStart = _cursor == 0 ? _origin : _path[_cursor - 1].Position;
            // The native current target may already be the next slide while
            // the previous physical slide finishes. Protect that current
            // target; the following queue entry is still amendable.
            var acquiredSlide = state.IsSlippery && _run != null && _path[_cursor].Sliding
                && (execution?.InputLocked == true || execution == null && Distance(player, segmentStart) > .05f);
            var protectedEnd = acquiredSlide ? _cursor : _cursor - 1;
            for (var i = _cursor; i < _path.Count; ++i)
            {
                var step = _path[i];
                // After the ice status ends, the remaining queued target is
                // ordinary movement. Revalidating it as another full slide
                // invents five metres of travel from the live dry position.
                if (_run != null && !state.IsSlippery && player.Z < -204.5f)
                    step = step with { Sliding = false, Speed = speed, LandingPosition = step.Position,
                        ShortLanding = step.Position, LongLanding = step.Position, LandingRadius = 0,
                        ShortTimeOffset = 0, LongTimeOffset = 0, Duration = null };
                var physical = _run != null && observation != null ? Prefix(i) : null;
                if (physical is { UsesObservation: true }) step = step with {
                    LandingPosition = physical.Anchor.Position, ShortLanding = physical.Anchor.ShortLanding,
                    LongLanding = physical.Anchor.LongLanding, LandingRadius = physical.Radius };
                var to = step.LandingPosition ?? step.Position; var arrival = physical is { UsesObservation: true } ? physical.Eta
                    : time + (i > _cursor ? step.Duration ?? TravelTime(from,to,step) : TravelTime(from,to,step));
                var wait = i == _cursor && _waitUntil != default ? Math.Max(0, (_waitUntil - now).TotalSeconds) : step.Wait;
                var settle = wait + (i == _path.Count - 1 ? step.EndpointSafety : 0);
                var shortTo = step.ShortLanding ?? step.Position; var longTo = step.LongLanding ?? step.Position;
                var shortArrival = shortTime + TravelTime(shortFrom, shortTo, step);
                var longArrival = longTime + TravelTime(longFrom, longTo, step);
                var safe = step.Sliding && step.LandingPosition != null
                    ? SafeSlide(floor, Validation(), from, shortFrom, longFrom, step.Position,
                        Math.Min(shortTime,time), Math.Max(longTime,time), settle, .1,
                        fromRadius, step.LandingRadius, i > 0 && _path[i-1].Sliding || from.Z < -181.4f)
                    : Safe(floor, Validation(), from, to, Math.Min(shortTime,time), arrival, settle, Math.Max(0,longTime-Math.Min(shortTime,time)), step.Sliding, step.Speed, step.LandingRadius)
                        && Safe(floor, Validation(), shortFrom, shortTo, shortTime, shortArrival, settle, 0, step.Sliding, step.Speed)
                        && Safe(floor, Validation(), longFrom, longTo, longTime, longArrival, settle, 0, step.Sliding, step.Speed);
                if (i > protectedEnd && !safe)
                {
                    if (acquiredSlide)
                    {
                        RequestSuffix(protectedEnd, true);
                        break;
                    }
                    if (_run != null && i > _cursor && Safe(floor, Validation(), player, _path[_cursor].Position,
                        0, TravelTime(player, _path[_cursor].Position, _path[_cursor]), 0, 0, _path[_cursor].Sliding, _path[_cursor].Speed))
                    {
                        // A future invalid ray may be impossible to repair
                        // from its immediate predecessor. While still walking
                        // we can change the approach after the current native
                        // point, before committing to that dead end.
                        RequestSuffix(_cursor, true);
                        break;
                    }
                    valid = false; break;
                }
                if (_run != null && observation != null && Prefix(i) is { UsesObservation: true } physicalAnchor)
                {
                    from = physicalAnchor.Anchor.Position; time = physicalAnchor.Eta;
                    shortFrom = physicalAnchor.Anchor.ShortLanding ?? from; longFrom = physicalAnchor.Anchor.LongLanding ?? from;
                    shortTime = physicalAnchor.Earliest; longTime = physicalAnchor.Latest;
                    fromRadius = physicalAnchor.Radius;
                    continue;
                }
                from = to; time = arrival + wait + (wait > 0 ? .1 : 0);
                shortFrom = shortTo; longFrom = longTo;
                shortTime = arrival + step.ShortTimeOffset + wait + (wait > 0 ? .1 : 0); longTime = arrival + step.LongTimeOffset + wait + (wait > 0 ? .1 : 0);
                fromRadius = step.LandingRadius;
            }
            if (valid)
            {
                if (_waitUntil - now > TimeSpan.FromTicks(10))
                {
                    return new(player, true, false, null, "圆圈机关：在安全间隙等待下一拍", true);
                }
                if (_run == null)
                {
                    if (CanPublish(_path.Skip(_cursor).ToArray())) { PublishRun(); _publishedAt = now; }
                    else ClearPath();
                }
                if (_run != null)
                {
                    // Extend a still-safe queue at its tail. Replacing safe
                    // turns every scan changes the approach to the next seam
                    // before better live evidence is available. Invalid future
                    // segments are repaired immediately by the checks above.
                    // Reserve two physical slides and one publication window.
                    // A known blocked tail needs repair immediately, even if
                    // the safe approach still has more time remaining.
                    var continuationLead = 2 * 5.1 / 2.88 + publicationDelay + .1;
                    if (_path[^1].Position.Z > -208 && _proposal == null
                        && (_continuationBlocked || TimeTo(_path.Count - 1) <= continuationLead))
                        RequestSuffix(_continuationBlocked ? _cursor : _path.Count - 1, _continuationBlocked);
                    var published = _proposalRun ?? _run;
                    return new(published[^1], false, false, null, state.IsSlippery
                        ? "冰桥：按五米滑移段连续穿行" : "圆圈机关：沿动画通路连续前进", true)
                    { ContinuousPath = published, TraversalMotion = (_proposalMotion ?? _motion)! with { InputLockActive = state.IsSlippery } };
                }
            }
            ClearPath();
        }
        else if (_path.Count > 0) ClearPath();
        if (_pending == null && execution?.InputLocked != true && now >= _retry && encounter.BumpersReady(player))
        {
            RequestPlan(player);
        }
        return new(player, true, false, null, "圆圈机关：等待通路开放", true);

        ObstacleIndex Validation() => validation ??= new(hazards, now, .2);

        void RequestPlan(Vector3 origin)
        {
            if (_pending != null || _searchInFlight is { IsCompleted: false } || now < _retry || !encounter.BumpersReady(origin)) return;
            _retry = now.AddSeconds(.15); _requestedAt = now; _requestedPosition = origin;
            _suffixAnchor = -1; _requestedRun = null;
            // Use one absolute launch time within the existing publication
            // deadline. Actual CPU time must not shift an already timed route.
            var departure = _plannedDeparture = now.AddSeconds(PublicationWindow);
            var onIce = state.IsSlippery;
            _pending = (schedule ?? (work => Task.Run(work)))(() => Search(floor, hazards, origin, speed, departure, onIce: onIce));
            _searchInFlight = _pending;
        }
        void RequestSuffix(int anchor, bool required = false)
        {
            // A tail extension cannot repair an earlier unsafe turn. Discard
            // that request's result, while retaining the single-worker limit.
            if (required && ReferenceEquals(_requestedRun, _run) && anchor < _suffixAnchor)
            { _pending = null; _suffixAnchor = -1; _requestedRun = null; }
            if (required && ReferenceEquals(_requestedRun, _run)) _suffixRequired = true;
            if (_pending != null || _searchInFlight is { IsCompleted: false } || _proposal != null || now < _retry
                || anchor >= _path.Count || !encounter.BumpersReady(player)) return;
            var delay = TimeTo(anchor);
            while (delay < publicationDelay + .1 && anchor + 1 < _path.Count)
                delay = TimeTo(++anchor);
            if (delay < publicationDelay + .1) return;
            _retry = now.AddSeconds(.15); _requestedAt = now; _suffixAnchor = anchor; _requestedRun = _run; _suffixRequired = required;
            var forecast = Prefix(anchor); var anchorStep = forecast.Anchor; var departure = now.AddSeconds(delay);
            _pending = (schedule ?? (work => Task.Run(work)))(() => Search(floor, hazards, anchorStep.Position, speed, departure,
                20.5 - delay, anchorStep, false));
            _searchInFlight = _pending;
        }
        double TimeTo(int anchor) => Prefix(anchor).Eta;
        bool Improves(Step[] candidate)
        {
            var candidateRemaining = candidate[^1].RemainingDuration; var oldRemaining = _path[^1].RemainingDuration;
            // The terminal estimate is not queued input. Extending a partial
            // certified route cannot be vetoed by its old optimistic estimate.
            if (_path[^1].Position.Z > -208 && double.IsFinite(candidateRemaining)
                && (candidate[^1].LandingPosition ?? candidate[^1].Position).Z
                    < (_path[^1].LandingPosition ?? _path[^1].Position).Z - .5f) return true;
            if (double.IsFinite(candidateRemaining))
                return !double.IsFinite(oldRemaining) || CandidateTime(candidate,Prefix(_suffixAnchor).Anchor.Position)
                    + TimeTo(_suffixAnchor) + candidateRemaining < TimeTo(_path.Count-1) + oldRemaining - .5;
            return !double.IsFinite(oldRemaining) && (candidate[^1].LandingPosition ?? candidate[^1].Position).Z
                < (_path[^1].LandingPosition ?? _path[^1].Position).Z - .5f;
        }
        double CandidateTime(Step[] candidate, Vector3 from)
        {
            var time = 0.0;
            foreach (var step in candidate) { var to = step.LandingPosition ?? step.Position; time += step.Duration ?? TravelTime(from, to, step); from = to; }
            return time;
        }
        bool CanPublish(Step[] candidate, Step? anchor = null, double delay = 0, float radius = 0)
        {
            var from = anchor?.Position ?? player; var time = delay;
            var shortFrom = anchor?.ShortLanding ?? from; var longFrom = anchor?.LongLanding ?? from;
            var shortTime = delay + (anchor?.ShortTimeOffset ?? 0); var longTime = delay + (anchor?.LongTimeOffset ?? 0);
            foreach (var step in candidate)
            {
                var settle = step.Wait + (ReferenceEquals(step, candidate[^1]) ? step.EndpointSafety : 0);
                var to = step.LandingPosition ?? step.Position; var arrival = time + (step.Duration ?? TravelTime(from, to, step));
                var shortTo = step.ShortLanding ?? step.Position; var longTo = step.LongLanding ?? step.Position;
                var shortArrival = shortTime + TravelTime(shortFrom, shortTo, step);
                var longArrival = longTime + TravelTime(longFrom, longTo, step);
                if (step.Sliding && step.LandingPosition != null)
                {
                    if (!SafeSlide(floor, Validation(), from, shortFrom, longFrom, step.Position,
                        Math.Min(shortTime,time), Math.Max(longTime,time), settle,.1,
                        radius, step.LandingRadius, anchor?.Sliding == true || from.Z < -181.4f))
                        return false;
                }
                else if (!Safe(floor, Validation(), from, to, Math.Min(shortTime, time), arrival, settle, .1 + Math.Max(0, longTime - Math.Min(shortTime, time)), step.Sliding, step.Speed, Math.Max(radius, step.LandingRadius))
                    || !Safe(floor, Validation(), shortFrom, shortTo, shortTime, shortArrival, settle, .1, step.Sliding, step.Speed)
                    || !Safe(floor, Validation(), longFrom, longTo, longTime, longArrival, settle, .1, step.Sliding, step.Speed)) return false;
                var wait = step.Wait + (step.Wait > 0 ? .1 : 0);
                from = to; time = arrival + wait;
                radius = step.LandingRadius;
                shortFrom = shortTo; longFrom = longTo; shortTime = arrival + step.ShortTimeOffset + wait; longTime = arrival + step.LongTimeOffset + wait;
            }
            return true;
        }
        void AdvanceCursor()
        {
            if (_run != null && execution is { HasControl: true } && execution.Path.SequenceEqual(_run)) return;
            while (_cursor < _path.Count && (Distance(player, _path[_cursor].Position) < (_path[_cursor].Sliding
                    ? (_path.Take(_cursor).Any(s => s.Sliding) ? .35f : .55f) : .06f)
                || _path[_cursor].Wait == 0 && Passed(player, _cursor == 0 ? _origin : _path[_cursor - 1].Position, _path[_cursor].Position)))
            {
                if (_path[_cursor].Wait > 0)
                {
                    if (_waitUntil == default) _waitUntil = now.AddSeconds(_path[_cursor].Wait);
                    // Sub-microsecond conversion noise must not delay a
                    // scheduled departure by an entire 100ms policy scan.
                    if (_waitUntil - now > TimeSpan.FromTicks(10)) break;
                }
                ++_cursor; _waitUntil = default;
            }
        }
    }

    private void PublishRun()
    {
        // Include the shared prefix so the executor can amend its actual
        // remaining queue without resetting a locked current point.
        _run = _path.Select(s => s.Position).ToArray();
        _motion = Motion(_path);
    }

    private static DirectTraversalMotion Motion(IReadOnlyList<Step> path)
        => new(path.Select(s => s.Speed).ToArray(), path.Select(s => s.Sliding).ToArray(), Timing: BlundervilleIceTiming.Instance, PhysicalLandings: path.Select(s => s.LandingPosition ?? s.Position).ToArray());

    private bool OnPublishedRoute(Vector3 player)
    {
        var first = Math.Max(0, _cursor - 1);
        var from = first == 0 ? _origin : _path[first - 1].LandingPosition ?? _path[first - 1].Position;
        for (var i = first; i <= _cursor && i < _path.Count; ++i)
        {
            var step = _path[i];
            var to = step.LandingPosition ?? step.Position;
            if (GeometryHelper.DistanceSquaredPointToSegmentXZ(player, from, to) <= .8f * .8f) return true;
            from = to;
        }
        return false;
    }

    private static Step[] Search(IGeometryShape floor, BlundervilleHazard[] hazards, Vector3 player, float speed, DateTime now,
        double horizon = 20.5, Step? anchor = null, bool allowInitialWait = true, bool onIce = false)
    {
        // Shared by the parallel layer expansion below.
        var ground = new ConcurrentDictionary<(Vector3 From, Vector3 To, bool Sliding), bool>();
        var obstacles = new ObstacleIndex(hazards, now, .25);
        // Initial departure is now absolute, independent of worker latency.
        // Reserve one 100ms policy scan plus 100ms native input acquisition.
        var acquisitionDelay = anchor == null ? .2 : .1;
        bool CanGo(Vector3 from, Vector3 to, double start, double end, double settle, bool sliding = false, double timingSpread = 0, float radius = 0)
        {
            if (!ground.TryGetValue((from, to, sliding), out var traversable))
                ground[(from, to, sliding)] = traversable = OnFloor(floor, from, to, sliding ? .2f : .1f)
                    && (sliding || Distance(from, to) < .01f || OnFloor(floor,
                        from, to + Vector3.Normalize(to - from) * (speed / 30), .1f));
            if (!traversable || radius > 0 && !OnFloor(floor, from, to, .2f + radius)) return false;
            // Native arrival can consume a walking corner one input frame
            // after passing it. Validate that incoming overrun explicitly;
            // live correction uses the same .1m floor margin as the executor.
            var overrun = !sliding && Distance(from, to) > .01f ? speed / 30 : 0;
            var physicalTo = overrun > 0 ? to + Vector3.Normalize(to - from) * overrun : to;
            if (sliding) return Distance(from,to) < .001f
                ? obstacles.Avoids(from,to,start,end,settle,acquisitionDelay + timingSpread,.1f + radius)
                : obstacles.AvoidsSlide(from, to, start, settle, acquisitionDelay + timingSpread, .1f, radius);
            return obstacles.Avoids(from, physicalTo, start, end + overrun / speed, settle, acquisitionDelay + timingSpread, sliding ? .4f : .1f);
        }
        List<Node> layer = [new(player, 0, 0, null, speed, anchor?.Sliding ?? false,
            anchor?.ShortLanding, anchor?.LongLanding, anchor?.ShortTimeOffset, anchor?.LongTimeOffset, anchor?.LandingRadius ?? 0)];
        // A fresh plan whose standing point is itself about to be hit may not
        // wait there for a complete exit route (01:01 ARR replay: a pulse hit
        // the idle player on the ice after its continuation failed).
        var endangered = anchor == null && allowInitialWait && player.Z < -158.5f
            && !CanGo(player, player, 0, EscapeTrigger, 0);
        List<Node>? firstLayer = null;
        var refuges = new ConcurrentDictionary<Node, bool>(ReferenceEqualityComparer.Instance);
        const float entry = -180.75f;
        for (var z = player.Z - 2; player.Z > entry && layer.Count != 0; z -= 2)
        {
            z = MathF.Max(entry, z);
            var next = new Dictionary<(int X, int Time), Node>();
            // Expanded per node and wait in parallel, merged in the sequential order.
            var expansions = layer.SelectMany(previous => Enumerable.Range(0, (int)Math.Round((allowInitialWait && player.Z > -163 && previous.Parent == null ? 6 : 0) / .25) + 1)
                .Select(k => (Previous: previous, Wait: k * .25))).ToArray();
            var walked = new List<((int X, int Time) Key, Node Node, double Late)>[expansions.Length];
            Parallel.For(0, expansions.Length, index =>
            {
                var (previous, wait) = expansions[index];
                var found = new List<((int X, int Time) Key, Node Node, double Late)>();
                walked[index] = found;
                if (wait > 0 && !CanGo(previous.Position, previous.Position, previous.Time, previous.Time + wait + .1, 0)) return;
                var lastX = z <= -180 ? 29 : 10;
                for (var x = -6; x <= lastX+2; ++x)
                {
                    var p = new Vector3(x == lastX+1 ? -207.4f : x == lastX+2 ? -214.4f : -211 + x * .5f, player.Y, z);
                    var start = previous.Time + wait + (wait > 0 ? .1 : 0); var time = start + Distance(previous.Position, p) / speed;
                    var cornerDelay = WalkingCornerDelay(previous, p);
                    var late = (previous.LongTime ?? previous.Time) + time - previous.Time + cornerDelay;
                    if (late > horizon || !CanGo(previous.Position, p, start, time, 0, timingSpread: late - time)) continue;
                    found.Add(((x, (int)(time / .05)), new(p, time, wait, previous, speed, ShortTime: time, LongTime: late), late));
                }
            });
            foreach (var found in walked)
                foreach (var (key, node, late) in found)
                    if (!next.TryGetValue(key, out var old) || (old.LongTime ?? old.Time) > late) next[key] = node;
            layer = SelectLayer(next.Values, 24, z > -180 ? 120 : 240, false);
            if (endangered) firstLayer ??= [.. layer];
            if (z == entry) break;
        }
        // 17:44 ARR: there is no dry strip between the bridge holes (ending at
        // -180.475) and the ice: the 3693 status lands near -180.3, so the old
        // lateral platform walk along -180.75 became uncontrolled 5m slides.
        // Only an unchanged walk keeps 6m/s past the status edge; any lateral
        // alignment there is planned as a slide (StartsSlide).
        if (player.Z > entry || player.Z >= -181.2f && anchor is { Sliding: false })
            layer = SelectLayer(layer, 24, 48, true);
        // Any new input after the status edge is a native slide: a standing
        // start (the root while slippery) or a turn at a walked node there.
        bool StartsSlide(Node node, Vector3 target)
        {
            if (node.Position.Z < -204.5f) return false;
            if (node.Parent == null) return onIce || node.Position.Z <= IceStatusEdge;
            if (node.Position.Z > IceStatusEdge) return false;
            var walked = node.Position - node.Parent.Position; walked.Y = 0;
            var next = target - node.Position; next.Y = 0;
            return walked.LengthSquared() < 1e-4f || next.LengthSquared() < 1e-4f
                || Vector3.Dot(Vector3.Normalize(walked), Vector3.Normalize(next)) < .985f;
        }
        // 3693 extra=50: two complete recorded slides are 4.88/4.97m,
        // and two consecutive slides are 9.83m. Turns belong at these seams,
        // never at the ordinary 2m lattice points.
        Node? last = null; var bestScore = double.PositiveInfinity;
        Node? Exit(Node n)
        {
            var a = n.ShortLanding ?? n.Position; var b = n.LongLanding ?? n.Position;
            if (MathF.Max(a.Z,b.Z)+n.Radius >= -204.85f || n.NativeTarget!.Value.Z >= -204.85f) return null;
            var heading = Vector3.Normalize(n.Position-n.Parent!.Position);
            if (heading.Z >= -.1f) return null;
            var finish = n.Position + heading * MathF.Max(.5f,(-209.2f-n.Position.Z)/heading.Z);
            // The ice status may still be active at search time. Beyond the
            // fixed surface exit the physical model resumes ordinary 6m/s.
            const float exitSpeed = 6;
            var duration = Distance(n.Position,finish)/exitSpeed;
            var early = (n.ShortTime ?? n.Time) + Math.Max(0,Distance(a,finish)-n.Radius)/exitSpeed;
            var late = (n.LongTime ?? n.Time) + (Distance(b,finish)+n.Radius)/exitSpeed;
            if (n.Time+duration > horizon || !Safe(floor,obstacles,n.Position,finish,n.ShortTime ?? n.Time,
                late,.4,acquisitionDelay+Math.Max(0,(n.LongTime ?? n.Time)-(n.ShortTime ?? n.Time)),radius:n.Radius)
                || !Safe(floor,obstacles,a,finish,n.ShortTime ?? n.Time,late,.4,acquisitionDelay,radius:n.Radius)
                || !Safe(floor,obstacles,b,finish,n.ShortTime ?? n.Time,late,.4,acquisitionDelay,radius:n.Radius)) return null;
            return new(finish,n.Time+duration,0,n,exitSpeed,false,finish,finish,early,late,0,null,n.Preview);
        }
        // A fresh plan that starts standing on the ice may hold position
        // instead of burning time with lateral slides. Holds are only offered
        // at the root, before input is acquired, and each one is validated.
        if (anchor == null && allowInitialWait && player.Z <= entry && layer.Count == 1 && layer[0].Parent == null)
        {
            var standing = layer[0];
            for (var hold = .25; hold <= 3.001; hold += .25)
            {
                if (!CanGo(standing.Position, standing.Position, 0, hold + .1, 0)) break;
                layer.Add(standing with { Time = hold + .1, WaitBefore = hold });
            }
        }
        const int committedSlides = 2;
        var slideLimit = Math.Ceiling(horizon * 3 / 5);
        for (var slide = 0; slide < slideLimit && layer.Count > 0; ++slide)
        {
            layer.RemoveAll(n=>n.Time+MinimumRemainingTime(n.Position.Z)>=bestScore);
            if(layer.Count==0) break;
            var next = new Dictionary<(int X, int Z, int Time, int ShortX, int ShortZ, int LongX, int LongZ, int ShortTime, int LongTime), Node>();
            // Refine a blocked layer before abandoning a narrow animation
            // gap. The coarse quarter-metre headings can straddle both safe
            // tangents even though a fully checked ray exists between them.
            for(var refinement=0;refinement<2 && next.Count==0;++refinement)
            {
            // 23:48 ARR: one search took 0.5–0.6s on a single core while the
            // player waited for it. Each node of a layer expands on its own
            // core; the merge keeps the sequential order and pruning, so the
            // result does not depend on thread timing.
            var snapshot = bestScore;
            // In generation order: an exit (Key null) or a node with the bound it was pruned by.
            var expanded = new List<((int, int, int, int, int, int, int, int, int)? Key, Node Node, double Bound)>[layer.Count];
            Parallel.For(0, layer.Count, index =>
            {
                var previous = layer[index];
                var found = new List<((int, int, int, int, int, int, int, int, int)? Key, Node Node, double Bound)>();
                foreach (var target in SlideTargets(previous.Position,refinement==0 ? .25f : .05f))
                {
                    var startedOnIce = previous.Sliding || player.Z < -181.2f || StartsSlide(previous, target);
                    if (!startedOnIce && target.Z >= previous.Position.Z - .5f) continue;
                    if (startedOnIce && target.Z > -181.7f) continue;
                    // A lateral detour must leave its row before reversing.
                    // Apply this to previews and published suffixes alike.
                    var incoming = previous.Parent is { } parent && previous.Sliding
                        ? previous.Position - parent.Position : anchor?.IncomingDirection;
                    if (incoming is { } direction && direction.X * (target.X - previous.Position.X) < 0
                        && (MathF.Abs(direction.Z) < .5f && target.Z >= previous.Position.Z - .5f
                            || PlatformReversal(previous.Position, direction, target))) continue;
                    var free = startedOnIce ? 0 : IceEntryDistance(previous.Position, target, -181.2f);
                    var queueOrigin = previous.Position;
                    var queueFree = startedOnIce ? 0 : IceEntryDistance(queueOrigin, target, -181f);
                    var q = Landing(queueOrigin, target, queueFree + 4.5f);
                    var p = Landing(previous.Position, q, free + 5);
                    var time = previous.Time + Duration(previous.Position,p);
                    // The nominal point and constant 3m/s motion are members
                    // of the full uncertainty envelope. Reject impossible
                    // floor/body rays before allocating all swept supports.
                    if (!OnFloor(floor,previous.Position,p,.2f)) continue;
                    if (startedOnIce && previous.Position.Z <= -181.4f && p.Z >= -204.5f
                        && !obstacles.Avoids(previous.Position,p,previous.Time,previous.Time+5.0/3,0,acquisitionDelay)) continue;
                    var bound = time+MinimumRemainingTime(p.Z);
                    if(bound>=snapshot) continue;
                    if (slide >= committedSlides)
                    {
                        // Estimate the continuation after observing its live
                        // ray. A new position constrains lateral error, while
                        // seam and length uncertainty remain along the ray.
                        // This cost is never queued as executable input.
                        var incomingRay=Vector3.Normalize(previous.Position-previous.Parent!.Position);
                        var spread=MathF.Max(.12f,MathF.Max(Distance(previous.ShortLanding??previous.Position,previous.Position),
                            Distance(previous.LongLanding??previous.Position,previous.Position))+previous.Radius);
                        var a=previous.Position-incomingRay*spread; var b=previous.Position+incomingRay*spread;
                        var envelopePreview=BlundervilleSlideEnvelope.Propagate(a,b,.02f,previous.Position,q,true);
                        if(envelopePreview==null) continue;
                        if (time > horizon || !SafeSlide(floor,obstacles,previous.Position,a,b,q,
                            previous.Time-.2,previous.Time+.3,0,acquisitionDelay,.02f,envelopePreview.Radius,startedOnIce)) continue;
                        var outgoing=Vector3.Normalize(p-previous.Position);
                        var preview = new Node(p,time,0,previous,3,true,
                            p-outgoing*.12f,p+outgoing*.12f,time,time,.02f,q,true);
                        if (Exit(preview) is { } previewExit)
                        {
                            if (previewExit.Time < snapshot && Standable(previous, RefugeHold)) found.Add((null, previewExit, 0));
                            continue;
                        }
                        var previewKey = ((int)MathF.Round(p.X/.25f),(int)MathF.Round(p.Z/.25f),(int)(time/.05),0,0,0,0,0,0);
                        found.Add((previewKey, preview, bound));
                        continue;
                    }
                    // Re-aim only at a native slide seam. A 5.1m slide overshoots
                    // the nominal target, and that displaced landing changes
                    // the heading of the next slide. Propagate both recorded
                    // length extremes instead of widening every corridor.
                    var shortFrom = previous.ShortLanding ?? previous.Position;
                    var longFrom = previous.LongLanding ?? previous.Position;
                    var shortFree = startedOnIce ? 0 : IceEntryDistance(shortFrom,q,-181f);
                    var longFree = startedOnIce ? 0 : IceEntryDistance(longFrom,q,-181.4f);
                    var shortStart = previous.ShortTime ?? previous.Time; var longStart = previous.LongTime ?? previous.Time;
                    var envelope = BlundervilleSlideEnvelope.Propagate(shortFrom, longFrom, previous.Radius, previous.Position, q, startedOnIce);
                    if (envelope == null) continue;
                    p = envelope.Nominal; var shortTo = envelope.AxisA; var longTo = envelope.AxisB;
                    time = previous.Time + envelope.NominalDuration;
                    if (time + Math.Max(0, p.Z + 204.5) / 3 + Math.Max(0, Math.Min(-204.5,p.Z) + 208) / 6 > horizon) continue;
                    var shortTime = Math.Min(shortStart, longStart) + envelope.EarliestDuration;
                    var longTime = Math.Max(shortStart, longStart) + envelope.LatestDuration;
                    var tolerance = startedOnIce ? .35f : .55f;
                    if (Distance(shortFrom, q) + previous.Radius > shortFree + 4.88f + tolerance
                        || Distance(longFrom, q) + previous.Radius > longFree + 4.88f + tolerance) continue;
                    var radius = envelope.Radius;
                    if (!SafeSlide(floor, obstacles, previous.Position, shortFrom, longFrom, q,
                        Math.Min(shortStart,longStart), Math.Max(shortStart,longStart), p.Z < -208 ? .4 : 0,
                        acquisitionDelay, previous.Radius, radius, startedOnIce)) continue;
                    var n = new Node(p, time, 0, previous, 3, true, shortTo, longTo, shortTime, longTime, radius, q);
                    if (Exit(n) is { } exit) { if (exit.Time < snapshot) found.Add((null, exit, 0)); continue; }
                    // Landing displacement changes the following locked ray.
                    // Nominal position and arrival alone do not describe a
                    // complete state, even when the nominal target is equal.
                    var key = ((int)MathF.Round(p.X / .25f), (int)MathF.Round(p.Z / .25f), (int)(time / .05),
                        (int)MathF.Round((shortTo.X - p.X) / .1f), (int)MathF.Round((shortTo.Z - p.Z) / .1f),
                        (int)MathF.Round((longTo.X - p.X) / .1f), (int)MathF.Round((longTo.Z - p.Z) / .1f),
                        (int)Math.Round((shortTime - time) / .025), (int)Math.Round((longTime - time) / .025));
                    found.Add((key, n, bound));
                }
                expanded[index] = found;
            });
            foreach (var found in expanded)
                foreach (var (key, candidate, bound) in found)
                    if (key is not { } k) { if (candidate.Time < bestScore) { last = candidate; bestScore = candidate.Time; } }
                    else if (bound < bestScore && (!next.TryGetValue(k, out var old) || old.Time > candidate.Time)) next[k] = candidate;
            }
            layer = SelectLayer(next.Values, 24, slide >= committedSlides ? 16 : 32, true);
            if (endangered && slide == 0) firstLayer ??= [.. layer];
        }
        var endpointSafety = 0.0;
        if (last == null && endangered)
        {
            // Escape only: one validated move to the landing that stays clear
            // longest, preferring progress. The next fresh plan restarts there.
            Node? refuge = null; var refugeHold = 0.0;
            foreach (var n in firstLayer ?? [])
            {
                if (n.Preview || n.Parent == null) continue;
                var hold = 0.0;
                for (var h = EscapeMaximumHold; h >= EscapeMinimumHold - 1e-6; h -= .5)
                    if (CanStand(n, h)) { hold = h; break; }
                if (hold > refugeHold || hold > 0 && hold == refugeHold && n.Position.Z < refuge!.Position.Z)
                { refuge = n; refugeHold = hold; }
            }
            // 23:48 ARR / phase-40 replay: mid-bridge at -176.4 the live check
            // dropped the route, every forward landing was blocked and the
            // player stood in the next pulse. Dodge sideways or back as well.
            if (refuge == null && player.Z > entry)
            {
                var root = new Node(player, 0, 0, null, speed);
                for (var dz = -3f; dz <= 4f; dz += .5f)
                    for (var dx = -4f; dx <= 4f; dx += .5f)
                    {
                        var to = new Vector3(player.X + dx, player.Y, MathF.Min(-158.25f, player.Z + dz));
                        var distance = Distance(player, to);
                        if (distance < .5f || distance > 4.5f) continue;
                        var n = new Node(to, distance / speed, 0, root, speed);
                        if (!CanGo(player, to, 0, n.Time, 0)) continue;
                        var hold = 0.0;
                        for (var h = EscapeMaximumHold; h >= EscapeMinimumHold - 1e-6; h -= .5)
                            if (CanStand(n, h)) { hold = h; break; }
                        if (hold > refugeHold || hold > 0 && hold == refugeHold && (to.Z < refuge!.Position.Z - .01f
                            || MathF.Abs(to.Z - refuge.Position.Z) <= .01f && distance < Distance(player, refuge.Position)))
                        { refuge = n; refugeHold = hold; }
                    }
            }
            if (refuge != null) { last = refuge; endpointSafety = refugeHold - .5; }
        }
        if (last != null)
        {
            var estimatedFinish = last.Time;
            while (last.Preview) last = last.Parent!;
            var nodes = new List<Node>();
            for (var n = last; n.Parent != null; n = n.Parent) nodes.Add(n);
            nodes.Reverse(); var path = new List<Step>();
            if (nodes.Count > 0 && nodes[0].Parent is { WaitBefore: > 0 } standingRoot) path.Add(new(player, standingRoot.WaitBefore));
            foreach (var n in nodes)
            {
                if (n.WaitBefore > 0)
                {
                    if (path.Count == 0) path.Add(new(player, n.WaitBefore));
                    else path[^1] = path[^1] with { Wait = n.WaitBefore };
                }
                path.Add(new(n.NativeTarget ?? n.Position, 0, n.Speed, n.Sliding, n.ShortLanding, n.LongLanding,
                    (n.ShortTime ?? n.Time) - n.Time, (n.LongTime ?? n.Time) - n.Time, LandingRadius: n.Radius, LandingPosition: n.Position,
                    Duration: n.Sliding ? n.Time - n.Parent!.Time - n.WaitBefore : null,
                    IncomingDirection: n.Sliding ? n.Position - n.Parent!.Position : null));
            }
            for (var i = path.Count - 2; i > 0; --i)
                if (!path[i].Sliding && !path[i + 1].Sliding && path[i].Wait == 0 && Vector3.Dot(Vector3.Normalize(path[i].Position - path[i - 1].Position), Vector3.Normalize(path[i + 1].Position - path[i].Position)) > .9999f)
                    path.RemoveAt(i);
            path[^1] = path[^1] with { EndpointSafety = endpointSafety > 0 ? endpointSafety : last.Position.Z > -208 ? 0 : .4,
                RemainingDuration = Math.Max(0,estimatedFinish-last.Time) };
            return path.ToArray();
        }
        return [];

        static double MinimumRemainingTime(float z)
            => Math.Max(0, z + 181.2f) / 6 + Math.Max(0, Math.Min(z, -181.2f) + 204.5f) / 3
                + Math.Max(0, Math.Min(z, -204.5f) + 209.2f) / 6;

        bool CanStand(Node n, double hold)
        {
            var from = n.ShortTime ?? n.Time; var until = (n.LongTime ?? n.Time) + hold + acquisitionDelay;
            return obstacles.AvoidsStandingRange(n.ShortLanding ?? n.Position, n.LongLanding ?? n.Position, from, until, .1f + n.Radius)
                && obstacles.AvoidsStandingRange(n.Position, n.Position, from, until, .1f + n.Radius);
        }

        // Only the committed prefix of a preview-estimated route is queued.
        // If its continuation later fails, the player stands at that end;
        // it must be a temporary refuge, not a pulse or body lane.
        bool Standable(Node n, double hold)
        {
            while (n.Preview) n = n.Parent!;
            if (n.Parent == null) return true;
            if (!refuges.TryGetValue(n, out var value)) refuges[n] = value = CanStand(n, hold);
            return value;
        }

        double WalkingCornerDelay(Node previous, Vector3 target)
        {
            // GreenMove can pass the vertex by one frame before turning.
            // Bound that extra incoming distance plus the displaced outgoing
            // ray; straight segments cancel instead of adding a frame each.
            if (previous.Parent == null || Distance(previous.Position, target) < .01f) return 0;
            var incoming = previous.Position - previous.Parent.Position;
            if (incoming.LengthSquared() < .0001f) return 0;
            var overrun = speed / 30;
            var displaced = previous.Position + Vector3.Normalize(incoming) * overrun;
            return Math.Max(0, overrun + Distance(displaced, target) - Distance(previous.Position, target)) / speed;
        }
    }

    private static List<Node> SelectLayer(IEnumerable<Node> candidates, int perTime, int perLane, bool sliding)
    {
        double Score(Node n) => (n.LongTime ?? n.Time) + (sliding ? Math.Max(0, n.Position.Z + 208) / 3 : 0);
        return candidates.GroupBy(n => n.Position.X < -200 ? 0 : 1).SelectMany(lane => lane
            .GroupBy(n => (int)(n.Time / .3)).SelectMany(time => time
                // Spend the first slots on distinct positions before keeping
                // alternate histories at the same nominal position.
                .GroupBy(n => (int)MathF.Round(n.Position.X * 2))
                .SelectMany(position => position.GroupBy(n => (int)MathF.Round(n.Position.Z * 2))
                    .SelectMany(depth => new[] { depth.MinBy(Score)!, depth.MinBy(n =>
                        Distance(n.ShortLanding ?? n.Position, n.Position) + Distance(n.LongLanding ?? n.Position, n.Position))!,
                        depth.MaxBy(n => n.Time)! }.Distinct()
                        .Select((n, rank) => (Node: n, Rank: rank)))
                    .OrderBy(n => n.Rank).ThenBy(n => Score(n.Node)).Take(6).Select((n, rank) => (n.Node, Rank: rank)))
                .OrderBy(n => n.Rank).ThenBy(n => Score(n.Node)).Take(perTime)
                .Select((n, rank) => (n.Node, Rank: rank)))
            // A later entry can be the only safe window. Keep each time band
            // represented instead of filling the lane with its earliest band.
            .OrderBy(n => n.Rank).ThenBy(n => Score(n.Node)).Take(perLane).Select(n => n.Node)).ToList();
    }

    private static double Duration(Vector3 from, Vector3 to) => BlundervilleIceTiming.Instance.Duration(from, to, 3);

    private static double TravelTime(Vector3 from, Vector3 to, Step step)
        => step.Sliding ? Duration(from, to) : Distance(from, to) / step.Speed;

    private static float IceEntryDistance(Vector3 from, Vector3 target, float entry)
    {
        if (from.Z <= entry) return 0;
        var direction = Vector3.Normalize(target-from);
        return direction.Z < -.001f ? (entry-from.Z)/direction.Z : float.PositiveInfinity;
    }

    // ARR 2026-10-12 00:17: on the wide ice platform in front of the two lanes
    // the planner slid the full width east, west and east again for 16s. Each
    // reversal dropped about 0.7m and passed the 0.5m same-row rule. On that
    // platform a mostly lateral slide may only reverse while clearly advancing.
    internal const float IcePlatformSouthEdge = -185.5f;
    internal const float PlatformReversalAdvance = 2f;
    internal static bool PlatformReversal(Vector3 from, Vector3 incoming, Vector3 target)
    {
        if (from.Z <= IcePlatformSouthEdge || from.Z > -180.5f) return false;
        var length = MathF.Sqrt(incoming.X * incoming.X + incoming.Z * incoming.Z);
        return length > .01f && MathF.Abs(incoming.Z) < .4f * length
            && target.Z >= from.Z - PlatformReversalAdvance;
    }

    private static IEnumerable<Vector3> SlideTargets(Vector3 from,float spacing=.25f)
    {
        yield return from - Vector3.UnitZ * 4.9f;
        foreach (var side in new[] { -1, 1 })
            yield return from + Vector3.UnitX * (side * 4.9f);
        // Sample directions toward absolute lateral seams so a uniform angle
        // grid cannot skip a narrow lane edge. Search separately chooses the
        // native target and checks the full 4.88–5.1m physical slide envelope.
        var reach = MathF.Sqrt(4.9f * 4.9f - .5f * .5f);
        for (var x = MathF.Ceiling((from.X - reach) / spacing) * spacing; x <= from.X + reach; x += spacing)
            if (MathF.Abs(x - from.X) > .001f)
                yield return new(x, from.Y, from.Z - MathF.Sqrt(4.9f * 4.9f - (x - from.X) * (x - from.X)));
    }

    private static bool SafeSlide(IGeometryShape floor, ObstacleIndex hazards, Vector3 nominal,
        Vector3 startA, Vector3 startB, Vector3 target, double earliest, double latest, double settle,
        double acquisition, float incomingRadius, float landingRadius, bool onIce)
    {
        var sweep = BlundervilleSlideEnvelope.PrepareSweep(startA,startB,incomingRadius,nominal,target,onIce);
        if (sweep == null) return false;
        var radius = MathF.Max(landingRadius,sweep.SweepRadius);
        foreach (var support in sweep.Supports)
            if (!OnFloor(floor,support.From,support.To,.2f + radius)
                || !hazards.AvoidsSlide(support.From,support.To,earliest-sweep.TimeUncertainty,settle,
                    acquisition + Math.Max(0,latest-earliest) + sweep.TimeUncertainty*2,.1f,radius,support.Clock,
                    support.EarliestLanding)) return false;
        return true;
    }

    private static bool Safe(IGeometryShape floor, ObstacleIndex hazards, Vector3 from, Vector3 to, double start, double end, double settle, double delay,
        bool sliding = false, float speed = 6, float radius = 0)
    {
        var overrun = !sliding && Distance(from, to) > .01f ? speed / 30 : 0;
        var physicalTo = overrun > 0 ? to + Vector3.Normalize(to - from) * overrun : to;
        if (!OnFloor(floor, from, physicalTo, (sliding ? .2f : .1f) + radius)) return false;
        if (sliding) return Distance(from,to) < .001f
            ? hazards.Avoids(from,to,start,end,settle,delay,.1f + radius)
            : hazards.AvoidsSlide(from, to, start, settle, delay, .1f, radius);
        // Live check of a cylinder-bridge walk: the phase-40 replay planned a
        // pass .1m from a parked cylinder; 5mm of drift failed this check
        // mid-bridge, dropped the route and the player stood in the next
        // pulse. Planning keeps .1m (more changes the ice entries).
        var clearance = settle <= 0 && to.Z > -180.76f ? .05f : .1f;
        return hazards.Avoids(from, physicalTo, start, end + (overrun > 0 ? 1.0 / 30 : 0), settle, delay, clearance + radius);
    }

    private static bool AnimationEnvelope(BlundervilleHazard hazard)
        => hazard.Id.StartsWith("bumper/", StringComparison.Ordinal)
            || hazard.Id.StartsWith("rhythm/", StringComparison.Ordinal) && hazard.Id.Contains("/34717/", StringComparison.Ordinal);

    private static bool OnFloor(IGeometryShape floor, Vector3 from, Vector3 to, float clearance = .2f)
        => ArenaBoundaryTraversal.ContainsTube(floor,from,to,clearance);

    private readonly record struct Obstacle(IGeometryShape Geometry, GeometryBounds Bounds, double From, double Until, bool Motion);
    private sealed class ObstacleIndex
    {
        private readonly Dictionary<int, List<Obstacle>> _rows = [];
        private readonly ConcurrentDictionary<(Vector3 From, Vector3 To), BlundervilleIceTiming.IceTraversalClock> _slides = [];
        public ObstacleIndex(BlundervilleHazard[] hazards, DateTime now, double earlyPadding)
        {
            foreach (var hazard in hazards)
            {
                var envelope = AnimationEnvelope(hazard);
                var obstacle = new Obstacle(hazard.Geometry, hazard.Geometry.GetBounds(),
                    (hazard.From - now).TotalSeconds - (envelope ? .05 : earlyPadding),
                    (hazard.Until - now).TotalSeconds + (envelope ? .05 : .35),
                    hazard.Id.StartsWith("bumper/", StringComparison.Ordinal));
                // Expand by the largest caller clearance before assigning rows.
                // A ray near a row boundary must still see its neighboring shape.
                for (var row = (int)MathF.Floor((obstacle.Bounds.MinZ - .4f) / 4);
                    row <= (int)MathF.Floor((obstacle.Bounds.MaxZ + .4f) / 4); ++row)
                {
                    if (!_rows.TryGetValue(row, out var band)) _rows[row] = band = [];
                    band.Add(obstacle);
                }
            }
        }
        public bool Avoids(Vector3 from, Vector3 to, double start, double end, double settle, double delay, float clearance = .1f)
        {
            for (var row = (int)MathF.Floor(MathF.Min(from.Z, to.Z) / 4); row <= (int)MathF.Floor(MathF.Max(from.Z, to.Z) / 4); ++row)
                if (_rows.TryGetValue(row, out var band)
                    && !BlundervilleBumperPlanner.Avoids(band, from, to, start, end, settle, delay, clearance)) return false;
            return true;
        }
        public bool AvoidsSlide(Vector3 from, Vector3 to, double start, double settle, double delay, float clearance, float radius = 0, IDirectTraversalClock? suppliedClock = null, Vector3? earliestLanding = null)
        {
            // Uniform 3m/s is one member of the physical envelope. Reject it
            // cheaply before constructing the nonlinear occupancy slices.
            if (suppliedClock == null && from.Z <= -181.4f && from.Z >= -204.5f && to.Z <= -181.4f && to.Z >= -204.5f
                && !Avoids(from, to, start, start + Distance(from, to) / 3, settle, delay, clearance)) return false;
            IDirectTraversalClock clock;
            if (suppliedClock != null) clock = suppliedClock;
            else
            {
                if (!_slides.TryGetValue((from,to),out var prepared))
                    _slides[(from,to)] = prepared = BlundervilleIceTiming.Instance.Prepare(from,to);
                clock = prepared;
            }
            var reach = clearance + radius;
            var minX = MathF.Min(from.X, to.X) - reach; var maxX = MathF.Max(from.X, to.X) + reach;
            var minZ = MathF.Min(from.Z, to.Z) - reach; var maxZ = MathF.Max(from.Z, to.Z) + reach;
            for (var row = (int)MathF.Floor(minZ / 4); row <= (int)MathF.Floor(maxZ / 4); ++row)
                if (_rows.TryGetValue(row, out var band))
                    foreach (var h in band)
                    {
                        if (h.Bounds.MinX > maxX || h.Bounds.MaxX < minX || h.Bounds.MinZ > maxZ || h.Bounds.MaxZ < minZ
                            || h.Until < start - delay || h.From > start + clock.LatestDuration + 1.0 / 30 + delay + settle) continue;
                        var occupancy = clock.Occupancy(h.From - start - delay, h.Until - start);
                        if (occupancy is not { } occupied) continue;
                        var a = occupied.From; var b = occupied.To;
                        var margin = h.Motion ? .1f + radius : clearance + radius;
                        if (h.Geometry.Contains(a, margin) || h.Geometry.Contains(b, margin) || h.Geometry.IntersectsSegment(a, b, margin)) return false;
                    }
            return settle <= 0 || AvoidsStandingRange(earliestLanding ?? to, to,
                start + clock.EarliestDuration, start + clock.LatestDuration + 1.0 / 30 + settle + delay, clearance + radius);
        }
        public bool AvoidsStandingRange(Vector3 from, Vector3 to, double earliest, double latest, float clearance)
        {
            // Every endpoint in this interval can be occupied throughout the
            // arrival/settle window. It is not a timed walk from one end to the other.
            for (var row = (int)MathF.Floor((MathF.Min(from.Z,to.Z)-clearance)/4);
                row <= (int)MathF.Floor((MathF.Max(from.Z,to.Z)+clearance)/4); ++row)
                if (_rows.TryGetValue(row,out var band))
                    foreach (var h in band)
                        if (h.From <= latest && h.Until >= earliest
                            && (h.Geometry.Contains(from,clearance) || h.Geometry.Contains(to,clearance)
                                || h.Geometry.IntersectsSegment(from,to,clearance))) return false;
            return true;
        }
    }
    private static bool Avoids(List<Obstacle> hazards, Vector3 from, Vector3 to, double start, double end, double settle, double delay, float clearance = .1f)
    {
        var minX = MathF.Min(from.X, to.X) - clearance; var maxX = MathF.Max(from.X, to.X) + clearance;
        var minZ = MathF.Min(from.Z, to.Z) - clearance; var maxZ = MathF.Max(from.Z, to.Z) + clearance;
        foreach (var h in hazards)
        {
            if (h.Bounds.MinX > maxX || h.Bounds.MaxX < minX || h.Bounds.MinZ > maxZ || h.Bounds.MaxZ < minZ) continue;
            var activeFrom = h.From;
            var activeUntil = h.Until;
            if (activeUntil < start || activeFrom > end + delay + settle) continue;
            var duration = Math.Max(.001, end - start);
            var a = Vector3.Lerp(from, to, (float)Math.Clamp((activeFrom - delay - start) / duration, 0, 1));
            var b = Vector3.Lerp(from, to, (float)Math.Clamp((activeUntil - start) / duration, 0, 1));
            var margin = h.Motion ? MathF.Max(.1f, clearance) : clearance;
            if (h.Geometry.Contains(a, margin) || h.Geometry.Contains(b, margin) || h.Geometry.IntersectsSegment(a, b, margin)) return false;
        }
        return true;
    }
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static Vector3 Landing(Vector3 from, Vector3 target, float length)
        => from + Vector3.Normalize(target - from) * length;
    private static bool Passed(Vector3 player, Vector3 from, Vector3 target)
        => Vector3.Dot(player - target, target - from) > 0
            && GeometryHelper.DistanceSquaredPointToSegmentXZ(player, from, target) <= .8f * .8f;
}

using System.Numerics;
using MasterPlayer.Spatial;

namespace MasterPlayer.Mechanics.Blunderville;

internal static class BlundervilleSlideEnvelope
{
    internal sealed record Result(Vector3 Nominal, Vector3 AxisA, Vector3 AxisB, float Radius,
        double EarliestDuration, double LatestDuration, double NominalDuration);
    internal sealed record SweepSupport(Vector3 From, Vector3 To, IDirectTraversalClock Clock, Vector3 EarliestLanding);
    internal sealed record SweptSupport(IReadOnlyList<SweepSupport> Supports, float SweepRadius, double TimeUncertainty, float Growth);
    private const float Short = 4.88f, Long = 5.1f, EarlyEntry = -181, LateEntry = -181.4f, Exit = -204.5f;
    private const float SweepEpsilon = .05f;
    private const double Frame = 1.0 / 30;
    private static readonly float[] Entries = [EarlyEntry, LateEntry];
    private static readonly float[] Lengths = [Short, Long];
    private static readonly Lazy<(BlundervilleIceTiming.IceTraversalClock Short, BlundervilleIceTiming.IceTraversalClock Long)> FullCurves = new(() =>
        (BlundervilleIceTiming.Instance.PrepareFullSlide(new(0, 0, -190), new(Short, 0, -190),Short),
            BlundervilleIceTiming.Instance.PrepareFullSlide(new(0, 0, -190), new(Long, 0, -190),Long)));
    private static readonly Lazy<(double Earliest, double Latest)> FullClock = new(() =>
    {
        var (a, b) = FullCurves.Value;
        return (Math.Min(a.EarliestDuration, b.EarliestDuration), Math.Max(a.LatestDuration, b.LatestDuration));
    });

    public static SweptSupport? PrepareSweep(Vector3 startA, Vector3 startB, float radius, Vector3 nominal,
        Vector3 queuedTarget, bool alreadyOnIce, float maxEntryOvershoot = .2f)
    {
        if (!Finite(startA) || !Finite(startB) || !Finite(nominal) || !Finite(queuedTarget)
            || !float.IsFinite(radius) || radius < 0 || !float.IsFinite(maxEntryOvershoot) || maxEntryOvershoot < 0)
            return null;
        var span = Distance(startA, startB);
        var minimum = DistanceToSegment(queuedTarget, startA, startB) - radius;
        if (minimum <= .3f) return null;
        // F_d(s)=s+d*normalize(q-s) has eigenvalues 1 and 1-d/|q-s|.
        // Every intermediate pure-ice displacement has gain at most this
        // maximum; a radial starting segment therefore keeps gain exactly 1.
        var growth = MathF.Max(1, Long / minimum - 1);
        var freeGradient = 0f;
        if (!alreadyOnIce)
        {
            if (queuedTarget.Z >= LateEntry - .05f) return null;
            var dzA = queuedTarget.Z - startA.Z; var dzB = queuedTarget.Z - startB.Z;
            if (dzA >= -radius - .05f || dzB >= -radius - .05f) return null;
            var minimumZ = MathF.Min(MathF.Abs(dzA), MathF.Abs(dzB)) - radius;
            var maximum = MathF.Max(Distance(startA, queuedTarget), Distance(startB, queuedTarget)) + radius;
            foreach (var entry in Entries)
            {
                var k = MathF.Abs(queuedTarget.Z - entry);
                var entryGrowth = k * maximum / (minimumZ * minimumZ);
                growth = MathF.Max(growth, MathF.Max(1, entryGrowth) + (Long + maxEntryOvershoot) / minimum);
                freeGradient = MathF.Max(freeGradient, entryGrowth
                    + MathF.Max(MathF.Abs(entry - startA.Z), MathF.Abs(entry - startB.Z)) / minimumZ);
            }
        }
        var required = MathF.Ceiling(growth * span / (2 * SweepEpsilon));
        // An unresolved ray needs a new anchor, rather than silently capping a
        // mesh and losing its coverage guarantee.
        if (!float.IsFinite(growth) || !float.IsFinite(freeGradient) || !float.IsFinite(required) || required > 256) return null;
        var intervals = Math.Max(1, (int)required);
        var starts = new List<Vector3>(intervals + 4);
        for (var i = 0; i <= intervals; ++i) AddStart(Vector3.Lerp(startA, startB, (float)i / intervals));
        if (!alreadyOnIce)
            foreach (var entry in Entries)
                if ((startA.Z - entry) * (startB.Z - entry) < 0)
                    AddStart(Vector3.Lerp(startA, startB, (entry - startA.Z) / (startB.Z - startA.Z)));
        AddStart(nominal);
        var startError = radius + span / (2 * intervals);
        var sweepRadius = growth * startError;
        var timeUncertainty = alreadyOnIce ? 0 : freeGradient * startError / 6;
        var supports = new SweepSupport[starts.Count];
        for (var i = 0; i < starts.Count; ++i)
        {
            var from = starts[i]; var heading = queuedTarget - from; heading.Y = 0;
            if (heading.LengthSquared() <= .000001f) return null;
            heading = Vector3.Normalize(heading);
            var clocks = new List<IDirectTraversalClock>(alreadyOnIce ? 4 : 8);
            var minimumLength = float.PositiveInfinity;
            var maximumLength = 0f;
            foreach (var length in Lengths)
            {
                var pureCurve = length == Short ? FullCurves.Value.Short : FullCurves.Value.Long;
                if (alreadyOnIce)
                {
                    var to = from + heading * length;
                    // Only the part beyond the surface boundary can accelerate
                    // to dry speed. A late crossing does not turn the entire
                    // five-metre slide into an ordinary walk.
                    clocks.Add(BlundervilleIceTiming.Instance.PrepareSlideExit(from,to,length,sweepRadius));
                    maximumLength = MathF.Max(maximumLength, length);
                    minimumLength = MathF.Min(minimumLength, length);
                    continue;
                }
                foreach (var entry in Entries)
                {
                    var free = FreeDistance(from, queuedTarget, entry);
                    if (!float.IsFinite(free)) return null;
                    var canOverrun = free > 0 || from.Z + radius + span / (2 * intervals) >= entry;
                    for (var delay = 0; delay < (canOverrun && maxEntryOvershoot > 0 ? 2 : 1); ++delay)
                    {
                        var walking = free + (delay == 0 ? 0 : maxEntryOvershoot);
                        var slideStart = from + heading * walking; var to = slideStart + heading * length;
                        clocks.Add(new EntryClock(from, heading, walking, pureCurve.Rebase(slideStart, to, length)));
                        if (MathF.Min(slideStart.Z, to.Z) - sweepRadius <= Exit)
                            clocks.Add(new WalkingClock(from, heading, walking + length));
                        maximumLength = MathF.Max(maximumLength, walking + length);
                        minimumLength = MathF.Min(minimumLength, walking + length);
                    }
                }
            }
            var end = from + heading * maximumLength;
            supports[i] = new(from, end, new UnionClock(from, heading, clocks.ToArray()), from + heading * minimumLength);
        }
        return new(supports, sweepRadius, timeUncertainty, growth);

        void AddStart(Vector3 point) { if (!starts.Contains(point)) starts.Add(point); }
    }

    public static Result? Propagate(Vector3 startA, Vector3 startB, float radius, Vector3 nominal,
        Vector3 queuedTarget, bool alreadyOnIce, float maxEntryOvershoot = .2f)
    {
        if (!Finite(startA) || !Finite(startB) || !Finite(nominal) || !Finite(queuedTarget)
            || !float.IsFinite(radius) || radius < 0 || !float.IsFinite(maxEntryOvershoot) || maxEntryOvershoot < 0)
            return null;
        var span = Distance(startA, startB);
        var minimum = DistanceToSegment(queuedTarget, startA, startB) - radius;
        if (minimum <= .3f) return null;
        var maximum = MathF.Max(Distance(startA, queuedTarget), Distance(startB, queuedTarget)) + radius;
        var sin = span > .000001f ? MathF.Min(1, MathF.Abs(Cross(XZ(queuedTarget - startA), XZ(startB - startA))) / (span * minimum)) : 0;
        var growth = MathF.Max(1, Long / minimum - 1);
        var curvature = 3 * Long * span * span * sin / (8 * minimum * minimum);
        var freeUncertainty = 0f;
        Span<Vector3> starts = stackalloc Vector3[4]; var count = 2;
        starts[0] = startA; starts[1] = startB;
        if (!alreadyOnIce)
        {
            if (queuedTarget.Z >= LateEntry - .05f) return null;
            // The entry is observed after ordinary movement has crossed the
            // trigger. A 30 FPS frame can carry the player another .2m along
            // the same ray before the locked slide begins.
            var longestEntryRay = Long + maxEntryOvershoot;
            // Kinks where some starts have entered ice are explicit support
            // points; each side is smooth and has its own curvature bound.
            foreach (var entry in Entries)
            {
                if ((startA.Z - entry) * (startB.Z - entry) < 0)
                    starts[count++] = Vector3.Lerp(startA, startB, (entry - startA.Z) / (startB.Z - startA.Z));
                var dz = queuedTarget.Z - startA.Z; var otherDz = queuedTarget.Z - startB.Z;
                if (dz >= -radius - .05f || otherDz >= -radius - .05f) return null;
                var minimumZ = MathF.Min(MathF.Abs(dz), MathF.Abs(otherDz)) - radius;
                var k = MathF.Abs(queuedTarget.Z - entry);
                var entryGrowth = k * maximum / (minimumZ * minimumZ);
                growth = MathF.Max(growth, MathF.Max(1, entryGrowth) + longestEntryRay / minimum);
                var freeGradient = k * maximum / (minimumZ * minimumZ)
                    + MathF.Max(MathF.Abs(entry - startA.Z), MathF.Abs(entry - startB.Z)) / minimumZ;
                // Every start is within span/2 of an endpoint plus the
                // capsule radius. This also bounds interior time extrema.
                freeUncertainty = MathF.Max(freeUncertainty, freeGradient * (radius + span / 2));
                if (span > .000001f)
                {
                    var direction = XZ(startB - startA) / span;
                    var x = MathF.Max(MathF.Abs(startA.X - queuedTarget.X), MathF.Abs(startB.X - queuedTarget.X)) + radius;
                    var second = 2 * k * (MathF.Abs(direction.X * direction.Y) / (minimumZ * minimumZ)
                        + x * direction.Y * direction.Y / (minimumZ * minimumZ * minimumZ));
                    curvature = MathF.Max(curvature, second * span * span / 8
                        + 3 * longestEntryRay * span * span * sin / (8 * minimum * minimum));
                }
            }
        }
        Span<Vector3> corners = stackalloc Vector3[32]; var cornerCount = 0;
        var earliest = double.PositiveInfinity; var latest = double.NegativeInfinity;
        for (var i = 0; i < count; ++i)
        {
            var entryCount = alreadyOnIce ? 1 : 2;
            for (var entryIndex = 0; entryIndex < entryCount; ++entryIndex)
            {
                var entry = entryIndex == 0 ? EarlyEntry : LateEntry;
                var free = alreadyOnIce ? 0 : FreeDistance(starts[i], queuedTarget, entry);
                var direction = queuedTarget - starts[i]; direction.Y = 0; direction = Vector3.Normalize(direction);
                // At a kink, include the dry-side limit even when the support
                // itself lies on the trigger. A capsule touching that plane
                // can contain starts with an ordinary-movement overrun.
                var overrunCount = !alreadyOnIce && maxEntryOvershoot > 0
                    && (free > 0 || starts[i].Z + radius >= entry) ? 2 : 1;
                for (var lengthIndex = 0; lengthIndex < 2; ++lengthIndex)
                for (var overrunIndex = 0; overrunIndex < overrunCount; ++overrunIndex)
                {
                    var length = lengthIndex == 0 ? Short : Long;
                    var overrun = overrunIndex == 0 ? 0 : maxEntryOvershoot;
                    var end = starts[i] + direction * (free + overrun + length); corners[cornerCount++] = end;
                    if (!alreadyOnIce)
                    {
                        earliest = Math.Min(earliest, (free + overrun) / 6 + FullClock.Value.Earliest);
                        latest = Math.Max(latest, (free + overrun) / 6 + FullClock.Value.Latest);
                    }
                    else
                    {
                        if (starts[i].Z <= LateEntry && starts[i].Z >= Exit && end.Z <= LateEntry && end.Z >= Exit)
                        {
                            earliest = Math.Min(earliest, FullClock.Value.Earliest); latest = Math.Max(latest, FullClock.Value.Latest);
                        }
                        else
                        {
                            var clock = BlundervilleIceTiming.Instance.Prepare(starts[i], end);
                            earliest = Math.Min(earliest, clock.EarliestDuration); latest = Math.Max(latest, clock.LatestDuration);
                        }
                    }
                }
            }
        }
        var nominalFree = alreadyOnIce ? 0 : FreeDistance(nominal, queuedTarget, -181.2f);
        var nominalRay = queuedTarget - nominal; nominalRay.Y = 0;
        if (nominalRay.LengthSquared() <= .000001f || !float.IsFinite(nominalFree)) return null;
        var nominalOverrun = !alreadyOnIce && nominalFree > 0 ? maxEntryOvershoot / 2 : 0;
        var nominalDirection = Vector3.Normalize(nominalRay);
        var nominalSlideStart = nominal + nominalDirection * (nominalFree + nominalOverrun);
        var nominalEnd = nominalSlideStart + nominalDirection * 5;
        var nominalDuration = alreadyOnIce
            ? BlundervilleIceTiming.Instance.Duration(nominal, nominalEnd, 3)
            : (nominalFree + nominalOverrun) / 6 + BlundervilleIceTiming.Instance.Duration(nominalSlideStart, nominalEnd, 3);
        var first = 0; var last = 1; var furthest = 0f;
        for (var i = 0; i < cornerCount; ++i)
            for (var j = i + 1; j < cornerCount; ++j)
            {
                var distance = Vector2.DistanceSquared(XZ(corners[i]), XZ(corners[j]));
                if (distance > furthest) { furthest = distance; first = i; last = j; }
            }
        var a = corners[first]; var b = corners[last];
        var residual = DistanceToSegment(nominalEnd, a, b);
        for (var i = 0; i < cornerCount; ++i) residual = MathF.Max(residual, DistanceToSegment(corners[i], a, b));
        var resultRadius = radius * growth + curvature + residual;
        if (!alreadyOnIce) { earliest = Math.Max(0, earliest - freeUncertainty / 6); latest += freeUncertainty / 6; }
        if (alreadyOnIce && MathF.Min(a.Z,b.Z)-resultRadius < Exit)
        {
            var sweep=PrepareSweep(startA,startB,radius,nominal,queuedTarget,true);
            if(sweep==null)return null;
            earliest=sweep.Supports.Min(s=>s.Clock.EarliestDuration);
            latest=sweep.Supports.Max(s=>s.Clock.LatestDuration);
        }
        if (alreadyOnIce && MathF.Max(startA.Z, startB.Z) + radius > EarlyEntry)
        {
            // A tube grazing a surface boundary can switch an entire nearly
            // horizontal slide. Its time range includes both whole surfaces.
            earliest = Math.Min(earliest, Short / 6.0); latest = Math.Max(latest, FullClock.Value.Latest);
        }
        return new(nominalEnd, a, b, resultRadius, earliest, latest, nominalDuration);
    }

    private sealed class WalkingClock(Vector3 from, Vector3 heading, float length) : IDirectTraversalClock
    {
        public double EarliestDuration => length / 6.0;
        public double LatestDuration => EarliestDuration;
        public (Vector3 From, Vector3 To)? Occupancy(double fromSeconds, double untilSeconds)
        {
            if (double.IsNaN(fromSeconds) || double.IsNaN(untilSeconds) || fromSeconds > untilSeconds
                || untilSeconds < -Frame || fromSeconds > LatestDuration + Frame) return null;
            var first = (float)Math.Clamp((fromSeconds - Frame) * 6, 0, length);
            var last = (float)Math.Clamp((untilSeconds + Frame) * 6, 0, length);
            return (from + heading * first, from + heading * last);
        }
    }

    private sealed class EntryClock(Vector3 from, Vector3 heading, float walking, IDirectTraversalClock ice) : IDirectTraversalClock
    {
        private readonly double _dryTime = walking / 6.0;
        public double EarliestDuration => _dryTime + ice.EarliestDuration;
        public double LatestDuration => _dryTime + ice.LatestDuration;
        public (Vector3 From, Vector3 To)? Occupancy(double fromSeconds, double untilSeconds)
        {
            if (double.IsNaN(fromSeconds) || double.IsNaN(untilSeconds) || fromSeconds > untilSeconds
                || untilSeconds < -Frame || fromSeconds > LatestDuration + Frame) return null;
            var first = float.PositiveInfinity; var last = float.NegativeInfinity;
            if (untilSeconds >= -Frame && fromSeconds <= _dryTime + Frame)
            {
                first = (float)Math.Clamp((fromSeconds - Frame) * 6, 0, walking);
                last = (float)Math.Clamp((untilSeconds + Frame) * 6, 0, walking);
            }
            // The cached ice clock already supplies its own one-frame bound.
            // Only translate its time origin; do not add another ice frame.
            if (ice.Occupancy(fromSeconds - _dryTime, untilSeconds - _dryTime) is { } occupied)
            {
                first = MathF.Min(first, Vector3.Dot(occupied.From - from, heading));
                last = MathF.Max(last, Vector3.Dot(occupied.To - from, heading));
            }
            return first <= last ? (from + heading * first, from + heading * last) : null;
        }
    }

    private sealed class UnionClock(Vector3 from, Vector3 heading, IDirectTraversalClock[] clocks) : IDirectTraversalClock
    {
        public double EarliestDuration { get; } = clocks.Min(clock => clock.EarliestDuration);
        public double LatestDuration { get; } = clocks.Max(clock => clock.LatestDuration);
        public (Vector3 From, Vector3 To)? Occupancy(double fromSeconds, double untilSeconds)
        {
            if (double.IsNaN(fromSeconds) || double.IsNaN(untilSeconds) || fromSeconds > untilSeconds
                || untilSeconds < -Frame || fromSeconds > LatestDuration + Frame) return null;
            var first = float.PositiveInfinity; var last = float.NegativeInfinity;
            foreach (var clock in clocks)
                if (clock.Occupancy(Math.Min(fromSeconds,clock.LatestDuration + Frame),
                    Math.Min(untilSeconds,clock.LatestDuration + Frame)) is { } occupied)
                {
                    first = MathF.Min(first, Vector3.Dot(occupied.From - from, heading));
                    last = MathF.Max(last, Vector3.Dot(occupied.To - from, heading));
                }
            return first <= last ? (from + heading * first, from + heading * last) : null;
        }
    }

    private static float FreeDistance(Vector3 from, Vector3 target, float entry)
    {
        if (from.Z <= entry) return 0;
        var delta = target - from; delta.Y = 0;
        return delta.Z < -.000001f ? (entry - from.Z) * delta.Length() / delta.Z : float.PositiveInfinity;
    }
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(XZ(a), XZ(b));
    private static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
    {
        var a = XZ(from); var delta = XZ(to) - a; var length = delta.LengthSquared();
        return Vector2.Distance(XZ(point), a + delta * (length > .000001f ? Math.Clamp(Vector2.Dot(XZ(point) - a, delta) / length, 0, 1) : 0));
    }
    private static Vector2 XZ(Vector3 p) => new(p.X, p.Z);
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}

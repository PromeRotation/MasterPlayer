using System.Numerics;
using MasterPlayer.Spatial;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleIceTiming : IDirectTraversalTiming
{
    public static readonly BlundervilleIceTiming Instance = new();
    private const float Exit = -204.5f;
    private const double Frame = 1.0 / 30;
    private readonly record struct Knot(double Fraction, double Earliest, double Latest);
    // Seconds per metre, including both recorded acceleration curves and
    // constant 2.88-3m/s. The recorded spatial breakpoints make interpolation
    // bound each curve between server samples, including its stationary tail.
    private static readonly Knot[] Envelope =
    [
        new(0, 0, .00943239),
        new(.02590900, .00863633, .03644223),
        new(.06739217, .02246406, .06069541),
        new(.15256830, .05085610, .08764268),
        new(.22683401, .07561134, .11113821),
        new(.35801980, .11933993, .14280135),
        new(.45366802, .15122267, .16588711),
        new(.57664667, .18925797, .20022454),
        new(.67229652, .21226356, .23343629),
        new(.78592164, .23959254, .27288946),
        new(.85717698, .26228795, .29763090),
        new(.94332176, .28972577, .32754228),
        new(.98045160, .31701047, .34043458),
        new(1, 1.0 / 3, .34776828),
    ];
    private static readonly Lazy<IceTraversalClock> FullIce = new(() => PrepareCore(new(0, 0, -183), new(5.25f, 0, -183)));
    private static readonly Lazy<IceTraversalClock> ShortSlide = new(() => SlideClock(4.88f));
    private static readonly Lazy<IceTraversalClock> LongSlide = new(() => SlideClock(5.1f));

    // A whole physical slide has a known length. Its endpoint must not use a
    // prefix of the longer profile, which belongs only to residual-ray checks.
    internal IceTraversalClock PrepareFullSlide(Vector3 from, Vector3 to, float length)
        => (length == 4.88f ? ShortSlide.Value : length == 5.1f ? LongSlide.Value : SlideClock(length)).Rebase(from,to,length);

    internal IceTraversalClock PrepareSlideExit(Vector3 from,Vector3 to,float length,float clearance)
    {
        var dz=((double)to.Z-from.Z)/length;
        double first=length,last=length;
        if(dz < -.000001)
        {
            first=Math.Clamp((from.Z-Exit-clearance)/-dz,0,length);
            last=Math.Clamp((from.Z-Exit+clearance)/-dz,0,length);
        }
        else if(from.Z-clearance<=Exit)
        {
            first=0;
            last=dz<=0 && from.Z+clearance<=Exit ? 0 : length;
        }
        if(first>=length) return PrepareFullSlide(from,to,length);
        var cuts=new SortedSet<double>(Envelope.Select(k=>k.Fraction*length)){first,last,length};
        var distances=cuts.ToArray();
        double Time(double distance,double crossing,bool late)
        {
            var ice=Math.Min(distance,crossing);
            var bounds=ice<=0 ? (Earliest:0.0,Latest:0.0) : Curve(ice,length);
            return (late ? bounds.Latest : bounds.Earliest)+Math.Max(0,distance-crossing)/6;
        }
        return new(from,to,distances,distances.Select(d=>Time(d,first,false)).ToArray(),
            distances.Select(d=>Time(d,last,true)).ToArray());
    }

    private static IceTraversalClock SlideClock(float length)
        => new(Vector3.Zero,Vector3.UnitX*length,Envelope.Select(k=>k.Fraction*length).ToArray(),
            Envelope.Select(k=>k.Earliest*length).ToArray(),Envelope.Select(k=>k.Latest*length).ToArray());

    private BlundervilleIceTiming() { }

    public double Duration(Vector3 from, Vector3 to, float speed)
    {
        var length = Distance(from, to);
        if (speed > 3.1f) return length / speed;
        var clock = Clock(from, to, -181.2f);
        return (length - clock.IceLength) / 6 + clock.IceLength / Math.Max(.1f, speed);
    }

    public IReadOnlyList<DirectTraversalSlice> Sweep(Vector3 from, Vector3 to, float speed)
    {
        var length = Distance(from, to);
        if (length <= .00001f) return [new(from, to, 0, 0)];
        var count = Math.Max(1, (int)Math.Ceiling(length / .25));
        var result = new DirectTraversalSlice[count];
        var earlyEntry = Clock(from, to, -181f);
        var delayedEntry = DelayEntry(Clock(from, to, -181.4f));
        var previous = from; var earliest = 0.0;
        var delta = to - from;
        for (var i = 0; i < count; ++i)
        {
            var distance = (double)length * (i + 1) / count;
            (double Earliest, double Latest) early = speed > 3.1f ? (distance / speed, distance / speed) : Bounds(earlyEntry, distance);
            var delayed = speed > 3.1f ? early : Bounds(delayedEntry, distance);
            var next = i + 1 == count ? to : from + delta * ((float)(i + 1) / count);
            result[i] = new(previous, next, Math.Max(0, earliest - Frame), early.Latest + Frame);
            previous = next; earliest = delayed.Earliest;
        }
        return result;
    }

    public (Vector3 From, Vector3 To)? Occupancy(Vector3 from, Vector3 to, double fromSeconds, double untilSeconds)
        => Prepare(from, to).Occupancy(fromSeconds, untilSeconds);

    IDirectTraversalClock IDirectTraversalTiming.Prepare(Vector3 from, Vector3 to, float speed)
        => speed > 3.1f ? new DirectTraversalSliceClock(Sweep(from, to, speed)) : Prepare(from, to);

    public IceTraversalClock Prepare(Vector3 from, Vector3 to)
    {
        var length = Distance(from, to);
        if (length > .00001f && length <= 5.2f
            && from.Z <= -181.4f && from.Z >= Exit && to.Z <= -181.4f && to.Z >= Exit)
            return FullIce.Value.Rebase(from, to, length);
        return PrepareCore(from, to);
    }

    private static IceTraversalClock PrepareCore(Vector3 from, Vector3 to)
    {
        var length = Distance(from, to);
        if (length <= .00001f) return new(from, to, [0], [0], [0]);
        var slow = Clock(from, to, -181f); var fast = DelayEntry(Clock(from, to, -181.4f));
        // Every curve is slower than the dry 6m/s motion. More free distance
        // monotonically lowers arrival times, so intermediate entry clocks
        // cannot change either bound. Only the two slide lengths can cross.
        var sources = new[] { (Clock: fast, Length: 4.88), (Clock: fast, Length: 5.1),
            (Clock: slow, Length: 4.88), (Clock: slow, Length: 5.1) };
        var cuts = new SortedSet<double> { 0, length };
        foreach (var source in sources)
        {
            if (source.Clock.IceLength <= .000001) continue;
            var entry = source.Clock.BeforeIce; var exit = entry + source.Clock.IceLength;
            if (entry > 0 && entry < length)
            {
                cuts.Add(Math.BitDecrement(entry)); cuts.Add(entry);
            }
            if (exit > 0 && exit < length) cuts.Add(exit);
            foreach (var knot in Envelope)
            {
                var ice = source.Length * knot.Fraction;
                if (ice > 0 && ice < source.Clock.IceLength) cuts.Add(entry + ice);
            }
        }
        var distances = cuts.ToArray();
        var values = distances.Select(distance => new ClockBounds(
            AccurateBounds(fast, distance, 4.88).Earliest, AccurateBounds(fast, distance, 5.1).Earliest,
            AccurateBounds(slow, distance, 4.88).Latest, AccurateBounds(slow, distance, 5.1).Latest)).ToArray();
        var points = new List<(double Distance, double Earliest, double Latest)>();
        for (var i = 0; i + 1 < distances.Length; ++i)
        {
            var a = values[i]; var b = values[i + 1];
            Append(0);
            var first = Crossing(a.FastShort - a.FastLong, b.FastShort - b.FastLong);
            var second = Crossing(a.SlowShort - a.SlowLong, b.SlowShort - b.SlowLong);
            if (first > second) (first, second) = (second, first);
            if (first < 1) Append(first);
            if (second < 1 && second != first) Append(second);
            void Append(double fraction)
            {
                var earliest = Math.Min(a.FastShort + (b.FastShort - a.FastShort) * fraction,
                    a.FastLong + (b.FastLong - a.FastLong) * fraction);
                var latest = Math.Max(a.SlowShort + (b.SlowShort - a.SlowShort) * fraction,
                    a.SlowLong + (b.SlowLong - a.SlowLong) * fraction);
                points.Add((distances[i] + (distances[i + 1] - distances[i]) * fraction, earliest, latest));
            }
        }
        points.Add((length, Math.Min(values[^1].FastShort, values[^1].FastLong), Math.Max(values[^1].SlowShort, values[^1].SlowLong)));
        return new(from, to, points.Select(point => point.Distance).ToArray(),
            points.Select(point => point.Earliest).ToArray(), points.Select(point => point.Latest).ToArray());
    }

    private readonly record struct ClockBounds(double FastShort, double FastLong, double SlowShort, double SlowLong);

    private static double Crossing(double fromDifference, double toDifference)
    {
        if (fromDifference * toDifference >= 0) return double.PositiveInfinity;
        var fraction = fromDifference / (fromDifference - toDifference);
        return fraction > .000000001 && fraction < .999999999 ? fraction : double.PositiveInfinity;
    }

    internal sealed class IceTraversalClock(Vector3 from, Vector3 to, double[] distances, double[] earliest, double[] latest,
        double length = double.NaN) : IDirectTraversalClock
    {
        private readonly double _length = double.IsNaN(length) ? distances[^1] : length;
        public double EarliestDuration { get; } = TimeAt(distances, earliest, double.IsNaN(length) ? distances[^1] : length);
        public double LatestDuration { get; } = TimeAt(distances, latest, double.IsNaN(length) ? distances[^1] : length);

        internal IceTraversalClock Rebase(Vector3 origin, Vector3 target, double distance)
            => new(origin, target, distances, earliest, latest, distance);

        public (double Earliest, double Latest) TimeBoundsAt(double distance)
        {
            distance = Math.Clamp(distance, 0, _length);
            return (TimeAt(distances, earliest, distance), TimeAt(distances, latest, distance));
        }

        public (double Earliest, double Latest) Remaining(double distance, double? elapsedSeconds = null)
        {
            if (elapsedSeconds is { } elapsed)
                return (Math.Max(0, EarliestDuration - elapsed), Math.Max(0, LatestDuration - elapsed));
            var bounds = TimeBoundsAt(distance);
            return (Math.Max(0, EarliestDuration - bounds.Latest), Math.Max(0, LatestDuration - bounds.Earliest));
        }

        public (Vector3 From, Vector3 To)? Occupancy(double fromSeconds, double untilSeconds)
        {
            if (double.IsNaN(fromSeconds) || double.IsNaN(untilSeconds) || fromSeconds > untilSeconds
                || untilSeconds < -Frame || fromSeconds > LatestDuration + Frame) return null;
            if (_length <= .00001) return (from, to);
            var first = Inverse(latest, fromSeconds - Frame);
            var last = Inverse(earliest, untilSeconds + Frame);
            if (first > last + .00000001) return null;
            var delta = to - from;
            return (from + delta * (float)(first / _length), from + delta * (float)(last / _length));
        }

        private double Inverse(double[] times, double time)
        {
            if (time <= times[0]) return 0;
            if (time >= times[^1]) return _length;
            var low = 1; var high = times.Length - 1;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (times[middle] < time) low = middle + 1; else high = middle;
            }
            var duration = times[low] - times[low - 1];
            var fraction = duration > .000000001 ? (time - times[low - 1]) / duration : 0;
            return Math.Min(_length, distances[low - 1] + (distances[low] - distances[low - 1]) * fraction);
        }

        private static double TimeAt(double[] distances, double[] times, double distance)
        {
            if (distance <= distances[0]) return times[0];
            if (distance >= distances[^1]) return times[^1];
            var low = 1; var high = distances.Length - 1;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (distances[middle] < distance) low = middle + 1; else high = middle;
            }
            var length = distances[low] - distances[low - 1];
            var fraction = length > 0 ? (distance - distances[low - 1]) / length : 0;
            return times[low - 1] + (times[low] - times[low - 1]) * fraction;
        }
    }

    private readonly record struct SegmentClock(double BeforeIce, double IceLength);

    private static SegmentClock DelayEntry(SegmentClock clock)
    {
        if (clock.BeforeIce <= .000001 || clock.IceLength <= .000001) return clock;
        // State is sampled before movement. At 30fps a dry 6m/s frame can
        // cross the entry before the following sample acquires the slide.
        var overrun = Math.Min(6 * Frame, clock.IceLength);
        return new(clock.BeforeIce + overrun, clock.IceLength - overrun);
    }

    private static SegmentClock Clock(Vector3 from, Vector3 to, float entry)
    {
        var dx = (double)to.X - from.X; var delta = (double)to.Z - from.Z;
        var length = Math.Sqrt(dx * dx + delta * delta);
        if (Math.Abs(delta) < .00001)
            return from.Z <= entry && from.Z >= Exit ? new(0, length) : new(length, 0);
        var a = ((double)entry - from.Z) / delta; var b = ((double)Exit - from.Z) / delta;
        var first = Math.Clamp(Math.Min(a, b), 0, 1); var last = Math.Clamp(Math.Max(a, b), 0, 1);
        return new(length * first, length * (last - first));
    }

    private static (double Earliest, double Latest) Bounds(SegmentClock clock, double distance)
    {
        var before = Math.Min(clock.BeforeIce, distance);
        var ice = Math.Clamp(distance - clock.BeforeIce, 0, clock.IceLength);
        var dry = before + Math.Max(0, distance - clock.BeforeIce - clock.IceLength);
        if (ice <= .000001) return (dry / 6, dry / 6);
        var shortSlide = Curve(ice, 4.88); var longSlide = Curve(ice, 5.1);
        return (dry / 6 + Math.Min(shortSlide.Earliest, longSlide.Earliest),
            dry / 6 + Math.Max(shortSlide.Latest, longSlide.Latest));
    }

    private static (double Earliest, double Latest) AccurateBounds(SegmentClock clock, double distance, double slideLength)
    {
        if (clock.IceLength <= .000001 || distance < clock.BeforeIce) return (distance / 6, distance / 6);
        var ice = Math.Clamp(distance - clock.BeforeIce, 0, clock.IceLength);
        var dry = clock.BeforeIce + Math.Max(0, distance - clock.BeforeIce - clock.IceLength);
        var curve = Curve(ice, slideLength);
        return (dry / 6 + curve.Earliest, dry / 6 + curve.Latest);
    }

    private static (double Earliest, double Latest) Curve(double distance, double length)
    {
        var fraction = distance / length;
        if (fraction >= 1)
        {
            var tail = distance - length;
            return (length * Envelope[^1].Earliest + tail / 3, length * Envelope[^1].Latest + tail / 2.88);
        }
        var low = 1; var high = Envelope.Length - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (Envelope[middle].Fraction < fraction) low = middle + 1; else high = middle;
        }
        var a = Envelope[low - 1]; var b = Envelope[low];
        var f = (fraction - a.Fraction) / (b.Fraction - a.Fraction);
        return (length * (a.Earliest + (b.Earliest - a.Earliest) * f),
            length * (a.Latest + (b.Latest - a.Latest) * f));
    }

    private static float Distance(Vector3 from, Vector3 to) => Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z));
}

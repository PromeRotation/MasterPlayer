using System.Numerics;

namespace MasterPlayer.Spatial;

internal interface IDirectTraversalTiming
{
    double Duration(Vector3 from, Vector3 to, float speed);
    IReadOnlyList<DirectTraversalSlice> Sweep(Vector3 from, Vector3 to, float speed);
    IDirectTraversalClock Prepare(Vector3 from, Vector3 to, float speed)
        => new DirectTraversalSliceClock(Sweep(from, to, speed));
}

internal readonly record struct DirectTraversalSlice(Vector3 From, Vector3 To, double EarliestStart, double LatestEnd);

internal interface IDirectTraversalClock
{
    double EarliestDuration { get; }
    double LatestDuration { get; }
    (Vector3 From, Vector3 To)? Occupancy(double fromSeconds, double untilSeconds);
}

internal sealed class DirectTraversalSliceClock(IReadOnlyList<DirectTraversalSlice> slices) : IDirectTraversalClock
{
    public double EarliestDuration => slices.Count > 0 ? slices[^1].EarliestStart : 0;
    public double LatestDuration => slices.Count > 0 ? slices[^1].LatestEnd : 0;

    public (Vector3 From, Vector3 To)? Occupancy(double fromSeconds, double untilSeconds)
    {
        if (double.IsNaN(fromSeconds) || double.IsNaN(untilSeconds) || fromSeconds > untilSeconds) return null;
        Vector3? first = null; var last = Vector3.Zero;
        foreach (var slice in slices)
            if (slice.LatestEnd >= fromSeconds && slice.EarliestStart <= untilSeconds)
            {
                first ??= slice.From; last = slice.To;
            }
        return first is { } from ? (from, last) : null;
    }
}

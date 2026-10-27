using System.Numerics;

namespace MasterPlayer.Spatial;

internal sealed record DirectTraversalMotion(IReadOnlyList<float> Speeds, IReadOnlyList<bool> LockedSegments,
    float LockedArrivalTolerance = .35f, bool InputLockActive = true, IDirectTraversalTiming? Timing = null,
    IReadOnlyList<float>? ArrivalTolerances = null, IReadOnlyList<Vector3>? PhysicalLandings = null)
{
    public float Speed(int index, float fallback) => index < Speeds.Count && Speeds[index] > 0 ? Speeds[index] : fallback;
    public bool Locked(int index) => index < LockedSegments.Count && LockedSegments[index];
    public Vector3 Landing(int index, Vector3 fallback)
        => PhysicalLandings != null && index >= 0 && index < PhysicalLandings.Count ? PhysicalLandings[index] : fallback;
    public bool Matches(DirectTraversalMotion? other, IReadOnlyList<Vector3> path)
    {
        if (other == null || !Equals(Timing, other.Timing)) return false;
        for (var i = 0; i < path.Count; ++i)
            if (Speed(i, 0) != other.Speed(i, 0) || Locked(i) != other.Locked(i)
                || ArrivalTolerance(i) != other.ArrivalTolerance(i) || Landing(i, path[i]) != other.Landing(i, path[i])) return false;
        return true;
    }
    public float ArrivalTolerance(int index) => ArrivalTolerances != null && index < ArrivalTolerances.Count
        ? ArrivalTolerances[index] : Locked(index) && !LockedSegments.Take(index).Any(s => s)
            ? MathF.Max(.55f, LockedArrivalTolerance) : LockedArrivalTolerance;
}

using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class EurekaTwister(uint action)
{
    internal uint Action { get; } = action;
    internal DungeonPendingCast? Cast { get; private set; }
    internal DateTime PredictStart { get; private set; } = DateTime.MaxValue;
    internal DateTime Activation { get; private set; }
    internal List<Vector3> Positions { get; } = [];

    internal void Start(DungeonPendingCast cast)
    {
        if (PredictStart != DateTime.MaxValue) return;
        Cast = cast;
        PredictStart = cast.FinishAt.AddSeconds(-(double).25f);
    }

    internal void Update(DateTime now, bool hasObjects, IEnumerable<Vector3> party)
    {
        if (Positions.Count == 0 && !hasObjects && now >= PredictStart)
            Sample(now, .25f + .4f, party);
    }

    internal void Finish(DateTime now, IEnumerable<Vector3> party)
    {
        if (PredictStart != DateTime.MaxValue) Sample(now, .4f, party);
    }

    private void Sample(DateTime now, float delay, IEnumerable<Vector3> party)
    {
        Positions.Clear();
        Positions.AddRange(party);
        Activation = now.AddSeconds((double)delay);
        PredictStart = DateTime.MaxValue;
    }

    internal void Reset()
    {
        Positions.Clear();
        PredictStart = DateTime.MaxValue;
        Cast = null;
    }
}

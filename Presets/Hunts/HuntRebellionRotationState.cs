using System.Numerics;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntRebellionRotationState
{
    private readonly List<(HuntPendingCast Cast, float Increment, double Interval, int Remaining)> sequences = [];
    private readonly List<HuntPendingCast> areas = [];
    private int version;
    private int lastVersion;
    private int lastCount;
    internal bool HasSequence => sequences.Count != 0;
    internal IEnumerable<HuntPendingCast> Areas => areas;

    internal void AddCandidate(HuntPendingCast cast) => areas.Add(cast);

    internal void FinishCandidate()
    {
        if (areas.Count != 0) areas.RemoveAt(0);
    }

    internal void ColorCandidates()
    {
        if (HasSequence || areas.Count == 0) return;
        var first = areas[0].Activation;
        for (var i = 0; i < areas.Count; ++i)
            if ((areas[i].Activation - first).TotalSeconds < 1d)
                areas[i] = areas[i] with { Color = new(1, 1, 0, 1) };
    }

    internal void Start(HuntPendingCast cast, float increment, double interval, int count)
        => sequences.Add((cast, increment, interval, count));

    internal void Advance(DateTime now)
    {
        ++version;
        if (sequences.Count == 0) return;
        var sequence = sequences[0];
        if (--sequence.Remaining == 0) sequences.RemoveAt(0);
        else sequences[0] = (sequence.Cast with
        {
            Rotation = sequence.Cast.Rotation + sequence.Increment,
            Activation = now.AddSeconds(sequence.Interval), Stage = sequence.Cast.Stage + 1
        }, sequence.Increment, sequence.Interval, sequence.Remaining);
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated,
        Func<HuntPendingCast, HuntPendingCast, Vector3> origin, float? correctedIncrement = null)
    {
        if (sequences.Any(s => s.Cast.Source == previous.Source && s.Cast.Order == previous.Order && s.Cast.Stage != 0))
            return;
        var matched = sequences.FindIndex(s => s.Cast.Source == previous.Source && s.Cast.Order == previous.Order);
        var incrementDelta = matched >= 0 && correctedIncrement is { } nextIncrement
            ? nextIncrement - sequences[matched].Increment : 0;
        for (var i = 0; i < areas.Count; ++i)
        {
            var area = areas[i];
            if (area.Source != previous.Source || area.Order != previous.Order) continue;
            areas[i] = area with
            {
                Location = origin(area, updated), Rotation = area.Rotation + updated.Rotation - previous.Rotation
                    + (area.SequenceId?.EndsWith("/future") == true ? incrementDelta : 0),
                Activation = area.Activation.Add(updated.Activation - previous.Activation), Input = updated.Input
            };
        }
        for (var i = 0; i < sequences.Count; ++i)
        {
            var sequence = sequences[i];
            var cast = sequence.Cast;
            if (cast.Source != previous.Source || cast.Order != previous.Order || cast.Stage != 0) continue;
            sequences[i] = (cast with
            {
                Location = origin(cast, updated), Rotation = updated.Rotation,
                Activation = cast.Activation.Add(updated.Activation - previous.Activation), Input = updated.Input
            }, correctedIncrement ?? sequence.Increment, sequence.Interval, sequence.Remaining);
        }
    }

    internal void Update(DateTime now)
    {
        // The source rebuilds only after sequence count or actual consumption changes.
        // A late clock alone must not slide the cached future AOE.
        if (lastCount == sequences.Count && lastVersion == version) return;
        lastCount = sequences.Count;
        lastVersion = version;
        areas.Clear();
        foreach (var (cast, increment, interval, remaining) in sequences)
        {
            if (remaining > 1)
                areas.Add(cast with
                {
                    Rotation = cast.Rotation + increment, Stage = cast.Stage + 1,
                    Activation = (cast.Activation > now ? cast.Activation : now).AddSeconds(interval),
                    SequenceId = cast.SequenceId + "/future", Color = null
                });
            areas.Add(cast with
            {
                SequenceId = cast.SequenceId + "/current", Color = remaining > 1 ? new(1, 1, 0, 1) : null
            });
        }
    }
}

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    // These consumers share only GenericRotatingAOE's published list algorithm.
    // Their callbacks, counters and invalidation rules remain independent.
    private void PublishRotations(List<(BozjaCast Cast, float Increment, int Remaining)> sequences,
        List<BozjaAoe> output, double interval)
    {
        output.Clear();
        foreach (var (cast, increment, remaining) in sequences)
        {
            if (remaining > 1)
                output.Add(new(cast with
                {
                    Generation = ++serial,
                    Rotation = cast.Rotation + increment,
                    Activation = (cast.Activation > Now ? cast.Activation : Now).AddSeconds(interval),
                }, false));
            if (remaining != 0) output.Add(new(cast, remaining > 1));
        }
    }
}

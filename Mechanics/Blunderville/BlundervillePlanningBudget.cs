namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervillePlanningBudget
{
    private readonly Queue<double> samples = new();
    private double coldSeconds, reserveSeconds;
    internal int Samples => samples.Count;
    internal double ReserveSeconds => samples.Count == 0 ? coldSeconds : reserveSeconds;

    internal void Observe(double seconds, bool cold)
    {
        if (!double.IsFinite(seconds) || seconds <= 0)
            return;
        if (cold)
        {
            coldSeconds = Math.Max(seconds, coldSeconds * .95);
            return;
        }
        if (samples.Count == 64)
            samples.Dequeue();
        samples.Enqueue(seconds);
        var ordered = samples.Order().ToArray();
        var p95 = ordered[(int)Math.Ceiling(ordered.Length * .95) - 1];
        reserveSeconds = Math.Max(seconds, Math.Max(p95, reserveSeconds * .98));
    }

    // This budgets when to start work; it never changes a hazard window.
    internal double LeadSeconds(bool cold, double frameSeconds)
    {
        var frame = double.IsFinite(frameSeconds) ? Math.Clamp(frameSeconds, 1d / 240, .1) : .1;
        var measured = cold ? Math.Max(coldSeconds, reserveSeconds) : reserveSeconds;
        return Math.Max(.25, measured * 1.25 + 2 * frame);
    }
}

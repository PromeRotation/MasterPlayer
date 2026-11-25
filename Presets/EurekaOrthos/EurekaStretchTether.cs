namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class EurekaStretchTether(uint bad, uint good, double delay, bool kite)
{
    internal uint Bad { get; } = bad;
    internal uint Good { get; } = good;
    internal bool Kite { get; } = kite;
    internal readonly List<EurekaTetherBait> Baits = [];
    internal readonly List<(AlphaActorState Actor, uint Type)> Members = [];
    internal readonly List<(AlphaActorState Actor, DateTime Activation)> Timers = [];

    private (AlphaActorState Player, AlphaActorState Enemy)? Sides(AlphaActorState source,
        AlphaActorState? target, uint type, Func<AlphaActorState, bool> party)
        => target == null || type != Bad && type != Good ? null
            : party(source) ? (source, target) : (target, source);

    internal void Gain(AlphaActorState source, AlphaActorState? target, uint type,
        Func<AlphaActorState, bool> party, DateTime now, long generation)
    {
        if (Sides(source, target, type, party) is not { } pair) return;
        var index = Timers.FindIndex(t => ReferenceEquals(t.Actor, pair.Player));
        if (index < 0)
        {
            Timers.Add((pair.Player, now.AddSeconds(delay)));
            index = Timers.Count - 1;
        }
        Baits.Add(new(generation, pair.Enemy, pair.Player, now, Timers[index].Activation));
        Members.Add((pair.Player, type));
    }

    internal void Lose(AlphaActorState source, AlphaActorState? target, uint type,
        Func<AlphaActorState, bool> party)
    {
        if (Sides(source, target, type, party) is not { } pair) return;
        Baits.RemoveAll(b => ReferenceEquals(b.Source, pair.Enemy) && ReferenceEquals(b.Target, pair.Player));
        // The source removes one tuple using the original target, not the normalized player.
        Members.Remove((target!, type));
    }

    internal void Update(DateTime now) => Timers.RemoveAll(t => t.Activation.AddSeconds(1d) <= now);
    internal void Clear()
    {
        Baits.Clear();
        Members.Clear();
        Timers.Clear();
    }
}

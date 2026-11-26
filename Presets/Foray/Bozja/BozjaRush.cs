namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaRush(long generation, BozjaRule rule, BozjaActor source,
    BozjaActor target, float length, DateTime activation)
{
    internal long Generation { get; } = generation;
    internal BozjaRule Rule { get; } = rule;
    internal BozjaActor Source { get; } = source;
    internal BozjaActor Target { get; } = target;
    internal DateTime Activation { get; } = activation;
    internal float Length { get; set; } = length;
}

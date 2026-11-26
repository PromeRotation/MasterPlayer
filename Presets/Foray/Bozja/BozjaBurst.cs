namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaBurst(BozjaCast cast)
{
    internal BozjaCast Cast { get; } = cast;
    internal int NumCasts;
}

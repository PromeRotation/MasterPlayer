using System.Numerics;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaExaflare(BozjaCast cast, Vector3 advance, int left = 8, int maxShown = 4, double interval = .8d)
{
    internal BozjaCast Cast = cast;
    internal Vector3 Advance = advance;
    internal int Left = left;
    internal readonly int MaxShown = maxShown;
    internal readonly double Interval = interval;
}

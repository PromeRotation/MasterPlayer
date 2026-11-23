using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DeadEndsExaflareLine(long Id, ulong Source, Vector3 Next, Vector3 Advance, DateTime NextAt, int Left);

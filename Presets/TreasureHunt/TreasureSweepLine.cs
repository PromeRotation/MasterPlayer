using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureSweepLine(long Id, ulong Source, DateTime StartedAt, Vector3 Next,
    Vector3 Advance, DateTime NextExplosion, int Remaining = 4);

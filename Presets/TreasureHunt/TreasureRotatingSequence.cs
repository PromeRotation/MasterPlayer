using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureRotatingSequence(long Id, ulong Source, uint SourceOid, DateTime StartedAt,
    Vector3 Origin, float Rotation, float Increment, DateTime NextActivation, double Interval,
    int Remaining, int MaxShown, float Radius, float Angle);

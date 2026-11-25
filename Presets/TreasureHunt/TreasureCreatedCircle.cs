using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureCreatedCircle(ulong Source, uint SourceOid, Vector3 Position,
    DateTime CreatedAt, DateTime Activation, float Radius);

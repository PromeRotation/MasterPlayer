using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record HeavenOnHighSpawnAoe(ulong Generation, ulong ActorId, uint Oid,
    Vector3 Origin, DateTime CreatedAt, DateTime Activation, int Hits = 0);

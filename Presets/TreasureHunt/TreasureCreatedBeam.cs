using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureCreatedBeam(ulong Source, Vector3 Origin, float Rotation, DateTime CreatedAt, DateTime Activation);

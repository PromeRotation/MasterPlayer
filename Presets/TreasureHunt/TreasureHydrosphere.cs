using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureHydrosphere(ulong Source, Vector3 Position, DateTime CreatedAt, DateTime Activation);

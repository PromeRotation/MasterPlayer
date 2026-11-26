namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record AlteRoiteOrb(long Generation, DateTime Spawn, bool Outer,
    DateTime Predicted, DateTime? Actual = null);

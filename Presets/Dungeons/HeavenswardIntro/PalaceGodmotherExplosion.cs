using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record PalaceGodmotherExplosion(long Generation, AlphaActorState Source, Vector3 Origin, DateTime Activation);

namespace MasterPlayer.Mechanics.Dungeons;

// DirForward with a direction recomputed from the original Primary to the
// current viewer. Zero delta therefore means +Z, rather than no displacement.
internal sealed record CarnivaleDadJoke(long Generation, ulong Source, ulong Target, uint Icon,
    DateTime OccurredAt, DateTime ReceivedAt, DateTime Activation, AlphaActorState Origin, float Distance);

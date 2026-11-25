using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaMagnetism(long Generation, AlphaActorState Source, AlphaActorState Target,
    int Slot, Vector3 Origin, bool Pull, DateTime PreparedAt, DateTime Activation)
{
    internal float Distance => 10;
    internal bool IgnoreImmunes => true;
    internal bool StopAfterWall => true;
}

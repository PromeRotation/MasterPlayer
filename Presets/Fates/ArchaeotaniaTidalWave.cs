using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record ArchaeotaniaTidalWave(DungeonPendingCast Cast)
{
    internal float Distance => 48;
    internal bool StopAtWall => false;
    internal bool IgnoreImmunes => false;
    internal Vector3 Direction => new(MathF.Sin(Cast.Rotation), 0, MathF.Cos(Cast.Rotation));
    internal Vector3 Destination(Vector3 from) => from + Direction * Distance;
}

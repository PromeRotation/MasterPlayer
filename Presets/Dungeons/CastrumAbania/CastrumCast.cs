using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CastrumCast(ulong Source, uint Action, Vector3 Position, float Rotation,
    DateTime Start, DateTime Finish, ulong TargetId)
{
    internal string Key => $"cast/{Source}/{Action}/{Start.Ticks}";
}

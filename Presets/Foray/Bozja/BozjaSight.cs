using System.Numerics;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed class BozjaSight
{
    internal List<BozjaActor> Casters { get; } = [];
    internal List<BozjaCast> Safezones { get; } = [];
    internal Vector3? Origin { get; set; }
}

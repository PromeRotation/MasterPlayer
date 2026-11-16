using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed record EnuoCast(ulong Source, uint Action, ulong Target, Vector3 Position, float Rotation, DateTime Start, DateTime Finish);
internal sealed record EnuoHazard(string Key, GeometryTemplate Shape, Vector3 Position, float Rotation, DateTime From, DateTime Until, DateTime Telegraph)
{
    public bool Contains(Vector3 point, float margin = 0.7f) => Shape.Resolve(Position, Rotation, null).Contains(point, margin);
}
internal sealed record EnuoGoal(string Name, Vector3 Position, DateTime Until, float Radius = 0.6f);
internal sealed record EnuoAction(uint Id, ulong Target, DateTime Until, string Reason);
internal sealed record EnuoPlan
{
    public List<EnuoHazard> Hazards { get; } = [];
    public EnuoGoal? Goal { get; set; }
    public bool HoldMovement { get; set; }
    public bool KeepMoving { get; set; }
    public bool HoldActions { get; set; }
    public bool BlockCasts { get; set; }
    public float? Facing { get; set; }
    public ulong Target { get; set; }
    public ulong[] ForbiddenTargets { get; set; } = [];
    public List<EnuoAction> Actions { get; } = [];
    public List<string> Hints { get; } = [];
    public float ArenaRadius { get; set; } = 20;
}

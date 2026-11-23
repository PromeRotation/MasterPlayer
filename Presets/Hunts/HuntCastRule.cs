using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed record HuntCastRule(uint Action, string Name, GeometryTemplate? Shape,
    string? DamageType = null, string? Hint = null, float ChargeHalfWidth = 0,
    float GazeRange = 0, float KnockbackDistance = 0, bool InvertedGaze = false, bool FollowCasterRotation = false,
    float BaitRadius = 0, float SpreadRadius = 0, float PullDistance = 0, bool PullFromCaster = false);

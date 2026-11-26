using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaRule(string Component, uint[] Actions, BozjaRuleKind Kind,
    GeometryTemplate? Shape = null, bool GroupedFinish = false, int? MaxDangerColor = null,
    int? MaxRisky = null, bool Safe = false, int MinStackSize = 2,
    uint EnemyOid = 0, string? Hint = null, bool GlobalHint = false,
    int MaxCasts = int.MaxValue, int ExpectedNumCasters = 99, float GazeRange = 10000f);

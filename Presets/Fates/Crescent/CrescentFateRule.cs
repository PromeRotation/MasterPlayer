using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Fates;

internal sealed record CrescentFateRule(string Component, uint[] Actions, GeometryTemplate? Shape,
    CrescentFateRuleKind Kind = CrescentFateRuleKind.Aoe, float GazeRange = 0);

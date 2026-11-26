using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hydatos;

internal sealed record HydatosRule(string Component, uint[] Actions, GeometryTemplate? Shape = null,
    HydatosRuleKind Kind = HydatosRuleKind.Aoe, float Range = 0);

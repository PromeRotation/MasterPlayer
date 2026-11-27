using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class IrminsulCatalog
{
    internal static readonly BozjaDefinition Definition = new("A12IrminsulSawtooth", "天柱树", 0x13F1, 0,
        new(0, 0, 130), IrminsulArenas.Ring(0),
        [
            new("WhiteBreath", [5208], BozjaRuleKind.Aoe, new FanTemplate(28, 120 * (MathF.PI / 180))),
            new("MeanThrash", [5209], BozjaRuleKind.Aoe, new FanTemplate(12, 120 * (MathF.PI / 180))),
            new("MeanThrashKnockback", [5209], BozjaRuleKind.AllianceKnockback,
                KnockbackDistance: 10, KnockbackStopAtWall: true),
            new("MucusBomb", [5205], BozjaRuleKind.Spread, new CircleTemplate(10)),
            new("MucusSpray", [5472], BozjaRuleKind.Aoe, new DonutTemplate(6, 20)),
            new("Rootstorm", [5212], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Ambush", [5172], BozjaRuleKind.Aoe, new CircleTemplate(9)),
            new("ShockwaveStomp", [5213], BozjaRuleKind.CrystalSight, EnemyOid: 0x13F1),
        ], PullRadius: float.PositiveInfinity, SampleRadius: IrminsulArenas.RingRadius, Territory: 508);
}

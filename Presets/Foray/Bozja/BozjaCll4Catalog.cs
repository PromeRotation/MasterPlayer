using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class BozjaCll4Catalog
{
    internal static readonly IGeometryShape DawonStarting = CastrumLacusArenas.DawonStarting(0);
    internal static readonly IGeometryShape DawonDefault = CastrumLacusArenas.DawonDefault(0);
    internal static readonly IGeometryShape LyonStarting = CastrumLacusArenas.LyonStarting(0);
    internal static readonly IGeometryShape LyonDefault = CastrumLacusArenas.LyonDefault(0);
    internal static readonly BozjaDefinition Definition = new("CLL4Dawon", "达温",
        0x2EBD, 0, DawonStarting.Position, DawonStarting,
        [
            new("ArenaChange", [20858, 20836], BozjaRuleKind.Cll4Arena),
            new("Pentagust", [20855], BozjaRuleKind.Aoe, new FanTemplate(50, MathF.PI / 9)),
            new("FervidPulse", [20857], BozjaRuleKind.Aoe, new DungeonCrossTemplate(50, 7)),
            new("FrigidPulse", [20856], BozjaRuleKind.Aoe, new DonutTemplate(12, 60)),
            new("SwoopingFrenzy", [20853], BozjaRuleKind.Aoe, new CircleTemplate(12)),
            new("MoltingPlumage", [20858], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Scratch", [20859], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("CrackleHiss", [20870], BozjaRuleKind.Aoe, new FanTemplate(25, 2 * MathF.PI / 3)),
            new("RipperClaw", [20872], BozjaRuleKind.Aoe, new FanTemplate(9, MathF.PI / 2)),
            new("SpikeFlail", [20871], BozjaRuleKind.Aoe, new FanTemplate(25, MathF.PI / 3)),
            new("LeftRightHammer", [20874, 20879, 20877, 20876], BozjaRuleKind.Cll4Hammer, new FanTemplate(20, MathF.PI)),
            new("VerdantScarletPlume", [20880, 20865, 20866], BozjaRuleKind.Cll4Plumes),
            new("Obey", [], BozjaRuleKind.Cll4Obey),
            new("TasteOfBlood", [20851], BozjaRuleKind.Aoe, new FanTemplate(40, MathF.PI)),
            new("NaturesBlood", [20831, 20832], BozjaRuleKind.Exaflare, new CircleTemplate(4)),
            new("NaturesPulse", [20833, 20834, 20835], BozjaRuleKind.Earthbreaker),
            new("TwinAgonies", [20852], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("TheKingsNotice", [20846], BozjaRuleKind.CastGaze, GazeRange: 10000),
            new("HeartOfNature", [20830], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("WindsPeak", [20847], BozjaRuleKind.Aoe, new CircleTemplate(5)),
            // The source watches 20847, not the helper's 20848.
            new("WindsPeakKB", [20847], BozjaRuleKind.Cll4Wind),
        ], PullRadius: float.PositiveInfinity, SampleRadius: CastrumLacusArenas.DawonStartingRadius);
}

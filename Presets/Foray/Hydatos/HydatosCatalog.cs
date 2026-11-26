using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hydatos;

internal static class HydatosCatalog
{
    // GroupID=639 is a CFC. The actual territory is verified by the DLL runner.
    internal const uint TERRITORY = 827;
    internal const uint CFC = 639;
    internal static readonly HydatosDefinition[] Definitions =
    [
        new("Ceto", 0x2765, [0x2767, 0x26C5, 0x26C6],
        [
            new("SickleStrike", [15466], Kind: HydatosRuleKind.SingleTarget),
            new("Petrifaction", [15469, 15475], Kind: HydatosRuleKind.Gaze, Range: 50),
            new("AbyssalReaper", [15468], new CircleTemplate(18)),
            new("CircleOfFlames", [15472], new CircleTemplate(5)),
            new("TailSlap", [15471], Cone(12, 60)),
            new("Petrattraction", [15473], new CircleTemplate(50), HydatosRuleKind.Petrattraction, 50),
            new("CircleBlade", [15470, 15448], new CircleTemplate(7)),
            new("GrimFate", [15451], Cone(8, 60)),
            new("Desolation", [15453], RayRectangleTemplate.Symmetric(8, 12)),
            new("FireToss", [15449], new CircleTemplate(5)),
        ]),
        new("Daphne", 0x2744, [0x26BA],
        [
            new("Spellwind", [15031], Kind: HydatosRuleKind.Raidwide),
            new("Upburst", [15025], new CircleTemplate(8)),
            new("RoilingReach", [15029], new DungeonCrossTemplate(32, 3.5f)),
            new("Wallop", [15027], RayRectangleTemplate.Symmetric(7, 50)),
            new("ChillingGlare", [15030], Kind: HydatosRuleKind.Gaze, Range: 10000),
            new("Netherwater", [15033], new CircleTemplate(6), HydatosRuleKind.IconStack),
            new("Spiritus", [15417], Cone(5, 30)),
        ]),
        new("Molech", 0x275D, [0x275E],
        [
            new("W11TonzeSwipe", [14972], Cone(9, 60)),
            new("W111TonzeSwing", [14973], new CircleTemplate(13)),
            new("W111TonzeSwingAdds", [14979], new CircleTemplate(13)),
            new("W111TonzeSwingBig", [14974], new CircleTemplate(20)),
            new("ZoomIn", [14980], Kind: HydatosRuleKind.Charge, Range: 4),
        ]),
        new("Ovni", 0x2685, [0x26BF],
        [
            new("PullOfTheVoid", [14782], new CircleTemplate(30), HydatosRuleKind.Pull, 30),
            new("Megastorm", [14784], new DonutTemplate(5, 40)),
            new("ConcussiveOscillation", [14783], new CircleTemplate(24)),
            new("VitriolicBarrage", [14790], Kind: HydatosRuleKind.Raidwide),
            new("RockHard", [14786], new CircleTemplate(8)),
            new("TorrentialTorment", [14785], Cone(56, 22.5f)),
            new("IonShower", [14788], new CircleTemplate(20), HydatosRuleKind.IonShower),
            new("Fluorescence", [1766], Kind: HydatosRuleKind.Dispel),
            new("PlainPound", [15431], new CircleTemplate(6)),
        ]),
        new("ProvenanceWatcher", 0x2686, [],
        [
            new("TheScarletPrice", [15001], new CircleTemplate(3), HydatosRuleKind.Bait),
            new("TheScarletWhisper", [15000], Cone(22, 60)),
            new("EuhedralSwat", [14992], RayRectangleTemplate.Symmetric(26, 100)),
            new("Touchdown", [14993], Kind: HydatosRuleKind.Touchdown),
            new("PillarImpact", [15346], new CircleTemplate(6.5f)),
            new("PillarPierce", [15344], RayRectangleTemplate.Symmetric(10, 52.5f)),
            new("Thunderstorm", [15005], new CircleTemplate(5)),
            new("IceAndLevin", [14997], new DonutTemplate(11, 40), HydatosRuleKind.IceAndLevin),
            new("Charybdis", [15002], new CircleTemplate(6), HydatosRuleKind.Charybdis),
            new("HotTail", [14999], RayRectangleTemplate.Symmetric(16, 77)),
            new("HotTailSecond", [15007], new RectangleTemplate(16, 154), HydatosRuleKind.HotTailSecond),
            new("AkhMorn", [14994], new CircleTemplate(6), HydatosRuleKind.AkhMorn),
        ]),
        new("Phuabo", 0x2741, [0x2741, 0x2740],
        [
            new("SpineLash1", [14924], Cone(9, 45)),
        ]),
        new("Tristitia", 0x2740, [0x2741, 0x2740],
        [
            new("ShockSpikes", [199], Kind: HydatosRuleKind.Dispel),
            new("MightyStrikes", [1797], Kind: HydatosRuleKind.Dispel),
            new("TornadoIIAerogaIVDualCast", [15909, 15914, 15912], Kind: HydatosRuleKind.DualCast),
            new("WatergaIII", [14922], new CircleTemplate(8)),
            // The second activation replaces and cold-broadcasts the first instance.
            new("TornadoIIAerogaIVDualCast", [15909, 15914, 15912], Kind: HydatosRuleKind.DualCast),
            new("SpineLash2", [14921], Cone(11, 45)),
            new("Meteor", [15911], Kind: HydatosRuleKind.Raidwide),
        ]),
    ];

    internal static bool Owns(uint territory, uint oid)
        => territory == TERRITORY && Definitions.Any(d => d.PrimaryOid == oid);

    private static FanTemplate Cone(float radius, float halfDegrees)
        => new(radius, halfDegrees * (MathF.PI / 180f) * 2);
}

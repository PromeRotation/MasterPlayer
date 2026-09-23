namespace BossMod.Stormblood.Alliance.A11Mateus;

public enum OID : uint
{
    Boss = 0x1F9B, // R4.5
    IceAzer = 0x1F9C, // R1.04
    Totema = 0x1F9D, // R1.0
    BlizzardIII = 0x1FA0, // R1.5
    AquaSphere = 0x1F9F, // R1.8
    AquaBubble = 0x1F9E, // R1.0
    IceSlave = 0x2062, // R1.0
    BlizzardIV = 0x1FA4, // R0.7
    Icicle = 0x1FA2, // R1.0
    FlumeToad = 0x1FA1, // R1.92
    Froth = 0x1FA3, // R0.7-1.4
    BlizzardSphere = 0x1FA6, // R1.0
    AzureGuard = 0x1FA5, // R2.8
    BlizzardIIITowers = 0x1EA1A1, // R2.0
    Helper = 0x18D6
}

public enum AID : uint
{
    AutoAttack = 872, // Boss/IceSlave/FlumeToad/AzureGuard->player, no cast, single-target

    Blizzard = 9784, // IceAzer->player, 1.0s cast, single-target
    FlashFreeze = 9799, // Boss->self, no cast, range 12+R 120-degree cone
    IcePhaseStart1 = 9988, // Boss->self, no cast, single-target
    IcePhaseStart2 = 10004, // Totema->self, no cast, single-target
    IcePhaseStart3 = 9890, // Boss->self, no cast, single-target
    HypothermalCombustion = 9785, // IceAzer->self, 5.0s cast, range 8+R circle
    Unbind = 9779, // Boss->self, 5.0s cast, single-target
    DreadTide = 10046, // AquaBubble->self, no cast, range 4 circle
    BallOfIce = 9780, // Helper->self, no cast, range 2 circle
    ConcealmentVisual = 9783, // Helper->self, no cast, single-target
    DarkBlizzardIII = 10045, // IceSlave->players, 3.0s cast, range 5 circle
    Rebind = 9781, // Boss->self, 5.0s cast, single-target
    ReturnToBody = 9782, // Totema->Boss, no cast, single-target
    Dualcast = 9788, // Boss->self, 5.0s cast, single-target
    SummonBlizzard = 9787, // Boss->self, no cast, single-target
    BlizzardIVVisual = 9789, // Boss->self, 10.0s cast, single-target
    BlizzardIV = 9790, // BlizzardIV->location, 10.0s cast, range 100 circle, proximity aoe
    Froth = 9791, // FlumeToad->self, no cast, single-target
    Snowpierce = 9792, // Icicle->players, 10.0s cast, width 3 rect charge
    Dendrite = 9797, // Boss->self, no cast, single-target
    Chill = 9798, // BlizzardSphere->self, 5.0s cast, range 40+R 20-degree cone
    IcePhaseEnd = 9836, // Boss->self, no cast, single-target
    FinRays = 9794, // AzureGuard->self, no cast, range 9+R 120-degree cone
    Frostwave = 9793, // Boss->self, no cast, range 100 circle
    TheWhiteWhisper = 10030 // BlizzardIII->self, 15.0s cast, single-target
}

public enum SID : uint
{
    Breathless = 1429 // none->player, extra=0x1/0x2/0x3/0x4/0x5/0x6/0x7/0x8/0x9
}

public enum TetherID : uint
{
    KiteTether = 8 // player/BlizzardSphere->BlizzardIII/player
}

namespace BossMod.Stormblood.Foray.BaldesionArsenal.BA3AbsoluteVirtue;

public enum OID : uint
{
    Boss = 0x25DC, // R5.4
    RelativeVirtue = 0x25DD, // R5.4
    DarkAurora = 0x25E0, // R1.0
    BrightAurora = 0x25DF, // R1.0
    BrightAuroraHelper = 0x1EAA4A, // R0.5
    DarkAuroraHelper = 0x1EAA49, // R0.5
    AernsWynav = 0x25DE, // R1.0
    Helper = 0x2628
}

public enum AID : uint
{
    AutoAttack1 = 14532, // Boss->player, no cast, single-target
    AutoAttack2 = 870, // AernsWynav->player, no cast, single-target

    Meteor = 14233, // Boss->self, 4.0s cast, range 60 circle
    EidosUmbral = 14215, // Boss->self, 2.0s cast, single-target
    EidosAstral = 14214, // Boss->self, 2.0s cast, single-target
    UmbralRays1 = 14222, // Helper->self, 8.0s cast, range 8 circle
    UmbralRays2 = 14223, // Helper->self, 8.0s cast, range 8 circle
    AstralRays1 = 14221, // Helper->self, 8.0s cast, range 8 circle
    AstralRays2 = 14220, // Helper->self, 8.0s cast, range 8 circle

    HostileAspect = 14219, // Boss->self, 8.0s cast, single-target
    MedusaJavelin = 14235, // Boss->self, 3.0s cast, range 60+R 90-degree cone
    ImpactStream1 = 14216, // Boss->self, 3.0s cast, single-target
    ImpactStream2 = 14229, // RelativeVirtue->self, 5.0s cast, single-target

    BrightAurora1 = 14217, // Helper->self, 3.0s cast, range 30 width 100 rect
    BrightAurora2 = 14230, // Helper->self, 5.0s cast, range 30 width 100 rect
    DarkAurora1 = 14218, // Helper->self, 3.0s cast, range 30 width 100 rect
    DarkAurora2 = 14231, // Helper->self, 5.0s cast, range 30 width 100 rect
    Explosion1 = 14226, // DarkAurora->self, no cast, range 6 circle
    Explosion2 = 14225, // BrightAurora->self, no cast, range 6 circle

    AuroralWind = 14234, // Boss->players, 5.0s cast, range 5 circle, tankbuster
    TurbulentAether = 14224, // Boss->self, 3.0s cast, single-target

    ExplosiveImpulse1 = 14227, // RelativeVirtue->self, 5.0s cast, range 60 circle, proximity AOE
    ExplosiveImpulse2 = 14228, // Boss->self, 5.0s cast, range 60 circle, proximity AOE

    CallWyvern = 14232, // Boss->self, 3.0s cast, single-target
    ExplosionWyvern = 14676, // AernsWynav->self, 8.0s cast, range 60 circle, add soft enrage

    MeteorEnrage = 14700, // Boss->self, 10.0s cast, range 60 circle
    MeteorEnrageRepeat = 14703, // Boss->self, no cast, range 60 circle, repeat until everyone is dead
}

public enum TetherID : uint
{
    BrightAurora = 2, // BrightAurora->player
    DarkAurora = 1 // DarkAurora->player
}

public enum SID : uint
{
    UmbralEssence = 1711, // Boss->Helper/Boss/RelativeVirtue, extra=0x0
    AstralEssence = 1710 // Boss->Boss/Helper/RelativeVirtue, extra=0x0
}

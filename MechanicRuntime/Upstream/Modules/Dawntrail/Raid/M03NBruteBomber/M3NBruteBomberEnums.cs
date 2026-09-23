namespace BossMod.Dawntrail.Raid.M03NBruteBomber;

public enum OID : uint
{
    Boss = 0x42C2, // R5.016
    LitFuse = 0x42C3, // R1.2
    Refbot = 0x42C4, // R3.36
    LariatHelper = 0x42C5, // R1.0
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 39553, // Boss->player, no cast, single-target
    Teleport = 37803, // Boss->location, no cast, single-target

    BrutalBurnVisual = 37928, // Boss->self, 4.5+0.5s cast, single-target
    BrutalBurn = 37929, // Helper->players, 5.0s cast, range 6 circle

    BrutalImpactFirst = 37846, // Boss->self, 5.0s cast, range 60 circle
    BrutalImpactRest = 37847, // Boss->self, no cast, range 60 circle

    BrutalLariatVisual1 = 39670, // LariatHelper->self, 4.9s cast, single-target
    BrutalLariatVisual2 = 39636, // Boss->location, 4.9+1.2s cast, single-target
    BrutalLariatVisual3 = 39637, // Boss->location, 4.9+1.2s cast, single-target
    BrutalLariatVisual4 = 39808, // LariatHelper->self, 4.9s cast, single-target
    BrutalLariatVisual5 = 39670, // LariatHelper->self, 4.9s cast, single-target
    BrutalLariatVisual6 = 39671, // LariatHelper->self, no cast, single-target
    BrutalLariat1 = 39638, // Helper->self, 6.1s cast, range 50 width 34 rect
    BrutalLariat2 = 39639, // Helper->self, 6.1s cast, range 50 width 34 rect

    LariatComboVisual1 = 39644, // Boss->location, 4.9+1.2s cast, single-target, down west, up east
    LariatComboVisual2 = 39645, // Boss->location, 4.9+1.2s cast, single-target, down west, up west
    LariatComboVisual3 = 39646, // Boss->location, 4.9+1.2s cast, single-target, down east, up west
    LariatComboVisual4 = 39647, // Boss->location, 4.9+1.2s cast, single-target, down east, up east
    LariatComboVisual5 = 39648, // Boss->location, 3.1s cast, single-target
    LariatComboVisual6 = 39649, // Boss->location, 3.1s cast, single-target
    LariatComboVisual7 = 39650, // Boss->location, 3.1s cast, single-target
    LariatComboVisual8 = 39651, // Boss->location, 3.1s cast, single-target
    LariatCombo1 = 39652, // Helper->self, 6.1s cast, range 70 width 34 rect
    LariatCombo2 = 39653, // Helper->self, 6.1s cast, range 70 width 34 rect
    LariatCombo3 = 39654, // Helper->self, 3.1s cast, range 50 width 34 rect
    LariatCombo4 = 39655, // Helper->self, 3.1s cast, range 50 width 34 rect

    DopingDraught = 37822, // Boss->self, 4.0s cast, single-target

    BarbarousBarrage = 37810, // Boss->self, 4.0s cast, single-target, spawns towers
    Explosion = 37811, // Helper->self, no cast, range 4 circle, knockback 22, away from source
    UnmitigatedExplosion = 37812, // Helper->self, no cast, range 60 circle, tower fail

    ExplosiveRain1 = 37837, // Helper->self, 5.0s cast, range 8 circle
    ExplosiveRain2 = 37838, // Helper->self, 7.0s cast, range 8-16 donut
    ExplosiveRain3 = 37839, // Helper->self, 9.0s cast, range 16-24 donut
    ExplosiveRain4 = 38541, // Helper->self, 3.0s cast, range 6 circle

    FireSpinCW = 37840, // Boss->self, 4.5+0.5s cast, single-target
    FireSpinCCW = 37841, // Boss->self, 4.5+0.5s cast, single-target
    FireSpinVisual = 37842, // Boss->self, no cast, single-target
    FireSpinFirst = 39768, // Helper->self, 5.0s cast, range 40 60-degree cone, 8 casts
    FireSpinRest = 39769, // Helper->self, 0.5s cast, range 40 60-degree cone

    FusesOfFury = 37814, // Boss->self, 4.0s cast, single-target

    InfernalSpinCW = 39746, // Boss->self, 4.5+0.5s cast, single-target
    InfernalSpinCCW = 39747, // Boss->self, 4.5+0.5s cast, single-target
    InfernalSpinVisual = 39748, // Boss->self, no cast, single-target
    InfernalSpinFirst = 39770, // Helper->self, 5.0s cast, range 40 60-degree cone
    InfernalSpinRest = 39771, // Helper->self, 0.5s cast, range 40 60-degree cone

    KnuckleSandwich = 37845, // Boss->players, 5.0s cast, range 6 circle, shared tankbuster

    MurderousMist = 37813, // Boss->self, 5.0s cast, range 40 270-degree cone

    SelfDestruct1 = 37816, // LitFuse->self, 5.0s cast, range 8 circle
    SelfDestruct2 = 37817 // LitFuse->self, 8.0s cast, range 8 circle
}

public enum SID : uint
{
    LitFuseShort = 4015, // none->LitFuse, extra=0x2DF
    LitFuseLong = 4016, // none->LitFuse, extra=0x2E0
    LitFuseShortBurning = 4017, // none->LitFuse, extra=0x2E1
    LitFuseLongBurning = 4018, // none->LitFuse, extra=0x2E2
}

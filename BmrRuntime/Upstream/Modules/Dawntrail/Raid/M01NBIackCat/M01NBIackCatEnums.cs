namespace BossMod.Dawntrail.Raid.M01NBlackCat;

public enum OID : uint
{
    Boss = 0x429C, // R3.993
    CopyCat = 0x429D, // R3.993
    LeapingAttacks = 0x429E, // R1.0
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 39151, // Boss->player, no cast, single-target
    Teleport = 37640, // Boss->location, no cast, single-target

    BiscuitMaker = 37706, // Boss->player, 5.0s cast, single-target, tankbuster

    BlackCatCrossingVisual1 = 37647, // Boss->self, 6.0s cast, single-target
    BlackCatCrossingVisual2 = 37648, // Boss->self, 1.0s cast, single-target
    BlackCatCrossingFirst = 37649, // Helper->self, 7.0s cast, range 60 45-degree cone
    BlackCatCrossingRest = 37650, // Helper->self, 7.0s cast, range 60 45-degree cone

    BloodyScratch = 37696, // Boss->self, 5.0s cast, range 60 circlel, raidwide

    ClawfulVisual = 37692, // Boss->self, 4.0+1.0s cast, single-target
    Clawful = 37693, // Helper->players, 5.0s cast, range 5 circle, stack

    Copycat = 37656, // Boss->self, 3.0s cast, single-target
    ElevateAndEviscerate = 37655, // Boss/CopyCat->player, 8.0s cast, single-target

    GrimalkinGaleVisual = 37694, // Boss->self, no cast, single-target
    GrimalkinGale = 37695, // Helper->player, 5.0s cast, range 5 circle, spread

    Impact = 39250, // Helper->self, no cast, range 10 width 10 rect

    LeapingBlackCatCrossingVisual1 = 37673, // Boss->self, 7.0s cast, single-target
    LeapingBlackCatCrossingVisual2 = 38928, // Boss->self, 7.0s cast, single-target
    LeapingBlackCatCrossingVisual3 = 37674, // Boss->self, no cast, single-target
    LeapingBlackCatCrossingVisual4 = 37675, // Boss->self, no cast, single-target
    LeapingBlackCatCrossingFirst = 37676, // Helper->self, 1.0s cast, range 60 45-degree cone
    LeapingBlackCatCrossingRest = 37677, // Helper->self, 3.0s cast, range 60 45-degree cone

    LeapingOneTwoPawVisual1 = 37663, // Boss->self, 7.0s cast, single-target (90 -> -90 degrees)
    LeapingOneTwoPawVisual2 = 37664, // Boss->self, 7.0s cast, single-target (-90 -> 90 degrees)
    LeapingOneTwoPawVisual3 = 37665, // Boss->self, 7.0s cast, single-target (90 -> -90 degrees)
    LeapingOneTwoPawVisual4 = 37666, // Boss->self, 7.0s cast, single-target (-90 -> 90 degrees)
    LeapingOneTwoPawVisual5 = 37670, // Boss->self, no cast, single-target
    LeapingOneTwoPawVisual6 = 37667, // Boss->self, no cast, single-target
    LeapingOneTwoPaw1 = 37668, // Helper->self, 0.8s cast, range 60 180-degree cone
    LeapingOneTwoPaw2 = 37669, // Helper->self, 2.8s cast, range 60 180-degree cone
    LeapingOneTwoPaw3 = 37671, // Helper->self, 2.8s cast, range 60 180-degree cone
    LeapingOneTwoPaw4 = 37672, // Helper->self, 0.8s cast, range 60 180-degree cone

    MouserVisual1 = 37651, // Boss->self, 9.0s cast, single-target
    MouserVisual2 = 37654, // Helper->location, no cast, single-target
    MouserVisual3 = 37652, // Boss->self, no cast, single-target
    MouserTelegraphFirst = 37653, // Helper->self, 1.0s cast, range 10 width 10 rect
    MouserTelegraphSecond = 39275, // Helper->self, 1.0s cast, range 10 width 10 rect
    Mouser = 38053, // Helper->self, no cast, range 10 width 10 rect

    OneTwoPawVisual1 = 37641, // Boss->self, 6.0s cast, single-target
    OneTwoPawVisual2 = 37644, // Boss->self, 6.0s cast, single-target
    OneTwoPaw1 = 37642, // Helper->self, 6.8s cast, range 60 180-degree cone
    OneTwoPaw2 = 37643, // Helper->self, 9.8s cast, range 60 180-degree cone
    OneTwoPaw3 = 37645, // Helper->self, 9.8s cast, range 60 180-degree cone
    OneTwoPaw4 = 37646, // Helper->self, 6.8s cast, range 60 180-degree cone

    OvershadowVisual = 37657, // Boss->player, 5.0s cast, single-target
    OverShadowMarker = 26708, // Helper->player, no cast, single-target
    Overshadow = 37658, // Boss->players, no cast, range 60 width 5 rect, line stack

    PredaceousPounceVisual1 = 39634, // Boss/CopyCat->location, 13.0s cast, single-target
    PredaceousPounceVisual2 = 37680, // Boss/CopyCat->location, no cast, single-target
    PredaceousPounceTelegraphCharge1 = 37682, // Helper->location, 2.0s cast, width 6 rect charge
    PredaceousPounceTelegraphCircle1 = 37683, // Helper->self, 3.0s cast, range 11 circle
    PredaceousPounceTelegraphCharge2 = 37684, // Helper->location, 4.0s cast, width 6 rect charge
    PredaceousPounceTelegraphCircle2 = 37685, // Helper->self, 5.0s cast, range 11 circle
    PredaceousPounceTelegraphCharge3 = 37686, // Helper->location, 6.0s cast, width 6 rect charge
    PredaceousPounceTelegraphCircle3 = 37687, // Helper->self, 7.0s cast, range 11 circle
    PredaceousPounceTelegraphCharge4 = 37688, // Helper->location, 8.0s cast, width 6 rect charge
    PredaceousPounceTelegraphCircle4 = 37689, // Helper->self, 9.0s cast, range 11 circle
    PredaceousPounceTelegraphCharge5 = 37690, // Helper->location, 10.0s cast, width 6 rect charge
    PredaceousPounceTelegraphCircle5 = 37691, // Helper->self, 11.0s cast, range 11 circle
    PredaceousPounceTelegraphCharge6 = 39630, // Helper->location, 12.0s cast, width 6 rect charge
    PredaceousPounceTelegraphCircle6 = 39631, // Helper->self, 13.0s cast, range 11 circle
    PredaceousPounceCircle1 = 37681, // Helper->self, 1.5s cast, range 11 circle
    PredaceousPounceCircle2 = 39703, // Helper->self, 14.0s cast, range 11 circle
    PredaceousPounceCharge1 = 39268, // Helper->location, 1.0s cast, width 6 rect charge
    PredaceousPounceCharge2 = 39702, // Helper->location, 13.5s cast, width 6 rect charge

    ShockwaveVisual = 37661, // Boss->self, 6.0+1.0s cast, single-target
    Shockwave = 37662, // Helper->self, 7.0s cast, range 30 circle, knockback 18, away from source
}

public enum SID : uint
{
    BlackCatCrossing1 = 2056, // none->Boss, extra=0x2CB/0x307 -> 0x2CB attack does intercardinals first, 0x307 used in Mouser mechanic
    BlackCatCrossing2 = 2193 // none->Boss, extra=0x2CC -> attack does cardinals first
}

public enum TetherID : uint
{
    ElevateAndEviscerateGood = 267, // Boss/CopyCat->player
    ElevateAndEviscerateBad = 268, // Boss/CopyCat->player
    LeapingAttacks = 12, // Boss->LeapingAttacks
}

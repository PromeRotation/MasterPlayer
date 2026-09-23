namespace BossMod.Dawntrail.Raid.M04NWickedThunder;

public enum OID : uint
{
    Boss = 0x4263, // R4.9
    WickedThunder = 0x4569, // R1.0
    WickedReplica = 0x4264, // R3.675-4.9
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack1 = 39146, // Boss->player, no cast, single-target
    AutoAttack2 = 36759, // Boss->player, no cast, single-target
    Teleport = 37577, // Boss->location, no cast, single-target

    BewitchingFlightVisual1 = 37545, // Boss->self, no cast, single-target
    BewitchingFlightVisual2 = 36324, // Boss/WickedReplica->self, 6.0+1.0s cast, single-target
    BewitchingFlightVisual3 = 36326, // Boss/WickedReplica->self, 6.0+1.0s cast, single-target
    BewitchingFlight = 37560, // Helper->self, 7.0s cast, range 40 width 5 rect

    Burst = 37561, // Helper->self, 7.0s cast, range 40 width 16 rect

    ShadowsSabbath1 = 38044, // Boss->self, 3.0s cast, single-target
    ShadowsSabbath2 = 39871, // Boss->self, 3.0s cast, single-target

    SidewiseSpark1 = 37564, // Boss->self, 7.0s cast, range 60 180-degree cone
    SidewiseSpark2 = 37565, // Boss->self, 7.0s cast, range 60 180-degree cone
    SidewiseSpark3 = 37566, // Boss->self, 7.0s cast, range 60 180-degree cone
    SidewiseSpark4 = 37567, // Boss->self, 7.0s cast, range 60 180-degree cone
    SidewiseSpark5 = 39429, // WickedReplica->self, 1.0s cast, range 60 180-degree cone
    SidewiseSpark6 = 39439, // WickedReplica->self, 1.0s cast, range 60 180-degree cone

    SoaringSoulpressVisual1 = 37568, // Boss->self, 5.0s cast, single-target
    SoaringSoulpressVisual2 = 37546, // Boss->self, no cast, single-target
    SoaringSoulpress = 37569, // Boss->players, no cast, range 6 circle

    StampedingThunderVisualWest = 37547, // Boss->self, no cast, single-target
    StampedingThunderVisualEast = 37548, // Boss->self, no cast, single-target
    StampedingThunderVisual0 = 37543, // Boss->self, no cast, single-target
    StampedingThunderVisual1 = 35334, // Boss->self, no cast, single-target
    StampedingThunderVisual2 = 36150, // Helper->self, 7.3s cast, range 40 width 30 rect
    StampedingThunderVisual = 35335, // Helper->self, no cast, range 40 width 30 rect

    Thunderslam = 37574, // Helper->location, 3.0s cast, range 5 circle
    ThunderstormVisual = 37544, // Boss->self, no cast, single-target
    Thunderstorm = 37573, // Helper->player, 8.0s cast, range 5 circle, spread

    WickedBoltVisual = 37570, // Boss->self, 4.0+1.0s cast, single-target, stack
    WickedBolt = 37571, // Helper->players, no cast, range 5 circle, 5 hits

    ThreefoldBlast1 = 37549, // Boss->self, 11.0s cast, single-target
    ThreefoldBlast2 = 37552, // Boss->self, 11.0s cast, single-target
    FourfoldBlast1 = 39759, // Boss->self, 13.5s cast, single-target
    FourfoldBlast2 = 39765, // Boss->self, 13.5s cast, single-target
    FivefoldBlast1 = 39766, // Boss->self, 16.0s cast, single-target
    FivefoldBlast2 = 39767, // Boss->self, 16.0s cast, single-target
    WickedCannon1 = 20032, // Helper->self, no cast, range 40 width 10 rect
    WickedCannon2 = 37550, // Boss->self, no cast, range 40 width 10 rect
    WickedCannon3 = 37551, // Boss->self, no cast, range 40 width 10 rect
    WickedCannon4 = 39852, // Boss->self, no cast, range 40 width 10 rect
    WickedCannon5 = 39870, // Boss->self, no cast, range 40 width 10 rect

    WickedHypercannonVisual1 = 37102, // WickedThunder->self, no cast, single-target
    WickedHypercannonVisual2 = 37553, // Boss->self, 5.0s cast, single-target
    WickedHypercannonVisual3 = 37554, // Boss->self, 5.0s cast, single-target
    WickedHypercannon = 37555, // Helper->self, no cast, range 40 width 10 rect

    WickedJolt = 37576, // Boss->self/player, 5.0s cast, range 60 width 5 rect

    WitchHuntVisual = 37556, // Boss->self, 3.0s cast, single-target
    WitchHuntTelegraph = 37557, // Helper->location, 1.5s cast, range 6 circle
    WitchHunt = 37558, // WickedReplica->location, no cast, range 6 circle

    WrathOfZeus = 37575 // Boss->self, 5.0s cast, range 60 circle
}

public enum SID : uint
{
    WickedReplica = 2056, // none->WickedReplica, extra=0x319/0x31A
    WickedCannon = 2970 // none->Boss, extra=0x2D4/0x2D3 -> 0x2D4 south, 0x2D3 north
}

public enum IconID : uint
{
    SoaringSoulpress = 161, // player
    WickedBolt = 316, // player
    Thunderstorm = 345, // player
    WickedJolt = 471, // player
}

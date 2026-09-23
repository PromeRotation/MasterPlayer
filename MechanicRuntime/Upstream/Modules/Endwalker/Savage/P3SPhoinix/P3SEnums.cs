namespace BossMod.Endwalker.Savage.P3SPhoinix;

public enum OID : uint
{
    Boss = 0x353F, // R5.98
    Sparkfledged = 0x3540, // R1.5, spawned mid fight, "eyes" with cone aoe
    SunbirdSmall = 0x3541, // R1.08, spawned mid fight
    SunbirdLarge = 0x3543, // R2.7, spawned mid fight
    Sunshadow = 0x3544, // R0.98, spawned mid fight, mini birds that charge during fountains of fire
    DarkenedFire = 0x3545, // R2.0, spawned mid fight
    FountainOfFire = 0x3546, // R2.0, spawned mid fight, towers that healers soak
    DarkblazeTwister = 0x3547, // R2.5, spawned mid fight, tornadoes
    TwisterVoidzone = 0x1EA1FA, // R0.5
    SparkfledgedHelper = 0x3800, // R2.3, spawned mid fight, have weird kind... - look like "eyes" during death toll?..
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttackBoss = 872, // Boss->player, no cast, single-target
    AutoAttackSpark = 28125, // SparkfledgedHelper->player, no cast, single-target
    AutoAttackSunBirdS = 27917, // SunbirdSmall->player, no cast, single-target
    AutoAttackSunbirdL = 27919, // SunbirdLarge->player, no cast, single-target
    Teleport = 28438, // Boss->location, no cast, single-target

    FledglingFlight = 26282, // Boss->self, 3.0s cast, single-target
    DarkenedFire = 26297, // Boss->self, 6.0s cast, single-target
    DarkenedFireFail = 26298, // DarkenedFire->self, no cast, range 60 circle - wipe if they spawn too close to each other
    DarkenedBlaze = 26299, // DarkenedFire->self, 22.0s cast, range 60 circle
    BrightenedFire = 26300, // Boss->self, 5.0s cast, single-target
    BrightenedFireAOE = 26301, // Boss->players, no cast, range 7 circle
    ExperimentalFireplumeSingle = 26302, // Boss->self, 5.0s cast, single-target
    ExperimentalFireplumeSingleAOE = 26303, // Helper->self, 6.0s cast, range 15 circle
    ExperimentalFireplumeMulti = 26304, // Boss->self, 5.0s cast, single-target
    ExperimentalFireplumeMultiAOE = 26305, // Helper->self, 2.0s cast, range 10 circle
    ExperimentalAshplumeStack = 26306, // Boss->Boss
    ExperimentalAshplumeStackAOE = 26307, // Helper->targets, no cast
    ExperimentalAshplumeSpread = 26308, // Boss->self, 5.0s cast, single-target
    ExperimentalAshplumeSpreadAOE = 26309, // Helper->player, no cast, range 6 circle, 7sec after cast end
    ExperimentalGloryplumeSingle = 26310, // Boss->Boss, single+whatever variant
    ExperimentalGloryplumeSingleAOE = 26311, // Helper->Helper, 'normal single plume' aoe
    ExperimentalGloryplumeSpread = 26312, // Boss->self, no cast, single-target, cast 3sec after gloryplume cast end for 'spread' variant and determines visual cue
    ExperimentalGloryplumeSpreadAOE = 26313, // Helper->player, no cast, range 6 circle, actual damage, ~10sec after cue
    ExperimentalGloryplumeMulti = 26314, // Boss->self, 5.0s cast, single-target
    ExperimentalGloryplumeMultiAOE = 26315, // Helper->self, 7.0s cast, range 10 circle, 'normal multi plume' aoes
    ExperimentalGloryplumeStack = 26316, // Boss->self, no cast, single-target, cast 3sec after gloryplume cast end for 'stack' variant and determines visual cue
    ExperimentalGloryplumeStackAOE = 26317, // Helper->players, no cast, range 8 circle, actual damage, ~10sec after cue
    DevouringBrand = 26318, // Boss->self, 3.0s cast, single-target
    DevouringBrandExpanding = 26320, // Helper->self, no cast, range 4 width 5 rect
    DevouringBrandMiniAOE = 26319, // Helper->self, 3.0s cast, range 2 width 5 rect (cardinals)
    DevouringBrandAOE = 28035, // Helper->self, 20.0s cast, range 40 width 10 cross (in center)
    DevouringBrandLargeAOE = 26321, // Helper->self, no cast, range 40 width 10 cross (ones standing on cardinals)
    GreatWhirlwindSmall = 26323, // SunbirdSmall->self, 3.0s cast, range 60 circle (enrage)
    GreatWhirlwindLarge = 26325, // SunbirdLarge->self, 3.0s cast, range 60 circle (enrage)
    FlamesOfUndeath = 26326, // Boss->self, no cast, range 60 circle - aoe when small or big birds all die (?)
    JointPyre = 26329, // Sparkfledged->self, no cast, range 13 circle - aoe when big birds die too close to each other (?)
    JointPyreSunbirdL = 26328, // SunbirdLarge->self, no cast, range 13 circle, damage up buff
    Burst = 26341, // Sparkfledged->self, no cast, range 60 circle
    FireglideSweep = 26336, // SunbirdLarge->self, 11.0s cast, single-target
    FireglideSweepAOE = 26337, // SunbirdLarge->players, no cast, width 6 rect charge
    DeadRebirth = 26340, // Boss->self, 10.0s cast, range 60 circle
    AshenEye = 26342, // Sparkfledged->self, 4.0s cast, range 60 90-degree cone
    SparkFledgedVisual = 26284, // Boss->self, no cast, single-target
    FountainOfFire = 26343, // Boss->self, 6.0s cast, single-target
    FountainOfLife = 26344, // FountainOfFire->Boss, no cast, single-target
    FountainOfDeath = 26345, // FountainOfFire->player, no cast, single-target
    SunsPinion = 26346, // Boss->self, 6.0s cast, single-target
    SunsPinionAOE = 26347, // Helper->players, no cast, range 6 circle
    Fireglide = 26348, // Sunshadow->players, no cast, range 50 width 6 rect
    DeathToll = 26349, // Boss->Boss
    LifesAgonies = 26350, // Boss->Boss
    FirestormsOfAsphodelos = 26352, // Boss->self, 5.0s cast, range 60 circle
    FlamesOfAsphodelos = 26353, // Boss->self, 3.0s cast, single-target
    FlamesOfAsphodelosAOE1 = 26354, // Helper->self, 7.0s cast, range 60 60-degree cone
    FlamesOfAsphodelosAOE2 = 26355, // Helper->self, 8.0s cast, range 60 60-degree cone
    FlamesOfAsphodelosAOE3 = 26356, // Helper->self, 9.0s cast, range 60 60-degree cone
    StormsOfAsphodelos = 26357, // Boss->self, 8.0s cast, single-target
    WindsOfAsphodelos = 26358, // Helper->self, no cast, range 60 60-degree cone, some damage during storms
    BeaconsOfAsphodelos = 26359, // Helper->players, no cast, range 6 circle, some damage during storms
    DarkblazeTwister = 26360, // Boss->self, 4.0s cast, single-target
    DarkTwister = 26361, // DarkblazeTwister->self, 17.0s cast, range 60 circle, knockback 17
    BurningTwister = 26362, // DarkblazeTwister->self, 19.0s cast, range 7-20 donut
    TrailOfCondemnationCenter = 26363, // Boss->self, 6.0s cast, single-target (central aoe variant - spread)
    TrailOfCondemnationSides = 26364, // Boss->self, 6.0s cast, single-target (side aoe variant - stack in pairs)
    TrailOfCondemnationAOE = 26365, // Helper->self, 7.0s cast, range 40 width 15 rect (actual aoe that hits those who fail the mechanic)
    FlareOfCondemnation = 26366, // Helper->player, no cast, range 6 circle, apply fire resist debuff (spread variant)
    SparksOfCondemnation = 26367, // Helper->players, no cast, range 6 circle, apply fire resist debuff (pairs variant)
    HeatOfCondemnation = 26368, // Boss->self, 6.0s cast, single-target, tank tethers
    HeatOfCondemnationAOE = 26369, // Helper->players, no cast, range 6 circle, apply fire resist debuff
    RightCinderwing = 26370, // Boss->self, 5.0s cast, range 60 180-degree cone
    LeftCinderwing = 26371, // Boss->self, 5.0s cast, range 60 180-degree cone
    SearingBreeze = 26372, // Boss->self, 3.0s cast, single-target
    SearingBreezeAOE = 26373, // Helper->self, 3.0s cast, range 6 circle
    ScorchedExaltation = 26374, // Boss->self, 5.0s cast, range 60 circle
    FinalExaltation = 27691, // Boss->Boss
    BlazingRain = 26322, // Helper->self, no cast, range 60 circle
}

public enum SID : uint
{
    DeathsToll = 2762,
    Invincibility = 775, // none->DarkenedFire, extra=0x0
}

public enum TetherID : uint
{
    HeatOfCondemnation = 89, // player->Boss
    LargeBirdClose = 57, // player/SunbirdLarge/Sunshadow->player
    LargeBirdFar = 1, // player/SunbirdLarge/Sunshadow->player
    FountainOfFire = 27, // FountainOfFire->player/Boss
    BurningTwister = 167, // DarkblazeTwister->Boss
    DarkTwister = 168, // DarkblazeTwister->Boss
}

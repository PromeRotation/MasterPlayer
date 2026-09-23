namespace BossMod.Dawntrail.Raid.M02NHoneyBLovely;

public enum OID : uint
{
    Boss = 0x422A, // R5.004
    Tower1 = 0x1EBAA2, // R0.5
    Tower2 = 0x1EBAA3, // R0.5
    Groupbee = 0x422B, // R1.5
    PoisonCloud = 0x4230, // R1.0
    Sweetheart = 0x422C, // R1.0
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 37319, // Boss->player, no cast, single-target

    AlarmPheromones = 37245, // Boss->self, 3.0s cast, single-target

    BlindingLoveVisual1 = 37246, // Groupbee->self, 6.3+0.7s cast, single-target
    BlindingLoveVisual2 = 37247, // Groupbee->self, 6.3+0.7s cast, single-target
    BlindingLove1 = 39525, // Helper->self, 7.0s cast, range 50 width 8 rect
    BlindingLove2 = 39526, // Helper->self, 7.0s cast, range 50 width 8 rect

    BlowKiss = 37235, // Boss->self, 6.0s cast, range 40 120-degree cone
    CallMeHoney = 37220, // Boss->self, 5.0s cast, range 60 circle

    DropOfVenomVisual = 37226, // Boss->self, 3.0s cast, single-target
    DropOfVenom = 37232, // Helper->players, 7.0s cast, range 6 circle

    Fracture = 37240, // Helper->self, 8.0s cast, range 4 circle, tower
    BigBurst = 37241, // Helper->self, no cast, range 60 circle, tower fail

    Heartsick = 39821, // Helper->players, 7.0s cast, range 6 circle
    Heartsore = 37242, // Helper->player, 7.0s cast, range 6 circle

    HeartStruck1 = 37237, // Helper->location, 3.0s cast, range 4 circle
    HeartStruck2 = 37238, // Helper->location, 4.0s cast, range 6 circle
    HeartStruck3 = 37239, // Helper->location, 5.0s cast, range 10 circle

    HoneyBeelineVisual1 = 37221, // Boss->self, 5.5+0.7s cast, single-target
    HoneyBeelineVisual2 = 37227, // Boss->self, 5.5+0.7s cast, single-target
    HoneyBeeline1 = 39737, // Helper->self, 6.2s cast, range 60 width 14 rect
    HoneyBeeline2 = 39739, // Helper->self, 6.2s cast, range 60 width 14 rect

    HoneyBFinale = 37243, // Boss->self, 5.0s cast, range 60 circle

    HoneyBLiveVisual = 37234, // Boss->self, 2.0+5.0s cast, single-target
    HoneyBLive = 39550, // Helper->self, no cast, range 60 circle

    HoneyedBreezeVisual = 37223, // Boss->self, 4.0+1.0s cast, single-target
    HoneyedBreeze = 37224, // Helper->self, no cast, range 40 30-degree cone

    LoveMeTender = 37236, // Boss->self, 4.0s cast, single-target

    LoveseekerVisual = 39530, // Boss->self, 3.0+1.0s cast, single-target
    Loveseeker = 39617, // Helper->self, 4.0s cast, range 10 circle

    SplashOfVenomVisual = 37225, // Boss->self, 3.0s cast, single-target
    SplashOfVenom = 37231, // Helper->player, 7.0s cast, range 6 circle

    SplinterVisual1 = 37229, // PoisonCloud->location, no cast, single-target
    Splinter = 37230, // PoisonCloud->self, 4.0s cast, range 8 circle

    SweetheartTouch = 37244, // Sweetheart->player, no cast, single-target

    TemptingTwistVisual1 = 37222, // Boss->self, 5.5+0.7s cast, single-target
    TemptingTwistVisual2 = 37228, // Boss->self, 5.5+0.7s cast, single-target
    TemptingTwist1 = 39738, // Helper->self, 6.2s cast, range 7-30 donut
    TemptingTwist2 = 39740, // Helper->self, 6.2s cast, range 7-30 donut

    Teleport = 37219 // Boss->location, no cast, single-target
}

public enum SID : uint
{
    FatalAttraction = 3920, // none->player, extra=0x2162, 6s stun at 3 hearts
    Hearts0 = 3916, // none->player, extra=0x2D6, 0 hearts
    Infatuated = 3917, // none->player, extra=0x2D7, 1 heart
    HeadOverHeels = 3918, // none->player, extra=0x2D8, 2 hearts
    HopelessDevotion = 3919, // none->player, extra=0x2D9, 3 hearts
    LovelyPoison = 3915, // Boss->Boss, extra=0x31B
    TopOfTheHive = 4143, // none->Boss, extra=0x0
    Sweetheart = 2234, // none->Sweetheart, extra=0x19
}

public enum IconID : uint
{
    HoneyedBreezeTB = 230, // player
    DropOfVenom = 317, // player
    SplashOfVenom = 375, // player
    Heartsore = 515, // player
    Heartsick = 517 // player
}

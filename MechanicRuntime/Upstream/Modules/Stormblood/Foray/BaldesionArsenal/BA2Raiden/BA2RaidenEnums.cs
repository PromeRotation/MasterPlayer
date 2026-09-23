namespace BossMod.Stormblood.Foray.BaldesionArsenal.BA2Raiden;

public enum OID : uint
{
    Boss = 0x2605, // R5.4
    BallLightning = 0x2606, // R1.0
    StreakLightning = 0x2607, // R1.2
    Electricwall = 0x1EA1A1, // R2.0
    Helper = 0x261B
}

public enum AID : uint
{
    AutoAttack = 14777, // Boss->player, no cast, single-target

    SpiritsOfTheFallen = 14458, // Boss->self, 4.0s cast, range 35+R circle
    Shingan = 14459, // Boss->player, 4.0s cast, single-target
    Thundercall = 14463, // Boss->self, 3.0s cast, single-target, activates electrified wall
    AmeNoSakahokoVisual = 14440, // Boss->self, 4.5s cast, single-target
    AmeNoSakahoko = 14441, // Helper->self, 7.5s cast, range 25 circle
    WhirlingZantetsuken = 14442, // Boss->self, 5.5s cast, range 5-60 donut
    Shock = 14445, // BallLightning->self, 3.0s cast, range 8 circle
    LateralZantetsuken1 = 14443, // Boss->self, 6.5s cast, range 70+R width 39 rect
    LateralZantetsuken2 = 14444, // Boss->self, 6.5s cast, range 70+R width 39 rect
    LancingBolt = 14454, // Boss->self, 3.0s cast, single-target, apply spread markers
    LancingBlow = 14455, // StreakLightning->self, no cast, range 10 circle
    BoomingLament = 14461, // Boss->location, 4.0s cast, range 10 circle
    CloudToGroundVisual = 14448, // Boss->self, 4.0s cast, single-target
    CloudToGroundFirst = 14449, // Helper->self, 5.0s cast, range 6 circle
    CloudToGroundRest = 14450, // Helper->self, no cast, range 6 circle
    BitterBarbs = 14452, // Boss->self, 4.0s cast, single-target, chains
    Barbs = 14453, // Helper->self, no cast, ???
    SilentLevin = 14451, // Helper->location, 3.0s cast, range 5 circle
    LevinwhorlVisual = 14446, // Boss->self, 8.0s cast, single-target
    Levinwhorl = 14447, // Helper->self, 8.0s cast, range 80 circle
    ForHonor = 14460, // Boss->self, 4.5s cast, range 6+R circle
    UltimateZantetsuken = 14456, // Boss->self, 18.0s cast, range 80+R circle, enrage
    UltimateZantetsukenRepeat = 14457 // Boss->self, no cast, range 80+R circle
}

public enum IconID : uint
{
    Spreadmarker = 138, // player->self
}

public enum TetherID : uint
{
    Chains = 18, // player->player
}


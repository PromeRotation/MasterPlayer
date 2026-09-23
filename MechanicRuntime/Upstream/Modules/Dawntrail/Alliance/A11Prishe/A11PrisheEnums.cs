namespace BossMod.Dawntrail.Alliance.A11Prishe;

public enum OID : uint
{
    Boss = 0x4673, // R6.342
    LuminousRemnant = 0x4674, // R1.000-1.86
    Tower = 0x1EBCC9, // R0.5
    Helper3 = 0x4696, // R0.465
    Helper2 = 0x46CF, // R1.2
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 40934, // Boss->player, no cast, single-target
    Teleport = 40933, // Boss->location, no cast, single-target

    Banishga = 40935, // Boss->self, 5.0s cast, range 80 circle

    KnuckleSandwichVisual1 = 40936, // Boss->location, 12.0+1.0s cast, single-target
    KnuckleSandwichVisual2 = 40937, // Boss->location, 12.0+1.0s cast, single-target
    KnuckleSandwichVisual3 = 40938, // Boss->location, 12.0+1.0s cast, single-target
    KnuckleSandwich1 = 40939, // Helper->self, 13.0s cast, range 9 circle
    KnuckleSandwich2 = 40940, // Helper->self, 13.0s cast, range 18 circle
    KnuckleSandwich3 = 40941, // Helper->self, 13.0s cast, range 27 circle
    BrittleImpact1 = 40942, // Helper->self, 14.5s cast, range 9-60 donut
    BrittleImpact2 = 40943, // Helper->self, 14.5s cast, range 18-60 donut
    BrittleImpact3 = 40944, // Helper->self, 14.5s cast, range 27-60 donut

    NullifyingDropkickVisual = 40945, // Boss->players, 5.0+1.5s cast, range 6 circle
    NullifyingDropkick = 40957, // Helper->players, 6.5s cast, range 6 circle

    BanishStorm = 40946, // Boss->self, 4.0s cast, single-target, marching AOEs
    Banish = 40947, // Helper->self, no cast, range 6 circle

    HolyVisual = 40962, // Boss->self, 4.0+1.0s cast, single-target
    Holy = 40963, // Helper->players, 5.0s cast, range 6 circle, spread Markers

    CrystallineThorns = 40948, // Boss->self, 4.0+1.0s cast, single-target, arena change
    Thornbite = 40949, // Helper->self, no cast, range 80 circle

    AuroralUppercut1 = 40950, // Boss->self, 11.4+1,6s cast, single-target, knockback 12
    AuroralUppercut2 = 40951, // Helper->self, 11.4+1.6s cast, single-target, knockback 25
    AuroralUppercut3 = 40952, // Boss->self, 11.4+1.6s cast, single-target, knockback 38
    AuroralUppercut4 = 40953, // Helper->self, no cast, range 80 circle

    BanishgaIV = 40954, // Boss->self, 5.0s cast, range 80 circle, raidwide that spawns AOEs
    Explosion = 40955, // LuminousRemnant->self, 5.0s cast, range 8 circle

    AsuranFistsVisual = 40956, // Boss->self, 6.5+0.5s cast, single-target
    AsuranFists1 = 40958, // Helper->self, no cast, range 6 circle
    AsuranFists2 = 40959, // Helper->self, no cast, range 6 circle
    AsuranFists3 = 40960 // Helper->self, no cast, range 6 circle
}

public enum SID : uint
{
    Knockback = 3517 // Helper->player, extra=0x0
}

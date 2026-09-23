namespace BossMod.Stormblood.Foray.BaldesionArsenal.BA1Owain;

public enum OID : uint
{
    Boss = 0x265D, // R2.7
    Art = 0x2661, // R2.7
    Munderg = 0x265E, // R2.7
    IvoryPalm = 0x266B, // R2.0
    Helper1 = 0x265F,
    Helper2 = 0x2671,
    Helper3 = 0x2660
}

public enum AID : uint
{
    AutoAttack = 14679, // Boss->player, no cast, single-target
    ElementalShift1 = 14647, // Boss->self, 2.0s cast, single-target
    ElementalShift2 = 14649, // Boss->self, no cast, single-target

    AcallamNaSenorach = 14662, // Boss->self, 5.0s cast, range 60 circle, raidwide
    AcallamNaSenorachArt = 14628, // Boss->self, 7.0s cast, range 80 circle, enrage if Art side does not get pulled, Art teleports to Owain
    AcallamNaSenorachOwain = 14629, // Owain->self, 7.0s cast, range 80 circle

    Thricecull = 14661, // Boss->player, 5.0s cast, single-target, tankbuster

    Mythcall = 14646, // Boss->self, 2.0s cast, single-target
    ElementalMagicksVisual = 14648, // Helper3->self, no cast, single-target
    ElementalMagicksFireBoss = 14650, // Boss->self, 5.0s cast, range 13 circle
    ElementalMagicksFireSpears = 14652, // Munderg->self, no cast, range 13 circle
    ElementalMagicksIceBoss = 14651, // Boss->self, 5.0s cast, range 13 circle
    ElementalMagicksIceSpears = 14653, // Munderg->self, no cast, range 13 circle

    Spiritcull = 14654, // Boss->self, 3.0s cast, single-target
    LegendaryImbas = 14656, // Helper1/Helper2->self, 5.0s cast, ???
    PiercingLight1 = 14655, // Helper1/Helper2->player, 5.0s cast, range 6 circle
    PiercingLight2 = 14660, // Helper1/Helper2->player, 5.0s cast, range 6 circle
    Pitfall = 14669, // Boss->location, 5.0s cast, range 80 circle
    EurekanAero = 14657, // IvoryPalm->self, no cast, range 6 120-degree cone
    Explosion = 14658 // IvoryPalm->self, 6.0s cast, range 60 circle, mini enrage
}

public enum SID : uint
{
    SoulOfFire = 1783, // none->Munderg, extra=0x121
    SoulOfIce = 1784, // none->Munderg, extra=0x122
    BloodSacrifice = 1753, // none->player, extra=0x0
    Petrification = 610 // none->IvoryPalm, extra=0x0
}

public enum IconID : uint
{
    DoritoStack = 55 // player->self
}

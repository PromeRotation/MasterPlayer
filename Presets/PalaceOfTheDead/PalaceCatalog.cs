using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal static class PalaceCatalog
{
    internal static readonly uint[] Territories = [561, 562, 563, 564, 565,
        593, 594, 595, 596, 597, 598, 599, 600, 601, 602, 603, 604, 605, 606, 607];
    internal static readonly uint[] Interrupts = [6386, 6388, 6397, 7049, 7014, 7029, 7058];
    internal static readonly uint[] SpecialActions = [6351, 6356, 6953, 6365, 7090, 7103,
        7154, 7155, 7156, 7158, 7159, 7160, 7161, 7162, 7163, 7164, 7165, 7166, 7167, 7168, 7169, 7170, 7171, 7172, 7179];
    internal static bool OwnsTerritory(uint territory) => Territories.Contains(territory);
    internal static bool IsEarlyTerritory(uint territory) => territory is >= 561 and <= 565 or >= 593 and <= 602;
    internal static bool IsScript(string id) => id is "promeradar.potd1-150" or "promeradar.potd151-200";
    internal static bool IsBoss(uint oid) => oid is >= 0x181D and <= 0x1820;
    internal static bool IsBossAction(uint action) => action is 7090 or 7103 or >= 7154 and <= 7172;
    internal static bool IsEnemy(uint oid) => IsBoss(oid) || oid is >= 0x1890 and <= 0x18F3 || oid == 0x1842;
    internal static bool IsGaze(uint action) => action is 6351 or 6356 or 6953;
    internal static GeometryTemplate? SpecialShape(uint action) => action switch
    {
        7090 => new CircleTemplate(7),
        7158 or 7161 or 7169 => new CircleTemplate(8),
        7163 => new CircleTemplate(6),
        7162 => new FanTemplate(16.6f, MathF.PI * 2 / 3),
        7165 => new FanTemplate(51.6f, MathF.PI / 3),
        7170 => new CircleTemplate(7.2f),
        7172 => new CircleTemplate(6.6f),
        _ => null,
    };
}

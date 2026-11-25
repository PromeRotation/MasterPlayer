using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NoviceCatalog
{
    internal const uint TANK04 = 540;
    internal const uint DPS03 = 545;
    internal const uint DPS05 = 547;
    internal const uint BASIC_FINAL = 552;

    internal static bool IsTerritory(uint territory) => territory is TANK04 or DPS03 or DPS05 or BASIC_FINAL || NoviceTacticalCatalog.IsTerritory(territory);
    internal static bool Owns(uint territory, uint oid) => territory switch
    {
        TANK04 => oid == 0x1564,
        DPS03 => oid == 0x1571,
        DPS05 => oid == 0x157A,
        BASIC_FINAL => oid == 0x1591,
        1127 or 1128 or 1129 => oid == 0x463B,
        _ => false,
    };

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        TANK04 => [new(0x1564, "NoviceTank04", p => new CircleGeometry(new(0, p.Y, 0), 20), [], [])],
        DPS03 => [new(0x1571, "NoviceDPS03", p => new CircleGeometry(new(0, p.Y, 0), 20), [], [])],
        DPS05 => [new(0x157A, "NoviceDPS05", p => new CircleGeometry(new(0, p.Y, 0), 20), [], [])],
        BASIC_FINAL => [new(0x1591, "NoviceBasicFinal",
            p => new CircleGeometry(new(457.383f, p.Y, 271.721f), 20), [], [])],
        1127 or 1128 or 1129 => [new(0x463B, "NoviceTactical",
            p => new CircleGeometry(new(0, p.Y, 0), 20), NoviceTacticalCatalog.Rules(), [])],
        _ => [],
    };
}

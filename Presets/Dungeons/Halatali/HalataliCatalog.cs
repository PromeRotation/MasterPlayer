using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HalataliCatalog
{
    // CFC 7 -> TerritoryType 1245, verified against the installed CN client.
    internal const uint TERRITORY = 1245;
    internal const uint BURNING_WARD = 4175;

    internal static bool Owns(uint oid, uint territory)
        => territory == TERRITORY && oid is 0x4643 or 0x4644 or 0x4645;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x4643, "Firemane", p => HalataliArenas.Firemane(p.Y),
        [
            new(40592, 0x4643, "FireII", new CircleTemplate(5)),
            new(40588, 0x233C, "Fireflow", new FanTemplate(60, MathF.PI / 4)),
            new(40589, 0x233C, "Fireflow", new FanTemplate(60, MathF.PI / 4)),
            new(40591, 0x233C, "BurningBolt", null, "单体伤害"),
        ], [40587, 40590]),
        new(0x4644, "Thunderclap Guivre", p => HalataliArenas.Thunderclap(p.Y),
        [
            new(40595, 0x4644, "Electrify", new CircleTemplate(6)),
            new(41113, 0x233C, "HydroelectricShock", new HalataliShockTemplate()),
            new(40594, 0x4644, "Levinfang", null, "单体伤害"),
        ], [40593]),
        new(0x4645, "Tangata", p => HalataliArenas.Tangata(p.Y),
        [
            new(40602, 0x4645, "StraightPunch", null, "单体伤害"),
            new(40599, 0x4645, "PlainPound", new CircleTemplate(10)),
            new(40600, 0x233C, "Tremblor", new DonutTemplate(10, 20)),
            new(40601, 0x233C, "Earthquake", new DonutTemplate(20, 30)),
            new(41123, 0x4645, "Firewater", new CircleTemplate(3)),
        ], [40596, 40597, 40598]),
    ];
}

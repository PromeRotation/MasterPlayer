using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class YuweyawataCatalog
{
    internal const uint TERRITORY = 1242;
    internal static bool IsTrash(uint oid) => oid is 0x4612 or 0x46A4 or 0x4652 or 0x4616 or 0x4619 or 0x461B;
    internal static bool BoundedPull(uint oid) => oid is 0x46A4 or 0x4616 or 0x461B;
    internal static bool Owns(uint oid) => Create().Any(d => d.BossOid == oid || d.PrimaryAliases?.Contains(oid) == true);
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4612, "Forest Bat", p => YuweyawataArenas.Bat(p.Y),
            [new(40668, 0, "Soundwave", new FanTemplate(9, MathF.PI / 2)), new(40665, 0, "StraightSkewer", RayRectangleTemplate.Symmetric(4, 14))],
            [872], [0x46A3, 0x4613]),
        new(0x46A4, "Forest Woolback", p => YuweyawataArenas.Woolback(p.Y),
            [new(40668, 0, "Soundwave", new FanTemplate(9, MathF.PI / 2)), new(40666, 0, "Ram", new CircleTemplate(8))],
            [872], [0x4611, 0x4613, 0x46A3]),
        new(0x4641, "Lindblum Zaghnal", p => YuweyawataArenas.Zaghnal(p.Y),
        [
            new(40635, 0, "ElectricalOverload", null, "全屏伤害"),
            new(41267, 0, "SparkingFissure", null, "全屏伤害"), new(41258, 0, "SparkingFissure", null, "全屏伤害"),
            new(41121, 0, "LineVoltage", RayRectangleTemplate.Symmetric(10, 50)), new(40627, 0, "LineVoltage", RayRectangleTemplate.Symmetric(10, 50)),
            new(41122, 0, "LineVoltage", RayRectangleTemplate.Symmetric(5, 50)), new(40625, 0, "LineVoltage", RayRectangleTemplate.Symmetric(5, 50)),
            new(40626, 0, "CellShock", null), new(40638, 0, "LightningBolt", new CircleTemplate(6)),
            new(40637, 0, "LightningStorm", new CircleTemplate(5), PartyAoe: "分散"),
        ], [872, 40622, 40630, 41266, 40624, 40636, 40632, 40631, 40634]),
        new(0x4652, "Rotten Researcher", p => YuweyawataArenas.Researcher(p.Y), [],
            [870, 872, 40670], [0x4654, 0x4615, 0x4653, 0x46D4, 0x4614]),
        new(0x4616, "Station Specter", p => YuweyawataArenas.Specter(p.Y),
            [new(40671, 0, "SpookyTouch", new FanTemplate(7, MathF.PI * 2 / 3)), new(40672, 0, "SoulDrain", new CircleTemplate(6))],
            [41270, 872], [0x4653, 0x4654, 0x4615, 0x4617, 0x4655]),
        new(0x464A, "Overseer Kanilokka", p => YuweyawataArenas.KanilokkaStarting(p.Y),
        [
            new(40658, 0, "DarkSouls", null, "死刑"), new(40640, 0, "FreeSpirits", null, "全屏伤害"),
            new(40642, 0, "Soulweave", new DonutTemplate(28, 32)), new(40641, 0, "Soulweave", new DonutTemplate(28, 32)),
            new(40644, 0, "PhantomFlood", null), new(40656, 0, "DarkII", new FanTemplate(35, MathF.PI / 6)),
            new(40657, 0, "DarkII", new FanTemplate(35, MathF.PI / 6)),
            new(40649, 0, "TelltaleTears", new CircleTemplate(5), PartyAoe: "分散"),
            new(40645, 0, "LostHope", null), new(40646, 0, "Necrohazard", new CircleTemplate(18)),
            new(40647, 0, "Bloodburst", null, "全屏伤害"),
            new(40651, 0, "SoulDouse", new CircleTemplate(6), PartyAoe: "分摊"),
        ], [40659, 40639, 40643, 40654, 40655]),
        new(0x4619, "Sprightly Phoebad", p => YuweyawataArenas.Phoebad(p.Y),
            [new(41118, 0, "MountainBuster", new FanTemplate(12, MathF.PI * 2 / 3)), new(40676, 0, "Earthquake", new CircleTemplate(10))],
            [41119, 872, 40674], [0x461A, 0x461D, 0x461C]),
        new(0x461B, "Sprightly Clay Golem", p => YuweyawataArenas.Golem(p.Y),
            [new(40675, 0, "StoneSkull", new FanTemplate(15, MathF.PI * 2 / 3)), new(40676, 0, "Earthquake", new CircleTemplate(10))],
            [41119, 872], [0x46B1, 0x461D, 0x4618]),
        new(0x464C, "Lunipyati", p => YuweyawataArenas.LunipyatiStarting(p.Y),
        [
            new(40613, 0, "RagingClaw", new FanTemplate(45, MathF.PI)),
            new(40603, 0, "LeporineLoaf", null, "全屏伤害"), new(40662, 0, "LeapingEarthVisual", null),
            new(40661, 0, "LeapingEarthVisual", null), new(40606, 0, "LeapingEarth", null),
            new(40607, 0, "BoulderDance", new CircleTemplate(7)), new(40608, 0, "BoulderDance", new CircleTemplate(7)),
            new(40615, 0, "JaggedEdge", new CircleTemplate(6), PartyAoe: "分散"),
            new(40605, 0, "CraterCarve", new CircleTemplate(11)),
            new(40610, 0, "BeastlyRoar", new CircleTemplate(25), "全屏伤害"),
            new(40611, 0, "RockBlast", null), new(40616, 0, "TuraliStoneIV", new CircleTemplate(6), PartyAoe: "分摊"),
            new(40618, 0, "SonicHowl", null, "全屏伤害"), new(40619, 0, "Slabber", null, "死刑"),
        ], [40621, 40620, 40612, 40614, 40609, 40604]),
    ];
}

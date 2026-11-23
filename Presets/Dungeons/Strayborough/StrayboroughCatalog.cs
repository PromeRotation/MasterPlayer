using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class StrayboroughCatalog
{
    internal const uint TERRITORY = 1204;
    internal static bool Owns(uint oid) => oid is 0x4183 or 0x41CA or 0x421F;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4183, "His Royal Headness Leonogg I", p => new CircleGeometry(new(0, p.Y, 150), 19.5f),
        [
            new(36529, 0, "MaliciousMist", null, "全屏伤害"),
            new(36532, 0, "FallingNightmare", null), new(36536, 0, "FallingNightmare", null),
            new(39683, 0, "EvilScheme", null), new(39686, 0, "LoomingNightmare", null),
            new(36531, 0, "Scream", new FanTemplate(20, MathF.PI / 3)),
        ], [872, 36526, 36528, 36527, 36598, 36533, 36534, 36535, 39682, 39684, 39685, 39687, 36530, 36541]),
        new(0x41CA, "Jack-in-the-Pot", p => StrayboroughArenas.Jack(p.Y),
        [
            new(36720, 0, "TricksomeTreat", null),
            new(36726, 0, "LastDrop", null, "单体伤害"),
            new(36725, 0, "SordidSteam", null, "全屏伤害"),
        ], [37169, 36716, 36717, 36722, 36721, 36723, 36724]),
        new(0x421F, "Träumerei", p => new RectangleGeometry(new(148, p.Y, -433), 0, 39, 39),
        [
            new(37139, 0, "BitterRegret", RayRectangleTemplate.Symmetric(16, 50)),
            new(37147, 0, "BitterRegret", RayRectangleTemplate.Symmetric(12, 50)),
            new(37340, 0, "BitterRegret", RayRectangleTemplate.Symmetric(4, 40)),
            new(37133, 0, "Impact", RayRectangleTemplate.Symmetric(4, 40)),
            new(37146, 0, "Ghostduster", new CircleTemplate(8), PartyAoe: "分散"),
            new(37168, 0, "MaliciousMist", null, "全屏伤害"),
            new(37148, 0, "Fleshbuster", null),
        ], [16764, 37140, 37132, 37136, 37065, 39607, 37138, 37145, 37142, 37144, 37143]),
    ];
}

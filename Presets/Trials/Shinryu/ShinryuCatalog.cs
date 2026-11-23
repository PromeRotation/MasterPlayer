using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ShinryuCatalog
{
    internal const uint TERRITORY = 679;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1983, "Shinryu", p => new RectangleGeometry(new(0, p.Y, 0), 0, 40, 40),
        [
            new(8075, 0, "TidalWave", null),
            new(8091, 0, "LevinboltVisual", null),
            new(8107, 0, "HellfireVisual", null),
            new(8094, 0, "EarthBreath", new FanTemplate(60, MathF.PI / 3)),
            new(8100, 0, "AkhMorn", null),
            new(8097, 0, "Spikesicle", RayRectangleTemplate.Symmetric(10, 64.5f)),
            new(9291, 0, "MeteorImpact", new CircleTemplate(14)),
            new(8732, 0, "Fireball", new CircleTemplate(4)),
            new(8730, 0, "BlazingTrail", RayRectangleTemplate.Symmetric(11, 15)),
            new(8099, 0, "IceStorm", null, "全屏伤害"),
            new(8085, 0, "Protostar", null, "全屏伤害"),
            new(8088, 0, "DarkMatter", null, "全屏伤害"),
            new(8077, 0, "JudgmentBolt", null, "全屏伤害"),
            new(8079, 0, "EarthenFury", null, "全屏伤害"),
            new(8180, 0, "GyreCharge", RayRectangleTemplate.Symmetric(60, 100)),
            new(8083, 0, "TailSlap1", RayRectangleTemplate.Symmetric(20, 40)),
            new(9130, 0, "TailSlap2", RayRectangleTemplate.Symmetric(20, 40)),
            new(9456, 0, "Dragonfist", new CircleTemplate(16)),
            new(9146, 0, "EarthenFurySmash", RayRectangleTemplate.Symmetric(20, 20)),
            new(8984, 0, "SuperCyclone", null),
            new(8080, 0, "AerialBlast", null),
        ], [8105, 8106, 8092, 8101, 8095, 8096, 8076, 8074, 8086, 8514, 870, 8728, 8123,
            8488, 8104, 8081, 8084, 8142, 8082, 8098, 8144, 8143, 9455, 8078, 8109, 8731, 8729,
            8108, 8110, 8102, 8103, 8089, 8090, 8111, 8093])
    ];
}

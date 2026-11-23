using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HadesCatalog
{
    internal const uint TERRITORY = 847;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2949, "Hades", p => new CircleGeometry(new(100, p.Y, 100), 20),
        [
            new(16728, 0, "RavenousAssault", null),
            new(16715, 0, "BadFaith", RayRectangleTemplate.Symmetric(20, 20)),
            new(16716, 0, "BadFaith1", RayRectangleTemplate.Symmetric(20, 20)),
            new(16722, 0, "DarkEruptionCircle", new CircleTemplate(6)),
            new(16726, 0, "ShadowSpread", new FanTemplate(40, MathF.PI / 6)),
            new(16727, 0, "ShadowSpread3", new FanTemplate(40, MathF.PI / 6)),
            new(16718, 0, "BrokenFaith", new CircleTemplate(10)),
            new(17812, 0, "AncientWaterIII", null),
            new(17811, 0, "AncientDarkness", null),
            new(17813, 0, "AncientAero", RayRectangleTemplate.Symmetric(8, 40)),
            new(17815, 0, "AncientDarkIV", null, "全屏伤害"),
            new(16768, 0, "Titanomachy", null, "全屏伤害"),
            new(16732, 0, "ShadowStream", RayRectangleTemplate.Symmetric(16, 100)),
            new(16754, 0, "PolydegmonsPurgation", RayRectangleTemplate.Symmetric(16, 100)),
            new(16753, 0, "PolydegmonsPurgation1", RayRectangleTemplate.Symmetric(16, 100)),
            new(16738, 0, "DualStrike", null),
            new(16740, 0, "EchoOfTheLost", new FanTemplate(100, MathF.PI / 2)),
            new(16739, 0, "EchoOfTheLost1", new FanTemplate(100, MathF.PI / 2)),
            new(16751, 0, "HellbornYawp", null),
            new(16757, 0, "LifeInCaptivity", null, "全屏伤害"),
            new(16758, 0, "BlackCauldron", null, "全屏伤害"),
            new(16759, 0, "TheDarkDevours", null, "全屏伤害"),
            new(16748, 0, "ChorusOfTheLost", null, "全屏伤害"),
        ], [872, 16713, 16714, 16719, 16720, 16723, 16721, 16717, 16724, 16725, 17817, 17816,
            17814, 16764, 16737, 16752, 16750, 16744, 16745, 16767, 16746, 16747, 16749, 16755,
            17452, 16730, 16761, 16762])
    ];
}

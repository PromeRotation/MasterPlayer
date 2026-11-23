using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ValigarmandaCatalog
{
    internal const uint TERRITORY = 1195;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4115, "Valigarmanda", p => new RectangleGeometry(new Vector3(100, p.Y, 100), 0, 40, 30),
        [
            new(36158, 0, "SlitheringStrike", new FanTemplate(24, MathF.PI)),
            new(36160, 0, "StranglingCoil", null),
            new(36156, 0, "SusurrantBreath", null),
            new(36162, 0, "Skyruin1", null, "全屏伤害"),
            new(36163, 0, "Skyruin2", null, "全屏伤害"),
            new(36361, 0, "HailOfFeathers", null, "全屏伤害"),
            new(36165, 0, "DisasterZone1", null, "全屏伤害"),
            new(36167, 0, "DisasterZone2", null, "全屏伤害"),
            new(38545, 0, "RuinForetold", null, "全屏伤害"),
            new(36187, 0, "RuinfallTower", null),
            new(36189, 0, "RuinfallKB", null),
            new(39129, 0, "RuinfallAOE", new CircleTemplate(6)),
            new(36177, 0, "FreezingDust", null),
            new(36195, 0, "CalamitousEcho", new FanTemplate(40, 20 * (MathF.PI / 180))),
            new(36191, 0, "Eruption", new CircleTemplate(6)),
            new(36197, 0, "TulidisasterVisual", null),
            new(36176, 0, "ThunderousBreath", null),
            new(36172, 0, "BlightedBoltVisual", null),
            new(36174, 0, "BlightedBolt2", new CircleTemplate(7)),
            new(39001, 0, "ArcaneLightning", null),
            new(39265, 0, "ChillingCataclysm", null),
        ],
        [36159, 36157, 36155, 36161, 38338, 36175, 36170, 36171, 36173, 36164, 36166, 36186,
            36168, 36169, 39264, 36192, 36193, 26708, 34722, 36194, 38245, 38247, 38246,
            38323, 36199, 36200, 36198, 36190, 36184, 36185]),
    ];
}

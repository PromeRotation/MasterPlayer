using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class LakshmiCatalog
{
    internal const uint TERRITORY = 719;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1E20, "Lakshmi", p => new CircleGeometry(new(0, p.Y, 0), 20),
        [
            new(9612, 0, "TailSlap", new FanTemplate(7, MathF.Tau / 3)),
            new(9613, 0, "InnerDemons", null, "背对视线"),
            new(9349, 0, "DivineDenial", null),
            new(9347, 0, "Stotram", null, "全屏伤害"),
            new(9374, 0, "StotramShielding", null, "全屏伤害"),
            new(9026, 0, "Jagadishwari", null, "全屏伤害"),
            new(9361, 0, "ThePallOfLight", null),
            new(9362, 0, "ThePullOfLight1", null, "死刑"),
            new(9363, 0, "ThePullOfLight2", null, "死刑"),
        ], [8535, 9357, 9352, 9358, 9366, 9353, 9354, 9355, 9356, 9364, 9365, 9348, 9350, 9351, 9360, 9359, 9377, 9305, 9306])
    ];
}

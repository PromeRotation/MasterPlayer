using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NecronCatalog
{
    internal const uint TERRITORY = 1295;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4870, "Necron", p => new RectangleGeometry(new(100, p.Y, 100), 0, 36, 30),
        [
            new(44521, 0, "FearOfDeath", null, "全屏伤害"),
            new(44533, 0, "GrandCross", null, "全屏伤害"),
            new(44535, 0, "GrandCrossProximity", RayRectangleTemplate.Symmetric(10, 100), "全屏伤害"),
            new(44540, 0, "FearOfDeathAOE2", new CircleTemplate(3)),
            new(44536, 0, "GrandCrossBait", new CircleTemplate(3)),
            new(44522, 0, "FearOfDeathAOE1", null),
            new(44523, 0, "ChokingGrasp", RayRectangleTemplate.Symmetric(6, 24)),
            new(44532, 0, "MementoMori", RayRectangleTemplate.Symmetric(12, 37)),
            new(44524, 0, "ColdGripVisual1", null),
            new(44525, 0, "ColdGripVisual2", null),
            new(44611, 0, "ColdGrip", RayRectangleTemplate.Symmetric(12, 30)),
            new(44526, 0, "ExistentialDread", null),
            new(44527, 0, "SoulReaping", null),
            new(44531, 0, "RelentlessReaping", null),
            new(44528, 0, "Aetherblight", null),
            new(45181, 0, "AetherblightCircle", null),
            new(45182, 0, "AetherblightDonut", null),
            new(44603, 0, "ArenaChangeVisual", null),
            new(44538, 0, "NeutronRingVisual", null),
            new(44541, 0, "DarknessOfEternityVisual", null),
            new(44534, 0, "GrandCrossRect", RayRectangleTemplate.Symmetric(4, 100)),
            new(44817, 0, "Invitation", null),
            new(44549, 0, "SpreadingFear", null, "手掌即将狂暴"),
        ],
        [44614, 44546, 44547, 44609, 45166, 44530, 44529, 44539, 44542, 44544, 44605, 44545, 44548]),
    ];
}

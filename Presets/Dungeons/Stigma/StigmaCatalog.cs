using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class StigmaCatalog
{
    internal const uint TERRITORY = 986;
    internal static bool Owns(uint oid) => oid is 0x3417 or 0x3416 or 0x3428;
    internal static bool IsCharge(uint action) => action is 25516 or 25517 or 25518 or 25645;
    internal static bool IsMarchStatus(uint status) => status is 1958 or 1959 or 1960 or 1961 or 1257 or 3629;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3417, "ProtoOmega", p => new RectangleGeometry(new(-144, p.Y, -136), 0, 40, 40),
        [
            new(25386, 0x3417, "ElectricSlide", new CircleTemplate(6), PartyAoe: "分摊"),
            new(25383, 0x3418, "IronKiss", new CircleTemplate(3)),
            new(25376, 0x3417, "SideCannonsLeft", new FanTemplate(60, MathF.PI)),
            new(25377, 0x3417, "SideCannonsRight", new FanTemplate(60, MathF.PI)),
            new(25387, 0x3417, "MustardBomb", null, "死刑"),
        ], [872]),
        new(0x3416, "ArchLambda", p => new CircleGeometry(new(0, p.Y, 0), 20),
        [
            new(25516, 0x3416, "TreadFirst", null),
            new(25517, 0x3416, "TreadNext", null),
            new(25518, 0x233C, "TreadPreview", null),
            new(25522, 0x233C, "AutoMobileSniperCannon", RayRectangleTemplate.Symmetric(6, 40)),
            new(25521, 0x233C, "Entrench", RayRectangleTemplate.Symmetric(8, 41)),
            new(25519, 0x3416, "WaveCannon", new FanTemplate(40, MathF.PI)),
            new(25524, 0x3416, "AtomicFlame", null, "全屏伤害"),
            new(25525, 0x3416, "Wheel", null, "死刑"),
        ], [872, 25515, 25520, 25514]),
        new(0x3428, "Stigma4", p => new RectangleGeometry(new(0, p.Y, 2.5f), 0, 40, 40),
        [
            new(25650, 0x233C, "ElectromagneticReleaseIn", new DonutTemplate(7, 60)),
            new(25652, 0x233C, "ElectromagneticReleaseOut", new CircleTemplate(16)),
            new(25646, 0x342B, "FireBreath", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(26873, 0x342B, "Touchdown", new CircleTemplate(7)),
            new(25642, 0x342A, "ProtoWaveCannonLeft", new FanTemplate(60, MathF.PI)),
            new(25643, 0x342A, "ProtoWaveCannonRight", new FanTemplate(60, MathF.PI)),
            new(25645, 0x3429, "Rush", null),
            new(25644, 0x342A, "SelfDestructOmega", null, "全屏伤害"),
            new(25647, 0x342B, "SelfDestructDragon", null, "全屏伤害"),
            new(25654, 0x3428, "AtomicRay", null, "全屏伤害"),
        ], [25641, 25649, 25651, 27723]),
    ];
}

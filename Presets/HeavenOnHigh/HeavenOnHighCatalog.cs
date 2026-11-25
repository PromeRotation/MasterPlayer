using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HeavenOnHighCatalog
{
    internal const uint FIRST = 770;
    internal const uint SECOND = 771;
    internal const uint THIRD = 772;
    internal const uint FOURTH = 782;
    internal const uint FIFTH = 773;
    internal const uint SIXTH = 783;
    internal const uint SEVENTH = 774;
    internal const uint EIGHTH = 784;
    internal const uint NINTH = 775;

    internal static bool IsTerritory(uint territory) => territory is FIRST or SECOND or THIRD or FOURTH or FIFTH or SIXTH or SEVENTH or EIGHTH or NINTH;
    internal static bool Owns(uint territory, uint oid) => territory switch
    {
        FIRST => oid == 0x23E1,
        SECOND => oid == 0x23E7,
        THIRD => oid == 0x2251,
        FOURTH => oid == 0x23E2,
        FIFTH => oid == 0x23E9,
        SIXTH => oid == 0x23E4,
        SEVENTH => oid == 0x23EB,
        EIGHTH => oid == 0x23EC,
        NINTH => oid == 0x23EF,
        _ => false,
    };
    internal static IGeometryShape Arena(uint territory, float y) => territory switch
    {
        NINTH => HeavenOnHighArenas.Ninth(y),
        FIFTH or SIXTH or SEVENTH or EIGHTH => HeavenOnHighArenas.Second(y),
        _ => HeavenOnHighArenas.First(y),
    };
    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        FIRST => [new(0x23E1, "亡者船", p => Arena(territory, p.Y),
        [
            new(11878, 0, "Concussive Oscillation", new CircleTemplate(7)),
            new(11877, 0, "Overtow", null),
            new(11876, 0, "Amorphous Applause", new FanTemplate(27.4f, MathF.PI)),
        ], [])],
        SECOND => [new(0x23E7, "别蝶", p => Arena(territory, p.Y),
        [
            new(11899, 0, "Psycho Squama", new FanTemplate(53, MathF.PI / 2)),
            new(11900, 0, "Neuro Squama", null),
            new(11901, 0, "Fragility", new CircleTemplate(8)),
        ], [])],
        THIRD => [new(0x2251, "蝶化身", p => Arena(territory, p.Y),
        [
            new(11293, 0, "Supercell", RayRectangleTemplate.Symmetric(100, 55.25f)),
            new(11291, 0, "Shiko", new CircleTemplate(6)),
            new(11292, 0, "Lightning Strike", RayRectangleTemplate.Symmetric(6, 55.25f)),
            new(11294, 0, "Lightning Bolt", new DonutTemplate(1, 8)),
        ], [])],
        FOURTH => [new(0x23E2, "怖军", p => Arena(territory, p.Y),
        [
            new(11905, 0, "Ancient Aero", RayRectangleTemplate.Symmetric(8, 52.4f)),
            new(11903, 0, "Ancient Aero II", new CircleTemplate(6)),
            new(11904, 0, "Ancient Aero III", null),
            new(11902, 0, "Tornado", null),
        ], [])],
        FIFTH => [new(0x23E9, "牛头", p => Arena(territory, p.Y),
        [
            new(11922, 0, "Eye of the Fire", null),
            new(11919, 0, "Rusting Claw", new FanTemplate(11.45f, 2 * MathF.PI / 3)),
            new(11921, 0, "The Spin", new CircleTemplate(20)),
            new(11920, 0, "Words of Woe", RayRectangleTemplate.Symmetric(6, 48.45f)),
        ], [])],
        SIXTH => [new(0x23E4, "忍冬", p => Arena(territory, p.Y),
        [
            new(11925, 0, "Firewalker", new FanTemplate(10, MathF.PI / 2)),
            new(11930, 0, "Infinite Anguish", null),
            new(11927, 0, "Fire II", new CircleTemplate(5)),
            new(11926, 0, "Topple", new CircleTemplate(5.5f)),
            new(11929, 0, "Searing Chain", RayRectangleTemplate.Symmetric(4, 61)),
            new(11928, 0, "Ancient Flare", null, "全屏伤害"),
        ], [])],
        SEVENTH => [new(0x23EB, "犬蛊", p => Arena(territory, p.Y),
        [
            new(12205, 0, "Predator Claws", new FanTemplate(15, 2 * MathF.PI / 3)),
            new(12203, 0, "Slabber", new CircleTemplate(8)),
            new(12207, 0, "Innerspace", null),
            new(12208, 0, "Ululation", null, "全屏伤害"),
            new(12206, 0, "Hound out of Hell", null),
        ], [])],
        EIGHTH => [new(0x23EC, "锻冶媪", p => Arena(territory, p.Y),
        [
            new(11985, 0, "Heavensward Howl", new FanTemplate(12.5f, 2 * MathF.PI / 3)),
            new(11990, 0, "Pillar Impact", new CircleTemplate(6.5f)),
            new(11989, 0, "Pillar Pierce", RayRectangleTemplate.Symmetric(4, 82)),
            new(11987, 0, "Lunar Cry", null, "全屏伤害"),
        ], [])],
        NINTH => [new(0x23EF, "温罗", p => Arena(territory, p.Y),
        [
            new(12210, 0, "Subduction", null),
            new(12213, 0, "Burning Rave", new CircleTemplate(8)),
            new(12214, 0, "Ancient Quaga", null, "全屏伤害"),
            new(12212, 0, "Meteor Impact", new CircleTemplate(20)),
            new(12216, 0, "Knuckle Press", new CircleTemplate(9)),
            new(12215, 0, "Aura Cannon", RayRectangleTemplate.Symmetric(10, 63)),
        ], [])],
        _ => [],
    };
}

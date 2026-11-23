using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class BardamCatalog
{
    internal const uint TERRITORY = 1143;
    internal static bool Owns(uint oid) => oid is 0x1A9E or 0x1AA5 or 0x1AA6;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1A9E, "Garula", p => BardamArenas.Garula(p.Y),
        [
            new(7927, 0x1A9E, "Heave", new FanTemplate(13, MathF.PI * 2 / 3)),
            new(7955, 0x19A, "CrumblingCrust", new CircleTemplate(3)),
            new(7929, 0x1A9E, "Rush", null, "拉长连线并避免冲锋覆盖队友"),
            new(9394, 0x1A9F, "Lullaby", new CircleTemplate(3.7f)),
            new(7932, 0x1AA1, "RushYamaa", null),
            new(7932, 0x1AA0, "RushYamaa", null),
            new(9395, 0x1AA2, "WideBlaster", new FanTemplate(29.15f, MathF.PI * 2 / 3)),
        ], [872, 7928, 7930, 7931]),
        new(0x1AA5, "Hunter of Bardam", p => BardamArenas.Hunter(p.Y),
        [
            new(7940, 0x1AA5, "EmptyGaze", null, "背对视线"),
            new(9599, 0x1F49, "Charge", RayRectangleTemplate.Symmetric(5, 46.25f)),
            new(7937, 0x18D6, "Sacrifice", null, "每塔一人"),
            new(9597, 0x18D6, "CometFirst", new CircleTemplate(4)),
            new(9598, 0x18D6, "CometRest", new CircleTemplate(4)),
            new(9591, 0x1AA5, "HeavyStrike", new FanTemplate(6.5f, MathF.PI * 1.5f)),
            new(9591, 0x1AA4, "HeavyStrike", new FanTemplate(6.5f, MathF.PI * 1.5f)),
            new(9592, 0x18D6, "HeavyStrike1", new FanTemplate(6.5f, MathF.PI * 1.5f)),
            new(9593, 0x18D6, "HeavyStrike2", new FanTemplate(12.5f, MathF.PI * 1.5f)),
            new(9594, 0x18D6, "HeavyStrike3", new FanTemplate(18.5f, MathF.PI * 1.5f)),
            new(9600, 0x1F4A, "CometImpact", new CircleTemplate(9)),
            new(7934, 0x18D6, "Reconstruct", new CircleTemplate(5)),
            new(9596, 0x18D6, "Tremblor1", null),
            new(9595, 0x18D6, "Tremblor2", null),
            new(9602, 0x1F4D, "MeteorImpact", null, "躲到唯一活星片后"),
        ], [9450, 9451, 9611, 7944, 7935, 7938, 9601, 7933, 9605]),
        new(0x1AA6, "Yol", p => BardamArenas.Yol(p.Y),
        [
            new(7946, 0x1AA6, "WindUnbound", null, "全屏伤害"),
            new(7953, 0x1AA8, "Pinion", RayRectangleTemplate.Symmetric(2, 40.5f)),
            new(7947, 0x1AA6, "FlutterfallSpreadVisual", null),
            new(7954, 0x19A, "Flutterfall", new CircleTemplate(6)),
            new(7949, 0x1AA6, "EyeOfTheFierce", null, "背对视线"),
        ], [871, 870, 7945, 7948, 7952, 7950, 7951]),
    ];
}

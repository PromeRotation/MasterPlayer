using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class BrayfloxCatalog
{
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1A7, "Great Yellow Pelican", p => BrayfloxArenas.Pelican(p.Y),
        [
            new(506, 0x1A7, "NumbingBreath", new FanTemplate(9.2f, MathF.PI * 2 / 3)),
        ], []),
        new(0x3DE, "Inferno Drake", p => BrayfloxArenas.Drake(p.Y), [], [984]),
        new(0x1AB, "Hellbender", p => BrayfloxArenas.Hellbender(p.Y),
        [
            new(980, 0x1AB, "BogBubble", new CircleTemplate(6)),
            new(982, 0x1AB, "PeculiarLight", new CircleTemplate(8)),
            new(448, 0x1AB, "StagnantSpray", new FanTemplate(8, MathF.PI * 2 / 3)),
        ], []),
        new(0x38C5, "Aiatar", p => BrayfloxArenas.Aiatar(p.Y),
        [
            new(28659, 0x38C5, "SalivousSnap", null, "死刑"),
            new(28657, 0x233C, "ToxicVomit", new CircleTemplate(2)),
            new(28658, 0x233C, "Burst", new CircleTemplate(10)),
            new(28660, 0x38C5, "DragonBreath", RayRectangleTemplate.Symmetric(8, 30)),
        ], [28656]),
    ];
}

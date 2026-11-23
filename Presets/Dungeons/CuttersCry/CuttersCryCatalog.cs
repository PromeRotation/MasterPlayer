using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CuttersCryCatalog
{
    internal const uint TERRITORY = 1303;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x48FB, "Myrmidon Princess", p => CuttersCryArenas.Princess(p.Y),
        [
            new(44227, 0x233C, "StoneII", new CircleTemplate(5)),
            new(44228, 0x48FB, "Silence", null, "打断沉默"),
            new(44229, 0x48FB, "MandibleBite", new FanTemplate(9, MathF.PI / 2)),
        ], [44226, 44231, 44230]),
        new(0x48FF, "Giant Tunnel Worm", p => CuttersCryArenas.Worm(p.Y), [],
            [44235, 44236, 44232, 44233, 44234]),
        new(0x4900, "Chimera", p => CuttersCryArenas.Chimera(p.Y),
        [
            new(44243, 0x4900, "TheDragonsBreath", new FanTemplate(9.7f, MathF.PI * 2 / 3)),
            new(44242, 0x4900, "TheRamsBreath", new FanTemplate(9.7f, MathF.PI * 2 / 3)),
            new(44237, 0x4900, "TheRamsVoice", new CircleTemplate(9.7f)),
            new(44238, 0x4900, "TheDragonsVoice", new DonutTemplate(8, 30)),
            new(44245, 0x233C, "LightningStorm", new CircleTemplate(5)),
        ], [44241, 44239, 44240, 44244]),
    ];
}

using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal static class PalaceTrashSpell
{
    // AIHintsBuilder.GuessShape, including its fixed donut hole and zero-length
    // charge descriptor. The charge's actual endpoints are resolved every frame.
    internal static GeometryTemplate? Shape(byte castType, float range, float width, float hitbox, string? omenPath)
    {
        var angle = MathF.PI;
        var pos = omenPath?.IndexOf("fan", StringComparison.Ordinal) ?? -1;
        if (pos >= 0 && pos + 6 <= omenPath!.Length && int.TryParse(omenPath.AsSpan(pos + 3, 3), out var degrees))
            angle = degrees * (MathF.PI / 180f);
        return castType switch
        {
            2 => new CircleTemplate(range),
            3 => new FanTemplate(range + hitbox, angle),
            4 => RayRectangleTemplate.Symmetric(width, range + hitbox),
            5 => new CircleTemplate(range + hitbox),
            8 => RayRectangleTemplate.Symmetric(width, 0),
            10 => new DonutTemplate(3, range),
            11 => new DungeonCrossTemplate(range, width * .5f),
            12 => RayRectangleTemplate.Symmetric(width, range),
            13 => new FanTemplate(range, angle),
            _ => null,
        };
    }
}

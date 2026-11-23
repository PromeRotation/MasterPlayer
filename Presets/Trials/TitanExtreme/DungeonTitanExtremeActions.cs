using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private int TitanExPriority(DungeonActor actor)
    {
        if (!TitanExCombat) return 0;
        return actor.Oid switch
        {
            0xF8 or 0x5E5 => 1,
            0x8F2 => 2,
            0x5E0 => titanExKillBomb && Vector2.DistanceSquared(new(actor.Position.X, actor.Position.Z), Vector2.Zero) <= 1 ? 3 : 0,
            0x5E2 => TitanExBoss is { } boss
                && Vector2.DistanceSquared(new(actor.Position.X, actor.Position.Z), new(boss.Position.X, boss.Position.Z)) <= 25 ? 5 : 4,
            _ => 0,
        };
    }

}

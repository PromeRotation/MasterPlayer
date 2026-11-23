using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    // DrawEnemies 归属跟随当前 Boss，避免跨场景的道中敌人进入该模块的轮廓输出。
    private IEnumerable<DungeonAmbientAoe> EarlyArrEnemyOutlines()
    {
        if (Active == null || territory is not (1036 or 1037 or 1038)) yield break;
        foreach (var actor in _actors.Values)
        {
            var visible = actor.Id == PrimaryId || Active.BossOid switch
            {
                0x1A1 => actor.Oid == 0x1A2,
                0x4C => actor.Oid is 0x7D or 0x7F or 0x7E,
                _ => false,
            };
            if (!visible || actor.Dead || !float.IsFinite(actor.HitboxRadius) || actor.HitboxRadius <= 0) continue;
            yield return new($"early-arr/enemy/{actor.Id}", "敌人轮廓", new CircleTemplate(actor.HitboxRadius),
                actor.Position, actor.Rotation, Navigate: false, OutlineOnly: true, Unclipped: true,
                Color: new Vector4(1, 0, 0, 1));
        }
    }
}

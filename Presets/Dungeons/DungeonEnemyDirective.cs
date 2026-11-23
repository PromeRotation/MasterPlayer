using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

// 每帧重建，位置与朝向属于敌人；玩家朝向仍由凝视控制独立处理。
internal sealed record DungeonEnemyDirective(DungeonActor Actor, Vector3 Position, float Rotation,
    bool ShouldBeTanked, bool PreferProvoking = false, float TankDistance = 2,
    float AttackStrength = .05f, bool StayAtLongRange = false);

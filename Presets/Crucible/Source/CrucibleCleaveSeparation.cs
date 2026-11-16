namespace MasterPlayer.Mechanics.Crucible;

// kano 11-14：低3 加特勒、高2 劳妲的平 A 是范围攻击，人不能和宠物站一起。
// 劳妲平 A 50860：CT13、R9、瞬发，朝当前目标的正前方扇形（run2392 封存 ARR：43 下里 4 下同时打到两个目标；
// 56° 以外、9 米内的其它单位一次都没被带到，所以半角取 45°+10° 余量）。加特勒没有封存样本，先按同样处理。
// 只让人物让开，宠物位置不动：
//  - Boss 在打人物：扇形朝人物，宠物在 Boss→人物方向的半角内且在 9 米内就会被带到 → 禁止人物站在 Boss→宠物方向的扇形里；
//  - Boss 在打宠物：扇形朝宠物 → 禁止人物站在 Boss→宠物方向、R9 的扇形里。
sealed class CrucibleCleaveSeparation(BossModule module) : BossComponent(module)
{
    public const float Radius = 9f, Margin = 1f, FarRadius = 40f;
    public static readonly Angle HalfAngle = 55f.Degrees();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (Zone(actor) is { } zone)
            hints.AddForbiddenZone(zone, WorldState.FutureTime(1.5));
    }

    internal ShapeDistance? Zone(Actor actor)
    {
        var boss = Module.PrimaryActor;
        if (boss.IsDeadOrDestroyed || !boss.InCombat || actor.IsDeadOrDestroyed) return null;
        var pet = WorldState.Actors.FirstOrDefault(a => a != actor && a.OwnerID == actor.InstanceID && !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0);
        if (pet == null) return null;
        var toPet = pet.Position - boss.Position;
        var reach = Radius + Margin + pet.HitboxRadius;
        if (toPet.LengthSq() < 0.25f || toPet.LengthSq() > reach * reach) return null;
        var direction = Angle.FromDirection(toPet);
        if (boss.TargetID == actor.InstanceID)
            return new AOEShapeCone(FarRadius, HalfAngle).Distance(boss.Position, direction);
        if (boss.TargetID == pet.InstanceID)
            return new AOEShapeCone(reach, HalfAngle).Distance(boss.Position, direction);
        return null;
    }
}

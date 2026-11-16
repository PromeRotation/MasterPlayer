// See docs/reference/CrucibleAll.spec.md.
// run2278 劳妲两遍：霹雳(49470, 50x3 直线, 从劳妲指向读条目标=玩家) 读完时宠物站在劳妲身前同侧，
// 两遍都被这一下打死 18935 胡蜂(3459→0 @03:54:41, 2164→0 @03:57:14)，第一下也把开场宠 18959 打掉 2063。
// 宠物死后到下一只出场 6~8s，补的 18925 只剩 2336 血，几秒内被劳妲平A打死 → 后半段无宠，DPS 从 ~1550/s 掉到 ~600/s。
// 读条期间把宠物移出直线：沿垂直方向站到劳妲侧面（离线 ≥6y），保持在主平台上。
using System.Runtime.CompilerServices;

namespace MasterPlayer.Mechanics.Crucible;

[SkipLocalsInit]
sealed class CrucibleLaudaThunderPetDodge(BossModule module) : BossComponent(module)
{
    private const uint Thunder = 49470;
    private const float HalfWidth = 3f;
    private const float Clear = 6.5f;
    private readonly List<(Actor Caster, ulong Target, DateTime End)> _casts = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != Thunder || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        _casts.RemoveAll(c => c.Caster == caster);
        _casts.Add((caster, spell.TargetID, Module.CastFinishAt(spell, 0.8f)));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == Thunder)
            _casts.RemoveAll(c => c.Caster == caster);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == Thunder && spell.Interrupted)
            _casts.RemoveAll(c => c.Caster == caster);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _casts.RemoveAll(c => c.End <= WorldState.CurrentTime || c.Caster.IsDeadOrDestroyed);
        if (_casts.Count == 0 || hints.PetMoveTo != null)
            return;
        var (caster, targetId, _) = _casts[0];
        var target = WorldState.Actors.Find(targetId) ?? actor;
        var dirVec = target.Position - caster.Position;
        if (dirVec.LengthSq() < 0.01f)
            return;
        var dir = dirVec.Normalized();
        var perp = dir.OrthoL();
        foreach (var pet in CrucibleNightmare.Pets(WorldState, actor))
        {
            if (targetId == pet.InstanceID)
                continue; // 宠物自己被点名时躲不开
            var rel = pet.Position - caster.Position;
            var along = rel.Dot(dir);
            var side = rel.Dot(perp);
            if (along < -2f || MathF.Abs(side) >= HalfWidth + 1.5f)
                continue;
            var sign = side >= 0 ? 1f : -1f;
            WPos? pick = null;
            foreach (var s in new[] { sign, -sign })
            {
                var p = caster.Position + perp * (s * Clear) + dir * -1f;
                if (CrucibleLaudaFloor.Contains(p)) { pick = p; break; }
            }
            if (pick != null)
                hints.PetMoveTo = pick;
            return;
        }
    }
}

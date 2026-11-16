// See docs/reference/CrucibleAll.spec.md.
// 高2 眼王(B44) 死亡收割者(19739) + 死亡沙漏(19737) + 指针(19738)。
// v2（run2287 后，ACT 2282/2283/2286/2287 共 11 个沙漏复核）：
//  - 指针出生朝向 = 沙漏方向；之后每 ~1.556s 转 -90°（模型朝向的变化比 C10D 49421 的事件晚约 0.49s），
//    所以是第 4、8… 次转动后重新指向沙漏，不是 v1 以为的第 1、5、9 跳。
//  - 沙漏 HP0 → C10C(49420) 中断 → ~0.3s 后 即死 C10E(49422)，方向 = 指针【当前】朝向；
//    指向沙漏时站在沙漏边的人吃 死亡宣告 6s 必死（run2287 07:27:24.64，指向沙漏后 0.8s 打死）。
//  - v1 的禁区 activation 设在第 5 跳之后，MP 报「晚走路线来不及」，人没走开。
// v2：直接读指针模型朝向（同 CrucibleEyeRoulette）。指针正指向沙漏、或下一次转动就会指向沙漏且本格已过 0.95s 时，
//   收割者→沙漏方向的直线 (30m, ±5m) 立即禁止站人；沙漏 PriorityForbidden。ACR 侧另有 BSTEyeReaperGuard 停手。
using System.Runtime.CompilerServices;

namespace MasterPlayer.Mechanics.Crucible;

[SkipLocalsInit]
sealed class CrucibleEyeReaperClock(BossModule module) : BossComponent(module)
{
    private const uint GlassOid = 19737, ReaperOid = 19739, HandOid = 19738;
    private const uint Doom = 49422;
    private const double TurnSeconds = 1.556, NextLead = 0.95;
    private const float LaneHalfWidth = 5f, LaneLength = 30f;

    private Actor? _glass, _reaper, _hand;
    private Angle _rotation;
    private DateTime _changedAt;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == GlassOid)
            _glass = actor;
        else if (actor.OID == ReaperOid)
            _reaper = actor;
        else if (actor.OID == HandOid)
        {
            _hand = actor;
            _rotation = actor.Rotation;
            _changedAt = default;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == Doom)
            _glass = null;
    }

    public override void Update()
    {
        if (_hand is not { IsDestroyed: false } hand)
            return;
        if ((hand.Rotation - _rotation).Normalized().Abs().Deg > 30)
        {
            _rotation = hand.Rotation;
            _changedAt = WorldState.CurrentTime;
        }
    }

    private WPos Origin => _reaper is { IsDestroyed: false } r ? r.Position : _hand is { IsDestroyed: false } h ? h.Position : Module.Center;

    // 返回禁区结束时刻；default = 现在不危险。
    private DateTime DangerUntil()
    {
        if (_glass is not { IsDeadOrDestroyed: false } glass || glass.HPMP.CurHP == 0 || _hand is not { IsDestroyed: false } hand)
            return default;
        var dir = glass.Position - hand.Position;
        if (dir.LengthSq() < 1)
            return default;
        var toGlass = Angle.FromDirection(dir);
        bool Points(Angle a) => (a - toGlass).Normalized().Abs().Deg < 35;
        var now = WorldState.CurrentTime;
        if (_changedAt == default) // 指针还没开始转（出生时就指向沙漏，沙漏此时打不到）
            return default;
        var elapsed = (now - _changedAt).TotalSeconds;
        if (Points(hand.Rotation))
            return _changedAt.AddSeconds(TurnSeconds + 1.0);
        if (Points(hand.Rotation - 90f.Degrees()) && elapsed >= NextLead && elapsed < TurnSeconds + 0.8)
            return _changedAt.AddSeconds(2 * TurnSeconds + 1.0);
        return default;
    }

    // run2312 第一试：人物在「下一格转向」开头就离开了沙漏，盗龙 1.7s 后照样出手，把 239 血的沙漏打死在指向格里。
    // 魔兽换目标比人物晚（10 局 377 次：中位 2.3s，90% 分位 5.7s），靠切目标管不住。
    // 沙漏活着就把魔兽叫到场心另一侧（离沙漏约 19m），够不着；沙漏只由人物在安全格里打。
    private const float PetHoldDistance = 6f;
    private WPos? PetHold()
    {
        if (_glass is not { IsDeadOrDestroyed: false } glass || glass.HPMP.CurHP == 0)
            return null;
        var dir = glass.Position - Module.Center;
        return dir.LengthSq() < 1 ? null : Module.Center - dir.Normalized() * PetHoldDistance;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (hints.PetMoveTo == null && PetHold() is { } hold)
            hints.PetMoveTo = hold;
        var until = DangerUntil();
        if (until == default || _glass == null)
            return;
        hints.SetPriority(_glass, AIHints.Enemy.PriorityForbidden);
        var dir = _glass.Position - Origin;
        if (dir.LengthSq() > 0.01f)
            hints.AddForbiddenZone(new AOEShapeRect(LaneLength, LaneHalfWidth).Distance(Origin, Angle.FromDirection(dir)), WorldState.CurrentTime, until);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (DangerUntil() != default)
            hints.Add("死亡收割者指针指向/将转向沙漏：先别打沙漏，离开沙漏方向！");
    }
}

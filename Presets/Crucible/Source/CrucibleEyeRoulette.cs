// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Killing the hourglass stops Roulette. ARR 22:01:47: the 19738 model turns
// ~0.5s before its 49421 event, at ~1.5s intervals. Read the live model rotation;
// using only the event (or the stationary 19739 execution helper) attacks late.
sealed class CrucibleEyeRoulette(BossModule module) : BossComponent(module)
{
    private Actor? _hourglass, _spinner;
    private Angle _direction;
    private DateTime _changedAt, _expires;
    private bool _haveDirection;
    private const float TurnPeriod = 1.5f, StopLead = 0.5f;

    private bool Active => _expires > WorldState.CurrentTime && _hourglass is { IsDeadOrDestroyed: false } h && h.HPMP.CurHP > 0;

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID == 19737)
        {
            _hourglass = actor;
            _spinner = Module.Enemies(19738).LastOrDefault(a => !a.IsDeadOrDestroyed);
            _haveDirection = false;
            if (_spinner != null)
                _direction = _spinner.Rotation;
            _changedAt = WorldState.CurrentTime;
            _expires = WorldState.FutureTime(45);
        }
        else if (actor.OID == 19738)
        {
            _spinner = actor;
            _direction = actor.Rotation;
            _changedAt = WorldState.CurrentTime;
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 49418 && !spell.EventHappened)
        {
            _hourglass = Module.Enemies(19737).LastOrDefault(a => !a.IsDeadOrDestroyed && a.HPMP.CurHP > 0);
            _spinner = Module.Enemies(19738).LastOrDefault(a => !a.IsDeadOrDestroyed);
            _haveDirection = false;
            _expires = WorldState.FutureTime(45);
        }
        else if (spell.Action.ID == 49420 && !spell.EventHappened)
            _expires = Module.CastFinishAt(spell, 1);
    }

    private void RefreshDirection()
    {
        if (_spinner is not { IsDeadOrDestroyed: false } spinner)
            return;
        if ((_direction - spinner.Rotation).Normalized().Abs().Deg > 10)
        {
            _direction = spinner.Rotation;
            _changedAt = WorldState.CurrentTime;
            _haveDirection = true;
        }
    }

    public override void Update() => RefreshDirection();

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 49421 && caster.OID == 19738)
        {
            _spinner = caster;
            RefreshDirection();
            _haveDirection = true;
        }
        else if (spell.Action.ID == 49422)
            _expires = default;
    }

    private bool CanAttack(Actor actor)
    {
        RefreshDirection();
        if (!_haveDirection || _spinner is not { IsDeadOrDestroyed: false } spinner)
            return false;
        var toPlayer = actor.Position - spinner.Position;
        if (toPlayer.LengthSq() < 1)
            return false;
        var playerDirection = Angle.FromDirection(toPlayer);
        // The player is bound in a cardinal sector. Use a 60-degree half-angle
        // (with boundary margin) for target suppression, not a claimed AOE outline.
        bool PointsAtPlayer(Angle angle) => (angle - playerDirection).Normalized().Abs().Deg <= 60;
        var elapsed = (WorldState.CurrentTime - _changedAt).TotalSeconds;
        return elapsed < TurnPeriod + 0.7 && !PointsAtPlayer(_direction)
            && !(elapsed >= TurnPeriod - StopLead && PointsAtPlayer(_direction - 90f.Degrees()));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_hourglass != null && actor.FindStatus(3625) is { } bind)
        {
            // Binding prevents walking, but safe-sector casts must still finish.
            // ForcedMarchImminent also cancels casts through AIController.
            hints.ForcedMovement = Vector3.Zero;
            hints.AddSpecialMode(AIHints.SpecialMode.NoMovement, WorldState.CurrentTime, bind.ExpireAt);
        }
        if (!Active || _hourglass is not { IsTargetable: true } hourglass)
        {
            if (_hourglass != null && actor.TargetID == _hourglass.InstanceID)
            {
                hints.ClearTargetID = _hourglass.InstanceID;
                if (Module.PrimaryActor is { IsTargetable: true, IsDeadOrDestroyed: false } boss)
                    hints.ForcedTarget = boss;
            }
            return;
        }
        var safe = CanAttack(actor);
        if (hints.FindEnemy(hourglass) is { } enemy)
        {
            enemy.Priority = safe ? 10 : AIHints.Enemy.PriorityForbidden;
            enemy.ForbidDOTs = true; // delayed ticks must not stop a later dangerous sector
        }
        if (safe)
            hints.ForcedTarget = hourglass;
        else
        {
            hints.ClearTargetID = hourglass.InstanceID;
            if (actor.CastInfo?.TargetID == hourglass.InstanceID)
                hints.ForceCancelCast = true;
            if (Module.PrimaryActor is { IsTargetable: true, IsDeadOrDestroyed: false } boss)
                hints.ForcedTarget = boss;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (Active && _hourglass is { IsTargetable: true })
            hints.Add(CanAttack(actor) ? "轮盘安全：击杀死亡沙漏停盘！" : "即死区转向你：暂停攻击沙漏，等它转走！", false);
    }
}

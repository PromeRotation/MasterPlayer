// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// ARR 20:51:08: tether1/icon23 precede 49277/49278 by 5.8s. The helper
// rotation is fixed at cast start, leaving 3s to dodge. 49278 applies 2550
// to Lamia and 437 to Cyclops; aiming is useful only before that snapshot.
sealed class CrucibleMedusaRay(BossModule module) : Components.GenericBaitAway(module)
{
    private static readonly AOEShapeCone Ray = new(60, 22.5f.Degrees());
    private Actor? _add;
    private int _side;
    private Actor? _baitPlayer;
    private WPos _lockedOrigin;
    private Angle _lockedRotation;
    private DateTime _lockedUntil;

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID != 19705 || tether.ID != 1 || source.CastInfo?.Action.ID is 49277 or 49278 or 49279 or 49281
            || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        if (CurrentBaits.Any(b => b.Source == source && b.Target == target))
            return;
        CurrentBaits.Clear();
        CurrentBaits.Add(new(source, target, Ray, WorldState.FutureTime(8.8)));
        _add = null;
        _side = 0;
        _baitPlayer = target;
        _lockedUntil = default;
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        CurrentBaits.RemoveAll(b => b.Source == source);
        if (CurrentBaits.Count == 0 && _lockedUntil == default)
            _add = null;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49277 or 49278 or 49279 or 49281)
        {
            if (_add != null && _baitPlayer != null)
            {
                _lockedOrigin = caster.Position;
                _lockedRotation = spell.Rotation;
                _lockedUntil = Module.CastFinishAt(spell, 0.3f);
            }
            CurrentBaits.Clear();
        }
    }

    public override void Update()
    {
        // Do not keep trying to bait if a lock/untether packet was lost.
        CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed
            || b.Activation.AddSeconds(-2.3) < WorldState.CurrentTime);
        if (CurrentBaits.Count == 0 && _lockedUntil <= WorldState.CurrentTime)
        {
            _add = null;
            _baitPlayer = null;
            _lockedUntil = default;
        }
    }

    private static bool IsLivingAdd(Actor actor) => actor.OID is 19706u or 19707u
        && !actor.IsDeadOrDestroyed && actor.IsTargetable && actor.HPMP.CurHP > 0;

    private WPos? Aim(Actor player)
    {
        if (!IsBaitTarget(player))
            return null;
        var boss = CurrentBaits.First(b => b.Target == player).Source;
        // Respect manual target changes. Never choose an add on the player's
        // behalf, including when the previous add dies or the player targets boss.
        var selected = WorldState.Actors.Find(player.TargetID);
        if (selected == null || !IsLivingAdd(selected))
            selected = null;
        if (_add != selected)
        {
            _add = selected;
            _side = 0;
        }
        if (_add == null)
            return null;
        var offset = _add.Position - boss.Position;
        var distance = offset.Length();
        if (distance < 1)
            return null; // No reliable ray direction when the actors overlap.
        // Keep the add's CENTER 0.8y inside the edge, rather than assuming
        // that merely touching its hitbox applies petrification. Project onto
        // that ray to minimize the baiter's distance from the add.
        var angle = MathF.Max(0, 22.5f.Degrees().Rad - MathF.Asin(MathF.Min(1, 0.8f / distance)));
        WPos Point(int side) => boss.Position + (Angle.FromDirection(offset) + new Angle(side * angle)).ToDirection() * (distance * MathF.Cos(angle));
        if (_side == 0)
        {
            var left = Point(-1);
            var right = Point(1);
            _side = Arena.InBounds(left) && (!Arena.InBounds(right) || (left - player.Position).LengthSq() <= (right - player.Position).LengthSq()) ? -1 : 1;
        }
        var point = Point(_side);
        return Arena.InBounds(point) ? point : null;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (Aim(actor) is { } point)
        {
            // A goal, not a mandatory zone: existing hazards must stay avoidable.
            hints.GoalPoints.Add((point, 0.2f, 100));
            if (!actor.Position.InCircle(point, 1))
                hints.MaxCastTime = 0;
        }
        else if (actor == _baitPlayer && _lockedUntil > WorldState.CurrentTime && _add is { } add && actor.TargetID == add.InstanceID && IsLivingAdd(add))
        {
            // After snapshot, attack from the safe edge of the same add.
            // Sample within melee reach; normal forbidden zones still decide
            // when the player must leave the locked cone and other hazards.
            WPos? best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < 32; ++i)
            {
                var candidate = add.Position + new Angle(i * MathF.Tau / 32).ToDirection() * (add.HitboxRadius + 1.5f);
                if (!Arena.InBounds(candidate) || Ray.Check(candidate, _lockedOrigin, _lockedRotation))
                    continue;
                var distance = (candidate - actor.Position).LengthSq();
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (best is { } safe)
                hints.GoalPoints.Add((safe, 0.2f, 100));
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (IsBaitTarget(actor) && WorldState.Actors.Find(actor.TargetID) is { } selected && IsLivingAdd(selected))
            hints.Add("射线边缘蹭到小怪，锁定后侧移并继续打小怪！", false);
    }
}

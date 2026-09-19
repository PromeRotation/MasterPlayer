// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 5555 freezes the facing reference. 49485/86 turns along or against that
// reference; 5341 later expels a blade in the corresponding player-relative direction.
sealed class CrucibleLaudaShockwave(BossModule module) : Components.GenericKnockback(module, 49489)
{
    private sealed class Mark(Angle facing, DateTime boundUntil)
    {
        public Angle Facing = facing;
        public DateTime BoundUntil = boundUntil, Activation;
        public bool? Forward;
        public int Side;
    }
    private readonly Dictionary<ulong, Mark> _marks = [];
    private readonly Knockback[] _source = new Knockback[1];

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        base.OnStatusGain(actor, ref status);
        if (status.ID == 5555)
        {
            if (!_marks.TryGetValue(actor.InstanceID, out var mark))
                _marks[actor.InstanceID] = new(actor.Rotation, status.ExpireAt);
            else
            {
                // Native scans and queued status packets can arrive in either order.
                if (mark.BoundUntil == default && mark.Forward == null) mark.Facing = actor.Rotation;
                mark.BoundUntil = status.ExpireAt;
            }
        }
        else if (status.ID == 5341)
        {
            if (!_marks.TryGetValue(actor.InstanceID, out var mark))
                _marks[actor.InstanceID] = mark = new(actor.Rotation, default);
            mark.Activation = status.ExpireAt;
        }
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        base.OnStatusLose(actor, ref status);
        if (_marks.TryGetValue(actor.InstanceID, out var mark))
        {
            if (status.ID == 5555)
                mark.BoundUntil = default;
            else if (status.ID == 5341)
                mark.Activation = WorldState.FutureTime(0.25); // Removal packet precedes 49489 by ~80ms.
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is 49485 or 49486)
        {
            foreach (var (id, mark) in _marks)
                if (WorldState.Actors.Find(id) is { } target && target.Position.InCircle(caster.Position, 1))
                    mark.Forward = spell.Rotation.ToDirection().Dot(mark.Facing.ToDirection()) > 0;
        }
        else if (spell.Action.ID == WatchedAction)
        {
            foreach (var target in spell.Targets)
                _marks.Remove(target.ID);
            ++NumCasts;
        }
    }

    public override void Update()
    {
        foreach (var (id, mark) in _marks.ToArray())
            if (WorldState.Actors.Find(id) is not { IsDeadOrDestroyed: false }
                || (mark.Activation != default ? mark.Activation < WorldState.CurrentTime.AddSeconds(-1) : mark.BoundUntil < WorldState.CurrentTime.AddSeconds(-20)))
                _marks.Remove(id);
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        if (!_marks.TryGetValue(actor.InstanceID, out var mark) || mark.Activation == default || mark.Activation == DateTime.MaxValue || mark.Forward is not { } forward)
            return [];
        _source[0] = new(actor.Position, 40, mark.Activation, direction: actor.Rotation + (forward ? default : 180f.Degrees()), kind: Kind.DirForward, ignoreImmunes: true);
        return _source;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !CrucibleLaudaFloor.Contains(pos);

    protected override bool LandingUnsafe(int slot, Actor actor, WPos landing, DateTime activation)
        => base.LandingUnsafe(slot, actor, landing, activation)
            || Module.FindComponent<XBMB45AOE>() is { } aoes && aoes.ActiveAOEs(slot, actor).ToArray()
                .Any(a => ResolvesAfterJump(actor, a.Activation) && a.Check(landing));

    // Navigation cannot represent a future teleport across an AOE. Keep the
    // later pattern on the overlay and assess it at the landing instead.
    public bool ResolvesAfterJump(Actor actor, DateTime activation) => _marks.TryGetValue(actor.InstanceID, out var mark)
        && mark.Activation != default && mark.Activation != DateTime.MaxValue && mark.Forward != null
        && activation > mark.Activation.AddSeconds(0.5) && activation <= mark.Activation.AddSeconds(4);


    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_marks.TryGetValue(actor.InstanceID, out var mark))
        {
            hints.Add(mark.Forward switch
            {
                true => "止步飞刀：沿长轴站位，面向击退落点！",
                false => "止步飞刀：沿长轴站位，背对击退落点！",
                _ => "止步飞刀：记录Boss出现的前后方向，准备长轴击退！"
            }, false);
            base.AddHints(slot, actor, hints);
        }
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (!_marks.TryGetValue(actor.InstanceID, out var mark))
            return;
        if (mark.BoundUntil > WorldState.CurrentTime)
        {
            hints.ForcedMarchImminent = true;
            return;
        }
        if (mark.Activation == default || mark.Activation == DateTime.MaxValue || mark.Forward is not { } forward)
            return;

        // Starting 18y from center leaves the 40y landing on the opposite tip.
        // Consider overlapping AOEs at the landing, including the following cleave.
        float Score(int side)
        {
            var start = Module.Center + new WDir(0, side * 18);
            var end = Module.Center - new WDir(0, side * 22);
            float penalty = 0;
            foreach (var zone in hints.ForbiddenZones)
                if (zone.activation >= mark.Activation.AddSeconds(-0.5) && zone.activation <= mark.Activation.AddSeconds(4)
                    && zone.shapeDistance.Distance(end) <= 0)
                    penalty += 10000;
            if (Module.FindComponent<XBMB45AOE>() is { } aoes)
                foreach (var aoe in aoes.ActiveAOEs(slot, actor))
                    if (ResolvesAfterJump(actor, aoe.Activation) && aoe.Shape.Check(end, aoe.Origin, aoe.Rotation))
                        penalty += 10000;
            return penalty + (actor.Position - start).LengthSq() + (mark.Side == side ? -100 : 0);
        }
        mark.Side = Score(1) < Score(-1) ? 1 : -1;
        var goal = Module.Center + new WDir(0, mark.Side * 18);
        var landing = Module.Center - new WDir(0, mark.Side * 22);
        hints.GoalPoints.Add((goal, 0.2f, 100));
        // This standing pocket must survive grid dilation. Do not add damage
        // inflation or advance the deadline: the preceding R8 circles still
        // cover it. PR adds its own preparation time before the actual impact.
        hints.ForbiddenZones.Add(new(new SDInvertedCircle(goal, 2), mark.Activation, null, ExactGeometry: true));
        hints.MaxCastTime = 0;
        var direction = (landing - actor.Position).Normalized();
        var actualLanding = actor.Position + direction * 40;
        if (actor.Position.InCircle(goal, 2.0f) && !LandingUnsafe(slot, actor, actualLanding, mark.Activation))
        {
            // Aim from the actual position. A 12-degree facing error moves a
            // 40y landing almost 9y sideways and misses the narrow end platform.
            hints.DesiredFacing = Angle.FromDirection(forward ? direction : -direction);
            hints.DesiredFacingExpire = mark.Activation.AddSeconds(0.5);
            hints.ForcedMarchImminent = true;
            hints.ForceCancelCast = true;
        }
    }
}

// Icon669 snapshots a knife (49468), followed by the proximity cast 49469.
// Its R80 sheet radius describes damage falloff, not a forbidden arena-sized circle.
sealed class CrucibleLaudaFireKnife(BossModule module) : BossComponent(module)
{
    private ulong _target;
    private ulong _explosionCaster;
    private WPos? _drop, _explosion;
    private DateTime _dropUntil, _explodeAt;

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != 669)
            return;
        _target = actor.InstanceID;
        // These two side tips remain outside the four 49460 prison circles.
        WPos west = new(507, -422.5f), east = new(533, -417.5f);
        _drop = (actor.Position - west).LengthSq() < (actor.Position - east).LengthSq() ? west : east;
        _dropUntil = WorldState.FutureTime(8);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        if (spell.Action.ID == 49468)
        {
            _explosionCaster = caster.InstanceID;
            _drop = null;
            _explosion = spell.LocXZ;
            _explodeAt = Module.CastFinishAt(spell, 12);
        }
        else if (spell.Action.ID == 49469)
        {
            _explosionCaster = caster.InstanceID;
            _drop = null;
            _explosion = spell.LocXZ;
            _explodeAt = Module.CastFinishAt(spell);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 49468)
            _explosionCaster = 0; // The landed knife outlives the placement helper.
        if (spell.Action.ID == 49469)
            _explosion = null;
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49468 or 49469 && caster.InstanceID == _explosionCaster && spell.Interrupted)
            _explosion = null;
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.InstanceID == _explosionCaster) _explosion = null;
        if (actor.InstanceID == _target) _drop = null;
    }
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);

    public override void Update()
    {
        if (_dropUntil < WorldState.CurrentTime)
            _drop = null;
        if (_explodeAt.AddSeconds(1) < WorldState.CurrentTime)
            _explosion = null;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_drop is { } drop && _target == actor.InstanceID && Module.FindComponent<CrucibleLaudaBaits>()?.Preparing != true)
        {
            hints.GoalZones.Add(p => p.InCircle(drop, 1) ? 50 : 0);
            hints.MaxCastTime = 0;
        }
        if (_explosion is { } explosion)
            hints.GoalZones.Add(p => Math.Min(10, (p - explosion).Length() / 4));
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (_drop != null && _target == actor.InstanceID)
            hints.Add(Module.FindComponent<CrucibleLaudaBaits>()?.Preparing == true
                ? "飞刀与剑线重叠：保持上下中央凸台，剑线锁定后躲开！"
                : "飞刀点名：放到侧面安全凸台！", false);
        if (_explosion != null)
            hints.Add("魔刃核爆：远离飞刀，注意同时处理止步击退！", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        if (_drop is { } drop && _target == pc.InstanceID && Module.FindComponent<CrucibleLaudaBaits>()?.Preparing != true)
            new AOEShapeCircle(1).Outline(Arena, drop, default, Colors.Safe);
        if (_explosion is { } explosion)
            new AOEShapeCircle(6).Outline(Arena, explosion, default, Colors.Danger);
    }
}

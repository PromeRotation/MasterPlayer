// See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

sealed class CrucibleTreantBody(BossModule module) : Components.GenericAOEs(module)
{
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var result = new List<AOEInstance>();
        foreach (var treant in Module.Enemies((uint)OID.E14618))
            if (!treant.IsDeadOrDestroyed)
                // Keep a margin outside the body even between casts and ring waves.
                result.Add(new(new AOEShapeCircle(treant.HitboxRadius + 0.5f), treant.Position));
        return CollectionsMarshal.AsSpan(result);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        var bodies = ActiveAOEs(slot, actor).ToArray();
        foreach (var aoe in bodies)
            hints.TemporaryObstacles.Add(aoe.Shape.Distance(aoe.Origin, aoe.Rotation));

        if (bodies.Length != 0 && actor.Role is Role.Melee or Role.Tank
            && WorldState.Actors.Find(actor.TargetID) is { IsDeadOrDestroyed: false, IsTargetable: true } target
            && target.OID is >= 19661u and <= 19665u)
        {
            // A full attack circle includes the forbidden body, making the safe-area
            // fraction fall below navigation's 25% threshold for large treants.
            // Keep the selected target, but score only positions outside the body.
            var position = target.Position;
            var range = target.HitboxRadius + 2.6f;
            hints.GoalZones.Add(p => p.InCircle(position, range) && !bodies.Any(b => b.Check(p)) ? 1f : 0f);
        }
    }
}

// ARR 2026-09-11 23:16:05: five casts start together, then resolve every 2s.
// The tornado is centered on the cast location (13y north of the arena center).
class CrucibleTreantAOEs(BossModule module, Battle battle) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    private static bool IsRing(uint action) => action is >= 48771 and <= 48775;
    private const double RingLeadSeconds = 2.4;
    private const float ReturnDistance = 12.5f, ReturnRadius = 1f;
    private ulong _acornCaster;
    private DateTime _acornUntil;
    private ulong _acornPet;
    private WPos _acornPetHold;
    protected override AOEConfig? ConfigFor(uint actionID) => battle == Battle.B29
        && (IsRing(actionID) || actionID is 48778 or 48779 or 48780 or 48781 or 48783 or 48785)
        ? new(CrucibleSpells.Table[actionID].Shape, true) : null;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        base.OnCastStarted(caster, spell);
        if (battle == Battle.B29 && caster.OID == 19661 && spell.Action.ID == 50518 && !spell.EventHappened)
        {
            _acornCaster = caster.InstanceID;
            _acornPet = 0;
            // Eight sealed hits land 0.480-0.530s after the cast bar, including after its dummy 50518 effect.
            _acornUntil = Module.CastFinishAt(spell, 0.9);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        base.OnCastFinished(caster, spell);
        if (caster.InstanceID == _acornCaster && spell.Action.ID == 50518 && spell.Interrupted)
            _acornCaster = 0;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        base.OnEventCast(caster, spell);
        if (caster.InstanceID == _acornCaster && spell.Action.ID == 48781)
            _acornCaster = 0;
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _ = base.ActiveAOEs(slot, actor);
        DateTime? firstRing = null;
        var result = new List<AOEInstance>();
        foreach (var entry in Pending)
        {
            var aoe = entry.AOE;
            if (IsRing(entry.ActionID))
            {
                firstRing ??= aoe.Activation;
                // run2000: the next ring turned risky the moment the previous one landed, while its 0.6s window still
                // blocked the way back in, so the player ran on through every ring to 15y out of melee for 10.8s. Hold it
                // until that window has nearly lapsed: the inward step is then 1y and no later ring reaches melee range.
                aoe.Risky = aoe.Activation <= firstRing.Value.AddSeconds(0.5) && (aoe.Activation - WorldState.CurrentTime).TotalSeconds <= RingLeadSeconds;
            }
            result.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(result);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        foreach (var aoe in ActiveAOEs(pcSlot, pc))
            if (aoe.Risky)
                aoe.Shape.Draw(Arena, aoe.Origin, aoe.Rotation);
            else
                aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, Colors.AOE);
    }

    // run1856/run1858 ARR (2026-11-02/03): the helper cones 48779/48780 (±90°, 150°) and 48778 (facing
    // the arena center, 90°) never hit the player, but the offline replay parked the player 2.6° outside
    // the 90° cone (~0.5y at 12y). The danger margin only grows a cone's radius, so give navigation an
    // angular margin too; the drawn shape stays the BMR cone.
    private const float NavigationHalfAngleMargin = 4f;
    private const float NavigationRadiusMargin = 0.35f;

    private static AOEInstance ForNavigation(AOEInstance aoe)
    {
        if (aoe.Shape is not AOEShapeCone cone)
            return aoe;
        var shape = new AOEShapeCone(cone.Radius + NavigationRadiusMargin, cone.HalfAngle + NavigationHalfAngleMargin.Degrees());
        return new(shape, aoe.Origin, aoe.Rotation, aoe.Activation, aoe.Color, aoe.Risky, aoe.ActorID, shape.Distance(aoe.Origin, aoe.Rotation));
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        var pending = Pending;
        var sideCones = 0;
        var frontCone = false;
        foreach (var entry in pending)
        {
            if (entry.ActionID is 48779 or 48780) ++sideCones;
            else if (entry.ActionID == 48778) frontCone = true;
        }
        if (sideCones > 0)
            hints.Add("簌簌轻风（双 150° 扇）：安全带是树精正前/正后的窄条，站树精面向的场心一线");
        if (frontCone)
            hints.Add("簌簌轻风（90° 扇）：离开树精面向的扇形，站到其侧方");
    }

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // run1976: a wind cone overlapping the rings left no way out once the next ring appeared; plan them all together.
        var cones = false;
        var innerRings = false;
        WPos? rings = null;
        foreach (var entry in Pending)
        {
            cones |= !IsRing(entry.ActionID);
            if (IsRing(entry.ActionID))
                rings = entry.AOE.Origin;
            innerRings |= entry.ActionID is 48771 or 48772;
        }
        foreach (var aoe in ActiveAOEs(slot, actor))
            if (aoe.Risky || cones)
                CrucibleScorpionAI.Avoid(hints, ForNavigation(aoe));
        // run2106 (16:50:00.35 and 16:52:51.52) and run2096-b5 replay: waiting just outside the R12 circle left only
        // 2s after it to cross the whole 12-18 band before 48772; a web (48785 + Heavy) or a slow start in between
        // and the second ring hit. The pocket inside R12 is too thin beside the body, so be outside 48772 before 48771
        // lands, then step back in after 48772 (ring three is 18-24, the existing 12.5y return stays clear of it).
        AOEInstance? circle = null, band = null;
        foreach (var entry in Pending)
            if (entry.ActionID == 48771) circle ??= entry.AOE;
            else if (entry.ActionID == 48772) band ??= entry.AOE;
        if (circle is { } c && band is { } b && (c.Activation - WorldState.CurrentTime).TotalSeconds <= RingLeadSeconds)
        {
            var donut = (AOEShapeDonut)b.Shape;
            hints.AddForbiddenZone(new AOEShapeDonut(Math.Max(0, donut.InnerRadius - 0.35f), donut.OuterRadius + 0.35f),
                b.Origin, b.Rotation, default, b.Activation.AddSeconds(0.6));
        }
        // run1976: the wind's lock arrives with its cones and only the arena-centre line of the treant stays clear.
        // Come back from the rings onto that line; 3.5y off it the player could not step in once the lock lifted.
        if (!innerRings && !cones && rings is { } origin && (Module.Center - origin).LengthSq() > 1)
            hints.GoalPoints.Add((origin + (Module.Center - origin).Normalized() * ReturnDistance, ReturnRadius, 1));
        // run1976/1980: the old "step inside the first circle" constraint left only 10.5-11.15y between the body
        // and the second ring, which the 0.5y grid cannot hold (NoReachableSafeCell, second ring hit 3/3). Only the
        // next ring is risky, so the solver steps out past it and back in once it has resolved.
        AddAcornSeparation(actor, hints);
    }

    private void AddAcornSeparation(Actor actor, AIHints hints)
    {
        if (_acornCaster == 0)
            return;
        if (WorldState.CurrentTime >= _acornUntil || actor.IsDeadOrDestroyed
            || WorldState.Actors.Find(_acornCaster) is not { IsDeadOrDestroyed: false, HPMP.CurHP: > 0 } boss)
        {
            _acornCaster = 0;
            return;
        }
        if (actor.FindStatus(2413) is not { } cover || cover.ExpireAt <= WorldState.CurrentTime
            || WorldState.Actors.Find(cover.SourceID) is not { IsDeadOrDestroyed: false, HPMP.CurHP: > 0 } pet
            || pet == actor || pet.OwnerID != actor.InstanceID)
            return;

        if (_acornPet != pet.InstanceID)
        {
            _acornPet = pet.InstanceID;
            var away = pet.Position - boss.Position;
            _acornPetHold = away.LengthSq() > 0.01f
                ? boss.Position + away.Normalized() * (boss.HitboxRadius + pet.HitboxRadius + 0.8f)
                : pet.Position;
        }
        hints.PetMoveTo = _acornPetHold;

        // The player's splash also hits the covering beast when their bodies overlap. Its actual position and
        // hitbox matter: the knight is R1, while Behemoth is R2.9. Do not substitute a nearby unverified pet.
        var radius = Math.Max(4.75f, 3 + pet.HitboxRadius + 0.5f);
        var separation = radius + 1;
        bool Safe(WPos point) => Arena.InBounds(point)
            && (Arena.Bounds is not ArenaBoundsCircle bounds || (point - Module.Center).Length() < bounds.Radius - 0.75f)
            && (point - pet.Position).Length() >= separation
            && (point - _acornPetHold).Length() >= separation
            && (point - boss.Position).Length() > boss.HitboxRadius + 1.8f
            && !hints.ForbiddenZones.Any(z => z.Activation <= _acornUntil
                && (z.ExpiresAt == null || z.ExpiresAt > WorldState.CurrentTime) && z.Shape.Distance(point) <= 0.6f);

        WPos? goal = null;
        var bestDistance = float.MaxValue;
        if ((actor.Position - boss.Position).Length() <= boss.HitboxRadius + 2.5f && Safe(actor.Position))
            goal = actor.Position;
        else
            foreach (var distance in new[] { boss.HitboxRadius + 2f, boss.HitboxRadius + 2.5f })
                for (var degrees = 0; degrees < 360; degrees += 5)
                {
                    var point = boss.Position + degrees.Degrees().ToDirection() * distance;
                    var travel = (point - actor.Position).LengthSq();
                    if (travel >= bestDistance || !Safe(point))
                        continue;
                    goal = point;
                    bestDistance = travel;
                }
        hints.AddForbiddenZone(new AOEShapeCircle(radius), pet.Position, expiresAt: _acornUntil);
        if ((pet.Position - _acornPetHold).LengthSq() > 0.25f)
            hints.AddForbiddenZone(new AOEShapeCircle(radius), _acornPetHold, expiresAt: _acornUntil);
        if (goal is { } position)
            hints.GoalPoints.Add((position, 0.25f, 120));
    }
}

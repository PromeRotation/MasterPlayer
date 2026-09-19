// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// The mindflayer's toxic mushrooms are the intended target only once a water
// bait can actually hit them. Until a mushroom is inside the active water
// circle, preserve the current target so the bait setup does not waste uptime.
sealed class CrucibleMindflayerAdds(BossModule module) : BossComponent(module)
{
    private static bool IsLiving(Actor a) => a.OID == 19678u && !a.IsDeadOrDestroyed && a.IsTargetable && a.HPMP.CurHP > 0;
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var adds = WorldState.Actors.Where(IsLiving).OrderBy(a => a.InstanceID).ToArray();
        var water = Module.FindComponent<CrucibleMindflayerWater>();
        var hittable = water?.IsBaitTarget(actor) == true
            ? adds.Where(a => a.Position.InCircle(actor.Position, 7.5f)).ToArray()
            : [];
        foreach (var add in hittable)
            hints.SetPriority(add, 10);
        if ((hittable.FirstOrDefault(a => a.InstanceID == actor.TargetID) ?? hittable.FirstOrDefault()) is { } next)
            hints.ForcedTarget = next;
        // Retarget only an invalid selected mushroom. An unconditional fallback
        // forces boss every frame and prevents all manual target changes.
        else if (WorldState.Actors.Find(actor.TargetID) is { OID: 19678u } previous && !IsLiving(previous)
            && Module.PrimaryActor.IsTargetable && !Module.PrimaryActor.IsDeadOrDestroyed)
            hints.ForcedTarget = Module.PrimaryActor;
    }
}

// High second board: ARR 2026-09-13 17:19:06 / 17:22:48 / 17:39:23.
// Geometry checked against the installed CN Action/Omen sheets. In particular,
// 49244 is visual, 49245 is a 5y ground circle, and 49247 is a 6y ground circle.
class High2CrucibleCastAOEs(BossModule module, Battle battle) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => (battle, actionID) switch
    {
        (Battle.B32, 49186) => new(new AOEShapeCircle(6), true),
        (Battle.B32, 49191) => new(new AOEShapeCircle(16), true),
        (Battle.B32, 49193) => new(new AOEShapeRect(50, 20), true),
        (Battle.B32, 49194) => new(new AOEShapeRect(100, 1), true),
        (Battle.B32, 49195) => new(new AOEShapeRect(100, 3), true),
        (Battle.B33, 49196) => new(new AOEShapeCircle(6), true),
        // These packets locate the origin 50y behind the magic glyph, making
        // the 100y line bidirectional about the glyph. Do not use actor.Position.
        (Battle.B33, 49203) => new(new AOEShapeRect(100, 2), true),
        (Battle.B33, 49204) => new(new AOEShapeRect(100, 5), true),
        (Battle.B33, 49207 or 50939) => new(new AOEShapeCross(50, 5), true),
        (Battle.B35, 49233) => new(new AOEShapeCircle(8), true),
        (Battle.B35, 49236) => new(new AOEShapeCircle(6), true),
        (Battle.B35, 49237) => new(new AOEShapeCircle(12), true),
        (Battle.B35, 49240) => new(new AOEShapeCone(60, 60f.Degrees()), true),
        (Battle.B35, 49245) => new(new AOEShapeCircle(5), true),
        (Battle.B35, 49247) => new(new AOEShapeCircle(6), true),
        (Battle.B35, 49248) => new(new AOEShapeCone(28, 60f.Degrees()), true),
        (Battle.B35, 49249) => new(new AOEShapeCone(30, 45f.Degrees()), true),
        (Battle.B37, 49265) => new(new AOEShapeCircle(20), true),
        (Battle.B37, 49266) => new(new AOEShapeCircle(6), true),
        // The missile damage cast belongs to a generic 9020/NameID108 helper.
        (Battle.B37, 50688) => new(new AOEShapeRect(100, 2), true),
        // B38: helper casts carry the actual relocated origin in the replay.
        // The main cast is a visual shell; draw the resolved helper shapes.
        (Battle.B38, 49278 or 49281) => new(new AOEShapeCone(60, 22.5f.Degrees()), true),
        // Lamia's targeted fire ring is a ground circle at the packet location.
        (Battle.B38, 49284) => new(new AOEShapeCircle(5), true),
        (Battle.B38, 49291 or 49293) => new(new AOEShapeDonut(5, 60), true),
        // Fear miasma is a targeted R6 ground burst; the cast location is the
        // snapshot point, which may differ slightly from the moving helper.
        (Battle.B38, 49286) => new(new AOEShapeCircle(6), true),
        (Battle.B38, 49289 or 49295) => new(new AOEShapeCircle(6), true),
        // Eight helper actors fan out the shockwave in 45-degree slices.
        (Battle.B38, 49296) => new(new AOEShapeCone(60, 22.5f.Degrees()), true),
        (Battle.B38, 49297) => new(new AOEShapeCircle(20), true),
        // B39: the two raidwide-size ground patterns and the targeted ice eye.
        (Battle.B39, 49306 or 49315) => new(new AOEShapeCircle(12), true),
        (Battle.B39, 49308 or 49316) => new(new AOEShapeDonut(8, 40), true),
        (Battle.B39, 49318) => new(new AOEShapeCircle(6), true),
        // 49332 is unavoidable raidwide damage, not an area to navigate out of.
        // Chimera breath helpers use x6d3_b1_fan240_p1: a 240-degree fan
        // (AOEShapeCone takes the half-angle).
        (Battle.B39, 49300 or 49302 or 49304 or 49327 or 49329 or 49331) => new(new AOEShapeCone(60, 120f.Degrees()), true),
        // Sphinx: only the damage helpers carry the actual circle/donut/half-room rotation.
        (Battle.B40, 49335) => new(new AOEShapeDonut(10, 60), true),
        (Battle.B40, 49337) => new(new AOEShapeCircle(18), true),
        (Battle.B40, 49339 or 49341) => new(new AOEShapeCone(60, 90f.Degrees()), true),
        // 22:48 ARR: the giant's front/back variants have distinct helper cast rotations.
        (Battle.B41, 49359) => new(new AOEShapeCircle(15), true),
        (Battle.B41, 49361 or 49363) => new(new AOEShapeCone(40, 90f.Degrees()), true),
        (Battle.B41, 49366) => new(new AOEShapeCircle(6), true),
        (Battle.B41, 49367) => new(new AOEShapeRect(40, 1.5f), true),
        (Battle.B41, 49378) => new(new AOEShapeRect(40, 4), true),
        (Battle.B41, 49379) => new(new AOEShapeRect(7, 2), true),
        // Lauda's origins can be 40y behind the boss. Use the packet location.
        (Battle.B45, 49451) => new(new AOEShapeRect(80, 10), true),
        (Battle.B45, 49464 or 50848) => new(new AOEShapeRect(60, 3), true),
        (Battle.B45, 49443) => new(new AOEShapeCircle(8), true),
        (Battle.B45, 49446) => new(new AOEShapeRect(50, 20), true),
        (Battle.B45, 49449) => new(new AOEShapeCircle(40), true),
        (Battle.B45, 49457) => new(new AOEShapeCross(15, 5), true),
        (Battle.B45, 49460) => new(new AOEShapeCircle(12), true),
        (Battle.B45, 49468) => new(new AOEShapeCircle(6), true),
        _ => null
    };

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Populate the display list first so the base risk/color policy remains intact.
        _ = ActiveAOEs(slot, actor);
        foreach (var entry in Pending)
        {
            ref readonly var aoe = ref entry.AOE;
            if (!aoe.Risky)
                continue;

            if (battle == Battle.B45 && entry.ActionID is 49446 or 49449
                && Module.FindComponent<CrucibleLaudaShockwave>()?.ResolvesAfterJump(actor, aoe.Activation) == true)
                continue;

            hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), aoe.Activation);
        }

    }
}

// 49197 repeatedly applies poison within 6y of the real, stationary fungal bed.
// Spores (19680) instead explode raidwide: they must not create a 60y forbidden zone.
sealed class CrucibleFungalBeds(BossModule module) : Components.Voidzone(module, 6, m => m.Enemies(19679).Where(a => !a.IsDeadOrDestroyed))
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        var target = Module.PrimaryActor;
        if (target.IsDeadOrDestroyed || !target.IsTargetable)
            return;
        // A bed covering the boss's center does not cover every attack position.
        // Score the safe part of the attack circle, as with the treant body;
        // otherwise navigation's 25% safe-goal threshold can discard the whole
        // circle. Snapshot positions for the asynchronous navigation worker.
        var beds = Sources(Module).Select(s => s.Position).ToArray();
        var position = target.Position;
        var range = target.HitboxRadius + 2.6f;
        hints.GoalZones.Add(p => p.InCircle(position, range) && !beds.Any(b => p.InCircle(b, 6.5f)) ? 1f : 0f);
    }
}

// Icon-marked attacks follow their targets (including the beastmaster's companion).
// Drawing the cast-start position as a ground AOE causes pointless running.
abstract class High2CrucibleSpread(BossModule module, uint icon, uint action, float radius, double delay, uint visualAction = 0)
    : Components.GenericBaitAway(module, action, centerAtTarget: true)
{
    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID != icon)
            return;
        var target = WorldState.Actors.Find(targetID) ?? actor;
        if (ActiveBaits.Any(b => b.Target == target && b.Activation.AddSeconds(1) > WorldState.CurrentTime))
            return; // Duplicate icons must not postpone an already running mechanic.
        CurrentBaits.RemoveAll(b => b.Target.InstanceID == target.InstanceID);
        CurrentBaits.Add(new(Module.PrimaryActor, target, new AOEShapeCircle(radius), WorldState.FutureTime(delay)));
    }

    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed || b.Activation.AddSeconds(1) < WorldState.CurrentTime);

    // Fire has a real targeted cast as well as its icon, so reloads during the
    // cast can recover the target and the remaining time without the old icon.
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime
            || WorldState.Actors.Find(spell.TargetID) is not { } target || target == caster)
            return;
        CurrentBaits.RemoveAll(b => b.Target.InstanceID == target.InstanceID);
        CurrentBaits.Add(new(caster, target, new AOEShapeCircle(radius), Module.CastFinishAt(spell)));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if ((spell.Action.ID == WatchedAction || visualAction != 0 && spell.Action.ID == visualAction) && spell.Interrupted)
            CurrentBaits.RemoveAll(b => b.Source.InstanceID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            // Multiple marked targets resolve together; targets in the damage
            // packet include splash victims, so do not infer the bait owner from them.
            CurrentBaits.RemoveAll(b => b.Activation <= WorldState.FutureTime(0.5));
        }
    }
}

sealed class CrucibleMindflayerWater(BossModule module) : High2CrucibleSpread(module, 135, 49200, 8, 5.9, 49199)
{
    public const float PoolRadius = 8;
    // ARR: pools are created after the water hit, at their own final positions.
    // Read live objects so reloads recover them without retaining stale icon locations.
    public IEnumerable<Actor> Pools => WorldState.Actors.Where(a => a.OID == 0x1E9998 && a.EventState != 7 && !a.IsDeadOrDestroyed);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
        => base.ActiveAOEs(slot, actor).ToArray().Concat(Pools.Select(p => new AOEInstance(new AOEShapeCircle(PoolRadius),
            p.Position, color: Colors.Object, risky: false, actorID: p.InstanceID))).ToArray();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (!IsBaitTarget(actor))
            return;

        // ARR 17:45:55: the marked companion is replaced during the first
        // water, and the second water has only a player icon but two hits.
        // Never depend on having two live icon owners. Include the currently
        // summoned companion, even after a swap or when its icon is missing.
        // 18916/18931 are the two companion OIDs observed in this recording;
        // OwnerID covers other owned companions without guessing an OID range.
        var others = ActiveBaits.Where(b => b.Target != actor && !b.Target.IsDeadOrDestroyed).Select(b => b.Target)
            .Concat(WorldState.Actors.Where(a => a != actor && !a.IsDeadOrDestroyed
                && (a.OwnerID == actor.InstanceID || a.OID is 18916u or 18931u)))
            .DistinctBy(a => a.InstanceID).Select(a => a.Position).ToArray();
        if (others.Length == 0)
            return;
        var activation = CurrentBaits.First(b => b.Target == actor).Activation;
        // Each target only needs to avoid the OTHER target's R8 splash.
        // Do not separate the full circle footprints by 16y or force an edge
        // destination: normal navigation can keep attacking from the nearest
        // safe position, with 0.5y extra clearance for movement latency.
        foreach (var other in others)
            hints.AddForbiddenZone(new SDCircle(other, 8.5f), activation.AddSeconds(-0.5), activation.AddSeconds(0.6));
        if (others.Any(o => actor.Position.InCircle(o, 8.5f)))
            hints.MaxCastTime = 0;
    }
}
sealed class CrucibleMindflayerThunder(BossModule module) : High2CrucibleSpread(module, 344, 49206, 6, 5.9, 49205)
{
    private const float PlacementRadius = CrucibleMindflayerWater.PoolRadius + 6;
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Apply this only to the thunder target. During a water-only mechanic,
        // forbidding the player's own moving water circle makes every new
        // position unsafe again and causes perpetual movement corrections.
        if (IsBaitTarget(actor) && Module.FindComponent<CrucibleMindflayerWater>() is { } water)
        {
            var activation = ActiveBaits.First(b => b.Target == actor).Activation;
            // Keep the entire R6 thunder footprint outside the R8 pools.
            // This is a placement constraint at thunder time, not a permanent water hazard.
            var centers = water.Pools.Select(p => p.Position)
                .Concat(water.ActiveBaits.Where(b => b.Target != actor).Select(b => b.Target.Position));
            foreach (var center in centers)
                hints.AddForbiddenConstraint(new SDCircle(center, PlacementRadius), activation.AddSeconds(-0.5), activation.AddSeconds(1));
        }
        base.AddAIHints(slot, actor, assignment, hints);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (IsBaitTarget(actor) && Module.FindComponent<CrucibleMindflayerWater>() is { } water
            && water.Pools.Any(p => actor.Position.InCircle(p.Position, PlacementRadius)))
            hints.Add("雷点名：将雷圈放到水圈外");
    }
};
sealed class CrucibleJellyFire(BossModule module) : High2CrucibleSpread(module, 100, 49243, 6, 8);
sealed class CrucibleJellyThunder(BossModule module) : High2CrucibleSpread(module, 704, 49246, 5, 8.2);

// The actual hit payloads reference Knockback rows 186 (16y) and 3 (20y).
// Both arenas are circular electric fences: the visible outer edge is not safe.
abstract class High2CrucibleKnockback(BossModule module, uint action, float distance) : HighCrucibleKnockback(module, action, distance)
{
    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos) => !pos.InCircle(Module.Center, 19.5f);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var kb in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, kb.Activation))
                hints.AddForbiddenConstraint(new LandingZone(Module.Center, kb.Origin, Distance), kb.Activation.AddSeconds(-0.5));
    }

    private sealed class LandingZone(WPos center, WPos origin, float distance) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            if ((p - origin).LengthSq() < 0.25f)
                return 0; // An exact-center cell has no reliable knockback direction.
            var landing = AwayFromSource(p, origin, distance);
            return 19.5f - (landing - center).Length();
        }
    }
}

sealed class CrucibleAtomosShockwave(BossModule module) : High2CrucibleKnockback(module, 49238, 16);
sealed class CrucibleJellyWater(BossModule module) : High2CrucibleKnockback(module, 49242, 20);
sealed class CrucibleDurgaRayHint(BossModule module) : Components.CastHints(module, [49272u], "原子射线：全屏伤害，准备减伤！");

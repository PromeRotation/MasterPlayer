// Ported from BossmodRebornCN f6914f3e8. See docs/reference/CrucibleAll.spec.md.
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// 46917 leaves two 5y pools; the smaller 46916 explosions do not leave voidzones.
// ARR 2026-09-10 16:49:04: EObj 0x1EC025 spawns at the two large impacts,
// then receives EventState 7 after ~23s. Track the objects, not a guessed duration.
sealed class CrucibleLava(BossModule module) : Components.Voidzone(module, 5f, GetLava)
{
    private static IEnumerable<Actor> GetLava(BossModule module) => module.Enemies(0x1EC025)
        .Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}

// Fire Spirit's Ward leaves EObj 0x1E9927 at the arena center. ARR 17:30:59
// records Burning (3065) inside this field; the object persists until EventState 7.
// Its b0740 SGB/AVFX has a 6y core. Keep it blocked even after the summoning cast.
sealed class CrucibleFireSpiritWard(BossModule module) : Components.Voidzone(module, 6f, GetWard)
{
    private static IEnumerable<Actor> GetWard(BossModule module) => module.Enemies(0x1E9927)
        .Where(a => a.EventState != 7 && !a.IsDeadOrDestroyed);
}

// Spreading Fire (46922): client radius 10y, but the actual cast is only 0.7s.
// The stationary fireball is already visible 5.8-6.9s before the hit in the two
// September 10 recordings. Draw and avoid it immediately on spawn, including
// when loading midway through the mechanic; do not guess its remaining fuse.
sealed class CrucibleFireball(BossModule module) : Components.GenericAOEs(module, 46922u, "离开火球的延烧范围！")
{
    private static readonly AOEShapeCircle Shape = new(10f);
    private readonly HashSet<ulong> _resolved = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var aoes = new List<AOEInstance>();
        foreach (var fireball in Module.Enemies(19343u))
            if (!fireball.IsDeadOrDestroyed && !_resolved.Contains(fireball.InstanceID))
                aoes.Add(new(Shape, fireball.Position, actorID: fireball.InstanceID));
        return CollectionsMarshal.AsSpan(aoes);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _resolved.Add(caster.InstanceID);
        }
    }

    public override void OnActorCreated(Actor actor) => _resolved.Remove(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _resolved.Remove(actor.InstanceID);
}

// Four simultaneous cast starts, resolving from the center out at 2s intervals.
// Only this sequence is risk-graded; concurrent web mechanics remain independently dangerous.
sealed class CrucibleGroundUpliftAOEs(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    internal static bool IsUplift(uint actionID) => actionID is >= 46902 and <= 46905;
    protected override double RiskyActivationWindow => 0.5d;
    protected override AOEConfig? ConfigFor(uint actionID) => IsUplift(actionID)
        ? new(CrucibleSpells.Table[actionID].Shape, LocationTargeted: true) : null;

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _ = ActiveAOEs(slot, actor); // Prune expired/canceled casts before reading the complete queue.
        var opening = Pending.Length > 0 && Pending[0].ActionID == 46902;
        foreach (var entry in Pending)
            CrucibleScorpionAI.Avoid(hints, entry.AOE);

        // PR represents every wave with its own window, including the center reopening.
        if (opening && Pending.Length > 1)
            // R8 collapses against the R6 circle after component, user and host
            // margins. R10 leaves a usable staging annulus for the return inward.
            hints.AddForbiddenConstraint(new SDInvertedCircle(Pending[0].AOE.Origin, 10f), Pending[0].AOE.Activation.AddSeconds(-1d), Pending[0].AOE.Activation.AddSeconds(0.6));
    }
}

// The central web and eight outer circles resolve ~0.25s apart, not as one filled disk.
sealed class CrucibleWebAOEs(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override int MaxDisplayed => int.MaxValue;
    protected override double RiskyActivationWindow => 0.15d;
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 46908
        ? new(CrucibleSpells.Table[actionID].Shape, LocationTargeted: true) : null;

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        _ = ActiveAOEs(slot, actor);
        // Recognize the central cast geometrically, including module activation midway through
        // a sequence (existing casts are replayed in actor order, not cast-start order).
        var opening = Pending.Length > 0 && Pending[0].AOE.Origin.AlmostEqual(Module.Center, 1f);
        var openingActivation = Pending.Length > 0 ? Pending[0].AOE.Activation : default;
        // All central and outer casts retain their individual PR danger windows.
        foreach (var entry in Pending)
            CrucibleScorpionAI.Avoid(hints, entry.AOE);
        if (opening && Pending.Length > 1)
            hints.AddForbiddenConstraint(new SDInvertedCircle(Pending[0].AOE.Origin, 11f), openingActivation.AddSeconds(-1d), openingActivation.AddSeconds(0.6));
    }
}

static class CrucibleScorpionAI
{
    // ARR hits can precede CastFinishAt by ~0.09s. Leave an additional 0.25s timing margin and
    // 0.35y spatial margin for movement/network sampling; these do not change the rendered shape.
    public static void Avoid(AIHints hints, Components.GenericAOEs.AOEInstance aoe)
    {
        var shape = aoe.Shape switch
        {
            AOEShapeCircle circle => new AOEShapeCircle(circle.Radius + 0.35f),
            AOEShapeDonut donut => new AOEShapeDonut(Math.Max(0, donut.InnerRadius - 0.35f), donut.OuterRadius + 0.35f),
            _ => aoe.Shape
        };
        hints.AddForbiddenZone(shape, aoe.Origin, aoe.Rotation, aoe.Activation == default ? default : aoe.Activation.AddSeconds(-0.25d), aoe.Activation == default ? null : aoe.Activation.AddSeconds(0.6));
    }
}

sealed class CrucibleVoidFlareHint(BossModule module) : Components.CastHints(module, [46893u, 46894u, 46895u], "核爆：尽量远离落点，距离越远伤害越低");

sealed class CrucibleVoidFlare(BossModule module) : CriticalEngagement.ReplayValidatedCastAOEs(module)
{
    protected override AOEConfig? ConfigFor(uint actionID) => actionID == 46895
        ? new(new AOEShapeCircle(100f), LocationTargeted: true) : null;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        // 100y is the damage reach, not a lethal/safe-distance threshold. Do not forbid the whole arena.
        foreach (var pending in Pending)
            pending.AOE.Risky = false;
        return base.ActiveAOEs(slot, actor);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc) { }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
        {
            // Small rings identify the impact point, not a claimed safe radius.
            Arena.ZoneCircleOutline(aoe.Origin, 1f, Colors.Danger, 3f);
            Arena.ZoneCircleOutline(aoe.Origin, 2f, Colors.Danger, 2f);
            Arena.AddLine(pc.Position, aoe.Origin, Colors.Danger);
        }
    }
}

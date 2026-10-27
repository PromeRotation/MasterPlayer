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
    protected override AOEConfig? ConfigFor(uint actionID) => battle == Battle.B29
        && (IsRing(actionID) || actionID is 48778 or 48779 or 48780 or 48781 or 48783 or 48785)
        ? new(CrucibleSpells.Table[actionID].Shape, true) : null;

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
                aoe.Risky = aoe.Activation <= firstRing.Value.AddSeconds(0.5);
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

    protected override void AddAOEForbiddenZones(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var aoe in ActiveAOEs(slot, actor))
            CrucibleScorpionAI.Avoid(hints, aoe);
        foreach (var entry in Pending)
        {
            // Wait close to the first circle so the cleared center is reachable
            // before the next ring. Later waves must not chase us into the fence.
            if (entry.ActionID == 48771)
                hints.AddForbiddenConstraint(new SDInvertedCircle(entry.AOE.Origin, 14f), entry.AOE.Activation.AddSeconds(-0.5), entry.AOE.Activation.AddSeconds(0.6));
            else if (entry.ActionID == 48772)
            {
                if (!Pending.ToArray().Any(e => e.ActionID == 48771))
                    hints.AddForbiddenConstraint(new SDInvertedCircle(entry.AOE.Origin, 11.5f), entry.AOE.Activation.AddSeconds(-0.4), entry.AOE.Activation.AddSeconds(0.6));
                break;
            }
        }
    }
}

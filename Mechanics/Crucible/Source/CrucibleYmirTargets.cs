using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Player strategy: sahagin first, then each regenerated shell, then Ymir.
// ARR 2026-09-19: 2198 returns before the second shell spawns, so the body's
// protection must also block attacks during that short object-creation gap.
sealed class CrucibleYmirTargets(BossModule module) : BossComponent(module)
{
    private static bool Alive(Actor actor) => !actor.IsDeadOrDestroyed && actor.HPMP.CurHP > 0;
    private uint RequiredOID => Module.Enemies(19605).Any(Alive) ? 19605u
        : Module.Enemies(19604).Any(Alive) ? 19604u : 19603u;

    private bool CanAttack(Actor actor, uint required) => actor.OID == required && Alive(actor) && actor.IsTargetable
        && (actor.OID != 19603 || actor.FindStatus(2198u) == null);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.OwnsTargetSelection = true;
        var required = RequiredOID;
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.OID is 19603 or 19604 or 19605)
                enemy.Priority = CanAttack(enemy.Actor, required) ? 10 : AIHints.Enemy.PriorityForbidden;

        // This encounter explicitly requests target switching, including while
        // BMR only supplies movement for another rotation plugin.
        var selected = Module.Enemies(required).Where(a => CanAttack(a, required))
            .OrderBy(a => a.InstanceID == actor.TargetID ? 0 : 1)
            .ThenBy(a => (a.Position - actor.Position).LengthSq()).FirstOrDefault();
        if (selected != null)
            hints.ForcedTarget = selected;
        if (WorldState.Actors.Find(actor.TargetID) is { OID: 19603 or 19604 or 19605 } current && !CanAttack(current, required))
            hints.ClearTargetID = current.InstanceID;
        if (actor.CastInfo is { } cast && WorldState.Actors.Find(cast.TargetID) is { OID: 19603 or 19604 or 19605 } castTarget
            && !CanAttack(castTarget, required))
            hints.ForceCancelCast = true;
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (!Module.Enemies(19603).Any(Alive) && !Module.Enemies(19605).Any(Alive))
            return;
        hints.Add(RequiredOID switch
        {
            19605 => "先击杀鱼人，暂不攻击尤弥尔和外壳",
            19604 => "外壳存在：先破壳，再攻击尤弥尔本体",
            _ => Module.Enemies(19603).Any(a => Alive(a) && a.FindStatus(2198u) != null)
                ? "尤弥尔仍受外壳保护：等待外壳出现后击破" : "外壳已破：攻击尤弥尔；新外壳出现后立即转火"
        });
    }
}

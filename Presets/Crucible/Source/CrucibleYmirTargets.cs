using System.Runtime.InteropServices;

namespace MasterPlayer.Mechanics.Crucible;

// Player strategy: sahagin first, then each regenerated shell, then Ymir.
// ARR 2026-09-19: 2198 returns before the second shell spawns, so the body's
// protection must also block attacks during that short object-creation gap.
sealed class CrucibleYmirTargets(BossModule module) : BossComponent(module)
{
    private readonly Dictionary<ulong, DateTime> _spikesPreview = [];
    private static bool Alive(Actor actor) => !actor.IsDeadOrDestroyed && actor.HPMP.CurHP > 0;
    private bool FishProtected(Actor actor) => actor.FindStatus(5434u) != null
        || _spikesPreview.TryGetValue(actor.InstanceID, out var until) && until > WorldState.CurrentTime;
    private uint RequiredOID => Module.Enemies(19605).Any(a => Alive(a) && !FishProtected(a)) ? 19605u
        : Module.Enemies(19604).Any(Alive) ? 19604u : 19603u;

    private bool CanAttack(Actor actor, uint required) => actor.OID == required && Alive(actor) && actor.IsTargetable
        && (actor.OID != 19605 || !FishProtected(actor))
        && (actor.OID != 19603 || actor.FindStatus(2198u) == null);

    // The ACR rejects a spiked fish and temporarily attacks the shell. Agree on both target and
    // movement: forcing the fish here otherwise forbids its only permitted damage target there.
    // run2007: the status arrives 0.72s after the visible cast; use the ACR's bounded 1.5s bridge.
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == 19605 && spell.Action.ID == 50532 && !spell.EventHappened && spell.FinishAt > WorldState.CurrentTime)
            _spikesPreview[caster.InstanceID] = Module.CastFinishAt(spell).AddSeconds(1.5);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID == 19605 && spell.Action.ID == 50532 && spell.Interrupted)
            _spikesPreview.Remove(caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => _spikesPreview.Remove(actor.InstanceID);
    public override void OnActorDeath(Actor actor) => _spikesPreview.Remove(actor.InstanceID);

    internal Actor? Selected(Actor actor)
    {
        var required = RequiredOID;
        return Module.Enemies(required).Where(a => CanAttack(a, required))
            .OrderBy(a => a.InstanceID == actor.TargetID ? 0 : 1)
            .ThenBy(a => (a.Position - actor.Position).LengthSq()).FirstOrDefault();
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        hints.OwnsTargetSelection = true;
        var required = RequiredOID;
        foreach (var enemy in hints.PotentialTargets)
            if (enemy.Actor.OID is 19603 or 19604 or 19605)
                enemy.Priority = CanAttack(enemy.Actor, required) ? 10 : AIHints.Enemy.PriorityForbidden;

        // This encounter explicitly requests target switching, including while
        // Movement only; target selection is left to the rotation plugin.
        var selected = Selected(actor);
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
            _ when Module.Enemies(19605).Any(a => Alive(a) && FishProtected(a))
                => "鱼人麻痹尖刺：先打可攻击的外壳或本体，尖刺消失后回鱼人",
            19604 => "外壳存在：先破壳，再攻击尤弥尔本体",
            _ => Module.Enemies(19603).Any(a => Alive(a) && a.FindStatus(2198u) != null)
                ? "尤弥尔仍受外壳保护：等待外壳出现后击破" : "外壳已破：攻击尤弥尔；新外壳出现后立即转火"
        });
    }
}

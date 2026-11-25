namespace MasterPlayer.Mechanics.Dungeons;

// The caster reference survives death/targetability changes. Finish or module unload ends the intent.
internal sealed record CarnivaleInterruptCast(long Generation, AlphaActorState Caster, DungeonPendingCast Cast)
{
    // Match the source Enemy projection, including Pointless (pending-dead) enemies.
    // The previous MP host did not execute these flags; hints consume the unfiltered caster list.
    internal bool ShouldBeInterrupted => Cast.Rule.Action != 14753 && EligibleEnemy;
    internal bool ShouldBeStunned => Cast.Rule.Action == 14753 && EligibleEnemy;
    private bool EligibleEnemy => Caster.Actor is { Dead: false, Targetable: true, IsEnemy: true, ObjectIndex: < 200 } actor
        && (actor.ObjectIndex.Value & 1) == 0;
}

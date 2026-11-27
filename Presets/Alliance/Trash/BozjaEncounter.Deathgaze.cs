namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsDeathgaze => Definition.Key == "A31DeathgazeHollow";
    internal ulong DeathgazeGreenTargets { get; private set; }

    private void ClearDeathgaze() => DeathgazeGreenTargets = 0;

    private void DeathgazeIcon(BozjaActor actor, uint icon)
    {
        if (!IsDeathgaze || !Active || icon != 70) return;
        var slot = ViewerSlot(actor);
        if (slot is >= 0 and < 64) DeathgazeGreenTargets |= 1UL << slot;
    }

    private void DeathgazeEffect(uint action, ulong target)
    {
        if (!IsDeathgaze || !Active || action != 7308) return;
        var slot = Party.FirstOrDefault(p => p.EntityId == target)?.Slot ?? -1;
        if (slot is >= 0 and < 64) DeathgazeGreenTargets &= ~(1UL << slot);
    }

    internal IEnumerable<(int Slot, BozjaActor Actor)> DeathgazeGreenPlayers()
    {
        if (!IsDeathgaze || !Active) yield break;
        foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot))
            if ((DeathgazeGreenTargets & (1UL << member.Slot)) != 0
                && Actors.GetValueOrDefault(member.EntityId) is { Actor.Dead: false } actor)
                yield return (member.Slot, actor);
    }

    private IEnumerable<BozjaHint> DeathgazeHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.AllianceWindSpread || DeathgazeGreenTargets == 0) yield break;
        var slot = ViewerSlot(viewer);
        var green = slot is >= 0 and < 64 && (DeathgazeGreenTargets & (1UL << slot)) != 0;
        var clipped = DeathgazeGreenPlayers().Any(p => p.Slot < 24 && p.Slot != slot
            && DistanceSquared(viewer.Actor.Position, p.Actor.Actor.Position) <= 25);
        yield return new($"Spread! (debuff: {(green ? "green" : "none")})", clipped);
    }
}

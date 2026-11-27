using MasterPlayer.Replay;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private void ObserveStatuses(BozjaActor actor)
    {
        if (!IsAllianceTrash && !IsVirtue && !IsOwain && !IsCll4 && !IsCll3 && !IsCll1 && !IsGauntlet && !IsDiablo && !IsSaunion && !IsCuchulainn && !IsRobots && !IsQueen && !IsBurn && !IsGrave && !Definition.Rules.Any(r => r.Kind is BozjaRuleKind.DeadLeaves or BozjaRuleKind.Wind or BozjaRuleKind.BracingWind or BozjaRuleKind.BombStack or BozjaRuleKind.Stormcall or BozjaRuleKind.SeekerMercy or BozjaRuleKind.GuardParry)) return;
        var next = new ReplayStatus?[60];
        for (var i = 0; i < actor.Actor.Statuses.Length; ++i)
        {
            var status = actor.Actor.Statuses[i];
            var slot = status.SlotIndex ?? i;
            if (slot is >= 0 and < 60 && status.Id != 0)
                next[slot] = status with
                {
                    SourceId = status.SourceId == 0xE0000000 ? 0 : status.SourceId,
                    ExpiresAt = status.ZeroRemainingTime == true ? Now
                        : status.ExpiresAt == DateTime.MaxValue ? Now.AddSeconds(100000)
                        : Now.AddSeconds(Math.Clamp((float)(status.ExpiresAt - Now).TotalSeconds, 0f, 100000f)),
                };
        }
        for (var i = 0; i < 60; ++i)
        {
            var old = actor.Statuses[i];
            var value = next[i];
            actor.Statuses[i] = value;
            if (!Active || actor.IsPet) continue;
            var identityChanged = old?.Id != value?.Id || old?.SourceId != value?.SourceId;
            if (old != null && identityChanged) Status(actor, old, false);
            if (value != null && (identityChanged || old?.Param != value.Param
                || old == null || (value.ExpiresAt - old.ExpiresAt).TotalSeconds > 1d))
                Status(actor, value, true);
        }
    }

    private void Status(BozjaActor actor, ReplayStatus status, bool gain)
    {
        OzmaStatus(actor, status, gain);
        Cll1Status(actor, status, gain);
        Cll3Status(actor, status, gain);
        OwainStatus(status, gain);
        FireStatus(actor, status, gain);
        SaunionStatus(status, gain);
        DiabloStatus(actor, status, gain);
        GauntletStatus(actor, status, gain);
        ShadowStatus(actor, status, gain);
        SeekerStatus(actor, status, gain);
        GuardStatus(actor, status, gain);
        QueenStatus(actor, status, gain);
        BurnStatus(actor, status, gain, BozjaRuleKind.BurnEscort);
        BurnStatus(actor, status, gain, BozjaRuleKind.BurnWeakpoint);
        GraveStatus(actor, status, gain);
        RobotStatus(actor, status, gain);
        if (IsCuchulainn)
            foreach (var rule in ActiveRules) CuchStatus(actor, status, gain, rule);
        ImmuneStatus(actor, status, gain);
        var slot = Party.FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
        if (slot is < 0 or >= 64) return;
        if (!Definition.Rules.Any(r => r.Kind == BozjaRuleKind.DeadLeaves)) return;
        var bit = 1UL << slot;
        if (status.Id == 2301)
            tenderStatuses = gain ? tenderStatuses | bit : tenderStatuses & ~bit;
        if (status.Id == 2302)
            jealousStatuses = gain ? jealousStatuses | bit : jealousStatuses & ~bit;
    }

    private void ImmuneStatus(BozjaActor actor, ReplayStatus status, bool gain)
    {
        if (!IsAllianceTrash && !IsCll4 && !IsCll3 && !IsGauntlet && !IsDiablo && !IsQueen && !IsSaunion && !Definition.Rules.Any(r => r.Kind is BozjaRuleKind.Wind or BozjaRuleKind.BracingWind)) return;
        var slot = Party.FirstOrDefault(p => p.EntityId == actor.Actor.Id)?.Slot ?? -1;
        if (slot is < 0 or >= 64) return;
        var kind = status.Id switch
        {
            3054 or 160 or 1209 => 0,
            1722 or 2663 => 1,
            2345 => 2,
            _ => -1,
        };
        if (kind >= 0) immunity[slot, kind] = gain ? status.ExpiresAt : default;
    }

    private uint LeavesAction(BozjaActor? viewer)
    {
        var slot = Party.FirstOrDefault(p => p.EntityId == viewer?.Actor.Id)?.Slot ?? -1;
        if (slot is < 0 or >= 64) return 0;
        var bit = 1UL << slot;
        if ((tenderStatuses & bit) != 0) return 20518;
        return (jealousStatuses & bit) != 0 ? 20519u : 0;
    }
}

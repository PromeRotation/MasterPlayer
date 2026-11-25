using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private long carnivaleChargeGeneration;
    internal IEnumerable<CarnivaleChargeCast> CarnivaleCharges => IsCarnivale && Active?.BossOid is 0x26F4 or 0x2724 or 0x2725 or 0x2739
        && carnivaleSelected is { Started: true } module ? module.Charges : [];
    internal CarnivaleChargeKnockback? CarnivaleChargeKnockback => IsCarnivale && Active?.BossOid == 0x26F4
        ? carnivaleSelected?.ChargeKnockback : null;

    private static bool CarnivaleKegAction(uint action) => action is 15054 or 15051 or 15050 or 15052 or 15055;
    private static bool CarnivaleChargeAction(uint action) => action is 14873 or 15055 or 15325;
    private bool CarnivaleChargeModule(CarnivaleModuleState module, uint action) => action switch
    {
        14873 => module.Primary.Actor.Oid == 0x26F4,
        15055 => module.Primary.Actor.Oid is 0x2724 or 0x2725,
        15325 => module.Primary.Actor.Oid == 0x2739,
        _ => false,
    };

    private static bool CarnivaleSunsetAction(uint action) => action is 14686 or 14872 or 14871 or 14876 or 14875 or 14873 or 15268;

    private void CarnivaleChargeStarted(DungeonPendingCast cast, bool correction, bool firstRaw = false)
    {
        if (!IsCarnivale || !CarnivaleChargeAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.ChargeNeedsBootstrap && CarnivaleChargeModule(m, cast.Rule.Action)))
            CarnivaleChargeStarted(module, cast, correction, firstRaw);
    }

    private void CarnivaleChargeStarted(CarnivaleModuleState module, DungeonPendingCast cast, bool correction, bool firstRaw = false)
    {
        if (!carnivaleActors.TryGetValue(cast.Source, out var source)) return;
        var hasInput = module.ChargeInputs.TryGetValue(cast.Source, out var generation);
        if (correction && !hasInput) return;
        if (!correction)
        {
            generation = ++carnivaleChargeGeneration;
            module.ChargeInputs[cast.Source] = generation;
        }
        var index = module.Charges.FindIndex(c => c.Generation == generation);
        // A target absent on the initial event must never be bound by a later correction.
        if (firstRaw)
        {
            if (carnivaleActors.TryGetValue(cast.TargetId, out var rawTarget))
            {
                var confirmed = new CarnivaleChargeCast(generation, source, rawTarget, cast);
                if (index < 0) module.Charges.Add(confirmed);
                else module.Charges[index] = confirmed;
            }
            else if (index >= 0) module.Charges.RemoveAt(index);
        }
        else if (index >= 0)
            module.Charges[index] = module.Charges[index] with { Cast = cast };
        else if (!correction && carnivaleActors.TryGetValue(cast.TargetId, out var target))
            module.Charges.Add(new(generation, source, target, cast));
        if (cast.Rule.Action == 14873 && (!correction || module.ChargeKnockback?.Generation == generation))
            module.ChargeKnockback = new(generation, cast);
    }

    private void CarnivaleChargeBootstrap()
    {
        foreach (var module in carnivaleLoaded.Where(m => m.ChargeNeedsBootstrap))
        {
            module.ChargeNeedsBootstrap = false;
            if (module.Primary.Actor.Oid is 0x2724 or 0x2725)
                module.ChargeKegs.AddRange(carnivaleActorOrder.Where(a => a.Actor.Oid == 0x2726 && !a.Actor.Dead));
            foreach (var cast in CarnivaleOrderedCasts.Where(c => CarnivaleChargeAction(c.Rule.Action) && CarnivaleChargeModule(module, c.Rule.Action)))
                CarnivaleChargeStarted(module, cast, false);
        }
        var roster = new Dictionary<ulong, int>();
        foreach (var member in CurrentPartyMembers) roster.TryAdd(member.EntityId, member.Slot);
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0)) roster.TryAdd(actor.Id, actor.PartySlot);
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid is 0x26F4 or 0x272A or 0x272C or 0x2739 or 0x2C58 or 0x2CF8 or 0x2C5B or 0x2C5D or 0x2C67 or 0x2C6A or 0x2C6C or 0x3FA5 or 0x3FA7))
            module.ChargeImmunity.Observe(_actors.Values, roster);
    }

    private void CarnivaleChargeFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale || !CarnivaleChargeAction(cast.Rule.Action)) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && CarnivaleChargeModule(m, cast.Rule.Action)))
        {
            var index = module.Charges.FindIndex(c => c.Cast.Source == cast.Source);
            if (index >= 0) module.Charges.RemoveAt(index);
            module.ChargeInputs.Remove(cast.Source);
            // Source deliberately clears the slot on *any* matching finish.
            if (cast.Rule.Action == 14873) module.ChargeKnockback = null;
        }
    }

    private static RayRectangleTemplate CarnivaleChargeShape(CarnivaleChargeCast charge) =>
        RayRectangleTemplate.Symmetric(8, Vector2.Distance(
            new(charge.Source.Actor.Position.X, charge.Source.Actor.Position.Z),
            new(charge.Target.Actor.Position.X, charge.Target.Actor.Position.Z)));

    private static float CarnivaleChargeRotation(CarnivaleChargeCast charge) =>
        ReferenceEquals(charge.Source, charge.Target) ? charge.Source.Actor.Rotation
            : MathF.Atan2(charge.Target.Actor.Position.X - charge.Source.Actor.Position.X,
                charge.Target.Actor.Position.Z - charge.Source.Actor.Position.Z);

    private IEnumerable<DungeonActor> CarnivaleRaid => _actors.Values.Where(a => a.PartySlot is >= 0 and < 64 && !a.Dead);

    private int CarnivaleSlot(ulong actor) => CurrentPartyMembers.FirstOrDefault(m => m.EntityId == actor)?.Slot
        ?? _actors.GetValueOrDefault(actor)?.PartySlot
        ?? jobQuestNpcSlots.FirstOrDefault(m => m.Value == actor, new(-1, 0)).Key;

    private void CarnivaleChargeStatus(ulong actor, uint status, bool present, DateTime? expiry)
    {
        if (!IsCarnivale || CarnivaleKnockbackImmunity.Category(status) < 0) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid is 0x26F4 or 0x272A or 0x272C or 0x2739 or 0x2C58 or 0x2CF8 or 0x2C5B or 0x2C5D or 0x2C67 or 0x2C6A or 0x2C6C or 0x3FA5 or 0x3FA7))
            module.ChargeImmunity.Status(CarnivaleSlot(actor), status, present, expiry ?? DateTime.MaxValue);
    }

    internal Vector3 CarnivaleChargeLanding(DungeonActor actor)
    {
        if (CarnivaleChargeKnockback is not { } knockback || carnivaleSelected == null
            || carnivaleSelected.ChargeImmunity.ImmuneAt(CarnivaleSlot(actor.Id), knockback.Cast.FinishAt)) return actor.Position;
        var delta = actor.Position - carnivaleSelected.Primary.Actor.Position;
        delta.Y = 0;
        // AwayFromOrigin, no wall stop; coincident actors do not move.
        return delta.LengthSquared() == 0 ? actor.Position : actor.Position + Vector3.Normalize(delta) * knockback.Distance;
    }

    private IEnumerable<DungeonAmbientAoe> CarnivaleChargeAoes()
    {
        foreach (var charge in CarnivaleCharges.Where(c => !c.Source.Actor.Dead))
        {
            var shape = CarnivaleChargeShape(charge);
            var origin = charge.Source.Actor.Position;
            var rotation = CarnivaleChargeRotation(charge);
            var targetIsViewer = charge.Target.Actor.Id == PlayerId;
            yield return new($"carnivale/charge/{charge.Generation}/display", charge.Cast.Rule.Name, shape, origin, rotation,
                Navigate: false, OutlineOnly: targetIsViewer);
            var targetIsCurrentActor = !charge.Target.Removed
                && carnivaleActors.TryGetValue(PlayerId, out var current) && ReferenceEquals(charge.Target, current);
            if (!targetIsCurrentActor)
            {
                yield return new($"carnivale/charge/{charge.Generation}/avoid", charge.Cast.Rule.Name, shape, origin, rotation,
                    Display: false, Activation: charge.Cast.FinishAt);
                continue;
            }
            if (ReferenceEquals(charge.Source, charge.Target)) continue;
            foreach (var member in CarnivaleRaid.Where(a => a.Id != PlayerId))
            {
                var delta = member.Position - origin;
                var distance = new Vector2(delta.X, delta.Z).Length();
                yield return new($"carnivale/charge/{charge.Generation}/party/{member.Id}", "诱导远离队友",
                    new FanTemplate(100, 2 * MathF.Asin(4 / distance)), origin, MathF.Atan2(delta.X, delta.Z),
                    Display: false, Activation: charge.Cast.FinishAt);
            }
        }
        if (_actors.GetValueOrDefault(PlayerId) is not { } viewer) yield break;
        var to = CarnivaleChargeLanding(viewer);
        foreach (var aoe in QuestKnockbackProjection("carnivale/charge/knockback", viewer, viewer.Position, to))
            yield return aoe;
    }

    private IEnumerable<string> CarnivaleChargeHints(Vector3 position)
    {
        if (Active?.BossOid is 0x2724 or 0x2725)
        {
            if (carnivaleSelected!.Charges.Count != 0 && carnivaleSelected.ChargeKegs.Count != 0)
                yield return "Aim charge at a keg!";
            yield break;
        }
        foreach (var charge in CarnivaleCharges.Where(c => !c.Source.Actor.Dead))
        {
            var shape = CarnivaleChargeShape(charge).Resolve(charge.Source.Actor.Position, CarnivaleChargeRotation(charge), null);
            if (charge.Target.Actor.Id == PlayerId && CarnivaleRaid.Any(a => a.Id != PlayerId && shape.Contains(a.Position)))
                yield return "Bait away from raid!";
            if (charge.Target.Actor.Id != PlayerId && shape.Contains(position))
                yield return "GTFO from baited aoe!";
        }
        if (CarnivaleChargeKnockback != null && _actors.GetValueOrDefault(PlayerId) is { } viewer
            && CarnivaleChargeLanding(viewer) is var landing && landing != viewer.Position && Arena?.Contains(landing) == false)
            yield return "About to be knocked into danger!";
    }

    private IEnumerable<DungeonDamagePrediction> CarnivaleChargeDamage()
    {
        var charges = CarnivaleCharges.ToArray();
        if (charges.Length == 0) yield break;
        foreach (var target in charges.Where(c => !c.Source.Actor.Dead).Select(c => c.Target.Actor).DistinctBy(a => a.Id))
        {
            var slot = CarnivaleSlot(target.Id);
            var member = CurrentPartyMembers.FirstOrDefault(m => m.EntityId == target.Id);
            if (slot is >= 0 and < 64)
                yield return new(target.Id, slot, member?.Role ?? target.PartyRole, target.Name, charges[0].Cast.FinishAt, "Tankbuster");
        }
    }
}

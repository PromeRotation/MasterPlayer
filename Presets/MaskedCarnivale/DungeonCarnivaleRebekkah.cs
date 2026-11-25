using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static bool CarnivaleRebekkahAction(uint action) => action is >= 15073 and <= 15078;
    internal DungeonPendingCast? CarnivaleReflectCast => IsCarnivale && Active?.BossOid is 0x2727 or 0x2728
        ? carnivaleSelected?.ReflectCast : null;
    internal DungeonPendingCast? CarnivaleBlindTrigger => IsCarnivale && Active?.BossOid == 0x2728
        ? carnivaleSelected?.BlindTrigger : null;

    private void CarnivaleRebekkahStarted(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.RebekkahNeedsBootstrap
            && m.Primary.Actor.Oid is 0x2727 or 0x2728))
            CarnivaleRebekkahStarted(module, cast);
    }

    private static void CarnivaleRebekkahStarted(CarnivaleModuleState module, DungeonPendingCast cast)
    {
        if (cast.Rule.Action == 15073)
        {
            module.ReflectCast = cast;
            module.ReflectCasting = true;
        }
        if (module.Primary.Actor.Oid == 0x2728 && cast.Rule.Action == 15077)
        {
            module.BlindTrigger = cast;
            module.BlindCasting = true;
        }
        // The source overrides CastGaze's callbacks without calling base.
        // Explosive Dehiscence never adds an eye, even when the viewer is not blinded.
    }

    private void CarnivaleRebekkahFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.RebekkahNeedsBootstrap
            && m.Primary.Actor.Oid is 0x2727 or 0x2728))
        {
            if (cast.Rule.Action == 15073)
            {
                module.ReflectCast = cast;
                module.ReflectCasting = false;
                module.ReflectActive = true;
            }
            if (module.Primary.Actor.Oid == 0x2728 && cast.Rule.Action == 15078)
                module.BlindCasting = false;
        }
    }

    private void CarnivaleRebekkahUpdate()
    {
        if (!IsCarnivale) return;
        var roster = new Dictionary<ulong, int>();
        foreach (var member in CurrentPartyMembers) roster.TryAdd(member.EntityId, member.Slot);
        foreach (var actor in _actors.Values.Where(a => a.PartySlot >= 0)) roster.TryAdd(actor.Id, actor.PartySlot);
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid is 0x2727 or 0x2728))
        {
            var bootstrap = module.RebekkahNeedsBootstrap;
            if (bootstrap)
            {
                module.RebekkahNeedsBootstrap = false;
                foreach (var cast in CarnivaleOrderedCasts)
                    CarnivaleRebekkahStarted(module, cast);
            }
            if (module.Primary.Actor.Oid == 0x2728)
                module.Blindness.Observe(_actors.Values, roster, bootstrap);
        }
    }

    private void CarnivaleBlindStatus(ulong actor, uint status, bool present)
    {
        if (!IsCarnivale || status != 15) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid == 0x2728))
            module.Blindness.Status(CarnivaleSlot(actor), present);
    }

    private IEnumerable<string> CarnivaleRebekkahHints(Vector3 position)
    {
        if (Active?.BossOid is not (0x2727 or 0x2728) || carnivaleSelected is not { } module) yield break;
        if (module.ReflectCasting)
            yield return "Boss will reflect all magic damage!";
        else if (module.ReflectActive)
            yield return "Boss reflects all magic damage!";
        if (module.Primary.Actor.Oid == 0x2728 && module.BlindCasting && !module.Blindness.Blinded(CarnivaleSlot(PlayerId)))
            yield return "Cast Ink Jet on boss to get blinded!";
        if (CarnivaleClawAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
    }
}

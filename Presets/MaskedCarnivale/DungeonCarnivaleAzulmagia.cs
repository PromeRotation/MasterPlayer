using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static bool CarnivaleAzulmagiaAction(uint action) => action is 14766 or 14767 or 14763 or 14764
        or 14765 or 14774 or 14775 or 14773 or 14771 or 14770;

    private bool CarnivaleHasStatus(DungeonActor actor, uint status)
        => statusChanges.TryGetValue((actor.Id, status), out var change) && _now < change.ChangedAt.AddSeconds(.5)
            ? change.Present
            : actor.StatusSlots?.Any(s => s.Id == status) ?? actor.Statuses?.Contains(status) == true;

    private void CarnivaleAzulmagiaStarted(DungeonPendingCast cast, bool correction)
    {
        if (!IsCarnivale || correction || cast.Rule.Action != 14770) return;
        foreach (var module in carnivaleLoaded.Where(m => m.Started && !m.AzulmagiaNeedsBootstrap && m.Primary.Actor.Oid == 0x2680))
            module.WebCasting = true;
    }

    private void CarnivaleAzulmagiaFinished(DungeonPendingCast cast)
    {
        if (!IsCarnivale || cast.Rule.Action != 14770) return;
        // The source is one global boolean: any matching Finish clears it,
        // even while another source is still casting Web.
        foreach (var module in carnivaleLoaded.Where(m => m.Started && m.Primary.Actor.Oid == 0x2680))
            module.WebCasting = false;
    }

    private void CarnivaleAzulmagiaBootstrap()
    {
        if (!IsCarnivale) return;
        foreach (var module in carnivaleLoaded.Where(m => m.AzulmagiaNeedsBootstrap))
        {
            module.AzulmagiaNeedsBootstrap = false;
            if (module.Primary.Actor.Oid == 0x2680)
                module.WebCasting = CarnivaleOrderedCasts.Any(c => c.Rule.Action == 14770);
        }
    }

    private IEnumerable<string> CarnivaleAzulmagiaHints(Vector3 position)
    {
        if (Active?.BossOid is not (0x2678 or 0x267F or 0x2680) || carnivalePrimary == null) yield break;
        if (Active.BossOid != 0x2678 && CarnivaleHasStatus(carnivalePrimary, 556))
            yield return $"{carnivalePrimary.Name} will reflect all magic damage!";
        else if (Active.BossOid != 0x267F && CarnivaleHasStatus(carnivalePrimary, 1307))
            yield return $"{carnivalePrimary.Name} will reflect all physical damage!";
        if (_actors.GetValueOrDefault(PlayerId) is { } player && CarnivaleHasStatus(player, 910))
            yield return "You were doomed! Cleanse it with Exuviation or finish the act fast.";
        if (Active.BossOid == 0x267F && CarnivaleEnemies(0x2682).FirstOrDefault(a => !a.Dead) is { } angon)
            yield return $"Kill {angon.Name}! Use physical attacks except fire aspected.";
        if (Active.BossOid != 0x2680) yield break;
        if (carnivaleSelected!.WebCasting)
            yield return "Bait the Meteor to the edge of the arena!\nUse Loom to escape or Diamondback to survive.";
        if (CarnivaleClawAoes().Any(a => a.Shape.Resolve(a.Position, a.Rotation, null).Contains(position)))
            yield return "GTFO from voidzone!";
    }
}

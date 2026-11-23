namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? incendiarySupport;

    private bool CastrumCastVisible(DungeonPendingCast cast)
    {
        return cast.Rule.Action switch
        {
            28787 => !_casts.Values.Any(c => c.Rule.Action == 29163),
            29356 => !_casts.Values.Any(c => c.Rule.Action == 28790),
            _ => true,
        };
    }

    private IEnumerable<DungeonDamagePrediction> RaidwideDamage
    {
        get
        {
            var casts = _casts.Values.Where(c => c.Rule.Hint == "全屏伤害" && c.Rule.Action != 29891);
            if (incendiarySupport != null) casts = casts.Append(incendiarySupport);
            if (snowDrift != null) casts = casts.Append(snowDrift);
            if (keeperDisgust != null) casts = casts.Append(keeperDisgust);
            foreach (var cast in casts)
                foreach (var actor in _actors.Values.Where(a => !a.Dead && a.PartySlot >= 0))
                    yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, cast.FinishAt, "Raidwide");
        }
    }
}

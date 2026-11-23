using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? goobbuesGrief;
    private ulong bavaroisKiter;

    private void WandererCast(DungeonPendingCast cast)
    {
        if (cast.Rule.Action == 950)
            goobbuesGrief = cast with
            {
                Rule = new(942, 0x41C, "GoobbuesGrief", new CircleTemplate(8.85f)),
                FinishAt = cast.FinishAt.AddSeconds(1.1),
            };
        else if (cast.Rule.Action == 942)
            goobbuesGrief = cast;
    }

    private void SynchronizeWanderer()
    {
        if (goobbuesGrief != null && _actors.TryGetValue(goobbuesGrief.Source, out var boss) && goobbuesGrief.Location != boss.Position)
            goobbuesGrief = goobbuesGrief with { Location = boss.Position };
        if (bavaroisKiter != 0 && (!_actors.TryGetValue(bavaroisKiter, out var kiter) || kiter.Dead))
            bavaroisKiter = 0;
    }

    private IEnumerable<DungeonAmbientAoe> WandererAoes()
    {
        if (Active?.BossOid == 0x41D && bavaroisKiter != 0 && bavaroisKiter == PlayerId && _actors.TryGetValue(PrimaryId, out var boss))
            yield return new("bavarois/kite", "点名：远离首领8码", new CircleTemplate(8), boss.Position);
    }
}

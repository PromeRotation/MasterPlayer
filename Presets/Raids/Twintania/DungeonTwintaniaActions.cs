namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal IReadOnlyList<DungeonActionRequest> TwintaniaActions()
    {
        if (!IsTwintania || Active == null || _actors.GetValueOrDefault(PlayerId) is not { Dead: false } player) return [];
        List<DungeonActionRequest> actions = [];
        void Add(uint action, ulong target, DateTime until)
        {
            if (TwintaniaActionAvailable(action, player.ClassJob) && until > _now) actions.Add(new(action, target, until));
        }
        var role = CoilRole(player);
        if (TwinCommonActive && _casts.GetValueOrDefault(PrimaryId) is { Rule.Action: 1458 } cast)
        {
            var until = cast.FinishAt.AddSeconds(-.3);
            if (role is "MT" or "OT" && role != TwinTankRole)
            {
                var first = (twinSentenceHits & 2) == 0;
                if (player.ClassJob == 21)
                {
                    if (first) Add(44, PlayerId, until);
                    else { Add(7531, PlayerId, until); Add(7535, PlayerId, until); Add(40, PlayerId, until); }
                }
                if (player.ClassJob == 19)
                {
                    Add(3542, PlayerId, until);
                    if (first) Add(17, PlayerId, until);
                    else { Add(7531, PlayerId, until); Add(7535, PlayerId, until); }
                }
            }
            if (role == "M1" && twinSentenceHits % 3 == 0 || role == "M2" && twinSentenceHits % 3 == 1)
                Add(7549, PrimaryId, until);
            if (role is "R1" or "R2" && twinSentenceHits % 3 == 2) Add(7560, PrimaryId, until);
        }
        if (twinStage == 17 && TwinCombatRole(player) == "Ranged") Add(7560, PrimaryId, twinProfusionAt);
        if (TwinP4 && twinDreadTarget != 0)
            foreach (var knight in _actors.Values.Where(a => a.Oid == 0x7EA && !a.Dead))
            {
                Add(7554, knight.Id, DateTime.MaxValue);
                Add(7540, knight.Id, DateTime.MaxValue);
                Add(7863, knight.Id, DateTime.MaxValue);
            }
        return actions;
    }

    private static bool TwintaniaActionAvailable(uint action, uint job) => action switch
    {
        44 or 40 => job == 21,
        3542 or 17 => job == 19,
        7531 or 7535 or 7540 => job is 1 or 3 or 19 or 21 or 32 or 37,
        7549 or 7863 => job is 2 or 4 or 20 or 22 or 29 or 30 or 34 or 39 or 41,
        7554 => job is 5 or 23 or 31 or 38,
        7560 => job is 7 or 25 or 26 or 27 or 35 or 36 or 42,
        _ => false,
    };
}

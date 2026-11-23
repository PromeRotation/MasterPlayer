namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTitanExtreme => territory == TitanExtremeCatalog.TERRITORY;
    private bool TitanExCombat => titanExPhase is 0 or 2 or 4 or 6;
    private bool titanExWiping;
    private int titanExPhase = -1;
    private readonly List<TitanExtremeStep> titanExSteps = [];
    private int titanExStep;
    private DateTime titanExEnteredAt;
    private int titanExMaxBombs = 9;
    private bool titanExKillBomb;
    private uint titanExBuster;
    private int titanExBusterHits;
    private DateTime titanExBusterAt;
    private bool titanExTumult;
    private int titanExTumultHits;
    private DateTime titanExTumultAt;
    private float titanExCrushRadius;
    private DateTime titanExHoldUntil;
    private readonly HashSet<ulong> titanExFetters = [];
    private DateTime titanExFettersAt;
    private readonly List<ulong> titanExBursts = [];
    private int TitanExBurstCount => titanExBursts.Count;
    private DungeonActor? TitanExBoss => _actors.GetValueOrDefault(PrimaryId);
    private DungeonActor? TitanExHeart => _actors.Values.FirstOrDefault(a => a.Oid == 0x5E5);
    private DungeonCastObservation? TitanExBossCast => TitanExBoss?.Cast;
    private bool TitanExCasting => _casts.ContainsKey(PrimaryId) || TitanExBossCast != null;

    private void TitanExDirector(uint command)
    {
        if (!IsTitanExtreme) return;
        if (command == 0x40000005) { titanExWiping = true; ResetFight(); }
        if (command == 0x40000011) titanExWiping = false;
    }

    private void TitanExObserve()
    {
        if (!IsTitanExtreme || Active == null || TitanExBoss is not { } boss) return;
        if (titanExPhase < 0) TitanExEnterPhase(0);
        // Phase predicates precede the per-mechanic state, including early HP transitions.
        for (var i = 0; i < 7; ++i)
        {
            var next = titanExPhase switch
            {
                0 or 2 => !boss.Targetable,
                1 or 5 => boss.Targetable,
                3 => TitanExHeart?.Targetable == true,
                4 => TitanExHeart?.Targetable != true,
                _ => false,
            };
            if (!next) break;
            TitanExEnterPhase(titanExPhase + 1);
        }
        if (titanExKillBomb && !_actors.Values.Any(a => a.Oid == 0x5E0)) titanExKillBomb = false;
        for (var transitions = 0; transitions < titanExSteps.Count; ++transitions)
        {
            var step = titanExSteps[titanExStep];
            var elapsed = (_now - titanExEnteredAt).TotalSeconds;
            if (elapsed < step.CheckDelay) break;
            if (!step.Ready())
            {
                if (step.MaxOverdue is not { } overdue || elapsed < step.Expected + overdue) break;
                warning?.Invoke($"Titan Extreme：{step.Name} 未收到预期效果，按源超时推进。");
            }
            if (titanExStep + 1 >= titanExSteps.Count) break;
            step.Exit?.Invoke();
            ++titanExStep;
            titanExEnteredAt = _now;
            titanExSteps[titanExStep].Enter?.Invoke();
        }
    }

    private void TitanExEnterPhase(int phase)
    {
        titanExPhase = phase;
        titanExBuster = 0;
        titanExTumult = false;
        titanExCrushRadius = 0;
        titanExHoldUntil = default;
        titanExFetters.Clear();
        titanExBursts.Clear();
        titanExMaxBombs = 9;
        titanExKillBomb = false;
        // Components are recreated at each phase; old helpers cannot leak into the new phase.
        foreach (var id in _casts.Keys.ToArray()) _casts.Remove(id);
        titanExSteps.Clear();
        TitanExBuildPhase(phase);
        titanExSteps.Add(new("阶段结束等待", 1000, () => false));
        titanExStep = 0;
        titanExEnteredAt = _now;
        titanExSteps[0].Enter?.Invoke();
        if (phase is 2 or 4 or 6)
        {
            foreach (var actor in _actors.Values)
                if (actor.Tethers?.TryGetValue(7u, out var partner) == true)
                    TitanExTether(actor.Id, partner, 7, true, _now);
            foreach (var actor in _actors.Values.Where(a => HasStatus(a, 292))) titanExFetters.Remove(actor.Id);
        }
    }

    private bool TitanExCastAllowed(uint action) => action switch
    {
        1466 or 1477 => titanExPhase is 2 or 4 or 6,
        1467 or 1468 or 1475 or 1469 or 1470 or 1471 => TitanExCombat,
        1472 => titanExCrushRadius > 0,
        1474 => true,
        _ => false,
    };

    private void TitanExCast(DungeonPendingCast cast)
    {
        if (!IsTitanExtreme) return;
        if (cast.Rule.Action == 1471 && !titanExBursts.Contains(cast.Source)) titanExBursts.Add(cast.Source);
        if (cast.Rule.Action == 1466) titanExHoldUntil = cast.FinishAt.AddSeconds(1);
        TitanExObserve();
    }

    private void TitanExFinish(DungeonPendingCast cast, DateTime at)
    {
        if (!IsTitanExtreme) return;
        if (cast.Rule.Action == 1471) titanExBursts.Remove(cast.Source);
        if (cast.Rule.Action == 1466) titanExHoldUntil = at.AddSeconds(1);
        // The common Finish caller removes the pending cast after notifying consumers.
        // Cast-end advancement happens on the following observation, using the real null cast.
    }

    private void TitanExEffect(uint action, ulong target, DateTime at)
    {
        if (!IsTitanExtreme) return;
        if (action == titanExBuster && titanExBuster != 0) ++titanExBusterHits;
        if (action == 1465 && titanExTumult)
        {
            ++titanExTumultHits;
            titanExTumultAt = at.AddSeconds(1.2);
        }
        if (TitanExCombat && titanExPhase != 0 && action is 645 or 1652
            && _actors.GetValueOrDefault(target)?.PartySlot >= 0 && titanExFetters.Add(target))
            titanExFettersAt = at.AddSeconds(2.9);
    }

    private void TitanExTether(ulong source, ulong target, uint tether, bool present, DateTime at)
    {
        if (!IsTitanExtreme || !TitanExCombat || titanExPhase == 0 || tether != 7 || !present) return;
        if (_actors.GetValueOrDefault(source)?.PartySlot >= 0) titanExFetters.Add(source);
        if (_actors.GetValueOrDefault(target)?.PartySlot >= 0) titanExFetters.Add(target);
        titanExFettersAt = at.AddSeconds(2.9);
    }

    private void ResetTitanEx()
    {
        titanExPhase = -1;
        titanExSteps.Clear();
        titanExFetters.Clear();
        titanExBursts.Clear();
        titanExBuster = 0;
        titanExTumult = titanExKillBomb = false;
        titanExCrushRadius = 0;
        titanExHoldUntil = default;
        titanExMaxBombs = 9;
    }
}

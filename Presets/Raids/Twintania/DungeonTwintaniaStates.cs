namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTwintania => territory == TwintaniaCatalog.TERRITORY;
    private int twinStage = -1;
    private DateTime twinEntered;
    private bool twinWiping;
    private bool twinAddsActive;
    private bool TwinCommonActive => IsTwintania && Active != null && twinStage is 0 or 1 or 18 or 19;
    private bool TwinDiveActive => IsTwintania && Active != null && twinStage is >= 2 and <= 16;
    private bool TwinP2 => IsTwintania && Active != null && twinStage == 1;
    private bool TwinP4 => IsTwintania && Active != null && twinStage == 18;
    private bool TwinP5 => IsTwintania && Active != null && twinStage == 19;
    private IEnumerable<DungeonActor> TwinNeurolinks => _actors.Values.Where(a => a.Oid == 0x7E6);

    private void TwinDirector(uint command)
    {
        if (!IsTwintania) return;
        if (command == 0x40000005) { twinWiping = true; ResetFight(); }
        if (command == 0x40000011) twinWiping = false;
    }

    private void TwinTick()
    {
        if (!IsTwintania || Active == null) return;
        if (twinStage < 0) TwinEnter(0);
        for (var i = 0; i < 20 && twinStage < 19; ++i)
        {
            var elapsed = (float)(_now - twinEntered).TotalSeconds;
            var boss = _actors.GetValueOrDefault(PrimaryId);
            var advance = twinStage switch
            {
                0 => TwinNeurolinks.Any() || elapsed >= 1100,
                1 => TwinNeurolinks.Count() > 1 || elapsed >= 1100,
                2 => boss?.Targetable == false,
                3 => twinDiveTarget != null || elapsed >= 5.2f + 1,
                4 => twinDiveTarget == null || elapsed >= 1.7f + 1,
                5 => twinDiveTarget != null || elapsed >= 5.9f + 1,
                6 => twinDiveTarget == null || elapsed >= 1.7f + 1,
                7 => twinDiveTarget != null || elapsed >= 6.2f + 1,
                8 => twinDiveTarget == null || elapsed >= 1.7f + 1,
                9 => _actors.Values.Any(a => a.Oid == 0x7E9 && a.Targetable) || elapsed >= 2.2f + 1,
                10 => twinDiveTarget != null || elapsed >= 45.5f + 1,
                11 => twinDiveTarget == null || elapsed >= 1.7f + 1,
                12 or 14 => twinDiveTarget != null || elapsed >= 5.7f + 1,
                13 or 15 => twinDiveTarget == null || elapsed >= 1.7f + 1,
                16 => boss?.Targetable == true,
                17 => twinProfusionHits > 0 || elapsed >= 6.7f + 1,
                18 => TwinNeurolinks.Count() > 2 || elapsed >= 1100,
                _ => false,
            };
            if (!advance) break;
            TwinEnter(twinStage + 1);
        }
    }

    private void TwinEnter(int stage)
    {
        if (twinStage == 0) twinPuddles.Clear();
        if (twinStage == 1)
        {
            twinFireball = 0; twinConflag = 0; twinPuddles.Clear();
            foreach (var key in _casts.Where(p => p.Value.Rule.Action == 1243).Select(p => p.Key).ToArray()) _casts.Remove(key);
        }
        if (twinStage == 16) twinDiveTarget = null;
        if (twinStage == 18)
        {
            twinTwisterPositions.Clear(); twinDreadTarget = 0;
            foreach (var key in _casts.Where(p => p.Value.Rule.Action == 1249).Select(p => p.Key).ToArray()) _casts.Remove(key);
        }
        twinStage = stage;
        twinEntered = _now;
        if (stage is 0 or 18)
        {
            twinNextPlummet = _now.AddSeconds(6.5);
            twinNextSentence = _now.AddSeconds(18);
            twinTankedByOT = false; twinSentenceHits = 0; twinSentenceCasts.Clear();
        }
        if (stage == 1)
        {
            twinNextFireball = _now.AddSeconds(7.5);
            twinNextConflag = _now.AddSeconds(29);
        }
        if (stage == 9) twinAddsActive = true;
        if (stage == 17) { twinProfusionAt = _now.AddSeconds(6.7); twinProfusionHits = 0; }
        if (stage == 19) { twinLiquidTarget = 0; twinHatchTarget = 0; twinLiquidHits = 0; twinPuddles.Clear(); }
    }

    private bool TwinCastAllowed(uint action) => action switch
    {
        1458 => TwinCommonActive,
        1243 => twinStage == 0,
        1247 => TwinDiveActive,
        1249 => TwinP4,
        670 => TwinP5,
        _ => false,
    };

    private void ResetTwintania()
    {
        twinStage = -1; twinEntered = default; twinAddsActive = false;
        twinSentenceCasts.Clear(); twinFettered.Clear(); twinPuddles.Clear(); twinTwisterPositions.Clear();
        twinDisseminate.Clear(); twinPuddleKey = 0; twinSentenceHits = 0; twinLiquidHits = 0; twinProfusionHits = 0;
        twinMarkedActors.Clear();
        twinTankedByOT = false; twinFireball = 0; twinConflag = 0; twinDreadTarget = 0; twinHatchTarget = 0; twinLiquidTarget = 0;
        twinDiveTarget = null; twinNextSentence = default; twinNextPlummet = default; twinNextFireball = default;
        twinNextConflag = default; twinFireballAt = default; twinConflagAt = default; twinDiveAt = default; twinProfusionAt = default;
    }
}

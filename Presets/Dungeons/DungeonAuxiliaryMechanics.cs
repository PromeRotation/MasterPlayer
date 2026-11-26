using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    public ulong PlayerId { get; private set; }
    public IEnumerable<DungeonPendingCast> Gazes => _casts.Values.Where(c => c.Rule.Action is 28648 or 4331 or 5216 or 7364 or 8066 or 7940 or 7949 or 9829
        || IsTactical && c.Rule.Action is 40743 or 40757
        || IsHeavenOnHigh && c.Rule.Action is 11900 or 11922
        || territory == HeavenswardIntroCatalog.EDDA && c.Rule.Action == 6403
        || territory == JobQuestCatalog.SOMEWHERE_ONLY_SHE_KNOWS && c.Rule.Action is 37523 or 37537
        || IsGamboling && c.Rule.Action == 17198
        || IsFadedThordan && c.Rule.Action == 21090
        || HasTreasureGaze && NativeTreasureCatalog.GazeAction(c.Rule.Action) && !TreasurePlayerGazeAction(c.Rule.Action)
        || territory == DohnMhegCatalog.TERRITORY && c.Rule.Action == 13552
        || territory == WorqorCatalog.TERRITORY && c.Rule.Action == 36297
        || territory == LakshmiCatalog.TERRITORY && c.Rule.Action == 9613
        || IsDarkness && Active?.BossOid == 0xE00 && c.Rule.Action == 3281
        || HasCarnivaleGaze && c.Rule.Action is 14951 or 14875
        || IsInnocence && c.Rule.Action == 16025).Concat(AntitowerGazes()).Concat(TroiaGazes()).Concat(LapisGazes()).Concat(QueenGazes()).Concat(TreasurePlayerGazes()).Concat(CarnivaleDemonEyes);

    public ulong PriorityTarget(ulong current, Vector3 player)
    {
        if (IsQuantum) return QuantumPriorityTarget(current, player);
        var initial = DamagePriorityTarget(current, player);
        return NativeTankTarget(initial, player)?.Actor.Id ?? initial;
    }

    private ulong DamagePriorityTarget(ulong current, Vector3 player)
    {
        if (IsNativeFate) return FatePriorityTarget(current, player);
        if (IsNovice) return NovicePriorityTarget(current, player);
        if (IsNativeTreasure) return TreasurePriorityTarget(current, player);
        if (IsMainScenarioQuest || IsBarPassage || IsCarnivale) return MainScenarioPriorityTarget(current, player);
        if (IsFallenKuribu || IsSummoningNode) return FallenPriorityTarget(current, player);
        if (IsNpcPartyQuest || IsIo || IsRaisingSword) return JobQuestPriorityTarget(current, player);
        var haveTail = _actors.Values.Any(a => a.Oid == 0x10A && !a.Dead);
        int Priority(DungeonActor a) => Active?.BossOid switch
        {
            _ when IsHeavenswardIntro => HeavenswardIntroPriority(a),
            _ when IsOrcus && a.Oid is 0x3666 or 0x3667 => 1,
            _ when IsCaduceus => CaduceusPriority(a),
            _ when IsTwintania => TwinPriority(a),
            _ when IsUltimaExtreme => a.Oid switch { 0x915 => 2, 0x90F => 1, _ => 0 },
            _ when IsGarudaExtreme => GarudaExPriority(a),
            _ when IsTitanExtreme => TitanExPriority(a),
            _ when IsIfritExtreme => IfritExPriority(a),
            0x1A1 => a.Oid switch { 0x1A2 => 2, 0x1A1 => 1, _ => 0 },
            0x4C => a.Oid == 0x7D ? 1 : 0,
            _ when IsCoilNodes => CoilPriority(a),
            _ when IsCoilGauntlet => GauntletPriority(a),
            _ when IsArrTrial => ArrTrialPriority(a),
            _ when IsPorta => PortaPriority(a),
            _ when IsHardTrial => HardTrialPriority(a),
            _ when IsThornmarch => ThornmarchCatalog.IsEnemy(a.Oid) ? 1 : 0,
            _ when IsWhorleater => WhorlPriority(a),
            _ when IsShinryu => a.Oid is 0x1B14 or 0x1C83 ? 1 : 0,
            _ when IsTsukuyomi => TsukuyomiCatalog.IsAdd(a.Oid) ? 1 : 0,
            _ when IsYojimbo => a.Oid == 0x2602 ? 1 : 0,
            _ when IsTitania => TitaniaCatalog.IsAdd(a.Oid) ? 1 : 0,
            _ when IsHades => HadesGaolAlive && a.Oid == 0x294B ? 1 : 0,
            _ when IsEnuo => a.Oid is 0x4DBE or 0x4DBD ? 1 : 0,
            _ when IsNecron => a.Oid == 0x4870 ? 0 : 1,
            0x4891 when IsMesoHeadsmen => MesoPriority(a),
            0x33EF => a.Oid == 0x33F0 ? 0 : 1,
            0x33F1 => a.Oid switch { 0x33F1 => 3, 0x33F3 => 2, _ => 1 },
            0x103 => a.Oid switch { 0x10A => 2, 0x104 => haveTail ? 2 : 1, 0x103 => 1, _ => 0 },
            0x112 => a.Oid switch { 0x111 => 2, 0x112 => 1, _ => 0 },
            0x38A8 => a.Oid switch { 0x38AA => 2, 0x38A8 => 1, _ => 0 },
            0x1A7 => a.Oid switch { 0x1A9 => 3, 0x1AA => 2, 0x1A7 => 1, _ => 0 },
            0x3DE => a.Oid switch { 0x3DF => 2, 0x3DE => 1, _ => 0 },
            0x1AB => a.Oid switch { 0x428 => 2, 0x1AB => 1, _ => 0 },
            0x477A => a.Oid switch { 0x477B => 2, 0x477A => 1, _ => 0 },
            0x477C => a.Oid switch { 0x477D => 2, 0x477C => 1, _ => 0 },
            0x477E => a.Oid switch { 0x477F or 0x4780 => 2, 0x477E => 1, _ => 0 },
            0x48FB => a.Oid switch { 0x48FC => 2, 0x48FB => 1, _ => 0 },
            0x4A8D => a.Oid switch
            {
                0x4A8E => 2,
                0x4A8D when !HasStatus(a, DzemaelCatalog.INVINCIBILITY) => 1,
                _ => 0,
            },
            0x4B0E => a.Oid switch { 0x4B10 or 0x4B0F => 2, 0x4B0E => 1, _ => 0 },
            0x3873 => a.Oid switch { 0x3874 => 2, 0x3873 => 1, _ => 0 },
            0x3875 => a.Oid switch { 0x3877 => 2, 0x3875 => 1, _ => 0 },
            0x374 => a.Oid == 0x374 ? 1 : 0,
            0x5D1 => a.Oid switch { 0x5D3 => 4, 0x5D2 => 3, 0x5D1 => 2, 0x5D0 => 1, _ => 0 },
            0x5CC => a.Oid switch { 0x5CD => 2, 0x5CC => 1, _ => 0 },
            0x8FB => a.Oid switch { 0x8FC => 2, 0x8FB => 1, _ => 0 },
            0x8F4 => a.Oid switch { 0x8F5 or 0x8F6 => 2, 0x8F4 => 1, _ => 0 },
            0x918 => a.Oid switch { 0x91A => 2, 0x918 => 1, _ => 0 },
            0x8EF => a.Oid switch { 0x8F0 or 0x8F1 => 2, 0x8EF => 1, _ => 0 },
            0xD1E => IsSnowcloakTrash(a) ? 1 : 0,
            0xD07 => a.Oid switch { 0xD09 => 2, 0xD07 => 1, _ => 0 },
            0xE68 => a.Oid switch { 0xE71 or 0xE72 or 0xE6F or 0xE70 or 0xE69 => 2, 0xE68 => 1, _ => 0 },
            0xE8F => a.Oid switch { 0x13C2 => 2, 0xE8F => 1, _ => 0 },
            0xE91 => a.Oid switch { 0xE92 => 2, 0xE91 => 1, _ => 0 },
            0xE96 => a.Oid switch { 0x10B5 or 0x10B4 => 2, 0xE96 => 1, _ => 0 },
            0xEA6 => a.Oid switch { 0xEA7 => 2, 0xEA6 => 1, _ => 0 },
            0x39CA => a.Oid switch { 0x39CB => 3, 0x39CC or 0x39CD => 2, 0x39CA when !AeryBossForbidden => 1, _ => 0 },
            0x1051 => a.Oid switch { 0x1050 or 0x104F => 2, 0x104E or 0x1051 => 1, _ => 0 },
            0x1054 => a.Oid is 0x1053 or 0x1054 ? 1 : 0,
            0x1056 => a.Oid switch { 0x1059 => 2, 0x1056 => 1, _ => 0 },
            0xE82 => a.Oid == 0xE82 ? 1 : 0,
            0xE83 when !GubalBossForbidden(a) => a.Oid switch { 0x10B7 => 2, 0xE83 => 1, _ => 0 },
            0xE84 => a.Oid switch { 0xE85 or 0xE86 or 0xE87 => 2, 0xE84 => 1, _ => 0 },
            0xE97 => a.Oid is 0xE97 or 0xE98 or 0xE99 or 0xF5C ? 1 : 0,
            0xE9A => a.Oid == 0xE9A ? 1 : 0,
            0x3DA4 => a.Oid is 0x3DA4 or 0x3DA3 ? 1 : 0,
            0x3DA7 => a.Oid switch { 0x3DAC => 2, 0x3DA7 => 1, _ => 0 },
            0x1012 => a.Oid == 0x1012 ? 1 : 0,
            0x1013 => a.Oid is 0x1014 or 0x1015 or 0x1016 ? 1 : 0,
            0x1018 => a.Oid == 0x1019 ? 1 : 0,
            0x142F => a.Oid switch { 0x1431 => 2, 0x142F => 1, _ => 0 },
            0x1432 when !HasStatus(a, 152) => a.Oid is 0x1432 or 0x1433 ? 1 : 0,
            0x1434 => a.Oid switch { 0x1436 => 2, 0x1434 => 1, _ => 0 },
            0x14FC => a.Oid switch { 0x14FD => 2, 0x14FC => 1, _ => 0 },
            0x1503 when !AntParryForbidden(a) && AntitowerCatalog.IsPlayerDoll(a.Oid) => 2,
            0x1503 when !AntParryForbidden(a) && AntitowerCatalog.IsNpcDoll(a.Oid) => 1,
            0x15FB => SohrPriority(a),
            0x179B when !HasStatus(a, 775) => a.Oid switch { 0x179D => 2, 0x179E => 1, _ => 0 },
            0x1938 => a.Oid == 0x1939 ? 1 : 0,
            0x1B0C => a.Oid switch { 0x1B2A => 2, 0x1B0D => 1, _ => 0 },
            0x1B0F => a.Oid switch { 0x1B10 or 0x1C87 => 2, 0x1B11 => 1, _ => 0 },
            0x1AA5 => 0,
            0x1FAC => a.Oid switch { 0x1FAD => 2, 0x1FAC when !HasStatus(a, 325) => 1, _ => 0 },
            0x23AA => a.Oid == 0x23AD ? 1 : 0,
            0x2431 => a.Oid switch { 0x2433 => 2, 0x2432 => 1, _ => 0 },
            0x278C when holminsterChained && PlayerId != holminsterChainTarget => a.Oid == 0x2895 ? 1 : 0,
            0x98E => a.Oid switch { 0xF08 => 2, 0x98E => 1, _ => 0 },
            0xF14 => a.Oid switch { 0xF63 when !DohnForbidden(a) => 2, 0xF14 => 1, _ => 0 },
            0x2679 => a.Oid switch { 0x2683 => 2, 0x2679 => 1, _ => 0 },
            0x1AA6 => a.Oid == 0x1AA6 ? 0 : 1,
            0x2C04 => a.Oid switch { 0x2C05 => 2, 0x2C04 => 1, _ => 0 },
            0x2CFF => a.Oid == 0x2D01 ? 2 : 1,
            0x193D => a.Oid switch
            {
                0x193F when Arena != null && MathF.Abs(a.Position.X - Arena.Position.X) <= 5
                    && MathF.Abs(a.Position.Z - Arena.Position.Z) <= 5 => 2,
                0x193F => 0,
                0x193E => 2,
                _ => 1,
            },
            0x3EAA => a.Oid == 0x3EAA ? 1 : 0,
            0x415D => a.Oid == 0x415E ? 1 : 0,
            0x4641 => a.Oid == 0x4642 ? 1 : 0,
            _ => 0,
        };
        return _actors.Values.Where(a => a.Hostile && a.Targetable && !a.Dead
                && (Priority(a) > 0 || IsHeavenswardIntro && Priority(a) == 0 || IsCaduceus && a.Oid == 0x7D7 && Priority(a) == 0
                    || IsTwintania && Priority(a) == 0 || IsTitanExtreme && TitanExCombat && a.Oid is 0x5E0 && Priority(a) == 0
                    || Active?.BossOid == 0x4C && a.Oid is 0x4C or 0x7F or 0x7E && Priority(a) == 0))
            .OrderByDescending(Priority).ThenByDescending(a => a.Id == current)
            .ThenBy(a => Vector3.DistanceSquared(player, a.Position)).Select(a => a.Id).FirstOrDefault();
    }

    public float? GazeFacing(Vector3 player, float rotation)
    {
        if (IsThordan) return Thordan.Facing(player, rotation);
        if (IsKefka) return Kefka.Facing(player, rotation);
        if (territory == SkydeepCatalog.TERRITORY) return SkydeepFacing(player, rotation);
        if (territory == StigmaCatalog.TERRITORY && Active?.BossOid == 0x3428) return StigmaFacing(player, rotation);
        if (Active?.BossOid == 0x1AFE) return SirenFacing(player, rotation);
        if (VisibleCarnivaleEyes != null && Active == null) return null;
        var directions = Gazes.Where(c => c.FinishAt <= _now.AddSeconds(.75))
            .Where(c => c.Rule.Action is not (11900 or 11922 or 14691 or 14951 or 14875 or 36624 or 3281 or 6403 or 37523 or 37537 or 17198 or 21090 or 40743 or 40757) && !NativeTreasureCatalog.GazeAction(c.Rule.Action)
                || Vector2.DistanceSquared(new(c.Location.X, c.Location.Z), new(player.X, player.Z)) <= 10000f * 10000f)
            .Select(c => (Center: MathF.Atan2(c.Location.X - player.X, c.Location.Z - player.Z), HalfWidth: MathF.PI / 4))
            .Concat(TreasureHexDirections(player).Where(d => d.Activation <= _now.AddSeconds(.75))
                .Select(d => (d.Center, d.HalfWidth))).ToArray();
        return DungeonFacingDirections.Solve(directions, rotation);
    }

    public IEnumerable<DungeonAmbientAoe> AmbientAoes()
    {
        if (IsAlteRoite)
        {
            foreach (var aoe in AlteRoiteAoes()) yield return aoe;
            yield break;
        }
        if (IsHalicarnassus)
        {
            foreach (var aoe in HalicarnassusAoes()) yield return aoe;
            yield break;
        }
        if (IsThordan)
        {
            foreach (var aoe in ThordanAoes()) yield return aoe;
            yield break;
        }
        if (IsWickedThunder)
        {
            foreach (var aoe in WickedThunderAoes()) yield return aoe;
            yield break;
        }
        if (IsKefka)
        {
            foreach (var aoe in KefkaAoes()) yield return aoe;
            yield break;
        }
        if (IsQuantum)
        {
            foreach (var aoe in QuantumOutput.Aoes(Quantum)) yield return aoe;
            yield break;
        }
        foreach (var aoe in EurekaAoes()) yield return aoe;
        foreach (var aoe in HeavenOnHighAoes()) yield return aoe;
        foreach (var aoe in TacticalAoes()) yield return aoe;
        foreach (var aoe in FateAoes()) yield return aoe;
        foreach (var aoe in NovicePrecombatAoes()) yield return aoe;
        foreach (var aoe in TreasureLights()) yield return aoe;
        foreach (var aoe in TreasureSequenceAoes()) yield return aoe;
        foreach (var aoe in TreasureRotationAoes()) yield return aoe;
        foreach (var aoe in TreasureGymnasionSequenceAoes()) yield return aoe;
        foreach (var aoe in TreasureNarkissosAoes()) yield return aoe;
        foreach (var aoe in TreasureMolterAoes()) yield return aoe;
        foreach (var aoe in TreasureCastBaitAoes()) yield return aoe;
        foreach (var aoe in TreasureWallKnockbackAoes()) yield return aoe;
        foreach (var aoe in TreasureBuffetAoes()) yield return aoe;
        foreach (var aoe in TreasureKelpieAoes()) yield return aoe;
        foreach (var aoe in TreasureCreatedCircleAoes()) yield return aoe;
        foreach (var aoe in TreasureIceAoes()) yield return aoe;
        foreach (var aoe in TreasureVaultGroupAoes()) yield return aoe;
        foreach (var aoe in TreasureBitterAoes()) yield return aoe;
        foreach (var aoe in TreasureFortressAoes()) yield return aoe;
        foreach (var aoe in TreasureYanAoes()) yield return aoe;
        foreach (var aoe in TreasureHexAoes()) yield return aoe;
        foreach (var aoe in TreasureBeamAoes()) yield return aoe;
        foreach (var aoe in TreasureObstacles()) yield return aoe;
        foreach (var aoe in TreasureCleaves()) yield return aoe;
        foreach (var aoe in TreasureBaitAoes()) yield return aoe;
        foreach (var aoe in TreasureOdiousAoes()) yield return aoe;
        foreach (var aoe in TreasureEnemies()) yield return aoe;
        foreach (var aoe in EarlyArrEnemyOutlines()) yield return aoe;
        foreach (var aoe in HeavenswardIntroEnemies()) yield return aoe;
        foreach (var aoe in JobQuestEnemies()) yield return aoe;
        foreach (var aoe in PortraitAoes()) yield return aoe;
        foreach (var aoe in SamuraiAoes()) yield return aoe;
        foreach (var aoe in JobQuestPartyAoes()) yield return aoe;
        foreach (var aoe in HeroesPretendersAoes()) yield return aoe;
        foreach (var aoe in BarP2Aoes()) yield return aoe;
        foreach (var aoe in BarP3Aoes()) yield return aoe;
        foreach (var aoe in CarnivaleAoes()) yield return aoe;
        foreach (var aoe in AlphaAoes()) yield return aoe;
        foreach (var aoe in IseultalonAoes()) yield return aoe;
        foreach (var aoe in DikaiosyneAoes()) yield return aoe;
        foreach (var aoe in BekkoAoes()) yield return aoe;
        foreach (var aoe in OrcusAoes()) yield return aoe;
        foreach (var aoe in FallenAoes()) yield return aoe;
        foreach (var aoe in SageLifeAoes()) yield return aoe;
        foreach (var aoe in GuildivainAoes()) yield return aoe;
        foreach (var aoe in SleepOtisAoes()) yield return aoe;
        foreach (var aoe in SlickshellAoes()) yield return aoe;
        foreach (var aoe in MaxQuestAoes()) yield return aoe;
        foreach (var aoe in TimeBetweenAoes()) yield return aoe;
        foreach (var aoe in NodeAoes()) yield return aoe;
        foreach (var aoe in SteelAoes()) yield return aoe;
        foreach (var aoe in QuestWindAoes()) yield return aoe;
        foreach (var aoe in MainScenarioKnockbacks()) yield return aoe;
        foreach (var aoe in FeastElectrocutions()) yield return aoe;
        foreach (var aoe in RhalgrAoes()) yield return aoe;
        foreach (var aoe in EndwalkerQuestAoes()) yield return aoe;
        foreach (var aoe in PalacePoxAreas()) yield return aoe;
        foreach (var aoe in HeavenswardQuestAoes()) yield return aoe;
        foreach (var aoe in TisiphoneZombieAreas()) yield return aoe;
        foreach (var aoe in PalaceSerpentAoes()) yield return aoe;
        foreach (var aoe in PalacePlantAoes()) yield return aoe;
        foreach (var aoe in NybethAoes()) yield return aoe;
        foreach (var aoe in GodmotherAoes()) yield return aoe;
        foreach (var aoe in GudannaAoes()) yield return aoe;
        foreach (var aoe in BlackRiderAoes()) yield return aoe;
        foreach (var aoe in YaquaruAoes()) yield return aoe;
        foreach (var aoe in GraoullyAoes()) yield return aoe;
        foreach (var aoe in EddaGazeDrawing()) yield return aoe;
        foreach (var aoe in NativeEnemyGoals()) yield return aoe;
        foreach (var aoe in NativeEnemyMarkers()) yield return aoe;
        foreach (var aoe in ArrTrialAoes()) yield return aoe;
        foreach (var aoe in PortaAoes()) yield return aoe;
        foreach (var aoe in HardTrialAoes()) yield return aoe;
        foreach (var aoe in ThornAoes()) yield return aoe;
        foreach (var aoe in WhorlAoes()) yield return aoe;
        foreach (var aoe in SusanoAoes()) yield return aoe;
        foreach (var aoe in LakshmiAoes()) yield return aoe;
        foreach (var aoe in ShinryuAoes()) yield return aoe;
        foreach (var aoe in TsukuyomiAoes()) yield return aoe;
        foreach (var aoe in YojimboAoes()) yield return aoe;
        foreach (var aoe in ByakkoAoes()) yield return aoe;
        foreach (var aoe in SuzakuAoes()) yield return aoe;
        foreach (var aoe in SeiryuAoes()) yield return aoe;
        foreach (var aoe in TitaniaAoes()) yield return aoe;
        foreach (var aoe in InnocenceAoes()) yield return aoe;
        foreach (var aoe in HadesAoes()) yield return aoe;
        foreach (var aoe in WolAoes()) yield return aoe;
        foreach (var aoe in HydaAoes()) yield return aoe;
        foreach (var aoe in AsuraAoes()) yield return aoe;
        foreach (var aoe in ZeleniaAoes()) yield return aoe;
        foreach (var aoe in ValigAoes()) yield return aoe;
        foreach (var aoe in ArkAoes()) yield return aoe;
        foreach (var aoe in EnuoAoes()) yield return aoe;
        foreach (var aoe in NecronAoes()) yield return aoe;
        foreach (var aoe in QueenAoes()) yield return aoe;
        foreach (var aoe in ZoraalAoes()) yield return aoe;
        foreach (var aoe in DoomAoes()) yield return aoe;
        foreach (var aoe in CoilAoes()) yield return aoe;
        foreach (var aoe in CaduceusAoes()) yield return aoe;
        foreach (var aoe in TwinAoes()) yield return aoe;
        foreach (var aoe in LabyrinthAoes()) yield return aoe;
        foreach (var aoe in SyrcusAoes()) yield return aoe;
        foreach (var aoe in DarknessAoes()) yield return aoe;
        foreach (var aoe in ArrQuestAoes()) yield return aoe;
        foreach (var aoe in UltimaAoes()) yield return aoe;
        foreach (var aoe in GarudaExAoes()) yield return aoe;
        foreach (var aoe in TitanExAoes()) yield return aoe;
        foreach (var aoe in IfritExAoes()) yield return aoe;
        foreach (var aoe in GauntletAoes()) yield return aoe;
        foreach (var aoe in TrialVoidzoneAoes()) yield return aoe;
        foreach (var aoe in LapisAoes()) yield return aoe;
        foreach (var aoe in AetherfontAoes()) yield return aoe;
        foreach (var aoe in LunarAoes()) yield return aoe;
        foreach (var aoe in IhuyAoes()) yield return aoe;
        foreach (var aoe in WorqorAoes()) yield return aoe;
        foreach (var aoe in SkydeepAoes()) yield return aoe;
        foreach (var aoe in VanguardAoes()) yield return aoe;
        foreach (var aoe in OrigenicsAoes()) yield return aoe;
        foreach (var aoe in AlexAoes()) yield return aoe;
        foreach (var aoe in TenderAoes()) yield return aoe;
        foreach (var aoe in StrayAoes()) yield return aoe;
        foreach (var aoe in YuweAoes()) yield return aoe;
        foreach (var aoe in UnderAoes()) yield return aoe;
        foreach (var aoe in MesoAoes()) yield return aoe;
        foreach (var aoe in MistAoes()) yield return aoe;
        foreach (var aoe in ClyAoes()) yield return aoe;
        foreach (var aoe in OrbAoes()) yield return aoe;
        foreach (var aoe in FruitAoes()) yield return aoe;
        foreach (var aoe in PartyAoes()) yield return aoe;
        foreach (var aoe in PraetoriumAoes()) yield return aoe;
        foreach (var aoe in WandererAoes()) yield return aoe;
        foreach (var aoe in AmdaporAoes()) yield return aoe;
        foreach (var aoe in PharosAoes()) yield return aoe;
        foreach (var aoe in SnowcloakAoes()) yield return aoe;
        foreach (var aoe in SastashaHardAoes()) yield return aoe;
        foreach (var aoe in KeeperAoes()) yield return aoe;
        foreach (var aoe in SohmAoes()) yield return aoe;
        foreach (var aoe in AeryAoes()) yield return aoe;
        foreach (var aoe in VaultAoes()) yield return aoe;
        foreach (var aoe in GubalAoes()) yield return aoe;
        foreach (var aoe in ResearchAoes()) yield return aoe;
        foreach (var aoe in ResearchTrashAoes()) yield return aoe;
        foreach (var aoe in FractalAoes()) yield return aoe;
        foreach (var aoe in ArboretumAoes()) yield return aoe;
        foreach (var aoe in ArboretumTrashAoes()) yield return aoe;
        foreach (var aoe in AntitowerAoes()) yield return aoe;
        foreach (var aoe in SohrAoes()) yield return aoe;
        foreach (var aoe in XelAoes()) yield return aoe;
        foreach (var aoe in BaelAoes()) yield return aoe;
        foreach (var aoe in SirenAoes()) yield return aoe;
        foreach (var aoe in ShisuiAoes()) yield return aoe;
        foreach (var aoe in BardamAoes()) yield return aoe;
        foreach (var aoe in DomaAoes()) yield return aoe;
        foreach (var aoe in AlaMhigoAoes()) yield return aoe;
        foreach (var aoe in SkallaAoes()) yield return aoe;
        foreach (var aoe in BurnAoes()) yield return aoe;
        foreach (var aoe in GhimlytAoes()) yield return aoe;
        foreach (var aoe in HolminsterAoes()) yield return aoe;
        foreach (var aoe in DohnAoes()) yield return aoe;
        foreach (var aoe in QitanaAoes()) yield return aoe;
        foreach (var aoe in MalikahAoes()) yield return aoe;
        foreach (var aoe in GulgAoes()) yield return aoe;
        foreach (var aoe in AmaurotAoes()) yield return aoe;
        foreach (var aoe in TwinningAoes()) yield return aoe;
        foreach (var aoe in AnyderAoes()) yield return aoe;
        foreach (var aoe in CosmosAoes()) yield return aoe;
        foreach (var aoe in AnamnesisAoes()) yield return aoe;
        foreach (var aoe in HeroesAoes()) yield return aoe;
        foreach (var aoe in MatoyaAoes()) yield return aoe;
        foreach (var aoe in PaglthanAoes()) yield return aoe;
        foreach (var aoe in ZotAoes()) yield return aoe;
        foreach (var aoe in BabilAoes()) yield return aoe;
        foreach (var aoe in VanaspatiAoes()) yield return aoe;
        foreach (var aoe in KtisisAoes()) yield return aoe;
        foreach (var aoe in AitiascopeAoes()) yield return aoe;
        foreach (var aoe in DeadEndsAoes()) yield return aoe;
        foreach (var aoe in SmiletonAoes()) yield return aoe;
        foreach (var aoe in StigmaAoes()) yield return aoe;
        foreach (var aoe in AlzadaalAoes()) yield return aoe;
        foreach (var aoe in TroiaAoes()) yield return aoe;
        if (Active?.BossOid == 0x477A && _actors.TryGetValue(PlayerId, out var doomed)
            && HasStatus(doomed, QarnCatalog.DOOM) && _actors.TryGetValue(glowingPlatform, out var platform))
        {
            yield return new("doom/platform", "死宣解除平台", new RectangleTemplate(3.5f, 3.5f),
                platform.Position, Safe: true);
        }
        if (Active?.BossOid == 0x103)
        {
            foreach (var pod in _actors.Values.Where(a => a.Oid == 0x1E8614 && !a.Dead))
                yield return new($"pollen/{pod.Id}", "PollenZone", new CircleTemplate(10), pod.Position);
        }
        if (Active?.BossOid == 0x1AB)
        {
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && HasStatus(a, 280)))
                yield return new($"bound/{member.Id}", "束缚队友", new CircleTemplate(1.5f), member.Position,
                    Navigate: false, OutlineOnly: true, Safe: true);
        }
        var radius = Active?.BossOid switch
        {
            0x110 => 7.4f, 0x3DE => 8.6f, 0x4900 => 9.7f, 0x5B5 => 10.24f, 0x5AF => 27,
            0x41C => 8.85f, 0x5CC => 11.5f, 0x8F4 => 9, 0x8EF => 7, 0xD07 => 11.23f,
            0xE8F => 11.68f, 0xE91 => 10.9f, 0xE96 => 31.84f, 0xEA6 => 12.9f, 0xE83 => 8, 0xE9A => 13.3f, _ => 0,
        };
        if (Active?.BossOid == 0xE9A && researchStacks.Count != 0) yield break;
        if (Active?.BossOid == 0xE91 && _casts.Values.Any(c => c.Rule.Action == 3809)) yield break;
        if (Active?.BossOid is 0x5B5 or 0x5AF or 0xD07 && _casts.ContainsKey(PrimaryId)) yield break;
        if (radius == 0 || !_actors.TryGetValue(PrimaryId, out var boss)
            || !boss.Targetable || !_actors.TryGetValue(boss.TargetId, out var target)) yield break;
        if (GubalBossForbidden(boss)) yield break;
        var angle = MathF.Atan2(target.Position.X - boss.Position.X, target.Position.Z - boss.Position.Z);
        var name = Active!.BossOid switch
        {
            0x110 => "SweetSteel", 0x4900 => "TheLionsBreath", 0x5B5 => "LionsBreath",
            0x5AF => "FrostBreath", 0x41C => "MoldySneeze", 0x5CC => "TheLook",
            0x8F4 => "FrontalCleave", 0x8EF => "SirenCleave",
            0xE8F => "BloodyCaress", 0xE91 => "ThirdLegForward", 0xE96 => "AbyssicBuster",
            0xEA6 => "ElectricPredation",
            0xE83 => "PageTear",
            0xE9A => "ChthonicHush",
            _ => "BurningCyclone",
        };
        var cleaveAngle = Active.BossOid switch
        {
            0x41C or 0x5CC or 0xE96 or 0xE83 => MathF.PI / 2,
            0x8EF => 130 * MathF.PI / 180,
            _ => MathF.PI * 2 / 3,
        };
        yield return new("cleave/display", name, new FanTemplate(radius, cleaveAngle),
            boss.Position, angle, Navigate: false, OutlineOnly: true);
        if (PlayerId != target.Id)
        {
            yield return new("cleave/avoid", name, new FanTemplate(radius, cleaveAngle),
                boss.Position, angle, Display: false);
        }
        else
        {
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                var direction = MathF.Atan2(member.Position.X - boss.Position.X, member.Position.Z - boss.Position.Z);
                yield return new($"cleave/party/{member.Id}", name + " / 避免面向队友",
                    new FanTemplate(100, cleaveAngle), boss.Position, direction, Display: false);
            }
        }
    }
}

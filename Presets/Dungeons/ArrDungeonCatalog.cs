using System.Numerics;
using MasterPlayer.Dodge;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonCastRule(uint Action, uint CasterOid, string Name, GeometryTemplate? Shape, string? Hint = null, string? PartyAoe = null);
internal sealed record DungeonDefinition(uint BossOid, string Name, Func<Vector3, IGeometryShape> Arena, DungeonCastRule[] Casts, uint[] VisualActions, uint[]? PrimaryAliases = null);

internal static class ArrDungeonCatalog
{
    public static DungeonDefinition[] Create(uint territory) => territory switch
    {
        AlteRoiteCatalog.TERRITORY => AlteRoiteCatalog.Create(),
        HalicarnassusCatalog.TERRITORY => HalicarnassusCatalog.Create(),
        ThordanCatalog.TERRITORY => ThordanCatalog.Create(),
        WickedThunderCatalog.TERRITORY => WickedThunderCatalog.Create(),
        KefkaCatalog.TERRITORY => KefkaCatalog.Create(),
        QuantumCatalog.TERRITORY => QuantumCatalog.Create(),
        EurekaOrthosCatalog.GANCANAGH or EurekaOrthosCatalog.CLONING_NODE or EurekaOrthosCatalog.TIAMAT or EurekaOrthosCatalog.TWINTANIA or EurekaOrthosCatalog.CHIMERA or EurekaOrthosCatalog.MINOTAUR or EurekaOrthosCatalog.AETURNA or EurekaOrthosCatalog.PROTO_KALIYA or EurekaOrthosCatalog.ADMINISTRATOR or EurekaOrthosCatalog.EXCALIBUR => EurekaOrthosCatalog.Create(territory),
        HeavenOnHighCatalog.FIRST or HeavenOnHighCatalog.SECOND or HeavenOnHighCatalog.THIRD
            or HeavenOnHighCatalog.FOURTH or HeavenOnHighCatalog.FIFTH or HeavenOnHighCatalog.SIXTH
            or HeavenOnHighCatalog.SEVENTH or HeavenOnHighCatalog.EIGHTH or HeavenOnHighCatalog.NINTH => HeavenOnHighCatalog.Create(territory),
        CarnivaleCatalog.TERRITORY => CarnivaleCatalog.Create(),
        NativeFateCatalog.ARCHAEOTANIA or NativeFateCatalog.DAIVADIPA or NativeFateCatalog.FORMIDABLE or NativeFateCatalog.CHI or NativeFateCatalog.MICA or NativeFateCatalog.TTOKRRONE => NativeFateCatalog.Create(territory),
        NoviceCatalog.TANK04 or NoviceCatalog.DPS03 or NoviceCatalog.DPS05 or NoviceCatalog.BASIC_FINAL or 1127 or 1128 or 1129 => NoviceCatalog.Create(territory),
        NativeTreasureCatalog.VAULT_ONEIRON or NativeTreasureCatalog.SHIFTING_ALTARS or NativeTreasureCatalog.SHIFTING_GYMNASION or NativeTreasureCatalog.SHIFTING_OUBLIETTES or NativeTreasureCatalog.CENOTE_JA_JA_GURAL or NativeTreasureCatalog.DUNGEONS_LYHE_GHIAH or NativeTreasureCatalog.EXCITATRON or NativeTreasureCatalog.HIDDEN_CANALS or NativeTreasureCatalog.LOST_CANALS => NativeTreasureCatalog.Create(territory),
        JobQuestCatalog.BAR_THE_PASSAGE or JobQuestCatalog.TO_HAVE_LOVED_AND_LOST or JobQuestCatalog.HEROES_AND_PRETENDERS or JobQuestCatalog.BLOOD_ON_THE_DECK or JobQuestCatalog.MIND_OVER_MANOR or JobQuestCatalog.SOMEWHERE_ONLY_SHE_KNOWS
            or JobQuestCatalog.FANGS_OF_THE_VIPER or JobQuestCatalog.FACE_OF_TRUE_EVIL
            or JobQuestCatalog.SAGES_FOCUS or JobQuestCatalog.ORPHANS_AND_BROKEN_BLADE
            or JobQuestCatalog.BATTLE_ON_BEKKO
            or JobQuestCatalog.KILLING_ART
            or JobQuestCatalog.OUR_UNSUNG_HEROES
            or JobQuestCatalog.LIFE_EPHEMERAL
            or JobQuestCatalog.POWER_TO_PROTECT or JobQuestCatalog.ART_FOR_THE_LIVING or JobQuestCatalog.STEEL_AGAINST_STEEL
            or JobQuestCatalog.RAISING_THE_SWORD or JobQuestCatalog.GAMBOLING_FOR_GIL
            => JobQuestCatalog.Create(territory),
        MainScenarioQuestCatalog.WARMTH_OF_THE_FAMILY or MainScenarioQuestCatalog.ISEULTALON or MainScenarioQuestCatalog.MEASURE_OF_HIS_REACH or MainScenarioQuestCatalog.FADED_MEMORIES or MainScenarioQuestCatalog.DEATH_UNTO_DAWN or MainScenarioQuestCatalog.ORACLE_OF_LIGHT or MainScenarioQuestCatalog.FEAST_OF_LIES or MainScenarioQuestCatalog.RHALGRS_BEACON or MainScenarioQuestCatalog.WHERE_EVERYTHING_BEGINS or MainScenarioQuestCatalog.FROSTY_RECEPTION or MainScenarioQuestCatalog.GAME_IS_AFOOT or MainScenarioQuestCatalog.SLEEP_DISTURBED or MainScenarioQuestCatalog.OTIS_OATHBROKEN or MainScenarioQuestCatalog.HIS_FORGOTTEN_HOME or MainScenarioQuestCatalog.MAX_QUEST or MainScenarioQuestCatalog.HOPE_ON_THE_WAVES or MainScenarioQuestCatalog.TIME_BETWEEN => MainScenarioQuestCatalog.Create(territory),
        1036 => [
            new(0x19B, "Chopper", p => ArrDungeonArenas.Chopper(p.Y), [
                new(351, 0x19B, "ChargedWhisker", new CircleTemplate(6.15f))], []),
            new(0x1A1, "Denn the Orcatoothed", p => ArrDungeonArenas.Denn(p.Y), [
                new(556, 0x1A1, "Hydroball", new FanTemplate(8, MathF.PI / 2))], [])],
        1037 => [
            new(0x237, "Void Soulcounter", p => p.X > -10 ? ArrDungeonArenas.VoidFirst(p.Y)
                : p.X is > -25 and < -15 ? ArrDungeonArenas.VoidSecond(p.Y) : ArrDungeonArenas.VoidThird(p.Y), [
                new(912, 0x237, "Condemnation", new FanTemplate(7, MathF.PI / 2))], [430]),
            new(0x4C, "Galvanth the Dominator", p => new CircleGeometry(new(-52.765f, p.Y, -12.789f), 18), [
                new(987, 0x4C, "MindBlast", new CircleTemplate(9.95f))], [971])],
        1038 => [
            new(0x387C, "Kottos", p => ArrDungeonArenas.Kottos(p.Y), [
                new(28545, 0x387C, "GrandSlam", null, "死刑"),
                new(28544, 0x233C, "LumberingLeap", new CircleTemplate(12)),
                new(28546, 0x387C, "ColossalSlam", new FanTemplate(30, MathF.PI / 3)),
                new(28547, 0x387C, "Catapult", null, "单体伤害")], [28543, 28549]),
            new(0x3870, "Ichorous Ire", p => ArrDungeonArenas.Ichorous(p.Y), [
                new(28462, 0x3870, "Syrup", new CircleTemplate(4)),
                new(28461, 0x3870, "FluidSpread", null, "死刑"),
                new(28465, 0x3871, "Divide", new CircleTemplate(8))], [28463, 28464]),
            new(0x38C9, "Gyges the Great", p => new CircleGeometry(new(-100, p.Y, 6), 19.5f), [
                new(28762, 0x38C9, "GiganticSwing", new DonutTemplate(4, 40)),
                new(28760, 0x38C9, "GiganticSmash", new CircleTemplate(10)),
                new(28761, 0x233C, "GiganticBlast", new CircleTemplate(8)),
                new(28764, 0x38C9, "GrandSlam", null, "死刑"),
                new(28763, 0x38C9, "ColossalSlam", new FanTemplate(40, MathF.PI / 3))], [])],
        HeavenswardIntroCatalog.REGULA or HeavenswardIntroCatalog.DEATHGAZE or HeavenswardIntroCatalog.ALICANTO or HeavenswardIntroCatalog.EDDA
            or HeavenswardIntroCatalog.IXTAB or HeavenswardIntroCatalog.AH_PUCH
            or HeavenswardIntroCatalog.ALDYNN or HeavenswardIntroCatalog.REGULA_VITH or HeavenswardIntroCatalog.TISIPHONE
            or HeavenswardIntroCatalog.NINGISHZIDA or HeavenswardIntroCatalog.ALFARD
            or HeavenswardIntroCatalog.DIVINE_INTERVENTION or HeavenswardIntroCatalog.GRAOULLY
            or HeavenswardIntroCatalog.SPURGE or HeavenswardIntroCatalog.KIRTIMUKHA or HeavenswardIntroCatalog.NYBETH or HeavenswardIntroCatalog.GODMOTHER or HeavenswardIntroCatalog.GUDANNA or HeavenswardIntroCatalog.BLACK_RIDER or HeavenswardIntroCatalog.YAQUARU => HeavenswardIntroCatalog.Create(territory),
        HalataliCatalog.TERRITORY => HalataliCatalog.Create(),
        1039 => [
            new(0x103, "Graffias", p => new CircleGeometry(new(215, p.Y, -145), 20),
            [
                new(701, 0x103, "Silkscreen", RayRectangleTemplate.Symmetric(4, 18)),
                new(698, 0x103, "StickyWeb", null, "目标处将出现延迟爆炸茧"),
                new(730, 0x23C, "PodBurst", new CircleTemplate(7.05f)),
                new(702, 0x103, "DeadlyThrust", null, "目标处将出现持续毒区"),
            ], [704])],
        1040 => [
            new(0x110, "Manor Claviger", p => HaukkeArenas.Claviger(p.Y),
            [
                new(855, 0x110, "VoidFireII", new CircleTemplate(5)),
                new(705, 0x110, "DarkMist", new CircleTemplate(9.4f)),
            ], [489]),
            new(0x112, "Manor Steward", p => HaukkeArenas.Steward(p.Y),
            [
                new(860, 0x112, "SoulDrain", new CircleTemplate(9)),
            ], []),
            new(0x38A8, "Lady Amandine", p => HaukkeArenas.Amandine(p.Y),
            [
                new(28646, 0x38A8, "DarkMist", new CircleTemplate(9)),
                new(28649, 0x38A8, "BeguilingMist", null, "即将被强制移动至首领"),
                new(28645, 0x38A8, "VoidThunderIII", null, "可打断死刑"),
                new(28648, 0x38A9, "PetrifyingEye", null, "背对视线"),
            ], [28640, 28643])],
        1041 => BrayfloxCatalog.Create(),
        QarnCatalog.TERRITORY => QarnCatalog.Create(),
        CuttersCryCatalog.TERRITORY => CuttersCryCatalog.Create(),
        StoneVigilCatalog.TERRITORY => StoneVigilCatalog.Create(),
        DzemaelCatalog.TERRITORY => DzemaelCatalog.Create(),
        AurumValeCatalog.TERRITORY => AurumValeCatalog.Create(),
        CastrumMeridianumCatalog.TERRITORY => CastrumMeridianumCatalog.Create(),
        PraetoriumCatalog.TERRITORY => PraetoriumCatalog.Create(),
        WanderersPalaceCatalog.TERRITORY => WanderersPalaceCatalog.Create(),
        AmdaporCatalog.TERRITORY => AmdaporCatalog.Create(),
        PharosSiriusCatalog.TERRITORY => PharosSiriusCatalog.Create(),
        SnowcloakCatalog.TERRITORY => SnowcloakCatalog.Create(),
        SastashaHardCatalog.TERRITORY => SastashaHardCatalog.Create(),
        KeeperCatalog.TERRITORY => KeeperCatalog.Create(),
        SohmAlCatalog.TERRITORY => SohmAlCatalog.Create(),
        AeryCatalog.TERRITORY => AeryCatalog.Create(),
        VaultCatalog.TERRITORY => VaultCatalog.Create(),
        GubalCatalog.TERRITORY => GubalCatalog.Create(),
        ResearchFacilityCatalog.TERRITORY => ResearchFacilityCatalog.Create(),
        FractalCatalog.TERRITORY => FractalCatalog.Create(),
        ArboretumCatalog.TERRITORY => ArboretumCatalog.Create(),
        AntitowerCatalog.TERRITORY => AntitowerCatalog.Create(),
        SohrKhaiCatalog.TERRITORY => SohrKhaiCatalog.Create(),
        XelphatolCatalog.TERRITORY => XelphatolCatalog.Create(),
        BaelsarCatalog.TERRITORY => BaelsarCatalog.Create(),
        SirensongCatalog.TERRITORY => SirensongCatalog.Create(),
        ShisuiCatalog.TERRITORY => ShisuiCatalog.Create(),
        BardamCatalog.TERRITORY => BardamCatalog.Create(),
        DomaCatalog.TERRITORY => DomaCatalog.Create(),
        AlaMhigoCatalog.TERRITORY => AlaMhigoCatalog.Create(),
        SkallaCatalog.TERRITORY => SkallaCatalog.Create(),
        BurnCatalog.TERRITORY => BurnCatalog.Create(),
        GhimlytCatalog.TERRITORY => GhimlytCatalog.Create(),
        HolminsterCatalog.TERRITORY => HolminsterCatalog.Create(),
        QitanaCatalog.TERRITORY => QitanaCatalog.Create(),
        MalikahCatalog.TERRITORY => MalikahCatalog.Create(),
        MtGulgCatalog.TERRITORY => MtGulgCatalog.Create(),
        AmaurotCatalog.TERRITORY => AmaurotCatalog.Create(),
        TwinningCatalog.TERRITORY => TwinningCatalog.Create(),
        AnyderCatalog.TERRITORY => AnyderCatalog.Create(),
        CosmosCatalog.TERRITORY => CosmosCatalog.Create(),
        AnamnesisCatalog.TERRITORY => AnamnesisCatalog.Create(),
        HeroesCatalog.TERRITORY => HeroesCatalog.Create(),
        MatoyaCatalog.TERRITORY => MatoyaCatalog.Create(),
        PaglthanCatalog.TERRITORY => PaglthanCatalog.Create(),
        ZotCatalog.TERRITORY => ZotCatalog.Create(),
        BabilCatalog.TERRITORY => BabilCatalog.Create(),
        VanaspatiCatalog.TERRITORY => VanaspatiCatalog.Create(),
        KtisisCatalog.TERRITORY => KtisisCatalog.Create(),
        AitiascopeCatalog.TERRITORY => AitiascopeCatalog.Create(),
        DeadEndsCatalog.TERRITORY => DeadEndsCatalog.Create(),
        SmiletonCatalog.TERRITORY => SmiletonCatalog.Create(),
        StigmaCatalog.TERRITORY => StigmaCatalog.Create(),
        AlzadaalCatalog.TERRITORY => AlzadaalCatalog.Create(),
        TroiaCatalog.TERRITORY => TroiaCatalog.Create(),
        LapisCatalog.TERRITORY => LapisCatalog.Create(),
        AetherfontCatalog.TERRITORY => AetherfontCatalog.Create(),
        LunarCatalog.TERRITORY => LunarCatalog.Create(),
        IhuykatumuCatalog.TERRITORY => IhuykatumuCatalog.Create(),
        WorqorCatalog.TERRITORY => WorqorCatalog.Create(),
        SkydeepCatalog.TERRITORY => SkydeepCatalog.Create(),
        VanguardCatalog.TERRITORY => VanguardCatalog.Create(),
        OrigenicsCatalog.TERRITORY => OrigenicsCatalog.Create(),
        AlexandriaCatalog.TERRITORY => AlexandriaCatalog.Create(),
        TenderValleyCatalog.TERRITORY => TenderValleyCatalog.Create(),
        StrayboroughCatalog.TERRITORY => StrayboroughCatalog.Create(),
        YuweyawataCatalog.TERRITORY => YuweyawataCatalog.Create(),
        UnderkeepCatalog.TERRITORY => UnderkeepCatalog.Create(),
        MesoCatalog.TERRITORY => MesoCatalog.Create(),
        MistwakeCatalog.TERRITORY => MistwakeCatalog.Create(),
        ClyteumCatalog.TERRITORY => ClyteumCatalog.Create(),
        DohnMhegCatalog.TERRITORY => DohnMhegCatalog.Create(),
        ArrTrialCatalog.IFRIT or ArrTrialCatalog.TITAN or ArrTrialCatalog.GARUDA => ArrTrialCatalog.Create(territory),
        PortaCatalog.TERRITORY => PortaCatalog.Create(),
        HardTrialCatalog.IFRIT or HardTrialCatalog.TITAN or HardTrialCatalog.GARUDA => HardTrialCatalog.Create(territory),
        ThornmarchCatalog.TERRITORY => ThornmarchCatalog.Create(),
        WhorleaterCatalog.TERRITORY => WhorleaterCatalog.Create(),
        SusanoCatalog.TERRITORY => SusanoCatalog.Create(),
        LakshmiCatalog.TERRITORY => LakshmiCatalog.Create(),
        ShinryuCatalog.TERRITORY => ShinryuCatalog.Create(),
        TsukuyomiCatalog.TERRITORY => TsukuyomiCatalog.Create(),
        YojimboCatalog.TERRITORY => YojimboCatalog.Create(),
        ByakkoCatalog.TERRITORY => ByakkoCatalog.Create(),
        SuzakuCatalog.TERRITORY => SuzakuCatalog.Create(),
        SeiryuCatalog.TERRITORY => SeiryuCatalog.Create(),
        TitaniaCatalog.TERRITORY => TitaniaCatalog.Create(),
        InnocenceCatalog.TERRITORY => InnocenceCatalog.Create(),
        HadesCatalog.TERRITORY => HadesCatalog.Create(),
        WarriorOfLightCatalog.TERRITORY => WarriorOfLightCatalog.Create(),
        HydaelynCatalog.TERRITORY => HydaelynCatalog.Create(),
        AsuraCatalog.TERRITORY => AsuraCatalog.Create(),
        ZeleniaCatalog.TERRITORY => ZeleniaCatalog.Create(),
        ValigarmandaCatalog.TERRITORY => ValigarmandaCatalog.Create(),
        ArkveldCatalog.TERRITORY => ArkveldCatalog.Create(),
        EnuoCatalog.TERRITORY => EnuoCatalog.Create(),
        NecronCatalog.TERRITORY => NecronCatalog.Create(),
        QueenEternalCatalog.TERRITORY => QueenEternalCatalog.Create(),
        ZoraalJaCatalog.TERRITORY => ZoraalJaCatalog.Create(),
        DoomtrainCatalog.TERRITORY => DoomtrainCatalog.Create(),
        CoilNodesCatalog.FIRST or CoilNodesCatalog.SECOND => CoilNodesCatalog.Create(territory),
        CoilGauntletCatalog.TERRITORY => CoilGauntletCatalog.Create(),
        TwintaniaCatalog.TERRITORY => TwintaniaCatalog.Create(),
        LabyrinthCatalog.TERRITORY => LabyrinthCatalog.Create(),
        SyrcusCatalog.TERRITORY => SyrcusCatalog.Create(),
        DarknessCatalog.TERRITORY => DarknessCatalog.Create(),
        UltimaExtremeCatalog.TERRITORY => UltimaExtremeCatalog.Create(),
        GarudaExtremeCatalog.TERRITORY => GarudaExtremeCatalog.Create(),
        TitanExtremeCatalog.TERRITORY => TitanExtremeCatalog.Create(),
        IfritExtremeCatalog.TERRITORY => IfritExtremeCatalog.Create(),
        ArrQuestCatalog.ARCHON or ArrQuestCatalog.ULTIMATE_WEAPON or ArrQuestCatalog.STEPS_OF_FAITH => ArrQuestCatalog.Create(territory),
        _ => []
    };

    public static bool Owns(uint oid, uint territory) => territory switch
    {
        ThordanCatalog.TERRITORY => oid == 0x1453,
        AlteRoiteCatalog.TERRITORY => oid == AlteRoiteCatalog.BOSS,
        HalicarnassusCatalog.TERRITORY => oid == HalicarnassusCatalog.BOSS,
        WickedThunderCatalog.TERRITORY => oid == WickedThunderCatalog.BOSS,
        KefkaCatalog.TERRITORY => oid == 0x2153,
        QuantumCatalog.TERRITORY => oid == 0x48EE,
        HeavenOnHighCatalog.FIRST or HeavenOnHighCatalog.SECOND or HeavenOnHighCatalog.THIRD
            or HeavenOnHighCatalog.FOURTH or HeavenOnHighCatalog.FIFTH or HeavenOnHighCatalog.SIXTH
            or HeavenOnHighCatalog.SEVENTH or HeavenOnHighCatalog.EIGHTH or HeavenOnHighCatalog.NINTH => HeavenOnHighCatalog.Owns(territory, oid),
        EurekaOrthosCatalog.GANCANAGH or EurekaOrthosCatalog.CLONING_NODE or EurekaOrthosCatalog.TIAMAT or EurekaOrthosCatalog.TWINTANIA or EurekaOrthosCatalog.CHIMERA or EurekaOrthosCatalog.MINOTAUR or EurekaOrthosCatalog.AETURNA or EurekaOrthosCatalog.PROTO_KALIYA or EurekaOrthosCatalog.ADMINISTRATOR or EurekaOrthosCatalog.EXCALIBUR => EurekaOrthosCatalog.Owns(territory, oid),
        NativeFateCatalog.ARCHAEOTANIA or NativeFateCatalog.DAIVADIPA or NativeFateCatalog.FORMIDABLE or NativeFateCatalog.CHI or NativeFateCatalog.MICA or NativeFateCatalog.TTOKRRONE => NativeFateCatalog.Owns(territory, oid),
        NoviceCatalog.TANK04 or NoviceCatalog.DPS03 or NoviceCatalog.DPS05 or NoviceCatalog.BASIC_FINAL or 1127 or 1128 or 1129 => NoviceCatalog.Owns(territory, oid),
        NativeTreasureCatalog.VAULT_ONEIRON or NativeTreasureCatalog.SHIFTING_ALTARS or NativeTreasureCatalog.SHIFTING_GYMNASION or NativeTreasureCatalog.SHIFTING_OUBLIETTES or NativeTreasureCatalog.CENOTE_JA_JA_GURAL or NativeTreasureCatalog.DUNGEONS_LYHE_GHIAH or NativeTreasureCatalog.EXCITATRON or NativeTreasureCatalog.HIDDEN_CANALS or NativeTreasureCatalog.LOST_CANALS => NativeTreasureCatalog.Owns(oid, territory),
        JobQuestCatalog.BAR_THE_PASSAGE or JobQuestCatalog.TO_HAVE_LOVED_AND_LOST or JobQuestCatalog.HEROES_AND_PRETENDERS or JobQuestCatalog.BLOOD_ON_THE_DECK or JobQuestCatalog.MIND_OVER_MANOR or JobQuestCatalog.SOMEWHERE_ONLY_SHE_KNOWS
            or JobQuestCatalog.FANGS_OF_THE_VIPER or JobQuestCatalog.FACE_OF_TRUE_EVIL
            or JobQuestCatalog.SAGES_FOCUS or JobQuestCatalog.ORPHANS_AND_BROKEN_BLADE
            or JobQuestCatalog.BATTLE_ON_BEKKO
            or JobQuestCatalog.KILLING_ART
            or JobQuestCatalog.OUR_UNSUNG_HEROES
            or JobQuestCatalog.LIFE_EPHEMERAL
            or JobQuestCatalog.POWER_TO_PROTECT or JobQuestCatalog.ART_FOR_THE_LIVING or JobQuestCatalog.STEEL_AGAINST_STEEL
            or JobQuestCatalog.RAISING_THE_SWORD or JobQuestCatalog.GAMBOLING_FOR_GIL
            => JobQuestCatalog.Owns(territory, oid),
        MainScenarioQuestCatalog.WARMTH_OF_THE_FAMILY or MainScenarioQuestCatalog.ISEULTALON or MainScenarioQuestCatalog.MEASURE_OF_HIS_REACH or MainScenarioQuestCatalog.FADED_MEMORIES or MainScenarioQuestCatalog.DEATH_UNTO_DAWN or MainScenarioQuestCatalog.ORACLE_OF_LIGHT or MainScenarioQuestCatalog.FEAST_OF_LIES or MainScenarioQuestCatalog.RHALGRS_BEACON or MainScenarioQuestCatalog.WHERE_EVERYTHING_BEGINS or MainScenarioQuestCatalog.FROSTY_RECEPTION or MainScenarioQuestCatalog.GAME_IS_AFOOT or MainScenarioQuestCatalog.SLEEP_DISTURBED or MainScenarioQuestCatalog.OTIS_OATHBROKEN or MainScenarioQuestCatalog.HIS_FORGOTTEN_HOME or MainScenarioQuestCatalog.MAX_QUEST or MainScenarioQuestCatalog.HOPE_ON_THE_WAVES or MainScenarioQuestCatalog.TIME_BETWEEN => MainScenarioQuestCatalog.Owns(territory, oid),
        CarnivaleCatalog.TERRITORY => CarnivaleCatalog.Owns(territory, oid),
        HeavenswardIntroCatalog.REGULA => oid == 0x161E,
        HeavenswardIntroCatalog.DIVINE_INTERVENTION => oid is 0x1010 or 0x1011,
        HeavenswardIntroCatalog.GRAOULLY => oid == 0x10B9,
        HeavenswardIntroCatalog.EDDA => oid == 0x16C6,
        HeavenswardIntroCatalog.IXTAB => oid == 0x16B9,
        HeavenswardIntroCatalog.AH_PUCH => oid == 0x181B,
        HeavenswardIntroCatalog.ALDYNN => oid == 0x154E,
        HeavenswardIntroCatalog.REGULA_VITH => oid == 0xF1C,
        HeavenswardIntroCatalog.TISIPHONE => oid == 0x181C,
        HeavenswardIntroCatalog.NINGISHZIDA => oid == 0x16AC,
        HeavenswardIntroCatalog.ALFARD => oid == 0x181A,
        HeavenswardIntroCatalog.DEATHGAZE => oid == 0x1692,
        HeavenswardIntroCatalog.ALICANTO => oid == 0x1818,
        HeavenswardIntroCatalog.SPURGE => oid == 0x169F,
        HeavenswardIntroCatalog.KIRTIMUKHA => oid == 0x1819,
        HeavenswardIntroCatalog.NYBETH => oid == 0x1808,
        HeavenswardIntroCatalog.GODMOTHER => oid == 0x1817,
        HeavenswardIntroCatalog.GUDANNA => oid == 0x1816,
        HeavenswardIntroCatalog.BLACK_RIDER => oid == 0x1814,
        HeavenswardIntroCatalog.YAQUARU => oid == 0x1815,
        ArrTrialCatalog.IFRIT or ArrTrialCatalog.TITAN or ArrTrialCatalog.GARUDA => ArrTrialCatalog.Owns(oid, territory),
        PortaCatalog.TERRITORY => PortaCatalog.Owns(oid),
        HardTrialCatalog.IFRIT or HardTrialCatalog.TITAN or HardTrialCatalog.GARUDA => HardTrialCatalog.Owns(oid, territory),
        ThornmarchCatalog.TERRITORY => oid == 0x391C,
        WhorleaterCatalog.TERRITORY => oid == 0xA67,
        SusanoCatalog.TERRITORY => oid == 0x1AF7,
        LakshmiCatalog.TERRITORY => oid == 0x1E20,
        ShinryuCatalog.TERRITORY => oid == 0x1983,
        TsukuyomiCatalog.TERRITORY => oid == 0x2210,
        YojimboCatalog.TERRITORY => oid == 0x25FD,
        ByakkoCatalog.TERRITORY => oid == 0x20F7,
        SuzakuCatalog.TERRITORY => oid == 0x2459,
        SeiryuCatalog.TERRITORY => oid == 0x25F4,
        TitaniaCatalog.TERRITORY => oid == 0x27ED,
        InnocenceCatalog.TERRITORY => oid == 0x28FB,
        HadesCatalog.TERRITORY => oid == 0x2949,
        WarriorOfLightCatalog.TERRITORY => oid is 0x2DDD or 0x2DDE,
        HydaelynCatalog.TERRITORY => oid == 0x34EA,
        AsuraCatalog.TERRITORY => oid == 0x409B,
        ZeleniaCatalog.TERRITORY => oid == 0x47C3,
        ValigarmandaCatalog.TERRITORY => oid == 0x4115,
        ArkveldCatalog.TERRITORY => oid == 0x48E2,
        EnuoCatalog.TERRITORY => oid == 0x4DB9,
        NecronCatalog.TERRITORY => oid == 0x4870,
        QueenEternalCatalog.TERRITORY => oid == 0x41C0,
        ZoraalJaCatalog.TERRITORY => oid is 0x42A9 or 0x42B4,
        DoomtrainCatalog.TERRITORY => oid == 0x4A30,
        CoilNodesCatalog.FIRST => oid is 0x887 or 0x7D7,
        CoilNodesCatalog.SECOND => oid is >= 0x7D9 and <= 0x7DF,
        CoilGauntletCatalog.TERRITORY => oid == 0x1E86F9,
        TwintaniaCatalog.TERRITORY => oid == 0x7E5,
        LabyrinthCatalog.TERRITORY => oid is 0x92B or 0x92E or 0x932 or 0x938,
        SyrcusCatalog.TERRITORY => oid is 0xBC1 or 0xBD7 or 0xBE0 or 0xBDB,
        DarknessCatalog.TERRITORY => oid is 0xE00 or 0xDFC or 0xDF6 or 0xCFA,
        UltimaExtremeCatalog.TERRITORY => oid == 0x90F,
        GarudaExtremeCatalog.TERRITORY => oid == 0xF5,
        TitanExtremeCatalog.TERRITORY => oid == 0xF8,
        IfritExtremeCatalog.TERRITORY => oid == 0xD3,
        ArrQuestCatalog.ARCHON => oid == 0x38F5,
        ArrQuestCatalog.ULTIMATE_WEAPON => oid == 0x3933,
        ArrQuestCatalog.STEPS_OF_FAITH => oid == 0x3A5F,
        1036 => oid is 0x19B or 0x1A1,
        1037 => oid is 0x237 or 0x4C,
        1038 => oid is 0x387C or 0x3870 or 0x38C9,
        1039 => oid == 0x103,
        1040 => oid is 0x110 or 0x112 or 0x38A8,
        1041 => oid is 0x1A7 or 0x3DE or 0x1AB or 0x38C5,
        QarnCatalog.TERRITORY => oid is 0x477A or 0x477C or 0x477E,
        CuttersCryCatalog.TERRITORY => oid is 0x48FB or 0x48FF or 0x4900,
        StoneVigilCatalog.TERRITORY => oid is 0x5B5 or 0x38C7 or 0x5AF,
        DzemaelCatalog.TERRITORY => oid is 0x4A8A or 0x4A8B or 0x4A8D,
        AurumValeCatalog.TERRITORY => oid is 0x4B0C or 0x4B0D or 0x4B0E,
        CastrumMeridianumCatalog.TERRITORY => oid is 0x38CA or 0x38CD or 0x38CE,
        PraetoriumCatalog.TERRITORY => oid is 0x3872 or 0x3873 or 0x3875,
        WanderersPalaceCatalog.TERRITORY => oid is 0x41C or 0x41D or 0x374,
        AmdaporCatalog.TERRITORY => oid is 0x5D1 or 0x5D0 or 0x283 or 0x5CC,
        PharosSiriusCatalog.TERRITORY => oid is 0x8FB or 0x8F4 or 0x918 or 0x8EF,
        SnowcloakCatalog.TERRITORY => oid is 0xD1E or 0xD07 or 0x3977 or 0x3979,
        SastashaHardCatalog.TERRITORY => oid is 0xCBA or 0xCBB,
        KeeperCatalog.TERRITORY => oid is 0x3927 or 0xE68 or 0x392A,
        SohmAlCatalog.TERRITORY => oid is 0xE8F or 0xE91 or 0xE96,
        AeryCatalog.TERRITORY => oid is 0xEA6 or 0x3970 or 0x39CA,
        VaultCatalog.TERRITORY => oid is 0x1051 or 0x104E or 0x1054 or 0x1053 or 0x1056,
        GubalCatalog.TERRITORY => oid is 0xE82 or 0xE83 or 0xE84,
        ResearchFacilityCatalog.TERRITORY => oid is 0xE97 or 0xE9A or 0x3DA4 or 0x3DA7 || ResearchTrashCatalog.IsPrimary(oid),
        FractalCatalog.TERRITORY => FractalCatalog.Owns(oid),
        ArboretumCatalog.TERRITORY => ArboretumCatalog.Owns(oid),
        AntitowerCatalog.TERRITORY => AntitowerCatalog.Owns(oid),
        SohrKhaiCatalog.TERRITORY => SohrKhaiCatalog.Owns(oid),
        XelphatolCatalog.TERRITORY => XelphatolCatalog.Owns(oid),
        BaelsarCatalog.TERRITORY => BaelsarCatalog.Owns(oid),
        SirensongCatalog.TERRITORY => SirensongCatalog.Owns(oid),
        ShisuiCatalog.TERRITORY => ShisuiCatalog.Owns(oid),
        BardamCatalog.TERRITORY => BardamCatalog.Owns(oid),
        DomaCatalog.TERRITORY => DomaCatalog.Owns(oid),
        AlaMhigoCatalog.TERRITORY => AlaMhigoCatalog.Owns(oid),
        SkallaCatalog.TERRITORY => SkallaCatalog.Owns(oid),
        BurnCatalog.TERRITORY => BurnCatalog.Owns(oid),
        GhimlytCatalog.TERRITORY => GhimlytCatalog.Owns(oid),
        HolminsterCatalog.TERRITORY => HolminsterCatalog.Owns(oid),
        QitanaCatalog.TERRITORY => QitanaCatalog.Owns(oid),
        MalikahCatalog.TERRITORY => MalikahCatalog.Owns(oid),
        MtGulgCatalog.TERRITORY => MtGulgCatalog.Owns(oid),
        AmaurotCatalog.TERRITORY => AmaurotCatalog.Owns(oid),
        TwinningCatalog.TERRITORY => TwinningCatalog.Owns(oid),
        AnyderCatalog.TERRITORY => AnyderCatalog.Owns(oid),
        CosmosCatalog.TERRITORY => CosmosCatalog.Owns(oid),
        AnamnesisCatalog.TERRITORY => AnamnesisCatalog.Owns(oid),
        HeroesCatalog.TERRITORY => HeroesCatalog.Owns(oid),
        MatoyaCatalog.TERRITORY => MatoyaCatalog.Owns(oid),
        PaglthanCatalog.TERRITORY => PaglthanCatalog.Owns(oid),
        ZotCatalog.TERRITORY => ZotCatalog.Owns(oid),
        BabilCatalog.TERRITORY => BabilCatalog.Owns(oid),
        VanaspatiCatalog.TERRITORY => VanaspatiCatalog.Owns(oid),
        KtisisCatalog.TERRITORY => KtisisCatalog.Owns(oid),
        AitiascopeCatalog.TERRITORY => AitiascopeCatalog.Owns(oid),
        DeadEndsCatalog.TERRITORY => DeadEndsCatalog.Owns(oid),
        SmiletonCatalog.TERRITORY => SmiletonCatalog.Owns(oid),
        StigmaCatalog.TERRITORY => StigmaCatalog.Owns(oid),
        AlzadaalCatalog.TERRITORY => AlzadaalCatalog.Owns(oid),
        TroiaCatalog.TERRITORY => TroiaCatalog.Owns(oid),
        LapisCatalog.TERRITORY => LapisCatalog.Owns(oid),
        AetherfontCatalog.TERRITORY => AetherfontCatalog.Owns(oid),
        LunarCatalog.TERRITORY => LunarCatalog.Owns(oid),
        IhuykatumuCatalog.TERRITORY => IhuykatumuCatalog.Owns(oid),
        WorqorCatalog.TERRITORY => WorqorCatalog.Owns(oid),
        SkydeepCatalog.TERRITORY => SkydeepCatalog.Owns(oid),
        VanguardCatalog.TERRITORY => VanguardCatalog.Owns(oid),
        OrigenicsCatalog.TERRITORY => OrigenicsCatalog.Owns(oid),
        AlexandriaCatalog.TERRITORY => AlexandriaCatalog.Owns(oid),
        TenderValleyCatalog.TERRITORY => TenderValleyCatalog.Owns(oid),
        StrayboroughCatalog.TERRITORY => StrayboroughCatalog.Owns(oid),
        YuweyawataCatalog.TERRITORY => YuweyawataCatalog.Owns(oid),
        UnderkeepCatalog.TERRITORY => UnderkeepCatalog.Owns(oid),
        MesoCatalog.TERRITORY => MesoCatalog.Owns(oid),
        MistwakeCatalog.TERRITORY => MistwakeCatalog.Owns(oid),
        ClyteumCatalog.TERRITORY => ClyteumCatalog.Owns(oid),
        DohnMhegCatalog.TERRITORY => DohnMhegCatalog.Owns(oid),
        HalataliCatalog.TERRITORY => HalataliCatalog.Owns(oid, territory),
        _ => false,
    };

    public static bool IsScript(string id) => id.ToLowerInvariant() is
        "masterplayer.raid.alteroite.normal" or "masterplayer.raid.halicarnassus.normal" or
        "masterplayer.hoh.10" or "masterplayer.hoh.20" or "masterplayer.hoh.30" or "masterplayer.hoh.40" or "masterplayer.hoh.50"
        or "masterplayer.hoh.60" or "masterplayer.hoh.70" or "masterplayer.hoh.80" or "masterplayer.hoh.90" or
        "masterplayer.treasure.vault-oneiron" or "masterplayer.treasure.shifting-altars" or "masterplayer.treasure.shifting-gymnasion" or "masterplayer.treasure.shifting-oubliettes" or "masterplayer.treasure.cenote-ja-ja-gural" or
        "masterplayer.palace.ixtab" or "masterplayer.palace.ah-puch" or
        "masterplayer.palace.tisiphone" or
        "masterplayer.palace.spurge" or "masterplayer.palace.kirtimukha" or "masterplayer.palace.nybeth" or "masterplayer.palace.godmother" or "masterplayer.palace.gudanna" or
        "masterplayer.palace.blackrider" or
        "masterplayer.palace.yaquaru" or
        "masterplayer.palace.ningishzida" or "masterplayer.palace.alfard" or
        "masterplayer.quest.a-spectacle-for-the-ages" or "masterplayer.quest.close-encounters-of-the-vith-kind" or
        "masterplayer.palace.edda" or "masterplayer.palace.deathgaze" or "masterplayer.palace.alicanto" or "masterplayer.quest.a-bloody-reunion" or
        "masterplayer.trial.ultima-extreme" or
        "masterplayer.trial.garuda-extreme" or
        "masterplayer.trial.titan-extreme" or
        "masterplayer.trial.ifrit-extreme" or
        "masterplayer.alliance.labyrinth" or
        "masterplayer.alliance.syrcus" or
        "masterplayer.alliance.darkness" or
        "masterplayer.quest.operation-archon" or "masterplayer.quest.ultimate-weapon" or "masterplayer.quest.steps-of-faith" or
        "masterplayer.raid.coil1" or "masterplayer.raid.coil2" or "masterplayer.raid.coil4" or "masterplayer.raid.coil5" or
        "masterplayer.trial.doomtrain" or
        "masterplayer.trial.titania" or "masterplayer.trial.innocence" or "masterplayer.trial.hades" or "masterplayer.trial.warrioroflight" or "masterplayer.trial.hydaelyn" or "masterplayer.trial.asura" or "masterplayer.trial.zelenia" or "masterplayer.trial.valigarmanda" or "masterplayer.trial.arkveld" or "masterplayer.trial.enuo" or "masterplayer.trial.necron" or "masterplayer.trial.queen-eternal" or "masterplayer.trial.zoraal-ja" or
        "masterplayer.trial.byakko" or "masterplayer.trial.suzaku" or "masterplayer.trial.seiryu" or "masterplayer.trial.susano" or
        "masterplayer.trial.lakshmi" or
        "masterplayer.trial.shinryu" or "masterplayer.trial.tsukuyomi" or "masterplayer.trial.yojimbo" or
        "masterplayer.trial.thornmarch-hard" or "masterplayer.trial.whorleater-hard" or
        "masterplayer.trial.porta-decumana" or
        "masterplayer.trial.ifrit-hard" or "masterplayer.trial.garuda-hard" or "masterplayer.trial.titan-hard" or
        "masterplayer.trial.ifrit-normal" or "masterplayer.trial.titan-normal" or "masterplayer.trial.garuda-normal" or
        "masterplayer.dungeon.underkeep" or
        "masterplayer.dungeon.meso" or
        "masterplayer.dungeon.mistwake" or
        "masterplayer.dungeon.clyteum" or
        "masterplayer.dungeon.yuweyawata" or
        "masterplayer.dungeon.strayborough" or
        "masterplayer.dungeon.tender-valley" or
        "masterplayer.dungeon.heroes" or
        "masterplayer.dungeon.matoya" or
        "masterplayer.dungeon.paglthan" or
        "masterplayer.dungeon.zot" or
        "masterplayer.dungeon.babil" or
        "masterplayer.dungeon.vanaspati" or
        "masterplayer.dungeon.ktisis" or
        "masterplayer.dungeon.aitiascope" or
        "masterplayer.dungeon.dead-ends" or
        "masterplayer.dungeon.smileton" or
        "masterplayer.dungeon.stigma" or
        "masterplayer.dungeon.alzadaal" or
        "masterplayer.dungeon.troia" or
        "masterplayer.dungeon.lapis" or
        "masterplayer.dungeon.aetherfont" or
        "masterplayer.dungeon.lunar" or
        "masterplayer.dungeon.ihuykatumu" or
        "masterplayer.dungeon.worqor" or
        "masterplayer.dungeon.skydeep" or
        "masterplayer.dungeon.vanguard" or
        "masterplayer.dungeon.origenics" or
        "masterplayer.dungeon.alexandria" or
        "masterplayer.dungeon.anamnesis" or
        "masterplayer.dungeon.sastasha" or "masterplayer.dungeon.tamtara" or "masterplayer.dungeon.copperbell"
        or "masterplayer.dungeon.halatali" or "masterplayer.dungeon.totorak" or "masterplayer.dungeon.haukke"
        or "masterplayer.dungeon.brayflox" or "masterplayer.dungeon.qarn" or "masterplayer.dungeon.cutters-cry"
        or "masterplayer.dungeon.stone-vigil" or "masterplayer.dungeon.dzemael" or "masterplayer.dungeon.aurum-vale"
        or "masterplayer.dungeon.castrum-meridianum" or "masterplayer.dungeon.praetorium"
        or "masterplayer.dungeon.wanderers-palace" or "masterplayer.dungeon.amdapor-keep"
        or "masterplayer.dungeon.pharos-sirius" or "masterplayer.dungeon.snowcloak" or "masterplayer.dungeon.sastasha-hard"
        or "masterplayer.dungeon.keeper-of-the-lake" or "masterplayer.dungeon.sohm-al" or "masterplayer.dungeon.aery"
        or "masterplayer.dungeon.vault" or "masterplayer.dungeon.gubal-library" or "masterplayer.dungeon.research-facility"
        or "masterplayer.dungeon.fractal-continuum" or "masterplayer.dungeon.arboretum" or "masterplayer.dungeon.antitower"
        or "masterplayer.dungeon.sohr-khai" or "masterplayer.dungeon.xelphatol" or "masterplayer.dungeon.baelsars-wall"
        or "masterplayer.dungeon.sirensong-sea" or "masterplayer.dungeon.shisui" or "masterplayer.dungeon.bardam"
        or "masterplayer.dungeon.doma-castle" or "masterplayer.dungeon.ala-mhigo" or "masterplayer.dungeon.skalla"
        or "masterplayer.dungeon.burn" or "masterplayer.dungeon.ghimlyt" or "masterplayer.dungeon.holminster"
        or "masterplayer.dungeon.dohn-mheg" or "masterplayer.dungeon.qitana" or "masterplayer.dungeon.malikah"
        or "masterplayer.dungeon.mt-gulg" or "masterplayer.dungeon.amaurot" or "masterplayer.dungeon.twinning"
        or "masterplayer.dungeon.anyder" or "masterplayer.dungeon.cosmos";

    public static IEnumerable<uint> ClaimedActions(IEnumerable<DungeonDefinition> encounters)
        => encounters.SelectMany(e => e.Casts.Select(c => c.Action).Concat(e.VisualActions)).Distinct();

    public static bool TryGetShape(uint action, out AoeShapeDescriptor? descriptor)
    {
        descriptor = action switch
        {
            351 => Circle(6.15f), 987 => Circle(9.95f), 28544 => Circle(12),
            28462 => Circle(4), 28465 or 28761 => Circle(8), 28760 => Circle(10),
            556 => Cone(8, MathF.PI / 2), 912 => Cone(7, MathF.PI / 2),
            28546 => Cone(30, MathF.PI / 3), 28763 => Cone(40, MathF.PI / 3),
            28762 => new(AoeShapeKind.Donut, 4, 40, false, 0.3f),
            _ => null
        };
        return descriptor != null;
    }

    private static AoeShapeDescriptor Circle(float radius) => new(AoeShapeKind.Circle, radius, 0, false, 0.3f);
    private static AoeShapeDescriptor Cone(float radius, float angle) => new(AoeShapeKind.Fan, radius, angle, false, 0.3f) { UseCastRotation = true };
}

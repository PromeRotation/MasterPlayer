using System.Reflection;
using System.Runtime.Loader;
using System.Text;

internal static class NativeDungeonArenaExport
{
    public static void RunDalriadaSpirals(string runtimePath, string destination)
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(runtimePath));
        var type = assembly.GetType("BossMod.Shadowbringers.Foray.TheDalriada.DAL3SaunionDawon.SpiralScourge", true)!;
        var patterns = (Array)type.GetField("patterns", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var output = new StringBuilder("// Generated from source SpiralScourge patterns using --dalriada-spirals.\nusing System.Numerics;\n\nnamespace MasterPlayer.Mechanics.Bozja;\n\ninternal static class DalriadaSpirals\n{\n    internal static readonly Vector3[][] Patterns =\n    [\n");
        var count = 0;
        foreach (Array pattern in patterns)
        {
            output.Append("        [\n");
            foreach (var point in pattern)
            {
                var x = (float)point!.GetType().GetField("X")!.GetValue(point)!;
                var z = (float)point.GetType().GetField("Z")!.GetValue(point)!;
                output.Append(FormattableString.Invariant($"            new({x:R}f, 0, {z:R}f),\n"));
                ++count;
            }
            output.Append("        ],\n");
        }
        output.Append("    ];\n}\n");
        File.WriteAllText(destination, output.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"Exported {patterns.Length} SpiralScourge patterns, {count} exact points.");
    }

    public static void Run(string runtimePath, string destination, string group = "brayflox")
    {
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(runtimePath));
        var (className, folder) = group switch
        {
            "delubrum" => ("DelubrumArenas", ""),
            "dalriada" => ("DalriadaArenas", ""),
            "castrum-lacus" => ("CastrumLacusArenas", ""),
            "thordan" => ("ThordanArenas", ""),
            "quantum" => ("QuantumArenaData", ""),
            "eureka" => ("EurekaOrthosArenas", ""),
            "hoh" => ("HeavenOnHighArenas", ""),
            "carnivale" => ("CarnivaleArenas", ""),
            "ttokrrone" => ("TtokrroneArenas", ""),
            "treasure-cenote-rooms" => ("NativeCenoteRoomArenas", ""),
            "treasure-canals-lucky" => ("NativeTreasureArenas", ""),
            "endwalker-quest" => ("EndwalkerQuestArenas", ""),
            "heavensward-quest" => ("HeavenswardQuestArenas", ""),
            "labyrinth" => ("LabyrinthArenas", ""),
            "doomtrain" => ("DoomtrainArenas", ""),
            "queen" => ("QueenEternalArenas", ""),
            "four-lords" => ("FourLordsArenas", ""),
            "mistwake" => ("MistwakeArenas", "D12Mistwake"),
            "meso" => ("MesoArenas", "D11MesoTerminal"),
            "underkeep" => ("UnderkeepArenas", "D10Underkeep"),
            "yuweyawata" => ("YuweyawataArenas", "D09YuweyawataFieldStation"),
            "strayborough" => ("StrayboroughArenas", "D08StrayboroughDeadwalk"),
            "tender-valley" => ("TenderValleyArenas", "D07TenderValley"),
            "zot" => ("ZotArenas", "D01TowerOfZot"),
            "babil" => ("BabilArenas", "D02TowerOfBabil"),
            "vanaspati" => ("VanaspatiArenas", "D03Vanaspati"),
            "ktisis" => ("KtisisArenas", "D04KtisisHyperboreia"),
            "aitiascope" => ("AitiascopeArenas", "D05Aitiascope"),
            "deadends" => ("DeadEndsArenas", "D06DeadEnds"),
            "alzadaal" => ("AlzadaalArenas", "D09AlzadaalsLegacy"),
            "troia" => ("TroiaArenas", "D10FellCourtOfTroia"),
            "lapis" => ("LapisArenas", "D11LapisManalis"),
            "aetherfont" => ("AetherfontArenas", "D12Aetherfont"),
            "ihuykatumu" => ("IhuykatumuArenas", "D01Ihuykatumu"),
            "worqor" => ("WorqorArenas", "D02WorqorZormor"),
            "skydeep" => ("SkydeepArenas", "D03SkydeepCenote"),
            "vanguard" => ("VanguardArenas", "D04Vanguard"),
            "origenics" => ("OrigenicsArenas", "D05Origenics"),
            "sohm-al" => ("SohmAlArenas", "D02SohmAl"),
            "aery" => ("AeryArenas", "D03Aery"),
            "vault" => ("VaultArenas", "D04TheVault"),
            "gubal" => ("GubalArenas", "D05GreatGubalLibrary"),
            "arf" => ("ResearchFacilityArenas", "D06AetherochemicalResearchFacility"),
            "arf-trash" => ("ResearchTrashArenas", "D06AetherochemicalResearchFacility"),
            "fractal" => ("FractalArenas", "D08FractalContinuum"),
            "arboretum" => ("ArboretumArenas", "D09SaintMociannesArboretum"),
            "malboro" => ("MalboroArenas", "D09SaintMociannesArboretum"),
            "antitower" => ("AntitowerArenas", "D11Antitower"),
            "sohr-khai" => ("SohrKhaiArenas", "D13SohrKhai"),
            "xelphatol" => ("XelphatolArenas", "D15Xelphatol"),
            "baelsar" => ("BaelsarArenas", "D17BaelsarsWall"),
            "sirensong" => ("SirensongArenas", "D01SirensongSea"),
            "shisui" => ("ShisuiArenas", "D02ShisuiOfTheVioletTides"),
            "bardam" => ("BardamArenas", "D03BardamsMettle"),
            "abania" => ("AbaniaArenas", "D05CastrumAbania"),
            "ala-mhigo" => ("AlaMhigoArenas", "D06AlaMhigo"),
            "burn" => ("BurnArenas", "D13TheBurn"),
            "ghimlyt" => ("GhimlytArenas", "D15TheGhimlytDark"),
            "holminster" => ("HolminsterArenas", "D01Holminster"),
            "qitana" => ("QitanaArenas", "D03QitanaRavel"),
            "malikah" => ("MalikahArenas", "D04MalikahsWell"),
            "mt-gulg" => ("MtGulgArenas", "D05MtGulg"),
            "amaurot" => ("AmaurotArenas", "D06Amaurot"),
            "twinning" => ("TwinningArenas", "D07Twinning"),
            "anyder" => ("AnyderArenas", "D08AkadaemiaAnyder"),
            "anamnesis" => ("AnamnesisArenas", "D10AnamnesisAnyder"),
            "heroes" => ("HeroesArenas", "D11HeroesGauntlet"),
            "paglthan" => ("PaglthanArenas", "D13Paglthan"),
            "dohn-mheg" => ("DohnMhegArenas", "D02DohnMheg"),
            "keeper" => ("KeeperArenas", "D29TheKeeperoftheLake"),
            "snowcloak" => ("SnowcloakArenas", "D26Snowcloak"),
            "pharos" => ("PharosSiriusArenas", "D17PharosSirius"),
            "castrum" => ("CastrumMeridianumArenas", "D13CastrumMeridianum"),
            "praetorium" => ("PraetoriumArenas", "D14Praetorium"),
            "aurum" => ("AurumValeArenas", "D12AurumVale"),
            "dzemael" => ("DzemaelArenas", "D11DzemaelDarkhold"),
            "stonevigil" => ("StoneVigilArenas", "D10StoneVigil"),
            _ => ("BrayfloxArenas", "D07Brayflox"),
        };
        var output = new StringBuilder($"// Generated from resolved runtime polygons using --{group}-arenas.\nusing System.Numerics;\nusing MasterPlayer.Spatial.Geometry;\n\nnamespace MasterPlayer.Mechanics.Dungeons;\n\ninternal static class {className}\n{{\n");
        (string Name, string Type)[] encounters = group switch
        {
            "delubrum" => [("Dahu", "BossMod.Shadowbringers.Foray.DelubrumReginae.Dahu"),
                ("PhantomHazard", "BossMod.Shadowbringers.Foray.DelubrumReginae.Phantom"),
                ("SeekerStarting", "BossMod.Shadowbringers.Foray.DelubrumReginae.TrinitySeeker"),
                ("SeekerDefault", "BossMod.Shadowbringers.Foray.DelubrumReginae.TrinitySeeker"),
                ("SeekerBladeOuter", "BossMod.Shadowbringers.Foray.DelubrumReginae.TrinitySeeker"),
                ("SeekerBladeInner", "BossMod.Shadowbringers.Foray.DelubrumReginae.TrinitySeeker"),
                ("SeekerBladeOuterStarting", "BossMod.Shadowbringers.Foray.DelubrumReginae.TrinitySeeker"),
                ("SeekerBladeInnerStarting", "BossMod.Shadowbringers.Foray.DelubrumReginae.TrinitySeeker"),
                ("GuardStarting", "BossMod.Shadowbringers.Foray.DelubrumReginae.QueensGuard"),
                ("GuardDefault", "BossMod.Shadowbringers.Foray.DelubrumReginae.QueensGuard"),
                ("QueenDefault", "BossMod.Shadowbringers.Foray.DelubrumReginae.Queen")],
            "castrum-lacus" => [("Albeleo", "BossMod.Shadowbringers.Foray.CastrumLacusLitore.CLL2AlbeleoTheMaleficient.CLL2AlbeleoTheMaleficent")],
            "dalriada" => [("Colossus", "BossMod.Shadowbringers.Foray.TheDalriada.DAL04thLegionColossusRubricatus.DAL04thLegionColossusRubricatus"),
                ("Cuchulainn", "BossMod.Shadowbringers.Foray.TheDalriada.DAL2Cuchulainn.DAL2Cuchulainn"),
                ("Diablo", "BossMod.Shadowbringers.Foray.TheDalriada.DAL4DiabloArmament.DAL4DiabloArmament"),
                ("GauntletHazard", "BossMod.Shadowbringers.Foray.TheDalriada.DAL1Gauntlet.ArenaChange")],
            "thordan" => [("Starting", "BossMod.Heavensward.Extreme.Ex3Thordan.Ex3Thordan"),
                ("Reduced", "BossMod.Heavensward.Extreme.Ex3Thordan.Ex3Thordan")],
            "quantum" => [("Initial", "BossMod.Dawntrail.Quantum.Q1FinalVerse.Q1FinalVerse")],
            "eureka" => [("ProtoKaliya", "BossMod.Endwalker.DeepDungeon.EurekaOrthos.DD80ProtoKaliya.DD80ProtoKaliya")],
            "hoh" => [("First", "BossMod.Stormblood.DeepDungeon.HeavenOnHigh.HoHArena1"),
                ("Second", "BossMod.Stormblood.DeepDungeon.HeavenOnHigh.HoHArena2"),
                ("Ninth", "BossMod.Stormblood.DeepDungeon.HeavenOnHigh.DD90Onra.DD90Onra")],
            "carnivale" => [("Layout4Quads", "BossMod.Global.MaskedCarnivale.Layouts"),
                ("Layout2Corners", "BossMod.Global.MaskedCarnivale.Layouts"),
                ("LayoutBigQuad", "BossMod.Global.MaskedCarnivale.Layouts")],
            "ttokrrone" => [("Ttokrrone", "BossMod.Dawntrail.FATE.Ttokrrone.Ttokrrone")],
            "treasure-cenote-rooms" => [("Room1", "BossMod.Dawntrail.TreasureHunt.CenoteJaJaGural.Room1.Room1"),
                ("Room2", "BossMod.Dawntrail.TreasureHunt.CenoteJaJaGural.Room2.Room2"),
                ("Room3", "BossMod.Dawntrail.TreasureHunt.CenoteJaJaGural.Room3.Room3"),
                ("Room4", "BossMod.Dawntrail.TreasureHunt.CenoteJaJaGural.Room4.Room4")],
            "treasure-canals-lucky" => [("Canals", "BossMod.Stormblood.TreasureHunt.FinalRoomArena"),
                ("LuckyFace", "BossMod.Endwalker.TreasureHunt.Excitatron6000.LuckyFace.LuckyFace")],
            "endwalker-quest" => [("Scarmiglione", "WhereEverythingBegins.P1.ScarmiglioneP1"),
                ("Vergilia", "AFrostyReception.VergiliaVanCorculum"), ("Colibri", "TheGameIsAfoot.GiantColibri")],
            "heavensward-quest" => [("DivineIntervention", "DivineIntervention.SerGrinnaux")],
            "labyrinth" => [("BoneDragon", "A11BoneDragon.A11BoneDragon")],
            "doomtrain" => new[] { "Car2", "Car3Ground", "Car3Air", "Car4Ground", "Car4Air", "Car5Ground", "Car5Air", "Intermission" }
                .Select(n => (n, "T07Doomtrain.Doomtrain")).ToArray(),
            "queen" => new[] { "X", "XInsetHalf", "XInsetOne", "Split" }
                .Select(n => (n, "T03QueenEternal.T03QueenEternal")).ToArray(),
            "four-lords" => [("SuzakuRing", "T08Suzaku.T08Suzaku"), ("SeiryuPhase1", "T09Seiryu.Seiryu"), ("SeiryuPhase2", "T09Seiryu.Seiryu")],
            "mistwake" => [("Amdusias", "D122Amdusias.D122Amdusias"), ("Griffin", "D123ThundergustGriffin.D123ThundergustGriffin"),
                ("GriffinSmall", "D123ThundergustGriffin.D123ThundergustGriffin")],
            "meso" => [("Headsmen", "D112FourHeadsmen.D112FourHeadsmen")],
            "underkeep" => [("Gargant", "D101Gargant.D101Gargant")],
            "yuweyawata" =>
            [
                ("Bat", "D90ForestBat.D90ForestBat"), ("Woolback", "D90ForestWoolback.D90ForestWoolback"),
                ("Zaghnal", "D091LindblumZaghnal.D091LindblumZaghnal"), ("Researcher", "D90RottenResearcher.D90RottenResearcher"),
                ("Specter", "D90StationSpecter.D90StationSpecter"), ("Phoebad", "D90SprightlyPhoebad.D90SprightlyPhoebad"),
                ("Golem", "D90SprightlyClayGolem.D90SprightlyClayGolem"),
                .. new[] { "KanilokkaStarting", "KanilokkaDefault", "KanilokkaTiny", "KanilokkaPathsA", "KanilokkaPathsB" }
                    .Select(n => (n, "D092OverseerKanilokka.D092OverseerKanilokka")),
                .. new[] { "LunipyatiStarting", "LunipyatiDefault", "LunipyatiDonut" }.Select(n => (n, "D093Lunipyati.cs.D093Lunipyati")),
            ],
            "strayborough" => [("Jack", "D082JackInThePot.D082JackInThePot"), ("Cross", "D083Träumerei.D083Träumerei"),
                .. Enumerable.Range(0, 4).Select(i => ($"Platform{i}", "D083Träumerei.GhostlyGuise"))],
            "tender-valley" => [("Anthracite", "D072Anthracite.D072Anthracite"), ("Attestant", "D073YokHuyAttestant.D073YokHuyAttestant"),
                .. Enumerable.Range(0, 4).Select(i => ($"Labyrinth{i}", "D074GreatestSerpentOfTural.GreatestLabyrinth"))],
            "origenics" => [("Aerostat", "D050OrigenicsAerostat.D050OrigenicsAerostat"),
                .. new[] { 0x2A, 0x1B, 0x2C, 0x1E, 0x2D, 0x1D, 0x2B, 0x1C }
                    .Select(i => ($"Deceiver{i:X2}", "D052Deceiver.ArenaChanges"))],
            "vanguard" => [("Leptocyon", "D040VanguardLeptocyon.D040VanguardLeptocyon"),
                ("Sentry", "D040VanguardSentryR7.D040VanguardSentryR7"), ("Aerostat", "D040VanguardAerostat2.D040VanguardAerostat2"),
                ("ZanderStarting", "D043Zander.D043Zander"), ("ZanderSmall", "D043Zander.D043Zander"),
                .. Enumerable.Range(0, 4).SelectMany(i => new[] { ($"Fence{i}Aoe", "D042Protector.ArenaChanges"), ($"Fence{i}Arena", "D042Protector.ArenaChanges") })],
            "skydeep" => [("FeatherSmall", "D031FeatherRay.D031FeatherRay")],
            "ihuykatumu" => [("Drowsie", "D012Drowsie.D012Drowsie"), ("Apollyon", "D013Apollyon.D013Apollyon")],
            "worqor" => [("RyoqorStarting", "D021RyoqorTerteh.D021RyoqorTerteh"),
                ("RyoqorSmall", "D021RyoqorTerteh.D021RyoqorTerteh"), ("Kahderyor", "D022Kahderyor.D022Kahderyor"),
                ("Wind20Wide", "D022Kahderyor.WindEarthShot"), ("Wind20Narrow", "D022Kahderyor.WindEarthShot"),
                ("Wind21Wide", "D022Kahderyor.WindEarthShot"), ("Wind21Narrow", "D022Kahderyor.WindEarthShot")],
            "aetherfont" => [("Otodus", "D120HaamOtodus.D120HaamOtodus"), ("Halonid", "D120HaamHalonid.D120HaamHalonid"),
                ("Troll", "D120HaamTroll.D120HaamTroll"), ("Crystal", "D120HaamCrystal.D120HaamCrystal"),
                ("Forlorn", "D120HaamForlorn.D120HaamForlorn"), ("Lyngbakr", "D121Lyngbakr.D121Lyngbakr"),
                ("Arkas", "D122Arkas.D122Arkas"), ("ArkasSmall", "D122Arkas.D122Arkas"),
                ("Octomammoth", "D123Octomammoth.D123Octomammoth")],
            "lapis" => [("Galatea", "D112GalateaMagna.D112GalateaMagna")],
            "troia" => new (string, string)[]
            {
                ("ScavangerFirst", "D100TroianScavanger.D100TroianScavanger"),
                ("ScavangerSecond", "D100TroianScavanger.D100TroianScavanger"),
                ("Footman", "D100TroianFootman.D100TroianFootman"), ("Bishop", "D100TroianBishop.D100TroianBishop"),
                ("ButlerFirst", "D100TroianButler.D100TroianButler"), ("ButlerSecond", "D100TroianButler.D100TroianButler"),
                ("Dreamers", "D101EvilDreamers.D101EvilDreamers"), ("Beatrice", "D102Beatrice.D102Beatrice"),
                ("ScarmiglioneStarting", "D103Scarmiglione.D103Scarmiglione"),
                ("ScarmiglioneLargeBase", "D103Scarmiglione.D103Scarmiglione"),
                ("ScarmiglioneSmallBase", "D103Scarmiglione.D103Scarmiglione"),
            }.Concat(Enumerable.Range(0, 12).Select(i => ($"ScarmiglioneWall{i}", "D103Scarmiglione.D103Scarmiglione"))).ToArray(),
            "alzadaal" => [("Ambujam", "D091Ambujam.D091Ambujam")],
            "zot" => [("Minduruva", "D011Minduruva.D011Minduruva"), ("Sanduruva", "D012Sanduruva.D012Sanduruva"),
                ("Sisters", "D013MagusSisters.D013MagusSisters")],
            "babil" => [("BarnabasStarting", "D021Barnabas.D021Barnabas"), ("BarnabasSmall", "D021Barnabas.D021Barnabas")],
            "vanaspati" => [("Snatcher", "D031Snatcher.D031Snatcher"), ("WreckerStarting", "D032Wrecker.D032Wrecker"),
                ("WreckerSmall", "D032Wrecker.D032Wrecker")],
            "ktisis" => [("Lyssa", "D041Lyssa.D041Lyssa"), ("HermesStarting", "D043Hermes.D043Hermes"),
                ("HermesSmall", "D043Hermes.D043Hermes")],
            "aitiascope" => [("Livia", "D051Livia.D051Livia"), ("Amon", "D053Amon.D053Amon"),
                ("AmonCurtain", "D053Amon.CurtainCallArenaChange")],
            "deadends" => [("Grebuloff", "D061CausticGrebuloff.D061CausticGrebuloff"),
                ("PeacekeeperStarting", "D062Peacekeeper.D062Peacekeeper"), ("PeacekeeperSmall", "D062Peacekeeper.D062Peacekeeper"),
                ("Rala", "D063Rala.D063Rala")],
            "sohm-al" => [("Raskovnik", "D021Raskovnik.D021Raskovnik"), ("Myath", "D022Myath.D022Myath"),
                ("Tioman", "D023Tioman.D023Tioman")],
            "aery" => [("Rangda", "D031Rangda.D031Rangda"), ("GyascutusStarting", "D032Gyascutus.D032Gyascutus"),
                ("Nidhogg", "D033Nidhogg.D033Nidhogg")],
            "vault" => [("Adelphel", "D041SerAdelphel.D041SerAdelphel"), ("Grinnaux", "D042SerGrinnaux.D042SerGrinnaux")],
            "gubal" => [("DemonTome", "D051DemonTome.D051DemonTome"), ("DemonTomeSpin", "D051DemonTome.D051DemonTome"),
                ("Byblos", "D052Byblos.D052Byblos"), ("Bibliotaph", "D053EverlivingBibliotaph.D053EverlivingBibliotaph")],
            "arf" => [("Regula", "D061Regula.D061Regula"), ("Harmachis", "D062Harmachis.D062Harmachis"),
                ("Lahabrea", "D063LahabreaIgeyorhm.D063LahabreaIgeyorhm"), ("AscianPrime", "D064AscianPrime.D064AscianPrime")],
            "arf-trash" => [("Enforcement", "D060Trash1.D060Trash1"),
                ("Vanguard", "D060SixthLegionMagitekVanguardH1.D060SixthLegionMagitekVanguardH1"),
                ("Dreadnaught", "D060FacilityDreadnaught.D060FacilityDreadnaught"),
                ("Shabti", "D060CulturedShabti.D060CulturedShabti"),
                ("Conjurer", "D060ClonedConjurer.D060ClonedConjurer"),
                ("ShabtiConjurer", "ShabtiConjurer2Bounds"),
                ("BiocultureFirst", "D060BiocultureNode.D060BiocultureNode"),
                ("BiocultureFirstFilled", "D060BiocultureNode.D060BiocultureNode"),
                ("BiocultureSecond", "D060BiocultureNode.D060BiocultureNode"),
                ("BiocultureSecondFilled", "D060BiocultureNode.D060BiocultureNode")],
            "antitower" => [("ZuroRoggo", "D111ZuroRoggo.D111ZuroRoggo"), ("Ziggy", "D112Ziggy.D112Ziggy"),
                ("Calcabrina", "D113Calcabrina.D113Calcabrina")],
            "sohr-khai" => [("Moglin", "D131ChieftainMoglin.D131ChieftainMoglin"), ("Hraesvelgr", "D133Hraesvelgr.D133Hraesvelgr"),
                ("GardenFirst", "D130CloudGardener.D130CloudGardener"), ("GardenSecond", "D130CloudGardener.D130CloudGardener"),
                ("Kargas", "D130Kargas.D130Kargas"), ("BlizzardFirst", "D130BlizzardDragon.D130BlizzardDragon"),
                ("BlizzardBridge", "D130BlizzardDragon.D130BlizzardDragon")],
            "xelphatol" => [
                ("HarpeiaFirst", "D150ChasmHarpeia.D150ChasmHarpeia"), ("HarpeiaSecond", "D150ChasmHarpeia.D150ChasmHarpeia"),
                ("SiegeFirst", "D150SiegeGobbue.D150SiegeGobbue"), ("SiegeSecond", "D150SiegeGobbue.D150SiegeGobbue"),
                ("SwiftFirst", "D150XelphatolSwiftbeak.D150XelphatolSwiftbeak"),
                ("SwiftSecond", "D150XelphatolSwiftbeak.D150XelphatolSwiftbeak"),
                ("SwiftThird", "D150XelphatolSwiftbeak.D150XelphatolSwiftbeak"),
                ("Skycaller", "D150XelphatolSkycaller.D150XelphatolSkycaller"),
                ("Nuzal", "D151NuzalHueloc.D151NuzalHueloc"), ("Tozol", "D153TozolHuatotl.D153TozolHuatotl"),
                ("DotoliStarting", "Dotoli"), ("DotoliSmall", "Dotoli"), ("DotoliRing", "Dotoli"),
                ("SiegeHazardFirst", "D150SiegeGobbue.ArenaChange"), ("SiegeHazardSecond", "D150SiegeGobbue.ArenaChange")],
            "baelsar" => [("Griffin", "D173TheGriffin.D173TheGriffin")],
            "sirensong" => [("Lugat", "D011Lugat.D011Lugat"), ("Governor", "D012TheGovernor.DO12TheGovernor"),
                ("Lorelei", "D013Lorelei.D013Lorelei"), ("LoreleiSmall", "D013Lorelei.D013Lorelei")],
            "shisui" => [("Amikiri", "D021Amikiri.D021Amikiri"), ("RubyPrincess", "D022RubyPrincess.D022RubyPrincess"),
                ("Yohi", "D023ShisuiYohi.D023ShisuiYohi")],
            "bardam" => [("Garula", "D031Garula.D031Garula"), ("Hunter", "D032HunterOfBardam.D032HunterOfBardam"),
                ("Yol", "D033Yol.D033Yol")],
            "abania" => [("Magna", "D051MagnaRoader.D051MagnaRoader"), ("Subject", "D052SubjectNumberXXIV.D052SubjectNumberXXIV"),
                ("Inferno", "D053Inferno.D053Inferno")],
            "ala-mhigo" => [("Scorpion", "D061MagitekScorpion.D061MagitekScorpion"), ("Aulus", "D062AulusMalAsina.D062AulusMalAsina")],
            "burn" => [("Hedetet", "D131Hedetet.D131Hedetet")],
            "holminster" => [("Dissonance", "D011ForgivenDissonance.D011ForgivenDissonance"),
                ("Tesleen", "D012TesleentheForgiven.D012TesleentheForgiven"),
                ("Philia", "D013Philia.D013Philia")],
            "qitana" => [("DreamerFirst", "D030RonkanDreamer.D030RonkanDreamer"),
                ("DreamerSecond", "D030RonkanDreamer.D030RonkanDreamer"),
                ("Lozatl", "D031Lozatl.D031Lozatl"), ("Batsquatch", "D032Batsquatch.D032Batsquatch")],
            "malikah" => [("Armadillo", "D041GreaterArmadillo.D041GreaterArmadillo"),
                ("Talos", "D042AmphibiousTalos.D042AmphibiousTalos"), ("Storge", "D043Storge.D043Storge")],
            "mt-gulg" => [("Cruelty", "D051ForgivenCruelty.D051ForgivenCruelty"),
                ("ApathyFirst", "D052ForgivenApathy.D052ForgivenApathy"),
                ("ApathySecond", "D052ForgivenApathy.D052ForgivenApathy"),
                ("ObscenitySmall", "D055ForgivenObscenity.PenancePianissimo")],
            "amaurot" => [("Bellwether", "D062Bellwether.D062Bellwether"),
                .. Enumerable.Range(0, 256).Select(mask => ($"Therion{mask}", "D063Therion.Border"))],
            "twinning" => [("Mithridates", "D072Mithridates.D072Mithridates"), ("Tycoon", "D073TheTycoon.D073TheTycoon")],
            "anyder" => [("Sharks", "D081Cladoselache.D081Cladoselache"),
                ("Morbol", "D082MorbolMarquis.D082MorbolMarquis"),
                ("MorbolBlue", "D082MorbolMarquis.D082MorbolMarquis"),
                ("MorbolYellow", "D082MorbolMarquis.D082MorbolMarquis"),
                ("MorbolBlueDanger", "D082MorbolMarquis.D082MorbolMarquis"),
                ("MorbolYellowDanger", "D082MorbolMarquis.D082MorbolMarquis"),
                ("Quetzalcoatl", "D083Quetzalcoatl.D083Quetzalcoatl")],
            "anamnesis" => [("Unknown", "D101Unknown.D101Unknown"), ("Split", "D103RukshsDheem.D103RukshsDheem")],
            "heroes" => [("Necromancer", "D112SpectralNecromancer.D112SpectralNecromancer"),
                ("Berserker", "D113SpectralBerserker.D113SpectralBerserker"),
                ("BerserkerLanding", "D113SpectralBerserker.D113SpectralBerserker"),
                ("BerserkerCollapse", "D113SpectralBerserker.D113SpectralBerserker")],
            "paglthan" => [("Amhuluk", "D131Amhuluk.D131Amhuluk"), ("LunarBahamut", "D133LunarBahamut.D133LunarBahamut")],
            "dohn-mheg" => [("First", "D021AencThon.D021AencThon"), ("Griaule", "D022Griaule.D022Griaule"),
                ("Last", "D031AencThon.D033AencThon"), ("Chasm", "D031AencThon.D033AencThon")],
            "ghimlyt" => [("Schola", "D150ScholaColossus.D0150ScholaColossus"),
                ("ScholaII", "D150ScholaMarkIIColossus.D0150ScholaMarkIIColossus"),
                ("Colossus", "D151MarkIIIBMagitekColossus.D151MarkIIIBMagitekColossus"),
                ("PrometheusStarting", "D152Prometheus.D152Prometheus"),
                ("Prometheus", "D152Prometheus.D152Prometheus"), ("Nitro", "D152Prometheus.D152Prometheus"),
                ("Duo", "D153SoranusDuo.D153SoranusDuo")],
            "malboro" => [("First", "D090MalboroTerra.D090MalboroTerra"),
                ("Open", "D090MalboroTerra.D090MalboroTerra"), ("Second", "D090MalboroTerra.D090MalboroTerra")],
            "arboretum" => [("RoseGarden", "D091RoseGarden.D091RoseGarden"), ("QueenHawk", "D092QueenHawk.D092QueenHawk"),
                ("Belladonna", "D093Belladonna.D093Belladonna"),
                ("AntFirst", "D090VelveteenAnt.D090VelveteenAnt"), ("AntFirstOpen", "D090VelveteenAnt.D090VelveteenAnt"),
                ("AntSecond", "D090VelveteenAnt.D090VelveteenAnt"), ("AntSecondOpen", "D090VelveteenAnt.D090VelveteenAnt"),
                ("NarbrooiFirst", "D090TangledNarbrooi.D090TangledNarbrooi"), ("NarbrooiFirstOpen", "D090TangledNarbrooi.D090TangledNarbrooi"),
                ("NarbrooiSecond", "D090TangledNarbrooi.D090TangledNarbrooi")],
            "fractal" => [("Clockwork", "D080ClockworkDreadnaught.D080ClockworkDreadnaught"),
                ("Manikin", "D080ManikinConjurer.D080ManikinConjurer"),
                ("Naga", "D080FlawlessNaga.D080FlawlessNaga"),
                ("Violet", "D080VioletControlUnit.D080VioletControlUnit"),
                ("Emerald", "D080EmeraldControlUnit.D080EmeraldControlUnit"),
                ("Crimson", "D080CrimsonControlUnit.D080CrimsonControlUnit"),
                ("PhantomRay", "D081PhantomRay.D081PhantomRay"), ("Minotaur", "D082Minotaur.D082Minotaur")],
            "keeper" => [("Einhander", "D291Einhander.D291Einhander"), ("Gunship", "D292MagitekGunship.D292MagitekGunship"),
                ("Midgardsormr", "D293Midgardsormr.D293Midgardsormr")],
            "snowcloak" => [
                ("NorthernStarting", "D260NorthernBateleur.D260NorthernBateleur"),
                ("Northern", "D260NorthernBateleur.D260NorthernBateleur"),
                ("WandilStarting", "D261Wandil.D261Wandil"),
                ("Wandil", "D261Wandil.D261Wandil"),
                ("Yeti", "D262Yeti.D262Yeti"), ("Fenrir", "D263Fenrir.D263Fenrir")],
            "pharos" => [
                ("Zu", "D172Zu.D172Zu"), ("Tyrant", "D173Tyrant.D173Tyrant")],
            "castrum" => [
                ("BlackEft", "D131BlackEft.D131BlackEft"),
                ("Vanguard", "D132MagitekVanguardF1.D132MagitekVanguardF1"),
                ("Livia", "D133Livia.D133Livia")],
            "praetorium" => [
                ("Colossus", "D141Colossus.D141Colossus"),
                ("NeroStarting", "D142Nero.D142Nero"),
                ("Nero", "D142Nero.D142Nero")],
            "aurum" => [
                ("Locksmith", "D121Locksmith.D121Locksmith"),
                ("Coincounter", "D122Coincounter.D122Coincounter"),
                ("Mistress", "D123MisersMistress.D123MisersMistress")],
            "dzemael" => [
                ("AllSeeingEye", "D111AllSeeingEye.D111AllSeeingEye"),
                ("Taulurd", "D112Taulurd.D112Taulurd"),
                ("Batraal", "D113Batraal.D113Batraal")],
            "stonevigil" => [
                ("ChudoYudo", "D101ChudoYudo.D101ChudoYudo"),
                ("Koshchei", "D102Koshchei.D102Koshchei"),
                ("Isgebind", "D103Isgebind.D103Isgebind")],
            _ => [
                ("Pelican", "D071GreatYellowPelican.D071GreatYellowPelican"),
                ("Drake", "D072InfernoDrake.D072InfernoDrake"),
                ("Hellbender", "D073Hellbender.D120Hellbender"),
                ("Aiatar", "D074Aiatar.D074Aiatar")],
        };
        foreach (var (name, type) in encounters)
        {
            var expansion = group switch
            {
                "ihuykatumu" or "worqor" or "skydeep" or "vanguard" or "origenics" or "tender-valley" or "strayborough" or "yuweyawata" or "underkeep" or "meso" or "mistwake" => "Dawntrail",
                "zot" or "babil" or "vanaspati" or "ktisis" or "aitiascope" or "deadends" or "alzadaal" or "troia" or "lapis" or "aetherfont" => "Endwalker",
                "holminster" or "dohn-mheg" or "qitana" or "malikah" or "mt-gulg" or "amaurot" or "twinning" or "anyder" or "anamnesis" or "heroes" or "paglthan" => "Shadowbringers",
                "sirensong" or "shisui" or "bardam" or "abania" or "ala-mhigo" or "burn" or "ghimlyt" => "Stormblood",
                "sohm-al" or "aery" or "vault" or "gubal" or "arf" or "arf-trash" or "fractal" or "arboretum" or "malboro"
                    or "antitower" or "sohr-khai" or "xelphatol" or "baelsar" => "Heavensward",
                _ => "RealmReborn",
            };
            var encounter = assembly.GetType(group is "queen" or "doomtrain"
                ? "BossMod.Dawntrail.Trial." + type
                : group == "four-lords"
                ? "BossMod.Stormblood.Trial." + type
                : group == "zot" && name == "Sanduruva"
                ? "BossMod.Endwalker.Dungeon.D01TheTowerOifZot." + type
                : group == "zot" && name == "Sisters"
                ? "BossMod.Endwalker.Dungeon.D01TheTowerOfZot." + type
                : name == "Philia" && group == "holminster"
                ? "BossMod.Shadowbringers.Dungeon.D01Holmintser.D013Philia.D013Philia"
                : type == "Dotoli"
                ? "BossMod.Heavensward.Dungeon.D11Antitower.D152DotoliCiloc.D152DotoliCiloc"
                : $"BossMod.{expansion}.Dungeon.{folder}." + type)!;
            if (group == "labyrinth") encounter = assembly.GetType("BossMod.RealmReborn.Alliance." + type)!;
            if (group == "heavensward-quest") encounter = assembly.GetType("BossMod.Heavensward.Quest.MSQ." + type)!;
            if (group == "endwalker-quest") encounter = assembly.GetType("BossMod.Endwalker.Quest.MSQ." + type)!;
            if (group is "treasure-canals-lucky" or "treasure-cenote-rooms") encounter = assembly.GetType(type)!;
            if (group is "ttokrrone" or "carnivale" or "hoh" or "eureka" or "quantum" or "thordan" or "delubrum" or "dalriada" or "castrum-lacus") encounter = assembly.GetType(type)!;
            object bounds;
            object? customPolygon = null;
            object? customCenter = null;
            var fieldName = name switch
            {
                "Dahu" when group == "delubrum" => "arena",
                "Diablo" when group == "dalriada" => "DefaultArena",
                _ when group is "dalriada" or "castrum-lacus" => "arena",
                "SeekerStarting" when group == "delubrum" => "StartingArena",
                "SeekerDefault" when group == "delubrum" => "DefaultArena",
                "GuardStarting" when group == "delubrum" => "startingArena",
                "GuardDefault" when group == "delubrum" => "DefaultArena",
                "QueenDefault" when group == "delubrum" => "DefaultArena",
                "Starting" when group == "thordan" => "startingArena",
                "Reduced" when group == "thordan" => "DefaultBounds",
                _ when group == "eureka" => "DonutArena",
                _ when group == "hoh" => "arena",
                _ when group == "carnivale" => name,
                _ when group == "ttokrrone" => "arena",
                _ when group == "treasure-cenote-rooms" => "ArenaBounds",
                "Scarmiglione" when group == "endwalker-quest" => "ArenaBounds",
                "X" or "XInsetHalf" or "XInsetOne" when group == "queen" => "XArena",
                "Split" when group == "queen" => "SplitArena",
                "SuzakuRing" => "Phase2Arena",
                "SeiryuPhase1" => "phase1Bounds",
                "SeiryuPhase2" => "Phase2Bounds",
                "First" when group == "malboro" => "Arena1",
                "Open" when group == "malboro" => "Arena1B",
                "Second" when group == "malboro" => "Arena2",
                "Headsmen" => "DefaultBounds",
                "KanilokkaStarting" => "StartingBounds", "KanilokkaDefault" => "DefaultArena", "KanilokkaTiny" => "TinyArena",
                "KanilokkaPathsA" => "ArenaENVC00800040", "KanilokkaPathsB" => "ArenaENVC02000100",
                "LunipyatiStarting" => "startingBounds", "LunipyatiDefault" => "DefaultBounds", "LunipyatiDonut" => "DonutBounds",
                "Cross" when group == "strayborough" => "CrossBounds",
                "ZanderStarting" => "StartingBounds", "ZanderSmall" => "DefaultBounds",
                _ when group == "vanguard" && name.StartsWith("Fence") => "electricFences"
                    + new[] { "01000080", "08000400", "00020001", "00200010" }[name[5] - '0']
                    + (name.EndsWith("Aoe") ? "AOE" : "Arena"),
                "FeatherSmall" => "CircleBounds",
                "Arkas" => "DefaultBounds", "ArkasSmall" => "SmallerBounds",
                "Drowsie" or "Apollyon" => "DefaultBounds",
                "RyoqorStarting" => "StartingBounds", "RyoqorSmall" or "Kahderyor" => "DefaultBounds",
                "AmonCurtain" => "CurtaincallArena",
                "Grebuloff" or "Rala" => "defaultBounds",
                "PeacekeeperStarting" => "StartingBounds", "PeacekeeperSmall" => "SmallerBounds",
                "WreckerStarting" => "StartingArena", "WreckerSmall" => "DefaultArena",
                "Ambujam" => "DefaultBounds",
                "ScavangerFirst" or "ButlerFirst" => "arena1",
                "ScavangerSecond" or "ButlerSecond" => "arena2",
                "ScarmiglioneStarting" => "StartingArena",
                "HermesStarting" => "StartingBounds", "HermesSmall" => "DefaultBounds",
                "BarnabasStarting" => "StartingBounds", "BarnabasSmall" => "SmallerBounds",
                "Berserker" when group == "heroes" => "DefaultBounds",
                "Split" when group == "anamnesis" => "SplitBounds",
                "DreamerFirst" => "arena1", "DreamerSecond" => "arena2",
                "Morbol" => "DefaultBounds", "MorbolBlue" => "BlueBlossomBounds", "MorbolYellow" => "YellowBlossomBounds",
                "ApathyFirst" => "arena1", "ApathySecond" => "arena2",
                "Last" when group == "dohn-mheg" => "ArenaBounds",
                "Chasm" when group == "dohn-mheg" => "ChasmArena",
                "PrometheusStarting" => "StartingArena", "Prometheus" => "DefaultArena",
                "NorthernStarting" => "arena1", "Northern" => "Arena2",
                "WandilStarting" => "DefaultArena", "Wandil" => "SmallArena",
                "NeroStarting" => "startingBounds", "Nero" => "DefaultBounds",
                "GyascutusStarting" => "StartingBounds",
                "DemonTome" => "DefaultArena", "DemonTomeSpin" => "SpinArena",
                "Shabti" or "Conjurer" => "arena1", "ShabtiConjurer" => "Arena",
                "BiocultureFirst" => "Arena1", "BiocultureFirstFilled" => "Arena1b",
                "BiocultureSecond" => "arena2", "BiocultureSecondFilled" => "Arena2b",
                "Manikin" or "Naga" or "Violet" => "arena1",
                "QueenHawk" => "ArenaBounds",
                "AntFirst" or "NarbrooiFirst" => "Arena1",
                "AntFirstOpen" or "NarbrooiFirstOpen" => "Arena1B",
                "AntSecond" or "NarbrooiSecond" => "Arena2",
                "AntSecondOpen" => "Arena2B",
                "GardenFirst" or "BlizzardFirst" => "arena1",
                "GardenSecond" => "arena2",
                "BlizzardBridge" => "Arena2",
                _ => "arena",
            };
            var field = encounter.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (group == "quantum")
            {
                var wpos = assembly.GetType("BossMod.WPos")!;
                var rectangle = Activator.CreateInstance(assembly.GetType("BossMod.Rectangle")!,
                    [Activator.CreateInstance(wpos, [-600f, -300f])!, 20f, 15f,
                        Activator.CreateInstance(assembly.GetType("BossMod.Angle")!)!])!;
                var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, 1);
                shapes.SetValue(rectangle, 0);
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var parameters = ctor.GetParameters();
                var values = parameters.Select(p => p.DefaultValue).ToArray();
                values[0] = shapes;
                values[Array.FindIndex(parameters, p => p.Name == "AdjustForHitboxOutwards")] = true;
                bounds = ctor.Invoke(values);
            }
            else if (group == "doomtrain")
            {
                object Point(float x, float z) => Activator.CreateInstance(assembly.GetType("BossMod.WPos")!, [x, z])!;
                object Rect(float x, float z, float w, float h) => Activator.CreateInstance(assembly.GetType("BossMod.Rectangle")!,
                    [Point(x, z), w, h, Activator.CreateInstance(assembly.GetType("BossMod.Angle")!)])!;
                Array Shapes(object[] items)
                {
                    var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, items.Length);
                    for (var i = 0; i < items.Length; ++i) shapes.SetValue(items[i], i);
                    return shapes;
                }
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var parameters = ctor.GetParameters();
                var values = parameters.Select(p => p.DefaultValue).ToArray();
                object[] union;
                object[] cuts = [];
                if (name == "Intermission")
                {
                    union = [Activator.CreateInstance(assembly.GetType("BossMod.Circle")!, [Point(-400, -400), 14.5f])!,
                        Activator.CreateInstance(assembly.GetType("BossMod.Donut")!, [Point(-400, -400), 22.4f, 27.5f])!];
                    values[Array.FindIndex(parameters, p => p.Name == "AdjustForHitboxInwards")] = true;
                }
                else if (name == "Car2")
                {
                    union = [Rect(100, 150, 10, 15)];
                    cuts = [Rect(102.5f, 147.5f, 2.5f, 2.5f), Rect(97.5f, 157.5f, 2.5f, 2.5f)];
                }
                else
                {
                    var fifth = name.StartsWith("Car5");
                    var dz = name.StartsWith("Car4") ? 50 : 0;
                    object[] platforms = fifth ? [Rect(92.4f, 310, 2.3f, 5), Rect(107.6f, 300, 2.3f, 5)]
                        : [Rect(92.5f, 205 + dz, 2.4f, 10), Rect(107.5f, 205 + dz, 2.4f, 10)];
                    union = name.EndsWith("Air") ? platforms : [Rect(100, fifth ? 300 : 200 + dz, 10, 15)];
                    if (name.EndsWith("Ground")) cuts = platforms;
                    // AOEShapeCustom is placed at the car center, not at the
                    // asymmetric platform union's bounding-box center.
                    values[Array.FindIndex(parameters, p => p.Name == "CenterOverride")] = Point(100, fifth ? 300 : 200 + dz);
                }
                values[0] = Shapes(union); values[1] = Shapes(cuts);
                bounds = ctor.Invoke(values);
            }
            else if (group == "strayborough" && name.StartsWith("Platform"))
            {
                var circles = (Array)encounter.GetField("circles", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, 1);
                shapes.SetValue(circles.GetValue(name[^1] - '0'), 0);
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var parameters = ctor.GetParameters();
                var args = parameters.Select(p => p.DefaultValue).ToArray();
                args[0] = shapes;
                args[Array.FindIndex(parameters, p => p.Name == "Offset")] = 0f;
                bounds = ctor.Invoke(args);
            }
            else if (group == "delubrum" && name.StartsWith("SeekerBlade"))
            {
                var center = encounter.GetField("ArenaCenter", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                if (name.EndsWith("Starting"))
                {
                    dynamic starting = encounter.GetField("StartingArena", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
                    center = starting.Center;
                }
                var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, 4);
                var angleType = assembly.GetType("BossMod.Angle")!;
                for (var i = 0; i < 4; ++i)
                {
                    var direction = Activator.CreateInstance(angleType, [(45f + i * 90f) * (MathF.PI / 180f)])!;
                    var halfAngle = Activator.CreateInstance(angleType, [22.5f * (MathF.PI / 180f)])!;
                    var shape = name.Contains("Outer")
                        ? Activator.CreateInstance(assembly.GetType("BossMod.DonutSegmentV")!, [center, 20f, 30f, direction, halfAngle, 6])
                        : Activator.CreateInstance(assembly.GetType("BossMod.ConeV")!, [center, 20f, direction, halfAngle, 6]);
                    shapes.SetValue(shape, i);
                }
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var parameters = ctor.GetParameters();
                var values = parameters.Select(p => p.DefaultValue).ToArray();
                values[0] = shapes;
                values[Array.FindIndex(parameters, p => p.Name == "Offset")] = 0f;
                bounds = ctor.Invoke(values);
                var customCtor = assembly.GetType("BossMod.AOEShapeCustom")!.GetConstructors().Single();
                var customArgs = customCtor.GetParameters().Select(p => p.DefaultValue).ToArray();
                customArgs[0] = shapes;
                var customShape = customCtor.Invoke(customArgs);
                customPolygon = customShape.GetType().GetMethod("GetCombinedPolygon")!.Invoke(customShape, [center]);
                customCenter = center;
            }
            else if (group == "tender-valley" && name.StartsWith("Labyrinth") || group == "delubrum" && name == "PhantomHazard"
                || group == "dalriada" && name == "GauntletHazard")
            {
                var shape = group == "dalriada"
                    ? encounter.GetField("square", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!
                    : group == "delubrum"
                    ? encounter.GetField("ArenaChange", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!
                    : ((Array)encounter.GetField("forbiddenShapes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)
                        .GetValue(int.Parse(name["Labyrinth".Length..]))!;
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var parameters = ctor.GetParameters();
                var args = parameters.Select(p => p.DefaultValue).ToArray();
                for (var i = 0; i < 2; ++i)
                {
                    var items = ((System.Collections.IEnumerable)shape.GetType().GetField(i == 0 ? "Shapes1" : "DifferenceShapes",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shape)!).Cast<object>().ToArray();
                    var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, items.Length);
                    for (var j = 0; j < items.Length; ++j) shapes.SetValue(items[j], j);
                    args[i] = shapes;
                }
                args[Array.FindIndex(parameters, p => p.Name == "Offset")] = 0f;
                bounds = ctor.Invoke(args);
            }
            else if (group == "worqor" && name.StartsWith("Wind") || group == "vanguard" && name.EndsWith("Aoe"))
            {
                var shape = encounter.GetField(group == "vanguard" ? fieldName
                    : "ENVC" + name.Substring(4, 2) + (name.EndsWith("Narrow") ? "Inverted" : ""),
                    BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var shapes = shape.GetType().GetField("Shapes1", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shape)!;
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                var items = ((System.Collections.IEnumerable)shapes).Cast<object>().ToArray();
                var shapeArray = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, items.Length);
                for (var i = 0; i < items.Length; ++i) shapeArray.SetValue(items[i], i);
                args[0] = shapeArray;
                bounds = ctor.Invoke(args);
            }
            else if (group == "troia" && name.StartsWith("Scarmiglione") && name != "ScarmiglioneStarting")
            {
                var shapeType = assembly.GetType("BossMod.Shape")!;
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                if (name == "ScarmiglioneLargeBase")
                    args[0] = encounter.GetField("StartingCircle", BindingFlags.Static | BindingFlags.Public)!.GetValue(null);
                else
                {
                    var circle = Array.CreateInstance(shapeType, 1);
                    var center = encounter.GetField("ArenaCenter", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
                    circle.SetValue(Activator.CreateInstance(assembly.GetType("BossMod.Polygon")!,
                        [center, 20f, 64, Activator.CreateInstance(assembly.GetType("BossMod.Angle")!)]), 0);
                    args[0] = circle;
                }
                if (name.StartsWith("ScarmiglioneWall"))
                {
                    var walls = (Array)encounter.GetField("SafeWalls", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
                    var cut = Array.CreateInstance(shapeType, 1);
                    cut.SetValue(walls.GetValue(int.Parse(name["ScarmiglioneWall".Length..])), 0); args[0] = cut;
                }
                bounds = ctor.Invoke(args);
            }
            else if (group == "heroes" && name.StartsWith("Berserker") && name != "Berserker")
            {
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var parameters = ctor.GetParameters();
                var args = parameters.Select(p => p.DefaultValue).ToArray();
                var cross = encounter.GetField("Cross", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
                args[0] = cross;
                if (name == "BerserkerCollapse")
                {
                    var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, 1);
                    shapes.SetValue(Activator.CreateInstance(assembly.GetType("BossMod.Square")!,
                        [encounter.GetField("ArenaCenter", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!, 23f,
                            Activator.CreateInstance(assembly.GetType("BossMod.Angle")!)]), 0);
                    args[0] = shapes; args[1] = cross;
                }
                bounds = ctor.Invoke(args);
            }
            else if (group == "anyder" && name.EndsWith("Danger"))
            {
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                args[0] = encounter.GetField(name == "MorbolBlueDanger" ? "blueBlossom" : "yellowBlossom", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
                args[1] = encounter.GetField("defaultDifference", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
                bounds = ctor.Invoke(args);
            }
            else if (group == "amaurot" && name.StartsWith("Therion"))
            {
                var mask = int.Parse(name[7..]);
                var union = ((System.Collections.IEnumerable)encounter.GetMethod("Union", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!).Cast<object>().ToList();
                var alcoves = ((System.Collections.IEnumerable)encounter.GetField("alcoves", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>().ToArray();
                for (var i = 0; i < 8; ++i) if ((mask & (1 << i)) != 0) union.Remove(alcoves[i]);
                var cutCount = Math.Min(2, System.Numerics.BitOperations.PopCount((uint)mask) / 2);
                var cuts = ((System.Collections.IEnumerable)encounter.GetField("differences", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Cast<object>().Take(cutCount).ToArray();
                Array Shapes(IEnumerable<object> values)
                {
                    var items = values.ToArray(); var array = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, items.Length);
                    for (var i = 0; i < items.Length; ++i) array.SetValue(items[i], i);
                    return array;
                }
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var parameters = ctor.GetParameters();
                var args = parameters.Select(p => p.DefaultValue).ToArray();
                args[0] = Shapes(union); args[1] = Shapes(cuts);
                args[Array.FindIndex(parameters, p => p.Name == "Offset")] = -1f;
                bounds = ctor.Invoke(args);
            }
            else if (group == "mt-gulg" && name == "ObscenitySmall")
            {
                var shapeType = assembly.GetType("BossMod.Shape")!;
                var centerType = assembly.GetType("BossMod.WPos")!;
                var angle = Activator.CreateInstance(assembly.GetType("BossMod.Angle")!)!;
                var center = Activator.CreateInstance(centerType, [-240f, 237f])!;
                var union = Array.CreateInstance(shapeType, 1);
                union.SetValue(Activator.CreateInstance(assembly.GetType("BossMod.Polygon")!, [center, 15f, 64, angle]), 0);
                var difference = Array.CreateInstance(shapeType, 2);
                for (var i = 0; i < 2; ++i)
                {
                    var side = Activator.CreateInstance(centerType, [-240f + (i == 0 ? 15 : -15), 237f])!;
                    difference.SetValue(Activator.CreateInstance(assembly.GetType("BossMod.Rectangle")!, [side, .5f, 20f, angle]), i);
                }
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                args[0] = union; args[1] = difference;
                bounds = ctor.Invoke(args);
            }
            else if (group == "ghimlyt" && name == "Nitro")
            {
                var center = encounter.GetField("ArenaCenter", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
                var circle = Activator.CreateInstance(assembly.GetType("BossMod.Circle")!,
                    [center, 50f])!;
                var union = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, 1);
                union.SetValue(circle, 0);
                var difference = encounter.GetField("Polygon", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                args[0] = union; args[1] = difference;
                bounds = ctor.Invoke(args);
            }
            else if (group == "sirensong" && name == "LoreleiSmall")
            {
                var center = Activator.CreateInstance(assembly.GetType("BossMod.WPos")!, [-44.49977f, 465.00049f])!;
                var polygon = Activator.CreateInstance(assembly.GetType("BossMod.Polygon")!,
                    [center, 15.95688f, 24, Activator.CreateInstance(assembly.GetType("BossMod.Angle")!)])!;
                var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, 1);
                shapes.SetValue(polygon, 0);
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                args[0] = shapes;
                bounds = ctor.Invoke(args);
            }
            else if (group == "xelphatol")
                bounds = XelphatolBounds(assembly, encounter, name);
            else if (group == "origenics" && name.StartsWith("Deceiver"))
            {
                var map = (System.Collections.IDictionary)encounter.GetField("ArenaBoundsMap", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                bounds = map[Convert.ToByte(name["Deceiver".Length..], 16)]!;
            }
            else if (group == "mistwake" && name == "GriffinSmall")
            {
                var large = encounter.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                var center = large.GetType().GetField("Item1")!.GetValue(large)!;
                var polygon = Activator.CreateInstance(assembly.GetType("BossMod.Polygon")!,
                    [center, 20f, 64, Activator.CreateInstance(assembly.GetType("BossMod.Angle")!)])!;
                var shapes = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, 1);
                shapes.SetValue(polygon, 0);
                var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
                var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
                args[0] = shapes;
                bounds = ctor.Invoke(args);
            }
            else if (field == null)
            {
                var tuple = encounter.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                bounds = tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
            }
            else bounds = field.GetValue(null)!;
            dynamic arena = bounds;
            dynamic resolvedPolygon = customPolygon ?? arena.Polygon;
            dynamic resolvedCenter = customCenter ?? arena.Center;
            if (group == "delubrum" && name is "SeekerStarting" or "SeekerDefault" or "GuardStarting" or "GuardDefault")
                output.Append(FormattableString.Invariant($"    internal const float {name}Radius = {(float)arena.Radius:R}f;\n"));
            if (group is "dalriada" or "castrum-lacus")
                output.Append(FormattableString.Invariant($"    internal const float {name}Radius = {(float)arena.Radius:R}f;\n"));
            if (group == "queen" && name == "XInsetHalf") resolvedPolygon = resolvedPolygon.Offset(-.5f);
            if (group == "queen" && name == "XInsetOne") resolvedPolygon = resolvedPolygon.Offset(-1f);
            if (group == "heroes" && name == "BerserkerLanding") resolvedPolygon = resolvedPolygon.Offset(-1f);
            if ((int)resolvedPolygon.Parts.Count > 1 || (int)resolvedPolygon.Parts[0].HoleStarts.Count > 0)
            {
                var exteriors = new List<List<(float X, float Z)>>();
                var holes = new List<List<(float X, float Z)>>();
                foreach (dynamic part in resolvedPolygon.Parts)
                {
                    var points = new List<(float X, float Z)>();
                    foreach (dynamic vertex in part.Vertices) points.Add(((float)vertex.X, (float)vertex.Z));
                    var starts = new List<int> { 0 };
                    foreach (int start in part.HoleStarts) starts.Add(start);
                    starts.Add(points.Count);
                    exteriors.Add(points.GetRange(0, starts[1]));
                    for (var i = 1; i < starts.Count - 1; ++i)
                        holes.Add(points.GetRange(starts[i], starts[i + 1] - starts[i]));
                }
                var triangles = new List<List<(float X, float Z)>>();
                foreach (dynamic triangle in resolvedPolygon.Triangulate())
                    triangles.Add([((float)triangle.A.X, (float)triangle.A.Z), ((float)triangle.B.X, (float)triangle.B.Z),
                        ((float)triangle.C.X, (float)triangle.C.Z)]);
                var position = FormattableString.Invariant($"new({(float)resolvedCenter.X:R}f, y, {(float)resolvedCenter.Z:R}f)");
                var argument = "float y";
                if (group == "delubrum" && name.StartsWith("SeekerBlade"))
                {
                    argument += ", Vector3? drawCenter = null";
                    position = "drawCenter ?? " + position;
                }
                output.Append($"    internal static IGeometryShape {name}({argument}) => new PolygonRegionGeometry({position},\n");
                void AppendPolygons(List<List<(float X, float Z)>> polygons, bool last)
                {
                    output.Append("    [\n");
                    foreach (var polygon in polygons)
                    {
                        output.Append("        [");
                        output.Append(string.Join(", ", polygon.Select(p => FormattableString.Invariant($"new({p.X:R}f, {p.Z:R}f)"))));
                        output.Append("],\n");
                    }
                    output.Append(last ? "    ]);\n\n" : "    ],\n");
                }
                AppendPolygons(exteriors, false); AppendPolygons(holes, false); AppendPolygons(triangles, true);
                continue;
            }
            Check((int)resolvedPolygon.Parts.Count == 1 && (int)resolvedPolygon.Parts[0].HoleStarts.Count == 0,
                "Arena topology requires an explicit multi-part export.");
            if (group == "castrum-lacus")
            {
                // Match ConvertBounds world-coordinate arithmetic at corridor edges.
                output.Append($"    internal static IGeometryShape {name}(float y) => new WorldPolygonGeometry([\n");
                foreach (dynamic vertex in resolvedPolygon.Parts[0].Vertices)
                    output.Append(FormattableString.Invariant($"        new({(float)((float)arena.Center.X + (float)vertex.X):R}f, y, {(float)((float)arena.Center.Z + (float)vertex.Z):R}f),\n"));
                output.Append(FormattableString.Invariant($"    ], new({(float)arena.Center.X:R}f, y, {(float)arena.Center.Z:R}f));\n\n"));
                continue;
            }
            output.Append(FormattableString.Invariant($"    internal static IGeometryShape {name}(float y) => new LocalPolygonGeometry(new({(float)arena.Center.X:R}f, y, {(float)arena.Center.Z:R}f), 0, [\n"));
            foreach (dynamic vertex in resolvedPolygon.Parts[0].Vertices)
                output.Append(FormattableString.Invariant($"        new({-(float)vertex.X:R}f, {-(float)vertex.Z:R}f),\n"));
            output.Append("    ]);\n\n");
        }
        if (group == "amaurot")
        {
            output.Append("    internal static IGeometryShape Therion(int removedMask, float y) => removedMask switch\n    {\n");
            for (var i = 0; i < 256; ++i) output.Append($"        {i} => Therion{i}(y),\n");
            output.Append("        _ => throw new System.ArgumentOutOfRangeException(nameof(removedMask)),\n    };\n");
        }
        if (group == "carnivale")
        {
            var layouts = assembly.GetType("BossMod.Global.MaskedCarnivale.Layouts")!;
            foreach (var layout in new[] { "Layout2Corners", "LayoutBigQuad" })
            {
                dynamic polygon = layouts.GetField(layout + "Blockers", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                output.Append($"    internal static readonly Vector2[][] {layout}SightContours =\n    [\n");
                foreach (dynamic part in polygon.Parts)
                {
                    var points = new List<(float X, float Z)>();
                    foreach (dynamic vertex in part.Vertices) points.Add(((float)vertex.X, (float)vertex.Z));
                    var starts = new List<int> { 0 };
                    foreach (int start in part.HoleStarts) starts.Add(start);
                    starts.Add(points.Count);
                    for (var i = 0; i < starts.Count - 1; ++i)
                    {
                        output.Append("        [");
                        output.Append(string.Join(", ", points.GetRange(starts[i], starts[i + 1] - starts[i])
                            .Select(p => FormattableString.Invariant($"new({p.X:R}f, {p.Z:R}f)"))));
                        output.Append("],\n");
                    }
                }
                output.Append("    ];\n\n");
            }
        }
        output.Append("}\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, output.ToString(), new UTF8Encoding(false));
        if (group == "aetherfont")
        {
            var patterns = new StringBuilder("// Generated from source ForkedFissures map patterns using --aetherfont-arenas.\nusing System.Numerics;\n\nnamespace MasterPlayer.Mechanics.Dungeons;\n\ninternal static class AetherfontFissures\n{\n    internal static readonly (Vector2[] Start, Vector2[] End)[] Patterns =\n    [\n");
            var fissures = assembly.GetType("BossMod.Endwalker.Dungeon.D12Aetherfont.D122Arkas.ForkedFissures")!;
            for (var i = 1; i <= 4; ++i)
            {
                var pair = fissures.GetField($"pattern0x0{i}", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                patterns.Append("        (\n");
                foreach (var field in new[] { "Item1", "Item2" })
                {
                    var points = (Array)pair.GetType().GetField(field)!.GetValue(pair)!;
                    patterns.Append("            [");
                    foreach (var point in points)
                    {
                        var x = (float)point!.GetType().GetField("X")!.GetValue(point)!;
                        var z = (float)point.GetType().GetField("Z")!.GetValue(point)!;
                        patterns.Append(FormattableString.Invariant($"new({x:R}f, {z:R}f), "));
                    }
                    patterns.Append(field == "Item1" ? "],\n" : "]\n");
                }
                patterns.Append("        ),\n");
            }
            patterns.Append("    ];\n}\n");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(destination))!, "AetherfontFissures.cs"), patterns.ToString(), new UTF8Encoding(false));
        }
        Console.WriteLine($"Exported {encounters.Length} exact {group} polygons with subtraction and union applied.");
    }

    private static object XelphatolBounds(Assembly assembly, Type encounter, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        object Field(string field) => encounter.GetField(field, flags)!.GetValue(null)!;
        object Bounds(object union, object difference, bool inward = false)
        {
            var ctor = assembly.GetType("BossMod.ArenaBoundsCustom")!.GetConstructors().Single();
            var args = ctor.GetParameters().Select(p => p.DefaultValue).ToArray();
            Array Shapes(object values)
            {
                var items = ((System.Collections.IEnumerable)values).Cast<object>().ToArray();
                var result = Array.CreateInstance(assembly.GetType("BossMod.Shape")!, items.Length);
                for (var i = 0; i < items.Length; ++i) result.SetValue(items[i], i);
                return result;
            }
            args[0] = Shapes(union); args[1] = Shapes(difference); args[7] = inward;
            return ctor.Invoke(args);
        }
        if (name.StartsWith("Dotoli"))
            return Bounds(Field(name == "DotoliSmall" ? "DefaultBoundsP" : "StartingBoundsP"),
                Field(name == "DotoliRing" ? "DefaultBoundsP" : "Difference"), name != "DotoliRing");
        if (name.StartsWith("SiegeHazard"))
        {
            var shape = Field(name.EndsWith("First") ? "poly1" : "poly2");
            var shapeFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            return Bounds(shape.GetType().GetField("Shapes1", shapeFlags)!.GetValue(shape)!,
                shape.GetType().GetField("DifferenceShapes", shapeFlags)!.GetValue(shape)!);
        }
        var method = name switch
        {
            "HarpeiaFirst" or "SiegeFirst" or "SwiftFirst" => "BuildArena1",
            "HarpeiaSecond" or "SiegeSecond" or "SwiftSecond" => "BuildArena2",
            "SwiftThird" => "BuildArena3",
            _ => "BuildArena",
        };
        var tuple = encounter.GetMethod(method, flags)!.Invoke(null, null)!;
        return tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

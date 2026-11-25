using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NativeTreasureCatalog
{
    internal const uint VAULT_ONEIRON = 1279;
    internal const uint GILDED_SENTRY = 0x48B0;
    internal const uint GOLDY_CAT = 0x48B7;
    internal const uint VAULTKEEPER = 0x48B8;
    internal const uint VAULT_GARGANTUAN = 0x4898;
    internal const uint VAULT_BAT = 0x489E;
    internal const uint VAULT_FLUID_SOUL = 0x4895;
    internal const uint VAULT_WIND_SPIRIT = 0x489B;
    internal const uint GWYDDNEU = 0x48A4;
    internal const uint GREAT_GIMME_CAT = 0x48AC;
    internal const uint VAULT_ROADRIPPER = 0x48A1;
    internal const uint FASTITOCALON = 0x48A8;
    internal const uint PAEONIA = 0x48AE;
    internal const uint SPIRIT_OF_THUNDER = 0x48B5;
    internal const uint OLD_BITTER_EYE = 0x48AA;
    internal const uint MOBILE_FORTRESS = 0x48B2;
    internal const uint SILVER_SACK = 0x1EBE48;
    internal const uint GOLD_SACK = 0x1EBE47;
    internal const uint HEXAPOD = 0x48B3;
    internal const uint SHIFTING_ALTARS = 794;
    internal const uint GOLD_WHISKER = 0x2541;
    internal const uint ALTAR_MANDRAGORA = 0x2542;
    internal const uint HATI = 0x2538;
    internal const uint ALTAR_SKATENE = 0x2535;
    internal const uint ALTAR_BEAST = 0x2536;
    internal const uint ALTAR_ARACHNE = 0x253B;
    internal const uint THE_OLDER_ONE = 0x253F;
    internal const uint ALTAR_CHIMERA = 0x2539;
    internal const uint ALTAR_DIRESAUR = 0x253A;
    internal const uint ALTAR_TOTEM = 0x2534;
    internal const uint ALTAR_DULLAHAN = 0x2533;
    internal const uint THE_WINGED = 0x253D;
    internal const uint ALTAR_AIRAVATA = 0x2543;
    internal const uint ALTAR_KELPIE = 0x2537;
    internal const uint SHIFTING_GYMNASION = 1123;
    internal const uint GYMNASIOU_MANDRAGORAS = 0x3D41;
    internal const uint GYMNASIOU_LEON = 0x3D27;
    internal const uint LAMPAS_CHRYSINE = 0x3D40;
    internal const uint GYMNASIOU_TIGRIS = 0x3D29;
    internal const uint GYMNASIOU_SATYROS = 0x3D2D;
    internal const uint GYMNASIOU_TRITON = 0x3D30;
    internal const uint GYMNASIOU_SPHINX = 0x3D3B;
    internal const uint GYMNASIOU_MEGAKANTHA = 0x3D33;
    internal const uint LYSSA_CHRYSINE = 0x3D43;
    internal const uint GYMNASIOU_PITHEKOS = 0x3D2B;
    internal const uint GYMNASIOU_STYPHNOLOBION = 0x3D37;
    internal const uint GYMNASIOU_MEGANEREIS = 0x3D39;
    internal const uint GYMNASIOU_ACHELOIOS = 0x3D3E;
    internal const uint HIPPOMENES = 0x3D49;
    internal const uint NARKISSOS = 0x3D48;
    internal const uint SHIFTING_OUBLIETTES = 924;
    internal const uint SECRET_UNDINE = 0x3011;
    internal const uint SECRET_SWALLOW = 0x302B;
    internal const uint SECRET_PEGASUS = 0x3016;
    internal const uint GREEDY_PIXIE = 0x3018;
    internal const uint SECRET_KORRIGAN = 0x3022;
    internal const uint SECRET_SERPENT = 0x3025;
    internal const uint FUATH_TROUBLEMAKER = 0x302F;
    internal const uint SECRET_BASKET = 0x302D;
    internal const uint SECRET_PORXIE = 0x3014;
    internal const uint SECRET_DJINN = 0x300F;
    internal const uint SECRET_WORM = 0x3029;
    internal const uint SECRET_KEEPER = 0x3023;
    internal const uint DAEN_ULTROS = 0x3030;
    internal const uint DAEN_ECHIDNA = 0x3032;
    internal const uint SECRET_CLADOSELACHE = 0x3027;
    internal const uint DAEN_TYPHON = 0x301A;
    internal const uint DUNGEONS_LYHE_GHIAH = 879;
    internal const uint ALICHINO = 0x2BA7;
    internal const uint GOLIATH = 0x2BA5;
    internal const uint EXCITATRON = 1000;
    internal const uint LUCKY_FACE = 0x377F;
    internal const uint HIDDEN_CANALS = 725;
    internal const uint AIRAVATA = 0x1FCC;
    internal const uint LOST_CANALS = 712;
    internal const uint CANAL_ICEBEAST = 0x1F15;
    internal const uint CENOTE_JA_JA_GURAL = 1209;
    internal const uint BULL_APOLLYON = 0x4305;
    internal const uint GOLDEN_MOLTER = 0x4306;

    internal static bool IsTerritory(uint territory) => territory is VAULT_ONEIRON or SHIFTING_ALTARS or SHIFTING_GYMNASION or SHIFTING_OUBLIETTES or CENOTE_JA_JA_GURAL or DUNGEONS_LYHE_GHIAH or EXCITATRON or HIDDEN_CANALS or LOST_CANALS;
    internal static bool SingleTargetAction(uint action) => action is 43725 or 32297 or 32200 or 32290 or 21716 or 38261 or 32206 or 32218 or 32228 or 32265 or 21699 or 32238 or 21734 or 21721 or 17880 or 17873 or 27981 or 9893 or 21710 or 32306 or 32211 or 32245 or 32255 or 21743 or 21757 or 21703 or 32275 or 32341 or 32330 or 38274 or 43716;
    internal static bool RaidwideAction(uint action) => action is 43723 or 21662 or 21682 or 38262 or 32266 or 13343 or 13389 or 13283 or 43616 or 43633 or 43608 or 43626 or 17882 or 17876 or 27982 or 13314 or 13380 or 13440 or 21654 or 32246 or 32263 or 43648 or 43654 or 43694 or 43639 or 43672 or 32276 or 32356 or 32331 or 38275 or 43715 or 43786 or 43677 or 43744 or 43770;
    internal static bool CastBaitAction(uint action) => action is 43630 or 43607 or 43623 or 32251 or 43650 or 43698 or 43640 or 43668 or 21709 or 38281 or 43784 or 43682 or 43748 or 43768;
    internal static bool FinishBaitAction(uint action) => action is 43650 or 43698 or 43640 or 43668 or 21709 or 38281 or 43784 or 43768;
    internal static bool CircleBaitAction(uint action) => action is 43650 or 43698 or 43668 or 43784 or 43768;
    internal static bool VaultGroupAction(uint action) => action is 43645 or 43687 or 43688 or >= 43659 and <= 43662 or >= 22797 and <= 22800
        or 43706 or 43707 or 43773 or 43774 or >= 43778 and <= 43781;
    internal static uint CachedBossOid(uint primary) => primary switch
    {
        VAULT_GARGANTUAN => 0x489A,
        VAULT_BAT => 0x48A0,
        VAULT_FLUID_SOUL => 0x4897,
        VAULT_WIND_SPIRIT => 0x489D,
        VAULT_ROADRIPPER => 0x48A3,
        _ => 0,
    };
    internal static bool DelayableAction(uint action) => action is 13364 or 13367 or 13311 or 13342 or 13385 or 21673 or 13217 or 43615 or 13371 or 13438 or 43638;
    internal static bool GazeAction(uint action) => action is 32207 or 13263 or 13784 or 21674 or 9557 or 21761 or 21762 or 21760 or 32332 or 43701 or 43702;
    internal static bool OdiousEffect(uint action) => action is 32242 or 33015 or 33016;
    internal static bool HasMatangaCleave(uint primary) => primary is ALTAR_SKATENE or ALTAR_BEAST or ALTAR_ARACHNE or THE_OLDER_ONE
        or ALTAR_CHIMERA or ALTAR_DIRESAUR or ALTAR_TOTEM or ALTAR_DULLAHAN or THE_WINGED or ALTAR_AIRAVATA or ALTAR_KELPIE;
    internal static uint CleaveSourceOid(uint primary) => primary switch
    {
        AIRAVATA => 0x2064,
        CANAL_ICEBEAST => 0x1EBF,
        SECRET_KEEPER => SECRET_KEEPER,
        _ => HasMatangaCleave(primary) ? 0x2545u : 0,
    };
    internal static uint VoidzoneAction(uint primary) => primary switch
    {
        GYMNASIOU_TRITON => 32229,
        ALTAR_CHIMERA => 13396,
        ALTAR_DIRESAUR => 13219,
        ALTAR_TOTEM => 13452,
        SECRET_KEEPER => 21679,
        _ => 0,
    };

    internal static bool Owns(uint oid, uint territory) => territory switch
    {
        VAULT_ONEIRON => oid is GILDED_SENTRY or VAULT_GARGANTUAN or VAULT_BAT or VAULT_FLUID_SOUL or VAULT_WIND_SPIRIT
            or GWYDDNEU or GREAT_GIMME_CAT or VAULT_ROADRIPPER or FASTITOCALON or PAEONIA or SPIRIT_OF_THUNDER or OLD_BITTER_EYE or MOBILE_FORTRESS or SILVER_SACK or HEXAPOD,
        SHIFTING_ALTARS => oid is GOLD_WHISKER or ALTAR_MANDRAGORA or HATI
            or ALTAR_SKATENE or ALTAR_BEAST or ALTAR_ARACHNE or THE_OLDER_ONE or ALTAR_CHIMERA or ALTAR_DIRESAUR or ALTAR_TOTEM or ALTAR_DULLAHAN or THE_WINGED or ALTAR_AIRAVATA or ALTAR_KELPIE,
        SHIFTING_GYMNASION => oid is GYMNASIOU_MANDRAGORAS or GYMNASIOU_LEON or LAMPAS_CHRYSINE
            or GYMNASIOU_TIGRIS or GYMNASIOU_SATYROS or GYMNASIOU_TRITON or GYMNASIOU_SPHINX or GYMNASIOU_MEGAKANTHA or LYSSA_CHRYSINE
            or GYMNASIOU_PITHEKOS or GYMNASIOU_STYPHNOLOBION or GYMNASIOU_MEGANEREIS or GYMNASIOU_ACHELOIOS or HIPPOMENES or NARKISSOS,
        SHIFTING_OUBLIETTES => oid is SECRET_UNDINE or SECRET_SWALLOW or SECRET_PEGASUS or GREEDY_PIXIE or SECRET_KORRIGAN or SECRET_SERPENT or FUATH_TROUBLEMAKER or SECRET_BASKET or SECRET_PORXIE or SECRET_DJINN or SECRET_WORM or SECRET_KEEPER or DAEN_ULTROS or DAEN_ECHIDNA or SECRET_CLADOSELACHE or DAEN_TYPHON,
        CENOTE_JA_JA_GURAL => oid is BULL_APOLLYON or GOLDEN_MOLTER || NativeCenoteRoomCatalog.IsRoom(oid),
        DUNGEONS_LYHE_GHIAH => oid is ALICHINO or GOLIATH,
        EXCITATRON => oid == LUCKY_FACE,
        HIDDEN_CANALS => oid == AIRAVATA,
        LOST_CANALS => oid == CANAL_ICEBEAST,
        _ => false,
    };

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        EXCITATRON =>
        [
            new(LUCKY_FACE, "Lucky Face", p => NativeTreasureArenas.LuckyFace(p.Y),
            [
                new(27990, 0, "Left in the Dark", new FanTemplate(20, MathF.PI)),
                new(27994, 0, "Left in the Dark", new FanTemplate(20, MathF.PI)),
                new(27992, 0, "Right in the Dark", new FanTemplate(20, MathF.PI)),
                new(27996, 0, "Right in the Dark", new FanTemplate(20, MathF.PI)),
                new(27998, 0, "Quake in Your Boots", new CircleTemplate(10)),
                new(28190, 0, "Quake Me Away", new CircleTemplate(10)),
                new(28189, 0, "Quake in Your Boots", new DonutTemplate(10, 20)),
                new(28000, 0, "Quake Me Away", new DonutTemplate(10, 20)),
                new(28001, 0, "Heart on Fire II", new CircleTemplate(6)),
                new(27981, 0, "Heart on Fire IV", null, "死刑"),
                new(28002, 0, "Heart on Fire III", null),
                new(27982, 0, "Tempers Flare", null, "全屏伤害"),
                .. MandragoraCasts(),
            ], []),
        ],
        HIDDEN_CANALS =>
        [
            new(AIRAVATA, "Airavata", p => NativeTreasureArenas.Canals(p.Y),
            [
                new(9891, 0, "Hurl", new CircleTemplate(6)),
                new(5352, 0, "Hurl", new CircleTemplate(6)),
                new(9892, 0, "Spin", new FanTemplate(20, 2 * MathF.PI / 3)),
                new(9894, 0, "Barbarous Scream", new CircleTemplate(14)),
                new(9893, 0, "Buffet", null, "死刑"),
                new(8598, 0, "Raucous Scritch", new FanTemplate(8.42f, 2 * MathF.PI / 3)),
                .. MandragoraCasts(),
            ], []),
        ],
        LOST_CANALS =>
        [
            new(CANAL_ICEBEAST, "Canal Icebeast", p => NativeTreasureArenas.Canals(p.Y),
            [
                new(9557, 0, "Eyeshine", null),
                new(9558, 0, "Absolute Zero", new FanTemplate(45.5f, MathF.PI / 2)),
                new(4486, 0, "Freezeover", new CircleTemplate(6)),
                new(4487, 0, "Plain Pound", new CircleTemplate(4.56f)),
                .. MatangaCasts(),
            ], []),
        ],
        DUNGEONS_LYHE_GHIAH =>
        [
            new(ALICHINO, "Alichino", LyheGhiahRoom5Arena,
            [
                new(17885, 0, "Discordant Echo", new CircleTemplate(6)),
                new(17883, 0, "Heat Gaze", new DonutTemplate(5, 10)),
                new(17887, 0, "Heat Gaze", new DonutTemplate(5, 10)),
                new(17881, 0, "Heat Gaze", new FanTemplate(43.3f, 2 * MathF.PI / 3)),
                new(17886, 0, "Heat Gaze", new FanTemplate(19.9f, MathF.PI / 3)),
                new(17882, 0, "Slapstick", null, "全屏伤害"),
                new(17880, 0, "Knockout", null, "死刑"),
                .. LyheGhiahRewardCasts(),
            ], []),
            new(GOLIATH, "Goliath", LyheGhiahRoom5Arena,
            [
                new(17874, 0, "Wellbore", new CircleTemplate(15)),
                new(17879, 0, "Compress", new DungeonCrossTemplate(100, 3.5f)),
                new(17878, 0, "Compress", RayRectangleTemplate.Symmetric(7, 102.1f)),
                new(17877, 0, "Accelerate", null),
                new(17876, 0, "Incinerate", null, "全屏伤害"),
                new(17873, 0, "Mechanical Blow", null, "死刑"),
                .. LyheGhiahRewardCasts(),
            ], []),
        ],
        VAULT_ONEIRON =>
    [
        new(SILVER_SACK, "Vault Yan", VaultArena,
        [
            new(44269, 0, "Rush", RayRectangleTemplate.Symmetric(8, 0)),
        ], []),
        new(HEXAPOD, "Hexapod", VaultArena,
        [
            new(43755, 0, "Shock Circle", new CircleTemplate(10)),
            new(43756, 0, "Shock Rect", RayRectangleTemplate.Symmetric(6, 40)),
            new(43769, 0, "Diffusion Ray", new FanTemplate(30, 2 * MathF.PI / 3)),
            new(43770, 0, "Electroblast", null, "全屏伤害"),
            new(43759, 0, "Electric Excess", new CircleTemplate(5)),
            new(43768, 0, "Anti-hero Artillery", null),
            new(43754, 0, "Pole Shift Minus", null),
            new(43752, 0, "Pole Shift Plus", null),
            .. VaultRewardCasts(),
        ], []),
        new(OLD_BITTER_EYE, "Old Bitter Eye", VaultArena,
        [
            new(43675, 0, "Ten-stone Swing", new CircleTemplate(11)),
            new(43678, 0, "Hundred-stone Swipe", new FanTemplate(40, MathF.PI / 3)),
            new(43683, 0, "Lumbering Leap", new CircleTemplate(10)),
            new(43680, 0, "Eye of the Thunderstorm", null),
            new(43682, 0, "Hundred-stone Smash", null),
            new(43681, 0, "Hypercharged Glower", RayRectangleTemplate.Symmetric(7, 20)),
            new(43677, 0, "Thousand-stone Swing", null, "全屏伤害"),
            new(43685, 0, "Glower", RayRectangleTemplate.Symmetric(7, 19)),
            new(43684, 0, "Predatorial Instinct", null),
            new(43676, 0, "Hundred-stone Swing", null),
            .. VaultRewardCasts(),
        ], []),
        new(MOBILE_FORTRESS, "Mobile Fortress", VaultArena,
        [
            new(43732, 0, "Quiet World", RayRectangleTemplate.Symmetric(40, 10)),
            new(43744, 0, "Surface Missile", null, "全屏伤害"),
            new(43746, 0, "Anti-personnel Missile", new CircleTemplate(6)),
            new(43734, 0, "Carpet Bomb I", RayRectangleTemplate.Symmetric(40, 10)),
            new(43735, 0, "Carpet Bomb II", RayRectangleTemplate.Symmetric(40, 10)),
            new(43736, 0, "Carpet Bomb III", RayRectangleTemplate.Symmetric(40, 10)),
            new(43737, 0, "Carpet Bomb IV", RayRectangleTemplate.Symmetric(40, 10)),
            new(43742, 0, "Guided Missile", null),
            new(43748, 0, "Elimination", null),
            new(43739, 0, "Systematic Bombardment I", RayRectangleTemplate.Symmetric(10, 10)),
            new(43740, 0, "Systematic Bombardment II", RayRectangleTemplate.Symmetric(10, 10)),
            new(43741, 0, "Systematic Bombardment III", RayRectangleTemplate.Symmetric(10, 10)),
            .. VaultRewardCasts(),
        ], []),
        new(PAEONIA, "Paeonia", VaultArena,
        [
            new(43701, 0, "Hypnotize", null),
            new(43702, 0, "Hypnotize", null),
            new(43714, 0, "Lightning Storm", null),
            new(43706, 0, "Lightning Crossing", new FanTemplate(40, MathF.PI / 4)),
            new(43707, 0, "Lightning Crossing", new FanTemplate(40, MathF.PI / 4)),
            new(43715, 0, "Mandrastorm", null, "全屏伤害"),
            new(43716, 0, "Ram", null, "死刑"),
            new(43710, 0, "Epicenter Shock I", null),
            new(43712, 0, "Levinroot Ring II", null),
            new(43713, 0, "Levinroot Ring I", null),
            new(43709, 0, "Epicenter Shock II", null),
            new(43704, 0, "Lightning Bolt", new CircleTemplate(6)),
            .. GymnasticCasts(),
        ], []),
        new(SPIRIT_OF_THUNDER, "Spirit of Thunder", VaultArena,
        [
            new(43773, 0, "Electray", RayRectangleTemplate.Symmetric(10, 20)),
            new(43774, 0, "Electray", RayRectangleTemplate.Symmetric(10, 20)),
            new(43778, 0, "High Voltage Electray", RayRectangleTemplate.Symmetric(20, 20)),
            new(43779, 0, "High Voltage Electray", RayRectangleTemplate.Symmetric(20, 20)),
            new(43780, 0, "High Voltage Electray", RayRectangleTemplate.Symmetric(20, 20)),
            new(43781, 0, "High Voltage Electray", RayRectangleTemplate.Symmetric(20, 20)),
            new(43776, 0, "Tidal Thunder", new FanTemplate(30, MathF.PI / 6)),
            new(43788, 0, "Power Line", null),
            new(43786, 0, "Electrowave", null, "全屏伤害"),
            new(43782, 0, "Electric Vortex", null),
            new(43784, 0, "Hypercondensed Thunder", null),
            .. VaultRewardCasts(),
        ], []),
        new(GWYDDNEU, "Gwyddneu", VaultArena,
        [
            new(43645, 0, "Violet Bolt", new FanTemplate(70, MathF.PI / 4)),
            new(43648, 0, "Crackling Howl", null, "全屏伤害"),
            new(43654, 0, "Fulgurous Howl", null, "全屏伤害"),
            new(43650, 0, "Thundercrack", null),
            new(43652, 0, "Lightning Bolt", new CircleTemplate(6)),
            new(43656, 0, "Electrify", null),
            .. VaultRewardCasts(),
        ], []),
        new(GREAT_GIMME_CAT, "Great Gimme Cat", VaultArena,
        [
            new(43687, 0, "Preening Prism", TreasurePrismShape()),
            new(43688, 0, "Preening Prism", TreasurePrismShape()),
            new(43692, 0, "Glitterbox", new CircleTemplate(5)),
            new(43696, 0, "Glitterbox Spread", null),
            new(43694, 0, "Sparkling Intensifies", null, "全屏伤害"),
            new(43698, 0, "Tormenting Twinkle", null),
            new(43690, 0, "Basking Beam", null),
        ], []),
        new(VAULT_ROADRIPPER, "Vault Roadripper", VaultArena,
        [
            new(43635, 0, "Homing Shot", new CircleTemplate(6)),
            new(43636, 0, "Thrown Flames", new CircleTemplate(8)),
            new(43637, 0, "Incendiary Circle", new DonutTemplate(3, 12)),
            new(43640, 0, "Run Amok", null),
            new(43638, 0, "Wheel", null),
            new(43641, 0, "Wheeling Shot", new FanTemplate(40, MathF.PI)),
            new(43639, 0, "Electroflame", null, "全屏伤害"),
            .. VaultRewardCasts(),
        ], []),
        new(FASTITOCALON, "Fastitocalon", VaultArena,
        [
            new(43659, 0, "Uplift Sequence", new CircleTemplate(6)),
            new(43660, 0, "Uplift Sequence", new CircleTemplate(6)),
            new(43661, 0, "Uplift Sequence", new CircleTemplate(6)),
            new(43662, 0, "Uplift Sequence", new CircleTemplate(6)),
            new(43664, 0, "Tremblor I", null),
            new(43665, 0, "Tremblor II", null),
            new(43666, 0, "Tremblor III", null),
            new(43668, 0, "Uplift", null),
            new(43670, 0, "Earthshake", new CircleTemplate(6)),
            new(43672, 0, "Canyon", null, "全屏伤害"),
            .. VaultRewardCasts(),
        ], []),
        new(GILDED_SENTRY, "Gilded Sentry",
            p => new RectangleGeometry(new(100, p.Y, 100), 0, 39, 39),
        [
            new(43721, 0, "Alexandrian Thunder", new CircleTemplate(6)),
            new(43719, 0, "Cross Lightning", new DungeonCrossTemplate(50, 5)),
            new(43728, 0, "Electray", RayRectangleTemplate.Symmetric(4, 75)),
            new(43725, 0, "Skewering Lance", null, "死刑"),
            new(43723, 0, "Alexandrian Thunder III", null, "全屏伤害"),
            new(43727, 0, "Thunderlance", RayRectangleTemplate.Symmetric(3, 20)),
            new(43726, 0, "Lance Swing", new CircleTemplate(8)),
        ], []),
        new(VAULT_GARGANTUAN, "Vault Gargantuan",
            p => new RectangleGeometry(new(100, p.Y, 100), 0, 39, 39),
        [
            new(43612, 0, "Molten Silk", new FanTemplate(9, 3 * MathF.PI / 2)),
            new(43613, 0, "Molten Silk", new FanTemplate(9, 3 * MathF.PI / 2)),
            new(43614, 0, "Flying Press", new CircleTemplate(6)),
            new(43618, 0, "Scoop", new FanTemplate(15, 2 * MathF.PI / 3)),
            new(43619, 0, "Spin", new CircleTemplate(11)),
            new(43615, 0, "Mash", null),
            new(43616, 0, "Brushfire", null, "全屏伤害"),
            new(43727, 0, "Thunderlance", RayRectangleTemplate.Symmetric(3, 20)),
            new(43726, 0, "Lance Swing", new CircleTemplate(8)),
            .. GymnasticCasts(),
        ], []),
        new(VAULT_BAT, "Vault Bat", VaultArena,
        [
            new(43630, 0, "Phantom Light", null),
            new(43627, 0, "Fast Boulder", new CircleTemplate(3)),
            new(43632, 0, "Chilling Draft", new FanTemplate(40, MathF.PI / 4)),
            new(43628, 0, "Acrocatics", new CircleTemplate(6)),
            new(43631, 0, "Cold Spot", new CircleTemplate(6)),
            new(43629, 0, "Catchoo", RayRectangleTemplate.Symmetric(8, 40)),
            new(43633, 0, "Paranormal Activity", null, "全屏伤害"),
            .. VaultRewardCasts(),
        ], []),
        new(VAULT_FLUID_SOUL, "Vault Fluid Soul", VaultArena,
        [
            new(43607, 0, "Fluid Swing", null),
            new(43602, 0, "Batter", new CircleTemplate(6)),
            new(43603, 0, "Twisted Pool", new DonutTemplate(6, 40)),
            new(43610, 0, "Brine Bomb", new CircleTemplate(5)),
            new(43606, 0, "Water III", new CircleTemplate(8)),
            new(43605, 0, "Flood", new CircleTemplate(8)),
            new(43609, 0, "Protean Wave", new FanTemplate(39, MathF.PI / 6)),
            new(43608, 0, "Splash", null, "全屏伤害"),
            .. VaultRewardCasts(),
        ], []),
        new(VAULT_WIND_SPIRIT, "Vault Wind Spirit", VaultArena,
        [
            new(43620, 0, "Leafcutter", RayRectangleTemplate.Symmetric(4, 14)),
            new(43621, 0, "Entangle", new CircleTemplate(4)),
            new(43622, 0, "Stump", new CircleTemplate(6)),
            new(43624, 0, "Gust", new CircleTemplate(6)),
            new(43625, 0, "Whipwind", RayRectangleTemplate.Symmetric(40, 60)),
            new(43623, 0, "Changeless Winds", null),
            new(43626, 0, "Whirling Gaol", null, "全屏伤害"),
            .. VaultRewardCasts(),
        ], []),
    ],
        SHIFTING_ALTARS =>
        [
            new(ALTAR_AIRAVATA, "Altar Airavata", AltarArena,
            [
                new(13372, 0, "Hurl", new CircleTemplate(6)),
                new(13373, 0, "Spin", new FanTemplate(30, 2 * MathF.PI / 3)),
                new(13375, 0, "Barbarous Scream", new CircleTemplate(14)),
                new(13371, 0, "Huff", null),
                new(13374, 0, "Buffet", null),
                .. MatangaCasts(),
            ], []),
            new(ALTAR_KELPIE, "Altar Kelpie", AltarArena,
            [
                new(13439, 0, "Innocence", new CircleTemplate(5)),
                new(13442, 0, "Hydro Push", RayRectangleTemplate.Symmetric(44, 49.4f)),
                new(13438, 0, "Torpedo", null),
                new(13440, 0, "Rising Seas", null, "全屏伤害"),
                // Geometry is created with the Hydrosphere; this rule only observes its real ending.
                new(13443, 0, "Bloody Puddle", null),
                .. MatangaCasts(),
                .. MandragoraCasts(),
            ], []),
            new(ALTAR_DULLAHAN, "Altar Dullahan", AltarArena,
            [
                new(13316, 0, "Iron Justice", new FanTemplate(11.8f, 2 * MathF.PI / 3)),
                new(13477, 0, "Cloudcover", new CircleTemplate(6)),
                new(13644, 0, "Terror Eye", new CircleTemplate(6)),
                new(13315, 0, "Villainous Rebuke", null),
                new(13314, 0, "Stygian Release", null, "全屏伤害"),
                .. MatangaCasts(),
                .. MandragoraCasts(),
            ], []),
            new(THE_WINGED, "The Winged", AltarArena,
            [
                new(13376, 0, "Filoplumes", RayRectangleTemplate.Symmetric(4, 11.36f)),
                new(13377, 0, "Wingbeat", new FanTemplate(43.36f, MathF.PI / 3)),
                new(13379, 0, "Feather Squall", new CircleTemplate(6)),
                new(13381, 0, "Pinion", RayRectangleTemplate.Symmetric(3, 40.5f)),
                new(13380, 0, "Sideslip", null, "全屏伤害"),
                .. MatangaCasts(),
                .. MandragoraCasts(),
            ], []),
            new(GOLD_WHISKER, "The Great Gold Whisker", AltarArena,
            [
                new(13364, 0, "Triple Trident", null),
                new(13366, 0, "Fish Out of Water", null, "Spawns adds"),
                new(13365, 0, "Tingle", new CircleTemplate(12.4f)),
            ], []),
            new(ALTAR_MANDRAGORA, "Altar Mandragora", AltarArena,
            [
                new(13367, 0, "Optical Intrusion", null),
                new(13370, 0, "Saibai Mandragora", null, "Calls adds"),
                new(13369, 0, "Leaf Dagger", new CircleTemplate(3)),
                new(13368, 0, "Hypnotize", new FanTemplate(22.85f, MathF.PI / 2)),
                .. MandragoraCasts(),
            ], []),
            new(HATI, "Hati", AltarArena,
            [
                new(13360, 0, "Polar Roar", new DonutTemplate(9, 40)),
                new(13363, 0, "Hellstorm", new CircleTemplate(10)),
                new(13741, 0, "Netherwind", RayRectangleTemplate.Symmetric(4, 18)),
                new(13362, 0, "Glassy Nova", RayRectangleTemplate.Symmetric(8, 45.4f)),
                new(13361, 0, "Brain Freeze", new CircleTemplate(15.4f)),
                .. MandragoraCasts(),
            ], []),
            new(ALTAR_SKATENE, "Altar Skatene", AltarArena,
            [
                new(13310, 0, "Chirp", new CircleTemplate(12.48f)),
                new(13309, 0, "Tornado", new CircleTemplate(6)),
                new(13312, 0, "Void Call", null, "Calls adds"),
                new(13311, 0, "Reckless Abandon", null),
                .. MatangaCasts(),
                .. MandragoraCasts(),
            ], []),
            new(ALTAR_BEAST, "Altar Beast", AltarArena,
            [
                new(13259, 0, "Rusting Claw", new FanTemplate(12.6f, 2 * MathF.PI / 3)),
                new(13261, 0, "Tail Drive", new FanTemplate(34.6f, 2 * MathF.PI / 3)),
                new(13262, 0, "The Spin", new CircleTemplate(15)),
                new(13260, 0, "Words of Woe", RayRectangleTemplate.Symmetric(6, 49.6f)),
                new(13740, 0, "Vengeful Soul", new CircleTemplate(6)),
                new(13263, 0, "Eye of the Fire", null),
                .. MatangaCasts(),
                .. MandragoraCasts(),
            ], []),
            new(ALTAR_ARACHNE, "Altar Arachne", AltarArena,
            [
                new(13342, 0, "Dark Spike", null),
                new(13784, 0, "Frond Affeared", null),
                new(13455, 0, "Silken Spray", new FanTemplate(24, MathF.PI / 3)),
                new(13343, 0, "Implosion", null, "全屏伤害"),
                new(13346, 0, "Earthquake", new CircleTemplate(10.5f)),
                new(13345, 0, "Earthquake", new DonutTemplate(10, 20)),
                new(13344, 0, "Earthquake", new DonutTemplate(20, 30)),
                .. MatangaCasts(),
            ], []),
            new(THE_OLDER_ONE, "The Older One", AltarArena,
            [
                new(13386, 0, "Mystic Light", new FanTemplate(45.06f, MathF.PI / 3)),
                new(13388, 0, "Mystic Flame", new CircleTemplate(7)),
                new(13390, 0, "Mystic Heat", RayRectangleTemplate.Symmetric(3, 41.72f)),
                new(13389, 0, "Mystic Levin", null, "全屏伤害"),
                new(13385, 0, "Mystic Flash", null),
                new(13391, 0, "Self Detonate", new CircleTemplate(10.72f)),
                .. MatangaCasts(),
                .. MandragoraCasts(),
            ], []),
            new(ALTAR_CHIMERA, "Altar Chimera", AltarArena,
            [
                new(13393, 0, "The Scorpion's Sting", new FanTemplate(11.92f, MathF.PI / 2)),
                new(13394, 0, "The Ram's Voice", new CircleTemplate(9.92f)),
                new(13395, 0, "The Dragon's Voice", new DonutTemplate(8, 30)),
                new(13392, 0, "The Lion's Breath", new FanTemplate(11.92f, 2 * MathF.PI / 3)),
                new(13742, 0, "Languorous Gaze", new FanTemplate(8.07f, MathF.PI / 2)),
                new(13396, 0, "The Ram's Keeper", null),
                .. MatangaCasts(),
            ], []),
            new(ALTAR_DIRESAUR, "Altar Diresaur", AltarArena,
            [
                new(13217, 0, "Deadly Hold", null),
                new(13218, 0, "Heat Breath", new FanTemplate(14.6f, MathF.PI / 2)),
                new(13220, 0, "Tail Smash", new FanTemplate(26.6f, MathF.PI / 2)),
                new(13283, 0, "Raging Inferno", null, "全屏伤害"),
                new(13835, 0, "Comet", new CircleTemplate(4)),
                new(13743, 0, "Hard Stomp", new CircleTemplate(10)),
                new(13219, 0, "Fireball", null),
                .. MatangaCasts(),
            ], []),
            new(ALTAR_TOTEM, "Altar Totem", AltarArena,
            [
                new(13451, 0, "Flurry of Rage", new FanTemplate(13.06f, 2 * MathF.PI / 3)),
                new(13454, 0, "Wave of Malice", new CircleTemplate(5)),
                new(13453, 0, "Whorl of Frenzy", new CircleTemplate(11.06f)),
                new(13739, 0, "The Warden's Verdict", RayRectangleTemplate.Symmetric(4, 45.06f)),
                new(13452, 0, "Flames of Fury", null),
                .. MatangaCasts(),
            ], []),
        ],
        SHIFTING_GYMNASION =>
        [
            new(NARKISSOS, "Narkissos", AltarArena,
            [
                new(32336, 0, "Sap Shower", new CircleTemplate(8)),
                new(32330, 0, "Lash", null, "死刑"),
                new(32332, 0, "Fetching Fulgence", null),
                new(32333, 0, "Potent Perfume", new CircleTemplate(8)),
                new(32339, 0, "Extensible Tendrils", new DungeonCrossTemplate(25, 3)),
                new(32338, 0, "Putrid Breath", new FanTemplate(25, MathF.PI / 2)),
                new(32340, 0, "Rock Hard", null),
                new(32331, 0, "Beguiling Gas", null, "全屏伤害"),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
            ], []),
            new(GYMNASIOU_ACHELOIOS, "Gymnasiou Acheloios", AltarArena,
            [
                new(32284, 0, "Double Hammer LR", null),
                new(32281, 0, "Double Hammer RL", null),
                new(32859, 0, "Double Hammer First", null),
                new(32860, 0, "Right Hammer", null),
                new(32861, 0, "Left Hammer", null),
                new(32280, 0, "Quadruple Hammer CCW", null),
                new(32283, 0, "Quadruple Hammer CW", null),
                new(32858, 0, "Quadruple Hammer First", null),
                new(32279, 0, "Tail Swing", new CircleTemplate(13)),
                new(32286, 0, "Critical Bite", new FanTemplate(10, 2 * MathF.PI / 3)),
                new(32275, 0, "Deadly Hold", null, "死刑"),
                new(32278, 0, "Earthbreak", new CircleTemplate(5)),
                new(32276, 0, "Volcanic Howl", null, "全屏伤害"),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(HIPPOMENES, "Hippomenes", AltarArena,
            [
                new(33164, 0, "Charge Blaster Front Rear", null),
                new(32342, 0, "Charge Blaster Front Rear Left Right", null),
                new(32343, 0, "Charge Blaster Front Right Rear Left", null),
                new(32344, 0, "Charge Blaster Front Left Right Rear", null),
                new(32345, 0, "Dibrid Blaster Front", null),
                new(33165, 0, "Dibrid Blaster Rear", null),
                new(32346, 0, "Tetrabrid Blaster Front", null),
                new(32348, 0, "Tetrabrid Blaster Left", null),
                new(32349, 0, "Tetrabrid Blaster Right", null),
                new(32347, 0, "Tetrabrid Blaster Rear", null),
                new(32354, 0, "Thunderbolt", null),
                new(32341, 0, "Gouge", null, "死刑"),
                new(32356, 0, "Electric Burst", null, "全屏伤害"),
                new(32357, 0, "Shock", new CircleTemplate(10)),
                new(32351, 0, "Rumbling Thunder", new CircleTemplate(6)),
                new(32353, 0, "Electric Whisker", new FanTemplate(40, MathF.PI / 3)),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(GYMNASIOU_PITHEKOS, "Gymnasiou Pithekos", AltarArena,
            [
                new(32216, 0, "Spark", new DonutTemplate(14, 30)),
                new(32212, 0, "Thundercall", new CircleTemplate(3)),
                new(32217, 0, "Rock Throw", new CircleTemplate(6)),
                new(32215, 0, "Lightning Bolt", new CircleTemplate(6)),
                new(32211, 0, "Sweeping Gouge", null, "死刑"),
                new(32213, 0, "Thunder IV", new CircleTemplate(18)),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(GYMNASIOU_STYPHNOLOBION, "Gymnasiou Styphnolobion", AltarArena,
            [
                new(32245, 0, "Rake", null, "死刑"),
                new(32246, 0, "Tiiimbeeer", null, "全屏伤害"),
                new(32253, 0, "Stone III", new CircleTemplate(6)),
                new(32251, 0, "Earth Shaker", null),
                new(32247, 0, "Earth Quaker", null),
                new(32248, 0, "Earth Quaker I", null),
                new(32249, 0, "Earth Quaker II", null),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(GYMNASIOU_MEGANEREIS, "Gymnasiou Meganereis", AltarArena,
            [
                new(32255, 0, "Ceras", null, "死刑"),
                new(32259, 0, "Hydrobomb", new CircleTemplate(10)),
                new(32258, 0, "Wave of Turmoil", null),
                new(32262, 0, "Waterspout", new CircleTemplate(8)),
                new(32264, 0, "Hydrocannon", RayRectangleTemplate.Symmetric(3, 17)),
                new(32256, 0, "Hydrocannon", RayRectangleTemplate.Symmetric(6, 27)),
                new(32260, 0, "Falling Water", null),
                new(32263, 0, "Immersion", null, "全屏伤害"),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(GYMNASIOU_MANDRAGORAS, "Gymnasiou Mandragoras", AltarArena,
            [
                new(32297, 0, "Ram", null, "死刑"),
                new(32300, 0, "Saibai Mandragora", null, "Calls adds"),
                new(32299, 0, "Leaf Dagger", new CircleTemplate(3)),
                .. GymnasticCasts(),
            ], []),
            new(GYMNASIOU_LEON, "Gymnasiou Leon", AltarArena,
            [
                new(32204, 0, "Inferno Blast", RayRectangleTemplate.Symmetric(20, 46)),
                new(32201, 0, "Roar", new CircleTemplate(12)),
                new(32816, 0, "Flare Star", new CircleTemplate(12)),
                new(32205, 0, "Mark of the Beast", new FanTemplate(8, 2 * MathF.PI / 3)),
                new(32200, 0, "Pounce", null, "死刑"),
                new(32203, 0, "Magma Chamber", new CircleTemplate(8)),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(LAMPAS_CHRYSINE, "Lampas Chrysine", AltarArena,
            [
                new(32292, 0, "Shine", new CircleTemplate(5)),
                new(32294, 0, "Aetherial Light", new FanTemplate(40, MathF.PI / 3)),
                new(32290, 0, "Lightburst", null, "死刑"),
                new(32288, 0, "Summon", null, "Calls bonus adds"),
            ], []),
            new(GYMNASIOU_TIGRIS, "Gymnasiou Tigris", AltarArena,
            [
                new(32208, 0, "Absolute Zero", new FanTemplate(45, MathF.PI / 2)),
                new(32209, 0, "Blizzard III", new CircleTemplate(6)),
                new(32206, 0, "Frumious Jaws", null, "死刑"),
                new(32207, 0, "Eyeshine", null),
                new(32210, 0, "Catching Claws", new FanTemplate(12, MathF.PI / 2)),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(GYMNASIOU_SATYROS, "Gymnasiou Satyros", AltarArena,
            [
                new(32221, 0, "Storm Wing", new FanTemplate(40, MathF.PI / 2)),
                new(32222, 0, "Flash Gale", new CircleTemplate(6)),
                new(32225, 0, "Wingblow", new CircleTemplate(15)),
                new(32218, 0, "Dread Dive", null, "死刑"),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
            ], []),
            new(GYMNASIOU_TRITON, "Gymnasiou Triton", AltarArena,
            [
                new(32230, 0, "Pelagic Cleaver", new FanTemplate(40, MathF.PI / 3)),
                new(32229, 0, "Foul Waters", null),
                new(32231, 0, "Aquatic Lance", new CircleTemplate(13)),
                new(32228, 0, "Protolithic Puncture", null, "死刑"),
                .. GymnasticCasts(),
            ], []),
            new(GYMNASIOU_SPHINX, "Gymnasiou Sphinx", AltarArena,
            [
                new(32265, 0, "Scratch", null, "死刑"),
                new(32273, 0, "Explosion", new DonutTemplate(3, 12)),
                new(32270, 0, "Frigid Pulse", new DonutTemplate(12, 60)),
                new(32272, 0, "Fervid Pulse", new DungeonCrossTemplate(50, 7)),
                new(32266, 0, "Molting Plumage", null, "全屏伤害"),
                new(32274, 0, "Alpine Draft", RayRectangleTemplate.Symmetric(5, 45)),
                new(32271, 0, "Feather Rain", null),
                new(32269, 0, "Aero II", new CircleTemplate(4)),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(LYSSA_CHRYSINE, "Lyssa Chrysine", AltarArena,
            [
                new(32315, 0, "Ice Pillar", null),
                new(32316, 0, "Pillar Pierce", RayRectangleTemplate.Symmetric(4, 80)),
                new(32306, 0, "Skull Dasher", null, "死刑"),
                new(32314, 0, "Heavy Smash", null),
                new(32296, 0, "Howl", null, "Calls adds"),
                new(32309, 0, "Frigid Stone", new CircleTemplate(5)),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                new(32310, 0, "Frigid Needle", null),
                new(32312, 0, "Circle of Ice", null),
                new(32311, 0, "Frigid Needle", null),
                new(32313, 0, "Circle of Ice", null),
            ], []),
            new(GYMNASIOU_MEGAKANTHA, "Gymnasiou Megakantha", AltarArena,
            [
                new(32240, 0, "Sludge Bomb", new CircleTemplate(8)),
                new(32244, 0, "Rustling Wind", RayRectangleTemplate.Symmetric(4, 15)),
                new(32238, 0, "Vine Whip", null, "死刑"),
                new(32237, 0, "Odious Air", new FanTemplate(12, 2 * MathF.PI / 3)),
                new(32242, 0, "Odious Atmosphere", null),
                new(32243, 0, "Acid Mist", new CircleTemplate(6)),
                new(32317, 0, "Heavy Smash", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
        ],
        SHIFTING_OUBLIETTES =>
        [
            new(SECRET_CLADOSELACHE, "Secret Cladoselache", AltarArena,
            [
                new(21705, 0, "Pelagic Cleaver", new FanTemplate(40, MathF.PI / 3)),
                new(21706, 0, "Pelagic Cleaver Rotation", null),
                new(21704, 0, "Tidal Guillotine", new CircleTemplate(13)),
                new(21703, 0, "Protolithic Puncture", null, "死刑"),
                new(21709, 0, "Bite and Run", null),
                new(21708, 0, "Aquatic Lance", null),
                .. KeeperCasts(),
            ], []),
            new(DAEN_TYPHON, "Daen Ose the Avaricious Typhon", AltarArena,
            [
                new(21689, 0, "A-choo", new FanTemplate(12, MathF.PI / 2)),
                new(21771, 0, "Fell Swipe", new FanTemplate(8, 2 * MathF.PI / 3)),
                new(21772, 0, "Wind Shot", RayRectangleTemplate.Symmetric(6, 40)),
                new(21695, 0, "Lingering Snort", new CircleTemplate(20)),
                new(21697, 0, "Unpleasant Breeze", new CircleTemplate(6)),
                new(21688, 0, "Fireball", null),
                new(21690, 0, "Snortsault", null),
                .. MandragoraCasts(),
            ], []),
            new(SECRET_KEEPER, "Secret Keeper", AltarArena,
            [
                new(21680, 0, "Buffet", new FanTemplate(11, 2 * MathF.PI / 3)),
                new(21677, 0, "Inhale", new FanTemplate(20, 2 * MathF.PI / 3)),
                new(21681, 0, "Heavy Scrapline", new CircleTemplate(11)),
                new(21679, 0, "Moldy Phlegm", null),
                .. KeeperCasts(),
            ], []),
            new(DAEN_ULTROS, "Daen Ose the Avaricious Ultros", AltarArena,
            [
                new(21751, 0, "Aqua Breath", new FanTemplate(13, MathF.PI / 2)),
                new(21754, 0, "Tentacle", new CircleTemplate(8)),
                new(21755, 0, "Wallop", RayRectangleTemplate.Symmetric(10, 20)),
                new(21752, 0, "Megavolt", new CircleTemplate(11)),
                new(21745, 0, "Waterspout", new CircleTemplate(4)),
                new(21750, 0, "Soaking Splatter", new CircleTemplate(10)),
                new(21747, 0, "Falling Water", null),
                new(21743, 0, "Thunder III", null, "死刑"),
                new(21749, 0, "Wave of Turmoil", null),
                .. MandragoraCasts(),
                .. KeeperCasts(),
            ], []),
            new(DAEN_ECHIDNA, "Daen Ose the Avaricious Echidna", AltarArena,
            [
                new(21758, 0, "Abyssal Reaper", new CircleTemplate(18)),
                new(21764, 0, "Abyssal Pillar", new CircleTemplate(3)),
                new(21760, 0, "Petriburst", null),
                new(21761, 0, "Petrifaction", null),
                new(21762, 0, "Petrifaction Player", null),
                new(21757, 0, "Sickle Strike", null, "死刑"),
                new(22798, 0, "Void Stream", new FanTemplate(40, MathF.PI / 4)),
                new(22797, 0, "Void Stream", new FanTemplate(40, MathF.PI / 4)),
                new(22799, 0, "Void Stream", new FanTemplate(40, MathF.PI / 4)),
                new(22800, 0, "Void Stream", new FanTemplate(40, MathF.PI / 4)),
                .. MandragoraCasts(),
                .. KeeperCasts(),
            ], []),
            new(SECRET_DJINN, "Secret Djinn", AltarArena,
            [
                new(21655, 0, "Gust", new CircleTemplate(6)),
                new(21657, 0, "Changeless Winds", RayRectangleTemplate.Symmetric(8, 40)),
                new(21656, 0, "Whipwind", RayRectangleTemplate.Symmetric(40, 55)),
                new(21653, 0, "Gentle Breeze", RayRectangleTemplate.Symmetric(4, 15)),
                new(21654, 0, "Whirling Gaol", null, "Raidwide + Knockback"),
                .. KeeperCasts(),
            ], []),
            new(SECRET_WORM, "Secret Worm", AltarArena,
            [
                new(21713, 0, "Hydrocannon", new CircleTemplate(8)),
                new(21711, 0, "Freshwater Cannon", RayRectangleTemplate.Symmetric(4, 46)),
                new(21710, 0, "Brine Breath", null, "死刑"),
                new(21715, 0, "Aqua Burst", null),
                .. MandragoraCasts(),
            ], []),
            new(SECRET_UNDINE, "Secret Undine", AltarArena,
            [
                new(21663, 0, "Hydrofan", new FanTemplate(44, MathF.PI / 6)),
                new(21659, 0, "Hypnowave", new FanTemplate(30, 2 * MathF.PI / 3)),
                new(21660, 0, "Hydropins", RayRectangleTemplate.Symmetric(4, 12)),
                new(21664, 0, "Aqua Globe", new CircleTemplate(8)),
                new(21658, 0, "Hydrowhirl", new CircleTemplate(8)),
                new(21662, 0, "Hydrotaph", null, "全屏伤害"),
                .. KeeperCasts(),
                .. MandragoraCasts(),
            ], []),
            new(SECRET_SWALLOW, "Secret Swallow", AltarArena,
            [
                new(21719, 0, "Seventh Wave", new CircleTemplate(11)),
                new(21717, 0, "Prevailing Current", RayRectangleTemplate.Symmetric(6, 24)),
                new(21720, 0, "Electric Whorl", new DonutTemplate(8, 60)),
                new(21766, 0, "Hydrocannon", new CircleTemplate(8)),
                new(21716, 0, "Ceras", null, "死刑"),
                new(21718, 0, "Body Slam", new CircleTemplate(10)),
                .. KeeperCasts(),
            ], []),
            new(SECRET_PEGASUS, "Secret Pegasus", AltarArena,
            [
                new(21667, 0, "Burning Bright", RayRectangleTemplate.Symmetric(6, 47)),
                new(21668, 0, "Nicker", new CircleTemplate(12)),
                new(21666, 0, "Cloud Call", null, "Calls thunderclouds"),
                new(21669, 0, "Lightning Bolt", new CircleTemplate(8)),
                .. KeeperCasts(),
                .. MandragoraCasts(),
            ], []),
            new(GREEDY_PIXIE, "Greedy Pixie", AltarArena,
            [
                new(21686, 0, "Wind Rune", RayRectangleTemplate.Symmetric(8, 40)),
                new(21684, 0, "Song Rune", new CircleTemplate(6)),
                new(21682, 0, "Storm Rune", null, "全屏伤害"),
                new(22779, 0, "Bush Bash", new CircleTemplate(12)),
                new(21683, 0, "Bush Bash", new CircleTemplate(12)),
                new(22780, 0, "Nature Call", new FanTemplate(30, 2 * MathF.PI / 3)),
                new(21685, 0, "Nature Call", new FanTemplate(30, 2 * MathF.PI / 3)),
                .. KeeperCasts(),
                .. MandragoraCasts(),
            ], []),
            new(SECRET_KORRIGAN, "Secret Korrigan", AltarArena,
            [
                new(21674, 0, "Hypnotize", null),
                new(21675, 0, "Leaf Dagger", new CircleTemplate(3)),
                new(21676, 0, "Saibai Mandragora", null, "Calls adds"),
                new(21673, 0, "Ram", null),
                .. MandragoraCasts(),
            ], []),
            new(SECRET_SERPENT, "Secret Serpent", AltarArena,
            [
                new(21701, 0, "Douse", new CircleTemplate(8)),
                new(21702, 0, "Scale Ripple", new CircleTemplate(8)),
                new(21699, 0, "Fang's End", null, "死刑"),
                new(21700, 0, "Drench", new FanTemplate(15.29f, MathF.PI / 2)),
                new(22771, 0, "Drench", new FanTemplate(13.45f, MathF.PI / 2)),
                .. MandragoraCasts(),
                .. KeeperCasts(),
            ], []),
            new(FUATH_TROUBLEMAKER, "Fuath Troublemaker", AltarArena,
            [
                new(21738, 0, "Croaking Chorus", null, "Calls adds"),
                new(21734, 0, "Toy Hammer", null, "死刑"),
                new(21736, 0, "Spittle", new CircleTemplate(8)),
                new(21737, 0, "Hydrocannon", null),
                new(21740, 0, "Frigid Needle", new DungeonCrossTemplate(40, 2.5f)),
            ], []),
            new(SECRET_BASKET, "Secret Basket", AltarArena,
            [
                new(21724, 0, "Heavy Strike I", null),
                new(21725, 0, "Heavy Strike II", null),
                new(21726, 0, "Heavy Strike III", null),
                new(21729, 0, "Earthquake", null),
                new(21722, 0, "Pollen Corona", new CircleTemplate(8)),
                new(21721, 0, "Straight Punch", null, "死刑"),
                new(21732, 0, "Leafcutter", RayRectangleTemplate.Symmetric(4, 15)),
                new(21728, 0, "Earth Crusher", new DonutTemplate(10, 20)),
                .. MandragoraCasts(),
                .. KeeperCasts(),
            ], []),
            new(SECRET_PORXIE, "Secret Porxie", AltarArena,
            [
                new(21670, 0, "Brewing Storm", new FanTemplate(5, MathF.PI / 3)),
                new(21671, 0, "Harrowing Dream", new CircleTemplate(6)),
                new(22936, 0, "Beclouding Dust", new CircleTemplate(6)),
                new(22937, 0, "Sweep", null),
                .. KeeperCasts(),
                .. MandragoraCasts(),
            ], []),
        ],
        CENOTE_JA_JA_GURAL =>
        [
            .. NativeCenoteRoomCatalog.Create(),
            new(GOLDEN_MOLTER, "Golden Molter", p => CenoteArena(p.Y),
            [
                new(38274, 0, "Lap", null, "死刑"),
                new(38275, 0, "Lightburst", null, "全屏伤害"),
                new(38265, 0, "Crypsis", null),
                new(38266, 0, "Crypsis", null),
                new(38267, 0, "Golden Gall", new FanTemplate(40, MathF.PI)),
                new(38273, 0, "Golden Radiance", new CircleTemplate(5)),
                new(38580, 0, "Blinding Light", null),
                new(38271, 0, "Aetherial Light", new FanTemplate(40, MathF.PI / 3)),
                new(38335, 0, "Vasoconstrictor", new CircleTemplate(6)),
                new(38336, 0, "Vasoconstrictor", new CircleTemplate(6)),
                new(38337, 0, "Vasoconstrictor", new CircleTemplate(6)),
                new(38279, 0, "Spin", new CircleTemplate(11)),
                new(38278, 0, "Scoop", new FanTemplate(15, 2 * MathF.PI / 3)),
                new(38277, 0, "Rotten Spores", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
            new(BULL_APOLLYON, "Bull Apollyon", p => CenoteArena(p.Y),
            [
                new(38261, 0, "Blade", null, "死刑"),
                new(38262, 0, "Pyreburst", null, "全屏伤害"),
                new(38249, 0, "Flame Blade", RayRectangleTemplate.Symmetric(10, 40)),
                new(38250, 0, "Flame Blade", RayRectangleTemplate.Symmetric(10, 40)),
                new(38255, 0, "Crossfire Blade", RayRectangleTemplate.Symmetric(5, 40)),
                new(38251, 0, "Flame Blade", RayRectangleTemplate.Symmetric(5, 40)),
                new(38253, 0, "Crossfire Blade", new DungeonCrossTemplate(20, 5)),
                new(38254, 0, "Crossfire Blade", new DungeonCrossTemplate(20, 5)),
                new(38258, 0, "Blazing Breath", RayRectangleTemplate.Symmetric(10, 44)),
                new(38260, 0, "Blazing Blast", new CircleTemplate(6)),
                new(38279, 0, "Spin", new CircleTemplate(11)),
                new(38278, 0, "Scoop", new FanTemplate(15, 2 * MathF.PI / 3)),
                new(38277, 0, "Rotten Spores", new CircleTemplate(6)),
                .. GymnasticCasts(),
            ], []),
        ],
        _ => [],
    };

    private static uint[] GymnasionRewards() => [0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53, 0x3D4E, 0x3D4D];

    internal static uint[] EncounterOids(uint primary) => primary switch
    {
        GILDED_SENTRY => [GILDED_SENTRY, GOLDY_CAT, VAULTKEEPER],
        VAULT_GARGANTUAN => [0x4898, 0x4899, 0x48B9, 0x48BC, 0x48BB, 0x48BA, 0x48BD, GOLDY_CAT, VAULTKEEPER],
        VAULT_BAT => [VAULT_BAT, 0x489F, .. VaultRewards()],
        VAULT_FLUID_SOUL => [VAULT_FLUID_SOUL, 0x4896, .. VaultRewards()],
        VAULT_WIND_SPIRIT => [VAULT_WIND_SPIRIT, 0x489C, .. VaultRewards()],
        GWYDDNEU => [GWYDDNEU, 0x48A5, .. VaultRewards()],
        GREAT_GIMME_CAT => [GREAT_GIMME_CAT, GOLDY_CAT],
        VAULT_ROADRIPPER => [VAULT_ROADRIPPER, 0x48A2, .. VaultRewards()],
        FASTITOCALON => [FASTITOCALON, 0x48A9, .. VaultRewards()],
        PAEONIA => [PAEONIA, 0x48AF, 0x48B9, 0x48BC, 0x48BB, 0x48BA, 0x48BD],
        SPIRIT_OF_THUNDER => [SPIRIT_OF_THUNDER, .. VaultRewards()],
        OLD_BITTER_EYE => [OLD_BITTER_EYE, 0x48AB, .. VaultRewards()],
        MOBILE_FORTRESS => [MOBILE_FORTRESS, .. VaultRewards()],
        SILVER_SACK => [SILVER_SACK, GOLD_SACK],
        HEXAPOD => [HEXAPOD, .. VaultRewards()],
        GOLD_WHISKER => [GOLD_WHISKER, 0x2544],
        ALTAR_MANDRAGORA => [ALTAR_MANDRAGORA, 0x255C, 0x2546, 0x2547, 0x2548, 0x2549, 0x254A],
        HATI => [HATI, 0x2569, 0x2546, 0x2547, 0x2548, 0x2549, 0x254A],
        ALTAR_SKATENE => [ALTAR_SKATENE, 0x255E, 0x2545, 0x2546, 0x2547, 0x2548, 0x2549, 0x254A],
        ALTAR_BEAST => [ALTAR_BEAST, 0x2567, 0x2544, 0x2545, 0x2546, 0x2547, 0x2548, 0x2549, 0x254A],
        ALTAR_ARACHNE => [ALTAR_ARACHNE, 0x2544, 0x2545],
        THE_OLDER_ONE => [THE_OLDER_ONE, 0x2544, 0x2545, 0x2546, 0x2547, 0x2548, 0x2549, 0x254A],
        ALTAR_CHIMERA => [ALTAR_CHIMERA, 0x256A, 0x2545],
        ALTAR_DIRESAUR => [ALTAR_DIRESAUR, 0x256F, 0x2544, 0x2545],
        ALTAR_TOTEM => [ALTAR_TOTEM, 0x2566, 0x2545],
        ALTAR_DULLAHAN => [ALTAR_DULLAHAN, 0x2563, 0x2547, 0x2548, 0x2546, 0x2549, 0x254A, 0x2545],
        THE_WINGED => [THE_WINGED, 0x2547, 0x2548, 0x2546, 0x2549, 0x254A, 0x2544, 0x2545],
        ALTAR_AIRAVATA => [ALTAR_AIRAVATA, 0x2544, 0x2545],
        ALTAR_KELPIE => [ALTAR_KELPIE, 0x2547, 0x2548, 0x2546, 0x2549, 0x254A, 0x2545, 0x2544],
        GYMNASIOU_MANDRAGORAS => [GYMNASIOU_MANDRAGORAS, 0x3D42, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53],
        GYMNASIOU_LEON => [GYMNASIOU_LEON, 0x3D28, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53, 0x3D4E, 0x3D4D],
        LAMPAS_CHRYSINE => [LAMPAS_CHRYSINE, 0x3D4D],
        GYMNASIOU_TIGRIS => [GYMNASIOU_TIGRIS, 0x3D2A, 0x3D4E, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53],
        GYMNASIOU_SATYROS => [GYMNASIOU_SATYROS, 0x3D2E, 0x3D4D, 0x3D4E],
        GYMNASIOU_TRITON => [GYMNASIOU_TRITON, 0x3D31, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53],
        GYMNASIOU_SPHINX => [GYMNASIOU_SPHINX, 0x3D3C, 0x3D4D, 0x3D4E, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53],
        GYMNASIOU_MEGAKANTHA => [GYMNASIOU_MEGAKANTHA, 0x3D36, 0x3D35, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53, 0x3D4E, 0x3D4D],
        LYSSA_CHRYSINE => [LYSSA_CHRYSINE, 0x3D4D, 0x3D4E],
        GYMNASIOU_PITHEKOS => [GYMNASIOU_PITHEKOS, 0x3D2C, 0x3D4E, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53],
        GYMNASIOU_STYPHNOLOBION => [GYMNASIOU_STYPHNOLOBION, 0x3D38, 0x3D4E, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53],
        GYMNASIOU_MEGANEREIS => [GYMNASIOU_MEGANEREIS, 0x3D3A, 0x3D4D, 0x3D4E, 0x3D4F, 0x3D50, 0x3D51, 0x3D52, 0x3D53],
        GYMNASIOU_ACHELOIOS => [GYMNASIOU_ACHELOIOS, 0x3D3F, .. GymnasionRewards()],
        HIPPOMENES => [HIPPOMENES, .. GymnasionRewards()],
        NARKISSOS => [NARKISSOS, 0x3D4D, 0x3D4E],
        SECRET_DJINN => [SECRET_DJINN, 0x3010, 0x3033, 0x3034],
        SECRET_WORM => [SECRET_WORM, 0x301E, 0x301F, 0x301D, 0x3020, 0x3021],
        SECRET_KEEPER => [SECRET_KEEPER, 0x3033, 0x3034],
        DAEN_ULTROS => [DAEN_ULTROS, .. OublietteRewards()],
        DAEN_ECHIDNA => [DAEN_ECHIDNA, .. OublietteRewards()],
        SECRET_CLADOSELACHE => [SECRET_CLADOSELACHE, 0x3028, 0x3033, 0x3034],
        DAEN_TYPHON => [DAEN_TYPHON, 0x3024, 0x301D, 0x301E, 0x301F, 0x3020, 0x3021],
        SECRET_UNDINE => [SECRET_UNDINE, 0x3013, .. OublietteRewards()],
        SECRET_SWALLOW => [SECRET_SWALLOW, 0x302C, .. OublietteRewards()],
        SECRET_PEGASUS => [SECRET_PEGASUS, .. OublietteRewards()],
        GREEDY_PIXIE => [GREEDY_PIXIE, 0x3019, .. OublietteRewards()],
        SECRET_KORRIGAN => [SECRET_KORRIGAN, 0x301C, 0x301D, 0x301E, 0x301F, 0x3020, 0x3021],
        SECRET_SERPENT => [SECRET_SERPENT, 0x3026, .. OublietteRewards()],
        FUATH_TROUBLEMAKER => [FUATH_TROUBLEMAKER, 0x3033],
        SECRET_BASKET => [SECRET_BASKET, 0x302E, .. OublietteRewards()],
        SECRET_PORXIE => [SECRET_PORXIE, 0x301D, 0x301E, 0x301F, 0x3020, 0x3021, 0x3034],
        BULL_APOLLYON or GOLDEN_MOLTER => [primary, 0x4300, 0x4301, 0x4302, 0x4303, 0x4304, 0x42FF, 0x42FE],
        ALICHINO => [ALICHINO, 0x2BA8, 0x2BA9, .. LyheGhiahRewards()],
        GOLIATH => [GOLIATH, 0x2BA6, .. LyheGhiahRewards()],
        LUCKY_FACE => [LUCKY_FACE, 0x3809, 0x380C, 0x3808, 0x380B, 0x380A],
        AIRAVATA => [AIRAVATA, 0x1F13, 0x1F0F, 0x1F0D, 0x1F11, 0x1F0C, 0x1F10, 0x1FEC, 0x1FEB,
            0x1FCE, 0x1FCF, 0x1FCD, 0x1FD0, 0x1FD1, 0x2064, 0x2063],
        CANAL_ICEBEAST => [CANAL_ICEBEAST, 0x1EBF, 0x1F0E, 0x1F0F],
        _ when NativeCenoteRoomCatalog.IsRoom(primary) => NativeCenoteRoomCatalog.Enemies(primary),
        _ => [],
    };

    internal static int Priority(uint primary, uint oid) => (primary, oid) switch
    {
        (GYMNASIOU_ACHELOIOS, >= 0x3D4F and <= 0x3D53) => 7 - (int)(oid - 0x3D4F),
        (GYMNASIOU_ACHELOIOS, 0x3D4E) => 3,
        (GYMNASIOU_ACHELOIOS, 0x3D4D) => 2,
        (GYMNASIOU_ACHELOIOS, 0x3D3F) => 1,
        (HIPPOMENES, >= 0x3D4F and <= 0x3D53) => 5 - (int)(oid - 0x3D4F),
        (HIPPOMENES, 0x3D4D or 0x3D4E) => 1,
        (NARKISSOS, 0x3D4D) => 2,
        (NARKISSOS, 0x3D4E) => 1,
        (GREAT_GIMME_CAT, GOLDY_CAT) => 1,
        (PAEONIA or SPIRIT_OF_THUNDER or MOBILE_FORTRESS or HEXAPOD, >= 0x48B9 and <= 0x48BD) => 6 - (int)(oid - 0x48B9),
        (OLD_BITTER_EYE, >= 0x48B9 and <= 0x48BD) => 7 - (int)(oid - 0x48B9),
        (OLD_BITTER_EYE, GOLDY_CAT) => 3,
        (OLD_BITTER_EYE, VAULTKEEPER) => 2,
        (OLD_BITTER_EYE, 0x48AB) => 1,
        (PAEONIA, 0x48AF) => 1,
        (SPIRIT_OF_THUNDER or MOBILE_FORTRESS or HEXAPOD, GOLDY_CAT) => 2,
        (SPIRIT_OF_THUNDER or MOBILE_FORTRESS or HEXAPOD, VAULTKEEPER) => 1,
        (GWYDDNEU or VAULT_ROADRIPPER, >= 0x48B9 and <= 0x48BD) => 6 - (int)(oid - 0x48B9),
        (GWYDDNEU or VAULT_ROADRIPPER, GOLDY_CAT) => 2,
        (GWYDDNEU or VAULT_ROADRIPPER, VAULTKEEPER) => 1,
        (FASTITOCALON, >= 0x48B9 and <= 0x48BD) => 7 - (int)(oid - 0x48B9),
        (FASTITOCALON, GOLDY_CAT) => 3,
        (FASTITOCALON, VAULTKEEPER) => 2,
        (FASTITOCALON, 0x48A9) => 1,
        _ when NativeCenoteRoomCatalog.IsRoom(primary) => NativeCenoteRoomCatalog.Priority(oid),
        (GYMNASIOU_PITHEKOS or GYMNASIOU_STYPHNOLOBION or GYMNASIOU_MEGANEREIS, >= 0x3D4F and <= 0x3D53) => 6 - (int)(oid - 0x3D4F),
        (GYMNASIOU_PITHEKOS or GYMNASIOU_STYPHNOLOBION or GYMNASIOU_MEGANEREIS, 0x3D4E) or (GYMNASIOU_MEGANEREIS, 0x3D4D) => 2,
        (GYMNASIOU_PITHEKOS, 0x3D2C) or (GYMNASIOU_STYPHNOLOBION, 0x3D38) or (GYMNASIOU_MEGANEREIS, 0x3D3A) => 1,
        (GILDED_SENTRY, GOLDY_CAT) => 2,
        (GILDED_SENTRY, VAULTKEEPER) => 1,
        (VAULT_GARGANTUAN, >= 0x48B9 and <= 0x48BD) => 6 - (int)(oid - 0x48B9),
        (VAULT_GARGANTUAN, GOLDY_CAT) => 2,
        (VAULT_GARGANTUAN, VAULTKEEPER) or (FUATH_TROUBLEMAKER, 0x3033) => 1,
        (VAULT_BAT or VAULT_FLUID_SOUL or VAULT_WIND_SPIRIT, >= 0x48B9 and <= 0x48BD) => 6 - (int)(oid - 0x48B9),
        (VAULT_BAT or VAULT_FLUID_SOUL or VAULT_WIND_SPIRIT, VAULTKEEPER) => 1,
        (VAULT_BAT, 0x48A0) or (VAULT_FLUID_SOUL, GOLDY_CAT) => 2,
        (GOLD_WHISKER, 0x2544) => 1,
        (ALTAR_DULLAHAN or THE_WINGED or ALTAR_KELPIE, >= 0x2546 and <= 0x254A) => 6 - (int)(oid - 0x2546),
        (ALTAR_DULLAHAN, 0x2545) or (THE_WINGED, 0x2544) => 2,
        (ALTAR_DULLAHAN, 0x2563) or (THE_WINGED, 0x2545) => 1,
        (ALTAR_AIRAVATA or ALTAR_KELPIE, 0x2544) => 2,
        (ALTAR_AIRAVATA or ALTAR_KELPIE, 0x2545) => 1,
        (ALTAR_CHIMERA or ALTAR_DIRESAUR or ALTAR_TOTEM, 0x2545) => 2,
        (ALTAR_CHIMERA, 0x256A) or (ALTAR_DIRESAUR, 0x256F) or (ALTAR_TOTEM, 0x2566) => 1,
        (ALTAR_DIRESAUR, 0x2544) => 3,
        (ALTAR_MANDRAGORA or HATI, 0x2546) => 6,
        (ALTAR_MANDRAGORA or HATI, 0x2547) => 5,
        (ALTAR_MANDRAGORA or HATI, 0x2548) => 4,
        (ALTAR_MANDRAGORA or HATI, 0x2549) => 3,
        (ALTAR_MANDRAGORA or HATI, 0x254A) => 2,
        (ALTAR_MANDRAGORA, 0x255C) or (HATI, 0x2569) => 1,
        (ALTAR_SKATENE or ALTAR_BEAST or THE_OLDER_ONE, >= 0x2546 and <= 0x254A) => 6 - (int)(oid - 0x2546),
        (ALTAR_SKATENE, 0x2545) or (ALTAR_BEAST or ALTAR_ARACHNE or THE_OLDER_ONE, 0x2544) => 2,
        (ALTAR_SKATENE, 0x255E) or (ALTAR_BEAST, 0x2567)
            or (ALTAR_BEAST or ALTAR_ARACHNE or THE_OLDER_ONE, 0x2545) => 1,
        (GYMNASIOU_MANDRAGORAS, >= 0x3D4F and <= 0x3D53) => 6 - (int)(oid - 0x3D4F),
        (GYMNASIOU_MANDRAGORAS, 0x3D42) => 1,
        (GYMNASIOU_LEON, >= 0x3D4F and <= 0x3D53) => 7 - (int)(oid - 0x3D4F),
        (GYMNASIOU_LEON, 0x3D4E) => 3,
        (GYMNASIOU_LEON, 0x3D4D) => 2,
        (GYMNASIOU_LEON, 0x3D28) or (LAMPAS_CHRYSINE, 0x3D4D) => 1,
        (GYMNASIOU_TIGRIS or GYMNASIOU_TRITON or GYMNASIOU_SPHINX, >= 0x3D4F and <= 0x3D53) => 6 - (int)(oid - 0x3D4F),
        (GYMNASIOU_TIGRIS, 0x3D2A) or (GYMNASIOU_SATYROS, 0x3D2E)
            or (GYMNASIOU_TRITON, 0x3D31) or (GYMNASIOU_SPHINX, 0x3D3C) => 1,
        (GYMNASIOU_SATYROS, 0x3D4D) => 3,
        (GYMNASIOU_SATYROS or GYMNASIOU_SPHINX, 0x3D4E) or (GYMNASIOU_SPHINX, 0x3D4D) => 2,
        (GYMNASIOU_MEGAKANTHA, >= 0x3D4F and <= 0x3D53) => 7 - (int)(oid - 0x3D4F),
        (GYMNASIOU_MEGAKANTHA, 0x3D4D) => 3,
        (GYMNASIOU_MEGAKANTHA, 0x3D4E) => 2,
        (GYMNASIOU_MEGAKANTHA, 0x3D35 or 0x3D36) => 1,
        (LYSSA_CHRYSINE, 0x3D4D) or (SECRET_DJINN, 0x3034) => 2,
        (LYSSA_CHRYSINE, 0x3D4E) or (SECRET_DJINN, 0x3010) => 1,
        (SECRET_DJINN, 0x3033) => 3,
        (SECRET_WORM, >= 0x301D and <= 0x3021) => 5 - (int)(oid - 0x301D),
        (DAEN_ULTROS or DAEN_ECHIDNA, >= 0x301D and <= 0x3021) => 6 - (int)(oid - 0x301D),
        (SECRET_KEEPER or DAEN_ULTROS or DAEN_ECHIDNA, 0x3033) => 2,
        (SECRET_KEEPER or DAEN_ULTROS or DAEN_ECHIDNA, 0x3034) => 1,
        (SECRET_CLADOSELACHE, 0x3033) => 3,
        (SECRET_CLADOSELACHE, 0x3034) => 2,
        (SECRET_CLADOSELACHE, 0x3028) => 1,
        (DAEN_TYPHON, >= 0x301D and <= 0x3021) => 6 - (int)(oid - 0x301D),
        (DAEN_TYPHON, 0x3024) => 1,
        (SECRET_KORRIGAN, >= 0x301D and <= 0x3021) => 6 - (int)(oid - 0x301D),
        (SECRET_KORRIGAN, 0x301C) => 1,
        (SECRET_SERPENT, >= 0x301D and <= 0x3021) => 7 - (int)(oid - 0x301D),
        (SECRET_SERPENT, 0x3033) => 3,
        (SECRET_SERPENT, 0x3034) => 2,
        (SECRET_SERPENT, 0x3026) => 1,
        (SECRET_BASKET, >= 0x301D and <= 0x3021) => 6 - (int)(oid - 0x301D),
        (SECRET_BASKET, 0x3033) => 3,
        (SECRET_BASKET, 0x3034) => 2,
        (SECRET_BASKET, 0x302E) => 1,
        (SECRET_PORXIE, >= 0x301D and <= 0x3021) => 5 - (int)(oid - 0x301D),
        (SECRET_PORXIE, 0x3034) => 1,
        (SECRET_UNDINE or SECRET_SWALLOW, >= 0x301D and <= 0x3021) => 7 - (int)(oid - 0x301D),
        (SECRET_UNDINE or SECRET_SWALLOW, 0x3033) => 3,
        (SECRET_UNDINE or SECRET_SWALLOW, 0x3034) => 2,
        (SECRET_UNDINE, 0x3013) or (SECRET_SWALLOW, 0x302C) => 1,
        (SECRET_PEGASUS, >= 0x301D and <= 0x3021) => 5 - (int)(oid - 0x301D),
        (SECRET_PEGASUS, 0x3033) => 2,
        (SECRET_PEGASUS, 0x3034) => 1,
        (GREEDY_PIXIE, >= 0x301D and <= 0x3021) => 6 - (int)(oid - 0x301D),
        (GREEDY_PIXIE, 0x3033) => 3,
        (GREEDY_PIXIE, 0x3034) => 2,
        (GREEDY_PIXIE, 0x3019) => 1,
        (BULL_APOLLYON or GOLDEN_MOLTER, >= 0x4300 and <= 0x4304) => 6 - (int)(oid - 0x4300),
        (BULL_APOLLYON or GOLDEN_MOLTER, 0x42FE) => 2,
        (BULL_APOLLYON or GOLDEN_MOLTER, 0x42FF) => 1,
        (ALICHINO or GOLIATH, >= 0x2A06 and <= 0x2A0A) => 6 - (int)(oid - 0x2A06),
        (ALICHINO or GOLIATH, 0x2A05) => 2,
        (ALICHINO, 0x2BA8 or 0x2BA9) or (GOLIATH, 0x2BA6) => 1,
        (LUCKY_FACE, >= 0x3808 and <= 0x380C) => 6 - (int)(oid - 0x3808),
        (LUCKY_FACE, LUCKY_FACE) => 1,
        (AIRAVATA, >= 0x1FCD and <= 0x1FD1) => 7 - (int)(oid - 0x1FCD),
        (AIRAVATA, 0x2063) => 3,
        (AIRAVATA, 0x2064) or (CANAL_ICEBEAST, 0x1EBF) => 2,
        (AIRAVATA, 0x1F0D or 0x1F0F or 0x1F11 or 0x1F13 or 0x1F0C or 0x1F10 or 0x1FEC or 0x1FEB)
            or (CANAL_ICEBEAST, 0x1F0E or 0x1F0F) => 1,
        _ => 0,
    };

    internal static bool Vulnerable(uint primary, uint oid) => primary switch
    {
        GYMNASIOU_ACHELOIOS or HIPPOMENES => GymnasionRewards().Contains(oid),
        NARKISSOS => oid is 0x3D4D or 0x3D4E,
        GILDED_SENTRY => oid is GOLDY_CAT or VAULTKEEPER,
        VAULT_GARGANTUAN => oid is GOLDY_CAT or VAULTKEEPER or >= 0x48B9 and <= 0x48BD,
        VAULT_BAT or VAULT_FLUID_SOUL or VAULT_WIND_SPIRIT => VaultRewards().Contains(oid),
        GWYDDNEU or VAULT_ROADRIPPER or FASTITOCALON => VaultRewards().Contains(oid),
        SPIRIT_OF_THUNDER or OLD_BITTER_EYE or MOBILE_FORTRESS or HEXAPOD => VaultRewards().Contains(oid),
        PAEONIA => oid is >= 0x48B9 and <= 0x48BD,
        GREAT_GIMME_CAT => oid == GOLDY_CAT,
        FUATH_TROUBLEMAKER => oid == 0x3033,
        GOLD_WHISKER => oid == 0x2544,
        ALTAR_DULLAHAN => oid is >= 0x2545 and <= 0x254A,
        THE_WINGED => oid is >= 0x2544 and <= 0x254A,
        ALTAR_AIRAVATA => oid is 0x2544 or 0x2545,
        ALTAR_KELPIE => oid is >= 0x2544 and <= 0x254A,
        ALTAR_MANDRAGORA or HATI => oid is >= 0x2546 and <= 0x254A,
        ALTAR_SKATENE => oid is >= 0x2545 and <= 0x254A,
        ALTAR_BEAST or THE_OLDER_ONE => oid is >= 0x2544 and <= 0x254A,
        ALTAR_ARACHNE => oid is 0x2544 or 0x2545,
        ALTAR_CHIMERA or ALTAR_TOTEM => oid == 0x2545,
        ALTAR_DIRESAUR => oid is 0x2544 or 0x2545,
        GYMNASIOU_MANDRAGORAS => oid is >= 0x3D4F and <= 0x3D53,
        GYMNASIOU_LEON => oid is >= 0x3D4D and <= 0x3D53,
        LAMPAS_CHRYSINE => oid == 0x3D4D,
        GYMNASIOU_TIGRIS => oid is >= 0x3D4F and <= 0x3D53 or 0x3D4E,
        GYMNASIOU_SATYROS => oid is 0x3D4D or 0x3D4E,
        GYMNASIOU_TRITON => oid is >= 0x3D4F and <= 0x3D53,
        GYMNASIOU_SPHINX => oid is >= 0x3D4D and <= 0x3D53,
        GYMNASIOU_MEGAKANTHA => oid is >= 0x3D4D and <= 0x3D53,
        LYSSA_CHRYSINE => oid is 0x3D4D or 0x3D4E,
        GYMNASIOU_PITHEKOS or GYMNASIOU_STYPHNOLOBION => oid is >= 0x3D4E and <= 0x3D53,
        GYMNASIOU_MEGANEREIS => oid is >= 0x3D4D and <= 0x3D53,
        SECRET_DJINN => oid is 0x3033 or 0x3034,
        SECRET_WORM => oid is >= 0x301D and <= 0x3021,
        SECRET_KEEPER => oid is 0x3033 or 0x3034,
        DAEN_ULTROS or DAEN_ECHIDNA => OublietteRewards().Contains(oid),
        SECRET_CLADOSELACHE => oid is 0x3033 or 0x3034,
        DAEN_TYPHON => oid is >= 0x301D and <= 0x3021,
        SECRET_KORRIGAN => oid is >= 0x301D and <= 0x3021,
        SECRET_UNDINE or SECRET_SWALLOW or SECRET_PEGASUS or GREEDY_PIXIE or SECRET_SERPENT or SECRET_BASKET => OublietteRewards().Contains(oid),
        SECRET_PORXIE => oid is >= 0x301D and <= 0x3021 or 0x3034,
        BULL_APOLLYON or GOLDEN_MOLTER => oid is >= 0x42FE and <= 0x4304,
        _ when NativeCenoteRoomCatalog.IsRoom(primary) => oid is >= 0x42FE and <= 0x4304,
        ALICHINO or GOLIATH => LyheGhiahRewards().Contains(oid),
        LUCKY_FACE => oid is >= 0x3808 and <= 0x380C,
        AIRAVATA => oid is >= 0x1FCD and <= 0x1FD1 or 0x2064 or 0x2063,
        CANAL_ICEBEAST => oid == 0x1EBF,
        _ => false,
    };

    private static DungeonCastRule[] MatangaCasts() =>
    [
        new(5352, 0, "Hurl", new CircleTemplate(6)),
        new(8598, 0, "Raucous Scritch", new FanTemplate(8.42f, 2 * MathF.PI / 3)),
    ];

    private static DungeonCastRule[] MandragoraCasts() =>
    [
        new(6448, 0, "Teary Twirl", new CircleTemplate(6.84f)),
        new(6449, 0, "Pluck and Prune", new CircleTemplate(6.84f)),
        new(6450, 0, "Pungent Pirouette", new CircleTemplate(6.84f)),
        new(6451, 0, "Heirloom Scream", new CircleTemplate(6.84f)),
        new(6452, 0, "Pollen", new CircleTemplate(6.84f)),
    ];

    private static DungeonCastRule[] GymnasticCasts() =>
    [
        new(32301, 0, "Teary Twirl", new CircleTemplate(7)),
        new(32302, 0, "Pluck and Prune", new CircleTemplate(7)),
        new(32303, 0, "Pungent Pirouette", new CircleTemplate(7)),
        new(32304, 0, "Heirloom Scream", new CircleTemplate(7)),
        new(32305, 0, "Pollen", new CircleTemplate(7)),
    ];

    private static DungeonCastRule[] VaultRewardCasts() =>
    [
        new(43727, 0, "Thunderlance", RayRectangleTemplate.Symmetric(3, 20)),
        new(43726, 0, "Lance Swing", new CircleTemplate(8)),
        .. GymnasticCasts(),
    ];

    private static DungeonCastRule[] LyheGhiahRewardCasts() =>
    [
        new(17854, 0, "Spin", new CircleTemplate(11)),
        new(17852, 0, "Mash", RayRectangleTemplate.Symmetric(4, 15.23f)),
        new(17853, 0, "Scoop", new FanTemplate(15, 2 * MathF.PI / 3)),
        .. MandragoraCasts(),
    ];

    private static uint[] LyheGhiahRewards() => [0x2A07, 0x2A08, 0x2A09, 0x2A06, 0x2A0A, 0x2A05];

    private static IGeometryShape LyheGhiahRoom5Arena(Vector3 position)
    {
        Vector2[] vertices =
        [
            new(14.82f, -416.43f), new(15.44f, -416.52f), new(15.93f, -416.35f), new(16.07f, -415.68f), new(16.5f, -415.19f),
            new(16.96f, -414.83f), new(17.51f, -414.6f), new(18.1f, -414.65f), new(18.68f, -414.76f), new(19.2f, -415.17f),
            new(19.58f, -415.61f), new(20.2f, -415.48f), new(25.7f, -409.9f), new(25.42f, -409.38f), new(24.96f, -408.85f),
            new(24.7f, -408.28f), new(24.59f, -407.65f), new(24.77f, -407.03f), new(25.06f, -406.47f), new(25.53f, -406.09f),
            new(26.12f, -405.86f), new(26.31f, -405.2f), new(26.42f, -404.55f), new(26.82f, -403.14f), new(26.89f, -402.41f),
            new(26.23f, -402.21f), new(25.56f, -402.16f), new(24.96f, -402.45f), new(23.87f, -403.13f), new(23.28f, -403.4f),
            new(22.6f, -403.55f), new(21.94f, -403.64f), new(20.7f, -403.52f), new(20.06f, -403.35f), new(19.47f, -403.15f),
            new(18.28f, -402.6f), new(16.62f, -401.59f), new(16.22f, -401.18f), new(16.43f, -400.63f), new(17.95f, -397.79f),
            new(18.19f, -397.2f), new(19.1f, -394.21f), new(19.24f, -393.55f), new(19.57f, -390.25f), new(19.22f, -386.22f),
            new(18.08f, -382.45f), new(16.2f, -379.01f), new(13.69f, -376.02f), new(11.09f, -373.88f), new(10.54f, -373.53f),
            new(4.02f, -370.55f), new(-3.7f, -370.53f), new(-4.26f, -370.63f), new(-7.46f, -371.88f), new(-10.85f, -373.69f),
            new(-13.99f, -376.31f), new(-16.19f, -378.99f), new(-18.17f, -382.71f), new(-19.22f, -386.21f), new(-19.58f, -390.05f),
            new(-19.18f, -393.95f), new(-18.2f, -397.19f), new(-17.93f, -397.82f), new(-16.9f, -399.73f), new(-16.4f, -400.11f),
            new(-19.26f, -403.06f), new(-19.84f, -403.32f), new(-20.51f, -403.49f), new(-21.14f, -403.59f), new(-21.81f, -403.63f),
            new(-22.45f, -403.58f), new(-23.15f, -403.43f), new(-23.74f, -403.23f), new(-25.37f, -402.2f), new(-26.07f, -402.17f),
            new(-26.73f, -402.35f), new(-26.88f, -402.99f), new(-26.35f, -404.91f), new(-26.43f, -405.51f), new(-25.94f, -405.9f),
            new(-25.4f, -406.21f), new(-24.91f, -406.65f), new(-24.69f, -407.24f), new(-24.6f, -407.86f), new(-24.71f, -408.52f),
            new(-25.06f, -409), new(-25.63f, -410), new(-19.96f, -415.7f), new(-19.46f, -415.43f), new(-19.01f, -415.03f),
            new(-18.48f, -414.74f), new(-17.82f, -414.61f), new(-17.22f, -414.76f), new(-16.66f, -414.99f), new(-16.28f, -415.44f),
            new(-16, -415.95f), new(-15.89f, -416.49f), new(-15.38f, -416.25f), new(-14.79f, -416.26f), new(-12.91f, -416.85f),
            new(-12.48f, -416.31f), new(-12.34f, -415.66f), new(-12.57f, -415.01f), new(-13.56f, -413.38f), new(-13.85f, -412.06f),
            new(-13.75f, -410.77f), new(-13.4f, -409.53f), new(-12.85f, -408.34f), new(-12.16f, -407.17f), new(-11.35f, -406.27f),
            new(-10.81f, -406.33f), new(-7.5f, -408.1f), new(-3.67f, -409.23f), new(0.15f, -409.58f), new(3.94f, -409.18f),
            new(7.16f, -408.2f), new(7.77f, -407.95f), new(10.71f, -406.38f), new(11.27f, -406.26f), new(11.72f, -406.65f),
            new(12.16f, -407.15f), new(12.85f, -408.31f), new(13.38f, -409.48f), new(13.72f, -410.7f), new(13.83f, -411.98f),
            new(13.6f, -413.3f), new(12.62f, -414.9f), new(12.36f, -415.55f), new(12.47f, -416.28f), new(12.81f, -416.95f),
        ];
        var center = new Vector2((vertices.Min(v => v.X) + vertices.Max(v => v.X)) / 2,
            (vertices.Min(v => v.Y) + vertices.Max(v => v.Y)) / 2);
        return new WorldPolygonGeometry(vertices.Select(v => new Vector3(v.X, position.Y, v.Y)),
            new Vector3(center.X, position.Y, center.Y));
    }

    private static uint[] VaultRewards() => [0x48B9, 0x48BC, 0x48BB, 0x48BA, 0x48BD, VAULTKEEPER, GOLDY_CAT];

    private static IGeometryShape VaultArena(Vector3 position) => new RectangleGeometry(new(100, position.Y, 100), 0, 39, 39);

    private static uint[] OublietteRewards() => [0x301D, 0x301E, 0x301F, 0x3020, 0x3021, 0x3034, 0x3033];

    private static DungeonCastRule[] KeeperCasts() =>
    [
        new(21769, 0, "Spin", new CircleTemplate(11)),
        new(21767, 0, "Mash", RayRectangleTemplate.Symmetric(4, 13)),
        new(21768, 0, "Scoop", new FanTemplate(15, 2 * MathF.PI / 3)),
    ];

    private static PolygonRegionGeometry CenoteArena(float y)
    {
        var center = new Vector3(0, y, -372);
        // CosPI.Pi32th is the source reciprocal constant, rather than cos(pi/32).
        var radius = 19.5f * 1.004839f;
        var increment = MathF.Tau / 32;
        var polygon = new LocalPolygonGeometry(center, 0, Enumerable.Range(0, 32).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * increment);
            return new Vector2(-radius * (float)sin, -radius * (float)cos);
        }));
        Vector2[] entrance = [new(-20, 18.35f), new(20, 18.35f), new(20, 21.65f), new(-20, 21.65f)];
        var region = (PolygonRegionGeometry)DungeonPolygonDifference.Build(polygon, [(Array.Empty<Vector2>(), entrance)]);
        var bounds = region.GetBounds();
        var sourceCenter = new Vector3((bounds.MinX + bounds.MaxX) / 2, y, (bounds.MinZ + bounds.MaxZ) / 2);
        var triangles = region.FillPolygons.Select(p => p.Select(v =>
            new Vector2(v.X - sourceCenter.X, v.Z - sourceCenter.Z)).ToArray()).ToArray();
        return new(sourceCenter, triangles, [], triangles, region.GetContours());
    }

    private static GeometryTemplate TreasurePrismShape()
    {
        var side = 20 * MathF.Sqrt(.5f);
        return new LocalPolygonTemplate([Vector2.Zero, new(-side, -side), new(side, -side)]);
    }

    private static IGeometryShape AltarArena(Vector3 position) => new LocalPolygonGeometry(new(100, position.Y, 100), 0,
        Enumerable.Range(0, 48).Select(i =>
        {
            var angle = i * (MathF.Tau / 48);
            return new Vector2(19 * MathF.Sin(angle), 19 * MathF.Cos(angle));
        }));
}

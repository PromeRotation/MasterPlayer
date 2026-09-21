// 斗兽棋·高一（斗兽奇弈 高段 第一盘，B22~B31 共 10 组敌人）机制脚本
// 迁移自 BossModReborn-7.56 BossMod/Modules/Dawntrail/Foray/Crucible/，唯一事实来源：docs/reference/CrucibleHigh1.spec.md
// 45 组敌人共用同一张大地图（TerritoryID 1342）：脚本限定该区域，内部仍按 NameID 扫描激活对应组，纯事件驱动。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.Drawing;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using FfxivGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace MasterPlayer.Scripts;

[MechanicScript(
    name: "斗兽棋·高一",
    territorys: [1342u],
    guid: "masterplayer.crucible.high1",
    version: "1.0.3",
    author: "XSZ",
    note: "斗兽奇弈高段第一盘 B22-B31 全机制 + AI 躲避（迁移自 BossModReborn）")]
public sealed class CrucibleHigh1Script : MasterPlayerScript
{
    // ════════════════ 常量：通用 ════════════════
    private const float NpcCastPadding = 0.3f;      // NPC 读条补正（BMR NPCRemainingTime 含 +0.3s）
    private const float DangerTimeMargin = 0.25f;   // 危险区生效提前（CrucibleScorpionAI.Avoid）
    private const float DangerRadiusMargin = 0.35f; // 圆/月环半径余量（月环内径 -0.35/外径 +0.35）
    private const float ScanIntervalSeconds = 0.5f;
    private const float HintThrottleSeconds = 10f;
    private const float Fan20 = MathF.PI / 9f;
    private const float Fan60 = MathF.PI / 3f;
    private const float Fan90 = MathF.PI / 2f;
    private const float Fan120 = MathF.PI * 2f / 3f;
    private const float Fan150 = MathF.PI * 5f / 6f;
    private const float Fan180 = MathF.PI;
    private const float Step45 = MathF.PI / 4f;

    // ════════════════ 常量：NameID（组激活检测）════════════════
    private const uint NameOwl = 14596;               // B22 奇子·博学林鸮
    private const uint NameOwlFeather = 14598;        // 博学林鸮的羽毛
    private const uint NameMorbol = 14599;            // B23 奇子·魔界花
    private const uint NameMorbolSeedling = 14600;    // 幼苗
    private const uint NameMorbolMouth = 14601;       // 大口花
    private const uint NameMorbolSap = 14602;         // 腐汁
    private const uint NameCorpseFlower = 14603;      // B24 奇子·尸生花
    private const uint NameFlowerBud = 14604;         // 花苗
    private const uint NameQueenWasp = 14605;         // 女王鹰蜂
    private const uint NameIceDragon = 14606;         // B25 奇子·冰龙
    private const uint NameIceSprite = 14607;         // 冰元精
    private const uint NameGargoyle = 14608;          // B26 奇子·石像鬼
    private const uint NameMiasmaBall = 14609;        // 瘴气球
    private const uint NameManager = 14610;           // B27 管理者
    private const uint NameClockworkInfantry = 14612; // B27 奇子·发条步兵
    private const uint NamePanicArmor = 14613;        // 恐慌装甲
    private const uint NameDrone = 14614;             // 无人机
    private const uint NameCobra = 14616;             // 眼镜蛇
    private const uint NameGolem = 14617;             // B28 奇子·巨像
    private const uint NameTreant = 14618;            // B29 奇子·树精
    private const uint NameSlug = 14619;              // 蛞蝓
    private const uint NameTreantBud = 14620;         // 花苗
    private const uint NameScorpion = 14621;          // 凶蛛蝎
    private const uint NameBiloko = 14622;            // 比洛克
    private const uint NameBombQueen = 14623;         // B30 爆弹之母
    private const uint NameGrenade = 14624;           // B30 奇子·榴弹怪
    private const uint NameFireBomb = 14625;          // 火弹
    private const uint NameIceBomb = 14626;           // 冰弹
    private const uint NameFlameBall = 14627;         // 焰球
    private const uint NameGrudgeDragon = 14628;      // B31 怨毒龙 博尔格尼
    private const uint NameToxicSubstance = 14629;    // 有毒物质
    private const uint NamePoisonCloud = 14630;       // 毒气团

    // ════════════════ 常量：B22 AID ════════════════
    private const ushort AidB22Drop = 0xBE10;             // 48656 掉落 R10 落点
    private const ushort AidB22ChapterLevitate = 0xBE11;  // 48657 章节魔法：浮空 pad
    private const ushort AidB22ChapterKappa = 0xBE12;     // 48658 章节魔法：河童 pad
    private const ushort AidB22ChapterTransform = 0xBE13; // 48659 章节魔法：变身 pad（判定 +0.8s）
    private const ushort AidB22HammerHit = 0xBE14;        // 48660 魔法锤命中
    private const ushort AidB22ManaBurst = 0xBE19;        // 48665 魔力爆散 R15 落点
    private const ushort AidB22Gust = 0xBE1B;             // 48667 暴风：击退 25y（羽毛 11.7s 读条）
    private const ushort AidB22Focus = 0xBE1C;            // 48668 精神集中
    private const ushort AidB22ChapterHint = 0xBE1D;      // 48669 章节提示

    // ════════════════ 常量：B23 AID ════════════════
    private const ushort AidB23BreathLeft = 0xBE1F;       // 48671 恶臭气转向读条 4.2s（步进 -45°）
    private const ushort AidB23BreathRight = 0xBE20;      // 48672 恶臭气转向读条（步进 +45°）
    private const ushort AidB23BreathHitA = 0xBE21;       // 48673 恶臭气实际伤害 0.2s
    private const ushort AidB23BreathHitB = 0xBE23;       // 48675 恶臭气实际伤害
    private const ushort AidB23AcidMist = 0xBE27;         // 48679 酸雾 R6 落点
    private const ushort AidB23Missile = 0xBE28;          // 48680 粘液飞弹点名
    private const ushort AidB23VineThrust = 0xBE2A;       // 48682 藤鞭穿刺 13×8
    private const ushort AidB23Quake = 0xC646;            // 50758 地震：击退 10y
    private const ushort AidB23Paralysis = 0xC570;        // 50544 麻痹（单体）
    private const ushort AidB23Poison = 0xC63E;           // 50750 中毒（单体）

    // ════════════════ 常量：B24 AID ════════════════
    private const ushort AidB24FlowerTrap = 0xBE2B;       // 48683 鲜花陷阱
    private const ushort AidB24Devour = 0xBE2D;           // 48685 捕食（单体）
    private const ushort AidB24RottenStench = 0xBE32;     // 48690 腐烂恶臭视觉读条 4.7s
    private const ushort AidB24AcidRain = 0xBE35;         // 48693 酸雨 R6 落点

    // ════════════════ 常量：B25 AID ════════════════
    private const ushort AidB25IceTormentA = 0xBE39;      // 48697 冰寒折磨 100×15
    private const ushort AidB25IceTormentB = 0xBE3B;      // 48699 冰寒折磨
    private const ushort AidB25Dive = 0xBE40;             // 48704 低温俯冲 46×23（BMR 有意跳过，本移植补齐待实测）
    private const ushort AidB25Rot = 0xBE44;              // 48708 无终的枯朽 R9 落点
    private const ushort AidB25IceCover = 0xBE48;         // 48712 寒冰覆盖 R5 落点

    // ════════════════ 常量：B26 AID ════════════════
    private const ushort AidB26ArmCast = 0xBE4A;          // 48714 布防读条（读条结束即布防）
    private const ushort AidB26MiasmaPlace = 0xBE4B;      // 48715 瘴气布防 R6 落点
    private const ushort AidB26MineBlast = 0xBE4C;        // 48716 瘴气爆炸
    private const ushort AidB26RushHint = 0xBE4D;         // 48717 突进提示
    private const ushort AidB26HalfCleave = 0xBE4E;       // 48718 半场斩伤害
    private const ushort AidB26InOutPredict = 0xBE4F;     // 48719 圆环连斩预测
    private const ushort AidB26SteelActual = 0xBE50;      // 48720 钢铁实际
    private const ushort AidB26RingActual = 0xBE51;       // 48721 月环实际
    private const ushort AidB26Chain3 = 0xBE54;           // 48724 五连斩击退链（+0.6s）
    private const ushort AidB26Chain4 = 0xBE55;           // 48725 五连斩最后一击（即刻，击退 20y）
    private const ushort AidB26DesolateNova = 0xBE57;     // 48727 荒芜新星 60×7
    private const ushort AidB26Asphalt = 0xBE59;          // 48729 沥青圈 R6 落点
    private const ushort AidB26Chain1 = 0xC607;           // 50695 五连斩击退链（+9.5s）
    private const ushort AidB26Chain2 = 0xC608;           // 50696 五连斩击退链（+9.0s）
    private const ushort AidB26ChargePack = 0xC6F4;       // 50932 冲锋效果包

    // ════════════════ 常量：B27 AID ════════════════
    private const ushort AidB27AutoArtillery = 0xBE5F;    // 48735 自动火炮 40×5
    private const ushort AidB27SpinSmash = 0xBE60;        // 48736 旋转挥打 R5
    private const ushort AidB27ManaBurst = 0xBE61;        // 48737 魔力爆散 R6
    private const ushort AidB27PoisonBurst = 0xBE62;      // 48738 毒素爆散（全体）
    private const ushort AidB27Confusion = 0xBE64;        // 48740 混乱术（全体）
    private const ushort AidB27BurnProcess = 0xBE66;      // 48742 燃烧处理（单体）
    private const ushort AidB27FireBreath = 0xBE6B;       // 48747 火炎放射 锥 R40 半角 60°

    // ════════════════ 常量：B28 AID ════════════════
    private const ushort AidB28RockWall = 0xBE6C;         // 48748 岩墙+中央塌陷
    private const ushort AidB28Shockwave = 0xBE6E;        // 48750 冲击波 5×5
    private const ushort AidB28CollapseHit = 0xBE6F;      // 48751 塌陷伤害
    private const ushort AidB28CoreRing = 0xBE71;         // 48753 核心环光 月环 5-50
    private const ushort AidB28PlainSplit = 0xBE73;       // 48755 平原震裂 R20
    private const ushort AidB28RockFallCircle = 0xBE75;   // 48757 落石实际读条（圆 R8）0.7s
    private const ushort AidB28RockFallDonut = 0xBE76;    // 48758 落石实际读条（月环 3-11）
    private const ushort AidB28JumpStartA = 0xBE77;       // 48759 起跳
    private const ushort AidB28JumpStartB = 0xBE78;       // 48760 起跳
    private const ushort AidB28JumpPack = 0xBE79;         // 48761 跳跃效果包（主目标=被跳巨像）
    private const ushort AidB28JumpRingActual = 0xBE7B;   // 48763 跳跃月环实际读条
    private const ushort AidB28SelfDestruct = 0xBE7E;     // 48766 自爆提示
    private const ushort AidB28RockSpike = 0xBE80;        // 48768 岩突 锥 R40 半角 30°
    private const ushort AidB28Knockback = 0xC5D9;        // 50649 击退 17y

    // ════════════════ 常量：B29 AID ════════════════
    private const ushort AidB29Tornado1 = 0xBE83;         // 48771 巨木龙卷：圆 R12
    private const ushort AidB29Tornado2 = 0xBE84;         // 48772 环 12-18
    private const ushort AidB29Tornado3 = 0xBE85;         // 48773 环 18-24
    private const ushort AidB29Tornado4 = 0xBE86;         // 48774 环 24-30
    private const ushort AidB29Tornado5 = 0xBE87;         // 48775 环 30-36
    private const ushort AidB29GustA = 0xBE8A;            // 48778 簌簌轻风 锥 R60 半角 45°
    private const ushort AidB29GustB = 0xBE8B;            // 48779 半角 75°
    private const ushort AidB29GustC = 0xBE8C;            // 48780 半角 75°
    private const ushort AidB29Acorn = 0xBE8D;            // 48781 橡果炸弹 R3
    private const ushort AidB29Heal = 0xBE8E;             // 48782 治疗之水
    private const ushort AidB29Sap = 0xBE8F;              // 48783 释放汁液 R5
    private const ushort AidB29Web = 0xBE91;              // 48785 蛛网屏 40×4

    // ════════════════ 常量：B30 AID ════════════════
    private const ushort AidB30Enrage = 0xBE96;           // 48790 连续大爆炸（狂暴）
    private const ushort AidB30Blaze = 0xBE97;            // 48791 烈炎 R5 落点
    private const ushort AidB30SelfDestructHit = 0xBE98;  // 48792 自爆命中（火弹）
    private const ushort AidB30IceSpikes = 0xBE99;        // 48793 冰棘屏障
    private const ushort AidB30IceShatterHit = 0xBE9A;    // 48794 冰碎命中（冰弹）
    private const ushort AidB30PoisonSmoke = 0xBE9C;      // 48796 剧毒烟雾 锥 R40 半角 10°
    private const ushort AidB30FireRoar = 0xBE9F;         // 48799 烈火怒骂 锥 R40 半角 90°
    private const ushort AidB30MeltdownTelegraph = 0xBEA0;// 48800 熔毁预告

    // ════════════════ 常量：B31 AID ════════════════
    private const ushort AidB31PoisonBreath = 0xBEA7;     // 48807 猛毒吐息 3s（六连毒浪）
    private const ushort AidB31PoisonWave = 0xBEA8;       // 48808 毒浪命中
    private const ushort AidB31PoisonBait = 0xBEA9;       // 48809 连线放毒读条 3.2s
    private const ushort AidB31PoisonSnap = 0xBEAA;       // 48810 放毒快照 ×3
    private const ushort AidB31Smog = 0xBEAC;             // 48812 浓烟呛呕 R6 落点
    private const ushort AidB31Dive = 0xBEAE;             // 48814 低温俯冲 48×20
    private const ushort AidB31LandingOuter = 0xBEB0;     // 48816 空降外环
    private const ushort AidB31Phlegm = 0xBEB3;           // 48819 蠕动的痰 R6 落点
    private const ushort AidB31DeadlyPoison = 0xBEB5;     // 48821 剧毒全场
    private const ushort AidB31LandingCenter = 0xBEBC;    // 48828 空降中心 R6

    // ════════════════ 常量：EObj / Tether / MapEffect / 状态 ════════════════
    private const uint EObjPadLevitate = 0x1E9582;        // 浮空 pad
    private const uint EObjPadKappa = 0x1EC0E1;           // 河童 pad
    private const uint EObjPadTransform = 0x1EC0E0;       // 变身 pad
    private const uint EObjMorbolPool = 0x1E9F40;         // B23 毒池 R6
    private const uint EObjIcePool = 0x1E972A;            // B25 冰池 R9
    private const uint EObjRock = 0x1EC0D2;               // B28 落石
    private const uint EObjBombPool = 0x1E8D9B;           // B30 毒池 R6
    private const uint EObjPoisonPool = 0x1EB704;         // B31 毒池 R6
    // EObj 动画状态 = (Param1 << 16) | Param2（与 BMR OnActorEAnim 组合同序）
    private const uint RockAnimCircle = 0x00010002;       // 圆 R8 预告（+10s）
    private const uint RockAnimDonut = 0x00400080;        // 月环 3-11 预告（+10s）
    private const uint RockAnimCircleFinal = 0x00040008;  // 最终阶段（收紧到 +3.1s）
    private const uint RockAnimDonutFinal = 0x00040100;   // 最终阶段（收紧到 +3.1s）
    private const uint TetherMiasma = 0x01AA;             // 426 瘴气球布防连线
    private const uint TetherDragonPoison = 0x11;         // 17 连线放毒
    private const uint OidGargoyle = 0x4CC2;              // B26 石像鬼本体 OID（效果包归属判定）
    // MapEffect state = Param1 | (Param2 << 16)
    private const uint MapEffectOn = 0x00020001;
    private const uint MapEffectOff = 0x00080004;
    // B31 连线放毒路线几何（RouteOuterOffset = √(18.3² - 7²) ≈ 16.9）
    private const float BaitRouteRadius = 18.3f;
    private const float BaitRouteHalfWidth = 7f;
    private const float BaitRouteDepth = 7f;
    private const float BaitPointRadius = 0.2f;
    private static readonly float BaitRouteOuterOffset = MathF.Sqrt(BaitRouteRadius * BaitRouteRadius - BaitRouteHalfWidth * BaitRouteHalfWidth);

    // ════════════════ 场地定义（规格 §2）════════════════
    private enum BattleGroup { None = 0, B22, B23, B24, B25, B26, B27, B28, B29, B30, B31 }
    private enum ArenaShape { Circle, Square, Rect, Dragon, Treant }

    private sealed record ArenaDef(string Label, ArenaShape Shape, Vector3 Center, int FenceIndex);

    private static readonly ArenaDef[] ArenaFields =
    [
        new("圆场", ArenaShape.Circle, new Vector3(120f, 0f, -420f), 0), // 安全 R20，电网环 R20~23
        new("方场", ArenaShape.Square, new Vector3(120f, 0f, 0f), 1),    // 安全 40×40，电网外扩 3y
        new("矩形场", ArenaShape.Rect, new Vector3(520f, 0f, 0f), 2),    // 安全 40×30，电网外扩 3y
    ];

    private static readonly ArenaDef ArenaTreantField = new("树精场", ArenaShape.Treant, new Vector3(120f, 0f, -420f), 0); // 安全 R20 内缩 0.3；与圆场同一物理场地，电网沿用 index 0
    private static readonly ArenaDef ArenaDragonField = new("龙场", ArenaShape.Dragon, new Vector3(920f, 0f, -420f), -1);  // 实体墙 R19.75 内缩 0.7，无电网

    // ════════════════ 机制开关名（规格 §3.6，12 项）════════════════
    private const string MechB22 = "B22 林鸮";
    private const string MechB23 = "B23 魔界花";
    private const string MechB24 = "B24 尸生花";
    private const string MechB25 = "B25 冰龙";
    private const string MechB26 = "B26 石像鬼";
    private const string MechB27 = "B27 发条步兵";
    private const string MechB28 = "B28 巨像";
    private const string MechB29 = "B29 树精";
    private const string MechB30 = "B30 榴弹怪";
    private const string MechB31 = "B31 怨毒龙";
    private const string MechTetherLines = "连线绘制";
    private const string MechTextHints = "文字提示";

    public override IEnumerable<string> GetMechanisms() =>
        [MechB22, MechB23, MechB24, MechB25, MechB26, MechB27, MechB28, MechB29, MechB30, MechB31, MechTetherLines, MechTextHints];

    // ════════════════ 实例状态（铁律 7：无静态可变状态）════════════════
    private CastAoeTracker _tracker = null!;
    private GroupHandler[] _handlers = [];
    private Dictionary<BattleGroup, GroupHandler> _handlerByGroup = new();
    private readonly HashSet<BattleGroup> _activeGroups = [];
    private readonly HashSet<BattleGroup> _seenGroups = [];
    private readonly Dictionary<BattleGroup, (uint NameId, Vector3 Pos)> _seenInfo = new();
    private readonly List<BattleGroup> _deactivateScratch = [];
    private readonly Dictionary<string, DateTime> _hintLast = new(StringComparer.Ordinal);
    private readonly Dictionary<int, List<SafeZoneAreaHandle>> _fenceHandles = new();
    private readonly Dictionary<int, int> _fenceUsers = new();
    private readonly Dictionary<int, float> _fenceY = new();
    private BattleGroup _boundaryOwner = BattleGroup.None;
    private float _scanAccum;

    private BattleGroup BoundaryOwner => _boundaryOwner;
    private bool TetherLinesOn => IsMechanismEnabled(MechTetherLines);
    private bool TextHintsOn => IsMechanismEnabled(MechTextHints);
    private CastAoeTracker Tracker => _tracker;

    // ════════════════ 生命周期 ════════════════

    protected override void OnStart()
    {
        _tracker = new CastAoeTracker(this);
        _handlers =
        [
            new B22OwlHandler(this),
            new B23MorbolHandler(this),
            new B24CorpseFlowerHandler(this),
            new B25IceDragonHandler(this),
            new B26GargoyleHandler(this),
            new B27ClockworkHandler(this),
            new B28GolemHandler(this),
            new B29TreantHandler(this),
            new B30BombHandler(this),
            new B31DragonHandler(this),
        ];
        _handlerByGroup = _handlers.ToDictionary(h => h.Group);

        OnStartCast(DispatchCastStart);
        OnCancelCast(e => DispatchCastEnd((uint)e.SourceId, e.ActionId));
        OnInterruptCast(e => DispatchCastEnd((uint)e.SourceId, e.ActionId));
        OnActionEffect(DispatchActionEffect);
        OnTether(DispatchTether);
        OnTetherCancel(DispatchTetherCancel);
        OnEObjAnimation(DispatchEObjAnim);
        OnEObjSetState(DispatchEObjState);
        OnMapEffect(DispatchMapEffect);

        // 接管 B22~B31 全部读条技能：脚本运行期间通用躲避跳过这些技能，避免重复注册危险区
        SuppressGenericDodge(
            AidB22Drop, AidB22ChapterLevitate, AidB22ChapterKappa, AidB22ChapterTransform, AidB22HammerHit,
            AidB22ManaBurst, AidB22Gust, AidB22Focus, AidB22ChapterHint,
            AidB23BreathLeft, AidB23BreathRight, AidB23BreathHitA, AidB23BreathHitB, AidB23AcidMist,
            AidB23Missile, AidB23VineThrust, AidB23Quake, AidB23Paralysis, AidB23Poison,
            AidB24FlowerTrap, AidB24Devour, AidB24RottenStench, AidB24AcidRain,
            AidB25IceTormentA, AidB25IceTormentB, AidB25Dive, AidB25Rot, AidB25IceCover,
            AidB26ArmCast, AidB26MiasmaPlace, AidB26MineBlast, AidB26RushHint, AidB26HalfCleave,
            AidB26InOutPredict, AidB26SteelActual, AidB26RingActual, AidB26Chain3, AidB26Chain4,
            AidB26DesolateNova, AidB26Asphalt, AidB26Chain1, AidB26Chain2, AidB26ChargePack,
            AidB27AutoArtillery, AidB27SpinSmash, AidB27ManaBurst, AidB27PoisonBurst, AidB27Confusion,
            AidB27BurnProcess, AidB27FireBreath,
            AidB28RockWall, AidB28Shockwave, AidB28CollapseHit, AidB28CoreRing, AidB28PlainSplit,
            AidB28RockFallCircle, AidB28RockFallDonut, AidB28JumpStartA, AidB28JumpStartB, AidB28JumpPack,
            AidB28JumpRingActual, AidB28SelfDestruct, AidB28RockSpike, AidB28Knockback,
            AidB29Tornado1, AidB29Tornado2, AidB29Tornado3, AidB29Tornado4, AidB29Tornado5,
            AidB29GustA, AidB29GustB, AidB29GustC, AidB29Acorn, AidB29Heal, AidB29Sap, AidB29Web,
            AidB30Enrage, AidB30Blaze, AidB30SelfDestructHit, AidB30IceSpikes, AidB30IceShatterHit,
            AidB30PoisonSmoke, AidB30FireRoar, AidB30MeltdownTelegraph,
            AidB31PoisonBreath, AidB31PoisonWave, AidB31PoisonBait, AidB31PoisonSnap, AidB31Smog,
            AidB31Dive, AidB31LandingOuter, AidB31Phlegm, AidB31DeadlyPoison, AidB31LandingCenter);

        ScanTick(); // 热重载后立即恢复激活状态
        Log("【斗兽棋高一】脚本已启动：按 NameID 扫描激活 B22~B31 机制组");
    }

    protected override void OnUpdate(float deltaTime)
    {
        _scanAccum += deltaTime;
        if (_scanAccum >= ScanIntervalSeconds)
        {
            _scanAccum = 0f;
            ScanTick();
        }

        _tracker.Purge();
        foreach (var h in _handlers)
        {
            if (!_activeGroups.Contains(h.Group))
                continue;
            if (!IsMechanismEnabled(h.Mechanism))
            {
                h.Cleanup(); // 机制开关关闭：持续清理该组句柄（各 Cleanup 幂等）
                _tracker.CancelGroup(h.Group); // 含该组经共享 tracker 注册的读条危险区
                continue;
            }
            using var drawingGroup = SafeZone.BeginDrawingGroup(h.Mechanism);
            h.Update(deltaTime);
        }
    }

    protected override void OnStop()
    {
        foreach (var h in _handlers)
        {
            try { h.Cleanup(); }
            catch (Exception ex) { LogError($"【斗兽棋高一】{h.Mechanism} 清理失败：{ex.Message}"); }
        }
        try { _tracker.Clear(); }
        catch (Exception ex) { LogError($"【斗兽棋高一】读条追踪清理失败：{ex.Message}"); }
        // SetArenaBoundary 是手工直调本体 API（门面不含），必须自行清理
        try { MasterPlayerPlugin.Instance!.SafeZone.Registry.ClearArenaBoundary(); }
        catch (Exception ex) { LogError($"【斗兽棋高一】场地边界清理失败：{ex.Message}"); }
        _activeGroups.Clear();
        _boundaryOwner = BattleGroup.None;
        _fenceHandles.Clear();
        _fenceUsers.Clear();
    }

    // ════════════════ 激活状态机（规格 §3.5：按 NameID 扫描）════════════════

    private void ScanTick()
    {
        _seenGroups.Clear();
        _seenInfo.Clear();
        for (var i = 0; i < Svc.Objects.Length; i++)
        {
            if (Svc.Objects[i] is not IBattleChara bc || bc.IsDead)
                continue;
            if (GroupForNameId(bc.NameId) is not { } group)
                continue;
            _seenGroups.Add(group);
            if (!_seenInfo.ContainsKey(group))
                _seenInfo[group] = (bc.NameId, bc.Position);
        }

        foreach (var group in _seenGroups)
            if (!_activeGroups.Contains(group))
                ActivateGroup(group);

        _deactivateScratch.Clear();
        foreach (var group in _activeGroups)
            if (!_seenGroups.Contains(group))
                _deactivateScratch.Add(group);
        foreach (var group in _deactivateScratch)
            DeactivateGroup(group);
    }

    private static BattleGroup? GroupForNameId(uint nameId) => nameId switch
    {
        NameOwl or NameOwlFeather => BattleGroup.B22,
        >= NameMorbol and <= NameMorbolSap => BattleGroup.B23,
        >= NameCorpseFlower and <= NameQueenWasp => BattleGroup.B24,
        NameIceDragon or NameIceSprite => BattleGroup.B25,
        NameGargoyle or NameMiasmaBall => BattleGroup.B26,
        NameManager or NameClockworkInfantry or NamePanicArmor or NameDrone or NameCobra => BattleGroup.B27,
        NameGolem => BattleGroup.B28,
        >= NameTreant and <= NameBiloko => BattleGroup.B29,
        >= NameBombQueen and <= NameFlameBall => BattleGroup.B30,
        >= NameGrudgeDragon and <= NamePoisonCloud => BattleGroup.B31,
        _ => null,
    };

    private void ActivateGroup(BattleGroup group)
    {
        var h = _handlerByGroup[group];
        var (nameId, pos) = _seenInfo[group];
        h.LockedArena = group == BattleGroup.B27 ? null : FindArena(nameId, pos);
        h.ArenaY = pos.Y;
        _activeGroups.Add(group);
        if (h.LockedArena is { } arena)
            AcquireFence(arena.FenceIndex, pos.Y);
        RefreshBoundary();
        using var drawingGroup = SafeZone.BeginDrawingGroup(h.Mechanism);
        h.Activate(pos);
        Log($"【斗兽棋高一】检测到 {h.Mechanism}（场地：{h.LockedArena?.Label ?? "无固定"}），机制已激活");
    }

    private void DeactivateGroup(BattleGroup group)
    {
        var h = _handlerByGroup[group];
        try { h.Cleanup(); }
        catch (Exception ex) { LogError($"【斗兽棋高一】{h.Mechanism} 收尾清理失败：{ex.Message}"); }
        _tracker.CancelGroup(group);
        _activeGroups.Remove(group);
        if (h.LockedArena is { } arena)
            ReleaseFence(arena.FenceIndex);
        h.LockedArena = null;
        RefreshBoundary();
        Log($"【斗兽棋高一】{h.Mechanism} 已全灭，该组句柄已清理");
    }

    // B29 树精场 / B31 龙场按 NameID 特判，其余按主怪位置就近（60y 内）
    private static ArenaDef? FindArena(uint nameId, Vector3 pos)
    {
        if (nameId is >= NameTreant and <= NameBiloko)
            return ArenaTreantField;
        if (nameId is >= NameGrudgeDragon and <= NamePoisonCloud)
            return ArenaDragonField;
        ArenaDef? best = null;
        var bestDistSq = 60f * 60f;
        foreach (var field in ArenaFields)
        {
            var distSq = PromeRotation.Helpers.GeometryHelper.DistanceSquaredXZ(pos, field.Center);
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = field;
            }
        }
        return best;
    }

    // ════════════════ 场地边界 / 电网（规格 §2）════════════════

    private void RefreshBoundary()
    {
        foreach (var h in _handlers)
        {
            if (!_activeGroups.Contains(h.Group) || h.LockedArena is not { } arena)
                continue;
            if (_boundaryOwner != h.Group)
            {
                _boundaryOwner = h.Group;
                SetBoundaryShape(MakeBoundary(arena, h.ArenaY));
            }
            return;
        }
        if (_activeGroups.Contains(BattleGroup.B27))
        {
            _boundaryOwner = BattleGroup.B27; // 无电网：R40 圆跟随质心，由 B27 handler 周期刷新
            return;
        }
        if (_boundaryOwner != BattleGroup.None)
        {
            _boundaryOwner = BattleGroup.None;
            SetBoundaryShape(null);
        }
    }

    private static IGeometryShape MakeBoundary(ArenaDef arena, float y) => arena.Shape switch
    {
        ArenaShape.Circle => new CircleGeometry(new Vector3(arena.Center.X, y, arena.Center.Z), 20f),
        ArenaShape.Square => new RectangleGeometry(new Vector3(arena.Center.X, y, arena.Center.Z), 0f, 40f, 40f),
        ArenaShape.Rect => new RectangleGeometry(new Vector3(arena.Center.X, y, arena.Center.Z), 0f, 40f, 30f),
        ArenaShape.Dragon => new CircleGeometry(new Vector3(arena.Center.X, y, arena.Center.Z), 19.05f),
        ArenaShape.Treant => new CircleGeometry(new Vector3(arena.Center.X, y, arena.Center.Z), 19.7f),
        _ => throw new ArgumentOutOfRangeException(nameof(arena)),
    };

    private void SetBoundaryShape(IGeometryShape? shape)
    {
        try
        {
            if (shape is null)
                MasterPlayerPlugin.Instance!.SafeZone.Registry.ClearArenaBoundary();
            else
                MasterPlayerPlugin.Instance!.SafeZone.Registry.SetArenaBoundary(shape);
        }
        catch (Exception ex)
        {
            LogError($"【斗兽棋高一】设置场地边界失败：{ex.Message}");
        }
    }

    private void AcquireFence(int fenceIndex, float y)
    {
        if (fenceIndex < 0)
            return;
        _fenceY[fenceIndex] = y;
        if (_fenceUsers.TryGetValue(fenceIndex, out var users))
        {
            _fenceUsers[fenceIndex] = users + 1;
            return;
        }
        _fenceUsers[fenceIndex] = 1;
        // 与 BMR 一致：开包通常先于模块构造，默认按"开"注册
        if (!_fenceHandles.ContainsKey(fenceIndex))
            _fenceHandles[fenceIndex] = RegisterFenceShapes(fenceIndex, y);
    }

    private void ReleaseFence(int fenceIndex)
    {
        if (fenceIndex < 0 || !_fenceUsers.TryGetValue(fenceIndex, out var users))
            return;
        if (users > 1)
        {
            _fenceUsers[fenceIndex] = users - 1;
            return;
        }
        _fenceUsers.Remove(fenceIndex);
        RemoveFence(fenceIndex);
    }

    private void SetFenceState(int fenceIndex, bool on)
    {
        if (!_fenceUsers.ContainsKey(fenceIndex))
            return;
        if (on)
        {
            if (!_fenceHandles.ContainsKey(fenceIndex))
                _fenceHandles[fenceIndex] = RegisterFenceShapes(fenceIndex, _fenceY.GetValueOrDefault(fenceIndex));
        }
        else
        {
            RemoveFence(fenceIndex);
        }
    }

    private void RemoveFence(int fenceIndex)
    {
        if (!_fenceHandles.Remove(fenceIndex, out var handles))
            return;
        foreach (var handle in handles)
            SafeZone.Remove(handle);
    }

    private List<SafeZoneAreaHandle> RegisterFenceShapes(int fenceIndex, float y)
    {
        using var drawingGroup = SafeZone.BeginDrawingGroup("场地电网");
        var handles = new List<SafeZoneAreaHandle>();
        switch (fenceIndex)
        {
            case 0: // 圆场/树精场：R20~23 电网环
                handles.Add(RegisterPersistentDanger(
                    new DonutTemplate(20f, 23f),
                    new StaticTransform(new Vector3(120f, y, -420f), 0f)));
                break;
            case 1: // 方场 40×40，电网外扩 3y
                AddRectFenceBands(handles, new Vector3(120f, y, 0f), 20f, 20f);
                break;
            case 2: // 矩形场 40×30，电网外扩 3y
                AddRectFenceBands(handles, new Vector3(520f, y, 0f), 20f, 15f);
                break;
        }
        return handles;
    }

    // 四条矩形电网带（BMR CrucibleArena.Boundary：上下带宽 2×(halfW+3)，左右带宽 2×halfH）
    private void AddRectFenceBands(List<SafeZoneAreaHandle> handles, Vector3 center, float halfW, float halfH)
    {
        const float band = 3f;
        var topBottomWidth = (halfW + band) * 2f;
        var sideWidth = halfH * 2f;
        handles.Add(RegisterPersistentDanger(RayRectangleTemplate.Symmetric(topBottomWidth, band),
            new StaticTransform(center + new Vector3(0f, 0f, halfH), 0f)));
        handles.Add(RegisterPersistentDanger(RayRectangleTemplate.Symmetric(topBottomWidth, band),
            new StaticTransform(center - new Vector3(0f, 0f, halfH), MathF.PI)));
        handles.Add(RegisterPersistentDanger(RayRectangleTemplate.Symmetric(sideWidth, band),
            new StaticTransform(center + new Vector3(halfW, 0f, 0f), MathF.PI / 2f)));
        handles.Add(RegisterPersistentDanger(RayRectangleTemplate.Symmetric(sideWidth, band),
            new StaticTransform(center - new Vector3(halfW, 0f, 0f), -MathF.PI / 2f)));
    }

    // ════════════════ 事件分发（仅活跃且开启的组）════════════════

    private bool IsLive(GroupHandler h) => _activeGroups.Contains(h.Group) && IsMechanismEnabled(h.Mechanism);

    private void DispatchHandlers(Action<GroupHandler> callback)
    {
        foreach (var h in _handlers)
            if (IsLive(h))
            {
                using var drawingGroup = SafeZone.BeginDrawingGroup(h.Mechanism);
                _tracker.CurrentGroup = h.Group; // 注册条目打组标记，供 CancelGroup 按组撤除
                try { callback(h); }
                finally { _tracker.CurrentGroup = BattleGroup.None; }
            }
    }

    private void DispatchCastStart(LogSystemCastStartEvent e)
        => DispatchHandlers(h => h.OnCastStart(e));

    private void DispatchCastEnd(uint sourceId, uint actionId)
    {
        _tracker.Cancel(sourceId, actionId);
        DispatchHandlers(h => h.OnCastCancel(sourceId, actionId));
    }

    private void DispatchActionEffect(LogSystemActionEffectEvent e)
        => DispatchHandlers(h => h.OnEffect(e));

    private void DispatchTether(LogSystemTetherEvent e)
        => DispatchHandlers(h => h.OnTether(e));

    private void DispatchTetherCancel(LogSystemTetherCancelEvent e)
        => DispatchHandlers(h => h.OnTetherCancel(e));

    private void DispatchEObjAnim(LogSystemEObjAnimationEvent e)
        => DispatchHandlers(h => h.OnEObjAnim(e));

    private void DispatchEObjState(LogSystemEObjSetStateEvent e)
        => DispatchHandlers(h => h.OnEObjState(e));

    private void DispatchMapEffect(LogSystemMapEffectEvent e)
    {
        var state = e.Param1 | ((uint)e.Param2 << 16);
        if ((state is MapEffectOn or MapEffectOff) && e.Position <= 2)
            SetFenceState((int)e.Position, state == MapEffectOn);
        DispatchHandlers(h => h.OnMapEffect(e.Position, state));
    }

    // ════════════════ 宿主辅助（供 handler 使用）════════════════

    // 危险区统一注册：圆/月环半径 +0.35，生效提前 0.25s（规格 §0 全局余量）
    private SafeZoneAreaHandle RegisterDanger(GeometryTemplate template, TransformBinding binding, float delaySeconds, float hitDurationSeconds = 0.6f, string? drawingGroup = null)
    {
        using var scope = drawingGroup is null ? null : SafeZone.BeginDrawingGroup(drawingGroup);
        return SafeZone.AddDelayedHit(ApplyMargin(template), binding, MathF.Max(0.05f, delaySeconds - DangerTimeMargin), hitDurationSeconds);
    }

    private SafeZoneAreaHandle RegisterPersistentDanger(GeometryTemplate template, TransformBinding binding)
        => SafeZone.AddPersistentDanger(ApplyMargin(template), binding);

    private static GeometryTemplate ApplyMargin(GeometryTemplate template) => template switch
    {
        CircleTemplate c => new CircleTemplate(c.Radius + DangerRadiusMargin),
        DonutTemplate d => new DonutTemplate(MathF.Max(0f, d.InnerRadius - DangerRadiusMargin), d.OuterRadius + DangerRadiusMargin),
        _ => template,
    };

    // 文字提示：按 key 节流 10s，受"文字提示"开关控制
    private void Hint(string key, string text)
    {
        if (!TextHintsOn)
            return;
        var now = DateTime.UtcNow;
        if (_hintLast.TryGetValue(key, out var last) && (now - last).TotalSeconds < HintThrottleSeconds)
            return;
        _hintLast[key] = now;
        Log(text);
    }

    private bool IsLocalPlayer(uint entityId) => Svc.Objects.LocalPlayer is { } player && player.EntityId == entityId;

    private static float DistanceXZ(Vector3 a, Vector3 b)
        => PromeRotation.Helpers.GeometryHelper.DistanceXZ(a, b);

    private static string ActionDrawingName(uint actionId)
    {
        var name = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(actionId)?.Name.ToString();
        return $"{(string.IsNullOrWhiteSpace(name) ? "读条 AOE" : name)} ({actionId})";
    }

    // EObj EventState（FFCS GameObject+0x70）；7 = 物件休眠/消失
    private static unsafe byte EventStateOf(IGameObject obj)
    {
        try
        {
            return obj.Address == nint.Zero ? (byte)7 : ((FfxivGameObject*)obj.Address)->EventState;
        }
        catch
        {
            return 7;
        }
    }

    // ════════════════ 基础设施：读条 AoE 追踪（规格 §3.1）════════════════

    private sealed class CastAoeTracker(CrucibleHigh1Script host)
    {
        private sealed record Entry(SafeZoneAreaHandle Handle, DateTime ActivationUtc, DateTime ExpireUtc, Vector3 Position, BattleGroup Group);

        private readonly Dictionary<(uint Caster, uint Action), List<Entry>> _entries = new();
        private readonly List<(uint, uint)> _scratch = [];

        // 注册时由事件分发器标记当前处理的组，供按组撤除（机制开关中途关闭/组全灭收尾）
        public BattleGroup CurrentGroup = BattleGroup.None;

        // 读条 → 形状 + 绑定 → AddDelayedHit。多波共存语义（BMR Icefall 注释：波次 2.2s 间隔同屏共存）：
        // 同 (caster, action) 键下，仅当新读条的 activation（±0.75s）与落点（≤1y）都接近已有项时视为重复读条、刷新旧句柄；
        // 时间窗相差更大且落点不同的读条视为并存波次，各持句柄共存。取消/中断事件按键撤全部。
        public SafeZoneAreaHandle Register(LogSystemCastStartEvent e, GeometryTemplate template, TransformBinding binding, float extraDelay = 0f, float hitDuration = 0.6f)
        {
            var key = ((uint)e.SourceId, (uint)e.ActionId);
            var delay = e.CastTime + NpcCastPadding + extraDelay;
            var now = DateTime.UtcNow;
            var activation = now.AddSeconds(delay);
            if (!_entries.TryGetValue(key, out var list))
                _entries[key] = list = [];
            var index = list.FindIndex(x =>
                Math.Abs((x.ActivationUtc - activation).TotalSeconds) <= 0.75
                && DistanceXZ(x.Position, e.EffectPosition) <= 1f);
            if (index >= 0)
            {
                host.SafeZone.Remove(list[index].Handle);
                list.RemoveAt(index);
            }
            var handle = host.RegisterDanger(template, binding, delay, hitDuration, ActionDrawingName(e.ActionId));
            list.Add(new Entry(handle, activation, activation.AddSeconds(hitDuration + 2.0), e.EffectPosition, CurrentGroup));
            return handle;
        }

        public void Cancel(uint casterId, uint actionId)
        {
            if (!_entries.Remove((casterId, actionId), out var list))
                return;
            foreach (var entry in list)
                host.SafeZone.Remove(entry.Handle);
        }

        public void CancelGroup(BattleGroup group)
        {
            if (_entries.Count == 0)
                return;
            _scratch.Clear();
            foreach (var (key, list) in _entries)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                    if (list[i].Group == group)
                    {
                        host.SafeZone.Remove(list[i].Handle);
                        list.RemoveAt(i);
                    }
                if (list.Count == 0)
                    _scratch.Add(key);
            }
            foreach (var key in _scratch)
                _entries.Remove(key);
        }

        public void Purge()
        {
            if (_entries.Count == 0)
                return;
            var now = DateTime.UtcNow;
            _scratch.Clear();
            foreach (var (key, list) in _entries)
            {
                list.RemoveAll(x => x.ExpireUtc < now);
                if (list.Count == 0)
                    _scratch.Add(key);
            }
            foreach (var key in _scratch)
                _entries.Remove(key);
        }

        public void Clear()
        {
            foreach (var (_, list) in _entries)
                foreach (var entry in list)
                    host.SafeZone.Remove(entry.Handle);
            _entries.Clear();
        }
    }

    // ════════════════ 基础设施：常驻池监视（规格 §3.2，EObj → 持续危险区）════════════════

    private sealed class VoidzoneWatcher(CrucibleHigh1Script host, uint dataId, float radius, string drawingGroup, float armDelaySeconds = 0f)
    {
        private sealed class PoolState
        {
            public DateTime AppearedUtc;
            public SafeZoneAreaHandle Handle;
            public Vector3 Pos;
        }

        private readonly Dictionary<uint, PoolState> _pools = new();
        private readonly List<(uint Id, Vector3 Pos)> _alive = [];
        private readonly List<uint> _scratch = [];
        private float _accum;

        public IReadOnlyList<(uint Id, Vector3 Pos)> Alive => _alive;

        public void ActivateImmediately(uint entityId)
        {
            if (!_pools.TryGetValue(entityId, out var p) || p.Handle.IsValid)
                return;
            using var scope = host.SafeZone.BeginDrawingGroup(drawingGroup);
            p.Handle = host.SafeZone.AddPersistentDanger(new CircleTemplate(radius), new StaticTransform(p.Pos, 0f));
        }

        public void Update(float deltaTime)
        {
            _accum += deltaTime;
            if (_accum < 0.3f)
                return;
            _accum = 0f;
            var now = DateTime.UtcNow;
            _alive.Clear();
            for (var i = 0; i < Svc.Objects.Length; i++)
            {
                var obj = Svc.Objects[i];
                if (obj is not { ObjectKind: ObjectKind.EventObj } || obj.BaseId != dataId)
                    continue;
                if (EventStateOf(obj) == 7)
                    continue; // 休眠/消失：下方统一清理
                var id = obj.EntityId;
                _alive.Add((id, obj.Position));
                if (!_pools.TryGetValue(id, out var p))
                {
                    p = new PoolState { AppearedUtc = now, Pos = obj.Position };
                    _pools[id] = p;
                }
                if (!p.Handle.IsValid && (now - p.AppearedUtc).TotalSeconds >= armDelaySeconds)
                    ActivateImmediately(id);
            }
            _scratch.Clear();
            foreach (var (id, _) in _pools)
                if (!_alive.Exists(a => a.Id == id))
                    _scratch.Add(id);
            foreach (var id in _scratch)
                if (_pools.Remove(id, out var p) && p.Handle.IsValid)
                    host.SafeZone.Remove(p.Handle);
        }

        public void Cleanup()
        {
            foreach (var (_, p) in _pools)
                if (p.Handle.IsValid)
                    host.SafeZone.Remove(p.Handle);
            _pools.Clear();
            _alive.Clear();
        }
    }

    // ════════════════ 基础设施：连线绘制（B22/B24 用，<20y 红 ≥20y 绿）════════════════

    private sealed class TetherDrawer(CrucibleHigh1Script host, string idPrefix, uint[] sourceNameIds, string hintKey, string hintText)
    {
        private readonly HashSet<(uint Src, uint Dst)> _links = [];
        private readonly List<(uint Src, uint Dst)> _scratch = [];
        private float _accum;

        public void OnTether(LogSystemTetherEvent e)
        {
            var src = (uint)e.SourceId;
            var dst = (uint)e.TargetId;
            if (!IsWatched(src))
                return;
            _links.Add((src, dst));
            Redraw(src, dst);
            if (host.IsLocalPlayer(dst))
                host.Hint(hintKey, hintText);
        }

        public void OnTetherCancel(LogSystemTetherCancelEvent e)
        {
            var src = (uint)e.SourceId;
            var dst = (uint)e.TargetId;
            if (_links.Remove((src, dst)))
                host.Draw.Remove($"{idPrefix}_tether_{src}_{dst}");
        }

        public void Update(float deltaTime)
        {
            _accum += deltaTime;
            if (_accum < 0.5f)
                return;
            _accum = 0f;
            if (_links.Count == 0)
                return;
            if (!host.TetherLinesOn)
            {
                Cleanup();
                return;
            }
            _scratch.Clear();
            foreach (var link in _links)
                if (Svc.Objects.SearchByEntityId(link.Src) is not { IsDead: false }
                    || Svc.Objects.SearchByEntityId(link.Dst) is not { IsDead: false })
                    _scratch.Add(link);
            foreach (var link in _scratch)
            {
                _links.Remove(link);
                host.Draw.Remove($"{idPrefix}_tether_{link.Src}_{link.Dst}");
            }
            foreach (var (src, dst) in _links)
                Redraw(src, dst);
        }

        public void Cleanup()
        {
            foreach (var (src, dst) in _links)
                host.Draw.Remove($"{idPrefix}_tether_{src}_{dst}");
            _links.Clear();
        }

        private bool IsWatched(uint entityId)
            => Svc.Objects.SearchByEntityId(entityId) is IBattleChara bc && sourceNameIds.Contains(bc.NameId);

        private void Redraw(uint src, uint dst)
        {
            if (!host.TetherLinesOn)
                return;
            var a = Svc.Objects.SearchByEntityId(src);
            var b = Svc.Objects.SearchByEntityId(dst);
            if (a == null || b == null)
                return;
            var style = !host.IsLocalPlayer(dst)
                ? DrawStyle.Default // BMR：他人连线画中性色，只有本地玩家目标按距离变色
                : DistanceXZ(a.Position, b.Position) < 20f ? DrawStyle.Danger : DrawStyle.Safe;
            host.Draw.AddTether($"{idPrefix}_tether_{src}_{dst}", src, dst, style);
        }
    }

    // ════════════════ Handler 基类 ════════════════

    private abstract class GroupHandler(CrucibleHigh1Script host)
    {
        protected readonly CrucibleHigh1Script H = host;
        public abstract BattleGroup Group { get; }
        public abstract string Mechanism { get; }
        public abstract uint[] NameIds { get; }
        public ArenaDef? LockedArena { get; set; }
        public float ArenaY { get; set; }
        public Vector3 ArenaCenter { get; private set; }

        public void Activate(Vector3 primaryPos)
        {
            ArenaCenter = LockedArena is { } arena
                ? new Vector3(arena.Center.X, ArenaY, arena.Center.Z)
                : primaryPos;
            OnActivate(primaryPos);
        }

        protected virtual void OnActivate(Vector3 primaryPos) { }
        public virtual void OnCastStart(LogSystemCastStartEvent e) { }
        public virtual void OnCastCancel(uint sourceId, uint actionId) { }
        public virtual void OnEffect(LogSystemActionEffectEvent e) { }
        public virtual void OnTether(LogSystemTetherEvent e) { }
        public virtual void OnTetherCancel(LogSystemTetherCancelEvent e) { }
        public virtual void OnEObjAnim(LogSystemEObjAnimationEvent e) { }
        public virtual void OnEObjState(LogSystemEObjSetStateEvent e) { }
        public virtual void OnMapEffect(uint index, uint state) { }
        public virtual void Update(float deltaTime) { }
        public virtual void Cleanup() { }

        protected static Vector3 CastOrigin(LogSystemCastStartEvent e) => e.SourcePosition ?? e.EffectPosition;
        protected static StaticTransform At(Vector3 pos, float rotation = 0f) => new(pos, rotation);

        protected static FollowEntityPositionAndRotation FollowCaster(ulong entityId)
            => new((uint)entityId, Vector2.Zero, MissingEntityPolicy.KeepLastResolved);

        protected void RemoveIfValid(ref SafeZoneAreaHandle handle)
        {
            if (handle.IsValid)
                H.SafeZone.Remove(handle);
            handle = default;
        }
    }


    // ════════════════ B22 博学林鸮（圆场 R20）════════════════

    private sealed class B22OwlHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B22;
        public override string Mechanism => MechB22;
        public override uint[] NameIds => [NameOwl, NameOwlFeather];

        private readonly TetherDrawer _tethers = new(host, "b22", [NameOwl, NameOwlFeather],
            "b22_tether", "【B22 林鸮】拉线：与博学林鸮/羽毛连线中，注意随后的暴风击退！");
        private readonly Dictionary<uint, GustState> _gusts = new();
        private readonly List<uint> _scratch = [];
        private ChapterState? _chapter;
        private float _gustDrawAccum;

        private sealed class GustState
        {
            public Vector3 Source;
            public DateTime ExpiryUtc;
        }

        private sealed class ChapterState
        {
            public ushort ActionId;
            public uint PadDataId;
            public DateTime ShowAtUtc;
            public DateTime ActivationUtc;
            public bool Shown;
            public readonly List<SafeZoneAreaHandle> PadHandles = [];
        }

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB22Drop: // 掉落：R10 落点圆（多波共存，全注册）
                    H.Tracker.Register(e, new CircleTemplate(10f), At(e.EffectPosition));
                    break;
                case AidB22ManaBurst: // 魔力爆散：R15 落点圆
                    H.Tracker.Register(e, new CircleTemplate(15f), At(e.EffectPosition));
                    break;
                case AidB22Gust: // 暴风：击退 25y（降级：提示 + 源点 + 反向提示线）
                    StartGust(e);
                    break;
                case AidB22ChapterLevitate:
                case AidB22ChapterKappa:
                case AidB22ChapterTransform:
                    StartChapter(e);
                    break;
                case AidB22Focus: // 精神集中
                    H.Hint("b22_focus", "【B22 林鸮】精神集中：单体魔法增伤，注意减伤");
                    break;
                case AidB22ChapterHint: // 章节提醒
                    H.Hint("b22_chapter_hint", "【B22 林鸮】章节魔法：利用场地效果应对，注意减伤！");
                    break;
            }
        }

        public override void OnCastCancel(uint sourceId, uint actionId)
        {
            if (actionId == AidB22Gust)
                EndGust(sourceId);
            if (_chapter is { } c && actionId == c.ActionId)
                ClearChapter();
        }

        public override void OnEffect(LogSystemActionEffectEvent e)
        {
            if (e.ActionId == AidB22Gust)
                EndGust((uint)e.SourceId);
            // 判定后撤掉安全垫引导（0xBE13 的判定事件是魔法锤 0xBE14）
            if (_chapter is { } c && e.ActionId == (c.ActionId == AidB22ChapterTransform ? AidB22HammerHit : c.ActionId))
                ClearChapter();
        }

        public override void OnTether(LogSystemTetherEvent e) => _tethers.OnTether(e);
        public override void OnTetherCancel(LogSystemTetherCancelEvent e) => _tethers.OnTetherCancel(e);

        public override void Update(float deltaTime)
        {
            _tethers.Update(deltaTime);
            var now = DateTime.UtcNow;
            if (_gusts.Count > 0)
            {
                _gustDrawAccum += deltaTime;
                var redraw = _gustDrawAccum >= 0.25f;
                if (redraw)
                    _gustDrawAccum = 0f;
                _scratch.Clear();
                foreach (var (caster, gust) in _gusts)
                {
                    if (now > gust.ExpiryUtc)
                    {
                        _scratch.Add(caster);
                        continue;
                    }
                    if (redraw)
                        RedrawGustLine(caster, gust);
                }
                foreach (var caster in _scratch)
                    EndGust(caster);
            }
            UpdateChapter(now);
        }

        public override void Cleanup()
        {
            _tethers.Cleanup();
            _scratch.Clear();
            _scratch.AddRange(_gusts.Keys);
            foreach (var caster in _scratch)
                EndGust(caster);
            ClearChapter();
        }

        // 暴风 0xBE1B：读条期间画源点红圈 R2 + 玩家→源点反方向 25y 提示线
        private void StartGust(LogSystemCastStartEvent e)
        {
            var caster = (uint)e.SourceId;
            var seconds = e.CastTime + NpcCastPadding + 1f;
            _gusts[caster] = new GustState { Source = e.EffectPosition, ExpiryUtc = DateTime.UtcNow.AddSeconds(seconds) };
            H.Draw.Add($"b22_gust_src_{caster}", new CircleGeometry(e.EffectPosition, 2f), DrawStyle.Danger, seconds * 1000f);
            RedrawGustLine(caster, _gusts[caster]);
            H.Hint("b22_gust", "【B22 林鸮】暴风击退 25y：优先击杀读条羽毛，否则提前靠场心");
        }

        private void RedrawGustLine(uint caster, GustState gust)
        {
            if (Svc.Objects.LocalPlayer is not { } player)
                return;
            var dir = player.Position - gust.Source;
            dir.Y = 0f;
            if (dir.LengthSquared() < 0.25f)
                return;
            dir = Vector3.Normalize(dir);
            H.Draw.AddTether($"b22_gust_line_{caster}", player.EntityId, player.Position + dir * 25f, DrawStyle.Danger, 400f);
        }

        private void EndGust(uint caster)
        {
            if (!_gusts.Remove(caster))
                return;
            H.Draw.Remove($"b22_gust_src_{caster}");
            H.Draw.Remove($"b22_gust_line_{caster}");
        }

        // 章节魔法 0xBE11/12/13：读条剩余 ≤3s 起在 pad 上注册安全圈，判定后撤
        private void StartChapter(LogSystemCastStartEvent e)
        {
            ClearChapter();
            var pad = e.ActionId switch
            {
                AidB22ChapterLevitate => EObjPadLevitate,
                AidB22ChapterKappa => EObjPadKappa,
                _ => EObjPadTransform,
            };
            _chapter = new ChapterState
            {
                ActionId = e.ActionId,
                PadDataId = pad,
                ShowAtUtc = DateTime.UtcNow.AddSeconds(MathF.Max(0f, e.CastTime - 3f)),
                ActivationUtc = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding + (e.ActionId == AidB22ChapterTransform ? 0.8f : 0f)),
            };
        }

        private void UpdateChapter(DateTime now)
        {
            if (_chapter is not { } c)
                return;
            if (!c.Shown && now >= c.ShowAtUtc && now <= c.ActivationUtc.AddSeconds(1.0))
                TryShowChapter(c, now);
            if (now > c.ActivationUtc.AddSeconds(1.5))
                ClearChapter();
        }

        private void TryShowChapter(ChapterState c, DateTime now)
        {
            using var scope = H.SafeZone.BeginDrawingGroup(ActionDrawingName(c.ActionId));
            var duration = (float)(c.ActivationUtc - now).TotalSeconds + 1f; // BMR Pads() 显示窗到判定 +1s
            var found = false;
            for (var i = 0; i < Svc.Objects.Length; i++)
            {
                var obj = Svc.Objects[i];
                if (obj is not { ObjectKind: ObjectKind.EventObj } || obj.BaseId != c.PadDataId || EventStateOf(obj) == 7)
                    continue;
                found = true;
                c.PadHandles.Add(H.SafeZone.AddTimedSafeArea(new CircleTemplate(6f), At(obj.Position), duration));
            }
            if (!found)
                return; // pad 尚未刷出，下一帧重试
            c.Shown = true;
            H.Hint($"b22_chapter_{c.ActionId}", c.ActionId switch
            {
                AidB22ChapterLevitate => "【B22 林鸮】爆震之章：进入浮空区域，等爆震判定后再离开！",
                AidB22ChapterKappa => "【B22 林鸮】狂水之章：进入河童区域，等狂水判定后再离开！",
                _ => "【B22 林鸮】变身之章：进入变身区域应对魔法锤！",
            });
        }

        private void ClearChapter()
        {
            if (_chapter is not { } c)
                return;
            foreach (var handle in c.PadHandles)
                H.SafeZone.Remove(handle);
            _chapter = null;
        }
    }

    // ════════════════ B23 魔界花（圆场 R20）════════════════

    private sealed class B23MorbolHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B23;
        public override string Mechanism => MechB23;
        public override uint[] NameIds => [NameMorbol, NameMorbolSeedling, NameMorbolMouth, NameMorbolSap];

        private readonly VoidzoneWatcher _pools = new(host, EObjMorbolPool, 6f, "毒池");
        private BreathSequence? _breath;

        private sealed class BreathSequence
        {
            public ulong BossId;
            public DateTime FirstActivationUtc;
            public readonly List<(SafeZoneAreaHandle Handle, DateTime ActivationUtc)> Cones = [];
            public SafeZoneAreaHandle CloseArea;
        }

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            // 0xBE22 旋转视觉：忽略
            switch (e.ActionId)
            {
                case AidB23AcidMist: // 酸雾：R6 落点圆
                    H.Tracker.Register(e, new CircleTemplate(6f), At(e.EffectPosition));
                    break;
                case AidB23VineThrust: // 藤鞭穿刺：13×8 跟随施法者朝向
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(8f, 13f), FollowCaster(e.SourceId));
                    break;
                case AidB23BreathLeft:
                case AidB23BreathRight:
                    StartBreath(e);
                    break;
                case AidB23BreathHitA:
                case AidB23BreathHitB:
                    CorrectBreath(e);
                    break;
                case AidB23Quake: // 地震：击退 10y（降级：提示 + 画源点）
                    H.Draw.Add("b23_quake_src", new CircleGeometry(CastOrigin(e), 2f), DrawStyle.Danger, (e.CastTime + NpcCastPadding + 1f) * 1000f);
                    H.Hint("b23_quake", "【B23 魔界花】地震击退 10y：背对安全区，注意毒池");
                    break;
                case AidB23Missile: // 粘液飞弹点名（腐汁读条且目标是本地玩家）
                    if (H.IsLocalPlayer((uint)e.TargetId))
                        H.Hint("b23_missile", "【B23 魔界花】粘液飞弹锁定点名：注意减伤");
                    break;
                case AidB23Paralysis:
                    H.Hint("b23_paralysis", "【B23 魔界花】麻痹（单体）");
                    break;
                case AidB23Poison:
                    H.Hint("b23_poison", "【B23 魔界花】中毒（单体）");
                    break;
            }
        }

        public override void OnCastCancel(uint sourceId, uint actionId)
        {
            if (actionId is AidB23BreathLeft or AidB23BreathRight)
                ClearBreath();
        }

        public override void Update(float deltaTime)
        {
            _pools.Update(deltaTime);
            if (_breath is { } seq)
            {
                var now = DateTime.UtcNow;
                seq.Cones.RemoveAll(c => c.ActivationUtc.AddSeconds(1.0) < now);
                if (seq.Cones.Count == 0)
                    ClearBreath();
            }
        }

        public override void Cleanup()
        {
            ClearBreath();
            _pools.Cleanup();
        }

        // 恶臭气五连锥：4.2s 视觉读条开始即注册 5 道。顶点 = 场地中心（转向读条挂在 Boss 本体上，
        // 实际伤害由场心辅助体发出）；朝向基准 = 转向读条（第一个读条）的朝向 + i×45° 步进，每 2.1s 一道
        private void StartBreath(LogSystemCastStartEvent e)
        {
            using var scope = H.SafeZone.BeginDrawingGroup("恶臭气五连锥");
            var now = DateTime.UtcNow;
            var firstActivation = now.AddSeconds(e.CastTime + NpcCastPadding + 0.5f);
            if (_breath is { } seq && seq.BossId == e.SourceId
                && Math.Abs((firstActivation - seq.FirstActivationUtc).TotalSeconds) < 0.75)
                return; // 同 Boss ±0.75s 重复读条
            ClearBreath();
            var origin = ArenaCenter;
            var step = e.ActionId == AidB23BreathLeft ? -Step45 : Step45;
            seq = new BreathSequence { BossId = e.SourceId, FirstActivationUtc = firstActivation };
            for (var i = 0; i < 5; i++)
            {
                var delay = e.CastTime + NpcCastPadding + 0.5f + i * 2.1f;
                var handle = H.RegisterDanger(new FanTemplate(50f, Fan90), At(origin, e.SourceRotation + i * step), delay, 0.5f);
                seq.Cones.Add((handle, now.AddSeconds(delay)));
            }
            // 首道期间的"贴近"安全区：环带 4-11（圆心是五道锥的公共顶点，任何方向都被覆盖，排除圆心）
            seq.CloseArea = H.SafeZone.AddTimedSafeArea(
                new DonutTemplate(4f, 11f), At(origin),
                e.CastTime + NpcCastPadding + 0.5f + 0.6f);
            _breath = seq;
            H.Hint("b23_breath", "【B23 魔界花】恶臭气五连锥：贴近场心绿圈，随旋转方向横移");
        }

        // 0xBE21/0xBE23 实际伤害读条：按最近道 ±1s 窗口校正
        private void CorrectBreath(LogSystemCastStartEvent e)
        {
            using var scope = H.SafeZone.BeginDrawingGroup("恶臭气五连锥");
            var now = DateTime.UtcNow;
            var activation = now.AddSeconds(e.CastTime + NpcCastPadding);
            if (_breath is { } seq)
            {
                var index = seq.Cones.FindIndex(c => Math.Abs((c.ActivationUtc - activation).TotalSeconds) < 1.0);
                if (index >= 0)
                {
                    H.SafeZone.Remove(seq.Cones[index].Handle);
                    seq.Cones[index] = (H.RegisterDanger(new FanTemplate(50f, Fan90), At(CastOrigin(e), e.SourceRotation), e.CastTime + NpcCastPadding, 0.5f), activation);
                    return;
                }
                if (seq.Cones.Count > 0)
                    return;
            }
            // 无序列（迟到加载）：直接按实际读条注册
            H.RegisterDanger(new FanTemplate(50f, Fan90), At(CastOrigin(e), e.SourceRotation), e.CastTime + NpcCastPadding, 0.5f);
        }

        private void ClearBreath()
        {
            if (_breath is not { } seq)
                return;
            foreach (var (handle, _) in seq.Cones)
                H.SafeZone.Remove(handle);
            if (seq.CloseArea.IsValid)
                H.SafeZone.Remove(seq.CloseArea);
            _breath = null;
        }
    }

    // ════════════════ B24 尸生花（圆场 R20）════════════════

    private sealed class B24CorpseFlowerHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B24;
        public override string Mechanism => MechB24;
        public override uint[] NameIds => [NameCorpseFlower, NameFlowerBud, NameQueenWasp];

        private readonly TetherDrawer _tethers = new(host, "b24", [NameCorpseFlower, NameFlowerBud, NameQueenWasp],
            "b24_tether", "【B24 尸生花】拉线：被尸生花/花苗/女王鹰蜂连线中！");

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB24AcidRain: // 酸雨：R6 落点圆
                    H.Tracker.Register(e, new CircleTemplate(6f), At(e.EffectPosition));
                    break;
                case AidB24RottenStench: // 腐烂恶臭：4.7s 视觉读条 → 45×12（读条位置/朝向固定，打断可撤）
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(12f, 45f), At(CastOrigin(e), e.SourceRotation), 0.2f, 2f);
                    break;
                case AidB24FlowerTrap:
                    H.Hint("b24_trap", "【B24 尸生花】鲜花陷阱：准备吸引与捕食！");
                    break;
                case AidB24Devour:
                    H.Hint("b24_devour", "【B24 尸生花】捕食（单体）：注意减伤");
                    break;
            }
        }

        public override void OnTether(LogSystemTetherEvent e) => _tethers.OnTether(e);
        public override void OnTetherCancel(LogSystemTetherCancelEvent e) => _tethers.OnTetherCancel(e);
        public override void Update(float deltaTime) => _tethers.Update(deltaTime);
        public override void Cleanup() => _tethers.Cleanup();
    }

    // ════════════════ B25 冰龙（圆场 R20）════════════════

    private sealed class B25IceDragonHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B25;
        public override string Mechanism => MechB25;
        public override uint[] NameIds => [NameIceDragon, NameIceSprite];

        private readonly VoidzoneWatcher _pools = new(host, EObjIcePool, 9f, "冰池");

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB25IceTormentA: // 冰寒折磨：100×15 跟随施法者朝向（多波共存全注册）
                case AidB25IceTormentB:
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(15f, 100f), FollowCaster(e.SourceId));
                    break;
                case AidB25Rot: // 无终的枯朽：R9 落点圆
                    H.Tracker.Register(e, new CircleTemplate(9f), At(e.EffectPosition));
                    break;
                case AidB25IceCover: // 寒冰覆盖：R5 落点圆
                    H.Tracker.Register(e, new CircleTemplate(5f), At(e.EffectPosition));
                    break;
                case AidB25Dive: // 低温俯冲：46×23（BMR 有意跳过，本移植补齐，尺寸待实测）
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(23f, 46f), FollowCaster(e.SourceId));
                    break;
            }
        }

        public override void Update(float deltaTime) => _pools.Update(deltaTime);
        public override void Cleanup() => _pools.Cleanup();
    }

    // ════════════════ B26 石像鬼（方场 40×40）════════════════

    private sealed class B26GargoyleHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B26;
        public override string Mechanism => MechB26;
        public override uint[] NameIds => [NameGargoyle, NameMiasmaBall];

        // 瘴气地雷（接触即爆语义 → R1.8 障碍）
        private readonly HashSet<uint> _armedMines = [];
        private readonly Dictionary<uint, SafeZoneAreaHandle> _mineObstacles = new();
        private readonly Dictionary<uint, DateTime> _pendingArmUtc = new();
        private readonly List<uint> _scratch = [];
        private float _mineAccum;
        // 圆环连斩（钢铁 R13 → 2s 后月环 6-30）
        private SafeZoneAreaHandle _steel;
        private SafeZoneAreaHandle _ring;
        private DateTime _steelExpireUtc;
        private DateTime _ringExpireUtc;
        // 冲锋双向半场斩
        private SafeZoneAreaHandle _cleaveA;
        private SafeZoneAreaHandle _cleaveB;
        private DateTime _cleaveAExpireUtc;
        private DateTime _cleaveBExpireUtc;
        private uint _chargeCaster;
        private bool _firstCleaveResolved;
        private readonly HashSet<uint> _chargeSeq = [];
        // 五连斩击退链（20y，降级：提示 + 场心安全区引导）
        private int _kbStage;
        private SafeZoneAreaHandle _kbGuide;
        private DateTime _kbExpireUtc;

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB26MiasmaPlace: // 瘴气布防：R6 落点圆
                    H.Tracker.Register(e, new CircleTemplate(6f), At(e.EffectPosition));
                    break;
                case AidB26DesolateNova: // 荒芜新星：60×7 跟随施法者朝向
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(7f, 60f), FollowCaster(e.SourceId));
                    break;
                case AidB26Asphalt: // 沥青圈：R6 落点圆
                    H.Tracker.Register(e, new CircleTemplate(6f), At(e.EffectPosition));
                    break;
                case AidB26ArmCast: // 布防读条：读条结束转移完成后布防
                    if (IsMiasmaBall((uint)e.SourceId))
                        _pendingArmUtc[(uint)e.SourceId] = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding);
                    break;
                case AidB26InOutPredict:
                    StartInOut(e);
                    break;
                case AidB26SteelActual: // 钢铁实际读条：按新读条校正
                    RemoveIfValid(ref _steel);
                    _steel = H.RegisterDanger(new CircleTemplate(13f), At(CastOrigin(e)), e.CastTime + NpcCastPadding, 0.6f, ActionDrawingName(AidB26SteelActual));
                    _steelExpireUtc = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding + 1.6);
                    // 已预测的月环保留（BMR 语义：0xBE51 月环实际读条 2s 后会再校正一次；规格文字已按此修订）
                    if (!_ring.IsValid)
                    {
                        _ring = H.RegisterDanger(new DonutTemplate(6f, 30f), At(CastOrigin(e)), e.CastTime + NpcCastPadding + 2f, 1.0f, ActionDrawingName(AidB26RingActual));
                        _ringExpireUtc = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding + 4.0);
                    }
                    break;
                case AidB26RingActual: // 月环实际读条：按新读条校正
                    RemoveIfValid(ref _ring);
                    _ring = H.RegisterDanger(new DonutTemplate(6f, 30f), At(CastOrigin(e)), e.CastTime + NpcCastPadding, 1.0f, ActionDrawingName(AidB26RingActual));
                    _ringExpireUtc = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding + 2.0);
                    H.Hint("b26_ring", "【B26 石像鬼】钢铁后靠近首领，躲避月环！");
                    break;
                case AidB26Chain1:
                case AidB26Chain2:
                case AidB26Chain3:
                case AidB26Chain4:
                    StartKbChain(e);
                    break;
                case AidB26RushHint: // 突进提示
                    H.Hint("b26_rush", "【B26 石像鬼】突进后连续斩击，注意首领朝向！");
                    break;
            }
        }

        public override void OnCastCancel(uint sourceId, uint actionId)
        {
            switch (actionId)
            {
                case AidB26ArmCast:
                    _pendingArmUtc.Remove(sourceId);
                    break;
                case AidB26InOutPredict:
                    ClearInOut();
                    break;
                case AidB26SteelActual:
                    RemoveIfValid(ref _steel);
                    break;
                case AidB26RingActual:
                    RemoveIfValid(ref _ring);
                    break;
                case AidB26Chain1:
                case AidB26Chain2:
                case AidB26Chain3:
                case AidB26Chain4:
                    _kbStage = 0;
                    RemoveIfValid(ref _kbGuide);
                    break;
            }
        }

        public override void OnEffect(LogSystemActionEffectEvent e)
        {
            if (e.ActionId == AidB26MineBlast) // 瘴气爆炸命中：清该球障碍
            {
                DisarmMine((uint)e.SourceId);
                return;
            }
            if (e.ActionId == AidB26ChargePack) // 冲锋效果包：注册双向半场斩
            {
                StartCharge(e);
                return;
            }
            if (e.ActionId == AidB26HalfCleave) // 两刀共用同一技能 ID，按去重后的命中推进
            {
                CorrectCharge(e);
                return;
            }
            if (e.ActionId == AidB26Chain2 && _kbStage == 0)
            {
                using var scope = H.SafeZone.BeginDrawingGroup("五连斩击退引导");
                // 0xC608 命中兜底：热重载错过链首读条时按 +9s 补预测（BMR 同）
                _kbStage = 2;
                _kbExpireUtc = DateTime.UtcNow.AddSeconds(11.0);
                RemoveIfValid(ref _kbGuide);
                _kbGuide = H.SafeZone.AddTimedSafeArea(new CircleTemplate(5f), At(ArenaCenter), 10f);
                H.Hint("b26_kb", "【B26 石像鬼】五连斩最后一击 20y 击退：提前靠中，避开瘴气与电网");
                return;
            }
            if (e.ActionId == AidB26Chain4) // 最后一击命中：击退链结束
            {
                _kbStage = 0;
                RemoveIfValid(ref _kbGuide);
            }
        }

        public override void OnTether(LogSystemTetherEvent e)
        {
            // 瘴气球与本体间 tether 0x01AA = 已布防。
            // 已知缺口：Dalamud 对象表不暴露 tether 状态，无法像 BMR 每帧重扫 actor.Tether 兜底；
            // 战斗中热重载后，此前已布防的地雷要等下一次布防事件/读条才恢复障碍。
            if (e.TetherId != TetherMiasma)
                return;
            if (IsMiasmaBall((uint)e.SourceId))
                ArmMine((uint)e.SourceId);
            else if (IsMiasmaBall((uint)e.TargetId))
                ArmMine((uint)e.TargetId);
        }

        public override void Update(float deltaTime)
        {
            var now = DateTime.UtcNow;
            if (_pendingArmUtc.Count > 0)
            {
                _scratch.Clear();
                foreach (var (id, at) in _pendingArmUtc)
                    if (now >= at)
                        _scratch.Add(id);
                foreach (var id in _scratch)
                {
                    _pendingArmUtc.Remove(id);
                    ArmMine(id);
                }
            }
            if (_steel.IsValid && now > _steelExpireUtc)
                RemoveIfValid(ref _steel);
            if (_ring.IsValid && now > _ringExpireUtc)
                RemoveIfValid(ref _ring);
            if (_cleaveA.IsValid && now > _cleaveAExpireUtc)
                RemoveIfValid(ref _cleaveA);
            if (_chargeCaster != 0 && now > _cleaveBExpireUtc)
                ClearCharge();
            if (_kbStage != 0 && now > _kbExpireUtc)
            {
                _kbStage = 0;
                RemoveIfValid(ref _kbGuide);
            }
            // 每 0.3s 兜底：清掉已消失的地雷
            _mineAccum += deltaTime;
            if (_mineAccum < 0.3f)
                return;
            _mineAccum = 0f;
            _scratch.Clear();
            foreach (var id in _armedMines)
                if (Svc.Objects.SearchByEntityId(id) is not { IsDead: false })
                    _scratch.Add(id);
            foreach (var id in _scratch)
                DisarmMine(id);
        }

        public override void Cleanup()
        {
            ClearInOut();
            ClearCharge();
            _chargeSeq.Clear();
            _kbStage = 0;
            RemoveIfValid(ref _kbGuide);
            _scratch.Clear();
            _scratch.AddRange(_armedMines);
            foreach (var id in _scratch)
                DisarmMine(id);
            _pendingArmUtc.Clear();
        }

        // 圆环连斩 0xBE4F：注册钢铁 圆 R13（读条剩余+0.3+0.6）+ 月环 6-30（钢铁+2s）
        private void StartInOut(LogSystemCastStartEvent e)
        {
            ClearInOut();
            var pos = CastOrigin(e);
            var steelDelay = e.CastTime + NpcCastPadding + 0.6f;
            _steel = H.RegisterDanger(new CircleTemplate(13f), At(pos), steelDelay, 0.6f, ActionDrawingName(AidB26SteelActual));
            _ring = H.RegisterDanger(new DonutTemplate(6f, 30f), At(pos), steelDelay + 2f, 1.0f, ActionDrawingName(AidB26RingActual));
            var now = DateTime.UtcNow;
            _steelExpireUtc = now.AddSeconds(steelDelay + 1.6);
            _ringExpireUtc = now.AddSeconds(steelDelay + 4.0);
            H.Hint("b26_inout", "【B26 石像鬼】圆环连斩：先远离 R13 钢铁，2s 内进怀中躲月环");
        }

        private void ClearInOut()
        {
            RemoveIfValid(ref _steel);
            RemoveIfValid(ref _ring);
        }

        // 冲锋效果包 0xC6F4：仅本体（SourceDataId=OID 0x4CC2）且落点在场地中心 25×25 内才采纳（BMR 双门槛）；
        // TargetXZ=冲刺落点，第一刀 60y 半场（2.8s），第二刀反向（4.85s）
        private void StartCharge(LogSystemActionEffectEvent e)
        {
            if (e.SourceDataId != OidGargoyle)
                return;
            if ((e.EffectPosition ?? e.SourcePosition) is not { } origin)
                return;
            if (MathF.Abs(origin.X - ArenaCenter.X) > 25f || MathF.Abs(origin.Z - ArenaCenter.Z) > 25f)
                return;
            if (!_chargeSeq.Add(e.GlobalSequence))
                return;
            var rot = e.SourceRotation ?? 0f;
            ClearCharge();
            _chargeCaster = (uint)e.SourceId;
            var now = DateTime.UtcNow;
            _cleaveA = H.RegisterDanger(new FanTemplate(60f, Fan180), At(origin, rot), 2.8f, 0.3f, "冲锋半场斩·第一刀");
            _cleaveAExpireUtc = now.AddSeconds(3.3);
            _cleaveB = H.RegisterDanger(new FanTemplate(60f, Fan180), At(origin, rot + MathF.PI), 4.85f, 0.3f, "冲锋半场斩·第二刀");
            _cleaveBExpireUtc = now.AddSeconds(5.35);
        }

        // 0xBE4E：第一刀移除并校正第二刀（+180°，2.05s）；第二刀命中即清空。
        private void CorrectCharge(LogSystemActionEffectEvent e)
        {
            if (_chargeCaster == 0 || !_chargeSeq.Add(e.GlobalSequence))
                return;
            var now = DateTime.UtcNow;
            if (_firstCleaveResolved || now > _cleaveAExpireUtc)
            {
                ClearCharge();
                return;
            }
            _firstCleaveResolved = true;
            RemoveIfValid(ref _cleaveA);
            if ((e.SourcePosition ?? e.EffectPosition) is not { } origin || e.SourceRotation is not { } rotation)
                return;
            RemoveIfValid(ref _cleaveB);
            _cleaveB = H.RegisterDanger(new FanTemplate(60f, Fan180), At(origin, rotation + MathF.PI), 2.05f, 0.3f, "冲锋半场斩·第二刀");
            _cleaveBExpireUtc = now.AddSeconds(2.55);
        }

        private void ClearCharge()
        {
            RemoveIfValid(ref _cleaveA);
            RemoveIfValid(ref _cleaveB);
            _chargeCaster = 0;
            _firstCleaveResolved = false;
        }

        // 五连斩击退链：0xC607→+9.5s / 0xC608→+9.0s / 0xBE54→+0.6s / 0xBE55→即刻
        private void StartKbChain(LogSystemCastStartEvent e)
        {
            using var scope = H.SafeZone.BeginDrawingGroup("五连斩击退引导");
            var (stage, extra) = e.ActionId switch
            {
                AidB26Chain1 => (1, 9.5f),
                AidB26Chain2 => (2, 9.0f),
                AidB26Chain3 => (3, 0.6f),
                _ => (4, 0.0f),
            };
            if (_kbStage != 0 && stage < _kbStage)
                return; // 不回退到更早的预测
            var now = DateTime.UtcNow;
            var activation = now.AddSeconds(e.CastTime + NpcCastPadding + extra);
            if (activation <= now && stage < 4)
                return;
            _kbStage = stage;
            _kbExpireUtc = activation.AddSeconds(2.0);
            RemoveIfValid(ref _kbGuide);
            if (activation > now)
                _kbGuide = H.SafeZone.AddTimedSafeArea(new CircleTemplate(5f), At(ArenaCenter), (float)(activation - now).TotalSeconds + 1f);
            H.Hint("b26_kb", "【B26 石像鬼】五连斩最后一击 20y 击退：提前靠中，避开瘴气与电网");
        }

        private void ArmMine(uint id)
        {
            using var scope = H.SafeZone.BeginDrawingGroup("瘴气地雷");
            if (!_armedMines.Add(id))
                return;
            if (Svc.Objects.SearchByEntityId(id) is not { IsDead: false })
            {
                _armedMines.Remove(id);
                return;
            }
            _mineObstacles[id] = H.SafeZone.AddObstacle(new CircleTemplate(1.8f),
                new FollowEntityPosition(id, Vector2.Zero, 0f, MissingEntityPolicy.RemoveArea));
            H.Hint("b26_mine", "【B26 石像鬼】瘴气地雷已布防：绕开瘴气球");
        }

        private void DisarmMine(uint id)
        {
            _armedMines.Remove(id);
            if (_mineObstacles.Remove(id, out var handle))
                H.SafeZone.Remove(handle);
        }

        private static bool IsMiasmaBall(uint entityId)
            => Svc.Objects.SearchByEntityId(entityId) is IBattleChara bc && bc.NameId == NameMiasmaBall;
    }

    // ════════════════ B27 发条步兵（无电网，R40 圆跟随质心兜底）════════════════

    private sealed class B27ClockworkHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B27;
        public override string Mechanism => MechB27;
        public override uint[] NameIds => [NameManager, NameClockworkInfantry, NamePanicArmor, NameDrone, NameCobra];

        private float _centroidAccum = 1f;

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB27AutoArtillery: // 自动火炮：40×5 跟随施法者朝向
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(5f, 40f), FollowCaster(e.SourceId));
                    break;
                case AidB27SpinSmash: // 旋转挥打：R5 施法者位
                    H.Tracker.Register(e, new CircleTemplate(5f), FollowCaster(e.SourceId));
                    break;
                case AidB27ManaBurst: // 魔力爆散：R6 施法者位
                    H.Tracker.Register(e, new CircleTemplate(6f), FollowCaster(e.SourceId));
                    break;
                case AidB27FireBreath: // 火炎放射：R40 半角 60°
                    H.Tracker.Register(e, new FanTemplate(40f, Fan120), FollowCaster(e.SourceId));
                    break;
                case AidB27PoisonBurst:
                    H.Hint("b27_poison", "【B27 发条步兵】毒素爆散（全体）");
                    break;
                case AidB27Confusion:
                    H.Hint("b27_confuse", "【B27 发条步兵】混乱术（全体）");
                    break;
                case AidB27BurnProcess:
                    H.Hint("b27_burn", "【B27 发条步兵】燃烧处理：火耐性降低（单体）");
                    break;
            }
        }

        protected override void OnActivate(Vector3 primaryPos) => RefreshCentroidBoundary();

        public override void Update(float deltaTime)
        {
            _centroidAccum += deltaTime;
            if (_centroidAccum < 1f)
                return;
            _centroidAccum = 0f;
            RefreshCentroidBoundary();
        }

        // 场地跟随存活成员质心（每秒刷新，仅当本组持有边界时）
        private void RefreshCentroidBoundary()
        {
            if (H.BoundaryOwner != BattleGroup.B27)
                return;
            var x = 0f;
            var y = 0f;
            var z = 0f;
            var n = 0;
            for (var i = 0; i < Svc.Objects.Length; i++)
            {
                if (Svc.Objects[i] is not IBattleChara bc || bc.IsDead || !NameIds.Contains(bc.NameId))
                    continue;
                x += bc.Position.X;
                y += bc.Position.Y;
                z += bc.Position.Z;
                n++;
            }
            if (n == 0)
                return;
            H.SetBoundaryShape(new CircleGeometry(new Vector3(x / n, y / n, z / n), 40f));
        }
    }

    // ════════════════ B28 巨像（矩形场 40×30）════════════════

    private sealed class B28GolemHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B28;
        public override string Mechanism => MechB28;
        public override uint[] NameIds => [NameGolem];

        // 落石（EObj 0x1EC0D2 动画预测，迁移价值最高）
        private readonly Dictionary<uint, RockPrediction> _rocks = new();
        private readonly HashSet<uint> _resolvedRocks = [];
        // 岩墙（MapEffect 17~28）+ 中央塌陷
        private readonly Dictionary<uint, SafeZoneAreaHandle> _walls = new();
        private uint _collapseCaster;
        // 跳跃月环
        private DateTime _ringSequenceUtc;
        private SafeZoneAreaHandle _jumpPredict;
        private DateTime _jumpPredictExpireUtc;
        private uint _jumpTargetId;
        private readonly HashSet<uint> _jumpSeq = [];
        private readonly List<uint> _scratch = [];
        private float _housekeepAccum;

        private sealed class RockPrediction
        {
            public SafeZoneAreaHandle Handle;
            public DateTime ActivationUtc;
            public Vector3 Pos;
        }

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB28Shockwave: // 冲击波：5×5
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(5f, 5f), FollowCaster(e.SourceId));
                    break;
                case AidB28CoreRing: // 核心环光：月环 5-50 施法者位
                    H.Tracker.Register(e, new DonutTemplate(5f, 50f), FollowCaster(e.SourceId));
                    break;
                case AidB28PlainSplit: // 平原震裂：R20 施法者位
                    H.Tracker.Register(e, new CircleTemplate(20f), FollowCaster(e.SourceId));
                    break;
                case AidB28RockSpike: // 岩突：R40 半角 30°
                    H.Tracker.Register(e, new FanTemplate(40f, Fan60), FollowCaster(e.SourceId));
                    break;
                case AidB28RockWall: // 岩墙+中央塌陷：场心正北 15y 朝南 30×10，读条结束 +10s 结算
                    _collapseCaster = (uint)e.SourceId;
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(10f, 30f),
                        At(new Vector3(ArenaCenter.X, ArenaCenter.Y, ArenaCenter.Z - 15f), 0f), 10f, 2f);
                    break;
                case AidB28RockFallCircle:
                case AidB28RockFallDonut:
                    OnRockCast(e);
                    break;
                case AidB28JumpStartA: // 起跳：记录月环序列（效果包约 8s 后）
                    _ringSequenceUtc = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding + 8f);
                    RemoveJumpPredict();
                    break;
                case AidB28JumpStartB: // 起跳 B：取消分支——清空月环序列与预测（BMR 同）
                    _ringSequenceUtc = default;
                    RemoveJumpPredict();
                    break;
                case AidB28JumpRingActual: // 跳跃月环实际读条：按读条位置/时间替换预测
                    RemoveJumpPredict();
                    H.Tracker.Register(e, new DonutTemplate(5f, 50f), At(e.EffectPosition), 0f, 1f);
                    break;
                case AidB28Knockback: // 击退 17y（降级提示）
                    H.Hint("b28_kb", "【B28 巨像】击退 17y：提前靠中");
                    break;
                case AidB28SelfDestruct: // 自爆提示
                    H.Hint("b28_sd", "【B28 巨像】自爆：优先击杀正在读条的巨像！");
                    break;
            }
        }

        public override void OnEffect(LogSystemActionEffectEvent e)
        {
            if (e.ActionId == AidB28CollapseHit) // 塌陷命中：清全部墙障碍 + 塌陷
            {
                ClearWallsAndCollapse();
                return;
            }
            if (e.ActionId == AidB28JumpRingActual) // 月环实际命中：撤掉预测（BMR 同）
                RemoveJumpPredict();
            if (e.ActionId != AidB28JumpPack || !_jumpSeq.Add(e.GlobalSequence))
                return;
            // 跳跃效果包：主目标（TargetIndex 0）= 被跳巨像，按其当前位置预测月环（约 6s 后）
            if (_ringSequenceUtc <= DateTime.UtcNow || e.TargetIndex != 0)
                return;
            if (Svc.Objects.SearchByEntityId((uint)e.TargetId) is not { IsDead: false } target)
                return;
            RemoveJumpPredict();
            _jumpPredict = H.RegisterDanger(new DonutTemplate(5f, 50f), At(target.Position), 6f, 1f, ActionDrawingName(AidB28JumpRingActual));
            _jumpPredictExpireUtc = DateTime.UtcNow.AddSeconds(7.0);
            _jumpTargetId = (uint)e.TargetId;
        }

        // 落石 EObj 动画：0x00010002=圆 R8 / 0x00400080=月环 3-11（+10s）；0x00040008/0x00040100=最终阶段（+3.1s）
        public override void OnEObjAnim(LogSystemEObjAnimationEvent e)
        {
            var id = (uint)e.SourceId;
            if (_resolvedRocks.Contains(id))
                return;
            var obj = Svc.Objects.SearchByEntityId(id);
            if (obj is not { ObjectKind: ObjectKind.EventObj } || obj.BaseId != EObjRock)
                return;
            var state = (uint)((e.Param1 << 16) | e.Param2);
            GeometryTemplate? shape = state switch
            {
                RockAnimCircle or RockAnimCircleFinal => new CircleTemplate(8f),
                RockAnimDonut or RockAnimDonutFinal => new DonutTemplate(3f, 11f),
                _ => null,
            };
            if (shape == null)
                return;
            var finalPhase = state is RockAnimCircleFinal or RockAnimDonutFinal;
            var now = DateTime.UtcNow;
            var activation = now.AddSeconds(finalPhase ? 3.1 : 10.0);
            var pos = e.SourcePosition ?? obj.Position;
            if (_rocks.TryGetValue(id, out var prev))
            {
                if (prev.ActivationUtc < activation)
                    activation = prev.ActivationUtc; // 重复/乱序动画不得推迟预警
                H.SafeZone.Remove(prev.Handle);
            }
            var actionId = shape is CircleTemplate ? AidB28RockFallCircle : AidB28RockFallDonut;
            var handle = H.RegisterDanger(shape, At(pos), (float)(activation - now).TotalSeconds, 1f, ActionDrawingName(actionId));
            _rocks[id] = new RockPrediction { Handle = handle, ActivationUtc = activation, Pos = pos };
        }

        public override void OnEObjState(LogSystemEObjSetStateEvent e)
        {
            if (e.State != 7)
                return;
            var id = (uint)e.SourceId;
            if (_rocks.Remove(id, out var rock))
                H.SafeZone.Remove(rock.Handle);
            if (Svc.Objects.SearchByEntityId(id) is { ObjectKind: ObjectKind.EventObj } obj && obj.BaseId == EObjRock)
                _resolvedRocks.Add(id);
        }

        // MapEffect 17~28：state 开 → 逐块 5×5 障碍（x=场心±7.5 两列，z=场心-12.5+5×(idx-17)/2）；关 → 撤
        // 门槛：仅在 0xBE6C 岩墙读条已开始后接受"开"（同图第三盘的 MapEffect index 存在别名，BMR 注释过的坑，防止误触）
        public override void OnMapEffect(uint index, uint state)
        {
            if (index is < 17 or > 28)
                return;
            if (state == MapEffectOn)
            {
                using var scope = H.SafeZone.BeginDrawingGroup("岩墙障碍");
                if (_collapseCaster == 0 || _walls.ContainsKey(index))
                    return;
                var offset = (int)index - 17;
                var pos = new Vector3(
                    ArenaCenter.X + (offset % 2 == 0 ? 7.5f : -7.5f),
                    ArenaCenter.Y,
                    ArenaCenter.Z - 12.5f + 5f * (offset / 2));
                _walls[index] = H.SafeZone.AddObstacle(new RectangleTemplate(5f, 5f), At(pos));
            }
            else if (state == MapEffectOff && _walls.Remove(index, out var handle))
            {
                H.SafeZone.Remove(handle);
            }
        }

        public override void Update(float deltaTime)
        {
            var now = DateTime.UtcNow;
            if (_jumpPredict.IsValid && (now > _jumpPredictExpireUtc
                || _jumpTargetId != 0 && Svc.Objects.SearchByEntityId(_jumpTargetId) is not { IsDead: false }))
                RemoveJumpPredict(); // BMR 同：被跳巨像消失即撤预测
            _housekeepAccum += deltaTime;
            if (_housekeepAccum < 0.5f)
                return;
            _housekeepAccum = 0f;
            _scratch.Clear();
            foreach (var (id, rock) in _rocks)
                if (rock.ActivationUtc.AddSeconds(2.0) < now)
                    _scratch.Add(id);
            foreach (var id in _scratch)
            {
                H.SafeZone.Remove(_rocks[id].Handle);
                _rocks.Remove(id);
            }
            _scratch.Clear();
            foreach (var id in _resolvedRocks)
                if (Svc.Objects.SearchByEntityId(id) == null)
                    _scratch.Add(id);
            foreach (var id in _scratch)
                _resolvedRocks.Remove(id);
            // 施法者消失：塌陷命中事件可能由 9020 辅助体发出而缺失，兜底清场
            if (_collapseCaster != 0 && Svc.Objects.SearchByEntityId(_collapseCaster) is not { IsDead: false })
            {
                H.Tracker.Cancel(_collapseCaster, AidB28RockWall);
                _collapseCaster = 0;
                ClearWalls();
            }
        }

        public override void Cleanup()
        {
            _scratch.Clear();
            _scratch.AddRange(_rocks.Keys);
            foreach (var id in _scratch)
            {
                H.SafeZone.Remove(_rocks[id].Handle);
                _rocks.Remove(id);
            }
            _resolvedRocks.Clear();
            ClearWallsAndCollapse();
            RemoveJumpPredict();
            _jumpSeq.Clear();
        }

        // 落石实际读条（0.7s）：按位置 0.5y 内匹配替换预测
        private void OnRockCast(LogSystemCastStartEvent e)
        {
            _scratch.Clear();
            foreach (var (id, rock) in _rocks)
                if (DistanceXZ(rock.Pos, e.EffectPosition) <= 0.5f)
                    _scratch.Add(id);
            foreach (var id in _scratch)
            {
                H.SafeZone.Remove(_rocks[id].Handle);
                _rocks.Remove(id);
                _resolvedRocks.Add(id);
            }
            H.Tracker.Register(e,
                e.ActionId == AidB28RockFallCircle ? new CircleTemplate(8f) : (GeometryTemplate)new DonutTemplate(3f, 11f),
                At(e.EffectPosition), 0f, 1f);
        }

        private void ClearWalls()
        {
            foreach (var (_, handle) in _walls)
                H.SafeZone.Remove(handle);
            _walls.Clear();
        }

        private void ClearWallsAndCollapse()
        {
            if (_collapseCaster != 0)
            {
                H.Tracker.Cancel(_collapseCaster, AidB28RockWall);
                _collapseCaster = 0;
            }
            ClearWalls();
        }

        private void RemoveJumpPredict()
        {
            RemoveIfValid(ref _jumpPredict);
            _jumpTargetId = 0;
        }
    }

    // ════════════════ B29 树精（树精场 R20 内缩 0.3）════════════════

    private sealed class B29TreantHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B29;
        public override string Mechanism => MechB29;
        public override uint[] NameIds => [NameTreant, NameSlug, NameTreantBud, NameScorpion, NameBiloko];

        // 树精本体目标圈（BMR CrucibleTreantBody）：存活期间常显碰撞圈危险区跟随实体。
        // 半径直接用 hitbox，BMR 的 +0.5 余量由门面按 DodgeDangerMargin 叠加（默认正好 0.5）
        private readonly Dictionary<uint, (SafeZoneAreaHandle Handle, float Radius)> _bodies = new();
        private readonly List<uint> _scratch = [];
        private float _bodyAccum;

        public override void Update(float deltaTime)
        {
            _bodyAccum += deltaTime;
            if (_bodyAccum < 0.5f)
                return;
            _bodyAccum = 0f;
            var anyMemberAlive = false; // BMR CrucibleGroveAdds：组内有存活成员期间提示优先击杀小怪
            for (var i = 0; i < Svc.Objects.Length; i++)
            {
                if (Svc.Objects[i] is not IBattleChara bc || bc.IsDead)
                    continue;
                if (bc.NameId is >= NameTreant and <= NameBiloko)
                    anyMemberAlive = true;
                if (bc.NameId != NameTreant)
                    continue;
                if (_bodies.TryGetValue(bc.EntityId, out var existing))
                {
                    if (MathF.Abs(existing.Radius - bc.HitboxRadius) < 0.01f)
                        continue;
                    if (existing.Handle.IsValid)
                        H.SafeZone.Remove(existing.Handle);
                }
                using var scope = H.SafeZone.BeginDrawingGroup("树精本体");
                _bodies[bc.EntityId] = (H.SafeZone.AddPersistentDanger(new CircleTemplate(bc.HitboxRadius),
                    new FollowEntityPosition(bc.EntityId, Vector2.Zero, 0f, MissingEntityPolicy.RemoveArea)), bc.HitboxRadius);
            }
            _scratch.Clear();
            foreach (var id in _bodies.Keys)
                if (Svc.Objects.SearchByEntityId(id) is not { IsDead: false })
                    _scratch.Add(id);
            foreach (var id in _scratch)
                if (_bodies.Remove(id, out var entry))
                    H.SafeZone.Remove(entry.Handle);
            if (anyMemberAlive)
                H.Hint("b29_adds", "【B29 树精】优先击杀小怪（蛞蝓/花苗），别被贴身缠住！");
        }

        public override void Cleanup()
        {
            foreach (var (_, entry) in _bodies)
                H.SafeZone.Remove(entry.Handle);
            _bodies.Clear();
        }

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            // 巨木龙卷五连：五条同开读、中心相同（场心北 13y），各自读条剩余天然携带 2s 间隔
            switch (e.ActionId)
            {
                case AidB29Tornado1: // 圆 R12
                    H.Tracker.Register(e, new CircleTemplate(12f), At(e.EffectPosition), 0f, 1.5f);
                    H.Hint("b29_tornado", "【B29 树精】巨木龙卷五连：由内向外依次结算，先站 12 内或 36 外");
                    break;
                case AidB29Tornado2: // 环 12-18
                    H.Tracker.Register(e, new DonutTemplate(12f, 18f), At(e.EffectPosition), 0f, 1.5f);
                    break;
                case AidB29Tornado3: // 环 18-24
                    H.Tracker.Register(e, new DonutTemplate(18f, 24f), At(e.EffectPosition), 0f, 1.5f);
                    break;
                case AidB29Tornado4: // 环 24-30
                    H.Tracker.Register(e, new DonutTemplate(24f, 30f), At(e.EffectPosition), 0f, 1.5f);
                    break;
                case AidB29Tornado5: // 环 30-36
                    H.Tracker.Register(e, new DonutTemplate(30f, 36f), At(e.EffectPosition), 0f, 1.5f);
                    break;
                case AidB29GustA: // 簌簌轻风：R60 半角 45°
                case AidB29GustB: // 半角 75°
                case AidB29GustC:
                    // BMR LocationTargeted=true，spell.Rotation 来自 CastRotation。
                    if (CastRotation.TryGet(e, out var rotation))
                        H.Tracker.Register(e, new FanTemplate(60f, e.ActionId == AidB29GustA ? Fan90 : Fan150), At(e.EffectPosition, rotation));
                    else
                        H.LogError($"【B29 树精】无法读取当前施法朝向，跳过簌簌轻风预测（技能 {e.ActionId}，实体 {e.SourceId:X}）");
                    break;
                case AidB29Acorn: // 橡果炸弹：R3 静态落点（BMR LocationTargeted=true，不跟随施法者）
                    H.Tracker.Register(e, new CircleTemplate(3f), At(e.EffectPosition));
                    break;
                case AidB29Sap: // 释放汁液：R5 静态落点（同上）
                    H.Tracker.Register(e, new CircleTemplate(5f), At(e.EffectPosition));
                    break;
                case AidB29Web: // 蛛网屏：40×4 静态落点 + CastRotation（同簌簌轻风规则）
                    if (CastRotation.TryGet(e, out var webRotation))
                        H.Tracker.Register(e, RayRectangleTemplate.Symmetric(4f, 40f), At(e.EffectPosition, webRotation));
                    else
                        H.LogError($"【B29 树精】无法读取当前施法朝向，跳过蛛网屏预测（技能 {e.ActionId}，实体 {e.SourceId:X}）");
                    break;
                case AidB29Heal: // 治疗之水
                    H.Hint("b29_heal", "【B29 树精】治疗之水：优先打断/击杀治疗花苗");
                    break;
            }
        }
    }

    // ════════════════ B30 榴弹怪（圆场 R20）════════════════

    private sealed class B30BombHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B30;
        public override string Mechanism => MechB30;
        public override uint[] NameIds => [NameBombQueen, NameGrenade, NameFireBomb, NameIceBomb, NameFlameBall];

        private readonly VoidzoneWatcher _pools = new(host, EObjBombPool, 6f, "火焰池");
        private readonly Dictionary<uint, SafeZoneAreaHandle> _bombs = new();
        private readonly HashSet<uint> _meltdownSeq = [];
        private readonly List<uint> _scratch = [];
        private float _bombAccum;

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB30FireRoar: // 烈火怒骂：R40 半角 90°
                    H.Tracker.Register(e, new FanTemplate(40f, Fan180), FollowCaster(e.SourceId));
                    break;
                case AidB30Blaze: // 烈炎：R5 落点圆
                    H.Tracker.Register(e, new CircleTemplate(5f), At(e.EffectPosition));
                    break;
                case AidB30PoisonSmoke: // 剧毒烟雾：R40 半角 10°
                    H.Tracker.Register(e, new FanTemplate(40f, Fan20), FollowCaster(e.SourceId));
                    break;
                case AidB30Enrage: // 连续大爆炸（狂暴）
                    H.Hint("b30_enrage", "【B30 榴弹怪】连续大爆炸：优先处理榴弹怪，注意减伤！");
                    break;
                case AidB30IceSpikes: // 冰棘屏障
                    H.Hint("b30_icespikes", "【B30 榴弹怪】冰棘屏障（单体）");
                    break;
            }
        }

        public override void OnEffect(LogSystemActionEffectEvent e)
        {
            if (e.ActionId is AidB30SelfDestructHit or AidB30IceShatterHit) // 爆弹命中：立即撤危险区
            {
                RemoveBomb((uint)e.SourceId);
                return;
            }
            if (e.ActionId != AidB30MeltdownTelegraph || !_meltdownSeq.Add(e.GlobalSequence))
                return;
            // 熔毁：效果包预告 → 40×12（施法者位置/朝向，0.9s 后判定）
            if (e.SourcePosition is not { } pos)
                return;
            H.RegisterDanger(RayRectangleTemplate.Symmetric(12f, 40f), At(pos, e.SourceRotation ?? 0f), 0.9f, 1f, "熔毁");
        }

        // 移动爆弹：火弹 R6 / 冰弹 R10 持久危险区跟随实体
        public override void Update(float deltaTime)
        {
            _pools.Update(deltaTime);
            _bombAccum += deltaTime;
            if (_bombAccum < 0.3f)
                return;
            _bombAccum = 0f;
            for (var i = 0; i < Svc.Objects.Length; i++)
            {
                if (Svc.Objects[i] is not IBattleChara bc || bc.IsDead)
                    continue;
                var radius = bc.NameId == NameFireBomb ? 6f : bc.NameId == NameIceBomb ? 10f : 0f;
                if (radius <= 0f || _bombs.ContainsKey(bc.EntityId))
                    continue;
                using var scope = H.SafeZone.BeginDrawingGroup(bc.NameId == NameFireBomb ? "移动火弹" : "移动冰弹");
                _bombs[bc.EntityId] = H.RegisterPersistentDanger(new CircleTemplate(radius),
                    new FollowEntityPosition(bc.EntityId, Vector2.Zero, 0f, MissingEntityPolicy.RemoveArea));
            }
            _scratch.Clear();
            foreach (var id in _bombs.Keys)
                if (Svc.Objects.SearchByEntityId(id) is not { IsDead: false })
                    _scratch.Add(id);
            foreach (var id in _scratch)
                RemoveBomb(id);
        }

        public override void Cleanup()
        {
            _pools.Cleanup();
            _meltdownSeq.Clear();
            _scratch.Clear();
            _scratch.AddRange(_bombs.Keys);
            foreach (var id in _scratch)
                RemoveBomb(id);
        }

        private void RemoveBomb(uint id)
        {
            if (_bombs.Remove(id, out var handle))
                H.SafeZone.Remove(handle);
        }
    }

    // ════════════════ B31 怨毒龙（龙场 R19.75 实体墙，中心 920,-420）════════════════

    private sealed class B31DragonHandler(CrucibleHigh1Script host) : GroupHandler(host)
    {
        public override BattleGroup Group => BattleGroup.B31;
        public override string Mechanism => MechB31;
        public override uint[] NameIds => [NameGrudgeDragon, NameToxicSubstance, NamePoisonCloud];

        private readonly VoidzoneWatcher _pools = new(host, EObjPoisonPool, 6f, "毒池", 2f); // 毒池出现 2s 后激活
        // 猛毒吐息六连（安全点 = 场心 − 20×读条朝向）
        private SafeZoneAreaHandle _breathArea;
        private DateTime _breathFirstPulseUtc;
        private bool _breathCorrected;
        private readonly HashSet<uint> _breathSeq = [];
        // 连线放毒四点引导
        private BaitRound? _round;
        private Vector2 _north = new(0f, -1f);
        private readonly HashSet<uint> _baitSeq = [];
        private bool _baitTetherDrawn;
        private float _baitScanAccum;
        // 空降中心命中后的 8s 预备引导（BMR CrucibleDragonBaits._prepareUntil）
        private DateTime _prepareUntilUtc;
        private uint _prepareSourceId;
        private uint _previousBaitTargetId;
        private readonly Vector3[] _prepareRoute = new Vector3[4];
        private SafeZoneAreaHandle _prepareArea;
        private Vector3 _prepareGuidePos;
        private bool _prepareGuideShown;
        // 毒气团（R2 持续危险区跟随——硬障碍只约束寻路、不会在逼近时主动逃离；+ 1.2s 位移胶囊危险区提前规避）
        private readonly Dictionary<uint, SafeZoneAreaHandle> _clouds = new();
        private readonly Dictionary<uint, SafeZoneAreaHandle> _cloudCapsules = new();
        private readonly Dictionary<uint, CloudMotion> _cloudMotion = new();
        private readonly List<uint> _aliveScratch = [];
        private readonly List<uint> _scratch = [];
        private float _cloudDrawAccum;
        // 空降二连
        private SafeZoneAreaHandle _landingArea;
        private DateTime _landingExpireUtc;
        // 外环判定前中心圈只预览不进解算（BMR CrucibleDragonLanding 的 non-risky 语义）
        private bool _landingOuterPending;
        private DateTime _landingOuterActivationUtc;
        private (Vector3 Pos, DateTime ActivationUtc)? _pendingCenterHit;

        private sealed class CloudMotion
        {
            public Vector3 Pos;
            public DateTime AtUtc;
            public Vector2 Velocity;
        }

        private sealed class BaitRound
        {
            public uint SourceId;
            public uint TargetId;
            public bool IsLocal;
            public int Step;
            public DateTime ExpiresUtc;
            public readonly Vector3[] Route = new Vector3[4];
            public SafeZoneAreaHandle Guide;
            public int GuideStep = -1;
            public readonly HashSet<uint> SeenPools = [];
            public readonly List<(Vector3 Pos, DateTime ExpiresUtc, uint TargetId)> Snapshots = [];
        }

        public override void OnCastStart(LogSystemCastStartEvent e)
        {
            switch (e.ActionId)
            {
                case AidB31Smog: // 浓烟呛呕：R6 落点圆
                case AidB31Phlegm: // 蠕动的痰：R6 落点圆
                    H.Tracker.Register(e, new CircleTemplate(6f), At(e.EffectPosition));
                    break;
                case AidB31Dive: // 低温俯冲：48×20 跟随施法者朝向
                    H.Tracker.Register(e, RayRectangleTemplate.Symmetric(20f, 48f), FollowCaster(e.SourceId));
                    break;
                case AidB31PoisonBreath:
                    StartBreath(e);
                    break;
                case AidB31PoisonBait: // 连线放毒读条 3.2s：以玩家为目标则开一轮
                {
                    var targetId = (uint)e.TargetId;
                    if (Svc.Objects.SearchByEntityId(targetId) is { ObjectKind: not ObjectKind.Pc })
                        break;
                    BeginRound((uint)e.SourceId, targetId, DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding + 13.0));
                    break;
                }
                case AidB31LandingOuter: // 空降外环：期间先靠近落点中心
                {
                    using var scope = H.SafeZone.BeginDrawingGroup(ActionDrawingName(AidB31LandingOuter));
                    var duration = e.CastTime + NpcCastPadding + 1f;
                    RemoveIfValid(ref _landingArea);
                    _landingArea = H.SafeZone.AddTimedSafeArea(new CircleTemplate(5f), At(e.EffectPosition), duration);
                    _landingExpireUtc = DateTime.UtcNow.AddSeconds(duration);
                    _landingOuterPending = true;
                    _landingOuterActivationUtc = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding);
                    H.Hint("b31_landing", "【B31 怨毒龙】空降：先靠近绿圈，外环判定后立即出中心");
                    break;
                }
                case AidB31LandingCenter: // 空降中心：R6 落点圆
                {
                    // 外环 pending 期间中心圈只画预览（不进解算，避免提前把玩家推出中心安全点）；外环判定后按剩余读条注册
                    var now = DateTime.UtcNow;
                    var activation = now.AddSeconds(e.CastTime + NpcCastPadding);
                    if (_landingOuterPending && now <= _landingOuterActivationUtc.AddSeconds(0.5))
                    {
                        H.Draw.AddAoe("b31_landing_center", new CircleGeometry(e.EffectPosition, 6f),
                            durationMs: (float)(activation - now).TotalSeconds * 1000f + 1000f);
                        _pendingCenterHit = (e.EffectPosition, activation);
                    }
                    else
                    {
                        H.Tracker.Register(e, new CircleTemplate(6f), At(e.EffectPosition));
                    }
                    break;
                }
                case AidB31DeadlyPoison: // 剧毒全场
                    H.Hint("b31_poison", "【B31 怨毒龙】剧毒全场：注意减伤！");
                    break;
            }
        }

        public override void OnCastCancel(uint sourceId, uint actionId)
        {
            if (actionId == AidB31PoisonBreath)
                RemoveBreath();
            if (actionId == AidB31PoisonBait && _round is { } r && r.SourceId == sourceId)
                EndRound();
            if (actionId == AidB31LandingOuter)
            {
                RemoveIfValid(ref _landingArea);
                _landingOuterPending = false; // 外环打断：挂起的中心圈立即转正常注册（BMR 同）
                ReleaseLandingCenter(DateTime.UtcNow);
            }
            if (actionId == AidB31LandingCenter)
            {
                _pendingCenterHit = null;
                H.Draw.Remove("b31_landing_center");
            }
        }

        public override void OnEffect(LogSystemActionEffectEvent e)
        {
            // 毒浪命中：安全点校正为波源实际位置（0xBEA8 施法辅助体当前位置，与 BMR `_origin = caster.Position` 一致；
            // 仅首跳前一次。规格原"caster 位置 − 20×朝向"表述已按 BMR 行为修订）
            if (e.ActionId == AidB31PoisonWave && _breathSeq.Add(e.GlobalSequence))
            {
                if (_breathArea.IsValid && !_breathCorrected
                    && DateTime.UtcNow <= _breathFirstPulseUtc.AddSeconds(1.0)
                    && e.SourcePosition is { } src)
                {
                    H.SafeZone.Remove(_breathArea);
                    using var scope = H.SafeZone.BeginDrawingGroup("猛毒吐息安全点");
                    _breathArea = H.SafeZone.AddTimedSafeArea(new CircleTemplate(7f), At(src), 6.5f);
                    _breathCorrected = true;
                }
                return;
            }
            // 外环命中：解除门控，挂起的中心圈按剩余读条注册进解算
            if (e.ActionId == AidB31LandingOuter && _landingOuterPending)
            {
                _landingOuterPending = false;
                ReleaseLandingCenter(DateTime.UtcNow);
                return;
            }
            // 空降中心命中：8s 预备引导下一轮连线放毒（BMR：命中约 4.8s 后才有下一轮连线读条，光靠连线再跑位太晚）
            if (e.ActionId == AidB31LandingCenter && _previousBaitTargetId != 0
                && Svc.Objects.SearchByEntityId((uint)e.SourceId) is IBattleChara { NameId: NameGrudgeDragon })
            {
                _prepareSourceId = (uint)e.SourceId;
                _prepareUntilUtc = DateTime.UtcNow.AddSeconds(8.0);
                if (_round == null)
                    BuildRoute(_prepareSourceId, _prepareRoute);
                return;
            }
            // 放毒快照：记录目标位置（3.1s 内用于新池匹配）
            if (e.ActionId is AidB31PoisonBait or AidB31PoisonSnap && _baitSeq.Add(e.GlobalSequence))
            {
                var targetId = (uint)e.TargetId;
                if ((e.TargetPosition ?? Svc.Objects.SearchByEntityId(targetId)?.Position) is { } pos)
                    _round?.Snapshots.Add((pos, DateTime.UtcNow.AddSeconds(3.1), targetId));
            }
        }

        // 连线放毒触发：tether 0x11（龙→玩家）
        public override void OnTether(LogSystemTetherEvent e)
        {
            if (e.TetherId != TetherDragonPoison)
                return;
            if (Svc.Objects.SearchByEntityId((uint)e.SourceId) is not IBattleChara src || src.NameId != NameGrudgeDragon)
                return;
            if (Svc.Objects.SearchByEntityId((uint)e.TargetId) is not { ObjectKind: ObjectKind.Pc })
                return;
            BeginRound((uint)e.SourceId, (uint)e.TargetId, DateTime.UtcNow.AddSeconds(16.5));
        }

        public override void OnTetherCancel(LogSystemTetherCancelEvent e)
        {
            if (e.TetherId != TetherDragonPoison)
                return;
            H.Draw.Remove($"b31_tether_{(uint)e.SourceId}_{(uint)e.TargetId}");
            if (_round is { } r && r.SourceId == (uint)e.SourceId && r.TargetId == (uint)e.TargetId)
                _baitTetherDrawn = false;
        }

        public override void Update(float deltaTime)
        {
            var now = DateTime.UtcNow;
            _pools.Update(deltaTime);
            if (_breathArea.IsValid && now > _breathFirstPulseUtc.AddSeconds(7.5))
                RemoveBreath();
            UpdateBait(now);
            UpdateClouds(now, deltaTime);
            if (_landingArea.IsValid && now > _landingExpireUtc)
            {
                RemoveIfValid(ref _landingArea);
            }
            if (_landingOuterPending && now > _landingOuterActivationUtc.AddSeconds(0.5))
            {
                _landingOuterPending = false; // 外环命中事件缺失的兜底（与 BMR waitForOuter 同一时间窗）
                ReleaseLandingCenter(now);
            }
            // 热重载/错过 tether 兜底：无轮次时每秒扫在途 0xBEA9 读条补开一轮（BMR 同样以读条做迟到恢复）
            if (_round == null)
            {
                _baitScanAccum += deltaTime;
                if (_baitScanAccum >= 1f)
                {
                    _baitScanAccum = 0f;
                    TryRecoverBaitRound();
                }
            }
        }

        private void TryRecoverBaitRound()
        {
            for (var i = 0; i < Svc.Objects.Length; i++)
            {
                if (Svc.Objects[i] is not IBattleChara bc || bc.IsDead || bc.NameId != NameGrudgeDragon)
                    continue;
                if (!bc.IsCasting || bc.CastActionId != AidB31PoisonBait)
                    continue;
                if (Svc.Objects.SearchById(bc.CastTargetObjectId) is not { ObjectKind: ObjectKind.Pc } target)
                    return;
                var remain = MathF.Max(0f, bc.TotalCastTime - bc.CurrentCastTime);
                BeginRound(bc.EntityId, target.EntityId, DateTime.UtcNow.AddSeconds(remain + NpcCastPadding + 13.0));
                return;
            }
        }

        public override void Cleanup()
        {
            _pools.Cleanup();
            RemoveBreath();
            _breathSeq.Clear();
            EndRound();
            _baitSeq.Clear();
            _scratch.Clear();
            _scratch.AddRange(_clouds.Keys);
            foreach (var id in _scratch)
            {
                if (_clouds.Remove(id, out var handle))
                    H.SafeZone.Remove(handle);
                if (_cloudCapsules.Remove(id, out var capsule))
                    H.SafeZone.Remove(capsule);
            }
            _cloudMotion.Clear();
            RemoveIfValid(ref _landingArea);
            _landingOuterPending = false;
            _pendingCenterHit = null;
            H.Draw.Remove("b31_landing_center");
            ClearPrepare();
            _prepareSourceId = 0;
            _previousBaitTargetId = 0;
        }

        // 猛毒吐息 0xBEA7：读条开始即给安全点（场心 − 20×朝向），全程不动直到六连结束
        private void StartBreath(LogSystemCastStartEvent e)
        {
            var firstPulse = DateTime.UtcNow.AddSeconds(e.CastTime + NpcCastPadding + 2.4f);
            if (_breathArea.IsValid && Math.Abs((firstPulse - _breathFirstPulseUtc).TotalSeconds) < 0.75)
                return; // 重复读条
            RemoveBreath();
            if (!CastRotation.TryGet(e, out var rotation))
            {
                H.LogError($"【B31 怨毒龙】无法读取当前施法朝向，跳过猛毒吐息预测（技能 {e.ActionId}，实体 {e.SourceId:X}）");
                return;
            }
            using var scope = H.SafeZone.BeginDrawingGroup("猛毒吐息安全点");
            var forward = PromeRotation.Helpers.GeometryHelper.ForwardVector3FromRotation(rotation);
            var spot = ArenaCenter - forward * 20f;
            var duration = e.CastTime + NpcCastPadding + 2.4f + 6.5f; // BMR：安全点持续至首跳 +6.5s
            _breathArea = H.SafeZone.AddTimedSafeArea(new CircleTemplate(7f), At(spot), duration);
            _breathFirstPulseUtc = firstPulse;
            _breathCorrected = false;
            H.Hint("b31_breath", "【B31 怨毒龙】猛毒吐息六连：提前站进绿圈安全点，全程不动");
        }

        private void RemoveBreath()
        {
            RemoveIfValid(ref _breathArea);
        }

        // 门控解除后把挂起的中心圈按剩余读条时间注册进解算（预览已由 Draw.AddAoe 承担）
        private void ReleaseLandingCenter(DateTime now)
        {
            if (_pendingCenterHit is not { } pending)
                return;
            _pendingCenterHit = null;
            H.Draw.Remove("b31_landing_center");
            var remain = (float)(pending.ActivationUtc - now).TotalSeconds;
            if (remain > 0.05f)
                H.RegisterDanger(new CircleTemplate(6f), At(pending.Pos), remain, 0.6f, ActionDrawingName(AidB31LandingCenter));
        }

        // 8s 预备引导：路线随龙的当前位置刷新（BMR 同），仅对上一轮的本地玩家目标给安全区/绘制
        private void UpdatePrepare(DateTime now)
        {
            if (_prepareUntilUtc == default || now >= _prepareUntilUtc)
            {
                ClearPrepare();
                return;
            }
            if (_previousBaitTargetId == 0 || !H.IsLocalPlayer(_previousBaitTargetId)
                || Svc.Objects.SearchByEntityId(_previousBaitTargetId) is not { IsDead: false }
                || Svc.Objects.SearchByEntityId(_prepareSourceId) is not { IsDead: false })
            {
                ClearPrepare();
                return;
            }
            BuildRoute(_prepareSourceId, _prepareRoute);
            if (_prepareGuideShown && _prepareGuidePos == _prepareRoute[0])
                return;
            ClearPrepareGuide();
            _prepareGuideShown = true;
            _prepareGuidePos = _prepareRoute[0];
            var remain = (float)(_prepareUntilUtc - now).TotalSeconds;
            using var scope = H.SafeZone.BeginDrawingGroup("连线放毒预备");
            _prepareArea = H.SafeZone.AddTimedSafeArea(new CircleTemplate(BaitPointRadius), At(_prepareRoute[0]), remain,
                preference: PositionPreference.Center);
            H.Draw.Add("b31_bait_prepare_p0", new CircleGeometry(_prepareRoute[0], BaitPointRadius), DrawStyle.Safe);
            if (Svc.Objects.LocalPlayer is { } player)
                H.Draw.AddTether("b31_bait_prepare_guide", player.EntityId, _prepareRoute[0], DrawStyle.Safe);
            H.Hint("b31_bait_prepare", "【B31 怨毒龙】连线放毒预备：提前进入 1 号放毒点的 0.2y 圈内");
        }

        private void ClearPrepareGuide()
        {
            if (_prepareArea.IsValid)
                H.SafeZone.Remove(_prepareArea);
            _prepareArea = default;
            _prepareGuideShown = false;
            H.Draw.Remove("b31_bait_prepare_p0");
            H.Draw.Remove("b31_bait_prepare_guide");
        }

        private void ClearPrepare()
        {
            _prepareUntilUtc = default;
            ClearPrepareGuide();
        }

        // ── 连线放毒四点引导 ──

        private void BeginRound(uint sourceId, uint targetId, DateTime expiresUtc)
        {
            var now = DateTime.UtcNow;
            if (_round is { } r && now < r.ExpiresUtc && r.Step < 4 && r.SourceId == sourceId && r.TargetId == targetId)
                return;
            EndRound();
            ClearPrepare();
            _previousBaitTargetId = targetId;
            var round = new BaitRound
            {
                SourceId = sourceId,
                TargetId = targetId,
                IsLocal = H.IsLocalPlayer(targetId),
                ExpiresUtc = expiresUtc,
            };
            foreach (var (id, _) in _pools.Alive)
                round.SeenPools.Add(id); // 已有毒池归上一轮
            BuildRoute(round);
            _baitSeq.Clear();
            _round = round;
            // 任意目标都画线；仅本地玩家给 SafeArea/路线绘制
            _baitTetherDrawn = false;
            if (H.TetherLinesOn)
            {
                H.Draw.AddTether($"b31_tether_{sourceId}_{targetId}", sourceId, targetId, DrawStyle.Danger);
                _baitTetherDrawn = true;
            }
            if (round.IsLocal)
                H.Hint("b31_bait", "【B31 怨毒龙】连线放毒：按 1→4 顺序踩绿点放毒池，池子生成后去下一点");
        }

        // 路线：N = 开始时 Boss 相对场心方向（近场心保留上轮方向），S = N 逆时针转 90°
        // outer = 场心 + 16.9×N，inner = outer − 7×N；P1=inner+7S → P2=outer+7S → P3=outer−7S → P4=inner−7S
        private void BuildRoute(BaitRound round) => BuildRoute(round.SourceId, round.Route);

        private void BuildRoute(uint sourceId, Vector3[] route)
        {
            var bossPos = Svc.Objects.SearchByEntityId(sourceId)?.Position ?? ArenaCenter;
            var offset = bossPos - ArenaCenter;
            var n = new Vector2(offset.X, offset.Z);
            if (n.LengthSquared() > 0.01f)
                _north = Vector2.Normalize(n);
            var center = new Vector2(ArenaCenter.X, ArenaCenter.Z);
            var outer = center + _north * BaitRouteOuterOffset;
            var inner = outer - _north * BaitRouteDepth;
            var side = new Vector2(_north.Y, -_north.X) * BaitRouteHalfWidth;
            route[0] = Flat(inner + side);
            route[1] = Flat(outer + side);
            route[2] = Flat(outer - side);
            route[3] = Flat(inner - side);
        }

        private Vector3 Flat(Vector2 p) => new(p.X, ArenaCenter.Y, p.Y);

        private void UpdateBait(DateTime now)
        {
            if (_round is not { } r)
            {
                UpdatePrepare(now);
                return;
            }
            if (now > r.ExpiresUtc || r.Step >= 4)
            {
                EndRound();
                return;
            }
            var target = Svc.Objects.SearchByEntityId(r.TargetId);
            if (target is not { IsDead: false } || Svc.Objects.SearchByEntityId(r.SourceId) is not { IsDead: false })
            {
                EndRound();
                return;
            }
            // 「连线绘制」开关中途关闭：即时清除连线（与 TetherDrawer 行为对齐，不补画）
            if (_baitTetherDrawn && !H.TetherLinesOn)
            {
                H.Draw.Remove($"b31_tether_{r.SourceId}_{r.TargetId}");
                _baitTetherDrawn = false;
            }
            r.Snapshots.RemoveAll(s => now > s.ExpiresUtc);
            // 新毒池推进：当前目标点 / 目标玩家位置 / 3.1s 内快照，1y 内匹配
            foreach (var (id, pos) in _pools.Alive)
            {
                if (!r.SeenPools.Add(id))
                    continue;
                var match = DistanceXZ(pos, r.Route[r.Step]) <= 1f
                    || DistanceXZ(pos, target.Position) <= 1f
                    || r.Snapshots.Any(s => s.TargetId == r.TargetId && DistanceXZ(pos, s.Pos) <= 1f);
                if (!match)
                    continue;
                r.Snapshots.RemoveAll(s => s.TargetId == r.TargetId && DistanceXZ(pos, s.Pos) <= 1f);
                if (r.Step == 3)
                    _pools.ActivateImmediately(id); // 第 4 池确认即激活持续危险区
                r.Step++;
                if (r.Step >= 4)
                {
                    EndRound();
                    return;
                }
            }
            if (!r.IsLocal || r.GuideStep == r.Step)
                return;
            // 当前点引导：R0.2 安全区 + 剩余点圈/连线 + 玩家→当前点
            // 切点必须先撤旧安全区，否则旧圈残留到超时（最长约 16.5s），解算会停在旧点
            ClearBaitGuide();
            if (r.Guide.IsValid)
                H.SafeZone.Remove(r.Guide);
            r.GuideStep = r.Step;
            var remain = (float)(r.ExpiresUtc - now).TotalSeconds;
            using var scope = H.SafeZone.BeginDrawingGroup("连线放毒引导");
            r.Guide = H.SafeZone.AddTimedSafeArea(new CircleTemplate(BaitPointRadius), At(r.Route[r.Step]), remain,
                preference: PositionPreference.Center);
            for (var i = r.Step; i < 4; i++)
            {
                H.Draw.Add($"b31_bait_p{i}", new CircleGeometry(r.Route[i], BaitPointRadius), i == r.Step ? DrawStyle.Safe : DrawStyle.Default);
                if (i + 1 < 4)
                    H.Draw.AddTether($"b31_bait_l{i}", r.Route[i], r.Route[i + 1], DrawStyle.Default);
            }
            if (Svc.Objects.LocalPlayer is { } player)
                H.Draw.AddTether("b31_bait_guide", player.EntityId, r.Route[r.Step], DrawStyle.Safe);
        }

        private void EndRound()
        {
            if (_round is not { } r)
                return;
            if (r.Guide.IsValid)
                H.SafeZone.Remove(r.Guide);
            ClearBaitGuide();
            H.Draw.Remove($"b31_tether_{r.SourceId}_{r.TargetId}");
            _baitTetherDrawn = false;
            _round = null;
        }

        private void ClearBaitGuide()
        {
            for (var i = 0; i < 4; i++)
            {
                H.Draw.Remove($"b31_bait_p{i}");
                if (i < 3)
                    H.Draw.Remove($"b31_bait_l{i}");
            }
            H.Draw.Remove("b31_bait_guide");
        }

        // ── 毒气团 ──

        private void UpdateClouds(DateTime now, float dt)
        {
            _aliveScratch.Clear();
            for (var i = 0; i < Svc.Objects.Length; i++)
            {
                if (Svc.Objects[i] is not IBattleChara bc || bc.IsDead || bc.NameId != NamePoisonCloud)
                    continue;
                var id = bc.EntityId;
                _aliveScratch.Add(id);
                if (!_clouds.ContainsKey(id))
                {
                    using var scope = H.SafeZone.BeginDrawingGroup("毒气团");
                    _clouds[id] = H.SafeZone.AddPersistentDanger(new CircleTemplate(2f),
                        new FollowEntityPosition(id, Vector2.Zero, 0f, MissingEntityPolicy.RemoveArea));
                }
                if (_cloudMotion.TryGetValue(id, out var m))
                {
                    var elapsed = (float)(now - m.AtUtc).TotalSeconds;
                    if (elapsed > 0.001f && bc.Position != m.Pos)
                    {
                        var delta = new Vector2(bc.Position.X - m.Pos.X, bc.Position.Z - m.Pos.Z);
                        var velocity = delta / elapsed;
                        // >4y/s 或单帧 >5y 视为传送，不做预测
                        if (velocity.LengthSquared() > 16f || delta.LengthSquared() > 25f)
                            velocity = Vector2.Zero;
                        _cloudMotion[id] = new CloudMotion { Pos = bc.Position, AtUtc = now, Velocity = velocity };
                    }
                }
                else
                {
                    _cloudMotion[id] = new CloudMotion { Pos = bc.Position, AtUtc = now, Velocity = Vector2.Zero };
                }
            }
            _scratch.Clear();
            foreach (var id in _clouds.Keys)
                if (!_aliveScratch.Contains(id))
                    _scratch.Add(id);
            foreach (var id in _scratch)
            {
                if (_clouds.Remove(id, out var handle))
                    H.SafeZone.Remove(handle);
                if (_cloudCapsules.Remove(id, out var capsule))
                    H.SafeZone.Remove(capsule);
                _cloudMotion.Remove(id);
            }
            // 1.2s 位移胶囊危险区（0.15s 刷新；估速数据 >0.75s 视为过期）：云团即将经过玩家时提前规避
            _cloudDrawAccum += dt;
            if (_cloudDrawAccum < 0.15f)
                return;
            _cloudDrawAccum = 0f;
            foreach (var id in _aliveScratch)
            {
                if (_cloudCapsules.Remove(id, out var old))
                    H.SafeZone.Remove(old);
                if (!_cloudMotion.TryGetValue(id, out var m))
                    continue;
                var speed = m.Velocity.Length();
                if (speed < 0.1f || (now - m.AtUtc).TotalSeconds > 0.75)
                    continue;
                var rotation = PromeRotation.Helpers.GeometryHelper.AngleTo(Vector2.Zero, m.Velocity);
                using var scope = H.SafeZone.BeginDrawingGroup("毒气团移动预警");
                _cloudCapsules[id] = H.SafeZone.AddTimedDanger(
                    RayRectangleTemplate.Symmetric(4f, MathF.Max(0.3f, speed * 1.2f)),
                    new StaticTransform(m.Pos, rotation), 1.2f);
            }
        }
    }
}

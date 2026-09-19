# 普通第三盘技能资料

区域ID：1341；[线上盘面目录](https://ffxiv.consolegameswiki.com/wiki/Third_Board_of_the_Unbroken)；[资料总览](README.md)。

“官方展示”来自客户端选怪详情；完整已知列表另含当前源码记录。形状是现有代码参数，表读条不是伤害时间，具体证据见catalog.json。

## B14 奇子·游侠骑士、奇子·主教

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·游侠骑士 / 14564 | [Cavalier Piece](https://ffxiv.consolegameswiki.com/wiki/Cavalier_Piece) | 火 | 48473 钢铁裂斩 |
| 奇子·主教 / 14567 | [Bone Bishop](https://ffxiv.consolegameswiki.com/wiki/Bone_Bishop_(Third_Board_of_the_Unbroken)) | 打击 | 48621 冰结；50693 失魂 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 46874 | 黑火喷发 / Black Eruption | 6 | Circle(5f) | 代码关联；非该组官方展示项 |
| 46900 | 黑火喷发 / Black Eruption | 1.5 | Circle(5f) | 代码关联；非该组官方展示项 |
| 48462 | 残杀 / Valfodr | 6 | Rect(60, 4) | 代码关联；非该组官方展示项 |
| 48464 | 收割者的恐吓 / Menace | 6 | Circle(20) | 代码关联；非该组官方展示项 |
| 48471 | 粉碎剑 / Crushing Blade | 5 | — | 代码关联；非该组官方展示项 |
| 48473 | 钢铁裂斩 / Steelripper | 7 | Cone(60, 65f.Degrees()); Cone(60f, 65f.Degrees()) | 未标 / 前方范围 |
| 48621 | 冰结 / Blizzard | 0 | — | 最高仇恨 / 单体 |
| 50693 | 失魂 / Soul Douse | 5 | — | 未标 / 全体 |

当前组件：`CrucibleBoundary`、`XBMB14AOE`、`CrucibleRangerKnockback`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B15 奇子·尤弥尔、奇子·鱼人

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·尤弥尔 / 14569 | [Ymir Piece](https://ffxiv.consolegameswiki.com/wiki/Ymir_Piece) | 冰 | 48477 首角抢攻；48479 万雷 |
| 奇子·鱼人 / 14571 | [Sahagin Piece](https://ffxiv.consolegameswiki.com/wiki/Sahagin_Piece) | 打击 | 48485 恐慌洗礼；50532 麻痹尖刺 |

### 攻略线索

- 优先处理鱼人，麻痹技能和尤弥尔攻击会带来血量压力；作者建议通过宠物承伤安排爆发。不可据此把麻痹当作靠走位即可规避的普通AOE。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48477 | 首角抢攻 / Head Snatch | 0 | — | 未标 / 前方范围 |
| 48479 | 万雷 / Blanket Thunder | 5 | — | 未标 / 全体 |
| 48481 | 大海啸 / Tsunami | 8 | Rect(60, 30) | 代码关联；非该组官方展示项 |
| 48483 | 激水 / Water II | 3 | Circle(6) | 代码关联；非该组官方展示项 |
| 48485 | 恐慌洗礼 / Dreadwash | 8 | — | 未标 / 全体 / 恐慌 [296] |
| 50532 | 麻痹尖刺 / Paralyzing Spikes | 3 | — | 自身 / 单体 / 麻痹尖刺 [5434] |

当前组件：`CrucibleBoundary`、`XBMB15AOE`、`CrucibleYmirThunderHint`、`CrucibleYmirRocks`、`CrucibleYmirTsunami`、`XBMB15Hint0`、`XBMB15Hint1`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B16 奇子·祖、奇子·公雏鸟、奇子·母雏鸟

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·祖 / 14572 | [Zu Piece](https://ffxiv.consolegameswiki.com/wiki/Zu_Piece) | 土 | 48497 前方乱流；48499 后方乱流 |
| 奇子·公雏鸟 / 14573 | [Cockerel Piece](https://ffxiv.consolegameswiki.com/wiki/Cockerel_Piece) | 打击 | 未列技能（不代表没有攻击） |
| 奇子·母雏鸟 / 14574 | [Pullet Piece](https://ffxiv.consolegameswiki.com/wiki/Pullet_Piece) | 打击 | 48489 腐蚀物 |

### 攻略线索

- 范围输出注意不要误伤鸟蛋；作者物理队把祖稍向北拉，避免圆形范围碰蛋。该建议与宠物技能形状有关，不能给所有配队硬编码同一拉点。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48489 | 腐蚀物 / Caustic Vomit | 13 | — | 最高仇恨 / 单体 / 加重 [5356] |
| 48491 | 十字风 / Crossbreeze | 10 | — | 代码关联；非该组官方展示项 |
| 48492 | 十字风 / Crossbreeze | 3 | Cross(50, 4) | 代码关联；非该组官方展示项 |
| 48494 | 后方猛袭乱流 / Rampaging Rear Carve | 6 | — | 代码关联；非该组官方展示项 |
| 48495 | 强袭滑空 / Featherglide | 0 | — | 代码关联；非该组官方展示项 |
| 48497 | 前方乱流 / Fore Carve | 1 | Cone(15f, 90f.Degrees()) | 未标 / 前方范围 |
| 48499 | 后方乱流 / Rear Carve | 1 | Cone(15f, 90f.Degrees()) | 未标 / 后方范围 |
| 48501 | 顺风 / Airy Pursuit | 3 | Circle(6) | 代码关联；非该组官方展示项 |
| 50465 | 狂乱 / Flying Frenzy | 8 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB16AOE`、`CrucibleZuCrossHint`、`CrucibleZuFrenzyHint`、`CrucibleZuRear`、`XBMB16Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B17 奇子·卡托布莱帕斯

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·卡托布莱帕斯 / 14577 | [Catoblepas Piece](https://ffxiv.consolegameswiki.com/wiki/Catoblepas_Piece) | 风 | 48510 恶魔义眼；48512 邪光 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48506 | 圆形爆炸 / Nearburst | 0.5 | — | 代码关联；非该组官方展示项 |
| 48508 | 环形爆炸 / Farburst | 0.5 | — | 代码关联；非该组官方展示项 |
| 48509 | 魔眼移植 / Shifting Gaze | 3 | — | 代码关联；非该组官方展示项 |
| 48510 | 恶魔义眼 / False Demon Eye | 0 | — | 未标 / 全体 / 石化 [4891] |
| 48512 | 邪光 / Sinister Gleam | 6.5 | Cone(60f, 90f.Degrees()) | 未标 / 前方范围 |

当前组件：`CrucibleBoundary`、`XBMB17AOE`、`CrucibleCatoblepasEyes`、`CrucibleCatoblepasGaze`、`XBMB17Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B18 奇子·拉哈穆、奇子·巨像

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·拉哈穆 / 14580 | [Lakhamu Piece](https://ffxiv.consolegameswiki.com/wiki/Lakhamu_Piece) | 水 | 48556 滑坡；48561 沙尘暴 |
| 奇子·巨像 / 14581 | [Golem Piece](https://ffxiv.consolegameswiki.com/wiki/Golem_Piece_(Third_Board_of_the_Unbroken)) | 水 | 48555 岩石崩溃；48221 强腕 |

### 攻略线索

- 提前观察巨像攻击方向；移速降低时尤其需要预先到位，并留爆发处理后续巨像。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48221 | 强腕 / Might | 5 | — | 自身 / 单体 / 伤害提高 [1572] |
| 48555 | 岩石崩溃 / Rockslide | 7 | Rect(45, 5); Rect(45f, 5f) | 未标 / 前方范围 |
| 48556 | 滑坡 / Landslip | 8 | Rect(45, 5); Rect(45f, 5f) | 未标 / 全体 |
| 48557 | 大地摇动 / Earth Shaker | 4 | — | 代码关联；非该组官方展示项 |
| 48560 | 大地劈裂 / Earthrender | 3 | Circle(6) | 代码关联；非该组官方展示项 |
| 48561 | 沙尘暴 / Sand Tempest | 5 | — | 未标 / 全体 / 失明 [5389] |
| 48562 | 爆炸 / Burst | 3 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB18AOE`、`CrucibleLahamuKnockback`、`CrucibleLahamuGolems`、`CrucibleLahamuSand`、`CrucibleLahamuEarthshakerHint`、`XBMB18Hint0`、`XBMB18Hint1`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B19 奇子·塞壬、奇子·蹒跚鬼、奇子·爬行鬼

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·塞壬 / 14583 | [Siren Piece](https://ffxiv.consolegameswiki.com/wiki/Siren_Piece) | 突刺 | 48563 苦闷之歌；48575 亡者之歌 |
| 奇子·蹒跚鬼 / 14585 | [Shambling Piece](https://ffxiv.consolegameswiki.com/wiki/Shambling_Piece) | 火 | 50662 敲击 |
| 奇子·爬行鬼 / 14586 | [Crawling Piece](https://ffxiv.consolegameswiki.com/wiki/Crawling_Piece) | 火 | 48568 死亡引领 |

### 攻略线索

- 亡者之歌通过动作区分近身圆与月环：作者描述双手抱胸为近身圆、双手垂下为月环；上天后召小怪需要范围输出。动作与技能ID尚需对应验证。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48563 | 苦闷之歌 / Song of Torment | 5 | — | 玩家 / 单体 |
| 48564 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |
| 48565 | 混乱之歌 / Unmooring Melody | 0 | — | 代码关联；非该组官方展示项 |
| 48566 | 混乱之歌 / Unmooring Melody | 0 | — | 代码关联；非该组官方展示项 |
| 48568 | 死亡引领 / Death Throes | 0 | — | 玩家 / 单体 / 止步 [3134] |
| 48570 | 急袭 / Feral Lunge | 4 | Rect(50, 8) | 代码关联；非该组官方展示项 |
| 48575 | 亡者之歌 / Dead Man's Dirge | 7 | Donut(2.58f, 43); Donut(2.58f, 43f) | 自身 / 圆/环范围 / 渐渐混乱 [5424] |
| 50662 | 敲击 / Wallop | 2.5 | Rect(5f, 1.5f) | 未标 / 前方范围 |

当前组件：`CrucibleBoundary`、`XBMB19AOE`、`CrucibleSirenSong`、`XBMB19Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B20 奇子·勇士、奇子·仙人刺、奇子·仙人花、奇子·士兵、奇子·守卫

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·勇士 / 14587 | [Campeador Piece](https://ffxiv.consolegameswiki.com/wiki/Campeador_Piece) | 雷 | 48588 乱针刺；48589 乱针刺 |
| 奇子·仙人刺 / 14588 | [Sabotender Piece](https://ffxiv.consolegameswiki.com/wiki/Sabotender_Piece) | 雷 | 未列技能（不代表没有攻击） |
| 奇子·仙人花 / 14589 | [Flowertender Piece](https://ffxiv.consolegameswiki.com/wiki/Flowertender_Piece) | 雷 | 48579 治愈之水 |
| 奇子·士兵 / 14590 | [Soldado Piece](https://ffxiv.consolegameswiki.com/wiki/Soldado_Piece) | 雷 | 48581 ？针刺 |
| 奇子·守卫 / 14591 | [Guardia Piece](https://ffxiv.consolegameswiki.com/wiki/Guardia_Piece) | 冰 | 48582 仙人盾 |

### 攻略线索

- 随机仙人掌组需准备范围清怪；作者把数字点名描述为圆形近身攻击。名称、数字顺序和对应ID没有展开，保留为待补录线索。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48579 | 治愈之水 / Healing Water | 3 | — | 友方 / 圆形范围 |
| 48581 | ？针刺 / ??? Needles | 8 | Circle(15f) | 自身 / 圆形范围 |
| 48582 | 仙人盾 / Cactguard | 0 | — | 友方 / 单体 / 被保护 [2413] |
| 48588 | 乱针刺 / Seeding Needles | 6 | Donut(5f, 40f) | 自身 / 圆/环范围 |
| 48589 | 乱针刺 / Seeding Needles | 5 | — | 未标 / 全体 |

当前组件：`XBMB20AOE`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B21 贪食无厌 加特勒、奇子·塔纳托斯

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 贪食无厌 加特勒 / 14592 | [Guttler the Gutter](https://ffxiv.consolegameswiki.com/wiki/Guttler_the_Gutter) | 无 | 48607 大奔流；48620 霹雳 |
| 奇子·塔纳托斯 / 14595 | [Thanatos Piece](https://ffxiv.consolegameswiki.com/wiki/Thanatos_Piece_(Third_Board_of_the_Unbroken)) | 雷 | 50540 地狱之痛 |

### 攻略线索

- 闪现后的斧头动作提示站位：高举时远离，胸前持斧时去背后；后续牢笼避免中轴。动作判读和牢笼覆盖范围仍须以实录核验。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48597 | 魔力爆炸 / Magical Combustion | 5 | Circle(8) | 代码关联；非该组官方展示项 |
| 48603 | 贪食啮噬 / Gluttonous Goring | 11.6 | Circle(40) | 代码关联；非该组官方展示项 |
| 48605 | 垂直驱动 / Gyrocleave | 7 | Rect(80, 10) | 代码关联；非该组官方展示项 |
| 48607 | 大奔流 / Beastly Aura | 7 | Rect(80, 40) | 未标 / 全体 |
| 48611 | 剧毒魔刃 / Life Claim | 3 | — | 代码关联；非该组官方展示项 |
| 48614 | 支配魔刃 / Overpowering Point | 3.5 | Rect(60, 3) | 代码关联；非该组官方展示项 |
| 48620 | 霹雳 / Thunderbolt | 5 | Rect(50, 3); Rect(50f, 3f) | 玩家 / 前方范围 / 麻痹 [5388] |
| 50540 | 地狱之痛 / Infernal Pain | 8 | — | 未标 / 全体 |

当前组件：`CrucibleBoundary`、`XBMB21AOE`、`CrucibleGatlerTorrent`、`CrucibleGatlerExplosions`、`CrucibleGatlerTraps`、`XBMB21Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

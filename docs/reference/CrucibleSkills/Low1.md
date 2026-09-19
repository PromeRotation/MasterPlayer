# 普通第一盘技能资料

区域ID：1339；[线上盘面目录](https://ffxiv.consolegameswiki.com/wiki/First_Board_of_the_Unbroken)；[资料总览](README.md)。

“官方展示”来自客户端选怪详情；完整已知列表另含当前源码记录。形状是现有代码参数，表读条不是伤害时间，具体证据见catalog.json。

## B01 奇子·骑士、奇子·主教

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·骑士 / 14531 | [Bone Knight](https://ffxiv.consolegameswiki.com/wiki/Bone_Knight) | 打击 | 46866 古墓；46871 骨化 |
| 奇子·主教 / 14532 | [Bone Bishop](https://ffxiv.consolegameswiki.com/wiki/Bone_Bishop_(First_Board_of_the_Unbroken)) | 打击 | 46868 死亡螺旋；46870 古代疾风 |

### 攻略线索

- 攻略指出骑士举盾时可从背后攻击；放下盾牌后会迅速普攻。优先击杀顺序属于作者队伍与评分策略，不能固化为所有队伍的唯一解法。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 46864 | 前线护卫 / Forward Guard | 5 | — | 代码关联；非该组官方展示项 |
| 46866 | 古墓 / Tumulus | 5 | Circle(6f) | 自身 / 圆形范围 |
| 46867 | 死亡螺旋 / Death Spiral | 5 | — | 代码关联；非该组官方展示项 |
| 46868 | 死亡螺旋 / Death Spiral | 6 | Donut(4f, 40f) | 自身 / 环形范围 |
| 46869 | 古代疾风 / Ancient Aero | 5 | — | 代码关联；非该组官方展示项 |
| 46870 | 古代疾风 / Ancient Aero | 5.7 | Rect(40f, 4f) | 未标 / 前方范围 / 裂伤 [4880] |
| 46871 | 骨化 / Ossify | 8 | — | 自身 / 单体 / 物理伤害提高 [2074] |
| 46873 | 黑火喷发 / Black Eruption | 5 | — | 代码关联；非该组官方展示项 |
| 46874 | 黑火喷发 / Black Eruption | 6 | Circle(5f) | 代码关联；非该组官方展示项 |
| 46900 | 黑火喷发 / Black Eruption | 1.5 | Circle(5f) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB01AOE`、`XBMB01Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B02 奇子·上级恶魔

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·上级恶魔 / 14533 | [Arch Demon Piece](https://ffxiv.consolegameswiki.com/wiki/Arch_Demon_Piece) | 雷 | 46886 深渊回转 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 46875 | 深渊飞刺 / Abyssal Charge | 3 | — | 代码关联；非该组官方展示项 |
| 46876 | 深渊飞刺 / Abyssal Charge | 3 | Rect(40f, 2f) | 代码关联；非该组官方展示项 |
| 46877 | 肢解 / Dismember | 3 | — | 代码关联；非该组官方展示项 |
| 46878 | （客户端未命名） / — | 4 | — | 代码关联；非该组官方展示项 |
| 46879 | 肢解 / Dismember | 4.5 | Rect(35f, 4f) | 代码关联；非该组官方展示项 |
| 46880 | 深渊贯穿 / Abyssal Transfixion | 3 | — | 代码关联；非该组官方展示项 |
| 46882 | 深渊贯穿 / Abyssal Transfixion | 3.7 | Circle(6f) | 代码关联；非该组官方展示项 |
| 46884 | 深渊贯穿 / Abyssal Transfixion | 3.5 | Circle(3f) | 代码关联；非该组官方展示项 |
| 46886 | 深渊回转 / Abyssal Swing | 5.7 | Cone(40f, 90f.Degrees()) | 未标 / 前方范围 |

当前组件：`CrucibleBoundary`、`XBMB02AOE`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B03 奇子·夺灵魔

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·夺灵魔 / 14535 | [Piscodemon Piece](https://ffxiv.consolegameswiki.com/wiki/Piscodemon_Piece) | 斩击 | 46898 澄澈之心；46892 虚空暴雷 |

### 攻略线索

- 攻略第一盘第二战提到连续地火可利用前一轮已判定区域移动。原文称其为‘夺心魔’，与第一盘名录‘夺灵魔’不一致，按盘数保留线索，不能混入高二夺心魔规则。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 46889 | 虚空冰封 / Void Blizzard III | 6 | Circle(5f) | 代码关联；非该组官方展示项 |
| 46890 | 虚空冰封 / Void Blizzard III | 0 | — | 代码关联；非该组官方展示项 |
| 46892 | 虚空暴雷 / Void Thunder III | 5 | Cross(50f, 5f) | 地面 / 十字范围 |
| 46893 | 虚无耀星 / Void Flare Star | 2.5 | — | 代码关联；非该组官方展示项 |
| 46894 | 虚无耀星 / Void Flare Star | 3 | — | 代码关联；非该组官方展示项 |
| 46895 | 虚无耀星 / Void Flare Star | 6 | — | 代码关联；非该组官方展示项 |
| 46897 | 虚空暴风 / Void Aero III | 6 | Donut(5f, 60f) | 代码关联；非该组官方展示项 |
| 46898 | 澄澈之心 / Clear Mind | 4 | — | 自身 / 单体 / 伤害提高 [1225] |
| 49886 | 虚空冰封 / Void Blizzard III | 4.5 | Circle(5f) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB03AOE`、`CrucibleVoidFlare`、`CrucibleVoidFlareHint`、`XBMB03Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B04 奇子·祸蛛蝎、奇子·灵蛛蝎

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·祸蛛蝎 / 14536 | [Banemite Piece](https://ffxiv.consolegameswiki.com/wiki/Banemite_Piece) | 冰 | 46906 致命尾刺 |
| 奇子·灵蛛蝎 / 14537 | [Miteling Piece](https://ffxiv.consolegameswiki.com/wiki/Miteling_Piece) | 冰 | 46909 蛛网屏 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 46901 | 地面隆起 / Bedrock Uplift | 4 | — | 代码关联；非该组官方展示项 |
| 46902 | 地面隆起 / Bedrock Uplift | 5 | Circle(6f) | 代码关联；非该组官方展示项 |
| 46903 | 地面隆起 / Bedrock Uplift | 7 | Donut(6f, 12f) | 代码关联；非该组官方展示项 |
| 46904 | 地面隆起 / Bedrock Uplift | 9 | Donut(12f, 18f) | 代码关联；非该组官方展示项 |
| 46905 | 地面隆起 / Bedrock Uplift | 11 | Donut(18f, 24f) | 代码关联；非该组官方展示项 |
| 46906 | 致命尾刺 / Deadly Thrust | 5 | — | 玩家 / 单体 / 中毒 [5140] |
| 46907 | 毒蛛网 / Venom Web | 3 | — | 代码关联；非该组官方展示项 |
| 46908 | 毒蛛网 / Venom Web | 6 | Circle(9f) | 代码关联；非该组官方展示项 |
| 46909 | 蛛网屏 / Silkscreen | 5 | Rect(40f, 2f) | 未标 / 前方范围 / 加重 [5356] |

当前组件：`CrucibleBoundary`、`XBMB04AOE`、`CrucibleGroundUpliftAOEs`、`CrucibleWebAOEs`、`XBMB04Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B05 奇子·食人魔、奇子·妖火、奇子·大妖火

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·食人魔 / 14538 | [Ogre Piece](https://ffxiv.consolegameswiki.com/wiki/Ogre_Piece) | 风 | 46912 燃烧猛击；46920 碎骨拳 |
| 奇子·妖火 / 14539 | [Wisp Piece](https://ffxiv.consolegameswiki.com/wiki/Wisp_Piece) | 水 | 46919 大火焰 |
| 奇子·大妖火 / 14748 | [Great Wisp Piece](https://ffxiv.consolegameswiki.com/wiki/Great_Wisp_Piece) | 水 | 46919 大火焰 |

### 攻略线索

- 火灵守护阶段涉及小妖火和大妖火处理；作者建议准备远程/范围输出清理召唤物。高伤害跳过阶段的打法不代表该阶段没有机制。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 46910 | 燃烧猛击 / Scorching Smite | 6 | — | 代码关联；非该组官方展示项 |
| 46912 | 燃烧猛击 / Scorching Smite | 6 | Cone(40f, 60f.Degrees()) | 未标 / 前方范围 |
| 46913 | 燃烧猛击 / Scorching Smite | 5 | — | 代码关联；非该组官方展示项 |
| 46915 | 猛火喷发 / Allfire | 4 | — | 代码关联；非该组官方展示项 |
| 46916 | 熔岩 / Magma | 3 | Circle(3f) | 代码关联；非该组官方展示项 |
| 46917 | 熔岩 / Magma | 3 | Circle(5f) | 代码关联；非该组官方展示项 |
| 46918 | 火灵的守护 / Burning Ward | 3 | — | 代码关联；非该组官方展示项 |
| 46919 | 大火焰 / Scorched Earth | 0 | — | 未标 / 全体 |
| 46920 | 碎骨拳 / Straight Punch | 5 | — | 玩家 / 单体 |
| 46921 | 火球生成 / Fire Call | 4 | — | 代码关联；非该组官方展示项 |
| 46922 | 延烧 / Arm of Purgatory | 1 | — | 代码关联；非该组官方展示项 |
| 49688 | 燃烧猛击 / Scorching Smite | 9.3 | Cone(40f, 60f.Degrees()) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB05AOE`、`CrucibleLava`、`CrucibleFireSpiritWard`、`CrucibleFireball`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B06 魅惑女妖 帕德索、奇子·梦魔法师、奇子·梦魔骑士

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 魅惑女妖 帕德索 / 14541 | [Pas de Seul](https://ffxiv.consolegameswiki.com/wiki/Pas_de_Seul) | 突刺 | 46924 血雨；46935 寒毒接触 |
| 奇子·梦魔法师 / 14542 | [Succubus Mage](https://ffxiv.consolegameswiki.com/wiki/Succubus_Mage) | 火 | 46928 盲信；46929 虚空烈炎 |
| 奇子·梦魔骑士 / 14543 | [Succubus Knight](https://ffxiv.consolegameswiki.com/wiki/Succubus_Knight) | 火 | 46930 甜钢 |

### 攻略线索

- 血雨通过动作区分近身圆与月环：作者描述高举手且手上有光球为近身圆；虚空烈风先直线并在边缘留下风球，再接扇形，建议提前站目标圈侧面。小怪阶段不能稳定跳过时优先处理小怪。动作告诉仍需逐帧或ARR对应验证。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 46923 | 血雨 / Blood Rain | 5 | — | 代码关联；非该组官方展示项 |
| 46924 | 血雨 / Blood Rain | 6 | Donut(8f, 40f) | 自身 / 圆/环范围 |
| 46925 | 血雨 / Blood Rain | 5.4 | — | 代码关联；非该组官方展示项 |
| 46926 | 血雨 / Blood Rain | 6 | Circle(8f) | 代码关联；非该组官方展示项 |
| 46927 | 召唤 / Summon | 4 | — | 代码关联；非该组官方展示项 |
| 46928 | 盲信 / Fanaticism | 6 | — | 友方 / 单体 / 伤害提高 [1225] |
| 46929 | 虚空烈炎 / Void Fire II | 4 | Circle(10f) | 地面 / 圆形范围 |
| 46930 | 甜钢 / Sweet Steel | 4 | Cone(10f, 60f.Degrees()) | 未标 / 前方范围 |
| 46932 | 虚空烈风 / Void Aero II | 4 | Rect(60f, 4f) | 代码关联；非该组官方展示项 |
| 46933 | 虚空烈风 / Void Aero II | 3 | Cone(60f, 10f.Degrees()) | 代码关联；非该组官方展示项 |
| 46935 | 寒毒接触 / Cold Caress | 5 | — | 最高仇恨 / 单体 / 中毒 [5140] |

当前组件：`CrucibleBoundary`、`XBMB06AOE`、`XBMB06Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

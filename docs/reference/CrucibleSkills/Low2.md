# 普通第二盘技能资料

区域ID：1340；[线上盘面目录](https://ffxiv.consolegameswiki.com/wiki/Second_Board_of_the_Unbroken)；[资料总览](README.md)。

“官方展示”来自客户端选怪详情；完整已知列表另含当前源码记录。形状是现有代码参数，表读条不是伤害时间，具体证据见catalog.json。

## B07 奇子·曼提克

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·曼提克 / 14545 | [Manticore Piece](https://ffxiv.consolegameswiki.com/wiki/Manticore_Piece) | 风 | 48137 重锤跃击；48138 致命拥抱 |

### 攻略线索

- 重锤跃击为大范围近身圆；冲锋前观察发光手臂，作者描述路径经场边三角形再返回中场，冲锋后往另一侧处理后续范围。固定路径描述需与每次实际告诉核对。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48123 | 臂锤 / Arm and Hammer | 5.6 | — | 代码关联；非该组官方展示项 |
| 48125 | 臂锤 / Arm and Hammer | 5.6 | — | 代码关联；非该组官方展示项 |
| 48127 | 猛冲重锤 / Charge and Hammer | 10 | — | 代码关联；非该组官方展示项 |
| 48128 | （客户端未命名） / — | 1.5 | — | 代码关联；非该组官方展示项 |
| 48130 | 狂野冲锋 / Wild Charge | 1.1 | — | 代码关联；非该组官方展示项 |
| 48132 | 臂锤 / Arm and Hammer | 0.6 | — | 代码关联；非该组官方展示项 |
| 48134 | 臂锤 / Arm and Hammer | 0.6 | — | 代码关联；非该组官方展示项 |
| 48137 | 重锤跃击 / Hammerleap | 8.1 | Circle(30f) | 地面 / 圆形范围 |
| 48138 | 致命拥抱 / Deadly Hold | 5 | — | 玩家 / 单体 |
| 48140 | 利爪凶尾 / Heads and Tails | 3.9 | — | 代码关联；非该组官方展示项 |
| 48142 | 利爪凶尾 / Heads and Tails | 2.4 | — | 代码关联；非该组官方展示项 |
| 50411 | 凶尾利爪 / Tails and Heads | 3.9 | — | 代码关联；非该组官方展示项 |
| 50413 | 凶尾利爪 / Tails and Heads | 2.4 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB07AOE`、`CrucibleManticoreHalfArena`、`CrucibleManticoreCharges`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B08 奇子·牛头魔、奇子·冥鬼之眼

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·牛头魔 / 14546 | [Taurus Piece](https://ffxiv.consolegameswiki.com/wiki/Taurus_Piece) | 突刺 | 48143 致命射线；48155 中心破灭 |
| 奇子·冥鬼之眼 / 14548 | [Ahriman Piece](https://ffxiv.consolegameswiki.com/wiki/Ahriman_Piece) | 冰 | 48153 死亡视线 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48143 | 致命射线 / Mortal Ray | 5 | — | 未标 / 全体 / 死亡宣告 [5421] |
| 48153 | 死亡视线 / Mortal Gaze | 5 | — | 未标 / 全体 / 死亡宣告 [5421] |
| 48155 | 中心破灭 / Ruinous Locus | 6 | Circle(8f) | 自身 / 圆形范围 |
| 48157 | 外环破灭 / Ruinous Ring | 6 | Donut(7f, 50f) | 代码关联；非该组官方展示项 |

当前组件：`XBMB08AOE`、`XBMB08Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B09 奇子·双足飞龙

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·双足飞龙 / 14549 | [Wyvern Piece](https://ffxiv.consolegameswiki.com/wiki/Wyvern_Piece_(Enemy)) | 雷 | 48175 炽热弥漫；48168 台风 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48167 | 突风 / Buffet | 6 | Rect(40, 5) | 代码关联；非该组官方展示项 |
| 48168 | 台风 / Typhoon | 3 | — | 未标 / 全体 |
| 48172 | 液体地狱 / Liquid Hell | 6 | Circle(6) | 代码关联；非该组官方展示项 |
| 48175 | 炽热弥漫 / Blazing Trail | 8 | Cone(60, 90f.Degrees()); Cone(60f, 90f.Degrees()) | 未标 / 前方范围 |
| 48178 | 风暴轨迹 / Storm Trail | 6 | Cone(25, 30f.Degrees()) | 代码关联；非该组官方展示项 |
| 48180 | ウィンドスパイラル / ウィンドスパイラル | 6 | Circle(8) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB09AOE`、`CrucibleLiquidHell`、`CrucibleTyphoon`、`CrucibleTornadoes`、`XBMB09Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B10 奇子·虚灵法师、奇子·僵尸

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·虚灵法师 / 14552 | [Voidmancer Piece](https://ffxiv.consolegameswiki.com/wiki/Voidmancer_Piece) | 土 | 50793 流水；48186 暗天球 |
| 奇子·僵尸 / 14553 | [Zombie Piece](https://ffxiv.consolegameswiki.com/wiki/Zombie_Piece) | 冰 | 未列技能（不代表没有攻击） |

### 攻略线索

- 作者使用场地下侧偏左位置引导放圈，并称该处僵尸较少；这是站位经验，未给精确坐标或每轮保证，不能当永久安全点。 [来源](sources.md#bili-notes)
- 本项目当前策略：僵尸优先击杀，不注册僵尸身体或预测绕行；死亡驱动落点、持续瘴气和精神控制路线继续处理。以2026-09-19用户要求为准。 [来源](sources.md#local-user-policy)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48181 | 死亡驱动 / Death Drive | 7 | — | 代码关联；非该组官方展示项 |
| 48182 | 死亡驱动 / Death Drive | 4 | Circle(10) | 代码关联；非该组官方展示项 |
| 48183 | 恶魔毒雾 / Evil Mist | 4 | — | 代码关联；非该组官方展示项 |
| 48184 | 死灵潜质 / Necropurge | 1 | — | 代码关联；非该组官方展示项 |
| 48186 | 暗天球 / Dark Orb | 8 | Circle(18); Circle(18f) | 自身 / 圆形范围 |
| 48187 | 精神控制 / Mindjack | 4 | — | 代码关联；非该组官方展示项 |
| 50793 | 流水 / Water | 0 | — | 最高仇恨 / 单体 |

当前组件：`CrucibleBoundary`、`XBMB10AOE`、`CrucibleZombies`、`CrucibleNecromist`、`CrucibleNecromancerHint`、`CrucibleDeathDrive`、`CrucibleNecromancerMarch`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B11 奇子·牛魔老哥、奇子·牛魔老弟

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·牛魔老哥 / 14555 | [Elder Tablitaur Piece](https://ffxiv.consolegameswiki.com/wiki/Elder_Tablitaur_Piece) | 火 | 48189 千吨横扫；48193 千百十一吨回转 |
| 奇子·牛魔老弟 / 14556 | [Younger Tablitaur Piece](https://ffxiv.consolegameswiki.com/wiki/Younger_Tablitaur_Piece) | 火 | 48203 声援 |

### 攻略线索

- 两只牛魔需要压平血量、接近同时击杀，并分别观察读条；不可只顾一只而忽略另一只。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48189 | 千吨横扫 / 1000-tonze Swipe | 6.5 | — | 未标 / 前方范围 |
| 48190 | 千吨横扫 / 1000-tonze Swipe | 7 | Rect(60, 30) | 代码关联；非该组官方展示项 |
| 48192 | 千吨横扫 / 1000-tonze Swipe | 10 | Rect(60, 30) | 代码关联；非该组官方展示项 |
| 48193 | 千百十一吨回转 / 1111-tonze Swing | 7 | — | 自身 / 圆形范围 |
| 48194 | 千百十一吨回转 / 1111-tonze Swing | 8 | Circle(23) | 代码关联；非该组官方展示项 |
| 48196 | 千百十一吨回转 / 1111-tonze Swing | 14 | Circle(23) | 代码关联；非该组官方展示项 |
| 48198 | 十吨重踏 | 7 | Circle(5)，包内落点 | 22:50 ARR + 客户端字段 |
| 48199 | 冲击波 | 7 | 径向击退 20m | 效果包 Knockback=96；不画全场月环 |
| 48201 | 十吨重踏 | 10.5 | Circle(5)，包内落点 | 第二段，实际比首段晚 3.462s |
| 48202 | 冲击波 | 10.5 | 径向击退 20m | 从上一段落点继续推算，不从初始位置重算 |
| 48203 | 声援 / Rallying Cheer | 7 | — | 友方 / 单体 / 伤害提高 [1225] |
| 48206 | 无尽回转 / Endless Swing | 6 | Circle(8) | 代码关联；非该组官方展示项 |
| 48210 | 无尽横扫 / Endless Swipes | 5.2 | — | 代码关联；非该组官方展示项 |
| 48212 | 无尽横扫 / Endless Swipes | 0.2 | — | 代码关联；非该组官方展示项 |
| 48214 | 无尽挥打 / Endless Slashes | 9 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB11AOE`、`CrucibleMinotaurSequence`、`CrucibleMinotaurStomp`、`CrucibleMinotaurRotation`、`CrucibleMinotaurEnrageHint`。

22:50 ARR 补充：首击就准备两次击退均留在电网内的站位；仅在首击后临时跑位可能来不及。新增字段与实战时序见 [迁移规格](../CrucibleAll.spec.md)，早期 `catalog.json` 收集快照尚不含本次新增动作。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B12 奇子·恶魔、奇子·恶魔兵装、奇子·小恶魔

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·恶魔 / 14557 | [Demon Piece](https://ffxiv.consolegameswiki.com/wiki/Demon_Piece) | 斩击 | 48247 虚无麻痹；48223 定罪 |
| 奇子·恶魔兵装 / 14558 | [Evil Weapon Piece](https://ffxiv.consolegameswiki.com/wiki/Evil_Weapon_Piece) | 冰 | 48218 狂怒一击；48219 狂怒一击 |
| 奇子·小恶魔 / 14559 | [Devilet Piece](https://ffxiv.consolegameswiki.com/wiki/Devilet_Piece) | 土 | 48222 虚空爆炎 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48218 | 狂怒一击 / Smite of Excessive Rage | 7 | Rect(40f, 5f) | 未标 / 前后范围 |
| 48219 | 狂怒一击 / Smite of Excessive Rage | 7 | Donut(4f, 30f) | 自身 / 环形范围 |
| 48222 | 虚空爆炎 / Void Fire III | 10 | — | 未标 / 全体 |
| 48223 | 定罪 / Condemnation | 3 | Cone(12f, 45f.Degrees()) | 未标 / 前方范围 |
| 48247 | 虚无麻痹 / Void Paralyze | 5 | — | 最高仇恨 / 单体 / 麻痹 [5382] |

当前组件：`XBMB12AOE`、`XBMB12Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B13 寻兽探奇 路斯福洛克斯、小地豆

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 寻兽探奇 路斯福洛克斯 / 14561 | [Loosefrox Inkyjots](https://ffxiv.consolegameswiki.com/wiki/Loosefrox_Inkyjots) | 打击 | 48242 借我力量哥布；48229 哥布流回旋剑 |
| 小地豆 / 14562 | [Chewchum Popoto](https://ffxiv.consolegameswiki.com/wiki/Chewchum_Popoto) | 水 | 48227 黄沙吐息 |

### 攻略线索

- 哥布林回旋剑通过抬剑动作区分近身圆与月环：作者描述剑高举过头并出现圆环为月环，其他形态为近身圆。未提供帧数、判定延迟或精确半径。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48225 | 沙柱 / Sand Pillar | 0 | — | 代码关联；非该组官方展示项 |
| 48227 | 黄沙吐息 / Sand Breath | 9 | Cone(60, 45f.Degrees()); Cone(60f, 45f.Degrees()) | 未标 / 前方范围 / 失明 [5381] |
| 48229 | 哥布流回旋剑 / Gobspin Headlops | 6 | Circle(8); Circle(8f) | 自身 / 圆/环范围 |
| 48231 | 哥布流回旋剑 | 6 | Donut(4,40)，包内落点 | Omen=gl_sircle_4004bp1；22:50 ARR 的月环分支 |
| 48235 | 爆炸 / Explosion | 2 | — | 代码关联；非该组官方展示项 |
| 48239 | 地震 / Earthquake | 5 | — | 代码关联；非该组官方展示项 |
| 48242 | 借我力量哥布 / Kinborrow | 4 | — | 自身 / 单体 / 小地豆肤 [5423] |
| 48519 | 投弹 / Bomb Toss | 4 | Circle(6) | 代码关联；非该组官方展示项 |
| 50452 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB13AOE`、`CrucibleSandpits`、`CrucibleGoblinBombs`、`CrucibleSandPillars`、`CrucibleWormQuakeHint`、`XBMB13Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

# 高段第二盘技能资料

区域ID：1343；[线上盘面目录](https://ffxiv.consolegameswiki.com/wiki/Second_Master%27s_Board)；[资料总览](README.md)。

“官方展示”来自客户端选怪详情；完整已知列表另含当前源码记录。形状是现有代码参数，表读条不是伤害时间，具体证据见catalog.json。

## B32 奇子·佛劳洛斯、雷元精

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·佛劳洛斯 / 14631 | [Flauros Piece](https://ffxiv.consolegameswiki.com/wiki/Flauros_Piece) | 土 | 49193 蓄电雷击；49189 飘忽爆破 |
| 雷元精 / 14632 | [Lightning Sprite](https://ffxiv.consolegameswiki.com/wiki/Lightning_Sprite_(Second_Master%27s_Board_-_Enemy_1)) | 土 | 49194 线状放电 |

### 攻略线索

- 攻略主要讨论通过宠物承受单体高伤；没有给佛劳洛斯多激光的精确方向/时序，需继续使用实录和代码记录。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49186 | 惊电 / Heat Lightning | 4 | Circle(6) | 代码关联；非该组官方展示项 |
| 49189 | 飘忽爆破 / Erratic Blaster | 7 | — | 玩家 / 单体 / 麻痹 [5382] |
| 49191 | 电流冲击 / Electric Shock | 6 | Circle(16) | 代码关联；非该组官方展示项 |
| 49193 | 蓄电雷击 / Charged Lightning | 5.8 | Rect(50, 20); Rect(50f, 20f) | 未标 / 前方范围 |
| 49194 | 线状放电 / Line Voltage | 2 | Rect(100, 1); Rect(100f, 1f) | 未标 / 前方范围 |
| 49195 | 线状放电 / Line Voltage | 3 | Rect(100, 3) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB32AOE`、`XBMB32Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B33 奇子·夺心魔、奇子·毒性魔菇

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·夺心魔 / 14633 | [Mindflayer Piece](https://ffxiv.consolegameswiki.com/wiki/Mindflayer_Piece) | 斩击 | 49200 虚空狂水；49208 虚空强麻痹 |
| 奇子·毒性魔菇 / 14634 | [Myconid Piece](https://ffxiv.consolegameswiki.com/wiki/Myconid_Piece) | 水 | 49196 飞散；49198 大爆炸 |

### 攻略线索

- 先取得Boss仇恨、将宠物移到场边，引导虚空狂水；蘑菇出现后利用水池交汇处集中处理，击杀后及时离开毒区。作者指出站水中承受雷属性普攻会增加血量压力，召唤兽重新取得仇恨也可能把点名带回玩家身边。 [来源](sources.md#bili-notes)
- 兽道III攻略同样强调夺心魔早期装备不足时的范围清怪、拉怪和承伤压力。该简介未给水雷圆的半径或分离距离。 [来源](sources.md#bili-guide)
- 现有本地参考策略是水R8、雷R6整圈分离，雷点名放到水圈外；该尺寸及策略来自客户端、组件与本地修复，不是网页独立确认。 [来源](sources.md#local-current-fix)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49196 | 飞散 / Spore Spill | 1 | Circle(6); Circle(6f) | 自身 / 圆形范围 / 中毒 [5140] |
| 49197 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |
| 49198 | 大爆炸 / Big Burst | 0 | — | 未标 / 全体 / 中毒 [5140] |
| 49199 | 虚空狂水 / Void Water III | 5 | — | 代码关联；非该组官方展示项 |
| 49200 | 虚空狂水 / Void Water III | 0 | Circle(8f) | 玩家 / 圆形范围 / 水属性耐性降低 [5021] |
| 49203 | 黑暗奔流 / Dark Current | 2 | Rect(100, 2) | 代码关联；非该组官方展示项 |
| 49204 | 黑暗奔流 / Dark Current | 2 | Rect(100, 5) | 代码关联；非该组官方展示项 |
| 49205 | 虚空暴雷 / Void Thunder III | 5 | — | 代码关联；非该组官方展示项 |
| 49206 | 虚空暴雷 / Void Thunder III | 0 | — | 代码关联；非该组官方展示项 |
| 49207 | 虚空暴雷 / Void Thunder III | 4 | Cross(50, 5) | 代码关联；非该组官方展示项 |
| 49208 | 虚空强麻痹 / Void Paralyze III | 7 | — | 未标 / 全体 / 麻痹 [5382] |
| 50939 | （客户端未命名） / — | 4 | Cross(50, 5) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB33AOE`、`CrucibleMindflayerAdds`、`CrucibleFungalBeds`、`CrucibleMindflayerWater`、`CrucibleMindflayerThunder`、`XBMB33Hint0`、`XBMB33Hint1`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B34 奇子·夜魔人、奇子·深瞳、奇子·爆弹怪、光元精

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·夜魔人 / 14638 | [Boogyman Piece](https://ffxiv.consolegameswiki.com/wiki/Boogyman_Piece) | 冰 | 49231 暗黑冲击；49209 黑暗帷幕 |
| 奇子·深瞳 / 14639 | [Deepeye Piece](https://ffxiv.consolegameswiki.com/wiki/Deepeye_Piece) | 风 | 49214 石化视线 |
| 奇子·爆弹怪 / 14640 | [Bomb Piece](https://ffxiv.consolegameswiki.com/wiki/Bomb_Piece_(Second_Master%27s_Board)) | 风 | 49211 自爆；49210 爆炸 |
| 光元精 / 14641 | [Light Sprite](https://ffxiv.consolegameswiki.com/wiki/Light_Sprite_(Second_Master%27s_Board)) | 无 | 49213 放逐；49212 光散射 |

### 攻略线索

- 攻略简介提到右侧随机格的隐身怪会施加物理易伤，且同时有多只小怪；未给现身条件及精确技能时间。 [来源](sources.md#guide-bv1n3yy6jepl)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49209 | 黑暗帷幕 / Veil of Darkness | 4 | — | 未标 / 全体 / 无形 [616] |
| 49210 | 爆炸 / Explosion | 5 | Circle(9f) | 自身 / 圆形范围 |
| 49211 | 自爆 / Self-destruct | 8 | — | 未标 / 全体 |
| 49212 | 光散射 / Diffuse Light | 1.5 | Cone(20f, 22.5f.Degrees()) | 未标 / 前方范围 |
| 49213 | 放逐 / Banish | 1.5 | — | 最高仇恨 / 单体 |
| 49214 | 石化视线 / Oogle | 10 | — | 未标 / 全体 / 石化 [4891] |
| 49231 | 暗黑冲击 / Ripples of Gloom | 5 | — | 未标 / 全体 / 失明 [5389] |

当前组件：`XBMB34AOE`、`XBMB34Hint0`、`XBMB34Hint1`、`XBMB34Hint2`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B35 奇子·阿托莫斯、奇子·红格雷姆林、奇子·格雷姆林、奇子·软糊怪、奇子·奶冻怪、奇子·甜羹怪、奇子·石像魔、奇子·威胁扎哈克

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·阿托莫斯 / 14642 | [Atomos Piece](https://ffxiv.consolegameswiki.com/wiki/Atomos_Piece) | 打击 | 49238 震荡波；49240 昏暗 |
| 奇子·红格雷姆林 / 14643 | [Red Gremlin Piece](https://ffxiv.consolegameswiki.com/wiki/Red_Gremlin_Piece) | 突刺 | 49241 臭嘴 |
| 奇子·格雷姆林 / 14644 | [Gremlin Piece](https://ffxiv.consolegameswiki.com/wiki/Gremlin_Piece) | 突刺 | 未列技能（不代表没有攻击） |
| 奇子·软糊怪 / 14645 | [Pudding Piece](https://ffxiv.consolegameswiki.com/wiki/Pudding_Piece) | 风 | 49243 爆炎 |
| 奇子·奶冻怪 / 14646 | [Bavarois Piece](https://ffxiv.consolegameswiki.com/wiki/Bavarois_Piece) | 雷 | 49242 狂水 |
| 奇子·甜羹怪 / 14647 | [Flan Piece](https://ffxiv.consolegameswiki.com/wiki/Flan_Piece) | 土 | 49246 暴雷 |
| 奇子·石像魔 / 14648 | [Vodoriga Piece](https://ffxiv.consolegameswiki.com/wiki/Vodoriga_Piece) | 突刺 | 49247 恐怖眼 |
| 奇子·威胁扎哈克 / 14649 | [Menacing Dahak Piece](https://ffxiv.consolegameswiki.com/wiki/Menacing_Dahak_Piece) | 冰 | 49248 腐烂吐息；49249 尾部打击 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49233 | 聚魔 / Mana Manifestation | 4 | Circle(8) | 代码关联；非该组官方展示项 |
| 49236 | 爆炸 / Explosion | 2 | Circle(6) | 代码关联；非该组官方展示项 |
| 49237 | 爆炸 / Explosion | 2 | Circle(12) | 代码关联；非该组官方展示项 |
| 49238 | 震荡波 / Shockwave | 6 | — | 未标 / 全体 |
| 49240 | 昏暗 / Dark II | 7 | Cone(60, 60f.Degrees()); Cone(60f, 60f.Degrees()) | 未标 / 前方范围 |
| 49241 | 臭嘴 / Bad-mouth | 3 | — | 玩家 / 单体 / 物理受伤加重 [126] |
| 49242 | 狂水 / Water III | 2 | — | 未标 / 全体 |
| 49243 | 爆炎 / Fire III | 8 | Circle(6f) | 玩家 / 圆形范围 |
| 49244 | 暴雷 / Thunder III | 8 | — | 代码关联；非该组官方展示项 |
| 49245 | 暴雷 / Thunder III | 8 | Circle(5) | 代码关联；非该组官方展示项 |
| 49246 | 暴雷 / Thunder III | 0 | Circle(5f) | 玩家 / 圆形范围 |
| 49247 | 恐怖眼 / Terror Eye | 3 | Circle(6); Circle(6f) | 地面 / 圆形范围 |
| 49248 | 腐烂吐息 / Rotten Breath | 5 | Cone(28, 60f.Degrees()); Cone(28f, 60f.Degrees()) | 未标 / 前方范围 |
| 49249 | 尾部打击 / Tail Drive | 5 | Cone(30, 45f.Degrees()); Cone(30f, 45f.Degrees()) | 未标 / 后方范围 |

当前组件：`CrucibleBoundary`、`XBMB35AOE`、`CrucibleAtomosShockwave`、`CrucibleJellyWater`、`CrucibleJellyFire`、`CrucibleJellyThunder`、`XBMB35Hint0`、`XBMB35Hint1`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B36 奇子·火蛟、奇子·棘鼹、奇子·狱蟾蜍、奇子·蓝闪蝶

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·火蛟 / 14651 | [Drake Piece](https://ffxiv.consolegameswiki.com/wiki/Drake_Piece) | 风 | 49253 火棘屏障；49254 裂伤之牙 |
| 奇子·棘鼹 / 14653 | [Barbmole Piece](https://ffxiv.consolegameswiki.com/wiki/Barbmole_Piece) | 火 | 49250 射针；49252 乱针刺 |
| 奇子·狱蟾蜍 / 14655 | [Abaddon Piece](https://ffxiv.consolegameswiki.com/wiki/Abaddon_Piece) | 水 | 未列技能（不代表没有攻击） |
| 奇子·蓝闪蝶 / 14656 | [Morpho Piece](https://ffxiv.consolegameswiki.com/wiki/Morpho_Piece) | 打击 | 49260 迷惑鳞粉；49261 混乱鳞粉 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49250 | 射针 / Needles Out | 3 | — | 自身 / 单体 / 射针 [5145] |
| 49252 | 乱针刺 / Seeding Needles | 5 | Circle(20f) | 未标 / 全体 |
| 49253 | 火棘屏障 / Blaze Spikes | 3 | — | 自身 / 单体 / 火棘屏障 [5465] |
| 49254 | 裂伤之牙 / Mangling Fang | 5 | — | 玩家 / 单体 |
| 49260 | 迷惑鳞粉 / Unwitting Wings | 4 | — | 玩家 / 单体 / 渐渐混乱 [5146] |
| 49261 | 混乱鳞粉 / Delusion Dust | 0 | — | 未标 / 全体 / 混乱 [1283] |

当前组件：`XBMB36AOE`、`XBMB36Hint0`、`XBMB36Hint1`、`XBMB36Hint2`、`XBMB36Hint3`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B37 奇子·杜尔迦、奇子·转盘堡

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·杜尔迦 / 14657 | [Durga Piece](https://ffxiv.consolegameswiki.com/wiki/Durga_Piece) | 火 | 49272 原子射线；49265 雷气释放 |
| 奇子·转盘堡 / 14659 | [Spinner-rook Piece](https://ffxiv.consolegameswiki.com/wiki/Spinner-rook_Piece) | 打击 | 49271 以太供应 |

### 攻略线索

- 导弹阶段建议保持同一方向移动，不左右来回引导射线；三大圈加小圈后的转身机制，要记住相对Boss面向的安全侧。后者没有提供旋转角度与坐标，只作解法线索。 [来源](sources.md#bili-notes)
- 视频简介建议准备打断与转场承伤，并保留范围攻击处理机器人；没有提供导弹精确路径参数。 [来源](sources.md#guide-bv1a6yv6bewh)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49265 | 雷气释放 / Grounding Jolt | 5.2 | Circle(20); Circle(20f) | 地面 / 圆形范围 |
| 49266 | 雷气释放 / Grounding Jolt | 5.2 | Circle(6) | 代码关联；非该组官方展示项 |
| 49267 | 导弹 / Missile | 3 | — | 代码关联；非该组官方展示项 |
| 49268 | 突击 / Voyage | 1 | — | 代码关联；非该组官方展示项 |
| 49270 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |
| 49271 | 以太供应 / Aether Charge | 3 | — | 友方 / 单体 |
| 49272 | 原子射线 / Atomic Ray | 13 | — | 未标 / 全体 |
| 49273 | 气化炸弹 / Thermobaric Charge | 4.2 | — | 代码关联；非该组官方展示项 |
| 49274 | 气化炸弹 / Thermobaric Charge | 2 | — | 代码关联；非该组官方展示项 |
| 50688 | 突击 / Voyage | 1.5 | Rect(100, 2) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB37AOE`、`CrucibleDurgaEdgeKnockback`、`CrucibleDurgaRayHint`、`CrucibleDurgaAdds`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B38 奇子·美杜莎、奇子·拉米亚、奇子·独眼巨人

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·美杜莎 / 14660 | [Medusa Piece](https://ffxiv.consolegameswiki.com/wiki/Medusa_Piece) | 突刺 | 49278 石化光照射；49298 祸乱 |
| 奇子·拉米亚 / 14661 | [Lamia Piece](https://ffxiv.consolegameswiki.com/wiki/Lamia_Piece) | 突刺 | 49286 恐惧瘴气；49287 复活 |
| 奇子·独眼巨人 / 14662 | [Cyclops Piece](https://ffxiv.consolegameswiki.com/wiki/Cyclops_Piece_(Second_Master%27s_Board_-_Random_3)) | 无 | 49297 千吨回转；49370 怒视 |

### 攻略线索

- 作者提到美杜莎小怪会复活、承伤较高；图文攻略建议保留爆发到召怪后。复活事件触发条件需要额外实录确认。 [来源](sources.md#bili-guide)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49277 | 石化光照射 / Petrifying Regard | 3 | — | 代码关联；非该组官方展示项 |
| 49278 | 石化光照射 / Petrifying Regard | 3 | Cone(60, 22.5f.Degrees()); Cone(60f, 22.5f.Degrees()) | 未标 / 前方范围 / 石化的诅咒 [437] |
| 49279 | 祸乱石化光 / Petrifying Passion | 3 | — | 代码关联；非该组官方展示项 |
| 49281 | 祸乱石化光 / Petrifying Passion | 3 | Cone(60, 22.5f.Degrees()) | 代码关联；非该组官方展示项 |
| 49284 | 烈火回环 / Circle of Flames | 4 | Circle(5); Circle(5f) | 代码关联；非该组官方展示项 |
| 49286 | 恐惧瘴气 / Terrorizing Miasma | 4 | Circle(6); Circle(6f) | 自身 / 圆形范围 / 恐慌 [4167] |
| 49287 | 复活 / Raise | 10 | — | 友方 / 单体 |
| 49289 | 圆斩剑 / Circling Blade | 7 | Circle(6); Circle(6f) | 代码关联；非该组官方展示项 |
| 49291 | 环斩剑 / Ringing Blade | 9 | Donut(5, 60); Donut(5f, 60f) | 代码关联；非该组官方展示项 |
| 49293 | 环斩剑 / Ringing Blade | 7 | Donut(5, 60); Donut(5f, 60f) | 代码关联；非该组官方展示项 |
| 49295 | 圆斩剑 / Circling Blade | 9 | Circle(6); Circle(6f) | 代码关联；非该组官方展示项 |
| 49296 | 冲击波 / Shockwave | 7 | Cone(60, 22.5f.Degrees()); Cone(60f, 22.5f.Degrees()) | 代码关联；非该组官方展示项 |
| 49297 | 千吨回转 / 1000-tonze Swing | 10 | Circle(20); Circle(20f) | 自身 / 圆形范围 |
| 49298 | 祸乱 / Impassion | 8 | — | 自身 / 单体 / 伤害提高 [3129] |
| 49370 | 怒视 / Glower | 4 | Rect(40f, 1.5f) | 未标 / 前方范围 / 麻痹 [5382] |

当前组件：`CrucibleBoundary`、`XBMB38AOE`、`CrucibleMedusaRay`、`XBMB38Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B39 奇子·奇美拉

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·奇美拉 / 14663 | [Chimera Piece](https://ffxiv.consolegameswiki.com/wiki/Chimera_Piece) | 水 | 49315 冰结大咆哮；49316 雷电大咆哮 |

### 攻略线索

- 奇美拉有带持续伤害的全场攻击，作者通过宠物轮换减少持续伤害。不能为全屏伤害构造不存在的步行安全区。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49300 | 寒冰吐息 / the Ram's Breath | 7 | Cone(60, 120f.Degrees()); Cone(60f, 120f.Degrees()) | 代码关联；非该组官方展示项 |
| 49302 | 雷鸣吐息 / the Dragon's Breath | 7 | Cone(60, 120f.Degrees()); Cone(60f, 120f.Degrees()) | 代码关联；非该组官方展示项 |
| 49304 | 火焰吐息 / the Lion's Breath | 7 | Cone(60, 120f.Degrees()); Cone(60f, 120f.Degrees()) | 代码关联；非该组官方展示项 |
| 49306 | 氷結の咆哮 / 氷結の咆哮 | 6 | Circle(12); Circle(12f) | 代码关联；非该组官方展示项 |
| 49308 | 雷電の咆哮 / 雷電の咆哮 | 6 | Donut(8, 40); Donut(8f, 40f) | 代码关联；非该组官方展示项 |
| 49315 | 冰结大咆哮 / the Ram's Roar | 7 | Circle(12); Circle(12f) | 自身 / 圆形范围 / 冻伤 [5500] |
| 49316 | 雷电大咆哮 / the Dragon's Roar | 7 | Donut(8, 40); Donut(8f, 40f) | 自身 / 环形范围 / 感电 [5498] |
| 49318 | 无序的和声 / Chaotic Chorus | 0 | Circle(6); Circle(6f) | 代码关联；非该组官方展示项 |
| 49319 | 寒冰眼 / the Ram's Keeper | 4 | — | 代码关联；非该组官方展示项 |
| 49320 | 寒冰眼 / the Ram's Keeper | 4.5 | Circle(6f) | 代码关联；非该组官方展示项 |
| 49327 | 寒冰吐息 / the Ram's Breath | 1.5 | Cone(60, 120f.Degrees()); Cone(60f, 120f.Degrees()) | 代码关联；非该组官方展示项 |
| 49329 | 雷鸣吐息 / the Dragon's Breath | 1.5 | Cone(60, 120f.Degrees()); Cone(60f, 120f.Degrees()) | 代码关联；非该组官方展示项 |
| 49331 | 火焰吐息 / the Lion's Breath | 1.5 | Cone(60, 120f.Degrees()); Cone(60f, 120f.Degrees()) | 代码关联；非该组官方展示项 |
| 49332 | 毒性爆发 / Toxic Tantrum | 5 | Circle(60f) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`CrucibleChimeraTether`、`CrucibleChimeraExpandingIce`、`XBMB39AOE`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B40 奇子·斯芬克斯

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·斯芬克斯 / 14665 | [Sphinx Piece](https://ffxiv.consolegameswiki.com/wiki/Sphinx_Piece) | 斩击 | 49344 记忆的谜题；49353 数字谜题 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49335 | 放逐 / Banish | 6 | Donut(10, 60) | 代码关联；非该组官方展示项 |
| 49337 | 放逐 / Banish | 6 | Circle(18) | 代码关联；非该组官方展示项 |
| 49339 | 放逐 / Banish | 6 | Cone(60, 90f.Degrees()) | 代码关联；非该组官方展示项 |
| 49341 | 放逐 / Banish | 6 | Cone(60, 90f.Degrees()) | 代码关联；非该组官方展示项 |
| 49343 | 记忆的谜题 / Mnemonic Riddle | 4 | — | 代码关联；非该组官方展示项 |
| 49344 | 记忆的谜题 / Mnemonic Riddle | 5 | — | 未标 / 全体 |
| 49348 | 解谜成功 / Riddle Solved | 2 | — | 代码关联；非该组官方展示项 |
| 49350 | 解谜失败 / Riddle Failed | 2 | — | 代码关联；非该组官方展示项 |
| 49351 | 解谜失败 / Riddle Failed | 3 | — | 代码关联；非该组官方展示项 |
| 49353 | 数字谜题 / Numeric Riddle | 5 | — | 未标 / 全体 |

当前组件：`CrucibleBoundary`、`XBMB40AOE`、`CrucibleSphinxRiddles`、`CrucibleSphinxNumbers`、`CrucibleSphinxRaidwide`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B41 奇子·巨人、奇子·独眼巨人、凝胶化雷电、凝胶化火焰

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·巨人 / 14670 | [Gigantis Piece](https://ffxiv.consolegameswiki.com/wiki/Gigantis_Piece) | 无 | 49378 粉碎重踏；49359 巨躯狂怒 |
| 奇子·独眼巨人 / 14671 | [Cyclops Piece](https://ffxiv.consolegameswiki.com/wiki/Cyclops_Piece_(Second_Master%27s_Board_-_Enemy_4)) | 无 | 49367 怒视；49368 激励鼓舞 |
| 凝胶化雷电 / 14672 | [Congealed Lightning](https://ffxiv.consolegameswiki.com/wiki/Congealed_Lightning) | 土 | 49373 炸裂 |
| 凝胶化火焰 / 14673 | [Congealed Kindling](https://ffxiv.consolegameswiki.com/wiki/Congealed_Kindling) | 风 | 49375 炸裂 |

### 攻略线索

- 巨人动作区分近身圆、前半场和后半场：双手胸前举棍为近身圆、双手过头为后半场、单手过头为前半场。粉碎重踏点名应站到史莱姆本体，让直线击中史莱姆，随后处理分裂小怪。 [来源](sources.md#bili-notes)
- 作者描述火属性吸收后脚下出圆，雷属性吸收后出塔，漏塔造成全场伤害降低；两次吸收应选不同属性。各步的精确时间及场上选取条件仍需录制对应。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49359 | 巨躯狂怒 / Gigantic Rage | 6 | Circle(15); Circle(15f) | 自身 / ？？？？ |
| 49361 | 巨躯狂怒 / Gigantic Rage | 6 | Cone(40, 90f.Degrees()); Cone(40f, 90f.Degrees()) | 代码关联；非该组官方展示项 |
| 49363 | 巨躯狂怒 / Gigantic Rage | 6 | Cone(40, 90f.Degrees()); Cone(40f, 90f.Degrees()) | 代码关联；非该组官方展示项 |
| 49366 | 追炎 / Afterburn | 3 | Circle(6); Circle(6f) | 代码关联；非该组官方展示项 |
| 49367 | 怒视 / Glower | 4 | Rect(40, 1.5f); Rect(40f, 1.5f) | 未标 / 前方范围 / 麻痹 [5382] |
| 49368 | 激励鼓舞 / Camaraderie | 8 | — | 友方 / 单体 |
| 49373 | 炸裂 / Rupture | 7 | — | 未标 / 全体 |
| 49374 | 炸裂 / Rupture | 7 | — | 代码关联；非该组官方展示项 |
| 49375 | 炸裂 / Rupture | 7 | — | 未标 / 全体 |
| 49376 | 炸裂 / Rupture | 7 | — | 代码关联；非该组官方展示项 |
| 49377 | 粉碎重踏 / Smashing Stamp | 2.6 | — | 代码关联；非该组官方展示项 |
| 49378 | 粉碎重踏 / Smashing Stamp | 3 | Rect(40, 4); Rect(40f, 4f) | 未标 / 前方范围 / 击倒 [3908] |
| 49379 | （客户端未命名） / — | 3 | Rect(7, 2); Rect(7f, 2f) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB41AOE`、`CrucibleGiantExplosionHint`、`CrucibleGiantSlimes`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B42 奇子·莫古小剑、奇子·莫古小猛、奇子·莫古大医、奇子·莫古大术、奇子·莫古小医、奇子·莫古小术、奇子·莫古大剑、奇子·莫古小歌、奇子·莫古小贼

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·莫古小剑 / 14674 | [Swordsmog Piece](https://ffxiv.consolegameswiki.com/wiki/Swordsmog_Piece) | 雷 | 未列技能（不代表没有攻击） |
| 奇子·莫古小猛 / 14675 | [Mogbreaker Piece](https://ffxiv.consolegameswiki.com/wiki/Mogbreaker_Piece) | 雷 | 49381 百库啵横扫；49383 莫古暴风雨 |
| 奇子·莫古大医 / 14676 | [Kinged Kupocurer](https://ffxiv.consolegameswiki.com/wiki/Kinged_Kupocurer) | 打击 | 49385 绒绒洪水 |
| 奇子·莫古大术 / 14677 | [Kinged Kupocaster](https://ffxiv.consolegameswiki.com/wiki/Kinged_Kupocaster) | 打击 | 49384 绒绒核爆 |
| 奇子·莫古小医 / 14678 | [Kupocurer Piece](https://ffxiv.consolegameswiki.com/wiki/Kupocurer_Piece) | 打击 | 49387 绒绒神圣；48626 流水 |
| 奇子·莫古小术 / 14679 | [Kupocaster Piece](https://ffxiv.consolegameswiki.com/wiki/Kupocaster_Piece) | 打击 | 50943 绒绒陨石雨；48621 冰结 |
| 奇子·莫古大剑 / 14680 | [Kinged Swordsmog](https://ffxiv.consolegameswiki.com/wiki/Kinged_Swordsmog) | 雷 | 49395 莫古盾扫；49394 斗志满满 |
| 奇子·莫古小歌 / 14681 | [Melomog Piece](https://ffxiv.consolegameswiki.com/wiki/Melomog_Piece) | 水 | 49397 莫古狂想曲 |
| 奇子·莫古小贼 / 14683 | [Mogmugger Piece](https://ffxiv.consolegameswiki.com/wiki/Mogmugger_Piece) | 水 | 49399 拿来吧你库啵；49400 溜了溜了库啵 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48621 | 冰结 / Blizzard | 0 | — | 最高仇恨 / 单体 |
| 48626 | 流水 / Water | 0 | — | 最高仇恨 / 单体 |
| 49381 | 百库啵横扫 / 100-kuponze Swipe | 8 | Cone(60f, 30f.Degrees()) | 未标 / 前方范围 |
| 49383 | 莫古暴风雨 / Moogle Tempest | 8 | Circle(22f) | 自身 / 圆形范围 |
| 49384 | 绒绒核爆 / Pom Flare | 10 | — | 未标 / 全体 / 火属性耐性降低 [5336] |
| 49385 | 绒绒洪水 / Pom Flood | 10 | — | 未标 / 全体 / 水属性耐性降低 [5337] |
| 49387 | 绒绒神圣 / Pom Holy | 9 | — | 未标 / ？？？？ |
| 49394 | 斗志满满 / Spirit of Pompetition | 3 | — | 自身 / 单体 / 伤害提高 [1225] |
| 49395 | 莫古盾扫 / Mogshield Swipe | 0 | — | 未标 / 前方范围 |
| 49397 | 莫古狂想曲 / Moghemian Rhapsody | 0 | — | 友方 / 圆形范围 / 伤害提高 [2550] |
| 49399 | 拿来吧你库啵 / Daylight Pommery | 0 | — | 玩家 / 单体 |
| 49400 | 溜了溜了库啵 / Kupo and Good Night | 10 | — | 未标 / 单体 |
| 50943 | 绒绒陨石雨 / Pom Meteorain | 7.5 | Circle(6f) | 地面 / 圆形范围 |

当前组件：`XBMB42AOE`、`XBMB42Hint0`、`XBMB42Hint1`、`XBMB42Hint2`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B43 奇子·贝希摩斯、奇子·铁巨人、雷元精

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·贝希摩斯 / 14684 | [Behemoth Piece](https://ffxiv.consolegameswiki.com/wiki/Behemoth_Piece) | 土 | 49410 黄道陨石；49415 大漩涡 |
| 奇子·铁巨人 / 14685 | [Giant Piece](https://ffxiv.consolegameswiki.com/wiki/Giant_Piece) | 雷 | 49408 巨剑 |
| 雷元精 / 14686 | [Lightning Sprite](https://ffxiv.consolegameswiki.com/wiki/Lightning_Sprite_(Second_Master%27s_Board_-_Random_5)) | 土 | 49411 十字放雷 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49408 | 巨剑 / Grand Sword | 4 | Cone(30f, 60f.Degrees()) | 未标 / 前方范围 |
| 49410 | 黄道陨石 / Ecliptic Meteor | 11 | — | 未标 / 全体 |
| 49411 | 十字放雷 / Cross Charge | 4 | Cross(25f, 2.5f) | 自身 / 十字范围 / 麻痹 [5382] |
| 49415 | 大漩涡 / Charybdis | 6 | Circle(12f) | 自身 / 圆/环范围 |

当前组件：`XBMB43AOE`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B44 奇子·冥鬼之眼王、奇子·哈帕利特、奇子·肮脏之眼

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·冥鬼之眼王 / 14688 | [King Ahriman Piece](https://ffxiv.consolegameswiki.com/wiki/King_Ahriman_Piece) | 冰 | 49418 死亡轮盘；49429 5兽级即死 |
| 奇子·哈帕利特 / 14691 | [Hapalit Piece](https://ffxiv.consolegameswiki.com/wiki/Hapalit_Piece) | 风 | 49435 碎骨拳 |
| 奇子·肮脏之眼 / 14692 | [Dirty Eye Piece](https://ffxiv.consolegameswiki.com/wiki/Dirty_Eye_Piece) | 冰 | 49434 死亡视线；50747 飞石 |

### 攻略线索

- 死亡轮盘误处理可能获得死亡宣告；作者遇到宣告倒计时内击杀Boss可存活。保留为实战经验，不据此修改宣告或轮盘自动处理。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49418 | 死亡轮盘 / Roulette | 5 | — | 未标 / ？？？？ |
| 49419 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |
| 49420 | （客户端未命名） / — | 25 | — | 代码关联；非该组官方展示项 |
| 49421 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |
| 49422 | 即死 / Death | 0 | — | 代码关联；非该组官方展示项 |
| 49424 | 双重视线 / Double Vision | 5 | — | 代码关联；非该组官方展示项 |
| 49425 | 悲叹视线 / Sullen Gaze | 6 | — | 代码关联；非该组官方展示项 |
| 49426 | 愤怒视线 / Ireful Gaze | 6 | — | 代码关联；非该组官方展示项 |
| 49428 | 5兽级即死 / Curtains for Rank 5 | 5 | — | 代码关联；非该组官方展示项 |
| 49429 | 5兽级即死 / Curtains for Rank 5 | 6 | — | 未标 / 全体 |
| 49431 | 核爆 / Flare | 6 | — | 代码关联；非该组官方展示项 |
| 49434 | 死亡视线 / Mortal Gaze | 4 | — | 未标 / 全体 / 死亡宣告 [5421] |
| 49435 | 碎骨拳 / Straight Punch | 0 | — | 最高仇恨 / 单体 / 火伤 [5495] |
| 50747 | 飞石 / Stone | 0 | — | 最高仇恨 / 单体 |

当前组件：`CrucibleBoundary`、`XBMB44AOE`、`CrucibleEyeAttract`、`CrucibleEyeRoulette`、`CrucibleEyeGazes`、`CrucibleEyeBrands`、`CrucibleEyeRaidwide`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B45 魔斧之主 劳妲、奇子·塔纳托斯

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 魔斧之主 劳妲 / 14693 | [Lauda the Spellcleaver](https://ffxiv.consolegameswiki.com/wiki/Lauda_the_Spellcleaver) | 无 | 49453 大奔流；49470 霹雳 |
| 奇子·塔纳托斯 / 14699 | [Thanatos Piece](https://ffxiv.consolegameswiki.com/wiki/Thanatos_Piece_(Second_Master%27s_Board)) | 雷 | 50541 地狱之痛 |

### 攻略线索

- 飞刀、麻痹与大击退组合导致走位压力；作者使用突进抵消击退，场外持续伤害很高。突进解法属于技能位移，不能假定步行寻路同样可达。 [来源](sources.md#bili-notes)
- 简介建议保留突进到Boss背后处理小近身圆，并在击退后按后续攻击回位。没有给魔刃时间轴、异形地板坐标或各段伤害延迟。 [来源](sources.md#guide-bv1a6yv6bewh)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 49443 | 魔力爆炸 / Magical Combustion | 5 | Circle(8) | 代码关联；非该组官方展示项 |
| 49446 | 贪食裂膛 / Gluttonous Gutting | 13.6 | Rect(50, 20) | 代码关联；非该组官方展示项 |
| 49449 | 贪食啮噬 / Gluttonous Goring | 13.6 | Circle(40) | 代码关联；非该组官方展示项 |
| 49451 | 垂直驱动 / Gyrocleave | 7 | Rect(80, 10); Rect(80f, 10f) | 代码关联；非该组官方展示项 |
| 49453 | 大奔流 / Beastly Aura | 7 | — | 未标 / 全体 |
| 49457 | 剧毒魔刃 / Life Claim | 3 | Cross(15, 5) | 代码关联；非该组官方展示项 |
| 49460 | 火焰魔刃·牢狱 / Cage of Molten Metal | 3 | Circle(12) | 代码关联；非该组官方展示项 |
| 49462 | 支配魔刃·自律 / Point Maker | 3 | — | 代码关联；非该组官方展示项 |
| 49464 | 支配魔刃 / Overpowering Point | 2 | Rect(60, 3); Rect(60f, 3f) | 代码关联；非该组官方展示项 |
| 49468 | 火焰魔刃 / Molten Metal | 3 | Circle(6) | 代码关联；非该组官方展示项 |
| 49469 | 魔刃爆焰 / Beastly Flare | 11 | — | 代码关联；非该组官方展示项 |
| 49470 | 霹雳 / Thunderbolt | 5 | Rect(50, 3); Rect(50f, 3f) | 玩家 / 前方范围 / 麻痹 [5388] |
| 49472 | 强化魔刃 / Greater Strength | 3 | — | 代码关联；非该组官方展示项 |
| 49477 | 灼焰战斧 / Searing Axe | 27.1 | — | 代码关联；非该组官方展示项 |
| 49480 | 灼焰战斧 / Searing Axe | 2 | — | 代码关联；非该组官方展示项 |
| 49483 | 突进 / Rush | 1 | Rect(50f, 2.5f) | 代码关联；非该组官方展示项 |
| 49485 | 消失魔刃·冲波 / Unseen Force | 0 | — | 代码关联；非该组官方展示项 |
| 49486 | 消失魔刃·冲波 / Unseen Force | 0 | — | 代码关联；非该组官方展示项 |
| 49489 | 冲击波 / Shockwave | 0 | — | 代码关联；非该组官方展示项 |
| 50541 | 地狱之痛 / Infernal Pain | 8 | — | 未标 / 全体 |
| 50846 | 支配魔刃 / Overpowering Point | 4.9 | — | 代码关联；非该组官方展示项 |
| 50848 | 支配魔刃 / Overpowering Point | 2 | Rect(60, 3); Rect(60f, 3f) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB45AOE`、`CrucibleLaudaBlades`、`CrucibleLaudaTorrent`、`CrucibleLaudaThunder`、`CrucibleLaudaBaits`、`CrucibleLaudaFireKnife`、`CrucibleLaudaShockwave`、`CrucibleLaudaLaterHint`、`CrucibleLaudaEmpoweredBlades`、`CrucibleLaudaAddRaidwide`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

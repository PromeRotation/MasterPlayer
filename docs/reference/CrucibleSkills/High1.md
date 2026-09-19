# 高段第一盘技能资料

区域ID：1342；[线上盘面目录](https://ffxiv.consolegameswiki.com/wiki/First_Master%27s_Board)；[资料总览](README.md)。

“官方展示”来自客户端选怪详情；完整已知列表另含当前源码记录。形状是现有代码参数，表读条不是伤害时间，具体证据见catalog.json。

## B22 奇子·博学林鸮

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·博学林鸮 / 14596 | [Strix Piece](https://ffxiv.consolegameswiki.com/wiki/Strix_Piece) | 突刺 | 48668 精神集中；48657 爆震之章 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48656 | 掉落 / Plummet | 7.5 | Circle(10f) | 代码关联；非该组官方展示项 |
| 48657 | 爆震之章 / On the Properties of Quakes | 8 | — | 未标 / 全体 |
| 48658 | 狂水之章 / On the Properties of Floods | 8 | — | 代码关联；非该组官方展示项 |
| 48659 | 魔法锤之章 / Magical Mallet Theory | 8 | — | 代码关联；非该组官方展示项 |
| 48660 | 魔法锤之章 / Magical Mallet Theory | 0 | — | 代码关联；非该组官方展示项 |
| 48664 | 魔力爆散 / Overdue | 6.5 | — | 代码关联；非该组官方展示项 |
| 48665 | 魔力爆散 / Overdue | 7 | Circle(15f) | 代码关联；非该组官方展示项 |
| 48667 | 暴风 / Aero III | 12 | — | 代码关联；非该组官方展示项 |
| 48668 | 精神集中 / Ultimate Focus | 3 | — | 自身 / 单体 / 魔法伤害提高 [5020] |
| 48669 | 昏暗之章 / On the Properties of Darkness | 8 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB22AOE`、`CrucibleOwlTether`、`CrucibleOwlGust`、`CrucibleOwlGustPriority`、`CrucibleOwlGustHint`、`CrucibleOwlChapterHint`、`CrucibleOwlChapters`、`XBMB22Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B23 奇子·魔界花、奇子·幼苗、奇子·大口花、腐汁

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·魔界花 / 14599 | [Morbol Piece](https://ffxiv.consolegameswiki.com/wiki/Morbol_Piece) | 斩击 | 48673 恶臭气；50758 地震 |
| 奇子·幼苗 / 14600 | [Seedling Piece](https://ffxiv.consolegameswiki.com/wiki/Seedling_Piece) | 斩击 | 50750 攻击 |
| 奇子·大口花 / 14601 | [Ochu Piece](https://ffxiv.consolegameswiki.com/wiki/Ochu_Piece) | 斩击 | 48679 酸雾 |
| 腐汁 / 14602 | [Carrion Broth](https://ffxiv.consolegameswiki.com/wiki/Carrion_Broth) | 冰 | 50544 攻击；48680 粘液飞弹 |

### 攻略线索

- 魔界花会先后召出小怪与腐汁，作者保留两轮范围爆发处理。攻略不包含五连吐息的精确间隔。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48671 | 恶臭气 / Extremely Bad Breath | 4.5 | — | 代码关联；非该组官方展示项 |
| 48672 | 恶臭气 / Extremely Bad Breath | 4.5 | — | 代码关联；非该组官方展示项 |
| 48673 | 恶臭气 / Extremely Bad Breath | 5 | Cone(50f, 45f.Degrees()) | 未标 / 前方范围 / 麻痹 [5382] |
| 48674 | 恶臭气 / Extremely Bad Breath | 0 | — | 代码关联；非该组官方展示项 |
| 48675 | 恶臭气 / Extremely Bad Breath | 0.5 | — | 代码关联；非该组官方展示项 |
| 48679 | 酸雾 / Acid Mist | 4 | Circle(6f) | 自身 / 圆形范围 |
| 48680 | 粘液飞弹 / Sticky Spit | 2 | — | 玩家 / 单体 / 加重 [2551] |
| 48682 | 藤鞭穿刺 / Vine Probe | 5 | Rect(13f, 4f) | 代码关联；非该组官方展示项 |
| 50544 | 攻击 / attack | 0 | — | 玩家 / 单体 / 麻痹 [5382] |
| 50750 | 攻击 / attack | 0 | — | 玩家 / 单体 / 中毒 [5141] |
| 50758 | 地震 / Tremblor | 5 | — | 未标 / 全体 |

当前组件：`CrucibleBoundary`、`XBMB23AOE`、`CrucibleMorbolAdds`、`CrucibleMorbolBreath`、`CrucibleMorbolPools`、`CrucibleMorbolKnockback`、`XBMB23Hint0`、`XBMB23Hint1`、`XBMB23Hint2`、`XBMB23Hint3`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B24 奇子·尸生花、奇子·女王鹰蜂

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·尸生花 / 14603 | [Corpse Flower Piece](https://ffxiv.consolegameswiki.com/wiki/Corpse_Flower_Piece) | 风 | 48691 腐烂恶臭；48685 捕食 |
| 奇子·女王鹰蜂 / 14605 | [Queen Hawk Piece](https://ffxiv.consolegameswiki.com/wiki/Queen_Hawk_Piece) | 打击 | 48689 终极针 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48683 | 鲜花陷阱 / Floral Trap | 5 | — | 代码关联；非该组官方展示项 |
| 48685 | 捕食 / Devour | 0 | — | 未标 / 前方范围 / 捕食 [421] |
| 48689 | 终极针 / Final Sting | 5 | — | 最高仇恨 / 单体 |
| 48690 | 腐烂恶臭 / Rotten Stench | 5 | — | 代码关联；非该组官方展示项 |
| 48691 | 腐烂恶臭 / Rotten Stench | 0 | Rect(45f, 6f) | 未标 / 前方范围 |
| 48693 | 酸雨 / Acid Rain | 2 | Circle(6f) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB24AOE`、`CrucibleMorbolTether`、`CrucibleRottenStench`、`CrucibleFlowerTrapHint`、`XBMB24Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B25 奇子·冰龙、冰元精

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·冰龙 / 14606 | [Ice Dragon Piece](https://ffxiv.consolegameswiki.com/wiki/Ice_Dragon_Piece) | 火 | 48704 低温俯冲；48708 无终的枯朽 |
| 冰元精 / 14607 | [Ice Sprite](https://ffxiv.consolegameswiki.com/wiki/Ice_Sprite_(First_Master%27s_Board)) | 火 | 48621 冰结 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48621 | 冰结 / Blizzard | 0 | — | 最高仇恨 / 单体 |
| 48697 | 冰寒折磨 / Icy Torment | 6.5 | Rect(100f, 7.5f) | 代码关联；非该组官方展示项 |
| 48699 | 冰寒折磨 / Icy Torment | 6.5 | Rect(100f, 7.5f) | 代码关联；非该组官方展示项 |
| 48704 | 低温俯冲 / Cauterize | 1 | Rect(46f, 11.5f) | 未标 / 前方范围 |
| 48708 | 无终的枯朽 / Withering Eternity | 5 | Circle(9f) | 地面 / 圆形范围 / 渐冻 [5177] |
| 48712 | 寒冰覆盖 / Sheet of Ice | 5.5 | Circle(5f) | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB25AOE`、`CrucibleIcePools`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B26 奇子·石像鬼

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·石像鬼 / 14608 | [Gargoyle Piece](https://ffxiv.consolegameswiki.com/wiki/Gargoyle_Piece) | 无 | 48720 圆环连斩；48727 荒芜新星 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48714 | 瘴腐气 / Malady | 4.7 | — | 代码关联；非该组官方展示项 |
| 48715 | 瘴腐气 / Malady | 5 | Circle(6f) | 代码关联；非该组官方展示项 |
| 48716 | 爆炸 / Burst | 0 | — | 代码关联；非该组官方展示项 |
| 48717 | 突进连斩 / Sweeping Evisceration | 7.9 | — | 代码关联；非该组官方展示项 |
| 48718 | 突进连斩 / Sweeping Evisceration | 0 | — | 代码关联；非该组官方展示项 |
| 48719 | 圆环连斩 / Rippling Evisceration | 5.4 | — | 代码关联；非该组官方展示项 |
| 48720 | 圆环连斩 / Rippling Evisceration | 6 | Circle(13f) | 自身 / 圆/环范围 |
| 48721 | 圆环连斩 / Rippling Evisceration | 8 | — | 代码关联；非该组官方展示项 |
| 48724 | 冲波五连斩 / Fivefold Fallout | 2.5 | — | 代码关联；非该组官方展示项 |
| 48725 | 冲波五连斩 / Fivefold Fallout | 3 | — | 代码关联；非该组官方展示项 |
| 48727 | 荒芜新星 / Desolation | 3.5 | Rect(60f, 3.5f) | 未标 / 前方范围 / 渐渐恐慌 [5178] |
| 48729 | 沥青海 / Sea of Pitch | 3 | Circle(6f) | 代码关联；非该组官方展示项 |
| 50695 | 冲波五连斩 / Fivefold Fallout | 2.5 | — | 代码关联；非该组官方展示项 |
| 50696 | 冲波五连斩 / Fivefold Fallout | 3 | — | 代码关联；非该组官方展示项 |
| 50932 | 突进连斩 / Sweeping Evisceration | 0 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB26AOE`、`CrucibleGargoyleInOut`、`CrucibleGargoyleCharge`、`CrucibleMiasma`、`CrucibleGargoyleKnockback`、`CrucibleGargoyleSlashHint`、`CrucibleGargoyleRingHint`、`CrucibleGargoyleKnockbackHint`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B27 奇子·管理者、奇子·故障虫、奇子·发条步兵、奇子·恐慌装甲、奇子·无人机、奇子·眼镜蛇

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·管理者 / 14610 | [Administrator Piece](https://ffxiv.consolegameswiki.com/wiki/Administrator_Piece) | 雷 | 48740 混乱术；48747 火炎放射 |
| 奇子·故障虫 / 14611 | [Bug Piece](https://ffxiv.consolegameswiki.com/wiki/Bug_Piece) | 雷 | 未列技能（不代表没有攻击） |
| 奇子·发条步兵 / 14612 | [Footman Piece](https://ffxiv.consolegameswiki.com/wiki/Footman_Piece) | 雷 | 48736 旋转挥打 |
| 奇子·恐慌装甲 / 14613 | [Dreadnaught Piece](https://ffxiv.consolegameswiki.com/wiki/Dreadnaught_Piece) | 雷 | 48742 燃烧处理 |
| 奇子·无人机 / 14614 | [Drone Piece](https://ffxiv.consolegameswiki.com/wiki/Drone_Piece) | 雷 | 48735 自动火炮 |
| 奇子·眼镜蛇 / 14616 | [Cobra Piece](https://ffxiv.consolegameswiki.com/wiki/Cobra_Piece) | 突刺 | 48738 毒素爆散 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48735 | 自动火炮 / Auto-cannons | 3.5 | Rect(40f, 2.5f) | 未标 / 前方范围 |
| 48736 | 旋转挥打 / Headspin | 1.5 | Circle(5f) | 自身 / 圆形范围 |
| 48737 | 魔力爆散 / Explosive Aether | 2 | Circle(6f) | 代码关联；非该组官方展示项 |
| 48738 | 毒素爆散 / Noxious Nova | 8 | — | 未标 / 全体 / 中毒 [5140] |
| 48740 | 混乱术 / Confound | 8 | — | 未标 / 全体 / 混乱 [1283] |
| 48742 | 燃烧处理 / Incinerate | 0 | — | 自身 / 前方范围 / 火属性耐性降低 [5336] |
| 48747 | 火炎放射 / Flame Thrower | 7 | Cone(40f, 60f.Degrees()) | 未标 / 前方范围 |

当前组件：`XBMB27AOE`、`XBMB27Hint0`、`XBMB27Hint1`、`XBMB27Hint2`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B28 奇子·巨像

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·巨像 / 14617 | [Golem Piece](https://ffxiv.consolegameswiki.com/wiki/Golem_Piece_(First_Master%27s_Board)) | 水 | 48757 岩雨；48768 岩突 |

### 攻略线索

- 后续巨像激活时重新确认仇恨；这是防宠物受伤的评分建议，不代替击退、自爆等机制处理。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48748 | 岩壁 / Rock Wall | 3 | — | 代码关联；非该组官方展示项 |
| 48750 | 冲击波 / Shockwave | 4 | Rect(5f, 2.5f) | 代码关联；非该组官方展示项 |
| 48751 | 岩石崩溃 / Rockslide | 0 | — | 代码关联；非该组官方展示项 |
| 48753 | 核心环光 / Earthen Ring | 7 | Donut(5f, 50f) | 代码关联；非该组官方展示项 |
| 48755 | 平原震裂 / Plaincracker | 7 | Circle(20f) | 代码关联；非该组官方展示项 |
| 48757 | 岩雨 / Stoneshower | 1 | Circle(8f) | 自身 / 圆/环范围 |
| 48758 | 岩雨 / Stoneshower | 1 | — | 代码关联；非该组官方展示项 |
| 48759 | 核心环光 / Earthen Ring | 6.2 | — | 代码关联；非该组官方展示项 |
| 48760 | 平原震裂 / Plaincracker | 6 | — | 代码关联；非该组官方展示项 |
| 48761 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |
| 48763 | 核心环光 / Earthen Ring | 1 | — | 代码关联；非该组官方展示项 |
| 48766 | 自爆 / Self-destruct | 20 | — | 代码关联；非该组官方展示项 |
| 48768 | 岩突 / Outcrop | 5 | Cone(40f, 30f.Degrees()) | 未标 / 前方范围 |
| 50649 | 破魔震 / Obliterate | 5 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB28AOE`、`CrucibleGolemJump`、`CrucibleRockRain`、`CrucibleRockWalls`、`CrucibleGolemKnockback`、`CrucibleGolemKnockbackHint`、`CrucibleGolemSelfDestructHint`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B29 奇子·树精、奇子·蛞蝓、奇子·花苗、奇子·凶蛛蝎、奇子·比洛克

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·树精 / 14618 | [Treant Piece](https://ffxiv.consolegameswiki.com/wiki/Treant_Piece) | 打击 | 48781 橡果炸弹 |
| 奇子·蛞蝓 / 14619 | [Slug Piece](https://ffxiv.consolegameswiki.com/wiki/Slug_Piece) | 打击 | 48783 释放汁液 |
| 奇子·花苗 / 14620 | [Sapling Piece](https://ffxiv.consolegameswiki.com/wiki/Sapling_Piece) | 打击 | 48786 吸收养分 |
| 奇子·凶蛛蝎 / 14621 | [Diremite Piece](https://ffxiv.consolegameswiki.com/wiki/Diremite_Piece) | 冰 | 48785 蛛网屏 |
| 奇子·比洛克 / 14622 | [Biloko Piece](https://ffxiv.consolegameswiki.com/wiki/Biloko_Piece) | 冰 | 48782 治疗之水 |

### 攻略线索

本次未取得该组的可读文字机制说明。已保留敌人页面、官方展示和现有组件记录；视频入口见[来源索引](sources.md)。

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48771 | 巨木龙卷 / Arboreal Storm | 5 | Circle(12f) | 代码关联；非该组官方展示项 |
| 48772 | 巨木龙卷 / Arboreal Storm | 7 | Donut(12f, 18f) | 代码关联；非该组官方展示项 |
| 48773 | 巨木龙卷 / Arboreal Storm | 9 | Donut(18f, 24f) | 代码关联；非该组官方展示项 |
| 48774 | 巨木龙卷 / Arboreal Storm | 11 | Donut(24f, 30f) | 代码关联；非该组官方展示项 |
| 48775 | 巨木龙卷 / Arboreal Storm | 13 | Donut(30f, 36f) | 代码关联；非该组官方展示项 |
| 48778 | 簌簌轻风 / Rustling Breeze | 7 | Cone(60f, 45f.Degrees()) | 代码关联；非该组官方展示项 |
| 48779 | 簌簌轻风 / Rustling Breeze | 7 | Cone(60f, 75f.Degrees()) | 代码关联；非该组官方展示项 |
| 48780 | 簌簌轻风 / Rustling Breeze | 7 | Cone(60f, 75f.Degrees()) | 代码关联；非该组官方展示项 |
| 48781 | 橡果炸弹 / Acorn Bomb | 0 | Circle(3f) | 玩家 / 圆形范围 / 噩梦 [5383] |
| 48782 | 治疗之水 / Natural Nurture | 9 | — | 友方 / 单体 |
| 48783 | 释放汁液 / Aqueous Discharge | 2.5 | Circle(5f) | 自身 / 圆形范围 |
| 48785 | 蛛网屏 / Silkscreen | 3.5 | Rect(40f, 2f) | 未标 / 前方范围 / 加重 [5356] |
| 48786 | 吸收养分 / Grab and Grow | 0 | — | 友方 / 单体 / 生长中 [390] |

当前组件：`CrucibleBoundary`、`XBMB29AOE`、`CrucibleTreantBody`、`CrucibleGroveAdds`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B30 奇子·爆弹之母、奇子·榴弹怪、奇子·爆弹怪、奇子·雪弹怪、奇子·焰球

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 奇子·爆弹之母 / 14623 | [Progenitrix Piece](https://ffxiv.consolegameswiki.com/wiki/Progenitrix_Piece) | 风 | 48802 大爆炸；48801 熔毁 |
| 奇子·榴弹怪 / 14624 | [Grenade Piece](https://ffxiv.consolegameswiki.com/wiki/Grenade_Piece) | 风 | 48790 大爆炸 |
| 奇子·爆弹怪 / 14625 | [Bomb Piece](https://ffxiv.consolegameswiki.com/wiki/Bomb_Piece_(First_Master%27s_Board)) | 风 | 48791 烈炎；48792 自爆 |
| 奇子·雪弹怪 / 14626 | [Snoll Piece](https://ffxiv.consolegameswiki.com/wiki/Snoll_Piece) | 火 | 48793 冰棘屏障；48794 冰碎 |
| 奇子·焰球 / 14627 | [Pyrobolus Piece](https://ffxiv.consolegameswiki.com/wiki/Pyrobolus_Piece) | 水 | 48796 剧毒烟雾 |

### 攻略线索

- 保留一轮范围爆发到爆弹之母召怪时使用；不能把可高伤跳过的阶段认定为没有爆炸机制。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48790 | 大爆炸 / Massive Explosion | 4 | — | 未标 / 全体 |
| 48791 | 烈炎 / Fire II | 4 | Circle(5f) | 地面 / 圆形范围 |
| 48792 | 自爆 / Self-destruct | 0 | Circle(6f) | 自身 / 圆形范围 |
| 48793 | 冰棘屏障 / Ice Spikes | 3 | — | 自身 / 单体 / 冰棘屏障 [2528] |
| 48794 | 冰碎 / Hypothermal Combustion | 0 | Circle(10f) | 自身 / 圆形范围 |
| 48796 | 剧毒烟雾 / Toxic Fumes | 4 | Cone(40f, 10f.Degrees()) | 自身 / ？？？？ / 中毒 [5141] |
| 48799 | 烈火怒骂 / Scalding Scolding | 6 | Cone(40f, 90f.Degrees()) | 代码关联；非该组官方展示项 |
| 48800 | 熔毁 / Meltdown | 0 | — | 代码关联；非该组官方展示项 |
| 48801 | 熔毁 / Meltdown | 0 | Rect(40f, 6f) | 玩家 / 前方范围 |
| 48802 | 大爆炸 / Massive Explosion | 7 | — | 未标 / 全体 |

当前组件：`CrucibleBoundary`、`XBMB30AOE`、`CrucibleMovingBombs`、`CrucibleBombPools`、`CrucibleBombMeltdown`、`CrucibleBombEnrageHint`、`XBMB30Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

## B31 怨毒龙 博尔格尼、有毒物质

| 敌人 / NameID | 英文名与网页 | 弱点（客户端） | 官方展示技能 |
|---|---|---|---|
| 怨毒龙 博尔格尼 / 14628 | [Borgny the Venomous](https://ffxiv.consolegameswiki.com/wiki/Borgny_the_Venomous) | 土 | 48810 猛毒粘液；48814 低温俯冲 |
| 有毒物质 / 14629 | [Toxic Mass](https://ffxiv.consolegameswiki.com/wiki/Toxic_Mass) | 水 | 48821 大爆炸 |

### 攻略线索

- 毒圈沿场边依次放置，注意宠物位置及后撤步与旧毒池重叠。作者描述后撤扇形在目标圈内有近身安全范围，但没有精确几何，不能直接替换当前组件尺寸。 [来源](sources.md#bili-notes)

### 已知技能

| Action ID | 中文 / 英文 | 表读条(s) | 当前代码形状参数 | 官方目标 / 范围 / 状态 |
|---|---|---:|---|---|
| 48807 | 猛毒吐息 / Toxic Breath | 3 | — | 代码关联；非该组官方展示项 |
| 48808 | 猛毒吐息 / Toxic Breath | 0 | — | 代码关联；非该组官方展示项 |
| 48809 | 猛毒粘液 / Toxic Vomit | 3.5 | Circle(6f) | 代码关联；非该组官方展示项 |
| 48810 | 猛毒粘液 / Toxic Vomit | 0 | Circle(6f) | 玩家 / 圆形范围 / 猛毒 [5183] |
| 48812 | 浓烟呛呕 / Fuming Vomit | 6 | Circle(6f) | 代码关联；非该组官方展示项 |
| 48814 | 低温俯冲 / Cauterize | 6 | Rect(48f, 10f) | 未标 / 前方范围 / 猛毒 [5183] |
| 48816 | 空降 / Touchdown | 4 | — | 代码关联；非该组官方展示项 |
| 48819 | 蠕动的痰 / Wriggling Phlegm | 4 | Circle(6f) | 代码关联；非该组官方展示项 |
| 48821 | 大爆炸 / Noxious Explosion | 0 | — | 未标 / 全体 / 猛毒 [5183] |
| 48828 | 空降 / Touchdown | 5.5 | — | 代码关联；非该组官方展示项 |
| 50543 | （客户端未命名） / — | 0 | — | 代码关联；非该组官方展示项 |

当前组件：`CrucibleBoundary`、`XBMB31AOE`、`CrucibleDragonWaves`、`CrucibleDragonPoison`、`CrucibleDragonBaits`、`CrucibleDragonClouds`、`CrucibleDragonLanding`、`CrucibleDragonBreathHint`、`CrucibleDragonLandingHint`、`XBMB31Hint0`。

字段级来源、源码位置、CastType/EffectRange/Omen和未简化的形状表达式见[catalog.json](catalog.json)。

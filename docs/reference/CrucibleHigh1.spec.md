# 斗兽棋高一（斗兽奇弈·高段第一盘）BMR → MasterPlayer 迁移规格

来源：BossModReborn-7.56 `BossMod/Modules/Dawntrail/Foray/Crucible/`（CrucibleHigh1Mechanics/Sequences/Branches + XBMBattles + 共享组件）。本文是移植 `Scripts/CrucibleHigh1.cs` 的唯一事实来源。所有 ID 均为十六进制（括号内十进制）。

## 0. 战斗形态与总体架构

- "高一" = 第 22~31 组敌人（B22~B31，共 10 组）的**大地图巡回战**，45 组敌人共用同一张地图（TerritoryID **1342**）——BMR 靠 OID/NameID 注册。本脚本用 `territorys: [1342]`（限定斗兽棋地图）+ **内部激活检测**：每 ~0.5s 扫描对象表，发现 NameID ∈ 高一集合（见 §1）的战斗 NPC 才激活对应组。
- **无时间轴/阶段**，纯事件驱动。每组敌人一个 handler 类；组内成员全灭 → 该组收尾清理。
- BMR 组件 → 本脚本映射：
  - 读条 AoE（ReplayValidatedCastAOEs）→ `OnStartCast` → `SafeZone.AddDelayedHit(template, binding, 读条剩余+0.3s 补正)`，按 (casterId, actionId) 簿记句柄；`OnCancelCast`/`OnInterruptCast` 撤销；`OnActionEffect` 命中结算（时间窗 ±1s 匹配）。
  - 禁足区 → SafeZone 危险区；安全点/站位圈 → `AddTimedSafeArea`；常驻池 → `AddPersistentDanger`；地雷/实体墙 → `AddObstacle`。
  - CastHints/TetherLine → `Log` 文字提示（节流）+ `Draw.AddTether` 连线。
  - 击杀优先级（BMR AI 仇恨干预）→ 降级为文字提示。
- 全局余量约定（来自 CrucibleScorpionAI.Avoid）：注册危险区时**半径 +0.35y、生效提前 0.25s**（月环内径 -0.35/外径 +0.35）。
- 与通用躲避去重：脚本 `OnStart` 里 `SuppressGenericDodge(...)` 声明接管 §1 全部 AID，脚本运行期间通用躲避（DodgeController）跳过这些技能，避免同一 AOE 注册两遍。
- NPC 读条补正：BMR `NPCRemainingTime` 含 +0.3s；本脚本统一 `const float NpcCastPadding = 0.3f`，activation = `e.CastTime + NpcCastPadding`（可做 [UserSetting]）。
- 形状换算：BMR 锥=半角 → PR `FanTemplate(R, 2×half)`；BMR `Rect(前向长, 半宽)` → PR `RayRectangleTemplate.Symmetric(2×半宽, 前向长)`；落点圆 → `CircleTemplate` + `StaticTransform(e.EffectPosition, 0)`；施法者位/朝向 → `FollowEntityPositionAndRotation((uint)e.SourceId, Vector2.Zero, MissingEntityPolicy.KeepLastResolved)`。
- 事件字段注意（本体 LogSystemEvents）：`LogSystemCastStartEvent.ActionId` 是 **ushort**；SourceId/TargetId 是 **ulong**（实体 API 需 `(uint)` 截断）；`CastTime` 是 float 秒；`EffectPosition`/`SourcePosition` 为 Vector3。写代码前读本体 `LogSystem/LogSystemEvents.cs` 与 `Hosting/PromeEventBus.cs` 核对 OnEObjAnimation/OnEObjSetState/OnMapEffect/OnActionEffect 的字段。

## 1. 常量表

### 组与 NameID/OID（组内激活检测用；0xFF00xxxx 为 BMR 哨兵占位 → 本脚本一律按 NameID 匹配）

| 组 | 主 NameID | 主 OID | 辅助实体 |
|---|---|---|---|
| B22 博学林鸮 | 14596 | 0x4CB6 | 羽毛 NameID 14598 / OID 0x4CB8；pad EObj 0x1E9582 / 0x1EC0E1 / 0x1EC0E0 |
| B23 魔界花 | 14599 | 0x4CB9 | 幼苗 14600/0x4CBA、大口花 14601/0x4CBB、腐汁 14602/0x4CBC；毒池 EObj 0x1E9F40 |
| B24 尸生花 | 14603 | 0x4CBD | 花苗 14604/0x4CBE、女王鹰蜂 14605/0x4CBF |
| B25 冰龙 | 14606 | 0x4CC0 | 冰元精 14607（OID 未知按 NameID）；冰池 EObj 0x1E972A |
| B26 石像鬼 | 14608 | 0x4CC2 | 瘴气球 14609/0x4CC3 |
| B27 发条步兵 | 14612 | 占位 | 管理者 14610、恐慌装甲 14613、无人机 14614、眼镜蛇 14616（全按 NameID） |
| B28 巨像 | 14617 | 0x4CCC | 落石 EObj 0x1EC0D2；辅助体 9020=0x233C（通用复制体，不可判组） |
| B29 树精 | 14618 | 0x4CCD | 蛞蝓 14619/0x4CCE、花苗 14620/0x4CCF、凶蛛蝎 14621/0x4CD0、比洛克 14622/0x4CD1 |
| B30 榴弹怪 | 14624 | 0x4CD3 | 爆弹之母 14623/0x4CD2、火弹 14625/0x4CD4、冰弹 14626/0x4CD5、焰球 14627；毒池 EObj 0x1E8D9B |
| B31 怨毒龙 | 14628 | 0x4CD8 | 有毒物质 14629/0x4CD9、毒气团 14630/0x4CDA；毒池 EObj 0x1EB704 |

### AID 速查（按组）

- B22：0xBE10(48656) 掉落 R10；0xBE18(48664) 魔导书前置；0xBE19(48665) 魔力爆散 R15；0xBE1B(48667) 暴风 11.7s 击退 25y（Knockback row 242）；0xBE11/0xBE12/0xBE13(48657/48658/48659) 章节三魔法；0xBE14(48660) 魔法锤命中；0xBE1C(48668) 精神集中；0xBE1D(48669) 章节提示
- B23：0xBE1F/0xBE20(48671/48672) 恶臭气转向读条 4.2s（-45°/+45°）；0xBE21/0xBE23(48673/48675) 恶臭气实际伤害 0.2s；0xBE22(48674) 旋转视觉（忽略）；0xBE27(48679) 酸雾 R6；0xBE2A(48682) 藤鞭穿刺 13×8；0xBE28(48680) 粘液飞弹点名；0xC646(50758) 地震击退 10y；0xC570(50544) 麻痹；0xC63E(50750) 中毒
- B24：0xBE35(48693) 酸雨 R6；0xBE32(48690) 腐烂恶臭视觉读条 4.7s；0xBE33(48691) 腐烂恶臭实际伤害（无读条）；0xBE2B(48683) 鲜花陷阱；0xBE2D(48685) 捕食；0xBE31(48689) 终极针
- B25：0xBE39/0xBE3B(48697/48699) 冰寒折磨 100×15；0xBE44(48708) 无终的枯朽 R9；0xBE48(48712) 寒冰覆盖 R5；0xBE40(48704) 低温俯冲 46×23（**BMR 有意跳过，本移植补齐并注释待实测**）
- B26：0xBE4B(48715) 瘴气布防 R6；0xBE57(48727) 荒芜新星 60×7；0xBE59(48729) 沥青圈 R6；0xBE4A(48714) 布防读条；0xBE4C(48716) 瘴气爆炸（状态 0x143A(5178)）；0xBE4F(48719) 圆环连斩预测；0xBE50(48720) 钢铁实际；0xBE51(48721) 月环实际；0xC6F4(50932) 冲锋效果包；0xBE4E(48718) 半场斩伤害；0xC607/0xC608/0xBE54/0xBE55(50695/50696/48724/48725) 五连斩击退链 20y（row 3）；0xBE4D(48717) 突进提示
- B27：0xBE5F(48735) 自动火炮 40×5；0xBE60(48736) 旋转挥打 R5；0xBE61(48737) 魔力爆散 R6；0xBE6B(48747) 火炎放射 锥 R40 半角60°；提示 0xBE62/0xBE64/0xBE66(48738/48740/48742)
- B28：0xBE6E(48750) 冲击波 5×5；0xBE71(48753) 核心环光 月环5-50；0xBE73(48755) 平原震裂 R20；0xBE80(48768) 岩突 锥 R40 半角30°；0xBE6C(48748) 岩墙+塌陷；0xBE6F(48751) 塌陷伤害；0xBE75/0xBE76(48757/48758) 落石实际读条 0.7s；0xBE77/0xBE78(48759/48760) 起跳；0xBE79(48761) 跳跃效果包；0xBE7B(48763) 跳跃月环实际读条；0xC5D9(50649) 击退 17y（row 243）；0xBE7E(48766) 自爆提示
- B29：0xBE83~0xBE87(48771~48775) 巨木龙卷五连（R12 圆 → 环12-18 → 18-24 → 24-30 → 30-36，中心=落点，每 2s 结算一道）；0xBE8A(48778) 簌簌轻风 锥 R60 半角45°；0xBE8B/0xBE8C(48779/48780) 半角75°；0xBE8D(48781) 橡果炸弹 R3；0xBE8F(48783) 释放汁液 R5；0xBE91(48785) 蛛网屏 40×4；0xBE8E(48782) 治疗之水
- B30：0xBE9F(48799) 烈火怒骂 锥 R40 半角90°；0xBE98(48792) 自爆命中；0xBE9A(48794) 冰碎命中；0xBEA0(48800) 熔毁预告；0xBEA1(48801) 熔毁命中；0xBE96(48790) 连续大爆炸狂暴；0xBE99(48793) 冰棘屏障；0xBE97(48791) 烈炎 R5；0xBE9C(48796) 剧毒烟雾 锥 R40 半角10°
- B31：0xBEAC/0xBEB3(48812/48819) 浓烟呛呕/蠕动的痰 R6；0xBEAE(48814) 低温俯冲 48×20；0xBEA7(48807) 猛毒吐息 3s；0xBEA8(48808) 毒浪命中；0xBEA9(48809) 连线放毒读条 3.2s；0xBEAA(48810) 放毒快照×3；0xBEB0(48816) 空降外环；0xBEBC(48828) 空降中心 R6；0xBEB5(48821) 剧毒全场；0xC56F(50543) 毒气团接触

### 其他关键 ID

- Tether：林鸮/羽毛→玩家（画线即可）；尸生花组连线（画线）；瘴气球布防 tether **0x01AA(426)**；B31 连线放毒 tether **0x11(17)**
- EObj 动画（落石 0x1EC0D2）：0x00010002=圆 R8 预告（+10s）；0x00400080=月环3-11 预告；0x00040008/0x00040100=最终阶段（收紧到 +3.1s）
- EObj EventState==7 = 物件休眠/消失（全部池类通用）
- MapEffect：电网 index 0/1/2（state 0x00020001=开 / 0x00080004=关）；石墙 index 17~28（同 state 开/关）
- 章节安全垫状态：浮空 0x000C(12)（pad 0x1E9582）、河童变身 0x046E(1134)（pad 0x1EC0E1）、变身 0x0648(1608)（pad 0x1EC0E0）

## 2. 场地表与电网

| 场地 | 中心 (X,Z) | 形状 | 使用组 |
|---|---|---|---|
| 圆场 | (120, -420) | 安全 R20，电网环 R20~23 | B22/B23/B24/B25 |
| 方场 | (120, 0) | 安全 40×40，电网外扩 3y | B26 |
| 矩形场 | (520, 0) | 安全 40×30，电网外扩 3y | B28 |
| 龙场 | (920, -420) | 实体墙 R19.75（无电网，导航内缩 0.7） | B31 |
| 树精场 | (120, -420) | 安全 R20（内缩 0.3） | B29（NameID 14618~14622 特判） |

- 场地锁定：组激活时按 NameID 特判（B29/B31）否则按主怪位置就近（60y 内）。锁定后调 `PromeRotation.Plugin.Instance.SafeZoneRebornRegistry.SetArenaBoundary(...)` 设置解算边界（圆=CircleGeometry/矩形=RectangleGeometry，读本体 Geometry 类核对构造），组收尾时 `ClearArenaBoundary`（在脚本 OnStop 也要清）。注意 ScriptSafeZoneApi 门面不含此方法，直接调本体 API 并自行 try/catch。
- 电网：监听对应 index 的 MapEffect，开 → 注册持久危险区（圆场 DonutTemplate(20,23)；方/矩形场=4 条矩形带，可简化为边界外 donut/矩形环），关 → Remove。B27/B31 无电网。

## 3. 通用基础设施规格（脚本内私有辅助类）

1. **CastAoeTracker**：`OnStartCast` 命中表 → 构造 GeometryTemplate + TransformBinding（落点优先 `e.EffectPosition`，否则跟随施法者）→ `SafeZone.AddDelayedHit(t, b, e.CastTime + NpcCastPadding + extraDelay, hitDuration)`；字典按 `(uint casterId, uint actionId)` 存句柄（同键 ±0.75s 内重复读条只刷新）；`OnCancelCast`/`OnInterruptCast` 撤句柄；`OnActionEffect` 命中即等自然过期（hitDuration 默认 0.6s 会自清），序列类机制另簿记。
2. **VoidzoneWatcher（常驻池）**：OnUpdate 每 ~0.3s 扫 `Svc.Objects` 找 EObj（按 DataId），存活且 EventState≠7、达到激活延迟 → 确保 `AddPersistentDanger(CircleTemplate(R), StaticTransform(pos,0))` 已注册（按 EObj EntityId 簿记句柄）；消失/休眠 → Remove。池类使用持续危险区，让已在池内的玩家能逃出、池外玩家绕行；半径通过门面叠加 `DodgeDangerMargin`（默认 +0.5y），不叠加读条专用的 +0.35y。立即激活调用当次就注册，不等下一次轮询。
3. **序列簿记**：多段机制（钢铁→月环、五连锥、二连半场、六连毒浪、五连龙卷）用小型状态机类：事件推进 + OnUpdate 时间校验 + 每段注册对应危险区（BMR 只把当前段+下一段标危险/轮廓——本脚本直接给每段注册各自时窗的危险区即可，SafeZone 解算器本身是时间感知的，无需"轮廓"概念；绘制交给雷达模块自动完成）。
4. **提示节流**：文字提示 `Log(...)` 按 key 节流（同 key 10s 内不重复）。
5. **激活状态机**：枚举字段 `_activeGroups`；OnUpdate 扫描 NameID；新激活 → 锁定场地 + 注册电网监听 + Log；全灭 → 清该组全部句柄（各 handler 自带 Dispose/Cleanup 方法）。
6. **机制开关**：`GetMechanisms()` 返回 ["B22 林鸮","B23 魔界花","B24 尸生花","B25 冰龙","B26 石像鬼","B27 发条步兵","B28 巨像","B29 树精","B30 榴弹怪","B31 怨毒龙","连线绘制","文字提示"]；每个 handler 处理事件前查 `IsMechanismEnabled(...)`。

## 4. 逐组机制规格

### B22 博学林鸮（圆场 R20）
- 掉落 0xBE10：圆 R10 落点（多波共存，2.2s 间隔；**只禁第一波**：同组其他波次的危险区 hitDuration 不变但不做额外处理——SafeZone 解算器全部避让即可，BMR 的"只第一波 risky"是性能取舍，本移植全注册）。
- 魔力爆散 0xBE19：圆 R15 落点。
- 暴风 0xBE1B（羽毛 11.7s 读条）：击退 25y 源点=读条落点。**降级实现**：读条期间 Draw 画源点红圈 R2 + 玩家→源点反方向提示线 + Log"暴风击退 25y：优先击杀读条羽毛，否则提前靠场心"；不做落点校验。
- 章节魔法（核心）：0xBE11→pad 0x1E9582 + 需持浮空(12)；0xBE12→pad 0x1EC0E1 + 河童变身(1134)；0xBE13→pad 0x1EC0E0 + 变身(1608)，判定=读条结束+0.8s。**读条剩余 ≤3s 时起**：在 pad EObj 位置注册 `AddTimedSafeArea(CircleTemplate(6), StaticTransform(padPos,0), 剩余时间+1s)`（显示窗对齐 BMR Pads()：判定 +1s 关闭）；判定后（或撤销事件后）Remove。提示对齐 BMR 原文三档：48657"进入浮空区域，等爆震判定后再离开！"、48658"进入河童区域，等狂水判定后再离开！"、48659"进入变身区域应对魔法锤！"。pad 位置：Svc.Objects 按 DataId 找 IEventObj。
- 拉线（林鸮/羽毛→玩家）：Draw.AddTether，距离 <20y 红色 ≥20y 绿色（受"连线绘制"开关控制）。颜色仅对本地玩家目标按距离变色（<20y 红 / ≥20y 绿），他人连线中性色（BMR Colors.Object）。
- 文字提示：0xBE1C 精神集中（单体减伤）、0xBE1D 章节提醒（受"文字提示"开关）。

### B23 魔界花（圆场 R20）
- 酸雾 0xBE27：圆 R6 落点。藤鞭穿刺 0xBE2A：RayRect Symmetric(8, 13) 跟随施法者朝向。
- **恶臭气五连锥**：0xBE1F=步进 -45° / 0xBE20=+45°（4.2s 视觉读条）。读条开始即注册 5 道 `FanTemplate(50, 90°)`：**顶点 = 场地中心**（转向读条挂在 Boss 本体上，实际伤害由场心辅助体发出；BMR 靠 0xBE21/0xBE23 实际读条逐道校正回场心，本移植直接锚定），第 i 道（i=0..4）朝向 = **转向读条（第一个读条）的朝向** + i×步进，activation = 视觉读条剩余+0.3 + 0.5 + i×2.1s，hitDuration 0.5s；0xBE21/0xBE23 实际伤害读条出现时按最近道校正（±1s 窗口，采信实际读包位置/朝向）；0xBE22 忽略。另注册首道期间的"贴近"安全区：与锥同源，`AddTimedSafeArea(DonutTemplate(4, 11), StaticTransform(场心), ...)`——圆心是五道锥的公共顶点（任何方向都被覆盖），用环带排除圆心，只引导"贴近但不站圆心"。
- 毒池：EObj 0x1E9F40 → AddPersistentDanger R6。
- 地震 0xC646：击退 10y，降级：提示"击退 10y：背对安全区，注意毒池"+画源点。
- 点名提示 0xBE28（腐汁读条且 TargetId==本地玩家）：Log"锁定点名，注意减伤"。

### B24 尸生花（圆场 R20）
- 酸雨 0xBE35：圆 R6 落点。
- **腐烂恶臭**：0xBE32 视觉读条 4.7s 开始 → 注册 RayRect Symmetric(12, 45)（StaticTransform(读条位置, 读条朝向)），delay = 读条剩余+0.3+0.2，hitDuration 2s；打断（OnCancelCast/OnInterruptCast）撤销。
- 拉线（尸生花/花苗/女王鹰蜂）：画线。颜色仅对本地玩家目标按距离变色（<20y 红 / ≥20y 绿），他人连线中性色（BMR Colors.Object）。
- 文字：0xBE2B 鲜花陷阱、0xBE2D 捕食。

### B25 冰龙（圆场 R20）
- 冰寒折磨 0xBE39/0xBE3B：RayRect Symmetric(15, 100) 跟随施法者朝向（多波共存全注册）。
- 无终的枯朽 0xBE44：圆 R9 落点。寒冰覆盖 0xBE48：圆 R5 落点。
- 低温俯冲 0xBE40：RayRect Symmetric(23, 46)（补齐，注释待实测）。
- 冰池：EObj 0x1E972A → AddPersistentDanger R9。

### B26 石像鬼（方场 40×40）
- 瘴气布防 0xBE4B：圆 R6 落点。荒芜新星 0xBE57：RayRect Symmetric(7, 60)。沥青圈 0xBE59：圆 R6 落点。
- **瘴气地雷**：瘴气球（NameID 14609/OID 0x4CC3）与本体间 tether 0x01AA = 已布防 → 对球位置 AddObstacle R1.8（接触即爆语义）；命中 0xBE4C → 清该球障碍。每帧 Update 兜底扫存活球+其 tether 状态。
- **圆环连斩**：0xBE4F 读条开始 → 注册钢铁 圆 R13（delay=读条剩余+0.3+0.6）+ 月环 DonutTemplate(6,30)（delay=钢铁+2s）；0xBE50/0xBE51 实际读条出现时按新读条校正（移除旧句柄重注册）。提示"先远离 R13，2s 内进怀中"。
- **冲锋双向半场斩**：效果包 0xC6F4（SourceDataId=0x4CC2、落点距场心 X/Z 各不超过 25y）→ TargetXZ=冲刺落点，注册第一刀 FanTemplate(60, 180°)（delay 2.8s，hit 0.3s），第二刀反向 +180°（delay 4.85s，hit 0.3s）。两刀伤害均为 **0xBE4E**：先按 GlobalSequence 去重（PR 按目标展开同一动作），第一次立即移除第一刀、按命中时施法者位置与朝向反向校正第二刀（delay 2.05s）；第二次立即清空，不能再生成预测。第一刀事件缺失且已超过其 activation+0.5s 时，下一次伤害直接结束序列；第二刀也以 activation+0.5s 兜底收尾。BMR 伤害分支不限定 SourceId，保留辅助体发出伤害的可能。来源：`CrucibleHigh1Sequences.cs` 的 `CrucibleGargoyleCharge`。
- **五连斩击退链**：0xC607→预测击退 delay+9.5s；0xC608→+9.0s；0xBE54→+0.6s；0xBE55→即刻。链上任一出现即提示"五连斩最后一击 20y 击退：提前靠中，避开瘴气与电网"，并在预测时刻前注册场心方向安全区引导（AddTimedSafeArea 场心 R5）。落点校验降级为文字提示。
- 文字：0xBE4D、0xBE51、0xBE55。

### B27 发条步兵（无电网，场地跟随质心 R40 圆兜底）
- 自动火炮 0xBE5F：RayRect Symmetric(5, 40) 施法者朝向。旋转挥打 0xBE60：圆 R5 施法者位。魔力爆散 0xBE61：圆 R6 施法者位。火炎放射 0xBE6B：FanTemplate(40, 120°) 施法者朝向。
- 文字：0xBE62 毒素爆散、0xBE64 混乱术、0xBE66 燃烧处理。

### B28 巨像（矩形场 40×30）
- 冲击波 0xBE6E：RayRect Symmetric(5, 5)。核心环光 0xBE71：DonutTemplate(5,50) 施法者位。平原震裂 0xBE73：圆 R20 施法者位。岩突 0xBE80：FanTemplate(40, 60°) 施法者朝向。
- **落石（EObj 预测，迁移价值最高）**：EObj 0x1EC0D2 的 OnEObjAnimation：0x00010002→注册圆 R8（delay≈10s）；0x00400080→月环 3-11（delay≈10s）；0x00040008/0x00040100→把该位置危险区收紧到 +3.1s（移除重建）；实际读条 0xBE75（圆 R8）/0xBE76（月环3-11）出现（0.7s 条）时按位置 0.5y 内匹配替换预测。EObj 消失（OnEObjSetState 7）→ Remove。
- **岩墙+中央塌陷**：0xBE6C 读条 → 注册中央塌陷 RayRect Symmetric(10, 30) 于场心正北 15y 朝南（delay=读条剩余+0.3+10s，hit 2s）；MapEffect index 17~28 state 0x00020001 → 逐块 AddObstacle 5×5 方块（x=场心±7.5 两列，z=场心-12.5+5×(idx-17)/2，按 BMR 布局公式）；0x00080004 → 撤；0xBE6F 命中或施法者消失 → 清全部墙障碍+塌陷。
- **跳跃月环**：0xBE77/0xBE78 起跳读条记录；效果包 0xBE79（OnActionEffect 主目标=被跳巨像）→ 注册月环 DonutTemplate(5,50) 于目标当前位置（delay≈6s）；0xBE7B 实际读条出现 → 按其 EffectPosition/剩余时间替换。
- 击退 0xC5D9：17y，降级提示"击退 17y：提前靠中"。
- 文字：0xBE7E 自爆"优先击杀读条巨像"。

### B29 树精（树精场 R20）
- **本体目标圈**：存活树精（NameID 14618）常显碰撞圈危险区——`AddPersistentDanger(CircleTemplate(hitbox), FollowEntityPosition(..., RemoveArea))`（BMR `CrucibleTreantBody`；其 +0.5 余量由门面 `DodgeDangerMargin` 叠加，默认正好 0.5）；每 0.5s 扫描补登/按 hitbox 变化重登，死亡/消失即 Remove，组收尾 Cleanup 全清。
- **巨木龙卷五连**：0xBE83~0xBE87 五条同开读 → 中心=各自 EffectPosition（相同，场心北 13y）：注册 圆 R12 / 环12-18 / 18-24 / 24-30 / 30-36，activation 依次为 读条剩余+0.3 + 0/2/4/6/8s，hit 1.5s。提示"由内向外依次结算，先站 12 内或 36 外"。
- 簌簌轻风 0xBE8A：FanTemplate(60, 90°)；0xBE8B/0xBE8C：FanTemplate(60, 150°)。**三者均以读条 EffectPosition 为原点、施法实体的 Character.CastRotation 为朝向，使用 StaticTransform 锁定，不跟随施法者位置或转身**。BMR `CrucibleTreantAOEs.ConfigFor` 为它们指定 LocationTargeted=true，`ReplayValidatedCastAOEs` 保存 `spell.LocXZ / spell.Rotation`，其中 `spell.Rotation` 来自 `WorldStateGameSync` 读取的 `CastRotation`；半径和半角来自 `CrucibleSpells`，无额外偏转。脚本与通用识别均通过 `CastRotation.TryGet` 检查实体地址、当前读条及 ActionId 后读取；不可用时记录日志并跳过预测，不回退到普通 `SourceRotation`。通用 `AoeShapeDatabase` 配置相同精确形状和静态绑定。
- 橡果炸弹 0xBE8D：R3 静态落点；释放汁液 0xBE8F：R5 静态落点；蛛网屏 0xBE91：RayRect Symmetric(4, 40) 静态落点 + CastRotation（三者 BMR LocationTargeted=true，与簌簌轻风同规则，不跟随施法者；蛛网屏 CastRotation 不可用时记日志跳过）。
- 小怪提示（BMR CrucibleGroveAdds 降级）：组内存活成员期间节流提示"优先击杀小怪（蛞蝓/花苗），别被贴身缠住！"（BMR 的小怪优先级/StayAtLongRange 属 AI 仇恨干预，不移植）。
- 文字：0xBE8E 治疗之水"优先打断/击杀治疗花苗"。

### B30 榴弹怪（圆场 R20）
- 烈火怒骂 0xBE9F：FanTemplate(40, 180°) 施法者朝向。
- **移动爆弹**：OnUpdate 扫存活火弹（14625/0x4CD4）→ 注册/维持持久危险区 R6 跟随实体（AddPersistentDanger + FollowEntityPosition）；冰弹（14626）R10 同理；命中 0xBE98/0xBE9A → Remove。
- 毒池：EObj 0x1E8D9B → AddPersistentDanger R6。
- **熔毁**：效果包 0xBEA0 → 注册 RayRect Symmetric(12, 40)（StaticTransform(caster 位置, 朝向)，delay 0.9s，hit 1s）；0xBEA1 命中自清。
- 文字：0xBE96"榴弹怪连续大爆炸：优先处理"；0xBE99 冰棘屏障。

### B31 怨毒龙（龙场 R19.75 实体墙，中心 (920,-420)）
- 浓烟呛呕 0xBEAC / 蠕动的痰 0xBEB3：圆 R6 落点。低温俯冲 0xBEAE：RayRect Symmetric(20, 48)。
- **猛毒吐息六连**：0xBEA7 读条开始 → 通过 `CastRotation.TryGet` 锁定施法实体的 **Character.CastRotation**，安全点 = 场心 − 20×施法方向（PR `ForwardVector3FromRotation`）；施法朝向不可用时记录日志并跳过预测，不回退到普通 `SourceRotation`。立即注册 `AddTimedSafeArea(CircleTemplate(7), StaticTransform(安全点,0), 读条剩余+0.3+2.4+6.5)`（持续窗口对齐 BMR `_expires = 首跳+6.5s`），由统一视图绘制目标圈；提示"提前站进安全点，全程不动"。0xBEA8 命中事件用于校正（安全点改为**波源实际位置** = 0xBEA8 施法辅助体当前位置，与 BMR `_origin = caster.Position` 一致；原"caster 位置 − 20×朝向"表述删除；仅首跳附近一次校正）。
- **连线放毒 4 点引导**（最复杂，完整迁移）：触发= tether 0x11（龙→玩家）或 0xBEA9 读条以玩家为目标。路线：以"开始时 Boss 相对场心方向"为北 N，侧向 S=N 转 90°；outer=场心+16.9×N，inner=outer−7×N；四点顺序 P1=inner+7S → P2=outer+7S → P3=outer−7S → P4=inner−7S（0.2y 目标圈 Draw + 连线）。推进：OnUpdate 检测新毒池 EObj 0x1EB704 出现在当前目标点 1y 内（忽略 >3.1s 的旧池快照）→ 推进下一点；被点名期间对当前点注册 `AddTimedSafeArea(CircleTemplate(0.2), ..., preference: PositionPreference.Center)` 引导，**推进切点时先 `Remove` 上一点的 SafeArea 句柄再注册新点**（旧圈不得残留到超时，否则解算停在旧点）；一轮 ≤16.5s 超时复位。第 4 池确认当次即注册该池的持续危险区并开始逃离。仅当**被连线者是本地玩家**才给 SafeArea/绘制，否则只画线。**预备阶段**（BMR `_prepareUntil`）：空降中心 0xBEBC 命中后开 8s 预备窗——以龙的当前位置重建路线（近场心时保留上轮北方），若上一轮被连线者是本地玩家，对 1 号点注册 R0.2、Center 偏好的安全区引导 + 画点/引导线并提示"提前就位"；新一轮 BeginRound 或超时即撤。
- **放毒精度**：四个引导点及预备点均要求进入中心 0.2y 内；预备路线变化即刷新目标。PR 的 Reborn 解算器在 R0.2 小目标激活时采用 0.1y 网格保留可达目标格，抵达最佳 Center 候选后结束搜索，移动容差按「目标半径 − 格心偏差」进一步内缩，避免网格误差与停止距离叠加后停在圈外；毒池匹配仍保留 1y，容纳快照与物件生成位置差异。必须重新编译并加载带此修正的 PR 本体，单独刷新脚本不足以生效。
- **毒池**：EObj 0x1EB704 出现 2s 后 AddPersistentDanger R6（第 4 池确认当次立即生效）；EventState 7 撤销。已站在生效毒池中时，Reborn 自动移动立即逃离，即使同时叠有未来 AOE 也不等待玩家读条结束。
- **毒气团**：存活 0x4CDA/NameID 14630 → AddObstacle R2 跟随 + OnUpdate 逐帧估速（>4y/s 或单帧 >5y 视为传送不预测），画 1.2s 位移胶囊预览（Draw）。
- **空降二连**：0xBEB0 外环读条 → 期间注册落点中心 `AddTimedSafeArea(CircleTemplate(5), ...)`（先靠近）+ 提示"先靠近，外环判定后立即出中心"；0xBEBC 中心 R6 圆注册（delay=读条剩余+0.3）。中心圈对齐 BMR CrucibleDragonLanding 的 non-risky 语义：外环 pending 期间 0xBEBC 读条只画 `Draw.AddAoe` 预览（不进解算，防止提前把玩家推出中心安全点），外环命中/到点（activation+0.5s）/打断后按读条剩余注册真实危险区。
- 文字：0xBEA7、0xBEB0、0xBEB5"剧毒全场注意减伤"。

## 5. 降级/不做清单（相对 BMR）

1. 击退全部降级为"提示 + 可选场心安全区引导"（PR 无击退落点校验 API）。
2. 击杀优先级干预 → 文字提示。
3. ~~树精本体碰撞圈（hitbox+0.5）不做~~ → 已补齐：见 §4 B29「本体目标圈」（BMR CrucibleTreantBody）。
4. 疾跑推送不做。
5. BMR 的"风险分级/轮廓显示"不移植——SafeZone 解算器时间感知，全量注册即可；雷达模块自动按 TelegraphProgress 渐变显示。

## 6. 验收清单（评审用）

- [ ] 10 组全部 handler 注册且受机制开关控制；激活检测按 NameID（非 OID 哨兵）。
- [ ] §4 每条机制的 AID/形状/时序与本规格一致（抽查重点：五连锥 2.1s/45° 步进、钢铁 R13→月环6-30、半场斩 2.8s/4.85s、龙卷 2s 间隔、毒浪安全点 −20×朝向、放毒路线 16.9/7/18.3 几何）。
- [ ] 打断撤销：0xBE32 腐烂恶臭、0xBE6C 岩墙等注册后经 OnCancelCast/OnInterruptCast 能 Remove。
- [ ] EObj 三个预测状态与 MapEffect 石墙 index 17~28 正确。
- [ ] 章节魔法三个 pad 的 DataId/状态 ID/3s 显示窗/0xBE13 的 +0.8s。
- [ ] 脚本 OnStop 清理全部句柄（含 ClearArenaBoundary、电网、池危险区）。
- [ ] 毒池脚下生成后能逃出、池外路线能绕开；毒龙保留 2s 延迟，第四池确认当次立即生效；已生效毒池与未来 AOE 重叠时不等读条。
- [ ] B29 橡果炸弹/释放汁液/蛛网屏为静态落点绑定（蛛网屏用 CastRotation），不跟随施法者。
- [ ] B31 空降中心圈在外环判定前不进解算（仅预览）；命中后 8s 预备引导指向下一轮 1 号点。
- [ ] 连线绘制仅本地玩家目标按距离变色，他人中性色。
- [ ] 无静态可变状态（铁律 7）；全部事件 API 走脚本基类订阅（铁律 6）。

## 7. 已知限制（评审后补记）

- **热重载中段机制恢复缺口**：战斗进行中热重载脚本时，依赖一次性事件建立的机制状态会丢失。已缓解：B31 连线放毒由每秒在途 0xBEA9 读条扫描补开轮；B26 击退链由 0xC608 命中事件 +9s 兜底预测（均同 BMR 迟到加载路径）。未缓解：B26 瘴气地雷的已布防 tether 无法恢复——Dalamud 对象表不暴露 tether 状态（BMR 靠每帧读 `actor.Tether` 兜底），已布防雷需等下一次布防事件。
- **B25 低温俯冲 46×23**：BMR 有意跳过，本移植按 CrucibleSpells 表补齐，尺寸未经实测。
- **MapEffect index 别名坑**：同图第三盘（f1x3）会反转电网 0/1 索引且其 17+ 段是道具控制。缓解：石墙 17~28 的"开"仅在 0xBE6C 岩墙读条已开始后接受；电网 0/1/2 仅在脚本已锁定对应场地（有组激活占用）时响应。
- **B31 毒浪校正**：以 0xBEA8 施法辅助体实际位置为准（BMR 行为）；读条开始时的预测安全点仍是"场心 − 20×读条朝向"。

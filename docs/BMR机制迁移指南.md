# BMR 机制迁移指南（BossModReborn → MasterPlayer 机制脚本）

本文沉淀斗兽棋·高一（`Scripts/CrucibleHigh1.cs`，规格 `docs/reference/CrucibleHigh1.spec.md`）迁移过程中逐行核对过的语义对照、映射模式与踩坑记录。迁移任何 BMR 模块前请读完本文，并配合 `bmr-mechanic-migration` 技能的流程执行。

- BMR 源码：`C:\Users\xiaos\Documents\GitHub\BossModReborn-7.56`（斗兽棋模块在 `BossMod/Modules/Dawntrail/Foray/Crucible/`）
- PR 本体源码：`C:\Users\xiaos\Documents\GitHub\PromeRotation-1.0-XSZFork\PromeRotation\`（API 签名的唯一权威来源）
- 目标脚本基建与铁律：`AGENTS.md` §3.1 / §5 / §6；脚本开发门面用法：`docs/脚本开发指南.md`

---

## 1. 迁移流程总览

1. **盘点组件清单**：找到 BMR 模块的 States 类（斗兽棋在 `XBMBattles.cs`，如 `XBMB22States`），列出 `ActivateOnEnter<...>()` 的全部组件；**别忘了基类隐式组件**（`RecordedCrucibleStates.GroupPhase()` 自带 `CrucibleBoundary` 电网组件；`RecordedCrucibleModule` 自带场地中心锁定逻辑）。
2. **逐组件精读源码**，为每个组件写「语义卡」：触发事件 / 形状与尺寸 / 时序（含 NPC 补正）/ 撤销路径 / 兜底路径 / 提示文本 / AI 行为。**逐行读，不凭记忆猜**。
3. **写 spec 文档**：`docs/reference/<机制名>.spec.md` 是该脚本内容的唯一事实来源——常量表（NameID/OID/AID/EObj/Tether/MapEffect/状态，十六进制+十进制双标注）、场地表、逐机制规格、降级清单、验收清单。先写 spec 再写码，实现偏离 BMR 时在 spec 里注明原因。
4. **实现脚本**：复用既有基础设施（见 §7），每组敌人/每个机制一个 handler 或状态机。
5. **回归测试**：`tests/` 桩基建直接编译脚本源码做行为断言（见 §10）。
6. **双工程构建**：`dotnet build MasterPlayer.csproj` 与 `dotnet build tests/MasterPlayer.RegressionTests.csproj` 都必须 0 错误；`dotnet run --project tests/MasterPlayer.RegressionTests.csproj` 全绿。
7. **验收 + 收尾**：过 spec 验收清单；已知限制写进 spec；游戏内热重载实测。

---

## 2. BMR 组件模型速读

BMR 模块 = `BossModule`（场地/主 Actor/重置判定）+ States（`StateMachineBuilder`，注册组件）+ 组件（`BossComponent` 派生，事件回调 + 绘制 + AI 提示）。迁移时对应关系：

| BMR | MasterPlayer 脚本 |
|---|---|
| `BossModule` 场地中心/边界 | handler 的 `LockedArena`/`ArenaCenter` + `SafeZoneRebornRegistry.SetArenaBoundary` |
| States `ActivateOnEnter` | handler 注册进 `_handlers`，按 NameID 扫描激活 |
| 组件事件回调 | handler 的 `OnCastStart/OnEffect/OnTether/...` 覆写 |
| 组件 `Update()` | handler 的 `Update(deltaTime)`（脚本 `OnUpdate` 每帧驱动） |
| `AddHints`（文字提示） | `Log` 节流提示（按 key 10s 节流，受「文字提示」机制开关控制） |
| `AddAIHints`（禁区/目标/优先级） | 危险区注册 / `AddTimedSafeArea` 引导 / 降级为提示（见 §6） |
| `DrawArenaBackground/Foreground` | 不需要——SafeZone 注册的区域由雷达悬浮窗与真实世界绘制自动渲染；额外辅助线用 `Draw` 门面 |
| `SimpleKnockbacks`（击退） | 降级：提示 + 画源点（PR 无击退落点校验 API） |
| `Components.Voidzone`（常驻池） | `VoidzoneWatcher`（EObj 周期扫描） |
| `Components.CastHints` | `OnCastStart` 里 `Hint(...)` |

**`ReplayValidatedCastAOEs` 关键语义**（BMR 读条 AOE 的基类，`CriticalEngagement/ReplayValidatedCastAOEs.cs`）：

- `activation = Module.CastFinishAt(spell)` = 读条剩余 + **0.3s NPC 完成补正**（`ActorCastInfo.NPCFinishDelay`）。
- `origin = config.LocationTargeted ? spell.LocXZ : caster.Position`；`rotation = spell.Rotation`（来自 FFCS `Character.CastRotation`，**不是**实体朝向）。全部为**读包时刻的静态快照**，不跟随实体。
- 同 (action, caster) ±0.75s 内重复读条只刷新；命中事件按 activation+0.5s 窗口结算；`OnCastFinished` 剩余 >0.5s 且事件未发生 = 打断撤销；actor 死亡/销毁移除其全部 pending。
- `Risky` 分级（只禁第一波/只画前两段等）是**显示与 AI 紧迫度**取舍——**不迁移**：SafeZone 全部注册，PR Reborn 解算器的安全时间区间搜索天然支持错峰判定转场。

---

## 3. 事件映射（逐条核对过字段）

| BMR 回调 | MasterPlayer 脚本事件 | 注意 |
|---|---|---|
| `OnCastStarted` | `OnStartCast(e)` | 主线程派发，回调里直接注册。`e.ActionId` 是 **ushort**；`CastTime` 为读条总时长（秒，float） |
| `OnCastFinished` 且 `!EventHappened && NPCRemainingTime > 0.5f`（打断） | `OnCancelCast` / `OnInterruptCast` | 注册过的区域必须在此撤销 |
| `OnEventCast`（效果包） | `OnActionEffect(e)` | **PR 按目标展开同一动作**——必须按 `e.GlobalSequence` 去重（`HashSet<uint>`），否则多目标包会重复推进状态机 |
| `OnTethered` / tether 解除 | `OnTether` / `OnTetherCancel` | `e.TetherId`；SourceId/TargetId 为 ulong，实体 API 需 `(uint)` 截断 |
| `OnActorEAnim(actor, state)` | `OnEObjAnim(e)` | state 组合 = `(e.Param1 << 16) | e.Param2`（与 BMR 同序） |
| `OnActorEState(actor, 7)` | `OnEObjState(e)` | `State == 7` = 物件休眠/消失 |
| `OnMapEffect(index, state)` | `OnMapEffect(e)` | state = `e.Param1 \| ((uint)e.Param2 << 16)`；`0x00020001` 开 / `0x00080004` 关 |
| `Update()` | `OnUpdate(dt)` | 周期扫描（对象表/EObj）按 0.3~1s 节流，不要每帧扫 |
| `actor.Tether` 每帧读取 | **无对应 API** | Dalamud 对象表不暴露 tether 状态——只能事件 + 存活扫描兜底；热重载恢复缺口写进 spec 已知限制 |
| 读条在途扫描（迟到/热重载恢复） | `IBattleChara.IsCasting / CastActionId / CastTargetObjectId / TotalCastTime / CurrentCastTime` | 每秒扫一次补开机制（B31 连线放毒即用此法） |

**事件字段对照（效果包）**：`spell.MainTargetID` → `e.TargetIndex == 0` 的目标（或 `e.TargetId`）；`spell.TargetXZ` → `e.EffectPosition`；`spell.Rotation` → `e.SourceRotation`；`caster.Position` → `e.SourcePosition`；`caster.OID` → `e.SourceDataId`。

---

## 4. 形状换算（锥/矩形是半值→全值，最容易写错）

| BMR 形状 | PR 模板 | 备注 |
|---|---|---|
| `AOEShapeCircle(r)` | `CircleTemplate(r)` | |
| `AOEShapeDonut(inner, outer)` | `DonutTemplate(inner, outer)` | |
| `AOEShapeCone(R, halfAngle)` | `FanTemplate(R, 2×halfAngle)` | **BMR 锥是半角，PR 扇形是全角** |
| `AOEShapeRect(front, halfWidth)` | `RayRectangleTemplate.Symmetric(2×halfWidth, front)` | **BMR 矩形是半宽，PR 是全宽**；从施法者向前延伸 |
| `AOEShapeRect(front, halfWidth, back)` | `RayRectangleTemplate(minLocalX, maxLocalX, length)` | 带后伸的矩形用非对称构造 |
| 十字/视线等特殊形 | 无对应模板 | 跳过或降级（同 BMR `AIHintsBuilder.GuessShape` 的取舍） |

其他约定：FFXIV 弧度 0 = +Z、π/2 = +X；世界坐标 X/Z 为地面平面；`e.EffectPosition` = 读条落点，`e.SourcePosition` = 施法者位置。

**危险区余量（双层，写测试断言时别忘了）**：脚本层 `RegisterDanger` 对圆/月环 +0.35（对齐 BMR `CrucibleScorpionAI.Avoid` 的导航余量），门面层 `ScriptSafeZoneApi` 再按 `DodgeDangerMargin`（默认 0.5）经 `DangerMarginInflator` 外扩——注册进解算器的圆半径 = 标称 +0.85。**SafeArea / Obstacle 两层都不加**。BMR 的余量只影响导航不影响显示，本插件注册即显示，属于有意的保守取向；某个区域若要与 BMR 显示尺寸一致，直接调门面注册（如 B29 树精本体圈只经门面 +0.5）。

---

## 5. 时序换算

- `Module.CastFinishAt(spell, d)` → `e.CastTime + NpcCastPadding + d`（`NpcCastPadding = 0.3f`，NPC 完成补正）。
- 危险区生效再提前 0.25s（`DangerTimeMargin`，对齐 `CrucibleScorpionAI.Avoid` 的 `AddSeconds(-0.25)`）。
- BMR 的「activation +Ns 后移除」→ `AddDelayedHit` 的 `hitDurationSeconds` 参数或簿记超时（`DateTime.UtcNow` 记录，`Update` 里比对）。
- 序列机制（五连锥 2.1s 间隔、龙卷五连 2s 间隔等）按 BMR 注释里的实录间隔逐段注册各自时间窗。

---

## 6. BMR AI 行为 → 本插件语义转换

BMR 的 `AddAIHints` 是导航约束语言，本插件用 SafeZone 目标区/危险区表达。已验证的转换模式：

| BMR 写法 | 本插件写法 | 实例 |
|---|---|---|
| `hints.GoalZones.Add(拉向某点)` | `AddTimedSafeArea(CircleTemplate(0.2f), At(点), 时长, preference: PositionPreference.Center)` | B31 连线放毒四点引导：**精确踩点用 R0.2 + Center**（Nearest 贴边会停在圈边，不到点上）；仅引导方向用大圈 + Nearest |
| `SDInvertedCircle(origin, R)`（不得离开 R 范围） | 环带/贴近安全区 `DonutTemplate(inner, R)` | B23 五连锥贴近区：`DonutTemplate(4, 11)`——**圆心是五道锥公共顶点，任何方向都被覆盖，必须用环带排除圆心** |
| 预览但暂不推人（`Risky = false` 的 pending AOE） | `Draw.AddAoe` 只画不进解算 + 门控变量，条件解除后按剩余时间注册 | B31 空降中心圈：外环 pending 期间只预览，外环命中/到点/打断后注册 |
| 击退落点校验（`DestinationUnsafe`/落点禁区） | 降级：提示 + 画源点 + 可选场心安全区引导 | B22 暴风 25y、B26 五连斩 20y、B28 击退 17y |
| 击杀优先级（`hints.SetPriority`/`FindEnemy`） | 降级：节流文字提示 | B22 羽毛优先击杀、B29 小怪提示 |
| Sprint 推送（`hints.ActionsToExecute`） | 不移植 | — |
| 错峰判定（先站后判定区、再进已判定区） | **不需要脚本处理**：PR Reborn 解算器的安全时间区间搜索原生支持；脚本只须把每段的准确时间窗注册全 | 龙卷五连、五连锥 |

**安全区形状必须贴合机制几何语义**：引导区覆盖的区域若任何角度都被危险覆盖（如锥心、全场正中），用环带/偏心点把它排除，否则雷达上的绿圈会把玩家引向必死点。

---

## 7. 本插件可复用的基础设施（斗兽棋脚本实证）

- **`CastAoeTracker`**：读条 → 形状+绑定 → `AddDelayedHit`；按 (casterId, actionId) 簿记，±0.75s 且落点 ≤1y 的重复读条只刷新（对齐 BMR `CastMatchTolerance`），相差更大的视为并存波次共存；`OnCancelCast`/`OnInterruptCast` 按键撤全部；`hitDuration + 2s` 簿记过期。
- **`VoidzoneWatcher`**：EObj 常驻池（毒池/冰池/火焰池）→ 0.3s 扫描对象表按 `BaseId` 找 `ObjectKind.EventObj`，跳过 `EventState == 7`，存活即注册持续危险区、消失即撤；`armDelaySeconds` 支持延迟激活（B31 毒池 2s），`ActivateImmediately` 支持立即激活（第 4 池）。
- **`TetherDrawer`**：连线绘制；**仅本地玩家目标按距离变色（<20y 红 / ≥20y 绿），他人连线中性色**（BMR `Colors.Object` 语义）；0.5s 存活校验。
- **handler 状态机**：每组敌人一个 `GroupHandler` 嵌套类，`Activate/Cleanup/Update/On*` 覆写；组激活按 NameID 扫描，全灭自动收尾。
- **`CastRotation.TryGet(e, out rotation)`**：读条朝向需锁定时用（簌簌轻风/蛛网屏/猛毒吐息）；**失败记日志并跳过该预测，不回退 `SourceRotation`**（测试锁定了这个语义）。

---

## 8. 绑定选择（本次迁移踩过的最大的坑）

| 机制形态 | 正确绑定 | 反例 |
|---|---|---|
| BMR `LocationTargeted = true`（落点型读条） | `At(e.EffectPosition, rotation)` 静态 | 跟随施法者（B29 橡果炸弹/释放汁液/蛛网屏曾写错） |
| 落点型的朝向 | `CastRotation.TryGet` 读 `Character.CastRotation` | 用实体朝向 `SourceRotation`（读条开始后 Boss 转身会带偏） |
| 转向读条挂 Boss 本体、实际伤害由场心辅助体发出 | 锚定**场地中心**，朝向取转向读条 | 锚 Boss 读条时位置（B23 五连锥曾因此偏离场心） |
| 自身圆/近战范围（读条期间 Boss 不动） | `FollowEntityPositionAndRotation` 或静态均可，按机制选 | — |
| 常驻跟随（树精本体圈/移动爆弹/毒气团） | `FollowEntityPosition` + `MissingEntityPolicy.RemoveArea` | — |

判断依据：BMR 组件里 `origin = spell.LocXZ`（静态落点）还是 `caster.Position`（ caster 快照）——**BMR 从不跟随实体**，全部是读包时刻快照。本插件的 `Follow*` 绑定是规格 §0 对「施法者位」机制的有意选择，读条期间 Boss 可动的机制用它更准，但落点型机制必须静态。

---

## 9. 序列与状态机机制模式

- **多段同开读**（龙卷五连 48771~48775）：五条读条各自剩余时间天然携带间隔，逐条注册即可。
- **视觉读条预测 + 实际读条校正**（五连锥/钢铁月环/落石）：预测序列全量注册 → 实际短读条出现按 ±1s（或落点 ≤0.5y）匹配替换 → 打断全撤 → 簿记超时兜底。
- **效果包触发的预测**（50932 冲锋双向半场斩/48761 跳跃月环）：`GlobalSequence` 去重 + 来源 OID/位置双门槛过滤辅助体误报；每段命中推进状态机；末段命中清空；activation+0.5s 超时兜底；**目标死亡兜底**（B28 跳跃曾漏）。
- **链式预测**（五连斩击退链 50695→50696→48724→48725）：逐链收紧预测时间，不回退到更早阶段；链中命中事件可做热重载兜底预测。
- **EObj 动画四状态机**（落石 0x1EC0D2）：预告态 +10s / 最终态收紧到 +3.1s，重复/乱序动画**不得推迟**预警（保留较早 activation）。

---

## 10. 回归测试基建（tests/）

测试工程把 `Scripts/*.cs` 源码直接编译进测试程序集，`MasterPlayer.Scripting` 命名空间由 `tests/ScriptTestHost.cs` 提供**桩实现**：

- `ScriptSafeZoneApi` 桩直连真 `SafeZoneManager`（**无门面余量**），脚本层 `RegisterDanger` 的 +0.35 仍生效——**断言圆/月环半径要含 +0.35**（如标称 R3 → 实际 3.35）。
- `ScriptDrawApi` 桩全空操作；`Draw.AddAoe` 预览不进快照——预览只能断言「未进解算」（`Areas.Count == 0`），不能断言绘制内容。
- `Log` 桩抛 `NotSupportedException`：测试路径上所有提示必须走 `Hint(...)`（桩 `IsMechanismEnabled` 仅对「文字提示」返回 false，天然挡掉）。
- `Svc.Objects` / `Svc.Data` 用 `ScriptServiceProxy`（DispatchProxy）按方法名替换；**handler 里每多用一个对象表成员，代理就要多映射一个**，否则 `NotSupportedException`。
- `CastRotation` 相关测试用 `NativeMemory.AllocZeroed` 伪造 `BattleChara`，设置 `CastInfo.IsCasting/ActionId` 与 `CastRotation` 字段。
- 现有战斗夹具：`GargoyleFight`（效果包序列）、`CastFight`（读条+CastRotation，含 `Send/SendEffect/SendCancel`）、`TreantFight`（存活扫描）、`BaitFight`（连线+毒池推进+预备引导）。新机制优先复用/仿照它们。
- 运行：`dotnet run --project tests/MasterPlayer.RegressionTests.csproj`（先 `dotnet build tests/...`）。

---

## 11. 验收清单（迁移完成标准）

- [ ] BMR States 里每个组件（含基类隐式组件）在脚本中有对应处理，或已在 spec 降级清单中逐条注明原因。
- [ ] 每个机制的 AID/形状/尺寸/时序与 spec 一致；锥半角→全角、矩形半宽→全宽的换算逐个复核。
- [ ] 落点型机制全部静态绑定；需锁朝向的走 `CastRotation.TryGet` 且失败只记日志不回退。
- [ ] 全部接管 AID 已加入 `SuppressGenericDodge`（否则通用躲避会重复注册）；`AoeShapeDatabase` 静态表同步补齐精确形状（脚本未运行时通用躲避用）。
- [ ] 效果包事件全部 `GlobalSequence` 去重；打断路径（`OnCancelCast`/`OnInterruptCast`）能撤掉全部已注册区域；每条预测链有超时与实体死亡兜底。
- [ ] 引导区形状不覆盖必死点（锥心/正中）；精确踩点用 R0.2 + `PositionPreference.Center`。
- [ ] 机制开关关闭时该组区域持续清理干净；`OnStop`/`Cleanup` 幂等全清（含 `ClearArenaBoundary`）。
- [ ] 无静态可变状态（铁律 7）；全部事件走脚本基类订阅（铁律 6）。
- [ ] 回归测试全绿；`MasterPlayer.csproj` 与测试工程均 0 警告 0 错误。
- [ ] spec 已同步实现现状，已知限制（热重载恢复缺口、未实测尺寸等）逐条记录。

---

## 12. 踩坑实录（斗兽棋·高一实际案例）

1. **锥/矩形半值坑**：BMR `AOEShapeCone(50, 45°)` = 总开角 90°；`AOEShapeRect(13, 4)` = 全宽 8。直接抄参数会画出两倍/一半的形状。
2. **落点跟随坑**：BMR `LocationTargeted=true` 的技能（簌簌轻风/橡果炸弹/释放汁液/蛛网屏）必须静态锚定 `EffectPosition` + `CastRotation`，跟随施法者会被 Boss 移动/转身带偏。
3. **顶点归属坑**：魔界花转向读条挂在 Boss 本体上，但五道锥实际由场心辅助体发出——预测顶点要锚场地中心，BMR 是靠后续实际读条逐道校正回去的。
4. **引导区语义坑**：五连锥的贴近区画成实心圆会把公共锥心标成"可站"；改成环带（4-11）排除圆心。
5. **non-risky 预览坑**：B31 空降中心圈若读条即注册，会把玩家提前推出"先靠近中心"的安全区——BMR 的 non-risky 语义要用 `Draw.AddAoe` 预览 + 门控注册还原。
6. **预备引导坑**：B31 连线放毒在空降中心命中后有 8s 预备窗（下一轮连线读条在命中 ~4.8s 后才来），光靠 tether 触发再跑位太晚——BMR 的 `_prepareUntil` 阶段不能省。
7. **多目标展开坑**：PR 把同一动作按目标展开成多个效果事件，`GlobalSequence` 不去重则二连斩会画出第三刀、放毒快照会重复记录。
8. **MapEffect 别名坑**：同图第三盘反转电网 0/1 索引且其 17+ 段是道具控制——石墙 MapEffect 的"开"必须以本组岩墙读条已开始为门槛。
9. **tether 状态坑**：Dalamud 对象表不暴露 `actor.Tether`，BMR 每帧重扫布防地雷的写法不可移植；只能事件驱动 + 已知限制记录。
10. **余量断言坑**：测试桩门面无余量但脚本层 +0.35 仍在，断言半径漏算余量会全挂。
11. **安全区残留坑**：引导切点时必须先 `Remove` 上一点的安全区再注册新点，旧圈残留到超时会让解算停在旧点。
12. **提示文本对齐**：BMR `CastHints`/`AddHints` 的文本是实战打磨过的（如章节魔法三档原文），迁移时逐条对齐原文而非自行改写。
13. **障碍/危险区语义坑**：`AddObstacle` 是硬碰撞（A* 不可穿越 + 路径段 `IntersectsSegment` 校验 + 硬余量膨胀），只约束寻路，**不会在危险逼近时主动让玩家逃离**——只用于真·碰不得的东西（岩墙、接触即爆的地雷）。持续伤害区（毒池、毒气团、本体圈）必须注册持续危险区（`AddPersistentDanger`，激活后 MaxG=0 迫使解算逃离，且永不解禁）；移动中的伤害体还要沿估计速度方向注册短时胶囊危险区（`AddTimedDanger`），玩家才能在其经过前提前规避。

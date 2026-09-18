---
name: bmr-mechanic-migration
description: 把 BossModReborn（BMR）的副本机制模块迁移或对齐到 MasterPlayer 机制脚本（PromeRotation 卫星插件）的标准流程。当用户要求迁移/移植 BMR 机制、对齐 BMR 脚本、编写斗兽棋或 CE 等新机制脚本，或核对脚本与 BMR 差异时使用。
metadata:
  short-description: 迁移/对齐 BMR 机制模块到 MasterPlayer 机制脚本时使用
---

# BMR 机制迁移流程（BossModReborn → MasterPlayer 机制脚本）

## 必读（按序）

1. `AGENTS.md`（项目根）——§3.1 共享契约、§5 核心 API、§6 铁律；以磁盘最新内容为准。
2. `docs/BMR机制迁移指南.md`——事件/形状/时序/绑定/AI 语义的逐条对照表与踩坑实录，迁移前必须读完。
3. 目标 spec：`docs/reference/<机制名>.spec.md`——脚本内容的唯一事实来源。已存在则遵循；不存在则在第 3 步创建。
4. 既有范例：`tests/Legacy/CrucibleHigh1.cs` + `docs/reference/CrucibleHigh1.spec.md`（斗兽棋·高一，全模式实证）。

## 参考仓库（逐行核对，不凭记忆臆造 API/参数）

- BMR 源码：`C:\Users\xiaos\Documents\GitHub\BossModReborn-7.56`（斗兽棋模块在 `BossMod/Modules/Dawntrail/Foray/Crucible/`）
- PR 本体：`C:\Users\xiaos\Documents\GitHub\PromeRotation-1.0-XSZFork\PromeRotation\`
- Dalamud：`C:\Users\xiaos\Documents\GitHub\Dalamud`；FFCS：`C:\Users\xiaos\Documents\GitHub\FFXIVClientStructs`

## 流程

1. **盘点组件**：找 BMR 模块 States 类（斗兽棋在 `XBMBattles.cs`），列出全部 `ActivateOnEnter<...>()`；补上基类隐式组件（如 `RecordedCrucibleStates.GroupPhase()` 的 `CrucibleBoundary`、`RecordedCrucibleModule` 的场地中心锁定）。列成「组件 → 处理方式」对照表。
2. **逐组件写语义卡**：触发事件 / 形状尺寸 / 时序（`CastFinishAt(spell, d)` = 读条剩余 + 0.3s NPC 补正 + d）/ 撤销路径（打断、命中、超时、实体死亡）/ 提示文本 / AI 行为。逐行读组件源码与基类（尤其 `CriticalEngagement/ReplayValidatedCastAOEs.cs`）。
3. **写/更新 spec**：常量表（NameID/OID/AID/EObj/Tether/MapEffect/状态，十六进制 + 十进制）、场地表、逐机制规格、降级清单、验收清单。
4. **实现**：复用迁移指南 §7 的基础设施模式（CastAoeTracker / VoidzoneWatcher / TetherDrawer / handler 状态机）；按指南 §4/§5/§6/§8 的换算表处理形状、时序、AI 语义与绑定。
5. **回归测试**：用 `tests/` 的桩基建（CastFight / BaitFight 等夹具）为新机制写行为断言；注意危险区断言含脚本层 +0.35 余量，预览类断言「未进解算」。
6. **验证**：`dotnet build MasterPlayer.csproj` 与 `dotnet build tests/MasterPlayer.RegressionTests.csproj` 均 0 错误；`dotnet run --project tests/MasterPlayer.RegressionTests.csproj` 全绿。
7. **收尾**：过迁移指南 §11 验收清单；spec 记录已知限制与降级项。

## 关键换算（细节以迁移指南为准）

- `AOEShapeCone(R, 半角)` → `FanTemplate(R, 2×半角)`；`AOEShapeRect(前向长, 半宽)` → `RayRectangleTemplate.Symmetric(2×半宽, 前向长)`。
- `LocationTargeted = true` → 静态 `At(e.EffectPosition, rotation)`；朝向锁定用 `CastRotation.TryGet`（失败记日志跳过，不回退 `SourceRotation`）。BMR 从不跟随实体，落点型机制禁止 Follow 绑定。
- `OnEventCast` → `OnActionEffect`：PR 按目标展开同一动作，必须 `GlobalSequence` 去重。
- BMR AI hints：GoalZone → `AddTimedSafeArea`（精确踩点用 R0.2 + `PositionPreference.Center`）；InvertedCircle → 环带安全区（排除被全向覆盖的中心点）；non-risky 预览 → `Draw.AddAoe` + 门控注册；击退落点校验/仇恨优先级 → 降级为提示。
- 错峰判定不迁移 BMR 的 Risky 分级——每段按准确时间窗全量注册，PR Reborn 解算器原生处理。

## 红线（违反即 bug）

- 铁律 1/2/3/6/7：主线程、Dispose 幂等全清、移动指令 last-write-wins 且只停自己接管的、脚本事件全走基类订阅、无静态可变状态。
- 脚本接管的全部 AID 必须进 `SuppressGenericDodge`；精确形状同步补进 `Dodge/AoeShapeDatabase.cs` 静态表（脚本未运行时通用躲避用）。
- 每条预测链必须有：打断撤销 + 超时兜底 + 实体死亡兜底。
- 引导区不得覆盖必死点（锥心/正中）；引导切点先撤旧安全区再注册新点。
- 几何尺寸/时序未经实测时，标注「待实测」并告知用户，不得臆造后静默上线。

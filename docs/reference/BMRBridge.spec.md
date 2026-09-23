# BMR 宿主桥接规格（BmrBridge）

日期：2026-09-23。承接 [BMRRuntime.spec.md](BMRRuntime.spec.md) 的「必须继续完成」清单，本文档是 MP↔BMR 宿主适配的唯一设计事实来源。目标：BMR 原源码运行库在 MasterPlayer 内接收实时/回放世界输入，模块激活后其提示输出（AOE 绘制、导航区域、时间轴、文字提示）进入 MP 既有双视图与安全区解算，全程不修改 PR 本体。

## 总体结构

```
游戏对象表/网络包 ──► MP 采样与事件（BmrBridge/，主项目）
                          │  转换为纯数据帧/事件 DTO
                          ▼
                  BmrWorldFeed（BmrRuntime/Hosting/，纯逻辑，离线可测）
                          │  差分 → WorldState.Operation（先删后建，帧首 OpFrameStart）
                          ▼
                  BmrSession（World + BossModuleManager + ZoneModuleManager）
                          │
              ┌───────────┼────────────────┬──────────────┐
              ▼           ▼                ▼              ▼
        AIHints 聚合   MiniArena 捕获   BmrTimeline    TextHints/GlobalHints
        （导航桥）     （绘制桥）       （时间轴）     （文字提示→面板/日志）
```

分层纪律：帧差分与事件排序逻辑全部在 `BmrRuntime/Hosting/BmrWorldFeed.cs`（不引用 Dalamud 服务/PR 类型，可用 FFCS/Lumina 数值类型之外的纯托管数据），`tests/BmrRuntime` 用合成帧驱动验收；MP 侧 `BmrBridge/` 只做「实时对象表→DTO」「PromeEventBus→DTO」的转换与生命周期编排。回放模式复用同一 Feed：`ReplayPlayback` 帧/事件 → DTO → 独立 BmrSession，成为离线验收工具。

## 输入桥（M3）

### 帧节奏（每 Framework.Update，主线程）

1. `BmrSession.BeginFrame(now)`（禁止时钟倒退；暂停/回放由调用方给录制时钟）。
2. 区域变化：TerritoryType/CFC 变化时发 `OpZoneChange`（CFC 由 Lumina TerritoryType.ContentFinderCondition 解析；CFC 驱动 ZoneModuleManager，Territory 变化时 BossModuleManager 自动 ForceUnload）。
3. 对象差分（先删后建，对齐 WorldStateGameSync.UpdateActors）：
   - 本帧消失 → `OpDestroy`（其内部自动补发退战斗/解连线/CastFinished/清状态事件）。
   - 新出现 → `OpCreate`（OID=BaseId、SpawnIndex=对象表下标、LayoutID、Name/NameID、Type=ObjectKind&lt;&lt;8|SubKind、Class/Level、PosRot、HitboxRadius、HPMP、IsTargetable、IsAlly、OwnerID、FateID、Renderflags）。
   - 已存在 → 逐字段差分发 OpMove/OpHPMP/OpTargetable/OpDead/OpCombat/OpAlly/OpModelState/OpEventState/OpTarget/OpSizeChange/OpRenderflags/OpRename。
   - CastInfo 差分 → `OpCastInfo`（带读条朝向 CastRotation 与落点 TargetLocation；运行库内部有 0.2s 容差与 Tick 推进，不要每帧重发相同值）。
   - Statuses 差分 → `OpStatus`（逐槽，续期容差由运行库处理；ExpireAt 用 CurrentTime+RemainingTime 换算）。
4. 队伍：`OpPartyStateChange`/`PartyState.OpModify`（槽 0=本人，1-7 小队；小队用 PartyHelper.GetParty() 顺序；团队/友方 NPC 暂缓，记录缺口）。目标仇恨表 OpHateChange 暂缓。
5. 事件队列（PromeEventBus 主线程回调直接入队，帧内按到达顺序派发；不再需要 ConcurrentQueue，但保留入队-派发分离以便测试）：
   - StartCast/CancelCast/InterruptCast：只用于补充 CastInfo 落点/朝向精度（CastStart.EffectPosition、SourceRotation），实际读条状态仍以对象表帧扫为准（BMR 原版即如此，CastInfo 是帧扫通道）。
   - ActionEffect（PR 摘要）：**不直接转 OpCastEvent**；完整效果包由 MP 自挂 hook 捕获（见下）。
   - Tether/TetherCancel → `OpTether`（ActorTetherInfo(ID, Target)）；TargetIcon → `OpIcon`；TargetVfx → `OpVFX`；Marker → `OpSignChange`（头顶标记）；Waymark → `OpWaymarkChange`；MapEffect → `OpMapEffect`；EObjAnimation → `OpEventObjectAnimation`（Param1/Param2）；EObjSetState → `OpEventObjectStateChange`；DirectorUpdate → `OpDirectorUpdate`；NpcYell → `OpEventNpcYell`；SystemLog → `OpSystemLogMessage`；PlayActionTimeline → `OpPlayActionTimelineEvent`。
   - 战斗开始/结束：无直接 Op（InCombat 由对象差分覆盖）；切区清空由 OpZoneChange 处理。
6. `BmrSession.Update()`。

### 动作效果包捕获（MP 自挂，不改 PR）

`BmrBridge/BmrActionEffectCapture.cs`：用 `Svc.Hook.HookFromAddress<ActionEffectHandler.Delegates.Receive>(ActionEffectHandler.Addresses.Receive.Value, detour)` 复制 BMR ActionManagerEx.cs:753-776 的纯观察部分——构造 `ActorCastEvent`（Action/MainTargetID/AnimationLock/NumTargets/TargetPos/GlobalSequence/SourceSequence/包朝向 `IntToFloatAngle(RotationInt)` + 逐目标 8×ulong 原始效果）→ 主线程队列 → `OpCastEvent(casterID, info)`。detour 只读取入参并调用 Original，不修改动画锁、不拦截动作。hook 安装失败时降级：聚合 PR ActionEffect 摘要（同 GlobalSequence 窗口合并）构造无 Effects 的包，并记录一次性警告；降级只影响依赖效果数组的组件（击退距离、特定效果判定），OnEventCast 计数不受影响。

`OpEffectResult`（PendingHP/Status 确认）由对象差分兜底（HP/Status 每帧扫描），EffectResult 包暂不接，记录缺口。

### 会话生命周期

- `BmrBridgeController`（`BmrBridge/BmrBridgeController.cs`）：插件 Initialize 时在 `_mechanics` 之后构造；`Svc.Framework.Update` 中排在 `_dodge` 之前（模块当帧输出可参加当帧解算）；Dispose 反向对称、逐项 try/catch。
- 环境初始化：`BmrRuntimeEnvironment.Initialize(Svc.Data 的 Lumina.GameData, Svc.Log)`，失败（数据校验和等）则禁用桥并在诊断页报错，不影响其他组件。
- 会话：常驻一个 BmrSession；切区不退缩重建（OpZoneChange 驱动模块卸载/切换）；停止/Seek（回放）才新建会话。
- 门控：`MasterPlayerSettings.EnableBmrBridge`（默认 true）总开关 + `BmrMinMaturity`（默认 Verified，可降 Contributed/AISupport/WIP）。当前区域有 MP 区域专属脚本运行时（ScriptManager 已有查询），该区域暂停桥的模块激活与输出，避免与已移植脚本（斗兽棋、首批地城）双重接管。
- 回放：ReplayController 进入回放时桥切换为回放会话（输入来自 ReplayPlayback 帧/事件转换），退出恢复实时；回放中禁止任何移动输出（铁律 3）。

## 绘制桥（M4）

- 适配点：`BmrRuntime/Upstream/BossModule/MiniArena.cs` 单文件适配（登记 SourceChanges.json）：每个绘制原语在世界坐标层（RotatedCoords 之前）转发到可选静态汇 `BmrDrawSink`。Zone 族 13 方法在**形状参数层**捕获（不取三角化结果），描边/线/多边形/文字/角色标记同样在世界层捕获。`Begin/End` 的 ImGui 窗口管理在无汇时可空转。直画旁路（Gaze 眼睛、头顶标记贴图）一并转发。无汇时保持原行为（不影响离线测试与既有用例）。
- MP 侧 `BmrBridge/BmrDrawBridge.cs`：每帧 DrawArena 驱动改为「无 ImGui 捕获驱动」——调用 `module.DrawArena` 等效序列（背景→组件背景→前景→组件前景）但由捕获汇收集命令；命令按来源模块分组进 `AoeVisibilityRegistry`（键 `bmr/{moduleTypeName}`，名称取 ModuleInfo 显示名），双视图复用现有 `AddPreview/AddLine` 与配色/开关；文字提示进诊断/提示区。危险/安全配色默认映射 BMR Risky 语义，用户配色仍生效。

## 导航桥（M5）

- 每帧 `session.Update()` 后驱动 `AIHintsBuilder.Update(hints, playerSlot, false)`（rsr=null），消费：
  - `ForbiddenZones` → SD* 具体类型识别（SDCircle/SDRect/SDCone/SDDonut/SDCross 及组合）→ MP GeometryTemplate；无法识别回退 0.5 码网格采样连通域 → LocalPolygonTemplate。activation → 绝对时间（注册 AddDelayedHit/AddDelayedDanger；default=立即 → AddPersistentDanger + 帧 diff 续命）。键：`bmr/{sourceInstanceID}/{activationTick}/{序}`，帧 diff 移除未刷新句柄。
  - `TemporaryObstacles` → AddObstacle（同样 diff 生命周期）。
  - `PathfindMapBounds`（激活模块）→ SetArenaBoundary（Custom 多边形顶点直转；圆形/矩形边界转对应模板；模块卸载清空）。
  - `GoalZones`：仅当能识别为几何硬目标（GoalDonut/GoalRectangle 等工厂不可还原参数，故初版不映射权重型目标；记录缺口）。
  - `ForbiddenDirections` → 面向控制（经宿主 MechanicControl 可选接口，对齐斗兽棋先例；接口缺失时一次提示并跳过）。
  - `ImminentSpecialMode`（Pyretic/NoMovement/Misdirection）：映射为移动门控提示（初版仅记录+文字提示，不解算）。
  - `PredictedDamage/敌人优先级/ActionsToExecute`：不执行动作/减伤/转火调度（缺口记录；文字提示保留）。
- 注册仅当 `EnableAutoDodge`（智能躲避）开启时生效；绘制不受该开关限制（与 DodgeController 同纪律：绘制/雷达/躲避任一开启即识别）。两套余量体系不叠加：BMR activation 原样传入，MP 侧余量照常吃（结果更保守，可接受）。

## 时间轴（M6）

- 数据源：`BmrTimeline.Capture(module)`（保留分支未定与伤害标记）。
- 呈现：ReplayWindow「时间轴回放」页新增「BMR 预测」泳道（实时会话同样可显示当前激活模块预测）；MainWindow 诊断页显示激活模块/阶段/状态摘要。条目点击不 Seek（预测不是录制内容）。

## 验收（贯穿）

- `tests/BmrRuntime`：合成帧/事件驱动 Feed → 模块激活 → CastInfo/OpCastEvent/Tether/MapEffect 全链路断言；ZoneModule CFC 切换；对象销毁清理；回放 Seek 重建会话。
- 主回归/脚本冒烟保持全绿。实机项沿用 U05 口径记录，不宣称已实机验收。

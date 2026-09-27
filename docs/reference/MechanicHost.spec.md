# 机制宿主桥接规格（MechanicHost）

日期：2026-09-23。承接 [MechanicRuntime.spec.md](MechanicRuntime.spec.md) 的「必须继续完成」清单，本文档是 MP↔BMR 宿主适配的唯一设计事实来源。目标：BMR 原源码运行库在 MasterPlayer 内接收实时/回放世界输入，模块激活后其提示输出（AOE 绘制、导航区域、时间轴、文字提示）进入 MP 既有双视图与安全区解算，全程不修改 PR 本体。

## 总体结构

```
游戏对象表/网络包 ──► MP 采样与事件（MechanicHost/，主项目）
                          │  转换为纯数据帧/事件 DTO
                          ▼
                  MechanicWorldFeed（MechanicRuntime/Hosting/，纯逻辑，离线可测）
                          │  差分 → WorldState.Operation（先删后建，帧首 OpFrameStart）
                          ▼
                  MechanicSession（World + BossModuleManager + ZoneModuleManager）
                          │
              ┌───────────┼────────────────┬──────────────┐
              ▼           ▼                ▼              ▼
        AIHints 聚合   MiniArena 捕获   MechanicTimeline    TextHints/GlobalHints
        （导航桥）     （绘制桥）       （时间轴）     （文字提示→面板/日志）
```

分层纪律：帧差分与事件排序逻辑全部在 `MechanicRuntime/Hosting/MechanicWorldFeed.cs`（不引用 Dalamud 服务/PR 类型，可用 FFCS/Lumina 数值类型之外的纯托管数据），`tests/MechanicRuntime` 用合成帧驱动验收；MP 侧 `MechanicHost/` 只做「实时对象表→DTO」「PromeEventBus→DTO」的转换与生命周期编排。回放模式复用同一 Feed：`ReplayPlayback` 帧/事件 → DTO → 独立 MechanicSession，成为离线验收工具。

## 输入桥（M3）

### 帧节奏（每 Framework.Update，主线程）

1. `MechanicSession.BeginFrame(now)`（禁止时钟倒退；暂停/回放由调用方给录制时钟）。
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
6. `MechanicSession.Update()`。

### 动作效果包捕获（MP 自挂，不改 PR）

`MechanicHost/ActionEffectCapture.cs`：用 `Svc.Hook.HookFromAddress<ActionEffectHandler.Delegates.Receive>(ActionEffectHandler.Addresses.Receive.Value, detour)` 复制 BMR ActionManagerEx.cs:753-776 的纯观察部分——构造 `ActorCastEvent`（Action/MainTargetID/AnimationLock/NumTargets/TargetPos/GlobalSequence/SourceSequence/包朝向 `IntToFloatAngle(RotationInt)` + 逐目标 8×ulong 原始效果）→ 主线程队列 → `OpCastEvent(casterID, info)`。detour 只读取入参并调用 Original，不修改动画锁、不拦截动作。hook 安装失败时降级：聚合 PR ActionEffect 摘要（同 GlobalSequence 窗口合并）构造无 Effects 的包，并记录一次性警告；降级只影响依赖效果数组的组件（击退距离、特定效果判定），OnEventCast 计数不受影响。

`OpEffectResult`（PendingHP/Status 确认）：2026-09-27 已接入——`LiveWorldSampler` 逐角色扫描原生 `ActionEffectHandler.IncomingEffects` 32 槽（对齐 BMR WorldStateGameSync 的槽位语义，帧级稀疏上报非零槽），`MechanicWorldFeed` 按槽差分发 `OpIncomingEffect`（击退类效果即时武装 `PendingKnockbacks`，槽清空移除）；`TargetConfirmed` 翻转补发 `OpEffectResult` 加速待定 HP/MP/状态确认（不重发 OpIncomingEffect，避免重复武装）。回放录制未存槽位数据，回放帧该通道为空（保持降级口径）。

### 会话生命周期

- `MechanicHostController`（`MechanicHost/MechanicHostController.cs`）：插件 Initialize 时在 `_mechanics` 之后构造；`Svc.Framework.Update` 中排在 `_dodge` 之前（模块当帧输出可参加当帧解算）；Dispose 反向对称、逐项 try/catch。
- 环境初始化：`MechanicRuntimeEnvironment.Initialize(Svc.Data 的 Lumina.GameData, Svc.Log)`，失败（数据校验和等）则禁用桥并在诊断页报错，不影响其他组件。
- 会话：常驻一个 MechanicSession；切区不退缩重建（OpZoneChange 驱动模块卸载/切换）；停止/Seek（回放）才新建会话。
- 门控：`MasterPlayerSettings.EnableMechanicModules`（默认 true）总开关 + `MechanicModuleMinMaturity`（默认 Verified，可降 Contributed/AISupport/WIP）。当前区域有 MP 区域专属脚本运行时（ScriptManager 已有查询），该区域暂停桥的模块激活与输出，避免与已移植脚本（斗兽棋、首批地城）双重接管。
- 回放：ReplayController 进入回放时桥切换为回放会话（输入来自 ReplayPlayback 帧/事件转换），退出恢复实时；回放中禁止任何移动输出（铁律 3）。

## 回放接入（M7）

状态：2026-09-23 已实现（控制器回放会话生命周期 + `MechanicHost/ReplayFeedAdapter.cs` + `MechanicEventFactory.cs` + ReplayWindow 预测条；离线回归 `tests/MechanicReplayAdapterRegression.cs` 4 场景）。

- 挂接点：`ReplayController` 构造注入 `MechanicHostController?`（桥在插件 Initialize 顺序中先于回放控制器构造）；StartSession→`EnterReplaySession(录制区域)`、Seek 的 RestartSessionScripts→`RestartReplaySession()`、StopSession→`ExitReplaySession()`；`AdvanceTo` 的 dispatch 回调先 `OnReplayEvent` 再发脚本路由器，update 回调先 `OnReplayTick(World)` 再 `UpdateReplayScripts`（每步先喂桥再更新脚本）。
- 会话：实时会话与回放会话并存，输出侧（模块计数/时间轴/两座桥）读 ActiveSession 指针；`MechanicNavigationHost`/`MechanicDrawHost` 经 `SwitchSession` 切换（导航桥的 AIHintsBuilder 订阅 WorldState，切换时销毁重建）。回放中导航注册禁用（ReplayModeActive 判定），绘制/时间轴照常；回放区域有运行中的 MP 区域脚本时不激活模块（与实时压制一致）。
- 适配器取舍：CastStart/Cancel/Interrupt、状态增删、对象增删/瞬移/可选中、击退由帧通道覆盖返回 null；Tether/Icon/Marker/MapEffect/EObj/DirectorUpdate/NpcYell/PlayActionTimeline/TargetVfx 转 MechanicEvent*；ActionEffect 摘要按 GlobalSequence 合并为降级包（RawEffects 空，RotationInt 反量化编码，计 DegradedEffectCount）；Waymark 事件更新适配器 8 槽随帧下发；EventAnimation 变化补发 EObjAnimation 事件；Territory/Weather/Battle/Countdown/ChangeMap 由帧头/时钟覆盖或忽略（无对应 Op）。SpawnIndex 用帧内序号、LayoutID/FateId/Renderflags/ContentId 回放无存填 0；Cast.IsSpell 恒 true（回放不存 ActionType）、TotalTime=FinishAt−StartedAt、Rotation 缺省回退当前朝向；IsAlly 按 Kind/Hostile 推导（Pc 或 BattleNpc 非 Hostile 为盟友）。
- ReplayWindow 时间轴页顶部显示机制预测条（相对当前回放时刻的倒计时文本行，不随横向缩放，空数据不占行；M6 遗留在此落地）。

## 绘制桥（M4）

状态：2026-09-23 已实现（MP 侧 `MechanicHost/MechanicDrawHost.cs` + `MechanicDrawConvert.cs` + `RadarView.DrawMechanicExtras` + 诊断页提示区；离线回归 `tests/MechanicDrawConvertRegression.cs` 8 场景）。

- 适配点：`MechanicRuntime/Upstream/BossModule/MiniArena.cs` 单文件适配（登记 SourceChanges.json）：每个绘制原语在世界坐标层（RotatedCoords 之前）转发到可选静态汇 `MechanicDrawSink`。Zone 族 13 方法在**形状参数层**捕获（不取三角化结果），描边/线/多边形/文字/角色标记同样在世界层捕获。`Begin/End` 的 ImGui 窗口管理在无汇时可空转。直画旁路（Gaze 眼睛、头顶标记贴图）一并转发。无汇时保持原行为（不影响离线测试与既有用例）。
- MP 侧 `MechanicHost/MechanicDrawHost.cs`：每帧 DrawArena 驱动改为「无 ImGui 捕获驱动」——调用 `module.DrawArena` 等效序列（背景→组件背景→前景→组件前景）但由捕获汇收集命令；命令按来源模块分组进 `AoeVisibilityRegistry`（键 `module/{moduleTypeName}`，名称取 ModuleInfo 显示名），双视图复用现有 `AddPreview/AddLine` 与配色/开关；文字提示进诊断/提示区。危险/安全配色默认映射 BMR Risky 语义，用户配色仍生效。
- 落地差异（相对上一段设计）：模块显示名取 `module.GetType().Name`（BossModuleRegistry.Info 无 DisplayName 字段，BMR 自身窗口亦用类型名）；`MechanicDrawComplexPoly` 只取外轮廓（洞忽略），场地边框（Border 白）跳过由 MP 边界层绘制；ZoneTriangles 逐三角形转 `WorldPolygonGeometry`（不合并连通域）；extras（Eye/Text/Sign）经 `RadarView` 专用图层按模块键过可见性，Eye 用用户危险/安全色、Text 用 BMR 原色；安全色判定硬编码 ColorConfig 默认 ABGR（MP 不加载 BMR 配置文件）。已知缺口：ShowBorderRisk 时边框以 Enemy 色 ComplexPoly 出现，会短暂显示为用户危险描边；IconWorld（FontAwesome 图标）/ActorMarker/CardinalNames 跳过不画。

## 导航桥（M5）

状态：2026-09-23 已实现（MP 侧 `MechanicHost/MechanicNavigationHost.cs` + `MechanicHintsConvert.cs`，DodgeController 加机制模块激活压制；离线回归 `tests/MechanicHintsConvertRegression.cs` 9 场景）。

- 每帧 `session.Update()` 后驱动 `AIHintsBuilder.Update(hints, playerSlot, false)`（rsr=null），消费：
  - `ForbiddenZones` → SD* 具体类型识别（SDCircle/SDRect/SDCone/SDDonut/SDCross 及组合）→ MP GeometryTemplate；无法识别回退 0.5 码网格采样连通域 → LocalPolygonTemplate。activation → 绝对时间（注册 AddDelayedHit/AddDelayedDanger；default=立即 → AddPersistentDanger + 帧 diff 续命）。键：`module/{sourceInstanceID}/{activationTick}/{序}`，帧 diff 移除未刷新句柄。
  - `TemporaryObstacles` → AddObstacle（同样 diff 生命周期）。
  - `PathfindMapBounds`（激活模块）→ SetArenaBoundary（Custom 多边形顶点直转；圆形/矩形边界转对应模板；模块卸载清空）。
  - `GoalZones`：2026-09-27 起消费机制级强目标——权重峰值 ≥100（BMR「软覆盖」语义）才采样映射（0.5m 网格总和峰值 50% 以上区域轮廓，覆盖率 >65% 判噪声丢弃），注册为优先级 1 目标区，500ms 节流评估；uptime/身位等弱偏好（≤3）不拖拽走位。
  - `ForbiddenDirections` → 面向控制（经宿主 MechanicControl 可选接口，对齐斗兽棋先例；接口缺失时一次提示并跳过）。
  - `ImminentSpecialMode`（Pyretic/NoMovement/Misdirection）：映射为移动门控提示（初版仅记录+文字提示，不解算）。
  - `PredictedDamage/敌人优先级/ActionsToExecute`：不执行动作/减伤/转火调度（缺口记录；文字提示保留）。
  - 补集语义（2026-09-27）：SDInverted 族/SDOutsideOfUnion/SDPrecisePosition 不再丢弃——`TryConvertComplement` 取内侧安全形状（OutsideOfUnion 取「进入任一成员」主语义、Offset 变体仍跳过），经同一差集注册为优先级 1、Nearest 偏好的目标安全区（不外扩，与 SafeArea 纪律一致）；分摊/集合/数字格类机制从此获得导航约束。
  - 场地障碍位图（2026-09-27）：`PathfindMapObstacles`（SimpleBossModule 族开启 AllowObstacleMap）按角点网格连通域外轮廓转多边形注册为障碍；位图引用+矩形未变时复用转换结果，数据面随 9-27 执行层移除保留（ObstacleMapManager/417 资源）。
  - `Teleporters`（2026-09-27）：转 `SolverTeleporter` 经 `SafeZoneRuntime.SetTeleporters` 发布（内容签名去重，失活清空）；`BossModStyleAStar` 把入口圈内节点扩展出出口后继（耗时按移速折算进 g，落点不可站立跳过，leeway/Score 语义与步行一致）。FRU/M08S/末日列车的传送机制参与解算。
- 注册仅当 `EnableAutoDodge`（智能躲避）开启时生效；绘制不受该开关限制（与 DodgeController 同纪律：绘制/雷达/躲避任一开启即识别）。两套余量体系不叠加：BMR activation 原样传入，MP 侧余量照常吃（结果更保守，可接受）。
- 落地差异（相对上一段设计）：ShapeDistance 具体类的参数全部存在私有字段（Cone/DonutSector/ArcCapsule 只存派生法线），直转经反射配方读取（Cone/DonutSector 由边法线还原朝向与半角，coneFactor<0 分角线反向）；采样产物为 WorldPolygonTemplate（外轮廓用角点网格 marching-squares 边链接，对角捏合拆环、孔洞环随最大环丢弃）；Inverted 族/补集语义自 2026-09-27 起转目标安全区注册（见上），仅 SDInvertedUnionOffset 与采样填充率 >65% 的禁入区仍丢弃。危险区注册仍吃 MP 侧 `DodgeDangerMargin` 外扩（与脚本门面一致），障碍/边界/目标区不外扩；实体不绑定（StaticTransform 快照 + 签名变化才 UpdateArea/重建）。DodgeController 在桥有激活模块时压制通用躲避推断。

## 时间轴（M6）

状态：2026-09-23 已实现实时侧（雷达悬浮窗时间轴条 + 诊断页摘要 + 纯逻辑行模型 `MechanicHost/MechanicTimelineView.cs`，离线回归 `tests/MechanicTimelineViewRegression.cs` 3 场景）；ReplayWindow 预测条已在 M7 接线（回放会话激活且快照非空时显示于泳道区顶部）。

- 数据源：`MechanicTimeline.Capture(module)`（保留分支未定与伤害标记）。控制器每帧在 `session.Update` 后捕获（异常限频隔离），经 `MechanicHostController.CurrentTimeline` / `MechanicClockUtc` 暴露。
- 呈现：雷达悬浮窗雷达内容下方追加时间轴条（当前状态行 + 后续至多 3 条 + 未决分支「分支?」，伤害类型着色 Tankbuster/Raidwide/Knockback，右键菜单与 `ShowModuleTimeline` 设置开关）；MainWindow 诊断页显示激活模块/阶段/状态摘要。ReplayWindow 泳道留待 M7；条目点击不 Seek（预测不是录制内容）。

## 验收（贯穿）

- `tests/MechanicRuntime`：合成帧/事件驱动 Feed → 模块激活 → CastInfo/OpCastEvent/Tether/MapEffect 全链路断言；ZoneModule CFC 切换；对象销毁清理；回放 Seek 重建会话。
- 主回归/脚本冒烟保持全绿。实机项沿用 U05 口径记录，不宣称已实机验收。

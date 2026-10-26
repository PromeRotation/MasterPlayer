# 首批地城：沙斯塔夏、塔姆塔拉墓园、铜铃铜山（普通）

运行库源码版本 `f6914f3e85145b5c0390d2e53376b4ffd957cca6`。本批对应机制运行库模块 `MechanicRuntime/Upstream/Modules/RealmReborn/Dungeon/D01Sastasha`、`D02TamTaraDeepcroft`、`D03Copperbell`；不包含困难版和斗兽棋。本文件是本批实现的规格。

## 身份与边界

| 副本 | CFC（不是区域 ID） | 区域 ID | 主 OID（十六进制／十进制） | NameID（十六进制／十进制） | 模块 |
|---|---:|---:|---|---|---|
| 沙斯塔夏 | 4 | 1036 | 0x19B／411 | 0x4B4／1204 | D011Chopper |
| 沙斯塔夏 | 4 | 1036 | 0x1A1／417 | 0x4B6／1206 | D013DennTheOrcatoothed |
| 塔姆塔拉墓园 | 2 | 1037 | 0x237／567 | 0x1C7／455 | D021VoidSoulcounter（三次遭遇） |
| 塔姆塔拉墓园 | 2 | 1037 | 0x4C／76 | 0x49／73 | D022GalvanthTheDominator |
| 铜铃铜山 | 3 | 1038 | 0x387C／14460 | 0x224／548 | D031Kottos |
| 铜铃铜山 | 3 | 1038 | 0x3870／14448 | 0x22A／554 | D032IchorousIre |
| 铜铃铜山 | 3 | 1038 | 0x38C9／14537 | 0x65／101 | D033Gyges |

区域 ID 来源：2026-09-21 XIVAPI 表版本 `541c0c12e07da325` 的 ContentFinderCondition→TerritoryType 正反查询。旧区域 157／164／161 已为空行；当前运行客户端的表仍待实机确认。

- [CFC 正向查询](https://v2.xivapi.com/api/sheet/ContentFinderCondition?rows=2,3,4&fields=Name,TerritoryType.Bg)
- [区域反向查询](https://v2.xivapi.com/api/sheet/TerritoryType?rows=157,164,161,1036,1037,1038&fields=Name,PlaceName.Name,ContentFinderCondition.Name)

首领须存活、可选中且进入战斗才激活组件；优先保持当前实例。主怪死亡、销毁、退出战斗、机制关闭或脚本停止清理所属危险、绘制、边界。不能在进入副本时就把玩家锁到首领场地。相同 OID 的下一次遭遇按新实例重新选择场地。

## 18 个组件逐项对照

所有 SimpleAOEs 保存 `spell.LocXZ` 和 `spell.Rotation`，包含自身型圆/锥，均不跟随施法者。圆和月环不依赖方向；锥必须有锁定施法朝向，缺失记录错误并跳过。判定时间为事件 UTC + 剩余读条 + 0.3 秒 NPC 补正，扣除已消耗的派发延迟；不再额外增加斗兽棋专用的 0.35 米或提前 0.25 秒。

| 首领 | 组件 | AID 十六进制／十进制 | 几何／提示 |
|---|---|---|---|
| Chopper | ChargedWhisker | 0x15F／351 | 圆 R6.15 |
| Denn | Hydroball | 0x22C／556 | 扇形 R8，总角 90° |
| Denn | TrashSpawning | EAnim，见下 | 水纹轮廓和 `Adds spawning soon!` |
| VoidSoulcounter | Condemnation | 0x390／912 | 扇形 R7，总角 90°；已包含 hitbox |
| Galvanth | MindBlast | 0x3DB／987 | 圆 R9.95；已包含 hitbox |
| Galvanth | InconspicuousImp | OID 0x7D／125 | 存活时 `Kill the imps!` |
| Kottos | GrandSlam | 0x6F81／28545 | `Tankbuster`，不画 AOE |
| Kottos | LumberingLeap | 0x6F80／28544 | 各 Helper 圆 R12；不是视觉技能 28543 |
| Kottos | ColossalSlam | 0x6F82／28546 | 扇形 R30，总角 60° |
| Kottos | Catapult | 0x6F83／28547 | `Single-target damage`，不画 AOE |
| IchorousIre | Syrup | 0x6F2E／28462 | 固定落点圆 R4 |
| IchorousIre | FluidSpread | 0x6F2D／28461 | `Tankbuster`，不画 AOE |
| IchorousIre | Divide | 0x6F31／28465 | IchorousDrip 圆 R8；实际监听 Burst，不是 Divide 视觉 |
| Gyges | GiganticSwing | 0x705A／28762 | 月环 4–40 |
| Gyges | GiganticSmash | 0x7058／28760 | 固定落点圆 R10 |
| Gyges | GiganticBlast | 0x7059／28761 | 各 Helper 圆 R8 |
| Gyges | GrandSlam | 0x705C／28764 | `Tankbuster`，不画 AOE |
| Gyges | ColossalSlam | 0x705B／28763 | 扇形 R40，总角 60° |

运行库 `Components/GenericAOEs.cs:120` 在开始时登记，`:130` 在正常结束／取消／销毁补发 CastFinished 时移除；效果包只累加计数。本批无依赖计数的阶段。MP 每0.1秒扫描，没有独立 PR 正常结束回调；无施法时保留0.2秒启动保护及判定前0.5秒内的收尾保护，效果到达解除收尾保护，取消／打断／销毁立即移除。显示兜底至判定后2秒，导航危险窗口只到判定后0.6秒。重复包不得累加 AOE 或推迟截止；效果按 `(source, action, GlobalSequence)` 去重。

取消／打断以事件发生时间记录结束，正常结束与超时清理的截止不晚于原始预计读条结束，不能把处理延迟算进旧读条。启动下一次同技能读条后继续保留旧读条结束记录，阻止迟到旧包和旧扫描覆盖新落点；0.05秒快速重放、读条时长变化、结束后延迟到达的新包均有回归覆盖。

绘制用原始尺寸；用户 `DodgeDangerMargin` 只作用于导航副本，导航副本隐藏显示。普通注册不应用斗兽棋专用余量。所有接管 AID 压制通用躲避，并将 12 个确切 AOE 尺寸加入备用识别表。视觉和单体技能不得推断为空间危险区。

辅助施法者 OID：Kottos／Gyges 的 Helper 为 0x233C（9020），IchorousDrip 为 0x3871（14449）。除表中技能外，额外抑制通用空间预测的 AID 为 VoidSoulcounter 的 0x1AE（430）、Galvanth 的 0x3CB（971）、Kottos 的 0x6F7F（28543）／0x6F85（28549）、IchorousIre 的 0x6F2F（28463）／0x6F30（28464）；这些不计为额外已实现 AOE。

## 自定义与区域组件

- Denn 水纹：OID 0x1E8615–0x1E8618（2000405–2000408）。EAnim 状态为 `(Param1 << 16) | Param2`，位 `0x00040000` 激活，其他状态撤销。中心在物件位置、宽 4／长 6、朝向 0 的矩形轮廓（即从 Z−3 起画长6），不进解算。对象死亡／销毁以及退出首领清理。中途加载缺少历史动画，不能从 byte EventState 猜完整 EAnim。
- Galvanth：任一存活 OID 0x7D（125）触发 `Kill the imps!`；不要求可选中。小怪优先级与文字提示是不同功能，前者缺口单列。
- Sastasha ZoneModule：按第一个可选中染血纸条推断 Blue／Red／Green：OID 0x1E8554（2000212）／0x1E8A8C（2001548）／0x1E8A8D（2001549）。已知颜色且开关 0x1E8558（2000216）可选中后 Resolved。提示原文 `Correct switch: {Unknown|Blue|Red|Green}`，Resolved 后停止；跨区域重建状态。不自动交互，不自动点击开关。
- 运行库没有普通 Madison 的 BossModule；不能把没有的模块宣称为已支持。副本杂兵仅保留宿主原有通用识别。

## 场地

- Chopper 与 Denn：原样保留对应运行库模块的完整 PolygonCustom 顶点；中心取包围盒中心，不采用 Denn 的旧 centroid 注释。
- VoidSoulcounter：X > −10 用 FirstArena；否则 −25 < X < −15 用 SecondArena；其他用 ThirdArena，边界 −10／−15／−25 均按模块定义严格比较。三套完整多边形保留。
- Galvanth：中心 (−52.765, −12.789)，R18。
- Kottos：圆 (43, −89.8)、R14.75，减矩形 (42.9, −105.9)、半宽20／半高2，再减圆 (39.5, −74.4)、R1.5 与 (47.5, −74.4)、R1.8。
- IchorousIre：圆 (27,114)、R19.5，减矩形 (37.5,95)、半宽20／半高2.4／−25°，以及 (6,112)、半宽20／半高1.75／−270°。
- Gyges：中心 (−100,6)，R19.5。

复杂边界使用运行库几何导出的固定多边形；禁止以外接圆替代缺口。导出保留 `ArenaBoundsCustom.Center` 作为几何原点，局部顶点按 MP 坐标约定转换，雷达不会改用多边形重心。持有边界引用，仅当当前边界仍属于自己时清理，保护其他脚本。

## 缺失与未确定项

1. **F01已补首批目标预测**：四个 SingleTargetCast 以 PR 队伍排序映射实际目标槽位，记录目标ID／职能／名字／判定时间／伤害类型并用于提示；自目标施法读取施法者当前目标，缺失队员不猜 MT。本人时间轴职能单独保存，不将其当全队排序。Catapult 继承模块默认的 Tankbuster 预测类别，不将它擅自画成范围技。自动减伤／治疗消费仍属暂缓的 F05。
2. **F02暂缓：AI 目标优先级调度**。Denn 的 Guard=2／Boss=1，以及 Galvanth 的 Imp=1 保留说明与提示；不会持续强制改玩家目标。现有斗兽棋可选转火接口不等价于完整的模块目标优先级调度。
3. **F03暂缓：绘制细节**。双视图服从 MP 用户配色；水纹暂用安全色轮廓表达，不使用模块的 PlayerInteresting 原色。MP 通用敌人标记未按各模块 DrawEnemies 精确筛选。模块文字显示转为节流脚本提示，无场内文字图层。
4. **时序说明**：正常结束来自 0.1 秒对象扫描，事件丢失时额外保留至判定后2秒。导航使用 MP 自有解算器和用户余量。
5. **待实机**：三个区域 ID 在用户客户端表的对应、场地高度、EAnim 实际时序、取消与正常结束的网络顺序、AOE 渲染效果。源码或离线测试不是实战证据。
6. **U03已补新录制**：v2保存完整初始对象状态及变化、已知EAnim和PacketFacing；DungeonScript经ReplayWorld扫描，使用CurrentTime，还原战斗／纸条／水纹。暂停冻结，Seek从初始快照重建。旧v1无法补全；插件加载前未观察到的动画仍未知；缺失锥形朝向时明确记录并跳过，不读实时对象代替。见[录制回放格式](../录制回放格式.md)。
7. **来源成熟度**：Sastasha 两首领为 Contributed，其余五个为 Verified；均保持模块登记值，不将 Contributed 当作实测确认。

## 验收

覆盖 12 个真实 AOE 的形状/落点/锁定朝向/多源并发；4 个单体提示不能产生危险。验证读条重复、缺失朝向、取消、打断、替换、效果重复、死亡、销毁、超时、关机制和停止；验证三个相同 OID 场地及严格端点、差集缺口和轮廓。水纹仅预览，纸条 Unknown→颜色→Resolved。验证双视图原尺寸与导航余量分离、边界所有权、内置发现及外部覆盖。最终编译与回归结果记入 [开发记录](../开发记录.md)，实机验收单独保留。

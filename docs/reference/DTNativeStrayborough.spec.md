# 噩梦乐园迷途鬼区 MP 原生脚本

2026-11-22。本机简中客户端双向确认 `CFC981 → Territory1204 → CFC981`，名称“噩梦乐园迷途鬼区”。入口 `Presets/Dungeons/Entries/StrayboroughDeadwalk.cs`，GUID `masterplayer.dungeon.strayborough`，接管 OID4183、41CA、421F 三模块。生产代码只消费 MP 的对象与事件 DTO，不实例化 BossModule、MechanicSession 或 BMR 组件。

## 场地与显示

- Leonogg 初始圆 R19.5；36529 start 预告 R14–20 环，activation=F+.9；map00/00020001 实际改 R14 并清预告。超时或 Finish 不缩场。
- Jack 的源场地是圆 R19.5 减去南北两条矩形，实际中心(17,-169.825)。离线导出解析后的多边形、轮廓及三角剖分，保留真实边缘。
- Träumerei 初始半宽19.5方场。map0B/00800040 启用十字障碍；当前观看角色有3949时使用完整方场，无3949使用方场减半宽1.5十字。map0B/00080004恢复方场。状态事件立即同步场地，视角切换跟随 MP CurrentPlayer；无需等到绘制才改变导航。
- 四个变身平台的源 Circle 组合是 AOEShapeCustom；离线导出各平台实际 polygon，普通模式显示并规避平台，反转模式显示绿色平台并把平台外登记成隐藏硬危险。不能用软安全目标代替。
- 原始范围经 MP 同一几何进入雷达与 HostDrawingGeometry 世界转换；导航独立隐藏条目不再重复显示。危险余量由既有输出契约处理。

## Leonogg

- 11D1 Timeline且OID4205：原始位置快照R2，Now+3。36532/36536 Finish只删FIFO一项；不从读条创建，Effect、时间经过、无cast销毁不删。
- Effect36533追加caster，重复允许。每帧以缓存对象实时位置/朝向发布前6、全宽2矩形；1E3C删除该actor首项。死亡/销毁不擅自清缓存。
- 39683 start：caster实际位置、锁定施法方向步进4，首At=F+1.6，之后1.5秒，总5圈且全部参与导航。Effect39683/84以caster当前位置逐轴≤1匹配下一爆点；每次推进使用实际事件时间+1.5。只在队列数量/版本变化时重建所有圈缓存；已过期但未结算的时间不逐帧滑动，无匹配effect不刷新。
- 36531是R20总60°扇形，保持添加顺序、只显示前4，真实Finish推进。总队列>2时前2用Danger色，颜色不改变风险。
- Icon197追踪Targets保存actor、靠边mask保存target ID，两者分开；图标预告Now+5.1，mask本人禁入场心R13.5。
- 39686 start选离cast location最近的Targets，首击不移动。基类先创建再减MaxCasts：前两条5次，此后4次，整场计数不复位。首击/后续39686/87 Effect按预测点最近条目推进，不设匹配距离阈值。源/目标/落点取值保持公共组件顺序，下一At=Now+(double)1.6f。
- 追踪绘制R4；本人AI为预测点沿当前面向退4、R7，别人AI为预测点R5；另画上次爆点到目标连线。目标死亡/销毁只在剩余次数小于当前MaxCasts时删除。结束清Targets，不清mask，不重置次数。
- 36529输出真实raidwide预测；视觉动作不伪造普通AOE。

## Jack

- 1909状态Gain且Extra1，任意actor在量化位置生成R8、默认时间。其他Extra不生成；状态Lose、Effect、销毁、超时不消耗。OID41CB的11DD Timeline消费首项。适配器接1909；激活观察已有状态可恢复一次，首次未带状态不会耗尽恢复机会。
- Tether276保存source对象，可重复；地图事件读取当时实时位置，使用精确XZ相等判断。只处理index23/01，重复地图追加预测，不覆盖。
- state02000100、10000800、00400020分别使用源定义的5、3、2种首匹配图案，delay11.5/14.5/19秒，R19；00100001以队列前两个对象当前坐标各生成R19、Now+16。少于两个时不虚构对象或AOE。
- 36720 Finish一次清tea预测和cups。读条和Effect不另外生成/清R19。36725 raidwide、36726单体预测沿公共原生输出。

## Träumerei

- 37139前50/全宽16（源注释40与实现不一致，按实现）；37147前50/全宽12；37340前40/全宽4、前5可见；37133前40/全宽4。全部按source caster Finish消费，无时间或Effect兜底提前清理。
- Ghostduster37146：R8显示、默认不预测伤害；非Ghostly才登记AI，导航R9。本人避队友排除同组件其他分散目标。死亡只隐藏不删除缓存，复活恢复；Finish按target FIFO删除。
- Ghostcrusher37144 Marker Effect生成动态source→target直线分摊，前80/全宽8、Now+5；目标靠队友2码圈，未点名参与一个硬分摊，多个点名互避。37143 Resolve按target消费；单条且实际target可解析时允许修正。Shared预测取活动条目最早时间，源min/max4为尚未实现的元数据，不额外虚构人数判断。
- 拉线只处理57/1，队伍source时反转两端；首次每player记录Now+10，颜色切换复用At。At+1只过期时间缓存，不删bait。Untether删配对；source销毁合成已观察的owner断线，即使尚未有下一张对象快照。默认没有KB免疫动作/位移/伤害预测。
- 拉线始终对本人画好坏线和提示；仅Ghostly启用source中心R20禁区。死source只从活动视图隐藏，target死亡保留，与公共GenericBaitAway一致。
- map0C/00020001永久激活平台，0D–0F不独立激活。平台输出优先级：活动分散→Fleshbuster→活动拉线→默认。分散时ghost进入平台、flesh避开；Fleshbuster或拉线时相反。Fleshbuster37148从start到Finish；不用其视觉全屏范围伪造raidwide预测。
- 提示优先级保留源差异：Fleshbuster/拉线提示变幽灵，否则活动分散提示变肉身，不与平台shape优先级合并。

## 生命周期及控制

临时不可选、脱战不重置已激活的Boss；primary死亡或销毁结束。切人重新求解个人约束，脚本停止按owner清绘制、导航及提示。D08同caster相同action的新读条若已超出修正窗口，先Finish旧条目再建新条目，避免FIFO孤儿。

本批源模块没有护盾禁攻、gaze、强制面向或专门停手，生产不虚构这些限制；通用PR朝向/视线、PR ACR停手/目标禁攻仍由既有原生Hints门面负责。回放只规划绘制，宿主阻止实际执行。

## 验证证据与边界

- `tests/StrayboroughRegression.cs`：**42项通过**。包含精确场地和平台/普通AOE的native及宿主world逐点对照、连续爆炸真实effect推进/非零方向/无匹配不刷新、追踪首两波5后续4、茶杯全部10种地图图案、FIFO/死亡复活/取消/销毁、事件适配器、视角与ghost场地、分摊Shared及硬约束。
- `tests/ScriptCompilation/NativeDungeonReplayStrayborough.cs`：实际生产DLL三Boss合成回放，覆盖Icon→追踪五击、Exaflare、连线地图茶杯、幽灵平台/分散/直线分摊/拉线，前后Seek重建。全地城合成回放链通过。
- 主项目CI构建0错误，91个内置入口独立脚本编译通过。日志`.tmp/halatali-verify/strayborough-{export,regression,build,native-smoke}.log`，修改前备份`.tmp/strayborough-backup/`。
- 这些证据是源码等价性及离线合成验证；未获得本副本ARR完整实录，不代表已完成游戏像素、实际PR ACR/GreenMove执行验收。

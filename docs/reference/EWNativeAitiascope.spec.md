# 星海深幽寻因星晶镜 MP 原生迁移

2026-11-22。本机匹配版本客户端表 CFC 786 → Territory 978，名称“星海深幽寻因星晶镜”。入口 `Presets/Dungeons/Entries/Aitiascope.cs`，GUID `masterplayer.dungeon.aitiascope`，接管 OID 3469／346B／346E。生产状态和绘制位于 `Presets/Dungeons/Aitiascope/`，通过 MP 脚本门面输出，不创建 BMR 模块或会话。

## 机制与源码语义

来源：`MechanicRuntime/Upstream/Modules/Endwalker/Dungeon/D05Aitiascope/` 三个 Boss，以及 `Components/BaitAway.cs`、`LineOfSightAOE.cs`、`GenericAOEs.cs`、`UnavoidableDamage.cs`。

- Livia 使用精确 36 边形及入口差集。AglaeaBite 实际是 R9／120° 扇形；目标身份锁定、源与目标位置动态更新，点名者避开朝向队友的长 100 同角扇形，旁观者避实际 R9。读条结束保留，25673 真实效果消费；死刑预测同样保留。来源消失、死亡或目标死亡清理。
- 25670／25671 都按源实际长 20、宽 6，不采用技能注释中的宽 4 或回返长 40。当前射线非空时全部显示；首段结束 FIFO 消费并记录原来源，最后一次首段结束 +10 秒为回返时刻。只在剩余严格小于 5 秒且射线队列为空时显示动态来源位置／朝向的回返预告；实际回返开始清预告。分摊保留结算硬约束。
- Rhitahtyn 默认 39×39；map 0/20001 改为宽 13、长 39 的中间通道；map 1 或 2/200010 加入对应对角两个 4×3 凹口，并以原事件 +7.3 秒要求进入两个 R5 圈的并集。Safespots 期间盾冲继续显示但不直接施加直线危险，使用凹口约束；1 或 2/80004 恢复盾冲危险，0/80004 还原原场地并清凹口。
- 四种弹片坐标各 18 个，按 346C 的南侧位置及 ±0.1° 朝向、或 346D 的 X 相对 11 判定。源实际 R6、发现时 +8 秒；只有真实 25684 helper 结束按位置误差 1 逐个消费，helper 读条不重复画区域。队列为空时保留源重新扫描 marker 的行为。
- 25686 延迟死刑在开始时锁定目标；自然读条结束仍保留到真实效果，不随首领换目标而改变预测。
- Amon 默认精确 48 边场地及入口差集，map 5/20001 扣除中央带 8.5° 旋转的 20 边 R6.5 圆，5/80004 恢复。CurtainCall 以施法位置和当时全部 3470 冰块构造范围 60 的安全扇影快照；不增加冰块碰撞，影子起点距离为冰块中心距 +0.2。只有地图切入中央孔场地时输出安全显示和危险补集导航；没有冰块不生成整场禁区，晚生成／移动的冰块不自动刷新原快照。
- 25691／25692 合并队列：收到前 7 个保持到达顺序，第 8 个及后续加入时按判定时间排序，仅前 4 个显示和规划；结束按来源消费，后续递补。读条包修正通过当前锁定 cast 反映到输出。
- 普通冰圈、长线、左右半场、R15 近距离危险、全屏与死刑按源迁移。27756 的源实现只有 R6 危险圈，没有单独击退解算；未凭技能注释增加额外移动控制。

三个源没有护盾禁攻、PR 面向／视线禁向或停手请求。这里的“盾冲”是直线 AOE 名称，不代表护盾禁攻；CurtainCall 是空间遮挡视线，不是背对视线。所有已存在的移动约束均进入 MP SafeZone，两种视图消费 MP 几何。

## 验证与边界

在 `.tmp/halatali-verify` 使用 SDK 10.0.303：

```powershell
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=Aitiascope
dotnet build ../../MasterPlayer.csproj -p:CI=true
dotnet run --project ../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- ../.. ../../bin/Debug/MasterPlayer.dll --native-dungeons
```

31 项定向回归通过，包含精确场地及孔洞两视图、16 种技能几何、回返严格时间窗、死刑真实效果、两种分摊和两种凹口的真实解算、四组 18 个源坐标及逐次结算、8→4 连续雷扇排序递补、遮挡地图门控和快照边界。

实际 DLL 合成回放覆盖三 Boss、往返预测替换、引导真实命中、凹口与盾冲危险切换、弹片定位清理、延迟死刑、冰块扇影门控、两波雷扇、Seek 与 Stop。日志为 `.tmp/halatali-verify/aitiascope-regression.log`、`aitiascope-build.log`、`aitiascope-native-smoke.log`。

这些是源码和生产组件的离线验证；ARR 实录校准、游戏内最终绘制像素与实时移动仍待实机确认。

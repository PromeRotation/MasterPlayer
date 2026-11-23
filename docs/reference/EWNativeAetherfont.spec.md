# Aetherfont 原生机制

区域1126「间歇灵泉哈姆岛」，客户端CFC822；入口`Presets/Dungeons/Entries/Aetherfont.cs`，GUID `masterplayer.dungeon.aetherfont`。接管上游D12全部八模块：五组道中和Lyngbakr、Arkas、Octomammoth。生产只使用MP数据、几何和脚本门面。

## 生命周期与场地

五组道中按源码场地中心方形粗筛成员；共享OID依据组位置分开。任一成员开战启动，主怪死亡后选择存活成员，全部死亡或销毁才结束。三王按玩家进入平台启动，不要求Boss可选中或进入战斗。

`AetherfontArenas.cs`由测试工具读取匹配版本运行库静态场地、执行裁剪和hitbox内缩后导出九套轮廓及三角形。Octomammoth保留五平台与四桥并集及0.5码内缩，不允许从水面规划。最终边界中心为(-370,-355.5)，不是原始旋转锚点(-370,-368)。

## 连续机制和绘制

- 晶体33339/33340共享列表，半径8/15。仅追加至第11项时排序。输出从首项开始、结束时间严格早于首项加1秒的连续前缀，遇到第一项超窗即停止。结束或施法辅助体销毁按caster删除，不按预测时钟清除。
- Arkas的map01–04、state00020001或00200010，每事件按15→0逆序追加完整16条裂隙，宽4、长度由起终点计算，事件时钟加6秒。只发布前16条，后续列表保留。33361真实读条结束或读条中辅助体销毁推进FIFO，效果事件不推进。
- 33364开始预告10–15月环，时间为Finish加0.8秒。Map00/00020001换成半径10边界并清预告；00080004恢复原边界但不清预告。
- Icon161使用事件actor建立半径6四人分摊，事件时钟加5.2秒。34712效果按MainTarget删除，找不到时移除首项；33366视觉不清除。Lyngbakr33343由真实读条建立和结束分摊。
- 分散显示半径5/6的量化坐标轮廓，导航增加1码；分摊轮廓根据观看者点名、分散、当前圈内人数和容量选择安全色。靠拢与进入分摊为硬导航。共享预览由雷达和世界绘制消费。

不为普通圆分摊额外制造全队Raidwide预测：33343的上游CastStackSpread默认关闭预测；IconStackSpread在本例全员允许时也不发布整队预测。三王触手可以攻击，Boss保持优先级1。本区域源码没有专属护盾、视线、面向或停手机制。

## 定向验证

`tests/AetherfontRegression.cs`共44项，覆盖源码场地逐点对照、两视图形状/轮廓、八种map图案输入、严格晶体窗口、真实结束、销毁推进、分散和分摊个人输出、分摊容量、真实解算器站位和三王进场条件。

`tests/ScriptCompilation/NativeDungeonReplayAetherfont.cs`通过实际MasterPlayer.dll执行五道中、三王、事件FIFO、Seek重建和Stop清理。CI编译通过。证据在`.tmp/halatali-verify/aetherfont-regression.log`和`lunar-native-smoke.log`；后者同时覆盖最终Aetherfont版本。

以上为源码对照、定向离线和实际DLL合成回放验证。ARR实录时间校准、最终像素效果、PR实机执行仍需游戏内验收，未部署。

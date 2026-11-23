# 奇坦那神影洞 MP 原生迁移

2026-11-22。客户端 Lumina 表实查 CFC651 → Territory823「文明古迹奇坦那神影洞」。入口 `Presets/Dungeons/Entries/QitanaRavel.cs`，GUID `masterplayer.dungeon.qitana`；`QitanaCatalog` 固定接管 OID `2A40/27AF/27B0/27B1`，按区域隔离，脚本关闭也不恢复对应桥。生产机制仅消费 MP 事件和世界快照，不实例化 BMR 模块。

来源为 `MechanicRuntime/Upstream/Modules/Shadowbringers/Dungeon/D03QitanaRavel/` 四个模块，以及 `Components/Tethers.cs` 的 `StretchTetherDuo`。原生实现位于 `Presets/Dungeons/Qitana/`，通过共用 `DungeonOutput` 同时发布原尺寸预览与 NavigationOnly 解算危险，保留一次危险余量。

| 模块 | 原生状态、时序与几何 |
|---|---|
| Ronkan Dreamer 道中 | Z>550选择第一房间，否则第二房间；外轮廓、两处柱洞及源`AdjustForHitboxInwards`解析结果由`--qitana-arenas`导出为`PolygonRegionGeometry`。17387 R6，15923长15宽4，17433单体。37连线按原时间+6秒锁来源量化位置及实际朝向；八个位置映射长12/22，其余长35，宽8。15916/15917/15918任一真实效果清全波，旧快照不重建已消费连线，下一次真实连线可重建。源Helper枚举写2E8、注释写28E8，生产按连线和目标识别，不凭注释过滤OID。 |
| Lozatl | 15498 R5；15503/15504长60宽20锁读条朝向。任意实体动画00040008按原时间+8秒生成中心(0,315)长60**总宽40**的半场；X==10朝+90度，否则−90度。15500/15725真实效果清，旧动画快照不复活。15497延迟死刑保留到实际效果，15499全屏。预热visual不额外注册危险。 |
| Batsquatch | 15509/15510/15511分别R2/R3/R4；1EAACD出生原时间+12秒、原位置朝向预告R15总角30度。15512以实际caster位置匹配并移除初步预测，使用包内Location/Rotation/FinishAt替换；恢复读条前先恢复柱子，避免重复。任一自然15512完成清整组，取消只清本来源，消失清自身。15505延迟死刑；15506全屏及落柱，15507保留源“连续11次全屏”提示，不捏造逐击间隔。 |
| Eros | 场地29×39矩形中心(17,-538)。15518 R6读条→真实效果+.8秒预测→1E972C两码内实体交接，EventState7消失。15517 R6动态诱导圈，本人避队友并退出中央宽26长40矩形，其他人避目标；15523 R5分散；15525 R6四人分摊用危险补集强制进入；15522/15526/15527 R60总角60度。57/1连线识别两种端点方向，按每个玩家首次连线原时间+5.2秒离来源至少19码；好坏切换不重置期限，取消/效果清，切视角重算个人约束。源实例未启用抗击退免疫连线，不因免疫忽略拉线。 |
| Eros位移 | 17168按中心向内最多50码；15520按读条包方向向前35码并止于矩形墙，16923只作visual。双视图绘制观看者落点与连线，以全部已出现及预测毒池复核落点，并保留源毒池通道禁入（拉近：Boss当前朝向长40宽12；击退：+Z前后40宽12）。源循环误用aoes[0]，MP按所有毒池输出。免疫必须覆盖判定时刻，过早结束的免疫不跳过位移。 |

源中没有额外护盾禁攻或视线。移动约束交给同一MP解算器；执行沿用总开关、owner和回放门控。

验证从 `.tmp/halatali-verify` 使用临时SDK10.0.303选择文件执行，未修改根global.json：

```powershell
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=Qitana --keep-going
dotnet build ../../MasterPlayer.csproj -p:CI=true
dotnet run --project ../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- ../.. ../../bin/Debug/MasterPlayer.dll --native-dungeons
```

34项回归全部通过：四个源场地逐点及双视图转换、12种普通形状、五种石像位置、两向半场、落柱交接与清理、双端点拉线及免疫语义、持续毒池、动态诱导、强制分摊与分散、多毒池通道、位移落点、免疫到期、延迟死刑。真实解算覆盖十九码拉线、场边诱导、强制分摊、击退后安全落点。实际DLL回放覆盖两道中房间与三Boss、重叠机制、Seek和停止；60个入口独立编译及此前原生地城回放通过。日志为验证目录下`qitana-{territory,export,regression,build,native-smoke}.log`。

本轮没有ARR实录校准或游戏内验收；预测延迟来自源组件。合成回放、几何转换及真实解算不等于游戏内最终像素、移动闭环和宿主控制验收。

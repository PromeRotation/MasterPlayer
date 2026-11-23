# ARR 水晶塔原生机制

本文记录已从 BMR 转入 MP 的已启用组件及验证边界。原生入口通过 `DungeonScript`、`DungeonEncounter`、`DungeonOutput` 工作，不创建 BMR 模块或会话。上游 WIP 中只有枚举、注释而未启用的机制不计为迁移覆盖。

## 古代人迷宫

入口 `Presets/Raids/Entries/LabyrinthOfTheAncients.cs`，GUID `masterplayer.alliance.labyrinth`。本机 CN 客户端数据双向确认 CFC 92 ↔ Territory 174。

- 骨龙 0x92B：749 R6，750 R105 总角度120°，970 单体预测，1828 R7.8 总角度90°。EvilEye 是扇形，不施加面向限制。场地由上游真实 circle/rectangle union 静态导出，保留高度。首领死亡、HP0、脱战、不可选中及复活不结束阶段，实际移除才结束。
- 塔纳托斯 0x92E：758/760 R6、1829 R5、1759 R6.8，场地 (440,280) R30。
- 贝希摩斯 0x932：1756 黄道陨石冻结读条开始时存活彗星 0x936 的位置和 hitbox、最早结束施法者的落点；距离+0.2 为阴影内半径，最大60码，彗星可穿行。多施法者按源 FIFO 只显示首个安全区，真正 Finish/施法者移除后出队；提示只在当前玩家未进入阴影时出现。1789 R8.4。1752 效果+0.1秒预测 R6 漩涡，0x98C 非 State7 活体按位置逐个消费预测，无虚构超时。
- 弗莱格桑 0x938：1741–1744 的 R3/4/5/6 独立记录来源与时钟；1780 R15 总120°，1736 R80 总45°，1730 R35；场地 (-110,180) R35。

普通三首领死亡/HP0/移除结束；个人死亡不清整场。Director wipe 清理并阻止自动重开，recommence 恢复。读条范围直到实际 Finish，不以名义结束时刻删除。危险区进入 MP 解算，预览同时供雷达与游戏世界绘制；遮挡安全区只显示、对应反集用于解算。

源中未启用的塔纳托斯状态处理、踏板和其他未实现策略不计覆盖。源未实现护盾禁攻、停手或面向控制，不根据技能名称补造行为。

## 希尔科斯塔

入口 `Presets/Raids/Entries/SyrcusTower.cs`，GUID `masterplayer.alliance.syrcus`。本机 CN 客户端数据双向确认 CFC 102 ↔ Territory 372。四模块都对应上游 TrivialPhase，普通死亡/HP0/移除完成。

- 斯库拉 0xBC1：2321 R6.75；2330 前61码全宽4；2331 环形6–12；2317 全队伤害预告。场地 (0,-192) R35。
- 格拉西亚拉波斯 0xBD7：2348 R8；2353 R30 总45°，以组件实际几何为准。场地 (0,-200) R35。
- 亚蒙 0xBE0：2382 R10；1875 R6 总45°；2384/2383 R6 独立读条；2441 谢幕使用与源相同的冰块 0xBE1 存活过滤、最早施法落点、起读冻结快照、距离+0.2、最大60码和 FIFO 语义。冰块可穿行，移动或死亡不改变已生成阴影；提示只在阴影外出现。场地 (0,-200) R30。
- 赞德 0xBDB：2355 R10，2357/2356 R8，2358 前60码全宽10；2359/2361 按实际读条预告全队伤害，不为只有效果的事件虚构读条。场地 (-400,-200) R35。

所有辅助体来源独立，范围锁定读条落点与朝向；包校正不逐帧随实体转动。取消/Finish/移除按来源清理，个人死亡不清场，wipe/recommence 与古代人一致。源码中注释掉的分摊、未启用的球连线、变蛙、冰冻状态和踏板不计覆盖。敌人实体由现有雷达对象层绘制。

## 暗之世界

入口 `Presets/Raids/Entries/WorldOfDarkness.cs`，GUID `masterplayer.alliance.darkness`。本机 CN 客户端数据双向确认 CFC 111 ↔ Territory 151。

- 安哥拉曼纽 0xE00：3272 全屏预告、3275/3277 R10；3281 凝视冻结读条落点，默认有效范围依照组件为10000码，结束前0.75秒由 MP `GazeFacing` 合并禁向并经 `Hints.SetFacing` 续期 PR 请求。眼睛/瞳孔/当前玩家朝向弧供雷达和游戏世界绘制，提示与颜色按当前视角角色朝向更新。场地 (-145,300) R30。
- 五头龙 0xDFC：3295 R30 总120°；3285/3284/3288/3286 R6 独立来源；3289/3294 全屏预测。场地 (200,180) R30。
- 刻耳柏洛斯 0xDF6：3246 R19 总90°、3241 R8、3249 R9、3250 前35全宽6、3251 前45全宽6；3253/3247 根据起读施法者坐标到读条落点生成全宽4/14的冲锋，不依赖包朝向、不在后续帧跟随移动。3254 全屏预告。场地 (0,-197) 全宽40、全长80。
- 暗黑之云 0xCFA：3297 前74全宽24，3302 全屏预告；追踪仅对197点名列表中的最近目标起序列，无点名不回退任意玩家。3298 首击锁定落点与读条结束，3299 后续每击向目标最多追近4码，每次效果+1.5秒预告，共5击，采用源组件R10（枚举注释R8不是实际组件配置）。事件以离预测点最近的序列推进，目标=施法者时量化施法位置，否则优先目标位置、再用效果位置。自点名避让R14且中心沿角色反朝向偏移10码，其他玩家避R11；公开预览R10并显示追踪方向线。首击前保留已死目标，首击后目标死亡/销毁清后续；源不以取消首读条或名义超时清序列。场地 (-300,-400) R30。

四模块均为源 TrivialPhase，个人死亡不清整场，首领死亡/HP0/移除结束，Director wipe/recommence 门控重开。源未启用死亡宣告解法、双色烙印、五头龙热病停手、犬腹/锁链策略及护盾状态处理，不能把枚举存在算作功能。已有通用 PR ACR 停手与目标禁攻通道不变；本批没有凭枚举创建额外限制。

## 定向验证

在 `.tmp/halatali-verify` 使用已安装的兼容 SDK：

```powershell
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=Labyrinth
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=Syrcus
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=Darkness
dotnet build ../../MasterPlayer.csproj -p:CI=true --no-restore
dotnet run --project ../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- ../.. ../../bin/Debug/MasterPlayer.dll
```

- `LabyrinthRegression` 24组；筛选命令另命中4组已有 TenderValley 测试，总28组通过。包含源几何逐点对照、骨龙复活与 BattleEnded 保留、独立辅助体、冻结彗星阴影、漩涡预测/State7/逐个匹配、真实安全区路径和 owner 清理。
- `SyrcusRegression` 19组通过：13种范围源几何逐点对照、区域/生命周期、独立杖时序、3种全屏目标及精确时钟、冰块冻结、遮挡提示、FIFO 最早施法者、实际路径与清理。
- `DarknessRegression` 23组通过：13种源几何逐点对照、动态冲锋、全屏预测、凝视眼睛/禁向/有效距离/取消、多目标五连时序/量化/点名/切人/死亡与真实避让路径。
- 实际插件 DLL 全量冒烟通过，134个内置入口独立编译、发现与覆盖验证通过。三场联盟各四首领合成回放验证独立结束顺序、个人死亡、反向 Seek 和 owner 清理；贝希摩斯验证复合漩涡/遮挡，亚蒙验证谢幕与雷电并发，暗黑之云验证五次效果逐击推进，凝视验证回放不发布实际 PR 控制。
- 日志：`.tmp/halatali-verify/labyrinth-regression.log`、`syrcus-regression.log`、`syrcus-smoke-full.log`、`darkness-regression.log`、`darkness-build.log`、`darkness-smoke-full.log`。主构建0错误，4条既有斗兽警告。

以上是源组件对照、离线模拟与合成回放结果；没有替代 ARR 实录与实机绘制/移动验收。

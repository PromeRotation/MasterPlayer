# ARR 主线单人战 MP 原生规格

实现位于 `Presets/Quests/ArrMainStory/`，入口位于 `Presets/Quests/Entries/`。直接继承 MP `DungeonScript`，使用 MP 对象/事件、机制状态、安全区和绘制门面；生产实现不创建 BMR 模块或会话。

## 区域与生命周期

本机 CN 客户端 QuestParams 与 CFC/Territory 数据交叉核对：

| Quest | 单人战 | Territory | CFC | 主 OID | 场地 |
|---|---|---:|---:|---|---|
| 70057 | 贤者行军行动／里塔提恩 | 1049 | 865 | 38F5 | (-689,-815)，R14.5 |
| 70058 | 超越幻想，究极神兵／拉哈布雷亚 | 1052 | 867 | 3933 | (-704,480)，R20→R15 |
| 70127 | 皇都伊修加德保卫战／维沙普 | 1068 | 885 | 3A5F | X±22，Z±40，六阶段滚动 |

不能用 Quest ID/CFC ID 代替 Territory；1053 是过场地图，不加载拉哈布雷亚脚本。实际数据查询证据为 `.tmp/halatali-verify/arr-quests-client.log`。

主对象战斗中启用；维沙普开始时不可选中，也必须启用。个人死亡保留机制世界，主对象死亡/HP0、wipe 清理战斗；wipe 后不在旧战斗标记下立即重拉，recommence 后恢复。关闭、停用、Seek 重建及停止回放均清理本 owner。

## 里塔提恩

源：`MechanicRuntime/Upstream/Modules/RealmReborn/Quest/MSQ/OperationArchon.cs`，全部已启用组件。

- 28871/28865 R7；28869 R11；28870/28874 前30、全宽5；28877 R14；28876 前30、全宽30。各辅助体独立锁定落点、朝向、读条时钟，观察实际结束/取消清除，不按名义超时删除。
- 状态680低四位为方向格挡：前1、后2、左4、右8。按当前视角玩家相对首领的位置和首领实时朝向判定禁攻，经 `DungeonScript` → `Hints.SetActionRestrictions(forbiddenTargets: ...)` 交给 PR ACR，并停止禁攻目标的原生自动攻击。治疗/其他目标攻击不受全局停手。
- 绘制各格挡方向的弧线；仅当前目标为该首领且角色在格挡侧时，将被格挡方向登记为导航禁区，使 MP 移到未格挡侧。源用 MaxValue 表示持续格挡；MP 按状态当前有效性即时注册，避免把持续禁区误解释为遥远将来的单次伤害。
- 两种帝国援军保留显示；源 AddsMulti 默认 priority=0，没有新增选敌优先级。

## 拉哈布雷亚

源：同目录 `TheUltimateWeapon.cs`，全部已启用组件。

- 29327 全屏预告；首次缩圈预告为实际读条结束+.7秒，R15–20环。只有 Map index0/state00020001 正式缩至R15；读条结束不缩圈，Map 后不重置既有环形预告。
- 29329 R1；29331/29762 前60、全宽8；29340 单体预告；29335 R6；29760 R40总60°；29337 R40总150°。
- AncientCross29756 最多显示/导航前8个，保留隐藏的后续读条，前方结束后顺次进入显示。
- FiresphereSummon29332 观察到实际结束（含源 OnCastFinished 的取消路径）后，以观测时刻+12秒预告全部3934火球；Burst29333起读用实际结束时刻覆盖预测。严格只在预测时间大于当前时间时输出，不按效果包或火球死亡提前删除；位置逐帧量化。
- SeaOfPitch1EB738 非State7时R4，State7撤销。
- BurstFlare29758径向击退10码；仍有火球预告时不添加靠中导航，火球过期后仅限制中心R5内。落点箭头不做停墙截断；源未指定stopAtWall。

## 维沙普

源：同目录 `TheStepsOfFaith.cs`，全部已启用组件。仅源枚举而未激活/监听的动作不计迁移覆盖。

- ScrollingBounds 固定宽44长80，以当前视角玩家Z夹在合法区间内为中心。phase1 Z范围120–300；Map00020001 index3→phase2 120–200、index4→phase4 -40–40、index6→phase6 -200–-120；Map00800040 index0→phase3 -40–200、index1→phase5 -200–40。
- phase1玩家未战斗且无1268时引导向南开怪；phase3玩家Z>25、phase5玩家Z>-135时引导进入下一区域。源SDRect参数顺序为front/back/halfWidth：第一种38/22/40，后续40/22/38，不能误套场地半宽/半高顺序。边界中心未变时保留同一几何实例。
- RipperClaw31262 R9总90°；EarthshakerAOE30880/EarthrisingAOE26410 R31；Earthshaker30887 R80总30°，FIFO最多2，后续隐藏读条保留；SidewiseSlice30879 R50总120°。
- FireballSpread30875活目标R6分散；30894 R6；30883 R10。分散按个人视角决定远离目标还是远离其他队员。
- Earthrising30888从施法者当前位置建立5击地火列，沿-Z每步7.5码，间隔1秒，同时显示2个R8圈。30888/26412效果包按施法者位置与下一位置X/Z各≤1匹配一列；实际效果时刻+1推进，不能用效果落点、读条结束、取消或普通超时替代效果计数。
- FlameBreath30185冻结主对象位置/朝向，前500、全宽20，激活为读条结束+1秒；30884满35次清空且计数归零。该动作起读不归零35计数。
- FlameBreath2 26411起读归零独立计数，锁读条落点/朝向、前60全宽20；30884满14次清空。两套计数同时消费效果包，读条结束不删除持续区域。
- Cauterize30885起读指定主对象，任一对应读条实际结束清除。场地中心Z>218时以场地中心前40/后38/半宽22输出；否则以主对象实时位置、固定π、前160半宽22，采用主对象当前读条结束时刻。
- ScorchingBreath29789一旦起读持续输出主对象实时位置/朝向、前100后100半宽10；使用主对象当前读条结束时刻。源没有专门结束条件，保留至战斗清理。
- Levinshower30892仅3AB3/3AAB/3AB6/3AC2/3AB8，R6总120°，按实时目标瞄准；主目标规划避免面向队友，其他人规划避顺劈，双视图显示轮廓。
- BodySlam26401沿锁定朝向击退20；Touchdown26408径向击退10；均停墙并扣除玩家hitbox相对.499的余量，免疫有效至激活时刻则不画移动。径向原点无方向时不产生虚构路径。源无额外AI禁区，复用已有 MP 范围避让；主对象不可选中时保留绘制，所有源列出的龙群显示。

## 验证与边界

从 `.tmp/halatali-verify/` 运行，使用已安装SDK，不修改根global.json：

```text
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=ArrQuest
dotnet build ../../MasterPlayer.csproj -p:CI=true
dotnet run --project ../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- ../.. ../../bin/Debug/MasterPlayer.dll
```

- `arr-quest-regression.log`：34组通过，含独立源几何密集采样、全部格挡掩码、真实SafeZoneSolver绕格挡/向南移动、六阶段边界、两列地火、14/35计数、分散、顺劈、停墙/免疫。
- `arr-quest-build.log`：0错误，4条既有斗兽警告。
- `arr-quest-smoke-full.log`：137内置入口完整冒烟通过，含三场实际DLL合成回放、独立辅助体结束顺序、个人死亡、状态禁攻、回放PR控制隔离、缩圈和火球严格时钟、维沙普连续机制、反向Seek和owner清理。

AOE统一经MP安全区与`Draw.AddAoePreview`发布；雷达与世界绘制共用几何，但显示开关独立于导航。面向/停手的公共门面遵循现有实时续期和回放不执行契约。这三场源没有凝视或热病式全停手组件，不新增对应机制。

这些结果是离线定向回归与实际DLL合成回放，尚不等于ARR实录校准或游戏内验收。实机仍需确认包到达时序、世界绘制观感、PR ACR实际禁攻，以及维沙普转场中的滚动导航。

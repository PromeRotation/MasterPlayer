# BMR 至 MP 原生脚本迁移交接

交接日期：2026年11月25日，Asia/Shanghai。

原会话：[检查并逐步迁移BMR桥副本至MP脚本](codex://threads/01a1ff36-4a1e-7810-80be-7c8ebc4e9717)。

工作目录：`C:\Users\xiaos\Documents\GitHub\MasterPlayer`。下文仓库内路径均相对于该目录。当前分支为 `main`，HEAD 为 `be2cce0`；大量迁移实现、测试和规格尚未提交，必须接着现有工作区继续。

**当前正式审计为712个源类型离线已验、501个待迁、1个待补齐。最近中断于M4普通的首轮模型验证：56项通过、1项失败。正统优雷卡与朝圣路的楼层机制已有实现和通过日志，但各10个Zone仍未登记完成，应先收尾复核，再更新审计。**

这是交接快照，不代替后续仓库状态。接手时先核对当前文件和任务状态；本次交接只新增文档与提示词，未修复迁移代码，也未重新构建生产项目。

## 一 目标与持续执行要求

将当前通过BMR机制运行库及其宿主桥提供的机制，逐项迁入MP原生脚本。MP应持有机制状态、连续判定、雷达与游戏世界绘制、面向与视线、PR ACR动作限制和机制移动约束。

最终脚本继承 `MasterPlayerScript`，通过MP公共门面输出；不得在脚本内新建 `MechanicSession`、包装 `BossModule` 或继续转发旧桥输出来冒充原生迁移。BMR源码和源运行库可以继续作为语义对照与离线验证依据。

接手agent应按批连续实施，完成一批就转入下一批。只有讨论、列计划或报告某一批通过，均不等于完成全量迁移。单项受客观条件阻塞时，记录该分支并推进其他独立内容；不得通过改统计口径或虚报验证来消除阻塞。

## 二 必须保留的范围决定

| 事项 | 已确定的边界 |
|---|---|
| 深宫 | 只迁机制，包括Boss及道中敌人的AOE、面向/视线、停手禁攻和机制所需站位。禁止探索、开箱、机关交互、自动前进、道具使用及层间推进等流程自动化 |
| 已移除的深宫流程 | 当前死宫规格明确记载流程及相关设置已经移除；旧Floor、AutoPoms和开箱测试的历史段落不构成恢复授权 |
| 混合职责Zone | 分开审查机制和流程，只迁授权机制。Boss迁完或楼层桥会话共存不能证明Zone机制完整 |
| 统一寻路 | 传送与固定跳点试点另行推进，不是本任务前置条件，不扩成寻路重构 |
| 特殊位移 | 保留来源、代际、参数、锁定规则、判定钟、免疫及结束条件；按源与既有MP实际消费范围适配。仅显示的位移不扩成自动执行 |
| 禁DoT | 执行继续暂缓，保留意图/诊断，不扩大成整目标禁攻或全停手，不恢复已撤回PR接口 |
| PR宿主 | 不擅自修改PR，不通过更换门面绕过已确认的宿主限制 |
| PvP | 保留 `Svc.ClientState.IsPvP` 全插件门控，不为迁移清单中的PVP类型绕过门控 |
| ARR与MP回放 | 只规划、绘制和验证请求隔离，不发送GreenMove、PR面向/ACR、遥控或原生动作 |
| 清退桥 | 未迁内容仍需桥；只有相应消费者全部验收并确认无依赖后，才清退对应加载、桥、设置与打包 |

优先依据 `AGENTS.md`、`docs/reference/BMRToMPScripts.spec.md` §3.1至§5，以及用户后续明确指令。历史段落与上述较新决定冲突时，不沿用旧行为。

## 三 当前正式审计

2026年11月25日本次交接实际执行：

```text
rtk proxy python tools/bmr_migration_audit.py --check

States: {'bmr_pending': 501, 'mp_native_offline_verified': 712,
         'excluded_existing_mp': 45, 'mp_script_needs_parity_review': 1}
Included: {'BossModule': 1060, 'ZoneModule': 154}
Metadata groups: 525 (not a verified unique-duty/territory count)
Reports verified.
```

命令退出码为0。审计证明生成清单一致，不证明所有机制和宿主执行已通过。

| 状态 | 源类型数 | 含义 |
|---|---:|---|
| `mp_native_offline_verified` | 712 | 已登记原生实现及相应离线证据，真实执行和最终像素仍按各规格待验 |
| `bmr_pending` | 501 | 包含尚未实现、已有实现未闭合，以及已有通过日志但尚未完成复核登记的项目 |
| `mp_script_needs_parity_review` | 1 | 死宫DD190灰炸弹的精确禁攻宿主执行缺口 |
| `excluded_existing_mp` | 45 | 原有MP斗兽奇弈，未导入BMR，不计入1214个迁移目标 |

目标集合为1060个BossModule及154个ZoneModule，共1214个类型。525是元数据分组数。类型数、组件数、脚本入口数和副本数必须分别报告。

当前源码 `tests/ScriptCompilation/ScriptCatalogRegression.cs` 的 `BUILT_IN_COUNT` 已改为272；正式规格最近登记的发现数为269，后续增加了两个楼层入口及M4入口。272是当前源码预期，不能直接表述为272个入口均已用最新生产DLL验证。

501个待迁类型按原清单分类如下；分类沿用源元数据，不能直接当游戏版本或副本分类：

| 分类 | 待迁类型数 |
|---|---:|
| Foray | 140 |
| Quest | 124 |
| Alliance | 76 |
| VariantCriterion | 64 |
| Savage | 28 |
| DeepDungeon | 21 |
| Extreme | 19 |
| Raid | 14 |
| Ultimate | 8 |
| Unreal | 4 |
| Chaotic | 1 |
| GoldSaucer | 1 |
| PVP | 1 |

DeepDungeon的21项包括正统优雷卡10个Zone、朝圣路10个Zone，以及Quantum深想战1个类型。M4普通当前仍为 `bmr_pending`，尚未填写审计入口，尽管生产源码已经接线。

## 四 已完成批次与有效证据

所有下列日志均在 `.tmp/halatali-verify/`。表中结果来自现有规格及实际日志；每份日志只证明对应时点和产物，不能用于证明随后新增的M4代码。

| 内容 | 当前状态与证据 |
|---|---|
| 正统优雷卡10至99层十个Boss | 已登记。80/90/99层最终为87项模型、32条实际DLL专项、925条全目录检查通过；前缀 `native-eureka809099-`。EO100是楼层Zone，没有独立100层Boss |
| 凯夫卡O8N | 已登记。14个激活组件，Territory751、CFC289、OID0x2153；最终边界批次 `native-palace-boundaries-20261125-model-03.log` 106项相关模型、`build-02.log` 生产0错4既有警告、`catalog-01.log` 949条全目录检查。见 `NativeKefkaNormal.spec.md` |
| 死宫20个Zone授权机制 | 已登记。前15个由 `Palace1To150`、后5个由 `Palace151To200` 接管；独立Boss保留各入口。前缀 `native-palace-earlyzones-20261125-`，77项模型、29条相关专项、18条Palace检查、956条全目录检查通过。DD190执行缺口另列review |
| 极骑神Thordan | 已登记。Territory448、CFC91、OID0x1453；34组件、3个phase及335状态。前缀 `native-thordan-20261125-`，`model-05` 36项、`replay-07` 11条、`catalog-03` 974条通过；`build-02` 生产0错4既有警告 |
| 天之御柱10个Zone授权机制 | 已登记。前缀 `native-hoh-zones-20261125-`，38项相关模型、16条楼层专项、988条全目录通过；最后对应 `model-02`、`build-02`、`replay-floor-06`、`catalog-01` |
| 正统优雷卡和朝圣路楼层机制 | 实现与集中验证已推进，审计仍pending。最新 `native-deepzones-20261125-build-03.log` 生产0错4既有警告；`replay-05.log` 50条PASS/0FAIL、`catalog-05.log` 1013条PASS/0FAIL、`model-05.log` 29项PASS/0FAIL |

`native-deepzones-20261125-model-04.log` 曾包含EO LOS生命周期失败；该项在 `model-05` 及随后M4集中模型日志中均已通过。不要把旧失败继续当作未修，也不要把 `model-05` 的29项扩大为覆盖所有Palace及公共控制模型。

当前磁盘 `bin/Debug/MasterPlayer.dll` 在交接时的SHA256：

```text
0ea449ad7445b5ade18db50d4c62ce9d1a2d3e8ea14a7ccedf3920b281206344
```

这是磁盘产物快照。M4生产接线发生在最近楼层构建之后，不能用这份旧生产DLL或1013条目录记录宣称M4生产集成已验。接手修完后需集中重编，并让专项、全目录和产物校验对应同一份DLL。

## 五 立即续作的位置

### M4普通首轮失败

对象：`BossMod.Dawntrail.Raid.M04NWickedThunder.M04NWickedThunder`。

当前源码入口为 `Presets/Dungeons/Entries/WickedThunderNormal.cs`，GUID为 `masterplayer.raid.wickedthunder.normal`。生产Catalog记录Territory1231、Primary OID0x4263；接手仍须保留客户端表与源码核对证据，不能只据Catalog常量宣称完成核表。

已新增或接线的主要文件：

- `Presets/Raids/WickedThunder/` 下六个文件：`DungeonWickedThunder.cs`、`WickedThunderActor.cs`、`WickedThunderAoe.cs`、`WickedThunderCatalog.cs`、`WickedThunderEncounter.cs`、`WickedThunderEncounter.Output.cs`。
- `Presets/Dungeons/ArrDungeonCatalog.cs`、`DungeonEncounter.cs`、`DungeonAuxiliaryMechanics.cs`、`DungeonEventAdapter.cs`、`DungeonScript.cs`、`DungeonOutput.cs`。
- `tests/WickedThunderRegression.cs`、`tests/Program.cs`、`tests/ScriptCompilation/ScriptCatalogRegression.cs`。

首次集中命令已在 `.tmp/halatali-verify/` 执行，参数见第八节。结果保存在：

```text
native-wickedthunder-20261125-model-01.log
PASS: 56 regression scenarios
FAILED: 1 regression scenarios
FAIL WickedThunder native damage dynamic raw NPC clock no invented stack spread predictions
InvalidOperationException: Dynamic capped NPC clock.
```

15项M4场景中14项通过，失败位于 `tests/WickedThunderRegression.cs` 的 `Damage()`，交接时第276行。场景先建立分摊与分散，确认不产生额外伤害预测，再开启37575、令一名队员死亡并推进20秒，要求7个存活成员的预测时刻等于当前钟加 `.3f`。

相关生产位置：

- `WickedThunderEncounter.Output.cs` 的 `Damage()`，为37575调用 `Activation(cast.Source)`。
- `WickedThunderEncounter.cs` 的 `Activation()`、`Total()`、`Advance()`，交接时第278至286行。
- 源 `ActorCastInfo.NPCRemainingTime` 和相关伤害预测组件；可参考已验证Kefka/Thordan的处理，但必须以匹配版本源行为为准。

日志只给出失败断言，尚未证明是生产算法、浮点运算顺序还是夹具预期错误。先输出并核对实际人数、逐项时刻及源运算，再决定修复；不能直接加宽容差或降低断言。

M4全部13个激活组件仍需独立复核，尤其注意：

- 普通SimpleAOEs只消费各自WatchedAction的Finish，不能套用其他副本“任意Finish”或Count≥99排序。
- SidewiseSpark返回副本，WickedCannon和WitchHunt会修改原列表颜色；前缀Risky、隐藏尾项和晋升语义不同。
- 任意Spell Start对炮延迟表的重置，真实状态槽及冷激活Cast/Status交错顺序。
- 五次和一次分摊组件的独立列表/计数，重复Source图标、目标转移及旧Actor引用。
- Jolt诱导的动态原目标、Finish保留与实际Effect消费，分散R6显示/R7导航。
- 场地Map、十次Hyper效果及Stampede独立时钟/结束。

中断前已发起只读审查，但尚未收齐最终结论；不要假定旧子代理会自动继续。当前没有M4实际DLL回放runner，也没有对应M4机制规格文件，应在模型修复后补齐真实生产接入验证与来源记录。

备份位于 `.tmp/wickedthunder-20261125-backup/`。`.tmp/wire_wickedthunder_20261125.py` 是已执行过的接线脚本，不保证幂等，不应直接重跑。

### 正统优雷卡及朝圣路楼层收尾

主要实现与输入适配：

- `Presets/DeepDungeon/` 公共楼层脚本、机制和完整Effect输入。
- `Presets/EurekaOrthos/Floor/`，含 `EurekaFloorScript` 和AutoLOS位图资源。
- `Presets/PilgrimsTraverse/Floor/`、`Presets/PilgrimsTraverse/Entries/PilgrimsFloors.cs`。
- `Presets/HeavenOnHigh/Floor/`，公共修改后继续覆盖已登记的十个Zone。
- `tests/EurekaFloorRegression.cs`、`tests/PilgrimsFloorRegression.cs`、`tests/HeavenOnHighFloorRegression.cs`。
- `tests/ScriptCompilation/NativeDungeonReplayEurekaFloors.cs`、`NativeDungeonReplayPilgrimsFloors.cs`、`NativeDungeonReplayHeavenOnHighFloors.cs`。

最新50条专项已覆盖各十Zone、Boss交接、状态槽、完整Effect与legacy配对、切人/Seek/owner、EO位图遮挡及PT龟怪真实解算。EO LOS有24张源图、331688个像素内部分类的源oracle；PT扇区有3721点源Distance对照。

尚须完成的是收齐独立审查，确认禁桥之前无遗漏授权机制，特别是PT通用躲避与楼层输出的实时联合接入。修正后的双脚本断言已通过，仍需确认该断言确实覆盖生产门控，而非只证明脚本数量。

PT源没有墙/陷阱坐标表，生产显式使用空表，不能回落到死宫默认表。EO位图通过 `MasterPlayer.csproj` 以 `MasterPlayer.EurekaLOS.*` 嵌入，交接或打包必须包含资源。补齐现有相关规格、ownership和审计生成器后，才登记对应20个Zone；不要因为日志通过就无审查批量改状态。

## 六 两项不能被通过计数掩盖的缺口

### Quantum深想战

类型 `BossMod.Dawntrail.Quantum.Q1FinalVerse.Q1FinalVerse` 仍为pending。入口已存在：`Presets/Dungeons/Entries/QuantumFinalVerse.cs`。Territory1311、CFC1063、Primary0x48EE；九个实际机制组件已实现可定义分支。

缺口是冷激活缺少前置方向Effect：源保留零方向，RayAABB返回float.MaxValue，unchecked转换可能产生异常巨量预告。MP保留原线、剩余次数和判定钟并给诊断，不物化数亿个AOE，也不猜方向。需要带前置方向历史的真实录制，或另行明确的输入恢复合同，才能闭合该显示/导航分支。

源Q15至35及Q40后半本身WIP，不从枚举补造。AutoPotion不迁。已有普通分支、几何与实际解算通过，不足以升级整个类型。详见 `docs/reference/NativeQuantumFinalVerse.spec.md`。

### 死宫DD190灰炸弹

唯一review类型为 `BossMod.Heavensward.DeepDungeon.PalaceOfTheDead.DD190TheGodfather.DD190TheGodfather`，入口 `Presets/PalaceOfTheDead/Entries/Palace151To200.cs`。

本机PR的 `MechanicControl.ApplyTarget` 在存在任何ForbiddenTargets时返回true，导致 `TargetSelectorService` 跳过其他合法目标的普通重评估。精确灰炸弹禁攻会附带扩大选敌接管，无法仅通过换成 `ScriptHintsApi` 消除。

保留模型、双视图和已验证导航；不全停手补偿，不虚构替代目标，不擅改PR。缺口保持review，等待宿主能力及实机证据。详见 `docs/reference/PalaceOfTheDead151To200.spec.md` 和 `native-palace-earlyzones-20261125-palace-02.log`。

## 七 架构与验收中容易出错的约定

机制模型维护事件、原始身份/代际、锁点/锁向、准备/判定/结束时间；公共输出层转换几何与当前求解器数据；执行层统一处理控制权、移动总开关、owner和回放隔离。地城类优先沿 `DungeonEncounter → DungeonOutput → SafeZone`，不让模型依赖栅格、A*/SIPP节点或调度器内部状态。

状态按60个真实槽与SlotIndex处理，不能用Status ID字典替代。同槽身份/来源变化、Param及到期刷新判据、旧/新Party提交顺序、冷激活与热Pet过滤均按对应源组件验证。旧录像没有的物理槽和历史不能从实时对象补造。

连续机制区分预测和真实结束、当前危险和未来显示。不能统一用到期删除、统一sequence去重或统一“来源相同才消费”；部分组件明确跨来源FIFO，部分按目标或整组清理。Cancel、Death、Destroy、raw结束各自有不同语义。

双视图保真实凹边界、洞、排序与颜色，显示与导航分开验证。PR联合面向需持续续期；动作限制区分全停手、禁读条、实体禁攻及原生普攻停止。伤害预测沿现有诊断消费，不新增外部治疗策略。

当前未发现根目录 `.codegraph/` 或 `.helloagents/`，不需创建索引。子代理仅用于独立探索/审查，使用默认角色及 `fork_turns="none"`；主代理亲读当前要修改的实现，负责方案、修改和最终验证。

## 八 本机验证入口

根 `global.json` 要求.NET11.0.100，交接时本机仅安装9.0.321及10.0.303。现有 `.tmp/halatali-verify/global.json` 选择10.0.303，根配置未改。继续在该目录执行，以复用既有SDK选择；不能宣称已经验证.NET11。

Shell命令按项目要求使用 `rtk` 前缀，FastCtx终端为bash。下列是接手后待执行的命令，不是本次交接重新运行的测试。日志序号应先确认不存在，避免覆盖历史证据。

```bash
cd "C:/Users/xiaos/Documents/GitHub/MasterPlayer/.tmp/halatali-verify"

# 先单独复现M4失败；筛选会命中该名称，输出人数与时钟差异后再修。
rtk proxy dotnet run --project "../../tests/MasterPlayer.RegressionTests.csproj" \
  -p:CI=true -p:UseAppHost=false \
  -p:OutputPath="C:/Users/xiaos/Documents/GitHub/MasterPlayer/.tmp/halatali-verify/wicked-model/" \
  -- "--filter=WickedThunder native damage" --keep-going

# 修后合批覆盖M4、楼层与公共控制。
rtk proxy dotnet run --project "../../tests/MasterPlayer.RegressionTests.csproj" \
  -p:CI=true -p:UseAppHost=false \
  -p:OutputPath="C:/Users/xiaos/Documents/GitHub/MasterPlayer/.tmp/halatali-verify/wicked-model/" \
  -- "--filter=WickedThunder,EO floor,PT floor,HoH floor,Palace floor,script controls" --keep-going

# 集中生成CI产物；不部署到正在使用的宿主目录。
rtk proxy dotnet build "../../MasterPlayer.csproj" -p:CI=true

# 已存在的楼层实际DLL专项。新增M4 runner后，再加入其真实匹配名称。
rtk proxy dotnet run --project "../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj" \
  -p:CI=true -- "../.." "../../bin/Debug/MasterPlayer.dll" \
  "--native-replay=HeavenOnHighFloors,EurekaFloors,PilgrimsFloors"

# 批次完成后使用同一DLL执行全目录。
rtk proxy dotnet run --project "../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj" \
  -p:CI=true -- "../.." "../../bin/Debug/MasterPlayer.dll"
```

实际DLL中的部分输入场景需要 `FFXIV_SQPACK`，Palace录制参数验证使用 `PALACE_GAME_DATA`；根据本机实际游戏路径及既有成功命令设置。缺数据应明确报告，不能跳过场景后声称全目录通过。`--native-replay`拒绝未知名称和空匹配；`WickedThunder`当前尚无runner，不应直接使用该筛选并把失败归因于环境。

批次稳定后，在仓库根执行：

```bash
rtk proxy python tools/bmr_migration_audit.py --check
rtk proxy git diff --check -- "本次实际修改的文件路径"
```

先修改审计生成器，再按现有方式更新CSV；`--check`只核对报告，不生成新结果。保留每批唯一日志、命令退出码和对应DLL哈希。不要每个模块重复完整构建与全目录，也不要为文档交接重跑耗时生产验证。

## 九 工作区与文件接续

已有改动涉及宿主、脚本、录制、空间、UI、各区域实现及测试，并有大量未跟踪源文件和 `.tmp/` 证据。只复制Git已跟踪文件或只看HEAD会遗漏当前迁移成果。

以下五个已跟踪文件当前为删除状态，与死宫流程收敛相关，不能为了“修复缺文件”恢复：

- `Presets/PalaceOfTheDead/Hosting/PalaceFloorInteraction.cs`
- `Presets/PalaceOfTheDead/Hosting/PalacePomanderControl.cs`
- `Presets/PalaceOfTheDead/PalaceClearBehavior.cs`
- `Presets/PalaceOfTheDead/PalaceFloorNavigation.cs`
- `Presets/PalaceOfTheDead/PalaceFloorOptions.cs`

接手修改配置、环境或核心文件前保留可恢复备份；新文本UTF-8无BOM，现有文件保留编码和换行。不要清理其他任务的日志、重置工作区、覆盖用户修改或自动提交/推送/部署。

交接时FastCtx运行任务列表为空，原会话最新一轮状态为interrupted；接手仍应重新检查活动任务。不要重复启动写入同一输出目录的构建，也不要依赖旧子代理上下文。

## 十 接手顺序与结束标准

1. 读取本交接、根AGENTS、迁移总规格及相关模块规格；检查工作区和后台任务，复核712/501/1审计。
2. 修复或准确定位M4唯一模型失败，收齐13组件语义核对，补实际DLLrunner、客户端表来源与模块规格。
3. 收完EO/PT楼层禁桥前完整性和PT联合接入审查；对修改分支定向复验，再更新ownership、生成器及清单。M4与楼层可做互不冲突的只读并行审查，主线程集中构建。
4. 保留Quantum与DD190的真实缺口；能独立补的证据继续补，不能处理的分支记清条件后推进其他待迁内容。
5. 根据当前清单选择其余普通团队/联盟、极幻、多变异闻、零式绝灭、大规模探索、任务战等内容，逐批原生迁移并验证，不长期绕开复杂类型。
6. 所有授权机制完成并具备对应证据后，才审查并清退剩余桥消费者；明确报告仍依赖的类型、明确禁止/暂缓的范围及实机待验项。

授权范围内还有可推进的迁移时，不以“一批完成”结束。遇到必须人工介入或工具/额度硬限制时，留下精确文件、失败、运行命令和下一步，不把未完成写成全部完成。

主要权威入口为 `docs/reference/BMRToMPScripts.spec.md`、`docs/reference/BMRMigrationInventory.csv`、`docs/reference/BMRMigrationGroups.csv`、`tools/bmr_migration_audit.py`，以及各模块规格与对应生产源码；本交接用于快速恢复，不另建平行进度账。

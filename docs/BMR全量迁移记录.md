# BMR 全量机制与绘制迁移记录

日期：2026-09-22。目标：按批次逐组件对照 BMR，移植机制、场地与绘制；斗兽棋明确排除。**当前交付第一批三个普通地城，不代表全部副本已经移植，也不代表已完成实机一比一验收。** 后续目标要求补齐缺失能力、保留必要时间轴、使用内部测试验收，并逐里程碑提交；此前暂缓的 F02／F03／F05 仍记为未完成，随所需运行时能力继续处理。

## 来源与清点口径

- BMR：`C:\Users\xiaos\Documents\GitHub\BossModReborn-7.56`，提交 `f6914f3e85145b5c0390d2e53376b4ffd957cca6`，清点时工作区干净。
- MP 起始提交：`e6cf0351b380069798165d04f05bdd7b29e64bf9`。
- [完整模块清单 CSV](reference/BMRModuleInventory.csv)：每个完整类型名一行，包含模块类型、资料片、分类、成熟度、分组、源码位置、SHA-256、迁移状态与注册核验说明。支持用电子表格筛选；GroupID/CFCID 不等于 TerritoryType。
- 清单使用 Roslyn 语法树和基类链解析，包含间接继承、同文件多个模块、using 导入的基类、没有 ModuleInfo 的类型；不把注释里的类声明算成活跃源码。它是**源码候选清单**，不是成功加载数量，仍需逐批核验 States 构造、主 OID、注册冲突与条件。
- 2,841 个 Modules 下 C# 文件，**1,060 个非斗兽棋 BossModule 候选 + 57 个 ZoneModule**。另将 45 个斗兽棋模块保留为“排除”行，总计 1,162 行。野外狩猎、任务、金碟等独立分类保留，避免“所有副本”边界导致资料遗漏。
- 初始文本扫描曾得到 1,070；语法复核确认多出的 10 项完全处于注释中：A11CommandModel、A12Hobbes、A14WalkingFortress、A12Hashmal、A13Rofocale、A14Argath、Hydatos 的 Barong、Frostmane、KingGoldemar、Leuke。它们没有可运行模块可移植；不计入完成率。

| 资料片 | 非斗兽棋首领候选 |
|---|---:|
| RealmReborn | 98 |
| Heavensward | 99 |
| Stormblood | 122 |
| Shadowbringers | 186 |
| Endwalker | 211 |
| Dawntrail | 273 |
| Global | 71 |
| 合计 | 1060 |

| 类别 | 数量 | 类别 | 数量 |
|---|---:|---|---:|
| Dungeon | 287 | Trial | 34 |
| Extreme | 24 | Unreal | 4 |
| Raid | 26 | Savage | 28 |
| Alliance | 88 | Chaotic | 1 |
| Ultimate | 8 | Foray | 134 |
| DeepDungeon | 49 | VariantCriterion | 64 |
| TreasureHunt | 71 | MaskedCarnivale | 60 |
| Hunt | 66 | FATE | 6 |
| Quest | 103 | HallOfTheNovice | 5 |
| PVP | 1 | GoldSaucer | 1 |

来源成熟度为 Verified 713、Contributed 187、AISupport 102、WIP 58。成熟度只代表 BMR 的声明，不能替代 MP 验证。

57 个区域模块包含四种深层迷宫共50个、优雷卡4个、博兹雅／扎杜诺尔2个、沙斯塔夏1个。ForkedTowerMagic Extreme 的 TwoHeadedAevis、SwordDancer、Necrophobia、Index 没有显式 ModuleInfo，源码注释说 CN 不注册；实际注册器仍扫描这些类型，但缺少名为 `OID.Boss` 的默认成员会推断出主 OID 0，不能自动匹配真实首领。见 U04，不将注释当作编译排除证据。

## 批次与当前结果

| 批次 | 范围 | 当前状态 |
|---|---|---|
| 01 | 沙斯塔夏、塔姆塔拉墓园、铜铃铜山普通版 | 7个首领模块、18个组件、1个区域模块已接入；存在下表明确差异，待实机 |
| 后续 ARR 地城 | 从 D04 起，含困难版 | 待逐组件核验与移植 |
| 其余资料片地城 | Heavensward 至 Dawntrail | 待移植 |
| 讨伐／极／幻／团队／零式／联盟／绝境 | 按完整类型分别记账 | 待移植 |
| 探索／深层／变体／异闻／寻宝／假面等 | 含 ZoneModule 与隐式基类组件 | 待移植 |
| 野外与其他分类 | Hunt／FATE／Quest 等 | 已盘点，待逐项确定实际模块覆盖 |
| 斗兽棋 | 45个模块 | 按用户要求排除，本轮未改其机制 |

未在批次01范围内的 **1,053 个首领候选和56个区域模块尚未移植**。通用读条识别能够画圈，不计作对应副本机制已经移植。

首批规格、逐组件参数、常量、场地和验收条件见 [ARRDungeons01.spec.md](reference/ARRDungeons01.spec.md)。代码入口是 `Scripts/Dungeons/`，实现位于 `Mechanics/Dungeons/`；通过已有脚本目录规则随 DLL 内置发现，保留同名/GUID外部覆盖。

运行方式：重载本轮 MasterPlayer DLL，在“机制脚本”启用对应地城脚本，进入区域1036／1037／1038。自动移动和显示仍使用原有功能开关；不修改用户已保存设置。脚本随区域切换自动停止，首领场地只在该首领进入战斗后接管。

## 明确缺失、差异与不确定项

| 编号 | 类型 | 影响 | 现状／核验方向 |
|---|---|---|---|
| F01 | 已补首批目标预测 | BMR SingleTargetCast 的队伍槽位、时间及伤害类型 | 使用 PR 队伍排序槽位映射实际施法目标；本人时间轴职能另存。四个组件输出目标／槽位／时间／Tankbuster 类型并用于提示，自动减伤消费仍属 F05 |
| F02 | 按用户要求暂缓 | Denn Guard／Boss 和 Galvanth Imp 的 AI 目标优先级 | 当前未接入整套BMR目标调度；不等同于斗兽棋的可选单次转火接口 |
| F03 | 按用户要求暂缓 | BMR 任意颜色、线宽、文字层、逐模块 DrawEnemies | 双视图保留真实AOE尺寸和水纹轮廓，使用MP用户配色；水纹暂用安全色。敌人标记沿用通用雷达，提示进入脚本状态与日志 |
| F04 | 已解释，保留时序差异 | SimpleAOEs 正常结束事件 | 两侧都读取施法状态；BMR 将变化转换为 CastFinished，MP 每0.1秒扫描并加短保护。显示兜底至判定后2秒，导航窗口至判定后0.6秒 |
| F05 | 按用户要求暂缓 | BMR寻路、目标评分、职业减伤／疾跑／交互等AI能力 | MP独立解算器和用户余量生效。完整能力表须逐批扩充；未核验的组件不能套用斗兽棋轻量模型后直接算完成 |
| U01 | 待确认 | 当前客户端区域ID与场地高度 | 新区域ID已查询XIVAPI正反表，尚未在用户正在运行的客户端中实测 |
| U02 | 已解释，实机时序待验 | 水纹EAnim与EventState不是同一字段 | 完整EAnim来自两个ushort。录制缓存插件加载后收到的历史动画；插件加载之前的动画未知，不从EventState字节推测 |
| U03 | 已补新格式 | v2对象快照、变化、包朝向与录制时钟 | 首批地城／可视化脚本／雷达读取录制世界，支持暂停与Seek；旧v1文件缺失的信息不可补回，详见录制格式说明 |
| U04 | 已解释，保留来源限制 | Sastasha 两模块为Contributed、四个FTM Extreme模块的注册条件 | Contributed不等于错误；四个FTM类型的默认主OID为0，不能自动匹配真实首领，不能称为完全未被扫描 |
| U05 | 待实机 | 所有首批机制的网络时序和游戏内实际显示 | 离线通过不等于实机通过；需要各副本录像／游戏内验收 |

复杂场地不是外接圆替代。首批7套多边形由当前BMR模块源码重新编译并执行其场地定义导出；使用本地BMR几何运行库 SHA-256 `f011ccab502087aa1581c2bbb5f1374da613a3ec8de064d9e720deda06ff3678`。导出不启动BMR插件、不成为MP运行时依赖。库的构建提交未嵌入来源元信息，保留该散列；离线已检查铜铃的差集缺口与三套墓园边界，最终视觉仍按U05实测。

原始形状与导航副本分离：绘制不加0.5米用户余量，导航正常使用用户余量；没有引入斗兽棋的额外0.35米。水纹、单体伤害、视觉读条不会被当作需要躲避的空间危险。

## 本轮反馈说明

### F01：时间轴职能与队伍槽位

`TimelineRoleManager.CurrentRole` 是本人选择的 `None/MT/ST/H1/H2/D1/D2/D3/D4`，不能单独推导另外七名队员。完整映射读取 `PartyHelper.GetSortedPartyMember(PartyPrioritySlot)`；四人队使用 MT/H1/D1/D2，八人队使用八个槽位，缺员保持未解析。默认排序按职业分组，也可手动排序；默认顺序不保证实际主坦或近战／远程分工。两套枚举不能直接强转，时间轴枚举多一个 `None`。

首批四个 `SingleTargetCast` 预测实际施法目标；自目标施法才读取施法者当前目标，与 BMR `Components/UnavoidableDamage.cs` 的逻辑一致。目标必须在已解析队伍中，不能找不到目标就猜 MT。保留判定时间及伤害类型；Catapult 虽提示 `Single-target damage`，BMR 仍使用默认 `Tankbuster` 类别。当前消费为文字提示，尚无 BMR 式治疗／减伤调度，按 F05 暂缓。

### F04：普通读条如何结束

| 环节 | BMR | 当前 MP 首批地城 |
|---|---|---|
| 正常结束的来源 | WorldStateGameSync 读取原生 IsCasting；旧 CastInfo 变空或替换 | DungeonScript 每0.1秒读取同一类原生施法状态 |
| 内部通知／处理 | ActorState.OpCastInfo 对旧施法发 CastFinished，再分发给组件 | DungeonEncounter.Synchronize 直接清理对应施法 |
| SimpleAOEs 清理 | OnCastFinished 按施法者移除范围；对象销毁也会补结束 | 无施法时经过0.2秒启动保护，并满足已见效果／到达预计判定／距离判定超过0.5秒之一才清理；取消、打断、销毁和替换即时处理 |
| 效果事件 | CastCounter 只计数，不负责 SimpleAOEs 移除 | 标记 EffectSeen，按来源／技能／序列去重，不直接以效果事件删除范围 |
| 兜底 | 世界状态持续同步 | 预测显示最多到 FinishAt+2秒；空间危险窗口仅到 FinishAt+0.6秒 |

BMR 的 `CastFinished` 是世界状态内部事件，**不是 PR 少收了一个专门的“正常读条完成”网络包**。差异主要是扫描节奏及 MP 的收尾保护，仍需 U05 实机对照。出处：BMR `Framework/WorldStateGameSync.cs` 的施法同步、`Data/ActorState.cs` 的 `OpCastInfo`、`Components/GenericAOEs.cs` 的 `SimpleAOEs.OnCastFinished`。

### U01：区域编号与高度

BMR 的 CFC 是“哪一个副本”，MP 的 TerritoryType 是“当前地图”。沙斯塔夏 CFC4→区域1036，墓园 CFC2→1037，铜铃 CFC3→1038；旧区域号157／164／161不能直接沿用。当前映射已经用 XIVAPI 正反查询；尚未确认的是用户国服客户端的数据表对应关系，以及从首领高度恢复的 Y 坐标是否与实际场地相符。

### U02：水纹完整动画不能用一个状态字节代替

EAnim 来自 ActorControl 413，完整值为 `((uint)Param1 << 16) | Param2`。水纹以 `0x00040000` 位判断激活，例如 `0x00040004`。对象的 `EventState` 只有一个字节；ActorControl 409 的 EObjSetState 也是另外一种事件，二者不能替代完整动画。

BMR 的 `TrashSpawning` 也只记组件存活期间收到的动画，没有从 EventState 还原历史。MP 新录制会保存插件加载后已观察到的动画，并在回放初始快照和跳转重建时恢复；若动画早于插件加载或事件丢失，仍标为未知。实时脚本中途加载的既有行为仍是等待下一次事件，U02 不据此标为已实机验收。

### U03：对象状态和包朝向

新录制为 v2：初始完整对象快照、每0.1秒及事件前的对象变化、销毁列表，另保存 `PacketFacing`。记录玩家／怪物／场景对象的身份、位置、普通朝向、战斗与可选中状态、HP/MP、状态列表、读条、连线、模型状态、已知动画、队伍槽位和本人的时间轴职能。首批地城、可视化脚本和雷达已接入录制世界；绘制及区域计时随回放暂停／变速／跳转。

旧录制仍可读，但不能补出原本没录下的数据；回放缺对象或包朝向时不读取今天的游戏对象来伪造还原。脚本直接访问 `Svc.Objects`、`DateTime.UtcNow` 或 PR 原始绘制接口的情况仍需逐个迁移。字段、采样、校验与调用规则见 [录制回放格式](录制回放格式.md)。

### U04：来源成熟度与注册异常

`Contributed` 在 BMR 表示第三方贡献、尚未被插件作者验证，不能理解为“已知有错”。沙斯塔夏的 Chopper／Denn 是 Contributed；染血纸条所在 ZoneModule 是 Verified。Verified 只描述上游模块，不证明 MP 移植已实测。

FTM Extreme 四个类型仍会被注册器扫描；它们没有显式 ModuleInfo，OID 枚举也没有名为 `Boss` 的成员，于是默认推断主 OID 为0，不能自动匹配 TwoHeadedAevis `0x4C18`、SwordDancer `0x4D7E`、Necrophobia `0x4BE7`、Index `0x4B67`。没有发现按 CN 编译排除的对应分支，因此记录为“默认注册身份异常，待修正与验证”，不写成“类根本不存在于注册表”。

## 验证与再生成

验证构建使用 `-p:CI=true`，输出到仓库 `bin/Debug/`。2026-09-22 在当前提交 `cde2b27` 基础上重新验证后，按用户要求使用 `-p:CI=false` 部署至 `C:/Users/xiaos/AppData/Roaming/XIVLauncherCN/pluginConfigs/PromeRotation/Plugins/MasterPlayer`。DLL／PDB、PR 脚本依赖与两份许可文件均与验证产物或来源文件逐一核对 SHA-256；DLL 为 `7737efb7fb57c87a3a7aab379aa9a6d5808bab0ce2fd3a7a2a38765e2c4029d8`。没有代替用户执行游戏内热重载。最新验证结果：

| 检查 | 结果 |
|---|---|
| 主项目构建 | 0 错误、0 警告 |
| 回归项目构建 | 0 错误、2 条既有 CS0649 警告，见下 |
| 首批地城回归（包含在全量中） | 58／58 通过，包含真实 PR 事件转换、延迟／取消／快速重放边界与7套场地中心 |
| 回放定向回归 | 22／22 通过，包含本轮新增14项世界快照、时钟及目标预测检查 |
| 全量回归 | 676／676 通过，包含当前基线新增的移动回归 |
| 独立脚本编译与实际 DLL 目录检查 | 8个入口编译通过；内置发现、外部源文件／DLL／可视化覆盖、失败回退及生产门面清理通过 |
| 实际 DLL 回放 | 地城脚本注册、暂停、变速、前后跳转、最终帧、限时区域过期、实时生命周期隔离、刷新、退出清理和旧文件隔离通过；禁止访问实时对象和GreenMove |
| 清单与文件检查 | 清单沿用1,162行、无重复类型；47个变更文件UTF-8无BOM；`git diff --check` 通过 |

关键原始输出：

```text
ok dotnet build: 1 projects, 0 errors, 0 warnings (00:00:04.17)
PASS: 676 regression scenarios
PASS production local runtime: script registration, cleanup and replay transitions
PASS production replay: recorded objects, pause, speed, forward/backward seek, final frame, cleanup and legacy isolation
```

回归项目重新编译有两条既有 `CS0649`：`Mechanics/Crucible/CrucibleModel.cs` 的 `Actor.Role` 与 `Actor.ModelState` 在排除实时脚本的测试工程中未赋值；本批未修改斗兽棋实现。以上为离线验证，U01／U02／U05等实机项仍未完成。

```powershell
rtk dotnet build MasterPlayer.csproj -p:CI=true
rtk dotnet build tests/MasterPlayer.RegressionTests.csproj
rtk dotnet run --project tests/MasterPlayer.RegressionTests.csproj --no-build
rtk dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -- "C:/Users/xiaos/Documents/GitHub/MasterPlayer" "C:/Users/xiaos/Documents/GitHub/MasterPlayer/bin/Debug/MasterPlayer.dll"
```

定向回归：最后追加 `-- --filter="ARR dungeons"`。使用真实本地几何／安全区注册及解算库，测试桩捕获双视图预览；另外运行实际DLL的内置发现、外部覆盖与脚本独立编译检查。

清单与场地导出工具复用现有Roslyn测试工程；后续批次修改状态映射后再生成，避免手改CSV与源码脱节：

```powershell
rtk dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -- --bmr-inventory "C:/Users/xiaos/Documents/GitHub/BossModReborn-7.56" "C:/Users/xiaos/Documents/GitHub/MasterPlayer/docs/reference/BMRModuleInventory.csv"
rtk dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -- --bmr-arenas "C:/Users/xiaos/Documents/GitHub/BossModReborn-7.56" "C:/Users/xiaos/Documents/GitHub/MasterPlayer/Mechanics/Dungeons/ArrDungeonArenas.cs"
```

移植代码和场地数据沿用BMR许可；仓库已有 `ThirdPartyNotices/BossModReborn.LICENSE`，构建继续携带。

# ARR 极蛮神 MP 原生迁移规格

生产实现直接消费 MP 对象、读条、效果与时间轴事件，经 MP 安全区及 `Draw.AddAoePreview` 发布导航和雷达/世界预览，不创建 BMR 模块或会话。

## 究极神兵假想作战

入口 `Presets/Trials/Entries/UltimaWeaponExtreme.cs`，实现 `Presets/Trials/UltimaExtreme/`。本机客户端双向查询为 CFC68 ↔ Territory348，主对象90F，场地中心(0,0)、R20。查询证据 `.tmp/halatali-verify/ultima-client.log`。

源为 `MechanicRuntime/Upstream/Modules/RealmReborn/Extreme/Ex1Ultima/` 的全部已激活组件。源未启用的灭团、激光等 TODO 不计覆盖。

- RadiantPlume1521 R8、Weight1522 R6、Eruption1524 R8；三向MagitekRay1507/8/9前40全宽6；AssaultCannon1526前45全宽2。独立辅助体分别持有，实际结束/取消/销毁才清除，不按名义结束时间删除。
- MistralSong1517 或912时间轴588建立寒风之歌。时间轴预测+6.6秒，仅最后6秒输出；实时读取位置和朝向，R23.4、总150°。1518前按源场地两边缘投影到首领构造击退后寒风禁区，显示30码径向击退并截在场地R20；1518后改为寒风本体禁区，1517实际结束才撤销。该源自定义击退不检查免疫，迁移保持相同语义。
- Eye1519显示12–25环，导航为10–12窄环；优先于Geocrush1520的R18避让，两套状态独立，Eye结束后恢复仍有效的Geocrush。事件先于对象快照时，使用已接受读条的结束时刻。
- Crimson1525或910时间轴8D：时间轴+5秒，前43、后5、全宽12，实时跟踪伊弗利特位置/朝向，实际1525结束撤销。
- Freefire：1B2时间轴449添加施法者，全部使用最近一次时间轴+6秒的共享时钟。1515每次仅移除对应首个施法者；对象销毁不提前删除。首个1515之后，当前主坦为骑士/战士时保留显示、取消躲避并请求30/43无敌。源如此，不以技能可用性悄悄改回躲避。
- 追踪球913：1650记录队伍点名；按球实时朝向与存活点名者方向的点积最大值匹配。显示R6及追踪线，导航避R7；他人的追踪线额外全宽4禁区。1510记录已炸球，爆炸数严格等于点名数时同时清空两张表，因此尚未销毁的旧球可能重新显示。
- AethericBoom1511效果计数。接收爆弹914严格要求当前数量=2×(计数+1)，对应4/6/8球；数量不足时只预站位。第一轮按南→东，第二轮按南→东南→东北，第三轮按西南→东南→东北→西北排队。1512或销毁清除对应项。
- 当前主坦只避active球R3。前两次击退期间其他人站首领南侧20°窄锥；等待球时按分工±2,10；第三轮M1/M2改为±10,-2，并请求职能对应抗击退。非第三轮追下一球前方1码R6；近战/坦克且球R8内人数>5才进入R1.5触球。
- Viscous369四层按当前首领目标判定换坦，状态包先于快照时即时生效。非主坦、坦克职业且已站首领北侧才请求7533挑衅。顺劈显示目标R2；其他人避主坦，主坦避队员。
- TankPurge1514按效果次数奇偶在MT/OT、M1/M2、R1/R2之间轮换7535/7549/7560。动作执行复用 `DungeonActionControl`，遵守智能移动总开关、MovementAllowed、存活、无读条、动画锁及CanCast；ARR和MP回放禁执行。
- 雪仇只在5码加敌人hitbox内存在存活可选敌人时请求。公共动作执行先按显式对象检查原生 `GetActionStatus`，再用PR的Self解析入口和NetworkTid提交原显式目标，避免PR在读取NetworkTid之前因当前未选敌而丢弃请求。该接口只证明提交，不据此声称实机施放成功。
- 常态分工位置采用源偏移，只有确实存在主坦顺劈禁区且没有其他机制/浮游炮时输出低优先级站位；浮游炮优先于首领。主对象HP0/死亡、wipe、停用和Seek清理，个人死亡保留机制。

### 验证及尚存差异

在 `.tmp/halatali-verify/` 使用本机SDK运行：

```text
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=UltimaExtreme
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=Twintania
dotnet build ../../MasterPlayer.csproj -p:CI=true
dotnet run --project ../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- ../.. ../../bin/Debug/MasterPlayer.dll
```

定向验证包含独立源几何密集采样、寒风两边缘构造、实际SafeZoneSolver脱离Geocrush、首帧时钟、连续蛮神事件、爆弹精确数量/顺序/人数门槛、职能减伤、换坦以及销毁与效果时钟分离。实际DLL合成回放覆盖独立结束、个人死亡、HP0、实时转向、爆弹队列、四层状态、回放动作隔离、反向Seek和owner清理。

敌人整体坦克牵引到(0,-10)/朝北、攻击强度和远距离偏好没有可直接承接的PR控制接口；当前已实现个人站位、目标优先级和条件挑衅，尚未完整承接这些源AI元数据。审计保留 `mp_script_needs_parity_review`，不能据此宣称整个源AI已等价迁移。这些验证为离线回归及合成回放，仍需ARR实录和实机确认。

## 迦楼罗歼殛战

入口 `Presets/Trials/Entries/GarudaExtreme.cs`，实现 `Presets/Trials/GarudaExtreme/`；客户端交叉验证CFC65 ↔ Territory297（`.tmp/halatali-verify/garuda-extreme-client.log`）。主对象F5、中心(0,0)、R22；正常、真、极入口按区域隔离。

### 阶段及时间

源 `Ex2GarudaStates.cs` 的目标可选状态由实际观察推进，34.7/6.8/63.6等注释时长不作为超时。第一次姐妹分身620/621对象出现立即切phase2，连已死亡对象也计入；切阶段重建Eye状态并清phase1寒风缓存。个人死亡不重置整场。

- Phase1两次消失分别预览(0,-13)、(13,0)的石柱影区；消失停用顺劈、螺旋气流和常态AI，重现后等待实际读条，实际结束才恢复下一组。第二次结束后的源`SimpleState`没有自动推进条件，原生也不按37.6秒虚构phase2。
- Phase2首组姐妹阶段的消失检查有60秒`checkDelay`；60秒前即使首领不可选中，也保留该组组件。此门槛之后，实际消失停用顺劈/螺旋气流/摩擦/邪轮；实际重现后等待全屏读条及真实结束。
- 首组姐妹→Eye/Shriek→Whirlwind/Blast，如此交替共三组Eye及三组Whirlwind。Eye在Shriek结束后继续保留，直到下一次实际消失才停用；重现后才进入Whirlwind。GreatWhirlwind在首领消失期间保留，到实际重现时撤销；没有把消失与重现合为同一事件。
- Eye1671起读记录实际结束，实际结束时更新为观测时间+4.2秒。其预告不因超过4.2秒自动删除，位置逐帧量化；组件停用才整体撤销，符合源`ActiveAOEs`持有语义。
- WickedWheel在姐妹/旋风组进入时预测+25秒；严格剩余<3秒才显示R8.7轮廓，非当前主坦才躲避。1552效果后，仍有存活可选Suparna则重新+25，否则清空预测；不按普通读条Finish替代效果事件。

### 几何、护罩与控制

- 螺旋气流1382 R11.7总90°；姐妹摩擦1549 R5、羽毛雨1550 R3、Gigastorm1555 R6.5、GreatWhirlwind1777 R8，各辅助体独立持有，观察实际结束/取消/销毁清理。Gigastorm是刺羽死亡读条，允许已死亡对象起读并保留至真实结束。
- 三种下行突风按源实时目标瞄准：首领R11.7、姐妹R11.36、总120°；非目标避范围，目标避免面向存活队员。未选中/已死亡敌人不发布顺劈。
- 活石柱F3按真实hitbox显示，常态AI避石柱R5；远程/治疗在场中R7低优先级站位，实际伤害避让优先。羽毛与姐妹保留雷达/世界实体轮廓。
- 石柱LOS取活F3的hitbox−.5作为遮挡角宽，影区起于柱心距离+.2、最远31.7，并扣除真实柱体。消失先绘制预测影区；实际1761起读冻结实际施法者位置和当时活柱，按读条时钟发布导航危险，真实结束撤销。
- **源缺陷修正**：vendored `MistralSong.OnCastStarted`只调用`Modify`，没有调用基类/`AddSafezone`，因此`Safezones`始终为空。原生显式发布上述遮挡预览及导航；这是有意恢复该机制的绘制和躲避，不宣称逐点复刻源的空输出。
- phase2第一个非EventState7的1E8F68护罩显示R6绿色，并以即时反向危险区要求进入；State7撤销或改用下一护罩。提示只在当前玩家位于罩外时给出。
- 刺羽600在首领可选中时使用源`PriorityPointless=-1`软优先级，不升级成PR ACR硬禁攻，也不强制停止手动选中时的自动攻击。坦克自身尚未被该刺羽选中、其当前目标ThermalLow379≥2层时请求7533挑衅，但不强制选刺羽输出伤害；源Priority setter使先设的-1无法被随后6覆盖，原生保留这一实际行为。状态增减包先于快照即时生效。首领消失时优先级为6，转为优先击杀刺羽。
- 常态优先级保留源分工：Chirada2；Suparna3/Razor4/Satin5，MT对后三者为0；首领1。动作共用实时门控和回放隔离。

### 验证与差异

`GarudaExtremeRegression`覆盖源几何密集采样、两次寒风与真实SafeZoneSolver进影区、首组60秒门槛、Eye+4.2/跨Shriek保留、邪轮严格3秒边界、两层刺羽换坦/软优先级、护罩State7和真实解算进入、Gigastorm死亡读条及Shriek全队伤害预测。实际DLL合成回放覆盖这些组件的顺序启停、软优先级及挑衅请求、个人死亡、反向Seek和owner清理。

源敌人的拉位/期望朝向、攻击强度和远距离偏好尚未完整承接；当前保留分工优先级、个人导航和刺羽条件挑衅，审计仍为`mp_script_needs_parity_review`。GreatWhirlwind源只激活R8简单AOE，没有按枚举注释启用15码击退组件；原生不将注释当作已迁移击退证据。凝视与热病式全停手也没有对应源组件。这些结果仍需ARR实录和游戏内确认。

## 泰坦歼殛战

入口 `Presets/Trials/Entries/TitanExtreme.cs`，实现 `Presets/Trials/TitanExtreme/`。客户端双向查询 CFC64 ↔ Territory296，主对象F8，场地初始R25；查询证据 `.tmp/halatali-verify/titan-extreme-client.log`。

- 七阶段使用真实可选状态和心核5E5出现/不可选推进；阶段变化优先于当前机制。两次大地之怒实际结束后缩为R15/R12，分别显示13–15、10–12绿色安全环，内外危险区独立导航。最终阶段各轮继续按R12处理。
- `Condition`的expected不是最短等待：条件满足立即推进，否则在expected+maxOverdue后告警并推进；CastStart/CastEnd/Targetable不以注释时长代替真实事件。双重大地之重的checkDelay为1秒。心核提前退出会撤销上一阶段组件；源终端状态不会按1000秒自动推进。
- 三类地裂1467/1468/1475使用实际包朝向、前40.25全宽6；大地之重1470 R6，爆弹1471 R6.3。辅助体独立持有，名义结束不清除，真实结束、取消或销毁才清理。回归独立断言输入朝向，避免与错误缓存同时偏转导致假通过。
- 山崩1464 R21.25、岩石崩溃1463 R11.25，总角120°，实时读取目标转向。非目标避锥；当前目标避开队员方向。激震1465按四次真实效果计数，多目标同sequence不重复推进，每次更新下一预测为+1.2秒，排除死亡队员并发布可见提示。
- 地动1466保持首领朝场心方向2码内的R1.5站位；初始按Finish+1秒，真实结束改为观测时间+1秒。显示径向13码击退，免疫仅隐藏箭头，不撤销源站位要求。
- 牢狱连线7及645/1652效果建立+2.9秒pending，292获得立即清除。阶段激活恢复已有连线后，再用已有292清理；治疗站场后偏转−30°边缘，其他人站首领+Z1码。切换视角重算个人输出。
- 炸弹顺序保留cast到达顺序。第二阶段每轮为`0→奇数8/偶数5→9`，心核为`0→5`；最终五轮分别保留四加五的`0→4→9`与三排的`0→6→9`，由地裂及炸弹数量真实条件推进，不一次绘制未来全部危险。
- 狱卒死亡领域1E8F8F为R5不可通行障碍，EventState7撤销。首领/心核优先级1、狱卒2、中央待杀炸弹3、牢狱4/5；没有更高优先目标时，已知炸弹的0优先级仍可选。当前主坦126至少两层、未启用山崩组件且无活可选狱卒时，非当前主坦的坦克可请求挑衅首领/心核。

`TitanExtremeRegression`共12组，包含源几何密集对照、真实解算进安全环、两次缩圈、牢狱快照恢复、独立时钟、四连激震、完整心核及最终五轮、换坦与0优先级边界。实际DLL回放另验证七阶段、双缩圈、状态路由、个人死亡、反向Seek、回放禁执行和停用清理。

源敌人的期望位置/朝向、攻击强度、远程偏好，以及狱卒`ShouldBeTanked`分工尚未完整承接；源狱卒分支未设置`PreferProvoking`，不能用额外强制挑衅冒充分工。审计保留`mp_script_needs_parity_review`。

## 伊弗利特歼殛战

入口 `Presets/Trials/Entries/IfritExtreme.cs`，实现 `Presets/Trials/IfritExtreme/`。客户端双向查询 CFC63 ↔ Territory295，主对象D3，使用原R19.5的36边形场地，不能替换成R20圆；查询证据 `.tmp/halatali-verify/ifrit-extreme-client.log`。

- 单一死亡阶段内运行三轮火楔，之后进入终端Enrage；85%/50%/30%是源注释，不用于推进。活可选小钉进入Nails，首领不可选才退出Nails；775无敌不能提前结束火楔阶段。
- HellfireStart/End和PlumeStart/End均按首领实际读条存在/消失推进。无敌结束等待775移除，源超时为15.1秒；光辉炎柱清空后的resolve超时为1.9秒。消失阶段按实际不可选或新小钉推进，23.5秒兜底；后者允许同帧跳过旋风。终端不按1000秒推进。
- 地火1677/光辉炎柱1359 R8，深红旋风1532前49全宽18，保留各辅助体真实结束/取消/销毁与包朝向。PlumeEnd激活时补发已观察到的helper读条；地狱之火激活时也读取已有1536读条结束时间。
- 灼热风独立于子阶段保留。1529起读替换旧目标，首次预测+5.4秒；严格超过+3.4秒才启用导航避让。显示R14、导航名义R15，目标本人避开其他活队员。1530真实效果共三次，前两次更新+6秒，第三次清空；不靠376剩余时间判断旧目标。
- 地火视觉1676开启严格1秒的诱导识别窗口；1677位置匹配最近存活队员，窗口相等时不接收；没有地火caster的帧清空诱导者。保留输出职业在首领读条期间的固定第二次诱导点，以及之后避开非诱导者R8的行为。
- 锁链377仅第二/三轮及Enrage启用，激活恢复已有状态。最低/最高slot之间画线，strength>1为危险，其他为安全；状态移除将强度清零。非坦克要求位于第一个存活锁链伙伴10码内。
- 火楔击杀顺序在进入阶段时快照：距东侧(15,0)最近小钉先，之后按顺时针角度、同角度距离排序，大钉91B最后；死亡或销毁移除，不因不可选移除，不追加晚生钉。大钉HP严格<50%才发布存活队员伤害预测和减伤提示。
- 三轮OT换坦计数掩码为`0x8/0x7/0x3C70`，按1528实际次数而非375层数判断；Nails激活重置计数。非当前主坦的负责坦克仅在挑衅后R21/120°锥不覆盖活非坦队员时请求7533。Hellfire分工MT/OT/MT，并使用150°/110°/70°的18码安全点；Disappear/Enrage为Unassigned，不继承上一轮分工。
- 当前主坦的减伤窗口采用半开区间：第一轮铁壁`[10,20)`；第二轮`[8,13)`；第三轮铁壁`[9,13)/[91,97)`、雪仇`[20,25)/[102,109)`、骑士/战士强减`[35,40)/[57,63)`。雪仇增加5码加敌人hitbox的有效覆盖检查，动作执行仍受公共实时门控。
- 个人常态/火楔/地狱之火站位均按源角色分支；法系额外后撤15码，青魔属于Limited而非Caster，不套用该偏移。源`SDInvertedCircle`站位/锁链距离是即时禁止区，原生使用反向危险区表达，并统一应用一次MP危险余量；旧BMR宿主将该形状转成低优先级Goal的行为未沿用。此差异是为保留源约束语义，显示与规划仍分别输出。

`IfritExtremeRegression`共12组，覆盖源场地/双视图几何、三轮状态和高DPS跳阶段、严格风/诱导边界、已有helper恢复、火楔顺序、三套换坦掩码、全部减伤窗口、锁链、死亡与真实站位解算。实际DLL合成回放验证风时钟、火楔/地狱之火/炎柱/分身旋风、377事件路由、个人死亡、倒退Seek和owner清理。

小钉`ForbidDOTs`尚无PR逐目标禁DoT接口承接，不能擅自扩大成全技能禁攻；敌人期望位置/朝向、攻击强度和远距离偏好也尚未完整承接。源没有因775发布硬禁攻/全停手，迁移未凭无敌状态虚构停手机制。审计仍为`mp_script_needs_parity_review`，上述离线结果不能代替ARR实录或实机验收。

## 合批验证

共享编译后按场景名合跑；失败项用同一筛选器单独重跑，不再为每本重复构建主项目和完整冒烟。回归`--filter`及实际DLL回放`--native-replay`均支持逗号分隔的OR条件；实际DLL筛选入口会拒绝空匹配或未知名称，且跳过独立入口逐个Roslyn编译，保留真实脚本发现、启停和回放宿主。

在`.tmp/halatali-verify/`执行：

```text
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- "--filter=UltimaExtreme,GarudaExtreme,TitanExtreme,IfritExtreme,script controls" --keep-going
dotnet build ../../MasterPlayer.csproj -p:CI=true
dotnet run --project ../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- ../.. ../../bin/Debug/MasterPlayer.dll "--native-replay=UltimaExtreme,GarudaExtreme,TitanExtreme,IfritExtreme"
```

2026-11-23本批结果：69组回归全部通过；主项目0错误、4条既有Crucible警告；四套实际DLL定向回放合跑通过。日志分别为`arr-extreme-batch-regression.log`、`arr-extreme-batch-build.log`和`arr-extreme-batch-targeted-smoke.log`。此前完整冒烟中的141个入口独立编译均通过，伊弗利特的十进制秒到DateTime精确边界用例修正后以定向回放复验，不将此前退出码1的完整命令写成全量通过。

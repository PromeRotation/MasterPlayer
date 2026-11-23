# 狩猎原生脚本

入口为 `Presets/Hunts/Entries/SimpleHunts.cs`，共享生产实现位于 `Presets/Hunts/`，不依赖 BossMod 类型。按本机匹配CN数据的 `TerritoryIntendedUse=1` 发现48个野外区域；遇到支持的OID才建立实例。没有注册固定圆形场地，原局部地形继续生效。

## 已迁移来源

| 模块 | Boss OID | 已激活源规则 |
|---|---:|---|
| KerShroud | 0x3672 | 27725 R8；27724 长60全宽8 |
| CrystalIncarnation | 0x4571 | 39623 R5；39624 R40总45度 |
| Gajasura | 0x1ABF | 8188 R8.23总120度；8187 R6；8189单体预测和原提示 |
| Orghana | 0x1AB3 | 7997 长55.04全宽8；8000/8002全队预测 |
| OPoorestPauldia | 0x2820 | 16830 R12.025总120度；16831 R34.025总120度；16832长49.025全宽6；16833全队预测 |
| ArchEta | 0x35C0 | 27269长40全宽14；27270 R25总90度；27271 R17；27272全队预测；27273单体预测 |
| Hulder | 0x35DD | 27073 R30总120度；27075冲锋全宽8；27076环5–50；27077全队预测；27078单体预测 |
| ForgivenGossip | 0x2A03 | 17043 R5；18041普通凝视，真实默认范围10000 |
| BoneCrawler | 0x1AB5 | 7924 R14.2/90度、7920 R11.2/90度；7921/7922冲锋全宽8/12；7919 R10并击退20；7918 R18.2/90度 |
| Okina | 0x1AB1 | 7990长30全宽6；7991 R25/60度；7996环8–60；7995 R14并击退30；7993 R10并击退20；7994全队预测；7992纯Hint |
| Angada | 0x1AC0 | 8190 R9.4；8191 Butcher和8192 Rip同时R11.4/120度，Rip等实际效果结束；8193 R6 |
| Gamma | 0x1AB2 | 7914 R5；7913/7915全队预测及源Hint；个人SID960热病状态 |
| LunatenderQueen | 0x35DF | 27363/27369普通及反向凝视；27364/27371环6–40；27365/27370 R15；27366 R6；27367纯Hint |
| Baal | 0x2854 | 17956/17957 R12/180度；17423/17424建立两个R30/180度范围，后段反向并延迟2.3秒；17422/17421实际效果出队 |
| Huracan | 0x28B5 | 17496长80全宽10；17499全队预测；17495前长15全宽10、17497 R6、17498环10–20，各有延迟3.1秒的长80全宽10后段；18042效果出队 |
| Grassman | 0x283A | 17861 R5；17860单体预测；17862冲锋全宽6；17859/17863首次一击及后续五连击提示、持续伤害诊断 |
| Maliktender | 0x2874 | 18019 R8；18022/18024前长20全宽8；18020开始即锁存即死警告 |
| FanAil | 0x35C1 | 27373长30全宽11、固定落点随当前Boss朝向；27376 R6；27378 R8/90度；27379单体、27380全队预测 |
| Starcrier | 0x41FC | R8、环8–25、长40全宽8；真实SID1422/2936/3694/3909驱动方向错乱输入控制；37163全队预测 |
| Storsie | 0x35DE | 三种属性选择；R30/270度、环10–40、R22按观测时刻+10.4秒预告；实际结束消费 |
| MoussePrincess | 0x360B | SID2840/2841/2842/2958决定后续R40/120度方向；准备完成+2秒，实际Resolve结束清除 |
| Ophioneus | 0x35DC | 左右半圆；圈环与跳跃圈环预测，缺历史不猜形状，实际后续修正并清除 |
| Ruminator | 0x35BD | 记录圈环、顺序/逆序回放；26915/26916与四种无读条后续效果推进，动态队首 |
| Sphatika | 0x3670 | SID2835–2838按状态顺序记方向；两种stance决定正反侧；实际十种Cleave结束推进 |
| Gurangatch | 0x361B | 两次反向/八次左右旋转半圆；当前及下一次均参与规划，实际读条结束推进 |
| Burfurlur | 0x360A | Inhale记方向，五次Sneeze按真实读条结束推进；漏首段不推断，参考角按源取值 |
| Minerva | 0x3609 | Warning选择R6/环6–20并立即跟随目标；Warning结束锁点；实际落点仅补正，实际结束清除 |
| Armstrong | 0x35BE | 27470/27471选择±30度；十次四臂长50全宽7十字；第五效果后反转且延迟3.6秒 |
| Supay | 0x2839 | 17858 R8诱导及个人提示；17856才是真正凝视 |
| TheMudman | 0x281F | 环5–20、R40/60度、R8及R6目标诱导；GravityForce保留Interrupt提示 |
| Petalodus | 0x35FB | MarineMayhem纯Interrupt/Raidwide×3提示；R6分散、R13及R40/45度 |
| LilMurderer | 0x28B4 | R20分散、Tankbuster、R8地面雷；精确脚下雷预测结束+2.6秒R8 Slash，17489效果清除 |
| Nuckelavee | 0x288F | Torpedo开始锁目标并等实际主目标结算；R5分散；Gallop固定落点R8 Spite等待18037效果 |
| Rusalka | 0x2853 | R8、长12全宽4；R30范围吸引30至中心；固定R8 Flood在Finish+3.6秒预告，17369效果清除 |
| Sugaar | 0x2875 | R11、R13/R18总120度；三次±120度旋转，Finish+1.1f首击；动态Boss原点拉25，最小双方hitbox |
| Aegeiros | 0x3671 | 动态R10 Leafstorm及观测+9.6秒R40/180度Rimestorm；无读条R12/120度Canopy；R8/普通R12/120度 |
| Nariphon | 0x2890 | R12/120度、R6诱导；R6恰好8人分摊，起手冻结SID1435禁入名单 |
| Yilan | 0x35BF | Soundstorm Finish+12.1f预测动态R18；实际MiniLight补正/结束；R6及R10/90度；状态强制行进 |
| NarrowRift | 0x35DB | 圈环队列随Boss，任意主读条结束消费队首；27333+4秒预测动态长50全宽8光束，27516清；状态强制行进 |
| SallyTheSweeper | 0x4395 | 记录R10/环10–40/四臂40全宽10偏45度十字；正逆序执行锁点，效果消费；R18落点 |

对应源为 `MechanicRuntime/Upstream/Modules/<资料片>/Hunt/Rank*/<模块>.cs`。40模块共222条读条规则，另有23个无读条效果AID及不依赖27711事件的常驻Canopy。只计实际激活的组件。Orghana的`Raidwide x4`和Petalodus的`Raidwide x3`仅保留源Hint，不自造多次判定时钟；`Interrupt!`不等于已增加自动打断执行。TailDrive依据实际半角60度构造，而非过时的90度注释。Angada和Grassman技能注释中的击退未被源状态机实现，不增加臆测的击退组件；FanAil消失/再现AID也不增加源不存在的第二段。

## 生命周期和消费

首次激活要求敌人存活、可选中并战斗；激活后暂时不可选中不清会话，敌人脱战、死亡、移除或跨区清理。观看玩家死亡或BattleEnded不重置仍在战斗的敌人。机制开关独立控制对应定义及移动owner请求。

范围锁定读条落点和施法朝向，以事件UTC开始时间加实际读条长度和一次NPC补偿0.3秒。对象恢复的FinishAt已含补偿，不重复叠加。事件早于匹配Cast快照时等待首次观测，确认过本次Cast后观察无Cast才结束；取消/打断立即清除对应来源。已结束代保存时间水位，旧事件/旧快照不能替换较新一代或复活旧代。危险区在实际观察结束前保持，不以预测时刻加固定超时清除。

新快照及录像保存原始float `ReplayCast.TotalTime`，变化检测直接比较该字段，避免逐次采样通过开始/结束DateTime重建总时长产生假重启。旧录像无此字段时仅容许1微秒的重建误差，真实40ms修正仍重建。packet姿态补正修正原队列和时钟，不计作一次真实读条完成；对象随后追上已接受的同代packet输入只确认观测，不追加队列或复活已消耗后续。对象输入同时不同于上次观测和当前输入时仍保留Finished→Started语义。普通几何锁姿态，专用动态朝向按各自规则更新。

雷达和世界绘制共用原尺寸预览，导航使用脚本危险区门面应用一次余量；脚本停止仅清自身句柄、预览和移动请求，不改别人场地。通用躲避按Source/OID/规则/机制开关接管。BMR只在支持的已激活模块上休眠导航与绘制，未开战模块及其他野外机制保留原路径，不按整个territory压制。

全队预测排除死亡/缺失队员；单体仅在显式目标为施法者自身时，逐次读取敌人当前目标，否则保留显式目标。按旧MP桥契约持续发布目标/波次诊断和源Hint到脚本详情，没有新增PR治疗策略。

## 方向、击退及自定义时序

- 普通/反向凝视分别使用精确45度/135度禁向半角，联合求共同可行朝向，在命中前0.75秒通过PR面向门面续期。反向凝视要求面向眼睛，保留眼睛颜色反转及玩家背后270度弧；普通保留前方90度弧。窄共同解不额外扩大禁向；严格同XZ时不发布没有实际方向的控制。回放只保留规划和绘制。
- 冲锋起点按源倒数乘法量化，方向和长度取未量化起终点，不加hitbox；仅真实读条输入变化才重建。击退按OID/AID组件分别从玩家当前位置起算，同组件来源按开始顺序串联，重启来源移到尾部。六种免疫严格要求到期晚于activation，免疫不移除独立伤害圈。投影仅绘制连线/落点及源越界提示，不编造源没有的击退AI或技能请求。
- Angada：Butcher开始时同时发布普通和Rip扇形，activation相同；普通结束/取消只移除Butcher，Rip等待同实例8192效果。效果幂等，旧效果不清新一代，效果之后的首个对象观测不复活Rip。
- Baal：两个相反半圆使用不同activation；首段参与规划并显示危险色，次段仅预览。首段CastFinished后次段提升为危险，等待17422/17421效果结束。Huracan两段从创建起均参与规划，只有实际效果出队；WinterRain次段在首段未结算时随Boss更新并量化起点，首段出队后冻结。
- Grassman：首次读条完成结束单次提示；后续提示持续到四个17863后续效果，重复目标效果不多计。保留源始终发布的一条伤害诊断：首次施法前时间未知、机制结束后仍为上次activation，不补造五个伤害时钟。人员覆盖存活且有对象的0–23槽，排除24起NPC；生产采样保留PR本队槽并补普通联盟8–23，ARR取ReplayGroup，按源排除四人小组联盟。
- Maliktender：18020开始即锁存原警告，取消、状态缺失或状态移除不提前清除；脱战/死亡/停用重新建会话。SID1962本身不推断Haste开始。FanAil的Divebomb固定原落点，规划与双视图共用更新后的Boss朝向，27373结束/取消清除。
- Gamma：读条只预警，真实当前玩家SID960才触发PR ACR全停手、禁读条、取消当前施法和MP移动暂停；这是按用户要求补充的执行行为，源原来只给文字。状态事件按角色/来源隔离，接受来源0，旧来源移除不覆盖同帧新来源；大幅旧期限快照不提前确认刷新，100ms确认容差覆盖发布量化。死亡/缺对象清未确认事件，切人、到期、停用及停止撤销两种owner。移动hold不封锁手动输入或PR本体路线。
- Minerva目标丢失保留最后Actor位置，对象销毁后不绑定同ID替身。实际导弹AID不能改变已选择形状，也不能凭空补缺失Warning。Armstrong当前和下一十字均Risky；普通间隔2.1f，第五次先按旧方向推进、再对推进后的队首反向。
- 目标诱导本人只画轮廓、规划避队友；他人避目标圈。分散R原尺寸轮廓、R+1个人导航，使用源量化位置，死目标隐藏但复活恢复；销毁同ID替身不继承旧标记资格。读条补正不反复移除重加不变导航。
- Torpedo只由`NativeActionEffectEvent.MainTargetId`消费已锁目标。该事件来自FFCS `Header.AnimationTargetId`，包含一次完整包的目标集合；不从PR逐受击目标或TargetIndex推断主目标。目标暂时缺对象、pose补正或Boss换目标不改已锁预测。旧录像缺少该输入不能完整结算，保留未知边界。
- Rusalka的吸引范围不作为R30危险圆；拉动结束仅清投影，Flood保留到效果。Sugaar旋转首击Finish+1.1f，后两击由18098/18099真实Finish推进至now+2.7f，两个已显示扇形均Risky；拉动原点跟当前Boss，序列中心仍锁起手地点。多只Sugaar各从玩家位置独立计算，不串联。免疫严格晚于activation，落点提示只查源指定的Flood/已显示旋转扇形。
- Aegeiros圆及锥跟Boss当前位置；Leaf取消不清预测，实际Rimestorm显示时取代预测且其结束才清除。Canopy无需效果事件，任何读条期间隐藏，目标丢失或Boss不可选中时隐藏；真实R12锥仅轮廓，主目标规划避以每个存活队友方向构成的R100锥，其他人规划避真实R12锥。该源组件没有主动PR面向请求。

- Nariphon：读条开始冻结SID1435名单，后续状态变化不改该次资格；人数只算存活且合格队员，圈外人数<8才入圈，圈内<=8可留。本人选任一合格buddy的R3圈，其他人进入R6；这些是带原Activation的圈外禁区，不是可选goal。反向union覆盖整个规划图，多个buddy为OR；真实解算已覆盖旧priority101跟随落点锁、2秒入圈以及0.5/5秒不同deadline。预览单独保留原R6轮廓及资格颜色。当前MP按敌人实例隔离状态，避免上游全局事件广播将多只Boss队列/Shared合并；这属于MP多实例适配，不宣称源事件已按caster隔离。中途启用缺历史名单时仅能按当前快照接管。
- Yilan/NarrowRift共用1958/1959/1960/1961与1257/3629。每个pending段13.2码，按expiry排序、方向累计；active先按6.6乘剩余秒数投影，再接pending，无8秒截断，不依Param二次转向。只有最终落点参与Hint；Yilan始终查Boss当前R18，NarrowRift查玩家局部R30。事件先/后快照均不重复状态；状态Lose、对象移除、停用及Seek重建清理。源没有主动面向或移动hold策略，未增造。
- NarrowRift只显示队首圈/环，activation取当前主读条Finish，无读条时立即生效；未列AID的packet-only读条也接入结束消费。27333建立的光束跟Boss当前朝向/位置，超过预测时刻或27519视觉结束不清。Yilan的预测跨Soundstorm结束保留，actual MiniLight取代并最终清除。
- Sally记录和执行均可追加，显示全队列前两项；未来圈/环仅预览，未来十字仍Risky。倒序按源5.2f−2.6f*i，1/2/4项历史也不改成从0开始；缺历史不猜形状。只有七种真实Effect弹队首，CastFinished不消费；锁执行起手位置/朝向，packet修正未消耗项，重复Effect幂等。

## 离线证据与边界

- `tests/SimpleHuntRegression.cs`：19条规则逐项真实读条补偿、13形状边缘/宽度/角度、预测分类；同OID实例、锁姿态、取消旧快照、跨代旧输入、事件先于对象快照、目标更新、队员死亡/缺失、首次不可选中与机制停用。
- `tests/SimpleHuntControlsRegression.cs`、`SimpleHuntSequenceRegression.cs`、`SimpleHuntComboRegression.cs`：精确几何、两种凝视与窄共同解、击退分组件/同组件串联及重启顺序、六种免疫、实际后续判定、热病事件/快照/来源替换、采样时钟及packet补正。
- `tests/ScriptCompilation/NativeHuntReplaySimpleHunts.cs`、`NativeHuntReplayControls.cs`、`NativeHuntReplaySequences.cs`、`NativeHuntReplayCombos.cs`：实际MasterPlayer.dll回放六组模块；原19规则同时13危险区/32预览（含19个实例敌人标记）；后续批覆盖双形状、预览/风险分层、动态朝向、五连击、状态刷新、反向Seek、PR回放隔离及停止清理。
- `tests/ScriptCompilation/ReplayPartyCaptureRegression.cs`：直接调用生产DLL采样，使用匹配FFCS结构内存验证PR槽0/2、联盟8/16/23、Flags、非法/重复ID、洞位不压缩、ARR分组、小组联盟排除与空服务；不使用原生函数。
- `SimpleHuntStoredRegression`、`SimpleHuntTargetRegression`、`SimpleHuntPullRegression`、`SimpleHuntAegeirosRegression`、`SimpleHuntStackRegression`、`SimpleHuntMarchRegression`和`SimpleHuntExecutionRegression`补状态记忆、方向、导弹、十字、目标身份、分散、拉动和常驻顺劈边界；最新合计48个Hunt模型场景通过。
- 实际DLL新增Misdirection/Stored/Rotations/Missiles/Compressor/Targets/Followups/Pulls/Aegeiros，共18组Hunt回放；上次共用DLL全目录集中回放170条PASS，最新新增机制定向回放通过。`NativeActionEffectRegression`直接验证生产Capture/Router/Recorder/Reader：输入数组复制、主目标、UTC/Local跨流顺序、epoch/回放/owner/异常隔离、录前排除、玩家过滤、正常停止和DisposeRuntime尾包。测试未安装原生hook。
- 本机Lumina探针确认野外territory清单。实际DLL回放夹具禁止读取实时对象或执行GreenMove；没有部署或游戏内执行证据。

狩猎66个来源模块现已离线验证40个，仍余26个，包括强制行进、其他旋转记忆、分摊及目标机制；不能用入口启用数量代替模块完成数量。源组件异常重叠可能追加超过两项队列，原生不复制其空Shape绘制崩溃路径。没有部署或游戏内方向包、动作、移动执行的验收证据。
